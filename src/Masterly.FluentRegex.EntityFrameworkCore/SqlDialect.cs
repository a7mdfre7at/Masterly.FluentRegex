namespace Masterly.FluentRegex.EntityFrameworkCore;

/// <summary>
/// The SQL dialects a <see cref="Pattern"/> can be translated to. The dialect is detected from the
/// registered database provider and can be overridden with <see cref="FluentRegexOptionsBuilder.UseDialect"/>.
/// </summary>
public enum SqlDialect
{
    /// <summary>PostgreSQL: <c>column ~ 'regex'</c> (advanced regular expressions).</summary>
    PostgreSql,

    /// <summary>MySQL 8.0 and later: <c>REGEXP_LIKE(column, 'regex')</c> (ICU regular expressions).</summary>
    MySql,

    /// <summary>MariaDB 10.0.5 and later: <c>column REGEXP 'regex'</c> (PCRE regular expressions).</summary>
    MariaDb,

    /// <summary>
    /// SQL Server 2025 and Azure SQL at compatibility level 170 or later:
    /// <c>REGEXP_LIKE(column, 'regex')</c> (RE2 regular expressions).
    /// </summary>
    SqlServer2025,

    /// <summary>
    /// SQL Server before 2025: there is no regular expression support, so a pattern is translated to
    /// <c>LIKE</c> only when the translation is exact (see <see cref="LikeFallback"/>).
    /// </summary>
    SqlServer,

    /// <summary>
    /// SQLite through Microsoft.Data.Sqlite: <c>column REGEXP 'regex'</c>, evaluated by the .NET regex
    /// engine that the EF Core provider registers on the connection, so every pattern is exact.
    /// </summary>
    Sqlite
}
