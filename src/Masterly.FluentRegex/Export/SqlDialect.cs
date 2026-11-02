#nullable enable
using System;
using System.Text;

namespace Masterly.FluentRegex.Export;

/// <summary>SQL string-literal quoting and the regex-match condition syntax of each SQL flavor.</summary>
internal static class SqlDialect
{
    private const char Quote1 = '\'';
    private const string DoubledQuote = "''";
    private const char Backslash = '\\';
    private const string DoubledBackslash = @"\\";

    /// <summary>Returns true for the flavors that target a database engine.</summary>
    public static bool IsSql(RegexFlavor flavor)
    {
        switch (flavor)
        {
            case RegexFlavor.PostgreSql:
            case RegexFlavor.MySql:
            case RegexFlavor.MariaDb:
            case RegexFlavor.SqlServer:
            case RegexFlavor.Sqlite:
                return true;
            default:
                return false;
        }
    }

    /// <summary>Returns true when the flavor's string literals process C-style backslash escapes.</summary>
    public static bool EscapesBackslashes(RegexFlavor flavor) =>
        flavor == RegexFlavor.MySql || flavor == RegexFlavor.MariaDb;

    /// <summary>Writes <paramref name="text"/> as a single-quoted SQL string literal.</summary>
    public static string Quote(string text, RegexFlavor flavor)
    {
        bool escapeBackslashes = EscapesBackslashes(flavor);
        StringBuilder sb = new StringBuilder(text.Length + 2);
        sb.Append(Quote1);
        foreach (char c in text)
        {
            if (c == Quote1)
            {
                sb.Append(DoubledQuote);
            }
            else if (c == Backslash && escapeBackslashes)
            {
                sb.Append(DoubledBackslash);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.Append(Quote1).ToString();
    }

    /// <summary>Writes the boolean SQL expression that tests <paramref name="columnSql"/> against a regex literal.</summary>
    public static string Condition(string columnSql, string literal, RegexFlavor flavor)
    {
        switch (flavor)
        {
            case RegexFlavor.PostgreSql:
                return $"{columnSql} ~ {literal}";
            case RegexFlavor.MySql:
            case RegexFlavor.SqlServer:
                return $"REGEXP_LIKE({columnSql}, {literal})";
            case RegexFlavor.MariaDb:
            case RegexFlavor.Sqlite:
                return $"{columnSql} REGEXP {literal}";
            default:
                throw new ArgumentException($"{flavor} is not a SQL flavor.", nameof(flavor));
        }
    }

    /// <summary>The engine name used in messages.</summary>
    public static string Name(RegexFlavor flavor)
    {
        switch (flavor)
        {
            case RegexFlavor.PostgreSql:
                return "PostgreSQL";
            case RegexFlavor.MySql:
                return "MySQL";
            case RegexFlavor.MariaDb:
                return "MariaDB";
            case RegexFlavor.SqlServer:
                return "SQL Server";
            case RegexFlavor.Sqlite:
                return "SQLite";
            case RegexFlavor.JsonSchema:
                return "JSON Schema";
            default:
                return "JavaScript";
        }
    }
}
