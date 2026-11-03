using System;
using Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Masterly.FluentRegex.EntityFrameworkCore;

/// <summary>Model configuration that turns a pattern into a database CHECK constraint.</summary>
public static class FluentRegexPropertyBuilderExtensions
{
    /// <summary>
    /// Requires every non-null value of the string property to be accepted by <paramref name="pattern"/>, enforced by
    /// a CHECK constraint in the configured dialect (<c>CHECK ("Sku" ~ '...')</c> on PostgreSQL). The constraint has
    /// the semantics of <see cref="Pattern.IsMatch(string)"/> and <see cref="MatchesPatternAttribute"/>, so the
    /// database and the validator never disagree; anchor the pattern (<c>^...$</c>, or <c>\A...\z</c> to also reject
    /// a trailing newline) to constrain the whole value. NULL passes a CHECK constraint, so a nullable column stays
    /// nullable. Requires <c>UseFluentRegex()</c> on the
    /// context options; on <see cref="SqlDialect.SqlServer"/> the pattern must have an exact <c>LIKE</c> translation,
    /// otherwise model building throws with guidance.
    /// </summary>
    /// <typeparam name="TProperty">The property type, which must be <see cref="string"/>.</typeparam>
    /// <param name="builder">The property builder.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="constraintName">The constraint name, or null for <c>CK_{Table}_{Column}_Pattern</c>.</param>
    /// <returns>The property builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The property is not a string.</exception>
    /// <example>
    /// modelBuilder.Entity&lt;Product&gt;().Property(p => p.Sku).HasPattern(Patterns.Sku);
    /// </example>
    public static PropertyBuilder<TProperty> HasPattern<TProperty>(
        this PropertyBuilder<TProperty> builder,
        Pattern pattern,
        string? constraintName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(pattern);
        if (builder.Metadata.ClrType != typeof(string))
        {
            throw new InvalidOperationException(
                $"HasPattern can only be applied to string properties; '{builder.Metadata.Name}' is of type {builder.Metadata.ClrType}.");
        }

        builder.HasAnnotation(FluentRegexAnnotationNames.Pattern, pattern);
        if (constraintName != null)
        {
            builder.HasAnnotation(FluentRegexAnnotationNames.ConstraintName, constraintName);
        }

        return builder;
    }
}
