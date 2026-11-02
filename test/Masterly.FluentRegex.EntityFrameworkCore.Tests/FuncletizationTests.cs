using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

/// <summary>
/// EF Core 8 and EF Core 10 funcletize differently (EF 8 parameterizes most evaluatable expressions, EF 10 keeps
/// only captured variables as parameters). Every way of referencing a pattern must work on both, through either
/// the constant or the parameter translation path.
/// </summary>
public sealed class FuncletizationTests : IDisposable
{
    private static readonly int[] ExpectedIds = { 1, 3, 7 };

    private readonly SqliteConnection _connection = new SqliteConnection("DataSource=:memory:");
    private readonly ITestOutputHelper _output;

    public FuncletizationTests(ITestOutputHelper output)
    {
        _output = output;
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private async Task<CatalogContext> SeedAsync()
    {
        DbContextOptions<CatalogContext> options = new DbContextOptionsBuilder<CatalogContext>().UseSqlite(_connection).UseFluentRegex().Options;
        CatalogContext context = new CatalogContext(options);
        await context.Database.EnsureCreatedAsync(Cancellation);
        context.Products.AddRange(TestData.Products());
        await context.SaveChangesAsync(Cancellation);
        context.ChangeTracker.Clear();
        return context;
    }

    private async Task AssertTranslatedAsync(string form, IQueryable<Product> query)
    {
        string sql = query.ToQueryString();

        // ToQueryString prefixes parameter declarations (".param set", "-- @p=", "DECLARE") with the value, so only the statement decides.
        string statement = sql.Substring(sql.IndexOf("SELECT", StringComparison.Ordinal));
        string path = statement.Contains(TestPatterns.SkuRegex, StringComparison.Ordinal) ? "literal in SQL" : "parameter";
        _output.WriteLine($"{form}: {path} on {RuntimeName} -> {sql.Replace(Environment.NewLine, " ")}");

        List<int> ids = await query.Select(product => product.Id).ToListAsync(Cancellation);

        ids.ShouldBe(ExpectedIds, ignoreOrder: false, customMessage: form);
    }

    private static string RuntimeName => Environment.Version.Major >= 10 ? "EF Core 10" : "EF Core 8";

    [Fact]
    public async Task StaticReadonlyField_IsTranslated()
    {
        await using CatalogContext context = await SeedAsync();

        await AssertTranslatedAsync("static readonly field", context.Products.Where(product => PatternFunctions.Matches(product.Sku, TestPatterns.Sku)));
    }

    [Fact]
    public async Task StaticProperty_IsTranslated()
    {
        await using CatalogContext context = await SeedAsync();

        await AssertTranslatedAsync("static property", context.Products.Where(product => PatternFunctions.Matches(product.Sku, TestPatterns.SkuProperty)));
    }

    [Fact]
    public async Task LocalCapturedVariable_IsTranslated()
    {
        await using CatalogContext context = await SeedAsync();
        Pattern local = TestPatterns.Sku;

        await AssertTranslatedAsync("captured variable", context.Products.Where(product => PatternFunctions.Matches(product.Sku, local)));
    }

    [Fact]
    public async Task EfConstant_IsTranslated()
    {
        await using CatalogContext context = await SeedAsync();

        await AssertTranslatedAsync("EF.Constant", context.Products.Where(product => PatternFunctions.Matches(product.Sku, EF.Constant(TestPatterns.Sku))));
    }

    [Fact]
    public async Task InlineFromRegexCall_IsTranslated()
    {
        await using CatalogContext context = await SeedAsync();

        await AssertTranslatedAsync("inline Pattern.FromRegex", context.Products.Where(product => PatternFunctions.Matches(product.Sku, Pattern.FromRegex(TestPatterns.SkuRegex))));
    }

    [Fact]
    public async Task InstanceCallOnCapturedVariable_IsTranslated()
    {
        await using CatalogContext context = await SeedAsync();
        Pattern local = TestPatterns.Sku;

        await AssertTranslatedAsync("instance IsMatch on captured variable", context.Products.Where(product => local.IsMatch(product.Sku!)));
    }
}
