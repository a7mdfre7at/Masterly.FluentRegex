using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>
/// The options extension registered by <c>UseFluentRegex</c>. Immutable: every <c>With...</c> method returns a copy.
/// </summary>
internal sealed class FluentRegexOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    /// <summary>Creates the extension with default settings.</summary>
    public FluentRegexOptionsExtension()
    {
        IndexHints = true;
        LikeFallback = LikeFallback.ExactOnly;
    }

    private FluentRegexOptionsExtension(FluentRegexOptionsExtension copyFrom)
    {
        Dialect = copyFrom.Dialect;
        IndexHints = copyFrom.IndexHints;
        LikeFallback = copyFrom.LikeFallback;
        CheckConstraintsFromAttributes = copyFrom.CheckConstraintsFromAttributes;
    }

    /// <summary>Gets the configured dialect, or null to detect it from the provider.</summary>
    public SqlDialect? Dialect { get; private set; }

    /// <summary>Gets whether proven LIKE/length conjuncts are added next to the regex predicate.</summary>
    public bool IndexHints { get; private set; }

    /// <summary>Gets how patterns are translated on SQL Server without regex support.</summary>
    public LikeFallback LikeFallback { get; private set; }

    /// <summary>Gets whether <c>[MatchesPattern]</c> attributes produce CHECK constraints.</summary>
    public bool CheckConstraintsFromAttributes { get; private set; }

    /// <inheritdoc />
    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    /// <summary>Returns a copy with the dialect set.</summary>
    public FluentRegexOptionsExtension WithDialect(SqlDialect dialect)
    {
        FluentRegexOptionsExtension clone = new FluentRegexOptionsExtension(this);
        clone.Dialect = dialect;
        return clone;
    }

    /// <summary>Returns a copy with index hints enabled or disabled.</summary>
    public FluentRegexOptionsExtension WithIndexHints(bool enabled)
    {
        FluentRegexOptionsExtension clone = new FluentRegexOptionsExtension(this);
        clone.IndexHints = enabled;
        return clone;
    }

    /// <summary>Returns a copy with the LIKE fallback set.</summary>
    public FluentRegexOptionsExtension WithLikeFallback(LikeFallback fallback)
    {
        FluentRegexOptionsExtension clone = new FluentRegexOptionsExtension(this);
        clone.LikeFallback = fallback;
        return clone;
    }

    /// <summary>Returns a copy with attribute-driven check constraints enabled or disabled.</summary>
    public FluentRegexOptionsExtension WithCheckConstraintsFromAttributes(bool enabled)
    {
        FluentRegexOptionsExtension clone = new FluentRegexOptionsExtension(this);
        clone.CheckConstraintsFromAttributes = enabled;
        return clone;
    }

    /// <inheritdoc />
    public void ApplyServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMethodCallTranslatorPlugin, FluentRegexMethodCallTranslatorPlugin>()
            .TryAdd<IConventionSetPlugin, FluentRegexConventionSetPlugin>()
            .TryAdd<ISingletonOptions, IFluentRegexSingletonOptions>(provider => provider.GetRequiredService<IFluentRegexSingletonOptions>())
            .TryAddProviderSpecificServices(map => map.TryAddSingleton<IFluentRegexSingletonOptions, FluentRegexSingletonOptions>());
    }

    /// <inheritdoc />
    public void Validate(IDbContextOptions options)
    {
        // The dialect is resolved and validated by FluentRegexSingletonOptions once the provider is known.
    }

    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        private const string AutoDialect = "auto";

        public ExtensionInfo(FluentRegexOptionsExtension extension)
            : base(extension)
        {
        }

        private new FluentRegexOptionsExtension Extension => (FluentRegexOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider => false;

        public override string LogFragment =>
            $"using FluentRegex (Dialect={DialectText}, IndexHints={Extension.IndexHints}, " +
            $"LikeFallback={Extension.LikeFallback}, CheckConstraintsFromAttributes={Extension.CheckConstraintsFromAttributes}) ";

        private string DialectText => Extension.Dialect?.ToString() ?? AutoDialect;

        public override int GetServiceProviderHashCode() =>
            HashCode.Combine(Extension.Dialect, Extension.IndexHints, Extension.LikeFallback, Extension.CheckConstraintsFromAttributes);

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) =>
            other is ExtensionInfo info
            && info.Extension.Dialect == Extension.Dialect
            && info.Extension.IndexHints == Extension.IndexHints
            && info.Extension.LikeFallback == Extension.LikeFallback
            && info.Extension.CheckConstraintsFromAttributes == Extension.CheckConstraintsFromAttributes;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
            ArgumentNullException.ThrowIfNull(debugInfo);
            debugInfo["FluentRegex:Dialect"] = DialectText;
            debugInfo["FluentRegex:IndexHints"] = Extension.IndexHints.ToString();
            debugInfo["FluentRegex:LikeFallback"] = Extension.LikeFallback.ToString();
            debugInfo["FluentRegex:CheckConstraintsFromAttributes"] = Extension.CheckConstraintsFromAttributes.ToString();
        }
    }
}
