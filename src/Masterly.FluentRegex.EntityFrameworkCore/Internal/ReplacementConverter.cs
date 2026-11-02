using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Converts a .NET replacement string (<c>$1</c>, <c>${name}</c>, <c>$$</c>) to the replacement syntax of a
/// SQL regex dialect: <c>\1</c> on PostgreSQL, MariaDB and SQL Server 2025, <c>$1</c> on MySQL (ICU).
/// </summary>
internal static class ReplacementConverter
{
    private const char Dollar = '$';
    private const char Backslash = '\\';
    private const char OpenBrace = '{';
    private const char CloseBrace = '}';
    private const char Ampersand = '&';
    private const int WholeMatch = 0;
    private const string WholeMatchKey = "0";
    private const int MaxSingleDigitGroup = 9;
    private const string UnsupportedSubstitutions = "&`'+_";

    /// <summary>Converts <paramref name="replacement"/> for <paramref name="dialect"/>.</summary>
    /// <param name="replacement">The .NET replacement string.</param>
    /// <param name="dialect">The target dialect.</param>
    /// <param name="groupNumbers">
    /// The pattern's group names and .NET numbers mapped to the dialect's group numbers, or null when the pattern is
    /// not known at query compile time (every group reference except <c>$0</c> then throws).
    /// </param>
    /// <exception cref="NotSupportedException">The replacement uses a substitution the dialect cannot express.</exception>
    /// <exception cref="InvalidOperationException">A group is referenced but the pattern is not known.</exception>
    /// <exception cref="ArgumentException">A named group does not exist in the pattern.</exception>
    public static string Convert(string replacement, SqlDialect dialect, IReadOnlyDictionary<string, int>? groupNumbers)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        StringBuilder sql = new StringBuilder(replacement.Length);
        int index = 0;
        while (index < replacement.Length)
        {
            char current = replacement[index];
            if (current == Dollar && index + 1 < replacement.Length)
            {
                index = AppendSubstitution(replacement, index, sql, dialect, groupNumbers);
            }
            else
            {
                AppendLiteral(sql, current, dialect);
                index++;
            }
        }

        return sql.ToString();
    }

    /// <summary>Appends the substitution starting at <paramref name="index"/> and returns the index after it.</summary>
    private static int AppendSubstitution(
        string replacement,
        int index,
        StringBuilder sql,
        SqlDialect dialect,
        IReadOnlyDictionary<string, int>? groupNumbers)
    {
        char next = replacement[index + 1];
        if (next == Dollar)
        {
            AppendLiteral(sql, Dollar, dialect);
            return index + 2;
        }

        if (next == OpenBrace)
        {
            return AppendBracedGroup(replacement, index, sql, dialect, groupNumbers);
        }

        if (char.IsAsciiDigit(next))
        {
            return AppendNumberedGroup(replacement, index, sql, dialect, groupNumbers);
        }

        if (UnsupportedSubstitutions.IndexOf(next) >= 0)
        {
            throw new NotSupportedException(
                $"The replacement substitution '${next}' cannot be translated to SQL. " +
                "Only numbered groups ($1, ${1}), named groups (${name}) and the literal $$ are supported.");
        }

        // .NET treats a $ that starts no substitution as a literal.
        AppendLiteral(sql, Dollar, dialect);
        return index + 1;
    }

    private static int AppendBracedGroup(
        string replacement,
        int index,
        StringBuilder sql,
        SqlDialect dialect,
        IReadOnlyDictionary<string, int>? groupNumbers)
    {
        int close = replacement.IndexOf(CloseBrace, index + 2);
        if (close < 0)
        {
            AppendLiteral(sql, Dollar, dialect);
            return index + 1;
        }

        string name = replacement.Substring(index + 2, close - index - 2);
        AppendGroup(sql, ResolveGroup(name, groupNumbers), dialect);
        return close + 1;
    }

    private static int AppendNumberedGroup(
        string replacement,
        int index,
        StringBuilder sql,
        SqlDialect dialect,
        IReadOnlyDictionary<string, int>? groupNumbers)
    {
        int end = index + 1;
        while (end < replacement.Length && char.IsAsciiDigit(replacement[end]))
        {
            end++;
        }

        string digits = replacement.Substring(index + 1, end - index - 1);
        int? number = LongestExistingGroup(digits, groupNumbers, out int consumed);
        if (number == null)
        {
            AppendLiteral(sql, Dollar, dialect);
            return index + 1;
        }

        AppendGroup(sql, number.Value, dialect);
        return index + 1 + consumed;
    }

    private static int ResolveGroup(string name, IReadOnlyDictionary<string, int>? groupNumbers)
    {
        if (string.Equals(name, WholeMatchKey, StringComparison.Ordinal))
        {
            return WholeMatch;
        }

        if (groupNumbers == null)
        {
            throw UnknownPattern($"${{{name}}}");
        }

        if (groupNumbers.TryGetValue(name, out int number))
        {
            return number;
        }

        throw new ArgumentException($"The pattern has no group named '{name}'.", nameof(name));
    }

    /// <summary>
    /// Like .NET, takes the longest run of digits that names an existing group (group 0, the whole match,
    /// always exists) and maps it to the dialect's group number, which can differ from the .NET number.
    /// </summary>
    private static int? LongestExistingGroup(string digits, IReadOnlyDictionary<string, int>? groupNumbers, out int consumed)
    {
        if (groupNumbers == null)
        {
            if (string.Equals(digits, WholeMatchKey, StringComparison.Ordinal))
            {
                consumed = digits.Length;
                return WholeMatch;
            }

            throw UnknownPattern($"${digits}");
        }

        for (int length = digits.Length; length > 0; length--)
        {
            string prefix = digits.Substring(0, length);
            if (string.Equals(prefix, WholeMatchKey, StringComparison.Ordinal))
            {
                consumed = length;
                return WholeMatch;
            }

            if (groupNumbers.TryGetValue(prefix, out int number))
            {
                consumed = length;
                return number;
            }
        }

        consumed = 0;
        return null;
    }

    /// <summary>
    /// Group numbers differ between .NET and the database engines (.NET numbers unnamed groups first, PostgreSQL
    /// has no named groups and drops groups inside lookarounds), so a reference can only be mapped when the
    /// pattern is known at query compile time.
    /// </summary>
    private static InvalidOperationException UnknownPattern(string reference) =>
        new InvalidOperationException(
            $"The group reference '{reference}' can only be translated when the pattern is known at query compile time, " +
            "because the database numbers groups differently from .NET. Reference the pattern through a static " +
            "readonly field or build it inline in the query.");

    private static void AppendGroup(StringBuilder sql, int number, SqlDialect dialect)
    {
        switch (dialect)
        {
            case SqlDialect.MySql:
                // ICU replacement syntax: $n, any number of digits.
                sql.Append(Dollar).Append(number.ToString(CultureInfo.InvariantCulture));
                return;
            case SqlDialect.PostgreSql:
                if (number == WholeMatch)
                {
                    sql.Append(Backslash).Append(Ampersand);
                    return;
                }

                AppendBackslashGroup(sql, number, dialect);
                return;
            case SqlDialect.MariaDb:
                if (number == WholeMatch)
                {
                    throw new NotSupportedException("MariaDB cannot reference the whole match ($0) in a replacement.");
                }

                AppendBackslashGroup(sql, number, dialect);
                return;
            case SqlDialect.SqlServer2025:
                AppendBackslashGroup(sql, number, dialect);
                return;
            default:
                throw new NotSupportedException($"{dialect} has no regular expression replacement function.");
        }
    }

    private static void AppendBackslashGroup(StringBuilder sql, int number, SqlDialect dialect)
    {
        if (number > MaxSingleDigitGroup)
        {
            throw new NotSupportedException(
                $"{dialect} replacement strings can only reference groups 1 to {MaxSingleDigitGroup} (group {number} was referenced).");
        }

        sql.Append(Backslash).Append(number.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendLiteral(StringBuilder sql, char c, SqlDialect dialect)
    {
        // PostgreSQL, ICU (MySQL), PCRE (MariaDB) and RE2 (SQL Server 2025) all read "\\" in a replacement as one
        // literal backslash.
        if (c == Backslash)
        {
            sql.Append(Backslash).Append(Backslash);
            return;
        }

        if (c == Dollar && dialect == SqlDialect.MySql)
        {
            sql.Append(Backslash).Append(Dollar);
            return;
        }

        sql.Append(c);
    }
}
