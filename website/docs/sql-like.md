---
id: sql-like
title: "Compiling to SQL LIKE"
description: "Compile a pattern to a SQL LIKE pattern for SQL Server, PostgreSQL, MySQL, SQLite or standard SQL, with a proof of whether it is exact or a safe superset."
---

`ToLike` compiles a pattern to a SQL `LIKE` pattern and proves what that `LIKE` pattern is. The result always accepts every string the pattern accepts, so the database can use it to throw away rows that can never match, often through an index. When the library can also prove that `LIKE` accepts **exactly** the strings the pattern accepts, `IsExact` is `true` and you can drop the regex check altogether.

Use this page when you need to:

- filter rows in a database that has no regex support (SQL Server before 2025, SQLite without an extension) or where `LIKE` is faster than the regex functions,
- pre-filter rows with an index-friendly `LIKE` before your application runs the real regex,
- write a `WHERE` condition for raw SQL with the quoting and escaping each database expects,
- find out why a pattern cannot be expressed exactly as `LIKE`, and how to change it so it can.

All members on this page live in the `Masterly.FluentRegex` namespace and work on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10.

| Member | Kind | What it does |
|--------|------|--------------|
| `pattern.ToLike(dialect, scope, collation)` | Method | Compiles the pattern and returns a `LikeConversion`. |
| `LikeConversion` | Class | The `LIKE` pattern, its escape character, the exactness proof and the warnings. See [Reading the result](#reading-the-result). |
| `LikeConversion.ToSql(columnSql)` | Method | Writes the full condition, such as `[Number] LIKE N'INV-%'`. |
| `LikeDialect` | Enum | The `LIKE` syntax of the database: `Standard`, `SqlServer`, `PostgreSql`, `MySql`, `Sqlite`. |
| `LikeCollation` | Enum | What the column's collation does to case: `Binary` or `CaseInsensitive`. |
| `MatchScope` | Enum | `Substring` (what `IsMatch` accepts, the default) or `FullString`. |

## Compile a pattern

Call `ToLike` with the dialect of your database. Read `Expression` for the bare `LIKE` text, or call `ToSql` for a ready-to-use condition.

```csharp
using System;
using Masterly.FluentRegex;

Pattern invoice = Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z").WithAsciiDigits();

LikeConversion like = invoice.ToLike(LikeDialect.SqlServer);

Console.WriteLine(like.Expression);
// INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]
Console.WriteLine(like.IsExact);           // True
Console.WriteLine(like.ToSql("[Number]"));
// [Number] LIKE N'INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'

LikeConversion portable = invoice.ToLike(LikeDialect.PostgreSql);

Console.WriteLine(portable);               // INV-____-____ (superset)
Console.WriteLine(portable.Warnings[0]);
// A character class became '_' (any character); only SQL Server LIKE can express sets.
```

- SQL Server `LIKE` has character sets (`[0-9]`), so the digits stay digits and the result is exact.
- The other dialects only have `%` (any run of characters) and `_` (any one character). A digit becomes `_`, so `INV-____-____` also accepts `INV-ABCD-EFGH`. It is still a correct pre-filter: every real invoice number passes it.
- `WithAsciiDigits()` makes `\d` mean `0-9`. Without it, .NET's `\d` matches every Unicode decimal digit, and the SQL Server set lists all of them (see [SQL Server sets](#sql-server-sets)).
- `ToString()` gives the one-line summary `expression (exact)` or `expression (superset)`.

## Exact or superset

Every `LikeConversion` is a **superset**: the `LIKE` pattern accepts every string the pattern accepts. `IsSuperset` is always `true`; it is there so code can state the guarantee it relies on. What varies is `IsExact`:

| `IsExact` | Meaning | What to do |
|-----------|---------|------------|
| `true` | Language algebra proved that `LIKE` accepts exactly the strings the pattern accepts. | Use the `LIKE` condition alone. |
| `false` | `LIKE` accepts every matching string and possibly more. | Use `LIKE` to narrow the rows, then run the regex on what comes back. |

The two-step filter looks like this. The database does the cheap, index-friendly part; the regex makes the final decision only when it has to.

```csharp
using System.Collections.Generic;
using System.Linq;

// invoice is the pattern from the first sample
LikeConversion like = invoice.ToLike(LikeDialect.PostgreSql);
string sql = $"SELECT number FROM invoices WHERE {like.ToSql("number")}";

Console.WriteLine(sql);
// SELECT number FROM invoices WHERE number LIKE 'INV-____-____'

// candidates are the rows that query returned
string[] candidates = { "INV-2024-0001", "INV-20X4-0001" };
List<string> invoices = like.IsExact
    ? candidates.ToList()
    : candidates.Where(candidate => invoice.IsMatch(candidate)).ToList();

Console.WriteLine(string.Join(", ", invoices)); // INV-2024-0001
```

- `INV-20X4-0001` passes `LIKE 'INV-____-____'` because `_` matches `X`. The regex removes it.
- A `LIKE` pattern that starts with literal text (`INV-...`) can use a B-tree index on most databases. A pattern that starts with `%` cannot.
- The result is never wrong in the other direction: a row the regex would accept is never filtered out by the `LIKE`, as long as the column compares the way the `collation` argument says. SQL Server's linguistic collations are the exception to watch (see [Pitfalls](#pitfalls)).

:::note

"Proved" means proved, not tested. The library parses the `LIKE` text back into a regex, exactly as the dialect reads it, and compares the two languages with [pattern algebra](./pattern-algebra.md). A candidate that cannot be proven to contain every match is thrown away.

:::

## The ToLike method

```csharp
public LikeConversion ToLike(
    LikeDialect dialect,
    MatchScope scope = MatchScope.Substring,
    LikeCollation collation = LikeCollation.Binary)
```

| Parameter | Default | Meaning |
|-----------|---------|---------|
| `dialect` | (required) | The `LIKE` syntax of the target database. Only `LikeDialect.SqlServer` has sets. See [Dialects](#dialects). |
| `scope` | `MatchScope.Substring` | `Substring`: the strings `IsMatch` accepts, so an unanchored pattern gets `%` on each open side. `FullString`: the strings the pattern matches entirely. See [Choosing a scope](#choosing-a-scope). |
| `collation` | `LikeCollation.Binary` | What the column's collation does to `LIKE`. `Binary` compares code points; `CaseInsensitive` folds letter case. See [Collations](#collations). |

`ToLike` never throws for a valid pattern. In the worst case it returns `%`, which accepts everything. It throws `ArgumentOutOfRangeException` only when an enum argument is not a defined value, and `ToSql` throws `ArgumentNullException` for a null column.

```csharp
Pattern pattern = Pattern.FromRegex("abc");

try
{
    pattern.ToLike((LikeDialect)42);
}
catch (ArgumentOutOfRangeException exception)
{
    Console.WriteLine(exception.ParamName); // dialect
}

try
{
    pattern.ToLike(LikeDialect.Standard).ToSql(null!);
}
catch (ArgumentNullException exception)
{
    Console.WriteLine(exception.ParamName); // columnSql
}
```

:::tip

`ToLike` runs language algebra on every call, and the result is not cached. Compute the `LikeConversion` once per pattern and keep it. It is immutable, so it is safe to share between threads.

:::

## Reading the result

`ToLike` returns a `LikeConversion`.

| Member | Type | What it gives you |
|--------|------|-------------------|
| `Expression` | `string` | The `LIKE` pattern text, unquoted, as the database sees it. Escapes are already in place. |
| `EscapeCharacter` | `char?` | The escape character to declare with `ESCAPE`, or `null` when no clause is needed. Always a backslash when set. |
| `IsExact` | `bool` | `true` when `LIKE` is proven to accept exactly the strings the pattern accepts, in `Scope` and under `Collation`. |
| `IsSuperset` | `bool` | Always `true`: `LIKE` accepts every string the pattern accepts. |
| `Warnings` | `IReadOnlyList<string>` | Why the result is not exact, plus remarks that hold either way. See [Warnings](#warnings). |
| `Dialect` | `LikeDialect` | The dialect the pattern was written for. |
| `Scope` | `MatchScope` | The set of strings the proof is about. |
| `Collation` | `LikeCollation` | The collation the proof assumed. |
| `ToSql(columnSql)` | `string` | The full condition: column, `LIKE`, quoted literal and `ESCAPE` clause when needed. |
| `ToString()` | `string` | `expression (exact)` or `expression (superset)`. |

```csharp
LikeConversion like = Pattern.FromRegex(@"^ab?ba\z").ToLike(LikeDialect.Standard);

Console.WriteLine(like);                                             // a%ba (superset)
Console.WriteLine(like.IsExact);                                     // False
Console.WriteLine(like.IsSuperset);                                  // True
Console.WriteLine(like.EscapeCharacter == null);                     // True
Console.WriteLine($"{like.Dialect}, {like.Scope}, {like.Collation}"); // Standard, Substring, Binary
Console.WriteLine(like.Warnings[0]);
// A variable repeat (?, *, +, {m,n}) can only be covered by '%' after its mandatory part.
```

- `^ab?ba\z` accepts `aba` and `abba`. `LIKE` has no "optional character", so the optional `b` becomes `%`. The result `a%ba` also accepts `axyzba`.
- When you pass `Expression` to the database yourself, also pass `ESCAPE` with `EscapeCharacter` whenever it is not `null`. `ToSql` does this for you.

## Choosing a scope

`MatchScope` decides which strings the `LIKE` pattern must describe.

| Scope | Strings described | Effect on the `LIKE` pattern |
|-------|-------------------|------------------------------|
| `Substring` (default) | Those for which `IsMatch` returns `true`: they contain a match anywhere, unless the pattern is anchored. | `%` is added on each side the pattern does not anchor. |
| `FullString` | Those the pattern matches from the first to the last character. | No padding. |

```csharp
Pattern word = Pattern.FromRegex("abc");

Console.WriteLine(word.ToLike(LikeDialect.Standard));                         // %abc% (exact)
Console.WriteLine(word.ToLike(LikeDialect.Standard, MatchScope.FullString));  // abc (exact)
Console.WriteLine(Pattern.FromRegex("^abc").ToLike(LikeDialect.Standard));    // abc% (exact)
Console.WriteLine(Pattern.FromRegex(@"abc\z").ToLike(LikeDialect.Standard));  // %abc (exact)
Console.WriteLine(Pattern.FromRegex(@"^abc\z").ToLike(LikeDialect.Standard)); // abc (exact)
```

- Use the default when the `LIKE` must agree with `pattern.IsMatch(value)`.
- Use `FullString` when the value must be the whole match, for example for a validation pattern you did not anchor.
- `LIKE` itself always compares the whole value. The `%` padding is what turns "contains a match" into a whole-value test.

### `$` and `\Z` versus `\z`

`$` (without `RegexOptions.Multiline`) and `\Z` match at the very end **or before a final newline**. `LIKE` can only cover that optional newline with a trailing `%`, so the result is a superset. End the pattern with `\z` when you want an exact translation.

```csharp
Pattern dollar = Pattern.FromRegex(@"^INV-\d{4}-\d{4}$").WithAsciiDigits();
LikeConversion like = dollar.ToLike(LikeDialect.SqlServer);

Console.WriteLine(like);
// INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]% (superset)
Console.WriteLine(like.Warnings[0]);
// '$' (and '\Z') also accept a trailing newline, which LIKE can only cover with a trailing '%';
// end the pattern with '\z' for an exact translation.
Console.WriteLine(dollar.IsMatch("INV-1234-5678\n")); // True
```

The same pattern with `\z` is exact, as the first sample on this page shows. In the fluent builder, `EndOfInput` writes `\z` and `EndOfLine` writes `$`.

## Dialects

| `LikeDialect` | Wildcards | Escaping |
|---------------|-----------|----------|
| `Standard` | `%`, `_` | `ESCAPE '\'` only when `%` or `_` must be literal. A backslash on its own is plain text. |
| `SqlServer` | `%`, `_`, `[set]`, `[^set]`, ranges such as `[0-9]` | `[` is escaped too. Inside a set the escape character is doubled. `ToSql` writes a Unicode literal `N'...'`. |
| `PostgreSql` | `%`, `_` | Backslash is the default escape character, so a literal `\` is always written `\\` and declared with `ESCAPE '\'`. |
| `MySql` | `%`, `_` | As PostgreSQL. `ToSql` also doubles every backslash in the string literal, because MySQL string literals process C escapes. Use it for MariaDB too. |
| `Sqlite` | `%`, `_` | As `Standard`. |

The same pattern in all five dialects:

```csharp
Pattern code = Pattern.FromRegex("a%b_c[d]");
LikeDialect[] dialects =
{
    LikeDialect.Standard, LikeDialect.SqlServer, LikeDialect.PostgreSql, LikeDialect.MySql, LikeDialect.Sqlite
};

foreach (LikeDialect dialect in dialects)
{
    Console.WriteLine($"{dialect}: {code.ToLike(dialect).ToSql("name")}");
}
// Standard: name LIKE '%a\%b\_c_%' ESCAPE '\'
// SqlServer: name LIKE N'%a\%b\_c[d]%' ESCAPE '\'
// PostgreSql: name LIKE '%a\%b\_c_%' ESCAPE '\'
// MySql: name LIKE '%a\\%b\\_c_%' ESCAPE '\\'
// Sqlite: name LIKE '%a\%b\_c_%' ESCAPE '\'
```

- The regex `[d]` is a one-character class. SQL Server keeps it as the set `[d]` and the result is exact. The other dialects turn it into `_`, so their results are supersets.
- The literal `%` and `_` of the regex are escaped with a backslash, and an `ESCAPE` clause is added.
- In the MySQL line, `'\\'` in a MySQL string literal is one backslash, so the database sees the same `LIKE` text as the others.

## SQL Server sets

Only SQL Server `LIKE` can say "one character from this set". With `LikeDialect.SqlServer`, a character class, a case-insensitive letter or an alternation of single characters becomes a set, so far more patterns can be exact.

```csharp
Console.WriteLine(Pattern.FromRegex(@"^(?:a|b)x\z").ToLike(LikeDialect.SqlServer)); // [a-b]x (exact)
Console.WriteLine(Pattern.FromRegex(@"^[a-z]{3}\z").ToLike(LikeDialect.SqlServer)); // [a-z][a-z][a-z] (exact)
Console.WriteLine(Pattern.FromRegex(@"(?i)^abc\z").ToLike(LikeDialect.SqlServer));  // [Aa][Bb][Cc] (exact)
Console.WriteLine(Pattern.FromRegex(@"^[^a-z]\z").ToLike(LikeDialect.SqlServer));   // [^a-z] (superset)
Console.WriteLine(Pattern.FromRegex(@"^\w\z").ToLike(LikeDialect.SqlServer));       // _ (superset)
Console.WriteLine(Pattern.FromRegex(@"^[\[\]]\z").ToLike(LikeDialect.SqlServer));   // _ (superset)
```

- A bounded repeat such as `{3}` is unrolled into one set per character.
- A negated set is never exact: it can match a whole supplementary character, such as an emoji, where the regex expects one UTF-16 code unit (see [Characters versus UTF-16 code units](#characters-versus-utf-16-code-units)).
- A class falls back to `_` when it cannot be written as a set: it has more than 100 ranges (such as `\w` or `\p{L}`), it has more than 100 members under `LikeCollation.CaseInsensitive` (where members are listed one by one, so `[Ā-ǿ]` becomes `_`), it contains set syntax (`[`, `]`, `^`, `-`, so `[a\]]` and `[a-]` become `_`) or a lone surrogate, or it would need a negated set under a case-insensitive collation.
- `%` and `_` are plain members inside a SQL Server set, so `^[%_]\z` becomes the exact set `[%_]`.
- Ranges are written only under `LikeCollation.Binary`, where they follow code-point order. Under `CaseInsensitive` every member is listed instead (see [Collations](#collations)).
- Members are written as raw characters, including control characters. `^a.c\z` becomes `a`, then `[^` + a raw line feed + `]`, then `c`.

### Unicode digits

.NET's `\d` matches every Unicode decimal digit: Arabic-Indic, Devanagari, full-width and many more. SQL Server can list them, and the result is exact, but the set is long.

```csharp
LikeConversion unicode = Pattern.FromRegex(@"^\d\z").ToLike(LikeDialect.SqlServer);

Console.WriteLine(unicode.IsExact);           // True
Console.WriteLine(unicode.Expression.Length); // 113
Console.WriteLine(unicode.Warnings[0]);
// A set contains non-ASCII characters; they are written literally in the LIKE pattern.

LikeConversion ascii = Pattern.FromRegex(@"^\d\z").WithAsciiDigits().ToLike(LikeDialect.SqlServer);

Console.WriteLine(ascii);                     // [0-9] (exact)
```

When your data only holds ASCII digits, call `WithAsciiDigits()` (or write `[0-9]`). `RegexOptions.ECMAScript` has the same effect on `\d`. See [Unicode](./unicode.md) for more on what `\d`, `\w` and `\s` match.

## Escaping and ToSql

`Expression` already contains every escape the dialect needs. `ToSql` adds the quoting:

- The column text is inserted verbatim. Quote it yourself (`[Number]`, `"number"`, `` `number` ``).
- Single quotes in the pattern are doubled: `'it''s'`.
- `SqlServer` writes a Unicode literal (`N'...'`), so non-ASCII characters survive.
- `MySql` doubles every backslash in the literal, including the one in `ESCAPE '\\'`.
- An `ESCAPE` clause is added only when `EscapeCharacter` is not `null`.

```csharp
Console.WriteLine(Pattern.FromRegex(@"^100%\z").ToLike(LikeDialect.PostgreSql).ToSql("rate"));
// rate LIKE '100\%' ESCAPE '\'
Console.WriteLine(Pattern.FromRegex(@"^x\[1]\z").ToLike(LikeDialect.SqlServer).ToSql("[Key]"));
// [Key] LIKE N'x\[1]' ESCAPE '\'
Console.WriteLine(Pattern.FromRegex(@"^x\[1]\z").ToLike(LikeDialect.Standard).ToSql("key"));
// key LIKE 'x[1]'
Console.WriteLine(Pattern.FromRegex("it's").ToLike(LikeDialect.Standard).ToSql("note"));
// note LIKE '%it''s%'
```

- `[` only needs escaping on SQL Server, where it starts a set. In the other dialects it is plain text.
- The escape character is always a backslash, and it is chosen only when something must be escaped.

### Backslashes

A literal backslash is plain text in `Standard`, `SqlServer` and `Sqlite`. PostgreSQL and MySQL treat a backslash as the escape character even without an `ESCAPE` clause, so there it is always doubled.

```csharp
Pattern folder = Pattern.FromRegex(@"^C:\\temp\z");
LikeDialect[] dialects =
{
    LikeDialect.Standard, LikeDialect.SqlServer, LikeDialect.PostgreSql, LikeDialect.MySql, LikeDialect.Sqlite
};

foreach (LikeDialect dialect in dialects)
{
    LikeConversion like = folder.ToLike(dialect);
    Console.WriteLine($"{dialect}: {like.Expression}  ->  {like.ToSql("path")}");
}
// Standard: C:\temp  ->  path LIKE 'C:\temp'
// SqlServer: C:\temp  ->  path LIKE N'C:\temp'
// PostgreSql: C:\\temp  ->  path LIKE 'C:\\temp' ESCAPE '\'
// MySql: C:\\temp  ->  path LIKE 'C:\\\\temp' ESCAPE '\\'
// Sqlite: C:\temp  ->  path LIKE 'C:\temp'
```

### The escape character inside a SQL Server set

Under an `ESCAPE` clause, SQL Server reads the escape character inside brackets too: `[\a]` is just `a`. So when the pattern needs an `ESCAPE` clause, a set member that is a backslash is doubled. Without a clause the backslash is a plain member: `^[\\a]\z` alone becomes the exact `[\a]` with no `ESCAPE`.

```csharp
LikeConversion like = Pattern.FromRegex(@"^%[\\a]\z").ToLike(LikeDialect.SqlServer);

Console.WriteLine(like.Expression);      // \%[\\a]
Console.WriteLine(like.EscapeCharacter); // \
Console.WriteLine(like.IsExact);         // True
```

### Trailing blanks on SQL Server

A `varchar` pattern compared with a `varchar` column ignores trailing blanks (`'abc ' LIKE 'abc'` is true). Because `ToSql` writes `N'...'`, the comparison is a Unicode one, under which trailing blanks count, and the exactness proof assumes that.

```csharp
LikeConversion like = Pattern.FromRegex(@"^abc \z").ToLike(LikeDialect.SqlServer);

Console.WriteLine(like.ToSql("[Code]")); // [Code] LIKE N'abc '
```

:::warning

If you send `Expression` to SQL Server yourself, pass it as an `nvarchar` parameter, not `varchar`. Otherwise trailing blanks are ignored and an exact result can accept more than the regex.

:::

## Collations

The column's collation changes what `LIKE` matches. Tell `ToLike` which kind you have.

| `LikeCollation` | Use it for | Model |
|-----------------|-----------|-------|
| `Binary` (default) | SQL Server `_BIN2`, PostgreSQL `C`, MySQL `_bin`, SQLite with `PRAGMA case_sensitive_like = ON` | Characters compare by code point. |
| `CaseInsensitive` | SQL Server `_CI_` collations, MySQL `_ci` collations, SQLite's default `LIKE` | Letters match regardless of case; everything else compares by code point. |

```csharp
using System.Text.RegularExpressions;

Pattern ignoreCase = Pattern.FromRegex(@"^abc\z", RegexOptions.IgnoreCase);
LikeCollation folding = LikeCollation.CaseInsensitive;

Console.WriteLine(ignoreCase.ToLike(LikeDialect.Standard));                                  // ___ (superset)
Console.WriteLine(ignoreCase.ToLike(LikeDialect.SqlServer));                                 // [Aa][Bb][Cc] (exact)
Console.WriteLine(ignoreCase.ToLike(LikeDialect.Standard, MatchScope.Substring, folding));    // abc (exact)

Pattern caseSensitive = Pattern.FromRegex(@"^abc\z");

Console.WriteLine(caseSensitive.ToLike(LikeDialect.Standard, MatchScope.Substring, folding)); // abc (superset)
Console.WriteLine(Pattern.FromRegex(@"^123-\z").ToLike(LikeDialect.Standard, MatchScope.Substring, folding));
// 123- (exact)
```

- A case-insensitive pattern on a binary collation needs a set per letter. Only SQL Server has sets, so the other dialects fall back to `_`.
- On a case-insensitive collation the letters can stay plain text, and the result is exact.
- A case-sensitive pattern with cased letters is **never** exact on a case-insensitive collation: `LIKE 'abc'` also accepts `ABC`. Patterns without letters, such as `^123-\z`, can still be exact.
- `Warnings` says why: "The pattern is case-sensitive, but a case-insensitive collation also matches other casings of its letters, so the LIKE pattern accepts more strings. Add RegexOptions.IgnoreCase to the pattern, or use LikeCollation.Binary with a case-sensitive column, for an exact translation."
- Every result that is not exact has at least one warning. When no specific construct is to blame, as for a dropped leading lookahead such as `^(?=ab)[a-z]{3}\z` on SQL Server, the warning is "The LIKE pattern accepts some strings that the pattern rejects."

### What every case-insensitive collation folds

Collations disagree on what they fold. The compiler only relies on what **every** case-insensitive collation does: an ASCII letter matches its ASCII partner (`a` and `A`), except `i` and `I`, which Turkish and Azerbaijani collations keep apart. Anything else .NET folds is spelled out:

- `(?i)k` also matches U+212A KELVIN SIGN, and, under most cultures, `(?i)i` also matches U+0130 (capital I with dot above). Many collations do not fold those, so these letters become a set listing the variants on SQL Server, or `_` elsewhere.
- `i` and `I` are never folded by the guarantee, because of Turkish and Azerbaijani collations, so `(?i)i` is never plain text. Non-ASCII letters are not covered either: `(?i)^é\z` becomes the exact set `[Éé]` on SQL Server and `_` in the other dialects.
- A negated set is never written under `CaseInsensitive`: there `[^a]` would also reject `A`, so it would not be a superset. The class becomes `_` or `%` instead.
- Ranges are never written under `CaseInsensitive`, because their order follows the collation rather than code points. The members are listed.

```csharp
// folding is LikeCollation.CaseInsensitive, as in the previous sample
Console.WriteLine(Pattern.FromRegex(@"^[a-c]\z").ToLike(LikeDialect.SqlServer, MatchScope.Substring, folding));
// [abc] (superset)
Console.WriteLine(Pattern.FromRegex(@"(?i)^[a-c]\z").ToLike(LikeDialect.SqlServer, MatchScope.Substring, folding));
// [ABCabc] (exact)
Console.WriteLine(Pattern.FromRegex(@"(?i)^kid\z").ToLike(LikeDialect.MySql, MatchScope.Substring, folding));
// __d (superset)
Console.WriteLine(Pattern.FromRegex(@"^[^a]").ToLike(LikeDialect.SqlServer, MatchScope.Substring, folding));
// % (superset)
```

On SQL Server the same `(?i)^kid\z` becomes an exact `[Kk` + U+212A + `][Ii` + U+0130 + `]d` under an English current culture, and a warning notes the non-ASCII set members. The case variants come from .NET, so they follow the current culture unless the pattern uses `RegexOptions.CultureInvariant` (see [Pitfalls](#pitfalls)).

The proof models a case-insensitive collation twice. The superset proof gives the `LIKE` pattern only the guaranteed folds above. The exactness proof gives it .NET's full case folding (`RegexOptions.IgnoreCase`). So exactness is relative to .NET's folding: see [Pitfalls](#pitfalls) for collations that fold more.

## Characters versus UTF-16 code units

A .NET regex sees UTF-16 code units, so an emoji is two characters to `.`. PostgreSQL, MySQL, MariaDB, SQLite and SQL Server's supplementary-character-aware collations (`_SC` and `_UTF8` collations) count code points, so for them `_` matches the whole emoji and `__` rejects it, even though `^..\z` accepts it.

To stay a superset everywhere, the compiler looks at positions that can match a surrogate code unit (`.`, `[^a]`, `\W`, `[\s\S]`, a lone surrogate). Two such positions in a row can be one database character, so a run of `k` of them becomes `_` repeated `ceil(k/2)` times, followed by `%`.

```csharp
Console.WriteLine(Pattern.FromRegex(@"^..\z").IsMatch("😀"));                   // True
Console.WriteLine(Pattern.FromRegex(@"^..\z").ToLike(LikeDialect.Standard));    // _% (superset)
Console.WriteLine(Pattern.FromRegex(@"^...\z").ToLike(LikeDialect.Standard));   // __% (superset)
Console.WriteLine(Pattern.FromRegex(@"^a.b.c\z").ToLike(LikeDialect.Standard)); // a_b_c (superset)
Console.WriteLine(Pattern.FromRegex(@"^\w\w\z").ToLike(LikeDialect.Standard));  // __ (superset)
Console.WriteLine(Pattern.FromRegex(@"^😀\z").ToLike(LikeDialect.Standard));    // 😀 (exact)
Console.WriteLine(
    Pattern.FromRegex(@"^..\z").ToLike(LikeDialect.SqlServer, MatchScope.Substring, LikeCollation.CaseInsensitive));
// % (superset)
```

- A single such position between text cannot pair up with anything, so `a.b.c` keeps one `_` per dot.
- `\w` and `[0-9]` cannot match a surrogate, so `\w\w` keeps one `_` each.
- A literal surrogate pair (the emoji itself) stays text and can be exact.
- On SQL Server under `CaseInsensitive`, every such position becomes `%`: legacy case-insensitive collations (such as the default `SQL_Latin1_General_CP1_CI_AS`) give supplementary characters no weight at all, so `_` matches no part of them.
- `IsExact` is `false` whenever the `LIKE` pattern contains `_` or a negated set, for the same reason: in a code-point collation they also accept a supplementary character where the regex expects one code unit.

## Constructs LIKE cannot express

`LIKE` has literal text, `_`, `%` and (on SQL Server) sets. Everything else is widened to something `LIKE` can say:

| Regex construct | Becomes |
|-----------------|---------|
| Literal text | The same text, escaped as needed. |
| Character class | A set (SQL Server, when it can be written) or `_`. |
| `[\s\S]*`, `(?s).*` | `%` |
| Bounded repeat `{n}` | Unrolled: `a{3}` becomes `aaa`. |
| Variable repeat `?`, `*`, `+`, `{m,n}`, lazy forms | The mandatory part, then `%`. |
| Alternation of single characters | Their union, as one set or `_`. |
| Any other alternation | `%`, or a shared prefix/suffix (see below). |
| Backreference, conditional | `%` |
| Lookarounds, `\b`, `\B`, `^` in the middle | Nothing (zero width). |
| `$`, `\Z` at the end | `%`, for the optional final newline. |

```csharp
Console.WriteLine(Pattern.FromRegex(@"^a[\s\S]*b\z").ToLike(LikeDialect.Standard)); // a%b (exact)
Console.WriteLine(Pattern.FromRegex(@"^a.*b\z").ToLike(LikeDialect.Standard));      // a%b (superset)
Console.WriteLine(Pattern.FromRegex(@"^\d{2,4}\z").ToLike(LikeDialect.Standard));   // __% (superset)
Console.WriteLine(Pattern.FromRegex(@"^a{3}\z").ToLike(LikeDialect.Standard));      // aaa (exact)
Console.WriteLine(Pattern.FromRegex(@"^(\w)\1abc\z").ToLike(LikeDialect.PostgreSql)); // _%abc (superset)
Console.WriteLine(Pattern.FromRegex(@"\bfoo\b").ToLike(LikeDialect.PostgreSql));      // %foo% (superset)
Console.WriteLine(Pattern.FromRegex(@"^abc$", RegexOptions.Multiline).ToLike(LikeDialect.PostgreSql));
// %abc% (superset)
```

- `^a.*b\z` is not exact because `.` does not match a newline, while `%` does.
- A pattern outside the regular subset cannot be proven by the algebra: backreferences, conditionals, atomic groups, lookbehinds, lookaheads anywhere but the very start, `\b`, `\B`, `^` and `$` under `RegexOptions.Multiline`, and `RegexOptions.RightToLeft`. The result is a superset by construction, `IsExact` is `false`, and a warning says it could not be proven. See [Pattern algebra](./pattern-algebra.md) for the supported subset.
- A lookahead at the very start is inside the subset. `LIKE` drops it, and the proof still decides: `^(?!x)abc\z` gives an exact `abc`, while `^(?=ab)[a-z]{3}\z` gives a superset.

## How the compiler picks the pattern

`ToLike` builds up to three candidates and keeps the tightest one it can prove safe.

1. **Compile from the syntax tree.** Each construct is widened as in the table above. In `Substring` scope a `%` is added on each open side. Runs of positions that can match a surrogate are widened as described earlier. Every step only widens the language, so the result is a superset by construction.
2. **Prove it.** The `LIKE` text is parsed back into a regex exactly as the dialect reads it (`_` becomes `[\s\S]`, `%` becomes `[\s\S]*`, sets become classes, escapes are honored inside sets too) and compared with the scoped pattern. It is exact when it is proven equivalent and has no `_` and no negated set.
3. **Compete with the language facts.** When the first candidate is not exact, a `prefix%suffix` candidate is built from the pattern's [language facts](./language-facts.md): the required prefix, then `%`, then the required suffix. The suffix is only kept when the minimum length can hold both, and the text alone is used when every match has exactly the prefix's length. A candidate proven not to be a superset is dropped, and `%` is the last resort. Of the rest, the one whose language is proven strictly smaller wins.

```csharp
Console.WriteLine(Pattern.FromRegex(@"^(?:aba|ababa)\z").ToLike(LikeDialect.Standard));      // aba% (superset)
Console.WriteLine(Pattern.FromRegex(@"^(?:ab|ac)\z").ToLike(LikeDialect.Standard));          // a% (superset)
Console.WriteLine(Pattern.FromRegex(@"^(?:red|green|blue)\z").ToLike(LikeDialect.Standard)); // % (superset)
Console.WriteLine(Pattern.FromRegex(@"^a|^b").ToLike(LikeDialect.SqlServer));                // [a-b]% (exact)
```

- `^(?:aba|ababa)\z` requires the prefix `aba` and the suffix `aba`, but the shortest match is only 3 characters long, so `aba%aba` would wrongly reject `aba`. The suffix is dropped.
- `^(?:red|green|blue)\z` has no shared prefix or suffix, so nothing better than `%` can be proven. On a database with regex support, prefer a regex translation: see [SQL dialects](./sql-dialects.md).
- `^a|^b` is an alternation of single characters at the start, so SQL Server writes it as one set.

### Free-spacing comments

The scoped pattern is built so that a trailing `#` comment in free-spacing mode (`RegexOptions.IgnorePatternWhitespace` or `(?x)`) cannot swallow the `Substring` padding.

```csharp
Pattern commented = Pattern.FromRegex("^INV-[0-9]{4} # invoice number", RegexOptions.IgnorePatternWhitespace);

Console.WriteLine(commented.ToLike(LikeDialect.SqlServer)); // INV-[0-9][0-9][0-9][0-9]% (exact)
```

## Fluent patterns

`ToLike` works on any `Pattern`, however it was built.

```csharp
Pattern sku = Pattern.With.StartOfInput.Literal("SKU-").Digit.Repeat.Times(5).EndOfInput;

Console.WriteLine(sku);                                                 // \ASKU-\d{5}\z
Console.WriteLine(sku.WithAsciiDigits().ToLike(LikeDialect.SqlServer)); // SKU-[0-9][0-9][0-9][0-9][0-9] (exact)
Console.WriteLine(sku.ToLike(LikeDialect.PostgreSql));                  // SKU-_____ (superset)
```

`Literal` text is escaped for you, both in the regex and in the `LIKE` pattern, so a `%` or `_` in a literal is always matched as itself: `Literal("50%_off")` between `StartOfInput` and `EndOfInput` becomes `'50\%\_off' ESCAPE '\'` in standard SQL.

## Warnings

`Warnings` lists the reasons a result is not exact, followed by notes that hold either way. The texts are fixed, so you can log them or show them to a developer.

| Warning starts with | Cause | How to get closer to exact |
|---------------------|-------|----------------------------|
| `A character class became '_' (any character); only SQL Server LIKE can express sets.` | A class on a dialect without sets. | Use `LikeDialect.SqlServer`, or accept the superset. |
| `A character class became a wildcard ('_', or '%' after the surrogate widening below) because it cannot be written as a SQL Server set` | `SqlServer` only: too many ranges or members, set syntax members, surrogates, or a negated set under `CaseInsensitive`. It replaces the two case warnings below when a letter's set cannot be written. | Narrow the class, for example `[a-z]` instead of `\w`. |
| `The pattern is case-sensitive, but a case-insensitive collation also matches other casings` | A pattern without `IgnoreCase` under `CaseInsensitive`, when it has cased letters. | Add `RegexOptions.IgnoreCase` to the pattern, or use `Binary` with a case-sensitive column. |
| `The LIKE pattern accepts some strings that the pattern rejects.` | The result is a superset, but no single construct is to blame, for example a dropped leading lookahead. | Check the other warnings; simplify the pattern where the data allows it. |
| `A case-insensitive letter under a binary collation became '_'` | `IgnoreCase` under `Binary` on a dialect without sets. | Use `CaseInsensitive` if the column folds case, or `SqlServer`. |
| `A case-insensitive letter has .NET case variants` | `IgnoreCase` under `CaseInsensitive` on a dialect without sets, for a letter whose .NET variants go beyond its ASCII partner: `k` (Kelvin sign), `i`, and non-ASCII letters such as `é`. | Accept the superset, or use `SqlServer`, which lists the variants in a set. |
| `A variable repeat (?, *, +, {m,n}) can only be covered by '%'` | An optional or repeated part. | Use a fixed count where the data allows it. |
| `An alternation of more than single characters can only be covered by '%'.` | `cat\|dog` and similar. | Split it into several `LIKE` conditions joined with `OR`. |
| `A construct LIKE cannot express (backreference, conditional) became '%'.` | `\1`, `(?(...)...)`. | Keep the regex check. |
| `'$' (and '\Z') also accept a trailing newline` | `$` or `\Z` at the end. | End the pattern with `\z`. |
| `Positions that can match a surrogate` | A run of `.`, `[^a]`, `\W` and similar. | Use a class that excludes surrogates, such as `\w` or `[ -~]`. |
| `'_' and negated sets match one database character` | The result is equivalent in UTF-16 but contains `_` or `[^...]`. | Accept the superset; see [Characters versus UTF-16 code units](#characters-versus-utf-16-code-units). |
| `The pattern is outside the regular subset` | Lookbehinds, non-leading lookaheads, `\b`, backreferences, atomic groups, `^`/`$` under `Multiline`, `RightToLeft` and similar. | Keep the regex check. |
| `The direct translation is not a proven superset under this collation` | The compiled candidate was rejected; a weaker one was used. | Check the collation you passed. |
| `A set contains non-ASCII characters` | A note: non-ASCII set members are written literally. | Make sure your SQL client sends Unicode (`ToSql` uses `N'...'`). |

## From the command line

The [command-line tool](./command-line-tool.md) exposes `ToLike` through `export --to like`.

```text
fluentregex export <regex> --to like [--dialect standard|sqlserver|postgresql|mysql|sqlite]
                                     [--scope substring|full] [--case-insensitive] [--column <sql>]
                                     [--ascii-digits] [--options <list>]
```

```text
> fluentregex export "^INV-\d{4}-\d{4}\z" --to like --dialect sqlserver --ascii-digits --column "[Number]"
[Number] LIKE N'INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'
exact: LIKE accepts exactly the strings the pattern accepts (proven)

> fluentregex export "^INV-\d{4}-\d{4}\z" --to like --dialect postgresql --column number
number LIKE 'INV-____-____'
superset: LIKE accepts every string the pattern accepts and possibly more; keep the regex check
warning: A character class became '_' (any character); only SQL Server LIKE can express sets.
```

- Without `--column` the tool prints the bare `Expression`.
- `--dialect` defaults to `standard` and also accepts `ansi`, `mssql`, `postgres`, `pg` and `mariadb`. `--scope` defaults to `substring` (alias `search`); `full`, `fullstring` and `full-string` select `MatchScope.FullString`.
- `--case-insensitive` selects `LikeCollation.CaseInsensitive`.

## With Entity Framework Core

The [Entity Framework Core](./entity-framework-core.md) package uses `ToLike` to translate patterns on SQL Server versions without regex support (`SqlDialect.SqlServer`). It always proves the `LIKE` for a binary comparison, then:

| `UseLikeFallback(...)` | Behavior |
|------------------------|----------|
| `LikeFallback.ExactOnly` (default) | Uses the `LIKE` only when it is exact; otherwise throws `InvalidOperationException` with guidance. |
| `LikeFallback.AllowSuperset` | Also uses a superset `LIKE`, and logs a warning through EF Core's logger. The query can then return extra rows. |
| `LikeFallback.Never` | Never uses `LIKE`: every pattern in a query throws. |

`UseLikeCollation` states what your SQL Server columns are. With its default, `LikeCollation.CaseInsensitive`, the package adds `COLLATE Latin1_General_100_BIN2` to every such `LIKE`, so the database compares the way the proof assumed. Declare `LikeCollation.Binary` for `_BIN2` columns to drop the `COLLATE`. The package caches the conversions per pattern, dialect, scope and collation (and per culture for culture-sensitive `IgnoreCase` patterns), so you do not have to.

## Pitfalls

- **Assuming `Binary` when the column is case-insensitive.** The default collation is `Binary`. On a `_CI_` column a "proven exact" result accepts other casings too. Pass the collation that matches your column.
- **SQL Server linguistic collations.** Every non-binary SQL Server collation (`_CI_` and `_CS_` alike) compares collation elements, not characters. This can break the superset guarantee itself, not only exactness. Characters without weight (NUL, U+00AD SOFT HYPHEN, unassigned code points) are skipped, so `_` or a set cannot match them. A base letter followed by a combining mark can form one element, so in the version-80 collations `abc%` rejects `abc` followed by U+0301 COMBINING ACUTE ACCENT, which `^abc` accepts. Expansions such as `æ` = `ae` make `LIKE` accept more. `LikeCollation.CaseInsensitive` models case folding only. On such data, compare under a `_BIN2` collation (for example with `COLLATE Latin1_General_100_BIN2`) and use `LikeCollation.Binary`.
- **`IgnoreCase` and the current culture.** The case variants of an `IgnoreCase` letter come from .NET and follow `CultureInfo.CurrentCulture`, so the same pattern can compile differently on different threads or machines. On SQL Server, `(?i)^i\z` gives `[Iiİ]` under `en-US`, `[iİ]` under `tr-TR`, and `[Ii]` with `RegexOptions.CultureInvariant`. Add `RegexOptions.CultureInvariant` when the result must not depend on the culture.
- **Accent-insensitive collations.** Collations that also fold accents (`_AI`, MySQL's default `utf8mb4_0900_ai_ci`) or fold more than .NET keep the result a superset, but an `IsExact` result can then accept more than the regex.
- **`$` instead of `\z`.** A trailing `$` always costs exactness. See [`$` and `\Z` versus `\z`](#-and-z-versus-z).
- **Unicode `\d` on SQL Server.** Without `WithAsciiDigits()` each `\d` is a set of every Unicode digit range. It is exact but long: `^\d\d\d\d\z` already produces 452 characters of `LIKE` text.
- **Very long patterns.** Bounded repeats are unrolled, so `^[a-z]{1000}\z` produces 1,000 sets (5,000 characters on SQL Server). Check `Expression.Length` against your database's limits.
- **Pasting generated SQL Server text.** Sets are written with raw characters, including a raw newline for `[^` + newline + `]`. T-SQL treats a backslash right before a newline in a string literal as a line continuation; avoid that sequence when you paste generated SQL by hand. Passing the text as a parameter avoids the issue.
- **Inserting the column name.** `ToSql` inserts `columnSql` verbatim. Never build it from user input.

## Examples

All results below are from `ToLike` with the default scope and collation.

| Pattern | `Standard` | `SqlServer` |
|---------|------------|-------------|
| `^abc` | `abc%` (exact) | `abc%` (exact) |
| `xyz\z` | `%xyz` (exact) | `%xyz` (exact) |
| `xyz$` | `%xyz%` (superset) | `%xyz%` (superset) |
| `^aba\z` | `aba` (exact) | `aba` (exact) |
| `^a[\s\S]*b\z` | `a%b` (exact) | `a%b` (exact) |
| `(?s)^a.c\z` | `a_c` (superset) | `a_c` (superset) |
| `^a.c\z` | `a_c` (superset) | `a[^` + newline + `]c` (superset) |
| `^..\z` | `_%` (superset) | `_%` (superset) |
| `\d+` | `%_%` (superset) | `%[0-9]%` with `WithAsciiDigits()` (exact) |
| `^(?:a\|b)x\z` | `_x` (superset) | `[a-b]x` (exact) |
| `^[a-z]{3}\z` | `___` (superset) | `[a-z][a-z][a-z]` (exact) |
| `^ab?ba\z` | `a%ba` (superset) | `a%ba` (superset) |
| `^(?:aba\|ababa)\z` | `aba%` (superset) | `aba%` (superset) |
| `^(\w)\1abc\z` | `_%abc` (superset) | `_%abc` (superset) |
| `a%b_c[d]` | `%a\%b\_c_%` ESCAPE `\` (superset) | `%a\%b\_c[d]%` ESCAPE `\` (exact) |
| `^%[\\a]\z` | `\%_` ESCAPE `\` (superset) | `\%[\\a]` ESCAPE `\` (exact) |
| `^\z` | (empty) (exact) | (empty) (exact) |
| (empty pattern) | `%` (exact) | `%` (exact) |

## Related pages

- [Language facts](./language-facts.md): the required prefix, suffix and length bounds behind the `prefix%suffix` candidate.
- [Pattern algebra](./pattern-algebra.md): the equivalence proofs `ToLike` relies on.
- [SQL dialects](./sql-dialects.md): translate a pattern to the regex functions of PostgreSQL, MySQL, MariaDB, SQL Server 2025 and SQLite.
- [Entity Framework Core](./entity-framework-core.md): use patterns in LINQ queries, with `LIKE` as the SQL Server fallback.
- [Export](./export.md): export patterns to other regex flavors and formats.
