#if NET10_0
// Oracle's MySql.EntityFrameworkCore is the MySQL provider with an EF Core 10 build.
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

public sealed class OracleMySqlGenerationTests
{
    private const string MySqlConnection = "Server=localhost;Database=fluentregex;Uid=test;Pwd=test";

    private static CatalogContext Context() =>
        new CatalogContext(new DbContextOptionsBuilder<CatalogContext>().UseMySQL(MySqlConnection).UseFluentRegex().Options);

    [Fact]
    public void OracleProvider_IsDetectedAsMySql_AndUsesRegexpLike()
    {
        using CatalogContext context = Context();

        string sql = context.Products.Where(product => TestPatterns.Sku.IsMatch(product.Name)).ToQueryString();

        sql.ShouldContain("REGEXP_LIKE(", customMessage: sql);
        sql.ShouldContain(", 'c')", customMessage: sql);
    }

    [Fact]
    public void OracleProvider_RegexLiteral_DoublesBackslashesForMySql()
    {
        // The provider writes literals without escaping backslashes; MySQL would read '\A' as 'A'.
        using CatalogContext context = Context();

        string sql = context.Products.Where(product => TestPatterns.Sku.IsMatch(product.Name)).ToQueryString();

        sql.ShouldContain($"'{TestPatterns.SkuMySqlLiteral}'", customMessage: sql);
    }

    [Fact]
    public void OracleProvider_LikeEscapeClause_IsAValidMySqlLiteral()
    {
        using CatalogContext context = Context();

        string sql = context.Products.WhereMatches(product => product.Sku, TestPatterns.AbcSku).ToQueryString();

        sql.ShouldContain(@"LIKE 'ABC-%' ESCAPE '\\'", customMessage: sql);
    }
}
#endif
