#if NET8_0
// Pomelo.EntityFrameworkCore.MySql has no EF Core 10 build yet, so MySQL and MariaDB are covered on net8.0 only.
using Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

public sealed class MySqlGenerationTests : IDisposable
{
    private const string MySqlConnection = "Server=localhost;Database=fluentregex;User=test;Password=test";

    private static readonly ServerVersion MySql84 = new MySqlServerVersion(new Version(8, 4, 0));
    private static readonly ServerVersion MariaDb114 = new MariaDbServerVersion(new Version(11, 4, 0));

    public void Dispose() => CoreDialectAdapter.FactsOverride = null;

    private static CatalogContext Context(ServerVersion version, Action<FluentRegexOptionsBuilder>? configure = null) =>
        new CatalogContext(new DbContextOptionsBuilder<CatalogContext>().UseMySql(MySqlConnection, version).UseFluentRegex(configure).Options);

    [Fact]
    public void MySql_ConstantPattern_UsesRegexpLike()
    {
        using CatalogContext context = Context(MySql84);

        string sql = context.Products.Where(product => TestPatterns.Sku.IsMatch(product.Name)).ToQueryString();

        sql.ShouldContain("REGEXP_LIKE(");
        sql.ShouldContain($"'{TestPatterns.SkuRegex}', 'c')");
    }

    [Fact]
    public void MariaDb_IsDetectedFromTheServerVersion_AndUsesTheRegexpOperator()
    {
        using CatalogContext context = Context(MariaDb114);
        Pattern local = TestPatterns.Sku;

        string sql = context.Products.Where(product => local.IsMatch(product.Name)).ToQueryString();

        sql.ShouldContain(" REGEXP ", customMessage: sql);
        sql.ShouldNotContain("REGEXP_LIKE");
    }

    [Fact]
    public void MySql_Replace_UsesRegexpReplaceWithDollarReferences()
    {
        using CatalogContext context = Context(MySql84);

        string sql = context.Products.Select(product => PatternFunctions.Replace(product.Sku, TestPatterns.SkuParts, "$2/$1 $$")).ToQueryString();

        sql.ShouldContain("REGEXP_REPLACE(");
        sql.ShouldContain(@"'$2/$1 \\$')");
    }

    [Fact]
    public void MariaDb_Replace_UsesBackslashReferences()
    {
        using CatalogContext context = Context(MariaDb114);

        string sql = context.Products.Select(product => PatternFunctions.Replace(product.Sku, TestPatterns.SkuParts, "$2/$1")).ToQueryString();

        sql.ShouldContain("REGEXP_REPLACE(");
        sql.ShouldContain(@"'\\2/\\1')");
    }

    [Fact]
    public void MySql_Group_ThrowsNotSupported()
    {
        using CatalogContext context = Context(MySql84);

        Should.Throw<NotSupportedException>(
            () => context.Products.Select(product => PatternFunctions.Group(product.Sku, TestPatterns.SkuParts, 2)).ToQueryString());
    }

    [Fact]
    public void MySql_IndexHints_UseCharLength()
    {
        using CatalogContext context = Context(MySql84);
        CoreDialectAdapter.FactsOverride = (pattern, substring) => new DialectFacts(8, 8, "ABC-", string.Empty, true);

        string sql = context.Products.WhereMatches(product => product.Sku, TestPatterns.AbcSku).ToQueryString();

        sql.ShouldContain("LIKE 'ABC-%'");
        sql.ShouldContain("CHAR_LENGTH(");
        sql.ShouldContain("<= 8");
    }
}
#endif
