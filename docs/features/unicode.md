# Unicode: scripts, digits, graphemes and diacritics

Building blocks for text that is not ASCII. All members are on `Pattern` and return a new pattern.

| Member | Matches | Inside `Set(...)` |
|---|---|---|
| `Script(UnicodeScript)` | any character of a script's BMP blocks | yes |
| `LetterIn(UnicodeScript)` | letters (category `L`) of a script | yes |
| `DigitIn(UnicodeScript)` | the script's own decimal digits | yes |
| `AsciiDigit` | `[0-9]` | yes (renders `0-9`) |
| `CodePoint` | one Unicode scalar: a surrogate pair or a non-surrogate BMP character | no: throws `InvalidOperationException` when rendered |
| `Grapheme` | one user-perceived character (approximation) | no: throws `InvalidOperationException` when rendered |
| `IgnoreDiacritics()` | rewrites the whole pattern to match with or without diacritics | n/a |

`UnicodeScript` covers Latin, Greek, Cyrillic, Armenian, Hebrew, Arabic, Syriac, Thaana, Devanagari,
Bengali, Gurmukhi, Gujarati, Tamil, Telugu, Kannada, Malayalam, Thai, Lao, Tibetan, Georgian, Hangul,
Ethiopic, Khmer, Mongolian, Hiragana, Katakana and Han.

## Scripts

```csharp
Pattern.With.Script(UnicodeScript.Greek).Repeat.OneOrMore.Match("abc αβγ def").Value;       // "αβγ"
Pattern.With.Script(UnicodeScript.Arabic).Repeat.OneOrMore.Match("name: مُحَمَّد!").Value;  // "مُحَمَّد"
```

`Script` is built from the script's **Basic Multilingual Plane blocks**, so combining marks and the
few punctuation characters inside those blocks are included (Arabic harakat, the Arabic comma `،`,
tatweel). Latin covers the letters of Basic Latin and Latin-1 (not ASCII digits or punctuation), the
Latin extension blocks and the Combining Diacritical Marks block (shared with Greek and Cyrillic).
Code points above U+FFFF, such as CJK Extension B or historic scripts, are **not** included.

`LetterIn` keeps only letters. Standalone it renders a class subtraction; inside a `Set` (where
subtraction is not allowed) it renders the equivalent letter ranges, so both forms match exactly the
same characters:

```csharp
Pattern.With.LetterIn(UnicodeScript.Arabic).ToString();
// [\u0600-\u06FF\u0750-\u077F\u0870-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF-[\P{L}]]

Pattern.With.LetterIn(UnicodeScript.Arabic).Repeat.OneOrMore.Matches("مُحَمَّد ٢٠٢٦").Count;   // 4: the harakat split the runs
```

Use `Script` when diacritics belong to the word, and `LetterIn` when you need bare letters.

## Digits

The .NET class `\d` (`Pattern.Digit`) matches **every** Unicode decimal digit, not just 0-9:

```csharp
Pattern.With.Digit.IsMatch("٣");        // true
Pattern.With.AsciiDigit.IsMatch("٣");   // false
```

`DigitIn` selects one script's digits. Arabic covers both Arabic-Indic `٠-٩` and Extended
Arabic-Indic (Persian/Urdu) `۰-۹`; Latin gives `0-9`:

```csharp
Pattern.With.DigitIn(UnicodeScript.Arabic).Repeat.OneOrMore.Match("سنة ٢٠٢٦").Value;   // "٢٠٢٦"
Pattern.With.DigitIn(UnicodeScript.Arabic).ToString();                                 // [\u0660-\u0669\u06F0-\u06F9]
Pattern.With.Set(Pattern.With.AsciiDigit.DigitIn(UnicodeScript.Arabic)).ToString();    // [0-9\u0660-\u0669\u06F0-\u06F9]
```

Scripts with their own decimal digits: Latin, Arabic, Devanagari, Bengali, Gurmukhi, Gujarati, Tamil,
Telugu, Kannada, Malayalam, Thai, Lao, Tibetan, Khmer and Mongolian. Any other script throws
`ArgumentException` ("The Greek script has no decimal digits of its own. ...").

### Unicode digits in typed extraction

Because `\d` matches them, `Extract<T>`, `TryExtract<T>`, `ExtractAll<T>` and templates convert
Unicode decimal digits to ASCII before integer, `decimal`, `double` and `float` parsing. The Arabic
decimal separator `٫` and thousands separator `٬` become the culture's separators:

```csharp
public record IntValue(int Value);

Pattern.With.NamedGroup("value", Pattern.With.Digit.Repeat.OneOrMore).Extract<IntValue>("٢٠٢٦");   // Value = 2026
```

## Code points

`CodePoint` matches one Unicode scalar value, so an emoji outside the BMP counts once even though it
is two UTF-16 code units:

```csharp
Pattern.With.CodePoint.Count("a😀b");   // 3 ("a😀b".Length is 4)
```

Lone (unpaired) surrogates are not matched.

## Graphemes

`Grapheme` matches one user-perceived character:

```csharp
Pattern.With.Grapheme.Count("👨‍👩‍👧");     // 1 (man, ZWJ, woman, ZWJ, girl)
Pattern.With.Grapheme.Count("éa");        // 2
Pattern.With.Grapheme.Count("e\u0301a"); // 2 (decomposed é)
Pattern.With.Grapheme.Count("🇯🇵🇺🇸");     // 2 (two flags)
```

It handles CRLF, a base character followed by combining marks (`\p{M}`, which includes variation
selectors such as VS16 and the keycap mark U+20E3), emoji skin-tone modifiers, emoji tag sequences,
zero-width-joiner sequences, regional-indicator flag pairs and Hangul jamo sequences.

**It is an approximation of UAX #29, not a full implementation.** It does not apply the
Extended_Pictographic condition to zero-width joiners (any character after a ZWJ is joined), the
Prepend rule, Indic conjunct rules, or marks outside the BMP. When you need exact segmentation of a
string you already have, use `System.Globalization.StringInfo`; use `Grapheme` when you need cluster
matching inside a regex.

`CodePoint` and `Grapheme` can match several code units, so they cannot appear inside `Set(...)` or
`NegatedSet(...)`. Rendering such a set throws `InvalidOperationException`; combine them with `|` or
`OneOf(...)` instead. Both render as a non-capturing group, so quantifiers repeat the whole unit.

## Ignoring diacritics

`IgnoreDiacritics()` rewrites a pattern so it matches text with or without diacritics, in both
precomposed (NFC) and decomposed (NFD) form. Options and timeout are kept; the original pattern is unchanged.

```csharp
Pattern name = Pattern.With.Literal("محمد").IgnoreDiacritics();
name.IsMatch("مُحَمَّد");          // true

Pattern cafe = Pattern.With.Literal("cafe").IgnoreDiacritics();
cafe.IsMatch("un café noir");    // true (NFC)
cafe.IsMatch("cafe\u0301");     // true (NFD)
cafe.IsMatch("CAFÉ");            // false: case still matters

Pattern.With.Literal("cafe").WithOptions(RegexOptions.IgnoreCase).IgnoreDiacritics().IsMatch("CAFÉ");   // true
```

How the rewrite works:

- Each literal letter becomes a class of its base letter plus every BMP character whose canonical
  decomposition is that letter followed only by combining marks, then a run of non-spacing marks and
  Arabic tatweel. Arabic `ا` covers `أ إ آ`:

  ```csharp
  Pattern.With.Literal("ا").IgnoreDiacritics().ToString();
  // [\u0622-\u0623\u0625\u0627][\p{Mn}\u0640]*
  ```

- Precomposed letters in the pattern are reduced to their base first, so `"café"` also matches `"cafe"`.
- Literal non-spacing marks in the pattern (such as harakat) are dropped, because the mark run covers
  them: `"مُحَمَّد"` matches `"محمد"`. Other literal marks become optional.
- Character classes that can match a letter gain the same mark run; non-negated bracket classes also
  gain the precomposed variants of their letters. When the class can itself match a mark (such as
  `\w` or `.`), the run is atomic to keep matching linear:

  ```csharp
  Pattern.FromRegex(@"\b\w+\b").IgnoreDiacritics().ToString();
  // \b(?:\w(?>[\p{Mn}\u0640]*))+\b
  ```

- With `RegexOptions.IgnoreCase`, variants of both cases are added.
- Non-letters stay exact: `"a=b"` does not match `"a≠b"`, even though `≠` decomposes to `=` plus a mark.

Limitations: letters that differ by more than a diacritic (`ø`, `ł`, `ß`, `æ`, Arabic `ة` and `ه`,
`ى` and `ي`) are not folded; backreferences still compare the captured text exactly; the pattern
must not use `RegexOptions.ECMAScript` (it throws `InvalidOperationException`, because ECMAScript
mode has no `\p{...}`).
