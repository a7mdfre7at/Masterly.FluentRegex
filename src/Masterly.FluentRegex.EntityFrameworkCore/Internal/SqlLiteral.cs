using System;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>Quotes string literals for raw SQL in each dialect.</summary>
internal static class SqlLiteral
{
    private const string Quote = "'";
    private const string EscapedQuote = "''";
    private const string Backslash = "\\";
    private const string EscapedBackslash = "\\\\";
    private const string UnicodePrefix = "N";

    /// <summary>Returns <paramref name="value"/> as a quoted SQL string literal for <paramref name="dialect"/>.</summary>
    public static string String(string value, SqlDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(value);
        string body = value.Replace(Quote, EscapedQuote, StringComparison.Ordinal);
        switch (dialect)
        {
            case SqlDialect.SqlServer:
            case SqlDialect.SqlServer2025:
                return $"{UnicodePrefix}{Quote}{body}{Quote}";
            case SqlDialect.MySql:
            case SqlDialect.MariaDb:
                // MySQL and MariaDB treat a backslash in a string literal as an escape character (default sql_mode).
                return $"{Quote}{body.Replace(Backslash, EscapedBackslash, StringComparison.Ordinal)}{Quote}";
            default:
                return $"{Quote}{body}{Quote}";
        }
    }
}
