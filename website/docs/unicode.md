---
id: unicode
title: "Unicode and Scripts"
description: "Match by writing system, use one script's digits, count code points and graphemes, and match Arabic or accented text with or without diacritics."
---

.NET strings are UTF-16, and most of the world's text is not ASCII. Masterly.FluentRegex gives you building blocks that understand this: match the characters of a writing system such as Arabic or Greek, accept only the digits of one script, count emoji and user-perceived characters correctly, and match a name whether or not it carries diacritics such as Arabic harakat or French accents.

Use this page when you need to:

- accept or find text in one writing system, such as Arabic, Cyrillic or Han,
- decide which digits count: every Unicode digit, only `0-9`, or one script's own digits,
- match Unicode categories (uppercase letters, marks, currency symbols) and named blocks,
- count or limit text by code points or by user-perceived characters instead of UTF-16 code units,
- search for words without caring about diacritics: `محمد` and `مُحَمَّد`, `cafe` and `café`.

Every member on this page is on `Pattern` and returns a new pattern. The enums `UnicodeScript`, `UnicodeGroup` and `DigitMode` live in the `Masterly.FluentRegex` namespace. `UnicodeCategory` is the standard `System.Globalization.UnicodeCategory`. Everything works on all targets the package supports (.NET Standard 2.1, .NET 8 and .NET 10). The only exception is `UseNonBacktracking()`, mentioned in [Options, timeout and engines](#options-timeout-and-engines), which needs .NET 8 or later.

| Member | Matches | Allowed inside `Set(...)` |
|--------|---------|---------------------------|
| `Script(UnicodeScript)` | Any character of a script's BMP blocks, including its marks. | Yes |
| `LetterIn(UnicodeScript)` | Only the letters (category `L`) of a script. | Yes |
| `DigitIn(UnicodeScript)` | The script's own decimal digits. | Yes |
| `AsciiDigit` | `[0-9]` only. | Yes (renders `0-9`) |
| `Unicode(UnicodeCategory)`, `NotUnicode(UnicodeCategory)` | A character in (or not in) one category, such as `\p{Lu}`. | Yes |
| `Unicode(UnicodeGroup)`, `NotUnicode(UnicodeGroup)` | A character in (or not in) a general group, such as `\p{L}`. | Yes |
| `UnicodeBlock(string)` | A character in a named .NET block, such as `\p{IsGreek}`. | Yes |
| `CodePoint` | One Unicode scalar value, so an emoji counts once. | No |
| `Grapheme` | One user-perceived character (an approximation of a grapheme cluster). | No |
| `WithAsciiDigits()`, `WithDigits(DigitMode)` | Rewrites `\d` and `\D` in the whole pattern to mean `0-9`. | n/a |
| `IgnoreDiacritics()` | Rewrites the whole pattern to match with or without diacritics. | n/a |

## Matching a writing system

### Any character of a script

`Script(UnicodeScript)` matches one character of a writing system. Repeat it to match a word or a run of text in that script.

```csharp
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern greek = Pattern.With.Script(UnicodeScript.Greek).Repeat.OneOrMore;
Pattern arabic = Pattern.With.Script(UnicodeScript.Arabic).Repeat.OneOrMore;

string greekWord = greek.Match("abc αβγ def").Value;       // αβγ
string arabicWord = arabic.Match("name: مُحَمَّد!").Value;  // مُحَمَّد

Console.WriteLine(Pattern.With.Script(UnicodeScript.Arabic));
// prints: [\u0600-\u06FF\u0750-\u077F\u0870-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]
```

- `Script` renders a character class built from the **Basic Multilingual Plane (BMP) blocks** of the script. Non-ASCII characters are written as `\uXXXX` escapes, which are valid in every regex mode.
- Because it uses whole blocks, it also matches the combining marks and the few punctuation characters inside those blocks. For Arabic that means the harakat (so `مُحَمَّد` is one run), the Arabic comma `،`, the tatweel `ـ` and the Arabic-Indic digits `٠-٩`.
- The class for each script is built once and cached, so calling `Script` repeatedly is cheap.

```csharp
Pattern oneArabic = Pattern.With.StartOfInput.Script(UnicodeScript.Arabic).EndOfInput;

bool comma = oneArabic.IsMatch("،");    // true: the Arabic comma is in the Arabic block
bool tatweel = oneArabic.IsMatch("ـ");  // true
bool digit = oneArabic.IsMatch("٣");    // true: Arabic-Indic digits are in the block too
bool fatha = oneArabic.IsMatch("\u064E");    // true: a haraka (combining mark)
```

Use `Script` when diacritics and digits belong to the text you want, for example to pull a whole Arabic phrase out of mixed text.

### Only the letters of a script

`LetterIn(UnicodeScript)` keeps only the letters (Unicode category `L`) of the script. Combining marks, digits and punctuation are excluded.

```csharp
Pattern letters = Pattern.With.LetterIn(UnicodeScript.Arabic).Repeat.OneOrMore;
Pattern words = Pattern.With.Script(UnicodeScript.Arabic).Repeat.OneOrMore;

int letterRuns = letters.Matches("مُحَمَّد ٢٠٢٦").Count;  // 4: the harakat split the letters apart
int wordRuns = words.Matches("مُحَمَّد ٢٠٢٦").Count;      // 2: the name and the number

Console.WriteLine(Pattern.With.LetterIn(UnicodeScript.Arabic));
// prints: [\u0600-\u06FF\u0750-\u077F\u0870-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF-[\P{L}]]
```

- On its own, `LetterIn` renders a .NET **class subtraction**: the script's blocks minus everything that is not a letter (`-[\P{L}]`).
- Inside `Set(...)` a subtraction is not allowed, so `LetterIn` renders the equivalent list of letter ranges instead. Both forms match exactly the same characters.
- Use `LetterIn` when you need bare letters, for example to validate a name typed without diacritics. Use `Script` when the marks are part of the word.

### Supported scripts

`UnicodeScript` has 27 values. The second column lists the BMP ranges that `Script` and `LetterIn` use. The third column says whether `DigitIn` works for the script.

| `UnicodeScript` | BMP ranges | Own digits for `DigitIn` |
|-----------------|-----------|--------------------------|
| `Latin` | `A-Z`, `a-z`, `ª`, `º`, `U+00C0-02AF` except `×` and `÷`, combining diacritics `U+0300-036F`, `U+1D00-1DBF`, `U+1E00-1EFF`, `U+2C60-2C7F`, `U+A720-A7FF`, `U+AB30-AB6F`, ligatures `U+FB00-FB06`, fullwidth `Ａ-Ｚ` and `ａ-ｚ` | `0-9` |
| `Greek` | Combining diacritics `U+0300-036F`, `U+0370-03E1`, `U+03F0-03FF`, Greek Extended `U+1F00-1FFF` (the Coptic letters `U+03E2-03EF` are excluded) | No |
| `Cyrillic` | Combining diacritics, `U+0400-052F`, `U+1C80-1C8F`, `U+2DE0-2DFF`, `U+A640-A69F` | No |
| `Armenian` | `U+0530-058F`, ligatures `U+FB13-FB17` | No |
| `Hebrew` | `U+0590-05FF`, presentation forms `U+FB1D-FB4F` | No |
| `Arabic` | `U+0600-06FF`, `U+0750-077F`, `U+0870-08FF`, presentation forms `U+FB50-FDFF` and `U+FE70-FEFF` | `٠-٩` and `۰-۹` |
| `Syriac` | `U+0700-074F`, `U+0860-086F` | No |
| `Thaana` | `U+0780-07BF` | No |
| `Devanagari` | `U+0900-097F`, `U+A8E0-A8FF` | `०-९` |
| `Bengali` | `U+0980-09FF` | `০-৯` |
| `Gurmukhi` | `U+0A00-0A7F` | `੦-੯` |
| `Gujarati` | `U+0A80-0AFF` | `૦-૯` |
| `Tamil` | `U+0B80-0BFF` | `௦-௯` |
| `Telugu` | `U+0C00-0C7F` | `౦-౯` |
| `Kannada` | `U+0C80-0CFF` | `೦-೯` |
| `Malayalam` | `U+0D00-0D7F` | `൦-൯` |
| `Thai` | `U+0E00-0E7F` | `๐-๙` |
| `Lao` | `U+0E80-0EFF` | `໐-໙` |
| `Tibetan` | `U+0F00-0FFF` | `༠-༩` |
| `Georgian` | `U+10A0-10FF`, `U+1C90-1CBF`, `U+2D00-2D2F` | No |
| `Hangul` | Jamo `U+1100-11FF`, compatibility Jamo `U+3130-318F`, `U+A960-A97F`, syllables and Jamo Extended-B `U+AC00-D7FF`, halfwidth `U+FFA0-FFDF` | No |
| `Ethiopic` | `U+1200-139F`, `U+2D80-2DDF`, `U+AB00-AB2F` | No |
| `Khmer` | `U+1780-17FF`, Khmer Symbols `U+19E0-19FF` | `០-៩` |
| `Mongolian` | `U+1800-18AF` | `᠐-᠙` |
| `Hiragana` | `U+3040-309F` | No |
| `Katakana` | `U+30A0-30FF`, `U+31F0-31FF`, halfwidth `U+FF66-FF9F` | No |
| `Han` | Radicals `U+2E80-2FDF`, `々`, `〇`, `〡-〩`, `〸-〻`, Extension A `U+3400-4DBF`, `U+4E00-9FFF`, compatibility `U+F900-FAFF` | No |

### Combining scripts in a set

`Script`, `LetterIn` and `DigitIn` all work inside `Set(...)` and `NegatedSet(...)`, so you can mix them with each other and with literal characters.

```csharp
Pattern nameChar = Pattern.With.Set(
    Pattern.With.Script(UnicodeScript.Latin).Script(UnicodeScript.Arabic).Literal(" '-"));
Pattern name = Pattern.With.StartOfInput.Then(nameChar.Repeat.OneOrMore).EndOfInput;

bool french = name.IsMatch("Anne-Marie O'Neil");   // true
bool arabicName = name.IsMatch("محمد بن سلمان");   // true
bool cyrillic = name.IsMatch("Иван");              // false: Cyrillic is not in the set
bool withDigits = name.IsMatch("R2-D2");           // false: Latin does not include 0-9

Pattern notArabic = Pattern.With.NegatedSet(Pattern.With.Script(UnicodeScript.Arabic)).Repeat.OneOrMore;
string rest = notArabic.Match("مرحبا hello").Value;   // " hello" (with the leading space)
```

- `Literal(" '-")` inside a set adds the space, the apostrophe and the hyphen. They are escaped for you.
- Add `AsciiDigit` or `DigitIn(...)` to the set when digits are allowed.

### What a script does not include

:::warning

`Script` and `LetterIn` cover the **BMP only** (code points up to `U+FFFF`). Characters above that, such as CJK Extension B ideographs, historic scripts and most emoji, are never matched. `Pattern.With.Script(UnicodeScript.Han).Count("𠀀")` returns `0`.

:::

- `Latin` contains letters only. ASCII digits, spaces and punctuation are not Latin characters. Fullwidth letters such as `Ａ` are included.
- The Combining Diacritical Marks block (`U+0300-036F`) belongs to `Latin`, `Greek` and `Cyrillic` at the same time, so a lone combining accent matches all three.
- `Greek` excludes the Coptic letters of the "Greek and Coptic" block. The .NET block `\p{IsGreek}` includes them (see [Script, block or category](#script-block-or-category)).

## Unicode categories and blocks

Scripts answer "which writing system?". Categories answer "what kind of character?": an uppercase letter, a mark, a currency symbol. .NET regex supports both through `\p{...}`, and the fluent API wraps them so you do not have to remember the codes.

### Categories

`Unicode(UnicodeCategory)` matches one character of a category. `NotUnicode(UnicodeCategory)` matches any character outside it.

```csharp
Pattern upper = Pattern.With.Unicode(UnicodeCategory.UppercaseLetter);
Pattern currency = Pattern.With.Unicode(UnicodeCategory.CurrencySymbol);

Console.WriteLine(upper);                        // \p{Lu}
Console.WriteLine(Pattern.With.NotUnicode(UnicodeCategory.DecimalDigitNumber)); // \P{Nd}

int capitals = upper.Count("Ünïcode Ωmega");     // 2: Ü and Ω
int prices = currency.Count("5 € + 3 $ = ©");    // 2: € and $
```

Each `UnicodeCategory` value maps to a two-letter code:

| Letters | Marks | Numbers | Separators | Punctuation | Symbols | Other |
|---------|-------|---------|------------|-------------|---------|-------|
| `UppercaseLetter` `Lu` | `NonSpacingMark` `Mn` | `DecimalDigitNumber` `Nd` | `SpaceSeparator` `Zs` | `ConnectorPunctuation` `Pc` | `MathSymbol` `Sm` | `Control` `Cc` |
| `LowercaseLetter` `Ll` | `SpacingCombiningMark` `Mc` | `LetterNumber` `Nl` | `LineSeparator` `Zl` | `DashPunctuation` `Pd` | `CurrencySymbol` `Sc` | `Format` `Cf` |
| `TitlecaseLetter` `Lt` | `EnclosingMark` `Me` | `OtherNumber` `No` | `ParagraphSeparator` `Zp` | `OpenPunctuation` `Ps` | `ModifierSymbol` `Sk` | `Surrogate` `Cs` |
| `ModifierLetter` `Lm` | | | | `ClosePunctuation` `Pe` | `OtherSymbol` `So` | `PrivateUse` `Co` |
| `OtherLetter` `Lo` | | | | `InitialQuotePunctuation` `Pi` | | `OtherNotAssigned` `Cn` |
| | | | | `FinalQuotePunctuation` `Pf` | | |
| | | | | `OtherPunctuation` `Po` | | |

A value outside the enum throws `ArgumentOutOfRangeException`.

### General groups

`Unicode(UnicodeGroup)` and `NotUnicode(UnicodeGroup)` match a whole family of categories with one letter.

| `UnicodeGroup` | Regex | Covers |
|----------------|-------|--------|
| `Letter` | `\p{L}` | Any letter in any script. |
| `Mark` | `\p{M}` | Any combining mark: harakat, accents, Indic vowel signs. |
| `Number` | `\p{N}` | Decimal digits, letter numbers (Roman numerals) and other numbers (`½`, `²`). |
| `Punctuation` | `\p{P}` | Any punctuation. |
| `Symbol` | `\p{S}` | Math, currency, modifier and other symbols. |
| `Separator` | `\p{Z}` | Space, line and paragraph separators. |
| `Other` | `\p{C}` | Control, format, surrogate, private use and unassigned characters. |

Pair `Letter` with `Mark` to match whole words in scripts that write vowels as marks:

```csharp
Pattern lettersOnly = Pattern.With.Unicode(UnicodeGroup.Letter).Repeat.OneOrMore;
Pattern lettersAndMarks = Pattern.With.Set(
    Pattern.With.Unicode(UnicodeGroup.Letter).Unicode(UnicodeGroup.Mark)).Repeat.OneOrMore;

Console.WriteLine(lettersAndMarks);                             // [\p{L}\p{M}]+

string cut = lettersOnly.Match("  नमस्ते!").Value;              // नमस: stops at the first vowel sign
string whole = lettersAndMarks.Match("  नमस्ते!").Value;        // नमस्ते

int symbols = Pattern.With.Unicode(UnicodeGroup.Symbol).Count("5 € + 3 $ = ©");   // 5: € + $ = ©
```

### Named blocks

`UnicodeBlock(string)` matches a character in a named .NET Unicode block. Pass the .NET block name with or without the `Is` prefix.

```csharp
Console.WriteLine(Pattern.With.UnicodeBlock("Greek"));        // \p{IsGreek}
Console.WriteLine(Pattern.With.UnicodeBlock("IsCyrillic"));   // \p{IsCyrillic}

bool coptic = Pattern.With.UnicodeBlock("GreekandCoptic").IsMatch("ϣ");   // true
```

- The name may contain only letters, digits, `-` and `_`. Anything else, such as spaces, throws `ArgumentException` ("Invalid Unicode block name ...") when you call the method. A null or empty name throws `ArgumentException` too.
- The method does not check that the block exists. An unknown name such as `"Klingon"` builds fine, then throws `RegexParseException` ("Unknown property 'IsKlingon'") the first time the pattern runs.
- With the [compile-time checks](./compile-time-checks.md) analyzer installed, both mistakes are reported at build time when the name is a literal: `FRX006` for a name that throws while building, `FRX003` for a name the .NET regex parser rejects.

### Script, block or category

The three tools overlap. Pick by the question you are asking:

| Tool | Answers | Example | Notes |
|------|---------|---------|-------|
| `Script(...)` | Is it written in this writing system? | `Script(UnicodeScript.Arabic)` | Joins several blocks (Arabic has five). Includes marks and the script's punctuation. |
| `LetterIn(...)` | Is it a letter of this writing system? | `LetterIn(UnicodeScript.Greek)` | Script blocks minus non-letters. |
| `UnicodeBlock(...)` | Is it inside this one .NET block? | `UnicodeBlock("Arabic")` | One block only. `\p{IsGreek}` includes the Coptic letters that `Script(UnicodeScript.Greek)` leaves out. |
| `Unicode(...)` | What kind of character is it? | `Unicode(UnicodeGroup.Letter)` | Across all scripts. Like every .NET class it tests one UTF-16 code unit, so a letter above `U+FFFF` such as `𝒜` is seen as two surrogates (`\p{Cs}`), not as `\p{L}`. |

## Digits in every script

### `\d` matches every decimal digit

In .NET, `\d` (`Pattern.With.Digit`) matches **every** Unicode decimal digit (category `Nd`), not only `0-9`. That is right for free text in any language, but often wrong for formats that are defined over ASCII, such as IP addresses, card numbers or ISO dates.

```csharp
bool arabicIndic = Pattern.With.Digit.IsMatch("٣");        // true
bool asciiOnly = Pattern.With.AsciiDigit.IsMatch("٣");     // false

Pattern year = Pattern.With.StartOfInput.Digit.Repeat.Times(4).EndOfInput;
bool devanagariYear = year.IsMatch("२०२६");                 // true
bool arabicYear = year.IsMatch("٢٠٢٦");                     // true
```

### Only `0-9`: `AsciiDigit`

`AsciiDigit` matches exactly `[0-9]`. Inside a set it renders the bare range, so you can combine it with other members.

```csharp
Console.WriteLine(Pattern.With.AsciiDigit);                                   // [0-9]
Console.WriteLine(Pattern.With.Set(Pattern.With.AsciiDigit.Literal("_")));    // [0-9_]
```

### One script's digits: `DigitIn`

`DigitIn(UnicodeScript)` matches the decimal digits of one script.

```csharp
Pattern arabicDigits = Pattern.With.DigitIn(UnicodeScript.Arabic);

Console.WriteLine(arabicDigits);                                           // [\u0660-\u0669\u06F0-\u06F9]
string arabicYear = arabicDigits.Repeat.OneOrMore.Match("سنة ٢٠٢٦").Value; // ٢٠٢٦
string persianYear = arabicDigits.Repeat.OneOrMore.Match("سال ۱۴۰۵").Value; // ۱۴۰۵
bool latin = arabicDigits.IsMatch("2026");                                 // false

Pattern either = Pattern.With.Set(Pattern.With.AsciiDigit.DigitIn(UnicodeScript.Arabic));
Console.WriteLine(either);                                    // [0-9\u0660-\u0669\u06F0-\u06F9]
int numbers = either.Repeat.OneOrMore.Matches("12 ٣٤ ۵۶ २०").Count;   // 3: the Devanagari २० is not included
```

- `Arabic` covers both Arabic-Indic digits `٠-٩` (`U+0660-0669`) and Extended Arabic-Indic digits `۰-۹` (`U+06F0-06F9`), which Persian and Urdu use.
- `Latin` gives `[0-9]`, the same as `AsciiDigit`.
- The 15 scripts with their own digits are listed in [Supported scripts](#supported-scripts). Any other script throws `ArgumentException` with a message that lists the valid ones:

```text
The Greek script has no decimal digits of its own. Scripts with digits: Latin, Arabic, Devanagari, Bengali, Gurmukhi, Gujarati, Tamil, Telugu, Kannada, Malayalam, Thai, Lao, Tibetan, Khmer, Mongolian. (Parameter 'script')
```

- A value that is not a defined `UnicodeScript`, such as `(UnicodeScript)99`, throws `ArgumentOutOfRangeException` from `Script`, `LetterIn` and `DigitIn` alike.

:::note

The compile-time analyzer does not evaluate `DigitIn` with a script that has no digits, so `DigitIn(UnicodeScript.Greek)` is only caught when the code runs. The exception is thrown as soon as you call the method, not when the pattern is first matched.

:::

### Switching a whole pattern to ASCII digits

`WithAsciiDigits()` returns a copy of a pattern in which `\d` means `[0-9]` and `\D` means `[^0-9]`. It is the fix for patterns you did not write yourself, such as a regex from configuration or a [common pattern](./common-patterns.md).

```csharp
Pattern pin = Pattern.FromRegex(@"^\d{4}$");
Pattern asciiPin = pin.WithAsciiDigits();

Console.WriteLine(asciiPin);                     // ^[0-9]{4}$
bool unicodePin = pin.IsMatch("٢٠٢٦");           // true
bool strictPin = asciiPin.IsMatch("٢٠٢٦");       // false

Console.WriteLine(Pattern.FromRegex(@"[\d_]+\D").WithAsciiDigits());   // [0-9_]+[^0-9]
Console.WriteLine(Pattern.FromRegex(@"\d+(?=\D)").WithAsciiDigits());  // [0-9]+(?=[^0-9])
Console.WriteLine(Pattern.FromRegex(@"[\w-[\d]]").WithAsciiDigits());  // [\w-[0-9]]
Console.WriteLine(Pattern.FromRegex(@"\w+\p{Nd}").WithAsciiDigits());  // \w+\p{Nd}
```

- `\d` and `\D` are rewritten everywhere: inside sets, negated sets, class subtractions and lookarounds.
- `\w` and an explicit `\p{Nd}` are left as written. `\w` still matches Unicode digits.
- Options and timeout are kept. When the pattern has no `\d` or `\D`, the same instance is returned.
- Patterns that use `RegexOptions.ECMAScript` already treat `\d` as `0-9` and are returned unchanged.

`WithDigits(DigitMode)` picks the behavior from a value, which is handy when the choice comes from configuration:

| `DigitMode` | `WithDigits` returns |
|-------------|----------------------|
| `Unicode` | The same pattern. `\d` matches any Unicode decimal digit (the .NET default). |
| `Ascii` | `WithAsciiDigits()`. `\d` matches only `0-9`. |

An undefined value throws `ArgumentOutOfRangeException`. Every `CommonPatterns` method (including `Email(PatternStrictness)` and `Url(PatternStrictness)`) also has an overload that takes a `DigitMode` and simply calls `WithDigits` on the result:

```csharp
bool unicodeIp = CommonPatterns.IPv4().IsMatch("0.0.0.٩");                  // true: the default is Unicode
bool asciiIp = CommonPatterns.IPv4(DigitMode.Ascii).IsMatch("0.0.0.٩");     // false
Console.WriteLine(Pattern.FromRegex(@"^\d{4}$").WithDigits(DigitMode.Ascii)); // ^[0-9]{4}$
```

:::tip

For identifiers, card numbers, dates and network addresses, use `DigitMode.Ascii` or `AsciiDigit`. Keep the Unicode default for free text, such as finding quantities in a message written in Arabic or Hindi.

:::

### Unicode digits in typed extraction

Because `\d` accepts every decimal digit, [typed extraction](./typed-extraction.md) converts them before parsing. `Extract<T>`, `TryExtract<T>` and `ExtractAll<T>` turn Unicode decimal digits into ASCII digits before they parse the integer types (`int`, `long`, `short`, `byte`, `sbyte`, `uint`, `ulong`, `ushort`), `decimal`, `double` and `float`. The Arabic decimal separator `٫` and thousands separator `٬` become the separators of the culture you pass (the invariant culture by default).

```csharp
Pattern number = Pattern.With.NamedGroup("value", Pattern.With.Digit.Repeat.OneOrMore);

int arabic = number.Extract<IntValue>("٢٠٢٦").Value;        // 2026
int hindi = number.Extract<IntValue>("२०२६").Value;         // 2026
string raw = number.Extract<StringValue>("٢٠٢٦").Value;     // ٢٠٢٦: strings are not converted

Pattern amount = Pattern.With.NamedGroup("value", Pattern.FromRegex(@"[\d٬]+(?:٫\d+)?"));
decimal total = amount.Extract<DecimalValue>("١٢٬٣٤٥٫٥٠").Value;   // 12345.50

foreach (IntValue value in number.ExtractAll<IntValue>("12 ٣٤ ५६"))
{
    Console.Write($"{value.Value} ");   // prints: 12 34 56
}

// records used by this sample
public record IntValue(int Value);
public record DecimalValue(decimal Value);
public record StringValue(string Value);
```

- Only numeric targets are converted. A `string` member keeps the original digits, and date and time members (`DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly`) are parsed from the text as captured.
- [Templates](./templates.md) do the same for their number and date placeholders.

## Code points and grapheme clusters

A .NET `string` counts UTF-16 code units. An emoji such as `😀` takes two of them, and a family emoji takes eight. When you count, limit or step through text, decide what a "character" means to you:

| Unit | Member | `"👨‍👩‍👧"` counts as |
|------|--------|-------------------------|
| UTF-16 code unit | `Anything` (`.`), `string.Length` | 8 |
| Code point (Unicode scalar value) | `CodePoint` | 5 (three people and two joiners) |
| User-perceived character | `Grapheme` | 1 |

### One code point

`CodePoint` matches one Unicode scalar value: either a surrogate pair (a code point above `U+FFFF`, such as most emoji) or a single character that is not a surrogate.

```csharp
int codePoints = Pattern.With.CodePoint.Count("a😀b");   // 3
int codeUnits = Pattern.With.Anything.Count("a😀b");     // 4, the same as "a😀b".Length

Console.WriteLine(Pattern.With.CodePoint);
// prints: (?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^\uD800-\uDFFF])

Pattern threeSymbols = Pattern.With.StartOfInput.CodePoint.Repeat.Times(3).EndOfInput;
bool emoji = threeSymbols.IsMatch("😀😀😀");                                              // true
bool dots = Pattern.With.StartOfInput.Anything.Repeat.Times(3).EndOfInput.IsMatch("😀😀😀"); // false: 6 code units
```

- `CodePoint` renders as a non-capturing group, so a quantifier repeats the whole code point, never half a surrogate pair.
- A lone (unpaired) surrogate is not matched: `Pattern.With.CodePoint.Count("ab\uD800c")` returns `3`.
- Unlike `.`, `CodePoint` also matches `\n`: `Pattern.With.CodePoint.Count("a\nb")` returns `3`.

### One user-perceived character

`Grapheme` matches what a reader sees as one character: a letter with its accents, an emoji with its skin tone, a flag, a family of people joined by zero-width joiners.

```csharp
int family = Pattern.With.Grapheme.Count("👨‍👩‍👧");        // 1 (man, ZWJ, woman, ZWJ, girl)
int precomposed = Pattern.With.Grapheme.Count("éa");      // 2
int decomposed = Pattern.With.Grapheme.Count("e\u0301a"); // 2 (e followed by a combining acute)
int flags = Pattern.With.Grapheme.Count("🇯🇵🇺🇸");          // 2
int thumb = Pattern.With.Grapheme.Count("👍🏽");            // 1 (thumbs up and a skin tone)
int keycap = Pattern.With.Grapheme.Count("1\uFE0F\u20E3");           // 1 (digit, VS16 and the keycap mark)
int newline = Pattern.With.Grapheme.Count("a\r\nb");      // 3: CRLF is one cluster
int arabicName = Pattern.With.Grapheme.Count("مُحَمَّد");   // 4 (8 code points)

Match first = Pattern.With.Grapheme.Match("👨‍👩‍👧!");
Console.WriteLine($"{first.Index} {first.Length}");       // 0 8

Pattern firstThree = Pattern.With.StartOfInput.Grapheme.Repeat.AtMost(3);
string preview = firstThree.Match("e\u0301👍🏽🇯🇵xyz").Value;  // é👍🏽🇯🇵
```

`Grapheme` keeps these sequences together:

| Sequence | Example |
|----------|---------|
| Carriage return plus line feed | `\r\n` |
| A base character plus combining marks (`\p{M}`, which includes variation selectors such as VS16 and the keycap mark `U+20E3`) and ZWNJ | `e` + `U+0301`, `1️⃣` |
| Emoji skin-tone modifiers (`U+1F3FB-1F3FF`) | `👍🏽` |
| Emoji tag sequences (`U+E0020-E007F`) | `🏴󠁧󠁢󠁳󠁣󠁴󠁿` (the Scotland flag) |
| Zero-width-joiner sequences | `👨‍👩‍👧` |
| Regional-indicator pairs (flags) | `🇯🇵` |
| Hangul syllables and leading/vowel/trailing jamo sequences | `한`, `각` |

### Limits of the grapheme approximation

:::warning

`Grapheme` is an approximation of the Unicode text segmentation rules (UAX #29), not a full implementation. It does not apply the Extended_Pictographic condition to zero-width joiners (any character after a ZWJ is joined), the Prepend rule, the Indic conjunct rules, or combining marks outside the BMP.

:::

For example, a ZWJ between two plain letters joins them:

```csharp
int approximate = Pattern.With.Grapheme.Count("a\u200Db");             // 1
int exact = new StringInfo("a\u200Db").LengthInTextElements;            // 2
```

When you already have the string and need exact segmentation, use `System.Globalization.StringInfo`. Use `Grapheme` when you need cluster-aware matching inside a regex, for example to limit a display name to 20 visible characters.

The rendered expression is long (over 700 characters), so build it once and reuse the pattern.

### Code points and graphemes are not allowed inside a set

`CodePoint` and `Grapheme` can match several UTF-16 code units, and a character class only ever matches one. Putting them in `Set(...)` or `NegatedSet(...)` would silently change their meaning, so rendering such a set throws `InvalidOperationException`:

```text
Grapheme can match more than one UTF-16 code unit, so it cannot be used inside Set(...) or NegatedSet(...). Combine it with the | operator or OneOf(...) instead.
```

The exception is thrown when the set is rendered (`ToString()`, the first match), not when you call `Set`. Use alternation instead:

```csharp
Pattern digitOrSymbol = Pattern.With.Digit | Pattern.With.CodePoint;
int units = digitOrSymbol.Count("1😀");                                                       // 2

int sameUnits = Pattern.With.OneOf(Pattern.With.AsciiDigit, Pattern.With.CodePoint).Count("1😀"); // 2
```

## Ignoring diacritics

`IgnoreDiacritics()` rewrites a pattern so it matches text with or without diacritics, in both precomposed (NFC) and decomposed (NFD) form. It is the tool for search boxes and name matching, where users type `محمد` but the data says `مُحَمَّد`, or type `cafe` and the menu says `café`.

It returns a new pattern. The original is unchanged, and the options and timeout are carried over.

### Arabic with or without harakat

```csharp
Pattern name = Pattern.With.Literal("محمد").IgnoreDiacritics();

bool withHarakat = name.IsMatch("مُحَمَّد");   // true
bool plain = name.IsMatch("محمد");            // true
bool stretched = name.IsMatch("مـحـمـد");     // true: tatweel (kashida) is ignored too

Pattern voweled = Pattern.With.Literal("مُحَمَّد").IgnoreDiacritics();
bool reverse = voweled.IsMatch("محمد");       // true: harakat in the pattern are dropped

Pattern ahmad = Pattern.With.Literal("احمد").IgnoreDiacritics();
bool hamzaAbove = ahmad.IsMatch("أحمد");      // true
bool hamzaBelow = ahmad.IsMatch("إحمد");      // true
bool fromHamza = Pattern.With.Literal("أحمد").IgnoreDiacritics().IsMatch("احمد");   // true
```

- Harakat (fatha, damma, kasra, shadda, sukun, tanwin) are non-spacing marks. After every letter the rewritten pattern accepts any run of them, plus the tatweel `ـ`.
- Alef with hamza or madda (`أ`, `إ`, `آ`) decompose to bare alef plus a mark, so they fold to `ا`. It works in both directions: a hamza in the pattern is reduced to its base letter first.
- `voweled` and `name` render the same regex, because the marks in the pattern are dropped.

### Accented Latin, NFC and NFD

```csharp
Pattern cafe = Pattern.With.Literal("cafe").IgnoreDiacritics();

bool nfc = cafe.IsMatch("un café noir");    // true: é as one precomposed character
bool nfd = cafe.IsMatch("cafe\u0301");     // true: e followed by a combining acute
bool upper = cafe.IsMatch("CAFÉ");          // false: case still matters

bool accentedPattern = Pattern.With.Literal("café").IgnoreDiacritics().IsMatch("cafe");   // true

Pattern city = Pattern.With.Literal("Zurich").IgnoreDiacritics();
string found = city.Match("I live in Zürich.").Value;   // Zürich
```

### Ignoring case as well

`IgnoreDiacritics()` does not change case sensitivity. Add `RegexOptions.IgnoreCase` to make the match case-insensitive too:

```csharp
Pattern anyCafe = Pattern.With.Literal("cafe").WithOptions(RegexOptions.IgnoreCase).IgnoreDiacritics();
bool shouting = anyCafe.IsMatch("CAFÉ");   // true

Pattern later = Pattern.With.Literal("cafe").IgnoreDiacritics().WithOptions(RegexOptions.IgnoreCase);
bool alsoShouting = later.IsMatch("CAFÉ"); // true
```

- When the option is set **before** the rewrite, the variants of both cases are written into each class explicitly (`É` next to `é`).
- When it is set **after**, the .NET engine applies case-insensitivity to the rewritten classes, which works for ordinary letters such as `É`.
- Setting it first is the more predictable choice, because the rewritten regex then lists every character it accepts.

### How the rewrite works

```csharp
Console.WriteLine(Pattern.With.Literal("ا").IgnoreDiacritics());
// prints: [\u0622-\u0623\u0625\u0627][\p{Mn}\u0640]*

Console.WriteLine(Pattern.With.Literal("محمد").IgnoreDiacritics());
// prints: \u0645[\p{Mn}\u0640]*\u062D[\p{Mn}\u0640]*\u0645[\p{Mn}\u0640]*\u062F[\p{Mn}\u0640]*
```

- **Literal letters** become a class of the base letter plus every BMP character whose canonical decomposition is that letter followed only by combining marks. `e` gains `é è ê ë ē ė ę ě ...`, and `ا` gains `آ أ إ`. A letter with no variants stays a single character. Then comes a run of non-spacing marks and tatweel: `[\p{Mn}\u0640]*`.
- **Precomposed letters** in the pattern are reduced to their base first, so `"café"` behaves like `"cafe"`.
- **Non-spacing marks** in the pattern (such as harakat or a combining accent) are dropped, because the mark run already covers them. A quantified mark is dropped as well.
- **Other marks** in the pattern (spacing or enclosing marks) become optional: `(?:mark)?`.
- **Non-letters** stay exact. `"a=b"` does not match `"a≠b"`, even though `≠` decomposes to `=` plus a mark.
- **Quantified letters** are wrapped so the quantifier repeats the letter with its marks: `e+` becomes `(?:[e...][\p{Mn}\u0640]*)+` and matches `éèe`.
- **Groups, alternations, conditionals and lookarounds** are rewritten inside. `"(colou?r|naive)s?"` matches `naïve`.

The variant table covers every BMP code point outside the surrogate range. It is built once per process, the first time any pattern calls `IgnoreDiacritics()`.

### Character classes

A character class that can match a letter gains the same mark run. A plain (non-negated, no subtraction) bracket class also gains the precomposed variants of its letters:

```csharp
Pattern vowels = Pattern.FromRegex("[aeiou]+").IgnoreDiacritics();
string run = vowels.Match("xéèy").Value;                                   // éè

Console.WriteLine(Pattern.FromRegex("[0-9]+").IgnoreDiacritics());         // [0-9]+
Console.WriteLine(Pattern.FromRegex(@"\b\w+\b").IgnoreDiacritics());       // \b(?:\w(?>[\p{Mn}\u0640]*))+\b
Console.WriteLine(Pattern.FromRegex(@"\p{L}+").IgnoreDiacritics());        // (?:\p{L}(?>[\p{Mn}\u0640]*))+
Console.WriteLine(Pattern.FromRegex("[^a]").IgnoreDiacritics());           // [^a](?>[\p{Mn}\u0640]*)
```

- A class with no letters, such as `[0-9]`, is left alone.
- Shorthands (`\w`, `\p{L}`) and negated classes are not expanded with variants. They already match accented letters; they only gain the mark run.
- When the class can itself match a mark (as `\w`, `.` and `[^a]` can), the mark run is made **atomic** (`(?>...)`). Without that, the class and the run could split a long run of marks in many ways, and a failing match would backtrack exponentially. With it, a pattern such as `\b\w+x\b` rejects a letter followed by 20,000 combining marks in milliseconds.

### Options, timeout and engines

```csharp
Pattern resume = Pattern.With.Literal("resume")
    .WithOptions(RegexOptions.IgnoreCase)
    .WithTimeout(TimeSpan.FromSeconds(1));
Pattern folded = resume.IgnoreDiacritics();

Console.WriteLine(folded.Options);              // IgnoreCase
Console.WriteLine(folded.Timeout);              // 00:00:01
Console.WriteLine(resume);                      // resume: the original is unchanged
bool foldedMatch = folded.IsMatch("RÉSUMÉ");    // true
bool originalMatch = resume.IsMatch("RÉSUMÉ");  // false
```

- An empty pattern is returned as is.
- With `UseNonBacktracking()` (.NET 8 and later) the mark run is not made atomic, because that engine does not support atomic groups and does not backtrack anyway. `Pattern.FromRegex(@"\w+").UseNonBacktracking().IgnoreDiacritics()` renders `(?:\w[\p{Mn}\u0640]*)+`.
- A pattern with `RegexOptions.ECMAScript` throws `InvalidOperationException` ("IgnoreDiacritics needs Unicode categories, which RegexOptions.ECMAScript does not support."), because ECMAScript mode has no `\p{...}`.
- The result is built from raw regex text, so call `IgnoreDiacritics()` last, after you have composed the pattern.

### Limitations

`IgnoreDiacritics()` folds only what Unicode defines as "a letter plus combining marks". It does not fold:

| Pair | Why |
|------|-----|
| `ø` and `o`, `ł` and `l`, `đ` and `d` | These letters have no canonical decomposition. |
| `ß` and `ss`, `æ` and `ae` | They are distinct letters or ligatures, not marked letters. |
| Arabic `ة` and `ه` | Teh marbuta is a separate letter: `"فاطمة"` does not match `"فاطمه"`. |
| Arabic `ى` and `ي` | Alef maksura is a separate letter: `"على"` does not match `"علي"`. |
| Marks above `U+FFFF` | The variant table and the mark run are BMP only. |

Backreferences still compare the captured text exactly:

```csharp
Pattern repeated = Pattern.FromRegex(@"(\w+) \1").IgnoreDiacritics();
bool different = repeated.IsMatch("cafe café");   // false: \1 must repeat "cafe" exactly
bool same = repeated.IsMatch("café café");        // true
```

When you need these extra folds, normalize the input and the pattern yourself (for example, map `ة` to `ه` and `ى` to `ي`) before matching, or list the alternatives with `OneOf(...)`.

## Recipes

### An Arabic label with Arabic digits

```csharp
Pattern label = Pattern.With.StartOfInput
    .Script(UnicodeScript.Arabic).Repeat.OneOrMore
    .Literal("-")
    .DigitIn(UnicodeScript.Arabic).Repeat.Times(4)
    .EndOfInput;

bool arabicDigits = label.IsMatch("ملف-٢٠٢٦");   // true
bool asciiDigits = label.IsMatch("ملف-2026");    // false
```

### Limit a display name by visible characters

`string.Length` and `{1,20}` on `.` count UTF-16 code units, so a name made of emoji hits the limit far too early. Count graphemes instead:

```csharp
Pattern displayName = Pattern.With.StartOfInput.Grapheme.Repeat.Times(1, 10).EndOfInput;

bool twoFamilies = displayName.IsMatch("👨‍👩‍👧👨‍👩‍👧");   // true: 2 graphemes, although Length is 16
```

When the limit is defined in code points (as many databases and APIs define it), use `CodePoint` instead:

```csharp
Pattern fiveSymbols = Pattern.With.StartOfInput.CodePoint.Repeat.Times(1, 5).EndOfInput;
bool fiveThumbs = fiveSymbols.IsMatch("👍👍👍👍👍");   // true: 5 code points, Length is 10
```

### Accent-insensitive search

```csharp
Pattern query = Pattern.With.Literal("Sao Paulo").IgnoreDiacritics();
bool hit = query.IsMatch("Flights to São Paulo");   // true
```

Build the pattern from user input with `Literal(...)`, so that regex characters in the query are escaped, then call `IgnoreDiacritics()`.

## Performance and safety notes

- `Script`, `LetterIn` and `DigitIn` cache their character classes per script. The classes are plain ranges, so matching them costs the same as any other character class.
- `IgnoreDiacritics()` builds a BMP-wide variant table on the first call in the process. Later calls reuse it. The rewritten pattern is longer than the original (a single `e` becomes a class of 26 characters), but it is still linear: the mark run is atomic wherever it could be ambiguous.
- `Grapheme` renders a long expression with alternation. It is fine for validation and short inputs. To split a large text you already hold into user-perceived characters, use `StringInfo`.
- `WithAsciiDigits()`, `WithDigits(...)` and `IgnoreDiacritics()` keep the options and timeout of the pattern you call them on. See [ReDoS safety](./redos-safety.md) for how to check a pattern for catastrophic backtracking.

## Common pitfalls

| Pitfall | What happens | Fix |
|---------|--------------|-----|
| Using `\d` for a format such as an IP address or a card number | `٢٠٢٦` and `२०२६` are accepted. | Use `AsciiDigit`, `WithAsciiDigits()` or `DigitMode.Ascii`. |
| Expecting `Script(UnicodeScript.Latin)` to accept digits or spaces | It matches letters and Latin diacritics only. | Add `AsciiDigit` and `Literal(" ")` in a `Set(...)`. |
| Using `LetterIn` for text with diacritics | Harakat and accents split the match: `مُحَمَّد` gives four matches. | Use `Script`, or add `Unicode(UnicodeGroup.Mark)` to the set. |
| Matching CJK Extension B or historic scripts with `Script` | Code points above `U+FFFF` are never matched. | Use `RegEx(...)` with explicit surrogate-pair ranges for those code points. |
| Putting `CodePoint` or `Grapheme` in `Set(...)` | `InvalidOperationException` when the pattern is rendered. | Combine them with `\|` or `OneOf(...)`. |
| Treating `Grapheme` as exact UAX #29 | A ZWJ between letters joins them; Indic conjuncts are not merged. | Use `StringInfo` when you need exact segmentation. |
| Expecting `IgnoreDiacritics()` to ignore case | `cafe` does not match `CAFÉ`. | Add `RegexOptions.IgnoreCase`, preferably before calling `IgnoreDiacritics()`. |
| Expecting `IgnoreDiacritics()` to fold `ة`/`ه` or `ى`/`ي` | They are separate letters, not marks. | Normalize those letters yourself before matching. |
| Calling `DigitIn` with a script that has no digits | `ArgumentException` at the call. | Use one of the 15 scripts listed in [Supported scripts](#supported-scripts), or `Unicode(UnicodeCategory.DecimalDigitNumber)`. |

## See also

- [Character Classes](./character-classes.md) for `Set`, `NegatedSet`, ranges and shorthands.
- [Common Patterns](./common-patterns.md) for the ready-made patterns and their `DigitMode` overloads.
- [Typed Extraction](./typed-extraction.md) for `Extract<T>` and number conversion.
- [Templates](./templates.md) for placeholders that parse numbers and dates.
- [ReDoS Safety](./redos-safety.md) for checking patterns for catastrophic backtracking.
