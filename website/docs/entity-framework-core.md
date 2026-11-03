---
id: entity-framework-core
title: "Entity Framework Core"
description: "Run patterns inside LINQ queries on PostgreSQL, MySQL, MariaDB, SQL Server and SQLite, with index hints, exact hybrid matching and CHECK constraints."
---

The `Masterly.FluentRegex.EntityFrameworkCore` package lets you use a `Pattern` inside a LINQ query. EF Core translates the pattern to the database's own regex dialect, so the filter runs in SQL and keeps the exact .NET meaning. The same pattern can also become a database `CHECK` constraint, so the database and your validator always accept the same values.

Use it when you filter, extract or replace text in the database and want one pattern for your C# code, your queries and your schema.

| You want to | Use |
|-------------|-----|
| Filter rows in SQL | `pattern.IsMatch(column)`, `PatternFunctions.Matches`, `EF.Functions.Matches`, `WhereMatches` |
| Extract a capture group in SQL | `PatternFunctions.Group` or `EF.Functions.Group` |
| Replace text in SQL | `PatternFunctions.Replace` or `EF.Functions.Replace` |
| Get exact results on any database, for any pattern | `MatchingAsync`, `ToMatchingListAsync` |
| Enforce a pattern in the schema | `HasPattern(...)` or `[MatchesPattern]` with `AddCheckConstraintsFromAttributes()` |
| Write raw SQL for Dapper or ADO.NET | `SqlRegex.Condition` |

The package's own types live in the `Masterly.FluentRegex.EntityFrameworkCore` namespace. `Pattern`, `ConstrainedPattern`, `LikeCollation` and `[MatchesPattern]` come from the core library, in the `Masterly.FluentRegex` namespace, so most files need both `using` directives.

:::note

The package targets `net8.0` (EF Core 8) and `net10.0` (EF Core 10). There is no `netstandard2.1` build. Besides the core `Masterly.FluentRegex` package it references only `Microsoft.EntityFrameworkCore.Relational`, so it works with relational providers and you install your database provider as usual. See [Installation](./installation.md).

:::

## Enabling the integration

Install the package next to your provider, then call `UseFluentRegex()` after the provider call.

```shell
dotnet add package Masterly.FluentRegex.EntityFrameworkCore
```

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// services and connectionString come from your application's startup code
services.AddDbContext<ShopContext>(options => options
    .UseNpgsql(connectionString)
    .UseFluentRegex());
```

- `UseFluentRegex()` registers the query translator and the `CHECK` constraint convention, and detects the SQL dialect from the provider.
- There are two overloads: one on `DbContextOptionsBuilder` and a generic one on `DbContextOptionsBuilder<TContext>`. Both return the same builder, so you can keep chaining. A `null` builder throws `ArgumentNullException`.
- The optional `configure` argument receives a `FluentRegexOptionsBuilder`. Calling `UseFluentRegex` again updates the same settings instead of adding a second copy.
- Contexts configured with different FluentRegex options get separate EF Core internal service providers, so they never interfere. If you share one internal service provider yourself (`UseInternalServiceProvider`), configure FluentRegex the same way on every context, or EF Core throws `InvalidOperationException`.

The rest of this page uses these types:

```csharp
using Masterly.FluentRegex;
using Microsoft.EntityFrameworkCore;

public static class Patterns
{
    public static readonly Pattern Sku = Pattern.With
        .StartOfLine.UppercaseLetter.Repeat.Times(3).Literal("-").AsciiDigit.Repeat.Times(4).EndOfLine;

    public static readonly Pattern SkuParts = Pattern.FromRegex(@"^(?<letters>[A-Z]{3})-(?<number>[0-9]{4})\z");

    public static readonly Pattern Digits = Pattern.With.AsciiDigit.Repeat.OneOrMore;
}

public sealed class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }
}

public sealed class ShopContext : DbContext
{
    public ShopContext(DbContextOptions<ShopContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
}
```

- `Patterns.Sku` is `^[A-Z]{3}-[0-9]{4}$`. `Patterns.Digits` is `[0-9]+`.
- Keep query patterns in `static readonly` fields. The translator can then inspect them when it compiles the query, which index hints, named groups and the SQL Server `LIKE` fallback need. See [Constants and parameters on EF Core 8 and 10](#constants-and-parameters-on-ef-core-8-and-10).

### Options

Configure the translation through the lambda you pass to `UseFluentRegex`:

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// services and connectionString come from your application's startup code
services.AddDbContext<ShopContext>(options => options
    .UseSqlServer(connectionString)
    .UseFluentRegex(fluent => fluent
        .UseDialect(SqlDialect.SqlServer2025)          // skip detection
        .UseIndexHints(true)                           // the default
        .UseLikeFallback(LikeFallback.ExactOnly)       // the default
        .UseLikeCollation(LikeCollation.Binary)        // columns use a _BIN2 collation
        .AddCheckConstraintsFromAttributes()));        // [MatchesPattern] becomes CHECK
```

| Method | Default | What it does |
|--------|---------|--------------|
| `UseDialect(SqlDialect dialect)` | Detected from the provider | Uses this dialect instead of detecting it. Needed for providers the package does not know, and for SQL Server 2025 when the compatibility level is not configured. The SQL is then written for that dialect whatever the provider is, so pick the one your server speaks. An undefined enum value throws `ArgumentOutOfRangeException`. |
| `UseIndexHints(bool enabled = true)` | On | Adds proven `LIKE 'prefix%'` and length conjuncts next to the regex. See [Index hints](#index-hints). |
| `UseLikeFallback(LikeFallback fallback)` | `ExactOnly` | Chooses how patterns translate on SQL Server before 2025. See [SQL Server before 2025](#sql-server-before-2025). |
| `UseLikeCollation(LikeCollation collation)` | `CaseInsensitive` | Tells the package whether SQL Server columns use a binary (`_BIN2` or `_BIN`) collation. Other databases ignore it. See [SQL Server LIKE and collations](#sql-server-like-and-collations). |
| `AddCheckConstraintsFromAttributes(bool enabled = true)` | Off | Turns `[MatchesPattern]` on string properties into `CHECK` constraints. See [CHECK constraints](#check-constraints). |

Every method returns the same `FluentRegexOptionsBuilder`, so you can chain them.

### How the dialect is detected

The package reads the registered provider without referencing any provider package:

| Provider | Dialect (`SqlDialect`) | Regex engine |
|----------|------------------------|--------------|
| Npgsql (`UseNpgsql`) | `PostgreSql` | PostgreSQL advanced regular expressions |
| Pomelo (`UseMySql`) with a MySQL server version | `MySql` | ICU (MySQL 8.0 and later) |
| Pomelo (`UseMySql`) with a MariaDB server version | `MariaDb` | PCRE (MariaDB 10.0.5 and later) |
| Oracle's `MySql.EntityFrameworkCore` (`UseMySQL`) | `MySql` | ICU |
| SQL Server with `UseCompatibilityLevel(170)` or later, or Azure SQL (`UseAzureSql`) on EF Core 10 | `SqlServer2025` | RE2 |
| SQL Server without that level | `SqlServer` | None, `LIKE` only |
| Microsoft.Data.Sqlite (`UseSqlite`) | `Sqlite` | The .NET engine, through the `regexp()` function EF Core registers |

With any other provider and no `UseDialect`, creating the context throws `InvalidOperationException` (EF Core builds its internal services in the `DbContext` constructor):

```text
FluentRegex could not detect the SQL dialect of the database provider '...'. Supported providers are Npgsql,
Pomelo MySQL/MariaDB, Microsoft SQL Server and SQLite; for another provider configure the dialect explicitly
with UseFluentRegex(fluent => fluent.UseDialect(...)).
```

The message does not name Oracle's `MySql.EntityFrameworkCore`, but that provider is detected too, as the table shows.

:::warning

Without `UseFluentRegex()`, a query that calls `IsMatch` fails with EF Core's usual "could not be translated" `InvalidOperationException`, `HasPattern` silently creates no constraint, and `dotnet ef migrations add` fails with `Cannot scaffold C# literals of type 'Masterly.FluentRegex.Pattern'`. Always register it on every context that uses patterns.

:::

## Filtering rows in SQL

There are four ways to filter with a pattern. They all translate the same way.

```csharp
using System.Collections.Generic;
using System.Linq;
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

SqliteConnection connection = new SqliteConnection("DataSource=:memory:");
connection.Open();

DbContextOptions<ShopContext> options = new DbContextOptionsBuilder<ShopContext>()
    .UseSqlite(connection)
    .UseFluentRegex()
    .Options;

await using ShopContext db = new ShopContext(options);
await db.Database.EnsureCreatedAsync();

db.Products.AddRange(
    new Product { Name = "Desk lamp", Sku = "ABC-1234" },
    new Product { Name = "Coffee mug", Sku = "abc-1234" },
    new Product { Name = "Monitor 27", Sku = "XYZ-0001" },
    new Product { Name = "Cable", Sku = null },
    new Product { Name = "Bookshelf 7", Sku = "ABC-0007" });
await db.SaveChangesAsync();

List<string> withDigits = await db.Products
    .Where(p => Patterns.Digits.IsMatch(p.Name))                     // 1. instance call
    .Select(p => p.Name)
    .ToListAsync();                                                   // Monitor 27, Bookshelf 7

List<string?> validSkus = await db.Products
    .Where(p => PatternFunctions.Matches(p.Sku, Patterns.Sku))       // 2. null-safe helper
    .Select(p => p.Sku)
    .ToListAsync();                                                   // ABC-1234, XYZ-0001, ABC-0007

IQueryable<Product> viaEfFunctions =
    db.Products.Where(p => EF.Functions.Matches(p.Sku, Patterns.Sku)); // 3. EF.Functions style

IQueryable<Product> viaOperator =
    db.Products.WhereMatches(p => p.Sku, Patterns.Sku);                // 4. query operator
```

Forms 2, 3 and 4 send this SQL to SQLite (from `ToQueryString()`):

```sql
SELECT "p"."Id", "p"."Name", "p"."Sku"
FROM "Products" AS "p"
WHERE "p"."Sku" IS NOT NULL AND "p"."Sku" REGEXP '^[A-Z]{3}-[0-9]{4}$' AND length("p"."Sku") <= 9
```

| Form | Signature | Notes |
|------|-----------|-------|
| Instance call | `pattern.IsMatch(string input)` | Natural on non-nullable columns. `IsMatch` takes a non-null `string`, so prefer form 2 on nullable columns. |
| `PatternFunctions.Matches` | `bool Matches(string? value, Pattern pattern)` | Null-safe. Also runs in .NET with the same result, so the same expression works in memory and in a query. |
| `EF.Functions.Matches` | `bool Matches(this DbFunctions functions, string? value, Pattern pattern)` | Same translation. Throws `InvalidOperationException` if it runs in .NET instead of SQL. |
| `WhereMatches` | `IQueryable<T> WhereMatches<T>(this IQueryable<T> source, Expression<Func<T, string?>> selector, Pattern pattern)` | Embeds the pattern as a constant, so index hints always apply, even for a pattern held in a variable. Null arguments throw `ArgumentNullException`. |

- `IsMatch` has `Pattern.IsMatch` semantics: the pattern matches anywhere in the value unless you anchor it.
- The `IS NOT NULL` guard is the [NULL rule](#null-never-matches). EF Core drops it for required columns.
- The `length(...) <= 9` conjunct is an [index hint](#index-hints).
- A pattern the dialect cannot express throws `NotSupportedException` when the query is translated. It never runs with different results. Use [`MatchingAsync`](#exact-results-on-every-database-with-matchingasync) for those patterns.

### Generated SQL per dialect

The regex is not your .NET text passed through. The core library's [SQL dialects](./sql-dialects.md) translation rewrites it so it keeps the exact .NET meaning on each engine. This is the `WHERE` clause of `WhereMatches(p => p.Sku, Patterns.Sku)` on each dialect:

```sql
-- PostgreSQL (Npgsql)
WHERE p."Sku" IS NOT NULL AND p."Sku" ~ '\A[A-Z]{3}-[0-9]{4}(?=\n?\Z)' AND length(p."Sku") <= 9

-- MySQL 8 (Pomelo, or Oracle's MySql.EntityFrameworkCore)
WHERE `p`.`Sku` IS NOT NULL AND (REGEXP_LIKE(`p`.`Sku`, '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)', 'c') AND (CHAR_LENGTH(`p`.`Sku`) <= 9))

-- MariaDB (Pomelo with a MariaDB server version)
WHERE `p`.`Sku` IS NOT NULL AND (`p`.`Sku` REGEXP '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)' AND (CHAR_LENGTH(`p`.`Sku`) <= 9))

-- SQL Server 2025 and Azure SQL
WHERE [p].[Sku] IS NOT NULL AND IIF(REGEXP_LIKE([p].[Sku], N'\A[A-Z]{3}-[0-9]{4}\n?\z'), 1, 0) = 1 AND CAST(LEN([p].[Sku]) AS int) <= 9

-- SQLite
WHERE "p"."Sku" IS NOT NULL AND "p"."Sku" REGEXP '^[A-Z]{3}-[0-9]{4}$' AND length("p"."Sku") <= 9
```

- In .NET, `$` also matches before a final newline. It becomes `(?=\n?\Z)` on PostgreSQL, `(?=\n?\z)` on MySQL and MariaDB, and `\n?\z` on SQL Server, whose RE2 engine has no lookahead. `\d`, `\w`, `\b` and case-insensitivity are written out the same way.
- SQLite runs the .NET engine itself, so the text is unchanged.
- `REGEXP_LIKE` is a predicate on SQL Server, not a function, so it is wrapped in `IIF(...) = 1`. That form is valid wherever EF Core places a value.
- MySQL and MariaDB compare according to the column collation, which is usually case-insensitive. The `'c'` match type and the inline `(?-i)` restore .NET's case-sensitive default. A pattern with `RegexOptions.IgnoreCase` is expanded into explicit classes such as `[Ss]` and stays case-insensitive.
- MySQL and MariaDB string literals double every backslash.

:::tip

Write `[0-9]` instead of `\d` in database patterns. In .NET, `\d` matches every Unicode decimal digit, so the translation has to spell out dozens of digit ranges on PostgreSQL, or use `\p{Nd}` elsewhere. `[0-9]` stays short and matches what most schemas mean.

:::

### Patterns held in variables

A pattern in a captured variable stays a query parameter. The dialect regex is produced each time the query runs, so changing the variable never recompiles the query.

```csharp
// db is the seeded ShopContext from the first example in this section
Pattern search = Patterns.Digits;
IQueryable<string> names = db.Products.Where(p => search.IsMatch(p.Name)).Select(p => p.Name);

List<string> first = await names.ToListAsync();   // Monitor 27, Bookshelf 7

search = Pattern.FromRegex("^C");
List<string> second = await names.ToListAsync();  // Coffee mug, Cable
```

The parameter already carries the translated text. For `Pattern pattern = Patterns.Sku;` and `Where(p => pattern.IsMatch(p.Name))` on PostgreSQL with EF Core 10 (EF Core 8 names the parameter `@__pattern_0`):

```sql
-- @pattern='\A[A-Z]{3}-[0-9]{4}(?=\n?\Z)'
SELECT p."Id", p."Name", p."Sku"
FROM "Products" AS p
WHERE p."Name" ~ @pattern
```

A parameter cannot be inspected while the query is compiled, so it gets no index hints. `Group` and `Replace` need a pattern known at compile time and throw for a parameter. On SQL Server before 2025, a parameter pattern throws `InvalidOperationException`, because its `LIKE` form must be proven when the query is compiled.

### Constants and parameters on EF Core 8 and 10

The translator receives the pattern either as a compile-time constant or as a query parameter. EF Core 8 and EF Core 10 decide this differently. Verified with `ToQueryString()` on both:

| How the query references the pattern | EF Core 8 | EF Core 10 |
|--------------------------------------|-----------|------------|
| `static readonly` field | Constant | Constant |
| Inline construction, such as `Pattern.FromRegex("...")` in the lambda | Constant | Constant |
| `WhereMatches`, `MatchingAsync`, `ToMatchingListAsync` | Constant | Constant |
| `EF.Constant(pattern)` | Constant | Parameter during translation, then inlined as a literal |
| Captured local variable | Parameter | Parameter |
| `static` property | Parameter | Parameter |

On EF Core 10, `EF.Constant(pattern)` produces correct SQL with the regex as a literal, but without index hints, and `Group` throws for it. Reference the pattern through a `static readonly` field instead.

`Pattern` has value equality (same regex, options and timeout). Equal pattern instances share one entry in EF Core's query cache, and the package memoizes each translation, `LIKE` proof and fact set per pattern.

### NULL never matches

A `NULL` value never matches, exactly as in .NET: `PatternFunctions.Matches(null, pattern)` is `false`. So a negated filter returns the rows with a `NULL` column, as LINQ to Objects would:

```csharp
// db is the seeded ShopContext from the first example in this section
List<string> invalid = await db.Products
    .Where(p => !PatternFunctions.Matches(p.Sku, Patterns.Sku))
    .Select(p => p.Name)
    .ToListAsync();  // Coffee mug, Cable
```

```sql
WHERE "p"."Sku" IS NULL OR "p"."Sku" NOT REGEXP '^[A-Z]{3}-[0-9]{4}$' OR length("p"."Sku") > 9
```

The translation guards nullable columns with `column IS NOT NULL AND ...`. The result therefore never depends on how SQL's three-valued logic treats the index-hint conjuncts: a query returns the same rows with and without index hints.

## Index hints

The core library's [language facts](./language-facts.md) prove, from the pattern's minimal automaton, the text every match must start with and the longest possible match. When the pattern is a compile-time constant, these facts are added as extra conjuncts. Every row that matches the regex satisfies them, so results never change, but a B-tree index on the column can narrow the scan.

```csharp
// db is the seeded ShopContext from the first example on this page
IQueryable<Product> abc = db.Products.WhereMatches(p => p.Sku, Pattern.FromRegex("^ABC-[0-9]{4}$"));
// ABC-1234, ABC-0007
```

```sql
-- SQLite
WHERE "p"."Sku" IS NOT NULL AND "p"."Sku" REGEXP '^ABC-[0-9]{4}$'
  AND "p"."Sku" LIKE 'ABC-%' ESCAPE '\' AND length("p"."Sku") <= 9

-- PostgreSQL
WHERE p."Sku" IS NOT NULL AND p."Sku" ~ '\AABC-[0-9]{4}(?=\n?\Z)'
  AND p."Sku" LIKE 'ABC-%' ESCAPE '\' AND length(p."Sku") <= 9

-- MySQL
WHERE `p`.`Sku` IS NOT NULL AND ((REGEXP_LIKE(`p`.`Sku`, '(?-i)\\AABC-[0-9]{4}(?=\\n?\\z)', 'c')
  AND (`p`.`Sku` LIKE 'ABC-%' ESCAPE '\\')) AND (CHAR_LENGTH(`p`.`Sku`) <= 9))

-- SQL Server 2025, default collation: no prefix LIKE
WHERE [p].[Sku] IS NOT NULL AND IIF(REGEXP_LIKE([p].[Sku], N'\AABC-[0-9]{4}\n?\z'), 1, 0) = 1
  AND CAST(LEN([p].[Sku]) AS int) <= 9

-- SQL Server 2025 with UseLikeCollation(LikeCollation.Binary)
WHERE [p].[Sku] IS NOT NULL AND IIF(REGEXP_LIKE([p].[Sku], N'\AABC-[0-9]{4}\n?\z'), 1, 0) = 1
  AND [p].[Sku] LIKE N'ABC-%' ESCAPE N'\' AND CAST(LEN([p].[Sku]) AS int) <= 9
```

- The bound is 9, not 8, because `$` also accepts one final newline. End the pattern with `\z` and the bound becomes 8.
- Only the upper length bound is emitted. SQL length functions count code points, and `LEN` also strips trailing spaces, so a lower bound computed in UTF-16 units could drop real matches.
- On SQL Server the prefix `LIKE` is added only for columns declared binary with `UseLikeCollation(LikeCollation.Binary)`. Linguistic collations do not compare `LIKE` character by character, so the hint could drop rows there. The length bound is always safe and is kept.
- Hints apply only to patterns known at query compile time. A pattern in a captured variable gets none.
- `UseIndexHints(false)` turns them off. The SQL is then the regex predicate alone: `WHERE "p"."Sku" IS NOT NULL AND "p"."Sku" REGEXP '^ABC-[0-9]{4}$'`.

## Capture groups and replacements

`Group` returns the text a capture group captured in the first match. `Replace` replaces every match. Both exist on `PatternFunctions` (which also run in .NET) and on `EF.Functions`.

```csharp
// db is a ShopContext configured with UseNpgsql(...).UseFluentRegex()
IQueryable<string?> numbers = db.Products.Select(p => PatternFunctions.Group(p.Sku, Patterns.SkuParts, "number"));
IQueryable<string?> swapped = db.Products.Select(p => PatternFunctions.Replace(p.Sku, Patterns.SkuParts, "$2/$1"));
IQueryable<string?> viaEfFunctions = db.Products.Select(p => EF.Functions.Group(p.Sku, Patterns.SkuParts, "letters"));
```

The first two queries translate to:

```sql
-- PostgreSQL: names are removed and mapped to numbers
SELECT regexp_substr(p."Sku", '\A([A-Z]{3})-([0-9]{4})\Z', 1, 1, '', 2)
SELECT regexp_replace(p."Sku", '\A([A-Z]{3})-([0-9]{4})\Z', '\2/\1', 'g')

-- SQL Server 2025: names are spelled (?P<name>...)
SELECT REGEXP_SUBSTR([p].[Sku], N'\A(?P<letters>[A-Z]{3})-(?P<number>[0-9]{4})\z', 1, 1, 'c', 2)
SELECT REGEXP_REPLACE([p].[Sku], N'\A(?P<letters>[A-Z]{3})-(?P<number>[0-9]{4})\z', N'\2/\1')

-- MySQL
SELECT REGEXP_REPLACE(`p`.`Sku`, '(?-i)\\A(?<letters>[A-Z]{3})-(?<number>[0-9]{4})\\z', '$2/$1')

-- MariaDB
SELECT REGEXP_REPLACE(`p`.`Sku`, '(?-i)\\A(?<letters>[A-Z]{3})-(?<number>[0-9]{4})\\z', '\\2/\\1')
```

| Member | Translated on | Returns |
|--------|---------------|---------|
| `Group(string? value, Pattern pattern, string groupName)` | PostgreSQL 15+ (`regexp_substr`), SQL Server 2025 (`REGEXP_SUBSTR`) | The captured text, or `NULL` when the value is `NULL`, nothing matches, or the group did not take part. |
| `Group(string? value, Pattern pattern, int groupNumber)` | Same | Same. Group `0` is the whole match. |
| `Replace(string? value, Pattern pattern, string replacement)` | PostgreSQL, MySQL, MariaDB, SQL Server 2025 | The replaced text, or `NULL` for `NULL`. |

Other dialects throw `NotSupportedException` with the .NET alternative, for example on SQLite:

```text
Capture groups cannot be extracted in SQL on Sqlite. Group is translated on PostgreSQL (regexp_substr) and
SQL Server 2025 (REGEXP_SUBSTR) only; load the value and use Pattern.Match in .NET instead.
```

In .NET, the `PatternFunctions` versions follow the same `NULL` rules:

```csharp
using System;
using Masterly.FluentRegex.EntityFrameworkCore;

Console.WriteLine(PatternFunctions.Matches(null, Patterns.Sku));                      // False
Console.WriteLine(PatternFunctions.Group("ABC-1234", Patterns.SkuParts, "number"));   // 1234
Console.WriteLine(PatternFunctions.Group("nope", Patterns.SkuParts, 1) ?? "(null)");  // (null)
Console.WriteLine(PatternFunctions.Replace("ABC-1234", Patterns.SkuParts, "$2/$1"));  // 1234/ABC
```

### Group numbers

Group numbers in your code are .NET's, and they are mapped to the database's numbering, which often differs. .NET numbers unnamed groups before named ones, every engine numbers left to right, and PostgreSQL drops groups inside lookarounds. For `(?<year>[0-9]{4})-([0-9]{2})`, `Group(col, pattern, 1)` returns the month on every database, as in .NET, even though PostgreSQL calls that group 2:

```sql
SELECT regexp_substr(p."Sku", '([0-9]{4})-([0-9]{2})', 1, 1, '', 2)
```

- A group name must be a literal string.
- A group number held in a variable is accepted only when the database numbers the pattern's groups exactly as .NET does. Otherwise it throws `InvalidOperationException`.
- An unknown group name throws `ArgumentException`.

### Replacement syntax

Write the replacement in .NET syntax. It is converted to each engine's syntax:

| .NET | PostgreSQL | MySQL | MariaDB | SQL Server 2025 |
|------|------------|-------|---------|-----------------|
| `$1`, `${1}` | `\1` | `$1` | `\1` | `\1` |
| `${name}` | The group's number | The group's number | The group's number | The group's number |
| `$0` (whole match) | `\&` | `$0` | Throws `NotSupportedException` | `\0` |
| `$$` (literal `$`) | `$` | `\$` | `$` | `$` |
| `$&`, `` $` ``, `$'`, `$+`, `$_` | Throws `NotSupportedException` | Throws | Throws | Throws |

- The replacement must be a literal string. A replacement held in a variable throws `InvalidOperationException`.
- Groups above 9 are supported only on MySQL. Elsewhere `$10` throws `NotSupportedException`.
- `${name}` with a name the pattern does not have throws `ArgumentException`. `$7` for a group that does not exist stays the literal text `$7`, as in .NET.
- A backslash in the replacement is literal text and is escaped for the engine: `a\b` becomes `a\\b`.

- Digits after `$` are read the way .NET reads them. All of them form one number, so `$02` is group 2. When that number is no group, the `$` is literal: with two groups, `$12` stays the text `$12`, in .NET and in SQL. With `RegexOptions.ECMAScript`, .NET extends the number one digit at a time while it still names a group, so `$12` becomes group 1 followed by `2`, and the translation does the same.

:::tip

Write `${1}2` when you mean group 1 followed by the digit `2`. It means the same thing in .NET and in SQL, whatever the options.

:::

### When Group and Replace refuse a pattern

`Group` and `Replace` depend on which text matches, not only on whether a string matches. Engines can differ there even when they accept exactly the same strings:

- PostgreSQL prefers the longest match: `(a|ab)` matches `ab` where .NET matches `a`.
- SQL Server 2025 has no lookahead, so `$` is written `\n?\z`, which consumes the final newline that .NET leaves outside the match.
- MySQL returns an empty input unchanged, and MariaDB never replaces an empty match, so `Replace` refuses any pattern that can match the empty string on those two.

`Group` and `Replace` are translated only when the core library reports no such difference. Otherwise they throw `NotSupportedException` with the reason. For example, `Replace` with `^([A-Z]{3})-([0-9]{4})$` on SQL Server 2025:

```text
Replace cannot be translated for the pattern '^([A-Z]{3})-([0-9]{4})$' on SqlServer2025, because the database
would match a different substring or capture different text than .NET: '$' or '\Z' at position 22 is written
'\n?\z', which consumes a final newline that .NET leaves outside the match: on "a\n", replacing a$ gives "X"
in SQL Server and "X\n" in .NET. Load the values and use Pattern.Replace in .NET instead.
```

That check needs the pattern, so both functions require a pattern known at query compile time, such as a `static readonly` field. End patterns with `\z` rather than `$` when you use them with `Group` or `Replace` on SQL Server.

## Exact results on every database with MatchingAsync

Some patterns cannot run in SQL on some databases: lookarounds and backreferences on SQLite and SQL Server 2025, atomic groups everywhere except MySQL and MariaDB, or any regex on SQL Server before 2025. `MatchingAsync` and `ToMatchingListAsync` still give exact results. SQL applies a filter that is a proven superset of the pattern, then the .NET engine checks each candidate row with the exact pattern.

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;

// db is the seeded ShopContext and cancellationToken comes from your application
Pattern doubledLetter = Pattern.FromRegex(@"([a-z])\1");

// db.Products.WhereMatches(p => p.Name, doubledLetter) throws NotSupportedException on SQLite
List<Product> doubled = await db.Products.ToMatchingListAsync(p => p.Name, doubledLetter, cancellationToken);
// Coffee mug, Bookshelf 7

await foreach (Product product in db.Products.MatchingAsync(p => p.Sku, Patterns.Sku, cancellationToken))
{
    Console.WriteLine(product.Sku);  // prints: ABC-1234, then XYZ-0001, then ABC-0007
}
```

The SQL part depends on what the dialect can express:

1. When the dialect regex is exact, it is the regex itself, plus the index hints.
2. Otherwise, a superset `LIKE` from the [LIKE translation](./sql-like.md) and the upper length bound.
3. Otherwise, only `column IS NOT NULL`.

These are the queries the two calls above sent to SQLite:

```sql
-- the backreference cannot run in SQLite's regexp(), so the prefilter is a superset LIKE
SELECT "p"."Id", "p"."Name", "p"."Sku"
FROM "Products" AS "p"
WHERE "p"."Name" LIKE '%_%'

-- Patterns.Sku translates exactly, so the prefilter is the regex itself
SELECT "p"."Id", "p"."Name", "p"."Sku"
FROM "Products" AS "p"
WHERE "p"."Sku" REGEXP '^[A-Z]{3}-[0-9]{4}$' AND length("p"."Sku") <= 9
```

| Method | Returns | Notes |
|--------|---------|-------|
| `MatchingAsync<T>(selector, Pattern pattern, CancellationToken cancellationToken = default)` | `IAsyncEnumerable<T>` | Streams matching rows in query order. |
| `MatchingAsync<T>(selector, ConstrainedPattern pattern, CancellationToken cancellationToken = default)` | `IAsyncEnumerable<T>` | SQL filters on `pattern.Pattern`. The `Must` rules run in .NET. |
| `ToMatchingListAsync<T>(selector, Pattern pattern, CancellationToken cancellationToken = default)` | `Task<List<T>>` | Loads every matching row. |
| `ToMatchingListAsync<T>(selector, ConstrainedPattern pattern, CancellationToken cancellationToken = default)` | `Task<List<T>>` | Same, with `Must` rules. |

- All four extend `IQueryable<T>`, so you can call them after your own `Where`, `OrderBy` or `Include`. Null arguments throw `ArgumentNullException`.
- The pattern is embedded as a constant, so a pattern from a variable still gets the exact regex and the index hints.
- Rows whose selected value is `NULL` never match.
- Rows the prefilter lets through are loaded and checked in memory. A loose prefilter, such as `IS NOT NULL` alone, reads every row of the query, so narrow the query first when the table is large.

### Rules that SQL cannot check

A `ConstrainedPattern` adds `Must` rules that run in .NET (see [Semantic rules](./semantic-rules.md)). Its `IsMatch` cannot be translated:

```csharp
// db is the seeded ShopContext from the first example on this page
ConstrainedPattern endsWithSeven = Patterns.Sku.Must(sku => sku.EndsWith('7'), "ends with 7");

List<Product> sevens = await db.Products.ToMatchingListAsync(p => p.Sku, endsWithSeven);  // ABC-0007

// db.Products.Where(p => endsWithSeven.IsMatch(p.Name)) throws InvalidOperationException:
// "A ConstrainedPattern cannot be translated to SQL because its Must rules run only in .NET. ..."
```

## Case-insensitive patterns and culture

Like .NET, a pattern with `RegexOptions.IgnoreCase` folds case under the current culture. In Turkish and Azerbaijani, `i` pairs with `İ` instead of `I`. EF Core compiles a query once and reuses its SQL on every thread, so a constant case-insensitive pattern whose translation differs between cultures throws `NotSupportedException`:

```csharp
using System.Text.RegularExpressions;

// db is a ShopContext configured with UseNpgsql(...).UseFluentRegex()
Pattern items = Pattern.FromRegex("^item", RegexOptions.IgnoreCase);
string failing = db.Products.WhereMatches(p => p.Name, items).ToQueryString();
// throws NotSupportedException: The case-insensitive pattern '^item' matches differently in Turkish and
// Azerbaijani cultures (dotted and dotless i), ... Add RegexOptions.CultureInvariant to the pattern, or hold it
// in a variable so that it is translated on every execution.

Pattern invariantItems = Pattern.FromRegex("^item", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
string sql = db.Products.WhereMatches(p => p.Name, invariantItems).ToQueryString();
// ... WHERE p."Name" ~ '\A[Ii][Tt][Ee][Mm]'
```

- Add `RegexOptions.CultureInvariant`, or hold the pattern in a variable, which is translated on every execution under the caller's culture.
- Patterns without `i`, `I`, `İ` or `ı` are not affected: `^abc` with `IgnoreCase` becomes `\A[Aa][Bb][Cc]`.
- `MatchingAsync` never throws for this reason. Its SQL prefilter keeps only culture-free checks, and .NET applies the pattern under the caller's culture. For `^item [0-9]$` with `IgnoreCase` on SQLite the prefilter is only `WHERE length("p"."Name") <= 7`.

## SQL Server before 2025

SQL Server before 2025 has no regular expressions. On `SqlDialect.SqlServer`, a pattern is translated to `LIKE`, and only when the `LIKE` is proven to accept exactly the strings the pattern accepts. `LikeFallback` chooses what happens otherwise:

| `LikeFallback` | Exact `LIKE` exists | No exact `LIKE` |
|----------------|---------------------|-----------------|
| `ExactOnly` (default) | Uses it | Throws `InvalidOperationException` that names the pattern and suggests `MatchingAsync`, `UseDialect(SqlDialect.SqlServer2025)` or `AllowSuperset`. |
| `AllowSuperset` | Uses it | Uses a superset `LIKE`, which can return extra rows, and logs `FluentRegexEventId.LikeSupersetFallback`. |
| `Never` | Throws | Throws |

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

// db is a ShopContext configured with UseSqlServer(connectionString).UseFluentRegex()
string exact = db.Products.WhereMatches(p => p.Sku, SkuPatterns.StrictSku).ToQueryString();
// ... WHERE [p].[Sku] IS NOT NULL AND [p].[Sku] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]'

string failing = db.Products.WhereMatches(p => p.Sku, Patterns.Sku).ToQueryString();
// throws InvalidOperationException: The pattern '^[A-Z]{3}-[0-9]{4}$' cannot be translated exactly to LIKE, ...

public static class SkuPatterns
{
    public static readonly Pattern StrictSku = Pattern.FromRegex(@"^[A-Z]{3}-[0-9]{4}\z");
}
```

- `Patterns.Sku` ends with `$`, which also accepts a final newline. `LIKE` can only cover that with a trailing `%`, so there is no exact form. End such patterns with `\z`.
- With `AllowSuperset`, `Patterns.Sku` becomes `LIKE N'[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]%'` and this warning is logged: `The pattern '^[A-Z]{3}-[0-9]{4}$' was translated to the superset LIKE expression '[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]%' because SQL Server before 2025 has no regular expression support; rows that do not match the pattern can be returned.`
- A pattern held in a variable always throws, because its `LIKE` must be proven when the query is compiled.
- `Group` and `Replace` throw `NotSupportedException`.
- `MatchingAsync` works and needs no fallback. For `Patterns.Sku` it sends `[p].[Sku] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]%' AND CAST(LEN([p].[Sku]) AS int) <= 9` and checks the rest in .NET.
- When the database already runs at compatibility level 170, call `UseCompatibilityLevel(170)` on the provider or `UseDialect(SqlDialect.SqlServer2025)` to get real regex support.

### SQL Server LIKE and collations

SQL Server's linguistic collations, case-sensitive ones such as `_CS_AS` included, do not compare `LIKE` character by character. A combining accent joins the letter before it, Czech and Slovak collations treat `ch` as one letter, ignorable characters such as a soft hyphen are skipped, and case-insensitive collations fold case. The package's own test suite checks these effects against SQL Server 2025.

So every SQL Server `LIKE` that must be correct (the fallback, the `MatchingAsync` prefilter, `CHECK` constraints and `SqlRegex.Condition`) compares under `COLLATE Latin1_General_100_BIN2`. There, `LIKE` compares UTF-16 code units exactly as .NET does.

A `COLLATE` on the column prevents an index seek. When your columns already use a `_BIN2` or `_BIN` collation, declare it:

```csharp
// options and connectionString come from your application's startup code
options.UseSqlServer(connectionString)
    .UseFluentRegex(fluent => fluent.UseLikeCollation(LikeCollation.Binary));

// WhereMatches(p => p.Sku, SkuPatterns.StrictSku) now sends:
// WHERE [p].[Sku] IS NOT NULL AND [p].[Sku] LIKE N'[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]'
```

- `LikeCollation.Binary` drops the `COLLATE`, so the condition can seek an index, and it enables the prefix index hint on SQL Server 2025.
- `LIKE` patterns are always sent as Unicode literals (`N'...'`), also for `varchar` columns. SQL Server best-fit maps characters in non-Unicode literals and ignores trailing blanks in non-Unicode comparisons, and either would break the proof.

## CHECK constraints

The same pattern can enforce the column's format in the database. The constraint has `Pattern.IsMatch` semantics, like `[MatchesPattern]`, so the database accepts exactly the values your validator accepts.

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public sealed class Order
{
    public int Id { get; set; }

    [MatchesPattern(typeof(Patterns), nameof(Patterns.Sku))]   // needs AddCheckConstraintsFromAttributes()
    public string? Sku { get; set; }
}

public sealed class CatalogContext : DbContext
{
    public CatalogContext(DbContextOptions<CatalogContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .Property(p => p.Sku)
            .HasPattern(Patterns.Sku);                       // CK_Products_Sku_Pattern

        modelBuilder.Entity<Product>()
            .Property(p => p.Name)
            .HasPattern(Pattern.FromRegex(@"\S"), "CK_Products_Name_NotBlank");
    }
}
```

With `UseSqlite(...).UseFluentRegex(fluent => fluent.AddCheckConstraintsFromAttributes())`, `GenerateCreateScript()` returns:

```sql
CREATE TABLE "Orders" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Orders" PRIMARY KEY AUTOINCREMENT,
    "Sku" TEXT NULL,
    CONSTRAINT "CK_Orders_Sku_Pattern" CHECK ("Sku" REGEXP '^[A-Z]{3}-[0-9]{4}$')
);

CREATE TABLE "Products" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Sku" TEXT NULL,
    CONSTRAINT "CK_Products_Name_NotBlank" CHECK ("Name" REGEXP '\S'),
    CONSTRAINT "CK_Products_Sku_Pattern" CHECK ("Sku" REGEXP '^[A-Z]{3}-[0-9]{4}$')
);
```

The database then rejects bad values and lets `NULL` through:

```csharp
using System;
using Microsoft.EntityFrameworkCore;

// catalog is a CatalogContext on an open SQLite connection, created with EnsureCreatedAsync()
catalog.Products.Add(new Product { Name = "Desk lamp", Sku = "ABC-1234" });
catalog.Products.Add(new Product { Name = "Cable", Sku = null });
Console.WriteLine(await catalog.SaveChangesAsync());  // 2

catalog.Products.Add(new Product { Name = "Chair", Sku = "abc-1234" });
try
{
    await catalog.SaveChangesAsync();
}
catch (DbUpdateException ex)
{
    Console.WriteLine(ex.InnerException?.Message);
    // prints: SQLite Error 19: 'CHECK constraint failed: CK_Products_Sku_Pattern'.
}
```

| API | Purpose |
|-----|---------|
| `HasPattern<TProperty>(this PropertyBuilder<TProperty> builder, Pattern pattern, string? constraintName = null)` | Adds a constraint for a string property. The default name is `CK_{Table}_{Column}_Pattern`. A `null` builder or pattern throws `ArgumentNullException`. A non-string property throws `InvalidOperationException`. |
| `[MatchesPattern(typeof(T), nameof(T.Member))]` with `AddCheckConstraintsFromAttributes()` | Adds a constraint for every annotated string property. Without the option the attribute only validates in .NET. |

- Anchor the pattern to constrain the whole value: `^...$` (which, as in .NET, also accepts one final newline) or `^...\z`. An unanchored pattern only requires a match somewhere in the value, as `\S` above does.
- `NULL` passes a `CHECK` constraint, so nullable columns stay nullable. Add `IsRequired()` to forbid `NULL`.
- A `ConstrainedPattern` behind `[MatchesPattern]` contributes only its `Pattern`. The `Must` rules stay in .NET, so keep the attribute for validation.
- The constraint SQL is written once into migrations, so a culture-dependent case-insensitive pattern throws here too. Add `RegexOptions.CultureInvariant`.

The `Sku` constraint on the other dialects:

```sql
CONSTRAINT "CK_Products_Sku_Pattern" CHECK ("Sku" ~ '\A[A-Z]{3}-[0-9]{4}(?=\n?\Z)')                          -- PostgreSQL
CONSTRAINT `CK_Products_Sku_Pattern` CHECK (REGEXP_LIKE(`Sku`, '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)', 'c'))  -- MySQL
CONSTRAINT `CK_Products_Sku_Pattern` CHECK (`Sku` REGEXP '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)')             -- MariaDB
CONSTRAINT [CK_Products_Sku_Pattern] CHECK (REGEXP_LIKE([Sku], N'\A[A-Z]{3}-[0-9]{4}\n?\z'))                 -- SQL Server 2025
```

When the dialect cannot express a pattern, model building throws `InvalidOperationException` and names the property. On SQL Server before 2025, the pattern needs an exact `LIKE` form:

```text
The CHECK constraint for 'Order.Sku' cannot be created on SqlServer: The pattern '^[A-Z]{3}-[0-9]{4}$' cannot be
expressed exactly as a LIKE expression, and SQL Server before 2025 has no regular expression support. Run the
database at compatibility level 170 or later and configure UseDialect(SqlDialect.SqlServer2025), or keep this
check in .NET. Remove HasPattern/[MatchesPattern] for this property or change the dialect with UseDialect(...).
```

A lookaround on SQL Server 2025 fails the same way, because RE2 has no lookaround.

:::warning

The SQLite constraint calls the `regexp()` function that the EF Core provider registers on its own connections. Other tools that open the database file, such as the `sqlite3` command-line shell, do not have that function and cannot insert into the table.

:::

### Migrations

The constraint is created by a model-finalizing convention, so `dotnet ef migrations add` picks it up like any other `HasCheckConstraint`. This is the migration EF Core 10 scaffolds for `CatalogContext` on SQLite (the `Up` method, abridged):

```csharp
migrationBuilder.CreateTable(
    name: "Products",
    columns: table => new
    {
        Id = table.Column<int>(type: "INTEGER", nullable: false)
            .Annotation("Sqlite:Autoincrement", true),
        Name = table.Column<string>(type: "TEXT", nullable: false),
        Sku = table.Column<string>(type: "TEXT", nullable: true)
    },
    constraints: table =>
    {
        table.PrimaryKey("PK_Products", x => x.Id);
        table.CheckConstraint("CK_Products_Name_NotBlank", "\"Name\" REGEXP '\\S'");
        table.CheckConstraint("CK_Products_Sku_Pattern", "\"Sku\" REGEXP '^[A-Z]{3}-[0-9]{4}$'");
    });
```

- The model snapshot stores only the SQL text (`t.HasCheckConstraint("CK_Products_Sku_Pattern", "\"Sku\" REGEXP '^[A-Z]{3}-[0-9]{4}$'")`). The `Pattern` object itself is removed from the model by the convention, because a snapshot cannot represent it.
- When you change a pattern, the next migration drops and re-adds the constraint with the new SQL (`DropCheckConstraint` followed by `AddCheckConstraint`).
- The SQL is dialect-specific. If you keep migrations for several providers, generate a separate migrations set per provider.
- `UseFluentRegex()` must also be active at design time. Without it the `Pattern` annotation stays in the model and scaffolding fails with `Cannot scaffold C# literals of type 'Masterly.FluentRegex.Pattern'`.

## Raw SQL with SqlRegex

`SqlRegex.Condition` returns the predicate as SQL text, for Dapper, ADO.NET or hand-written migrations. It has `IsMatch` semantics and quotes the regex for the dialect.

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;

string postgres = SqlRegex.Condition("\"Sku\"", Patterns.Sku, SqlDialect.PostgreSql);
// "Sku" ~ '\A[A-Z]{3}-[0-9]{4}(?=\n?\Z)'

string mySql = SqlRegex.Condition("`Sku`", Patterns.Sku, SqlDialect.MySql);
// REGEXP_LIKE(`Sku`, '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)', 'c')

string mariaDb = SqlRegex.Condition("`Sku`", Patterns.Sku, SqlDialect.MariaDb);
// `Sku` REGEXP '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)'

string sqlServer2025 = SqlRegex.Condition("[Sku]", Patterns.Sku, SqlDialect.SqlServer2025);
// REGEXP_LIKE([Sku], N'\A[A-Z]{3}-[0-9]{4}\n?\z')

string sqlite = SqlRegex.Condition("\"Sku\"", Patterns.Sku, SqlDialect.Sqlite);
// "Sku" REGEXP '^[A-Z]{3}-[0-9]{4}$'

Pattern strictSku = Pattern.FromRegex(@"^[A-Z]{3}-[0-9]{4}\z");
string sqlServer = SqlRegex.Condition("[Sku]", strictSku, SqlDialect.SqlServer);
// [Sku] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]'

string sqlServerBinary = SqlRegex.Condition("[Sku]", strictSku, SqlDialect.SqlServer, LikeCollation.Binary);
// [Sku] LIKE N'[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]'
```

| Overload | Notes |
|----------|-------|
| `Condition(string columnSql, Pattern pattern, SqlDialect dialect)` | Assumes a non-binary collation on SQL Server. |
| `Condition(string columnSql, Pattern pattern, SqlDialect dialect, LikeCollation collation)` | Pass `LikeCollation.Binary` for `_BIN2` or `_BIN` columns to drop the `COLLATE`. Only `SqlDialect.SqlServer` uses it. |

- `columnSql` is inserted as-is, so quote it for the database yourself. Never build it from user input.
- Exceptions: `ArgumentNullException` for `null` arguments, `ArgumentException` for a blank `columnSql`, `ArgumentOutOfRangeException` for undefined enum values, `InvalidOperationException` on `SqlServer` when no exact `LIKE` exists, and `NotSupportedException` when the dialect cannot express the pattern.
- Single quotes in the regex are doubled, and MySQL and MariaDB literals double backslashes.

:::warning

EF Core's `FromSqlRaw` and `SqlQueryRaw` read `{` and `}` as parameter placeholders, so a regex like `[A-Z]{3}` fails with `FormatException`. Escape braces before you pass the condition to them: `where.Replace("{", "{{").Replace("}", "}}")`. Plain ADO.NET commands and Dapper need no escaping.

:::

## Logging and warnings

The package logs through EF Core's diagnostics logger, so its events honor `LogTo`, `ConfigureWarnings` and `DiagnosticSource` like EF Core's own events. Both events are warnings in the `Microsoft.EntityFrameworkCore.Query` category.

| `FluentRegexEventId` | Id | Logged when |
|----------------------|----|-------------|
| `LikeSupersetFallback` | 79000 | `LikeFallback.AllowSuperset` translated a pattern to a superset `LIKE` on SQL Server before 2025. |
| `TranslationWarning` | 79001 | A dialect translation reported behavior that differs from .NET. The message lists each difference. |

To turn the superset fallback into an error:

```csharp
// options and connectionString come from your application's startup code
options.UseSqlServer(connectionString)
    .UseFluentRegex(fluent => fluent.UseLikeFallback(LikeFallback.AllowSuperset))
    .ConfigureWarnings(warnings => warnings.Throw(FluentRegexEventId.LikeSupersetFallback));

// A superset translation now throws InvalidOperationException:
// "An error was generated for warning 'Microsoft.EntityFrameworkCore.Query.LikeSupersetFallback': The pattern ..."
```

:::note

In this version, every SQL regex translation is either exact or throws `NotSupportedException`, so `TranslationWarning` is not raised in practice. It stays available for `ConfigureWarnings` so that your configuration keeps working if a future translation reports differences.

:::

## Dialect support matrix

| | PostgreSQL | MySQL 8 | MariaDB | SQL Server 2025 | SQL Server | SQLite |
|---|---|---|---|---|---|---|
| `IsMatch` / `Matches` / `WhereMatches` | `~` | `REGEXP_LIKE(..., 'c')` | `REGEXP` | `REGEXP_LIKE` in `IIF` | Exact `LIKE` or throw | `REGEXP` |
| Pattern in a variable | Yes | Yes | Yes | Yes | Throws | Yes |
| `Group` | `regexp_substr` (PostgreSQL 15+) | No | No | `REGEXP_SUBSTR` | No | No |
| `Replace` | `regexp_replace(..., 'g')` | `REGEXP_REPLACE` | `REGEXP_REPLACE` | `REGEXP_REPLACE` | No | No |
| Index hints | Prefix and length | Prefix and length | Prefix and length | Length, prefix with `Binary` | Not applicable | Prefix and length |
| `MatchingAsync` | Yes | Yes | Yes | Yes | Yes (superset `LIKE`) | Yes |
| `CHECK` constraint | Yes | Yes | Yes | Yes | Exact `LIKE` or throw | Yes |
| Lookarounds and backreferences in SQL | Yes | Yes | Yes | No | No | No |
| Atomic groups `(?>...)` in SQL | No | Yes | Yes | No | No | No |

Constructs a dialect cannot express throw `NotSupportedException` at translation time, never at run time with different results:

- Conditionals such as `(?(a)a|b)` are rejected by every dialect, and so is `RegexOptions.RightToLeft`.
- Atomic groups pass through on MySQL and MariaDB only.
- SQL Server 2025 (RE2) rejects lookarounds and backreferences. SQLite rejects them too, because EF Core's `regexp()` uses the non-backtracking engine.

Use `MatchingAsync` for these patterns. See [SQL dialects](./sql-dialects.md) for the full per-engine rules.

## Common pitfalls

- **Forgetting `UseFluentRegex()`.** Queries fail, `HasPattern` creates nothing, and migrations cannot be scaffolded. Register it on every context, including the design-time one.
- **Ending patterns with `$`.** `$` also accepts a final newline. It makes the length bound one longer, blocks the exact `LIKE` on SQL Server before 2025, and makes `Group` and `Replace` refuse the pattern on SQL Server 2025. Use `\z` when you mean "end of value".
- **Using `\d` and `\w` for ASCII data.** They mean Unicode in .NET, so their translation is large. Use `[0-9]` and `[A-Za-z0-9_]`.
- **Holding query patterns in variables or static properties.** They become parameters: no index hints, no `Group`, no `Replace`, and an error on SQL Server before 2025. Use `static readonly` fields, inline construction, or `WhereMatches`.
- **Calling `ConstrainedPattern.IsMatch` in a query.** It throws. Use `MatchingAsync(selector, constrained)` or query `constrained.Pattern`.
- **Expecting `CHECK` constraints to enforce `Must` rules.** Only the pattern reaches the database.
- **Supplementary characters.** Exactness covers text in the Basic Multilingual Plane. For values with emoji or other supplementary characters, databases count code points where .NET counts UTF-16 units, so `.` or `{n}` can differ.
- **MySQL on EF Core 10.** Pomelo has no EF Core 10 build, so use Oracle's `MySql.EntityFrameworkCore` there. Oracle's provider writes string literals without escaping backslashes; the literals this package writes are escaped correctly, but your own string constants with backslashes are not. MariaDB needs Pomelo, so it is supported on EF Core 8.

## See also

- [SQL dialects](./sql-dialects.md): how a pattern becomes PostgreSQL, MySQL, MariaDB, SQL Server and SQLite regex text.
- [SQL LIKE](./sql-like.md): the proven `LIKE` translation behind the SQL Server fallback and the `MatchingAsync` prefilter.
- [Language facts](./language-facts.md): the prefix and length facts behind the index hints.
- [Semantic rules](./semantic-rules.md): `ConstrainedPattern`, `Must` and `[MatchesPattern]`.
- [ASP.NET Core](./aspnetcore.md): use the same patterns for request validation.
