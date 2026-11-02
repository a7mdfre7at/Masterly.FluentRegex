using System;
using Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Masterly.FluentRegex.EntityFrameworkCore;

/// <summary>Enables Masterly.FluentRegex pattern translation on a <see cref="DbContext"/>.</summary>
public static class FluentRegexDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Translates <see cref="Pattern.IsMatch(string)"/>, <see cref="PatternFunctions"/> and
    /// <c>EF.Functions.Matches</c> in LINQ queries to the database's regex dialect, and turns
    /// <c>HasPattern</c> configuration into CHECK constraints. The dialect is detected from the registered
    /// provider (Npgsql, Pomelo MySQL/MariaDB, SQL Server, SQLite) unless <see cref="FluentRegexOptionsBuilder.UseDialect"/> is called.
    /// </summary>
    /// <param name="builder">The options builder.</param>
    /// <param name="configure">Optional configuration.</param>
    /// <returns>The options builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    /// <example>
    /// services.AddDbContext&lt;ShopContext&gt;(options => options
    ///     .UseNpgsql(connectionString)
    ///     .UseFluentRegex(fluent => fluent.AddCheckConstraintsFromAttributes()));
    /// </example>
    public static DbContextOptionsBuilder UseFluentRegex(this DbContextOptionsBuilder builder, Action<FluentRegexOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        FluentRegexOptionsExtension extension = builder.Options.FindExtension<FluentRegexOptionsExtension>() ?? new FluentRegexOptionsExtension();
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);
        configure?.Invoke(new FluentRegexOptionsBuilder(builder));
        return builder;
    }

    /// <summary>
    /// Translates patterns in LINQ queries and emits CHECK constraints; see
    /// <see cref="UseFluentRegex(DbContextOptionsBuilder, Action{FluentRegexOptionsBuilder}?)"/>.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <param name="configure">Optional configuration.</param>
    /// <returns>The options builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static DbContextOptionsBuilder<TContext> UseFluentRegex<TContext>(
        this DbContextOptionsBuilder<TContext> builder,
        Action<FluentRegexOptionsBuilder>? configure = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseFluentRegex((DbContextOptionsBuilder)builder, configure);
}
