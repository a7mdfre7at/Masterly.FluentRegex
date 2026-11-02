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
| PostgreSQL (Npgsql) | `WHERE p."Name" ~ '^[A-Z]{3}-[0-9]{4}$'` |
| MySQL 8 (Pomelo) | ``WHERE REGEXP_LIKE(`p`.`Name`, '^[A-Z]{3}-[0-9]{4}$', 'c')`` |
| MariaDB (Pomelo) | ``WHERE `p`.`Name` REGEXP '(?-i)^[A-Z]{3}-[0-9]{4}$'`` |
| SQL Server 2025 | `WHERE IIF(REGEXP_LIKE([p].[Name], N'^[A-Z]{3}-[0-9]{4}$'), 1, 0) = 1` |
| SQLite | `WHERE "p"."Name" REGEXP '^[A-Z]{3}-[0-9]{4}$'` |

`REGEXP_LIKE` is a predicate on SQL Server, not a function, so it is wrapped in `IIF(...) = 1`, which is valid
wherever EF Core places a value. MySQL's `'c'` match type and MariaDB's inline `(?-i)` restore .NET's default
case sensitivity, because both servers otherwise follow the (usually case-insensitive) column collation; a
pattern with `RegexOptions.IgnoreCase` embeds `(?i)` and stays case-insensitive.

A pattern held in a variable stays a query parameter; the dialect regex is produced when the query executes,
so changing the variable never recompiles the query:

```sql
-- @local='^[A-Z]{3}-[0-9]{4}$'
SELECT p."Id", p."Email", p."Name", p."Sku"
FROM "Products" AS p
WHERE p."Name" ~ @local
```

### Capture groups and replacement

```sql
-- PostgreSQL: Group(p.Sku, Patterns.SkuParts, "number")   (named groups are mapped to their numbers)
SELECT regexp_substr(p."Sku", '^(?<letters>[A-Z]{3})-(?<number>[0-9]{4})$', 1, 1, '', 2) FROM "Products" AS p
-- SQL Server 2025
SELECT REGEXP_SUBSTR([p].[Sku], N'^(?<letters>[A-Z]{3})-(?<number>[0-9]{4})$', 1, 1, 'c', 2) FROM [Products] AS [p]

-- Replace(p.Sku, Patterns.SkuParts, "$2/$1"): .NET substitutions are converted to the dialect's syntax
SELECT regexp_replace(p."Sku", '^([A-Z]{3})-([0-9]{4})$', '\2/\1', 'g') FROM "Products" AS p      -- PostgreSQL
SELECT REGEXP_REPLACE(`p`.`Sku`, '^([A-Z]{3})-([0-9]{4})$', '$2/$1') FROM `Products` AS `p`        -- MySQL
SELECT REGEXP_REPLACE(`p`.`Sku`, '(?-i)^([A-Z]{3})-([0-9]{4})$', '\\2/\\1') FROM `Products` AS `p` -- MariaDB
SELECT REGEXP_REPLACE([p].[Sku], N'^([A-Z]{3})-([0-9]{4})$', N'\2/\1') FROM [Products] AS [p]     -- SQL Server 2025
```

`$1`, `${1}`, `${name}` and `$$` are supported; `$&`, `` $` ``, `$'`, `$+` and `$_` throw `NotSupportedException`.
`$0` is the whole match on PostgreSQL (`\&`), MySQL (`$0`) and SQL Server 2025 (`\0`); MariaDB cannot express it.
Groups above 9 are only supported on MySQL. The replacement must be a literal string.

### Index hints

When the pattern's proven facts (from the core library) give a required prefix or a finite maximum length, they
are added as conjuncts. Every row that matches the regex satisfies them, so results do not change, but a
B-tree index on the column can narrow the scan:

```sql
WHERE p."Sku" ~ '^ABC-[0-9]{4}$' AND p."Sku" LIKE 'ABC-%' ESCAPE '\' AND length(p."Sku") <= 8               -- PostgreSQL
WHERE IIF(REGEXP_LIKE([p].[Sku], N'^ABC-[0-9]{4}$'), 1, 0) = 1
  AND [p].[Sku] LIKE N'ABC-%' ESCAPE N'\' AND CAST(LEN([p].[Sku]) AS int) <= 8                                -- SQL Server 2025
WHERE (REGEXP_LIKE(`p`.`Sku`, '^ABC-[0-9]{4}$', 'c') AND (`p`.`Sku` LIKE 'ABC-%' ESCAPE '\\'))
  AND (CHAR_LENGTH(`p`.`Sku`) <= 8)                                                                          -- MySQL
```

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

A NULL value never matches. In SQL the regex predicate on NULL yields NULL, so the row is excluded, and so is
`!Matches(...)` (three-valued logic): `Where(p => !PatternFunctions.Matches(p.Sku, Patterns.Sku))` does not return
rows with a NULL `Sku`. `Group` and `Replace` return NULL for NULL. `PatternFunctions` mirror this in .NET.

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
name or named replacements; reference the pattern through a static readonly field instead. Equal-but-distinct
pattern instances compile separately until `Pattern` gains value equality in the core library.

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

The constraint has whole-value semantics (`\A(?:pattern)\z`) and NULL passes, so nullable columns stay nullable.
`UseFluentRegex()` must be configured on the context: the constraint is emitted by a model-finalizing convention
that knows the dialect. A `ConstrainedPattern` contributes only its `Pattern`; `Must` rules stay in .NET.

```sql
CONSTRAINT "CK_Products_Sku_Pattern" CHECK ("Sku" ~ '\A(?:^[A-Z]{3}-[0-9]{4}$)\Z')                     -- PostgreSQL
CONSTRAINT `CK_Products_Sku_Pattern` CHECK (REGEXP_LIKE(`Sku`, '\\A(?:^[A-Z]{3}-[0-9]{4}$)\\z', 'c'))  -- MySQL
CONSTRAINT `CK_Products_Sku_Pattern` CHECK (`Sku` REGEXP '(?-i)\\A(?:^[A-Z]{3}-[0-9]{4}$)\\z')         -- MariaDB
CONSTRAINT [CK_Products_Sku_Pattern] CHECK (REGEXP_LIKE([Sku], N'\A(?:^[A-Z]{3}-[0-9]{4}$)\z'))         -- SQL Server 2025
CONSTRAINT "CK_Products_Sku_Pattern" CHECK ("Sku" REGEXP '\A(?:^[A-Z]{3}-[0-9]{4}$)\z')                -- SQLite
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

- The SQL regex dialect conversion, the LIKE conversion and the language facts come from the core library through
  one internal adapter (`CoreDialectAdapter`). Until the core API lands, the adapter passes the .NET regex text
  through with inline options, reports every non-SQLite translation as not exact, has no LIKE conversion
  (`%`, a superset) and no facts, so index hints are not emitted, `MatchingAsync` prefilters with `IS NOT NULL`
  on regex dialects other than SQLite, and SQL Server without regex support always throws under `ExactOnly`.
- Named groups: PostgreSQL has none and SQL Server 2025 (RE2) uses `(?P<name>...)`; the dialect conversion must
  rewrite them. Group names are mapped to numbers for `Group` and `${name}` replacements.
- `RegexOptions.CultureInvariant`, `RightToLeft`, `ECMAScript` and `NonBacktracking` throw `NotSupportedException`.
- `Group` needs PostgreSQL 15+ (`regexp_substr`) or SQL Server 2025; `Replace` is unavailable on SQLite.
- EF Core 10 keeps `EF.Constant(pattern)` as a parameter during translation (see the table above).
- Relational providers only; the package detects Npgsql, Pomelo, SQL Server and SQLite, other providers need
  `UseDialect`.
