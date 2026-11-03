#nullable enable

namespace Masterly.FluentRegex;

/// <summary>The LIKE syntax of the target database.</summary>
public enum LikeDialect
{
    /// <summary>SQL-standard LIKE: <c>%</c> and <c>_</c> only, with an optional <c>ESCAPE</c> clause.</summary>
    Standard,

    /// <summary>
    /// SQL Server LIKE, which adds <c>[set]</c>, <c>[^set]</c> and ranges such as <c>[0-9]</c>. Ranges
    /// are only used under <see cref="LikeCollation.Binary"/>, where they follow code-point order.
    /// </summary>
    SqlServer,

    /// <summary>PostgreSQL LIKE: <c>%</c> and <c>_</c>; backslash is the default escape character.</summary>
    PostgreSql,

    /// <summary>MySQL and MariaDB LIKE: <c>%</c> and <c>_</c>; backslash is the default escape character and string literals process C escapes.</summary>
    MySql,

    /// <summary>SQLite LIKE: <c>%</c> and <c>_</c> (case-insensitive for ASCII letters unless <c>PRAGMA case_sensitive_like</c> is on).</summary>
    Sqlite
}
