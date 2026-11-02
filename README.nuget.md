# Masterly.FluentRegex

A fluent, immutable API for building .NET regular expressions, plus the tools to understand, test,
secure and ship them.

## Quick Start

```csharp
using Masterly.FluentRegex;

Pattern pattern = Pattern.With
    .StartOfLine
    .Digit.Repeat.OneOrMore
    .Literal("-")
    .Word.Repeat.Times(3, 5)
    .EndOfLine;

bool isMatch = pattern.IsMatch("123-abc");   // true
string regex = pattern.ToString();           // ^\d+-\w{3,5}$
```

## Highlights in 3.0

```csharp
// Typed extraction straight into records
record LogEntry(DateTime Timestamp, Level Level, string Message);
LogEntry entry = logPattern.Extract<LogEntry>("2024-05-17T10:30:00 ERROR Disk full");

// Legacy regex to readable fluent C#
string code = Pattern.FromRegex(@"^(?<user>[\w.]+)@(?<host>[\w-]+\.\w{2,})$").ToFluentCode();

// Verified test data: valid samples and near misses
string[] good = CommonPatterns.IPv4().GenerateMatching(20, seed: 42);
string[] bad  = CommonPatterns.IPv4().GenerateNonMatching(20, seed: 42);

// ReDoS analysis, proven with a timed attack input
SafetyReport report = Pattern.FromRegex(@"^(\w+\s?)*$").AnalyzeSafety(confirm: true);  // Risk: High, Confirmed

// See which part matched what, or exactly where matching failed
Console.WriteLine(datePattern.Trace("2024-5-17"));

// Railroad diagrams and Mermaid flowcharts
File.WriteAllText("email.svg", CommonPatterns.Email().ToRailroadSvg());

// Validated, fluent replacements
datePattern.Replace(text, r => r.Group("day").Literal("/").Group("month").Literal("/").Group("year"));
```

## Compile-Time Checks Included

The package ships a source generator, analyzers and a code fix. No extra install is needed.

- `[GeneratePattern]` evaluates a fluent chain at build time and emits the regex text and a cached, compiled `Regex`.
- Build diagnostics for invalid regexes (FRX003), ReDoS risks (FRX004, FRX005), builder calls that would throw (FRX006) and `Extract<T>` group typos (FRX008).
- A code fix adds a .NET `[GeneratedRegex]` method for any constant pattern (FRX007).

## Also New

- Immutable patterns: every call returns a new pattern, so building blocks are safe to share.
- `.Lazy`, `.Possessive`, `OneOf`, the `+` and `|` operators, `Range`, `HexDigit`, `AnyOf`, `NoneOf`, Unicode categories and inline options.
- `WithTimeout`, `UseNonBacktracking`, `Count`, span overloads, `EnumerateMatches` and `EnumerateSplits`.
- New common patterns: E.164 phones, IBAN, SemVer, JWT, MAC address, Base64, slugs, ISO 8601 date-times, Markdown links, and strictness levels for email and URL.
- Targets .NET 9, .NET 8 (trimming and Native AOT compatible) and .NET Standard 2.1.

## Command-Line Tool

```bash
dotnet tool install --global Masterly.FluentRegex.Tool
fluentregex scan src --fail-on high     # find ReDoS-prone regexes in any C# codebase
```

## Upgrading from 2.x

Patterns are now immutable, and several 2.x bugs are fixed, including `Literal` ranges inside sets,
`Letter` outside sets and `AtMost`. See the
[migration guide](https://github.com/a7mdfre7at/Masterly.FluentRegex#migrating-from-2x-to-30).

## Documentation

Full documentation, examples and API reference:
[GitHub repository](https://github.com/a7mdfre7at/Masterly.FluentRegex#readme).

## License

MIT
