# Entity Framework Core integration

New in 3.1: the `Masterly.FluentRegex.EntityFrameworkCore` package (.NET 8 with EF Core 8, .NET 10 with
EF Core 10) runs fluent patterns inside LINQ queries. Each pattern is translated to the database's own regex
dialect, proven facts about the pattern add index-friendly conjuncts, a hybrid operator gives exact results on
every database, and the same pattern becomes a CHECK constraint. Every SQL sample below was captured with
`ToQueryString()` / `GenerateCreateScript()`, and the semantics were verified against PostgreSQL 17, MySQL 8.4,
MariaDB 11.4 and SQL Server 2025 in containers, plus SQLite in memory.

```shell
dotnet add package Masterly.FluentRegex.EntityFrameworkCore
```

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;

public static class Patterns
{
    public static readonly Pattern Sku = Pattern.With
        .StartOfLine.UppercaseLetter.Repeat.Times(3).Literal("-").AsciiDigit.Repeat.Times(4).EndOfLine;
    public static readonly Pattern Digits = Pattern.With.AsciiDigit.Repeat.OneOrMore;
}

services.AddDbContext<ShopContext>(options => options
    .UseNpgsql(connectionString)
    .UseFluentRegex(fluent => fluent.AddCheckConstraintsFromAttributes()));
```

## Queries

Any of these is translated to SQL (the pattern is the instance, or an argument):

```csharp
db.Products.Where(p => Patterns.Sku.IsMatch(p.Name));                       // instance call
db.Products.Where(p => PatternFunctions.Matches(p.Sku, Patterns.Sku));      // null-safe helper, also runs in .NET
db.Products.Where(p => EF.Functions.Matches(p.Sku, Patterns.Sku));          // EF.Functions style
db.Products.WhereMatches(p => p.Sku, Patterns.Sku);                         // query operator
db.Products.Select(p => PatternFunctions.Group(p.Sku, Patterns.SkuParts, "number"));
db.Products.Select(p => PatternFunctions.Replace(p.Sku, Patterns.SkuParts, "$2/$1"));
```

Generated SQL for `Where(p => Patterns.Sku.IsMatch(p.Name))`:

| Dialect | SQL |
|---|---|
| PostgreSQL (Npgsql) | `WHERE p."Name" ~ '\A[A-Z]{3}-[0-9]{4}(?=\n?\Z)' AND length(p."Name") <= 9` |
| MySQL 8 (Pomelo) | ``WHERE REGEXP_LIKE(`p`.`Name`, '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)', 'c') AND (CHAR_LENGTH(`p`.`Name`) <= 9)`` |
| MariaDB (Pomelo) | ``WHERE `p`.`Name` REGEXP '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)' AND (CHAR_LENGTH(`p`.`Name`) <= 9)`` |
| SQL Server 2025 | `WHERE IIF(REGEXP_LIKE([p].[Name], N'\A[A-Z]{3}-[0-9]{4}\n?\z'), 1, 0) = 1 AND CAST(LEN([p].[Name]) AS int) <= 9` |
| SQLite | `WHERE "p"."Name" REGEXP '^[A-Z]{3}-[0-9]{4}$' AND length("p"."Name") <= 9` |

The regex is not the .NET text passed through: it is translated by the core library's
[SQL flavors](sql-flavors.md) so it keeps the exact .NET meaning on each engine. `$` accepts a final newline in
.NET, so it becomes `(?=\n?\Z)` (PostgreSQL), `(?=\n?\z)` (MySQL, MariaDB) or `\n?\z` (SQL Server, whose RE2 engine
has no lookahead); `\d`, `\w`, case-insensitivity and `\b` are written out the same way. SQLite runs the .NET
engine itself, so the text is unchanged. The `length` conjunct is an [index hint](#index-hints).

`REGEXP_LIKE` is a predicate on SQL Server, not a function, so it is wrapped in `IIF(...) = 1`, which is valid
wherever EF Core places a value. MySQL's `'c'` match type and the inline `(?-i)` restore .NET's default case
sensitivity, because MySQL and MariaDB otherwise follow the (usually case-insensitive) column collation; a pattern
with `RegexOptions.IgnoreCase` is expanded into explicit classes and stays case-insensitive.

A pattern held in a variable stays a query parameter; the dialect regex is produced when the query executes,
so changing the variable never recompiles the query:

```sql
-- @__local_0='\A[A-Z]{3}-[0-9]{4}(?=\n?\Z)'
SELECT p."Id", p."Email", p."Name", p."Sku"
FROM "Products" AS p
WHERE p."Name" ~ @__local_0
```

### Capture groups and replacement

```sql
-- Group(p.Sku, Patterns.SkuParts, "number"): PostgreSQL has no named groups, so names are removed and mapped
-- to their numbers; SQL Server 2025 (RE2) spells them (?P<name>...)
SELECT regexp_substr(p."Sku", '\A([A-Z]{3})-([0-9]{4})(?=\n?\Z)', 1, 1, '', 2)                    -- PostgreSQL
SELECT REGEXP_SUBSTR([p].[Sku], N'\A(?P<letters>[A-Z]{3})-(?P<number>[0-9]{4})\n?\z', 1, 1, 'c', 2) -- SQL Server 2025

-- Replace(p.Sku, Patterns.SkuParts, "$2/$1"): .NET substitutions are converted to the dialect's syntax
SELECT regexp_replace(p."Sku", '\A([A-Z]{3})-([0-9]{4})(?=\n?\Z)', '\2/\1', 'g')              -- PostgreSQL
SELECT REGEXP_REPLACE(`p`.`Sku`, '(?-i)\\A([A-Z]{3})-([0-9]{4})(?=\\n?\\z)', '$2/$1')           -- MySQL
SELECT REGEXP_REPLACE(`p`.`Sku`, '(?-i)\\A([A-Z]{3})-([0-9]{4})(?=\\n?\\z)', '\\2/\\1')         -- MariaDB
SELECT REGEXP_REPLACE([p].[Sku], N'\A([A-Z]{3})-([0-9]{4})\n?\z', N'\2/\1')                     -- SQL Server 2025
```

`$1`, `${1}`, `${name}` and `$$` are supported; `$&`, `` $` ``, `$'`, `$+` and `$_` throw `NotSupportedException`.
`$0` is the whole match on PostgreSQL (`\&`), MySQL (`$0`) and SQL Server 2025 (`\0`); MariaDB cannot express it.
Groups above 9 are only supported on MySQL. The replacement must be a literal string.

Group numbers are .NET's, and they are mapped to the database's own numbering, which often differs: .NET numbers
unnamed groups before named ones, every engine numbers left to right, and PostgreSQL drops groups inside
lookarounds. For `(?<year>\d{4})-(\d{2})`, `Group(col, pattern, 1)` returns the month on every database, as in
.NET, even though PostgreSQL calls that group 2. The mapping needs the pattern at query compile time, so a group
reference into a pattern held in a variable throws (except group 0, the whole match). A group number held in a
variable is accepted only when the pattern numbers its groups the same way in .NET and in the database.

### Index hints

The core library's [language facts](language-facts.md) prove, from the pattern's minimal automaton, the text every
match starts with and the longest possible match. When they give a required prefix or a finite maximum length,
they are added as conjuncts. Every row that matches the regex satisfies them, so results do not change, but a
B-tree index on the column can narrow the scan. For `WhereMatches(p => p.Sku, Pattern.FromRegex("^ABC-[0-9]{4}$"))`:

```sql
WHERE p."Sku" IS NOT NULL AND p."Sku" ~ '\AABC-[0-9]{4}(?=\n?\Z)'
  AND p."Sku" LIKE 'ABC-%' ESCAPE '\' AND length(p."Sku") <= 9                                    -- PostgreSQL
WHERE [p].[Sku] IS NOT NULL AND IIF(REGEXP_LIKE([p].[Sku], N'\AABC-[0-9]{4}\n?\z'), 1, 0) = 1
  AND [p].[Sku] LIKE N'ABC-%' ESCAPE N'\' AND CAST(LEN([p].[Sku]) AS int) <= 9                     -- SQL Server 2025
WHERE `p`.`Sku` IS NOT NULL AND ((REGEXP_LIKE(`p`.`Sku`, '(?-i)\\AABC-[0-9]{4}(?=\\n?\\z)', 'c')
  AND (`p`.`Sku` LIKE 'ABC-%' ESCAPE '\\')) AND (CHAR_LENGTH(`p`.`Sku`) <= 9))                    -- MySQL
```

The bound is 9, not 8, because `$` also accepts one final newline; end the pattern with `\z` to get 8. The
`IS NOT NULL` guard is the [NULL rule](#null-rule); EF Core drops it for required columns.

Only the upper length bound is emitted: SQL length functions count code points (`LEN` also strips trailing
spaces), so a lower bound computed in UTF-16 units could exclude matches. Hints apply to patterns known at query
compile time (see below); `UseIndexHints(false)` turns them off.

## `MatchingAsync`: exact results on every database

```csharp
await foreach (Product product in db.Products.MatchingAsync(p => p.Sku, Patterns.Sku)) { ... }
List<Product> list = await db.Products.ToMatchingListAsync(p => p.Email, CommonPatterns.Email());
List<Product> luhn = await db.Products.ToMatchingListAsync(p => p.Card, CommonPatterns.CreditCard().Must(Validators.Luhn));
```

The SQL applies a prefilter that is a proven superset of the pattern, then the .NET engine applies the exact
pattern (and the `Must` rules of a `ConstrainedPattern`) to each candidate:

1. the exact dialect regex when the translation is exact (plus the index hints);
2. otherwise the superset `LIKE` and the upper length bound from the facts (`col IS NOT NULL AND col LIKE ...`);
3. otherwise only `col IS NOT NULL`.

This is the operator to use for lookarounds, backreferences and other constructs a dialect cannot express, for
SQL Server before 2025, and for `ConstrainedPattern`, whose `IsMatch` throws in a query with the same advice.

## NULL rule

A NULL value never matches, exactly as in .NET: `PatternFunctions.Matches(null, pattern)` is `false`, so
`Where(p => !PatternFunctions.Matches(p.Sku, Patterns.Sku))` returns the rows with a NULL `Sku`, as LINQ to Objects
would. The translation guards nullable columns with `col IS NOT NULL AND ...` (EF Core drops the guard for required
columns), so the result never depends on how SQL's three-valued logic treats the index-hint conjuncts: a query
returns the same rows with and without `UseIndexHints`. `Group` and `Replace` return NULL for NULL.

## Constants and parameters (EF Core 8 vs EF Core 10)

The translator receives the pattern either as a compile-time constant or as a query parameter. Only a constant
can be inspected at translation time, which index hints, `Group` by name, `${name}` replacements and the SQL
Server `LIKE` fallback require. Verified with the test suite:

| How the pattern is referenced | EF Core 8 | EF Core 10 |
|---|---|---|
| `static readonly` field | constant | constant |
| inline construction, e.g. `Pattern.FromRegex("...")` in the lambda | constant | constant |
| `WhereMatches`, `MatchingAsync` | constant | constant |
| `EF.Constant(pattern)` | constant | parameter during translation, inlined as a literal in the final SQL |
| captured local variable | parameter | parameter |
| static property | parameter | parameter |

On EF Core 10 `EF.Constant` therefore gives correct SQL but no index hints, and cannot be used with `Group` by
name or named replacements; reference the pattern through a static readonly field instead. `Pattern` has value
equality (same regex, options and timeout), so equal pattern instances share one entry in EF Core's query cache,
and the package memoizes each translation, `LIKE` proof and fact set per pattern.

## CHECK constraints

```csharp
modelBuilder.Entity<Product>().Property(p => p.Sku).HasPattern(Patterns.Sku);                // CK_Products_Sku_Pattern
modelBuilder.Entity<Product>().Property(p => p.Sku).HasPattern(Patterns.Sku, "CK_Sku");

public sealed class Order
{
    [MatchesPattern(typeof(Patterns), nameof(Patterns.Sku))]   // needs AddCheckConstraintsFromAttributes()
    public string? Sku { get; set; }
}
```

The constraint has the semantics of `Pattern.IsMatch` and `[MatchesPattern]`, so the database accepts exactly the
values your validator accepts and the two can never disagree. Anchor the pattern to constrain the whole value:
`^...$` (which, as in .NET, also accepts one final newline) or `^...\z`. NULL passes, so nullable columns stay
nullable. `UseFluentRegex()` must be configured on the context: the constraint is emitted by a model-finalizing
convention that knows the dialect. A `ConstrainedPattern` contributes only its `Pattern`; `Must` rules stay in .NET.

```sql
CONSTRAINT "CK_Products_Sku_Pattern" CHECK ("Sku" ~ '\A[A-Z]{3}-[0-9]{4}(?=\n?\Z)')                         -- PostgreSQL
CONSTRAINT `CK_Products_Sku_Pattern` CHECK (REGEXP_LIKE(`Sku`, '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)', 'c')) -- MySQL
CONSTRAINT `CK_Products_Sku_Pattern` CHECK (`Sku` REGEXP '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)')            -- MariaDB
CONSTRAINT [CK_Products_Sku_Pattern] CHECK (REGEXP_LIKE([Sku], N'\A[A-Z]{3}-[0-9]{4}\n?\z'))                -- SQL Server 2025
CONSTRAINT "CK_Products_Sku_Pattern" CHECK ("Sku" REGEXP '^[A-Z]{3}-[0-9]{4}$')                            -- SQLite
```

On SQL Server without regex support the constraint is emitted only when the pattern has an exact `LIKE` form;
otherwise model building throws and names the property. The SQLite constraint relies on the `regexp` function the
EF Core provider registers on its connections, so other tools cannot insert into that table.

## Raw SQL

```csharp
string where = SqlRegex.Condition("\"Email\"", Patterns.Email, SqlDialect.PostgreSql);   // "Email" ~ '...'
IEnumerable<User> users = connection.Query<User>($"SELECT * FROM users WHERE {where}");
```

`SqlRegex.Condition` has `IsMatch` semantics and quotes the regex for the dialect (`N'...'` on SQL Server, doubled
backslashes on MySQL/MariaDB). When the text goes through EF Core's `FromSqlRaw` / `SqlQueryRaw`, escape braces
(`{` as `{{`), which EF Core reads as parameter placeholders.

## Configuration

```csharp
options.UseFluentRegex(fluent => fluent
    .UseDialect(SqlDialect.SqlServer2025)          // override detection, e.g. Azure SQL without an explicit level
    .UseIndexHints(true)                           // default
    .UseLikeFallback(LikeFallback.AllowSuperset)   // SQL Server without regex: superset LIKE + logged warning
    .UseLikeCollation(LikeCollation.Binary)        // SQL Server columns use a _BIN2 or _CS_AS collation
    .AddCheckConstraintsFromAttributes());         // [MatchesPattern] -> CHECK constraints
```

Dialect detection: Npgsql -> PostgreSQL; Pomelo -> MySQL, or MariaDB when the configured `ServerVersion` is
MariaDB; Microsoft.Data.Sqlite -> SQLite; SQL Server -> `SqlServer2025` when `UseCompatibilityLevel(170)` or later
is configured (or the engine is Azure SQL on EF Core 10, whose default level is 170), otherwise `SqlServer`. An
unknown provider throws and asks for `UseDialect`.

On `SqlDialect.SqlServer` the default `LikeFallback.ExactOnly` translates a pattern only when its `LIKE` form is
exact; otherwise an `InvalidOperationException` names the pattern and suggests `MatchingAsync`,
`UseDialect(SqlDialect.SqlServer2025)` or `LikeFallback.AllowSuperset`. `AllowSuperset` logs
`FluentRegexEventId.LikeSupersetFallback` (a warning) through EF Core's logger; `ConfigureWarnings` can make it
throw. Translation differences reported by the dialect conversion are logged as
`FluentRegexEventId.TranslationWarning`.

### SQL Server `LIKE` and collations

Whether a `LIKE` is exact depends on what the column's collation does to it, so the proof needs to know.
`UseLikeCollation` defaults to `LikeCollation.CaseInsensitive`, which matches SQL Server's default collations:
`LIKE 'INV-%'` also accepts `inv-`, so a case-sensitive pattern with letters is never exact, and sets list their
members instead of using ranges, whose order follows the collation. With `LikeCollation.Binary` (`_BIN2`, `_CS_AS`
columns) ranges are safe and case is exact:

```csharp
public static readonly Pattern Sku = Pattern.FromRegex(@"^[A-Z]{3}-[0-9]{4}\z");

options.UseSqlServer(cs).UseFluentRegex(fluent => fluent.UseLikeCollation(LikeCollation.Binary));
db.Products.WhereMatches(p => p.Sku, Sku);
// WHERE [p].[Sku] LIKE N'[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]'      -- proven exact, no regex engine needed
```

End such patterns with `\z`: `$` also accepts a final newline, which `LIKE` can only cover with a trailing `%`.

`LIKE` patterns are always sent as Unicode literals (`N'...'`), also for `varchar` columns: SQL Server best-fit
maps characters in a non-Unicode literal (`'lemon' LIKE 'ł%'` is true) and ignores trailing blanks in non-Unicode
comparisons, and either would break the proof. With Windows collations SQL Server still seeks an index on the
`varchar` column; with legacy `SQL_*` collations the conversion can turn the seek into a scan.
The `MatchingAsync` prefilter on the other dialects compiles its `LIKE` for a binary collation, which stays a
superset under any case- or accent-insensitive collation.

## Dialect support matrix

| | PostgreSQL | MySQL 8 | MariaDB | SQL Server 2025 | SQL Server | SQLite |
|---|---|---|---|---|---|---|
| `IsMatch` / `Matches` / `WhereMatches` | `~` | `REGEXP_LIKE(..., 'c')` | `REGEXP` | `REGEXP_LIKE` | exact `LIKE` or throw | `REGEXP` |
| `Group` | `regexp_substr` (PG 15+) | no | no | `REGEXP_SUBSTR` | no | no |
| `Replace` | `regexp_replace(..., 'g')` | `REGEXP_REPLACE` | `REGEXP_REPLACE` | `REGEXP_REPLACE` | no | no |
| CHECK constraint | yes | yes | yes | yes | exact `LIKE` or throw | yes |
| Index hints | yes | yes | yes | yes | n/a | yes |
| `MatchingAsync` | yes | yes | yes | yes | yes (LIKE superset) | yes |
| LIKE fallback | n/a | n/a | n/a | n/a | `ExactOnly` / `AllowSuperset` / `Never` | n/a |

## Known limitations

- The regex text comes from the core library's [SQL flavors](sql-flavors.md), so its limits apply: constructs an
  engine cannot express (atomic groups everywhere; lookarounds and backreferences on SQL Server 2025; lookarounds,
  backreferences and conditionals on SQLite, whose EF Core `regexp()` uses the non-backtracking engine) throw
  `NotSupportedException` at query translation, never at run time with different results. Use `MatchingAsync`
  for them. Group names are rewritten for PostgreSQL (none) and SQL Server 2025 (`(?P<name>...)`) and mapped to
  numbers for `Group` and `${name}` replacements.
- `RegexOptions.CultureInvariant`, `RightToLeft`, `ECMAScript` and `NonBacktracking` throw `NotSupportedException`.
- Exactness covers text in the Basic Multilingual Plane. For input containing supplementary characters, such as
  emoji, the engines count code points where .NET counts UTF-16 units, so `.` or `{n}` can differ there; see
  [SQL flavors](sql-flavors.md#verification) for the live verification against every supported engine.
- `Group` needs PostgreSQL 15+ (`regexp_substr`) or SQL Server 2025; `Replace` is unavailable on SQLite.
- EF Core 10 keeps `EF.Constant(pattern)` as a parameter during translation (see the table above).
- Relational providers only; the package detects Npgsql, Pomelo, SQL Server and SQLite, other providers need
  `UseDialect`.
