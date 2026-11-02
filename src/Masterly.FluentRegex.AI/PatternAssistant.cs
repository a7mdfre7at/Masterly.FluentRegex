using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.FluentRegex.AI;

/// <summary>
/// Drafts patterns from plain-language descriptions with Claude and verifies every draft locally.
/// </summary>
/// <remarks>
/// Each draft is checked on this machine: it must be valid .NET syntax, have no ReDoS findings from
/// <see cref="Pattern.AnalyzeSafety"/>, match every positive example in full and match no negative
/// example. Failures are sent back to the model as precise feedback, up to
/// <see cref="PatternAssistantOptions.MaxAttempts"/> drafts. When no draft passes, the best attempt is
/// returned with <see cref="PatternDraft.Passed"/> set to false; a draft is never reported as passing
/// unless it did.
/// <para>
/// The instance does not own the <see cref="HttpClient"/>, so it works with <c>IHttpClientFactory</c>.
/// It does not retry HTTP failures itself; add a resilience handler to the client for that, and use
/// <see cref="PatternAssistantException.IsTransient"/> to decide.
/// </para>
/// </remarks>
/// <example>
/// PatternAssistant assistant = new PatternAssistant(httpClient, new PatternAssistantOptions { ApiKey = key });
/// PatternDraft draft = await assistant.DraftAsync(
///     "a UK postcode such as SW1A 1AA",
///     shouldMatch: new[] { "SW1A 1AA", "M1 1AE" },
///     shouldNotMatch: new[] { "SW1A1AA!", "12345" });
/// if (draft.Passed) { Pattern postcode = draft.Pattern!; }
/// </example>
public sealed class PatternAssistant
{
    private const string UserRole = "user";
    private const string AssistantRole = "assistant";

    private readonly MessagesClient _client;
    private readonly int _maxAttempts;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="httpClient">The HTTP client to send requests with. It is not disposed by this instance.</param>
    /// <param name="options">The API key, model and limits. Values are copied, so later changes have no effect.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">A required option is missing or invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeric option or the timeout is out of range.</exception>
    public PatternAssistant(HttpClient httpClient, PatternAssistantOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        OptionsValidator.Validate(options);
        _client = new MessagesClient(httpClient, options);
        _maxAttempts = options.MaxAttempts;
    }

    /// <summary>
    /// Asks the model for a regex matching <paramref name="description"/> and verifies it against the examples.
    /// </summary>
    /// <param name="description">What the pattern should accept, in plain language.</param>
    /// <param name="shouldMatch">Strings the pattern must match in full.</param>
    /// <param name="shouldNotMatch">Strings the pattern must not match anywhere.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The first draft that passes every check, or the best failed draft.</returns>
    /// <exception cref="ArgumentException"><paramref name="description"/> is empty, or an example is null.</exception>
    /// <exception cref="PatternAssistantException">The API returned an error, timed out, was unreachable or declined.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<PatternDraft> DraftAsync(
        string description,
        IEnumerable<string>? shouldMatch = null,
        IEnumerable<string>? shouldNotMatch = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        IReadOnlyList<string> positives = Materialize(shouldMatch, nameof(shouldMatch));
        IReadOnlyList<string> negatives = Materialize(shouldNotMatch, nameof(shouldNotMatch));
        cancellationToken.ThrowIfCancellationRequested();

        PatternVerifier verifier = new PatternVerifier(positives, negatives);
        List<MessageParam> conversation = new List<MessageParam> { Turn(UserRole, Prompts.Initial(description, positives, negatives)) };
        PatternDraft? best = null;

        for (int attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            ModelReply reply = await _client.SendAsync(conversation, cancellationToken).ConfigureAwait(false);
            conversation.Add(new MessageParam { Role = AssistantRole, Content = reply.Content });

            Attempt current = Evaluate(reply, verifier);
            best = Better(best, current.ToDraft(attempt));
            if (current.Verification.Passed)
            {
                return current.ToDraft(attempt);
            }

            conversation.Add(Turn(UserRole, current.Feedback));
        }

        // MaxAttempts is validated to be at least 1, so the loop ran and 'best' is set.
        return WithAttempts(best!, _maxAttempts);
    }

    private static Attempt Evaluate(ModelReply reply, PatternVerifier verifier)
    {
        string? problem = ReplyParser.TryParse(reply, out DraftPayload payload);
        if (problem != null)
        {
            return new Attempt(null, string.Empty, string.Empty, PatternVerifier.Unusable(problem), Prompts.InvalidReply(problem));
        }

        string regex = payload.Regex!; // TryParse guarantees a non-null regex when it reports no problem.
        string explanation = payload.Explanation ?? string.Empty;
        VerifiedDraft verified = verifier.Verify(regex);
        return new Attempt(verified.Pattern, regex, explanation, verified.Verification, Prompts.Feedback(regex, verified.Verification));
    }

    private static PatternDraft Better(PatternDraft? best, PatternDraft candidate) =>
        best == null || candidate.Verification.FailureScore < best.Verification.FailureScore ? candidate : best;

    private static PatternDraft WithAttempts(PatternDraft draft, int attempts) =>
        new PatternDraft(draft.Pattern, draft.Regex, draft.Explanation, attempts, draft.Verification);

    private static MessageParam Turn(string role, string text) => new MessageParam
    {
        Role = role,
        Content = JsonSerializer.SerializeToElement(text, AssistantJsonContext.Default.String),
    };

    private static IReadOnlyList<string> Materialize(IEnumerable<string>? values, string parameterName)
    {
        if (values == null)
        {
            return Array.Empty<string>();
        }

        List<string> list = new List<string>();
        foreach (string? value in values)
        {
            list.Add(value ?? throw new ArgumentException("Examples cannot contain null.", parameterName));
        }

        return list;
    }

    /// <summary>One evaluated draft and the feedback to send if it failed.</summary>
    private readonly struct Attempt
    {
        public Attempt(Pattern? pattern, string regex, string explanation, PatternVerification verification, string feedback)
        {
            Pattern = pattern;
            Regex = regex;
            Explanation = explanation;
            Verification = verification;
            Feedback = feedback;
        }

        public Pattern? Pattern { get; }

        public string Regex { get; }

        public string Explanation { get; }

        public PatternVerification Verification { get; }

        public string Feedback { get; }

        public PatternDraft ToDraft(int attempts) => new PatternDraft(Pattern, Regex, Explanation, attempts, Verification);
    }
}
