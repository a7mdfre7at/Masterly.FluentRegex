using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Resolves the FluentRegex settings once per internal service provider, detecting the dialect from the
/// registered database provider unless one was configured.
/// </summary>
internal sealed class FluentRegexSingletonOptions : IFluentRegexSingletonOptions
{
    private const string UnknownProvider = "unknown";

    /// <inheritdoc />
    public SqlDialect Dialect { get; private set; }

    /// <inheritdoc />
    public bool IndexHints { get; private set; }

    /// <inheritdoc />
    public LikeFallback LikeFallback { get; private set; }

    /// <inheritdoc />
    public bool CheckConstraintsFromAttributes { get; private set; }

    /// <inheritdoc />
    public Assembly? ProviderAssembly { get; private set; }

    /// <inheritdoc />
    public void Initialize(IDbContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        FluentRegexOptionsExtension extension = Extension(options);
        ProviderDetection detection = DialectDetector.Detect(options);
        Dialect = ResolveDialect(extension, detection);
        IndexHints = extension.IndexHints;
        LikeFallback = extension.LikeFallback;
        CheckConstraintsFromAttributes = extension.CheckConstraintsFromAttributes;
        ProviderAssembly = detection.ProviderAssembly;
    }

    /// <inheritdoc />
    public void Validate(IDbContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        FluentRegexOptionsExtension extension = Extension(options);
        ProviderDetection detection = DialectDetector.Detect(options);
        if (ResolveDialect(extension, detection) != Dialect
            || extension.IndexHints != IndexHints
            || extension.LikeFallback != LikeFallback
            || extension.CheckConstraintsFromAttributes != CheckConstraintsFromAttributes)
        {
            throw new InvalidOperationException(
                "The UseFluentRegex options changed after the internal service provider was created. " +
                "Configure FluentRegex the same way for every DbContext that shares a service provider.");
        }
    }

    private static FluentRegexOptionsExtension Extension(IDbContextOptions options) =>
        options.FindExtension<FluentRegexOptionsExtension>() ?? new FluentRegexOptionsExtension();

    private static SqlDialect ResolveDialect(FluentRegexOptionsExtension extension, ProviderDetection detection)
    {
        if (extension.Dialect is SqlDialect configured)
        {
            return configured;
        }

        if (detection.Dialect is SqlDialect detected)
        {
            return detected;
        }

        throw new InvalidOperationException(
            $"FluentRegex could not detect the SQL dialect of the database provider '{detection.ProviderName ?? UnknownProvider}'. " +
            "Supported providers are Npgsql, Pomelo MySQL/MariaDB, Microsoft SQL Server and SQLite; for another provider " +
            "configure the dialect explicitly with UseFluentRegex(fluent => fluent.UseDialect(...)).");
    }
}
