using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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
    /// <param name="options">
    /// The pattern's options. Only <see cref="RegexOptions.ECMAScript"/> matters: it changes how .NET reads the digits
    /// of <c>$12</c>.
    /// </param>
    /// <exception cref="NotSupportedException">The replacement uses a substitution the dialect cannot express.</exception>
    /// <exception cref="InvalidOperationException">A group is referenced but the pattern is not known.</exception>
    /// <exception cref="ArgumentException">A named group does not exist in the pattern.</exception>
    public static string Convert(
        string replacement,
        SqlDialect dialect,
        IReadOnlyDictionary<string, int>? groupNumbers,
        RegexOptions options = RegexOptions.None)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        bool ecmaScript = (options & RegexOptions.ECMAScript) != 0;
        StringBuilder sql = new StringBuilder(replacement.Length);
        int index = 0;
        while (index < replacement.Length)
        {
            char current = replacement[index];
            if (current == Dollar && index + 1 < replacement.Length)
            {
                index = AppendSubstitution(replacement, index, sql, dialect, groupNumbers, ecmaScript);
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
        IReadOnlyDictionary<string, int>? groupNumbers,
        bool ecmaScript)
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
            return AppendNumberedGroup(replacement, index, sql, dialect, groupNumbers, ecmaScript);
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
        IReadOnlyDictionary<string, int>? groupNumbers,
        bool ecmaScript)
    {
        int end = index + 1;
        while (end < replacement.Length && char.IsAsciiDigit(replacement[end]))
        {
            end++;
        }

        string digits = replacement.Substring(index + 1, end - index - 1);
        int consumed;
        int? number = ecmaScript
            ? ExtendingGroup(digits, groupNumbers, out consumed)
            : WholeNumberGroup(digits, groupNumbers, out consumed);
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
    /// .NET's default rule: every digit after the <c>$</c> forms one number (so <c>$02</c> is group 2). When that
    /// number is no group, the <c>$</c> is literal, so <c>$12</c> with two groups stays the text <c>$12</c>.
    /// Returns the dialect's group number, which can differ from the .NET number, or null for a literal <c>$</c>.
    /// </summary>
    private static int? WholeNumberGroup(string digits, IReadOnlyDictionary<string, int>? groupNumbers, out int consumed)
    {
        consumed = digits.Length;
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int dotNetNumber))
        {
            consumed = 0;
            return null;
        }

        if (dotNetNumber == WholeMatch)
        {
            return WholeMatch;
        }

        if (groupNumbers == null)
        {
            throw UnknownPattern($"${digits}");
        }

        if (groupNumbers.TryGetValue(dotNetNumber.ToString(CultureInfo.InvariantCulture), out int number))
        {
            return number;
        }

        consumed = 0;
        return null;
    }

    /// <summary>
    /// .NET's rule under <see cref="RegexOptions.ECMAScript"/>: start with the first digit and add the next digit
    /// while the longer number is still a group, so <c>$12</c> is group 1 followed by "2" when group 12 does not
    /// exist. Returns the dialect's group number, or null for a literal <c>$</c>.
    /// </summary>
    private static int? ExtendingGroup(string digits, IReadOnlyDictionary<string, int>? groupNumbers, out int consumed)
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

        int dotNetNumber = digits[0] - '0';
        consumed = 1;
        while (consumed < digits.Length)
        {
            long longer = (dotNetNumber * 10L) + (digits[consumed] - '0');
            if (longer > int.MaxValue || !GroupExists((int)longer, groupNumbers))
            {
                break;
            }

            dotNetNumber = (int)longer;
            consumed++;
        }

        if (dotNetNumber == WholeMatch)
        {
            return WholeMatch;
        }

        if (groupNumbers.TryGetValue(dotNetNumber.ToString(CultureInfo.InvariantCulture), out int number))
        {
            return number;
        }

        consumed = 0;
        return null;
    }

    private static bool GroupExists(int dotNetNumber, IReadOnlyDictionary<string, int> groupNumbers) =>
        dotNetNumber == WholeMatch || groupNumbers.ContainsKey(dotNetNumber.ToString(CultureInfo.InvariantCulture));

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
