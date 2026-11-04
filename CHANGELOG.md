# What's New in 3.2.0

A correctness release: more than 30 bugs found while every feature was run end to end for the new documentation
site at https://a7mdfre7at.github.io/Masterly.FluentRegex/. Some fixes change behavior that was wrong; they are
marked **Behavior change**. `Repeat.Optional` after a quantifier is the one breaking change: use `.Lazy` where you
used it to mean "lazy".

## Fixed

### Core
- **Behavior change:** `CommonPatterns.PhoneNumber()` matched none of its documented formats (it required a bracketed area code followed by a second one). It now matches `(555) 123-4567`, `555-123-4567`, `555.123.4567` and `5551234567`.
- **Behavior change:** `CommonPatterns.Time12()` accepted hours 0-19 and rejected `Pm`. It now accepts hours 1-12 and AM/PM in any case, as documented.
- **Behavior change:** `IsValid()` and `TryValidate()` validate with the pattern's own options, so they accept valid `IgnorePatternWhitespace` patterns and reject `NonBacktracking` patterns the engine cannot run.
- **Behavior change:** under `RegexOptions.IgnorePatternWhitespace` (and inside `InlineOptions(IgnorePatternWhitespace, ...)`), `Literal` escapes whitespace and `#`, so it still matches its own text. Patterns without the option render as before.
- **Behavior change:** `a | b` keeps the options and timeout of the left operand, like `a + b`, instead of resetting them.
- **Behavior change:** `Repeat.Optional` directly after another quantifier makes the repetition optional, `(?:\d+)?`, instead of making it lazy (`\d+?`). Use `.Lazy` for a lazy quantifier.
- `Explain()` describes conditionals, `\A`, `\z`, `\x41`, `\u00E9`, `\p{Lu}`, octal escapes, inline options, comments, quoted and balancing groups, lazy `??`, escaped `]` in sets and multi-digit backreferences correctly, treats a `{` that is not a quantifier as a literal, and describes `\d` and `\w` as Unicode-aware unless `RegexOptions.ECMAScript` is set.
- `UnicodeBlock` accepts the `Is` prefix in any case and rejects unknown block names when the pattern is built.
- `AnyOf(null)` and `NoneOf(null)` throw `ArgumentNullException`; `IsEquivalentTo`, `IsSubsetOf` and `Overlaps` name `other` as the null parameter; `Literal(params char[])` names its parameter `characters`.
- The regex parser reads `\101`, and `\12` with fewer than 12 groups, as octal escapes, as .NET does. `ToFluentCode()` no longer emits `Backreference(101)` for them.

### Extraction, streaming and tracing
- `Extract<T>`, `ExtractAll<T>` and `Template<T>.Parse` set the properties of structs (they were lost on a boxed copy) and accept structs that have only settable properties.
- **Behavior change:** when redaction hits overlap, the part of the losing hit that reaches past the winner is masked too, instead of staying visible (`abcd` with `bcd` and `abc` was `***d`, now `****`).
- `Template<T>.Format` throws `ArgumentException` for a negative `TimeSpan` under a custom format, which cannot write a sign, instead of writing it as positive.
- **Behavior change:** `Validators.Luhn`, `IbanChecksum`, `IsoDate` and `IPv4` read any Unicode decimal digit by its value, as the default `\d` does, so a card number in Arabic-Indic digits passes `CreditCard().Must(Validators.Luhn)`.
- Template number placeholders accept the Arabic decimal separator `٫`, so `{Amount}` reads `١٢٫٥` as 12.5.
- Enumerating the result of `Matches(TextReader)` or `MatchesAsync` a second time throws `InvalidOperationException` instead of silently continuing in the middle of the input.
- `TraceFailure.MatchedSteps` lists the constructs before a group in which matching failed, instead of being empty, and the report escapes control characters on the `Found:` line.

### Analysis and translation
- `AnalyzeSafety()` reports no issues for `NonBacktracking` patterns, and no longer suggests a possessive quantifier for adjacent repetitions over the same characters, which would break the pattern.
- Every inexact `ToLike` result has a warning, including a case-sensitive pattern under `LikeCollation.CaseInsensitive`.
- Generating samples from a class with no usable character, such as `\p{Cs}`, explains the problem in the exception message.

### Source generator and analyzers
- A null-forgiving `!` or parentheses around a `[GeneratePattern]` body no longer crash the generator, and `TimeSpan.Parse` in a constant chain reports `FRX001` instead of crashing the generator or the analyzer. An unexpected evaluation failure affects only that member.
- `FRX001`, `FRX002` and `FRX009` can be configured in `.editorconfig` and suppressed with `#pragma`.
- A `NonBacktracking` chain with an unsupported construct reports `FRX003` instead of crashing the analyzer (`AD0001`).
- `FRX007` is no longer reported after its code fix was applied, on `[GeneratePattern]` members, or inside a non-partial outer type.
- `FRX008` says what happens at run time (extracting throws, or the value is always null), the generated `[GeneratedCode]` carries the package version, and diagnostic help links point to the documentation site.

### Integrations and tools
- EF Core: `Replace` reads `$12` and `$02` like .NET (whole number; literal `$` when it names no group).
- EF Core: `FluentRegexEventId.TranslationWarning` is marked `[Obsolete]`. It was never raised, because every SQL translation is exact or refused.
- ASP.NET Core: `AddFluentPatterns` registers the `pattern` route constraint even when the app registered its own `PatternRegistry` first.
- xUnit: `[MatchingData]` and `[NonMatchingData]` with a `null` type or a blank member name fail in `GetData` with a message xUnit shows, instead of in the constructor, where xUnit v3 reported only "No data found".
- CLI: the help lists every exit code 1 case, `convert --file` counts skipped literals separately, and the profiler says "1 step per input character".


# What's New in 3.1.1

A packaging and documentation release. There are no code or API changes.

## Changed
- **New brand:** every package now ships the new FluentRegex icon, and the NuGet readme shows the new banner.
- **Documentation site:** the full documentation now lives at https://a7mdfre7at.github.io/Masterly.FluentRegex/, with
  run-verified examples for every feature, the API reference and the upgrade guide. The browser playground is
  published next to it at https://a7mdfre7at.github.io/Masterly.FluentRegex/playground/.
- **README:** shortened to an introduction that points to the documentation site. The NuGet readme links to the site
  instead of README sections.


# What's New in 3.1.0

FluentRegex 3.1 proves things about patterns, learns them from examples, and takes them into
browsers, APIs, test suites and logs. There are no breaking changes to the 3.0 API, and the core
package has no new dependencies.

## Added

### Proving and Improving
- **Pattern algebra:** `Pattern.Compare`, `IsEquivalentTo`, `IsSubsetOf`, `Overlaps`, `MatchesNothing`, `ShortestMatch`, `Intersect`, `Except` and `Union`. Patterns are compiled to minimized automata over exact Unicode character sets; every counterexample is verified with the .NET engine. Lookaheads at the start of a pattern are supported; other non-regular constructs throw `NotSupportedException` naming the construct.
- **Verified optimizer:** `Optimize()` merges alternations into classes, factors common prefixes, removes duplicate branches, normalizes quantifiers, merges repeated atoms and unwraps redundant groups. Each rewrite is checked match-by-match and, for regular patterns, proven equivalent.
- **Learn from examples:** `Pattern.Learn(match, reject)` infers a readable, anchored pattern.
- **Language facts:** `GetLanguageFacts(MatchScope)` reads the shortest and longest accepted lengths, the text every match must start and end with, and whether the empty string is accepted from the pattern's minimal automaton. The facts are sound by construction; patterns outside the regular subset get a sound syntactic approximation.
- **Pattern equality:** `Pattern` implements `IEquatable<Pattern>`; two patterns are equal when they render the same regex with the same options and timeout, so patterns work as dictionary keys and in query caches.

### Testing
- **Regex coverage:** `MeasureCoverage(inputs)` and the thread-safe `TrackCoverage()` recorder report alternation branches, optional parts and upper bounds that inputs never exercise.
- **Property testing:** `PatternProperty.ForMatching` and `ForNonMatching` run a property over generated samples and shrink failures to a minimal counterexample.
- **Backtracking profiler:** `Profile(input)` counts steps and backtracks per construct with a step budget, cross-checks the result with the .NET engine and prints a heatmap.
- **Highlighting:** `Highlight(text, HighlightFormat.Ansi | Html | Brackets)`; HTML output is fully encoded.
- **Debugger view:** `Pattern` shows its regex in the debugger, with options, groups, explanation and safety verdict in the expanded view.

### Parsing and Validation
- **Typed templates:** `Template.Of<T>("{Date:yyyy-MM-dd} [{Level}] {Message}")` with `Parse`, `TryParse`, `ParseAll` and a round-tripping `Format`. Placeholders are typed by member: numbers, enums, Guids, booleans, dates and times with custom formats, and `re:` regex formats.
- **Semantic rules:** `Must(rule)` and `Must(group, rule)` return a `ConstrainedPattern` with `IsMatch`, `Matches`, `IsValid` and a diagnostic `Check`. `Validators.Luhn`, `IbanChecksum`, `IsoDate` and `IPv4` are included.
- **DataAnnotations:** `[MatchesPattern(typeof(Patterns), nameof(Patterns.Sku))]` (.NET 8 and 10).
- **Redaction:** `Redactor` with `Mask`, `Email`, `KeepFirst`, `KeepLast`, `Label` and `Custom` styles. Reports list redacted spans without the original values.
- **Streaming:** `Matches(TextReader)`, `MatchesAsync(TextReader)` and `MatchesAsync(Stream)` return the same matches as the whole text within documented length limits, in bounded memory.

### Unicode
- `Script(UnicodeScript)`, `LetterIn`, `DigitIn`, `AsciiDigit`, `CodePoint`, an approximate `Grapheme` and `IgnoreDiacritics()` (for example, "محمد" matches "مُحَمَّد" and "cafe" matches "café").
- **Digit mode:** every `CommonPatterns` method has a `DigitMode` overload, such as `CommonPatterns.IPv4(DigitMode.Ascii)`, and any pattern can switch with `WithAsciiDigits()` or `WithDigits(DigitMode)`. `DigitMode.Ascii` rewrites `\d` and `\D` everywhere, including inside sets and subtractions, so they mean exactly 0-9 and everything else. The overloads work in `[GeneratePattern]` chains, the CLI accepts `--ascii-digits`, and the playground has an "ASCII digits only" toggle.

### Export
- `ToFlavor(RegexFlavor.JavaScript | JsonSchema)`, `ToJavaScript()` and `PatternExport.ToTypeScript`. Translations keep the .NET meaning of `\d`, `\w`, `\s`, `\b`, `$` and `.`, expand class subtraction and Unicode blocks, and report every remaining difference as a warning. Constructs JavaScript cannot express, such as atomic groups, throw `NotSupportedException` listing each one.
- **SQL regex dialects:** `ToFlavor(RegexFlavor.PostgreSql | MySql | MariaDb | SqlServer | Sqlite)` and `ToSqlCondition(columnSql, flavor)` translate a pattern for the database's own regex engine with the exact .NET meaning: case folding, `\d`, `\w`, `\s`, `\b`, `$` and `.` are written out explicitly, `FlavorConversion.GroupNumbers` maps .NET groups to the engine's numbering, and anything an engine cannot express throws. `FlavorConversion.MatchExtentWarnings` and `PreservesMatchExtent` report when an engine would match a different substring or capture different text than .NET (PostgreSQL's longest-match preference, SQL Server's `$`), even where it accepts the same strings. Case-insensitive patterns are translated under the current culture, as .NET matches them, unless `RegexOptions.CultureInvariant` is set. SQL Server literals are `N'...'`, and patterns longer than its 4,000-character limit throw instead of being cut. The output was verified against live PostgreSQL 17, SQL Server 2025, MySQL 8.4 and MariaDB 11.4 servers, about 393,000 checks across their collations, with no disagreements for Basic Multilingual Plane input.
- **LIKE with proof:** `ToLike(LikeDialect, MatchScope, LikeCollation)` compiles a pattern to the tightest `LIKE` pattern that accepts every match (SQL Server sets such as `[0-9]` included) and uses language algebra to prove whether it is exact. The superset holds for every input, including emoji, which databases count as one character where .NET counts two, and under case-insensitive collations. `LikeConversion.ToSql(columnSql)` writes the condition with the right `ESCAPE` clause.

### New Packages
- **Masterly.FluentRegex.AspNetCore:** `AddFluentPatterns(...)` and the `pattern(name)` route constraint.
- **Masterly.FluentRegex.Xunit:** `[MatchingData]` and `[NonMatchingData]` theory data for xUnit v3.
- **Masterly.FluentRegex.AI:** `PatternAssistant` drafts a pattern from a description with Claude, verifies syntax, safety and your examples locally, and retries with precise feedback.
- **Masterly.FluentRegex.EntityFrameworkCore:** `UseFluentRegex()` on any `DbContextOptionsBuilder`. `EF.Functions.Matches(column, pattern)`, `Group` and `Replace` translate to the regex operator of PostgreSQL, MySQL, MariaDB, SQL Server 2025 or SQLite with the exact .NET meaning, with .NET group numbers mapped to the engine's, and `Group`/`Replace` only where the engine matches the same text as .NET; SQL Server before 2025 gets a `LIKE` that language algebra proved exact (or an opt-in proven superset), compared under `COLLATE Latin1_General_100_BIN2` so that linguistic collations cannot change its meaning (`UseLikeCollation(LikeCollation.Binary)` drops the `COLLATE` for binary columns); MySQL works through Pomelo or, on EF Core 10, Oracle's `MySql.EntityFrameworkCore`; every regex condition gets a proven length bound and, where the collation compares `LIKE` character by character, a proven `LIKE` prefix, so B-tree indexes stay usable; `MatchingAsync` runs a proven-superset prefilter in SQL and the exact check in .NET for patterns the database cannot run; `HasPattern` and `[MatchesPattern]` become `CHECK` constraints in migrations. Patterns that would silently change meaning are rejected at query translation, never run.

### Platforms
- **.NET 10:** the core, AspNetCore, Xunit and AI packages now also target .NET 10 (trimming and Native AOT compatible). The test suite runs on .NET 8 and .NET 10.
- **.NET 9 target removed:** the packages no longer ship a `net9.0` build. .NET 9 apps keep working on the .NET 8 build; only the span `EnumerateSplits` overload needs .NET 10.

### Tooling
- New CLI commands: `compare`, `optimize`, `learn`, `profile`, `highlight`, `coverage`, `export` and `facts`. `export --to` accepts `postgresql`, `mysql`, `mariadb`, `sqlserver`, `sqlite` and `like` (with `--dialect`, `--scope`, `--case-insensitive` and `--column`). The CLI now writes UTF-8 output.
- A browser playground (`playground/`) running the library in WebAssembly, with shareable links and a GitHub Pages workflow. Its Export tab shows the regex condition, the proven `LIKE` and the language facts for each database.

## Fixed
- `Extract<T>` and templates parse numbers written with any Unicode decimal digits, such as Arabic-Indic "٢٠٢٦", which `\d` already matched.

## Upgrade Notes
- The new public `Masterly.FluentRegex.Validators` class can clash with a `Validators` type in your own code when both namespaces are imported. Use a using alias to resolve it.
- `\d` matches every Unicode decimal digit, so the parameterless `CommonPatterns` methods accept digits such as "٩" (for example, `IPv4()` accepts "0.0.0.٩"). This is unchanged from 3.0. Pass `DigitMode.Ascii` or call `WithAsciiDigits()` when only 0-9 are valid.
- `Pattern.Equals` now compares the rendered regex, options and timeout; before 3.1 it compared references, so `Pattern.With.Digit.Equals(Pattern.With.Digit)` was `false`. This changes how patterns behave as `HashSet` and `Dictionary` keys. The `==` operator still compares references.

---

# What's New in 3.0.0

FluentRegex 3.0 goes beyond building patterns: it now helps you understand, test, secure and ship them.

3.0.0 was not published to NuGet. NuGet users upgrade from 2.0.0 straight to 3.1.0 or later, so these notes apply to them too.

## Breaking Changes

See [Upgrading from 2.x](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/upgrading-from-2x) for details.

- **Patterns are immutable.** Every member returns a new `Pattern`. Code that called a member as a statement and ignored the result, such as `pattern.WithOptions(...);`, must assign the result.
- **`Literal` inside a `Set` escapes `-`.** `Set(Literal("a-f"))` now matches `a`, `-` and `f`. Use the new `Range('a', 'f')` for ranges.
- **`Letter`, `LowercaseLetter` and `UppercaseLetter` outside a `Set`** now render `[a-zA-Z]`, `[a-z]` and `[A-Z]` instead of literal text.
- **`Repeat.AtMost(n)`** now renders `{0,n}`. .NET treated the old `{,n}` as literal text.
- **`ToRegex()` without arguments** now uses the pattern's options and timeout. This takes effect when you recompile: an assembly built against 2.x still calls the old `ToRegex(RegexOptions)` overload with `None`.
- **Invalid builder calls throw.** `Repeat` on an empty pattern throws `InvalidOperationException`, and negative or reversed bounds throw `ArgumentOutOfRangeException`. `null` arguments throw `ArgumentNullException`; in 2.x, `Group(null)` gave `()` and `Set(null)` gave `[]`. A constant chain with such an error now fails the build with FRX006 or FRX003.
- `GeneratePackageOnBuild` is off; pack with `dotnet pack`.

## Fixed

- The compiled-regex cache could go stale when a pattern was extended after matching. Immutability makes this impossible.
- `Repeat.AtMost` never matched as intended.
- `Letter` outside a set matched the literal text `a-zA-Z`.
- Literal hyphens inside sets silently created ranges. In 2.x, `CommonPatterns.StrongPassword()` treated every digit as a special character because of the range `+` to `=`.
- `CommonPatterns.Url()` allowed polynomial backtracking through dots in the first host label. The fix also changes which URLs match: hyphens in paths are accepted (`https://example.com/my-page`), while `<`, `>`, `{` and `}` in paths and empty host labels (`https://a..b.com`) are rejected.
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
