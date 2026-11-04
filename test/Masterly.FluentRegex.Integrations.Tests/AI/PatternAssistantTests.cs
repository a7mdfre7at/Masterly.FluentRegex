using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Masterly.FluentRegex.AI;

namespace Masterly.FluentRegex.Integrations.Tests.AI;

public class PatternAssistantTests
{
    private const string ApiKey = "sk-ant-test-0123456789-SECRET";
    private const string ZipDescription = "a five digit US ZIP code";

    private static readonly string[] ZipPositives = { "12345", "00501" };
    private static readonly string[] ZipNegatives = { "1234", "123456", "12a45" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DraftAsync_FirstDraftPasses_ReturnsVerifiedDraftAfterOneRequest()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft(@"^\d{5}$", "Five digits.");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternDraft draft = await assistant.DraftAsync(ZipDescription, ZipPositives, ZipNegatives, cancellationToken: Ct);

        draft.Passed.ShouldBeTrue();
        draft.Attempts.ShouldBe(1);
        draft.Regex.ShouldBe(@"^\d{5}$");
        draft.Explanation.ShouldBe("Five digits.");
        draft.Pattern!.IsMatch("98765").ShouldBeTrue();
        draft.Verification.IsValidSyntax.ShouldBeTrue();
        draft.Verification.SafetyReport!.Issues.ShouldBeEmpty();
        draft.Verification.Samples.Count.ShouldBe(5);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DraftAsync_Request_UsesMessagesApiContract()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft(@"^\d{5}$");
        PatternAssistant assistant = CreateAssistant(handler);

        await assistant.DraftAsync(ZipDescription, ZipPositives, ZipNegatives, cancellationToken: Ct);

        CapturedRequest request = handler.Requests[0];
        request.Uri.ShouldBe(new Uri("https://api.anthropic.com/v1/messages"));
        request.Headers["x-api-key"].ShouldBe(ApiKey);
        request.Headers["anthropic-version"].ShouldBe("2026-10-21");
        request.Headers["anthropic-beta"].ShouldBe("server-side-fallback-2026-07-01");
        request.Json.GetProperty("model").GetString().ShouldBe("claude-opus-5-5");
        request.Json.GetProperty("fallbacks").GetString().ShouldBe("default");
        request.Json.GetProperty("output_config").GetProperty("effort").GetString().ShouldBe("medium");
        request.Json.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString().ShouldBe("json_schema");
        request.Json.GetProperty("system").GetString()!.ShouldContain("anchor it with ^ and $");
        request.LastUserText.ShouldContain(ZipDescription);
        request.LastUserText.ShouldContain("\"12a45\"");
    }

    [Fact]
    public async Task DraftAsync_FallbackDisabled_OmitsFallbackFieldAndHeader()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft(@"^\d{5}$");
        PatternAssistant assistant = CreateAssistant(handler, options => options.UseServerSideFallback = false);

        await assistant.DraftAsync(ZipDescription, cancellationToken: Ct);

        handler.Requests[0].Headers.ContainsKey("anthropic-beta").ShouldBeFalse();
        handler.Requests[0].Json.TryGetProperty("fallbacks", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task DraftAsync_DraftAcceptsNegative_SendsPreciseFeedbackAndRetries()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueDraft(@"\d{5}")
            .EnqueueDraft(@"^\d{5}$");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternDraft draft = await assistant.DraftAsync(ZipDescription, ZipPositives, ZipNegatives, cancellationToken: Ct);

        draft.Passed.ShouldBeTrue();
        draft.Attempts.ShouldBe(2);
        string feedback = handler.Requests[1].LastUserText;
        feedback.ShouldContain(@"Your regex failed local verification: \d{5}");
        feedback.ShouldContain("These strings must not match but did:");
        feedback.ShouldContain("\"123456\"");
        feedback.ShouldNotContain("must match the entire string but did not");
    }

    [Fact]
    public async Task DraftAsync_Retry_EchoesAssistantTurnVerbatim()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueDraft(@"\d{5}")
            .EnqueueDraft(@"^\d{5}$");
        PatternAssistant assistant = CreateAssistant(handler);

        await assistant.DraftAsync(ZipDescription, ZipPositives, ZipNegatives, cancellationToken: Ct);

        JsonElement messages = handler.Requests[1].Messages;
        messages.GetArrayLength().ShouldBe(3);
        messages[1].GetProperty("role").GetString().ShouldBe("assistant");
        messages[1].GetProperty("content")[0].GetProperty("type").GetString().ShouldBe("thinking");
        messages[1].GetProperty("content")[0].GetProperty("signature").GetString().ShouldBe("sig-test");
        messages[2].GetProperty("role").GetString().ShouldBe("user");
    }

    [Fact]
    public async Task DraftAsync_DraftRejectsPositive_FeedbackListsRejectedStrings()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueDraft(@"^[1-9]\d{4}$")
            .EnqueueDraft(@"^\d{5}$");
        PatternAssistant assistant = CreateAssistant(handler);

        await assistant.DraftAsync(ZipDescription, ZipPositives, ZipNegatives, cancellationToken: Ct);

        string feedback = handler.Requests[1].LastUserText;
        feedback.ShouldContain("These strings must match the entire string but did not:");
        feedback.ShouldContain("\"00501\"");
    }

    [Fact]
    public async Task DraftAsync_UnsafeDraft_FeedbackNamesSafetyFinding()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueDraft(@"^(\d+)+$")
            .EnqueueDraft(@"^\d+$");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternDraft draft = await assistant.DraftAsync("one or more digits", new[] { "123" }, cancellationToken: Ct);

        draft.Passed.ShouldBeTrue();
        string feedback = handler.Requests[1].LastUserText;
        feedback.ShouldContain("ReDoS risk (High NestedQuantifier)");
        feedback.ShouldContain("Fix:");
    }

    [Fact]
    public async Task DraftAsync_InvalidSyntax_FeedbackIncludesSyntaxError()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueDraft("^(abc$")
            .EnqueueDraft("^abc$");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternDraft draft = await assistant.DraftAsync("the word abc", new[] { "abc" }, cancellationToken: Ct);

        draft.Passed.ShouldBeTrue();
        handler.Requests[1].LastUserText.ShouldContain("- Syntax error: The regular expression is invalid");
    }

    [Fact]
    public async Task DraftAsync_ReplyIsNotJson_AsksForJsonAndRetries()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueText("Sure! Here is a regex: ^abc$")
            .EnqueueDraft("^abc$");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternDraft draft = await assistant.DraftAsync("the word abc", new[] { "abc" }, cancellationToken: Ct);

        draft.Passed.ShouldBeTrue();
        draft.Attempts.ShouldBe(2);
        handler.Requests[1].LastUserText.ShouldContain("did not contain a JSON object");
    }

    [Fact]
    public async Task DraftAsync_ReplyIsMalformedJson_ReportsInvalidJson()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueText("{\"regex\": \"^abc$\",}")
            .EnqueueText("{\"explanation\": \"no regex\"}")
            .EnqueueText("{ not json }");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternDraft draft = await assistant.DraftAsync("the word abc", cancellationToken: Ct);

        draft.Passed.ShouldBeFalse();
        draft.Attempts.ShouldBe(3);
        draft.Pattern.ShouldBeNull();
        draft.Regex.ShouldBe(string.Empty);
        draft.Verification.IsValidSyntax.ShouldBeFalse();
        handler.Requests[1].LastUserText.ShouldContain("not valid JSON");
        handler.Requests[2].LastUserText.ShouldContain("no string property \"regex\"");
    }

    [Fact]
    public async Task DraftAsync_TruncatedReply_ReportsTokenLimit()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueText("{\"regex\": \"^ab", stopReason: "max_tokens")
            .EnqueueDraft("^abc$");
        PatternAssistant assistant = CreateAssistant(handler);

        await assistant.DraftAsync("the word abc", cancellationToken: Ct);

        handler.Requests[1].LastUserText.ShouldContain("cut off by the token limit");
    }

    [Fact]
    public async Task DraftAsync_EveryDraftFails_ReturnsBestAttemptWithoutClaimingSuccess()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueDraft("^(abc$")
            .EnqueueDraft(@"\d+", "Best so far.")
            .EnqueueDraft(@"[a-z]+");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternDraft draft = await assistant.DraftAsync(ZipDescription, ZipPositives, ZipNegatives, cancellationToken: Ct);

        draft.Passed.ShouldBeFalse();
        draft.Attempts.ShouldBe(3);
        draft.Regex.ShouldBe(@"\d+");
        draft.Explanation.ShouldBe("Best so far.");
        draft.Verification.AcceptedNegatives.ShouldBe(ZipNegatives);
        draft.ToString().ShouldContain("did NOT pass");
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task DraftAsync_MaxAttemptsOne_DoesNotRetry()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft(@"\d{5}");
        PatternAssistant assistant = CreateAssistant(handler, options => options.MaxAttempts = 1);

        PatternDraft draft = await assistant.DraftAsync(ZipDescription, ZipPositives, ZipNegatives, cancellationToken: Ct);

        draft.Passed.ShouldBeFalse();
        draft.Attempts.ShouldBe(1);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DraftAsync_Unauthorized_ThrowsWithoutRetryOrLeakingKey()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueError(HttpStatusCode.Unauthorized, "authentication_error", $"invalid x-api-key {ApiKey}", r => r.Headers.Add("request-id", "req_123"));
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        exception.ErrorType.ShouldBe("authentication_error");
        exception.RequestId.ShouldBe("req_123");
        exception.IsTransient.ShouldBeFalse();
        exception.Message.ShouldContain("HTTP 401");
        exception.Message.ShouldNotContain(ApiKey);
        exception.ToString().ShouldNotContain(ApiKey);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DraftAsync_RateLimited_ThrowsTransientWithRetryAfter()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueError(HttpStatusCode.TooManyRequests, "rate_limit_error", "Slow down.", r => r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30)));
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        exception.IsTransient.ShouldBeTrue();
        exception.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
        exception.Message.ShouldContain("Slow down.");
    }

    [Fact]
    public async Task DraftAsync_ServerErrorWithHtmlBody_ThrowsTransientWithStatus()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueJson(HttpStatusCode.BadGateway, "<html>bad gateway</html>");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        exception.IsTransient.ShouldBeTrue();
        exception.ErrorType.ShouldBeNull();
        exception.Message.ShouldBe("The Claude API returned HTTP 502.");
    }

    [Fact]
    public async Task DraftAsync_Refusal_ThrowsNonTransient()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueText(string.Empty, stopReason: "refusal");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.Message.ShouldContain("declined");
        exception.IsTransient.ShouldBeFalse();
    }

    [Fact]
    public async Task DraftAsync_ResponseNotJson_ThrowsPatternAssistantException()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueJson(HttpStatusCode.OK, "not json");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.InnerException.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task DraftAsync_CancelledWhileWaiting_ThrowsOperationCanceled()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueHang();
        PatternAssistant assistant = CreateAssistant(handler);
        using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Should.ThrowAsync<OperationCanceledException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task DraftAsync_AlreadyCancelled_SendsNothing()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler();
        PatternAssistant assistant = CreateAssistant(handler);

        await Should.ThrowAsync<OperationCanceledException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: new CancellationToken(canceled: true)));

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DraftAsync_RequestTimesOut_ThrowsTransientTimeout()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueHang();
        PatternAssistant assistant = CreateAssistant(handler, options => options.Timeout = TimeSpan.FromMilliseconds(100));

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.IsTransient.ShouldBeTrue();
        exception.StatusCode.ShouldBeNull();
        exception.Message.ShouldContain("did not respond within");
    }

    [Fact]
    public async Task DraftAsync_HttpClientTimeoutFiresFirst_NamesThatTimeout()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueHang();
        HttpClient client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(100) };
        PatternAssistant assistant = new PatternAssistant(client, new PatternAssistantOptions { ApiKey = ApiKey });

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.IsTransient.ShouldBeTrue();
        exception.Message.ShouldBe("The Claude API did not respond within 00:00:00.1000000 (HttpClient.Timeout).");
    }

    [Fact]
    public async Task DraftAsync_ErrorMessageEndingWithAPeriod_IsNotDoubled()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueError(HttpStatusCode.TooManyRequests, "rate_limit_error", "Number of requests exceeds your rate limit.");
        PatternAssistant assistant = CreateAssistant(handler, options => options.MaxAttempts = 1);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.Message.ShouldEndWith("rate limit.");
        exception.Message.ShouldNotContain("..");
    }

    [Fact]
    public async Task DraftAsync_ShortApiKey_DoesNotRedactInsideOtherWords()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueError(HttpStatusCode.Unauthorized, "authentication_error", "invalid x-api-key k");
        PatternAssistant assistant = CreateAssistant(handler, options => options.ApiKey = "k");

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.Message.ShouldContain("invalid x-api-key <redacted>");
    }

    [Fact]
    public async Task DraftAsync_CustomBaseAddressAndModel_AreUsed()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft("^abc$");
        PatternAssistant assistant = CreateAssistant(handler, options =>
        {
            options.BaseAddress = new Uri("https://gateway.example.com/anthropic/");
            options.Model = "claude-sonnet-5-5";
        });

        await assistant.DraftAsync("abc", cancellationToken: Ct);

        handler.Requests[0].Uri.ShouldBe(new Uri("https://gateway.example.com/anthropic/v1/messages"));
        handler.Requests[0].Json.GetProperty("model").GetString().ShouldBe("claude-sonnet-5-5");
    }

    [Fact]
    public async Task DraftAsync_BlankDescription_ThrowsArgument()
    {
        PatternAssistant assistant = CreateAssistant(new FakeAnthropicHandler());

        await Should.ThrowAsync<ArgumentException>(() => assistant.DraftAsync("  ", cancellationToken: Ct));
    }

    [Fact]
    public async Task DraftAsync_NullExample_ThrowsArgument()
    {
        PatternAssistant assistant = CreateAssistant(new FakeAnthropicHandler());

        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(
            () => assistant.DraftAsync("abc", shouldNotMatch: new[] { "x", null! }, cancellationToken: Ct));

        exception.ParamName.ShouldBe("shouldNotMatch");
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => new PatternAssistant(null!, new PatternAssistantOptions { ApiKey = ApiKey }));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNull()
    {
        using HttpClient client = new HttpClient(new FakeAnthropicHandler());

        Should.Throw<ArgumentNullException>(() => new PatternAssistant(client, null!));
    }

    [Fact]
    public void Constructor_MissingApiKey_ThrowsArgument()
    {
        using HttpClient client = new HttpClient(new FakeAnthropicHandler());

        ArgumentException exception = Should.Throw<ArgumentException>(() => new PatternAssistant(client, new PatternAssistantOptions()));

        exception.Message.ShouldContain("API key is required");
    }

    [Fact]
    public void Constructor_ApiKeyWithNewline_ThrowsWithoutEchoingKey()
    {
        using HttpClient client = new HttpClient(new FakeAnthropicHandler());
        PatternAssistantOptions options = new PatternAssistantOptions { ApiKey = "sk-ant-secret\nInjected: header" };

        ArgumentException exception = Should.Throw<ArgumentException>(() => new PatternAssistant(client, options));

        exception.Message.ShouldNotContain("sk-ant-secret");
    }

    [Fact]
    public void Constructor_MaxAttemptsBelowOne_ThrowsArgumentOutOfRange()
    {
        using HttpClient client = new HttpClient(new FakeAnthropicHandler());
        PatternAssistantOptions options = new PatternAssistantOptions { ApiKey = ApiKey, MaxAttempts = 0 };

        Should.Throw<ArgumentOutOfRangeException>(() => new PatternAssistant(client, options));
    }

    [Fact]
    public void Constructor_NonPositiveTimeout_ThrowsArgumentOutOfRange()
    {
        using HttpClient client = new HttpClient(new FakeAnthropicHandler());
        PatternAssistantOptions options = new PatternAssistantOptions { ApiKey = ApiKey, Timeout = TimeSpan.Zero };

        Should.Throw<ArgumentOutOfRangeException>(() => new PatternAssistant(client, options));
    }

    [Fact]
    public void Constructor_RelativeBaseAddress_ThrowsArgument()
    {
        using HttpClient client = new HttpClient(new FakeAnthropicHandler());
        PatternAssistantOptions options = new PatternAssistantOptions { ApiKey = ApiKey, BaseAddress = new Uri("v1/", UriKind.Relative) };

        Should.Throw<ArgumentException>(() => new PatternAssistant(client, options));
    }

    [Fact]
    public void Options_Defaults_MatchDocumentation()
    {
        PatternAssistantOptions options = new PatternAssistantOptions();

        options.Model.ShouldBe("claude-opus-5-5");
        options.MaxAttempts.ShouldBe(3);
        options.BaseAddress.ShouldBe(new Uri("https://api.anthropic.com/"));
        options.Effort.ShouldBe("medium");
        options.UseServerSideFallback.ShouldBeTrue();
    }

    [Fact]
    public void Options_ToString_RedactsApiKey()
    {
        PatternAssistantOptions options = new PatternAssistantOptions { ApiKey = ApiKey };

        string text = options.ToString();

        text.ShouldNotContain(ApiKey);
        text.ShouldContain("ApiKey = <redacted>");
    }

    [Fact]
    public async Task Verification_ToString_SummarizesEveryCheck()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft(@"\d{5}");
        PatternAssistant assistant = CreateAssistant(handler, options => options.MaxAttempts = 1);

        PatternDraft draft = await assistant.DraftAsync(ZipDescription, ZipPositives, ZipNegatives, cancellationToken: Ct);

        string summary = draft.Verification.ToString();
        summary.ShouldContain("Verification FAILED.");
        summary.ShouldContain("Syntax: valid");
        summary.ShouldContain("Safety: no findings");
        summary.ShouldContain("Accepted negatives: \"123456\"");
        summary.ShouldContain("Rejected positives: none");
    }

    [Fact]
    public async Task DraftAsync_CustomEffortAndMaxTokens_AreSentInRequest()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft("^abc$");
        PatternAssistant assistant = CreateAssistant(handler, options =>
        {
            options.Effort = "high";
            options.MaxTokens = 2048;
        });

        await assistant.DraftAsync("abc", cancellationToken: Ct);

        JsonElement json = handler.Requests[0].Json;
        json.GetProperty("max_tokens").GetInt32().ShouldBe(2048);
        json.GetProperty("output_config").GetProperty("effort").GetString().ShouldBe("high");
        json.GetProperty("output_config").GetProperty("format").GetProperty("schema").GetProperty("required")[0].GetString().ShouldBe("regex");
    }

    [Fact]
    public async Task DraftAsync_InfiniteTimeout_IsAcceptedAndRequestSucceeds()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft("^abc$");
        PatternAssistant assistant = CreateAssistant(handler, options => options.Timeout = Timeout.InfiniteTimeSpan);

        PatternDraft draft = await assistant.DraftAsync("abc", new[] { "abc" }, cancellationToken: Ct);

        draft.Passed.ShouldBeTrue();
    }

    [Fact]
    public async Task DraftAsync_NoExamples_InitialPromptIsDescriptionOnly()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft("^abc$");
        PatternAssistant assistant = CreateAssistant(handler);

        await assistant.DraftAsync("the word abc", cancellationToken: Ct);

        handler.Requests[0].LastUserText.ShouldBe("Write a .NET regular expression for: the word abc");
    }

    [Fact]
    public async Task DraftAsync_MoreThanTwentyExamples_PromptAndFeedbackListTwentyAndCountTheRest()
    {
        string[] positives = Enumerable.Range(0, 25).Select(index => $"x{index}").ToArray();
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueDraft("^y$")
            .EnqueueDraft(@"^x\d+$");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternDraft draft = await assistant.DraftAsync("x then a number", positives, cancellationToken: Ct);

        draft.Passed.ShouldBeTrue();
        string initial = handler.Requests[0].LastUserText;
        initial.ShouldContain("Strings that must match (the entire string):");
        initial.ShouldContain("\"x19\"");
        initial.ShouldNotContain("\"x20\"");
        initial.ShouldEndWith("(and 5 more)");
        initial.ShouldNotContain("Strings that must not match:");
        string feedback = handler.Requests[1].LastUserText;
        feedback.ShouldContain("\"x19\"");
        feedback.ShouldNotContain("\"x20\"");
        feedback.ShouldContain("  (and 5 more)");
    }

    [Fact]
    public async Task DraftAsync_ReplyWithoutExplanation_UsesEmptyExplanation()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueText("{\"regex\": \"^abc$\"}");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternDraft draft = await assistant.DraftAsync("abc", new[] { "abc" }, cancellationToken: Ct);

        draft.Passed.ShouldBeTrue();
        draft.Explanation.ShouldBe(string.Empty);
        draft.ToString().ShouldNotContain("Explanation:");
    }

    [Fact]
    public async Task DraftAsync_ReplyWrappedInProseAndFences_ExtractsOutermostObject()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueText("Here you go:\n```json\n{\"regex\": \"^a{2}$\", \"explanation\": \"Two a.\"}\n```\nDone.");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternDraft draft = await assistant.DraftAsync("two a", new[] { "aa" }, cancellationToken: Ct);

        draft.Passed.ShouldBeTrue();
        draft.Regex.ShouldBe("^a{2}$");
        draft.Explanation.ShouldBe("Two a.");
    }

    [Fact]
    public async Task DraftAsync_ClosingBraceBeforeOpeningBrace_ReportsNoJsonObject()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueText("} nothing here {")
            .EnqueueDraft("^abc$");
        PatternAssistant assistant = CreateAssistant(handler);

        await assistant.DraftAsync("abc", cancellationToken: Ct);

        handler.Requests[1].LastUserText.ShouldBe(
            "Your reply could not be used: it did not contain a JSON object. Respond with only a JSON object with string properties \"regex\" and \"explanation\".");
    }

    [Fact]
    public async Task DraftAsync_TextSplitAcrossBlocks_ConcatenatesTextBlocksAndIgnoresOthers()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueContent(
            "end_turn",
            "not an object",
            new { kind = "no type property" },
            new { type = "tool_use", text = "{\"regex\": \"ignored\"}" },
            new { type = "text", text = 42 },
            new { type = "text", text = "{\"regex\": \"^ab" },
            new { type = "text", text = "c$\", \"explanation\": \"Joined.\"}" });
        PatternAssistant assistant = CreateAssistant(handler);

        PatternDraft draft = await assistant.DraftAsync("abc", new[] { "abc" }, cancellationToken: Ct);

        draft.Passed.ShouldBeTrue();
        draft.Regex.ShouldBe("^abc$");
        draft.Explanation.ShouldBe("Joined.");
    }

    [Fact]
    public async Task DraftAsync_WholeMatchWrapperInvalid_FallsBackToSpanChecks()
    {
        // A trailing (?x) comment swallows the closing parenthesis of \A(?:...)\z, so verification checks match spans instead.
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft("abc(?x)#");
        PatternAssistant assistant = CreateAssistant(handler, options => options.MaxAttempts = 1);

        PatternDraft draft = await assistant.DraftAsync("abc", new[] { "abc", "xabc", "abcd" }, new[] { "xyz" }, Ct);

        draft.Verification.IsValidSyntax.ShouldBeTrue();
        draft.Verification.RejectedPositives.ShouldBe(new[] { "xabc", "abcd" });
        draft.Verification.AcceptedNegatives.ShouldBeEmpty();
        draft.Passed.ShouldBeFalse();
    }

    [Fact]
    public async Task DraftAsync_ExampleTimesOut_CountsAsRejectedPositiveAndAcceptedNegative()
    {
        // 40 'a's then '!' makes ^(a+)+$ backtrack far beyond the verifier's one-second match timeout.
        string catastrophic = $"{new string('a', 40)}!";
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft("^(a+)+$");
        PatternAssistant assistant = CreateAssistant(handler, options => options.MaxAttempts = 1);

        PatternDraft draft = await assistant.DraftAsync("letters a", new[] { catastrophic }, new[] { catastrophic }, Ct);

        draft.Passed.ShouldBeFalse();
        draft.Verification.RejectedPositives.ShouldBe(new[] { catastrophic });
        draft.Verification.AcceptedNegatives.ShouldBe(new[] { catastrophic });
    }

    [Fact]
    public async Task DraftAsync_SampleGenerationThrows_StillVerifiesExamples()
    {
        // GenerateMatching throws InvalidOperationException for a surrogate-only class such as \p{Cs}.
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft(@"^\p{Cs}$");
        PatternAssistant assistant = CreateAssistant(handler, options => options.MaxAttempts = 1);

        PatternDraft draft = await assistant.DraftAsync("one surrogate", new[] { "\uD800" }, new[] { "a" }, Ct);

        draft.Passed.ShouldBeTrue();
        draft.Verification.IsValidSyntax.ShouldBeTrue();
        draft.Verification.Samples.ShouldAllBe(sample => draft.Pattern!.IsMatch(sample));
    }

    [Fact]
    public async Task DraftAsync_SafetyAnalysisThrows_ReturnsFailedDraftInsteadOfThrowing()
    {
        // AnalyzeSafety throws InvalidOperationException when the ReDoS pump set holds only surrogates.
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft(@"^\p{Cs}*\p{Cs}*$");
        PatternAssistant assistant = CreateAssistant(handler, options => options.MaxAttempts = 1);

        PatternDraft draft = await assistant.DraftAsync("surrogates", new[] { "\uD800" }, cancellationToken: Ct);

        draft.Passed.ShouldBeFalse();
        draft.Attempts.ShouldBe(1);
        draft.Regex.ShouldBe(@"^\p{Cs}*\p{Cs}*$");
    }

    [Fact]
    public async Task DraftAsync_SyntaxErrorAfterValidDraft_KeepsEarlierBetterDraft()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueDraft(@"\d{5}", "Close.")
            .EnqueueDraft("^(abc$");
        PatternAssistant assistant = CreateAssistant(handler, options => options.MaxAttempts = 2);

        PatternDraft draft = await assistant.DraftAsync(ZipDescription, ZipPositives, ZipNegatives, Ct);

        draft.Regex.ShouldBe(@"\d{5}");
        draft.Explanation.ShouldBe("Close.");
        draft.Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task DraftAsync_EqualFailureScores_KeepsFirstDraft()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueDraft("^a$", "First.")
            .EnqueueDraft("^b$", "Second.");
        PatternAssistant assistant = CreateAssistant(handler, options => options.MaxAttempts = 2);

        PatternDraft draft = await assistant.DraftAsync("the letter c", new[] { "c" }, cancellationToken: Ct);

        draft.Regex.ShouldBe("^a$");
        draft.Explanation.ShouldBe("First.");
    }

    [Fact]
    public async Task DraftAsync_NullExampleInShouldMatch_ThrowsArgumentNamingShouldMatch()
    {
        PatternAssistant assistant = CreateAssistant(new FakeAnthropicHandler());

        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(
            () => assistant.DraftAsync("abc", shouldMatch: new string[] { "abc", null! }, cancellationToken: Ct));

        exception.ParamName.ShouldBe("shouldMatch");
        exception.Message.ShouldStartWith("Examples cannot contain null.");
    }

    [Fact]
    public async Task DraftAsync_NullDescription_ThrowsArgumentNull()
    {
        PatternAssistant assistant = CreateAssistant(new FakeAnthropicHandler());

        ArgumentNullException exception = await Should.ThrowAsync<ArgumentNullException>(() => assistant.DraftAsync(null!, cancellationToken: Ct));

        exception.ParamName.ShouldBe("description");
    }

    [Fact]
    public async Task DraftAsync_EmptyDescription_ThrowsArgumentNamingDescription()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler();
        PatternAssistant assistant = CreateAssistant(handler);

        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(() => assistant.DraftAsync(string.Empty, cancellationToken: Ct));

        exception.ParamName.ShouldBe("description");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DraftAsync_NetworkFailure_ThrowsTransientWithRedactedMessage()
    {
        HttpRequestException failure = new HttpRequestException($"Connection refused while sending {ApiKey}");
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueFailure(failure);
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.Message.ShouldBe("Could not reach the Claude API: Connection refused while sending <redacted>");
        exception.IsTransient.ShouldBeTrue();
        exception.StatusCode.ShouldBeNull();
        exception.ErrorType.ShouldBeNull();
        handler.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("{\"stop_reason\": \"end_turn\"}")]
    [InlineData("{\"stop_reason\": \"end_turn\", \"content\": \"text\"}")]
    [InlineData("null")]
    public async Task DraftAsync_ResponseWithoutContentArray_ThrowsPermanent(string body)
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueJson(HttpStatusCode.OK, body);
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.Message.ShouldBe("The Claude API response has no content array.");
        exception.IsTransient.ShouldBeFalse();
    }

    [Fact]
    public async Task DraftAsync_ResponseNotJson_HasPermanentFailureMessage()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueJson(HttpStatusCode.OK, "not json");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.Message.ShouldBe("The Claude API returned a response that is not valid JSON.");
        exception.IsTransient.ShouldBeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.Conflict, true)]
    [InlineData((HttpStatusCode)499, false)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData((HttpStatusCode)529, true)]
    public async Task DraftAsync_HttpError_ClassifiesTransientByStatus(HttpStatusCode status, bool expectedTransient)
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueError(status, "some_error", "Something failed");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.StatusCode.ShouldBe(status);
        exception.IsTransient.ShouldBe(expectedTransient);
        exception.Message.ShouldBe($"The Claude API returned HTTP {(int)status} some_error: Something failed.");
        exception.RequestId.ShouldBeNull();
        exception.RetryAfter.ShouldBeNull();
    }

    [Fact]
    public async Task DraftAsync_ErrorWithEmptyTypeAndMessage_ReportsStatusAndRequestIdOnly()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueError(HttpStatusCode.BadRequest, string.Empty, string.Empty, r => r.Headers.Add("request-id", new[] { "req_a", "req_b" }));
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.Message.ShouldBe("The Claude API returned HTTP 400 (request-id req_a,req_b).");
        exception.RequestId.ShouldBe("req_a,req_b");
        exception.ErrorType.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"type\": \"error\"}")]
    public async Task DraftAsync_ErrorBodyWithoutErrorObject_ReportsStatusOnly(string body)
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueJson(HttpStatusCode.ServiceUnavailable, body);
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.Message.ShouldBe("The Claude API returned HTTP 503.");
        exception.ErrorType.ShouldBeNull();
        exception.IsTransient.ShouldBeTrue();
    }

    [Fact]
    public async Task DraftAsync_LongErrorMessage_IsTruncatedTo500Characters()
    {
        string longMessage = new string('x', 600);
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueError(HttpStatusCode.BadRequest, "invalid_request_error", longMessage);
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.Message.ShouldBe($"The Claude API returned HTTP 400 invalid_request_error: {new string('x', 500)}...");
    }

    [Fact]
    public async Task DraftAsync_RetryAfterDateInPast_ReportsZeroDelay()
    {
        DateTimeOffset past = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueError(HttpStatusCode.TooManyRequests, "rate_limit_error", "Slow down.", r => r.Headers.RetryAfter = new RetryConditionHeaderValue(past));
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.RetryAfter.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task DraftAsync_RetryAfterDateInFuture_ReportsRemainingDelay()
    {
        DateTimeOffset future = DateTimeOffset.UtcNow.AddHours(1);
        FakeAnthropicHandler handler = new FakeAnthropicHandler()
            .EnqueueError(HttpStatusCode.ServiceUnavailable, "overloaded_error", "Busy.", r => r.Headers.RetryAfter = new RetryConditionHeaderValue(future));
        PatternAssistant assistant = CreateAssistant(handler);

        PatternAssistantException exception = await Should.ThrowAsync<PatternAssistantException>(
            () => assistant.DraftAsync(ZipDescription, cancellationToken: Ct));

        exception.RetryAfter!.Value.ShouldBeInRange(TimeSpan.FromMinutes(55), TimeSpan.FromHours(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankModel_ThrowsArgument(string model)
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => CreateAssistant(new FakeAnthropicHandler(), options => options.Model = model));

        exception.ParamName.ShouldBe("options");
        exception.Message.ShouldStartWith("A model id is required.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankEffort_ThrowsArgument(string effort)
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => CreateAssistant(new FakeAnthropicHandler(), options => options.Effort = effort));

        exception.ParamName.ShouldBe("options");
        exception.Message.ShouldStartWith("An effort level is required.");
    }

    [Fact]
    public void Constructor_NullBaseAddress_ThrowsArgument()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => CreateAssistant(new FakeAnthropicHandler(), options => options.BaseAddress = null!));

        exception.Message.ShouldStartWith("BaseAddress must be an absolute URI.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_MaxTokensBelowOne_ThrowsArgumentOutOfRange(int maxTokens)
    {
        ArgumentOutOfRangeException exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateAssistant(new FakeAnthropicHandler(), options => options.MaxTokens = maxTokens));

        exception.ParamName.ShouldBe("options");
        exception.ActualValue.ShouldBe(maxTokens);
        exception.Message.ShouldStartWith("MaxTokens must be at least 1.");
    }

    [Fact]
    public void Constructor_NegativeMaxAttempts_ReportsActualValue()
    {
        ArgumentOutOfRangeException exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateAssistant(new FakeAnthropicHandler(), options => options.MaxAttempts = -2));

        exception.ActualValue.ShouldBe(-2);
        exception.Message.ShouldStartWith("MaxAttempts must be at least 1.");
    }

    [Fact]
    public void Constructor_NegativeTimeout_ThrowsArgumentOutOfRange()
    {
        ArgumentOutOfRangeException exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateAssistant(new FakeAnthropicHandler(), options => options.Timeout = TimeSpan.FromSeconds(-5)));

        exception.ActualValue.ShouldBe(TimeSpan.FromSeconds(-5));
        exception.Message.ShouldStartWith("Timeout must be positive or Timeout.InfiniteTimeSpan.");
    }

    [Theory]
    [InlineData("   ", "An API key is required")]
    [InlineData("sk-ant key", "whitespace or control characters")]
    [InlineData("sk-ant\tkey", "whitespace or control characters")]
    [InlineData("sk-ant\u0001key", "whitespace or control characters")]
    [InlineData("sk-ant\u00A0key", "whitespace or control characters")]
    public void Constructor_InvalidApiKey_ThrowsWithoutEchoingKey(string apiKey, string expectedMessage)
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => CreateAssistant(new FakeAnthropicHandler(), options => options.ApiKey = apiKey));

        exception.ParamName.ShouldBe("options");
        exception.Message.ShouldContain(expectedMessage);
        exception.Message.ShouldNotContain("sk-ant");
    }

    [Fact]
    public async Task Constructor_OptionsChangedAfterConstruction_HasNoEffect()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft("^abc$");
        PatternAssistantOptions options = new PatternAssistantOptions { ApiKey = ApiKey };
        PatternAssistant assistant = new PatternAssistant(new HttpClient(handler), options);
        options.Model = "changed-model";
        options.ApiKey = "changed-key";

        await assistant.DraftAsync("abc", cancellationToken: Ct);

        handler.Requests[0].Json.GetProperty("model").GetString().ShouldBe("claude-opus-5-5");
        handler.Requests[0].Headers["x-api-key"].ShouldBe(ApiKey);
    }

    [Fact]
    public void Options_Defaults_IncludeTimeoutAndMaxTokens()
    {
        PatternAssistantOptions options = new PatternAssistantOptions();

        options.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
        options.MaxTokens.ShouldBe(16000);
        options.ApiKey.ShouldBe(string.Empty);
        PatternAssistantOptions.DefaultModel.ShouldBe("claude-opus-5-5");
        PatternAssistantOptions.DefaultEffort.ShouldBe("medium");
    }

    [Fact]
    public void Options_ToStringWithoutKey_ListsSettingsAndNotSet()
    {
        PatternAssistantOptions options = new PatternAssistantOptions();

        string text = options.ToString();

        text.ShouldBe("Model = claude-opus-5-5, MaxAttempts = 3, BaseAddress = https://api.anthropic.com/, Timeout = 00:05:00, MaxTokens = 16000, Effort = medium, ApiKey = <not set>");
    }

    [Fact]
    public void Exception_DefaultConstructor_HasDefaultMessageAndNoDetails()
    {
        PatternAssistantException exception = new PatternAssistantException();

        exception.Message.ShouldBe("The pattern assistant request failed.");
        exception.InnerException.ShouldBeNull();
        exception.StatusCode.ShouldBeNull();
        exception.ErrorType.ShouldBeNull();
        exception.RequestId.ShouldBeNull();
        exception.RetryAfter.ShouldBeNull();
        exception.IsTransient.ShouldBeFalse();
    }

    [Fact]
    public void Exception_MessageConstructor_KeepsMessage()
    {
        PatternAssistantException exception = new PatternAssistantException("Custom failure.");

        exception.Message.ShouldBe("Custom failure.");
        exception.InnerException.ShouldBeNull();
        exception.IsTransient.ShouldBeFalse();
    }

    [Fact]
    public void Exception_InnerExceptionConstructor_KeepsMessageAndInner()
    {
        InvalidOperationException inner = new InvalidOperationException("Root cause.");

        PatternAssistantException exception = new PatternAssistantException("Wrapped failure.", inner);

        exception.Message.ShouldBe("Wrapped failure.");
        exception.InnerException.ShouldBeSameAs(inner);
        exception.StatusCode.ShouldBeNull();
    }

    [Fact]
    public async Task Draft_ToStringWhenPassed_ReportsSuccessRegexAndExplanation()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft("^abc$", "The word abc.");
        PatternAssistant assistant = CreateAssistant(handler);

        PatternDraft draft = await assistant.DraftAsync("abc", new[] { "abc" }, cancellationToken: Ct);

        string text = draft.ToString();
        text.ShouldStartWith($"Draft passed verification after 1 attempt(s).{Environment.NewLine}Regex: ^abc${Environment.NewLine}Explanation: The word abc.{Environment.NewLine}Verification passed.");
        text.ShouldContain("Samples: \"abc\"");
    }

    [Fact]
    public async Task Verification_InvalidSyntax_ReportsSyntaxAndSkipsSafety()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft("^(abc$");
        PatternAssistant assistant = CreateAssistant(handler, options => options.MaxAttempts = 1);

        PatternDraft draft = await assistant.DraftAsync("abc", new[] { "abc" }, cancellationToken: Ct);

        PatternVerification verification = draft.Verification;
        verification.SafetyReport.ShouldBeNull();
        verification.HasSafetyFindings.ShouldBeFalse();
        verification.Samples.ShouldBeEmpty();
        verification.RejectedPositives.ShouldBeEmpty();
        string summary = verification.ToString();
        summary.ShouldContain("Syntax: invalid (The regular expression is invalid");
        summary.ShouldContain("Safety: not analyzed");
        summary.ShouldContain("Samples: none");
    }

    [Fact]
    public async Task Verification_UnsafeDraft_ListsFindingsAndFails()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft(@"^(\d+)+$");
        PatternAssistant assistant = CreateAssistant(handler, options => options.MaxAttempts = 1);

        PatternDraft draft = await assistant.DraftAsync("digits", new[] { "123" }, cancellationToken: Ct);

        PatternVerification verification = draft.Verification;
        verification.HasSafetyFindings.ShouldBeTrue();
        verification.IsValidSyntax.ShouldBeTrue();
        verification.RejectedPositives.ShouldBeEmpty();
        verification.Passed.ShouldBeFalse();
        string summary = verification.ToString();
        // HasSafetyFindings is true only when SafetyReport is non-null, so the null-forgiving operator is safe here.
        SafetyReport report = verification.SafetyReport!;
        summary.ShouldContain($"Safety: {report.Issues.Count} finding(s), risk High");
        summary.ShouldContain($"    {report.Issues[0]}");
    }

    [Fact]
    public async Task Verification_MoreThanFiveFailures_SummaryShowsFiveAndCountsTheRest()
    {
        string[] negatives = { "a1", "a2", "a3", "a4", "a5", "a6", "a7" };
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft("a");
        PatternAssistant assistant = CreateAssistant(handler, options => options.MaxAttempts = 1);

        PatternDraft draft = await assistant.DraftAsync("no a", shouldNotMatch: negatives, cancellationToken: Ct);

        draft.Verification.AcceptedNegatives.ShouldBe(negatives);
        draft.Verification.ToString().ShouldContain("Accepted negatives: \"a1\", \"a2\", \"a3\", \"a4\", \"a5\" (+2 more)");
    }

    private static PatternAssistant CreateAssistant(FakeAnthropicHandler handler, Action<PatternAssistantOptions>? configure = null)
    {
        PatternAssistantOptions options = new PatternAssistantOptions { ApiKey = ApiKey };
        configure?.Invoke(options);
        return new PatternAssistant(new HttpClient(handler), options);
    }
}
