using Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

/// <summary>CHECK constraint DDL from GenerateCreateScript(), which needs no server.</summary>
public sealed class CheckConstraintDdlTests
{
    private const string NpgsqlConnection = "Host=localhost;Database=fluentregex;Username=test;Password=test";
    private const string SqlServerConnection = "Server=localhost;Database=fluentregex;User Id=test;Password=test;TrustServerCertificate=True";
    private const string SqliteConnection = "DataSource=:memory:";

    private static DbContextOptionsBuilder<TContext> Builder<TContext>()
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>();

    [Fact]
    public void Npgsql_HasPattern_EmitsTheTildeCheckConstraintWithIsMatchSemantics()
    {
        using ConstrainedCatalogContext context = new ConstrainedCatalogContext(Builder<ConstrainedCatalogContext>().UseNpgsql(NpgsqlConnection).UseFluentRegex().Options);

        string script = context.Database.GenerateCreateScript();

        script.ShouldContain($"CONSTRAINT \"{ConstrainedCatalogContext.ConstraintName}\" CHECK (\"Sku\" ~ '{TestPatterns.SkuPostgreSql}')");
    }

    [Fact]
    public void SqlServer2025_HasPattern_EmitsRegexpLikeCheckConstraint()
    {
        using ConstrainedCatalogContext context = new ConstrainedCatalogContext(
            Builder<ConstrainedCatalogContext>().UseSqlServer(SqlServerConnection).UseFluentRegex(fluent => fluent.UseDialect(SqlDialect.SqlServer2025)).Options);

        string script = context.Database.GenerateCreateScript();

        script.ShouldContain($"CHECK (REGEXP_LIKE([Sku], N'{TestPatterns.SkuSqlServer}'))");
    }

    [Fact]
    public void SqlServer_HasPattern_WithoutExactLike_ThrowsAtModelBuildWithGuidance()
    {
        using ConstrainedCatalogContext context = new ConstrainedCatalogContext(Builder<ConstrainedCatalogContext>().UseSqlServer(SqlServerConnection).UseFluentRegex().Options);

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => context.Model);

        ex.Message.ShouldContain("Product.Sku");
        ex.Message.ShouldContain("SqlServer");
        ex.Message.ShouldContain("UseDialect");
    }

    [Fact]
    public void Sqlite_HasPattern_EmitsTheRegexpCheckConstraint()
    {
        using ConstrainedCatalogContext context = new ConstrainedCatalogContext(Builder<ConstrainedCatalogContext>().UseSqlite(SqliteConnection).UseFluentRegex().Options);

        string script = context.Database.GenerateCreateScript();

        script.ShouldContain($"CHECK (\"Sku\" REGEXP '{TestPatterns.SkuRegex}')");
    }

    [Fact]
    public void HasPattern_WithAName_UsesIt()
    {
        using NamedConstraintCatalogContext context = new NamedConstraintCatalogContext(Builder<NamedConstraintCatalogContext>().UseSqlite(SqliteConnection).UseFluentRegex().Options);

        string script = context.Database.GenerateCreateScript();

        script.ShouldContain($"CONSTRAINT \"{NamedConstraintCatalogContext.ConstraintName}\" CHECK");
    }

    [Fact]
    public void MatchesPatternAttribute_WhenEnabled_EmitsTheCheckConstraint()
    {
        using AttributeCatalogContext context = new AttributeCatalogContext(
            Builder<AttributeCatalogContext>().UseSqlite(SqliteConnection).UseFluentRegex(fluent => fluent.AddCheckConstraintsFromAttributes()).Options);

        string script = context.Database.GenerateCreateScript();

        script.ShouldContain($"CONSTRAINT \"{AttributeCatalogContext.ConstraintName}\" CHECK (\"Sku\" REGEXP '{TestPatterns.SkuRegex}')");
    }

    [Fact]
    public void MatchesPatternAttribute_WhenNotEnabled_EmitsNoCheckConstraint()
    {
        using AttributeCatalogContext context = new AttributeCatalogContext(Builder<AttributeCatalogContext>().UseSqlite(SqliteConnection).UseFluentRegex().Options);

        string script = context.Database.GenerateCreateScript();

        script.ShouldNotContain("CHECK");
    }

    [Fact]
    public void HasPattern_LeavesNoPatternAnnotationInTheFinalModel()
    {
        using ConstrainedCatalogContext context = new ConstrainedCatalogContext(Builder<ConstrainedCatalogContext>().UseSqlite(SqliteConnection).UseFluentRegex().Options);

        IProperty sku = context.Model.FindEntityType(typeof(Product))!.FindProperty(nameof(Product.Sku))!;

        sku.FindAnnotation(FluentRegexAnnotationNames.Pattern).ShouldBeNull();
        sku.FindAnnotation(FluentRegexAnnotationNames.ConstraintName).ShouldBeNull();
    }

    [Fact]
    public void HasPattern_OnANonStringProperty_Throws()
    {
        ModelBuilder modelBuilder = new ModelBuilder();

        Should.Throw<InvalidOperationException>(() => modelBuilder.Entity<Product>().Property(product => product.Id).HasPattern(TestPatterns.Sku));
    }

#if NET8_0
    [Fact]
    public void MySql_HasPattern_EmitsRegexpLikeWithDoubledBackslashes()
    {
        using ConstrainedCatalogContext context = new ConstrainedCatalogContext(
            Builder<ConstrainedCatalogContext>().UseMySql("Server=localhost;Database=fluentregex;User=test;Password=test", new MySqlServerVersion(new Version(8, 4, 0))).UseFluentRegex().Options);

        string script = context.Database.GenerateCreateScript();

        script.ShouldContain($"CHECK (REGEXP_LIKE(`Sku`, '{TestPatterns.SkuMySqlLiteral}', 'c'))");
    }

    [Fact]
    public void MariaDb_HasPattern_EmitsTheRegexpOperator()
    {
        using ConstrainedCatalogContext context = new ConstrainedCatalogContext(
            Builder<ConstrainedCatalogContext>().UseMySql("Server=localhost;Database=fluentregex;User=test;Password=test", new MariaDbServerVersion(new Version(11, 4, 0))).UseFluentRegex().Options);

        string script = context.Database.GenerateCreateScript();

        script.ShouldContain($"CHECK (`Sku` REGEXP '{TestPatterns.SkuMySqlLiteral}')");
    }
#endif
}
