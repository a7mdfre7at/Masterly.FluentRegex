---
id: basic-patterns
title: "Basic Patterns"
description: "Anchors, single-character matchers, literal text and raw regex: the building blocks every pattern starts from."
---

Every pattern is a chain of small building blocks. This page covers the most basic ones: **anchors**, which match a position; **character matchers**, which match one character from a fixed class; **literal text**, which matches exact characters and escapes them for you; and **raw regex**, which inserts regex syntax unchanged. Everything else in the fluent API, such as quantifiers, groups and assertions, is applied to these blocks.

Pick a building block based on what you need to match:

| You want to match | Use | Emits |
|-------------------|-----|-------|
| A position: start or end of a line or of the input, a word edge | [Anchors](#anchors) such as `StartOfLine`, `EndOfInput`, `WordBoundary` | `^`, `\z`, `\b` |
| One character from a common class: a digit, a letter, whitespace | [Character matchers](#character-matchers) such as `Digit`, `Letter`, `Whitespace` | `\d`, `[a-zA-Z]`, `\s` |
| Exact text, including characters such as `.`, `$` or `(` | [`Literal`](#literal-text) | The text, escaped |
| Regex syntax the fluent API has no member for | [`RegEx`](#raw-regex) | The text, unchanged |

All members on this page live on the `Pattern` class in the `Masterly.FluentRegex` namespace. They are available on every target framework of the package: `netstandard2.1`, `net8.0` and `net10.0`.

```csharp
using System;
using Masterly.FluentRegex;

Pattern orderNumber = Pattern.With
    .StartOfLine
    .Literal("ORD-")
    .Digit.Repeat.Times(6)
    .EndOfLine;

Console.WriteLine(orderNumber.ToString());            // prints: ^ORD-\d{6}$
Console.WriteLine(orderNumber.IsMatch("ORD-004211")); // prints: True
Console.WriteLine(orderNumber.IsMatch("ORD-42"));     // prints: False
```

- `StartOfLine` and `EndOfLine` are anchors. They match positions, not characters.
- `Literal("ORD-")` matches the text exactly. The `-` needs no escaping outside a character set, so it is written as is.
- `Digit` is a character matcher. `.Repeat.Times(6)` applies to it, because a quantifier always applies to the last element (see [Quantifiers](./quantifiers.md)).
- `ToString()` returns the regex text. `IsMatch` compiles the regex on first use and caches it.

## Starting a pattern

Start every chain with `Pattern.With`. It returns a new, empty pattern. Each member you call returns a **new** pattern, and the pattern you called it on is left unchanged.

```csharp
using System;
using Masterly.FluentRegex;

Pattern empty = Pattern.With;
Console.WriteLine(empty.IsEmpty);           // prints: True
Console.WriteLine(empty.ToString().Length); // prints: 0
Console.WriteLine(new Pattern().IsEmpty);   // prints: True

Pattern digit = Pattern.With.Digit;
Pattern digitThenA = digit.Literal('a');
Pattern digitThenB = digit.Literal('b');

Console.WriteLine(digit);      // prints: \d
Console.WriteLine(digitThenA); // prints: \da
Console.WriteLine(digitThenB); // prints: \db
```

| Member | What it does |
|--------|--------------|
| `Pattern.With` | Static property. Returns a new, empty pattern to start a chain. Every access creates a new instance. |
| `new Pattern()` | Same as `Pattern.With`. `Pattern.With` reads better in a chain. |
| `IsEmpty` | `true` when the pattern has no elements. |
| `ToString()` | The regex text. It is built once and cached, so calling it again is cheap. |

- Because patterns are immutable, you can keep a pattern in a `static readonly` field and reuse it as a building block in many other patterns. Extending a pattern is O(1): the new pattern shares the elements of the old one.
- Two patterns are equal (`Equals`) when they render the same regex text with the same options and timeout, however you built them. `Pattern.With.Literal("ab").Equals(Pattern.With.RegEx("ab"))` is `true`. `GetHashCode` agrees with `Equals`, so a pattern can be a dictionary key.
- The `==` and `!=` operators are not overloaded. They compare references, so `Pattern.With.Digit == Pattern.With.Digit` is `false`. Use `Equals` to compare patterns.
- The basics of the model (immutability, options, timeouts) are covered in [Core Concepts](./core-concepts.md).

## Anchors

Anchors match a **position** in the text, not a character. They never consume input, so they cannot make a match longer.

| Member | Regex | Matches |
|--------|-------|---------|
| `StartOfLine` | `^` | The start of the input. With `RegexOptions.Multiline`, also the position after every `\n`. |
| `EndOfLine` | `$` | The end of the input, or the position before a final `\n`. With `RegexOptions.Multiline`, also the position before every `\n`. |
| `StartOfInput` | `\A` | Only the start of the input, whatever the options. |
| `EndOfInput` | `\z` | Only the very end of the input, whatever the options. A trailing `\n` is not skipped. |
| `WordBoundary` | `\b` | A position between a word character (`\w`) and a non-word character, or between a word character and the start or end of the input. |
| `NonWordBoundary` | `\B` | Any position that is not a word boundary. |

### Start and end of a line

`StartOfLine` and `EndOfLine` emit `^` and `$`. Without options they behave like "start and end of the input". Turn on `RegexOptions.Multiline` to make them match at every line.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern errorLine = Pattern.With.StartOfLine.Literal("Error");
string log = "Info: ok\nError: disk full";

Console.WriteLine(errorLine);                                                  // prints: ^Error
Console.WriteLine(errorLine.IsMatch(log));                                     // prints: False
Console.WriteLine(errorLine.WithOptions(RegexOptions.Multiline).IsMatch(log)); // prints: True
```

- Without `Multiline`, `^` matches only at the start of `log`, and the text there is `Info`.
- `WithOptions` returns a copy of the pattern that runs with the given options. The regex text does not change.
- `EndOfLine` matches before a final `\n` even without `Multiline`. If that is not what you want, use `EndOfInput` (see the next section).

### Start and end of the input

`StartOfInput` and `EndOfInput` emit `\A` and `\z`. They ignore `Multiline`, and `\z` does not accept a trailing newline.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern lineEnd = Pattern.With.Digit.Repeat.OneOrMore.EndOfLine;
Pattern inputEnd = Pattern.With.Digit.Repeat.OneOrMore.EndOfInput;

Console.WriteLine(lineEnd);                     // prints: \d+$
Console.WriteLine(inputEnd);                    // prints: \d+\z
Console.WriteLine(lineEnd.IsMatch("12345\n"));  // prints: True
Console.WriteLine(inputEnd.IsMatch("12345\n")); // prints: False

Pattern firstLineOnly = Pattern.With.StartOfInput.Literal("Error")
    .WithOptions(RegexOptions.Multiline);

Console.WriteLine(firstLineOnly);                                        // prints: \AError
Console.WriteLine(firstLineOnly.IsMatch("Info: ok\nError: disk full")); // prints: False
```

- `$` accepts `"12345\n"` because it matches before a final newline. `\z` rejects it.
- `\A` matches only at the start of the input, even with `Multiline` on, so `Error` on the second line is not found.
- The regex `\Z` (capital Z: end of input or before a final newline) has no member. Write it with `RegEx(@"\Z")` (see [Raw regex](#raw-regex)).

### Validating a whole value

When you validate a value, such as a form field or an ID, the pattern must cover the whole input. Wrap it in `StartOfInput` and `EndOfInput`.

```csharp
using System;
using Masterly.FluentRegex;

Pattern zipCode = Pattern.With
    .StartOfInput
    .Digit.Repeat.Times(5)
    .EndOfInput;

Console.WriteLine(zipCode);                    // prints: \A\d{5}\z
Console.WriteLine(zipCode.IsMatch("12345"));   // prints: True
Console.WriteLine(zipCode.IsMatch("12345\n")); // prints: False
Console.WriteLine(zipCode.IsMatch("123456"));  // prints: False
```

:::tip

Prefer `StartOfInput` / `EndOfInput` (`\A...\z`) over `StartOfLine` / `EndOfLine` (`^...$`) for validation. `^...$` accepts a value with a trailing newline, and it matches a single line inside a longer text as soon as someone adds `RegexOptions.Multiline`.

:::

### Windows line endings

In multiline mode `$` matches before `\n` only. It does not know about `\r\n`, so a line that ends with `\r\n` has a `\r` between the text and the position `$` matches.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

string text = "disk full\r\nnext line";

Pattern naive = Pattern.With.Literal("full").EndOfLine.WithOptions(RegexOptions.Multiline);
Pattern crlfAware = Pattern.With
    .Literal("full")
    .CarriageReturn.Repeat.Optional
    .EndOfLine
    .WithOptions(RegexOptions.Multiline);

Console.WriteLine(naive.IsMatch(text));     // prints: False
Console.WriteLine(crlfAware);               // prints: full\r?$
Console.WriteLine(crlfAware.IsMatch(text)); // prints: True
```

- `CarriageReturn.Repeat.Optional` emits `\r?`: an optional carriage return before the end of the line.
- The `\r` becomes part of the match. Put the text you want in a group if you extract it (see [Groups and Backreferences](./groups-and-backreferences.md)).

### Word boundaries

`WordBoundary` (`\b`) matches where a word starts or ends. Use it to match whole words only. `NonWordBoundary` (`\B`) is the opposite: it matches only inside a word, or between two non-word characters.

```csharp
using System;
using Masterly.FluentRegex;

Pattern wholeWord = Pattern.With
    .WordBoundary
    .Literal("test")
    .WordBoundary;

Console.WriteLine(wholeWord);                           // prints: \btest\b
Console.WriteLine(wholeWord.IsMatch("this is a test")); // prints: True
Console.WriteLine(wholeWord.IsMatch("testing"));        // prints: False

Pattern insideWord = Pattern.With
    .NonWordBoundary
    .Literal("cat")
    .NonWordBoundary;

Console.WriteLine(insideWord);                        // prints: \Bcat\B
Console.WriteLine(insideWord.IsMatch("concatenate")); // prints: True
Console.WriteLine(insideWord.IsMatch("the cat sat")); // prints: False
```

- A word boundary is defined by `\w`, which in .NET includes Unicode letters and digits. `café` is one word, and there is no boundary before the `é`.
- A boundary next to a non-word character works only if your text has a word character on the other side. `\b` before `#` in `"#tag"` never matches at the start of the input, because both sides are non-word.
- To check what comes before or after a match without consuming it, use lookarounds (see [Assertions](./assertions.md)).

## Character matchers

A character matcher matches exactly **one** character from a fixed class. Add a quantifier to match more than one (see [Quantifiers](./quantifiers.md)).

### Shorthand classes

| Member | Regex | Matches |
|--------|-------|---------|
| `Digit` | `\d` | A Unicode decimal digit: `0`-`9` and the digits of other scripts, such as Arabic-Indic `٣` |
| `NonDigit` | `\D` | Any character that `\d` does not match |
| `Word` | `\w` | A Unicode letter (`\p{L}`), nonspacing mark (`\p{Mn}`), decimal digit (`\p{Nd}`) or connector punctuation (`\p{Pc}`) such as `_` |
| `NonWord` | `\W` | Any character that `\w` does not match |
| `Whitespace` | `\s` | A Unicode whitespace character: space, `\t`, `\n`, `\r`, `\f`, `\v`, no-break space and other Unicode spaces |
| `NonWhitespace` | `\S` | Any character that `\s` does not match |

```csharp
using System;
using Masterly.FluentRegex;

Pattern wordDigits = Pattern.With
    .Word.Repeat.OneOrMore
    .Whitespace
    .Digit.Repeat.OneOrMore;

Console.WriteLine(wordDigits);                     // prints: \w+\s\d+
Console.WriteLine(wordDigits.IsMatch("item 123")); // prints: True
Console.WriteLine(wordDigits.IsMatch("item123"));  // prints: False
```

:::warning

In .NET, `\d` and `\w` are **not** ASCII-only. `Digit` accepts `٢٠٢٦` (Arabic-Indic digits) and `Word` accepts `é`. When a format allows only `0`-`9`, use `AsciiDigit` or call `WithAsciiDigits()` on the finished pattern.

:::

```csharp
using System;
using Masterly.FluentRegex;

Pattern year = Pattern.With.StartOfInput.Digit.Repeat.Times(4).EndOfInput;
Pattern asciiYear = Pattern.With.StartOfInput.AsciiDigit.Repeat.Times(4).EndOfInput;

Console.WriteLine(year.IsMatch("2026"));      // prints: True
Console.WriteLine(year.IsMatch("٢٠٢٦"));      // prints: True
Console.WriteLine(asciiYear);                 // prints: \A[0-9]{4}\z
Console.WriteLine(asciiYear.IsMatch("٢٠٢٦")); // prints: False
Console.WriteLine(year.WithAsciiDigits());    // prints: \A[0-9]{4}\z
```

- `AsciiDigit` emits `[0-9]`. Use it while you build the pattern.
- `WithAsciiDigits()` rewrites every `\d` and `\D` in a finished pattern, including those inside sets and lookarounds. It leaves `\w` and explicit categories such as `\p{Nd}` alone. `WithDigits(DigitMode.Ascii)` does the same, and `WithDigits(DigitMode.Unicode)` returns the pattern unchanged.
- With `RegexOptions.ECMAScript`, .NET itself restricts `\d`, `\w` and `\s` to ASCII.
- Script-specific digits and letters (`DigitIn`, `LetterIn`, `Script`) are covered in [Unicode](./unicode.md).

### ASCII letter classes

These members match ASCII characters only (with one exception under `IgnoreCase`, shown below). They render as a bracketed class on their own and as a bare range inside a set.

| Member | Regex on its own | Inside a set | Matches |
|--------|------------------|--------------|---------|
| `Letter` | `[a-zA-Z]` | `a-zA-Z` | An ASCII letter |
| `LowercaseLetter` | `[a-z]` | `a-z` | An ASCII lowercase letter |
| `UppercaseLetter` | `[A-Z]` | `A-Z` | An ASCII uppercase letter |
| `HexDigit` | `[0-9a-fA-F]` | `0-9a-fA-F` | A hexadecimal digit |
| `AsciiDigit` | `[0-9]` | `0-9` | An ASCII digit |

```csharp
using System;
using Masterly.FluentRegex;

Pattern asciiWord = Pattern.With.StartOfInput.Letter.Repeat.OneOrMore.EndOfInput;
Pattern anyWord = Pattern.With.StartOfInput.Word.Repeat.OneOrMore.EndOfInput;

Console.WriteLine(asciiWord);                 // prints: \A[a-zA-Z]+\z
Console.WriteLine(asciiWord.IsMatch("cafe")); // prints: True
Console.WriteLine(asciiWord.IsMatch("café")); // prints: False
Console.WriteLine(anyWord.IsMatch("café"));   // prints: True

Pattern code = Pattern.With.UppercaseLetter.Repeat.Times(3).HexDigit.Repeat.Times(4);

Console.WriteLine(code);                     // prints: [A-Z]{3}[0-9a-fA-F]{4}
Console.WriteLine(code.IsMatch("ABC12fe9")); // prints: True
```

- For letters of any language, use `Unicode(UnicodeGroup.Letter)` (`\p{L}`), described in [Character Classes](./character-classes.md).
- `RegexOptions.IgnoreCase` affects these classes. With it, `LowercaseLetter` also matches uppercase letters, and even a few non-ASCII characters that fold to an ASCII letter, such as the Kelvin sign `K` (U+212A):

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern lowercaseOnly = Pattern.With.StartOfInput.LowercaseLetter.Repeat.OneOrMore.EndOfInput;
Pattern anyCase = lowercaseOnly.WithOptions(RegexOptions.IgnoreCase);

Console.WriteLine(lowercaseOnly.IsMatch("Hello")); // prints: False
Console.WriteLine(anyCase.IsMatch("Hello"));       // prints: True
Console.WriteLine(anyCase.IsMatch("K"));      // prints: True
```

:::note

Older documentation lists `Letter` as emitting `a-zA-Z`. That is only how it renders **inside** a set. On its own it emits `[a-zA-Z]`, so `Pattern.With.Letter` is a complete, valid regex.

:::

### Any character

`Anything` emits `.`, which matches any character **except** `\n`. Turn on `RegexOptions.Singleline` to make it match `\n` too.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern aAnyB = Pattern.With.Literal('a').Anything.Literal('b');

Console.WriteLine(aAnyB);                                                      // prints: a.b
Console.WriteLine(aAnyB.IsMatch("a-b"));                                       // prints: True
Console.WriteLine(aAnyB.IsMatch("a\nb"));                                      // prints: False
Console.WriteLine(aAnyB.WithOptions(RegexOptions.Singleline).IsMatch("a\nb")); // prints: True
```

- `Anything.Repeat.ZeroOrMore` (`.*`) is greedy: it runs to the end of the line and then backtracks. Next to other open-ended repeats it can cause slow matching. Prefer a narrower class, such as `NegatedSet(...)`, or a lazy quantifier (see [Quantifiers](./quantifiers.md) and [ReDoS Safety](./redos-safety.md)).
- `.` matches one UTF-16 code unit. An emoji above U+FFFF is two code units, so one `.` matches only half of it. Use `CodePoint` or `Grapheme` from [Unicode](./unicode.md) for user-perceived characters.

### Control characters

| Member | Regex | Matches |
|--------|-------|---------|
| `Tab` | `\t` | Horizontal tab, U+0009 |
| `Newline` | `\n` | Line feed, U+000A |
| `CarriageReturn` | `\r` | Carriage return, U+000D |

```csharp
using System;
using Masterly.FluentRegex;

Pattern tsvRow = Pattern.With
    .NonWhitespace.Repeat.OneOrMore
    .Tab
    .NonWhitespace.Repeat.OneOrMore
    .CarriageReturn.Repeat.Optional
    .Newline;

Console.WriteLine(tsvRow);                         // prints: \S+\t\S+\r?\n
Console.WriteLine(tsvRow.IsMatch("id\tname\r\n")); // prints: True
Console.WriteLine(tsvRow.IsMatch("id name\n"));    // prints: False
```

- `Literal('\t')`, `Literal('\n')` and `Literal('\r')` emit the same text. The named members read better.
- For any line break, `CarriageReturn.Repeat.Optional.Newline` (`\r?\n`) accepts both Unix and Windows endings.

### Matchers inside a set

Every character matcher can also be a member of a character set built with `Set` or `NegatedSet`. Shorthand classes render the same way in both places. Bracketed classes drop their brackets inside a set, so they combine into one set.

```csharp
using System;
using Masterly.FluentRegex;

Pattern identifierChar = Pattern.With.Set(Pattern.With.Letter.Digit.Literal('_'));
Pattern notSpaceOrDigit = Pattern.With.NegatedSet(Pattern.With.Whitespace.Digit);

Console.WriteLine(identifierChar);  // prints: [a-zA-Z\d_]
Console.WriteLine(notSpaceOrDigit); // prints: [^\s\d]
```

`Anything` and the anchors are not character classes. They emit their regex text as is, and inside `[...]` that text has a different meaning:

```csharp
using System;
using Masterly.FluentRegex;

Pattern dotInSet = Pattern.With.Set(Pattern.With.Anything);
Pattern boundaryInSet = Pattern.With.Set(Pattern.With.WordBoundary);

Console.WriteLine(dotInSet);                    // prints: [.]
Console.WriteLine(dotInSet.IsMatch("x"));       // prints: False
Console.WriteLine(dotInSet.IsMatch("."));       // prints: True
Console.WriteLine(boundaryInSet);               // prints: [\b]
Console.WriteLine(boundaryInSet.IsMatch("\b")); // prints: True
```

:::warning

Do not put `Anything`, `StartOfLine`, `EndOfLine`, `StartOfInput`, `EndOfInput`, `WordBoundary` or `NonWordBoundary` inside a set. `[.]` matches only a literal dot, and `[\b]` matches the backspace character (U+0008), not a word boundary. Sets, ranges and the other class members are covered in [Character Classes](./character-classes.md).

:::

## Literal text

`Literal` matches text exactly. It escapes every regex special character for you, so you never write a backslash by hand. Use it for fixed text and for **any text that comes from a user**.

```csharp
using System;
using Masterly.FluentRegex;

Pattern dollar = Pattern.With.Literal('$');
Pattern abc = Pattern.With.Literal('a', 'b', 'c');
Pattern greeting = Pattern.With.Literal("Hello, World!");
Pattern call = Pattern.With.Literal("(test)");
Pattern operators = Pattern.With.Literal("a+b*c?");

Console.WriteLine(dollar);    // prints: \$
Console.WriteLine(abc);       // prints: abc
Console.WriteLine(greeting);  // prints: Hello, World!
Console.WriteLine(call);      // prints: \(test\)
Console.WriteLine(operators); // prints: a\+b\*c\?

Console.WriteLine(operators.IsMatch("a+b*c?")); // prints: True
Console.WriteLine(operators.IsMatch("abc"));    // prints: False
```

| Overload | Parameter | Throws |
|----------|-----------|--------|
| `Literal(string literalChars)` | The text to match | `ArgumentNullException` when the text is `null` |
| `Literal(params char[] characters)` | One or more characters to match, in order | `ArgumentNullException` when the array is `null` |

- Both overloads add one element per character. `Literal("abc")` and `Literal('a', 'b', 'c')` build the same pattern.

**Searching for user input.** Escaping makes `Literal` safe for text you do not control. The user's `+`, `(` and `.` are matched as plain characters, not as regex syntax.

```csharp
using System;
using Masterly.FluentRegex;

// searchTerm comes from your application, here "1+1=2 (approx.)"
Pattern search = Pattern.With.Literal(searchTerm);

Console.WriteLine(search);                                         // prints: 1\+1=2 \(approx\.\)
Console.WriteLine(search.IsMatch("Note: 1+1=2 (approx.) holds.")); // prints: True
```

### What gets escaped

`Literal` escapes a character based on where it ends up: in normal text, or inside a character set.

| Character | In normal text | Inside a set |
|-----------|----------------|--------------|
| `^ $ . \| { } [ ] ( ) * + ? \` | Escaped with `\` | Escaped with `\` |
| `-` | Not escaped | Escaped as `\-`, so it never forms a range |
| Tab, line feed, carriage return, form feed, vertical tab, escape (U+001B) | `\t`, `\n`, `\r`, `\f`, `\v`, `\e` | Same |
| Any other control character | `\uXXXX`, for example `\u0000` | Same |
| Everything else, including space, `#`, `/`, `=` and non-ASCII letters | Not escaped | Not escaped |

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.With.Literal(@"^$.|{}[]()*+?\"));    // prints: \^\$\.\|\{\}\[\]\(\)\*\+\?\\
Console.WriteLine(Pattern.With.Literal("a-b #1/2"));           // prints: a-b #1/2
Console.WriteLine(Pattern.With.Literal("\t\n\r\f\v\u001B"));   // prints: \t\n\r\f\v\e
Console.WriteLine(Pattern.With.Literal('\0', '\u0007'));       // prints: \u0000\u0007
```

### Literals inside a set

Inside `Set` or `NegatedSet`, each literal character is one member of the set. A `-` is escaped, so `Literal("a-z")` inside a set means the three characters `a`, `-` and `z`, not a range.

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.With.Set(Pattern.With.Literal("a-z")));                // prints: [a\-z]
Console.WriteLine(Pattern.With.Set(Pattern.With.Literal("a-z")).IsMatch("m"));  // prints: False
```

- To match a range, use `Range('a', 'z')` (see [Character Classes](./character-classes.md)).
- `AnyOf("...")` and `NoneOf("...")` are shortcuts for a set or a negated set of literal characters.

### Empty and null text

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.With.Literal("").IsEmpty); // prints: True
Console.WriteLine(Pattern.With.Literal().IsEmpty);   // prints: True
```

- An empty string or an empty array adds nothing. The pattern is unchanged.
- `null` throws `ArgumentNullException`. When the `null` is a compile-time constant, the analyzer that ships with the package reports it at build time as error `FRX006`: `This pattern throws ArgumentNullException when it is built: Value cannot be null. (Parameter 'literalChars')`. See [Compile-Time Checks](./compile-time-checks.md).

### Literals and IgnorePatternWhitespace

Under `RegexOptions.IgnorePatternWhitespace`, the regex engine ignores unescaped whitespace and treats `#` as the start of a comment. A pattern with that option escapes the whitespace and `#` in its literals, so they still match what you wrote:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern fullName = Pattern.With.Literal("John Smith").WithOptions(RegexOptions.IgnorePatternWhitespace);
Pattern language = Pattern.With.Literal("C#").WithOptions(RegexOptions.IgnorePatternWhitespace);

Console.WriteLine(fullName);                        // prints: John\ Smith
Console.WriteLine(fullName.IsMatch("John Smith"));  // prints: True
Console.WriteLine(fullName.IsMatch("JohnSmith"));   // prints: False
Console.WriteLine(language);                        // prints: C\#
Console.WriteLine(language.IsMatch("C#"));          // prints: True
```

- Without the option, spaces and `#` are written as they are, because they are ordinary characters in a normal regex.
- The same applies inside an inline scope: `InlineOptions(RegexOptions.IgnorePatternWhitespace, Pattern.With.Literal("a b"))` renders `(?x:a\ b)`.
- Text added with `RegEx(...)` is never changed, so layout whitespace and `#` comments in a raw fragment still work in free-spacing mode.

## Raw regex

`RegEx` inserts regex text **unchanged**. Nothing is escaped and nothing is checked when you build the pattern. Use it for syntax the fluent API has no member for, or to paste a fragment you already have.

```csharp
using System;
using Masterly.FluentRegex;

Pattern raw = Pattern.With
    .StartOfLine
    .RegEx("[a-z]+")
    .RegEx(@"\d{3}")
    .EndOfLine;

Console.WriteLine(raw);                   // prints: ^[a-z]+\d{3}$
Console.WriteLine(raw.IsMatch("abc123")); // prints: True
```

- Use a verbatim string (`@"\d{3}"`) for regex text. Otherwise C# needs every backslash doubled (`"\\d{3}"`).
- On `net8.0` and `net10.0`, the parameter is marked `[StringSyntax(StringSyntaxAttribute.Regex)]`, so Visual Studio and Rider color the regex and flag syntax errors as you type. `netstandard2.1` builds have no such attribute.
- A raw fragment can mix with fluent members in any order. A whole regex can be wrapped: `Pattern.With.RegEx(existingRegex)` gives you a `Pattern` with every feature of the library.

**Constructs with no member.** These are common reasons to reach for `RegEx`:

```csharp
using System;
using Masterly.FluentRegex;

Pattern untilFinalNewline = Pattern.With.Digit.Repeat.OneOrMore.RegEx(@"\Z");

Console.WriteLine(untilFinalNewline);                   // prints: \d+\Z
Console.WriteLine(untilFinalNewline.IsMatch("123\n"));  // prints: True
Console.WriteLine(untilFinalNewline.IsMatch("123\nx")); // prints: False
```

- Other anchors with no member: `\G` (where the previous match ended) and `\Z` as shown.
- Before you use `RegEx`, check [Character Classes](./character-classes.md), [Groups and Backreferences](./groups-and-backreferences.md) and [Assertions](./assertions.md). Most constructs have a fluent member that is easier to read and validates its arguments.

### Raw text is checked only when the regex is built

`RegEx` stores the text as given. An invalid fragment does not fail when you build the pattern, and `ToString()` returns it without complaint. It fails when the regex is compiled: on the first `IsMatch`, `Match`, `Replace` or `ToRegex()` call, and again on every later call, because a failed compile is not cached.

```csharp
using System;
using Masterly.FluentRegex;

// ruleText comes from your configuration, here "[a-z"
Pattern broken = Pattern.With.RegEx(ruleText);

Console.WriteLine(broken); // prints: [a-z

try
{
    broken.IsMatch("abc");
}
catch (ArgumentException ex)
{
    Console.WriteLine(ex.GetType().Name); // prints: RegexParseException
    Console.WriteLine(ex.Message);        // prints: Invalid pattern '[a-z' at offset 4. Unterminated [] set.
}
```

- Catch `ArgumentException`. On .NET 7 and later the actual type is `RegexParseException`, which derives from `ArgumentException` and adds an `Error` code and an `Offset`.
- Validate fragments you load at run time before you use them, for example with the tools in [Validation and Debugging](./validation-and-debugging.md).
- When the fragment is a **constant**, you do not have to wait for run time. The analyzer that ships with the package compiles every constant chain during the build. `Pattern.With.RegEx("[a-z")` fails the build with `FRX003: The pattern produces an invalid regular expression '[a-z': Invalid pattern '[a-z' at offset 4. Unterminated [] set.` See [Compile-Time Checks](./compile-time-checks.md).
- `RegEx(null)` does not throw. It adds an element with empty text, so `ToString()` is `""` but `IsEmpty` is `false`.

### Raw text inside a set

Inside `Set` or `NegatedSet`, raw text is also inserted unchanged. That is a way to write class syntax directly:

```csharp
using System;
using Masterly.FluentRegex;

Pattern vowelOrDigit = Pattern.With.Set(Pattern.With.RegEx("aeiou").Digit);

Console.WriteLine(vowelOrDigit); // prints: [aeiou\d]
```

- Unlike `Literal`, raw text is not escaped inside a set. `RegEx("a-z")` there is a range, while `Literal("a-z")` is three characters.

## Quantifiers apply to the last element

A quantifier repeats only the **last element** of the chain. `Literal` adds one element per character and `RegEx` adds its text as one piece of raw text, so neither repeats a whole word on its own:

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.With.Literal("ab").Repeat.OneOrMore); // prints: ab+
Console.WriteLine(Pattern.With.RegEx("ab").Repeat.OneOrMore);   // prints: ab+

Pattern grouped = Pattern.With.NonCapturingGroup(Pattern.With.Literal("ab")).Repeat.OneOrMore;

Console.WriteLine(grouped);                                                            // prints: (?:ab)+
Console.WriteLine(Pattern.With.StartOfInput.Then(grouped).EndOfInput.IsMatch("abab")); // prints: True
```

- `ab+` means "`a`, then one or more `b`". To repeat the whole text, wrap it in a group first. `NonCapturingGroup` does not create a numbered group.
- The same rule applies to characters outside the Basic Multilingual Plane. An emoji such as `😀` is two UTF-16 characters, so `Literal("😀").Repeat.OneOrMore` repeats only its second half:

```csharp
using System;
using Masterly.FluentRegex;

Pattern smiles = Pattern.With.StartOfInput.Literal("😀").Repeat.OneOrMore.EndOfInput;
Pattern groupedSmiles = Pattern.With
    .StartOfInput
    .NonCapturingGroup(Pattern.With.Literal("😀")).Repeat.OneOrMore
    .EndOfInput;

Console.WriteLine(smiles.IsMatch("😀😀"));        // prints: False
Console.WriteLine(groupedSmiles.IsMatch("😀😀")); // prints: True
```

Quantifiers, lazy and possessive modes are covered in [Quantifiers](./quantifiers.md).

## Member reference

Every anchor, character matcher and text member covered on this page, with the regex it emits:

| Member | Kind | Regex on its own | Inside a set |
|--------|------|------------------|--------------|
| `StartOfLine` | Anchor | `^` | Not a class member (see [the warning](#matchers-inside-a-set)) |
| `EndOfLine` | Anchor | `$` | Not a class member |
| `StartOfInput` | Anchor | `\A` | Not a class member |
| `EndOfInput` | Anchor | `\z` | Not a class member |
| `WordBoundary` | Anchor | `\b` | Not a class member (`[\b]` is backspace) |
| `NonWordBoundary` | Anchor | `\B` | Not a class member |
| `Digit` | Character matcher | `\d` | `\d` |
| `NonDigit` | Character matcher | `\D` | `\D` |
| `Word` | Character matcher | `\w` | `\w` |
| `NonWord` | Character matcher | `\W` | `\W` |
| `Whitespace` | Character matcher | `\s` | `\s` |
| `NonWhitespace` | Character matcher | `\S` | `\S` |
| `Letter` | Character matcher | `[a-zA-Z]` | `a-zA-Z` |
| `LowercaseLetter` | Character matcher | `[a-z]` | `a-z` |
| `UppercaseLetter` | Character matcher | `[A-Z]` | `A-Z` |
| `HexDigit` | Character matcher | `[0-9a-fA-F]` | `0-9a-fA-F` |
| `AsciiDigit` | Character matcher | `[0-9]` | `0-9` |
| `Anything` | Character matcher | `.` | Not a class member (`[.]` is a literal dot) |
| `Tab` | Character matcher | `\t` | `\t` |
| `Newline` | Character matcher | `\n` | `\n` |
| `CarriageReturn` | Character matcher | `\r` | `\r` |
| `Literal(string)` | Text | Escaped text | Escaped text, `-` also escaped |
| `Literal(params char[])` | Text | Escaped characters | Escaped characters, `-` also escaped |
| `RegEx(string)` | Raw text | Text unchanged | Text unchanged |

- All of these are properties, except `Literal` and `RegEx`, which are methods. Every one returns a new `Pattern`.
- Related members on other pages: `Range`, `AnyOf`, `NoneOf`, `Set`, `NegatedSet` and the Unicode category members in [Character Classes](./character-classes.md); `Script`, `LetterIn`, `DigitIn`, `CodePoint` and `Grapheme` in [Unicode](./unicode.md); ready-made formats in [Common Patterns](./common-patterns.md).
- A one-page summary of regex syntax is in the [Regex Cheat Sheet](./regex-cheat-sheet.md).
