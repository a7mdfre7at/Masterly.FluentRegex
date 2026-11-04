---
id: ai-drafting
title: "AI Drafting with Claude"
description: "Draft a pattern from a plain-language description with Claude, then verify it on your machine for syntax, ReDoS safety and your own examples."
---

The `Masterly.FluentRegex.AI` package asks Claude to write a regex from a plain-language description, such as "a UK postcode" or "an ISO date". Every draft is then checked **on your machine** before you see it: it must be valid .NET syntax, have no ReDoS findings, match every example you said it must match, and match none of the examples you said it must not. When a check fails, the exact failures go back to the model and it tries again.

The model is a source of suggestions, never of truth. A draft reports `Passed` only when every local check passed. When no draft passes, you get the best attempt and a full list of what is still wrong.

Use AI drafting when:

- you can describe the format in words but do not want to write the regex yourself,
- you have a few good and bad examples, and want a pattern that is proven against them,
- you want a first draft that you then turn into fluent code, review and commit.

If you only have examples and no description, `Pattern.Learn` infers a pattern on your machine without any network call. See [Learning from Examples](./learning-from-examples.md). If you already have a regex, [Regex to Fluent Code](./regex-to-fluent-code.md) converts it.

:::note[What you need]

- The `Masterly.FluentRegex.AI` package. It targets `net8.0` and `net10.0` only. It is not available on .NET Standard 2.1.
- An Anthropic API key and network access to the Claude API, or to a gateway in front of it.
- The package calls the API with your own `HttpClient`. It has no dependency on an Anthropic SDK.

:::

All types live in the `Masterly.FluentRegex.AI` namespace:

| Type | What it is |
|------|------------|
| `PatternAssistant` | Sends the description to Claude, verifies each draft and retries with feedback. |
| `PatternAssistantOptions` | The API key, model, effort, attempt limit, timeout and endpoint. |
| `PatternDraft` | The result: the regex, the parsed `Pattern`, the model's explanation and the verification. |
| `PatternVerification` | The local checks of one draft: syntax, safety report, failed examples and sample matches. |
| `PatternAssistantException` | Thrown when the API cannot produce a draft: an HTTP error, a timeout, a network failure or a refusal. |

## Installing

```shell
dotnet add package Masterly.FluentRegex.AI
```

The package references `Masterly.FluentRegex`, so you do not need to add the core package separately.

## Drafting a pattern

Create a `PatternAssistant` with an `HttpClient` and your API key, then call `DraftAsync` with a description and examples.

```csharp
using System;
using System.Net.Http;
using Masterly.FluentRegex;
using Masterly.FluentRegex.AI;

// httpClient comes from your application, for example from IHttpClientFactory
string apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set the ANTHROPIC_API_KEY environment variable.");

PatternAssistant assistant = new PatternAssistant(httpClient, new PatternAssistantOptions { ApiKey = apiKey });

PatternDraft draft = await assistant.DraftAsync(
    "a five digit US ZIP code, optionally followed by a dash and four digits",
    shouldMatch: new[] { "12345", "12345-6789" },
    shouldNotMatch: new[] { "1234", "123456", "12345-678" });

if (draft.Passed)
{
    Pattern zip = draft.Pattern!; // a passing draft always has valid syntax, so Pattern is set
    Console.WriteLine(zip.IsMatch("90210"));
}
else
{
    Console.WriteLine(draft); // the best attempt and every check that failed
}
```

- `DraftAsync` always returns a `PatternDraft`, even when no draft passed. Check `Passed` before you use the pattern.
- A draft that fails verification is not an error and never throws. Only API failures throw, as a `PatternAssistantException`. See [Errors and retries](#errors-and-retries).
- The assistant never disposes the `HttpClient`. See [HttpClient, timeouts and dependency injection](#httpclient-timeouts-and-dependency-injection).

:::note

The outputs on this page come from real runs of the package. Because a live model gives different answers on every call, the model's replies were played by the scripted handler shown in [Testing code that uses the assistant](#testing-code-that-uses-the-assistant). Everything the package itself produced, such as prompts, feedback, verification results and exception messages, is shown exactly as it was printed.

:::

### Method reference

```csharp
Task<PatternDraft> DraftAsync(
    string description,
    IEnumerable<string>? shouldMatch = null,
    IEnumerable<string>? shouldNotMatch = null,
    CancellationToken cancellationToken = default)
```

| Parameter | Meaning |
|-----------|---------|
| `description` | What the pattern should accept, in plain language. Required. |
| `shouldMatch` | Strings the pattern must match **in full**. Optional. |
| `shouldNotMatch` | Strings the pattern must not match **anywhere**. Optional. |
| `cancellationToken` | Cancels the call, including a request that is in flight. |

Before any request is sent, `DraftAsync` checks its arguments:

| Input | Exception | Message |
|-------|-----------|---------|
| `description` is `null` | `ArgumentNullException` | `Value cannot be null. (Parameter 'description')` |
| `description` is empty or white space | `ArgumentException` | `The value cannot be an empty string or composed entirely of whitespace. (Parameter 'description')` |
| An example is `null` | `ArgumentException` | `Examples cannot contain null. (Parameter 'shouldMatch')` (or `'shouldNotMatch'`) |
| The token is already cancelled | `OperationCanceledException` | No request is sent. |

- The examples are copied once, so a lazy sequence is enumerated a single time.
- Both example lists are optional. Without them, the model works from the description alone, and the only checks are syntax and safety. Examples are what make a draft trustworthy, so always pass some.
- An empty string is a valid example. Use it in `shouldNotMatch` to forbid a pattern that matches empty input.

### What the model receives

The first message is built from your description and examples. For the ZIP code call above, the model received:

```text
Write a .NET regular expression for: a five digit US ZIP code, optionally followed by a dash and four digits

Strings that must match (the entire string):
  "12345"
  "12345-6789"

Strings that must not match:
  "1234"
  "123456"
  "12345-678"
```

- Examples are quoted JSON-style, so white space and control characters are unambiguous: a newline is shown as `\n`, a carriage return as `\r`, a tab as `\t`, a quote as `\"`, a backslash as `\\`, and other control characters as `\uXXXX` (for example `\u0001`). Characters such as `+` or `<` are left as they are, and an empty example is shown as `""`.
- A list with no examples is left out. With no examples at all, the message is only the first line: `Write a .NET regular expression for: the word abc`.
- Each list shows at most 20 examples, followed by a line such as `(and 5 more)`. Every example is still verified locally, not just the first 20.
- A fixed system prompt tells the model that the regex is compiled with `RegexOptions.None` and checked with `Regex.IsMatch`, that it should anchor with `^` and `$`, use only .NET syntax, avoid inline comments and the `x` option, and avoid the constructs that cause catastrophic backtracking.

## How a draft is verified

Every draft goes through four checks, all on your machine and with the real .NET regex engine. Nothing the model says about its own regex is taken on trust.

| Check | How it is done | Fails when |
|-------|----------------|------------|
| Syntax | `Pattern.FromRegex(regex)` | The regex does not compile. |
| Safety | `pattern.AnalyzeSafety()` | The report has any issue, at any risk level. See [ReDoS Safety](./redos-safety.md). |
| Positives | The regex wrapped as `\A(?:regex)\z` | A `shouldMatch` string is not matched from its first to its last character. |
| Negatives | `Regex.IsMatch` on the regex as written | A `shouldNotMatch` string is matched anywhere. |

- Positives must match the **whole** string, even when the regex has no anchors. A regex such as `\d{5}` therefore passes the positive `"12345"`.
- Negatives are checked the way the returned `Pattern` behaves: `IsMatch` finds a match anywhere in the input. An unanchored `\d{5}` matches inside `"123456"`, so that negative fails. This is what pushes the model to anchor its regex.
- Each example has a one-second match timeout. A positive that times out counts as rejected, and a negative that times out counts as accepted. A regex that is too slow to decide is never reported as passing.
- If wrapping the regex in `\A(?:...)\z` is not valid, for example because it ends in a `(?x)` comment, the positives are checked by matching the regex and comparing the match position and length to the input instead.
- The checks use `RegexOptions.None`. The model is told the same, so it uses inline options such as `(?i)` when it needs them.
- After the checks, the package generates up to five strings the regex matches, with a fixed seed, and stores them in `Verification.Samples`. They are for a quick look only. They do not take part in `Passed`.

### Anchors and the trailing newline

In .NET, `$` also matches just before a newline at the end of the input. A regex such as `^\d{5}$` therefore accepts `"12345\n"`. If that matters to you, add the case as a negative example, and the verification makes the model fix it:

```text
Your regex failed local verification: ^\d{5}$
- These strings must not match but did:
  "12345\n"
Fix every problem above and respond with the corrected JSON object.
```

A next draft of `^\d{5}\z` passes, because `\z` matches only at the very end of the input.

### Digits that are not ASCII

In .NET, `\d` matches every Unicode decimal digit, not only `0` to `9`. You can see it in the samples of a draft that uses `\d`:

```text
  Samples: "554౧44", "938٢", "᭔0497", "3႖19", "14618"
```

When you need ASCII digits only, say so in the description, and add a non-ASCII number such as `"١٢٣٤٥"` (Arabic-Indic digits) to `shouldNotMatch`. With that example, a `^\d{5}$` draft is rejected, and the feedback lists `"١٢٣٤٥"` as a string that must not match. A draft of `^[0-9]{5}$` passes. See [Unicode](./unicode.md) for more about character classes and scripts.

:::tip

Near misses make the best negative examples: one character too few, one too many, a wrong separator, a trailing newline, a non-ASCII digit. Each one rules out a whole family of wrong drafts.

:::

## The feedback loop

`DraftAsync` runs a conversation with the model:

1. It sends your description and examples.
2. It verifies the reply.
3. If the draft passed, it returns it at once.
4. If not, it sends the exact failures back and asks for a corrected draft.
5. It stops after `MaxAttempts` drafts (3 by default) and returns the best one.

Each attempt is one request to the API. The whole conversation is sent every time, including the model's earlier replies, which are passed back unchanged. So the model sees all its earlier drafts and every failure, and each later request is a little larger than the one before.

In the recorded ZIP code run, the first draft was `\d{5}(-\d{4})?`. It has no anchors, so two negatives matched inside the longer strings. The model received:

```text
Your regex failed local verification: \d{5}(-\d{4})?
- These strings must not match but did:
  "123456"
  "12345-678"
Fix every problem above and respond with the corrected JSON object.
```

Its second draft passed, and `Console.WriteLine(draft)` printed:

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

### What the feedback says

Every kind of failure produces its own line, so the model knows exactly what to fix. These are real feedback messages. `...` marks text left out to keep the table short: a ReDoS line carries the full description and suggestion of the finding, and every unusable-reply message ends with the same request for a JSON object.

| Failure | Feedback sent to the model |
|---------|----------------------------|
| Invalid syntax | `- Syntax error: The regular expression is invalid: Invalid pattern '^(\d{5}$' at offset 8. Not enough )'s. (Parameter 'regex')` |
| ReDoS finding | `- ReDoS risk (High NestedQuantifier) at position 1 in (\d+)+: The repetition '\d+' is nested inside the repetition '+'. ... Fix: Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. ...` |
| A positive did not match in full | `- These strings must match the entire string but did not:` followed by the strings |
| A negative matched | `- These strings must not match but did:` followed by the strings |
| No JSON object in the reply | `Your reply could not be used: it did not contain a JSON object. Respond with only a JSON object with string properties "regex" and "explanation".` |
| Invalid JSON, or a `regex` value that is not a string (such as `"regex": 5`) | `Your reply could not be used: it was not valid JSON. ...` |
| JSON with no `regex` property, or `"regex": null` | `Your reply could not be used: the JSON object had no string property "regex". ...` |
| Reply cut off by `MaxTokens` | `Your reply could not be used: it was cut off by the token limit before the JSON object was complete. ...` |

- One feedback message lists every failure of the draft at once: a draft can get a ReDoS finding and a list of accepted negatives in the same message.
- Like the first message, each failure list shows at most 20 strings, then `(and N more)`.
- The model is asked for structured JSON output (`{"regex": ..., "explanation": ...}`). When a gateway does not enforce the schema and the reply has extra prose or a code fence around the JSON, the package takes the text from the first `{` to the last `}`, so the draft still works. The text of every `text` block in the reply is joined first; other block types are ignored.
- A missing `explanation` is not a problem. The draft is checked as usual and `Explanation` is empty.
- An unusable reply counts as an attempt, the same as a draft that fails its checks.

### Choosing the best draft

When no draft passes, the assistant returns the draft with the fewest failed checks. A failed check is one safety issue, one rejected positive or one accepted negative. A draft that could not be checked at all, because of invalid syntax or an unusable reply, always ranks last. On a tie, the earlier draft wins. All drafts that could not be checked tie with each other, so when every draft is unusable or invalid, the first one is returned.

In this run, all three drafts failed. The first, `\d+`, accepted three negatives. The second, `^\d{4,6}$`, accepted two. The third, `^(\d{5}$`, was invalid. The second is returned:

```csharp
// draft comes from DraftAsync, called with "a five digit US ZIP code",
// shouldMatch: { "12345", "00501" } and shouldNotMatch: { "1234", "123456", "12a45" }
Console.WriteLine(draft.Passed);   // False
Console.WriteLine(draft.Attempts); // 3
Console.WriteLine(draft.Regex);    // ^\d{4,6}$
Console.WriteLine(string.Join(", ", draft.Verification.AcceptedNegatives)); // 1234, 123456

Console.WriteLine(draft);
// Draft did NOT pass verification after 3 attempt(s).
// Regex: ^\d{4,6}$
// Explanation: Four to six digits.
// Verification FAILED.
//   Syntax: valid
//   Safety: no findings
//   Rejected positives: none
//   Accepted negatives: "1234", "123456"
//   Samples: "554౧44", "938٢", "᭔0497", "3႖19", "14618"
```

- `Attempts` is the number of drafts requested, not the number of the draft returned. When nothing passes it equals `MaxAttempts`.
- The returned `Regex`, `Explanation`, `Pattern` and `Verification` all belong to the best draft, here the second one.
- Nothing is rounded up. `Passed` is `true` only for a draft that passed every check.

## Reading the result

`PatternDraft` holds one draft and its verification:

| Member | Type | Meaning |
|--------|------|---------|
| `Passed` | `bool` | `true` when every local check passed. Same as `Verification.Passed`. |
| `Pattern` | `Pattern?` | The draft as a `Pattern`, ready to use. `null` when the returned draft could not be checked. |
| `Regex` | `string` | The regex text the model wrote. Empty when the returned draft is an unusable reply. |
| `Explanation` | `string` | The model's one- or two-sentence description. Empty when it sent none. |
| `Attempts` | `int` | How many drafts were requested from the model. |
| `Verification` | `PatternVerification` | The result of every local check. |
| `ToString()` | `string` | The status, regex, explanation (when there is one) and the verification summary. |

`PatternVerification` holds the details:

| Member | Type | Meaning |
|--------|------|---------|
| `Passed` | `bool` | Valid syntax, no safety findings, no rejected positives and no accepted negatives. |
| `IsValidSyntax` | `bool` | `true` when `SyntaxError` is `null`. |
| `SyntaxError` | `string?` | Why the draft could not be checked: the syntax error, the reason the safety analysis could not run, or what was wrong with the model's reply. |
| `SafetyReport` | `SafetyReport?` | The full ReDoS analysis. `null` when the draft could not be checked. |
| `HasSafetyFindings` | `bool` | `true` when the safety report has at least one issue. |
| `RejectedPositives` | `IReadOnlyList<string>` | `shouldMatch` strings the regex did not match in full. |
| `AcceptedNegatives` | `IReadOnlyList<string>` | `shouldNotMatch` strings the regex matched. |
| `Samples` | `IReadOnlyList<string>` | Up to five strings the regex matches, generated locally. Empty when the draft could not be checked, or when the generator cannot build a matching string (for example for `^\p{Cs}$`, a lone surrogate). Such a draft can still pass. |
| `ToString()` | `string` | A multi-line summary of every check. Each list shows at most five strings, then a count such as `(+2 more)`. |

When a draft is not usable, the summary says why. This is a draft with invalid syntax, returned with `MaxAttempts = 1`:

```text
Draft did NOT pass verification after 1 attempt(s).
Regex: ^(\d{5}$
Explanation: x
Verification FAILED.
  Syntax: invalid (The regular expression is invalid: Invalid pattern '^(\d{5}$' at offset 8. Not enough )'s. (Parameter 'regex'))
  Safety: not analyzed
  Rejected positives: none
  Accepted negatives: none
  Samples: none
```

And this is an unsafe draft. Note that the nested quantifier passed both examples. Only the safety check caught it:

```text
Draft did NOT pass verification after 1 attempt(s).
Regex: ^(\d+)+$
Explanation: Digits.
Verification FAILED.
  Syntax: valid
  Safety: 1 finding(s), risk High
    High NestedQuantifier at 1 '(\d+)+': The repetition '\d+' is nested inside the repetition '+'. The same text can be split between iterations in exponentially many ways, so a failing match backtracks through all of them.
  Rejected positives: none
  Accepted negatives: none
  Samples: "54౧4١98٢᭔0473972", "27488680868711", "8795816", "90191630５55067966169", "69585"
```

`Pattern` is `null` when no draft could be checked. The returned draft is then the first one (see [Choosing the best draft](#choosing-the-best-draft)), and it is one of these:

- A draft with invalid syntax.
- An unusable reply. Then `Regex` is also empty and `SyntaxError` describes the reply. This happens even when a later draft had a regex: in a real run, the first reply had no JSON and the second had the invalid regex `^(a`, and the summary began like this:

  ```text
  Draft did NOT pass verification after 2 attempt(s).
  Regex: 
  Verification FAILED.
    Syntax: invalid (it did not contain a JSON object.)
    Safety: not analyzed
  ```

- A regex the safety analysis cannot process. This is rare. For example, .NET compiles `^\p{Cs}*\p{Cs}*$` (surrogate code points only), but `AnalyzeSafety` throws for it. The package catches the error and reports the draft as not checked: `IsValidSyntax` is `false` and the summary shows `Syntax: invalid (The character class has no character that can be generated (for example, it contains only surrogate code units).)`.

For an unsafe draft or a draft that fails examples, `Pattern` is set, so you can study it. Do not ship it without fixing it.

### Next steps with a passing draft

A passing draft is a starting point, not the end of the job:

```csharp
// draft is the passing ZIP code draft from "The feedback loop"
Console.WriteLine(draft.Pattern!.ToFluentCode()); // a passing draft always has a Pattern
// Pattern.With
//     .StartOfLine
//     .Range('0', '9').Repeat.Times(5)
//     .Group(Pattern.With.Literal("-").Range('0', '9').Repeat.Times(4)).Repeat.Optional
//     .EndOfLine
```

- Read the regex and the fluent code. The `Explanation` comes from the model: treat it as a description, not as a guarantee.
- Paste the fluent code into your project instead of calling the API at run time. See [Regex to Fluent Code](./regex-to-fluent-code.md).
- Keep your examples as unit tests, and add the cases you think of later. See [xUnit](./xunit.md), [Regex Coverage](./regex-coverage.md) and [Property Testing](./property-testing.md).
- For input from users, also set a timeout or use the non-backtracking engine. See [ReDoS Safety](./redos-safety.md).

## Options

`PatternAssistantOptions` configures the assistant. Only `ApiKey` is required.

```csharp
using System;
using System.Threading;
using Masterly.FluentRegex.AI;

// apiKey comes from your configuration or a secret store
PatternAssistantOptions options = new PatternAssistantOptions
{
    ApiKey = apiKey,
    Model = PatternAssistantOptions.DefaultModel, // "claude-opus-5-5"
    Effort = "high",
    MaxAttempts = 5,
    MaxTokens = 16000,
    Timeout = TimeSpan.FromMinutes(5),
    BaseAddress = new Uri("https://api.anthropic.com/"),
    UseServerSideFallback = true,
};

Console.WriteLine(options);
// Model = claude-opus-5-5, MaxAttempts = 5, BaseAddress = https://api.anthropic.com/, Timeout = 00:05:00, MaxTokens = 16000, Effort = high, ApiKey = <redacted>
```

| Option | Default | Notes |
|--------|---------|-------|
| `ApiKey` | Empty (required) | Sent in the `x-api-key` header. Never shown in `ToString()` or in exception messages. |
| `Model` | `claude-opus-5-5` (`DefaultModel`) | Any Messages API model id your account can use. |
| `Effort` | `medium` (`DefaultEffort`) | Sent as `output_config.effort`: `low`, `medium`, `high`, `xhigh` or `max`. Higher effort can give better drafts on hard formats, at more cost and time. |
| `MaxAttempts` | `3` | Drafts per `DraftAsync` call, counting the first one. `1` means no retries. |
| `MaxTokens` | `16000` | The response token limit of each request. A reply cut off by the limit is reported to the model and counts as an attempt. |
| `Timeout` | 5 minutes | The time limit of each request. `Timeout.InfiniteTimeSpan` turns it off. `HttpClient.Timeout` also applies. |
| `BaseAddress` | `https://api.anthropic.com/` | The API root. Requests go to `v1/messages` relative to it. |
| `UseServerSideFallback` | `true` | Asks the API to retry a request the model declines on Anthropic's recommended fallback model. Turn it off for gateways that do not support it. |

`ToString()` lists every setting except `UseServerSideFallback`, with the key shown as `<redacted>`, or `<not set>` when it is empty. It is safe to log.

### Validation

The constructor validates the options and throws before any request is sent. The messages never contain the key.

| Problem | Exception | Message |
|---------|-----------|---------|
| `ApiKey` empty or white space | `ArgumentException` | `An API key is required (PatternAssistantOptions.ApiKey).` |
| `ApiKey` has white space or control characters, such as a pasted newline | `ArgumentException` | `The API key contains whitespace or control characters.` |
| `Model` empty or white space | `ArgumentException` | `A model id is required.` |
| `Effort` empty or white space | `ArgumentException` | `An effort level is required.` |
| `BaseAddress` `null` or relative | `ArgumentException` | `BaseAddress must be an absolute URI.` |
| `MaxAttempts` below 1 | `ArgumentOutOfRangeException` | `MaxAttempts must be at least 1.` |
| `MaxTokens` below 1 | `ArgumentOutOfRangeException` | `MaxTokens must be at least 1.` |
| `Timeout` zero or negative, and not `Timeout.InfiniteTimeSpan` | `ArgumentOutOfRangeException` | `Timeout must be positive or Timeout.InfiniteTimeSpan.` |

- The table shows the message text. The parameter name of every option error is `options`, so `Message` ends with ` (Parameter 'options')`, for example `A model id is required. (Parameter 'options')`. The out-of-range errors also carry the bad value in `ActualValue`, and their `Message` has a second line such as `Actual value was 0.`
- The white space check on `ApiKey` covers every white space character, including a space in the middle of the key.
- A `null` `httpClient` or `options` throws `ArgumentNullException`.
- The model id and the effort level are not checked against a list. An unknown value is sent as it is, the API rejects the request, and `DraftAsync` throws a `PatternAssistantException` with the API's error.
- The constructor copies the options. Changing the options object later has no effect on an assistant that already exists.

:::warning[Keep the trailing slash on BaseAddress]

The request URL is built with `new Uri(BaseAddress, "v1/messages")`. Without a trailing slash, the last segment of the address is replaced. In a real run, `https://gateway.example.com/anthropic` sent requests to `https://gateway.example.com/v1/messages`. With `https://gateway.example.com/anthropic/` they went to `https://gateway.example.com/anthropic/v1/messages`, as intended.

:::

## HttpClient, timeouts and dependency injection

The assistant sends requests with the `HttpClient` you give it and never disposes it. This makes it a good fit for `IHttpClientFactory` and typed clients.

```csharp
using System;
using Masterly.FluentRegex.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// services and configuration come from your application, for example builder.Services and builder.Configuration
services.AddSingleton(new PatternAssistantOptions
{
    ApiKey = configuration["Anthropic:ApiKey"]
        ?? throw new InvalidOperationException("Anthropic:ApiKey is not configured."),
});

services.AddHttpClient<PatternAssistant>(client => client.Timeout = TimeSpan.FromMinutes(6));

// later, in a class that needs it:
// public RegexToolController(PatternAssistant assistant) { ... }
```

- `AddHttpClient<PatternAssistant>` registers the assistant as a typed client, with a transient lifetime. The factory creates it with a managed `HttpClient` and takes the `PatternAssistantOptions` from the container.
- The package has no `AddPatternAssistant` extension method or options binding of its own. Register it as shown.
- Keep the API key out of source control: use user secrets, environment variables or a secret store.
- An assistant keeps no state between calls. Each `DraftAsync` call has its own conversation, so one instance can serve several calls at the same time.

### Two timeouts apply

Every request is limited by both `PatternAssistantOptions.Timeout` (5 minutes by default) and `HttpClient.Timeout`. The default `HttpClient.Timeout` is 100 seconds, so it is usually the one that fires first. A draft at a high effort level can take longer than that. Raise `HttpClient.Timeout` above `Options.Timeout`, as in the registration above.

The exception names the timeout that fired. With `HttpClient.Timeout` set to 2 seconds and the default options, the call fails after 2 seconds with:

```text
The Claude API did not respond within 00:00:02 (HttpClient.Timeout).
```

- When `Options.Timeout` fires first, the message ends with `(PatternAssistantOptions.Timeout)` instead.
- `Options.Timeout` applies to each request separately, not to the whole `DraftAsync` call. A call with three attempts can take up to three times as long.
- Both timeouts cover everything the client does for one request, including retries by a resilience handler.

## Errors and retries

When the API cannot produce a draft, `DraftAsync` throws `PatternAssistantException`. A draft that fails verification is not an error. It is returned as a `PatternDraft` with `Passed == false`.

| Property | Type | Meaning |
|----------|------|---------|
| `StatusCode` | `HttpStatusCode?` | The HTTP status. `null` for timeouts, network failures, refusals and unreadable responses. |
| `ErrorType` | `string?` | The API error type, such as `authentication_error`, `rate_limit_error` or `overloaded_error`. `null` when the error body was not an API error, for example an HTML page from a proxy. |
| `RequestId` | `string?` | The `request-id` response header. Include it when you contact Anthropic support. |
| `RetryAfter` | `TimeSpan?` | The delay from the `Retry-After` header, when the server sent one. A date in the past gives `TimeSpan.Zero`. |
| `IsTransient` | `bool` | `true` when retrying later may succeed. |
| `InnerException` | `Exception?` | The original exception for network failures, timeouts and unreadable JSON. |

These are real messages, each from a handler that returned the failure in the first column:

| Failure | `Message` | `IsTransient` |
|---------|-----------|---------------|
| HTTP 401 with a `request-id` header | `The Claude API returned HTTP 401 authentication_error: invalid x-api-key (request-id req_011CabcXYZ).` | `false` |
| HTTP 400 | `The Claude API returned HTTP 400 invalid_request_error: model: not found.` | `false` |
| HTTP 429 with `Retry-After: 30` | `The Claude API returned HTTP 429 rate_limit_error: Number of request tokens has exceeded your per-minute rate limit.` (`RetryAfter` is `00:00:30`) | `true` |
| HTTP 529 | `The Claude API returned HTTP 529 overloaded_error: Overloaded.` | `true` |
| HTTP 502 with an HTML body | `The Claude API returned HTTP 502.` | `true` |
| Network failure (a `BaseAddress` whose host does not exist) | `Could not reach the Claude API: No such host is known. (nonexistent-host.invalid:443)` | `true` |
| `Options.Timeout` of 2 seconds expired | `The Claude API did not respond within 00:00:02 (PatternAssistantOptions.Timeout).` | `true` |
| `stop_reason` is `refusal` | `The model declined to draft this pattern (stop_reason "refusal").` | `false` |
| HTTP 200 with a body that is not JSON | `The Claude API returned a response that is not valid JSON.` | `false` |
| HTTP 200 without a `content` array | `The Claude API response has no content array.` | `false` |

- `IsTransient` is `true` for HTTP 408, 409, 429 and every status of 500 or above, and for timeouts and network failures. Other 4xx errors (such as 400, 401 and 403), refusals and unreadable responses are not transient.
- The message names the error type and the API's message only when the body is an API error. Otherwise it gives the status alone, as for the HTML page above. The text after `Could not reach the Claude API:` comes from .NET and the operating system, so it varies.
- The API's error message is cut at 500 characters and followed by `...`. If it contains your key, the key is replaced with `<redacted>`. The same applies to network error messages.
- The message always ends with a period, so an API message that already ends with one, like the 429 message above, shows two.
- When the server sends more than one `request-id` header, `RequestId` joins them with commas.
- With `UseServerSideFallback` on, the request asks the API to retry a declined request on the fallback model. The package throws for a refusal only when the reply it receives still has `stop_reason` `refusal`.
- An exception ends the whole `DraftAsync` call. Drafts from earlier attempts in the same call are not returned.
- Only `HttpRequestException` and timeouts are wrapped. Any other exception thrown by a message handler in the `HttpClient` passes through unchanged.
- `PatternAssistantException` also has the three standard public constructors (no arguments, a message, and a message with an inner exception), so you can throw it from a test double. An exception created this way has `StatusCode`, `ErrorType`, `RequestId` and `RetryAfter` set to `null` and `IsTransient` set to `false`. The constructor without arguments uses the message `The pattern assistant request failed.`

### Cancellation is not a timeout

Cancelling your own `CancellationToken` throws `OperationCanceledException` (in practice often a `TaskCanceledException`), never `PatternAssistantException`. A timeout from `Options.Timeout` or `HttpClient.Timeout` is reported as a transient `PatternAssistantException` instead, as shown above. So a `catch (OperationCanceledException)` sees only cancellations you asked for.

### Adding retries

The assistant does not retry HTTP failures itself, so a bad key is never sent twice. To retry transient failures automatically, add a resilience handler to the `HttpClient`. This example uses the `Microsoft.Extensions.Http.Resilience` package:

```csharp
using System;
using Masterly.FluentRegex.AI;
using Microsoft.Extensions.DependencyInjection;

// services comes from your application; PatternAssistantOptions is registered as shown above
services.AddHttpClient<PatternAssistant>(client => client.Timeout = TimeSpan.FromMinutes(6))
    .AddStandardResilienceHandler(resilience =>
    {
        resilience.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
        resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(4);
        resilience.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
    });
```

- In a real run with this registration, an HTTP 503 followed by a success gave a passing draft after two HTTP requests. `draft.Attempts` was still `1`: HTTP retries are separate from draft attempts.
- An HTTP 401 was sent once and not retried.
- The standard handler's default timeouts are 10 seconds per attempt and 30 seconds in total. That is too short for a model call, so raise them as shown.
- The handler validates its settings: the total timeout must be greater than the attempt timeout, and the circuit breaker's sampling duration (30 seconds by default) must be at least twice the attempt timeout. When you raise only the attempt timeout, both rules fail, and resolving `PatternAssistant` throws an `OptionsValidationException`.

Without a resilience handler, you can retry by hand with `IsTransient` and `RetryAfter`:

```csharp
using System;
using System.Threading.Tasks;
using Masterly.FluentRegex.AI;

// assistant comes from your application
PatternDraft? draft = null;
for (int attempt = 1; draft == null; attempt++)
{
    try
    {
        draft = await assistant.DraftAsync("an ISO 8601 date such as 2024-02-29");
    }
    catch (PatternAssistantException ex) when (ex.IsTransient && attempt < 3)
    {
        await Task.Delay(ex.RetryAfter ?? TimeSpan.FromSeconds(5 * attempt));
    }
}
```

## Testing code that uses the assistant

Unit tests should not call the real API. Because the assistant takes an `HttpClient`, you can give it a client with a handler that plays the model. This handler queues replies in the Messages API format and records every request:

```csharp
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Stands in for the Claude Messages API in tests. Replies are returned in the order they were queued.</summary>
public sealed class ScriptedClaudeHandler : HttpMessageHandler
{
    private readonly Queue<string> _replies = new Queue<string>();

    /// <summary>Gets the JSON body of every request the assistant sent.</summary>
    public List<string> RequestBodies { get; } = new List<string>();

    /// <summary>Queues a model reply that drafts the given regex.</summary>
    public ScriptedClaudeHandler Reply(string regex, string explanation)
    {
        string text = JsonSerializer.Serialize(new { regex, explanation });
        _replies.Enqueue(JsonSerializer.Serialize(new
        {
            type = "message",
            role = "assistant",
            stop_reason = "end_turn",
            content = new[] { new { type = "text", text } },
        }));
        return this;
    }

    /// <summary>Gets the text of the last user turn in a request: the feedback on every retry.</summary>
    public string LastUserText(int requestIndex)
    {
        using JsonDocument document = JsonDocument.Parse(RequestBodies[requestIndex]);
        JsonElement messages = document.RootElement.GetProperty("messages");
        return messages[messages.GetArrayLength() - 1].GetProperty("content").GetString() ?? string.Empty;
    }

    /// <summary>Records the request body and returns the next queued reply.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // The assistant always sends a JSON body, so Content is never null here.
        RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_replies.Dequeue(), Encoding.UTF8, "application/json"),
        };
    }
}
```

Script the replies, run the assistant, and check what it did. This is the run that produced the ZIP code output earlier on this page:

```csharp
using System;
using System.Net.Http;
using Masterly.FluentRegex.AI;

ScriptedClaudeHandler handler = new ScriptedClaudeHandler()
    .Reply(@"\d{5}(-\d{4})?", "Five digits, optional +4.")
    .Reply(@"^[0-9]{5}(-[0-9]{4})?$", "Five digits, optional +4.");

PatternAssistant assistant = new PatternAssistant(
    new HttpClient(handler),
    new PatternAssistantOptions { ApiKey = "test-key" });

PatternDraft draft = await assistant.DraftAsync(
    "a five digit US ZIP code, optionally followed by a dash and four digits",
    shouldMatch: new[] { "12345", "12345-6789" },
    shouldNotMatch: new[] { "1234", "123456", "12345-678" });

Console.WriteLine(draft.Passed);                     // True
Console.WriteLine(draft.Attempts);                   // 2
Console.WriteLine(handler.RequestBodies.Count);      // 2
Console.WriteLine(draft.Pattern!.IsMatch("90210-1234")); // True (a passing draft always has a Pattern)
Console.WriteLine(handler.LastUserText(1).Contains("These strings must not match but did:")); // True
```

- Queue one reply per expected attempt. The handler throws when the assistant asks for more replies than you queued, which makes an unexpected retry fail the test.
- To test error handling, return an error status from the handler with a body such as `{"type":"error","error":{"type":"rate_limit_error","message":"Slow down."}}`.
- The verification is the real one: your scripted regex is compiled, analyzed and matched against your examples, exactly as a live draft would be.
- This handler uses reflection-based JSON for brevity. It is fine in a test project, but not in a Native AOT app.

## What is sent to Claude

Each attempt is one `POST` to `{BaseAddress}v1/messages` with these headers:

| Header | Value |
|--------|-------|
| `x-api-key` | Your API key. |
| `anthropic-version` | `2026-10-21` |
| `anthropic-beta` | `server-side-fallback-2026-07-01`. Only sent when `UseServerSideFallback` is `true`. |
| `Content-Type` | `application/json` |

The JSON body has:

| Field | Value |
|-------|-------|
| `model` | `Options.Model` |
| `max_tokens` | `Options.MaxTokens` |
| `system` | The fixed system prompt described in [What the model receives](#what-the-model-receives). |
| `messages` | The conversation so far: your request, each earlier reply unchanged, and each feedback message. |
| `output_config.effort` | `Options.Effort` |
| `output_config.format` | `{"type": "json_schema", "schema": ...}`, where the schema describes an object with the required string properties `regex` and `explanation`, and no others. |
| `fallbacks` | `"default"`. Only sent when `UseServerSideFallback` is `true`. |

:::warning[Your examples leave your machine]

The description and every example string are sent to the Claude API in plain text, and so are the failed examples in each feedback message. Do not use real customer data, such as actual email addresses, account numbers or names, as examples. Write made-up values in the same format instead.

:::

## AOT and trimming

The package is marked as AOT compatible. It uses no reflection: requests and responses go through source-generated `System.Text.Json` serialization. A console app that drafts a pattern was published with `PublishAot=true` for .NET 10 without any trim or AOT warnings, and the native executable ran the full draft and verification loop.

Your own code still needs to be AOT safe. For example, the test handler above serializes anonymous types with reflection, so keep it in your test project.

## Good practice

- **Draft at design time, not at run time.** Use the assistant in a tool, a test or a one-off script, then commit the reviewed fluent code. Your app then does not depend on the network, an API key or a model's answer.
- **Give near-miss negatives.** The verification can only hold the model to the examples you give it. See [Anchors and the trailing newline](#anchors-and-the-trailing-newline) and [Digits that are not ASCII](#digits-that-are-not-ascii).
- **Always check `Passed`.** A failed draft still has a `Regex`, and usually a `Pattern`. Use `Verification` to see why it failed.
- **Do not trust the explanation.** It is the model's description of its own regex. The verification is the only part that is checked.
- **Keep `MaxAttempts` small.** Each attempt is a full request, and the conversation grows with each one. If three attempts do not pass, improve the description or the examples instead of adding attempts.
- **Never log the key.** `PatternAssistantOptions.ToString()` and every exception message from the package already hide it.

## Related pages

- [Learning from Examples](./learning-from-examples.md): infer a pattern from examples alone, with no network call.
- [Regex to Fluent Code](./regex-to-fluent-code.md): turn a drafted regex into fluent code.
- [ReDoS Safety](./redos-safety.md): the safety analysis every draft must pass.
- [Test Data Generation](./test-data-generation.md): how the samples in a verification are generated.
- [xUnit](./xunit.md): keep your examples as tests.
- [Best Practices](./best-practices.md)
