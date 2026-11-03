---
id: assertions
title: "Assertions (Lookaround)"
description: "Lookahead and lookbehind: match text only when the right context follows or precedes it, without making that context part of the match."
---

An assertion checks the text around the current position without consuming it. A lookahead looks at what comes next. A lookbehind looks at what came before. Each one can be positive (the context must be there) or negative (the context must not be there). The match itself never includes the text the assertion looked at.

Use assertions when you need to:

- match a value only when a marker follows or precedes it, such as digits after a `$` or before `px`,
- exclude a match because of its neighbours, such as a number that is part of a longer number,
- check several independent conditions on the same text, such as password rules,
- match a position between characters, to insert text there or split on it.

All four members live in the `Masterly.FluentRegex` namespace, are methods of `Pattern`, and work on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10.

| Member | Regex | Succeeds when |
|--------|-------|---------------|
| `.PositiveLookahead(pattern)` | `(?=...)` | `pattern` matches the text right after the current position. |
| `.NegativeLookahead(pattern)` | `(?!...)` | `pattern` does not match the text right after the current position. |
| `.PositiveLookbehind(pattern)` | `(?<=...)` | `pattern` matches the text that ends at the current position. |
| `.NegativeLookbehind(pattern)` | `(?<!...)` | `pattern` does not match the text that ends at the current position. |

Every member takes one `Pattern`, wraps it in the assertion and returns a new `Pattern`. The argument can be any pattern you can build: a literal, a class, a group, an alternation or a whole chain.

## How lookaround works

An assertion is a test at a position. The engine runs the inner pattern, notes whether it matched, and then goes back to where it was. Nothing the inner pattern read becomes part of the match.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern amount = Pattern.With
    .PositiveLookbehind(Pattern.With.Literal("$"))
    .Digit.Repeat.OneOrMore
    .NegativeLookahead(Pattern.With.Literal("%"));

Console.WriteLine(amount.ToString());                        // (?<=\$)\d+(?!%)

Match match = amount.Match("Save 15% today: only $120");
Console.WriteLine(match.Value);                              // 120
Console.WriteLine(match.Index);                              // 22
```

- `15` is skipped because no `$` precedes it. The lookbehind is what rejects it: on its own, `\d+(?!%)` would still match the `1` of `15`, because the `1` is followed by `5`, not by `%` (see [Negative lookbehind](#negative-lookbehind) for the same effect).
- The match is `120`, not `$120`. The `$` was only checked, never consumed, so `match.Index` points at the `1`.
- `Literal("$")` is escaped for you (`\$`). You never escape text inside an assertion yourself.
- `Match` comes from `System.Text.RegularExpressions`.

The difference from ordinary matching shows up in the match length:

```csharp
Pattern consuming = Pattern.With.Literal("q").Literal("u");
Pattern lookingAhead = Pattern.With.Literal("q").PositiveLookahead(Pattern.With.Literal("u"));

Console.WriteLine(consuming.Match("queen").Value);     // qu
Console.WriteLine(lookingAhead.Match("queen").Value);  // q
Console.WriteLine(lookingAhead.Match("queen").Length); // 1
```

Both patterns need a `u` after the `q`. Only the first one makes it part of the match.

:::tip

Because an assertion consumes nothing, the rest of the pattern continues from the same position. That is what lets you stack several assertions in a row and have each one examine the same text. See [Stacking lookaheads for AND rules](#stacking-lookaheads-for-and-rules).

:::

## Lookahead

A lookahead checks the text to the right of the current position.

### Positive lookahead

`PositiveLookahead` succeeds when its pattern matches at the current position. Use it to require a suffix that should not be part of the result.

```csharp
Pattern qWithU = Pattern.With
    .Literal("q")
    .PositiveLookahead(Pattern.With.Literal("u"));

Console.WriteLine(qWithU.ToString());       // q(?=u)
Console.WriteLine(qWithU.IsMatch("queen")); // True
Console.WriteLine(qWithU.IsMatch("iraq"));  // False
```

A common use is reading a value while ignoring its unit:

```csharp
Pattern pixelValue = Pattern.With
    .Digit.Repeat.OneOrMore
    .PositiveLookahead(Pattern.With.Literal("px"));

Console.WriteLine(pixelValue.ToString()); // \d+(?=px)

foreach (Match match in pixelValue.Matches("margin: 12px 4em 8px 0"))
{
    Console.WriteLine(match.Value);       // prints 12, then 8
}
```

- `4em` and `0` are skipped because `px` does not follow them.
- The values come back without the unit, so you can parse them directly.

### Negative lookahead

`NegativeLookahead` succeeds when its pattern does not match at the current position. It also succeeds at the end of the input, where nothing follows.

```csharp
Pattern qWithoutU = Pattern.With
    .Literal("q")
    .NegativeLookahead(Pattern.With.Literal("u"));

Console.WriteLine(qWithoutU.ToString());                     // q(?!u)
Console.WriteLine(qWithoutU.IsMatch("iraq"));                // True
Console.WriteLine(qWithoutU.IsMatch("queen"));               // False
Console.WriteLine(qWithoutU.Match("Qatar and Iraq").Index);  // 13
```

- In `iraq` the `q` is the last character. Nothing follows it, so "not followed by `u`" is true.
- The `Q` of `Qatar` does not match because `Literal("q")` is case-sensitive. The match is the final `q` of `Iraq`.

Put a negative lookahead right after the start anchor to reject whole inputs. This pattern accepts any word except a few reserved device names:

```csharp
Pattern fileName = Pattern.With
    .StartOfLine
    .NegativeLookahead(Pattern.With.OneOf("con", "nul", "prn").EndOfLine)
    .Word.Repeat.OneOrMore
    .EndOfLine;

Console.WriteLine(fileName.ToString());          // ^(?!(?:con|nul|prn)$)\w+$
Console.WriteLine(fileName.IsMatch("report"));   // True
Console.WriteLine(fileName.IsMatch("con"));      // False
Console.WriteLine(fileName.IsMatch("console"));  // True
```

- The `EndOfLine` inside the lookahead makes it reject only the exact names. Without it, `console` would be rejected too, because it starts with `con`.
- `OneOf` wraps the alternatives in a non-capturing group, so the anchor applies to all of them.

## Lookbehind

A lookbehind checks the text to the left of the current position: the text that ends exactly where the rest of the pattern starts.

### Positive lookbehind

`PositiveLookbehind` succeeds when its pattern matches the text just before the current position. Use it to require a prefix that should not be part of the result.

```csharp
Pattern price = Pattern.With
    .PositiveLookbehind(Pattern.With.Literal("$"))
    .Digit.Repeat.OneOrMore;

Console.WriteLine(price.ToString());                  // (?<=\$)\d+
Console.WriteLine(price.Match("Price: $100").Value);  // 100
```

### Negative lookbehind

`NegativeLookbehind` succeeds when its pattern does not match the text just before the current position. It also succeeds at the start of the input.

```csharp
Pattern unsigned = Pattern.With
    .NegativeLookbehind(Pattern.With.Literal("-"))
    .Digit.Repeat.OneOrMore;

Console.WriteLine(unsigned.ToString());           // (?<!-)\d+
Console.WriteLine(unsigned.Match("123").Value);   // 123
Console.WriteLine(unsigned.Match("-456").Value);  // 56
```

The second result is a classic surprise. The engine cannot start at `4`, because `-` precedes it. It moves one character on and starts at `5`, which is preceded by `4`, not by `-`. So it matches `56`.

:::warning

A negative lookbehind only guards the first character of the match. When the match can start in the middle of a run, also exclude the characters of that run. Here, "not after a `-` or a digit" stops the engine from starting inside a number:

```csharp
Pattern wholeUnsigned = Pattern.With
    .NegativeLookbehind(Pattern.With.Set(Pattern.With.Literal("-").Digit))
    .Digit.Repeat.OneOrMore;

Console.WriteLine(wholeUnsigned.ToString());       // (?<![\-\d])\d+
Console.WriteLine(wholeUnsigned.IsMatch("-456"));  // False

foreach (Match match in wholeUnsigned.Matches("7 -456 89"))
{
    Console.WriteLine(match.Value);                // prints 7, then 89
}
```

:::

The same thing happens with any prefix:

```csharp
Pattern notAfterDollar = Pattern.With
    .NegativeLookbehind(Pattern.With.Literal("$"))
    .Digit.Repeat.OneOrMore;

Console.WriteLine(notAfterDollar.IsMatch("Item 100"));   // True
Console.WriteLine(notAfterDollar.IsMatch("$100"));       // True
Console.WriteLine(notAfterDollar.Match("$100").Value);   // 00
```

`IsMatch("$100")` is `true`, because `00` is a run of digits that is not preceded by `$`. Use `NegativeLookbehind(Pattern.With.Set(Pattern.With.Literal("$").Digit))` when you mean "a number that does not start with `$`".

### Variable-length lookbehind

The .NET engine runs a lookbehind from right to left, so its pattern can have any length, including alternatives of different lengths and open-ended repeats. Many other engines cannot do this.

```csharp
Pattern labelledValue = Pattern.With
    .PositiveLookbehind(Pattern.With.OneOf("Total", "Subtotal").Literal(":").Whitespace.Repeat.ZeroOrMore)
    .Digit.Repeat.OneOrMore;

Console.WriteLine(labelledValue.ToString()); // (?<=(?:Total|Subtotal):\s*)\d+

foreach (Match match in labelledValue.Matches("Subtotal: 90, Tax:5, Total:   95"))
{
    Console.WriteLine(match.Value);          // prints 90, then 95
}
```

- `Tax:5` is skipped because `Tax` is not one of the labels.
- Any number of spaces may sit between the label and the value, and none of them end up in the match.

:::note

This pattern runs everywhere in .NET, but MySQL and MariaDB reject it when you export it to SQL. See [Exporting to other engines](#exporting-to-other-engines).

:::

## Combining assertions

### Guarding both sides

Put a lookbehind before an element and a lookahead after it to control both neighbours. This pattern finds exactly three digits that are not part of a longer number:

```csharp
Pattern isolatedNumber = Pattern.With
    .NegativeLookbehind(Pattern.With.Digit)
    .Digit.Repeat.Times(3)
    .NegativeLookahead(Pattern.With.Digit);

Console.WriteLine(isolatedNumber.ToString());        // (?<!\d)\d{3}(?!\d)
Console.WriteLine(isolatedNumber.IsMatch("123"));    // True
Console.WriteLine(isolatedNumber.IsMatch("1234"));   // False
Console.WriteLine(isolatedNumber.IsMatch("a123b"));  // True
```

- A word boundary (`WordBoundary`, `\b`) would reject `a123b`, because letters are word characters. The assertions only look at digits, so letters next to the number are fine.
- Both checks also pass at the start and end of the input.

### Stacking lookaheads for AND rules

Several lookaheads in a row all start from the same position. Put them after the start anchor and each one checks the whole input for one rule. The input passes only when every rule holds.

```csharp
Pattern strongPassword = Pattern.With
    .StartOfLine
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.LowercaseLetter))
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.UppercaseLetter))
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Digit)
    .Anything.Repeat.AtLeast(8)
    .EndOfLine;

Console.WriteLine(strongPassword.ToString());           // ^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$
Console.WriteLine(strongPassword.IsMatch("Password1")); // True
Console.WriteLine(strongPassword.IsMatch("password1")); // False (no uppercase letter)
Console.WriteLine(strongPassword.IsMatch("PASSWORD1")); // False (no lowercase letter)
Console.WriteLine(strongPassword.IsMatch("Password"));  // False (no digit)
Console.WriteLine(strongPassword.IsMatch("Pass1"));     // False (shorter than 8)
```

- Each lookahead reads "somewhere ahead there is a ...". `.*` skips any characters before the one the rule needs.
- The order of the lookaheads does not change the result. Put the rule that fails most often first, so bad input is rejected sooner.
- The consuming part (`.{8,}`) sets the length rule. The lookaheads never move the position, so they do not count towards it.
- `LowercaseLetter` and `UppercaseLetter` are ASCII ranges (`a-z`, `A-Z`). For letters in other scripts, see [Unicode](./unicode.md).

The library ships a ready-made version that also requires a special character:

```csharp
Pattern strong = CommonPatterns.StrongPassword();

Console.WriteLine(strong.ToString());              // ^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#\$%\^&\*\(\)_\+\-=]).{8,}$
Console.WriteLine(strong.IsMatch("Password1!"));   // True
Console.WriteLine(strong.IsMatch("Password1"));    // False (no special character)
```

See [Common Patterns](./common-patterns.md) for the rest of the catalog.

:::tip

A single regex only tells you that some rule failed, not which one. When you need to show the user each failed rule, keep one small pattern per rule, or use [Semantic Rules](./semantic-rules.md).

:::

### Requirements and exclusions together

Mix positive and negative lookaheads to state what the whole input must and must not be, then describe its shape with ordinary elements:

```csharp
Pattern username = Pattern.With
    .StartOfLine
    .NegativeLookahead(Pattern.With.OneOf("admin", "root").EndOfLine)
    .PositiveLookahead(Pattern.With.Anything.Repeat.Times(3, 16).EndOfLine)
    .Letter
    .Word.Repeat.ZeroOrMore
    .EndOfLine;

Console.WriteLine(username.ToString());                // ^(?!(?:admin|root)$)(?=.{3,16}$)[a-zA-Z]\w*$
Console.WriteLine(username.IsMatch("alice_01"));       // True
Console.WriteLine(username.IsMatch("admin"));          // False (reserved)
Console.WriteLine(username.IsMatch("administrator"));  // True (only the exact word is reserved)
Console.WriteLine(username.IsMatch("al"));             // False (shorter than 3)
Console.WriteLine(username.IsMatch("1alice"));         // False (starts with a digit)
```

- The length lookahead (`(?=.{3,16}$)`) keeps the length rule separate from the shape rule. Without it you would have to work the limit into the `\w*` repeat.
- `Letter` is the ASCII class `[a-zA-Z]`.

### Matching a position

A pattern made only of assertions matches an empty string at a position. Use it with `Replace` to insert text there, or with `Split` to cut between characters without losing any.

This pattern finds every gap between digits that is followed by a multiple of three digits, and inserts a thousands separator:

```csharp
Pattern thousandsGap = Pattern.With
    .PositiveLookbehind(Pattern.With.Digit)
    .PositiveLookahead(Pattern.With
        .NonCapturingGroup(Pattern.With.Digit.Repeat.Times(3)).Repeat.OneOrMore
        .NegativeLookahead(Pattern.With.Digit));

Console.WriteLine(thousandsGap.ToString());
// (?<=\d)(?=(?:\d{3})+(?!\d))

Console.WriteLine(thousandsGap.Replace("Population: 1234567, budget 950", ","));
// Population: 1,234,567, budget 950
```

- Assertions can nest. The negative lookahead inside the positive one makes sure the group of three digits reaches the end of the number.
- `950` has no gap that is followed by a multiple of three digits, so it is left alone.

This pattern splits a camelCase name at every lower-to-upper case change:

```csharp
Pattern wordGap = Pattern.With
    .PositiveLookbehind(Pattern.With.Set(Pattern.With.LowercaseLetter))
    .PositiveLookahead(Pattern.With.Set(Pattern.With.UppercaseLetter));

Console.WriteLine(wordGap.ToString());                                       // (?<=[a-z])(?=[A-Z])
Console.WriteLine(string.Join(" ", wordGap.Split("parseHttpRequestBody")));  // parse Http Request Body
```

Because the separator is empty, `Split` keeps every character.

## What goes inside an assertion

### Any pattern, reused freely

The argument is an ordinary `Pattern`. Patterns are immutable, so you can build a piece once and use it in several assertions:

```csharp
Pattern currency = Pattern.With.Literal("$");

Pattern price = Pattern.With.PositiveLookbehind(currency).Digit.Repeat.OneOrMore;
Pattern noCurrency = Pattern.With.NegativeLookbehind(currency).Digit.Repeat.OneOrMore;

Console.WriteLine(price.ToString());       // (?<=\$)\d+
Console.WriteLine(noCurrency.ToString());  // (?<!\$)\d+
```

Inner patterns can use anchors, groups, alternations, quantifiers and other assertions. An anchor inside a lookahead checks the position the lookahead reached:

```csharp
Pattern lastWord = Pattern.With
    .Word.Repeat.OneOrMore
    .PositiveLookahead(Pattern.With.EndOfLine);

Console.WriteLine(lastWord.ToString());                          // \w+(?=$)
Console.WriteLine(lastWord.Match("first second third").Value);  // third
```

### Captures inside assertions

Groups inside a positive assertion capture as usual, even though their text is not part of the match. Groups inside a negative assertion never keep a value, because the assertion only succeeds when its pattern failed.

```csharp
Pattern quantity = Pattern.With
    .Digit.Repeat.OneOrMore
    .PositiveLookahead(Pattern.With.Whitespace.NamedGroup("unit", Pattern.With.OneOf("kg", "lb")));

Match match = quantity.Match("Weight: 12 kg");

Console.WriteLine(quantity.ToString());             // \d+(?=\s(?<unit>(?:kg|lb)))
Console.WriteLine(match.Value);                     // 12
Console.WriteLine(match.Groups["unit"].Value);      // kg

Pattern notBefore = Pattern.With
    .Literal("a")
    .NegativeLookahead(Pattern.With.NamedGroup("next", Pattern.With.Literal("b")));

Console.WriteLine(notBefore.Match("ac").Groups["next"].Success);  // False
```

This is handy when you want a value and its context as separate results: `match.Value` holds the number and the `unit` group holds the unit. For more on groups, see [Groups and Backreferences](./groups-and-backreferences.md).

### Empty patterns

An empty inner pattern is allowed. It always matches, so a positive assertion around it always succeeds and a negative one always fails:

```csharp
Pattern alwaysPasses = Pattern.With.Literal("a").PositiveLookahead(Pattern.With);
Pattern neverPasses = Pattern.With.Literal("a").NegativeLookahead(Pattern.With);

Console.WriteLine(alwaysPasses.ToString());     // a(?=)
Console.WriteLine(alwaysPasses.IsMatch("a"));   // True
Console.WriteLine(neverPasses.ToString());      // a(?!)
Console.WriteLine(neverPasses.IsMatch("a"));    // False
```

You rarely write this on purpose. It usually means a conditional builder produced an empty pattern, so check for `pattern.IsEmpty` before you pass it in.

### Options belong to the outer pattern

Only the text of the inner pattern is copied into the assertion. Its execution settings (`WithOptions`, `WithTimeout`) are dropped, because the outer pattern decides how the whole expression runs. To change matching behaviour for just the inner part, use an inline option such as `IgnoreCase(...)`:

```csharp
Pattern currencyCode = Pattern.With.Literal("USD").WithOptions(RegexOptions.IgnoreCase);

Pattern ignoredOptions = Pattern.With
    .Digit.Repeat.OneOrMore
    .PositiveLookahead(Pattern.With.Whitespace.Then(currencyCode));

Console.WriteLine(ignoredOptions.ToString());          // \d+(?=\sUSD)
Console.WriteLine(ignoredOptions.IsMatch("100 usd"));  // False

Pattern inlineOptions = Pattern.With
    .Digit.Repeat.OneOrMore
    .PositiveLookahead(Pattern.With.Whitespace.IgnoreCase(Pattern.With.Literal("USD")));

Console.WriteLine(inlineOptions.ToString());           // \d+(?=\s(?i:USD))
Console.WriteLine(inlineOptions.IsMatch("100 usd"));   // True
```

`Then`, the group methods and the other members that take a pattern behave the same way. See [Core Concepts](./core-concepts.md).

### Null arguments

Passing `null` throws `ArgumentNullException` with `ParamName` set to `"pattern"`. When you pass a literal `null`, the compile-time analyzer reports this as error `FRX006` before you run anything (see [Compile-Time Checks](./compile-time-checks.md)).

## Pitfalls

### Quantifiers on an assertion

An assertion matches no text, so repeating it achieves nothing. The .NET engine accepts a quantifier after one, and `Repeat.Optional` quietly turns the check off:

```csharp
Pattern optionalCheck = Pattern.With
    .Literal("a")
    .PositiveLookahead(Pattern.With.Literal("b")).Repeat.Optional;

Console.WriteLine(optionalCheck.ToString());       // a(?=b)?
Console.WriteLine(optionalCheck.IsMatch("ac"));    // True: the check was made optional
```

Never put `.Repeat` right after an assertion. Put the quantifier inside the inner pattern instead. Export to MySQL also rejects this construct (`quantifier '?' on a zero-width construct`).

### Lookaheads, `.` and line breaks

`Anything` (`.`) does not match `\n`, and `EndOfLine` (`$`) also matches before a final newline. Both affect "anywhere in the input" rules:

```csharp
Pattern hasDigit = Pattern.With
    .StartOfLine
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Digit)
    .Anything.Repeat.AtLeast(8)
    .EndOfLine;

Console.WriteLine(hasDigit.IsMatch("abcdefg1\n"));   // True: $ also matches before a final newline
Console.WriteLine(hasDigit.IsMatch("abcdefgh\n1"));  // False: . stops at the newline

Pattern wholeInput = Pattern.With
    .StartOfInput
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Digit)
    .Anything.Repeat.AtLeast(8)
    .EndOfInput;

Console.WriteLine(wholeInput.ToString());              // \A(?=.*\d).{8,}\z
Console.WriteLine(wholeInput.IsMatch("abcdefg1\n"));   // False
```

- Use `StartOfInput` and `EndOfInput` (`\A`, `\z`) when you validate a whole value and a trailing newline must not slip through.
- Add `RegexOptions.Singleline` with `WithOptions` when the input may span lines and `.` should cross them.

### Cost of `.*` inside lookaheads

Each `(?=.*X)` scans forward to the end of the line and then backtracks to find `X`. For short inputs such as passwords this is cheap. For long inputs, a negated class finds the first match without backtracking:

```csharp
Pattern hasDigit = Pattern.With
    .StartOfLine
    .PositiveLookahead(Pattern.With.NegatedSet(Pattern.With.Digit).Repeat.ZeroOrMore.Digit)
    .Anything.Repeat.AtLeast(8)
    .EndOfLine;

Console.WriteLine(hasDigit.ToString());            // ^(?=[^\d]*\d).{8,}$
Console.WriteLine(hasDigit.IsMatch("abcdefg1"));   // True
Console.WriteLine(hasDigit.IsMatch("abcdefgh"));   // False
```

### ReDoS inside assertions

An assertion does not make its pattern safe. A nested repeat inside a lookahead can backtrack exponentially just like one outside it. `AnalyzeSafety` inspects assertion bodies too:

```csharp
Pattern risky = Pattern.With
    .StartOfLine
    .PositiveLookahead(Pattern.With
        .NonCapturingGroup(Pattern.With.Letter.Repeat.OneOrMore).Repeat.OneOrMore
        .Literal("!"))
    .Anything.Repeat.ZeroOrMore;

SafetyReport report = risky.AnalyzeSafety();

Console.WriteLine(report.Risk);               // High
Console.WriteLine(report.Issues[0].Kind);     // NestedQuantifier
Console.WriteLine(report.Issues[0].Fragment); // (?:[a-zA-Z]+)+
```

The compile-time analyzer reports the same problem as warning `FRX004` when you build the project. See [ReDoS Safety](./redos-safety.md).

### Assertions and the non-backtracking engine

The .NET non-backtracking engine does not support lookarounds. `UseNonBacktracking()` (.NET 8 and later) accepts the setting, but the pattern throws when it first runs:

```csharp
Pattern price = Pattern.With
    .PositiveLookbehind(Pattern.With.Literal("$"))
    .Digit.Repeat.OneOrMore
    .UseNonBacktracking();

try
{
    price.IsMatch("$5");
}
catch (NotSupportedException ex)
{
    Console.WriteLine(ex.Message);
    // RegexOptions.NonBacktracking is not supported in conjunction with expressions containing:
    // 'positive lookahead (?= pattern) or positive lookbehind (?<= pattern)'.
}
```

When you need assertions and protection from slow input, keep the backtracking engine and set a timeout with `WithTimeout(...)`.

## Lookaround across the library

Assertions work with every tool in the library, but some tools can only reason about part of what an assertion does. This table sums it up. The sections below show the cases where you are most likely to notice.

| Feature | How it treats assertions |
|---------|--------------------------|
| `Explain()` | Describes each one, for example "Positive lookbehind (must precede)". |
| `Pattern.FromRegex(...).ToFluentCode()` | Turns `(?=`, `(?!`, `(?<=` and `(?<!` back into the four fluent methods. See [Regex to Fluent Code](./regex-to-fluent-code.md). |
| `GenerateMatching` / `GenerateNonMatching` | Generates text for the consuming part and keeps only samples the .NET engine confirms, so every sample honours the assertions. Very narrow assertions can leave fewer samples than you asked for. See [Test Data Generation](./test-data-generation.md). |
| `AnalyzeSafety` | Inspects assertion bodies for ReDoS risks. |
| `Compare`, `IsEquivalentTo`, `IsSubsetOf` | Support positive and negative lookaheads at the very start of the pattern (with or without a leading `^` or `\A`). Any other lookahead and every lookbehind throw `NotSupportedException`. See [Pattern Algebra](./pattern-algebra.md). |
| `GetLanguageFacts` | Exact when the pattern only has leading lookaheads; otherwise a safe approximation (`IsExact` is `false`). See [Language Facts](./language-facts.md). |
| `MeasureCoverage`, `TrackCoverage` | Track branches inside positive assertions. Negative assertions are not instrumented, because their captures never survive. See [Regex Coverage](./regex-coverage.md). |
| `ToJavaScript()`, `ToFlavor(RegexFlavor.JavaScript)`, `ToFlavor(RegexFlavor.JsonSchema)` | Exported as-is, including variable-length lookbehinds. |
| `ToFlavor` for PostgreSQL | Supported. Groups inside an assertion do not capture in PostgreSQL, and a backreference inside an assertion is rejected. |
| `ToFlavor` for MySQL | Supported, but a lookbehind must have a bounded length and a quantifier on an assertion is rejected. |
| `ToFlavor` for MariaDB | Supported, but each top-level branch of a lookbehind must have a fixed length. |
| `ToFlavor` for SQL Server and SQLite | Not supported. Throws `NotSupportedException`. |
| `ToLike` | Drops assertions. The result matches a superset of the pattern and reports itself as not exact. See [SQL LIKE](./sql-like.md). |
| `UseNonBacktracking()` | Not supported by the .NET engine. Throws `NotSupportedException` when the pattern runs. |

### Reading and round-tripping assertions

`Explain` describes each assertion, and `ToFluentCode` rebuilds one from regex text:

```csharp
Pattern price = Pattern.With
    .PositiveLookbehind(Pattern.With.Literal("$"))
    .Digit.Repeat.OneOrMore;

Console.WriteLine(price.Explain());
// Pattern: (?<=\$)\d+
//
// Explanation:
//   - Positive lookbehind (must precede)
//     - Escaped literal '$'
//     - End of group
//   - Digit [0-9]
//   - One or more

Console.WriteLine(Pattern.FromRegex(@"(?<!\d)\d{3}(?=px)").ToFluentCode());
// Pattern.With
//     .NegativeLookbehind(Pattern.With.Digit)
//     .Digit.Repeat.Times(3)
//     .PositiveLookahead(Pattern.With.Literal("px"))
```

### Generating test data

Generated samples always respect the assertions, so you can feed them straight into tests:

```csharp
// strongPassword is the pattern from "Stacking lookaheads for AND rules"
string[] valid = strongPassword.GenerateMatching(count: 20, seed: 7);
string[] invalid = strongPassword.GenerateNonMatching(count: 20, seed: 7);

Console.WriteLine(Array.TrueForAll(valid, strongPassword.IsMatch));                       // True
Console.WriteLine(Array.TrueForAll(invalid, sample => !strongPassword.IsMatch(sample)));  // True
```

### Proving facts about leading lookaheads

Language algebra treats a lookahead at the very start of a pattern as an extra condition on the whole input, so it can prove facts about it. Other assertions are not regular in the sense the algebra needs, and it says so:

```csharp
Pattern fourDigits = Pattern.With.StartOfLine.Digit.Repeat.Times(4).EndOfLine;
Pattern guardedDigits = Pattern.With
    .StartOfLine
    .PositiveLookahead(Pattern.With.Digit.Repeat.Times(4).EndOfLine)
    .Digit.Repeat.OneOrMore
    .EndOfLine;

Console.WriteLine(guardedDigits.IsEquivalentTo(fourDigits));  // True

Pattern qWithoutU = Pattern.With.Literal("q").NegativeLookahead(Pattern.With.Literal("u"));
try
{
    qWithoutU.IsEquivalentTo(fourDigits);
}
catch (NotSupportedException ex)
{
    Console.WriteLine(ex.Message);
    // Language algebra does not support lookaheads except at the very start of the pattern: '(?!u)' at position 1. ...
}
```

### Exporting to other engines

The .NET engine is more flexible with lookbehinds than most. Check every pattern you export:

```csharp
// labelledValue is the pattern from "Variable-length lookbehind"
Console.WriteLine(labelledValue.ToJavaScript());
// /(?<=(?:Total|Subtotal):[\t\n\v\f\r\x85\p{Z}]*)\p{Nd}+/u

try
{
    labelledValue.ToFlavor(RegexFlavor.MySql);
}
catch (NotSupportedException ex)
{
    Console.WriteLine(ex.Message);
    // The pattern cannot be converted to MySQL without changing its meaning:
    //   - lookbehind with an unbounded length (ICU requires a bounded lookbehind) at position 0
}

Pattern boundedLabel = Pattern.With
    .PositiveLookbehind(Pattern.With.OneOf("Total", "Subtotal").Literal(":").Whitespace.Repeat.Times(0, 3))
    .Digit.Repeat.OneOrMore;

Console.WriteLine(boundedLabel.ToFlavor(RegexFlavor.MySql).Pattern);
// (?-i)(?<=(?:Total|Subtotal):[\t\n\x{000B}\f\r\x{0085}\p{Z}]{0,3})\p{Nd}+
```

- For MySQL, replace an open repeat inside a lookbehind with a bounded one such as `Repeat.Times(0, 3)`.
- MariaDB is stricter: each top-level branch of the lookbehind must have a fixed length. `OneOf` wraps its branches in a group, so `PositiveLookbehind(Pattern.With.OneOf("Total: ", "Subtotal: "))` (`(?<=(?:Total: |Subtotal: ))`) is rejected. Write the branches at the top level with raw text, for example `PositiveLookbehind(Pattern.With.RegEx("Total: |Subtotal: "))`, which exports as `(?<=Total: |Subtotal: )`.
- PostgreSQL never captures inside an assertion. A named group there has no entry in the conversion's `GroupNumbers`, so `GroupNumbers.ContainsKey("unit")` is `false` for the `quantity` pattern above.
- SQL Server and SQLite have no lookaround at all. Move the context check into the SQL query, or filter in .NET after the query.

See [Export](./export.md) and [SQL Dialects](./sql-dialects.md) for every rule per engine.

## Tips

- Prefer a plain match when you need the context anyway. `Literal("$").Digit.Repeat.OneOrMore` with a named group for the number is easier to read than a lookbehind, and works in every engine.
- Use a lookbehind or lookahead when the context must stay out of the match: replacements, splits, and values you pass on unchanged.
- For "the whole input must not be X", put a negative lookahead with an end anchor right after the start anchor.
- For several independent rules on one input, stack lookaheads after the start anchor, but keep separate patterns when users need per-rule messages.
- Run `AnalyzeSafety` on patterns that use `.*` inside assertions and run on untrusted input, and set `WithTimeout(...)`.
- For more patterns built from these pieces, see [Common Patterns](./common-patterns.md) and [Best Practices](./best-practices.md).
