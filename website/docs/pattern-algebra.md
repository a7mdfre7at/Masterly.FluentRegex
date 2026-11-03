---
id: pattern-algebra
title: "Pattern Algebra and Equivalence"
description: "Prove that two patterns match the same strings, find the shortest counterexample when they do not, and build intersections, differences and unions."
---

Two regular expressions that look different can match exactly the same strings. Two that look alike can differ in a way no unit test thought to check. Pattern algebra answers these questions with a proof instead of a guess: it turns each pattern into an automaton, compares the automata, and hands you the shortest string that shows any difference.

Use this page when you need to:

- prove that a rewritten or refactored regex still matches the same strings as the old one,
- find out how two patterns relate (equal, one inside the other, overlapping or disjoint),
- get the shortest string that one pattern accepts and the other rejects,
- check whether a pattern can match anything at all, and what its shortest match is,
- build a new pattern that matches the strings two patterns share, the strings one has and the other lacks, or the strings either one matches.

All members on this page live in the `Masterly.FluentRegex` namespace and work on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10.

| Member | Kind | Returns |
|--------|------|---------|
| `Pattern.Compare(left, right)` | Static method | A `PatternComparison`: the relation plus the shortest examples. |
| `IsEquivalentTo(other)` | Method | `true` when both patterns match exactly the same strings. |
| `IsSubsetOf(other)` | Method | `true` when every string this pattern matches, `other` matches too. |
| `Overlaps(other)` | Method | `true` when at least one string matches both patterns. |
| `MatchesNothing()` | Method | `true` when no string at all matches this pattern. |
| `ShortestMatch()` | Method | The shortest string this pattern matches, or `null`. |
| `Intersect(other)` | Method | A new pattern for the strings both patterns match. |
| `Except(other)` | Method | A new pattern for the strings this pattern matches and `other` rejects. |
| `Union(other)` | Method | A new pattern for the strings either pattern matches. |
| `PatternComparison` | Class | The result of `Compare`. See [Reading a comparison](#reading-a-comparison). |
| `PatternRelation` | Enum | `Equivalent`, `ProperSubset`, `ProperSuperset`, `Overlapping`, `Disjoint`. |

## A first comparison

`\d` and `[0-9]` look the same to most people. The algebra proves they are not:

```csharp
using System;
using Masterly.FluentRegex;

PatternComparison result = Pattern.Compare(Pattern.FromRegex(@"\d{3}"), Pattern.FromRegex("[0-9]{3}"));

Console.WriteLine(result.Relation);         // ProperSuperset
Console.WriteLine(result.OnlyLeftExample);  // 00٠  (the last character is U+0660 ARABIC-INDIC DIGIT ZERO)
Console.WriteLine(result.CommonExample);    // 000
Console.WriteLine(result);
// Not equivalent (left is a proper superset of right). Shortest counterexample: "00٠"
// (left accepts it, right rejects it). Both accept "000".
```

- In .NET, `\d` matches every Unicode decimal digit, not just `0` to `9`. The algebra finds that on its own and shows you a three-character string that proves it.
- `ProperSuperset` means the left pattern matches everything the right one matches, and more.
- Every example string has been checked with the real .NET regex engine before it is returned.

To make `\d` mean `0-9` only, see [Options and Unicode](#options-and-unicode) and [Unicode](./unicode.md).

## What "the strings a pattern matches" means

The algebra works on the *language* of a pattern: the set of strings it matches **in full**, as if it were wrapped in `\A(?:...)\z`. So `[a-z]+` is about strings made only of lowercase letters. It is not about strings that merely *contain* a lowercase letter, even though `IsMatch("x1")` returns `true` for it.

The pattern's own `RegexOptions` and any inline options such as `(?i)` are honored. Every character class is computed by the real .NET engine over all 65,536 UTF-16 code units, so Unicode categories, class subtraction and case-insensitivity rules are exact (case-insensitivity follows the current culture, see [IgnoreCase follows the current culture](#ignorecase-follows-the-current-culture)).

### $ also accepts a final newline

An explicit `$` or `\Z` at the end keeps its .NET meaning: it matches at the end of the input **or just before a final `\n`**. That is a classic validation bug, and the algebra reports it:

```csharp
PatternComparison result = Pattern.Compare(Pattern.FromRegex(@"^\d+$"), Pattern.FromRegex(@"\A\d+\z"));

Console.WriteLine(result.Relation);  // ProperSuperset
Console.WriteLine(result);
// Not equivalent (left is a proper superset of right). Shortest counterexample: "0\n"
// (left accepts it, right rejects it). Both accept "0".
```

The fluent `StartOfLine` and `EndOfLine` write `^` and `$`, so they have the same behavior:

```csharp
Pattern anchored = Pattern.With.StartOfLine.Digit.Repeat.Times(3).EndOfLine;

Console.WriteLine(anchored);  // ^\d{3}$
Console.WriteLine(Pattern.Compare(anchored, Pattern.FromRegex(@"\d{3}")));
// Not equivalent (left is a proper superset of right). Shortest counterexample: "000\n"
// (left accepts it, right rejects it). Both accept "000".
```

:::tip

When you validate whole inputs, prefer `\A` and `\z` (or `StartOfInput` and `EndOfInput` in the fluent API, see [Assertions](./assertions.md)). Use `Pattern.Compare` against a `\z` version to find out whether your `$` lets a trailing newline through.

:::

### Strings are sequences of UTF-16 code units

Each character the algebra reasons about is one UTF-16 code unit, exactly as the .NET engine sees it. A character outside the Basic Multilingual Plane, such as an emoji, is two code units. So `.` never matches an emoji on its own:

```csharp
Console.WriteLine(Pattern.Compare(Pattern.FromRegex("."), Pattern.FromRegex("😀")));
// Not equivalent (no string matches both). Shortest counterexample: "a" (left accepts it, right rejects it);
// also "\uD83D\uDE00" (right accepts it, left rejects it).
```

The emoji is shown as its two surrogate code units, because `ToString()` escapes surrogates (see [The ToString message](#the-tostring-message)).

## Comparing two patterns

`Pattern.Compare(left, right)` is the core of the algebra. Everything else on this page is built on it.

```csharp
PatternComparison result = Pattern.Compare(Pattern.FromRegex("[ab]+"), Pattern.FromRegex("[bc]+"));

Console.WriteLine(result.Relation);          // Overlapping
Console.WriteLine(result.OnlyLeftExample);   // a
Console.WriteLine(result.OnlyRightExample);  // c
Console.WriteLine(result.CommonExample);     // b
Console.WriteLine(result.AreEquivalent);     // False
```

- The order of the arguments matters. "Left" is the first argument and "right" is the second. `ProperSubset` and `ProperSuperset` swap when you swap the arguments.
- Both arguments must be non-null. A `null` argument throws `ArgumentNullException` whose `ParamName` is `left` or `right`.
- A pattern that uses a construct the algebra cannot model throws `NotSupportedException`. A pattern that is too large to analyze throws `InvalidOperationException`. See [What is not supported](#what-is-not-supported) and [Limits](#limits).

### Reading a comparison

`PatternComparison` is an immutable result object:

| Member | Type | Meaning |
|--------|------|---------|
| `Relation` | `PatternRelation` | How the two languages relate. |
| `OnlyLeftExample` | `string?` | The shortest string the left pattern accepts and the right one rejects, or `null` if there is none. |
| `OnlyRightExample` | `string?` | The shortest string the right pattern accepts and the left one rejects, or `null` if there is none. |
| `CommonExample` | `string?` | The shortest string both patterns accept, or `null` if there is none. |
| `AreEquivalent` | `bool` | `true` when `Relation` is `Equivalent`. |
| `ToString()` | `string` | A one-line explanation with the shortest counterexample. |

The relation is derived from which examples exist:

| `Relation` | `OnlyLeftExample` | `OnlyRightExample` | `CommonExample` | Meaning |
|------------|-------------------|--------------------|-----------------|---------|
| `Equivalent` | `null` | `null` | Usually set | Both patterns match exactly the same strings. |
| `ProperSubset` | `null` | Set | Usually set | Every string left matches, right matches too, but not the other way round. |
| `ProperSuperset` | Set | `null` | Usually set | Every string right matches, left matches too, but not the other way round. |
| `Overlapping` | Set | Set | Set | Some strings match both, and each pattern also matches strings the other rejects. |
| `Disjoint` | Set | Set | `null` | No string matches both patterns. |

`CommonExample` is `null` only when no string matches both patterns. For `Equivalent` that happens only when both patterns match nothing at all.

### The five relations by example

```csharp
Console.WriteLine(Pattern.Compare(Pattern.FromRegex("a|ab"), Pattern.FromRegex("ab?")).Relation);              // Equivalent
Console.WriteLine(Pattern.Compare(Pattern.FromRegex("(?:ab)+"), Pattern.FromRegex("a(?:ba)*b")).Relation);     // Equivalent
Console.WriteLine(Pattern.Compare(Pattern.FromRegex("[0-9]{3}"), Pattern.FromRegex(@"\d{3}")).Relation);      // ProperSubset
Console.WriteLine(Pattern.Compare(Pattern.FromRegex("a*"), Pattern.FromRegex("a+")).Relation);                 // ProperSuperset
Console.WriteLine(Pattern.Compare(Pattern.FromRegex("[ab]+"), Pattern.FromRegex("[bc]+")).Relation);           // Overlapping
Console.WriteLine(Pattern.Compare(Pattern.FromRegex("abc"), Pattern.FromRegex("xyz")).Relation);              // Disjoint
```

The empty string is a real string too. `a*` matches it and `a+` does not, so the shortest counterexample is `""`:

```csharp
PatternComparison result = Pattern.Compare(Pattern.FromRegex("a*"), Pattern.FromRegex("a+"));

Console.WriteLine(result.OnlyLeftExample == "");  // True
Console.WriteLine(result);
// Not equivalent (left is a proper superset of right). Shortest counterexample: "" (left accepts it,
// right rejects it). Both accept "a".
```

### The ToString message

`ToString()` is written for logs, test failure messages and the command line:

```csharp
Console.WriteLine(Pattern.Compare(Pattern.FromRegex("a|ab"), Pattern.FromRegex("ab?")));
// Equivalent: both patterns match exactly the same strings.

Console.WriteLine(Pattern.Compare(Pattern.FromRegex("a"), Pattern.FromRegex("a|bb")));
// Not equivalent (left is a proper subset of right). Shortest counterexample: "bb" (right accepts it,
// left rejects it). Both accept "a".

Console.WriteLine(Pattern.Compare(Pattern.FromRegex("xx|a"), Pattern.FromRegex("y|a")));
// Not equivalent (the patterns overlap). Shortest counterexample: "y" (right accepts it, left rejects it);
// also "xx" (left accepts it, right rejects it). Both accept "a".

Console.WriteLine(Pattern.Compare(Pattern.FromRegex("abc"), Pattern.FromRegex("xyz")));
// Not equivalent (no string matches both). Shortest counterexample: "abc" (left accepts it, right rejects it);
// also "xyz" (right accepts it, left rejects it).
```

- When both one-sided examples exist, the shorter one is listed first. On a tie, the left example comes first.
- `Both accept ...` is added whenever a common example exists.
- Example strings are quoted and escaped: `"` and `\` get a backslash, `\n`, `\r` and `\t` are written as escapes, and other control, whitespace and surrogate characters are written as `\uXXXX`. Printable non-ASCII characters such as `٠` are shown as they are.

### How the examples are chosen

Every example is a **shortest** string with its property. Among strings of that length, the search prefers readable characters: lowercase letters first, then uppercase letters, then digits, then other printable ASCII, then anything else (surrogate code units last).

```csharp
Console.WriteLine(Pattern.FromRegex("[A-Za-z]+").ShortestMatch());     // a
Console.WriteLine(Pattern.FromRegex("[A-Z]+").ShortestMatch());        // A
Console.WriteLine(Pattern.FromRegex(@"\p{Lu}\d").ShortestMatch());     // A0
Console.WriteLine(Pattern.FromRegex(@"[a-z]{2,}\d").ShortestMatch());  // aa0
```

Before an example is returned, it is run through the real .NET engine (anchored with `\A` and `\z`) to confirm that each pattern accepts or rejects it as claimed. If the engine ever disagreed with the automaton, you would get an `InvalidOperationException` whose message starts with `Internal inconsistency`, never a wrong answer.

## Quick questions

When you need a yes/no answer, use the shortcut methods instead of reading a `PatternComparison`:

```csharp
Console.WriteLine(Pattern.FromRegex("a|ab").IsEquivalentTo(Pattern.FromRegex("ab?")));    // True
Console.WriteLine(Pattern.FromRegex("[0-9]{3}").IsSubsetOf(Pattern.FromRegex(@"\d{3}")));  // True
Console.WriteLine(Pattern.FromRegex(@"\d{3}").IsSubsetOf(Pattern.FromRegex("[0-9]{3}")));  // False
Console.WriteLine(Pattern.FromRegex("[ab]+").Overlaps(Pattern.FromRegex("[bc]+")));        // True
Console.WriteLine(Pattern.FromRegex("abc").Overlaps(Pattern.FromRegex("xyz")));            // False
Console.WriteLine(Pattern.FromRegex("^(?!a)a").MatchesNothing());                         // True
Console.WriteLine(Pattern.FromRegex(@"[a-z]{2,}\d").ShortestMatch());                     // aa0
```

| Method | Returns `true` when | Built on |
|--------|---------------------|----------|
| `left.IsEquivalentTo(right)` | Both match exactly the same strings. | `Compare(left, right).Relation == Equivalent` |
| `left.IsSubsetOf(right)` | Every string `left` matches, `right` matches too. Equal patterns count. | `Compare(left, right).OnlyLeftExample == null` |
| `left.Overlaps(right)` | At least one string matches both. | `Compare(left, right).CommonExample != null` |
| `pattern.MatchesNothing()` | No string at all matches the pattern. | `ShortestMatch() == null` |

- `IsSubsetOf` is not strict: `Pattern.FromRegex("abc").IsSubsetOf(Pattern.FromRegex("abc"))` is `True`. Use `Compare(...).Relation == PatternRelation.ProperSubset` when you need "strictly smaller".
- `Overlaps` counts the empty string. `Pattern.FromRegex("a*").Overlaps(Pattern.FromRegex("b*"))` is `True`, because both accept `""`.
- `MatchesNothing()` finds patterns that can never succeed, such as `^(?!a)a` (the lookahead forbids the only character the body allows), `[^\s\S]`, or a typo that made a class empty.
- The shortcuts throw the same exceptions as `Compare`. A `null` argument throws `ArgumentNullException`. Because they call `Compare(this, other)`, its `ParamName` is `right`, not `other`.

### ShortestMatch

`ShortestMatch()` returns the shortest string the pattern matches in full, preferring readable characters, or `null` when the pattern matches nothing:

```csharp
Console.WriteLine(Pattern.FromRegex("x|yy|zzz").ShortestMatch());          // x
Console.WriteLine(Pattern.FromRegex(@"^\d+$").ShortestMatch());           // 0
Console.WriteLine(Pattern.FromRegex(@"\s").ShortestMatch() == " ");       // True
Console.WriteLine(Pattern.FromRegex("a*").ShortestMatch() == "");         // True
Console.WriteLine(Pattern.FromRegex(@"[^\s\S]+").ShortestMatch() == null); // True
```

:::warning

`ShortestMatch()` returns `""` (not `null`) for a pattern that matches the empty string, such as `a*` or `x?`. Test for `null` to find out whether the pattern matches nothing. `string.IsNullOrEmpty` mixes the two cases up.

:::

:::tip

Each shortcut runs a full comparison. When you need more than one answer about the same pair, call `Pattern.Compare` once and read `Relation`, `OnlyLeftExample`, `OnlyRightExample` and `CommonExample` from the result.

:::

## Options and Unicode

The language depends on the pattern's options, so the algebra always takes them into account. These results come from real runs:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

// Both case-insensitive: the same strings
Console.WriteLine(Pattern.FromRegex("abc", RegexOptions.IgnoreCase)
    .IsEquivalentTo(Pattern.FromRegex("ABC", RegexOptions.IgnoreCase)));                // True

// Case-sensitive against case-insensitive
Console.WriteLine(Pattern.Compare(Pattern.FromRegex("abc"), Pattern.FromRegex("ABC", RegexOptions.IgnoreCase)));
// Not equivalent (left is a proper subset of right). Shortest counterexample: "abC" (right accepts it,
// left rejects it). Both accept "abc".

// ECMAScript makes \d mean [0-9]
Console.WriteLine(Pattern.FromRegex(@"\d+", RegexOptions.ECMAScript)
    .IsEquivalentTo(Pattern.FromRegex("[0-9]+")));                                       // True

// So does WithAsciiDigits()
Console.WriteLine(Pattern.FromRegex(@"\d+").WithAsciiDigits()
    .IsEquivalentTo(Pattern.FromRegex("[0-9]+")));                                       // True

// Singleline lets . match \n
Console.WriteLine(Pattern.Compare(Pattern.FromRegex("."), Pattern.FromRegex(".", RegexOptions.Singleline)));
// Not equivalent (left is a proper subset of right). Shortest counterexample: "\n" (right accepts it,
// left rejects it). Both accept "a".

// Inline options and IgnorePatternWhitespace
Console.WriteLine(Pattern.FromRegex("a(?i)b").IsEquivalentTo(Pattern.FromRegex("a[bB]")));          // True
Console.WriteLine(Pattern.FromRegex("a b # comment", RegexOptions.IgnorePatternWhitespace)
    .IsEquivalentTo(Pattern.FromRegex("ab")));                                                      // True

// Execution-only options do not change the language
Console.WriteLine(Pattern.FromRegex(@"\d+").UseNonBacktracking().IsEquivalentTo(Pattern.FromRegex(@"\d+")));       // True
Console.WriteLine(Pattern.FromRegex(@"\d+", RegexOptions.Compiled).IsEquivalentTo(Pattern.FromRegex(@"\d+")));     // True
```

- Supported options: `IgnoreCase`, `Singleline`, `ExplicitCapture`, `IgnorePatternWhitespace`, `CultureInvariant`, `ECMAScript`, `Compiled` and `NonBacktracking`, plus inline option groups such as `(?i)`, `(?i:...)` and `(?-i:...)`.
- `Multiline` (the option or an inline `(?m)`) is supported only as long as no `^` or `$` falls under it. `RightToLeft` is not supported. See [What is not supported](#what-is-not-supported).
- `Compiled` and `NonBacktracking` only change how the engine runs, never which strings match, so they are ignored.

### Unicode surprises the algebra finds

Many "obvious" equivalences are false in .NET because of Unicode. These counterexamples were found by `Pattern.Compare`:

| Left | Right | Relation | Shortest counterexample (left only) |
|------|-------|----------|-------------------------------------|
| `\d{3}` | `[0-9]{3}` | `ProperSuperset` | `00٠` (U+0660 ARABIC-INDIC DIGIT ZERO) |
| `\w` | `[a-zA-Z0-9_]` | `ProperSuperset` | `ª` (U+00AA FEMININE ORDINAL INDICATOR) |
| `\s` | `[ \t\r\n]` | `ProperSuperset` | `\u000B` (vertical tab) |
| `k` with `IgnoreCase` | `[kK]` | `ProperSuperset` | `K` (U+212A KELVIN SIGN) |

The Kelvin sign case also holds with `RegexOptions.CultureInvariant`. To learn how to make these classes ASCII-only, see [Unicode](./unicode.md).

### IgnoreCase follows the current culture

Case-insensitive sets are computed the way `Regex` itself computes them: with the **current culture at the time of the call**, unless the pattern has `RegexOptions.CultureInvariant`. So the same comparison can give a different answer on a machine with a different culture:

```csharp
using System.Globalization;

CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

Console.WriteLine(Pattern.FromRegex("i", RegexOptions.IgnoreCase).IsEquivalentTo(Pattern.FromRegex("[iI]")));  // False
Console.WriteLine(Pattern.Compare(Pattern.FromRegex("i", RegexOptions.IgnoreCase), Pattern.FromRegex("[iI]")));
// Not equivalent (the patterns overlap). Shortest counterexample: "İ" (left accepts it, right rejects it);
// also "I" (right accepts it, left rejects it). Both accept "i".

Console.WriteLine(Pattern.FromRegex("i", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    .IsEquivalentTo(Pattern.FromRegex("[iI]")));                                                                // True
```

- Under Turkish rules, `i` pairs with `İ` (U+0130) instead of `I`.
- Add `RegexOptions.CultureInvariant` when a comparison must give the same result on every machine, for example in a CI test.

## Comparing fluent patterns with regex text

The algebra works on any `Pattern`, however it was built. A fluent chain and a regex string can be compared directly:

```csharp
Pattern fluent = Pattern.With.Digit.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);

Console.WriteLine(fluent);                                                // \d{3}-\d{4}
Console.WriteLine(fluent.IsEquivalentTo(Pattern.FromRegex(@"\d\d\d-\d{4}")));  // True
```

Groups, names and lazy quantifiers do not change which strings match, so they never make two patterns different:

```csharp
Pattern isoMonth = Pattern.With.StartOfLine
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
    .Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2))
    .EndOfLine;

Console.WriteLine(isoMonth);                                                      // ^(?<year>\d{4})-(?<month>\d{2})$
Console.WriteLine(isoMonth.IsEquivalentTo(Pattern.FromRegex(@"^\d{4}-\d{2}$")));  // True
Console.WriteLine(Pattern.FromRegex("a+?").IsEquivalentTo(Pattern.FromRegex("a+")));  // True
```

### Comparing built-in patterns

The ready-made patterns from [Common Patterns](./common-patterns.md) are ordinary `Pattern` objects too. The algebra shows exactly what a stricter variant gives up:

```csharp
PatternComparison email = Pattern.Compare(
    CommonPatterns.Email(PatternStrictness.Strict),
    CommonPatterns.Email(PatternStrictness.Loose));

Console.WriteLine(email.Relation);          // ProperSubset
Console.WriteLine(email.OnlyRightExample);  // a@a.a
Console.WriteLine(email.CommonExample);     // a@a.aa

Console.WriteLine(Pattern.Compare(CommonPatterns.Email(), CommonPatterns.Email(PatternStrictness.Loose)));
// Not equivalent (the patterns overlap). Shortest counterexample: "a@a.a" (right accepts it, left rejects it);
// also "a@..aa" (left accepts it, right rejects it). Both accept "a@a.aa".
```

- Every strict address is also a loose one. The loose pattern also accepts a one-letter top-level domain.
- The default `Email()` and the loose one overlap: each accepts strings the other rejects.

### Proving a refactoring in a unit test

The most common use of the algebra is a test that pins a rewritten pattern to the regex it replaces. Pass `ToString()` as the failure message, so a failing test shows the shortest string that differs:

```csharp
using Masterly.FluentRegex;
using Xunit;

public class OrderNumberPatternTests
{
    [Fact]
    public void OrderNumber_RewrittenPattern_MatchesSameStringsAsLegacyRegex()
    {
        Pattern legacy = Pattern.FromRegex(@"^ORD-[0-9][0-9][0-9][0-9]-(?:A|B|C)$");
        Pattern rewritten = Pattern.With.StartOfLine
            .Literal("ORD-")
            .Digit.Repeat.Times(4)
            .Literal("-")
            .AnyOf("ABC")
            .EndOfLine
            .WithAsciiDigits();

        PatternComparison comparison = Pattern.Compare(legacy, rewritten);

        Assert.True(comparison.AreEquivalent, comparison.ToString());
    }
}
```

This test passes. Without `.WithAsciiDigits()` it fails, and the message tells you exactly why:

```text
Not equivalent (left is a proper subset of right). Shortest counterexample: "ORD-000٠-A" (right accepts it, left rejects it). Both accept "ORD-0000-A".
```

A proof covers every possible input, so it replaces long lists of hand-picked test strings. To feed generated matching and non-matching strings into xUnit theories, see [xUnit Integration](./xunit.md).

## Leading lookaheads

Lookaheads are not regular in general, but a lookahead at the very start of a pattern constrains the whole input, so the algebra supports it. It may come after `^`, `\A`, inline options or a comment. This covers the usual password-policy style:

```csharp
Console.WriteLine(Pattern.Compare(
    Pattern.FromRegex(@"^(?=.*\d)(?=.*[a-z]).{8,}$"),
    Pattern.FromRegex("^.{8,}$")));
// Not equivalent (left is a proper subset of right). Shortest counterexample: "aaaaaaaa" (right accepts it,
// left rejects it). Both accept "aaaaaaa0".

Console.WriteLine(Pattern.Compare(Pattern.FromRegex(@"^(?!abc)[a-c]{3}\z"), Pattern.FromRegex("[a-c]{3}")));
// Not equivalent (left is a proper subset of right). Shortest counterexample: "abc" (right accepts it,
// left rejects it). Both accept "aaa".

Console.WriteLine(Pattern.FromRegex(@"(?=\d)\w").IsEquivalentTo(Pattern.FromRegex(@"\d")));  // True
```

- A positive lookahead `(?=...)` keeps only the inputs that start with something its body matches.
- A negative lookahead `(?!...)` removes the inputs that start with something its body matches.
- You can stack several leading lookaheads. Each one is applied in turn.
- The lookahead must be a top-level item of the pattern. A lookahead anywhere else throws `NotSupportedException`, both after a consumed character (`a(?=b)b`) and inside a group, even at its start (`(?:(?=a)\w)`).

## Combining patterns

`Intersect`, `Except` and `Union` build a new `Pattern` from the combined language of two patterns:

| Method | The new pattern matches |
|--------|-------------------------|
| `left.Intersect(right)` | Strings both patterns match. |
| `left.Except(right)` | Strings `left` matches and `right` rejects. |
| `left.Union(right)` | Strings either pattern matches. |

```csharp
Console.WriteLine(Pattern.FromRegex("[a-z]+").Intersect(Pattern.FromRegex(@"\w{3}")));   // ^[a-z]{3}\z
Console.WriteLine(Pattern.FromRegex(@"^\d+$").Intersect(Pattern.FromRegex(@"^\d{3}$"))); // ^\d{3}$
Console.WriteLine(Pattern.FromRegex("a{3,}").Intersect(Pattern.FromRegex("a+")));        // ^a{3,}\z

Console.WriteLine(Pattern.FromRegex(@"\w+").Except(Pattern.FromRegex("[a-z]+")));        // ^[a-z]*[\w-[a-z]]\w*\z
Console.WriteLine(Pattern.FromRegex(@"\d{3}").Except(Pattern.FromRegex("[0-9]{3}")));
// ^(?:[\d-[0-9]]\d{2}|[0-9](?:[0-9][\d-[0-9]]|[\d-[0-9]]\d))\z

Console.WriteLine(Pattern.FromRegex("[a-z]+").Union(Pattern.FromRegex("[0-9]+")));       // [a-z]+|[0-9]+
Console.WriteLine(Pattern.FromRegex("abc").Union(Pattern.FromRegex("XYZ", RegexOptions.IgnoreCase)));
// ^(?:abc|[Xx][Yy][Zz])\z
```

- The result is a normal `Pattern`. You can match with it, chain more calls, compare it again, or export it.
- Before the result is returned, it is proven equivalent to the combined automaton. If that proof ever failed, you would get an `InvalidOperationException` that starts with `Internal inconsistency` instead of a wrong pattern.
- The result keeps the **left** pattern's `Options` and `Timeout`. When the right pattern's options differ, their effect is spelled out in the text, as with `[Xx][Yy][Zz]` above.
- Results built from complex automata, for example ones that involve lookaheads, are correct but can be long.

### How a result is anchored

`Intersect` and `Except` always return a pattern anchored to the whole input, so `IsMatch` agrees with the language:

```csharp
Pattern threeLetters = Pattern.FromRegex("[a-z]+").Intersect(Pattern.FromRegex(@"\w{3}"));

Console.WriteLine(threeLetters.IsMatch("abc"));    // True
Console.WriteLine(threeLetters.IsMatch("abcd"));   // False
Console.WriteLine(threeLetters.IsMatch("x abc"));  // False
```

- The anchors are `^` and `\z`. When the result should also allow a final `\n` (because the inputs ended with `$`), the end anchor is `$` instead.
- When the left pattern has `RegexOptions.Multiline`, the anchors are `\A` and `\z`, because `^` would match at every line: `Pattern.FromRegex("a+", RegexOptions.Multiline).Intersect(Pattern.FromRegex("a{2}"))` is `\Aa{2}\z`.
- Case-sensitivity from the right pattern survives a case-insensitive left: `Pattern.FromRegex("[a-z]+", RegexOptions.IgnoreCase).Intersect(Pattern.FromRegex("[A-Z]+"))` is `^(?-i:[A-Z])+\z`. It matches `ABC` and not `abc`.

`Union` is different. When both patterns have the same options and can be joined directly, it returns a **plain alternation of the two original patterns**, without anchors:

```csharp
Pattern union = Pattern.FromRegex("[a-z]+").Union(Pattern.FromRegex("[0-9]+"));

Console.WriteLine(union);                // [a-z]+|[0-9]+
Console.WriteLine(union.IsMatch("a1!")); // True
```

:::warning

The language of `[a-z]+|[0-9]+` is still exactly "all letters or all digits", and `Compare`, `IsEquivalentTo` and the other algebra methods treat it that way. But `IsMatch` searches for a match anywhere in the input, so it accepts `a1!`. Wrap a union result in your own anchors, such as `\A(?:...)\z`, before you use it for validation.

:::

Otherwise, for example when the options differ or a pattern starts with a lookahead, `Union` is synthesized like the other two and comes back anchored: `Pattern.FromRegex(@"(?=a)\w").Union(Pattern.FromRegex("b"))` is `^[ab]\z`.

### Empty results

When the combined language is empty, you get a pattern that matches nothing. Check it with `MatchesNothing()`:

```csharp
Pattern nothing = Pattern.FromRegex("abc").Intersect(Pattern.FromRegex("xyz"));

Console.WriteLine(nothing);                   // ^[^\s\S]\z
Console.WriteLine(nothing.MatchesNothing());  // True

Pattern sameStrings = Pattern.FromRegex("(?:ab)+").Except(Pattern.FromRegex("a(?:ba)*b"));
Console.WriteLine(sameStrings.MatchesNothing());  // True
```

An empty `Except` result is another way to say "every string `left` matches, `right` matches too".

### Building a pattern by subtraction

`Except` is useful when it is easier to say what you do *not* want. This pattern accepts any three-letter uppercase code except two reserved ones:

```csharp
Pattern codes = Pattern.FromRegex("[A-Z]{3}").Except(Pattern.FromRegex("(?:XXX|TST)"));

Console.WriteLine(codes);
// ^(?:X{2}[A-WYZ]|(?:[A-SU-WYZ][A-Z]|X[A-WYZ])[A-Z]|T(?:S[A-SU-Z]|[A-RT-Z][A-Z]))\z
Console.WriteLine(codes.IsMatch("USD"));  // True
Console.WriteLine(codes.IsMatch("TST"));  // False
Console.WriteLine(Pattern.Compare(codes, Pattern.FromRegex("[A-Z]{3}")));
// Not equivalent (left is a proper subset of right). Shortest counterexample: "TST" (right accepts it,
// left rejects it). Both accept "AAA".
```

The generated text is exact but not always short. Run it through the [Optimizer](./optimizer.md) or keep the readable `Except` call in your code and let the library build the regex.

## What is not supported

The algebra handles the *regular* part of .NET regex: literals, character classes (including Unicode categories and class subtraction), groups of every capturing kind, alternation, greedy and lazy quantifiers, inline options, comments, and anchors at the very start or end. Anything that is not a regular language, or that changes the language in a way an automaton cannot model, throws `NotSupportedException`. The message names the construct and its position:

```csharp
try
{
    Pattern.FromRegex(@"(\w)\1").IsEquivalentTo(Pattern.FromRegex("aa"));
}
catch (NotSupportedException ex)
{
    Console.WriteLine(ex.Message);
    // Language algebra does not support backreferences: '\1' at position 4. Only regular constructs
    // (literals, classes, groups, alternation, quantifiers, and anchors at the very start or end) can be compared.
}
```

| Construct | Example | Message names |
|-----------|---------|---------------|
| Backreference | `(\w)\1` | `backreferences` |
| Lookbehind | `(?<=a)b` | `lookbehinds` |
| Lookahead that is not at the very start | `a(?=b)b` | `lookaheads except at the very start of the pattern` |
| Atomic group, including the fluent `.Possessive` | `(?>a+)b` | `atomic groups` |
| Conditional | `(a)?(?(1)b\|c)` | `conditionals` |
| Balancing group | `(?<o>a)(?<c-o>b)` | `balancing groups` |
| Word boundary | `\bcat\b` | `word boundaries` |
| Non-word boundary | `a\B` | `non-word boundaries` |
| Contiguous-match anchor | `\Ga` | `the contiguous-match anchor \G` |
| `^` or `\A` after something was consumed | `a^b` | `start anchors anywhere but the very start of the pattern` |
| `$`, `\Z` or `\z` before more input | `a$b` | `end anchors anywhere but the very end of the pattern` |
| `^` with `RegexOptions.Multiline` or inline `(?m)` | `^a`, `(?m:^a)` | `'^' with RegexOptions.Multiline (use \A instead)` |
| `$` with `RegexOptions.Multiline` | `a$` | `'$' with RegexOptions.Multiline (use \z instead)` |
| `RegexOptions.RightToLeft` | any | `RegexOptions.RightToLeft` (this message has no position) |

- "Very start" and "very end" are about what can be consumed, not about the text position. Anchors inside an alternation are fine when nothing can be matched before a start anchor or after an end anchor: `(?:^a|b)c` and `(?:a$|b)` both work.
- With `Multiline`, a pattern without `^` and `$` works. `\A` and `\z` also work, because they never depend on lines.
- The fluent `.Possessive` writes an atomic group, for example `(?>\d+)`, so a possessive pattern cannot be compared.
- A long construct is cut to its first 40 characters in the message, followed by `...`.

:::note

The algebra throws instead of guessing. If you need to compare patterns that use a backreference or a word boundary, compare the regular parts separately, or test them with [Property Testing](./property-testing.md).

:::

## Limits

Analysis is bounded, so it can never hang or run out of memory. When a pattern is too large, the call throws `InvalidOperationException` with a clear message:

| Limit | Value | Example that exceeds it | Message |
|-------|-------|-------------------------|---------|
| Largest repeat bound | 1,000 | `a{1001}`, `a{2,5000}`, `a{5000,}` | `The repeat '{2,5000}' at position 0 exceeds the language algebra limit of 1000 repetitions.` |
| NFA states for one pattern | 200,000 | `((a{100}){100}){100}` | `The pattern needs more than 200000 automaton states; it is too large for language algebra.` |
| DFA states, including product automata | 50,000 | `[ab]*a[ab]{18}` | `The automaton grew beyond 50000 states; the pattern is too complex for language algebra.` |
| Size of the regex built by `Intersect`, `Except` or `Union` | 200,000 terms | (rare) | `The resulting language is too complex to express as a readable regular expression.` |

```csharp
try
{
    Pattern.FromRegex("[ab]*a[ab]{18}").ShortestMatch();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message);
    // The automaton grew beyond 50000 states; the pattern is too complex for language algebra.
}

Console.WriteLine(Pattern.FromRegex("[ab]*a[ab]{10}").ShortestMatch());  // aaaaaaaaaaa
```

- A bound of exactly 1,000 is allowed: `Pattern.FromRegex("a{1000}").ShortestMatch()` returns a string of 1,000 `a` characters.
- The DFA limit is about how many distinct states the language needs, not about the length of the regex. `[ab]*a[ab]{n}` ("the n+1-th character from the end is `a`") needs a number of states that doubles with each step of `n`. It works up to `n = 14` and fails from `n = 15`.
- The last limit applies only when a combination is turned back into regex text. It guards against results that would be too large to read or run.
- Each witness check with the .NET engine has a 5-second timeout. If it expires, you get an `InvalidOperationException`.
- The limits are fixed. They cannot be configured.

## Exceptions

| Exception | When |
|-----------|------|
| `ArgumentNullException` | An argument of `Compare`, `IsEquivalentTo`, `IsSubsetOf`, `Overlaps`, `Intersect`, `Except` or `Union` is `null`. |
| `NotSupportedException` | A pattern uses a construct or option the algebra cannot model. See [What is not supported](#what-is-not-supported). |
| `InvalidOperationException` | A pattern or a combined result exceeds a [limit](#limits), a witness check timed out, or (never expected) the automaton and the .NET engine disagreed. |

`Pattern.FromRegex` already validates the regex syntax and throws `ArgumentException` for invalid text, so the algebra itself never sees broken syntax.

## From the command line

The `fluentregex` tool exposes `Compare` as the `compare` command. It prints the same message as `PatternComparison.ToString()`:

```bash
fluentregex compare "\d{3}" "[0-9]{3}"
# Not equivalent (left is a proper superset of right). Shortest counterexample: "00٠" (left accepts it, right rejects it). Both accept "000".

fluentregex compare "a|ab" "ab?"
# Equivalent: both patterns match exactly the same strings.

fluentregex compare "abc" "ABC" --options IgnoreCase
# Equivalent: both patterns match exactly the same strings.

fluentregex compare "\d{3}" "[0-9]{3}" --ascii-digits
# Equivalent: both patterns match exactly the same strings.
```

| Exit code | Meaning |
|-----------|---------|
| `0` | The patterns are equivalent. |
| `1` | The patterns are not equivalent. |
| `2` | Usage error, or a pattern the algebra cannot analyze (the message is printed after `error:`). |

- `--options` takes a comma-separated list of `RegexOptions` names, such as `IgnoreCase,Singleline` (case does not matter).
- `--options` and `--ascii-digits` apply to **both** patterns.
- Because `compare` exits with `1` when the patterns differ, you can use it as a CI check that a regex in a config file still matches the same strings as a reference.

See [Command-Line Tool](./command-line-tool.md) for installation and the other commands.

## How it works

You do not need this to use the algebra, but it explains the guarantees and the limits:

1. The pattern is parsed into the library's syntax tree. Each literal or class becomes an exact set of the 65,536 UTF-16 code units, computed by the .NET engine with the pattern's options.
2. All sets in play are split into *minterms*: the coarsest groups of characters that no part of either pattern can tell apart. This keeps the alphabet small but exact.
3. A Thompson NFA is built and its anchors are checked by position. Leading lookaheads become extra automata. The NFA is turned into a DFA by subset construction and minimized. Each lookahead automaton is intersected with it (or subtracted, for a negative lookahead).
4. A breadth-first search over the product of the two DFAs finds the shortest string for each region (left only, right only, both), trying the most readable character of each minterm first.
5. Every example is confirmed with the real .NET engine.
6. For `Intersect`, `Except` and synthesized `Union`, the product automaton is converted back into a regex by state elimination, simplified (class merging, prefix and suffix factoring, repeat folding) and proven equal to the automaton again.

The same equivalence proof backs the [Optimizer](./optimizer.md): when your pattern is regular, every rewrite it applies is proven to match the same strings as the original.

## Best practices

- Prove refactorings instead of sampling them. One `IsEquivalentTo` assertion covers every input; a list of test strings covers only the ones you thought of.
- Put `comparison.ToString()` in assertion messages. The shortest counterexample is usually all you need to fix the pattern.
- Read the relation, not just `AreEquivalent`. `ProperSubset` after a "tightening" change is the expected result; `Overlapping` usually means the change went wrong in two directions at once.
- Watch for the classic surprises: `\d` against `[0-9]`, `$` against `\z`, `\w` against `[a-zA-Z0-9_]`, and `IgnoreCase` with the Kelvin sign.
- Add `RegexOptions.CultureInvariant` to case-insensitive patterns in tests, so the result does not depend on the culture of the machine.
- Anchor `Union` results before you validate with them.
- Run the algebra in tests, build steps or tools, not on every request. A comparison builds and searches automata; it is fast for typical patterns but much more work than a single match.

## See also

- [Language Facts](./language-facts.md): proven length bounds, required prefixes and suffixes.
- [Optimizer](./optimizer.md): simplifications proven equivalent with this algebra.
- [ReDoS Safety](./redos-safety.md): find catastrophic backtracking risks.
- [SQL LIKE](./sql-like.md): LIKE translations that are proven exact or a superset.
- [Unicode](./unicode.md): ASCII digits, Unicode categories and case-insensitivity.
- [API Reference](./api-reference.md).
