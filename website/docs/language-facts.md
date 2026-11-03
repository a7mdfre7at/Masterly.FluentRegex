---
id: language-facts
title: "Language Facts"
description: "Prove the length bounds, required prefix and suffix that every accepted string shares, and use them for indexes and prefilters."
---

Language facts are things that are true for **every** string a pattern accepts: the shortest and longest accepted length, the text every accepted string starts with and ends with, and whether the empty string (or nothing at all) is accepted. You get them with one call, `GetLanguageFacts()`, and the library proves them from the pattern's minimal automaton instead of guessing from the regex text.

Because the facts are sound, you can act on them without ever losing a match. Use this page when you need to:

- know the shortest and longest string a pattern can accept, for example to size a database column or reject input early,
- find a literal prefix that lets a B-tree index or a sorted list narrow a search,
- build a cheap prefilter that throws away most candidates before the regex runs,
- understand why a pattern ending in `$` has no required suffix, or why a case-insensitive pattern has no required prefix.

`GetLanguageFacts`, `LanguageFacts` and `MatchScope` live in the `Masterly.FluentRegex` namespace and work on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10. The [Entity Framework Core index hints](#index-hints-in-entity-framework-core) need the `Masterly.FluentRegex.EntityFrameworkCore` package, which targets .NET 8 and .NET 10.

| Member | Kind | What it gives you |
|--------|------|-------------------|
| `pattern.GetLanguageFacts(scope = MatchScope.FullString)` | Method on `Pattern` | A `LanguageFacts` object for the chosen scope. |
| `LanguageFacts.Scope` | `MatchScope` | The set of strings the facts describe. |
| `LanguageFacts.MinLength` | `int` | The length of the shortest accepted string. |
| `LanguageFacts.MaxLength` | `int?` | The length of the longest accepted string, or `null` when there is no upper bound. |
| `LanguageFacts.RequiredPrefix` | `string` | The text every accepted string starts with. Empty when nothing is required. |
| `LanguageFacts.RequiredSuffix` | `string` | The text every accepted string ends with. Empty when nothing is required. |
| `LanguageFacts.AcceptsEmpty` | `bool` | `true` when the empty string is accepted. |
| `LanguageFacts.IsEmptyLanguage` | `bool` | `true` when no string at all is accepted. |
| `LanguageFacts.IsExact` | `bool` | `true` when the facts come from the automaton; `false` when they are a safe approximation. |
| `LanguageFacts.ToString()` | Method | A one-line summary of all the facts. |

`LanguageFacts` is a sealed, immutable class. You cannot create one yourself; only `GetLanguageFacts` returns it.

## Reading the facts of a pattern

Call `GetLanguageFacts()` on any pattern. Without an argument it describes the strings the pattern matches **entirely** (the `FullString` scope, explained in [Choosing a scope](#choosing-a-scope)).

```csharp
using System;
using Masterly.FluentRegex;

LanguageFacts facts = Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z").GetLanguageFacts();

int minLength = facts.MinLength;             // 13
int? maxLength = facts.MaxLength;            // 13
string prefix = facts.RequiredPrefix;        // "INV-"
string suffix = facts.RequiredSuffix;        // "" (the last digits vary)
bool acceptsEmpty = facts.AcceptsEmpty;      // false
bool matchesNothing = facts.IsEmptyLanguage; // false
bool isExact = facts.IsExact;                // true
MatchScope scope = facts.Scope;              // FullString
```

- Every invoice number is exactly 13 characters long, so `MinLength` and `MaxLength` are both 13.
- Every accepted string starts with `INV-`. No suffix is required, because the string ends with a digit that can be any digit.
- `IsExact` is `true`: the pattern is regular, so these are the real facts, not an estimate. See [Exact and approximate facts](#exact-and-approximate-facts).
- `GetLanguageFacts` never throws for a valid pattern. The only exceptions are `ArgumentOutOfRangeException` for a `MatchScope` value that is not defined, and `InvalidOperationException` when the pattern text is not valid regex syntax (for example a raw `RegEx(...)` piece with an unclosed group, which the [compile-time checks](./compile-time-checks.md) already flag).

### Facts of a fluent pattern

Facts work the same way on a pattern you build with the fluent API. They are computed from the regex the chain produces.

```csharp
Pattern sku = Pattern.With
    .StartOfInput
    .Literal("SKU-")
    .Digit.Repeat.Times(3, 6)
    .Literal("-EU")
    .EndOfInput;

LanguageFacts facts = sku.GetLanguageFacts();

Console.WriteLine(sku);   // prints: \ASKU-\d{3,6}-EU\z
Console.WriteLine(facts); // prints: FullString: length 10..13, prefix "SKU-", suffix "-EU", accepts empty: false (exact)
```

- The length runs from 10 (`SKU-` + 3 digits + `-EU`) to 13 (6 digits).
- `StartOfInput` and `EndOfInput` (`\A` and `\z`) pin both ends, so the facts are the same in both scopes.

### The summary line

`ToString()` puts all the facts on one line. It is handy in logs, in test failure messages and when you explore a pattern.

```text
FullString: length 13..13, prefix "INV-", suffix "", accepts empty: false (exact)
```

- The line starts with the scope, then `length MinLength..MaxLength`. A missing upper bound prints as `unbounded`.
- The prefix and suffix are quoted. Quotes, backslashes, `\n`, `\r`, `\t`, control characters, white space other than the plain space, and surrogates are escaped as `\uXXXX`, so the emoji `😀` prints as `\uD83D\uDE00`. The properties themselves hold the real characters.
- It ends with `(exact)` or `(approximate)`.
- A pattern that matches nothing prints `FullString: matches nothing (exact)` instead.

## Choosing a scope

The same pattern describes two different sets of strings, depending on how you use it. `GetLanguageFacts` takes a `MatchScope` to say which one you mean.

| `MatchScope` | The strings described | Use it when |
|--------------|----------------------|-------------|
| `FullString` (default) | Strings the pattern matches entirely, as if it were wrapped in `\A(?:...)\z`. An explicit `$` still accepts one final newline. This is the scope [pattern algebra](./pattern-algebra.md) uses. | You validate whole values, compare patterns, or reason about the language itself. |
| `Substring` | Strings for which `IsMatch` returns `true`: strings that contain a match anywhere. The padding is dropped on a side the pattern anchors. | You filter rows or lines with `IsMatch`, or build a prefilter, an index lookup or a SQL condition. |

```csharp
Pattern startsWithAbc = Pattern.FromRegex(@"^abc");

LanguageFacts full = startsWithAbc.GetLanguageFacts();                       // MatchScope.FullString
LanguageFacts search = startsWithAbc.GetLanguageFacts(MatchScope.Substring);

Console.WriteLine(full);   // prints: FullString: length 3..3, prefix "abc", suffix "abc", accepts empty: false (exact)
Console.WriteLine(search); // prints: Substring: length 3..unbounded, prefix "abc", suffix "", accepts empty: false (exact)
```

- In `FullString` scope, `^abc` accepts only `abc`.
- In `Substring` scope, `IsMatch` also accepts `abcdef` or `abc and more`. The prefix survives because `^` pins the start, but the end is open, so there is no suffix and no upper bound.

:::warning

Pick the scope that matches how you run the pattern. If you prefilter strings that you then test with `IsMatch`, use `MatchScope.Substring`. The `FullString` facts of an unanchored pattern such as `abc` say "exactly 3 characters", which would wrongly reject `xabcx`, a string `IsMatch` accepts.

:::

### How anchors change the facts

In `Substring` scope, only an anchor keeps an end fixed. `^` and `\A` pin the start. `\z` pins the end exactly. `$` and `\Z` pin the end too, but they also accept one final newline. With `RegexOptions.Multiline`, `^` and `$` match at every line, so they do not pin anything, and the facts become approximate (see [What makes facts approximate](#what-makes-facts-approximate)).

| Pattern | `FullString` facts | `Substring` facts |
|---------|-------------------|-------------------|
| `abc` | length 3..3, prefix `abc`, suffix `abc` | length 3..unbounded, no prefix, no suffix |
| `^abc` | length 3..3, prefix `abc`, suffix `abc` | length 3..unbounded, prefix `abc`, no suffix |
| `abc\z` | length 3..3, prefix `abc`, suffix `abc` | length 3..unbounded, no prefix, suffix `abc` |
| `abc$` | length 3..4, prefix `abc`, no suffix | length 3..unbounded, no prefix, no suffix |
| `abc\Z` | length 3..4, prefix `abc`, no suffix | length 3..unbounded, no prefix, no suffix |
| `^abc$` | length 3..4, prefix `abc`, no suffix | length 3..4, prefix `abc`, no suffix |
| `^abc\z` | length 3..3, prefix `abc`, suffix `abc` | length 3..3, prefix `abc`, suffix `abc` |
| `\Aabc\z` | length 3..3, prefix `abc`, suffix `abc` | length 3..3, prefix `abc`, suffix `abc` |

Every row is exact. A pattern anchored at both ends with `\z` has the same facts in both scopes.

### Requiring a suffix at the very end

A pattern that ends in `$` or `\Z` never has a required suffix, because the string may end with the newline those anchors accept. Use `\z` (or `EndOfInput` in the fluent API) when you mean the very end.

```csharp
LanguageFacts strictEnd = Pattern.FromRegex(@"xyz\z").GetLanguageFacts(MatchScope.Substring);
LanguageFacts lenientEnd = Pattern.FromRegex(@"xyz$").GetLanguageFacts(MatchScope.Substring);

string strictSuffix = strictEnd.RequiredSuffix;   // "xyz"
string lenientSuffix = lenientEnd.RequiredSuffix; // "" because "xyz\n" also matches
bool trailingNewline = Pattern.FromRegex(@"xyz$").IsMatch("xyz\n"); // true
```

This is not a bug in the facts. `xyz$` really accepts `"xyz\n"`, which does not end with `xyz`. The same newline is why `^abc$` has a maximum length of 4, not 3.

:::note

In free-spacing mode (`RegexOptions.IgnorePatternWhitespace`, or an inline `(?x)` still active at the end), a trailing `#` comment does not swallow the `Substring` padding. `^INV-[0-9]{4} # invoice number` has length `8..unbounded` in `Substring` scope, the same as without the comment.

:::

## Length bounds

`MinLength` is the length of the shortest accepted string. `MaxLength` is the length of the longest one, or `null` when accepted strings can be as long as you like.

```csharp
LanguageFacts withDollar = Pattern.FromRegex(@"^\d{3}-\d{4}$").GetLanguageFacts();
LanguageFacts withZ = Pattern.FromRegex(@"^\d{3}-\d{4}\z").GetLanguageFacts();
LanguageFacts open = Pattern.FromRegex(@"^[0-9]+(?:\.[0-9]+)?\z").GetLanguageFacts();

int? dollarMax = withDollar.MaxLength; // 9: "$" also accepts one final "\n"
int? zMax = withZ.MaxLength;           // 8
int? openMax = open.MaxLength;         // null: no upper bound
int openMin = open.MinLength;          // 1
```

- Test `MaxLength` with a pattern such as `facts.MaxLength is int max`. It is `null` whenever a `+`, `*` or `{n,}` can repeat.
- `MinLength` is `0` when the pattern accepts the empty string, and also when it accepts nothing at all.
- Leading lookaheads are modeled exactly. `^(?=.*\d).{8,}$` has `MinLength` 8 and is still exact. In `Substring` scope this needs a start anchor: `(?=a)abc` is exact in `FullString` scope but approximate in `Substring` scope, where the lookahead no longer sits at the start.

More exact examples, all in `FullString` scope:

| Pattern | Length | Prefix | Suffix |
|---------|--------|--------|--------|
| `^ab?ba\z` | 3..4 | `ab` | `ba` |
| `^colou?r\z` | 5..6 | `colo` | `r` |
| `^a\d*b\z` | 2..unbounded | `a` | `b` |
| `^(?:abc)+\z` | 3..unbounded | `abc` | `abc` |
| `^\d{4}\z` | 4..4 | none | none |
| `^(?=.*\d).{8,}$` | 8..unbounded | none | none |
| `CommonPatterns.Email(PatternStrictness.Strict)` | 6..255 | none | none |
| `CommonPatterns.Url()` | 10..unbounded | `http` | none |
| `CommonPatterns.ZipCode()` | 5..11 | none | none |

### Lengths count UTF-16 code units

`MinLength` and `MaxLength` count UTF-16 code units, the same unit as `string.Length` in .NET. An emoji outside the Basic Multilingual Plane counts as 2.

```csharp
Pattern twoCharacters = Pattern.FromRegex(@"^..\z");

int minLength = twoCharacters.GetLanguageFacts().MinLength; // 2
bool oneEmoji = twoCharacters.IsMatch("😀");                // true: one code point, two UTF-16 units
```

This matters when you move the bounds into a database:

- **`MaxLength` is safe with character-counting functions.** A string never has more code points than UTF-16 units, so a character count `<= MaxLength` never drops a matching row. That holds for PostgreSQL and SQLite `length`, MySQL and MariaDB `CHAR_LENGTH`, and SQL Server `LEN` (which also ignores trailing blanks). Do not use MySQL `LENGTH`: it counts bytes, which can exceed `MaxLength` for a matching row.
- **`MinLength` is not safe on databases that count code points.** `^..\z` has `MinLength` 2 but accepts a single emoji, which PostgreSQL counts as length 1. Do not turn `MinLength` into `length(column) >= MinLength` there. In .NET code, comparing it with `string.Length` is always safe.

## Required prefix and suffix

`RequiredPrefix` is the longest text that every accepted string starts with. `RequiredSuffix` is the longest text that every accepted string ends with. Both are empty when nothing is required. They are found by walking the automaton: the prefix follows the start state forward while exactly one character can come next, and the suffix walks backward from the accepting states the same way.

| Pattern | Prefix | Suffix | Why |
|---------|--------|--------|-----|
| `^(?:ab\|ac)\z` | `a` | none | Both branches start with `a`, then differ. |
| `^(?:PUT\|PATCH)\z` | `P` | none | Only the first letter is shared. |
| `^(?:GET\|POST\|PUT)\z` | none | `T` | The branches start differently but all end in `T`. |
| `\.(?:jpg\|png)\z` | `.` | `g` | `jpg` and `png` share only their last letter. In `Substring` scope the prefix is gone, the suffix stays. |
| `^https?://example\.com/` | `http` | `://example.com/` | The optional `s` stops the prefix. In `Substring` scope the suffix is gone, because the end is open. |
| `^[a-z]+\.log\z` | none | `.log` | A class of many characters stops the prefix immediately. |

A character class with more than one member always ends the walk, even a small one such as `[0-9]` or `\d`. Only a single, fixed character can be required.

### Case-insensitive patterns

Under `RegexOptions.IgnoreCase` (or `(?i)`), a letter also matches its other case, so it is no longer one fixed character. Only characters without case variants, such as digits and punctuation, can be required, and the walk stops at the first letter.

```csharp
using System.Text.RegularExpressions;

LanguageFacts caseless = Pattern.FromRegex(@"^ab-12\z", RegexOptions.IgnoreCase).GetLanguageFacts();
LanguageFacts hashFirst = Pattern.FromRegex(@"^#\d{3}-x\z", RegexOptions.IgnoreCase).GetLanguageFacts();

string caselessPrefix = caseless.RequiredPrefix; // "" because "a" also matches "A"
string caselessSuffix = caseless.RequiredSuffix; // "-12"
string hashPrefix = hashFirst.RequiredPrefix;    // "#": the walk stops at the digits
```

If you need an index-friendly prefix, keep the fixed part of the pattern case-sensitive and apply case-insensitivity only to the part that needs it, with an inline `(?i:...)` group:

```csharp
LanguageFacts scoped = Pattern.FromRegex(@"^INV-(?i:[a-z]+)\z").GetLanguageFacts();

string scopedPrefix = scoped.RequiredPrefix; // "INV-"
```

### Surrogate pairs and emoji

The prefix and suffix always contain whole code points. The prefix never ends with the first half of a surrogate pair, and the suffix never starts with the second half. This keeps them valid for databases that store UTF-8 or compare code points.

```csharp
LanguageFacts shared = Pattern.FromRegex("^(?:😀|😃)x\\z").GetLanguageFacts();
LanguageFacts whole = Pattern.FromRegex("^😀ab\\z").GetLanguageFacts();

string sharedPrefix = shared.RequiredPrefix; // "": the branches share only half of a surrogate pair
string wholePrefix = whole.RequiredPrefix;   // "😀ab"
int wholeLength = wholePrefix.Length;        // 4: the emoji is two UTF-16 units
Console.WriteLine(whole); // prints: FullString: length 4..4, prefix "\uD83D\uDE00ab", suffix "\uD83D\uDE00ab", accepts empty: false (exact)
```

- `😀` and `😃` share their high surrogate, but half a character is not a usable prefix, so `RequiredPrefix` is empty.
- `ToString()` escapes surrogates, so the summary line shows the emoji as `\uD83D\uDE00`. `RequiredPrefix` itself holds the real emoji.

## Empty strings and empty languages

Two flags answer the edge cases: `AcceptsEmpty` says whether `""` is accepted, and `IsEmptyLanguage` says whether anything is accepted at all.

```csharp
LanguageFacts optional = Pattern.FromRegex(@"^x*\z").GetLanguageFacts();
LanguageFacts impossible = Pattern.FromRegex(@"^abc[^\s\S]\z").GetLanguageFacts();

bool optionalAcceptsEmpty = optional.AcceptsEmpty; // true
int optionalMin = optional.MinLength;              // 0
bool nothing = impossible.IsEmptyLanguage;         // true
int? impossibleMax = impossible.MaxLength;         // 0
Console.WriteLine(impossible);                     // prints: FullString: matches nothing (exact)
```

- `[^\s\S]` is a class that contains no character, so `^abc[^\s\S]\z` can never match.
- When `IsEmptyLanguage` is `true`, `MinLength` and `MaxLength` are both `0`, the prefix and suffix are empty, and `AcceptsEmpty` is `false`. Check `IsEmptyLanguage` first; the other values carry no meaning then.
- An empty-language result is a bug signal in most applications. It usually means two parts of the pattern contradict each other. [Pattern algebra](./pattern-algebra.md) can show you why.
- The empty pattern `""` accepts only `""` in `FullString` scope (length 0..0) and every string in `Substring` scope (length 0..unbounded). `^$` accepts `""` and `"\n"`, so its length is 0..1.

## Exact and approximate facts

When the pattern is regular, the library builds its minimal automaton and reads the facts off it. Those facts are exact, and `IsExact` is `true`.

Some patterns cannot be turned into an automaton. For those, `GetLanguageFacts` does not throw. It returns a safe approximation read from the regex syntax tree, with `IsExact` set to `false`.

```csharp
LanguageFacts repeated = Pattern.FromRegex(@"^abc(\w)\1def\z").GetLanguageFacts();

bool isExact = repeated.IsExact;         // false: a backreference is not regular
int minLength = repeated.MinLength;      // 7 (a lower bound)
int? maxLength = repeated.MaxLength;     // null: approximations never give an upper bound
string prefix = repeated.RequiredPrefix; // "abc"
string suffix = repeated.RequiredSuffix; // "def"
Console.WriteLine(repeated); // prints: FullString: length 7..unbounded, prefix "abc", suffix "def", accepts empty: false (approximate)
```

### What makes facts approximate

| Construct | Example | Notes |
|-----------|---------|-------|
| Backreferences | `(\w)\1` | Never regular. |
| Lookbehinds, and lookaheads anywhere but the very start | `ab(?<=b)cd`, `^(?<=x)abc\z`, `^a{3}(?=b)b\z` | A lookahead at the very start, such as `^(?=.*\d).{8,}$`, stays exact. |
| Word boundaries | `\bcat\b` | |
| Conditionals and balancing groups | `(a)?(?(1)a\|b)`, `^(?<o>a)(?<-o>b)\z` | |
| Multiline anchors | `(?m)^abc$` | |
| `RegexOptions.RightToLeft` | | Even `^abc\z` is approximate. |
| Automaton size limits | `^a{1001}\z`, `^ab[ab]*a[ab]{18}\z` | A repeat bound above 1,000, or an automaton above 50,000 DFA states (200,000 NFA states). |

### How approximate facts are computed

- `RequiredPrefix` is the run of literal characters at the start of the pattern. Zero-width constructs such as lookarounds and `\b` are skipped because they consume nothing. A quantifier contributes its mandatory copies, and an alternation contributes what all its branches share. `RequiredSuffix` is the same walk from the end.
- The prefix is reported only when the scope or an anchor pins the start, and the suffix only when the scope or an anchor pins the end. Walking back from the end, the suffix stops at any `$` or `\Z` (without `Multiline`), even one inside a group or quantifier: `(a)\1(?:abc$){1}` requires no suffix.
- Under `IgnoreCase`, a letter that has case variants stops the walk, as for exact facts.
- `MinLength` is a lower bound summed over the tree. A backreference counts as 0 because the group it repeats may be empty. `MaxLength` is always `null`.
- `AcceptsEmpty` is not estimated. The library asks the .NET engine whether it accepts `""` in the requested scope (a timeout on that check counts as `true`).

| Pattern | Scope | Approximate facts |
|---------|-------|-------------------|
| `abc(\w)\1def` | `FullString` | length 7..unbounded, prefix `abc`, suffix `def` |
| `abc(\w)\1def` | `Substring` | length 7..unbounded, no prefix, no suffix |
| `^ab\bc(?<=c)d$` | `FullString` | length 4..unbounded, prefix `abcd`, no suffix |
| `\bcat\b` | `Substring` | length 3..unbounded, no prefix, no suffix |
| `(?m)^abc$` | `FullString` | length 3..unbounded, prefix `abc`, suffix `abc` |
| `(?m)^abc$` | `Substring` | length 3..unbounded, no prefix, no suffix |
| `^ab[ab]*a[ab]{18}\z` | `FullString` | length 21..unbounded, prefix `ab`, no suffix |

### Acting on approximate facts

Approximate facts are still sound. The real required prefix and suffix can only be longer, and the real minimum length only higher, so every check this page describes stays correct. You only lose precision: no upper length bound, and possibly a shorter prefix than the truth.

:::tip

If a prefilter or index matters for performance, check `IsExact` in a unit test. A refactoring that adds a backreference or a `\b` silently turns an exact `MaxLength` into `null`.

:::

## How exact facts are computed

You do not need this to use the facts, but it explains why they can be trusted.

1. The scoped pattern is compiled to a minimal DFA over exact character sets, the same automaton [pattern algebra](./pattern-algebra.md) uses for `Pattern.Compare`.
2. `MinLength` is a breadth-first search from the start state to the nearest accepting state.
3. `MaxLength` is the longest path to an accepting state, or `null` when the automaton has a cycle. Every state of a minimal DFA can reach an accepting state, so any cycle makes the language unbounded.
4. `RequiredPrefix` follows the forced path from the start state: while the state is not accepting and exactly one single-character transition leads on, that character begins every accepted string.
5. `RequiredSuffix` does the same walk backward from the set of accepting states. The reversed automaton is simulated lazily as sets of states, so it is never built in full.
6. The prefix and suffix are cut back to whole code points.

The test suite generates matching strings for every `CommonPatterns` member and a set of hand-picked patterns, and checks that each one respects the `FullString` facts. It also surrounds those strings with extra text and checks them against the `Substring` facts.

## Using facts as a prefilter

A regex check is cheap, but a length check and a `StartsWith` are cheaper. When you test many strings, let the facts throw away the strings that cannot match, and run the regex only on the rest.

```csharp
Pattern invoice = Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z");
LanguageFacts facts = invoice.GetLanguageFacts(MatchScope.Substring);

bool CouldMatch(string candidate) =>
    candidate.Length >= facts.MinLength
    && (facts.MaxLength is not int maxLength || candidate.Length <= maxLength)
    && candidate.StartsWith(facts.RequiredPrefix, StringComparison.Ordinal)
    && candidate.EndsWith(facts.RequiredSuffix, StringComparison.Ordinal);

string[] candidates = { "INV-2024-0001", "PO-2024-0001", "INV-2024-00001", "INV-2024-ABCD", "inv-2024-0001" };
foreach (string candidate in candidates)
{
    Console.WriteLine($"{candidate}: prefilter {CouldMatch(candidate)}, match {invoice.IsMatch(candidate)}");
}

// prints:
// INV-2024-0001: prefilter True, match True
// PO-2024-0001: prefilter False, match False
// INV-2024-00001: prefilter False, match False
// INV-2024-ABCD: prefilter True, match False
// inv-2024-0001: prefilter False, match False
```

- The prefilter never says `False` for a string the pattern accepts. It may say `True` for a string the pattern rejects (`INV-2024-ABCD`), so you still run `IsMatch` on what passes.
- Use `MatchScope.Substring` because the final check is `IsMatch`.
- Compare with `StringComparison.Ordinal`. A culture-aware `StartsWith` can treat different strings as equal, or equal strings as different, and break the guarantee.
- Compute the facts once and reuse them. See [Performance](#performance).

## Narrowing a sorted index

A required prefix turns a full scan into a range scan. On a list sorted with the ordinal comparer, every string that starts with the prefix sits in one block, so you can binary-search to the start of the block and stop at its end.

```csharp
using System.Collections.Generic;

Pattern invoice = Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z");
string prefix = invoice.GetLanguageFacts(MatchScope.Substring).RequiredPrefix; // "INV-"

// numbers comes from your application, sorted once with the ordinal comparer
List<string> numbers = new List<string> { "CN-2024-0003", "INV-2024-0001", "INV-2025-0420", "INV-2025-04201", "PO-0007" };

int start = numbers.BinarySearch(prefix, StringComparer.Ordinal);
if (start < 0)
{
    start = ~start;
}

List<string> matches = new List<string>();
for (int i = start; i < numbers.Count && numbers[i].StartsWith(prefix, StringComparison.Ordinal); i++)
{
    if (invoice.IsMatch(numbers[i]))
    {
        matches.Add(numbers[i]);
    }
}

Console.WriteLine(string.Join(", ", matches)); // prints: INV-2024-0001, INV-2025-0420
```

- The loop visits only the three `INV-` entries, not the whole list. `INV-2025-04201` is in the range but fails the regex.
- The same idea works for a database B-tree index: `column LIKE 'INV-%'` is a range scan on most engines. Escape `%`, `_` and your escape character in the prefix before you put it in a `LIKE`.
- When `RequiredPrefix` is empty, there is no range to narrow. Fall back to the length bound or a full scan.

## Index hints in Entity Framework Core

You do not have to write the database side by hand. The `Masterly.FluentRegex.EntityFrameworkCore` package reads the `Substring` facts of every pattern known when the query is compiled and adds them to the SQL as extra conditions. These index hints are on by default; `UseIndexHints(false)` turns them off.

```csharp
using System;
using System.Linq;
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public static class Patterns
{
    public static readonly Pattern Invoice = Pattern.FromRegex(@"^INV-[0-9]{4}-[0-9]{4}\z");
}

public sealed class Invoice
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
}

public sealed class ShopContext : DbContext
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) => options
        .UseSqlite("Data Source=:memory:")
        .UseFluentRegex(fluent => fluent.UseDialect(SqlDialect.Sqlite).UseIndexHints());
}
```

Query the invoices and print the SQL that EF Core sends:

```csharp
using ShopContext db = new ShopContext();
IQueryable<Invoice> query = db.Invoices.Where(invoice => Patterns.Invoice.IsMatch(invoice.Number));
Console.WriteLine(query.ToQueryString());
```

The generated SQL on SQLite:

```sql
SELECT "i"."Id", "i"."Number"
FROM "Invoices" AS "i"
WHERE "i"."Number" REGEXP '^INV-[0-9]{4}-[0-9]{4}\z' AND "i"."Number" LIKE 'INV-%' ESCAPE '\' AND length("i"."Number") <= 13
```

- `LIKE 'INV-%'` comes from `RequiredPrefix` and lets an index on `Number` narrow the scan. `length(...) <= 13` comes from `MaxLength`. The regex still decides; the hints never change the result.
- Only the upper length bound is emitted, never `MinLength`, for the code-point reason in [Lengths count UTF-16 code units](#lengths-count-utf-16-code-units).
- The hints are added for the dialects that translate `IsMatch` to a regex: PostgreSQL, MySQL, MariaDB, SQLite and `SqlDialect.SqlServer2025`. On SQL Server 2025 the prefix `LIKE` is added only when the column is declared binary with `UseLikeCollation(LikeCollation.Binary)`, because linguistic collations do not compare `LIKE` character by character; the `LEN` bound is always added. `SqlDialect.SqlServer` has no regex support, so there `IsMatch` becomes a `LIKE` translation instead of a regex with hints.
- A pattern held in a local variable becomes a query parameter, so no facts are known when the query is compiled, and no hints are added.
- The package caches the facts per pattern, so a query does not pay the cost on every execution.

See [Entity Framework Core](./entity-framework-core.md) for the full integration, and [SQL LIKE](./sql-like.md) for `ToLike`, which also uses language facts to build a proven `prefix%suffix` LIKE when it cannot translate a pattern exactly.

## From the command line

The [command-line tool](./command-line-tool.md) prints the same summary line with `fluentregex facts`. Use `--scope full` (the default) or `--scope substring`, `--options` for a comma-separated list of `RegexOptions`, and `--ascii-digits` to make `\d` match only `0-9`.

```bash
fluentregex facts "^INV-\d{4}-\d{4}\z"
fluentregex facts "^abc" --scope substring
fluentregex facts "abc" --options IgnoreCase
fluentregex facts "^(\w)\1\z"
```

```text
FullString: length 13..13, prefix "INV-", suffix "", accepts empty: false (exact)
Substring: length 3..unbounded, prefix "abc", suffix "", accepts empty: false (exact)
FullString: length 3..3, prefix "", suffix "", accepts empty: false (exact)
FullString: length 1..unbounded, prefix "", suffix "", accepts empty: false (approximate)
```

The tool also accepts `fullstring`, `full-string` and `search` as scope names. An unknown scope prints `error: Unknown --scope '...'. Use full or substring.` and exits with code 2, as does a missing regex argument.

## Performance

`GetLanguageFacts` builds and minimizes an automaton, so it costs far more than one `IsMatch`. Most patterns take milliseconds, but a large one can take much longer: on a development machine, the facts of `CommonPatterns.Email(PatternStrictness.Strict)` took about 1.7 seconds per call, and `CommonPatterns.Url()` about 10 milliseconds.

- The result is not cached on the `Pattern`. Every call computes it again.
- Compute the facts once, at startup or in a `static readonly` field, and reuse the `LanguageFacts` object. It is immutable and safe to share across threads.
- The work is bounded. A pattern that would need more than 50,000 DFA states falls back to approximate facts instead of running away.

## Common pitfalls

- **Using `FullString` facts to filter `IsMatch` input.** For an unanchored pattern they are far too strict. Use `MatchScope.Substring` whenever the real check is `IsMatch`.
- **Expecting a suffix from a pattern that ends in `$`.** `$` accepts a final newline, so no suffix can be required and `MaxLength` grows by one. End with `\z` (`EndOfInput`).
- **Expecting a prefix under `IgnoreCase`.** Only caseless characters can be required, and the walk stops at the first letter.
- **Turning `MinLength` into a SQL lower bound.** SQL length functions count code points (and `LEN` drops trailing blanks), so the bound can exclude matches. `MaxLength` is safe; `MinLength` is not.
- **Reading `MaxLength == null` as "approximate".** It also happens for exact facts whenever the pattern can repeat without limit. Check `IsExact` for exactness.
- **Ignoring `IsEmptyLanguage`.** When it is `true`, `MinLength` and `MaxLength` are both `0`. A check such as `length <= MaxLength` would then accept only empty strings, which the pattern also rejects.
- **Using a culture-aware comparison with the prefix.** Use `StringComparison.Ordinal` and `StringComparer.Ordinal`, as the facts compare UTF-16 code units.

## Related pages

- [Pattern Algebra](./pattern-algebra.md) proves two patterns equivalent, or shows a string that tells them apart, using the same automata.
- [Optimizer](./optimizer.md) rewrites patterns with verified, semantics-preserving changes.
- [SQL LIKE](./sql-like.md) and [SQL Dialects](./sql-dialects.md) move patterns into the database.
- [Entity Framework Core](./entity-framework-core.md) adds the index hints from this page to LINQ queries.
