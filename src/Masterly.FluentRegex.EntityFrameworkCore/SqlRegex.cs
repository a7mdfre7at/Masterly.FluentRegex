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
    /// translation is returned, compared under <c>COLLATE Latin1_General_100_BIN2</c> so that it means what .NET
    /// means, or an exception is thrown when there is none. NULL never matches.
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
    /// <paramref name="pattern"/>, stating whether the column's collation is binary. It only matters on
    /// <see cref="SqlDialect.SqlServer"/>: with <see cref="LikeCollation.Binary"/> (a <c>_BIN2</c> or <c>_BIN</c>
    /// column) the <c>LIKE</c> is written without <c>COLLATE</c>, so that it can use an index.
    /// </summary>
    /// <param name="columnSql">The column expression, quoted as the database requires.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="collation"><see cref="LikeCollation.Binary"/> when the column's collation is binary.</param>
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
