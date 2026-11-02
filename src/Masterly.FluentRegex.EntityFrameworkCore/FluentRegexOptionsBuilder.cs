using System;
using Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Masterly.FluentRegex.EntityFrameworkCore;

/// <summary>Configures how patterns are translated to SQL. Obtained from <c>UseFluentRegex(fluent => ...)</c>.</summary>
public sealed class FluentRegexOptionsBuilder
{
    private readonly DbContextOptionsBuilder _builder;

    internal FluentRegexOptionsBuilder(DbContextOptionsBuilder builder)
    {
        _builder = builder;
    }

    /// <summary>
    /// Uses <paramref name="dialect"/> instead of detecting it from the database provider. Needed for providers
    /// this package does not recognize, and to opt into <see cref="SqlDialect.SqlServer2025"/> when the
    /// compatibility level is not configured on the provider.
    /// </summary>
    /// <param name="dialect">The dialect.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dialect"/> is not a defined value.</exception>
    public FluentRegexOptionsBuilder UseDialect(SqlDialect dialect)
    {
        if (!Enum.IsDefined(dialect))
        {
            throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "Unknown SQL dialect.");
        }

        return WithOption(extension => extension.WithDialect(dialect));
    }

    /// <summary>
    /// Adds proven conjuncts next to the regex predicate, such as <c>column LIKE 'prefix%'</c> and an upper
    /// length bound, so that a B-tree index can narrow the scan. The facts are sound for every string the
    /// pattern accepts, so results are unchanged. Enabled by default.
    /// </summary>
    /// <param name="enabled">False to emit the regex predicate alone.</param>
    /// <returns>This builder, for chaining.</returns>
    public FluentRegexOptionsBuilder UseIndexHints(bool enabled = true) => WithOption(extension => extension.WithIndexHints(enabled));

    /// <summary>
    /// Chooses how patterns are translated on <see cref="SqlDialect.SqlServer"/>, which has no regular expression
    /// support. The default, <see cref="LikeFallback.ExactOnly"/>, translates to <c>LIKE</c> only when it is exact
    /// and otherwise throws with guidance.
    /// </summary>
    /// <param name="fallback">The fallback.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fallback"/> is not a defined value.</exception>
    public FluentRegexOptionsBuilder UseLikeFallback(LikeFallback fallback)
    {
        if (!Enum.IsDefined(fallback))
        {
            throw new ArgumentOutOfRangeException(nameof(fallback), fallback, "Unknown LIKE fallback.");
        }

        return WithOption(extension => extension.WithLikeFallback(fallback));
    }

    /// <summary>
    /// Creates a CHECK constraint for every string property annotated with <see cref="MatchesPatternAttribute"/>,
    /// with whole-value semantics. A <see cref="ConstrainedPattern"/> contributes only its
    /// <see cref="ConstrainedPattern.Pattern"/>; <c>Must</c> rules run in .NET only.
    /// </summary>
    /// <param name="enabled">False to ignore the attributes.</param>
    /// <returns>This builder, for chaining.</returns>
    public FluentRegexOptionsBuilder AddCheckConstraintsFromAttributes(bool enabled = true) =>
        WithOption(extension => extension.WithCheckConstraintsFromAttributes(enabled));

    private FluentRegexOptionsBuilder WithOption(Func<FluentRegexOptionsExtension, FluentRegexOptionsExtension> change)
    {
        FluentRegexOptionsExtension current = _builder.Options.FindExtension<FluentRegexOptionsExtension>() ?? new FluentRegexOptionsExtension();
        ((IDbContextOptionsBuilderInfrastructure)_builder).AddOrUpdateExtension(change(current));
        return this;
    }
}
