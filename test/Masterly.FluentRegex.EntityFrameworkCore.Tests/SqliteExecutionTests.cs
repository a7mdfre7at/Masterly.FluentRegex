using Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

/// <summary>Real execution on an in-memory SQLite database, where the regex engine is .NET itself.</summary>
public sealed class SqliteExecutionTests : IDisposable
{
    private readonly SqliteConnection _connection = new SqliteConnection("DataSource=:memory:");

    public SqliteExecutionTests()
    {
        _connection.Open();
    }

    public void Dispose()
    {
        CoreDialectAdapter.FactsOverride = null;
        _connection.Dispose();
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private DbContextOptions<TContext> Options<TContext>(Action<FluentRegexOptionsBuilder>? configure = null)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>().UseSqlite(_connection).UseFluentRegex(configure).Options;

    private async Task<CatalogContext> SeedAsync(Action<FluentRegexOptionsBuilder>? configure = null)
    {
        CatalogContext context = new CatalogContext(Options<CatalogContext>(configure));
        await context.Database.EnsureCreatedAsync(Cancellation);
        context.Products.AddRange(TestData.Products());
        await context.SaveChangesAsync(Cancellation);
        context.ChangeTracker.Clear();
        return context;
    }

    [Fact]
    public async Task IsMatch_StaticFieldPattern_ReturnsExactlyTheRowsDotNetMatches()
    {
        await using CatalogContext context = await SeedAsync();

        List<int> ids = await context.Products.Where(product => TestPatterns.Sku.IsMatch(product.Name)).Select(product => product.Id).ToListAsync(Cancellation);
        List<int> digits = await context.Products.Where(product => TestPatterns.Digits.IsMatch(product.Name)).Select(product => product.Id).ToListAsync(Cancellation);

        ids.ShouldBe(TestData.IdsMatching(product => product.Name, TestPatterns.Sku.IsMatch));
        digits.ShouldBe(TestData.IdsMatching(product => product.Name, TestPatterns.Digits.IsMatch));
    }

    [Fact]
    public async Task IsMatch_CapturedVariable_ChangingTheVariableChangesTheResults()
    {
        await using CatalogContext context = await SeedAsync();
        Pattern pattern = TestPatterns.Digits;

        List<int> withDigits = await context.Products.Where(product => pattern.IsMatch(product.Name)).Select(product => product.Id).ToListAsync(Cancellation);
        pattern = Pattern.FromRegex("^[A-Z]");
        List<int> uppercase = await context.Products.Where(product => pattern.IsMatch(product.Name)).Select(product => product.Id).ToListAsync(Cancellation);

        withDigits.ShouldBe(new[] { 1, 3, 5, 7 });
        uppercase.ShouldBe(new[] { 1, 3, 4, 5, 6, 7 });
    }

    [Fact]
    public async Task IsMatch_EfConstant_IsTranslated()
    {
        await using CatalogContext context = await SeedAsync();

        List<int> ids = await context.Products.Where(product => EF.Constant(TestPatterns.Digits).IsMatch(product.Name)).Select(product => product.Id).ToListAsync(Cancellation);

        ids.ShouldBe(new[] { 1, 3, 5, 7 });
    }

    [Fact]
    public async Task Matches_NullColumn_NeverMatchesEvenWhenNegated()
    {
        await using CatalogContext context = await SeedAsync();

        List<int> matching = await context.Products.Where(product => PatternFunctions.Matches(product.Sku, TestPatterns.Sku)).Select(product => product.Id).ToListAsync(Cancellation);
        List<int> notMatching = await context.Products.Where(product => !PatternFunctions.Matches(product.Sku, TestPatterns.Sku)).Select(product => product.Id).ToListAsync(Cancellation);

        matching.ShouldBe(new[] { 1, 3, 7 });
        notMatching.ShouldBe(new[] { 2, 5, 6 }, ignoreOrder: false, customMessage: "SQL three-valued logic excludes NULL from NOT as well");
    }

    [Fact]
    public async Task EfFunctionsMatches_IsTranslated()
    {
        await using CatalogContext context = await SeedAsync();

        List<int> ids = await context.Products.Where(product => EF.Functions.Matches(product.Email, TestPatterns.Email)).Select(product => product.Id).ToListAsync(Cancellation);

        ids.ShouldBe(TestData.IdsMatching(product => product.Email, TestPatterns.Email.IsMatch));
    }

    [Fact]
    public async Task WhereMatches_FiltersInSql()
    {
        await using CatalogContext context = await SeedAsync();

        List<int> ids = await context.Products.WhereMatches(product => product.Sku, TestPatterns.Sku).Select(product => product.Id).ToListAsync(Cancellation);

        ids.ShouldBe(new[] { 1, 3, 7 });
    }

    [Fact]
    public async Task MatchingAsync_EqualsClientSideFiltering_ForEveryPattern()
    {
        await using CatalogContext context = await SeedAsync();
        Pattern[] patterns = { TestPatterns.Sku, TestPatterns.Digits, TestPatterns.NameWithDigit, TestPatterns.CaseInsensitiveSku, TestPatterns.Email };

        foreach (Pattern pattern in patterns)
        {
            List<int> skus = new List<int>();
            await foreach (Product product in context.Products.MatchingAsync(product => product.Sku, pattern, Cancellation))
            {
                skus.Add(product.Id);
            }

            List<Product> names = await context.Products.ToMatchingListAsync(product => product.Name, pattern, Cancellation);

            skus.ShouldBe(TestData.IdsMatching(product => product.Sku, pattern.IsMatch), ignoreOrder: false, customMessage: $"pattern {pattern}");
            names.Select(product => product.Id).ShouldBe(TestData.IdsMatching(product => product.Name, pattern.IsMatch), ignoreOrder: false, customMessage: $"pattern {pattern}");
        }
    }

    [Fact]
    public async Task MatchingAsync_LikeFallbackDialect_UsesTheSupersetPrefilterAndStillMatchesExactly()
    {
        // SqlDialect.SqlServer has no regex, so the prefilter is the superset LIKE path; SQLite executes it.
        await using CatalogContext context = await SeedAsync(fluent => fluent.UseDialect(SqlDialect.SqlServer));

        List<Product> products = await context.Products.ToMatchingListAsync(product => product.Sku, TestPatterns.Sku, Cancellation);

        products.Select(product => product.Id).ShouldBe(new[] { 1, 3, 7 });
    }

    [Fact]
    public async Task MatchingAsync_ConstrainedPattern_AppliesRulesInDotNet()
    {
        await using CatalogContext context = await SeedAsync();
        ConstrainedPattern endsWithSeven = TestPatterns.Sku.Must(sku => sku.EndsWith('7'), "ends with 7");

        List<Product> products = await context.Products.ToMatchingListAsync(product => product.Sku, endsWithSeven, Cancellation);

        products.Select(product => product.Id).ShouldBe(new[] { 7 });
    }

    [Fact]
    public async Task IsMatch_ConstrainedPattern_ThrowsWithGuidance()
    {
        await using CatalogContext context = await SeedAsync();
        ConstrainedPattern constrained = TestPatterns.Sku.Must(sku => sku.Length == 8);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Products.Where(product => constrained.IsMatch(product.Name)).ToListAsync(Cancellation));

        ex.Message.ShouldContain("MatchingAsync");
    }

    [Fact]
    public async Task WhereMatches_LikeFallbackExactOnly_ThrowsTheDocumentedMessage()
    {
        await using CatalogContext context = await SeedAsync(fluent => fluent.UseDialect(SqlDialect.SqlServer));

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Products.WhereMatches(product => product.Sku, TestPatterns.Sku).ToListAsync(Cancellation));

        ex.Message.ShouldContain(TestPatterns.SkuRegex);
        ex.Message.ShouldContain("MatchingAsync");
        ex.Message.ShouldContain("SqlDialect.SqlServer2025");
        ex.Message.ShouldContain("LikeFallback.AllowSuperset");
    }

    [Fact]
    public async Task WhereMatches_LikeFallbackAllowSuperset_ReturnsTheSupersetAndLogsAWarning()
    {
        List<string> log = new List<string>();
        DbContextOptions<CatalogContext> options = new DbContextOptionsBuilder<CatalogContext>()
            .UseSqlite(_connection)
            .UseFluentRegex(fluent => fluent.UseDialect(SqlDialect.SqlServer).UseLikeFallback(LikeFallback.AllowSuperset))
            .LogTo(log.Add)
            .Options;
        await using CatalogContext context = new CatalogContext(options);
        await context.Database.EnsureCreatedAsync(Cancellation);
        context.Products.AddRange(TestData.Products());
        await context.SaveChangesAsync(Cancellation);

        List<int> ids = await context.Products.WhereMatches(product => product.Sku, TestPatterns.Sku).Select(product => product.Id).ToListAsync(Cancellation);

        ids.ShouldBe(new[] { 1, 2, 3, 5, 6, 7 }, ignoreOrder: false, customMessage: "the shim's LIKE '%' accepts every non-null value");
        log.ShouldContain(line => line.Contains("superset LIKE", StringComparison.Ordinal) && line.Contains(TestPatterns.SkuRegex, StringComparison.Ordinal));
    }

    [Fact]
    public async Task HasPattern_CheckConstraint_RejectsInvalidRowsAndAllowsNull()
    {
        await using ConstrainedCatalogContext context = new ConstrainedCatalogContext(Options<ConstrainedCatalogContext>());
        await context.Database.EnsureCreatedAsync(Cancellation);

        context.Products.Add(new Product { Id = 1, Name = "valid", Sku = "ABC-1234" });
        context.Products.Add(new Product { Id = 2, Name = "null", Sku = null });
        await context.SaveChangesAsync(Cancellation);
        context.Products.Add(new Product { Id = 3, Name = "invalid", Sku = "ABC-12345" });

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Cancellation));
    }

    [Fact]
    public async Task MatchesPatternAttribute_CheckConstraint_RejectsInvalidRows()
    {
        await using AttributeCatalogContext context = new AttributeCatalogContext(Options<AttributeCatalogContext>(fluent => fluent.AddCheckConstraintsFromAttributes()));
        await context.Database.EnsureCreatedAsync(Cancellation);

        context.Orders.Add(new Order { Sku = "XYZ-0001" });
        await context.SaveChangesAsync(Cancellation);
        context.Orders.Add(new Order { Sku = "xyz-0001" });

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Cancellation));
    }

    [Fact]
    public async Task IsMatch_EqualButDistinctPatternInstances_BothExecuteCorrectly()
    {
        await using CatalogContext context = await SeedAsync();
        Pattern first = Pattern.FromRegex(TestPatterns.SkuRegex);
        Pattern second = Pattern.FromRegex(TestPatterns.SkuRegex);

        List<int> firstIds = await context.Products.Where(product => EF.Constant(first).IsMatch(product.Name)).Select(product => product.Id).ToListAsync(Cancellation);
        List<int> secondIds = await context.Products.Where(product => EF.Constant(second).IsMatch(product.Name)).Select(product => product.Id).ToListAsync(Cancellation);

        firstIds.ShouldBe(secondIds);
    }

    [Fact]
    public async Task IndexHints_WithProvenFacts_AddConjunctsAndKeepResultsCorrect()
    {
        await using CatalogContext context = await SeedAsync();
        CoreDialectAdapter.FactsOverride = (pattern, substring) => pattern == TestPatterns.AbcSku
            ? new DialectFacts(8, 8, "ABC-", string.Empty, true)
            : new DialectFacts(0, null, string.Empty, string.Empty, false);
        IQueryable<Product> query = context.Products.WhereMatches(product => product.Sku, TestPatterns.AbcSku);

        string sql = query.ToQueryString();
        List<int> ids = await query.Select(product => product.Id).ToListAsync(Cancellation);

        sql.ShouldContain("LIKE 'ABC-%' ESCAPE '\\'");
        sql.ShouldContain("length(");
        ids.ShouldBe(new[] { 1, 7 });
    }

    [Fact]
    public async Task IndexHints_Disabled_EmitOnlyTheRegex()
    {
        await using CatalogContext context = await SeedAsync(fluent => fluent.UseIndexHints(false));
        CoreDialectAdapter.FactsOverride = (pattern, substring) => new DialectFacts(8, 8, "ABC-", string.Empty, true);

        string sql = context.Products.WhereMatches(product => product.Sku, TestPatterns.AbcSku).ToQueryString();

        sql.ShouldNotContain("LIKE");
        sql.ShouldNotContain("length(");
    }

    [Fact]
    public async Task Group_OnSqlite_ThrowsNotSupported()
    {
        await using CatalogContext context = await SeedAsync();

        await Should.ThrowAsync<NotSupportedException>(
            () => context.Products.Select(product => PatternFunctions.Group(product.Sku, TestPatterns.SkuParts, 2)).ToListAsync(Cancellation));
    }

    [Fact]
    public async Task Replace_OnSqlite_ThrowsNotSupported()
    {
        await using CatalogContext context = await SeedAsync();

        await Should.ThrowAsync<NotSupportedException>(
            () => context.Products.Select(product => PatternFunctions.Replace(product.Sku, TestPatterns.SkuParts, "$2")).ToListAsync(Cancellation));
    }

    [Fact]
    public void PatternFunctions_InDotNet_MirrorSqlNullSemantics()
    {
        PatternFunctions.Matches(null, TestPatterns.Sku).ShouldBeFalse();
        PatternFunctions.Matches("ABC-1234", TestPatterns.Sku).ShouldBeTrue();
        PatternFunctions.Group(null, TestPatterns.NamedSkuParts, "number").ShouldBeNull();
        PatternFunctions.Group("ABC-1234", TestPatterns.NamedSkuParts, "number").ShouldBe("1234");
        PatternFunctions.Group("nope", TestPatterns.NamedSkuParts, 1).ShouldBeNull();
        PatternFunctions.Replace(null, TestPatterns.SkuParts, "$2").ShouldBeNull();
        PatternFunctions.Replace("ABC-1234", TestPatterns.SkuParts, "$2/$1").ShouldBe("1234/ABC");
    }
}
