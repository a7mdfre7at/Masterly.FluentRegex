# LIKE compilation with proof

`ToLike` compiles a pattern to a SQL `LIKE` pattern and proves what it is. The result always accepts
every string the pattern accepts (`IsSuperset` is always true), so it can pre-filter rows with an index
before the regex, or the application, decides; `IsExact` tells you when the LIKE pattern alone is enough.

```csharp
LikeConversion like = Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z").WithAsciiDigits().ToLike(LikeDialect.SqlServer);

like.Expression;        // INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]
like.IsExact;           // true   (proven by language algebra)
like.EscapeCharacter;   // null
like.ToSql("[Number]"); // [Number] LIKE N'INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'

Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z").ToLike(LikeDialect.PostgreSql);
// INV-____-____ (superset)  — '_' also matches non-digits; only SQL Server LIKE has sets
```

`ToLike(dialect, scope = Substring, collation = Binary)` never throws for a valid pattern.

## Dialects and collations

| `LikeDialect` | Syntax | Escaping |
|---|---|---|
| `Standard` | `%`, `_` | `ESCAPE '\'` only when `%`, `_` or `\` must be literal |
| `SqlServer` | `%`, `_`, `[set]`, `[^set]`, ranges | `[` is escaped too; inside a set the escape character is doubled (`[\\a]`), because SQL Server reads it inside brackets as well; `ToSql` writes `N'...'` |
| `PostgreSql` | `%`, `_` | backslash is the default escape, so a literal `\` is always `\\` |
| `MySql` | `%`, `_` | as PostgreSQL; `ToSql` also doubles backslashes in the string literal |
| `Sqlite` | `%`, `_` | as Standard |

`LikeCollation.Binary` means code-point comparison (SQL Server `_BIN2`, PostgreSQL `C`, MySQL `_bin`,
SQLite with `case_sensitive_like`). `LikeCollation.CaseInsensitive` states that the database folds case.
Collations disagree on what they fold, so the compiler only relies on what every case-insensitive
collation does: an ASCII letter matches its ASCII partner (`a`/`A`), except `i`/`I`, which Turkish and
Azerbaijani collations keep apart. Everything else .NET folds is spelled out:

- A case-insensitive pattern letter is written as plain text only when its .NET case variants are covered
  by that guarantee. `(?i)k` also matches U+212A KELVIN SIGN and `(?i)i` also matches U+0130, which SQL
  Server's `Latin1_General_CI_AS`, MySQL's `utf8mb4_general_ci` and SQLite do not fold, so those letters
  become a set listing the variants (`[Kk` plus U+212A `]`, SQL Server) or `_` (other dialects).
- A negated set is never written: under a case-insensitive collation `[^a]` also rejects `A` (and whatever
  else the collation folds with `a`), so a class that would need one becomes `_` or `%`.
- Ranges are never used, because their order follows the collation rather than code points; members are
  listed instead.

The proof models the collation twice: the superset proof gives the LIKE pattern only the guaranteed
folds, the exactness proof gives it .NET's full case folding (`RegexOptions.IgnoreCase`). A
case-sensitive pattern with cased letters is therefore never exact under a case-insensitive collation.

```csharp
Pattern p = Pattern.FromRegex(@"^abc\z", RegexOptions.IgnoreCase);
p.ToLike(LikeDialect.Standard);                                                    // ___ (superset)
p.ToLike(LikeDialect.SqlServer);                                                   // [Aa][Bb][Cc] (exact)
p.ToLike(LikeDialect.Standard, MatchScope.Substring, LikeCollation.CaseInsensitive); // abc (exact)
Pattern.FromRegex(@"^abc\z").ToLike(LikeDialect.Standard, MatchScope.Substring, LikeCollation.CaseInsensitive);
                                                                                     // abc (superset)
Pattern.FromRegex(@"(?i)^[a-c]\z").ToLike(LikeDialect.SqlServer, MatchScope.Substring, LikeCollation.CaseInsensitive);
                                                                                     // [ABCabc] (exact)
Pattern.FromRegex(@"(?i)^kid\z").ToLike(LikeDialect.MySql, MatchScope.Substring, LikeCollation.CaseInsensitive);
                                                                                     // __d (superset)
Pattern.FromRegex(@"^[^a]").ToLike(LikeDialect.SqlServer, MatchScope.Substring, LikeCollation.CaseInsensitive);
                                                                                     // % (superset)
```

## Characters versus UTF-16 code units

A .NET regex sees UTF-16 code units, so an emoji is two characters to `.`. PostgreSQL, MySQL, MariaDB,
SQLite, and SQL Server's supplementary-character-aware collations (`_SC`, and `varchar` with a `_UTF8`
collation) count code points, so for them `_` matches the whole emoji and `__` rejects it, although
`^..\z` accepts it. SQL Server's other collations count UTF-16 code units, except that the legacy
case-insensitive ones (version 80, such as the default `SQL_Latin1_General_CP1_CI_AS`) give supplementary
characters no weight at all, so `_` matches no part of them.

To stay a superset for all of these, the compiler looks at positions whose class can match a surrogate
code unit (`.`, `[^a]`, `\W`, `[\s\S]`, a lone surrogate literal): two such positions in a row can be one
database character. A run of `k` of them is written as `_` repeated `ceil(k/2)` times followed by `%`
(`^..\z` becomes `_%`, `^...\z` becomes `__%`); a single one between text, `%` or positions that cannot
match a surrogate (such as `[0-9]` or `\w`) cannot pair up and stays `_` or a set. On SQL Server under
`LikeCollation.CaseInsensitive` every such position becomes `%`. A literal surrogate pair stays text, and
a lone surrogate never reaches the LIKE text. `IsExact` is false whenever the LIKE pattern contains `_` or
a negated set, because in a code-point collation those also accept a supplementary character where the
regex expects a single code unit.

## Examples

| Pattern | `Standard` | `SqlServer` |
|---|---|---|
| `^abc` | `abc%` (exact) | `abc%` (exact) |
| `xyz\z` | `%xyz` (exact) | `%xyz` (exact) |
| `xyz$` | `%xyz%` (superset: `$` also accepts a trailing newline) | same |
| `^aba\z` | `aba` (exact) | `aba` (exact) |
| `^a[\s\S]*b\z` | `a%b` (exact) | `a%b` (exact) |
| `(?s)^a.c\z` | `a_c` (superset: `_` also matches an emoji) | `a_c` (superset) |
| `^a.c\z` | `a_c` (superset: `_` matches a newline, `.` does not) | `a[^⏎]c` (superset, with a raw newline in the set) |
| `^..\z` | `_%` (superset: an emoji is one database character) | `_%` (superset) |
| `\d+` | `%_%` (superset) | `%[0-9٠-٩...]%` (exact; `[0-9]` with `WithAsciiDigits()`) |
| `^(?:a\|b)x\z` | `_x` (superset) | `[a-b]x` (exact) |
| `^[a-z]{3}\z` | `___` (superset) | `[a-z][a-z][a-z]` (exact) |
| `^ab?ba\z` | `a%ba` (superset) | same |
| `^(?:aba\|ababa)\z` | `aba%` (superset) | same |
| `^(\w)\1abc\z` | `_%abc` (superset, not proven: backreference) | same |
| `a%b_c[d]` | `%a\%b\_c_%` ESCAPE `\` (superset) | `%a\%b\_c[d]%` ESCAPE `\` (exact) |
| `^%[\\a]\z` | `\%_` ESCAPE `\` (superset) | `\%[\\a]` ESCAPE `\` (exact) |
| `` (empty) | `%` (exact) | `%` (exact) |

`^INV-\d{4}-\d{4}$` with ASCII digits becomes `INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]%` on SQL
Server and is not exact: `$` accepts `"INV-1234-5678\n"`, which only a trailing `%` can cover. The warning
says so and suggests `\z`, with which the same pattern is exact.

## How it works

1. **Compile a superset from the syntax tree.** Literals stay text; a class becomes a set (SQL Server,
   when it can be written) or `_`; `[\s\S]*` becomes `%`; a bounded repeat is unrolled and anything
   variable becomes `%`; an alternation of single characters becomes their union, any other alternation,
   a backreference or a conditional becomes `%`; zero-width constructs (lookarounds, `\b`, `^` in the
   middle) produce nothing; `$` and `\Z` produce `%` because of the trailing newline. In `Substring`
   scope a `%` is added on each side the pattern does not anchor. Runs of positions that can match a
   surrogate are then widened as described above. Each step only widens the language, and LIKE
   concatenation preserves supersets, so the result is sound by construction.
2. **Prove it.** The LIKE text that will be sent to the database is parsed back into a regex, exactly as
   the dialect reads it (`_` becomes `[\s\S]`, `%` becomes `[\s\S]*`, sets become classes, escapes are
   honored inside sets too, text is escaped) and compared with the scoped pattern by language algebra.
   The candidate is accepted only when the scoped pattern is proven equivalent to it or a proper subset
   of it (under a case-insensitive collation: of what every such collation is guaranteed to accept). It
   is exact when it is also proven equivalent under .NET's case folding and contains no `_` and no
   negated set.
3. **Compete with the facts.** When it is not exact, `prefix%suffix` from `GetLanguageFacts(scope)` is
   built too: the suffix is only kept when `MinLength >= prefix.Length + suffix.Length` (otherwise
   `aba%` would wrongly become `aba%aba`), `%` alone when nothing is required, and the text alone when
   `MaxLength` equals the prefix length. A candidate proven not to be a superset is dropped (`%` is the
   last resort); of the rest, the one whose language is proven strictly smaller wins; without proofs
   (pattern outside the regular subset) the syntactic candidate wins unless it is only `%`.

The scoped pattern is built so that a trailing `#` comment in free-spacing mode (`RegexOptions.IgnorePatternWhitespace`,
`(?x)`) cannot swallow the `Substring` padding: `^INV-[0-9]{4} # invoice number` becomes
`INV-[0-9][0-9][0-9][0-9]%`, not `INV-[0-9][0-9][0-9][0-9]`.

`Warnings` lists the reasons a result is not exact (and notes such as a set containing non-ASCII
characters). The tests check, for every `CommonPatterns` member and a set of hand-picked patterns across
dialects and collations, that what the LIKE pattern is guaranteed to accept contains the scoped pattern
(proven when the algebra supports the pattern, sampled otherwise, including samples with supplementary
characters checked under code-point semantics) and that `IsExact` agrees with the algebra's verdict. The
behaviours above were confirmed on PostgreSQL 17, MySQL 8.4, MariaDB 11.4, SQL Server 2025 and SQLite.

## Caveats

- **SQL Server `varchar` and trailing blanks.** A `varchar` pattern compared with a `varchar` column
  ignores trailing blanks of the value (`'abc ' LIKE 'abc'` is true). `ToSql` writes the pattern as a
  Unicode literal (`N'...'`), under which trailing blanks are significant, and the exactness proof
  assumes that; if you pass `Expression` to SQL Server yourself, pass it as `nvarchar`.
- **SQL Server linguistic collations** (every non-binary collation, `_CI_` and `_CS_` alike) compare
  collation elements, not characters. Characters without weight (NUL, U+00AD SOFT HYPHEN, unassigned or
  newer-than-the-collation code points) are skipped, so `_` or a set cannot match them; a base letter
  followed by a combining mark can form one element, so in the version-80 collations `abc%` rejects
  `abc` followed by U+0301 COMBINING ACUTE ACCENT, which `^abc` accepts; expansions such as `æ` = `ae`
  and `ß` = `ss` make LIKE accept more.
  `LikeCollation.CaseInsensitive` models case folding only, so on data with such characters use a
  `_BIN2` collation (and `LikeCollation.Binary`), which compares code units and has none of these effects.
- Collations that also fold accents (`_AI`, MySQL's default `utf8mb4_0900_ai_ci`) or fold more than .NET
  keep the LIKE pattern a superset but can make an `IsExact` result accept more than the regex.
- SQL Server sets are written with raw characters (including a raw newline for `[^⏎]`); non-ASCII members
  are reported in `Warnings`. T-SQL treats a backslash immediately before a newline inside a string
  literal as a line continuation; avoid that sequence when pasting generated SQL.
