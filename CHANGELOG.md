# What's New in 3.0.0

FluentRegex 3.0 goes beyond building patterns: it now helps you understand, test, secure and ship them.

## Breaking Changes

See the [migration guide](https://github.com/a7mdfre7at/Masterly.FluentRegex#migrating-from-2x-to-30) for details.

- **Patterns are immutable.** Every member returns a new `Pattern`. Code that called a member as a statement and ignored the result, such as `pattern.WithOptions(...);`, must assign the result.
- **`Literal` inside a `Set` escapes `-`.** `Set(Literal("a-f"))` now matches `a`, `-` and `f`. Use the new `Range('a', 'f')` for ranges.
- **`Letter`, `LowercaseLetter` and `UppercaseLetter` outside a `Set`** now render `[a-zA-Z]`, `[a-z]` and `[A-Z]` instead of literal text.
- **`Repeat.AtMost(n)`** now renders `{0,n}`. .NET treated the old `{,n}` as literal text.
- **`ToRegex()` without arguments** now uses the pattern's options and timeout.
- **Invalid builder calls throw.** `Repeat` on an empty pattern throws `InvalidOperationException`, and negative or reversed bounds throw `ArgumentOutOfRangeException`.
- `GeneratePackageOnBuild` is off; pack with `dotnet pack`.

## Fixed

- The compiled-regex cache could go stale when a pattern was extended after matching. Immutability makes this impossible.
- `Repeat.AtMost` never matched as intended.
- `Letter` outside a set matched the literal text `a-zA-Z`.
- Literal hyphens inside sets silently created ranges. In 2.x, `CommonPatterns.StrongPassword()` treated every digit as a special character because of the range `+` to `=`.
- `CommonPatterns.Url()` allowed polynomial backtracking through dots in the first host label.
- A quantifier applied to an already-quantified element produced an invalid nested quantifier; it is now wrapped in a non-capturing group.
- The README documented `.Lazy`, which did not exist.

## Added

### Builder
- `.Lazy` and `.Possessive` quantifier modes.
- `OneOf(...)` non-capturing alternation, the `+` (concatenate) and `|` (alternate) operators, and `Then(pattern)`.
- `Range(from, to)`, `HexDigit`, `AnyOf(chars)`, `NoneOf(chars)`.
- `Unicode(UnicodeCategory)`, `Unicode(UnicodeGroup)`, `NotUnicode(...)`, `UnicodeBlock(name)`.
- `StartOfInput`, `EndOfInput`, `NonWordBoundary`.
- `IgnoreCase(pattern)`, `CaseSensitive(pattern)`, `InlineOptions(enable, pattern, disable)`.
- `WithTimeout(timeout)`, `UseNonBacktracking()` (.NET 8+), and the `Options`, `Timeout` and `IsEmpty` properties.

### Understanding and Testing
- **Typed extraction:** `Extract<T>`, `TryExtract<T>` and `ExtractAll<T>` bind named groups to records and classes, with `[RegexGroup]` renaming and `ExtractionConverters` for custom types.
- **Fluent replacements:** `Replace(input, r => r.Group("day").Literal("/")...)` validates group names before replacing.
- **Regex to fluent code:** `Pattern.FromRegex(regex)` and `ToFluentCode()`.
- **Test data generation:** `GenerateMatching(count, seed)` and `GenerateNonMatching(count, seed)`, verified against the .NET engine.
- **ReDoS analysis:** `AnalyzeSafety(confirm)` detects nested quantifiers, ambiguous alternations and competing repetitions, builds attack inputs and can prove them with a timed run.
- **Match tracing:** `Trace(input)` reports the exact span each construct matched, or the deepest construct that failed and what was found there.
- **Diagrams:** `ToRailroadSvg()` and `ToMermaid()`.

### Compile Time
- **Source generator:** `[GeneratePattern]` evaluates a fluent chain at build time and emits the regex text and a cached, compiled `Regex`.
- **Analyzers:** FRX001 to FRX009 report invalid regexes, ReDoS risks, construction errors, `Extract<T>` group typos, `[GeneratedRegex]` opportunities and generator misuse.
- **Code fix:** adds a .NET `[GeneratedRegex]` method for a constant pattern.

### Common Patterns
- `Email(PatternStrictness)` and `Url(PatternStrictness)` with Loose, Standard and Strict levels.
- `PhoneNumberE164`, `Iban`, `SemVer`, `Jwt`, `MacAddress`, `Base64`, `Slug`, `DateTimeIso`, `MarkdownLink`.

### Platform
- Targets .NET 9, .NET 8 and .NET Standard 2.1. The .NET 8 and 9 builds are trimming and Native AOT compatible.
- `Count`, span overloads of `IsMatch` and `Count`, `EnumerateMatches` (.NET 8+) and `EnumerateSplits` (.NET 9+).
- `RegEx` and `FromRegex` parameters are marked as regex syntax for IDE colorization.

### Tooling
- **`Masterly.FluentRegex.Tool`**, a .NET tool with `convert`, `analyze`, `scan`, `explain`, `generate`, `trace` and `diagram` commands. `scan` finds risky regex literals in any C# codebase and fails CI on them.
- A showcase sample app in `samples/`.

## Quality

- The test suite grew from 224 to 586 tests, run on both .NET 8 and .NET 9.
- Generated fluent code is compiled with Roslyn and checked against the original regex on generated samples.
- The parser is checked against .NET's own group numbering, and every common pattern is checked for ReDoS and sample generation.
- The `[GeneratedRegex]` code fix is verified end to end with .NET's own regex source generator.
