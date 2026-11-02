#nullable enable

namespace Masterly.FluentRegex;

/// <summary>The regular expression dialects a <see cref="Pattern"/> can be exported to.</summary>
public enum RegexFlavor
{
    /// <summary>
    /// ECMAScript 2018+ as used by JavaScript and TypeScript, with the <c>u</c> flag
    /// (needed for <c>\p{...}</c>, named groups and lookbehind).
    /// </summary>
    JavaScript,

    /// <summary>
    /// The <c>pattern</c> keyword of JSON Schema (and OpenAPI): ECMA-262 syntax without flags, so
    /// case-insensitivity and dot-all are expanded into character classes. The output is written to
    /// be valid with the <c>u</c> flag, which validators such as Ajv use by default.
    /// </summary>
    JsonSchema,

    /// <summary>
    /// PostgreSQL advanced regular expressions (ARE, the Tcl-derived engine behind <c>~</c> and
    /// <c>regexp_like</c>). Options are expanded into the pattern text, so the result can be used
    /// with the plain <c>~</c> operator. Word boundaries, <c>$</c>, <c>.</c>, <c>\d</c>, <c>\w</c>,
    /// <c>\s</c>, Unicode categories and case-insensitivity are written out explicitly because the
    /// engine's own versions are locale-dependent or differ from .NET.
    /// </summary>
    PostgreSql,

    /// <summary>
    /// MySQL 8.0+ regular expressions (ICU, used by <c>REGEXP_LIKE</c> and <c>REGEXP</c>). The pattern
    /// starts with <c>(?-i)</c> because MySQL otherwise matches case-insensitively under
    /// case-insensitive collations; case-insensitive .NET patterns are expanded into exact classes.
    /// </summary>
    MySql,

    /// <summary>
    /// MariaDB regular expressions (PCRE2, used by <c>REGEXP</c>). The pattern starts with <c>(?-i)</c>
    /// because MariaDB otherwise matches case-insensitively under case-insensitive collations;
    /// case-insensitive .NET patterns are expanded into exact classes.
    /// </summary>
    MariaDb,

    /// <summary>
    /// SQL Server 2025 and Azure SQL <c>REGEXP_LIKE</c> (RE2 syntax): no lookaround, no backreferences,
    /// no atomic groups. Constructs the engine cannot express throw <see cref="System.NotSupportedException"/>.
    /// </summary>
    SqlServer,

    /// <summary>
    /// SQLite through the EF Core provider, whose <c>regexp()</c> function runs the .NET engine with
    /// <c>RegexOptions.NonBacktracking</c>. The pattern is the .NET
    /// pattern with its options written as an inline prefix such as <c>(?i)</c>; constructs the
    /// non-backtracking engine rejects (backreferences, lookarounds, atomic groups, conditionals)
    /// throw <see cref="System.NotSupportedException"/>.
    /// </summary>
    Sqlite
}
