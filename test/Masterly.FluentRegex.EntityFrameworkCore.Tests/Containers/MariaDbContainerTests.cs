#if NET8_0
// Pomelo.EntityFrameworkCore.MySql has no EF Core 10 build yet, so MariaDB runs on net8.0 only.
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.MariaDb;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests.Containers;

public sealed class MariaDbFixture : IAsyncLifetime
{
    private const string Image = "mariadb:11.4";

    private static readonly ServerVersion Version = new MariaDbServerVersion(new Version(11, 4, 0));

    private MariaDbContainer? _container;
    private DbContextOptions<ConstrainedCatalogContext>? _options;

    public DbContextOptions<ConstrainedCatalogContext> Options =>
        _options ?? throw new InvalidOperationException("The MariaDB container was not started.");

    public async ValueTask InitializeAsync()
    {
        if (!DockerProbe.IsAvailable)
        {
            return;
        }

        _container = new MariaDbBuilder(Image).Build();
        await _container.StartAsync();
        _options = new DbContextOptionsBuilder<ConstrainedCatalogContext>()
            .UseMySql(_container.GetConnectionString(), Version)
            .UseFluentRegex()
            .Options;
        await ContainerScenarios.SeedAsync(_options, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}

/// <summary>Real semantics on MariaDB 11.4 (detected from the server version): schema, CHECK constraint, Matches, Replace and raw SQL.</summary>
public sealed class MariaDbContainerTests : IClassFixture<MariaDbFixture>
{
    private readonly MariaDbFixture _fixture;

    public MariaDbContainerTests(MariaDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [DockerFact]
    public Task CheckConstraint_RejectsInvalidRowsAndAllowsNull() =>
        ContainerScenarios.CheckConstraintRejectsInvalidRowsAndAllowsNullAsync(_fixture.Options, Cancellation);

    [DockerFact]
    public Task Matches_WholeAndSubstring() => ContainerScenarios.MatchesWholeAndSubstringAsync(_fixture.Options, Cancellation);

    [DockerFact]
    public Task MatchingAsync_EqualsClientSide() => ContainerScenarios.MatchingAsyncEqualsClientSideAsync(_fixture.Options, Cancellation);

    [DockerFact]
    public Task Replace_RewritesWithGroupReferences() => ContainerScenarios.ReplaceRewritesWithGroupReferencesAsync(_fixture.Options, Cancellation);

    [DockerFact]
    public async Task Group_ThrowsNotSupported()
    {
        await using ConstrainedCatalogContext context = new ConstrainedCatalogContext(_fixture.Options);

        await Should.ThrowAsync<NotSupportedException>(
            () => context.Products.Select(product => PatternFunctions.Group(product.Sku, TestPatterns.SkuParts, 2)).ToListAsync(Cancellation));
    }

    [DockerFact]
    public Task SqlRegexCondition_RunsAsRawSql() =>
        ContainerScenarios.RawConditionCountsMatchesAsync(_fixture.Options, SqlDialect.MariaDb, name => $"`{name}`", Cancellation);
}
#endif
