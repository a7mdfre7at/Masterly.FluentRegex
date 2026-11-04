---
id: regex-cheat-sheet
title: "Regex Cheat Sheet"
description: "Every .NET regex construct next to the FluentRegex member that writes it, from anchors and classes to conditionals, inline options and Unicode."
---

This page maps regular expression syntax to the fluent API. Each row shows the regex you would write by hand, the FluentRegex call that produces it, and the text that call renders. Every rendering on this page comes from building the call and printing `ToString()`.

Use this page when you need to:

- translate a regex you already know into a fluent chain,
- find the member for a construct, such as `\A`, `(?>...)` or `(?(name)...)`,
- check exactly what a member renders, including how it escapes,
- see which constructs have no fluent member and need the `RegEx(...)` escape hatch.

How to read the tables:

- A member that starts with a dot, such as `.Digit`, is chained on a pattern, usually `Pattern.With.Digit`.
- `x`, `y` and `n` stand for any patterns, for example `Pattern.With.Literal("x")`.
- The **Renders** column is the real output of `ToString()`.

All types live in the `Masterly.FluentRegex` namespace, except `UnicodeScript`, which lives in `Masterly.FluentRegex.Unicode`. Every member works on .NET Standard 2.1, .NET 8 and .NET 10. The only exception is `UseNonBacktracking()`, which needs .NET 8 or later.

## From a regex to a fluent chain

Read the regex from left to right and write one member per element. A quantifier goes after the element it repeats, as `.Repeat...`.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Regex regex = new Regex(@"^\d{3}-\d{3}-\d{4}$");

Pattern phone = Pattern.With
    .StartOfLine
    .Digit.Repeat.Times(3)
    .Literal("-")
    .Digit.Repeat.Times(3)
    .Literal("-")
    .Digit.Repeat.Times(4)
    .EndOfLine;

Console.WriteLine(phone);                                   // ^\d{3}-\d{3}-\d{4}$
Console.WriteLine(phone.ToString() == regex.ToString());    // True
Console.WriteLine(phone.IsMatch("555-123-4567"));           // True
```

- `ToString()` returns the exact regex text. Compare it with your original to check a translation.
- `Literal("-")` escapes for you. You never write a backslash in a fluent chain, except inside `RegEx(...)`.
- Every call returns a new pattern, so you can store parts in variables and combine them (see [Composing Patterns](./composing-patterns.md)).

### Let the library translate it

For a long regex, let FluentRegex write the chain. `Pattern.FromRegex` validates the regex and wraps it. `ToFluentCode()` turns it into C# that rebuilds the same pattern.

```csharp
using System;
using Masterly.FluentRegex;

Pattern existing = Pattern.FromRegex(@"^(?<user>[\w.]+)@(?<host>[\w-]+\.\w{2,})$");

Console.WriteLine(existing.ToFluentCode());
```

It prints:

```text
Pattern.With
    .StartOfLine
    .NamedGroup("user", Pattern.With.Set(Pattern.With.Word.Literal(".")).Repeat.OneOrMore)
    .Literal("@")
    .NamedGroup(
        "host",
        Pattern.With
            .Set(Pattern.With.Word.Literal("-")).Repeat.OneOrMore
            .Literal(".")
            .Word.Repeat.AtLeast(2))
    .EndOfLine
```

- `FromRegex(regex, options)` takes optional `RegexOptions` (default `None`), validates the regex with them, and stores them on the pattern. It throws `ArgumentException` when the regex is not valid .NET syntax, and `ArgumentNullException` for `null`.
- `ToFluentCode(indentSize)` indents nested lines by 4 spaces unless you pass another size.
- Constructs without a fluent member come out as `RegEx(...)` calls. See [Constructs without a fluent member](#constructs-without-a-fluent-member).
- The command-line tool does the same conversion: `fluentregex convert "^\d{3}-\d{4}$"` prints the chain. So does the [Playground](./playground.md). See [Regex to Fluent Code](./regex-to-fluent-code.md).

## Anchors and positions

Anchors match a position, not a character.

| Regex | FluentRegex | Renders | Matches |
|-------|-------------|---------|---------|
| `^` | `.StartOfLine` | `^` | Start of the input, or of any line with `RegexOptions.Multiline` |
| `$` | `.EndOfLine` | `$` | End of the input or before a final `\n`; end of any line with `Multiline` |
| `\A` | `.StartOfInput` | `\A` | Start of the input, in every mode |
| `\z` | `.EndOfInput` | `\z` | The very end of the input, in every mode |
| `\b` | `.WordBoundary` | `\b` | Between a word character and a non-word character |
| `\B` | `.NonWordBoundary` | `\B` | Any position that is not a word boundary |
| `\Z` | `.RegEx(@"\Z")` | `\Z` | End of the input or before a final `\n` (no fluent member) |
| `\G` | `.RegEx(@"\G")` | `\G` | Where the previous match ended (no fluent member) |

`StartOfLine` and `EndOfLine` follow the `Multiline` option. `StartOfInput` and `EndOfInput` ignore it.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

string config = "name=web\nid=42\n";

Pattern anyLine = Pattern.With.StartOfLine.Literal("id=").Digit.Repeat.OneOrMore.EndOfLine
    .WithOptions(RegexOptions.Multiline);
Pattern wholeInput = Pattern.With.StartOfInput.Literal("id=").Digit.Repeat.OneOrMore.EndOfInput;

Console.WriteLine(anyLine.Match(config).Value);    // id=42
Console.WriteLine(wholeInput.IsMatch(config));     // False
Console.WriteLine(wholeInput.IsMatch("id=42"));    // True
```

`$` also matches just before a trailing newline. Use `EndOfInput` when the input must really end there.

```csharp
using System;
using Masterly.FluentRegex;

Pattern endOfLine = Pattern.With.Literal("42").EndOfLine;
Pattern endOfInput = Pattern.With.Literal("42").EndOfInput;

Console.WriteLine(endOfLine.IsMatch("42\n"));    // True
Console.WriteLine(endOfInput.IsMatch("42\n"));   // False
```

Word boundaries find whole words, or text inside a word:

```csharp
using System;
using Masterly.FluentRegex;

Pattern wholeWord = Pattern.With.WordBoundary.Literal("cat").WordBoundary;
Pattern insideWord = Pattern.With.NonWordBoundary.Literal("cat").NonWordBoundary;

Console.WriteLine(wholeWord);                          // \bcat\b
Console.WriteLine(wholeWord.IsMatch("concatenate"));   // False
Console.WriteLine(insideWord.IsMatch("concatenate"));  // True
```

:::tip

For validation, anchor with `StartOfInput` and `EndOfInput`. A pattern anchored with `StartOfLine` and `EndOfLine` accepts `"42\n"` as well as `"42"`. More detail is in [Assertions](./assertions.md).

:::

## Literal characters and escapes

`Literal` matches text exactly. It escapes every regex metacharacter for you.

| Regex | FluentRegex | Renders |
|-------|-------------|---------|
| `a\.b` | `.Literal("a.b")` | `a\.b` |
| `ab` | `.Literal('a', 'b')` | `ab` |
| `\$\(\)` | `.Literal("$()")` | `\$\(\)` |
| `.` | `.Anything` | `.` |
| `\t` | `.Tab` or `.Literal("\t")` | `\t` |
| `\n` | `.Newline` or `.Literal("\n")` | `\n` |
| `\r` | `.CarriageReturn` or `.Literal("\r")` | `\r` |
| `\f` | `.Literal("\f")` | `\f` |
| `\v` | `.Literal("\v")` | `\v` |
| `\e` | `.Literal("\u001B")` | `\e` |
| `\0`, `\a` and other control characters | `.Literal("\0")`, `.Literal("\a")` | `\u0000`, `\u0007` |
| `\x41`, `A` | `.Literal("A")` | `A` |
| `é`, `€` and other non-ASCII text | `.Literal("é€")` | `é€` |

- `Literal(string)` and `Literal(params char[])` do the same thing. Both throw `ArgumentNullException` for `null`.
- Control characters that have no short escape render as `\uXXXX`.
- Outside a set, `-`, `#`, `/` and spaces are not escaped, because they have no special meaning there. Inside a set, `-` is escaped (see [Sets](#sets)).
- `Anything` is `.`: any character except `\n`. With `RegexOptions.Singleline` it also matches `\n`.

Every metacharacter is escaped, so the text always matches itself:

```csharp
using System;
using Masterly.FluentRegex;

Pattern metacharacters = Pattern.With.Literal(@"^$.|{}[]()*+?\");

Console.WriteLine(metacharacters);                                // \^\$\.\|\{\}\[\]\(\)\*\+\?\\
Console.WriteLine(metacharacters.IsMatch(@"^$.|{}[]()*+?\"));     // True
```

Control characters get their short escape when one exists:

```csharp
using System;
using Masterly.FluentRegex;

Pattern price = Pattern.With.Literal("$9.99 (incl. tax)?");
Pattern controls = Pattern.With.Literal("\t\f\v\u001B\0");

Console.WriteLine(price);       // \$9\.99 \(incl\. tax\)\?
Console.WriteLine(controls);    // \t\f\v\e\u0000
Console.WriteLine(price.IsMatch("Total: $9.99 (incl. tax)?"));   // True
```

:::note

Under `RegexOptions.IgnorePatternWhitespace` (or an inline `(?x:...)` scope made with `InlineOptions`), the engine ignores unescaped whitespace and treats `#` as the start of a comment. `Literal` escapes both there, so `Pattern.With.Literal("a c").WithOptions(RegexOptions.IgnorePatternWhitespace)` renders `a\ c` and matches `"a c"`.

:::

## Character classes

### Shorthand classes

| Regex | FluentRegex | Renders | Matches |
|-------|-------------|---------|---------|
| `\d` | `.Digit` | `\d` | Any Unicode decimal digit, not only `0-9` |
| `\D` | `.NonDigit` | `\D` | Anything that is not a digit |
| `\w` | `.Word` | `\w` | A Unicode letter, decimal digit, connector such as `_`, or non-spacing mark |
| `\W` | `.NonWord` | `\W` | Anything that is not a word character |
| `\s` | `.Whitespace` | `\s` | Any Unicode whitespace, including the no-break space |
| `\S` | `.NonWhitespace` | `\S` | Anything that is not whitespace |

:::note

In .NET these classes are Unicode-aware. `\d` matches Arabic-Indic `٣`, and `\w` matches `é`. Use the ASCII members below when only ASCII is valid, or call `WithAsciiDigits()` on a finished pattern. See [Unicode](./unicode.md).

:::

### ASCII ranges

| Regex | FluentRegex | Renders | Inside a set |
|-------|-------------|---------|--------------|
| `[0-9]` | `.AsciiDigit` | `[0-9]` | `0-9` |
| `[a-zA-Z]` | `.Letter` | `[a-zA-Z]` | `a-zA-Z` |
| `[a-z]` | `.LowercaseLetter` | `[a-z]` | `a-z` |
| `[A-Z]` | `.UppercaseLetter` | `[A-Z]` | `A-Z` |
| `[0-9a-fA-F]` | `.HexDigit` | `[0-9a-fA-F]` | `0-9a-fA-F` |
| `[a-f]` | `.Range('a', 'f')` | `[a-f]` | `a-f` |
| `[ -~]` | `.Range(' ', '~')` | `[\u0020-~]` | `\u0020-~` |
| `[--/]` | `.Range('-', '/')` | `[\u002D-/]` | `\u002D-/` |

- These members carry their own brackets when they stand alone, and drop them inside `Set(...)`. That is why you can combine them in one set.
- `Range` writes a boundary that needs escaping (such as `-`, `]`, `\`, a space or a control character) as `\uXXXX`.
- `Range(from, to)` throws `ArgumentException` when `from` is greater than `to`.

### Sets

| Regex | FluentRegex | Renders |
|-------|-------------|---------|
| `[abc]` | `.AnyOf("abc")` or `.Set(Pattern.With.Literal("abc"))` | `[abc]` |
| `[^abc]` | `.NoneOf("abc")` or `.NegatedSet(Pattern.With.Literal("abc"))` | `[^abc]` |
| `[a-fA-F\d]` | `.Set(Pattern.With.Range('a', 'f').Range('A', 'F').Digit)` | `[a-fA-F\d]` |
| `[\w.-]` | `.Set(Pattern.With.Word.Literal(".-"))` | `[\w\.\-]` |
| `[^\s,]` | `.NegatedSet(Pattern.With.Whitespace.Literal(","))` | `[^\s,]` |
| `[a-zA-Z0-9_]` | `.Set(Pattern.With.Letter.AsciiDigit.Literal("_"))` | `[a-zA-Z0-9_]` |
| `[\p{Lu}\d]` | `.Set(Pattern.With.Unicode(UnicodeCategory.UppercaseLetter).Digit)` | `[\p{Lu}\d]` |
| `[-\]^\\]` | `.AnyOf(@"-]^\")` | `[\-\]\^\\]` |
| `[a-z-[aeiou]]` | `.RegEx(@"[a-z-[aeiou]]")` | `[a-z-[aeiou]]` (class subtraction has no fluent member) |

`Set` and `NegatedSet` take a pattern that lists the members. `AnyOf` and `NoneOf` are shortcuts for a list of literal characters.

```csharp
using System;
using Masterly.FluentRegex;

Pattern hexOrDash = Pattern.With.Set(Pattern.With.HexDigit.Literal("-"));
Pattern identifierStart = Pattern.With.Set(Pattern.With.Letter.Literal("_"));
Pattern notSeparator = Pattern.With.NegatedSet(Pattern.With.Whitespace.Literal(",;"));
Pattern range = Pattern.With.Set(Pattern.With.Range('a', 'f').Range('A', 'F').Digit);

Console.WriteLine(hexOrDash);         // [0-9a-fA-F\-]
Console.WriteLine(identifierStart);   // [a-zA-Z_]
Console.WriteLine(notSeparator);      // [^\s,;]
Console.WriteLine(range);             // [a-fA-F\d]
```

Every character given to `AnyOf`, `NoneOf` or `Literal` inside a set is literal. A `-` never forms a range by accident, and `^` and `]` are escaped too.

```csharp
using System;
using Masterly.FluentRegex;

Pattern punctuation = Pattern.With.AnyOf(".-^]");
Pattern noQuotes = Pattern.With.NoneOf("\"'");

Console.WriteLine(punctuation);                // [\.\-\^\]]
Console.WriteLine(noQuotes);                   // [^"']
Console.WriteLine(punctuation.IsMatch("/"));   // False
```

- `AnyOf` and `NoneOf` throw `ArgumentException` for `null` or an empty string.
- Use `Range` for ranges. `Literal("a-f")` inside a set means the three characters `a`, `-` and `f`.

### What changes inside a set

Some members mean something else, or are not allowed, inside `Set(...)`.

| Member | Inside a set | Why |
|--------|--------------|-----|
| `.Anything` | Renders `.`, which matches a literal dot | `.` has no special meaning in a class |
| `.Letter`, `.LowercaseLetter`, `.UppercaseLetter`, `.HexDigit`, `.AsciiDigit`, `.Range(...)`, `.Script(...)`, `.DigitIn(...)` | Render without brackets, so they combine | They are class members |
| `.Digit`, `.Word`, `.Whitespace` and their negations, `.Tab`, `.Newline`, `.CarriageReturn`, `.Unicode(...)`, `.NotUnicode(...)`, `.UnicodeBlock(...)` | Render unchanged | Shorthands and escapes work in classes |
| `.CodePoint`, `.Grapheme` | Throw `InvalidOperationException` when rendered | They can match more than one UTF-16 code unit |
| `.LetterIn(script)` | Renders the letter ranges instead of a class subtraction | Subtraction is not allowed inside a set |

```csharp
using System;
using Masterly.FluentRegex;

Pattern dotInSet = Pattern.With.Set(Pattern.With.Anything);

Console.WriteLine(dotInSet);               // [.]
Console.WriteLine(dotInSet.IsMatch("x"));  // False
Console.WriteLine(dotInSet.IsMatch("."));  // True
```

## Quantifiers

Write `.Repeat` after the element and pick a count. Add `.Lazy` or `.Possessive` right after the count to change how it backtracks.

| Regex | FluentRegex | Renders |
|-------|-------------|---------|
| `\d+` | `.Digit.Repeat.OneOrMore` | `\d+` |
| `\d*` | `.Digit.Repeat.ZeroOrMore` | `\d*` |
| `\d?` | `.Digit.Repeat.Optional` | `\d?` |
| `\d{3}` | `.Digit.Repeat.Times(3)` | `\d{3}` |
| `\d{2,4}` | `.Digit.Repeat.Times(2, 4)` | `\d{2,4}` |
| `\d{2,}` | `.Digit.Repeat.AtLeast(2)` | `\d{2,}` |
| `\d{0,4}` | `.Digit.Repeat.AtMost(4)` | `\d{0,4}` |
| `\d+?` | `.Digit.Repeat.OneOrMore.Lazy` | `\d+?` |
| `\d*?` | `.Digit.Repeat.ZeroOrMore.Lazy` | `\d*?` |
| `\d??` | `.Digit.Repeat.Optional.Lazy` | `\d??` |
| `\d{2,4}?` | `.Digit.Repeat.Times(2, 4).Lazy` | `\d{2,4}?` |
| `\d{2,}?` | `.Digit.Repeat.AtLeast(2).Lazy` | `\d{2,}?` |
| `\d{0,4}?` | `.Digit.Repeat.AtMost(4).Lazy` | `\d{0,4}?` |
| `\d++` (possessive) | `.Digit.Repeat.OneOrMore.Possessive` | `(?>\d+)` |
| `\d*+` (possessive) | `.Digit.Repeat.ZeroOrMore.Possessive` | `(?>\d*)` |
| `\d?+` (possessive) | `.Digit.Repeat.Optional.Possessive` | `(?>\d?)` |
| `\d{2,4}+` (possessive) | `.Digit.Repeat.Times(2, 4).Possessive` | `(?>\d{2,4})` |
| `(?:\d{2}){3}` | `.Digit.Repeat.Times(2).Repeat.Times(3)` | `(?:\d{2}){3}` |
| `(?:ab)+` | `.NonCapturingGroup(Pattern.With.Literal("ab")).Repeat.OneOrMore` | `(?:ab)+` |

- .NET has no possessive syntax (`\d++` is a "nested quantifier" error). `Possessive` renders the equivalent atomic group.
- `{,n}` is not a quantifier in .NET. It matches the literal text `{,n}`. `AtMost(n)` always writes `{0,n}`.
- A count on a count wraps the first one in `(?:...)`, so the result is always valid.

A quantifier repeats only the last element. On a literal string, that is the last character:

```csharp
using System;
using Masterly.FluentRegex;

Pattern lastOnly = Pattern.With.Literal("ab").Repeat.OneOrMore;
Pattern wholeWord = Pattern.With.NonCapturingGroup(Pattern.With.Literal("ab")).Repeat.OneOrMore;

Console.WriteLine(lastOnly);                         // ab+
Console.WriteLine(wholeWord);                        // (?:ab)+
Console.WriteLine(lastOnly.Match("ababab").Value);   // ab
Console.WriteLine(wholeWord.Match("ababab").Value);  // ababab
```

Lazy and possessive in practice:

```csharp
using System;
using Masterly.FluentRegex;

Pattern greedyTag = Pattern.With.Literal("<").Anything.Repeat.OneOrMore.Literal(">");
Pattern lazyTag = Pattern.With.Literal("<").Anything.Repeat.OneOrMore.Lazy.Literal(">");
Pattern possessive = Pattern.With.Digit.Repeat.OneOrMore.Possessive;

Console.WriteLine(lazyTag);                          // <.+?>
Console.WriteLine(greedyTag.Match("<a><b>").Value);  // <a><b>
Console.WriteLine(lazyTag.Match("<a><b>").Value);    // <a>
Console.WriteLine(possessive);                       // (?>\d+)
```

Two stacking rules to know:

```csharp
using System;
using Masterly.FluentRegex;

Pattern stacked = Pattern.With.Digit.Repeat.Times(2).Repeat.Times(3);
Pattern optionalAfterQuantifier = Pattern.With.Digit.Repeat.OneOrMore.Repeat.Optional;

Console.WriteLine(stacked);                  // (?:\d{2}){3}
Console.WriteLine(optionalAfterQuantifier);  // \d+?
```

- `Repeat.Optional` directly after another quantifier wraps the element too, so `Digit.Repeat.OneOrMore.Repeat.Optional` renders `(?:\d+)?`. Write `.Lazy` for a lazy quantifier.
- A quantifier on an empty pattern, such as `Pattern.With.Repeat.OneOrMore`, throws `InvalidOperationException`. Reading `.Repeat` alone does not throw; the count does. `Lazy` and `Possessive` also throw `InvalidOperationException` when the last element has no quantifier.
- Negative counts throw `ArgumentOutOfRangeException`, and so does `Times(min, max)` when `max` is less than `min`.

More detail, including catastrophic backtracking, is in [Quantifiers](./quantifiers.md).

## Alternation and concatenation

| Regex | FluentRegex | Renders | Capturing |
|-------|-------------|---------|-----------|
| `(?:x\|y)` | `x \| y` | `(?:x\|y)` | No |
| `(?:x\|y\|n)` | `x \| y \| n` | `(?:x\|y\|n)` | No; chains flatten into one group |
| `(?:x\|y)` | `.OneOf(x, y)` | `(?:x\|y)` | No |
| `(?:cat\|dog\|c\+\+)` | `.OneOf("cat", "dog", "c++")` | `(?:cat\|dog\|c\+\+)` | No; the words are escaped |
| `(x\|y)` | `.Choice(x, y)` | `(x\|y)` | Yes, a numbered group |
| `(x\|y\|n)` | `.Choice(x, y, n)` | `(x\|y\|n)` | Yes |
| `xy` | `x + y` or `x.Then(y)` | `xy` | Not a group |

```csharp
using System;
using Masterly.FluentRegex;

Pattern cat = Pattern.With.Literal("cat");
Pattern dog = Pattern.With.Literal("dog");

Pattern withOperator = cat | dog;
Pattern withOneOf = Pattern.With.OneOf(cat, dog);
Pattern withWords = Pattern.With.OneOf("cat", "dog", "c++");
Pattern withChoice = Pattern.With.Choice(cat, dog);

Console.WriteLine(withOperator);   // (?:cat|dog)
Console.WriteLine(withOneOf);      // (?:cat|dog)
Console.WriteLine(withWords);      // (?:cat|dog|c\+\+)
Console.WriteLine(withChoice);     // (cat|dog)
Console.WriteLine(withChoice.Match("hotdog").Groups[1].Value);  // dog
```

- The `|` operator and `OneOf` never add a numbered group. `Choice` always does, so it shifts the numbers of the groups after it.
- `OneOf` throws `ArgumentException` when you pass an empty array. A call with no arguments at all, `OneOf()`, does not compile, because it is ambiguous between the `Pattern[]` and `string[]` overloads. A `null` word or pattern throws `ArgumentNullException`.
- A bare `x|y` at the top level of a regex has no fluent form. The fluent API always wraps an alternation in a group, which matches the same text.

Concatenate with `+` or `Then`. A quantifier after `Then` applies to the last element of the appended pattern.

```csharp
using System;
using Masterly.FluentRegex;

Pattern area = Pattern.With.Digit.Repeat.Times(3);
Pattern dash = Pattern.With.Literal("-");

Console.WriteLine(area + dash + area);     // \d{3}-\d{3}
Console.WriteLine(area.Then(dash));        // \d{3}-
```

## Groups

| Regex | FluentRegex | Renders |
|-------|-------------|---------|
| `(x)` | `.Group(x)` | `(x)` |
| `(?<year>\d{4})` | `.NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))` | `(?<year>\d{4})` |
| `(?'year'\d{4})` | `.NamedGroup("year", ...)` | `(?<year>\d{4})` |
| `(?:x)` | `.NonCapturingGroup(x)` | `(?:x)` |
| `(?>\d+)` | `.AtomicGroup(Pattern.With.Digit.Repeat.OneOrMore)` | `(?>\d+)` |
| `(?<close-open>...)` | `.RegEx(@"(?<close-open>...)")` | Balancing groups have no fluent member |

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern date = Pattern.With
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
    .Literal("-")
    .Group(Pattern.With.Digit.Repeat.Times(2));

Match match = date.Match("Released 2026-11");

Console.WriteLine(date);                          // (?<year>\d{4})-(\d{2})
Console.WriteLine(match.Groups["year"].Value);    // 2026
Console.WriteLine(match.Groups[1].Value);         // 11
```

- A group name may contain only letters, digits and underscores. Any other name, or an empty or `null` one, throws `ArgumentException`.
- The builder does not check the first character. .NET rejects a name that starts with a digit, such as `"1a"`, when the pattern runs. A name made only of digits, such as `"2"`, renders `(?<2>...)`, which .NET treats as numbered group 2.
- .NET numbers unnamed groups first and named groups after them. That is why the unnamed `(\d{2})` is group 1 here.
- To read groups into a typed object instead of by name, see [Typed Extraction](./typed-extraction.md).

## Backreferences

| Regex | FluentRegex | Renders |
|-------|-------------|---------|
| `\1` | `.Backreference(1)` | `\1` |
| `\12` | `.Backreference(12)` | `\12` |
| `\k<word>` | `.Backreference("word")` | `\k<word>` |
| `\k'word'` | `.Backreference("word")` | `\k<word>` |

```csharp
using System;
using Masterly.FluentRegex;

Pattern repeatedWord = Pattern.With
    .WordBoundary
    .NamedGroup("word", Pattern.With.Word.Repeat.OneOrMore)
    .Whitespace.Repeat.OneOrMore
    .Backreference("word")
    .WordBoundary;

Pattern doubledLetter = Pattern.With.Group(Pattern.With.Letter).Backreference(1);

Console.WriteLine(repeatedWord);                                // \b(?<word>\w+)\s+\k<word>\b
Console.WriteLine(repeatedWord.Match("see the the cat").Value); // the the
Console.WriteLine(doubledLetter);                               // ([a-zA-Z])\1
Console.WriteLine(doubledLetter.Match("balloon").Value);        // ll
```

- `Backreference(int)` throws `ArgumentOutOfRangeException` for numbers below 1.
- A backreference to a group that the pattern does not define is a .NET error. The compile-time analyzer reports it as FRX003 (see [Compile-Time Checks](./compile-time-checks.md)).
- More examples are in [Groups and Backreferences](./groups-and-backreferences.md).

## Lookahead and lookbehind

| Regex | FluentRegex | Renders | Succeeds when |
|-------|-------------|---------|---------------|
| `(?=x)` | `.PositiveLookahead(x)` | `(?=x)` | `x` follows |
| `(?!x)` | `.NegativeLookahead(x)` | `(?!x)` | `x` does not follow |
| `(?<=x)` | `.PositiveLookbehind(x)` | `(?<=x)` | `x` precedes |
| `(?<!x)` | `.NegativeLookbehind(x)` | `(?<!x)` | `x` does not precede |

```csharp
using System;
using Masterly.FluentRegex;

Pattern amount = Pattern.With.PositiveLookbehind(Pattern.With.Literal("$")).Digit.Repeat.OneOrMore;
Pattern notNegative = Pattern.With
    .NegativeLookbehind(Pattern.With.Literal("-"))
    .WordBoundary
    .Digit.Repeat.OneOrMore;
Pattern hasDigit = Pattern.With
    .StartOfInput
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Digit)
    .NegativeLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Whitespace)
    .Anything.Repeat.AtLeast(8)
    .EndOfInput;

Console.WriteLine(amount);                                 // (?<=\$)\d+
Console.WriteLine(amount.Match("Price: $100").Value);      // 100
Console.WriteLine(notNegative.Match("-5 and 7").Value);    // 7
Console.WriteLine(hasDigit);                               // \A(?=.*\d)(?!.*\s).{8,}\z
Console.WriteLine(hasDigit.IsMatch("secret123"));          // True
Console.WriteLine(hasDigit.IsMatch("secret 123"));         // False
```

- Lookarounds are zero-width: the matched text does not include them. `amount` returns `100`, not `$100`.
- .NET allows variable-length lookbehind. Other engines may not. See [Export](./export.md) before you move a pattern to another engine.
- The non-backtracking engine does not support lookarounds. See [Options for the whole pattern](#options-for-the-whole-pattern).

## Conditionals

| Regex | FluentRegex | Renders |
|-------|-------------|---------|
| `(?(open)y\|n)` | `.Conditional("open", y, n)` | `(?(open)y\|n)` |
| `(?(1)y\|n)` | `.Conditional(1, y, n)` | `(?(1)y\|n)` |
| `(?(1)y)` | `.Conditional(1, y, Pattern.With)` or `.Conditional(1, y, null)` | `(?(1)y\|)` |
| `(?(?=x)y\|n)` | `.RegEx(@"(?(?=x)y\|n)")` | Expression conditions have no fluent member |

A conditional matches `y` when the group took part in the match, and `n` when it did not.

```csharp
using System;
using Masterly.FluentRegex;

Pattern areaCode = Pattern.With
    .StartOfInput
    .NamedGroup("open", Pattern.With.Literal("(")).Repeat.Optional
    .Digit.Repeat.Times(3)
    .Conditional("open", Pattern.With.Literal(")"), Pattern.With.Literal("-"))
    .EndOfInput;

Console.WriteLine(areaCode);                   // \A(?<open>\()?\d{3}(?(open)\)|-)\z
Console.WriteLine(areaCode.IsMatch("(555)"));  // True
Console.WriteLine(areaCode.IsMatch("555-"));   // True
Console.WriteLine(areaCode.IsMatch("(555-"));  // False
```

The numbered form works the same way. Pass an empty pattern when there is nothing to match in the "no" branch:

```csharp
using System;
using Masterly.FluentRegex;

Pattern quoted = Pattern.With
    .Group(Pattern.With.Literal("\"")).Repeat.Optional
    .Word.Repeat.OneOrMore
    .Conditional(1, Pattern.With.Literal("\""), Pattern.With);

Console.WriteLine(quoted);   // (")?\w+(?(1)"|)
```

- `Conditional(int, ...)` throws `ArgumentOutOfRangeException` for numbers below 1. Group names follow the same rules as `NamedGroup`.
- A `null` "no" branch is treated as an empty pattern. A `null` "yes" branch throws `ArgumentNullException`.

:::warning

Define the group before the conditional. When no group has the given name, .NET reads `(?(name)...)` as a lookahead test for the text `name`, not as a group check. The pattern builds without error but matches something else.

:::

## Options

### Inline option scopes

These members turn options on or off for one part of the pattern only.

| Regex | FluentRegex | Renders |
|-------|-------------|---------|
| `(?i:x)` | `.IgnoreCase(x)` | `(?i:x)` |
| `(?-i:x)` | `.CaseSensitive(x)` | `(?-i:x)` |
| `(?m:x)` | `.InlineOptions(RegexOptions.Multiline, x)` | `(?m:x)` |
| `(?s:x)` | `.InlineOptions(RegexOptions.Singleline, x)` | `(?s:x)` |
| `(?n:x)` | `.InlineOptions(RegexOptions.ExplicitCapture, x)` | `(?n:x)` |
| `(?x:x)` | `.InlineOptions(RegexOptions.IgnorePatternWhitespace, x)` | `(?x:x)` |
| `(?im-s:x)` | `.InlineOptions(RegexOptions.IgnoreCase \| RegexOptions.Multiline, x, RegexOptions.Singleline)` | `(?im-s:x)` |
| `(?-i:x)` | `.InlineOptions(RegexOptions.None, x, RegexOptions.IgnoreCase)` | `(?-i:x)` |

`InlineOptions(enable, pattern, disable)` takes the options to turn on, the inner pattern, and optionally the options to turn off. With nothing to turn on or off it renders a plain `(?:x)`.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern code = Pattern.With
    .Literal("ID-")
    .IgnoreCase(Pattern.With.Literal("abc"));

Console.WriteLine(code);                       // ID-(?i:abc)
Console.WriteLine(code.IsMatch("ID-AbC"));     // True
Console.WriteLine(code.IsMatch("id-abc"));     // False

Pattern mixed = Pattern.With.InlineOptions(
    RegexOptions.IgnoreCase | RegexOptions.Multiline,
    Pattern.With.StartOfLine.Literal("end"),
    RegexOptions.Singleline);

Console.WriteLine(mixed);                      // (?im-s:^end)
```

- Only `IgnoreCase` (`i`), `Multiline` (`m`), `ExplicitCapture` (`n`), `Singleline` (`s`) and `IgnorePatternWhitespace` (`x`) can be written inline. Any other flag, such as `RightToLeft` or `Compiled`, throws `ArgumentException`.
- An option switch without a scope, such as `(?i)` in the middle of a regex, has no fluent member. Use a scope, or `RegEx("(?i)")`.

### Options for the whole pattern

Options that apply to the whole pattern are not part of the regex text. They are stored on the pattern and used when it runs.

| Regex or .NET | FluentRegex | Effect |
|---------------|-------------|--------|
| `(?i)` at the start, or `RegexOptions.IgnoreCase` | `.WithOptions(RegexOptions.IgnoreCase)` | Case-insensitive matching |
| `(?m)`, `RegexOptions.Multiline` | `.WithOptions(RegexOptions.Multiline)` | `^` and `$` match at every line |
| `(?s)`, `RegexOptions.Singleline` | `.WithOptions(RegexOptions.Singleline)` | `.` also matches `\n` |
| `(?n)`, `RegexOptions.ExplicitCapture` | `.WithOptions(RegexOptions.ExplicitCapture)` | Only named groups capture |
| `(?x)`, `RegexOptions.IgnorePatternWhitespace` | `.WithOptions(RegexOptions.IgnorePatternWhitespace)` | Spaces ignored, `#` starts a comment |
| `RegexOptions.CultureInvariant`, `RightToLeft`, `ECMAScript`, `Compiled` | `.WithOptions(...)` | As in .NET |
| `RegexOptions.NonBacktracking` | `.UseNonBacktracking()` (.NET 8 or later) | Linear-time engine, immune to ReDoS |
| `new Regex(..., timeout)` | `.WithTimeout(TimeSpan.FromMilliseconds(100))` | Throws `RegexMatchTimeoutException` when the time runs out |

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern word = Pattern.With.Literal("hello").WithOptions(RegexOptions.IgnoreCase);
Pattern strictPart = word.Then(Pattern.With.CaseSensitive(Pattern.With.Literal("!X")));

Console.WriteLine(word);                           // hello
Console.WriteLine(word.Options);                   // IgnoreCase
Console.WriteLine(word.IsMatch("HELLO"));          // True
Console.WriteLine(strictPart);                     // hello(?-i:!X)
Console.WriteLine(strictPart.IsMatch("HELLO!x"));  // False
```

- `ToString()` does not show `WithOptions` options. Read them from the `Options` property.
- `WithOptions` replaces the options. To add one, combine them: `p.WithOptions(p.Options | RegexOptions.Multiline)`.
- `Then` and `+` keep the options of the left pattern. The `|` operator keeps none: `a | b` starts with `RegexOptions.None` even when `a` or `b` has options. Members that take an inner pattern, such as `Group(...)`, also ignore the inner pattern's options. Set options on the finished pattern.
- `WithTimeout` throws `ArgumentOutOfRangeException` for zero, a negative value, or a value over about 24 days. `Regex.InfiniteMatchTimeout` turns the timeout off.
- `UseNonBacktracking()` adds `RegexOptions.NonBacktracking`. Running a pattern that contains backreferences, lookarounds, atomic groups (including `Possessive`) or conditionals then throws `NotSupportedException`. Combined with `RegexOptions.RightToLeft` it throws `ArgumentOutOfRangeException`. Lazy quantifiers work. See [ReDoS Safety](./redos-safety.md).

Combining patterns with `|` starts again from no options:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern caseless = Pattern.With.Literal("a").WithOptions(RegexOptions.IgnoreCase);
Pattern either = caseless | Pattern.With.Literal("b");

Console.WriteLine(either.Options);                                          // None
Console.WriteLine(either.IsMatch("A"));                                     // False
Console.WriteLine(either.WithOptions(RegexOptions.IgnoreCase).IsMatch("A")); // True
```

## Unicode

| Regex | FluentRegex | Renders |
|-------|-------------|---------|
| `\p{Lu}` | `.Unicode(UnicodeCategory.UppercaseLetter)` | `\p{Lu}` |
| `\P{Lu}` | `.NotUnicode(UnicodeCategory.UppercaseLetter)` | `\P{Lu}` |
| `\p{L}` | `.Unicode(UnicodeGroup.Letter)` | `\p{L}` |
| `\P{L}` | `.NotUnicode(UnicodeGroup.Letter)` | `\P{L}` |
| `\p{IsGreek}` | `.UnicodeBlock("Greek")` or `.UnicodeBlock("IsGreek")` | `\p{IsGreek}` |
| `\p{IsCyrillic}` | `.UnicodeBlock("Cyrillic")` | `\p{IsCyrillic}` |
| A script's character ranges | `.Script(UnicodeScript.Greek)` | `[\u0300-\u036F\u0370-\u03E1\u03F0-\u03FF\u1F00-\u1FFF]` |
| A script's letters only | `.LetterIn(UnicodeScript.Arabic)` | `[\u0600-\u06FF\u0750-\u077F\u0870-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF-[\P{L}]]` |
| A script's digits | `.DigitIn(UnicodeScript.Arabic)` | `[\u0660-\u0669\u06F0-\u06F9]` |
| `[0-9]` | `.DigitIn(UnicodeScript.Latin)` | `[0-9]` |
| One code point (a surrogate pair or one character) | `.CodePoint` | `(?:[\uD800-\uDBFF][\uDC00-\uDFFF]\|[^\uD800-\uDFFF])` |
| One user-perceived character | `.Grapheme` | A long alternation that approximates a grapheme cluster |
| `\d` as ASCII only, across a whole pattern | `.WithAsciiDigits()` | `^\d{4}$` becomes `^[0-9]{4}$` |
| The same, chosen by a setting | `.WithDigits(DigitMode.Ascii)` | `^\d{4}\D$` becomes `^[0-9]{4}[^0-9]$`; `DigitMode.Unicode` leaves it unchanged |
| Ignore accents and harakat | `.IgnoreDiacritics()` | Each letter becomes a class of its accented forms |

`UnicodeCategory` comes from `System.Globalization`. `UnicodeGroup` is a FluentRegex enum for the seven general groups.

```csharp
using System;
using System.Globalization;
using Masterly.FluentRegex;

Pattern capital = Pattern.With.Unicode(UnicodeCategory.UppercaseLetter);
Pattern anyLetter = Pattern.With.Unicode(UnicodeGroup.Letter).Repeat.OneOrMore;
Pattern greek = Pattern.With.UnicodeBlock("Greek").Repeat.OneOrMore;
Pattern capitalOrDigit = Pattern.With.Set(Pattern.With.Unicode(UnicodeCategory.UppercaseLetter).Digit);

Console.WriteLine(capital);                                // \p{Lu}
Console.WriteLine(anyLetter.Match("42 Ünïcødé!").Value);   // Ünïcødé
Console.WriteLine(greek);                                  // \p{IsGreek}+
Console.WriteLine(greek.Match("alpha αβγ").Value);         // αβγ
Console.WriteLine(capitalOrDigit);                         // [\p{Lu}\d]
```

Scripts and digits:

```csharp
using System;
using Masterly.FluentRegex;
using Masterly.FluentRegex.Unicode;

Pattern arabicDigits = Pattern.With.DigitIn(UnicodeScript.Arabic).Repeat.OneOrMore;
Pattern asciiYear = Pattern.With.AsciiDigit.Repeat.Times(4);
Pattern unicodeYear = Pattern.With.Digit.Repeat.Times(4);

Console.WriteLine(arabicDigits);                          // [\u0660-\u0669\u06F0-\u06F9]+
Console.WriteLine(arabicDigits.Match("رقم ١٢٣").Value);   // ١٢٣
Console.WriteLine(unicodeYear.IsMatch("٢٠٢٦"));           // True
Console.WriteLine(asciiYear.IsMatch("٢٠٢٦"));             // False
```

Code points and graphemes count characters the way people see them:

```csharp
using System;
using Masterly.FluentRegex;

Pattern oneCodePoint = Pattern.With.CodePoint;
Pattern oneGrapheme = Pattern.With.Grapheme;
string family = "👨‍👩‍👧";

Console.WriteLine(oneCodePoint.Count(family));  // 5
Console.WriteLine(oneGrapheme.Count(family));   // 1
```

- The family emoji is five code points: three people joined by two zero-width joiners. `Grapheme` sees it as one character.
- `UnicodeBlock` checks only that the name is not empty and has letters, digits, `-` and `_` (otherwise `ArgumentException`), and adds the `Is` prefix when it is missing. An unknown block name builds, but .NET throws `RegexParseException` (an `ArgumentException`) when the pattern runs. The analyzer reports it at build time as FRX003.
- `WithAsciiDigits()` rewrites `\d` and `\D` everywhere, including inside sets. It leaves `\w` and `\p{Nd}` as written. `IgnoreDiacritics()` throws `InvalidOperationException` on a pattern that uses `RegexOptions.ECMAScript`.
- `DigitIn` throws `ArgumentException` for a script without its own digits, such as Greek or Han. Scripts with digits: Latin, Arabic, Devanagari, Bengali, Gurmukhi, Gujarati, Tamil, Telugu, Kannada, Malayalam, Thai, Lao, Tibetan, Khmer, Mongolian.
- `Script`, `LetterIn` and `DigitIn` cover the Basic Multilingual Plane only.
- The full guide, including every supported script, is [Unicode](./unicode.md).

### Category codes

| `UnicodeCategory` | Renders | `UnicodeCategory` | Renders |
|-------------------|---------|-------------------|---------|
| `UppercaseLetter` | `\p{Lu}` | `Control` | `\p{Cc}` |
| `LowercaseLetter` | `\p{Ll}` | `Format` | `\p{Cf}` |
| `TitlecaseLetter` | `\p{Lt}` | `Surrogate` | `\p{Cs}` |
| `ModifierLetter` | `\p{Lm}` | `PrivateUse` | `\p{Co}` |
| `OtherLetter` | `\p{Lo}` | `OtherNotAssigned` | `\p{Cn}` |
| `NonSpacingMark` | `\p{Mn}` | `ConnectorPunctuation` | `\p{Pc}` |
| `SpacingCombiningMark` | `\p{Mc}` | `DashPunctuation` | `\p{Pd}` |
| `EnclosingMark` | `\p{Me}` | `OpenPunctuation` | `\p{Ps}` |
| `DecimalDigitNumber` | `\p{Nd}` | `ClosePunctuation` | `\p{Pe}` |
| `LetterNumber` | `\p{Nl}` | `InitialQuotePunctuation` | `\p{Pi}` |
| `OtherNumber` | `\p{No}` | `FinalQuotePunctuation` | `\p{Pf}` |
| `SpaceSeparator` | `\p{Zs}` | `OtherPunctuation` | `\p{Po}` |
| `LineSeparator` | `\p{Zl}` | `MathSymbol` | `\p{Sm}` |
| `ParagraphSeparator` | `\p{Zp}` | `CurrencySymbol` | `\p{Sc}` |
| | | `ModifierSymbol` | `\p{Sk}` |
| | | `OtherSymbol` | `\p{So}` |

| `UnicodeGroup` | Renders | Covers |
|----------------|---------|--------|
| `Letter` | `\p{L}` | Any letter |
| `Mark` | `\p{M}` | Any combining mark |
| `Number` | `\p{N}` | Any number |
| `Punctuation` | `\p{P}` | Any punctuation |
| `Symbol` | `\p{S}` | Any symbol |
| `Separator` | `\p{Z}` | Any separator |
| `Other` | `\p{C}` | Control, format, surrogate, private use and unassigned |

## Constructs without a fluent member

Some .NET syntax has no fluent member. Write it with `RegEx(...)`, which appends regex text without escaping. `ToFluentCode()` does the same when it meets these constructs. The one exception is a top-level `x|y`, which `ToFluentCode()` writes as `Pattern.With.OneOf(...)`.

| Regex | Write it as |
|-------|-------------|
| `\G` (where the last match ended) | `.RegEx(@"\G")` |
| `\Z` (end, or before a final newline) | `.RegEx(@"\Z")`, or `.EndOfInput` when you mean the very end |
| `[a-z-[aeiou]]` (class subtraction) | `.RegEx(@"[a-z-[aeiou]]")` |
| `(?<close-open>...)` (balancing group) | `.RegEx(@"(?<close-open>...)")` |
| `(?(?=x)y\|n)` (expression condition) | `.RegEx(@"(?(?=x)y\|n)")` |
| `(?i)` mid-pattern (option switch) | `.RegEx("(?i)")`, or a scope such as `.IgnoreCase(x)` |
| `(?#note)` (inline comment) | `.RegEx("(?#note)")`, or a C# comment |
| `x\|y` at the top level | `x \| y`, which renders `(?:x\|y)` and matches the same text |

```csharp
using System;
using Masterly.FluentRegex;

Pattern consonants = Pattern.With.RegEx(@"[a-z-[aeiou]]").Repeat.OneOrMore;
Pattern contiguous = Pattern.With.RegEx(@"\G").Digit.Literal(",");
Pattern rawText = Pattern.With.RegEx("ab").Repeat.OneOrMore;

Console.WriteLine(consonants);                           // [a-z-[aeiou]]+
Console.WriteLine(consonants.Match("strength").Value);   // str
Console.WriteLine(contiguous.Count("1,2,3, 4,"));        // 3
Console.WriteLine(rawText);                              // ab+
```

- `RegEx(...)` is not validated when you call it. Invalid text fails when the pattern runs, and the analyzer reports constant chains at build time. `RegEx(null)` appends nothing.
- A quantifier after `RegEx("ab")` is appended to the raw text, so it repeats only `b`. Put a multi-character fragment in `NonCapturingGroup(Pattern.With.RegEx(...))` before repeating it.
- `Possessive` after a raw fragment moves only the last atom into the atomic group: `RegEx("abc").Repeat.OneOrMore.Possessive` renders `ab(?>c+)`.

## Errors the builder reports

Invalid calls fail when you build the pattern, not later when it runs. The compile-time analyzer reports the same problems as FRX006 for constant chains.

| Call | Exception |
|------|-----------|
| `.Repeat...` on an empty pattern | `InvalidOperationException`: Repeat must follow an element to repeat |
| `.Lazy` or `.Possessive` without a quantifier before it | `InvalidOperationException` |
| `.Repeat.Times(-1)`, `.AtLeast(-1)`, `.AtMost(-1)` | `ArgumentOutOfRangeException` |
| `.Repeat.Times(4, 2)` | `ArgumentOutOfRangeException`: the maximum cannot be less than the minimum |
| `.Range('z', 'a')` | `ArgumentException`: the range start is greater than the range end |
| `.AnyOf("")`, `.NoneOf("")` | `ArgumentException`: at least one character is required |
| `.OneOf(new Pattern[0])`, `.OneOf(new string[0])` | `ArgumentException`: at least one alternative is required |
| `.NamedGroup("my-name", x)`, `.Backreference("")`, `.Conditional(null, y, n)` | `ArgumentException`: the group name is `null`, empty, or has characters other than letters, digits and underscores |
| `.Backreference(0)`, `.Conditional(0, y, n)` | `ArgumentOutOfRangeException`: group numbers start at 1 |
| `.InlineOptions(RegexOptions.RightToLeft, x)` | `ArgumentException`: the option cannot be applied inline |
| `.DigitIn(UnicodeScript.Greek)` | `ArgumentException`: the script has no decimal digits of its own |
| `.UnicodeBlock("")`, `.UnicodeBlock("Greek and")` | `ArgumentException`: the block name is empty or invalid |
| `.Set(Pattern.With.CodePoint)` | `InvalidOperationException` when the pattern is rendered |
| `.WithTimeout(TimeSpan.Zero)` | `ArgumentOutOfRangeException` |
| A `null` pattern or text argument, such as `.Group(null)` or `.Literal((string)null)` | `ArgumentNullException`. Exceptions: the "no" branch of `Conditional` treats `null` as empty, `RegEx(null)` appends nothing, and a `null` group name throws `ArgumentException` |

See [Compile-Time Checks](./compile-time-checks.md) for the analyzer, and [Validation and Debugging](./validation-and-debugging.md) for checking a pattern at run time.

## Common pitfalls

- **A quantifier repeats one element.** `Literal("ab").Repeat.OneOrMore` is `ab+`. Group first: `NonCapturingGroup(Pattern.With.Literal("ab")).Repeat.OneOrMore`.
- **A lazy quantifier is `.Lazy`, not `Repeat.Optional`.** `Digit.Repeat.OneOrMore.Repeat.Optional` is `(?:\d+)?`, an optional repetition.
- **`Choice` captures.** It adds a numbered group and shifts later group numbers. Use `|` or `OneOf` when you only need alternation.
- **`$` is not the end of the input.** It also matches before a trailing `\n`. Use `EndOfInput` (`\z`) for validation.
- **`\d` and `\w` are Unicode.** Use `AsciiDigit`, `Letter`, `DigitIn(UnicodeScript.Latin)` or `WithAsciiDigits()` for ASCII-only formats.
- **`Anything` inside a set is a literal dot.** `Set(Pattern.With.Anything)` is `[.]`.
- **`Literal("a-f")` inside a set is three characters.** Use `Range('a', 'f')`.
- **`|` and `+` keep only the left side's options.** `a | b` and `a + b` use the options and timeout of `a`. Set options on the result when the sides differ.
- **Options are not in `ToString()`.** A pattern built with `WithOptions(RegexOptions.IgnoreCase)` prints the same text as one without. Pass `Options` along when you copy the regex text elsewhere, or use [Export](./export.md).
- **Patterns are immutable.** `p.WithOptions(...)` on its own line does nothing. Assign the result.

## See also

- [Core Concepts](./core-concepts.md): immutability, `Pattern.With` and how chains render.
- [Basic Patterns](./basic-patterns.md), [Character Classes](./character-classes.md), [Quantifiers](./quantifiers.md), [Groups and Backreferences](./groups-and-backreferences.md), [Assertions](./assertions.md): each construct in depth.
- [Regex to Fluent Code](./regex-to-fluent-code.md): convert an existing regex automatically.
- [Unicode](./unicode.md): scripts, digits, graphemes and diacritics.
- [API Reference](./api-reference.md): every public member.
