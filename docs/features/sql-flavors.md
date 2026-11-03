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
pg.PreservesMatchExtent; // true: regexp_substr and regexp_replace see the same matches as .NET
pg.ToLiteral();  // '\A[0-9]+(?=\n?\Z)'

Pattern.FromRegex(@"^[0-9]+$").ToSqlCondition("\"Email\"", RegexFlavor.PostgreSql);
// "Email" ~ '\A[0-9]+(?=\n?\Z)'
Pattern.FromRegex(@"^[0-9]+$").ToSqlCondition("Email", RegexFlavor.MySql);
// REGEXP_LIKE(Email, '(?-i)\\A[0-9]+(?=\\n?\\z)')
Pattern.FromRegex(@"^[0-9]+$").ToSqlCondition("Email", RegexFlavor.MariaDb);
// Email REGEXP '(?-i)\\A[0-9]+(?=\\n?\\z)'
Pattern.FromRegex(@"^[0-9]+$").ToSqlCondition("[Email]", RegexFlavor.SqlServer);
// REGEXP_LIKE([Email], N'\A[0-9]+\n?\z')
Pattern.FromRegex(@"^[0-9]+$").ToSqlCondition("Email", RegexFlavor.Sqlite);
// Email REGEXP '^[0-9]+$'
```

`columnSql` is inserted verbatim: quote it yourself. The pattern literal comes from `ToLiteral()`, which
doubles quotes and, for MySQL and MariaDB only, doubles backslashes because their string literals process
C escapes (assumes `sql_mode` without `NO_BACKSLASH_ESCAPES`, the default on MySQL 8.4 and MariaDB 11.4;
with that mode on, the doubled backslashes would reach the regex engine and change the pattern).
PostgreSQL output assumes `standard_conforming_strings = on`, the default since 9.1. SQL Server literals
are national literals (`N'...'`): a plain `'...'` literal is `varchar`, and SQL Server 2025 silently cuts a
`varchar` pattern down to 4,000 characters before `REGEXP_LIKE` sees it (a 5,000-character `\Axxx...\z`
literal turned into a 4,000-character prefix that matched far more strings, with no error).

## The contract

For every SQL flavor:

- `Flags` is empty. Options live in the pattern text, so a caller can always emit `column <op> 'text'`.
- `IsExact` means the engine accepts exactly the strings .NET does, for inputs in the Basic Multilingual
  Plane. A SQL translation never reports a `Warning`: anything the engine would accept differently throws
  `NotSupportedException` instead, so a returned SQL conversion is always exact. Inputs with supplementary characters (emoji, U+1D7CE MATHEMATICAL
  BOLD DIGIT ZERO, ...) are outside this promise: the engines see one code point where .NET sees two
  UTF-16 code units, so `.`, negated classes and `\P{..}` match such a character once instead of twice,
  `\d`, `\w` and `\p{..}` can match a supplementary digit or letter that .NET does not, and `\b` can move
  next to one. A supplementary character written literally in the pattern is translated as one code
  point (`\x{1F600}`); a lone surrogate in the pattern is unsupported.
- Constructs the engine cannot express throw one `NotSupportedException` listing all of them with their
  positions. A regex that would silently mean something different is never emitted.
- `MatchExtentWarnings` lists differences in *which substring* a match covers and *what each group
  captures*, for strings both sides accept; `PreservesMatchExtent` is true when it is empty. These do not
  affect `REGEXP_LIKE` or `~`, but they do affect `regexp_substr`, `REGEXP_REPLACE` and anything else that
  extracts or replaces text (the EF Core package refuses to translate `Group` and `Replace` when
  `PreservesMatchExtent` is false). The analysis is sound and conservative: a warning means a difference is
  possible, its absence means none is. See "Match extent and captures" below.
- Case-insensitive parts are expanded with the case equivalences .NET's `Regex` would use for the pattern
  at translation time: those of `CultureInfo.CurrentCulture`, or the invariant ones under
  `RegexOptions.CultureInvariant` (accepted by every regex-engine flavor). The translation therefore
  captures the current culture. Measured on .NET 8 and 10, only three tables exist: invariant
  (`i`~`I`), Turkish and Azerbaijani (`i`~`U+0130`, `I`~`U+0131`) and every other culture (`i`~`I`~`U+0130`):
  under `tr-TR`, `(?i)^i$` becomes `\A[i\u0130](?=\n?\Z)` for PostgreSQL, under `en-US`
  `\A[Ii\u0130](?=\n?\Z)`. `Pattern.IsMatch` applies the same rule with the culture current when the
  pattern first matches (its `Regex` is cached), so translate and match under the same culture, or use
  `CultureInvariant`.
- `GroupNumbers` maps every .NET capture group (by name, and by its .NET number as a string) to its 1-based
  number in the converted regex. .NET numbers unnamed groups before named ones; every engine here numbers
  left to right, and PostgreSQL has no names at all. The whole match (.NET group 0) is always group 0 and
  is intentionally absent:

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
| `\s` | explicit ranges | `[\t\n\x{000B}\f\r\x{0085}\p{Z}]` | same |
| `\b` | lookarounds over the exact word class | lookarounds over `[\p{L}\p{Mn}\p{Nd}\p{Pc}\x{200C}\x{200D}]` | not supported (ASCII `\b` only under `RegexOptions.ECMAScript`) |
| `\p{Lu}`, `\p{IsGreek}` | explicit ranges | `\p{Lu}`, explicit ranges for blocks | `\p{Lu}`, explicit ranges for blocks (`\p{Cn}` is too long: unsupported) |
| `[:alpha:]`, `[a:b]`, `[=a=]`, `[.a.]` | written from the exact set (`[\:ahlp]`) | `:`, `=` and `.` escaped (`[\:alpha\:]`) | same |
| `[^\W\d]` | explicit ranges | `[\p{L}\p{Mn}\p{Pc}]` | `[\p{L}\p{Mn}\p{Pc}]` |
| `(?<n>a)\k<n>` | `(a)\1` | `(?<n>a)\k<n>` / `(?<n>a)\g{1}` | not supported |
| `(a)\1\x30` | `(a)\1\u0030` | `(a)\1\x{0030}` | not supported |
| `a{300}` | `(?:a{255}a{45})` | `a{300}` | `a{300}` |
| non-ASCII, control characters | `\uXXXX` | `\x{XXXX}` | `\x{XXXX}` |
| `😀` (a literal surrogate pair) | `\U0001F600` | `\x{1F600}` | `\x{1F600}` |

Large sets are compacted with the general categories they fully contain (so `[^\W\d]` becomes
`[\p{L}\p{Mn}\p{Pc}]` rather than 7 KB of ranges, which SQL Server would reject). Every literal is pure
ASCII, which keeps the SQL text safe in any encoding. Inside a class, `:`, `=` and `.` are escaped: a
set written `[:alpha:]` in .NET means "one of `:alph`", but ICU (MySQL) read it as the POSIX class of
letters (it matched `A`, `é` and `Ω` but not `:`), and PCRE2 (MariaDB) rejected `[:...]`, `[=...]` and
`[....]` at query time.

### PostgreSQL

Advanced regular expressions (ARE), the Tcl-derived engine behind `~` and `regexp_like`
([documentation](https://www.postgresql.org/docs/17/functions-matching.html)). Facts the translation
relies on: `\b` is a backspace; `$` never matches before a trailing newline; `.` and `[^...]` match
newlines by default; `\d`, `\s` and `\w` are the locale-dependent POSIX classes; there are no named
groups, no atomic groups and no `\p{..}`; parentheses inside lookarounds never capture (such groups are
omitted from `GroupNumbers`); lookarounds cannot contain backreferences; bounds go up to 255 (larger
repeats of single-character atoms are split, larger repeats of groups are unsupported); embedded options
such as `(?i)` may only appear at the start, so case is expanded instead. Groups are reset at the start of
every repeat iteration: on `abab`, `^(?:(a)|(b))+\1\2$` matches in .NET (group 1 keeps the `a` of an
earlier iteration) and not in PostgreSQL, so a backreference to a group that an iteration of an
enclosing repeat (with a maximum above 1) can skip is unsupported; `^(?:(a)b)+\1$` and `^(a)*\1$`, whose
groups are set by every iteration, translate exactly. Match preferences differ too (longest/shortest
match, see "Match extent and captures").

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
otherwise. Both keep a group's capture from an earlier repeat iteration, like .NET.

### SQL Server 2025 / Azure SQL

`REGEXP_LIKE` is "based on the RE2 regular expression library"
([REGEXP_LIKE](https://learn.microsoft.com/en-us/sql/t-sql/functions/regexp-like-transact-sql),
[overview](https://learn.microsoft.com/en-us/sql/relational-databases/regular-expressions/overview),
[RE2 syntax](https://github.com/google/re2/wiki/Syntax)). RE2 has no lookaround, no backreferences, no
atomic groups, no conditionals, no `\Z` and no comments; `$` is `\z`, so .NET's `$` is written `\n?\z` and
is accepted only when nothing follows it in the pattern; `\d`, `\s`, `\w` and `\b` are ASCII, so the classes
are written with `\p{..}` and a Unicode `\b` is unsupported; repetition counts above 1,000 are rejected;
named groups are `(?P<name>...)`; matching ignores the column collation and is case-sensitive by default.
The pattern argument is at most `nvarchar(4000)`: a longer `N'...'` literal is rejected ("Argument data
type NVARCHAR(MAX) is invalid for argument 2"), and a longer plain `'...'` literal used to be cut to 4,000
characters without an error. Literals are therefore `N'...'`, and a translated pattern longer than 4,000
UTF-16 code units throws `NotSupportedException` at translation time. Category compaction keeps most
patterns far below the limit, but not all: `\p{Cn}` (4,465 characters of ranges, because RE2 has no
`Cn`), `[\p{L}-[a-z]]` (over 5,000) and very long literals are unsupported.

### SQLite (EF Core)

The EF Core SQLite provider registers `regexp()` as
`Regex.IsMatch(input, pattern, RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(1000))` and
translates only the two-argument `Regex.IsMatch`. The translation is therefore the .NET pattern itself
with its options as an inline prefix: `IgnoreCase`, `Multiline`, `Singleline`, `ExplicitCapture` and
`IgnorePatternWhitespace` become `(?imsnx)`. `RightToLeft`, `ECMAScript` and `NonBacktracking` have no
inline form and throw `NotSupportedException`, as do the constructs the non-backtracking engine rejects
(backreferences, lookarounds, atomic groups, conditionals, balancing groups, `\G`): better a
`NotSupportedException` at translation time than one from the database at query time. `regexp()` runs
in the application process, so case-insensitive matching uses the culture of the thread that executes
the query. `CultureInvariant` has no inline form either; it is dropped when it cannot change the result
(no case-insensitive construct can match `i`, `I`, U+0130 or U+0131, the only letters whose case
equivalents depend on the culture, and no backreference ignores case) and throws otherwise. SQLite string
literals do not process backslashes, so only quotes are doubled.

```csharp
Pattern.FromRegex("a b", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace).ToFlavor(RegexFlavor.Sqlite).Pattern;
// (?ix)a b
```

## Match extent and captures

`IsExact` is about acceptance. Extracting or replacing text also depends on *which* substring matches
and what each group captures, and the engines resolve choices differently. `MatchExtentWarnings` lists
every difference that can occur; `PreservesMatchExtent` is true when there is none:

```csharp
FlavorConversion pg = Pattern.FromRegex("(a|ab)").ToFlavor(RegexFlavor.PostgreSql);
pg.IsExact;              // true: the same strings contain a match
pg.PreservesMatchExtent; // false: on "ab", .NET matches "a", PostgreSQL "ab"
Pattern.FromRegex(@"(\d+)-(\d+)").ToFlavor(RegexFlavor.PostgreSql).PreservesMatchExtent; // true
```

The rules, each confirmed with `regexp_substr`/`REGEXP_SUBSTR` (group extraction) and
`regexp_replace`/`REGEXP_REPLACE` against the live engines:

- **PostgreSQL** takes the leftmost match and then the *longest* or *shortest* one, decided by the RE's
  first quantifier (an RE with a top-level `|` is always longest), and gives earlier groups the longest
  or shortest text; .NET takes the first alternative and the first greedy or lazy count that succeeds.
  `(a|ab)` replaced in `ab` gives `X` (.NET `Xb`), `^(a|ab)(b?)$` captures `ab` and an empty string (.NET `a` and `b`),
  `(a+?)(a*)` matches `a` of `aaa` (.NET `aaa`). The translation's own alternations count: `(?m)^a+?` and
  `\ba+?` are longest in PostgreSQL. PostgreSQL also resets groups on every repeat iteration:
  `(?:(a)|b)+` on `ab` leaves group 1 unset (.NET `a`). A pattern is reported unless a syntactic analysis
  proves the choice cannot matter: every alternation and variable repeat is decided by the next
  character (disjoint first characters, no empty iterations), except for where the match stops, and
  every such stopping choice prefers what PostgreSQL prefers (`\d+`, `(\d+)-(\d+)`, `[a-z]+@[a-z]+\.com`,
  `x(?:b|)`); or the pattern has no groups and must end at one place (`\z`, or `$` after something that
  cannot consume a final newline, as in `^[a-z.]+@[a-z.]+\.[a-z]{2,}$`).
- **MySQL** (ICU) and **MariaDB** (PCRE2) are backtracking engines with .NET's preferences: every pattern
  above matches and captures as in .NET, including captures kept across iterations. Two differences
  remain. A group in a repeat whose body can match empty text can capture differently (on `aa`,
  `[ab]((a\b)??|b{0,2}?){0,2}\z` leaves the group empty in .NET and at `a` in both). And empty matches in
  `REGEXP_REPLACE`: MariaDB never replaces an empty match and stops replacing at the first one
  (`REGEXP_REPLACE('abc', '^', '-')` is `abc`, `REGEXP_REPLACE('baaac', 'a*', '-')` is `baaac`; .NET gives
  `-abc` and `-b--c-`); MySQL replaces empty matches like .NET but returns an empty input unchanged
  (`REGEXP_REPLACE('', '^', 'X')` is the empty string). Both are reported whenever the pattern's
  syntactic minimum width is 0 (every anchor, `\b` and lookaround counts as 0, every repeat at its
  minimum), so `\ba?` and `(?=a)a??` are reported even though they never match an empty *string*.
  (MariaDB's `REGEXP_SUBSTR` also returns the empty string, not `NULL`, when nothing matches.)
- **SQL Server** (RE2) is leftmost-first with .NET's preferences: `(a|ab)` matches `a`, `a+?b*` matches all
  of `ab` (PostgreSQL only `a`), and a group skipped by a later iteration keeps its capture. Two differences: the `$`
  rewrite `\n?\z` consumes the final newline that .NET leaves outside the match (replacing `a$` in
  `"a\n"` gives `X`, .NET `X\n`), reported for every `$` and `\Z`; and RE2 refuses empty
  iterations where .NET stops a repeat at the first one, so a repeat whose body can match empty text
  can match and capture differently (`(?:a?|b{1,2})*` on `ab`: RE2 `ab`, .NET `a`; `(a?)+` on `aa`: RE2
  captures `a`, .NET empty).
  Anchor with `\z` instead of `$` when a final newline is not expected and the extent matters.
- **SQLite** runs .NET's own engine, so `MatchExtentWarnings` is always empty (the EF Core provider
  translates only `Regex.IsMatch` there).
- **JavaScript and JSON Schema** backtrack like .NET, but reset the groups inside a repeat on every
  iteration (`(?:(a)|b)+` on `ab`: `undefined`) and refuse empty iterations (`(a?)+` on `aa`: `a`;
  `(?:a?|b{1,2})*` on `ab`: `ab`, .NET `a`); both are reported.

Of the 30 parameterless `CommonPatterns` members, 22 preserve the match extent on PostgreSQL (for the
rest, groups or overlapping alternatives leave a choice the analysis cannot rule out), 29 on MySQL and MariaDB
(`Base64` can match the empty string), all 30 in JavaScript, and on SQL Server only `HtmlTag`, because
the others end with `$`.

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
points. Also unsupported:

- Under `RegexOptions.ECMAScript`, a backreference to a group that may not have participated (`^(a)?\1b$`):
  .NET then matches the empty string (it accepts `b`), every database engine fails.
- On PostgreSQL, a backreference to a group that a repeat iteration can skip (see "PostgreSQL").
- On SQL Server, a translated pattern longer than 4,000 characters.

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

A later review ran counterexamples against the same four images and found six more problems, all fixed
and re-verified there: the SQL Server `varchar` truncation (now `N'...'` literals and a 4,000-character
check), ICU reading `[:alpha:]` as a POSIX class, ECMAScript backreferences to groups that did not
participate, PostgreSQL's per-iteration group reset, culture-blind case expansion (a `tr-TR` translation
reused cached `en-US` case sets), and the missing match-extent contract. A differential harness compared,
for every pattern/input pair, acceptance, the matched substring (`regexp_substr` / `REGEXP_SUBSTR`), the
replace-all result (`regexp_replace(..., 'g')` / `REGEXP_REPLACE`) and every group (`regexp_substr(..., n)`
on PostgreSQL, `REGEXP_SUBSTR(..., 'c', n)` on SQL Server, a `$n` / `\n` replacement on MySQL and
MariaDB) with .NET, on about 70 hand-picked cases and 2,600 randomly generated patterns (alternation,
greedy, lazy and bounded repeats, nested capturing and non-capturing groups, `\b`, lookarounds and anchors
over `a`, `b`, `[ab]` and `.`) against 36 inputs each: 344,355 pattern/input/engine comparisons in all.
No pair that `IsExact` promised differed in acceptance, and no pair that `PreservesMatchExtent` promised
differed in substring, replacement or captures. The case-folding cases were run under `tr-TR` and `en-US`
with no disagreement on any engine. Two .NET findings came out of it: the backtracking interpreter
(`RegexOptions.None`) on .NET 8 and 10 can return a match that violates `^` for lazy loops whose body can
be empty (`^aa(|a)+?ab` matches `ab` at index 2 of `aaab`; `RegexOptions.Compiled` and `NonBacktracking`
match `aaab` at 0) and can throw `OverflowException` on deeply nested ones; the harness skipped patterns
on which .NET's own engines disagree. And ICU (MySQL) times out ("Timeout exceeded in regular
expression match") or overflows its backtrack stack on some lazy loops with empty bodies, an error at
query time rather than a wrong answer.

The unit tests do not need a database.
