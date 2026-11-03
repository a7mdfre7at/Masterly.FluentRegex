---
id: api-reference
title: "API Reference"
description: "Every public type and member of each package, with signatures, defaults, exceptions and target frameworks."
sidebar_label: "API Reference"
---

This page lists the public API of every Masterly.FluentRegex package: each type, each member, its signature, what it returns and when it throws. Use it to look up a member you already know about. To learn how a feature works, follow the links to the guide pages.

Each package uses one namespace that matches its name. The only exception is the `AddFluentPatterns` registration method of the ASP.NET Core package, which lives in `Microsoft.Extensions.DependencyInjection`, as ASP.NET Core extensions usually do.

| Package | Namespace | Target frameworks | Contents |
|---------|-----------|-------------------|----------|
| `Masterly.FluentRegex` | `Masterly.FluentRegex` | `netstandard2.1`, `net8.0`, `net10.0` | The fluent builder, matching, extraction, templates, streaming, redaction, common patterns, algebra, optimizer, safety analysis, debugging tools, test-data generation, coverage, property testing and export. Also contains the analyzers and the `[GeneratePattern]` source generator. |
| `Masterly.FluentRegex.EntityFrameworkCore` | `Masterly.FluentRegex.EntityFrameworkCore` | `net8.0` (EF Core 8), `net10.0` (EF Core 10) | Translate patterns to SQL in LINQ queries, CHECK constraints and raw SQL conditions. |
| `Masterly.FluentRegex.AspNetCore` | `Masterly.FluentRegex.AspNetCore` | `net8.0`, `net10.0` | A pattern registry and the `pattern` route constraint. |
| `Masterly.FluentRegex.Xunit` | `Masterly.FluentRegex.Xunit` | `net8.0`, `net10.0` | Theory data attributes for xUnit v3. |
| `Masterly.FluentRegex.AI` | `Masterly.FluentRegex.AI` | `net8.0`, `net10.0` | Draft patterns from plain English with Claude, verified locally. |
| `Masterly.FluentRegex.Tool` | (command-line tool) | `net8.0` | The `fluentregex` command. |

:::note

Signatures on this page are copied from the 3.1.0 assemblies. Parameter names are the real ones, so you can use them as named arguments. One of them has a spelling quirk: `Literal(params char[] charecters)`.

:::

## Masterly.FluentRegex

The core package. Everything in it lives in the `Masterly.FluentRegex` namespace.

### Pattern

`Pattern` is the fluent builder and the object you match with. Start a chain with `Pattern.With` and add elements one property or method at a time.

```csharp
using System;
using Masterly.FluentRegex;

Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;
Pattern code = Pattern.With.StartOfLine.Then(digits).EndOfLine;

Console.WriteLine(digits);               // \d+
Console.WriteLine(code);                 // ^\d+$
Console.WriteLine(code.IsMatch("2024")); // True
Console.WriteLine(digits);               // \d+  (digits is unchanged)
```

- `Pattern` is immutable. Every member that adds an element returns a new `Pattern` and leaves the original unchanged, so you can share patterns between threads and reuse one pattern as a part of many others.
- A pattern compiles its `Regex` once, on first use, and caches it on that instance. Keep patterns you match often in `static readonly` fields so the cached regex is reused.
- `Pattern` is a `class` that implements `IEquatable<Pattern>`. It does not overload `==`. See [Pattern: configuration and conversion](#pattern-configuration-and-conversion).

The members of `Pattern` are grouped by task in the sections below.

### Pattern: creating patterns

| Member | Returns | Description |
|--------|---------|-------------|
| `static Pattern With { get; }` | `Pattern` | A new, empty pattern to start a chain. |
| `Pattern()` | | Creates an empty pattern. `Pattern.With` reads better. |
| `static Pattern FromRegex(string regex, RegexOptions options = RegexOptions.None)` | `Pattern` | Wraps an existing regex after validating it. Throws `ArgumentNullException` for `null` and `ArgumentException` for invalid syntax. The result can be chained, analyzed, traced and converted like any other pattern. |
| `static Pattern Learn(params string[] match)` | `Pattern` | Infers an anchored `^...$` pattern that fully matches every example. See [Learning from examples](./learning-from-examples.md). |
| `static Pattern Learn(IEnumerable<string> match, IEnumerable<string>? reject = null)` | `Pattern` | The same, and the result must also reject every example in `reject`. Throws `ArgumentException` when `match` is empty or the two lists cannot be separated. |
| `bool IsEmpty { get; }` | `bool` | `true` when the pattern has no elements. |

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern empty = Pattern.With;
Pattern wrapped = Pattern.FromRegex(@"^[A-Z]{3}-\d{4}$", RegexOptions.IgnoreCase);
Pattern learned = Pattern.Learn("INV-0001", "INV-0042", "INV-1234");

Console.WriteLine(empty.IsEmpty);               // True
Console.WriteLine(wrapped.Options);             // IgnoreCase
Console.WriteLine(wrapped.IsMatch("abc-1234")); // True
Console.WriteLine(learned);                     // ^INV-\d{4}$
```

### Pattern: anchors

Anchors match a position, not a character. All of them are properties that return `Pattern`.

| Member | Regex | Description |
|--------|-------|-------------|
| `StartOfLine` | `^` | The start of the string, or of any line under `RegexOptions.Multiline`. |
| `EndOfLine` | `$` | The end of the string or the position before a final `\n`, or the end of any line under `Multiline`. |
| `StartOfInput` | `\A` | The start of the input, whatever the options. |
| `EndOfInput` | `\z` | The very end of the input, whatever the options. Does not accept a trailing `\n`. |
| `WordBoundary` | `\b` | A position between a word character and a non-word character. |
| `NonWordBoundary` | `\B` | A position that is not a word boundary. |

```csharp
Pattern line = Pattern.With.StartOfLine.Word.Repeat.OneOrMore.EndOfLine;
Pattern input = Pattern.With.StartOfInput.Word.Repeat.OneOrMore.EndOfInput;

Console.WriteLine(line);                     // ^\w+$
Console.WriteLine(input);                    // \A\w+\z
Console.WriteLine(line.IsMatch("hello\n"));  // True
Console.WriteLine(input.IsMatch("hello\n")); // False
```

:::tip

Use `StartOfInput` and `EndOfInput` to validate a whole value. `EndOfLine` also accepts a value that ends with a newline, which is rarely what a validator wants.

:::

### Pattern: character matchers

Each matcher is a property that returns `Pattern` and matches one character. Several of them render differently inside a `Set(...)`, so they combine into one character class.

| Member | Regex on its own | Inside `Set(...)` | Matches |
|--------|------------------|-------------------|---------|
| `Digit` | `\d` | `\d` | Any Unicode decimal digit, not only `0-9`. See [Unicode](./unicode.md). |
| `NonDigit` | `\D` | `\D` | Any character that is not a digit. |
| `AsciiDigit` | `[0-9]` | `0-9` | Only the ASCII digits. |
| `HexDigit` | `[0-9a-fA-F]` | `0-9a-fA-F` | A hexadecimal digit. |
| `Word` | `\w` | `\w` | A Unicode letter, digit, connector punctuation or combining mark. |
| `NonWord` | `\W` | `\W` | Any character that is not a word character. |
| `Whitespace` | `\s` | `\s` | Any whitespace character. |
| `NonWhitespace` | `\S` | `\S` | Any non-whitespace character. |
| `Letter` | `[a-zA-Z]` | `a-zA-Z` | An ASCII letter. Use `Unicode(UnicodeGroup.Letter)` for letters of every script. |
| `LowercaseLetter` | `[a-z]` | `a-z` | A lowercase ASCII letter. |
| `UppercaseLetter` | `[A-Z]` | `A-Z` | An uppercase ASCII letter. |
| `Anything` | `.` | | Any character except `\n` (any character under `RegexOptions.Singleline`). |
| `Tab` | `\t` | `\t` | A tab. |
| `Newline` | `\n` | `\n` | A line feed. |
| `CarriageReturn` | `\r` | `\r` | A carriage return. |
| `CodePoint` | | | One Unicode scalar value: a surrogate pair, such as most emoji, or one non-surrogate character. Lone surrogates do not match. |
| `Grapheme` | | | One user-perceived character, an approximation of an extended grapheme cluster. |

```csharp
Pattern alone = Pattern.With.Letter;
Pattern inSet = Pattern.With.Set(Pattern.With.Letter.AsciiDigit.Literal("_"));
Pattern hex = Pattern.With.Literal("0x").HexDigit.Repeat.OneOrMore;

Console.WriteLine(alone); // [a-zA-Z]
Console.WriteLine(inSet); // [a-zA-Z0-9_]
Console.WriteLine(hex);   // 0x[0-9a-fA-F]+
```

### Pattern: literal and raw text

| Member | Returns | Description |
|--------|---------|-------------|
| `Literal(string literalChars)` | `Pattern` | Matches the text exactly. Regex special characters are escaped, inside and outside a set. Throws `ArgumentNullException` for `null`. |
| `Literal(params char[] charecters)` | `Pattern` | Matches the characters exactly, escaped the same way. |
| `RegEx(string regex)` | `Pattern` | Adds raw regex text without escaping. Use it for constructs the builder does not cover. |

```csharp
Pattern price = Pattern.With.Literal("$").Digit.Repeat.OneOrMore.Literal(".").Digit.Repeat.Times(2);

Console.WriteLine(price); // \$\d+\.\d{2}
```

- `RegEx` does not validate its text when you call it. An invalid fragment fails when the pattern is first used, or at build time if the analyzer can see it (diagnostic `FRX003`). Check it yourself with `IsValid()` or `TryValidate`.

### Pattern: character classes and Unicode

| Member | Returns | Description |
|--------|---------|-------------|
| `Set(Pattern pattern)` | `Pattern` | One character from the set the inner pattern describes: `[...]`. Literal characters are escaped, so `Literal("a-z")` inside a set matches `a`, `-` and `z`. Use `Range` for ranges. |
| `NegatedSet(Pattern pattern)` | `Pattern` | One character not in the set: `[^...]`. |
| `Range(char from, char to)` | `Pattern` | An inclusive range. Renders `[a-f]` on its own and `a-f` inside a set. Throws `ArgumentException` when `from` is greater than `to`. |
| `AnyOf(string characters)` | `Pattern` | One of the listed characters, all taken literally, including `-`, `]` and `^`. Throws `ArgumentException` for an empty string. |
| `NoneOf(string characters)` | `Pattern` | Any character except the listed ones: `[^...]`. |
| `Unicode(UnicodeCategory category)` | `Pattern` | A character in a `System.Globalization.UnicodeCategory`, such as `\p{Lu}`. Works inside and outside a set. |
| `NotUnicode(UnicodeCategory category)` | `Pattern` | A character not in the category: `\P{Lu}`. |
| `Unicode(UnicodeGroup group)` | `Pattern` | A character in a general group, such as `\p{L}` for any letter in any script. |
| `NotUnicode(UnicodeGroup group)` | `Pattern` | A character not in the group: `\P{L}`. |
| `UnicodeBlock(string blockName)` | `Pattern` | A character in a named block, such as `\p{IsGreek}`. Throws `ArgumentException` for an empty or unknown block name. |
| `Script(UnicodeScript script)` | `Pattern` | Any character of a writing system. Works inside and outside a set. |
| `LetterIn(UnicodeScript script)` | `Pattern` | A letter (category `L`) of the script. Marks, digits and punctuation are excluded. |
| `DigitIn(UnicodeScript script)` | `Pattern` | A decimal digit of the script. `Latin` gives `0-9`. |

```csharp
using System;
using System.Globalization;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.With.Range('a', 'f'));                                   // [a-f]
Console.WriteLine(Pattern.With.Set(Pattern.With.Range('a', 'f').Range('0', '9'))); // [a-f0-9]
Console.WriteLine(Pattern.With.AnyOf("-+]"));                                      // [\-\+\]]
Console.WriteLine(Pattern.With.NoneOf("aeiou"));                                   // [^aeiou]
Console.WriteLine(Pattern.With.Unicode(UnicodeCategory.UppercaseLetter));          // \p{Lu}
Console.WriteLine(Pattern.With.NotUnicode(UnicodeGroup.Letter));                   // \P{L}
Console.WriteLine(Pattern.With.UnicodeBlock("IsGreek"));                           // \p{IsGreek}
Console.WriteLine(Pattern.With.DigitIn(UnicodeScript.Arabic));                     // [٠-٩۰-۹]
```

See [Character classes](./character-classes.md) and [Unicode](./unicode.md) for details.

### Pattern: quantifiers

A quantifier applies to the element just before it.

| Member | Returns | Description |
|--------|---------|-------------|
| `Repeat { get; }` | `Repeat` | Starts a quantifier for the last element. See [Repeat](#repeat). |
| `Lazy { get; }` | `Pattern` | Makes the preceding quantifier lazy: `+?`, `*?`, `??`, `{n,m}?`. |
| `Possessive { get; }` | `Pattern` | Makes the preceding quantifier possessive by wrapping it in an atomic group: `(?>\d+)`. The engine never gives characters back, which removes catastrophic backtracking for that element. |

```csharp
Console.WriteLine(Pattern.With.Anything.Repeat.OneOrMore.Lazy);    // .+?
Console.WriteLine(Pattern.With.Digit.Repeat.OneOrMore.Possessive); // (?>\d+)
```

- `Lazy` and `Possessive` must directly follow a quantifier. Otherwise they throw `InvalidOperationException` with a message such as `Lazy must directly follow a quantifier, for example Pattern.With.Digit.Repeat.OneOrMore.Lazy.`. The analyzer reports this mistake at build time as `FRX006`.
- See [Quantifiers](./quantifiers.md).

### Pattern: groups, backreferences and conditionals

| Member | Regex | Description |
|--------|-------|-------------|
| `Group(Pattern pattern)` | `(...)` | A numbered capturing group. |
| `NamedGroup(string name, Pattern pattern)` | `(?<name>...)` | A named capturing group. The name may contain only letters, digits and underscores; otherwise `ArgumentException`. |
| `NonCapturingGroup(Pattern pattern)` | `(?:...)` | Groups without capturing. |
| `AtomicGroup(Pattern pattern)` | `(?>...)` | Once matched, the engine never backtracks into it. |
| `Backreference(string name)` | `\k<name>` | Matches the text a named group captured. |
| `Backreference(int groupNumber)` | `\1` | Matches the text a numbered group captured. Throws `ArgumentOutOfRangeException` when `groupNumber` is less than 1. |
| `Conditional(string groupName, Pattern truePattern, Pattern falsePattern)` | `(?(name)yes\|no)` | Uses `truePattern` when the named group took part in the match, otherwise `falsePattern`. |
| `Conditional(int groupNumber, Pattern truePattern, Pattern falsePattern)` | `(?(1)yes\|no)` | The same for a numbered group. Group numbers start at 1. |

```csharp
Pattern word = Pattern.With.Word.Repeat.OneOrMore;

Pattern doubled = Pattern.With.NamedGroup("word", word).Whitespace.Backreference("word");

Pattern bracketed = Pattern.With
    .NamedGroup("open", Pattern.With.Literal("<")).Repeat.Optional
    .Word.Repeat.OneOrMore
    .Conditional("open", Pattern.With.Literal(">"), Pattern.With.EndOfLine);

Console.WriteLine(doubled);                             // (?<word>\w+)\s\k<word>
Console.WriteLine(doubled.Match("the the end").Value);  // the the
Console.WriteLine(bracketed);                           // (?<open><)?\w+(?(open)>|$)
```

See [Groups and backreferences](./groups-and-backreferences.md).

### Pattern: assertions

Lookarounds check what comes before or after the current position without consuming it.

| Member | Regex | Description |
|--------|-------|-------------|
| `PositiveLookahead(Pattern pattern)` | `(?=...)` | The pattern must follow. |
| `NegativeLookahead(Pattern pattern)` | `(?!...)` | The pattern must not follow. |
| `PositiveLookbehind(Pattern pattern)` | `(?<=...)` | The pattern must precede. |
| `NegativeLookbehind(Pattern pattern)` | `(?<!...)` | The pattern must not precede. |

```csharp
Pattern price = Pattern.With
    .PositiveLookbehind(Pattern.With.Literal("$"))
    .Digit.Repeat.OneOrMore
    .NegativeLookahead(Pattern.With.Literal("%"));

Console.WriteLine(price);                           // (?<=\$)\d+(?!%)
Console.WriteLine(price.Match("Cost: $120").Value); // 120
Console.WriteLine(price.IsMatch("Rate: 15%"));      // False
```

See [Assertions](./assertions.md).

### Pattern: composition

| Member | Returns | Description |
|--------|---------|-------------|
| `Then(Pattern pattern)` | `Pattern` | Appends every element of another pattern. A quantifier that follows applies to its last element, as if it were written inline. |
| `operator +(Pattern left, Pattern right)` | `Pattern` | Concatenation. The same as `left.Then(right)`. |
| `operator \|(Pattern left, Pattern right)` | `Pattern` | Non-capturing alternation. Chains flatten: `a \| b \| c` renders `(?:a\|b\|c)`. |
| `OneOf(params Pattern[] alternatives)` | `Pattern` | `(?:a\|b\|c)`. Throws `ArgumentException` when no alternative is given. |
| `OneOf(params string[] words)` | `Pattern` | Literal alternatives, escaped: `(?:cat\|dog)`. |
| `Choice(Pattern firstChoice, Pattern secondChoice, params Pattern[] additionalChoices)` | `Pattern` | Alternation inside a numbered capturing group: `(a\|b\|c)`. |
| `IgnoreCase(Pattern pattern)` | `Pattern` | Matches the inner pattern case-insensitively: `(?i:...)`. |
| `CaseSensitive(Pattern pattern)` | `Pattern` | Matches the inner pattern case-sensitively: `(?-i:...)`. |
| `InlineOptions(RegexOptions enable, Pattern pattern, RegexOptions disable = RegexOptions.None)` | `Pattern` | `(?imnsx-imnsx:...)`. Only `IgnoreCase`, `Multiline`, `ExplicitCapture`, `Singleline` and `IgnorePatternWhitespace` can be applied inline; others throw `ArgumentException`. |

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern year = Pattern.With.Digit.Repeat.Times(4);
Pattern month = Pattern.With.Digit.Repeat.Times(2);

Pattern date = year + Pattern.With.Literal("-") + month;
Pattern unit = Pattern.With.Literal("kg") | Pattern.With.Literal("lb") | Pattern.With.Literal("g");

Console.WriteLine(date);                                       // \d{4}-\d{2}
Console.WriteLine(unit);                                       // (?:kg|lb|g)
Console.WriteLine(Pattern.With.OneOf("c++", "c#"));            // (?:c\+\+|c#)
Console.WriteLine(Pattern.With.Choice(year, month));           // (\d{4}|\d{2})
Console.WriteLine(Pattern.With.IgnoreCase(Pattern.With.Literal("id")).Digit); // (?i:id)\d
Console.WriteLine(Pattern.With.InlineOptions(RegexOptions.Multiline, Pattern.With.StartOfLine, RegexOptions.IgnoreCase)); // (?m-i:^)
```

See [Composing patterns](./composing-patterns.md).

### Pattern: configuration and conversion

Settings travel with the pattern. Each `With...` member returns a copy.

| Member | Returns | Description |
|--------|---------|-------------|
| `Options { get; }` | `RegexOptions` | The options used when the pattern runs. Default `RegexOptions.None`. |
| `Timeout { get; }` | `TimeSpan` | The match timeout. Default `Regex.InfiniteMatchTimeout`. |
| `WithOptions(RegexOptions options)` | `Pattern` | A copy that uses these options. Replaces the previous options; it does not add to them. |
| `WithTimeout(TimeSpan timeout)` | `Pattern` | A copy that throws `RegexMatchTimeoutException` when a match runs longer. The runtime defense against ReDoS. Throws `ArgumentOutOfRangeException` unless the value is positive and below about 24 days, or `Regex.InfiniteMatchTimeout`. |
| `UseNonBacktracking()` | `Pattern` | A copy that runs on the linear-time `RegexOptions.NonBacktracking` engine. Patterns with backreferences, lookarounds, atomic groups or conditionals then throw `NotSupportedException` when they run. `net8.0` and `net10.0` only. |
| `WithAsciiDigits()` | `Pattern` | A copy in which `\d` matches only `0-9` and `\D` everything else. |
| `WithDigits(DigitMode digits)` | `Pattern` | Unchanged for `DigitMode.Unicode`; `WithAsciiDigits()` for `DigitMode.Ascii`. |
| `IgnoreDiacritics()` | `Pattern` | A copy that matches text with or without diacritics, in precomposed (NFC) and decomposed (NFD) form. Options and timeout are kept. |
| `ToString()` | `string` | The regex text. Computed once and cached. |
| `ToRegex()` | `Regex` | A new `Regex` with the pattern's options and timeout. |
| `ToRegex(RegexOptions options)` | `Regex` | A new `Regex` with explicit options. The pattern's timeout still applies. |
| `Equals(Pattern? other)` | `bool` | `true` when both render the same regex with the same options and timeout. `GetHashCode()` agrees with it. |

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern sku = Pattern.With.StartOfLine.Letter.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4).EndOfLine;

Pattern configured = sku
    .WithOptions(RegexOptions.IgnoreCase)
    .WithTimeout(TimeSpan.FromMilliseconds(250));

Console.WriteLine(configured.Options);                         // IgnoreCase
Console.WriteLine(configured.Timeout);                         // 00:00:00.2500000
Console.WriteLine(sku.Timeout == Regex.InfiniteMatchTimeout);  // True
Console.WriteLine(sku.IsMatch("ABC-١٢٣٤"));                    // True: \d accepts Arabic-Indic digits
Console.WriteLine(sku.WithAsciiDigits().IsMatch("ABC-١٢٣٤"));  // False
Console.WriteLine(sku.WithAsciiDigits());                      // ^[a-zA-Z]{3}-[0-9]{4}$
Console.WriteLine(sku.Equals(Pattern.FromRegex(sku.ToString()))); // True
Console.WriteLine(sku == Pattern.FromRegex(sku.ToString()));      // False: == compares references
Console.WriteLine(Pattern.With.Literal("café").IgnoreDiacritics().IsMatch("cafe")); // True
```

:::warning

`ToRegex()` and `IsMatch(input, options)` build a new `Regex` on every call. The other matching members reuse one cached `Regex` per pattern instance. Call `ToRegex()` once and keep the result if you need a `Regex` object.

:::

### Pattern: matching

These members match like the `Regex` members of the same name, using the pattern's options and timeout.

| Member | Returns | Description |
|--------|---------|-------------|
| `IsMatch(string input)` | `bool` | `true` when the pattern matches anywhere in the input. |
| `IsMatch(string input, RegexOptions options)` | `bool` | The same with explicit options. Builds a new `Regex` each call. |
| `Match(string input)` | `Match` | The first match. Check `Match.Success`. |
| `Match(string input, int startAt)` | `Match` | The first match at or after `startAt`. |
| `Matches(string input)` | `MatchCollection` | All non-overlapping matches. |
| `Count(string input)` | `int` | The number of matches. |
| `Replace(string input, string replacement)` | `string` | Replaces every match. The replacement supports `$1`, `${name}` and `$$`. |
| `Replace(string input, string replacement, int count)` | `string` | Replaces at most `count` matches. |
| `Replace(string input, MatchEvaluator evaluator)` | `string` | Replaces every match with the evaluator's result. |
| `Replace(string input, MatchEvaluator evaluator, int count)` | `string` | Replaces at most `count` matches. |
| `Replace(string input, Func<Replacement, Replacement> build)` | `string` | Replaces every match with a fluent [Replacement](#replacement). Group names and numbers are checked before anything is replaced. |
| `Replace(string input, Func<Replacement, Replacement> build, int count)` | `string` | The same for at most `count` matches. |
| `Split(string input)` | `string[]` | Splits the input at every match. |
| `Split(string input, int count)` | `string[]` | Splits into at most `count` parts. |
| `IsMatch(ReadOnlySpan<char> input)` | `bool` | Allocation-free test on a span. `net8.0` and `net10.0` only. |
| `Count(ReadOnlySpan<char> input)` | `int` | Allocation-free count on a span. `net8.0` and `net10.0` only. |
| `EnumerateMatches(ReadOnlySpan<char> input)` | `Regex.ValueMatchEnumerator` | Match positions without allocating `Match` objects. `net8.0` and `net10.0` only. |
| `EnumerateSplits(ReadOnlySpan<char> input)` | `Regex.ValueSplitEnumerator` | The ranges between matches, without allocating strings. `net10.0` only. |

```csharp
Pattern number = Pattern.With.Digit.Repeat.OneOrMore;
string text = "3 apples, 12 pears, 7 plums";

Console.WriteLine(number.Match(text).Value);         // 3
Console.WriteLine(number.Match(text, 2).Value);      // 12
Console.WriteLine(number.Count(text));               // 3
Console.WriteLine(number.Replace(text, "#", 1));     // # apples, 12 pears, 7 plums
Console.WriteLine(number.Replace(text, m => (int.Parse(m.Value) * 2).ToString())); // 6 apples, 24 pears, 14 plums
Console.WriteLine(string.Join("|", Pattern.With.Literal(", ").Split(text)));         // 3 apples|12 pears|7 plums
```

See [Matching](./matching.md) and [Fluent replacements](./fluent-replacements.md).

### Pattern: typed extraction and streaming

| Member | Returns | Description |
|--------|---------|-------------|
| `Extract<T>(string input, IFormatProvider provider = null)` | `T` | Matches the input and binds the named groups to a new `T`. Throws `FormatException` when the input does not match. |
| `TryExtract<T>(string input, out T result, IFormatProvider provider = null)` | `bool` | The same, returning `false` instead of throwing. |
| `ExtractAll<T>(string input, IFormatProvider provider = null)` | `IEnumerable<T>` | Binds every match. Evaluated lazily. |
| `Matches(TextReader reader, StreamMatchOptions? options = null)` | `IEnumerable<StreamMatch>` | Finds every match in text read from a reader, keeping only a bounded window in memory. |
| `MatchesAsync(TextReader reader, StreamMatchOptions? options = null, CancellationToken cancellationToken = default)` | `IAsyncEnumerable<StreamMatch>` | The async version. |
| `MatchesAsync(Stream stream, Encoding? encoding = null, StreamMatchOptions? options = null, CancellationToken cancellationToken = default)` | `IAsyncEnumerable<StreamMatch>` | Decodes a stream (UTF-8 by default) and finds every match. The stream is left open. |

```csharp
using System;
using System.IO;
using Masterly.FluentRegex;

Pattern version = Pattern.With
    .NamedGroup("major", Pattern.With.Digit.Repeat.OneOrMore).Literal(".")
    .NamedGroup("minor", Pattern.With.Digit.Repeat.OneOrMore).Literal(".")
    .NamedGroup("patch", Pattern.With.Digit.Repeat.OneOrMore);

Console.WriteLine(version.Extract<Version3>("v3.1.0"));            // Version3 { Major = 3, Minor = 1, Build = 0 }
Console.WriteLine(version.TryExtract("no version", out Version3 _)); // False

Pattern errorCode = Pattern.With.Literal("ERR-").NamedGroup("code", Pattern.With.Digit.Repeat.Times(3));

using StringReader reader = new StringReader("ok\nERR-404 missing\nok\nERR-500 crash\n");
foreach (StreamMatch match in errorCode.Matches(reader))
{
    Console.WriteLine($"{match.Index} {match.Value} {match.Groups["code"]}"); // 3 ERR-404 404, then 22 ERR-500 500
}

public sealed record Version3(int Major, int Minor, [RegexGroup("patch")] int Build);
```

- Groups bind to constructor parameters and settable properties by name, ignoring case. `[RegexGroup("name")]` binds a member to a group with another name. Types beyond the built-in ones need a converter from [ExtractionConverters](#regexgroupattribute-and-extractionconverters).
- Streaming throws `NotSupportedException` for `RegexOptions.RightToLeft` and for the `\G` anchor, and `InvalidOperationException` for a match longer than `StreamMatchOptions.MaxMatchLength` that is not at the end of the input.
- See [Typed extraction](./typed-extraction.md) and [Streaming](./streaming.md).

### Pattern: validation and semantic rules

| Member | Returns | Description |
|--------|---------|-------------|
| `IsValid()` | `bool` | `true` when the pattern compiles to a valid regex. |
| `TryValidate(out string errorMessage)` | `bool` | The same, with the parser's error message when it is invalid. |
| `GetGroupNames()` | `string[]` | The names of the named capturing groups. |
| `GetGroupCount()` | `int` | The number of capturing groups, not counting group 0 (the whole match). |
| `Explain()` | `string` | A plain-English, line-by-line description of the pattern. |
| `Must(Func<string, bool> rule, string? description = null)` | `ConstrainedPattern` | Adds a rule the whole match must also pass, such as a checksum. See [ConstrainedPattern](#constrainedpattern-and-constraintresult). |
| `Must(string groupName, Func<string, bool> rule, string? description = null)` | `ConstrainedPattern` | Adds a rule for the value of a named group. Skipped when the group did not take part in the match. Throws `ArgumentException` when the pattern has no such group. |

```csharp
// regexText comes from your application, for example "(unclosed"
Pattern userPattern = Pattern.With.RegEx(regexText);

Console.WriteLine(userPattern.IsValid());                    // False
Console.WriteLine(userPattern.TryValidate(out string error)); // False
Console.WriteLine(error);                                    // Invalid pattern '(unclosed' at offset 9. Not enough )'s.

Pattern card = Pattern.With.StartOfLine.NamedGroup("number", Pattern.With.Digit.Repeat.Times(16)).EndOfLine;
Console.WriteLine(card.GetGroupCount()); // 1

Console.WriteLine(Pattern.With.Digit.Repeat.Times(3).Explain());
// prints:
// Pattern: \d{3}
//
// Explanation:
//   - Digit [0-9]
//   - Exactly 3 times
```

See [Validation and debugging](./validation-and-debugging.md) and [Semantic rules](./semantic-rules.md).

### Pattern: language algebra and facts

These members treat a pattern as the set of strings it matches in full, and prove facts about it with automata. They work for the regular subset of .NET regex. A pattern with backreferences, lookarounds or conditionals throws `NotSupportedException`, and a pattern too large to analyze throws `InvalidOperationException`. `GetLanguageFacts` is the exception: for non-regular constructs it falls back to sound approximations (`IsExact` is `false`) instead of throwing.

| Member | Returns | Description |
|--------|---------|-------------|
| `static Compare(Pattern left, Pattern right)` | `PatternComparison` | How the two languages relate, with the shortest strings that show it. |
| `IsEquivalentTo(Pattern other)` | `bool` | Both match exactly the same strings. |
| `IsSubsetOf(Pattern other)` | `bool` | Every string this pattern matches, `other` matches too. |
| `Overlaps(Pattern other)` | `bool` | At least one string matches both. |
| `MatchesNothing()` | `bool` | No string matches this pattern in full. |
| `ShortestMatch()` | `string` | The shortest string the pattern matches in full, or `null` when it matches nothing. |
| `Intersect(Pattern other)` | `Pattern` | Matches the strings both patterns match. |
| `Except(Pattern other)` | `Pattern` | Matches the strings this pattern matches and `other` rejects. |
| `Union(Pattern other)` | `Pattern` | Matches the strings either pattern matches. |
| `GetLanguageFacts(MatchScope scope = MatchScope.FullString)` | `LanguageFacts` | Length bounds, the required prefix and suffix, and whether the empty string is accepted. |

```csharp
Pattern twoDigits = Pattern.With.Digit.Repeat.Times(2);
Pattern oneToThree = Pattern.With.Digit.Repeat.Times(1, 3);

PatternComparison comparison = Pattern.Compare(oneToThree, twoDigits);
Console.WriteLine(comparison.Relation);              // ProperSuperset
Console.WriteLine(comparison.OnlyLeftExample);       // 0
Console.WriteLine(twoDigits.IsSubsetOf(oneToThree)); // True
Console.WriteLine(twoDigits.Intersect(Pattern.With.Literal("x")).MatchesNothing()); // True

LanguageFacts facts = Pattern.With.Literal("INV-").Digit.Repeat.Times(4, 6).GetLanguageFacts();
Console.WriteLine(facts); // FullString: length 8..10, prefix "INV-", suffix "", accepts empty: false (exact)
```

See [Pattern algebra](./pattern-algebra.md) and [Language facts](./language-facts.md).

### Pattern: optimization and safety

| Member | Returns | Description |
|--------|---------|-------------|
| `Optimize()` | `OptimizationResult` | Rewrites the pattern into a simpler one with the same .NET match behavior: the same leftmost-first matches, groups, captures and options. Branch order never changes. |
| `AnalyzeSafety(bool confirm = false, TimeSpan? confirmationBudget = null)` | `SafetyReport` | Finds constructs that can backtrack catastrophically (ReDoS). With `confirm: true`, runs each attack input with a timeout to prove the slowdown; `confirmationBudget` defaults to 250 ms per finding and must be positive. |

```csharp
OptimizationResult optimized = Pattern.FromRegex("(?:a|b|c)(?:x)+").Optimize();
Console.WriteLine(optimized.Optimized);          // [abc]x+
Console.WriteLine(optimized.VerifiedEquivalent); // True

SafetyReport report = Pattern.FromRegex("^(a+)+$").AnalyzeSafety();
Console.WriteLine(report.Risk);                  // High
Console.WriteLine(report.Issues[0].Kind);        // NestedQuantifier
Console.WriteLine(report.Issues[0].AttackInput?.Length); // 33
```

See [Optimizer](./optimizer.md) and [ReDoS safety](./redos-safety.md).

### Pattern: debugging and visualization

| Member | Returns | Description |
|--------|---------|-------------|
| `Trace(string input)` | `MatchTrace` | Matches the input and explains which construct consumed which characters, or where the match failed. |
| `Highlight(string text, HighlightFormat format = HighlightFormat.Ansi)` | `string` | The text with every match and group capture marked. Throws `ArgumentOutOfRangeException` for an undefined format. |
| `Profile(string input, ProfileOptions? options = null)` | `ProfileReport` | Matches with a step-counting backtracking interpreter and reports the cost of each construct. Throws `NotSupportedException` for conditionals, balancing groups, `\G` or `RightToLeft`. |
| `ToRailroadSvg()` | `string` | A railroad diagram as a self-contained SVG document. |
| `ToMermaid()` | `string` | A Mermaid flowchart, starting with `flowchart LR`. |

```csharp
Pattern phone = Pattern.With.NamedGroup("area", Pattern.With.Digit.Repeat.Times(3)).Literal("-").Digit.Repeat.Times(4);

MatchTrace miss = phone.Trace("555-12");
Console.WriteLine(miss.IsMatch);             // False
Console.WriteLine(miss.Failure.Construct);   // \d{4}
Console.WriteLine(miss.Failure.Description); // a digit, exactly 4 times

Console.WriteLine(phone.Highlight("Call 555-1234 now", HighlightFormat.Brackets)); // Call [[area:555]-1234] now

ProfileReport profile = Pattern.FromRegex("^(a+)+$").Profile("aaaaaaaab");
Console.WriteLine(profile.Steps);      // 1543
Console.WriteLine(profile.Backtracks); // 510
```

See [Match tracing](./match-tracing.md), [Highlighting](./highlighting.md), [Diagrams](./diagrams.md) and [Backtracking profiler](./backtracking-profiler.md).

### Pattern: testing

| Member | Returns | Description |
|--------|---------|-------------|
| `GenerateMatching(int count = 10, int? seed = null)` | `string[]` | Distinct strings the pattern matches, each checked with the real .NET engine. Pass a seed for reproducible output; without one each call differs. May return fewer than `count` when the pattern has fewer distinct strings. Throws `ArgumentOutOfRangeException` for a negative count. |
| `GenerateNonMatching(int count = 10, int? seed = null)` | `string[]` | Distinct strings the pattern does not match, mostly near misses one edit away from a valid sample. |
| `MeasureCoverage(IEnumerable<string> inputs)` | `CoverageReport` | Matches every input and reports which branches, optional parts and repetition limits they exercised. |
| `TrackCoverage()` | `PatternCoverage` | A recorder you match through during a test run, then ask for a report. |

```csharp
Pattern title = Pattern.With.OneOf("Mr", "Mrs", "Ms").Literal(".").Repeat.Optional;

Console.WriteLine(string.Join(", ", title.GenerateMatching(count: 3, seed: 1)));    // Mr., Mrs., Mr
Console.WriteLine(string.Join(", ", title.GenerateNonMatching(count: 3, seed: 1))); // M ., rM, M!s
Console.WriteLine(title.MeasureCoverage(new[] { "Mr", "Mrs." }).Percentage);        // 25
```

See [Test data generation](./test-data-generation.md), [Regex coverage](./regex-coverage.md) and [Property testing](./property-testing.md).

### Pattern: code generation and export

| Member | Returns | Description |
|--------|---------|-------------|
| `ToFluentCode(int indentSize = 4)` | `string` | C# code that rebuilds the pattern with the fluent API. Throws `ArgumentOutOfRangeException` for a negative indent and `InvalidOperationException` when the pattern is not valid regex syntax. |
| `ToJavaScript()` | `string` | A JavaScript regex literal with exact .NET semantics. Throws `NotSupportedException` for constructs JavaScript cannot express. |
| `ToFlavor(RegexFlavor flavor, FlavorOptions? options = null)` | `FlavorConversion` | Translates the pattern, including its options, into another dialect and lists every difference. Throws `NotSupportedException` when the dialect cannot express a construct; the message lists them all with positions. |
| `ToSqlCondition(string columnSql, RegexFlavor flavor)` | `string` | A boolean SQL condition for raw SQL or Dapper, using the database's regex operator. `flavor` must be a SQL flavor; otherwise `ArgumentException`. |
| `ToLike(LikeDialect dialect, MatchScope scope = MatchScope.Substring, LikeCollation collation = LikeCollation.Binary)` | `LikeConversion` | A SQL `LIKE` pattern that accepts every string the pattern accepts, with a proof of whether it accepts exactly those. |

```csharp
Pattern digits = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;

Console.WriteLine(digits.ToJavaScript());                                                     // /^\p{Nd}+(?=\n?$)/u
Console.WriteLine(digits.ToFlavor(RegexFlavor.JsonSchema).ToLiteral());                       // "^\\p{Nd}+(?=\\n?$)"
Console.WriteLine(digits.WithAsciiDigits().ToSqlCondition("\"Code\"", RegexFlavor.PostgreSql)); // "Code" ~ '\A[0-9]+(?=\n?\Z)'

Pattern invoice = Pattern.With.StartOfInput.Literal("INV-").AsciiDigit.Repeat.Times(4).EndOfInput;
Console.WriteLine(invoice.ToLike(LikeDialect.SqlServer, MatchScope.FullString)); // INV-[0-9][0-9][0-9][0-9] (exact)

Console.WriteLine(Pattern.FromRegex(@"\d{3}-\d{4}").ToFluentCode());
// prints:
// Pattern.With
//     .Digit.Repeat.Times(3)
//     .Literal("-")
//     .Digit.Repeat.Times(4)
```

See [Export](./export.md), [SQL dialects](./sql-dialects.md), [SQL LIKE](./sql-like.md) and [Regex to fluent code](./regex-to-fluent-code.md).

### Repeat

`Repeat` applies a quantifier to the last element of a pattern. You reach it through `Pattern.Repeat`. Every member returns the parent `Pattern` with the quantifier applied.

| Member | Regex | Description |
|--------|-------|-------------|
| `Repeat(Pattern parentPattern)` | | Constructor. Throws `ArgumentNullException` for `null`. You rarely call it directly. |
| `OneOrMore` | `+` | One or more times. |
| `ZeroOrMore` | `*` | Zero or more times. |
| `Optional` | `?` | Zero or one time. |
| `Times(int exactly)` | `{n}` | Exactly `n` times. |
| `Times(int min, int max)` | `{min,max}` | Between `min` and `max` times. |
| `AtLeast(int x)` | `{x,}` | At least `x` times. |
| `AtMost(int x)` | `{0,x}` | At most `x` times. |

```csharp
Console.WriteLine(Pattern.With.Digit.Repeat.Times(2, 4)); // \d{2,4}
Console.WriteLine(Pattern.With.Digit.Repeat.AtLeast(2));  // \d{2,}
Console.WriteLine(Pattern.With.Digit.Repeat.AtMost(2));   // \d{0,2}
```

- Negative counts throw `ArgumentOutOfRangeException`, and so does `max` below `min`.
- Calling a quantifier on an empty pattern throws `InvalidOperationException`: `Repeat must follow an element to repeat, for example Pattern.With.Digit.Repeat.OneOrMore.`. The `Repeat` property itself does not throw; the quantifier member does.
- A quantifier on a quantifier wraps the first one in a non-capturing group, because `\d+{2}` is not valid .NET syntax: `Pattern.With.Digit.Repeat.OneOrMore.Repeat.Times(2)` renders `(?:\d+){2}`.
- Follow a quantifier with `.Lazy` or `.Possessive` (members of `Pattern`).

### Replacement

A fluent, validated replacement for `Pattern.Replace(input, r => ...)`. Group names and numbers are checked against the pattern before anything is replaced, and a missing group throws `ArgumentException`.

| Member | Returns | Description |
|--------|---------|-------------|
| `static With { get; }` | `Replacement` | An empty replacement. Inside `Replace(input, r => ...)` you get one as `r`. |
| `Group(string name)` | `Replacement` | The text a named group captured. |
| `Group(string name, Func<string, string> transform)` | `Replacement` | The same, transformed. |
| `Group(int number)` | `Replacement` | The text a numbered group captured. |
| `Group(int number, Func<string, string> transform)` | `Replacement` | The same, transformed. |
| `Literal(string text)` | `Replacement` | Literal text. Dollar signs are escaped for you. |
| `WholeMatch(Func<string, string> transform = null)` | `Replacement` | The whole match, optionally transformed: `$&`. |
| `BeforeMatch()` | `Replacement` | The input before the match: `` $` ``. |
| `AfterMatch()` | `Replacement` | The input after the match: `$'`. |
| `LastGroup()` | `Replacement` | The last captured group: `$+`. |
| `EntireInput()` | `Replacement` | The entire input: `$_`. |
| `Computed(Func<Match, string> compute)` | `Replacement` | Text computed from the match. |
| `ToString()` | `string` | The equivalent .NET replacement string. Transformed and computed parts appear as `{computed}`. |

```csharp
Pattern date = Pattern.With
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2)).Literal("/")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2)).Literal("/")
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4));

string iso = date.Replace("Due 17/05/2024.", r => r.Group("year").Literal("-").Group("month").Literal("-").Group("day"));

Console.WriteLine(iso);                                                  // Due 2024-05-17.
Console.WriteLine(Replacement.With.Group("year").Literal("-").Group(2)); // ${year}-${2}
```

### CommonPatterns

Ready-made patterns. Each method returns a new `Pattern`. Unless noted, the pattern is anchored with `^...$` for whole-value validation.

| Method | Matches |
|--------|---------|
| `Email()`, `Email(PatternStrictness strictness)` | An e-mail address. `Strict` follows the standard closely; `Loose` accepts anything with the right shape. |
| `Url()`, `Url(PatternStrictness strictness)` | HTTP and HTTPS URLs. |
| `PhoneNumber()` | US phone numbers. See the warning below. |
| `PhoneNumberE164()` | An international number in E.164 format: `+`, a non-zero digit, then at most 14 digits. |
| `IPv4()` | An IPv4 address with octets 0-255. |
| `IPv6()` | An IPv6 address in full notation (8 groups of 4 hex digits). |
| `MacAddress()` | Six pairs of hex digits separated consistently by `:` or `-`. |
| `Guid()` | A hyphenated GUID or UUID. |
| `HexColor()` | A CSS hex color: `#RGB`, `#RRGGBB` or `#RRGGBBAA`. |
| `DateIso()` | `yyyy-MM-dd`. |
| `DateTimeIso()` | An ISO 8601 date and time, such as `2024-05-17T10:30:00.123+02:00`. |
| `Date()` | Common US and ISO date formats. |
| `Time24()` | `HH:mm` or `HH:mm:ss`. |
| `Time12()` | 12-hour time with AM/PM. |
| `CreditCard()` | 16-digit card numbers with optional separators. Pair it with `Validators.Luhn`. |
| `Iban()` | The shape of an IBAN, compact or in groups of four. Pair it with `Validators.IbanChecksum`. |
| `SocialSecurityNumber()` | A US SSN. |
| `ZipCode()` | A US ZIP code, 5-digit or ZIP+4. |
| `Username()` | Letters, digits and underscores, 3 to 20 characters. |
| `StrongPassword()` | A password with several character-type requirements. |
| `Slug()` | Lowercase words separated by single hyphens. |
| `SemVer()` | A Semantic Versioning 2.0.0 version, using the official grammar. |
| `Jwt()` | A compact JSON Web Token: three base64url segments; header and payload start with `eyJ`. |
| `Base64()` | Non-empty standard Base64 with correct padding. |
| `FilePath()` | Windows and Unix file paths. |
| `HtmlTag()` | An HTML or XML tag with optional attributes. |
| `MarkdownLink()` | A Markdown inline link. Not anchored: use it with `Matches` or `ExtractAll`. |
| `Integer()` | A signed integer. |
| `Decimal()` | A signed decimal number. |
| `ScientificNotation()` | A number such as `1.5e10` or `-2E-5`. |

Every method also has an overload that takes a `DigitMode` as its last parameter, such as `Integer(DigitMode digits)` and `Email(PatternStrictness strictness, DigitMode digits)`. The parameterless overloads use `DigitMode.Unicode`, so `\d` in them matches every Unicode decimal digit.

```csharp
Console.WriteLine(CommonPatterns.Email().IsMatch("alice@example.com"));          // True
Console.WriteLine(CommonPatterns.Integer().IsMatch("١٢٣"));                      // True: \d is Unicode
Console.WriteLine(CommonPatterns.Integer(DigitMode.Ascii).IsMatch("١٢٣"));       // False
Console.WriteLine(CommonPatterns.Integer(DigitMode.Ascii));                      // ^-?[0-9]+$
Console.WriteLine(CommonPatterns.SemVer().IsMatch("3.1.0-docs"));                // True
```

:::warning

In 3.1.0, `PhoneNumber()` does not match the formats its documentation lists. It renders `^(?:\(\d{3}\)[\s\-]?)(\(\d{3}\)[\s\-]?|\d{3}[\.\-]?)\d{3}[\.\-]?\d{4}$`, which requires a parenthesized area code followed by a second area code, so `IsMatch("555-123-4567")` and `IsMatch("(555) 123-4567")` both return `false`. Use `PhoneNumberE164()` or build your own pattern until this is fixed.

:::

See [Common patterns](./common-patterns.md).

### PatternStrictness and DigitMode

| Enum | Values |
|------|--------|
| `PatternStrictness` | `Loose` (0): accepts anything with the right overall shape. `Standard` (1): the balanced default of the parameterless overloads. `Strict` (2): follows the relevant standard closely. |
| `DigitMode` | `Unicode` (0): `\d` matches any Unicode decimal digit, the .NET default. `Ascii` (1): `\d` matches only `0-9` and `\D` everything else. |

### ConstrainedPattern and ConstraintResult

A `ConstrainedPattern` is a `Pattern` plus rules a regex cannot express, such as a Luhn checksum or a real calendar date. A match counts only when every rule accepts it. Create one with `pattern.Must(...)` or `new ConstrainedPattern(pattern)`. It is immutable: `Must` returns a copy.

| Member | Returns | Description |
|--------|---------|-------------|
| `ConstrainedPattern(Pattern pattern)` | | A constrained pattern with no rules yet. |
| `Pattern { get; }` | `Pattern` | The underlying pattern. |
| `RuleDescriptions { get; }` | `IReadOnlyList<string>` | The rule descriptions, in evaluation order. |
| `Must(Func<string, bool> rule, string? description = null)` | `ConstrainedPattern` | A copy with a rule for the whole match. |
| `Must(string groupName, Func<string, bool> rule, string? description = null)` | `ConstrainedPattern` | A copy with a rule for a named group. Skipped when the group did not take part. |
| `IsMatch(string input)` | `bool` | Some match in the input passes every rule. |
| `Match(string input)` | `Match` | The first match that passes every rule. |
| `Matches(string input)` | `IEnumerable<Match>` | Every match that passes every rule, scanned lazily. |
| `IsValid(string input)` | `bool` | The whole input matches and passes every rule. The cheap form of `Check`. |
| `Check(string input)` | `ConstraintResult` | Validates the whole input and explains a failure. |

`ConstraintResult` members:

| Member | Returns | Description |
|--------|---------|-------------|
| `IsValid` | `bool` | The input matched and passed every rule. |
| `PatternFailed` | `bool` | The pattern itself rejected the input. |
| `FailedRule` | `string` | The description of the first rule that rejected the input, or `null`. |
| `Trace` | `MatchTrace` | When the pattern rejected the input, the trace that shows where; otherwise `null`. |
| `ToString()` | `string` | A one-line summary that does not include the input. |

```csharp
Pattern card = Pattern.With.StartOfLine.NamedGroup("number", Pattern.With.Digit.Repeat.Times(16)).EndOfLine;
ConstrainedPattern luhn = card.Must("number", Validators.Luhn, "Luhn checksum");

Console.WriteLine(luhn.IsValid("4111111111111111")); // True
ConstraintResult result = luhn.Check("4111111111111112");
Console.WriteLine(result.FailedRule);                // Luhn checksum
Console.WriteLine(result);                           // Invalid: the rule 'Luhn checksum' rejected the value
```

### Validators

Ready-made rules for `Must`. Every validator returns `false` for `null` instead of throwing.

| Method | Returns `true` when |
|--------|---------------------|
| `static bool Luhn(string value)` | The value passes the Luhn (mod 10) checksum, as used by card numbers and IMEIs. Spaces and dashes are ignored; any other non-digit makes it invalid. |
| `static bool IbanChecksum(string value)` | The ISO 13616 mod-97 check digits are right. Spaces are ignored; letters may be in either case. |
| `static bool IsoDate(string value)` | The value is exactly `yyyy-MM-dd` and names a real date: `2024-02-29` is valid, `2026-02-30` is not. |
| `static bool IPv4(string value)` | The value is a dotted quad with each octet 0-255. Leading zeros are accepted. |

```csharp
Console.WriteLine(Validators.Luhn("4111 1111 1111 1111"));               // True
Console.WriteLine(Validators.IbanChecksum("GB82 WEST 1234 5698 7654 32")); // True
Console.WriteLine(Validators.IsoDate("2026-02-30"));                     // False
```

### MatchesPatternAttribute

A data-annotations attribute that validates a string property against a `Pattern` or `ConstrainedPattern` exposed by a public static property, field or parameterless method. `net8.0` and `net10.0` only, because `System.ComponentModel.DataAnnotations` is not part of `netstandard2.1`.

| Member | Description |
|--------|-------------|
| `MatchesPatternAttribute(Type patternType, string memberName)` | Reads the pattern from `memberName` on `patternType`. |
| `PatternType`, `MemberName` | The constructor arguments. |
| `IsValid(object? value, ValidationContext? validationContext)` | Protected. The whole value must match, and every rule must pass. `null` is valid; add `[Required]` to forbid it. |

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Masterly.FluentRegex;

ProductInput input = new ProductInput { Sku = "abc-12" };
List<ValidationResult> errors = new List<ValidationResult>();

Console.WriteLine(Validator.TryValidateObject(input, new ValidationContext(input), errors, true)); // False
Console.WriteLine(errors[0].ErrorMessage); // The field Sku does not match the required pattern.

public sealed class ProductInput
{
    [MatchesPattern(typeof(Patterns), nameof(Patterns.Sku))]
    public string? Sku { get; set; }
}
```

`Patterns.Sku` is the field declared in the [GeneratePatternAttribute](#generatepatternattribute) sample below. The EF Core package can turn the same attribute into a CHECK constraint (`AddCheckConstraintsFromAttributes`).

### RegexGroupAttribute and ExtractionConverters

| Member | Description |
|--------|-------------|
| `RegexGroupAttribute(string name)` | Put it on a constructor parameter or a property to bind it to a group with a different name. `Name` returns the group name. |
| `ExtractionConverters.Register<T>(Func<string, IFormatProvider, T> converter)` | Registers a culture-aware converter for `T`, replacing any existing one. Global and thread-safe. |
| `ExtractionConverters.Register<T>(Func<string, T> converter)` | Registers a culture-independent converter. |
| `ExtractionConverters.Unregister<T>()` | Removes the converter for `T`. Returns `true` when there was one. |

Built in, without a converter: `string`, `object`, the integer types, `float`, `double`, `decimal`, `bool`, `char`, enums (names match ignoring case), `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`, `Uri`, `Version`, and `Nullable<T>` of any of them (an empty group gives `null`). `DateOnly` and `TimeOnly` work on `net8.0` and `net10.0`. Numbers written with Unicode digits, such as `٢٠٢٦`, are converted to ASCII first, because `\d` matches them. A registered converter wins over the built-in one.

### Template and `Template<T>`

Typed templates: string interpolation in reverse. A template such as `"{Date:yyyy-MM-dd} [{Level}] {Message}"` parses text into a typed value and formats the value back.

| Member | Returns | Description |
|--------|---------|-------------|
| `static Template.Of<T>(string template, TemplateOptions? options = null)` | `Template<T>` | Compiles a template for `T`. Throws `ArgumentException` for an empty or malformed template, an unknown member (the message lists the available ones), an unbound required constructor parameter or an unsupported format. |
| `Template<T>.Parse(string input)` | `T` | Parses an input that must match the whole template. Throws `FormatException` with an explanation when it does not. |
| `Template<T>.TryParse(string input, out T value)` | `bool` | The same, returning `false` instead of throwing. |
| `Template<T>.ParseAll(string text)` | `IEnumerable<T>` | Every occurrence of the template in the text. Evaluated lazily. |
| `Template<T>.Format(T value)` | `string` | Renders a value with the same formats and culture, so `Parse(Format(x))` round-trips. Throws `ArgumentException` when a value cannot be written so that it reads back. |
| `Template<T>.Pattern` | `Pattern` | The unanchored pattern for one occurrence, with a named group per placeholder. |
| `Template<T>.ToString()` | `string` | The template text. |

`TemplateOptions` has three `init`-only properties: `Culture` (`IFormatProvider`, default `CultureInfo.InvariantCulture`), `IgnoreCase` (`bool`, for literal text; enum names and booleans always ignore case) and `Timeout` (`TimeSpan?`, `null` for no timeout). Templates are immutable and thread-safe.

```csharp
Template<LogEntry> log = Template.Of<LogEntry>("{Date:yyyy-MM-dd} [{Level}] {Message}");

LogEntry entry = log.Parse("2024-05-17 [WARN] Disk almost full");
Console.WriteLine(entry.Level);       // WARN
Console.WriteLine(log.Format(entry)); // 2024-05-17 [WARN] Disk almost full
Console.WriteLine(log.Pattern);       // (?<Date>\d{4}-\d{2}-\d{2}) \[(?<Level>[^\r\n]*?)\] (?<Message>[^\r\n]*)

public sealed record LogEntry(DateTime Date, string Level, string Message);
```

See [Templates](./templates.md).

### StreamMatch and StreamMatchOptions

| Member | Type | Description |
|--------|------|-------------|
| `StreamMatch.Index` | `long` | Position of the match in the whole input, in characters. |
| `StreamMatch.Length` | `int` | Length of the match. |
| `StreamMatch.Value` | `string` | The matched text. |
| `StreamMatch.Groups` | `IReadOnlyDictionary<string, string>` | Every participating group, keyed by name or by number as a string (`"1"`). Group 0 is not included. |
| `StreamMatchOptions.MaxMatchLength` | `int` | The longest match guaranteed to be found exactly. Default 4096. |
| `StreamMatchOptions.LookbehindContext` | `int` | Characters kept before the scan position for lookbehinds, `\b` and `^`. Default 256. |
| `StreamMatchOptions.BufferSize` | `int` | Characters read at a time. Default 65536. |

The three options throw `ArgumentOutOfRangeException` for zero or negative values.

### Redactor, RedactionStyle and RedactionReport

A `Redactor` finds sensitive values in free text and replaces them. It is immutable: `Add` returns a new redactor. Anchors that wrap a whole validation pattern (`^...$`, `\A...\z`) are turned into word-boundary checks, so `CommonPatterns` work in free text.

| Member | Returns | Description |
|--------|---------|-------------|
| `static Redactor.For(params Pattern[] patterns)` | `Redactor` | A redactor that masks every value the patterns find with `RedactionStyle.Mask`. |
| `Add(Pattern pattern, RedactionStyle? style = null)` | `Redactor` | Also redacts the values this pattern finds. |
| `Add(ConstrainedPattern pattern, RedactionStyle? style = null)` | `Redactor` | Also redacts the values that pass all of its rules. |
| `Mask(string text)` | `string` | The redacted text. |
| `Redact(string text)` | `RedactionReport` | The redacted text and where each value was. |
| `PatternCount` | `int` | The number of registered patterns. |

| `RedactionStyle` member | Description |
|-------------------------|-------------|
| `Mask` | Replaces every letter and digit with `*` and keeps separators: `555-123-4567` becomes `***-***-****`. |
| `Email` | Keeps the first character and the domain: `a***@example.com`. Values without `@` are masked like `Mask`. |
| `KeepLast(int count)` | Masks letters and digits except the last `count`. A value with `count` or fewer letters and digits is masked completely. |
| `KeepFirst(int count)` | Masks letters and digits except the first `count`. |
| `Label(string label)` | Replaces the whole value with the label, such as `[EMAIL]`. |
| `Custom(Func<Match, string> replacement)` | Computes the replacement from the match. |
| `Name`, `LabelText` | The style name as reported, and the label of a `Label` style (otherwise `null`). |

`RedactionReport` has `Text`, `Spans` (ordered by position) and `HasRedactions`. Each `RedactedSpan` has `Start`, `Length`, `RedactedStart`, `RedactedLength`, `Style`, `Label` and `PatternIndex`. Neither keeps the original values, so a report is safe to log.

```csharp
Redactor redactor = Redactor
    .For(CommonPatterns.SocialSecurityNumber())
    .Add(CommonPatterns.Email(), RedactionStyle.Email)
    .Add(CommonPatterns.CreditCard(), RedactionStyle.KeepLast(4));

string text = "SSN 123-45-6789, mail alice@example.com, card 4111-1111-1111-1111.";

Console.WriteLine(redactor.Mask(text));   // SSN ***-**-****, mail a***@example.com, card ****-****-****-1111.
Console.WriteLine(redactor.Redact(text)); // Redacted 3 span(s): [4..15) Mask, [22..39) Email, [46..65) KeepLast(4)
```

See [PII redaction](./pii-redaction.md).

### PatternComparison and PatternRelation

The result of `Pattern.Compare(left, right)`. Every example string is checked with the real .NET engine.

| Member | Type | Description |
|--------|------|-------------|
| `Relation` | `PatternRelation` | How the two languages relate. |
| `AreEquivalent` | `bool` | Both match exactly the same strings. |
| `OnlyLeftExample` | `string` | The shortest string only the left pattern matches, or `null`. |
| `OnlyRightExample` | `string` | The shortest string only the right pattern matches, or `null`. |
| `CommonExample` | `string` | The shortest string both match, or `null`. |
| `ToString()` | `string` | For example `Not equivalent (left is a proper superset of right). Shortest counterexample: "0" (left accepts it, right rejects it). Both accept "00".` |

`PatternRelation` values: `Equivalent` (0), `ProperSubset` (1), `ProperSuperset` (2), `Overlapping` (3), `Disjoint` (4).

### LanguageFacts and MatchScope

The result of `GetLanguageFacts(scope)`. The facts are sound: they hold for every string the pattern accepts.

| Member | Type | Description |
|--------|------|-------------|
| `Scope` | `MatchScope` | The set of strings the facts describe. |
| `MinLength` | `int` | The shortest accepted length. A lower bound when `IsExact` is `false`. |
| `MaxLength` | `int?` | The longest accepted length, or `null` when unbounded. |
| `RequiredPrefix` | `string` | Text every accepted string starts with; empty when none. |
| `RequiredSuffix` | `string` | Text every accepted string ends with. A pattern ending in `$` requires no suffix, because it also accepts a trailing newline. |
| `AcceptsEmpty` | `bool` | The empty string is accepted. |
| `IsEmptyLanguage` | `bool` | No string is accepted. |
| `IsExact` | `bool` | Computed from the automaton (`true`) or approximated from the syntax tree for non-regular constructs (`false`). |

`MatchScope` values: `FullString` (0), the strings the pattern matches entirely; `Substring` (1), the strings for which `IsMatch` returns `true`.

```csharp
Pattern invoice = Pattern.With.Literal("INV-").Digit.Repeat.Times(4, 6);

Console.WriteLine(invoice.GetLanguageFacts(MatchScope.Substring)); // Substring: length 8..unbounded, prefix "", suffix "", accepts empty: false (exact)
```

### OptimizationResult

| Member | Type | Description |
|--------|------|-------------|
| `Original` | `Pattern` | The pattern that was optimized. |
| `Optimized` | `Pattern` | The rewritten pattern, or `Original` when nothing applied. |
| `IsChanged` | `bool` | At least one rewrite applied. |
| `AppliedRewrites` | `IReadOnlyList<string>` | A description of each rewrite, in order, such as `Merged alternation 'a\|b\|c' into '[abc]'`. |
| `VerifiedEquivalent` | `bool` | Language algebra proved both match the same strings. `false` for non-regular constructs, which were checked on sample inputs only. |

### SafetyReport, SafetyIssue, SafetyIssueKind and RiskLevel

| Member | Type | Description |
|--------|------|-------------|
| `SafetyReport.Pattern` | `string` | The analyzed regex. |
| `SafetyReport.Risk` | `RiskLevel` | The highest risk among all issues. |
| `SafetyReport.IsSafe` | `bool` | The risk is `Low` or lower. |
| `SafetyReport.Issues` | `IReadOnlyList<SafetyIssue>` | Every issue, most severe first. |
| `SafetyIssue.Kind`, `Risk` | `SafetyIssueKind`, `RiskLevel` | What the construct is and how dangerous. |
| `SafetyIssue.Position`, `Fragment` | `int`, `string` | Where the construct is in the regex text, and its text. |
| `SafetyIssue.Description`, `Suggestion` | `string` | A plain-language explanation and a suggested fix. |
| `SafetyIssue.AttackInput` | `string` | An input crafted to trigger the backtracking, or `null`. |
| `SafetyIssue.Confirmed` | `bool?` | Whether running the attack input exceeded the budget. `null` unless `confirm: true`. |
| `SafetyIssue.ConfirmationTime` | `TimeSpan?` | How long the confirmation run took. |

| Enum | Values |
|------|--------|
| `RiskLevel` | `None` (0), `Low` (1): not exploitable in practice, `Medium` (2): polynomial backtracking, `High` (3): exponential backtracking. |
| `SafetyIssueKind` | `NestedQuantifier` (0): `(a+)+`. `OverlappingAlternation` (1): `(\w\|\d)+`. `AdjacentOverlappingQuantifiers` (2): `\d+\d+`. |

### MatchTrace, TraceStep and TraceFailure

| Member | Type | Description |
|--------|------|-------------|
| `MatchTrace.Pattern`, `Input` | `string` | The regex and the traced input. |
| `MatchTrace.IsMatch` | `bool` | The pattern matched. |
| `MatchTrace.Index`, `Length`, `Value` | `int`, `int`, `string` | The match; `Index` is -1 and `Value` is `null` when there is none. |
| `MatchTrace.Steps` | `IReadOnlyList<TraceStep>` | The top-level steps of a successful match. |
| `MatchTrace.Failure` | `TraceFailure` | Why the match failed, or `null` on success. |
| `TraceStep.Construct`, `Description` | `string` | The regex text of the construct and a plain-language description. |
| `TraceStep.Index`, `Length`, `Value` | `int`, `int`, `string` | What the construct consumed. |
| `TraceStep.Children` | `IReadOnlyList<TraceStep>` | The steps of its parts. Repeated groups have one child per iteration. |
| `TraceFailure.Position`, `Construct`, `Description` | `int`, `string`, `string` | Where the match died, the construct that failed and what it expected. |
| `TraceFailure.MatchedSoFar`, `Found` | `string` | The text matched before the failure, and the quoted input at the failure position (or `the end of the input`). |
| `TraceFailure.MatchedSteps` | `IReadOnlyList<TraceStep>` | The constructs that matched before the failure. |

### HighlightFormat

| Value | Output |
|-------|--------|
| `Ansi` (0) | ANSI terminal colors: matches get a background color, groups a foreground color, and a one-line legend follows. |
| `Html` (1) | Matches in `<mark class="frx-match">`, groups in `<span class="frx-group frx-group-{name}" title="{name}">`. All text is HTML-encoded. |
| `Brackets` (2) | Plain text for tests and logs: `[...]` for matches and `[name:...]` for groups. |

### ProfileReport, ProfileHotspot and ProfileOptions

| Member | Type | Description |
|--------|------|-------------|
| `ProfileReport.IsMatch` | `bool` | The interpreter found a match. `false` when the budget ran out. |
| `ProfileReport.MatchIndex`, `MatchLength` | `int` | The match, or -1 and 0. |
| `ProfileReport.Steps`, `Backtracks` | `long` | Total attempts and total returns to try another alternative. |
| `ProfileReport.BudgetExhausted` | `bool` | The interpreter hit `StepBudget`: a sign of catastrophic backtracking on this input. |
| `ProfileReport.AgreesWithEngine` | `bool` | The real .NET engine found the same result. `false` when the budget ran out. |
| `ProfileReport.Hotspots` | `IReadOnlyList<ProfileHotspot>` | Constructs that did work, most expensive first. |
| `ProfileReport.ToString()` | `string` | A text heatmap with a table of the top hotspots and a verdict. |
| `ProfileHotspot.Fragment`, `Start`, `Length` | `string`, `int`, `int` | The construct and where it is in the regex text. |
| `ProfileHotspot.Steps`, `Backtracks`, `Share` | `long`, `long`, `double` | The work spent on it, and its fraction of all steps (0 to 1). |
| `ProfileOptions.StepBudget` | `long` | The most steps before giving up. Default 1,000,000. |

### CoverageReport, CoverageItem, CoverageItemKind and PatternCoverage

| Member | Type | Description |
|--------|------|-------------|
| `CoverageReport.Percentage` | `double` | Items exercised at least once, 0 to 100. A pattern without decision points is 100% covered. |
| `CoverageReport.Items`, `Uncovered` | `IReadOnlyList<CoverageItem>` | Every item in pattern order, and the ones never exercised. |
| `CoverageReport.InputsRecorded` | `int` | Inputs recorded, matched or not. |
| `CoverageItem.Kind`, `Description` | `CoverageItemKind`, `string` | What the decision point is, such as `branch "Mrs"`. |
| `CoverageItem.Start`, `Length` | `int` | Where the construct is in the regex text. |
| `CoverageItem.HitCount`, `IsCovered` | `int`, `bool` | How many matches exercised it. |
| `PatternCoverage.Pattern` | `Pattern` | The pattern being recorded. |
| `PatternCoverage.IsMatch(string input)`, `Match(string input)` | `bool`, `Match` | Match through the recorder; every match is recorded. |
| `PatternCoverage.Record(string input)` | `void` | Records every match without returning a result. |
| `PatternCoverage.Report()` | `CoverageReport` | The coverage recorded so far. |

`CoverageItemKind` values: `AlternationBranch` (0), `OptionalPartTaken` (1), `UpperBoundReached` (2).

### PatternProperty, PropertyOptions and PropertyResult

Property-based testing driven by a pattern.

| Member | Returns | Description |
|--------|---------|-------------|
| `static PatternProperty.ForMatching(Pattern pattern, Func<string, bool> property, PropertyOptions? options = null)` | `PropertyResult` | Checks the property for strings the pattern matches. |
| `static PatternProperty.ForNonMatching(Pattern pattern, Func<string, bool> property, PropertyOptions? options = null)` | `PropertyResult` | Checks the property for strings it does not match, mostly near misses. |
| `PropertyOptions.Samples` | `int` | Samples to check. Default 100. |
| `PropertyOptions.Seed` | `int` | The generation seed. Default 0, so runs are reproducible. |
| `PropertyOptions.MaxShrinkSteps` | `int` | Candidates tried while shrinking a counterexample. Default 1000; 0 disables shrinking. |
| `PropertyResult.Passed`, `SamplesTested`, `Seed`, `ShrinkSteps` | | The outcome and how it was reached. |
| `PropertyResult.Counterexample`, `OriginalCounterexample` | `string` | The shrunk and the first failing input, or `null` when it passed. |
| `PropertyResult.Exception` | `Exception` | What the property threw for the counterexample, or `null`. |
| `PropertyResult.ThrowIfFailed()` | `void` | Throws `PropertyFailedException` (with a `Result` property) when the property failed. |

```csharp
Pattern sku = Pattern.With.StartOfLine.Letter.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4).EndOfLine;

PropertyResult result = PatternProperty.ForMatching(sku, s => !s.Contains('0'), new PropertyOptions { Samples = 200, Seed = 7 });

Console.WriteLine(result.Passed);                 // False
Console.WriteLine(result.Counterexample);         // aaa-0000
Console.WriteLine(result.OriginalCounterexample); // URB-5409
```

### FlavorConversion, FlavorOptions, RegexFlavor and PatternExport

`FlavorConversion` is the result of `ToFlavor`.

| Member | Type | Description |
|--------|------|-------------|
| `Flavor` | `RegexFlavor` | The target dialect. |
| `Pattern` | `string` | The translated source, without delimiters or flags. |
| `Flags` | `string` | Flags to compile with, such as `iu`. Always empty for JSON Schema and SQL. |
| `IsExact` | `bool` | Accepts exactly the strings .NET accepts. |
| `Warnings` | `IReadOnlyList<string>` | Every difference in which strings match. Empty when exact. |
| `PreservesMatchExtent` | `bool` | Every match and group captures the same text as in .NET. |
| `MatchExtentWarnings` | `IReadOnlyList<string>` | Differences in what is matched or captured, which matter for extraction and replacement. |
| `GroupNumbers` | `IReadOnlyDictionary<string, int>` | The number of every .NET group in the translation, keyed by name and by .NET number. Numbers can differ because .NET numbers unnamed groups first. |
| `IsSql` | `bool` | The flavor is a SQL dialect. |
| `ToLiteral()` | `string` | Source code: `/.../u` for JavaScript, a quoted JSON string, or a single-quoted SQL literal. `ToString()` returns the same. |

`FlavorOptions` has two settable properties, both `true` by default: `PreserveUnicodeSemantics` (keep the Unicode meaning of `\d`, `\w`, `\s`, `\b`) and `PreserveLineSemantics` (keep .NET's `$`, `.` and multiline anchors). Turning them off gives shorter, idiomatic output and adds a warning.

```csharp
FlavorConversion ascii = digits.ToFlavor(
    RegexFlavor.JavaScript,
    new FlavorOptions { PreserveUnicodeSemantics = false, PreserveLineSemantics = false });

Console.WriteLine(ascii.ToLiteral()); // /^\d+$/u
Console.WriteLine(ascii.IsExact);     // False
Console.WriteLine(ascii.Warnings.Count); // 2
```

`digits` is the pattern from [Pattern: code generation and export](#pattern-code-generation-and-export).

| `RegexFlavor` value | Target |
|---------------------|--------|
| `JavaScript` (0) | ECMAScript 2018+ with the `u` flag. |
| `JsonSchema` (1) | The `pattern` keyword of JSON Schema and OpenAPI: no flags, valid with `u`. |
| `PostgreSql` (2) | PostgreSQL advanced regular expressions (`~`). Options are written into the pattern. |
| `MySql` (3) | MySQL 8.0+ (ICU, `REGEXP_LIKE`). The pattern starts with `(?-i)`. |
| `MariaDb` (4) | MariaDB (PCRE2, `REGEXP`). The pattern starts with `(?-i)`. |
| `SqlServer` (5) | SQL Server 2025 and Azure SQL `REGEXP_LIKE` (RE2): no lookarounds, backreferences or atomic groups. |
| `Sqlite` (6) | SQLite through the EF Core provider, whose `regexp()` runs .NET's non-backtracking engine. |

`PatternExport.ToTypeScript(IReadOnlyDictionary<string, Pattern> patterns, string? header = null)` returns a TypeScript (and JavaScript) module with one exported regex constant per pattern, in ordinal name order, each documented with its .NET source and any warnings.

### LikeConversion, LikeDialect and LikeCollation

`LikeConversion` is the result of `ToLike`. A LIKE pattern is only returned when it accepts every string the regex accepts, so it can always pre-filter rows.

| Member | Type | Description |
|--------|------|-------------|
| `Expression` | `string` | The LIKE pattern, unquoted. |
| `EscapeCharacter` | `char?` | The `ESCAPE` character, or `null` when none is needed. |
| `IsExact` | `bool` | LIKE accepts exactly the regex's strings in `Scope` under `Collation`, proven by language algebra. |
| `IsSuperset` | `bool` | Always `true`. |
| `Dialect`, `Scope`, `Collation` | | The settings the proof assumed. |
| `Warnings` | `IReadOnlyList<string>` | Why it is not exact, plus remarks. |
| `ToSql(string columnSql)` | `string` | A condition for raw SQL, such as `[Number] LIKE N'INV-[0-9][0-9][0-9][0-9]'`. |

| Enum | Values |
|------|--------|
| `LikeDialect` | `Standard` (0): `%` and `_` only. `SqlServer` (1): adds `[set]`, `[^set]` and ranges. `PostgreSql` (2). `MySql` (3), also for MariaDB. `Sqlite` (4). |
| `LikeCollation` | `Binary` (0): characters compare by code point. `CaseInsensitive` (1): letters match regardless of case. |

```csharp
Pattern anchoredByLine = Pattern.With.StartOfLine.Literal("INV-").AsciiDigit.Repeat.Times(4).EndOfLine;

Console.WriteLine(anchoredByLine.ToLike(LikeDialect.SqlServer, MatchScope.FullString)); // INV-[0-9][0-9][0-9][0-9]% (superset)
Console.WriteLine(anchoredByLine.ToLike(LikeDialect.Standard, MatchScope.FullString));  // INV-____% (superset)
```

- The `$` in this pattern also accepts a trailing newline, so LIKE needs a final `%` and is only a superset. The `\A...\z` pattern in [Pattern: code generation and export](#pattern-code-generation-and-export) gives an exact translation.

### UnicodeScript and UnicodeGroup

| Enum | Values |
|------|--------|
| `UnicodeScript` | `Latin`, `Greek`, `Cyrillic`, `Armenian`, `Hebrew`, `Arabic`, `Syriac`, `Thaana`, `Devanagari`, `Bengali`, `Gurmukhi`, `Gujarati`, `Tamil`, `Telugu`, `Kannada`, `Malayalam`, `Thai`, `Lao`, `Tibetan`, `Georgian`, `Hangul`, `Ethiopic`, `Khmer`, `Mongolian`, `Hiragana`, `Katakana`, `Han` (0 to 26, in this order). Used by `Script`, `LetterIn` and `DigitIn`. |
| `UnicodeGroup` | `Letter` (`\p{L}`), `Mark` (`\p{M}`), `Number` (`\p{N}`), `Punctuation` (`\p{P}`), `Symbol` (`\p{S}`), `Separator` (`\p{Z}`), `Other` (`\p{C}`). |

### GeneratePatternAttribute

Evaluates a static `Pattern` field or property at build time and generates a `{Member}Source` string constant and a method that returns a cached `Regex`. The containing type must be `partial`.

| Member | Description |
|--------|-------------|
| `GeneratePatternAttribute()` | The method is named after the member with a `Regex` suffix, such as `SkuRegex()`. |
| `GeneratePatternAttribute(string methodName)` | Uses the given method name. |
| `MethodName` | The custom name, or `null` for the default. |

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Regex sku = Patterns.SkuRegex();

Console.WriteLine(Patterns.SkuSource);                         // ^[A-Z]{3}-\d{4}$
Console.WriteLine(sku.IsMatch("ABC-1234"));                    // True
Console.WriteLine(Patterns.IsInvoice().IsMatch("INV-000042")); // True
Console.WriteLine(ReferenceEquals(sku, Patterns.SkuRegex()));  // True

public static partial class Patterns
{
    [GeneratePattern]
    public static readonly Pattern Sku =
        Pattern.With.StartOfLine.UppercaseLetter.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4).EndOfLine;

    [GeneratePattern("IsInvoice")]
    public static Pattern Invoice => Pattern.With.StartOfLine.Literal("INV-").Digit.Repeat.Times(6).EndOfLine.WithAsciiDigits();
}
```

:::warning

The build-time evaluator supports the core builder (anchors, matchers, literals, sets, ranges, Unicode categories and blocks, quantifiers, groups, assertions, composition, `WithOptions`, `WithTimeout`, `WithAsciiDigits`) and `CommonPatterns`. Members such as `AsciiDigit`, `Script`, `LetterIn`, `DigitIn`, `CodePoint`, `Grapheme` and `IgnoreDiacritics` cannot be evaluated: the analyzer reports `FRX001` and no method is generated. In the sample above, `Digit` with `WithAsciiDigits()` is used instead of `AsciiDigit` for that reason.

:::

### Compile-time diagnostics

The core package includes Roslyn analyzers and a code fix. They run for every target framework. See [Compile-time checks](./compile-time-checks.md).

| ID | Severity | Reported when |
|----|----------|---------------|
| `FRX001` | Warning | A `[GeneratePattern]` member cannot be evaluated at compile time. |
| `FRX002` | Error | The type containing a `[GeneratePattern]` member is not `partial`. |
| `FRX003` | Error | A pattern produces an invalid regular expression. |
| `FRX004` | Warning | A pattern is vulnerable to exponential backtracking (ReDoS, high risk). |
| `FRX005` | Info | A pattern may backtrack polynomially (medium risk). |
| `FRX006` | Error | Building the pattern throws, for example `.Lazy` without a quantifier. |
| `FRX007` | Info | A compile-time constant pattern can be backed by a `[GeneratedRegex]` method. A code fix generates it. |
| `FRX008` | Warning | An `Extract<T>` target member has no matching group, so it will never be set. |
| `FRX009` | Error | A `[GeneratePattern]` member is not `static`. |

### Target framework availability

The core package targets `netstandard2.1`, `net8.0` and `net10.0`. Almost everything is available on all three. These members are not:

| Member | `netstandard2.1` | `net8.0` | `net10.0` |
|--------|------------------|----------|-----------|
| `UseNonBacktracking()` | No | Yes | Yes |
| `IsMatch(ReadOnlySpan<char>)`, `Count(ReadOnlySpan<char>)`, `EnumerateMatches(ReadOnlySpan<char>)` | No | Yes | Yes |
| `EnumerateSplits(ReadOnlySpan<char>)` | No | No | Yes |
| `MatchesPatternAttribute` | No | Yes | Yes |
| `DateOnly` and `TimeOnly` in typed extraction and templates | No | Yes | Yes |

`Count(string)` works on all three. On `netstandard2.1` it counts by walking the matches.

## Masterly.FluentRegex.EntityFrameworkCore

Translates patterns to the database's regex dialect in LINQ queries, CHECK constraints and raw SQL. Namespace `Masterly.FluentRegex.EntityFrameworkCore`. See [Entity Framework Core](./entity-framework-core.md).

### UseFluentRegex and FluentRegexOptionsBuilder

| Member | Returns | Description |
|--------|---------|-------------|
| `UseFluentRegex(this DbContextOptionsBuilder builder, Action<FluentRegexOptionsBuilder>? configure = null)` | `DbContextOptionsBuilder` | Enables translation and CHECK constraints. The dialect is detected from the provider: Npgsql, Pomelo MySQL/MariaDB, SQL Server or SQLite. |
| `UseFluentRegex<TContext>(this DbContextOptionsBuilder<TContext> builder, Action<FluentRegexOptionsBuilder>? configure = null)` | `DbContextOptionsBuilder<TContext>` | The generic version. |
| `UseDialect(SqlDialect dialect)` | `FluentRegexOptionsBuilder` | Overrides detection. Needed for unknown providers and to choose `SqlServer2025` when the provider's compatibility level is not set. |
| `UseIndexHints(bool enabled = true)` | `FluentRegexOptionsBuilder` | Adds proven conditions such as `column LIKE 'prefix%'` and a length bound next to the regex, so a B-tree index can narrow the scan. On by default. |
| `UseLikeFallback(LikeFallback fallback)` | `FluentRegexOptionsBuilder` | How patterns translate on SQL Server before 2025. Default `ExactOnly`. |
| `UseLikeCollation(LikeCollation collation)` | `FluentRegexOptionsBuilder` | States whether SQL Server columns use a binary collation (`_BIN2`, `_BIN`). With the default, `CaseInsensitive`, LIKE comparisons are wrapped in `COLLATE Latin1_General_100_BIN2`. |
| `AddCheckConstraintsFromAttributes(bool enabled = true)` | `FluentRegexOptionsBuilder` | Creates a CHECK constraint for every string property with `[MatchesPattern]`. A `ConstrainedPattern` contributes only its pattern; its rules run in .NET only. |

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

// options is the DbContextOptionsBuilder from OnConfiguring or AddDbContext; connectionString comes from your configuration
options
    .UseSqlServer(connectionString, sql => sql.UseCompatibilityLevel(160))
    .UseFluentRegex(fluent => fluent
        .UseDialect(SqlDialect.SqlServer)
        .UseLikeFallback(LikeFallback.AllowSuperset)
        .UseLikeCollation(LikeCollation.Binary)
        .AddCheckConstraintsFromAttributes());
```

### Query functions

| Member | Returns | Description |
|--------|---------|-------------|
| `PatternFunctions.Matches(string? value, Pattern pattern)` | `bool` | `true` when the pattern matches anywhere in the value; `false` for `null`. Translated to the dialect's regex predicate. |
| `PatternFunctions.Group(string? value, Pattern pattern, string groupName)` | `string` | The named group in the first match, or `null`. Translated on PostgreSQL and SQL Server 2025. |
| `PatternFunctions.Group(string? value, Pattern pattern, int groupNumber)` | `string` | The numbered group in the first match, or `null`. |
| `PatternFunctions.Replace(string? value, Pattern pattern, string replacement)` | `string` | Replaces every match; .NET replacement syntax. Translated on PostgreSQL, MySQL, MariaDB and SQL Server 2025. |
| `EF.Functions.Matches(...)`, `EF.Functions.Group(...)`, `EF.Functions.Replace(...)` | | The same functions as `DbFunctions` extensions. They work only inside a query. |

`PatternFunctions` also run in .NET outside a query, with the same results, so one expression works against a database and in memory.

### Queryable extensions

| Member | Returns | Description |
|--------|---------|-------------|
| `WhereMatches<T>(this IQueryable<T> source, Expression<Func<T, string>> selector, Pattern pattern)` | `IQueryable<T>` | Filters entirely in SQL. Throws like the translator when the dialect cannot express the pattern. |
| `MatchingAsync<T>(this IQueryable<T> source, Expression<Func<T, string>> selector, Pattern pattern, CancellationToken cancellationToken = default)` | `IAsyncEnumerable<T>` | Works on every database: SQL applies a proven superset (exact regex, or a `LIKE` and length bound, or only a NULL check) and .NET applies the exact pattern. |
| `MatchingAsync<T>(..., ConstrainedPattern pattern, CancellationToken cancellationToken = default)` | `IAsyncEnumerable<T>` | The same; the rules run in .NET. |
| `ToMatchingListAsync<T>(..., Pattern pattern, CancellationToken cancellationToken = default)` | `Task<List<T>>` | Loads the matching rows into a list. |
| `ToMatchingListAsync<T>(..., ConstrainedPattern pattern, CancellationToken cancellationToken = default)` | `Task<List<T>>` | The same with rules. |

### HasPattern, SqlRegex and the enums

| Member | Returns | Description |
|--------|---------|-------------|
| `HasPattern<TProperty>(this PropertyBuilder<TProperty> builder, Pattern pattern, string? constraintName = null)` | `PropertyBuilder<TProperty>` | Adds a CHECK constraint that every non-null value must pass. Same semantics as `IsMatch`, so anchor the pattern to constrain the whole value. |
| `SqlRegex.Condition(string columnSql, Pattern pattern, SqlDialect dialect)` | `string` | A raw SQL condition for Dapper, ADO.NET or migrations. On `SqlServer` it returns the exact `LIKE` translation under `COLLATE Latin1_General_100_BIN2`, or throws `InvalidOperationException` when there is none. |
| `SqlRegex.Condition(string columnSql, Pattern pattern, SqlDialect dialect, LikeCollation collation)` | `string` | The same, stating the column's collation. With `Binary` on SQL Server, no `COLLATE` is added, so an index can be used. |

| Enum or class | Values |
|---------------|--------|
| `SqlDialect` | `PostgreSql` (0), `MySql` (1), `MariaDb` (2), `SqlServer2025` (3), `SqlServer` (4): before 2025, no regex, LIKE only, `Sqlite` (5). |
| `LikeFallback` | `ExactOnly` (0): LIKE only when exact, otherwise throw (the default). `AllowSuperset` (1): a superset LIKE plus a logged warning. `Never` (2): every pattern throws. |
| `FluentRegexEventId` | `LikeSupersetFallback` and `TranslationWarning`, both `static readonly EventId`, for `ConfigureWarnings`. |

```csharp
using System;
using System.Linq;
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

// db is a ShopContext configured with UseSqlite(connection).UseFluentRegex()
IQueryable<Product> query = db.Products.Where(p => PatternFunctions.Matches(p.Sku, ShopPatterns.Prefix));
Console.WriteLine(query.ToQueryString());
// prints:
// SELECT "p"."Id", "p"."Sku"
// FROM "Products" AS "p"
// WHERE "p"."Sku" REGEXP '\AABC-' AND "p"."Sku" LIKE 'ABC-%' ESCAPE '\'

Console.WriteLine(SqlRegex.Condition("\"Sku\"", ShopPatterns.Sku, SqlDialect.PostgreSql)); // "Sku" ~ '\A[A-Z]{3}-[0-9]{4}\Z'
Console.WriteLine(SqlRegex.Condition("[Sku]", ShopPatterns.Prefix, SqlDialect.SqlServer));  // [Sku] COLLATE Latin1_General_100_BIN2 LIKE N'ABC-%'

public static class ShopPatterns
{
    public static readonly Pattern Sku =
        Pattern.With.StartOfInput.UppercaseLetter.Repeat.Times(3).Literal("-").AsciiDigit.Repeat.Times(4).EndOfInput;

    public static readonly Pattern Prefix = Pattern.With.StartOfInput.Literal("ABC-");
}

public sealed class Product
{
    public int Id { get; set; }

    public string Sku { get; set; } = "";
}

public sealed class ShopContext : DbContext
{
    public ShopContext(DbContextOptions<ShopContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite: CONSTRAINT "CK_Product_Sku" CHECK ("Sku" REGEXP '\A[A-Z]{3}-[0-9]{4}\z')
        modelBuilder.Entity<Product>()
            .Property(p => p.Sku)
            .HasPattern(ShopPatterns.Sku, constraintName: "CK_Product_Sku");
    }
}
```

- The `LIKE 'ABC-%'` in the query is an index hint from `UseIndexHints`. It never changes the result.
- On SQLite, saving a `Product` with `Sku = "bad"` fails with `SQLite Error 19: 'CHECK constraint failed: CK_Product_Sku'.`.
- `PatternFunctions.Replace` uses .NET group numbering, where unnamed groups come before named ones. In `(?<prefix>[A-Z]{3})-([0-9]{4})`, `$1` is the digits and `$2` is `prefix`.

## Masterly.FluentRegex.AspNetCore

Named patterns for ASP.NET Core and the `pattern` route constraint. See [ASP.NET Core](./aspnetcore.md).

| Member | Returns | Description |
|--------|---------|-------------|
| `AddFluentPatterns(this IServiceCollection services, Action<PatternRegistry> configure)` | `IServiceCollection` | Registers a `PatternRegistry` singleton and the `pattern` route constraint. Calling it again adds to the same registry. In `Microsoft.Extensions.DependencyInjection`. |
| `FluentPatternsServiceCollectionExtensions.ConstraintName` | `const string` | `"pattern"`, as in `{id:pattern(sku)}`. |
| `PatternRegistry.Add(string name, Pattern pattern)` | `PatternRegistry` | Registers a pattern. Route values must match it completely. Throws `ArgumentException` for a duplicate name. |
| `PatternRegistry.Add(string name, ConstrainedPattern pattern)` | `PatternRegistry` | Registers a pattern with rules; every rule must also pass. |
| `PatternRegistry.Get(string name)` | `ConstrainedPattern` | The registered pattern. Throws `InvalidOperationException` that lists the registered names when it is missing. |
| `PatternRegistry.TryGet(string name, out ConstrainedPattern? pattern)` | `bool` | The same without throwing. |
| `PatternRegistry.Contains(string name)` | `bool` | A pattern with the name exists. |
| `PatternRegistry.Names` | `IReadOnlyList<string>` | Registered names in ordinal order. |
| `PatternRouteConstraint(PatternRegistry registry, string name)` | | The constraint behind `pattern(name)`. `Name` and `Pattern` return its settings; `Match(...)` accepts a route value only when it matches the whole pattern and every rule passes. |

Names are case-insensitive. The registry is thread-safe.

```csharp
using Masterly.FluentRegex;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

// builder is your WebApplicationBuilder
builder.Services.AddFluentPatterns(registry => registry
    .Add("sku", ShopPatterns.Sku)
    .Add("card", ShopPatterns.Card.Must(Validators.Luhn, "Luhn checksum")));

WebApplication app = builder.Build();

app.MapGet("/products/{sku:pattern(sku)}", (string sku) => $"Product {sku}");
app.MapGet("/cards/{number:pattern(card)}", (string number) => "Valid card");

// GET /products/ABC-1234          -> 200
// GET /products/abc               -> 404
// GET /cards/4111111111111111     -> 200
// GET /cards/4111111111111112     -> 404 (fails the Luhn rule)

public static class ShopPatterns
{
    public static readonly Pattern Sku =
        Pattern.With.StartOfLine.UppercaseLetter.Repeat.Times(3).Literal("-").AsciiDigit.Repeat.Times(4).EndOfLine;

    public static readonly Pattern Card = Pattern.With.StartOfLine.AsciiDigit.Repeat.Times(16).EndOfLine;
}
```

## Masterly.FluentRegex.Xunit

Theory data attributes for xUnit v3 that generate test strings from a pattern. The pattern is read from a public static property, field or parameterless method. See [xUnit](./xunit.md).

| Member | Description |
|--------|-------------|
| `MatchingDataAttribute(Type patternSource, string memberName)` | Supplies strings the pattern matches. |
| `NonMatchingDataAttribute(Type patternSource, string memberName)` | Supplies strings the pattern does not match, mostly near misses. |
| `PatternDataAttribute` | The abstract base. `PatternSource`, `MemberName`, `Count` (default 25), `Seed` (default 0). Derive from it and override `protected abstract string[] Generate(Pattern pattern, int count, int seed)` for your own samples. |

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.Xunit;
using Xunit;

public static class TestPatterns
{
    public static Pattern Sku =>
        Pattern.With.StartOfLine.UppercaseLetter.Repeat.Times(3).Literal("-").AsciiDigit.Repeat.Times(4).EndOfLine;
}

public class SkuTests
{
    [Theory]
    [MatchingData(typeof(TestPatterns), nameof(TestPatterns.Sku), Count = 10, Seed = 42)]
    public void Sku_GeneratedValidValue_IsAccepted(string sku)
    {
        Assert.True(TestPatterns.Sku.IsMatch(sku));
    }

    [Theory]
    [NonMatchingData(typeof(TestPatterns), nameof(TestPatterns.Sku), Count = 10)]
    public void Sku_NearMiss_IsRejected(string sku)
    {
        Assert.False(TestPatterns.Sku.IsMatch(sku));
    }
}
```

- Each attribute supplies one `string` per row, so the theory's first parameter must accept a `string`.
- The attributes throw `InvalidOperationException` with a clear message when `Count` is below 1, the member is missing, is not a `Pattern` or returns `null`, or no samples could be generated.
- Rows are deterministic, so they are listed during test discovery. Both attributes can be applied more than once.

## Masterly.FluentRegex.AI

Drafts a pattern from a plain-English description with Claude, then verifies every draft locally with the real .NET engine. See [AI drafting](./ai-drafting.md).

| Member | Returns | Description |
|--------|---------|-------------|
| `PatternAssistant(HttpClient httpClient, PatternAssistantOptions options)` | | Throws `ArgumentNullException` for `null` arguments and `ArgumentException` or `ArgumentOutOfRangeException` for invalid options, such as a missing API key. |
| `DraftAsync(string description, IEnumerable<string>? shouldMatch = null, IEnumerable<string>? shouldNotMatch = null, CancellationToken cancellationToken = default)` | `Task<PatternDraft>` | Asks for a regex, verifies it and sends failures back as feedback, up to `MaxAttempts` drafts. Positive examples must match the whole string; negative examples must not match anywhere. Throws `PatternAssistantException` when the API cannot produce a draft. |

| `PatternAssistantOptions` property | Default | Description |
|------------------------------------|---------|-------------|
| `ApiKey` | none | The Anthropic API key. Required. Never included in messages or `ToString()`. |
| `Model` | `claude-opus-5-5` (`DefaultModel`) | The Claude model id. |
| `Effort` | `medium` (`DefaultEffort`) | `low`, `medium`, `high`, `xhigh` or `max`. |
| `MaxAttempts` | 3 | How many drafts the model may produce. |
| `MaxTokens` | 16000 | The response token limit per request. |
| `Timeout` | 5 minutes | The time limit per request, or `Timeout.InfiniteTimeSpan`. |
| `BaseAddress` | `https://api.anthropic.com/` | The API root. |
| `UseServerSideFallback` | `true` | Retry a declined request on Anthropic's fallback model. Turn it off for gateways that do not support it. |

| Type | Members |
|------|---------|
| `PatternDraft` | `Pattern` (`null` when no valid syntax was produced), `Regex`, `Explanation`, `Passed`, `Attempts`, `Verification`. When no draft passed, this is the best failed attempt. |
| `PatternVerification` | `IsValidSyntax`, `SyntaxError`, `SafetyReport`, `HasSafetyFindings`, `RejectedPositives`, `AcceptedNegatives`, `Samples` (up to five generated matches), `Passed`. |
| `PatternAssistantException` | `StatusCode`, `ErrorType`, `RequestId`, `RetryAfter`, `IsTransient` (rate limits, overload, server errors, timeouts and network failures). |

```csharp
using System;
using System.Net.Http;
using Masterly.FluentRegex;
using Masterly.FluentRegex.AI;

// http is an HttpClient from your application
PatternAssistant assistant = new PatternAssistant(http, new PatternAssistantOptions
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "",
});

try
{
    PatternDraft draft = await assistant.DraftAsync(
        "a UK postcode such as SW1A 1AA",
        shouldMatch: new[] { "SW1A 1AA", "M1 1AE" },
        shouldNotMatch: new[] { "12345", "SW1A1AA1" });

    Console.WriteLine(draft.Passed ? draft.Regex : draft.Verification.ToString());
}
catch (PatternAssistantException ex) when (ex.IsTransient)
{
    Console.WriteLine($"Try again after {ex.RetryAfter}: {ex.Message}");
}
```

## Masterly.FluentRegex.Tool

The `fluentregex` command-line tool. Install it with `dotnet tool install -g Masterly.FluentRegex.Tool`. See [Command-line tool](./command-line-tool.md).

| Command | What it does |
|---------|--------------|
| `convert <regex>` / `convert --file <path>` | Prints fluent C# for a regex, or for every regex literal in C# files. |
| `analyze <regex> [--confirm]` | Reports ReDoS risks. |
| `scan <path> [--fail-on high\|medium\|none] [--confirm]` | Scans C# files for risky regex literals. CI friendly. |
| `explain <regex>` | Explains a regex in plain English. |
| `generate <regex> [--count N] [--seed N] [--non-matching]` | Generates matching or near-miss strings. |
| `trace <regex> <input>` | Shows which part matched what, or where it failed. |
| `diagram <regex> [--format svg\|mermaid] [--output <file>]` | Draws a railroad diagram or a Mermaid flowchart. |
| `compare <regexA> <regexB>` | Proves equivalence or shows the shortest counterexample. |
| `optimize <regex>` | Simplifies a regex with verified rewrites. |
| `learn <example>... [--not <example>...]` | Infers a regex from examples, or `--from-file <path>`. |
| `profile <regex> <input>` | Counts backtracking steps and shows hotspots. |
| `highlight <regex> <file\|-> [--format ansi\|html\|brackets]` | Shows matches and groups in a text file. |
| `coverage <regex> <file\|->` | Reports which branches and limits the lines of a file exercise. |
| `export <regex> [--to js\|json-schema\|typescript\|postgresql\|mysql\|mariadb\|sqlserver\|sqlite] [--column <sql>] [--name <identifier>]` | Translates to another dialect, or a full `WHERE` condition with `--column`. |
| `export <regex> --to like [--dialect ...] [--scope substring\|full] [--case-insensitive] [--column <sql>]` | Compiles to SQL LIKE and proves whether it is exact. |
| `facts <regex> [--scope full\|substring]` | Proves length bounds and the required prefix and suffix. |

Most commands also accept `--options <list>` (comma-separated `RegexOptions`) and `--ascii-digits`. `--help` and `--version` print help and the version.

```bash
fluentregex compare '\d{2}' '\d{1,3}'
# Not equivalent (left is a proper subset of right). Shortest counterexample: "0" (right accepts it, left rejects it). Both accept "00".
# exit code 1
```

Exit codes: 0 for success, 2 for a usage or input error, and 1 when a command reports a finding: `analyze` (high risk), `scan` (at or above `--fail-on`), `compare` (not equivalent), `profile` (step budget exhausted) and `coverage` (something uncovered).
