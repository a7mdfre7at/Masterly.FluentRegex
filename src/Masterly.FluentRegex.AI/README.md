# Masterly.FluentRegex.AI

Draft [Masterly.FluentRegex](https://github.com/a7mdfre7at/Masterly.FluentRegex) patterns from plain
language with Claude, and **verify every draft locally** before you see it.

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.AI;

PatternAssistant assistant = new PatternAssistant(
    httpClient,                                           // from IHttpClientFactory; not disposed by the assistant
    new PatternAssistantOptions { ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")! });

PatternDraft draft = await assistant.DraftAsync(
    "a five digit US ZIP code, optionally followed by a dash and four digits",
    shouldMatch: new[] { "12345", "12345-6789" },
    shouldNotMatch: new[] { "1234", "123456", "12345-678" });

if (draft.Passed)
{
    Pattern zip = draft.Pattern!;
}
else
{
    Console.WriteLine(draft);   // the best attempt and exactly which checks failed
}
```

Each draft must pass, on your machine: valid .NET syntax, no findings from `Pattern.AnalyzeSafety()` (ReDoS),
every `shouldMatch` string matched in full, and no `shouldNotMatch` string matched. Failures are sent back to
the model as precise feedback and retried up to `MaxAttempts` (default 3). If no draft passes you get the best
attempt with `Passed == false`; a draft is never reported as passing unless it did.

- Calls the Claude Messages API directly with `HttpClient` and source-generated `System.Text.Json`;
  no SDK dependency, AOT and trim safe.
- Defaults: model `claude-opus-5-5`, effort `medium`, structured JSON output, server-side refusal fallback.
- HTTP failures throw `PatternAssistantException` with `StatusCode`, `ErrorType`, `RequestId`, `RetryAfter`
  and `IsTransient`. The assistant does not retry HTTP errors itself; add a resilience handler to the
  `HttpClient` if you want that. The API key never appears in messages or `ToString()`.
