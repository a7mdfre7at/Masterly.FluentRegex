---
id: character-classes
title: "Character Classes"
description: "Shorthand classes, sets, ranges, character lists, negated sets and Unicode categories: every way to match one character from a group."
---

A character class matches exactly one character out of a group, such as "a digit", "a vowel" or "any letter in any script". You use a class every time one position in the text may hold several different characters. Repeat the class with a [quantifier](./quantifiers.md) to match a run of them.

The fluent API gives you four kinds of class:

| Kind | Members | Use it when |
|------|---------|-------------|
| Shorthand classes | `Digit`, `Word`, `Whitespace`, their negations, `Tab`, `Newline`, `CarriageReturn` | A standard .NET class already describes the characters. |
| ASCII classes and ranges | `Letter`, `LowercaseLetter`, `UppercaseLetter`, `HexDigit`, `AsciiDigit`, `Range` | The format is defined over plain ASCII, or over a fixed run of code points. |
| Sets | `Set`, `NegatedSet`, `AnyOf`, `NoneOf` | You list the allowed (or forbidden) characters yourself. |
| Unicode classes | `Unicode`, `NotUnicode`, `UnicodeBlock` | The text can be in any language. |

All members live on `Pattern` in the `Masterly.FluentRegex` namespace and are available on every target framework of the package (`netstandard2.1`, `net8.0` and `net10.0`). Like every other member, each one returns a new pattern and leaves the original unchanged.

```csharp
using System;
using Masterly.FluentRegex;

Pattern hexColor = Pattern.With
    .StartOfLine
    .Literal("#")
    .HexDigit.Repeat.Times(6)
    .EndOfLine;

Console.WriteLine(hexColor);                     // prints: ^#[0-9a-fA-F]{6}$
Console.WriteLine(hexColor.IsMatch("#1E90ff"));  // True
Console.WriteLine(hexColor.IsMatch("#1E90fg"));  // False
```

## Shorthand classes

The shorthand classes map one-to-one to the .NET regex escapes. They render the same way on their own and inside a set.

| Member | Renders | Matches |
|--------|---------|---------|
| `Digit` | `\d` | A Unicode decimal digit: `0`-`9`, and also digits such as Arabic-Indic `٣` or Devanagari `३`. |
| `NonDigit` | `\D` | Any character that is not a `\d` digit. |
| `Word` | `\w` | A word character in any script: letters, decimal digits, non-spacing marks and connector punctuation such as `_`. |
| `NonWord` | `\W` | Any character that is not a word character. |
| `Whitespace` | `\s` | A Unicode white-space character, including tab, newline and the no-break space (U+00A0). |
| `NonWhitespace` | `\S` | Any character that is not white space. |
| `Tab` | `\t` | A tab. |
| `Newline` | `\n` | A line feed. |
| `CarriageReturn` | `\r` | A carriage return. |

The .NET classes are Unicode-aware. That is usually what you want for free text, and usually not what you want for a machine format:

```csharp
Console.WriteLine(Pattern.With.Digit.IsMatch("٣"));             // True
Console.WriteLine(Pattern.With.AsciiDigit.IsMatch("٣"));        // False
Console.WriteLine(Pattern.With.Word.IsMatch("é"));              // True
Console.WriteLine(Pattern.With.Letter.IsMatch("é"));            // False
Console.WriteLine(Pattern.With.Whitespace.IsMatch("\u00A0"));   // True
Console.WriteLine(Pattern.With.Anything.IsMatch("\n"));         // False
```

- `Anything` (`.`) is not a class member. It matches any character except `\n`. See [Basic Patterns](./basic-patterns.md).
- When a format is defined over ASCII digits, such as a card number or a port, use `AsciiDigit`, or call `WithAsciiDigits()` on the finished pattern. See [Digits that are ASCII only](#digits-that-are-ascii-only).

:::warning

Many regex references, including older copies of this project's README, describe `\d` as `[0-9]` and `\w` as `[a-zA-Z0-9_]`. That is how they behave in JavaScript, not in .NET. In .NET, `\d` and `\w` accept characters from every script, as the sample shows.

:::

## ASCII letters and digits

These members describe plain ASCII ranges. They render with brackets on their own and without brackets inside a set, so you can combine them freely.

| Member | On its own | Inside `Set(...)` | Matches |
|--------|------------|-------------------|---------|
| `Letter` | `[a-zA-Z]` | `a-zA-Z` | An ASCII letter. `é`, `ß` and `Ω` do not match. |
| `LowercaseLetter` | `[a-z]` | `a-z` | `a` to `z`. |
| `UppercaseLetter` | `[A-Z]` | `A-Z` | `A` to `Z`. |
| `HexDigit` | `[0-9a-fA-F]` | `0-9a-fA-F` | A hexadecimal digit in either case. |
| `AsciiDigit` | `[0-9]` | `0-9` | Only the ten ASCII digits. |

```csharp
Console.WriteLine(Pattern.With.Letter);                                                  // prints: [a-zA-Z]
Console.WriteLine(Pattern.With.LowercaseLetter.UppercaseLetter);                         // prints: [a-z][A-Z]
Console.WriteLine(Pattern.With.Set(Pattern.With.LowercaseLetter.UppercaseLetter));       // prints: [a-zA-Z]
Console.WriteLine(Pattern.With.Set(Pattern.With.HexDigit.Literal("-")));                 // prints: [0-9a-fA-F\-]
```

- Chained on their own, the members are two positions: `LowercaseLetter.UppercaseLetter` is a lowercase letter followed by an uppercase letter.
- Inside a set, the same chain is one position that accepts either.
- For letters in every script, use `Unicode(UnicodeGroup.Letter)` from [Unicode categories](#unicode-categories).

## Building a set

`Set(pattern)` matches one character from the members you chain inside it. Build the members with the same fluent API you use everywhere else: class members, ranges and literals.

```csharp
Pattern identifier = Pattern.With
    .StartOfLine
    .Set(Pattern.With.Letter.Literal("_"))
    .Set(Pattern.With.Letter.Digit.Literal("_")).Repeat.ZeroOrMore
    .EndOfLine;

Console.WriteLine(identifier);                    // prints: ^[a-zA-Z_][a-zA-Z\d_]*$
Console.WriteLine(identifier.IsMatch("_total2")); // True
Console.WriteLine(identifier.IsMatch("2total"));  // False
```

- The set as a whole is one element, so a quantifier after it repeats the set: `[a-zA-Z\d_]*`.
- Class members such as `Letter` drop their brackets inside the set. Shorthands such as `Digit` render the same.
- The order of the members does not change what the set matches.

### Literals inside a set

`Literal` always means "these exact characters". Inside a set, the library escapes every character that is special in a character class, including `-`. You cannot build a range by accident.

```csharp
Pattern userChars = Pattern.With.Set(Pattern.With.Letter.Digit.Literal("._-"));
Console.WriteLine(userChars);                    // prints: [a-zA-Z\d\._\-]

Pattern threeChars = Pattern.With.Set(Pattern.With.Literal("a-z"));
Console.WriteLine(threeChars);                   // prints: [a\-z]
Console.WriteLine(threeChars.IsMatch("-"));      // True
Console.WriteLine(threeChars.IsMatch("m"));      // False

Pattern specials = Pattern.With.Set(Pattern.With.Literal('$', '^', '\\', '\t'));
Console.WriteLine(specials);                     // prints: [\$\^\\\t]
```

How a literal character is written:

| Character | Outside a set | Inside a set |
|-----------|---------------|--------------|
| `^ $ . \| { } [ ] ( ) * + ? \` | Escaped with `\` | Escaped with `\` |
| `-` | Written as is | Escaped as `\-` |
| Tab, line feed, carriage return, form feed, vertical tab, escape | `\t`, `\n`, `\r`, `\f`, `\v`, `\e` | Same |
| Other control characters | `\uXXXX` | Same |
| Everything else | Written as is | Written as is |

- `Literal` has two overloads: `Literal(string)` and `Literal(params char[])`. Both throw `ArgumentNullException` for `null`.
- An escaped character that did not need escaping, such as `\.` inside a set, matches the same thing. The escaping only makes the output safe.

### Ranges

`Range(from, to)` matches one character between two characters, inclusive. On its own it renders `[a-f]`. Inside a set it renders `a-f`, so several ranges combine into one set.

```csharp
Console.WriteLine(Pattern.With.Range('a', 'f'));                                               // prints: [a-f]
Console.WriteLine(Pattern.With.Set(Pattern.With.Range('a', 'f').Range('0', '9').Literal("_"))); // prints: [a-f0-9_]

Pattern printable = Pattern.With.Range(' ', '~');
Console.WriteLine(printable);                    // prints: [\u0020-~]
Console.WriteLine(printable.IsMatch("~"));       // True
Console.WriteLine(printable.IsMatch("\t"));      // False

Pattern punctuation = Pattern.With.Range('-', '/');
Console.WriteLine(punctuation);                  // prints: [\u002D-/]
Console.WriteLine(punctuation.IsMatch("."));     // True

Console.WriteLine(Pattern.With.Range('а', 'я'));  // prints: [а-я]
```

- Ranges compare UTF-16 code units, not alphabet order. `Range('а', 'я')` covers the basic lowercase Cyrillic letters but not `ё` (U+0451), which sits outside the run U+0430 to U+044F.
- A boundary that is special in a class (`\`, `]`, `[`, `^`, `-`), a control character or a white-space character is written as `\uXXXX`, as the space in `[\u0020-~]` and the hyphen in `[\u002D-/]` show. .NET does not accept an escaped hyphen as the start of a range, and the `\u` form works on both sides.
- Any other boundary, including a non-ASCII letter, is written as the character itself.
- A range of one character, such as `Range('a', 'a')`, is allowed and renders `[a-a]`.

`Range` throws `ArgumentException` when `from` is greater than `to`:

```csharp
// from and to come from your application, for example 'z' and 'a'
try
{
    Pattern backwards = Pattern.With.Range(from, to);
}
catch (ArgumentException exception)
{
    Console.WriteLine(exception.Message);  // prints: The range start 'z' is greater than the range end 'a'. (Parameter 'from')
}
```

When the arguments are constants, the analyzer that ships with the package reports this at build time as error `FRX006`, before the code ever runs. See [Compile-Time Checks](./compile-time-checks.md).

### Reusing set members

A chain of members is an ordinary pattern, so you can keep it in a variable and use it in a set and in a negated set:

```csharp
Pattern identifierChars = Pattern.With.Letter.Digit.Literal("_");

Pattern identifierChar = Pattern.With.Set(identifierChars);
Pattern separator = Pattern.With.NegatedSet(identifierChars);

Console.WriteLine(identifierChar);   // prints: [a-zA-Z\d_]
Console.WriteLine(separator);        // prints: [^a-zA-Z\d_]
Console.WriteLine(identifierChars);  // prints: [a-zA-Z]\d_
```

- The last line shows why the members need a `Set`: on their own they are three positions in a row, not one.

## Character lists

`AnyOf(characters)` and `NoneOf(characters)` are shortcuts for the most common set: a fixed list of characters. Every character is taken literally, even `-`, `]`, `^` and `\`.

```csharp
Pattern operatorChar = Pattern.With.AnyOf("+-*/");
Console.WriteLine(operatorChar);               // prints: [\+\-\*/]

Pattern awkward = Pattern.With.AnyOf("^-]\\");
Console.WriteLine(awkward);                    // prints: [\^\-\]\\]
Console.WriteLine(awkward.IsMatch("]"));       // True

Pattern notQuote = Pattern.With.NoneOf("\"'");
Console.WriteLine(notQuote);                   // prints: [^"']
Console.WriteLine(notQuote.IsMatch("x"));      // True
```

| Member | Same as | Renders |
|--------|---------|---------|
| `AnyOf(characters)` | `Set(Pattern.With.Literal(characters))` | `[abc]` |
| `NoneOf(characters)` | `NegatedSet(Pattern.With.Literal(characters))` | `[^abc]` |

- Both throw `ArgumentException` ("At least one character is required.") for an empty string, and also for `null`. They do not throw `ArgumentNullException`.
- Duplicates are kept. `AnyOf("aa")` renders `[aa]`, which matches the same as `[a]`.
- Use `Set` instead when you need ranges or class members next to the listed characters.

## Excluding characters

`NegatedSet(pattern)` matches one character that is not in the set. It accepts the same members as `Set`.

```csharp
Pattern csvField = Pattern.With.NegatedSet(Pattern.With.Literal(",\"\r\n")).Repeat.OneOrMore;
Console.WriteLine(csvField);                               // prints: [^,"\r\n]+
Console.WriteLine(csvField.Match("Amman,Jordan").Value);   // prints: Amman

Console.WriteLine(Pattern.With.NegatedSet(Pattern.With.Digit));  // prints: [^\d]
Console.WriteLine(Pattern.With.NonDigit);                        // prints: \D
```

- `NegatedSet(Pattern.With.Digit)` and `NonDigit` match the same characters. Use the shorthand when one exists. Use `NegatedSet` when you exclude several things at once.
- "Everything up to the next delimiter", written as a negated set followed by the delimiter, is the safe alternative to `.*?`. A negated set cannot run past the delimiter, so the engine has nothing to backtrack into. See [ReDoS Safety](./redos-safety.md).

:::warning

A negated set matches line breaks. `.` does not.

```csharp
Console.WriteLine(Pattern.With.NoneOf("\"").IsMatch("\n"));   // True
Console.WriteLine(Pattern.With.Anything.IsMatch("\n"));       // False
```

Add `\r\n` to the excluded characters, as the CSV sample does, when a match must stay on one line.

:::

### Combining negated members

Inside one set, the members are joined with "or". Two negated shorthands in the same set therefore accept almost everything:

```csharp
Pattern everything = Pattern.With.Set(Pattern.With.NonDigit.NonWhitespace);
Console.WriteLine(everything);              // prints: [\D\S]
Console.WriteLine(everything.IsMatch("5")); // True (5 is not white space)

Pattern neither = Pattern.With.NegatedSet(Pattern.With.Digit.Whitespace);
Console.WriteLine(neither);                 // prints: [^\d\s]
Console.WriteLine(neither.IsMatch("5"));    // False
Console.WriteLine(neither.IsMatch("x"));    // True
```

To say "neither a digit nor white space", negate the whole set of positive members, as `neither` does.

## Unicode categories

Every character belongs to one Unicode general category, such as "uppercase letter" or "currency symbol". Match by category to accept text in any language.

```csharp
using System.Globalization;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.With.Unicode(UnicodeGroup.Letter));                     // prints: \p{L}
Console.WriteLine(Pattern.With.Unicode(UnicodeCategory.UppercaseLetter));         // prints: \p{Lu}
Console.WriteLine(Pattern.With.NotUnicode(UnicodeGroup.Number));                  // prints: \P{N}
Console.WriteLine(Pattern.With.NotUnicode(UnicodeCategory.DecimalDigitNumber));   // prints: \P{Nd}

Pattern word = Pattern.With.StartOfLine.Unicode(UnicodeGroup.Letter).Repeat.OneOrMore.EndOfLine;
Console.WriteLine(word.IsMatch("Ωμέγα"));    // True
Console.WriteLine(word.IsMatch("Привет"));   // True
Console.WriteLine(word.IsMatch("abc1"));     // False
```

| Member | Renders | Matches |
|--------|---------|---------|
| `Unicode(UnicodeGroup group)` | `\p{L}` | A character in a general group. |
| `NotUnicode(UnicodeGroup group)` | `\P{L}` | A character outside the group. |
| `Unicode(UnicodeCategory category)` | `\p{Lu}` | A character in one exact category. |
| `NotUnicode(UnicodeCategory category)` | `\P{Lu}` | A character outside the category. |

- `UnicodeGroup` is defined by this library, in the `Masterly.FluentRegex` namespace. `UnicodeCategory` is the standard `System.Globalization` enum.
- All four render the same inside and outside a set, so `Set(Pattern.With.Unicode(UnicodeGroup.Letter).Digit)` renders `[\p{L}\d]`.
- A value outside the enum, such as `(UnicodeCategory)99`, throws `ArgumentOutOfRangeException`.

The seven general groups:

| `UnicodeGroup` | Code | Contains |
|----------------|------|----------|
| `Letter` | `L` | Letters of every script. |
| `Mark` | `M` | Combining marks, such as accents and Arabic harakat. |
| `Number` | `N` | Decimal digits, letter numbers (`Ⅻ`) and other numbers (`½`). |
| `Punctuation` | `P` | Punctuation of every kind. |
| `Symbol` | `S` | Math, currency, modifier and other symbols. |
| `Separator` | `Z` | Space, line and paragraph separators. |
| `Other` | `C` | Control, format, surrogate, private-use and unassigned code points. |

The thirty categories, with the code each one renders:

| Group | `UnicodeCategory` values (code) |
|-------|-------------------------------|
| Letter | `UppercaseLetter` (`Lu`), `LowercaseLetter` (`Ll`), `TitlecaseLetter` (`Lt`), `ModifierLetter` (`Lm`), `OtherLetter` (`Lo`) |
| Mark | `NonSpacingMark` (`Mn`), `SpacingCombiningMark` (`Mc`), `EnclosingMark` (`Me`) |
| Number | `DecimalDigitNumber` (`Nd`), `LetterNumber` (`Nl`), `OtherNumber` (`No`) |
| Punctuation | `ConnectorPunctuation` (`Pc`), `DashPunctuation` (`Pd`), `OpenPunctuation` (`Ps`), `ClosePunctuation` (`Pe`), `InitialQuotePunctuation` (`Pi`), `FinalQuotePunctuation` (`Pf`), `OtherPunctuation` (`Po`) |
| Symbol | `MathSymbol` (`Sm`), `CurrencySymbol` (`Sc`), `ModifierSymbol` (`Sk`), `OtherSymbol` (`So`) |
| Separator | `SpaceSeparator` (`Zs`), `LineSeparator` (`Zl`), `ParagraphSeparator` (`Zp`) |
| Other | `Control` (`Cc`), `Format` (`Cf`), `Surrogate` (`Cs`), `PrivateUse` (`Co`), `OtherNotAssigned` (`Cn`) |

Categories combine with literals in a set. This pattern accepts personal names in any script, with spaces, apostrophes and hyphens:

```csharp
Pattern personName = Pattern.With
    .StartOfLine
    .Set(Pattern.With.Unicode(UnicodeGroup.Letter).Unicode(UnicodeGroup.Mark).Literal(" '-")).Repeat.OneOrMore
    .EndOfLine;

Console.WriteLine(personName);                               // prints: ^[\p{L}\p{M} '\-]+$
Console.WriteLine(personName.IsMatch("Zoë O'Neil-Ávila"));   // True
Console.WriteLine(personName.IsMatch("محمد"));               // True
Console.WriteLine(personName.IsMatch("R2-D2"));              // False
```

- Include `Mark` next to `Letter` when the text may be in decomposed form (a base letter followed by a separate accent), or when it can carry vowel marks, as Arabic and Hebrew can.

## Unicode blocks

A block is a fixed range of code points, such as Greek (U+0370 to U+03FF). `UnicodeBlock(blockName)` matches one character in a named block.

```csharp
Console.WriteLine(Pattern.With.UnicodeBlock("Greek"));                         // prints: \p{IsGreek}
Console.WriteLine(Pattern.With.UnicodeBlock("IsCyrillic"));                    // prints: \p{IsCyrillic}
Console.WriteLine(Pattern.With.UnicodeBlock("Greek").IsMatch("λ"));            // True
Console.WriteLine(Pattern.With.UnicodeBlock("CJKUnifiedIdeographs").IsMatch("漢")); // True

Pattern kana = Pattern.With.Set(Pattern.With.UnicodeBlock("Hiragana").UnicodeBlock("Katakana"));
Console.WriteLine(kana);                                                       // prints: [\p{IsHiragana}\p{IsKatakana}]
```

- Pass the .NET block name with or without the `Is` prefix. The library adds `Is` when the name does not start with it.
- The prefix check is case-sensitive. `UnicodeBlock("isGreek")` renders `\p{IsisGreek}`, which is not a valid block. Write `Greek` or `IsGreek`.
- The name may contain only letters, digits, `-` and `_`. Anything else, such as a space, throws `ArgumentException` ("Invalid Unicode block name 'Greek Extended'."). An empty or `null` name throws `ArgumentException` ("A block name is required.").
- The builder does not know the list of block names. A well-formed but unknown name is accepted, and the regex fails when it is first used:

```csharp
// blockName comes from your application, for example "Klingon"
Pattern unknown = Pattern.With.UnicodeBlock(blockName);

Console.WriteLine(unknown);            // prints: \p{IsKlingon}
Console.WriteLine(unknown.IsValid());  // False
unknown.IsMatch("a");                  // throws RegexParseException, which derives from ArgumentException
```

When the name is a constant, the bundled analyzer reports an unknown block at build time as error `FRX003`.

:::tip

A block is not the same as a script. The Greek block also holds Coptic letters such as `Ϣ`, and the accented polytonic Greek letters such as `ἀ` live in the separate Greek Extended block, so `UnicodeBlock("Greek")` matches `Ϣ` but not `ἀ`. To match the letters or digits of a writing system, use `Script`, `LetterIn` and `DigitIn`. They work inside a set too. See [Unicode](./unicode.md).

:::

## Digits that are ASCII only

`AsciiDigit` and the script digit members also work inside a set, so you can accept exactly the digits a form allows:

```csharp
using Masterly.FluentRegex.Unicode;

Pattern anyDigit = Pattern.With.Set(Pattern.With.AsciiDigit.DigitIn(UnicodeScript.Arabic));
Console.WriteLine(anyDigit);                // prints: [0-9\u0660-\u0669\u06F0-\u06F9]
Console.WriteLine(anyDigit.IsMatch("٧"));   // True
Console.WriteLine(anyDigit.IsMatch("३"));   // False

Pattern amount = Pattern.With.Set(Pattern.With.Digit.Literal(".,")).Repeat.OneOrMore;
Console.WriteLine(amount.WithAsciiDigits()); // prints: [0-9\.,]+
```

- `DigitIn(UnicodeScript.Arabic)` adds the Arabic-Indic (U+0660 to U+0669) and Extended Arabic-Indic (U+06F0 to U+06F9) digits, written as `\u` escapes.
- `WithAsciiDigits()` rewrites `\d` and `\D` everywhere in a finished pattern, including inside sets and negated sets. It leaves `\w` and `\p{Nd}` as written. When there is nothing to rewrite, it returns the same pattern.
- `UnicodeScript` is in the `Masterly.FluentRegex.Unicode` namespace.

When the digit meaning is a setting rather than a fixed rule, pass a `DigitMode` to `WithDigits`:

```csharp
Pattern year = Pattern.With.StartOfLine.Digit.Repeat.Times(4).EndOfLine;

Console.WriteLine(year.IsMatch("٢٠٢٦"));                              // True
Console.WriteLine(year.WithDigits(DigitMode.Ascii));                   // prints: ^[0-9]{4}$
Console.WriteLine(year.WithDigits(DigitMode.Ascii).IsMatch("٢٠٢٦"));  // False
Console.WriteLine(year.WithDigits(DigitMode.Unicode));                 // prints: ^\d{4}$
```

| `DigitMode` | `WithDigits` returns |
|-------------|----------------------|
| `Unicode` | The pattern unchanged. `\d` keeps its .NET meaning. |
| `Ascii` | The same result as `WithAsciiDigits()`. |

- An undefined value, such as `(DigitMode)5`, throws `ArgumentOutOfRangeException`.
- `DigitMode` is in the `Masterly.FluentRegex` namespace. Many members of [Common Patterns](./common-patterns.md) accept it too.

## Case-insensitive classes

A range or a set is case-sensitive by default. Turn on `RegexOptions.IgnoreCase` for the whole pattern, or scope it to part of the pattern with an inline option.

```csharp
using System.Text.RegularExpressions;

Pattern hexWord = Pattern.With.StartOfLine.Range('a', 'f').Repeat.OneOrMore.EndOfLine
    .WithOptions(RegexOptions.IgnoreCase);
Console.WriteLine(hexWord.IsMatch("BEEF"));      // True

Pattern id = Pattern.With.Literal("id:").IgnoreCase(Pattern.With.Range('a', 'f').Repeat.OneOrMore);
Console.WriteLine(id);                           // prints: id:(?i:[a-f]+)
Console.WriteLine(id.IsMatch("id:CAFE"));        // True
Console.WriteLine(id.IsMatch("ID:CAFE"));        // False

Pattern code = Pattern.With.StartOfLine.Letter.Repeat.Times(2).CaseSensitive(Pattern.With.UppercaseLetter).EndOfLine
    .WithOptions(RegexOptions.IgnoreCase);
Console.WriteLine(code);                         // prints: ^[a-zA-Z]{2}(?-i:[A-Z])$
Console.WriteLine(code.IsMatch("abC"));          // True
Console.WriteLine(code.IsMatch("abc"));          // False
```

| Member | Renders | Effect |
|--------|---------|--------|
| `IgnoreCase(pattern)` | `(?i:...)` | The inner pattern ignores case, whatever the outer options. |
| `CaseSensitive(pattern)` | `(?-i:...)` | The inner pattern respects case, even when the whole pattern uses `IgnoreCase`. |
| `InlineOptions(enable, pattern, disable)` | `(?is-m:...)` | Turns any inline-capable options on and off for the inner pattern. `disable` defaults to `RegexOptions.None`. |

- `InlineOptions` accepts only `IgnoreCase`, `Multiline`, `ExplicitCapture`, `Singleline` and `IgnorePatternWhitespace`. Any other flag, such as `Compiled`, throws `ArgumentException`.
- `Pattern.With.InlineOptions(RegexOptions.IgnoreCase | RegexOptions.Singleline, Pattern.With.Anything, RegexOptions.Multiline)` renders `(?is-m:.)`.
- The inline scopes are non-capturing groups. They do not change group numbers.

:::note

With `IgnoreCase`, .NET also folds a few non-ASCII characters onto ASCII letters. For example, `[a-z]` with `IgnoreCase` matches the Kelvin sign `K` (U+212A), with or without `CultureInvariant`. If only ASCII may pass, list both cases explicitly (`Letter`) and keep the pattern case-sensitive.

:::

## One character or one of several words

A set picks one character. To pick one of several longer alternatives, use alternation. The two overlap when every alternative is a single character:

```csharp
Pattern separatorChar = Pattern.With.AnyOf("-/.");
Pattern separatorChoice = Pattern.With.Choice(
    Pattern.With.Literal("-"),
    Pattern.With.Literal("/"),
    Pattern.With.Literal("."));

Console.WriteLine(separatorChar);                                // prints: [\-/\.]
Console.WriteLine(separatorChoice);                              // prints: (-|/|\.)
Console.WriteLine(separatorChar.IsEquivalentTo(separatorChoice)); // True
Console.WriteLine(separatorChoice.GetGroupCount());              // 1
Console.WriteLine(separatorChar.GetGroupCount());                // 0

Console.WriteLine(Pattern.With.OneOf("jpg", "png", "gif"));                                          // prints: (?:jpg|png|gif)
Console.WriteLine(Pattern.With.Literal("cat") | Pattern.With.Literal("dog") | Pattern.With.Literal("bird")); // prints: (?:cat|dog|bird)
```

| You need | Use | Renders | Captures |
|----------|-----|---------|----------|
| One character from a list | `AnyOf`, `Set` | `[\-/\.]` | No |
| One of several words or sub-patterns | `OneOf(...)`, the `\|` operator | `(?:jpg\|png)` | No |
| One of several alternatives, captured | `Choice(first, second, ...)` | `(yes\|no)` | Yes, as a numbered group |

- `IsEquivalentTo` proves the set and the alternation accept exactly the same strings. See [Pattern Algebra](./pattern-algebra.md).
- Prefer a set when every alternative is one character. The engine tests a set in one step. It tries alternation branches one after another.
- `Choice` needs at least two alternatives. `OneOf` needs at least one and throws `ArgumentException` for none. `OneOf(params string[])` escapes each word and throws `ArgumentNullException` for a `null` word.
- More on alternation and the `+` and `|` operators: [Composing Patterns](./composing-patterns.md). More on numbered groups: [Groups and Backreferences](./groups-and-backreferences.md).

## What does not belong in a set

A set renders every member in "class" context, where only single characters and class items have meaning. Members that are not single characters do not fail. They render their text inside the brackets, and the brackets give it a different meaning:

```csharp
Console.WriteLine(Pattern.With.Set(Pattern.With.Anything));                        // prints: [.]
Console.WriteLine(Pattern.With.Set(Pattern.With.Digit.Repeat.OneOrMore));          // prints: [\d+]
Console.WriteLine(Pattern.With.Set(Pattern.With.Group(Pattern.With.Literal("ab")))); // prints: [(ab)]
Console.WriteLine(Pattern.With.Set(Pattern.With.StartOfLine.Digit));               // prints: [^\d]
Console.WriteLine(Pattern.With.Set(Pattern.With.Set(Pattern.With.Digit)));         // prints: [[\d]]
```

| You write | Renders | What it really matches |
|-----------|---------|------------------------|
| `Set(Anything)` | `[.]` | Only a dot. `IsMatch("x")` is `False`. |
| `Set(Digit.Repeat.OneOrMore)` | `[\d+]` | One digit or a plus sign. `IsMatch("+")` is `True`. |
| `Set(Group(...))`, `Set(OneOf(...))` | `[(ab)]`, `[(?:ab\|cd)]` | One of the bracket, letter and symbol characters. |
| `Set(StartOfLine.Digit)` | `[^\d]` | Any character except a digit: the anchor turned the set into a negated set. `IsMatch("7")` is `False`. |
| `Set(Set(Digit))` | `[[\d]]` | `[` or a digit, followed by a literal `]`. `IsMatch("7")` is `False`. |
| `Set(Pattern.With)` | `[]` | Nothing. The regex is invalid. |

Put quantifiers, groups and anchors outside the set. Put the members of a nested set straight into the outer one.

Two members refuse to render inside a set, because they can match more than one UTF-16 code unit: `CodePoint` and `Grapheme`. Building the set works. Rendering it (`ToString`, `IsMatch` and every other use) throws `InvalidOperationException`:

```csharp
Pattern emojiOrLetter = Pattern.With.Set(Pattern.With.CodePoint.Letter);
try
{
    Console.WriteLine(emojiOrLetter);
}
catch (InvalidOperationException exception)
{
    Console.WriteLine(exception.Message);
    // prints: CodePoint can match more than one UTF-16 code unit, so it cannot be used inside Set(...) or NegatedSet(...). Combine it with the | operator or OneOf(...) instead.
}

Pattern fixedPattern = Pattern.With.CodePoint | Pattern.With.Letter;
Console.WriteLine(fixedPattern.IsMatch("😀"));   // True
```

An empty set, `Set(Pattern.With)` or `NegatedSet(Pattern.With)`, builds without an error and renders `[]` or `[^]`. Neither is a valid .NET regex:

```csharp
// members comes from your application and turned out to be empty (Pattern.With)
Pattern empty = Pattern.With.Set(members);

Console.WriteLine(empty);                               // prints: []
Console.WriteLine(empty.IsValid());                     // False
Console.WriteLine(empty.TryValidate(out string error)); // False
Console.WriteLine(error);                               // prints: Invalid pattern '[]' at offset 2. Unterminated [] set.
```

- `Set(null)` and `NegatedSet(null)` throw `ArgumentNullException`.
- With constant arguments, the analyzer reports an empty set as `FRX003` at build time. See [Validation and Debugging](./validation-and-debugging.md) for checking patterns at run time.

## Class subtraction and other raw syntax

There is no fluent member for .NET class subtraction (`[a-z-[aeiou]]`). `RegEx(text)` inserts its text unescaped, also inside a set, so you can write the subtraction yourself:

```csharp
Pattern consonant = Pattern.With.Set(Pattern.With.RegEx("a-z-[aeiou]"));
Console.WriteLine(consonant);                 // prints: [a-z-[aeiou]]
Console.WriteLine(consonant.IsMatch("b"));    // True
Console.WriteLine(consonant.IsMatch("e"));    // False

Pattern computed = Pattern.With.LowercaseLetter.Except(Pattern.With.AnyOf("aeiou"));
Console.WriteLine(computed);                         // prints: ^[b-df-hj-np-tv-z]\z
Console.WriteLine(computed.IsEquivalentTo(consonant)); // True
```

- Raw text is not escaped or checked when you add it. A typo shows up when the regex is first used, or as `FRX003` at build time when the text is a constant.
- `Except` from [Pattern Algebra](./pattern-algebra.md) computes the remaining characters as explicit ranges. It returns a whole-string pattern, anchored with `^` and `\z`, so use it to work out a set, not as a piece in the middle of a longer pattern.

## Checking what a class matches

`Explain()` lists each part of a pattern in plain words, including sets and negated sets:

```csharp
Pattern sku = Pattern.With
    .StartOfLine
    .Set(Pattern.With.Range('a', 'f').Digit).Repeat.OneOrMore
    .NegatedSet(Pattern.With.Literal("-"))
    .EndOfLine;

Console.WriteLine(sku.Explain());
// prints:
// Pattern: ^[a-f\d]+[^\-]$
//
// Explanation:
//   - Start of line/string
//   - Any character in: [a-f\d]
//   - One or more
//   - Any character NOT in: [\-]
//   - End of line/string
```

- To see a match step by step, see [Match Tracing](./match-tracing.md). To see the pattern as a railroad diagram, see [Diagrams](./diagrams.md).
- To generate strings that a class accepts and rejects, for tests, see [Test Data Generation](./test-data-generation.md).

## Member reference

| Member | On its own | Inside `Set(...)` | Throws |
|--------|------------|-------------------|--------|
| `Set(pattern)` | `[...]` | Not meant to be nested | `ArgumentNullException` for `null` |
| `NegatedSet(pattern)` | `[^...]` | Not meant to be nested | `ArgumentNullException` for `null` |
| `AnyOf(characters)` | `[abc]` | Not meant to be nested | `ArgumentException` for `null` or empty |
| `NoneOf(characters)` | `[^abc]` | Not meant to be nested | `ArgumentException` for `null` or empty |
| `Range(from, to)` | `[a-f]` | `a-f` | `ArgumentException` when `from > to` |
| `Letter` | `[a-zA-Z]` | `a-zA-Z` | |
| `LowercaseLetter` | `[a-z]` | `a-z` | |
| `UppercaseLetter` | `[A-Z]` | `A-Z` | |
| `HexDigit` | `[0-9a-fA-F]` | `0-9a-fA-F` | |
| `AsciiDigit` | `[0-9]` | `0-9` | |
| `Digit`, `NonDigit` | `\d`, `\D` | Same | |
| `Word`, `NonWord` | `\w`, `\W` | Same | |
| `Whitespace`, `NonWhitespace` | `\s`, `\S` | Same | |
| `Tab`, `Newline`, `CarriageReturn` | `\t`, `\n`, `\r` | Same | |
| `Unicode(group)`, `NotUnicode(group)` | `\p{L}`, `\P{L}` | Same | `ArgumentOutOfRangeException` for an undefined value |
| `Unicode(category)`, `NotUnicode(category)` | `\p{Lu}`, `\P{Lu}` | Same | `ArgumentOutOfRangeException` for an undefined value |
| `UnicodeBlock(blockName)` | `\p{IsGreek}` | Same | `ArgumentException` for an empty or malformed name |
| `Script`, `LetterIn`, `DigitIn` | A class built from the script's blocks | The same class without brackets | See [Unicode](./unicode.md) |
| `WithAsciiDigits()`, `WithDigits(mode)` | Rewrites `\d`, `\D` in the finished pattern | Rewrites them inside sets too | `ArgumentOutOfRangeException` for an undefined `DigitMode` |
| `Literal(text)`, `Literal(chars)` | Escaped text | Escaped characters, `-` included | `ArgumentNullException` for `null` |
| `RegEx(text)` | Raw text | Raw text | |
| `CodePoint`, `Grapheme` | `(?:...)` | Not allowed | `InvalidOperationException` when rendered in a set |

The anchors `StartOfInput` (`\A`), `EndOfInput` (`\z`) and `NonWordBoundary` (`\B`) are declared next to the class members in the source, but they match positions, not characters. They are covered in [Assertions](./assertions.md) and [Basic Patterns](./basic-patterns.md).
