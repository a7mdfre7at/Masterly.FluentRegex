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

    private static PatternAssistant CreateAssistant(FakeAnthropicHandler handler, Action<PatternAssistantOptions>? configure = null)
    {
        PatternAssistantOptions options = new PatternAssistantOptions { ApiKey = ApiKey };
        configure?.Invoke(options);
        return new PatternAssistant(new HttpClient(handler), options);
    }
}
