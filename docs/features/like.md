# LIKE compilation with proof

`ToLike` compiles a pattern to a SQL `LIKE` pattern and proves what it is. The result always accepts
every string the pattern accepts (`IsSuperset` is always true), so it can pre-filter rows with an index
before the regex, or the application, decides; `IsExact` tells you when the LIKE pattern alone is enough.

```csharp
LikeConversion like = Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z").WithAsciiDigits().ToLike(LikeDialect.SqlServer);

like.Expression;        // INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]
like.IsExact;           // true   (proven by language algebra)
like.EscapeCharacter;   // null
like.ToSql("[Number]"); // [Number] LIKE 'INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'

Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z").ToLike(LikeDialect.PostgreSql);
// INV-____-____ (superset)  — '_' also matches non-digits; only SQL Server LIKE has sets
```

`ToLike(dialect, scope = Substring, collation = Binary)` never throws for a valid pattern.

## Dialects and collations

| `LikeDialect` | Syntax | Escaping |
|---|---|---|
| `Standard` | `%`, `_` | `ESCAPE '\'` only when `%`, `_` or `\` must be literal |
| `SqlServer` | `%`, `_`, `[set]`, `[^set]`, ranges | `[` is escaped too; sets are written raw |
| `PostgreSql` | `%`, `_` | backslash is the default escape, so a literal `\` is always `\\` |
| `MySql` | `%`, `_` | as PostgreSQL; `ToSql` also doubles backslashes in the string literal |
| `Sqlite` | `%`, `_` | as Standard |

`LikeCollation.Binary` means code-point comparison (SQL Server `_BIN2`, PostgreSQL `C`, MySQL `_bin`,
SQLite with `case_sensitive_like`). `LikeCollation.CaseInsensitive` states that the database folds case:
the LIKE pattern is then modeled as a regex with `RegexOptions.IgnoreCase` for the proof, and the
compiler may write a case-insensitive letter as plain text. A case-sensitive pattern with cased letters
is never exact under a case-insensitive collation; ranges are never used under one either, because their
order follows the collation rather than code points.

```csharp
Pattern p = Pattern.FromRegex(@"^abc\z", RegexOptions.IgnoreCase);
p.ToLike(LikeDialect.Standard);                                                    // ___ (superset)
p.ToLike(LikeDialect.SqlServer);                                                   // [Aa][Bb][Cc] (exact)
p.ToLike(LikeDialect.Standard, MatchScope.Substring, LikeCollation.CaseInsensitive); // abc (exact)
Pattern.FromRegex(@"^abc\z").ToLike(LikeDialect.Standard, MatchScope.Substring, LikeCollation.CaseInsensitive);
                                                                                     // abc (superset)
Pattern.FromRegex(@"(?i)^[a-c]\z").ToLike(LikeDialect.SqlServer, MatchScope.Substring, LikeCollation.CaseInsensitive);
                                                                                     // [ABCabc] (exact)
```

## Examples

| Pattern | `Standard` | `SqlServer` |
|---|---|---|
| `^abc` | `abc%` (exact) | `abc%` (exact) |
| `xyz\z` | `%xyz` (exact) | `%xyz` (exact) |
| `xyz$` | `%xyz%` (superset: `$` also accepts a trailing newline) | same |
| `^aba\z` | `aba` (exact) | `aba` (exact) |
| `^a[\s\S]*b\z` | `a%b` (exact) | `a%b` (exact) |
| `(?s)^a.c\z` | `a_c` (exact) | `a_c` (exact) |
| `^a.c\z` | `a_c` (superset: `_` matches a newline, `.` does not) | `a[^⏎]c` (exact, with a raw newline in the set) |
| `\d+` | `%_%` (superset) | `%[0-9٠-٩...]%` (exact; `[0-9]` with `WithAsciiDigits()`) |
| `^(?:a\|b)x\z` | `_x` (superset) | `[a-b]x` (exact) |
| `^[a-z]{3}\z` | `___` (superset) | `[a-z][a-z][a-z]` (exact) |
| `^ab?ba\z` | `a%ba` (superset) | same |
| `^(?:aba\|ababa)\z` | `aba%` (superset) | same |
| `^(\w)\1abc\z` | `_%abc` (superset, not proven: backreference) | same |
| `a%b_c[d]` | `%a\%b\_c_%` ESCAPE `\` (superset) | `%a\%b\_c[d]%` ESCAPE `\` (exact) |
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
   scope a `%` is added on each side the pattern does not anchor. Each step only widens the language,
   and LIKE concatenation preserves supersets, so the result is sound by construction.
2. **Prove it.** The LIKE text that will be sent to the database is parsed back into a regex
   (`_` becomes `[\s\S]`, `%` becomes `[\s\S]*`, sets become classes, text is escaped) and compared with
   the scoped pattern by language algebra. Equivalent means `IsExact`.
3. **Compete with the facts.** When it is not exact, `prefix%suffix` from `GetLanguageFacts(scope)` is
   built too: the suffix is only kept when `MinLength >= prefix.Length + suffix.Length` (otherwise
   `aba%` would wrongly become `aba%aba`), `%` alone when nothing is required, and the text alone when
   `MaxLength` equals the prefix length. The candidate whose language is proven strictly smaller wins;
   without proofs (pattern outside the regular subset) the syntactic candidate wins unless it is only `%`.

`Warnings` lists the reasons a result is not exact (and notes such as a set containing non-ASCII
characters). The tests check, for every `CommonPatterns` member and a set of hand-picked patterns across
dialects and collations, that the LIKE regex contains the scoped pattern (proven when the algebra supports
the pattern, sampled otherwise) and that `IsExact` agrees with the algebra's verdict.

## Caveats

- Exactness is for inputs in the Basic Multilingual Plane; SQL Server's `_` matches one UTF-16 code unit
  unless the collation is supplementary-character-aware.
- SQL Server sets are written with raw characters (including a raw newline for `[^⏎]`); non-ASCII members
  are reported in `Warnings`. T-SQL treats a backslash immediately before a newline inside a string
  literal as a line continuation; avoid that sequence when pasting generated SQL.
- Under a case-insensitive collation the model is .NET's case folding; collations that also fold accents
  or treat the Kelvin sign differently are outside the model.
