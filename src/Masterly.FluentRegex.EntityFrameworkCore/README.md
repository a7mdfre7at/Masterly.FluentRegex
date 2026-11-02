# Masterly.FluentRegex.EntityFrameworkCore

Entity Framework Core integration for [Masterly.FluentRegex](https://github.com/a7mdfre7at/Masterly.FluentRegex):
run fluent patterns inside LINQ queries, translated to the database's own regex dialect, with proven
index-friendly prefilters and CHECK constraints generated from the same pattern.

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;

public static class Patterns
{
    public static readonly Pattern Sku = Pattern.With
        .StartOfLine.UppercaseLetter.Repeat.Times(3).Literal("-").AsciiDigit.Repeat.Times(4).EndOfLine;
}

services.AddDbContext<ShopContext>(options => options
    .UseNpgsql(connectionString)
    .UseFluentRegex());                                   // dialect detected from the provider

// WHERE p."Sku" ~ '^[A-Z]{3}-[0-9]{4}$'
List<Product> skus = await db.Products.Where(p => Patterns.Sku.IsMatch(p.Sku!)).ToListAsync();

// Works on every database: SQL prefilter (a proven superset) + exact .NET match
List<Product> exact = await db.Products.ToMatchingListAsync(p => p.Sku, Patterns.Sku);

// CHECK ("Sku" ~ '\A(?:...)\Z') on PostgreSQL, REGEXP_LIKE([Sku], N'...') on SQL Server 2025, ...
modelBuilder.Entity<Product>().Property(p => p.Sku).HasPattern(Patterns.Sku);
```

| Dialect | Detected from | `IsMatch` / `Matches` | `Group` | `Replace` | CHECK constraint |
|---|---|---|---|---|---|
| PostgreSQL | Npgsql | `col ~ 're'` | `regexp_substr(col, re, 1, 1, '', n)` | `regexp_replace(col, re, repl, 'g')` | yes |
| MySQL 8 | Pomelo | `REGEXP_LIKE(col, 're', 'c')` | no | `REGEXP_REPLACE(col, re, repl)` | yes |
| MariaDB | Pomelo (server version) | `col REGEXP 're'` | no | `REGEXP_REPLACE(col, re, repl)` | yes |
| SQL Server 2025 / Azure SQL | compatibility level 170+ | `IIF(REGEXP_LIKE(col, N're'), 1, 0) = 1` | `REGEXP_SUBSTR(col, re, 1, 1, 'c', n)` | `REGEXP_REPLACE(col, re, repl)` | yes |
| SQL Server (older) | SQL Server provider | exact `LIKE` only, otherwise throws with guidance | no | no | exact `LIKE` or throws |
| SQLite | Microsoft.Data.Sqlite | `col REGEXP 're'` (.NET engine) | no | no | yes |

- `MatchingAsync` / `ToMatchingListAsync` run on every database: the SQL part is a proven superset of the
  pattern (the exact dialect regex, otherwise a superset `LIKE` plus a length bound, otherwise only a NULL check)
  and the .NET engine applies the exact pattern. `Must` rules of a `ConstrainedPattern` run in .NET.
- Index hints (on by default): proven facts such as a required prefix add `AND col LIKE 'prefix%'` and an upper
  length bound next to the regex, so a B-tree index can narrow the scan without changing results.
- NULL never matches: SQL regex predicates on NULL yield NULL and the row is excluded (also under `NOT`).
- `SqlRegex.Condition("\"Email\"", pattern, SqlDialect.PostgreSql)` returns the predicate as raw SQL for Dapper.
- Targets .NET 8 (EF Core 8) and .NET 10 (EF Core 10). Full documentation: `docs/features/entity-framework.md`.
