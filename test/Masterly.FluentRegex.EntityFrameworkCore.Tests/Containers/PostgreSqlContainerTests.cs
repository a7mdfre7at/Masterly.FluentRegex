using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests.Containers;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private const string Image = "postgres:17";

    private PostgreSqlContainer? _container;
    private DbContextOptions<ConstrainedCatalogContext>? _options;

    public DbContextOptions<ConstrainedCatalogContext> Options =>
        _options ?? throw new InvalidOperationException("The PostgreSQL container was not started.");

    public async ValueTask InitializeAsync()
    {
        if (!DockerProbe.IsAvailable)
        {
            return;
        }

        _container = new PostgreSqlBuilder(Image).Build();
        await _container.StartAsync();
        _options = new DbContextOptionsBuilder<ConstrainedCatalogContext>()
            .UseNpgsql(_container.GetConnectionString())
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

/// <summary>Real semantics on PostgreSQL 17: schema, CHECK constraint, Matches, Group, Replace and raw SQL.</summary>
public sealed class PostgreSqlContainerTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlContainerTests(PostgreSqlFixture fixture)
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
    public Task Group_ExtractsCaptures() => ContainerScenarios.GroupExtractsCapturesAsync(_fixture.Options, Cancellation);

    [DockerFact]
    public Task Replace_RewritesWithGroupReferences() => ContainerScenarios.ReplaceRewritesWithGroupReferencesAsync(_fixture.Options, Cancellation);

    [DockerFact]
    public Task SqlRegexCondition_RunsAsRawSql() =>
        ContainerScenarios.RawConditionCountsMatchesAsync(_fixture.Options, SqlDialect.PostgreSql, name => $"\"{name}\"", Cancellation);
}
