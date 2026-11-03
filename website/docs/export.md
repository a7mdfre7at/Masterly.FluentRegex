---
id: export
title: "Export to JavaScript and JSON Schema"
description: "Translate patterns to JavaScript, JSON Schema and TypeScript modules that keep the .NET meaning, with warnings for every remaining difference."
---

Export translates a pattern into another regex dialect. You write a validation rule once in C# and run the same rule in the browser, in Node.js, and in your OpenAPI or JSON Schema documents. Use it when client and server must accept exactly the same input, for example a form field that is checked in the browser and again in your API.

The translation keeps the .NET meaning. .NET and JavaScript disagree about several everyday constructs, such as `\d`, `\w` and `$`. The library writes these out so that the JavaScript regex accepts the same strings as the .NET one. When it cannot, it says so: you get a warning for a difference it could not remove, and an exception for a construct JavaScript cannot express.

Pick an output based on where the pattern goes:

| Output | Call | Produces | Use it for |
|--------|------|----------|------------|
| JavaScript regex literal | `pattern.ToJavaScript()` | `/^\p{Nd}{5}(?=\n?$)/u` | Copying one pattern into JavaScript or TypeScript code. |
| JavaScript, with details | `pattern.ToFlavor(RegexFlavor.JavaScript)` | A `FlavorConversion` | Reading the flags, warnings and group numbers as well. |
| JSON Schema `pattern` | `pattern.ToFlavor(RegexFlavor.JsonSchema)` | A `FlavorConversion` without flags | The `pattern` keyword in JSON Schema and OpenAPI documents. |
| TypeScript module | `PatternExport.ToTypeScript(patterns)` | A source file with one `export const` per pattern | Generating a shared rules file for your front end. |

The same `ToFlavor` call also targets databases. See [SQL regex dialects](./sql-dialects.md) for PostgreSQL, MySQL, MariaDB, SQL Server and SQLite, and [SQL LIKE](./sql-like.md) for databases without a regex engine.

:::tip[No extra package]

Export is part of the core `Masterly.FluentRegex` package, in the `Masterly.FluentRegex` namespace. It works on every target framework the package supports: `netstandard2.1`, `net8.0` and `net10.0`.

:::

## Quick start

`ToJavaScript()` returns a ready-to-paste regex literal. `ToFlavor` returns the same translation as a `FlavorConversion` object, so you can also check whether it is exact.

```csharp
using Masterly.FluentRegex;

string literal = CommonPatterns.ZipCode().ToJavaScript();
// /^\p{Nd}{5}(?:-\p{Nd}{4})?(?=\n?$)/u

FlavorConversion js = CommonPatterns.ZipCode().ToFlavor(RegexFlavor.JavaScript);
string pattern = js.Pattern;     // ^\p{Nd}{5}(?:-\p{Nd}{4})?(?=\n?$)
string flags = js.Flags;         // u
bool isExact = js.IsExact;       // true
int warnings = js.Warnings.Count; // 0
string text = js.ToLiteral();    // /^\p{Nd}{5}(?:-\p{Nd}{4})?(?=\n?$)/u

string fluent = Pattern.With.StartOfLine.Digit.Repeat.Times(4).ToJavaScript();
// /^\p{Nd}{4}/u
```

- `ToJavaScript()` is a shortcut for `ToFlavor(RegexFlavor.JavaScript).ToLiteral()`. It throws the same exceptions as `ToFlavor`.
- The output targets ECMAScript 2018 or later and always uses the `u` flag. The flag is needed for `\p{...}`, named groups and lookbehind, which the translation relies on.
- `\d` became `\p{Nd}` and `$` became `(?=\n?$)`. [Exact by default](#exact-by-default) explains why.
- `FlavorConversion.ToString()` returns the same text as `ToLiteral()`, so you can put a conversion straight into an interpolated string.

## The API at a glance

| Member | Returns | What it does |
|--------|---------|--------------|
| `Pattern.ToJavaScript()` | `string` | The JavaScript regex literal, with delimiters and flags. |
| `Pattern.ToFlavor(RegexFlavor flavor, FlavorOptions? options = null)` | `FlavorConversion` | Translates the pattern, including its `RegexOptions`, to the target dialect. `options` controls exactness for JavaScript and JSON Schema; `null` means the exact defaults. The SQL flavors ignore it. |
| `PatternExport.ToTypeScript(IReadOnlyDictionary<string, Pattern> patterns, string? header = null)` | `string` | A TypeScript (and JavaScript) module with one exported regex constant per pattern. See [TypeScript modules](#typescript-modules). |
| `FlavorOptions` | | Trades exactness for idiomatic output. See [Idiomatic output](#idiomatic-output). |

`ToFlavor` throws:

| Exception | Thrown when |
|-----------|-------------|
| `NotSupportedException` | The pattern uses a construct the target cannot express. The message lists every such construct with its position. See [Unsupported constructs](#unsupported-constructs). |
| `ArgumentOutOfRangeException` | `flavor` is not a defined `RegexFlavor` value. |

### The conversion result

`FlavorConversion` holds everything the translation found out:

| Member | Type | Meaning |
|--------|------|---------|
| `Flavor` | `RegexFlavor` | The target dialect. |
| `Pattern` | `string` | The translated pattern, without delimiters or flags. |
| `Flags` | `string` | The flags to compile it with, such as `"iu"`. Always empty for JSON Schema. |
| `Warnings` | `IReadOnlyList<string>` | Every difference in which strings match. Empty when the translation is exact. Never `null`. |
| `IsExact` | `bool` | `true` when `Warnings` is empty: the translation accepts the same strings as .NET. |
| `MatchExtentWarnings` | `IReadOnlyList<string>` | Differences in which substring is matched or what a group captures. See [Match extent warnings](#match-extent-warnings). Never `null`. |
| `PreservesMatchExtent` | `bool` | `true` when `MatchExtentWarnings` is empty. |
| `GroupNumbers` | `IReadOnlyDictionary<string, int>` | The JavaScript number of every .NET capture group, keyed by group name and by .NET number. See [Group names and numbers](#group-names-and-numbers). |
| `IsSql` | `bool` | `true` for the SQL flavors. Always `false` for JavaScript and JSON Schema. |
| `ToLiteral()` | `string` | The translation as source code: `/.../flags` for JavaScript, a quoted JSON string for JSON Schema. |

The two flavors on this page are:

| `RegexFlavor` | Target |
|---------------|--------|
| `JavaScript` | ECMAScript 2018+ as used by JavaScript and TypeScript, always with the `u` flag. |
| `JsonSchema` | The `pattern` keyword of JSON Schema and OpenAPI: ECMA-262 syntax without flags. The output is valid with the `u` flag. |

## Exact by default

By default every construct whose meaning differs between the two engines is written out so that it keeps the .NET meaning:

| .NET | JavaScript output | Why |
|------|-------------------|-----|
| `\d`, `\D` | `\p{Nd}`, `\P{Nd}` | JavaScript `\d` is ASCII-only. .NET `\d` matches every Unicode decimal digit. |
| `\w`, `\W` | `[\p{L}\p{Mn}\p{Nd}\p{Pc}]`, `[^\p{L}\p{Mn}\p{Nd}\p{Pc}]` | The exact .NET definition. JavaScript `\w` is ASCII-only. |
| `\s`, `\S` | `[\t\n\v\f\r\x85\p{Z}]`, `[^\t\n\v\f\r\x85\p{Z}]` | JavaScript `\s` also includes U+FEFF and leaves out U+0085. |
| `\b`, `\B` | Lookarounds over `[\p{L}\p{Mn}\p{Nd}\p{Pc}\u{200C}\u{200D}]` | .NET also treats the zero-width joiners U+200C and U+200D as word characters at a boundary. |
| `$`, `\Z` | `(?=\n?$)` | .NET `$` also matches before a final `\n`. |
| `\A`, `\z` | `^`, `$` | |
| `.` | `[^\n]` | JavaScript `.` also stops at `\r`, U+2028 and U+2029. |
| `(?'name'...)` | `(?<name>...)` | JavaScript has only the angle-bracket form. |
| `\2` (a .NET group number) | `\3` or `\k<name>` | .NET numbers unnamed groups first. See [Group names and numbers](#group-names-and-numbers). |
| `[a-z-[aeiou]]` | `[b-df-hj-np-tv-z]` | Class subtraction is expanded exactly. |
| `\p{IsBasicLatin}` | `[\x00-\x7F]` | Unicode blocks are expanded to code point ranges. |
| `ab(?i:cd)` | `ab(?:[Cc][Dd])` | Case-insensitivity that covers only part of the pattern is expanded into classes. |
| `/` | `\/` | Escaped so the literal stays valid. |
| `(?#...)`, comments and whitespace under `IgnorePatternWhitespace` | Removed | |

```csharp
string word = Pattern.FromRegex(@"^\w+$").ToJavaScript();
// /^[\p{L}\p{Mn}\p{Nd}\p{Pc}]+(?=\n?$)/u

string space = Pattern.FromRegex(@"\s").ToJavaScript();             // /[\t\n\v\f\r\x85\p{Z}]/u
string dot = Pattern.FromRegex("a.b").ToJavaScript();               // /a[^\n]b/u
string input = Pattern.FromRegex(@"\Aabc\z").ToJavaScript();        // /^abc$/u
string named = Pattern.FromRegex(@"(?'year'\d{4})").ToJavaScript(); // /(?<year>\p{Nd}{4})/u
string vowels = Pattern.FromRegex("[a-z-[aeiou]]").ToJavaScript();  // /[b-df-hj-np-tv-z]/u
string ascii = Pattern.FromRegex(@"\p{IsBasicLatin}+").ToJavaScript(); // /[\x00-\x7F]+/u
string scoped = Pattern.FromRegex("ab(?i:cd)").ToJavaScript();      // /ab(?:[Cc][Dd])/u
string slash = Pattern.FromRegex("^a/b$").ToJavaScript();           // /^a\/b(?=\n?$)/u
string empty = Pattern.With.ToJavaScript();                         // /(?:)/u
```

- An empty pattern is written as `(?:)`, because `//` would start a JavaScript comment.
- A numbered backreference followed by a digit gets an empty group in between, so `\1` followed by `0` never reads as `\10`.
- A quantified lookaround is wrapped in `(?:...)`, because JavaScript with the `u` flag cannot quantify an assertion directly. `(?<=a)?b` becomes `/(?:(?<=a))?b/u`.

### The same answers on both sides

Because the meaning is kept, the exported regex gives the same answers as `IsMatch` in .NET:

```csharp
Pattern zip = CommonPatterns.ZipCode();

bool plain = zip.IsMatch("12345");          // true
bool finalNewline = zip.IsMatch("12345\n"); // true: .NET '$' matches before a final \n
bool arabic = zip.IsMatch("١٢٣٤٥");         // true: Arabic-Indic digits are \d in .NET
bool shortPlus4 = zip.IsMatch("12345-678"); // false
```

```ts
const zip = /^\p{Nd}{5}(?:-\p{Nd}{4})?(?=\n?$)/u;

zip.test("12345");     // true
zip.test("12345\n");   // true
zip.test("١٢٣٤٥");     // true
zip.test("12345-678"); // false
```

A hand-written `/^\d{5}(?:-\d{4})?$/u` returns `false` for both `"12345\n"` and `"١٢٣٤٥"`, so client and server would disagree.

:::tip[Want ASCII digits on both sides?]

If your rule really means `0-9`, say so in the pattern instead of loosening the export. `WithAsciiDigits()` rewrites every `\d` as `[0-9]`, and the export stays exact:

```csharp
string digits = Pattern.FromRegex(@"^\d{5}$").WithAsciiDigits().ToJavaScript();
// /^[0-9]{5}(?=\n?$)/u
```

:::

## Options become flags

The pattern's `RegexOptions`, and inline options such as `(?i)`, are part of the translation:

| .NET option | JavaScript output |
|-------------|-------------------|
| `IgnoreCase` | The `i` flag when every case-sensitive part of the pattern is case-insensitive. Otherwise each case-insensitive part is expanded into classes, such as `[Aa]`. |
| `Singleline` | The `s` flag when every `.` is in single-line mode. Otherwise each single-line `.` becomes `[\s\S]`. |
| `Multiline` | `^` becomes `(?<=^\|\n)` and `$` becomes `(?=\n\|$)`. These keep the .NET meaning without the `m` flag. |
| `ExplicitCapture` | Unnamed groups become non-capturing `(?:...)`. |
| `IgnorePatternWhitespace` | Whitespace and `#` comments are removed. |
| `ECMAScript` | `\d`, `\w` and `\b` are written as is, because .NET already gives them the ASCII meaning. `\s` becomes `[ \f\n\r\t\v]`, its .NET meaning under this option. |
| `CultureInvariant` | Picks the case rules used for expansion. See [Case-insensitivity and culture](#case-insensitivity-and-culture). |
| `RightToLeft` | Not supported. Throws `NotSupportedException`. |

```csharp
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

string greeting = Pattern.FromRegex("^hello").WithOptions(RegexOptions.IgnoreCase).ToJavaScript();
// /^hello/iu

string anyChar = Pattern.FromRegex("a.c").WithOptions(RegexOptions.Singleline).ToJavaScript();
// /a.c/su

string lines = Pattern.FromRegex("^ab$").WithOptions(RegexOptions.Multiline).ToJavaScript();
// /(?<=^|\n)ab(?=\n|$)/u

string mixedCase = Pattern.FromRegex("(?i)ab(?-i:c)").ToJavaScript();  // /[Aa][Bb](?:c)/u
string digitsOnly = Pattern.FromRegex(@"(?i)\d+").ToJavaScript();      // /\p{Nd}+/u
string mixedDots = Pattern.FromRegex("a(?s:.)b.").ToJavaScript();      // /a(?:[\s\S])b[^\n]/u

string ecma = Pattern.FromRegex(@"\d\w\s").WithOptions(RegexOptions.ECMAScript).ToJavaScript();
// /\d\w[ \f\n\r\t\v]/u

string explicitCapture = Pattern.FromRegex("(a)(?<x>b)").WithOptions(RegexOptions.ExplicitCapture).ToJavaScript();
// /(?:a)(?<x>b)/u

string spaced = Pattern.FromRegex("a b # comment\n c").WithOptions(RegexOptions.IgnorePatternWhitespace).ToJavaScript();
// /abc/u
```

- Parts that case does not affect, such as digits and punctuation, do not count. `(?i)\d+` gets no `i` flag, because `\d` is the same either way.
- `(?i)ab(?-i:c)` mixes case sensitivity, so no `i` flag is possible. The case-insensitive letters are expanded instead, and the result is exact.
- Using the `i` flag adds a warning. See [Warnings](#warnings).

## Group names and numbers

.NET numbers unnamed groups first and named groups after them. JavaScript numbers all groups from left to right. The translation rewrites backreferences to match, and `GroupNumbers` tells you where each .NET group ended up:

```csharp
FlavorConversion date = Pattern.FromRegex(@"(?<year>\d{4})-(\d{2})").ToFlavor(RegexFlavor.JavaScript);

string literal = date.ToLiteral();      // /(?<year>\p{Nd}{4})-(\p{Nd}{2})/u
int year = date.GroupNumbers["year"];   // 1
int netGroup1 = date.GroupNumbers["1"]; // 2: .NET group 1 is (\d{2}), JavaScript group 2
int netGroup2 = date.GroupNumbers["2"]; // 1: .NET group 2 is year, JavaScript group 1
string warning = date.Warnings[0];
// Capture group numbers differ: .NET numbers unnamed groups before named ones, JavaScript numbers
// all groups left to right. Refer to groups by name.

string backreferences = Pattern.FromRegex(@"(a)(?<n>b)(c)\1\2\3").ToJavaScript();
// /(a)(?<n>b)(c)\1\3\k<n>/u
```

- `GroupNumbers` has one key per group name and one per .NET group number, written as a string. The whole match, group 0, is left out: it is group 0 everywhere.
- A backreference to a named group is written as `\k<name>`. A backreference to an unnamed group is renumbered, so `\2` (the .NET number of `(c)`) became `\3`.
- The numbering warning makes `IsExact` `false`, even though the regex accepts the same strings. Only the numbers you read from the match differ. Read groups by name (`match.groups.year` in JavaScript) and the difference does not matter.
- Group names must be valid JavaScript identifiers. A numeric name such as `(?<5>a)` cannot be translated.

## Warnings

Anything that still differs is reported in `Warnings`, and `IsExact` is then `false`. Warnings are plain sentences meant for a person. Each one is listed once, and positions are 0-based offsets in the .NET pattern text (`pattern.ToString()`).

```csharp
FlavorConversion caseless = Pattern.FromRegex(@"(?i)^[a-z]+\d$").ToFlavor(RegexFlavor.JavaScript);

string literal = caseless.ToLiteral(); // /^[a-z]+\p{Nd}(?=\n?$)/iu
bool isExact = caseless.IsExact;       // false
string warning = caseless.Warnings[0];
// Case-insensitive matching: JavaScript's 'i' flag uses Unicode simple case folding, which also equates
// characters .NET keeps distinct (for example 's' and U+017F, U+00B5 and U+03BC).
// RegexFlavor.JsonSchema expands case into exact character classes instead.

bool longS = Pattern.FromRegex(@"(?i)^[a-z]+\d$").IsMatch("\u017F1"); // false in .NET
```

In JavaScript, `/^[a-z]+\p{Nd}(?=\n?$)/iu.test("\u017F1")` returns `true`: the `i` flag treats U+017F (long s) as `s`. That is the difference the warning describes.

These are all the warnings the JavaScript and JSON Schema translation can report:

| Warning starts with | Raised when | What differs |
|---------------------|-------------|--------------|
| `Case-insensitive matching` | The pattern compiles with the `i` flag (JavaScript only). | JavaScript folds a few extra characters together, such as `s` and U+017F. |
| `Capture group numbers differ` | An unnamed group comes after a named one. | Group numbers, not which strings match. |
| `Supplementary characters such as emoji` | The pattern has `.`, a negated class such as `[^a]`, `\S`, `\W`, `\D` or `\P{...}`, or a literal surrogate. | .NET matches one UTF-16 code unit, JavaScript with `u` a whole code point. |
| `The backreference at position N refers to a group that may not have participated` | The referenced group can be skipped, as in `(a)?\1`. | .NET fails to match, JavaScript matches the empty string. |
| `'\d' matches only ASCII digits`, `'\w' and '\b' use ASCII word characters`, `'\s' differs` | `PreserveUnicodeSemantics` is `false`. | See [Idiomatic output](#idiomatic-output). |
| `'.' at position N also stops at \r`, `'$' at position N` | `PreserveLineSemantics` is `false`. | See [Idiomatic output](#idiomatic-output). |
| `With the 'm' flag` | `PreserveLineSemantics` is `false` and the pattern uses `Multiline`. | JavaScript also treats `\r`, U+2028 and U+2029 as line breaks. |

Two more examples:

```csharp
FlavorConversion optional = Pattern.FromRegex(@"(a)?\1").ToFlavor(RegexFlavor.JavaScript);
string backreference = optional.Warnings[0];
// The backreference at position 4 refers to a group that may not have participated;
// .NET then fails to match, JavaScript matches the empty string.

FlavorConversion upToThree = Pattern.FromRegex("^.{1,3}$").ToFlavor(RegexFlavor.JavaScript);
string literal = upToThree.ToLiteral(); // /^[^\n]{1,3}(?=\n?$)/u
string emoji = upToThree.Warnings[0];
// Supplementary characters such as emoji: the construct(s) at position 1 match one UTF-16 code unit
// in .NET but a whole code point in JavaScript with the 'u' flag.
```

- On `"b"`, .NET's `(a)?\1` returns `false`, while JavaScript's `/(a)?\1/u` returns `true`.
- On `"😀😀"`, .NET's `^.{1,3}$` returns `false`, because each emoji is two UTF-16 code units, so the text is four characters long. JavaScript returns `true`, because it sees two code points.
- The supplementary-character warning lists up to eight positions, then `...`.

:::note

`IsExact` covers text in the Basic Multilingual Plane (U+0000 to U+FFFF). There is one difference it does not report: a positive Unicode class such as `\p{L}` can match a supplementary letter (U+10000 and above) in JavaScript, while .NET sees that letter as two surrogates and does not match it. Constructs that can match a lone surrogate in .NET, such as `.` or `[^a]`, always produce the warning above.

:::

## Match extent warnings

`Warnings` and `IsExact` answer one question: does a string match? When you also use the regex to extract or replace text, with `exec`, `match` or `replace` in JavaScript, a second question matters: which substring is matched, and what does each group capture? Those differences are reported separately, in `MatchExtentWarnings`:

```csharp
FlavorConversion repeat = Pattern.FromRegex("(?:(a)|b)+").ToFlavor(RegexFlavor.JavaScript);

bool isExact = repeat.IsExact;                  // true
bool sameExtent = repeat.PreservesMatchExtent;  // false
string extent = repeat.MatchExtentWarnings[0];
// The group at position 3 can be skipped by an iteration of an enclosing repeat: .NET keeps its
// capture from an earlier iteration, JavaScript resets it on every iteration.

string netCapture = Pattern.FromRegex("(?:(a)|b)+").Match("ab").Groups[1].Value; // a

FlavorConversion nullable = Pattern.FromRegex("(a?)+").ToFlavor(RegexFlavor.JavaScript);
string emptyIteration = nullable.MatchExtentWarnings[0];
// The repeat at position 0 can run an iteration that matches empty text: .NET stops the repeat there,
// while JavaScript refuses empty iterations and tries the body's other ways, so the match and the
// captures can differ.

string netEmpty = Pattern.FromRegex("(a?)+").Match("aa").Groups[1].Value; // "" (empty)
```

In Node.js, `/(?:(a)|b)+/u.exec("ab")[1]` is `undefined` and `/(a?)+/u.exec("aa")[1]` is `"a"`.

- A pattern can be exact and still have match extent warnings, as here. If you only call `test()` in JavaScript or use the pattern in a JSON Schema, you can ignore them.
- The analysis is conservative. A warning means a difference is possible, not that it happens for every input.

## Idiomatic output

Sometimes you prefer the plain JavaScript constructs and accept the differences, for example to keep a short pattern readable in a code review. `FlavorOptions` lets you opt out of each kind of preservation:

| Property | Default | When `false` |
|----------|---------|--------------|
| `PreserveUnicodeSemantics` | `true` | `\d`, `\w`, `\s` and `\b` are written as their ASCII JavaScript namesakes, with a warning for each kind. |
| `PreserveLineSemantics` | `true` | `$` is written as `$`, `.` as `.`, and `Multiline` patterns use the `m` flag, with a warning for each. |

```csharp
FlavorOptions idiomatic = new FlavorOptions
{
    PreserveUnicodeSemantics = false,
    PreserveLineSemantics = false,
};

FlavorConversion zip = CommonPatterns.ZipCode().ToFlavor(RegexFlavor.JavaScript, idiomatic);
string literal = zip.ToLiteral(); // /^\d{5}(?:-\d{4})?$/u
// zip.Warnings:
// '\d' matches only ASCII digits in JavaScript; .NET also matches other Unicode decimal digits.
// '$' at position 17: .NET also matches before a final '\n'; JavaScript only at the very end.

FlavorConversion lines = Pattern.FromRegex("^ab$")
    .WithOptions(RegexOptions.Multiline)
    .ToFlavor(RegexFlavor.JavaScript, new FlavorOptions { PreserveLineSemantics = false });
string multiline = lines.ToLiteral(); // /^ab$/mu
// lines.Warnings[0]: With the 'm' flag JavaScript's '^' and '$' also treat \r, U+2028 and U+2029
// as line breaks; .NET only uses '\n'.

FlavorConversion word = Pattern.FromRegex(@"\bcat\b")
    .ToFlavor(RegexFlavor.JavaScript, new FlavorOptions { PreserveUnicodeSemantics = false });
string boundary = word.ToLiteral(); // /\bcat\b/u
// word.Warnings[0]: '\w' and '\b' use ASCII word characters in JavaScript; .NET uses Unicode
// letters, digits and connectors.
```

- The options only loosen the translation. They never make an unsupported construct supported.
- `FlavorOptions` applies to `RegexFlavor.JavaScript` and `RegexFlavor.JsonSchema`. The SQL flavors ignore it and always keep the .NET meaning.
- A `.` in single-line mode is not affected by `PreserveLineSemantics`. It is always exact.
- With the `m` flag, `^` and `$` mean line positions in JavaScript. So `\A` and `\z` in the same pattern are written as `(?<![\s\S])` and `(?![\s\S])`: `\A^a$\z` under `Multiline` becomes `/(?<![\s\S])^a$(?![\s\S])/mu`.
- JSON Schema has no flags, so it never gets the `m` flag. Its multiline anchors stay `(?<=^|\n)` and `(?=\n|$)` even when `PreserveLineSemantics` is `false`.
- `PatternExport.ToTypeScript` and `ToJavaScript()` always use the exact defaults.

:::warning

Idiomatic output can accept strings that .NET rejects, and reject strings that .NET accepts. If the browser validates with it and the server validates with .NET, users can pass one check and fail the other. Prefer the exact default for validation rules.

:::

## JSON Schema

`RegexFlavor.JsonSchema` produces a value for the `pattern` keyword of JSON Schema and OpenAPI. A schema pattern has no flags, so case-insensitivity and dot-all mode are expanded into classes exactly as .NET defines them:

```csharp
// Run under an English current culture (en-US).
FlavorConversion schema = Pattern.FromRegex(@"(?i)^[a-z]+\d$").ToFlavor(RegexFlavor.JsonSchema);

string pattern = schema.Pattern; // ^[A-Za-z\u0130\u212A]+\p{Nd}(?=\n?$)
string literal = schema.ToLiteral(); // "^[A-Za-z\\u0130\\u212A]+\\p{Nd}(?=\\n?$)"
string flags = schema.Flags;     // "" (empty)
bool isExact = schema.IsExact;   // true

string dotAll = Pattern.FromRegex("^a.c$")
    .WithOptions(RegexOptions.Singleline)
    .ToFlavor(RegexFlavor.JsonSchema)
    .Pattern; // ^a[\s\S]c(?=\n?$)

FlavorConversion fraction = Pattern.FromRegex(@"^\d+/\d+$").ToFlavor(RegexFlavor.JsonSchema);
string unescaped = fraction.Pattern;   // ^\p{Nd}+/\p{Nd}+(?=\n?$)
string quoted = fraction.ToLiteral();  // "^\\p{Nd}+/\\p{Nd}+(?=\\n?$)"
```

- `ToLiteral()` returns a quoted JSON string, with `"` and `\` escaped and control characters written as `\u00XX`. Paste it into a JSON document as is. Use `Pattern` when a JSON serializer writes the value for you.
- `/` is not escaped, because a schema pattern has no delimiters.
- The case-insensitive translation is exact, unlike the JavaScript `i` flag. .NET's `IgnoreCase` also equates `k` with U+212A (Kelvin sign), so that is always in the class. U+0130 (capital I with dot) is there because the sample ran under an English culture. The expansion follows the current culture: see [Case-insensitivity and culture](#case-insensitivity-and-culture).
- `Multiline` is written with the same lookarounds as for JavaScript: `^a$` becomes `(?<=^|\n)a(?=\n|$)`.

The literal goes straight into a schema:

```json
{
  "type": "object",
  "properties": {
    "code": {
      "type": "string",
      "pattern": "^[A-Za-z\\u0130\\u212A]+\\p{Nd}(?=\\n?$)"
    }
  }
}
```

:::note

The output uses `\p{...}`, which needs a validator that compiles patterns with the `u` flag. Ajv does by default. If you compile the pattern yourself in JavaScript, pass the flag: `new RegExp(schema.pattern, "u")`. JSON Schema `pattern` is a search, like `Regex.IsMatch`, so keep your anchors.

:::

With the expanded class, JavaScript agrees with .NET again: `new RegExp("^[A-Za-z\\u0130\\u212A]+\\p{Nd}(?=\\n?$)", "u").test("\u017F1")` returns `false`, and `.test("Abc1")` returns `true`.

### Case-insensitivity and culture

.NET's case rules depend on the culture. Case-insensitive parts are expanded with the equivalences .NET's `Regex` would use if the pattern were created at the moment of the call: those of `CultureInfo.CurrentCulture`, or the invariant ones when the pattern has `RegexOptions.CultureInvariant`. Only Turkish and Azerbaijani (dotted and dotless i) and the invariant culture differ from the rest:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern id = Pattern.FromRegex("(?i)^id$");

CultureInfo.CurrentCulture = new CultureInfo("en-US");
string english = id.ToFlavor(RegexFlavor.JsonSchema).Pattern;   // ^[Ii\u0130][Dd](?=\n?$)

CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
string turkish = id.ToFlavor(RegexFlavor.JsonSchema).Pattern;   // ^[i\u0130][Dd](?=\n?$)

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
string invariant = id.ToFlavor(RegexFlavor.JsonSchema).Pattern; // ^[Ii][Dd](?=\n?$)

string pinned = Pattern.FromRegex("(?i)^id$")
    .WithOptions(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    .ToFlavor(RegexFlavor.JsonSchema)
    .Pattern; // ^[Ii][Dd](?=\n?$), in every culture
```

The same rule changes larger classes. `(?i)^[a-z]+\d$` from the [JSON Schema](#json-schema) sample gives these classes:

| Current culture | Class |
|-----------------|-------|
| English (and most others) | `[A-Za-zİK]` |
| Turkish (`tr-TR`) | `[A-HJ-Za-zİK]`: capital `I` is not the uppercase of `i` |
| Invariant | `[A-Za-zK]`: no dotted capital I |

The Kelvin sign U+212A is in every case. The JavaScript `i` flag does not depend on the culture, so a pattern that uses the flag gives the same output everywhere.

:::tip

If you generate schemas or modules on build servers, add `RegexOptions.CultureInvariant` to case-insensitive patterns. The generated files then do not change with the machine's culture.

:::

## Unsupported constructs

When the target cannot express a construct, `ToFlavor` throws one `NotSupportedException` that lists every such construct with its position. It never silently changes the meaning:

```csharp
using System;
using Masterly.FluentRegex;

try
{
    Pattern.FromRegex(@"(?>\d+)-\G(?<x>a)(?<x>b)").ToFlavor(RegexFlavor.JavaScript);
}
catch (NotSupportedException ex)
{
    Console.WriteLine(ex.Message);
}
// prints:
// The pattern cannot be converted to JavaScript without changing its meaning:
//   - atomic group '(?>...)' at position 0
//   - '\G' (contiguous-match anchor) at position 8
//   - duplicate group name 'x' at position 17
```

| Construct | Example | Reported as |
|-----------|---------|-------------|
| Atomic group | `(?>\d+)` | `atomic group '(?>...)'` |
| Conditional | `(?(x)b\|c)` | `conditional '(?(x)...)'` |
| Balancing group | `(?<open>\()(?<close-open>\))` | `balancing group '(?<close-open>...)'` |
| Contiguous-match anchor | `\G` | `'\G' (contiguous-match anchor)` |
| Duplicate group name | `(?<x>a)(?<x>b)` | `duplicate group name 'x'` |
| Group name that is not a JavaScript identifier | `(?<5>a)` | `group name '5' (not a valid JavaScript identifier)` |
| Backreference to an undefined group | `(a)\k<0>` | `backreference to an undefined group` |
| Case-insensitive backreference without the `i` flag | `(a)(?i:\1)b` | `case-insensitive backreference (cannot be expressed in a pattern that mixes case sensitivity)` |
| `RegexOptions.RightToLeft` | | `RegexOptions.RightToLeft (ECMAScript only matches left to right)` |

- A case-insensitive backreference works in JavaScript when the whole pattern can use the `i` flag: `(?i)(a)\1` becomes `/(a)\1/iu`. JSON Schema has no flags, so it always throws there: `case-insensitive backreference (cannot be expressed in JSON Schema, which has no flags)`.
- For JSON Schema the message starts with `The pattern cannot be converted to JSON Schema`.
- .NET has no possessive quantifiers, so none can occur.
- Every `CommonPatterns` member, called with its default arguments, converts to both JavaScript and JSON Schema without an exception.

:::tip

Atomic groups are often added only for speed or [ReDoS safety](./redos-safety.md). If the [optimizer](./optimizer.md) or a rewrite without the atomic group gives the same language, export that version. [Pattern algebra](./pattern-algebra.md) can prove that the two patterns are equivalent.

:::

## TypeScript modules

`PatternExport.ToTypeScript` writes a whole source file: one exported constant per pattern, sorted by name with ordinal comparison. Each constant is documented with its .NET source and any warnings. The file is valid TypeScript and plain JavaScript.

```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Dictionary<string, Pattern> patterns = new Dictionary<string, Pattern>
{
    ["sku"] = Pattern.With.StartOfLine.UppercaseLetter.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4).EndOfLine,
    ["slug"] = CommonPatterns.Slug(),
    ["greeting"] = Pattern.FromRegex("^hello").WithOptions(RegexOptions.IgnoreCase),
};

string module = PatternExport.ToTypeScript(patterns);
```

The result:

```ts
// Generated by Masterly.FluentRegex. Do not edit by hand.

/**
 * .NET: ^hello
 * Warning: Case-insensitive matching: JavaScript's 'i' flag uses Unicode simple case folding, which also equates characters .NET keeps distinct (for example 's' and U+017F, U+00B5 and U+03BC). RegexFlavor.JsonSchema expands case into exact character classes instead.
 */
export const greeting = /^hello/iu;
/**
 * .NET: ^[A-Z]{3}-\d{4}$
 */
export const sku = /^[A-Z]{3}-\p{Nd}{4}(?=\n?$)/u;
/**
 * .NET: ^[a-z\d]+(?:-[a-z\d]+)*$
 */
export const slug = /^[a-z\p{Nd}]+(?:-[a-z\p{Nd}]+)*(?=\n?$)/u;
```

- The `.NET:` line is `pattern.ToString()`. Options set with `WithOptions` are not part of that text, so `greeting` shows `^hello`. The flag in the literal and the warning show that it is case-insensitive.
- Line endings are always `\n`. Text that would end the comment early (`*/`) is escaped, and line breaks inside a comment line are written as `\r` and `\n`.
- Match extent warnings are not written to the file. Check `ToFlavor(RegexFlavor.JavaScript).MatchExtentWarnings` if the front end extracts or replaces text with a pattern.
- An empty dictionary produces only the header.

### Custom header

Pass `header` to replace the generated notice. Each line becomes a `//` comment. Pass an empty string for no header at all:

```csharp
string module = PatternExport.ToTypeScript(
    new Dictionary<string, Pattern> { ["postalCode"] = CommonPatterns.ZipCode() },
    header: "Shared validation rules.\nRegenerate with: dotnet run --project tools/ExportRules");
```

```ts
// Shared validation rules.
// Regenerate with: dotnet run --project tools/ExportRules

/**
 * .NET: ^\d{5}(?:-\d{4})?$
 */
export const postalCode = /^\p{Nd}{5}(?:-\p{Nd}{4})?(?=\n?$)/u;
```

- `\r\n` in the header is treated as one line break.
- `header: null`, the default, writes `Generated by Masterly.FluentRegex. Do not edit by hand.`

### Names and errors

| Exception | Thrown when |
|-----------|-------------|
| `ArgumentNullException` | `patterns` is `null`. |
| `ArgumentException` | A name is not a valid identifier or is a reserved word, or a pattern is `null`. |
| `NotSupportedException` | A pattern cannot be expressed in JavaScript. The message names the constant. |

```csharp
try
{
    PatternExport.ToTypeScript(new Dictionary<string, Pattern> { ["counter"] = Pattern.FromRegex(@"(?>\d+)") });
}
catch (NotSupportedException ex)
{
    Console.WriteLine(ex.Message);
}
// prints:
// The pattern 'counter' cannot be exported: The pattern cannot be converted to JavaScript without changing its meaning:
//   - atomic group '(?>...)' at position 0
```

- A valid name starts with a letter, `$`, `_` or a letter number, and continues with those, digits, combining marks or connector punctuation. `postalCode`, `$x`, `_x` and `a1` are valid. `1abc`, `has-dash` and the empty string are not.
- Reserved words such as `class`, `const`, `default` or `enum` throw: `'class' is not a valid TypeScript identifier. (Parameter 'patterns')`.
- The original `NotSupportedException` is kept as `InnerException`.

### Using the module

Import the constants in your front end. They give the same answers as the .NET patterns:

```ts
import { greeting, sku, slug } from "./patterns";

sku.test("ABC-1234");      // true
sku.test("ABC-1234\n");    // true, like .NET
sku.test("abc-1234");      // false
slug.test("hello-world");  // true
greeting.test("HELLO there"); // true
```

A typical setup generates the file in a small console app or a test, and fails the build when the checked-in file is out of date. That way the server's patterns stay the single source of truth.

## From the command line

The [command-line tool](./command-line-tool.md) exposes the same translation:

```bash
fluentregex export '^\d{5}$'
# /^\p{Nd}{5}(?=\n?$)/u

fluentregex export '(?i)^[a-z]+\d$' --to json-schema
# ^[A-Za-z\u0130\u212A]+\p{Nd}(?=\n?$)

fluentregex export '^[A-Z]{3}-\d{4}$' --to typescript --name sku
# // Generated by Masterly.FluentRegex. Do not edit by hand.
#
# /**
#  * .NET: ^[A-Z]{3}-\d{4}$
#  */
# export const sku = /^[A-Z]{3}-\p{Nd}{4}(?=\n?$)/u;

fluentregex export 'abc' --options IgnoreCase
# /abc/iu
# warning: Case-insensitive matching: JavaScript's 'i' flag uses Unicode simple case folding, ...

fluentregex export '\d' --ascii-digits
# /[0-9]/u
```

- `--to` accepts `js` (the default, also `javascript`), `json-schema` (also `jsonschema`) and `typescript`, plus the SQL targets.
- For `json-schema` the tool prints the bare pattern, not the quoted JSON string.
- `--name` sets the constant name for `typescript`. It defaults to `pattern`.
- For `js` and `json-schema`, warnings are printed after the result, each starting with `warning:`. The exit code stays 0. For `typescript` the warnings appear only as `Warning:` lines inside the module comments.
- An unknown `--to` value prints `error:` with the list of targets and exits with code 2.
- An unsupported construct prints `error:` with the full list and exits with code 2.

The [Playground](./playground.md) shows the JavaScript and JSON Schema translations of the pattern you type, with their warnings.

## Tips and pitfalls

- **Keep the exact default for validation.** Its output is longer than a hand-written regex, but client and server then accept the same strings.
- **Check `IsExact` in a test.** A unit test that asserts `IsExact` for every exported rule catches a new warning as soon as someone changes a pattern.
- **Read groups by name.** Group numbers can differ between .NET and JavaScript. Names never do.
- **Use `test()` freely, `exec()` with care.** When `PreservesMatchExtent` is `false`, the match and its captures can differ even though the same strings match.
- **Anchor JSON Schema patterns.** Like `Regex.IsMatch`, the schema `pattern` keyword searches anywhere in the string.
- **Pin the culture for generated files.** Add `RegexOptions.CultureInvariant` to case-insensitive patterns, as described in [Case-insensitivity and culture](#case-insensitivity-and-culture).
- **Say `0-9` when you mean it.** `WithAsciiDigits()` gives an exact, short `[0-9]` instead of `\p{Nd}`.

## See also

- [SQL regex dialects](./sql-dialects.md): the same `ToFlavor` call for PostgreSQL, MySQL, MariaDB, SQL Server and SQLite.
- [SQL LIKE](./sql-like.md): compile patterns to `LIKE` with a proof of exactness.
- [ASP.NET Core](./aspnetcore.md): register the same named patterns as route constraints on the server.
- [Unicode](./unicode.md): why `\d` and `\w` cover more than ASCII in .NET.
- [Common patterns](./common-patterns.md): ready-made patterns that all export to both flavors.
