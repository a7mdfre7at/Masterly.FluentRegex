# Export to JavaScript, JSON Schema and TypeScript

New in 3.1. Write a validation rule once in C# and run the same rule in the browser, in Node.js and
in your OpenAPI / JSON Schema documents. Every output below was produced by the library, and a sample
of conversions (all `CommonPatterns` plus about 60 hand-picked constructs, against hundreds of inputs)
was cross-checked in Node.js 26 against the .NET engine.

The same `ToFlavor` call also targets databases: see [SQL regex flavors](sql-flavors.md) for PostgreSQL,
MySQL, MariaDB, SQL Server and SQLite, [LIKE compilation with proof](like.md) for databases without a
regex engine, and [Entity Framework Core](entity-framework.md) to use patterns directly in LINQ.

```csharp
CommonPatterns.Email().ToJavaScript();
// /^[a-zA-Z\p{Nd}._\-]+@[a-zA-Z\p{Nd}.\-]+\.[a-zA-Z]{2,}(?=\n?$)/u

FlavorConversion js = CommonPatterns.ZipCode().ToFlavor(RegexFlavor.JavaScript);
js.Pattern;     // ^\p{Nd}{5}(?:-\p{Nd}{4})?(?=\n?$)
js.Flags;       // u
js.IsExact;     // true
js.Warnings;    // empty
js.ToLiteral(); // /^\p{Nd}{5}(?:-\p{Nd}{4})?(?=\n?$)/u
```

The target is ECMAScript 2018+ with the `u` flag (needed for `\p{...}`, named groups and lookbehind).

## Exact by default

.NET and JavaScript disagree on several everyday constructs. The translation keeps the .NET meaning:

| .NET | JavaScript output | Why |
|---|---|---|
| `\d`, `\D` | `\p{Nd}`, `\P{Nd}` | JS `\d` is ASCII-only |
| `\w`, `\W` | `[\p{L}\p{Mn}\p{Nd}\p{Pc}]`, `[^...]` | the exact .NET definition; JS `\w` is ASCII-only |
| `\s`, `\S` | `[\t\n\v\f\r\x85\p{Z}]`, `[^...]` | JS includes U+FEFF and excludes U+0085 |
| `\b`, `\B` | lookarounds over `[\p{L}\p{Mn}\p{Nd}\p{Pc}\u{200C}\u{200D}]` | .NET also treats ZWNJ/ZWJ as word characters |
| `$` | `(?=\n?$)` | .NET `$` also matches before a final `\n` |
| `\A`, `\z`, `\Z` | `^`, `$`, `(?=\n?$)` | |
| `.` | `[^\n]` (or `.` with the `s` flag) | JS `.` also stops at `\r`, U+2028, U+2029 |
| `(?'n'...)` | `(?<n>...)` | |
| `\2` (numbered, .NET order) | `\3` or `\k<name>` | .NET numbers unnamed groups first |
| `[a-z-[aeiou]]` | `[b-df-hj-np-tv-z]` | subtraction expanded exactly |
| `\p{IsBasicLatin}` | `[\x00-\x7F]` | Unicode blocks expanded to ranges |
| `ab(?i:cd)` | `ab(?:[Cc][Dd])` | scoped case-insensitivity expanded into classes |
| `(?#...)`, `x`-mode whitespace and comments | removed | |

Options become flags when they apply to the whole pattern: `IgnoreCase` (or a leading `(?i)`) -> `i`,
`Singleline` -> `s`. `Multiline` anchors are written as `(?<=^|\n)` and `(?=\n|$)`, which keep the .NET
meaning without the `m` flag. `ExplicitCapture` and `IgnorePatternWhitespace` are applied during
translation.

## Warnings

Anything that still differs is reported in `Warnings`, and `IsExact` is `false`:

```csharp
FlavorConversion ci = Pattern.FromRegex(@"(?i)^[a-z]+\d$").ToFlavor(RegexFlavor.JavaScript);
ci.ToLiteral();   // /^[a-z]+\p{Nd}(?=\n?$)/iu
ci.Warnings[0];
// Case-insensitive matching: JavaScript's 'i' flag uses Unicode simple case folding, which also equates
// characters .NET keeps distinct (for example 's' and U+017F, U+00B5 and U+03BC). RegexFlavor.JsonSchema
// expands case into exact character classes instead.
```

Other warnings: a backreference to a group that may not have participated (.NET fails, JavaScript
matches empty), capture numbers that differ (refer to groups by name), and constructs such as `.`,
`[^a]` or `\S` that can match half of a surrogate pair in .NET but a whole emoji in JavaScript.

`IsExact` covers text in the Basic Multilingual Plane. Positive Unicode classes such as `\p{L}` can also
match supplementary letters (U+10000 and above) in JavaScript, which .NET sees as two surrogates.

### Idiomatic output

When you prefer the plain JavaScript constructs and accept the differences, opt out:

```csharp
FlavorOptions loose = new FlavorOptions { PreserveUnicodeSemantics = false, PreserveLineSemantics = false };
FlavorConversion zip = CommonPatterns.ZipCode().ToFlavor(RegexFlavor.JavaScript, loose);
zip.ToLiteral();  // /^\d{5}(?:-\d{4})?$/u
zip.Warnings;
// '\d' matches only ASCII digits in JavaScript; .NET also matches other Unicode decimal digits.
// '$' at position 17: .NET also matches before a final '\n'; JavaScript only at the very end.
```

`PreserveLineSemantics = false` also uses the `m` flag for multiline patterns.

## JSON Schema

`RegexFlavor.JsonSchema` produces a `pattern` value for JSON Schema and OpenAPI. Schemas have no flags,
so case-insensitivity and dot-all are expanded exactly as .NET defines them, and `/` is not escaped.
`ToLiteral()` returns a quoted JSON string:

```csharp
FlavorConversion schema = Pattern.FromRegex(@"(?i)^[a-z]+\d$").ToFlavor(RegexFlavor.JsonSchema);
schema.ToLiteral();  // "^[A-Za-z\\u0130\\u212A]+\\p{Nd}(?=\\n?$)"
schema.Flags;        // ""
schema.IsExact;      // true
```

(.NET's `IgnoreCase` also equates `i` with U+0130 and `k` with the Kelvin sign U+212A, so they are
included.) The output uses `\p{...}`, which needs a validator that compiles patterns with the `u` flag;
Ajv does by default. JSON Schema `pattern` is a search, like `Regex.IsMatch`, so keep your anchors.

## Unsupported constructs

Constructs JavaScript cannot express throw one `NotSupportedException` that lists all of them with
their positions, instead of silently changing the meaning:

```csharp
Pattern.FromRegex(@"(?>\d+)-\G(?<x>a)(?<x>b)").ToFlavor(RegexFlavor.JavaScript);
// NotSupportedException:
// The pattern cannot be converted to JavaScript without changing its meaning:
//   - atomic group '(?>...)' at position 0
//   - '\G' (contiguous-match anchor) at position 8
//   - duplicate group name 'x' at position 17
```

Unsupported: atomic groups, conditionals, balancing groups, `\G`, `RegexOptions.RightToLeft`, duplicate
or numeric group names, and case-insensitive backreferences when the `i` flag cannot be used (always in
JSON Schema). .NET has no possessive quantifiers, so none can occur. Every `CommonPatterns` member
converts to both flavors.

## TypeScript modules

`PatternExport.ToTypeScript` writes one exported constant per pattern, in name order, documented with
the .NET source and any warnings:

```csharp
string module = PatternExport.ToTypeScript(new Dictionary<string, Pattern>
{
    ["sku"] = Pattern.With.StartOfLine.UppercaseLetter.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4).EndOfLine,
    ["slug"] = CommonPatterns.Slug(),
});
```

```ts
// Generated by Masterly.FluentRegex. Do not edit by hand.

/**
 * .NET: ^[A-Z]{3}-\d{4}$
 */
export const sku = /^[A-Z]{3}-\p{Nd}{4}(?=\n?$)/u;
/**
 * .NET: ^[a-z\d]+(?:-[a-z\d]+)*$
 */
export const slug = /^[a-z\p{Nd}]+(?:-[a-z\p{Nd}]+)*(?=\n?$)/u;
```

Names must be valid identifiers and not reserved words (`ArgumentException` otherwise). Pass a
`header` to replace the generated notice, or an empty string for none. A pattern that cannot be
converted throws `NotSupportedException` naming its constant.
