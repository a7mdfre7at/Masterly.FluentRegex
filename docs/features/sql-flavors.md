# SQL regex flavors: PostgreSQL, MySQL, MariaDB, SQL Server and SQLite

`ToFlavor` also targets the regex engines of the major databases, so a `Pattern` can run as a `WHERE`
clause with the same meaning it has in .NET. These translations are the groundwork for the
`Masterly.FluentRegex.EntityFrameworkCore` package; they are equally usable with raw SQL or Dapper.
Every output below was produced by the library, and the PostgreSQL, MySQL, MariaDB and SQL Server
outputs were cross-checked against live engines (see "Verification").

```csharp
FlavorConversion pg = Pattern.FromRegex(@"^[0-9]+$").ToFlavor(RegexFlavor.PostgreSql);
pg.Pattern;      // \A[0-9]+(?=\n?\Z)
pg.Flags;        // ""   (always empty for SQL flavors: options are embedded in the pattern)
pg.IsExact;      // true
pg.ToLiteral();  // '\A[0-9]+(?=\n?\Z)'

Pattern.FromRegex(@"^[0-9]+$").ToSqlCondition("\"Email\"", RegexFlavor.PostgreSql);
// "Email" ~ '\A[0-9]+(?=\n?\Z)'
Pattern.FromRegex(@"^[0-9]+$").ToSqlCondition("Email", RegexFlavor.MySql);
// REGEXP_LIKE(Email, '(?-i)\\A[0-9]+(?=\\n?\\z)')
Pattern.FromRegex(@"^[0-9]+$").ToSqlCondition("Email", RegexFlavor.MariaDb);
// Email REGEXP '(?-i)\\A[0-9]+(?=\\n?\\z)'
Pattern.FromRegex(@"^[0-9]+$").ToSqlCondition("[Email]", RegexFlavor.SqlServer);
// REGEXP_LIKE([Email], '\A[0-9]+\n?\z')
Pattern.FromRegex(@"^[0-9]+$").ToSqlCondition("Email", RegexFlavor.Sqlite);
// Email REGEXP '^[0-9]+$'
```

`columnSql` is inserted verbatim: quote it yourself. The pattern literal comes from `ToLiteral()`, which
doubles quotes and, for MySQL and MariaDB only, doubles backslashes because their string literals process
C escapes (assumes `sql_mode` without `NO_BACKSLASH_ESCAPES`, the default on MySQL 8.4 and MariaDB 11.4;
with that mode on, the doubled backslashes would reach the regex engine and change the pattern).
PostgreSQL output assumes `standard_conforming_strings = on`, the default since 9.1.

## The contract

For every SQL flavor:

- `Flags` is empty. Options live in the pattern text, so a caller can always emit `column <op> 'text'`.
- `IsExact` means the engine accepts exactly the strings .NET does, for inputs in the Basic Multilingual
  Plane. Anything else is a `Warning`. Inputs with supplementary characters (emoji, U+1D7CE MATHEMATICAL
  BOLD DIGIT ZERO, ...) are outside this promise: the engines see one code point where .NET sees two
  UTF-16 code units, so `.`, negated classes and `\P{..}` match such a character once instead of twice,
  `\d`, `\w` and `\p{..}` can match a supplementary digit or letter that .NET does not, and `\b` can move
  next to one. A supplementary character written literally in the pattern is translated as one code
  point (`\x{1F600}`); a lone surrogate in the pattern is unsupported.
- Constructs the engine cannot express throw one `NotSupportedException` listing all of them with their
  positions. A regex that would silently mean something different is never emitted.
- `GroupNumbers` maps every .NET capture group (by name, and by its .NET number as a string) to its 1-based
  number in the converted regex. .NET numbers unnamed groups before named ones; every engine here numbers
  left to right, and PostgreSQL has no names at all:

```csharp
Pattern.FromRegex(@"(?<year>\d{4})-(\d{2})").ToFlavor(RegexFlavor.PostgreSql).GroupNumbers;
// { ["year"] = 1, ["2"] = 1, ["1"] = 2 }
```

`FlavorOptions` is ignored by the SQL flavors: the .NET meaning is always preserved.

## What is written out explicitly

No database engine folds case, defines `\w` or treats `$` the way .NET does, so those are spelled out:

| .NET | PostgreSQL | MySQL (ICU), MariaDB (PCRE2) | SQL Server (RE2) |
|---|---|---|---|
| `(?i)hello` | `[Hh][Ee][Ll][Ll][Oo]` | `(?-i)[Hh][Ee][Ll][Ll][Oo]` | `[Hh][Ee][Ll][Ll][Oo]` |
| `^`, `\A` | `\A` | `\A` | `\A` |
| `$`, `\Z` | `(?=\n?\Z)` | `(?=\n?\z)` | `\n?\z` (only at the end of the pattern) |
| `\z` | `\Z` | `\z` | `\z` |
| `(?m)^` / `(?m)$` | `(?:\A\|(?<=\n))` / `(?=\n\|\Z)` | `(?:\A\|(?<=\n))` / `(?=\n\|\z)` | `(?m:^)` / `(?m:$)` |
| `.` | `[^\n]` | `[^\n]` | `.` |
| `(?s).` | `.` | `(?s:.)` | `(?s:.)` |
| `\d` | `[0-9٠-٩...]` (37 ranges) | `\p{Nd}` | `\p{Nd}` |
| `\w` | 487 explicit ranges | `[\p{L}\p{Mn}\p{Nd}\p{Pc}]` | `[\p{L}\p{Mn}\p{Nd}\p{Pc}]` |
| `\s` | 10 explicit ranges | `[\t\n\x{000B}\f\r\x{0085}\p{Z}]` | same |
| `\b` | lookarounds over the exact word class | lookarounds over `[\p{L}\p{Mn}\p{Nd}\p{Pc}\x{200C}\x{200D}]` | not supported (ASCII `\b` only under `RegexOptions.ECMAScript`) |
| `\p{Lu}`, `\p{IsGreek}` | explicit ranges | `\p{Lu}`, explicit ranges for blocks | `\p{Lu}`, explicit ranges for blocks and `Cn` |
| `[^\W\d]` | explicit ranges | `[\p{L}\p{Mn}\p{Pc}]` | `[\p{L}\p{Mn}\p{Pc}]` |
| `(?<n>a)\k<n>` | `(a)\1` | `(?<n>a)\k<n>` / `(?<n>a)\g{1}` | not supported |
| `(a)\1\x30` | `(a)\1\u0030` | `(a)\1\x{0030}` | not supported |
| `a{300}` | `(?:a{255}a{45})` | `a{300}` | `a{300}` |
| non-ASCII, control characters | `\uXXXX` | `\x{XXXX}` | `\x{XXXX}` |
| `😀` (a literal surrogate pair) | `\U0001F600` | `\x{1F600}` | `\x{1F600}` |

Large sets are compacted with the general categories they fully contain (so `[^\W\d]` becomes
`[\p{L}\p{Mn}\p{Pc}]` rather than 7 KB of ranges, which SQL Server would reject). Every literal is pure
ASCII, which keeps the SQL text safe in any encoding.

### PostgreSQL

Advanced regular expressions (ARE), the Tcl-derived engine behind `~` and `regexp_like`
([documentation](https://www.postgresql.org/docs/17/functions-matching.html)). Facts the translation
relies on: `\b` is a backspace; `$` never matches before a trailing newline; `.` and `[^...]` match
newlines by default; `\d`, `\s` and `\w` are the locale-dependent POSIX classes; there are no named
groups, no atomic groups and no `\p{..}`; parentheses inside lookarounds never capture (such groups are
omitted from `GroupNumbers`); lookarounds cannot contain backreferences; bounds go up to 255 (larger
repeats of single-character atoms are split, larger repeats of groups are unsupported); embedded options
such as `(?i)` may only appear at the start, so case is expanded instead. A pattern that mixes greedy
and lazy quantifiers gets a warning: PostgreSQL decides whether the whole match is longest or shortest
from its first quantifier, which changes the extent of a match (not whether one exists).

### MySQL and MariaDB

MySQL 8 uses ICU ([MySQL](https://dev.mysql.com/doc/refman/8.4/en/regexp.html),
[ICU](https://unicode-org.github.io/icu/userguide/strings/regexp.html)); MariaDB uses PCRE2
([MariaDB](https://mariadb.com/kb/en/pcre/), [PCRE2](https://www.pcre.org/current/doc/html/pcre2syntax.html)).
Both match case-insensitively by default, so every pattern starts with `(?-i)`, which turns that off on
both engines; case-insensitive .NET patterns are expanded into exact classes. Measured on live servers:

- MySQL 8.4 folds case under every `_ci` collation, including the default `utf8mb4_0900_ai_ci`, with
  ICU's full case folding: without `(?-i)`, `E` matches `e`, the Kelvin sign matches `k`, `ß` and `ẞ`
  match `ss`, and even `\p{Lu}` matches lowercase letters. `utf8mb4_bin` and `utf8mb4_0900_as_cs` are
  case-sensitive.
- MariaDB 11.4 folds case under every collation except `_bin` and binary strings, including the default
  `utf8mb4_uca1400_ai_ci` and even `utf8mb4_uca1400_as_cs`: without `(?-i)`, `E` matches `e` and the
  Kelvin sign matches `k` (simple folding: `ß` does not match `ss`).
- Accent-insensitivity (`_ai`) never reaches REGEXP on either engine: `é` does not match `e`, with or
  without `(?-i)`, under every collation tested. Neither engine applies canonical equivalence, so
  precomposed `é` and `e` followed by a combining acute accent stay different, as in .NET.

ICU's `\w`, `\s`, `\b` and line terminators differ from .NET, PCRE2's `\d`, `\w` and `\b` are ASCII-only
unless UCP is enabled, so all of them are written as `\p{..}` classes. ICU requires bounded lookbehinds;
PCRE2 (10.42 in the `mariadb:11.4` image) requires each top-level lookbehind branch to have a fixed
length. Repeat counts go up to 16,777,215 in ICU and 65,535 in PCRE2; larger bounds are unsupported.
PCRE2 rejects `\x{..}` naming a surrogate, so a supplementary character written literally in the .NET
pattern is emitted as one code point. MariaDB backreferences are written `\g{n}` to avoid the `\10`
octal ambiguity. Group names are kept when the engine accepts them (ICU: ASCII letters and digits,
starting with a letter; PCRE2: up to 32 letters, digits and underscores) and dropped in favor of numbers
otherwise.

### SQL Server 2025 / Azure SQL

`REGEXP_LIKE` is "based on the RE2 regular expression library"
([REGEXP_LIKE](https://learn.microsoft.com/en-us/sql/t-sql/functions/regexp-like-transact-sql),
[overview](https://learn.microsoft.com/en-us/sql/relational-databases/regular-expressions/overview),
[RE2 syntax](https://github.com/google/re2/wiki/Syntax)). RE2 has no lookaround, no backreferences, no
atomic groups, no conditionals, no `\Z` and no comments; `$` is `\z`, so .NET's `$` is written `\n?\z` and
is accepted only when nothing follows it in the pattern; `\d`, `\s`, `\w` and `\b` are ASCII, so the classes
are written with `\p{..}` and a Unicode `\b` is unsupported; repetition counts above 1,000 are rejected;
named groups are `(?P<name>...)`; matching ignores the column collation and is case-sensitive by default.
Patterns longer than 4,000 characters are rejected by the function, which the category compaction keeps
well clear of.

### SQLite (EF Core)

The EF Core SQLite provider registers `regexp()` as
`Regex.IsMatch(input, pattern, RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(1000))` and
translates only the two-argument `Regex.IsMatch`. The translation is therefore the .NET pattern itself
with its options as an inline prefix: `IgnoreCase`, `Multiline`, `Singleline`, `ExplicitCapture` and
`IgnorePatternWhitespace` become `(?imsnx)`. `CultureInvariant`, `RightToLeft`, `ECMAScript` and
`NonBacktracking` have no inline form and throw `NotSupportedException`, as do the constructs the
non-backtracking engine rejects (backreferences, lookarounds, atomic groups, conditionals, balancing
groups, `\G`): better a `NotSupportedException` at translation time than one from the database at query
time. SQLite string literals do not process backslashes, so only quotes are doubled.

```csharp
Pattern.FromRegex("a b", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace).ToFlavor(RegexFlavor.Sqlite).Pattern;
// (?ix)a b
```

## Unsupported constructs

```csharp
Pattern.FromRegex(@"(?=a)(b)\1(?>c)").ToFlavor(RegexFlavor.SqlServer);
// NotSupportedException:
// The pattern cannot be converted to SQL Server without changing its meaning:
//   - lookaround '(?=...)' at position 0
//   - backreference at position 8
//   - atomic group '(?>...)' at position 10
```

Every `CommonPatterns` member converts exactly to PostgreSQL, MySQL and MariaDB. For SQL Server and
SQLite, `StrongPassword`, `Email(Strict)`, `Base64` and `MarkdownLink` throw because of their lookarounds
and `MacAddress` because of its backreference; the other 29 convert exactly. `RegexOptions.RightToLeft`
is unsupported everywhere. Quantified zero-width constructs such as `(?=a)*` are unsupported (PostgreSQL
and ICU reject them), and so is a lone surrogate in the pattern, including a quantified emoji such as
`😀+` (in .NET the quantifier repeats only the second half of the pair): every engine matches whole code
points.

## Verification

During development a harness generated matching and non-matching samples for all 35 `CommonPatterns`
variants and about 55 hand-picked constructs (anchors, trailing newlines, case folding including Kelvin
sign and U+0130, Unicode digits, `\b`, lookarounds, backreferences, large repeats, escapes) and compared
`.NET IsMatch` with the database:

- PostgreSQL 17 (`postgres:17`): 3,270 checks, 0 disagreements.
- SQL Server 2025 (`mcr.microsoft.com/mssql/server:2025-latest`, compatibility level 170): 2,736 checks,
  0 disagreements (one harness-only artifact: T-SQL treats a backslash before a newline inside a string
  literal as a line continuation; the translator never emits raw newlines).
- MySQL 8.4 (`mysql:8.4`, 8.4.13) and MariaDB 11.4 (`mariadb:11.4`, 11.4.13, PCRE2 10.42), default
  `sql_mode`, utf8mb4: a larger harness covered all 72 `CommonPatterns` variants (every parameterless
  method, every `DigitMode.Ascii` overload, and `Email`/`Url` at each `PatternStrictness` in both digit
  modes) and 162 hand-picked constructs (case-insensitive patterns with the Kelvin sign, U+0130, U+0131,
  `ſ`, `ß`, final sigma and titlecase digraphs; case- and accent-sensitive literals and classes;
  `\d \w \s \b \B` and their negations; `.` with and without `Singleline`; every anchor with and without
  `Multiline`; class subtraction; categories and blocks; lazy, bounded and very large repeats;
  alternation; lookarounds; numbered and named backreferences; `ExplicitCapture`,
  `IgnorePatternWhitespace`; quotes and backslashes; literal emoji). Inputs were each pattern's seeded
  `GenerateMatching`/`GenerateNonMatching` samples, variants of them (trailing and leading `\n`, trailing
  `\r`, upper- and lower-cased, accented, Kelvin/dotless-i/long-s substituted, Arabic-Indic and Devanagari
  digits) and 88 fixed edge inputs (empty string, line terminators, `é`/`É`/`e`/`E`, decomposed accents,
  `٣`, supplementary characters, ...). Each pattern/input pair was checked six ways: against the value
  bound as a parameter (connection collation) and against table columns in the server's default
  utf8mb4 collation (`utf8mb4_0900_ai_ci` on MySQL, `utf8mb4_uca1400_ai_ci` on MariaDB), `utf8mb4_bin`,
  `utf8mb4_general_ci`, `utf8mb4_unicode_ci` and `utf8mb4_0900_as_cs` / `utf8mb4_uca1400_as_cs`.
  - MySQL 8.4: 32,499 pattern/input pairs, 194,994 checks, 0 disagreements on inputs in the Basic
    Multilingual Plane; 7 patterns correctly rejected (unbounded lookbehind, case-insensitive
    backreferences, lone surrogates, a repeat count above 16,777,215).
  - MariaDB 11.4: 32,037 pattern/input pairs, 192,222 checks, 0 disagreements on inputs in the Basic
    Multilingual Plane; 11 patterns correctly rejected (variable-length lookbehind branches, repeat counts
    above 65,535, case-insensitive backreferences, lone surrogates).
  - On both engines the only disagreements (34 per check mode) are inputs containing supplementary
    characters (U+1F600, U+1D7CE), all of the kinds described under "The contract".
  - Negative control: with the `(?-i)` prefix removed, the same run reports 848 (MySQL) and 666 (MariaDB)
    disagreements in every check mode that folds case (all but `utf8mb4_bin` on both engines and
    `utf8mb4_0900_as_cs` on MySQL), all of them case differences and none an accent difference.

  The run found and fixed two translation bugs: a supplementary character written literally in the
  pattern was emitted as two surrogate escapes, which PCRE2 rejects at query time (MariaDB) and RE2
  silently never matches (SQL Server, confirmed against SQL Server 2025); and MySQL accepted repeat
  counts up to `int.MaxValue`, while ICU rejects anything above 16,777,215 at query time.

The unit tests do not need a database.
