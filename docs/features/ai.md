# AI pattern drafting with local verification

The `Masterly.FluentRegex.AI` package asks Claude to draft a regex from a plain-language description,
then **verifies the draft on your machine** before handing it to you. The model is a source of
suggestions, never of truth: a draft only reports `Passed` when every local check passed.

```shell
dotnet add package Masterly.FluentRegex.AI
```

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.AI;

PatternAssistant assistant = new PatternAssistant(
    httpClient,
    new PatternAssistantOptions { ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")! });

PatternDraft draft = await assistant.DraftAsync(
    "a five digit US ZIP code, optionally followed by a dash and four digits",
    shouldMatch: new[] { "12345", "12345-6789" },
    shouldNotMatch: new[] { "1234", "123456", "12345-678" });

Console.WriteLine(draft);
```

## What is verified

For every draft, locally, with the real .NET engine:

1. **Syntax**: `Pattern.FromRegex` accepts it.
2. **Safety**: `Pattern.AnalyzeSafety()` reports no findings (nested quantifiers, overlapping
   alternation, competing adjacent quantifiers).
3. **Positives**: every `shouldMatch` string matches the **entire** string.
4. **Negatives**: no `shouldNotMatch` string matches anywhere (`IsMatch`), which is how the returned
   `Pattern` behaves. This pushes the model to anchor its regex.

When a check fails, the exact failures are sent back and the model tries again, up to `MaxAttempts`
(default 3). Here is a real exchange, recorded with a stubbed transport that returned an unanchored regex
first and an anchored one second:

```text
Your regex failed local verification: \d{5}(-\d{4})?
- These strings must not match but did:
  "123456"
  "12345-678"
Fix every problem above and respond with the corrected JSON object.
```

and the resulting `draft.ToString()`:

```text
Draft passed verification after 2 attempt(s).
Regex: ^[0-9]{5}(-[0-9]{4})?$
Explanation: Five digits, optional +4.
Verification passed.
  Syntax: valid
  Safety: no findings
  Rejected positives: none
  Accepted negatives: none
  Samples: "85542", "40938", "56049-3972", "74274-8868", "01896-7115"
```

`draft.Pattern!.ToFluentCode()` turns it into fluent code:

```csharp
Pattern.With
    .StartOfLine
    .Range('0', '9').Repeat.Times(5)
    .Group(Pattern.With.Literal("-").Range('0', '9').Repeat.Times(4)).Repeat.Optional
    .EndOfLine
```

Syntax errors are fed back as `- Syntax error: ...`, safety findings as
`- ReDoS risk (High NestedQuantifier) at position ... Fix: ...`, and rejected positives as
`- These strings must match the entire string but did not:`.

## When nothing passes

You get the best attempt (fewest failed checks) with `Passed == false` and the full verification, so you
can see exactly what is wrong. Nothing is hidden or rounded up:

```csharp
if (!draft.Passed)
{
    Console.WriteLine(draft.Verification);   // which syntax/safety/example checks failed
    IReadOnlyList<string> rejected = draft.Verification.RejectedPositives;
    IReadOnlyList<string> accepted = draft.Verification.AcceptedNegatives;
}
```

`draft.Pattern` is `null` when no attempt produced a usable regex: every reply was unusable (not JSON, invalid
JSON, no regex, or cut off by `max_tokens`) or had invalid syntax, or the safety analysis could not process it.

## Options

| Option | Default | Notes |
|---|---|---|
| `ApiKey` | required | Sent as `x-api-key`. Never appears in exception messages or `ToString()`. |
| `Model` | `claude-opus-5-5` | Any Messages API model id. |
| `Effort` | `medium` | `output_config.effort`: `low`, `medium`, `high`, `xhigh` or `max`. |
| `MaxAttempts` | 3 | Drafts per call, including retries with feedback. |
| `MaxTokens` | 16000 | Per request. |
| `Timeout` | 5 minutes | Per request; `Timeout.InfiniteTimeSpan` disables it. `HttpClient.Timeout` also applies. |
| `BaseAddress` | `https://api.anthropic.com/` | Point it at a gateway or proxy; requests go to `{BaseAddress}v1/messages`. End it with `/`: otherwise its last path segment is replaced, so `https://gw.example.com/anthropic` sends to `https://gw.example.com/v1/messages`. |
| `UseServerSideFallback` | `true` | Sends `fallbacks: "default"` with the `server-side-fallback-2026-07-01` beta, so a declined request is retried on Anthropic's recommended fallback model. Turn it off for gateways that reject the beta. |

## HTTP, errors and dependency injection

The assistant uses the `HttpClient` you give it and never disposes it, so it works with
`IHttpClientFactory`:

```csharp
HttpClient http = httpClientFactory.CreateClient("anthropic");
PatternAssistant assistant = new PatternAssistant(http, options);
```

Requests go to the Claude Messages API with `anthropic-version: 2026-10-21`, structured JSON output
(`output_config.format` with a JSON schema for `{"regex", "explanation"}`) and no SDK dependency; JSON is
handled by source-generated `System.Text.Json`, so the package is AOT and trim safe.

Failures throw `PatternAssistantException`:

| Property | Meaning |
|---|---|
| `StatusCode` | HTTP status, or `null` for timeouts and network failures. |
| `ErrorType` | API error type, such as `authentication_error` or `rate_limit_error`. |
| `RequestId` | The `request-id` header, for support. |
| `RetryAfter` | From the `Retry-After` header, when present. |
| `IsTransient` | `true` for 408, 409, 429, 5xx, timeouts and network failures; `false` for 401, 400 and refusals. |

The assistant does not retry HTTP errors itself (so a 401 is never retried). Add a resilience handler to
the `HttpClient` if you want automatic retries of transient failures. Cancelling the
`CancellationToken` throws `OperationCanceledException`. A response with `stop_reason: "refusal"` throws a
non-transient `PatternAssistantException`.
