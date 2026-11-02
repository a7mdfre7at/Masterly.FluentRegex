using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests.Containers;

/// <summary>
/// The scenarios every real database engine runs. Only valid SKUs are seeded because the schema carries the
/// HasPattern CHECK constraint; the Name column keeps the variety needed for substring matching.
/// </summary>
internal static class ContainerScenarios
{
    private const string Replacement = "$2/$1";

    public static async Task SeedAsync(DbContextOptions<ConstrainedCatalogContext> options, CancellationToken cancellationToken)
    {
        await using ConstrainedCatalogContext context = new ConstrainedCatalogContext(options);
        await context.Database.EnsureCreatedAsync(cancellationToken);
        context.Products.AddRange(TestData.Products().Where(product => product.Sku == null || TestPatterns.Sku.IsMatch(product.Sku)));
        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task CheckConstraintRejectsInvalidRowsAndAllowsNullAsync(DbContextOptions<ConstrainedCatalogContext> options, CancellationToken cancellationToken)
    {
        await using ConstrainedCatalogContext context = new ConstrainedCatalogContext(options);

        context.Products.Add(new Product { Id = 100, Name = "nullable", Sku = null });
        await context.SaveChangesAsync(cancellationToken);
        context.Products.Add(new Product { Id = 101, Name = "invalid", Sku = "ABC-12345" });
        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
        context.ChangeTracker.Clear();

        context.Products.Add(new Product { Id = 102, Name = "lowercase", Sku = "abc-1234" });
        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
        context.ChangeTracker.Clear();

        context.Products.RemoveRange(context.Products.Where(product => product.Name == "nullable"));
        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task MatchesWholeAndSubstringAsync(DbContextOptions<ConstrainedCatalogContext> options, CancellationToken cancellationToken)
    {
        await using ConstrainedCatalogContext context = new ConstrainedCatalogContext(options);

        List<int> skus = await context.Products.Where(product => PatternFunctions.Matches(product.Sku, TestPatterns.Sku)).Select(product => product.Id).OrderBy(id => id).ToListAsync(cancellationToken);
        List<int> namesAsSkus = await context.Products.Where(product => TestPatterns.Sku.IsMatch(product.Name)).Select(product => product.Id).ToListAsync(cancellationToken);
        List<int> namesWithDigits = await context.Products.Where(product => TestPatterns.Digits.IsMatch(product.Name)).Select(product => product.Id).OrderBy(id => id).ToListAsync(cancellationToken);
        Pattern captured = TestPatterns.Digits;
        List<int> capturedDigits = await context.Products.Where(product => captured.IsMatch(product.Name)).Select(product => product.Id).OrderBy(id => id).ToListAsync(cancellationToken);
        captured = Pattern.FromRegex("^[DE]");
        List<int> capturedLetters = await context.Products.Where(product => captured.IsMatch(product.Name)).Select(product => product.Id).OrderBy(id => id).ToListAsync(cancellationToken);
        List<int> whereMatches = await context.Products.WhereMatches(product => product.Sku, TestPatterns.AbcSku).Select(product => product.Id).OrderBy(id => id).ToListAsync(cancellationToken);

        skus.ShouldBe(new[] { 1, 3, 7 });
        namesAsSkus.ShouldBeEmpty();
        namesWithDigits.ShouldBe(new[] { 1, 3, 7 });
        capturedDigits.ShouldBe(new[] { 1, 3, 7 });
        capturedLetters.ShouldBe(new[] { 4, 7 });
        whereMatches.ShouldBe(new[] { 1, 7 });
    }

    public static async Task MatchingAsyncEqualsClientSideAsync(DbContextOptions<ConstrainedCatalogContext> options, CancellationToken cancellationToken)
    {
        await using ConstrainedCatalogContext context = new ConstrainedCatalogContext(options);
        List<Product> all = await context.Products.OrderBy(product => product.Id).ToListAsync(cancellationToken);
        Pattern[] patterns = { TestPatterns.Sku, TestPatterns.Digits, TestPatterns.NameWithDigit, TestPatterns.CaseInsensitiveSku };

        foreach (Pattern pattern in patterns)
        {
            List<Product> names = await context.Products.OrderBy(product => product.Id).ToMatchingListAsync(product => product.Name, pattern, cancellationToken);
            List<Product> skus = await context.Products.OrderBy(product => product.Id).ToMatchingListAsync(product => product.Sku, pattern, cancellationToken);

            names.Select(product => product.Id).ShouldBe(all.Where(product => pattern.IsMatch(product.Name)).Select(product => product.Id), ignoreOrder: false, customMessage: $"names for {pattern}");
            skus.Select(product => product.Id).ShouldBe(all.Where(product => product.Sku != null && pattern.IsMatch(product.Sku)).Select(product => product.Id), ignoreOrder: false, customMessage: $"skus for {pattern}");
        }
    }

    public static async Task GroupExtractsCapturesAsync(DbContextOptions<ConstrainedCatalogContext> options, CancellationToken cancellationToken)
    {
        await using ConstrainedCatalogContext context = new ConstrainedCatalogContext(options);

        List<string?> numbers = await context.Products.OrderBy(product => product.Id).Select(product => PatternFunctions.Group(product.Sku, TestPatterns.StrictSkuParts, 2)).ToListAsync(cancellationToken);
        List<string?> letters = await context.Products.OrderBy(product => product.Id).Select(product => EF.Functions.Group(product.Sku, TestPatterns.StrictSkuParts, 1)).ToListAsync(cancellationToken);

        numbers.ShouldBe(new[] { "1234", "0001", null, "0007" });
        letters.ShouldBe(new[] { "ABC", "XYZ", null, "ABC" });
    }

    /// <summary>A pattern mixing named and unnamed groups, whose .NET numbers differ from the engine's.</summary>
    public static async Task GroupWithRenumberedGroupsEqualsDotNetAsync(DbContextOptions<ConstrainedCatalogContext> options, CancellationToken cancellationToken)
    {
        await using ConstrainedCatalogContext context = new ConstrainedCatalogContext(options);
        List<string?> skus = await context.Products.OrderBy(product => product.Id).Select(product => product.Sku).ToListAsync(cancellationToken);

        List<string?> inSql = await context.Products.OrderBy(product => product.Id)
            .Select(product => PatternFunctions.Group(product.Sku, TestPatterns.NamedLettersUnnamedDigits, 1)).ToListAsync(cancellationToken);
        List<string?> replaced = await context.Products.OrderBy(product => product.Id)
            .Select(product => PatternFunctions.Replace(product.Sku, TestPatterns.NamedLettersUnnamedDigits, "$1:${letters}")).ToListAsync(cancellationToken);

        inSql.ShouldBe(skus.Select(sku => PatternFunctions.Group(sku, TestPatterns.NamedLettersUnnamedDigits, 1)));
        inSql.ShouldBe(new[] { "1234", "0001", null, "0007" });
        replaced.ShouldBe(skus.Select(sku => PatternFunctions.Replace(sku, TestPatterns.NamedLettersUnnamedDigits, "$1:${letters}")));
    }

    public static async Task ReplaceRewritesWithGroupReferencesAsync(DbContextOptions<ConstrainedCatalogContext> options, CancellationToken cancellationToken)
    {
        await using ConstrainedCatalogContext context = new ConstrainedCatalogContext(options);

        List<string?> replaced = await context.Products.OrderBy(product => product.Id).Select(product => PatternFunctions.Replace(product.Sku, TestPatterns.StrictSkuParts, Replacement)).ToListAsync(cancellationToken);
        List<string?> digitsMasked = await context.Products.OrderBy(product => product.Id).Select(product => PatternFunctions.Replace(product.Name, TestPatterns.Digits, "#")).ToListAsync(cancellationToken);

        replaced.ShouldBe(new[] { "1234/ABC", "0001/XYZ", null, "0007/ABC" });
        digitsMasked.ShouldBe(new[] { "Alpha #", "Gamma #", "Delta", "Eta #" });
    }

    public static async Task RawConditionCountsMatchesAsync(
        DbContextOptions<ConstrainedCatalogContext> options,
        SqlDialect dialect,
        Func<string, string> delimit,
        CancellationToken cancellationToken)
    {
        await using ConstrainedCatalogContext context = new ConstrainedCatalogContext(options);
        string condition = SqlRegex.Condition(delimit("Sku"), TestPatterns.AbcSku, dialect);
        string sql = $"SELECT {delimit("Id")} AS {delimit("Value")} FROM {delimit("Products")} WHERE {condition}";

        // EF Core reads braces in raw SQL as parameter placeholders, so the regex quantifiers must be escaped for SqlQueryRaw.
        string escaped = sql.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);
        List<int> ids = await context.Database.SqlQueryRaw<int>(escaped).OrderBy(id => id).ToListAsync(cancellationToken);

        ids.ShouldBe(new[] { 1, 7 });
    }
}
