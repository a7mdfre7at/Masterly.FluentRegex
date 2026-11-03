using System;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.EntityFrameworkCore;

/// <summary>
/// Pattern functions that EF Core translates to SQL, usable like <c>EF.Functions</c>. Outside a query they run in .NET
/// with the same semantics, so the same expression works against a database and in memory.
/// </summary>
/// <remarks>
/// In SQL a NULL value never matches: regex predicates on NULL yield NULL, so the row is excluded, and
/// <c>Group</c> and <see cref="Replace"/> return NULL. The .NET implementations mirror that.
/// </remarks>
public static class PatternFunctions
{
    /// <summary>
    /// True when <paramref name="pattern"/> matches anywhere in <paramref name="value"/>; false for null.
    /// Translated to the dialect's regex predicate, e.g. <c>"Email" ~ '...'</c> on PostgreSQL.
    /// </summary>
    /// <param name="value">The text to search.</param>
    /// <param name="pattern">The pattern.</param>
    /// <returns>True when the pattern matches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    public static bool Matches(string? value, Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return value != null && pattern.IsMatch(value);
    }

    /// <summary>
    /// The text captured by the group named <paramref name="groupName"/> in the first match, or null when
    /// <paramref name="value"/> is null, the pattern does not match or the group did not participate.
    /// Translated on PostgreSQL (<c>regexp_substr</c>) and SQL Server 2025 (<c>REGEXP_SUBSTR</c>).
    /// </summary>
    /// <param name="value">The text to search.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="groupName">The group name.</param>
    /// <returns>The captured text, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> or <paramref name="groupName"/> is null.</exception>
    public static string? Group(string? value, Pattern pattern, string groupName)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(groupName);
        if (value == null)
        {
            return null;
        }

        Match match = pattern.Match(value);
        return CapturedText(match, match.Groups[groupName]);
    }

    /// <summary>
    /// The text captured by group <paramref name="groupNumber"/> in the first match, or null when
    /// <paramref name="value"/> is null, the pattern does not match or the group did not participate.
    /// Translated on PostgreSQL (<c>regexp_substr</c>) and SQL Server 2025 (<c>REGEXP_SUBSTR</c>).
    /// </summary>
    /// <param name="value">The text to search.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="groupNumber">The group number; 0 is the whole match.</param>
    /// <returns>The captured text, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    public static string? Group(string? value, Pattern pattern, int groupNumber)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (value == null)
        {
            return null;
        }

        Match match = pattern.Match(value);
        return CapturedText(match, match.Groups[groupNumber]);
    }

    /// <summary>
    /// Replaces every match of <paramref name="pattern"/> in <paramref name="value"/> with
    /// <paramref name="replacement"/> (.NET syntax: <c>$1</c>, <c>${name}</c>, <c>$$</c>); null for null.
    /// Translated to <c>regexp_replace</c> / <c>REGEXP_REPLACE</c> on PostgreSQL, MySQL, MariaDB and SQL Server 2025,
    /// with the substitutions converted to the dialect's syntax.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="replacement">The replacement, in .NET syntax.</param>
    /// <returns>The replaced text, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> or <paramref name="replacement"/> is null.</exception>
    public static string? Replace(string? value, Pattern pattern, string replacement)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(replacement);
        return value == null ? null : pattern.Replace(value, replacement);
    }

    private static string? CapturedText(Match match, System.Text.RegularExpressions.Group group) =>
        match.Success && group.Success ? group.Value : null;
}
