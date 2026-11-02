using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>What was learned about the registered database provider.</summary>
/// <param name="Dialect">The detected dialect, or null when the provider is unknown.</param>
/// <param name="ProviderAssembly">The provider assembly, used to build provider-specific SQL expressions.</param>
/// <param name="ProviderName">The provider's options extension type name, for error messages.</param>
internal sealed record ProviderDetection(SqlDialect? Dialect, Assembly? ProviderAssembly, string? ProviderName);

/// <summary>
/// Detects the SQL dialect from the provider's options extension without referencing any provider package.
/// </summary>
internal static class DialectDetector
{
    private const string NpgsqlExtension = "NpgsqlOptionsExtension";
    private const string PomeloExtension = "MySqlOptionsExtension";
    private const string SqliteExtension = "SqliteOptionsExtension";
    private const string SqlServerExtension = "SqlServerOptionsExtension";

    private const string ServerVersionProperty = "ServerVersion";
    private const string ServerTypeProperty = "Type";
    private const string MariaDbServerType = "MariaDb";

    // EF Core 8: the explicitly configured level, or null.
    private const string CompatibilityLevelWithoutDefaultProperty = "CompatibilityLevelWithoutDefault";

    // EF Core 10: the level is split per engine; only the backing field reveals whether it was configured.
    private const string EngineTypeProperty = "EngineType";
    private const string AzureSqlEngine = "AzureSql";
    private const string AzureSqlCompatibilityLevelProperty = "AzureSqlCompatibilityLevel";
    private const string SqlServerCompatibilityLevelField = "_sqlServerCompatibilityLevel";

    private const int RegexCompatibilityLevel = 170;

    /// <summary>Detects the dialect of the database provider registered in <paramref name="options"/>.</summary>
    public static ProviderDetection Detect(IDbContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        IDbContextOptionsExtension? provider = FindProvider(options);
        if (provider == null)
        {
            return new ProviderDetection(null, null, null);
        }

        Type type = provider.GetType();
        SqlDialect? dialect = type.Name switch
        {
            NpgsqlExtension => SqlDialect.PostgreSql,
            PomeloExtension => IsMariaDb(provider) ? SqlDialect.MariaDb : SqlDialect.MySql,
            SqliteExtension => SqlDialect.Sqlite,
            SqlServerExtension => SupportsRegex(provider) ? SqlDialect.SqlServer2025 : SqlDialect.SqlServer,
            _ => null
        };

        return new ProviderDetection(dialect, type.Assembly, type.FullName);
    }

    private static IDbContextOptionsExtension? FindProvider(IDbContextOptions options)
    {
        foreach (IDbContextOptionsExtension extension in options.Extensions)
        {
            if (extension.Info.IsDatabaseProvider)
            {
                return extension;
            }
        }

        return null;
    }

    private static bool IsMariaDb(IDbContextOptionsExtension provider)
    {
        object? serverVersion = ReadProperty(provider, ServerVersionProperty);
        object? serverType = serverVersion == null ? null : ReadProperty(serverVersion, ServerTypeProperty);
        return string.Equals(serverType?.ToString(), MariaDbServerType, StringComparison.Ordinal);
    }

    /// <summary>
    /// True when the SQL Server provider was told the database runs at compatibility level 170 or later,
    /// or targets Azure SQL, whose default level in EF Core 10 already enables the REGEXP functions.
    /// </summary>
    private static bool SupportsRegex(IDbContextOptionsExtension provider)
    {
        Type type = provider.GetType();
        PropertyInfo? withoutDefault = type.GetProperty(CompatibilityLevelWithoutDefaultProperty);
        if (withoutDefault != null)
        {
            return withoutDefault.GetValue(provider) is int configured && configured >= RegexCompatibilityLevel;
        }

        string? engine = ReadProperty(provider, EngineTypeProperty)?.ToString();
        if (string.Equals(engine, AzureSqlEngine, StringComparison.Ordinal))
        {
            return ReadProperty(provider, AzureSqlCompatibilityLevelProperty) is int effective && effective >= RegexCompatibilityLevel;
        }

        FieldInfo? field = type.GetField(SqlServerCompatibilityLevelField, BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(provider) is int explicitLevel && explicitLevel >= RegexCompatibilityLevel;
    }

    private static object? ReadProperty(object instance, string name) =>
        instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);
}
