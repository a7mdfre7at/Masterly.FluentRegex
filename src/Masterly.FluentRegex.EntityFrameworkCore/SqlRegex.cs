using System;
using Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

namespace Masterly.FluentRegex.EntityFrameworkCore;

/// <summary>Raw SQL conditions for Dapper, ADO.NET and hand-written migrations.</summary>
public static class SqlRegex
{
    /// <summary>
    /// Returns the SQL condition that is true when <paramref name="columnSql"/> contains a match of
    /// <paramref name="pattern"/> (the semantics of <see cref="Pattern.IsMatch(string)"/>): for example
    /// <c>"Email" ~ '...'</c> on PostgreSQL, <c>REGEXP_LIKE(Email, '...')</c> on MySQL and SQL Server 2025,
    /// <c>Email REGEXP '...'</c> on MariaDB and SQLite. On <see cref="SqlDialect.SqlServer"/> the exact <c>LIKE</c>
    /// translation is returned, or an exception is thrown when there is none. NULL never matches.
    /// </summary>
    /// <param name="columnSql">The column expression, quoted as the database requires.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="dialect">The dialect.</param>
    /// <returns>The condition.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="columnSql"/> is blank.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="dialect"/> is SQL Server without regex support and the pattern has no exact LIKE form.</exception>
    /// <exception cref="NotSupportedException">The pattern uses an option or construct the dialect cannot express.</exception>
    /// <example>
    /// string where = SqlRegex.Condition("\"Email\"", Patterns.Email, SqlDialect.PostgreSql);
    /// connection.Query&lt;User&gt;($"SELECT * FROM users WHERE {where}");
    /// </example>
    public static string Condition(string columnSql, Pattern pattern, SqlDialect dialect) =>
        Condition(columnSql, pattern, dialect, LikeCollation.CaseInsensitive);

    /// <summary>
    /// Returns the SQL condition that is true when <paramref name="columnSql"/> contains a match of
    /// <paramref name="pattern"/>, stating what the column's collation does to <c>LIKE</c>. The collation only
    /// matters on <see cref="SqlDialect.SqlServer"/>, where it decides whether the <c>LIKE</c> translation is
    /// proven exact: under <see cref="LikeCollation.CaseInsensitive"/> a case-sensitive pattern with letters never is.
    /// </summary>
    /// <param name="columnSql">The column expression, quoted as the database requires.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="collation">What the column's collation does to <c>LIKE</c>.</param>
    /// <returns>The condition.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="columnSql"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument is not a defined value.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="dialect"/> is SQL Server without regex support and the pattern has no exact LIKE form.</exception>
    /// <exception cref="NotSupportedException">The pattern uses an option or construct the dialect cannot express.</exception>
    public static string Condition(string columnSql, Pattern pattern, SqlDialect dialect, LikeCollation collation)
    {
        ArgumentNullException.ThrowIfNull(columnSql);
        ArgumentNullException.ThrowIfNull(pattern);
        if (columnSql.Trim().Length == 0)
        {
            throw new ArgumentException("The column SQL must not be blank.", nameof(columnSql));
        }

        if (!Enum.IsDefined(dialect))
        {
            throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "Unknown SQL dialect.");
        }

        if (!Enum.IsDefined(collation))
        {
            throw new ArgumentOutOfRangeException(nameof(collation), collation, "Unknown LIKE collation.");
        }

        return SqlConditionBuilder.Build(columnSql, pattern, dialect, collation).Sql;
    }
}
