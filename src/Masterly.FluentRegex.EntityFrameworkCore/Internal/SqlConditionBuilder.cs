using System;
using System.Collections.Generic;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>A raw SQL condition plus the translation warnings it was produced with.</summary>
/// <param name="Sql">The condition, e.g. <c>"Email" ~ '...'</c>.</param>
/// <param name="Warnings">Behaviors of the dialect regex that differ from .NET.</param>
internal sealed record SqlCondition(string Sql, IReadOnlyList<string> Warnings);

/// <summary>
/// Builds raw SQL conditions (for <see cref="SqlRegex"/> and CHECK constraints) from a pattern. Every condition has
/// the semantics of <see cref="Pattern.IsMatch(string)"/>, so the database accepts exactly the values that the .NET
/// pattern and <see cref="MatchesPatternAttribute"/> accept.
/// </summary>
internal static class SqlConditionBuilder
{
    private const string LikeEscapeKeyword = "ESCAPE";

    // MySQL's REGEXP_LIKE follows the (usually case-insensitive) collation unless the 'c' match type is given.
    private const string CaseSensitiveFlag = "c";

    /// <summary>
    /// Builds the condition that is true when <paramref name="columnSql"/> contains a match of <paramref name="pattern"/>.
    /// </summary>
    /// <param name="columnSql">The column expression, already quoted for the dialect.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="dialect">The dialect.</param>
    /// <param name="collation">What the collation does to <c>LIKE</c>; only used on <see cref="SqlDialect.SqlServer"/>.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="dialect"/> is <see cref="SqlDialect.SqlServer"/> and the pattern has no exact LIKE translation.
    /// </exception>
    /// <exception cref="NotSupportedException">The pattern uses a construct the dialect cannot express.</exception>
    public static SqlCondition Build(string columnSql, Pattern pattern, SqlDialect dialect, LikeCollation collation)
    {
        ArgumentNullException.ThrowIfNull(columnSql);
        ArgumentNullException.ThrowIfNull(pattern);
        if (dialect == SqlDialect.SqlServer)
        {
            return BuildLike(columnSql, pattern, collation);
        }

        DialectRegex regex = CoreDialectAdapter.ToDialectRegex(pattern, dialect);
        string literal = SqlLiteral.String(regex.RegexText, dialect);
        string sql = dialect switch
        {
            SqlDialect.PostgreSql => $"{columnSql} ~ {literal}",
            SqlDialect.MySql => $"REGEXP_LIKE({columnSql}, {literal}, '{CaseSensitiveFlag}')",
            SqlDialect.MariaDb => $"{columnSql} REGEXP {literal}",
            SqlDialect.SqlServer2025 => $"REGEXP_LIKE({columnSql}, {literal})",
            SqlDialect.Sqlite => $"{columnSql} REGEXP {literal}",
            _ => throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "Unknown SQL dialect.")
        };

        return new SqlCondition(sql, regex.Warnings);
    }

    /// <summary>The message explaining that SQL Server before 2025 cannot express a pattern exactly.</summary>
    public static string NoExactLikeMessage(Pattern pattern) =>
        $"The pattern '{pattern}' cannot be expressed exactly as a LIKE expression, and SQL Server before 2025 has no " +
        "regular expression support. Run the database at compatibility level 170 or later and configure " +
        "UseDialect(SqlDialect.SqlServer2025), or keep this check in .NET.";

    private static SqlCondition BuildLike(string columnSql, Pattern pattern, LikeCollation collation)
    {
        bool caseInsensitive = collation == LikeCollation.CaseInsensitive;
        DialectLike like = CoreDialectAdapter.ToDialectLike(pattern, SqlDialect.SqlServer, substring: true, caseInsensitiveCollation: caseInsensitive);
        if (!like.IsExact)
        {
            throw new InvalidOperationException(NoExactLikeMessage(pattern));
        }

        string condition = $"{columnSql} LIKE {SqlLiteral.String(like.Expression, SqlDialect.SqlServer)}";
        string sql = like.EscapeCharacter is char escape
            ? $"{condition} {LikeEscapeKeyword} {SqlLiteral.String(escape.ToString(), SqlDialect.SqlServer)}"
            : condition;
        return new SqlCondition(sql, Array.Empty<string>());
    }
}
