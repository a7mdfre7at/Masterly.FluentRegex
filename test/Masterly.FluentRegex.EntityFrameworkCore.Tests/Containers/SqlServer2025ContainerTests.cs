using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests.Containers;

public sealed class SqlServer2025Fixture : IAsyncLifetime
{
    private const string Image = "mcr.microsoft.com/mssql/server:2025-latest";
    private const string DatabaseName = "fluentregex";
    private const int RegexCompatibilityLevel = 170;

    private MsSqlContainer? _container;
    private DbContextOptions<ConstrainedCatalogContext>? _options;

    public DbContextOptions<ConstrainedCatalogContext> Options =>
        _options ?? throw new InvalidOperationException("The SQL Server container was not started.");

    public async ValueTask InitializeAsync()
    {
        if (!DockerProbe.IsAvailable)
        {
            return;
        }

        _container = new MsSqlBuilder(Image).Build();
        await _container.StartAsync();

        // The container's connection string targets master; EnsureCreated creates a database of its own at level 170.
        SqlConnectionStringBuilder connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = DatabaseName };
        _options = new DbContextOptionsBuilder<ConstrainedCatalogContext>()
            .UseSqlServer(connectionString.ConnectionString, sqlServer => sqlServer.UseCompatibilityLevel(RegexCompatibilityLevel))
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

/// <summary>
/// Real semantics on SQL Server 2025, detected as <see cref="SqlDialect.SqlServer2025"/> from the configured
/// compatibility level: schema, CHECK constraint, Matches, Group, Replace and raw SQL.
/// </summary>
public sealed class SqlServer2025ContainerTests : IClassFixture<SqlServer2025Fixture>
{
    private readonly SqlServer2025Fixture _fixture;

    public SqlServer2025ContainerTests(SqlServer2025Fixture fixture)
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
    public Task Group_WithRenumberedGroups_EqualsDotNet() => ContainerScenarios.GroupWithRenumberedGroupsEqualsDotNetAsync(_fixture.Options, Cancellation);

    [DockerFact]
    public Task Replace_RewritesWithGroupReferences() => ContainerScenarios.ReplaceRewritesWithGroupReferencesAsync(_fixture.Options, Cancellation);

    [DockerFact]
    public Task SqlRegexCondition_RunsAsRawSql() =>
        ContainerScenarios.RawConditionCountsMatchesAsync(_fixture.Options, SqlDialect.SqlServer2025, name => $"[{name}]", Cancellation);
}
