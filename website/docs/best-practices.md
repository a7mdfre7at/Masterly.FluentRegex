---
id: best-practices
title: "Best Practices and Performance"
description: "Write readable patterns, check them before they ship, keep matching fast, and guard untrusted input with timeouts, the non-backtracking engine and compile-time generation."
---

This page collects the habits that keep FluentRegex code readable, fast and safe. Most of them are small: store a pattern in a field instead of rebuilding it, name the groups you read, anchor validators, and give every pattern that sees user input a time budget. Read it once when you start using the library, and again before a pattern goes into a hot path or faces untrusted input.

Every claim about caching and performance here comes from the library source, not from rules of thumb. Where the guidance differs from older examples in the README, the page says so.

All samples use the `Masterly.FluentRegex` namespace. `Regex`, `RegexOptions` and `Match` come from `System.Text.RegularExpressions`.

:::tip[The short version]

| Do this | Why |
|---------|-----|
| Store patterns in `static readonly` fields | Each pattern instance builds and caches its own `Regex` on first use. |
| Name the groups you read; use non-capturing groups for the rest | Names survive edits; unused captures cost time and shift group numbers. |
| Anchor validators with `StartOfInput` and `EndOfInput` | `IsMatch` searches anywhere, and `$` accepts a trailing newline. |
| Pass user text through `Literal`, never `RegEx` | `Literal` escapes special characters. |
| Add `WithTimeout` to every pattern that sees untrusted input | The default timeout is infinite, and the process-wide default does not apply. |
| Use `UseNonBacktracking()` when the pattern allows it (.NET 8+) | Matching time becomes linear in the input length. |
| Use `[GeneratePattern]` or a `[GeneratedRegex]` method for constant patterns | The chain is evaluated at compile time; `[GeneratedRegex]` also emits the matching code at build time, which suits Native AOT. |
| Run `AnalyzeSafety()` in a unit test | A ReDoS risk fails the build instead of a production server. |

:::

## Writing readable patterns

### Start simple, then refine

Write the smallest pattern that captures the idea, check it against a few inputs, then tighten it. A fluent chain is easy to extend, so there is no reason to write the final version in one go.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

// First draft: the shape of an email address
Pattern email = Pattern.With
    .Word.Repeat.OneOrMore
    .Literal("@")
    .Word.Repeat.OneOrMore;

Console.WriteLine(email);                                  // \w+@\w+
Console.WriteLine(email.Match("ada@example.com").Value);   // ada@example

// Refined: allowed characters, a top-level domain, and anchors
Pattern emailBetter = Pattern.With
    .StartOfInput
    .Set(Pattern.With.Letter.Digit.Literal("._+-")).Repeat.OneOrMore
    .Literal("@")
    .Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore
    .Literal(".")
    .Letter.Repeat.AtLeast(2)
    .EndOfInput;

Console.WriteLine(emailBetter);
// prints: \A[a-zA-Z\d\._\+\-]+@[a-zA-Z\d\.\-]+\.[a-zA-Z]{2,}\z

Console.WriteLine(emailBetter.IsMatch("ada.lovelace+news@example.com"));  // True
Console.WriteLine(emailBetter.IsMatch("ada@example"));                    // False
```

- The first draft matches only `ada@example` inside `ada@example.com`, because `\w` does not include the dot. Printing `Match(...).Value` shows such gaps quickly.
- `Literal("._+-")` inside `Set(...)` is escaped for you, so `-` is a literal dash, not a range.
- For an email address in real code, start from `CommonPatterns.Email()` instead (see [Common Patterns](./common-patterns.md)).

### Name and reuse building blocks

A pattern is immutable, so a piece stored in a field can be reused in many larger patterns without being changed by them. Give each piece a name from your domain, and the final pattern reads like a sentence.

```csharp
public static class InvoicePatterns
{
    private static readonly Pattern Year = Pattern.With.Digit.Repeat.Times(4);
    private static readonly Pattern TwoDigits = Pattern.With.Digit.Repeat.Times(2);

    private static readonly Pattern IsoDate = Year.Literal("-").Then(TwoDigits).Literal("-").Then(TwoDigits);
    private static readonly Pattern InvoiceNumber = Pattern.With.Literal("INV-").Then(Year).Literal("-").Digit.Repeat.Times(4);

    public static readonly Pattern InvoiceLine = Pattern.With
        .StartOfInput
        .NamedGroup("number", InvoiceNumber)
        .Whitespace
        .NamedGroup("date", IsoDate)
        .EndOfInput;
}

Console.WriteLine(InvoicePatterns.InvoiceLine);
// prints: \A(?<number>INV-\d{4}-\d{4})\s(?<date>\d{4}-\d{2}-\d{2})\z

Console.WriteLine(InvoicePatterns.InvoiceLine.IsMatch("INV-2026-0042 2026-11-03"));  // True
```

- `Year.Literal("-")` returns a new pattern. `Year` itself is unchanged, so `InvoiceNumber` can use it too.
- `Then(...)` appends a whole pattern. The `+` operator does the same. See [Composing Patterns](./composing-patterns.md).
- Declare the pieces before the patterns that use them. Static fields are initialized in order, and a piece declared later is still `null` when it is read.

### Set options and timeouts on the final pattern

Options and the timeout belong to the pattern you run, not to its pieces. When you compose patterns, the settings of a piece are dropped:

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore.WithTimeout(TimeSpan.FromMilliseconds(100));

Pattern extended = digits.Literal("!");
Pattern composed = Pattern.With.Literal("#").Then(digits);
Pattern alternated = digits | Pattern.With.Letter.Repeat.OneOrMore;

Console.WriteLine(extended.Timeout.TotalMilliseconds);                  // 100
Console.WriteLine(composed.Timeout == Regex.InfiniteMatchTimeout);    // True
Console.WriteLine(alternated.Timeout == Regex.InfiniteMatchTimeout);  // True
```

- Extending a pattern (`digits.Literal(...)`) keeps its options and timeout.
- `Then(...)`, `+`, `Group(...)`, `NamedGroup(...)` and the other members that take a pattern keep the settings of the outer pattern, the one you call the member on. The inner pattern's options and timeout are ignored.
- The `|` operator starts from `Pattern.With`, so its result has no options and no timeout, whatever the two sides had.
- Call `WithOptions`, `WithTimeout` and `UseNonBacktracking` last, on the pattern you store and run.
- When only one piece must ignore case, use the inline scope instead of an option: `Pattern.With.Literal("id-").IgnoreCase(Pattern.With.Literal("abc"))` renders `id-(?i:abc)`, which matches `id-ABC` but not `ID-abc`.

### Use named groups for the parts you extract

Group numbers change when someone adds a group in front. Names do not.

```csharp
// Numbered: you must remember that 1 is the area code and 2 is the number
Pattern numbered = Pattern.With
    .Group(Pattern.With.Digit.Repeat.Times(3))
    .Literal("-")
    .Group(Pattern.With.Digit.Repeat.Times(4));

// Named: the code that reads the match says what it reads
Pattern phone = Pattern.With
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .Literal("-")
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4));

Match match = phone.Match("call 555-0199 now");

Console.WriteLine($"{match.Groups["area"].Value} {match.Groups["number"].Value}");  // 555 0199
Console.WriteLine(string.Join(", ", phone.GetGroupNames()));                         // area, number
```

- Named groups are also what [Typed Extraction](./typed-extraction.md) binds to properties. The analyzer reports FRX008 when an `Extract<T>` target property has no group with its name.
- More on groups and backreferences: [Groups and Backreferences](./groups-and-backreferences.md).

### Use non-capturing groups for everything else

When you group only to apply a quantifier or an alternation, do not capture. A capture costs work on every match, and it adds a group number that shifts every group after it.

```csharp
Pattern capturing = Pattern.With
    .Group(Pattern.With.Literal("http").Literal("s").Repeat.Optional)
    .Literal("://");

Pattern nonCapturing = Pattern.With
    .NonCapturingGroup(Pattern.With.Literal("http").Literal("s").Repeat.Optional)
    .Literal("://");

Pattern oneOf = Pattern.With.OneOf("http", "https").Literal("://");

Console.WriteLine($"{capturing} {capturing.GetGroupCount()}");        // (https?):// 1
Console.WriteLine($"{nonCapturing} {nonCapturing.GetGroupCount()}");  // (?:https?):// 0
Console.WriteLine($"{oneOf} {oneOf.GetGroupCount()}");                // (?:http|https):// 0
```

- `OneOf(...)` and the `|` operator build a non-capturing alternation. `Choice(...)` builds a capturing one.
- `GetGroupCount()` does not count group 0, the whole match.

### Anchor validation patterns to the whole input

`IsMatch` returns `true` when the pattern matches **anywhere** in the input. A validator must say that the match covers the whole input.

```csharp
Pattern digitsLoose = Pattern.With.Digit.Repeat.OneOrMore;
Pattern digitsLine = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;
Pattern digitsInput = Pattern.With.StartOfInput.Digit.Repeat.OneOrMore.EndOfInput;

Console.WriteLine(digitsLoose.IsMatch("abc123"));  // True
Console.WriteLine(digitsLine.IsMatch("123\n"));    // True
Console.WriteLine(digitsInput.IsMatch("123\n"));   // False
```

- Without anchors, `abc123` passes a "digits only" check.
- `EndOfLine` renders `$`, which also matches just before a final newline. `EndOfInput` renders `\z`, which matches only at the very end. Use `StartOfInput` and `EndOfInput` for validators.
- See [Assertions](./assertions.md) for every anchor.

### Use CommonPatterns, but know what they accept

`CommonPatterns` has ready-made, tested patterns for emails, URLs, IP addresses, GUIDs, dates and more. Use them instead of writing your own. Read their page first, because each one makes choices you may not share:

```csharp
Console.WriteLine(CommonPatterns.Email().IsMatch("ada@example.com\n"));  // True
```

- Every `CommonPatterns` method except `HtmlTag()` and `MarkdownLink()` is anchored with `^` and `$`, so a trailing newline is accepted. Trim input from forms and files, or validate with a constrained pattern (see [Semantic Rules](./semantic-rules.md)).
- Every call such as `CommonPatterns.Email()` builds a **new** pattern, with its own empty `Regex` cache. Call it once and store the result. See [Store patterns in static readonly fields](#store-patterns-in-static-readonly-fields).

### Treat user text as a literal

When a pattern includes text from a user, a file or a database, add it with `Literal`, which escapes every special character. `RegEx` adds raw regex text and interprets it.

```csharp
// userText comes from your application, for example a search box
string userText = "1+1=2 (really?)";

Pattern safeSearch = Pattern.With.Literal(userText);
Pattern unsafeSearch = Pattern.With.RegEx(userText);

Console.WriteLine(safeSearch);  // 1\+1=2 \(really\?\)
Console.WriteLine(safeSearch.IsMatch("I said 1+1=2 (really?) twice"));    // True
Console.WriteLine(unsafeSearch.IsMatch("I said 1+1=2 (really?) twice"));  // False
```

:::warning

Never pass user input to `RegEx(...)` or `Pattern.FromRegex(...)`. A user can then inject quantifiers and groups, change what the pattern matches, or send a pattern built to backtrack for minutes. If users really must supply a regex, validate it, run `AnalyzeSafety()` on it, and run it only with a timeout or the non-backtracking engine.

:::

## Checking patterns before they ship

### Let the build check constant patterns

The analyzers inside the core package evaluate every fluent chain built from constants while you type, and report problems as build diagnostics. You get these checks without writing any code:

| ID | Severity | Reported when |
|----|----------|---------------|
| FRX003 | Error | The chain produces an invalid regex, for example `RegEx("(abc")`. |
| FRX004 | Warning | The pattern can backtrack exponentially (ReDoS). |
| FRX005 | Info | The pattern can backtrack polynomially. |
| FRX006 | Error | Building the chain throws, for example `.Lazy` after an element that is not quantified. |
| FRX007 | Info | A static, constant pattern in a `partial` type can be backed by a `[GeneratedRegex]` method. Reported only when your project targets .NET 7 or later. |
| FRX008 | Warning | An `Extract<T>` target property has no matching group. |

For example, this line does not compile:

```csharp
Pattern unbalanced = Pattern.With.RegEx("(abc");
```

```text
error FRX003: The pattern produces an invalid regular expression '(abc': Invalid pattern '(abc' at offset 4. Not enough )'s. (https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/compile-time-checks)
```

- Treat FRX004 as a bug, not as noise. Consider `<WarningsAsErrors>FRX004</WarningsAsErrors>` in your project file.
- The analyzers see only chains whose values are known at compile time. Text loaded at run time needs the checks below.
- The full list, including the `[GeneratePattern]` diagnostics, is on [Compile-Time Checks](./compile-time-checks.md).

### Validate patterns you load at run time

When regex text comes from configuration or a database, check it once at startup with `TryValidate`, and fail with a clear message instead of an exception on the first request.

```csharp
// regexText comes from your configuration, for example "(abc"
Pattern custom = Pattern.With.RegEx(regexText);

if (!custom.TryValidate(out string error))
{
    Console.WriteLine($"Invalid pattern: {error}");
}
// prints: Invalid pattern: Invalid pattern '(abc' at offset 4. Not enough )'s.
```

- `IsValid()` answers the same question without the message.
- Both compile the regex text **without** the pattern's options. A pattern that is valid text but uses a construct the non-backtracking engine rejects still passes. See [Use the non-backtracking engine](#use-the-non-backtracking-engine).
- Both build a new `Regex` on every call. Validate once, not on every request.
- More on validation: [Validation and Debugging](./validation-and-debugging.md).

### Explain and document complex patterns

`Explain()` turns a pattern into an indented, plain-English description. Paste it into a comment or a pull request so reviewers can check the intent.

```csharp
// phone is the named-group pattern from "Use named groups for the parts you extract"
Console.WriteLine(phone.Explain());
// prints:
// Pattern: (?<area>\d{3})-(?<number>\d{4})
//
// Explanation:
//   - Named capturing group 'area'
//     - Digit (any Unicode decimal digit)
//     - Exactly 3 times
//     - End of group
//   - Character '-'
//   - Named capturing group 'number'
//     - Digit (any Unicode decimal digit)
//     - Exactly 4 times
//     - End of group
```

To see a pattern as a picture, draw a railroad diagram (see [Diagrams](./diagrams.md)). To see what the engine actually does on an input, use [Match Tracing](./match-tracing.md).

### Test patterns with generated data

Hand-picked test inputs miss the cases you did not think of. Let the library generate them:

- [Test Data Generation](./test-data-generation.md) produces strings that match, and near misses that do not.
- [Regex Coverage](./regex-coverage.md) shows which branches and quantifiers your tests never exercised.
- [Property Testing](./property-testing.md) compares a pattern against a reference validator on generated input.
- [xUnit Integration](./xunit.md) turns generated matches and near misses into theory data.
- [Pattern Algebra](./pattern-algebra.md) proves that a refactored pattern accepts exactly the same strings as the old one.

A small test class that guards a pattern:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;
using Masterly.FluentRegex.Xunit;
using Xunit;

public static class OrderPatterns
{
    public static readonly Pattern OrderNumber = Pattern.With
        .StartOfInput
        .Literal("ORD-")
        .Digit.Repeat.Times(6)
        .EndOfInput
        .WithTimeout(TimeSpan.FromMilliseconds(100));
}

public class OrderPatternTests
{
    [Fact]
    public void OrderNumber_HasNoBacktrackingRisk()
    {
        Assert.True(OrderPatterns.OrderNumber.AnalyzeSafety().IsSafe);
    }

    [Fact]
    public void OrderNumber_HasATimeout()
    {
        Assert.NotEqual(Regex.InfiniteMatchTimeout, OrderPatterns.OrderNumber.Timeout);
    }

    [Theory]
    [MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.OrderNumber))]
    public void OrderNumber_AcceptsEveryGeneratedMatch(string value)
    {
        Assert.True(OrderPatterns.OrderNumber.IsMatch(value));
    }

    [Theory]
    [NonMatchingData(typeof(OrderPatterns), nameof(OrderPatterns.OrderNumber))]
    public void OrderNumber_RejectsEveryGeneratedNearMiss(string value)
    {
        Assert.False(OrderPatterns.OrderNumber.IsMatch(value));
    }
}
```

- `[MatchingData]` and `[NonMatchingData]` come from the `Masterly.FluentRegex.Xunit` package (xUnit v3, `net8.0` and `net10.0`). Each generates 25 rows by default, so this class runs 52 tests.
- In a real suite, the theories would call your validator instead of the pattern itself. The point is that the inputs are generated from the pattern.

## Performance

### How a pattern caches its Regex

The expensive part of a regex is not matching; it is parsing and compiling the regex text into a `Regex` object. A `Pattern` does this once:

- The first call to a matching method creates a `Regex` with the pattern's options and timeout, and stores it **in that pattern instance**.
- Later calls on the same instance reuse it. Because a pattern is immutable, the cached `Regex` can never go stale.
- The regex text from `ToString()` is cached the same way.

Not every member uses the cache:

| Uses the cached `Regex` | Builds a new `Regex` on every call |
|-------------------------|-----------------------------------|
| `IsMatch(string)`, `IsMatch(ReadOnlySpan<char>)` | `IsMatch(string, RegexOptions)` |
| `Match`, `Matches`, `Count`, `EnumerateMatches`, `EnumerateSplits` | `ToRegex()`, `ToRegex(RegexOptions)` |
| `Replace`, `Split` and fluent replacements | `IsValid()`, `TryValidate(...)` |
| `Extract<T>`, `TryExtract<T>`, `ExtractAll<T>` | `GetGroupNames()`, `GetGroupCount()` |
| Streaming scanners and highlighting | |

- Analysis members such as `AnalyzeSafety()` and `Explain()` do not use the cache either. They parse the regex text again on every call.
- The cache belongs to the instance, not to the regex text. Two patterns that are `Equals` still build two `Regex` objects.
- Patterns run on their own `Regex` instances, not through the static `Regex.IsMatch(input, pattern)` methods, so `Regex.CacheSize` has no effect on them.
- A [Template](./templates.md) caches its own anchored pattern the same way. Store templates in fields too.

### Store patterns in static readonly fields

Build each pattern once and keep it. A method that rebuilds its pattern on every call pays for a new `Regex` on every call.

```csharp
public static class Contacts
{
    private static readonly Pattern Email = CommonPatterns.Email()
        .WithTimeout(TimeSpan.FromMilliseconds(100));

    private static readonly Pattern Phone = Pattern.With
        .Digit.Repeat.Times(3)
        .Literal("-")
        .Digit.Repeat.Times(4)
        .WithOptions(RegexOptions.Compiled);

    public static bool IsValidEmail(string value) => Email.IsMatch(value);

    public static int CountPhones(string text) => Phone.Count(text);
}

Console.WriteLine(Contacts.IsValidEmail("ada@example.com"));                  // True
Console.WriteLine(Contacts.CountPhones("call 555-0199 or 555-0100"));        // 2
```

Avoid these shapes on a hot path:

```csharp
// value comes from your application
// Each call builds a new pattern, so each call parses and compiles a new Regex
bool slowEmail = CommonPatterns.Email().IsMatch(value);
bool slowDigits = Pattern.With.Digit.Repeat.OneOrMore.IsMatch(value);
```

- In dependency-injected code, a singleton service holding the pattern in a `readonly` field works just as well as a `static readonly` field.
- `CommonPatterns` methods are methods, not cached properties. Every call returns a new pattern with an empty cache.

### Hold the Regex from ToRegex

`ToRegex()` returns a **new** `Regex` each time. Call it once and keep the result when an API needs a `Regex`.

```csharp
Pattern cached = Pattern.With.Digit.Repeat.OneOrMore;

Console.WriteLine(ReferenceEquals(cached.ToRegex(), cached.ToRegex()));  // False

Regex held = cached.ToRegex();  // keep this one in a field
```

The same applies to `IsMatch(input, options)`. When you always match with the same extra options, put them on the pattern instead:

```csharp
// Builds a new Regex on every call
bool once = Pattern.With.Literal("abc").IsMatch("ABC", RegexOptions.IgnoreCase);  // true

// Builds it once, then reuses it
Pattern caseInsensitive = Pattern.With.Literal("abc").WithOptions(RegexOptions.IgnoreCase);
bool reused = caseInsensitive.IsMatch("ABC");  // true
```

### Use RegexOptions.Compiled for hot patterns

`RegexOptions.Compiled` turns the regex into IL that the JIT compiles to machine code. Matching gets faster; creating the `Regex` gets slower. Because a stored pattern creates its `Regex` only once, the extra start-up cost is paid once.

```csharp
Pattern hot = Pattern.With.Digit.Repeat.OneOrMore.WithOptions(RegexOptions.Compiled);

Console.WriteLine(hot.Options);                                      // Compiled
Console.WriteLine(hot.ToRegex().Options);                            // Compiled
Console.WriteLine(hot.ToRegex(RegexOptions.IgnoreCase).Options);     // IgnoreCase
```

- Use `Compiled` for patterns that run many times in a long-lived process. Skip it for patterns that run a few times, such as in a command-line tool.
- `ToRegex(options)` **replaces** the pattern's options, so `Compiled` is gone in the last line. Combine them yourself: `hot.ToRegex(hot.Options | RegexOptions.IgnoreCase)` has `IgnoreCase, Compiled`.
- `WithOptions(...)` also replaces options, including a `NonBacktracking` flag added earlier.
- For constant patterns, a source-generated regex is better still. See [Build patterns at compile time](#build-patterns-at-compile-time).

### Prefer specific patterns over greedy wildcards

`Anything.Repeat.ZeroOrMore` (`.*`) first runs to the end of the line, then gives characters back one at a time until the rest of the pattern fits. A specific class or a lazy quantifier needs less backtracking. They are **not** drop-in replacements, though: they can find different matches.

```csharp
string text = "the end of the legend";

Pattern greedy = Pattern.With.Anything.Repeat.ZeroOrMore.Literal("end");
Pattern lazy = Pattern.With.Anything.Repeat.ZeroOrMore.Lazy.Literal("end");
Pattern notE = Pattern.With.NegatedSet(Pattern.With.Literal("e")).Repeat.ZeroOrMore.Literal("end");

Console.WriteLine($"{greedy}: '{greedy.Match(text).Value}'");  // .*end: 'the end of the legend'
Console.WriteLine($"{lazy}: '{lazy.Match(text).Value}'");      // .*?end: 'the end'
Console.WriteLine($"{notE}: '{notE.Match(text).Value}'");      // [^e]*end: ' end'
```

- Greedy runs to the **last** `end`; lazy stops at the **first**. They accept the same set of whole strings, but `Match` returns different text.
- `[^e]*end` accepts a different set of strings: it cannot match `eend`, and it can match across a newline. The library proves it:

```csharp
Console.WriteLine(Pattern.Compare(greedy, notE));
// prints: Not equivalent (the patterns overlap). Shortest counterexample: "eend" (left accepts it, right rejects it); also "\nend" (right accepts it, left rejects it). Both accept "end".

Console.WriteLine(greedy.IsEquivalentTo(lazy));  // True
```

When you make a pattern "more specific" for speed, prove that it still means the same thing with [Pattern Algebra](./pattern-algebra.md), or let [the optimizer](./optimizer.md) do the rewrite; it checks every rewrite before it applies it.

### Use atomic groups and possessive quantifiers correctly

An atomic group (`AtomicGroup(...)`, `(?>...)`) and a possessive quantifier (`.Possessive`) match as much as they can and **never give anything back**. That removes backtracking, but it also changes what the pattern can match. Use them only where the next element cannot match what the group consumed.

```csharp
// Good: digits can never be part of "px", so giving them back would never help
Pattern size = Pattern.With.Digit.Repeat.OneOrMore.Possessive.Literal("px");

Console.WriteLine($"{size} {size.IsMatch("120px")} {size.IsMatch("120")}");  // (?>\d+)px True False
```

```csharp
// Wrong: the second \w+ needs a character the atomic group already took
Pattern risky = Pattern.With.Word.Repeat.OneOrMore.Word.Repeat.OneOrMore;
Pattern broken = Pattern.With.AtomicGroup(Pattern.With.Word.Repeat.OneOrMore).Word.Repeat.OneOrMore;

Console.WriteLine($"{risky} {risky.IsMatch("hello")}");    // \w+\w+ True
Console.WriteLine($"{broken} {broken.IsMatch("hello")}");  // (?>\w+)\w+ False
```

:::warning

An older README example suggested `AtomicGroup(Word.OneOrMore)` followed by `Word.OneOrMore` as the "safe" form of `\w+\w+`. That pattern **never matches anything**: the atomic group takes every word character, and the second `\w+` has nothing left. The real fix for `\w+\w+` is to remove the redundant repetition: `Word.Repeat.AtLeast(2)` (`\w{2,}`) is provably equivalent and has risk `None`. `AnalyzeSafety()` rates the original `Medium` (polynomial), not exponential.

:::

- `.Possessive` renders the equivalent atomic group, because .NET has no possessive syntax. `Digit.Repeat.OneOrMore.Possessive` and `AtomicGroup(Digit.Repeat.OneOrMore)` are equal patterns.
- `.Possessive` must directly follow a quantifier, or it throws `InvalidOperationException` (FRX006 at build time).
- Atomic groups are not supported by the non-backtracking engine, and pattern algebra cannot compare patterns that contain them.
- See [Quantifiers](./quantifiers.md) for greedy, lazy and possessive modes.

### Use the span APIs and Count on hot paths

On .NET 8 and later, `IsMatch(ReadOnlySpan<char>)`, `Count(ReadOnlySpan<char>)` and `EnumerateMatches` match without allocating `Match` objects or substrings. When you need only the number of matches, call `Count(...)` instead of `Matches(...).Count`.

```csharp
Pattern number = Pattern.With.Digit.Repeat.OneOrMore;

Console.WriteLine(number.Count("order 17, order 204"));           // 2
Console.WriteLine(number.Matches("order 17, order 204").Count);   // 2, but builds a Match per match
```

- On the `net8.0` and `net10.0` builds, `Count(string)` calls `Regex.Count`, which creates no `Match` objects.
- On the `netstandard2.1` build, `Count(string)` walks the matches one by one, so it costs about the same as `Matches(...).Count`. The span overloads do not exist there.

The span members are described on [Matching](./matching.md). For input that does not fit in memory, scan a `TextReader` or a stream instead (see [Streaming](./streaming.md)).

### Build patterns at compile time

A `static readonly` pattern still runs its fluent chain and parses its regex when the program starts. For constant patterns you can move that work to the build. There are two ways:

| | `[GeneratePattern]` | `[GeneratedRegex]` through the FRX007 code fix |
|-|---------------------|-----------------------------------------------|
| What you write | The attribute on a static `Pattern` property or field | Nothing; apply the code fix in your IDE |
| What is generated | A `const string {Member}Source` and a method returning a cached `Regex` | A `[GeneratedRegex]` partial method next to your pattern |
| Matching code | `RegexOptions.Compiled` (IL compiled at run time) | C# emitted by the .NET regex source generator at build time |
| Stays in sync with the chain | Yes, regenerated on every build | No, the regex text is copied once |
| Needs | Any target; a `partial` containing type | .NET 7 or later; a `partial` containing type |
| Native AOT | Works, but `Compiled` is not available, so the regex is interpreted | Best choice: no run-time code generation |

`[GeneratePattern]` evaluates the chain at compile time:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

public static partial class Validators
{
    [GeneratePattern]
    private static Pattern IsoDate => Pattern.With
        .StartOfInput
        .Digit.Repeat.Times(4)
        .Literal("-")
        .Digit.Repeat.Times(2)
        .Literal("-")
        .Digit.Repeat.Times(2)
        .EndOfInput;

    [GeneratePattern("OrderNumber")]
    public static Pattern OrderNumberPattern => Pattern.With
        .StartOfInput
        .Literal("ORD-")
        .Digit.Repeat.Times(6)
        .EndOfInput
        .WithTimeout(TimeSpan.FromMilliseconds(100));

    public static bool IsIsoDate(string value) => IsoDateRegex().IsMatch(value);
}

Console.WriteLine(Validators.IsIsoDate("2026-11-03"));  // True
Console.WriteLine(Validators.IsoDateSource);            // \A\d{4}-\d{2}-\d{2}\z
Console.WriteLine(Validators.OrderNumberPatternSource); // \AORD-\d{6}\z

Regex order = Validators.OrderNumber();
Console.WriteLine($"{order.Options} {order.MatchTimeout.TotalMilliseconds}");  // Compiled 100
Console.WriteLine(ReferenceEquals(Validators.OrderNumber(), Validators.OrderNumber()));  // True
```

- The generated method is named `{Member}Regex()` unless you pass a name to the attribute. It has the same accessibility as the member. The `{Member}Source` constant is always `public`.
- The generated method always adds `RegexOptions.Compiled`, and carries the pattern's options and timeout.
- Use an expression-bodied property (`=>`), as above. The chain then never runs at run time, because the generated method uses the constant. A field initializer still runs once when the type is first used.
- The chain may use only constants, other static patterns and `CommonPatterns`. Otherwise the build reports FRX001. The type must be `partial` (FRX002) and the member `static` (FRX009).

When the analyzer reports FRX007 on a static pattern in a `partial` type, the code fix adds a `[GeneratedRegex]` method. The result looks like this:

```csharp
public static partial class Skus
{
    private static readonly Pattern Sku = Pattern.With
        .StartOfInput
        .UppercaseLetter.Repeat.Times(3)
        .Digit.Repeat.Times(4)
        .EndOfInput;

    // Added by the FRX007 code fix (shown with shorter type names;
    // the fix writes global::System.Text.RegularExpressions.GeneratedRegex and adds a summary comment)
    [GeneratedRegex(@"\A[A-Z]{3}\d{4}\z")]
    private static partial Regex SkuRegex();

    public static bool IsSku(string value) => SkuRegex().IsMatch(value);
}

Console.WriteLine(Skus.IsSku("ABC1234"));  // True
```

- The fix drops `Compiled` (the source generator makes it unnecessary), and passes the other options and the timeout in milliseconds.
- The fix copies the regex text. If you later edit the fluent chain, delete the method and apply the fix again.
- The method is named `{Member}Regex`, with a number appended when that name is taken.
- FRX007 is also reported on `[GeneratePattern]` members. It is only a suggestion; pick one of the two approaches per pattern.

Details and the full diagnostic list: [Compile-Time Checks](./compile-time-checks.md).

## Guarding untrusted input

A backtracking regex can take exponential time on a short, hostile input. That is a regular expression denial of service (ReDoS). Every pattern that sees text from users, files or other systems needs at least one of these defenses:

| Defense | Protects by | Cost | Available on |
|---------|-------------|------|--------------|
| Fix the pattern (`.Possessive`, `AtomicGroup`, rewrite) | Removing the risky backtracking | None at run time | All targets |
| `WithTimeout(...)` | Aborting a slow match with `RegexMatchTimeoutException` | You must catch the exception | All targets |
| `UseNonBacktracking()` | Linear-time matching, whatever the input | No backreferences, lookarounds, atomic groups or conditionals | .NET 8 and later |
| `AnalyzeSafety()`, FRX004 | Finding the risk before it ships | None at run time | All targets |

### Add a timeout

A new pattern has no timeout. `WithTimeout` gives it a budget; when the budget runs out, matching throws `RegexMatchTimeoutException` instead of hanging.

```csharp
// The analyzer reports FRX004 (ReDoS risk) for this nested quantifier
Pattern nested = Pattern.With.Group(Pattern.With.Letter.Repeat.OneOrMore).Repeat.OneOrMore.EndOfInput;
Pattern guarded = nested.WithTimeout(TimeSpan.FromMilliseconds(100));

string attack = new string('a', 40) + "!";

try
{
    guarded.IsMatch(attack);
}
catch (RegexMatchTimeoutException exception)
{
    Console.WriteLine($"Timed out after {exception.MatchTimeout.TotalMilliseconds} ms");  // Timed out after 100 ms
}
```

- Catch `RegexMatchTimeoutException` where you handle the request, and treat it as invalid input. Do not log `exception.Input` when it can hold personal data.
- The timeout must be positive and below about 24 days, or `Regex.InfiniteMatchTimeout`. `TimeSpan.Zero` throws `ArgumentOutOfRangeException`.
- The timeout travels with the pattern through `WithOptions`, through extending the chain, into `ToRegex()` and into code generated by `[GeneratePattern]`. It does not travel into a larger pattern that uses this one as a piece. See [Set options and timeouts on the final pattern](#set-options-and-timeouts-on-the-final-pattern).
- A pattern always passes its own timeout to `Regex`, even an infinite one. The process-wide `REGEX_DEFAULT_MATCH_TIMEOUT` setting therefore does **not** apply to patterns.
- A timeout limits the damage. It does not fix the pattern.

### Use the non-backtracking engine

On .NET 8 and later, `UseNonBacktracking()` switches the pattern to the linear-time engine. The attack that timed out above now returns at once, even 50,000 characters long:

```csharp
// nested is from "Add a timeout"
Pattern linear = nested.UseNonBacktracking();

Console.WriteLine(linear.Options);                                    // NonBacktracking
Console.WriteLine(linear.IsMatch(new string('a', 50_000) + "!"));     // False
```

The flag is easy to lose, and an unsupported construct fails late:

```csharp
// WithOptions replaces all options, including NonBacktracking
Console.WriteLine(linear.WithOptions(RegexOptions.IgnoreCase).Options);                   // IgnoreCase
Console.WriteLine(linear.WithOptions(linear.Options | RegexOptions.IgnoreCase).Options);  // IgnoreCase, NonBacktracking

// Possessive renders an atomic group, which this engine rejects
Pattern possessiveLinear = Pattern.With.Digit.Repeat.OneOrMore.Possessive.Literal("px").UseNonBacktracking();

Console.WriteLine(possessiveLinear.TryValidate(out string? validationError));  // True

try
{
    possessiveLinear.IsMatch("12px");
}
catch (NotSupportedException exception)
{
    Console.WriteLine(exception.Message);
}
// prints: RegexOptions.NonBacktracking is not supported in conjunction with expressions containing: 'atomic subexpressions (?> pattern)'.
```

- Call `UseNonBacktracking()` last, after any `WithOptions`.
- The engine rejects backreferences, lookarounds, atomic groups (including `.Possessive`) and conditionals with `NotSupportedException`. Combining it with `RegexOptions.RightToLeft` throws `ArgumentOutOfRangeException`. Either exception comes at the first match, or at `ToRegex()`, not when you build the pattern.
- `TryValidate` and `IsValid` compile the pattern with its options, so they report such a combination as invalid before the first match.
- Do not combine `.Possessive` with this engine. The engine never backtracks, so the possessive fix is not needed.
- `UseNonBacktracking()` does not exist in the `netstandard2.1` build.
- You can combine both defenses: `nested.UseNonBacktracking().WithTimeout(TimeSpan.FromMilliseconds(100))` keeps the flag and the timeout.

### Analyze for ReDoS

`AnalyzeSafety()` finds nested and overlapping repetitions, rates the risk, suggests a fix and builds an input that triggers the slowdown.

```csharp
// nested is from "Add a timeout"
SafetyReport report = nested.AnalyzeSafety();

Console.WriteLine(report.Risk);    // High
Console.WriteLine(report.IsSafe);  // False
Console.WriteLine(report);
// prints:
// Pattern: ([a-zA-Z]+)+\z
// Risk:    High
//
//   [High] NestedQuantifier at position 0: ([a-zA-Z]+)+
//     The repetition '[a-zA-Z]+' is nested inside the repetition '+'. The same text can be split between iterations in exponentially many ways, so a failing match backtracks through all of them.
//     Fix: Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. For untrusted input also use WithTimeout(...) or UseNonBacktracking().
//     Attack input (33 chars): AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA!

Pattern fixedPattern = Pattern.With.Letter.Repeat.OneOrMore.EndOfInput;
Console.WriteLine(fixedPattern.AnalyzeSafety().Risk);  // None

SafetyReport confirmed = nested.AnalyzeSafety(confirm: true);
Console.WriteLine(confirmed.Issues[0].Confirmed);      // True
```

- `SafetyReport`, `SafetyIssue` and `RiskLevel` live in the `Masterly.FluentRegex` namespace, so no extra `using` is needed.
- `IsSafe` is `true` for the `None` and `Low` risk levels.
- `confirm: true` runs each attack input with a short timeout to prove the slowdown is real. The timeout is the `confirmationBudget` parameter, 250 ms by default; zero or a negative value throws `ArgumentOutOfRangeException`. It costs up to that time per finding, so use it in tests, not on every request.
- Analysis parses the pattern each time. Run it at startup or in a unit test, as in [Test patterns with generated data](#test-patterns-with-generated-data).
- The command-line tool can scan a whole code base in CI. See [ReDoS Safety](./redos-safety.md) and [Command-Line Tool](./command-line-tool.md).
- To measure how much a pattern backtracks on real inputs, use the [Backtracking Profiler](./backtracking-profiler.md).

## Native AOT and trimming

The `net8.0` and `net10.0` builds of the core package are marked trimming and Native AOT compatible. Reflection-based features such as `Extract<T>` and templates carry trimming annotations, so they work in trimmed apps. See [Installation](./installation.md) and [Typed Extraction](./typed-extraction.md).

For AOT apps, keep these points in mind:

- Native AOT has no JIT, so .NET cannot compile a regex to IL at run time. `RegexOptions.Compiled` (and the `Regex` that `[GeneratePattern]` creates) runs on the interpreter instead.
- For constant, hot patterns, use a `[GeneratedRegex]` method through the FRX007 code fix. The .NET source generator emits the matching code at build time.
- `UseNonBacktracking()` works under Native AOT.
- Analysis features (`AnalyzeSafety`, pattern algebra, the optimizer, generation) need no run-time code generation, but they are build-time and test-time tools. Keep them out of hot paths.

## Thread safety

- A `Pattern` is immutable, and every member returns a new pattern. Share patterns freely between threads.
- The `Regex` it caches is thread-safe for matching. When two threads use a new pattern at the same moment, both may build a `Regex`, and one is kept. The result is the same either way.
- The `Regex` returned by a `[GeneratePattern]` method is cached in a static field the same way.
- `Match` and `MatchCollection` results are not shared state; each call returns its own.

## Availability by target framework

| Feature | netstandard2.1 | net8.0 | net10.0 |
|---------|:--------------:|:------:|:-------:|
| Per-instance `Regex` cache, `WithOptions`, `WithTimeout` | Yes | Yes | Yes |
| `AnalyzeSafety()`, `TryValidate`, `Explain()` | Yes | Yes | Yes |
| Analyzers, `[GeneratePattern]` | Yes | Yes | Yes |
| `UseNonBacktracking()` | No | Yes | Yes |
| `IsMatch` and `Count` on spans, `EnumerateMatches` | No | Yes | Yes |
| `EnumerateSplits` | No | No | Yes |
| `[GeneratedRegex]` from the FRX007 fix | Only in a .NET 7 project | Yes | Yes |
| Trimming and Native AOT annotations | No | Yes | Yes |

A .NET 9 project gets the `net8.0` build. Projects on .NET Core 3.x to .NET 7 get the `netstandard2.1` build.

## Common pitfalls

- **Rebuilding a pattern in a method.** `CommonPatterns.Email().IsMatch(x)` in a loop parses and compiles a new `Regex` on every call. Store the pattern once.
- **Calling `ToRegex()` repeatedly.** It returns a new `Regex` every time. Keep the result.
- **Forgetting anchors.** `IsMatch` searches anywhere. Use `StartOfInput` and `EndOfInput` for validators, and remember that `$` accepts a trailing newline.
- **Passing user text to `RegEx`.** Use `Literal`, which escapes it.
- **Making a pattern "faster" and changing its meaning.** `[^e]*end` is not `.*end`, and `(?>\w+)\w+` matches nothing. Prove rewrites with `IsEquivalentTo`.
- **Losing `NonBacktracking` or `Compiled`.** `WithOptions(...)` and `ToRegex(options)` replace the options. Combine flags with `|`.
- **Setting a timeout on a building block.** `Then`, `+`, groups and the `|` operator drop the inner pattern's options and timeout. Set them on the final pattern.
- **Relying on a global timeout.** Patterns ignore `REGEX_DEFAULT_MATCH_TIMEOUT`. Call `WithTimeout` on each pattern that sees untrusted input.
- **Swallowing `RegexMatchTimeoutException`.** Catch it, reject the input, and log the pattern name, not the input.
- **Running analysis per request.** `AnalyzeSafety`, `Explain`, `GetGroupNames` and the validation methods do real work on each call. Run them at startup or in tests.
