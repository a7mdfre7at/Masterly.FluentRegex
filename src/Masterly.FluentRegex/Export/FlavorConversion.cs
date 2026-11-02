#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Masterly.FluentRegex.Export;

namespace Masterly.FluentRegex;

/// <summary>
/// A pattern translated to another regex dialect by <see cref="Pattern.ToFlavor"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IsExact"/> is true when no warning was reported, meaning the translation accepts the
/// same strings as the .NET pattern for text in the Basic Multilingual Plane. Positive Unicode classes
/// such as <c>\p{L}</c> can additionally match supplementary letters (outside U+0000-U+FFFF) in
/// JavaScript, which .NET sees as two surrogate code units; constructs that can match a lone surrogate
/// in .NET, such as <c>.</c> or <c>[^a]</c>, always produce a warning.
/// </para>
/// <para>
/// For the SQL flavors (<see cref="RegexFlavor.PostgreSql"/>, <see cref="RegexFlavor.MySql"/>,
/// <see cref="RegexFlavor.MariaDb"/>, <see cref="RegexFlavor.SqlServer"/> and <see cref="RegexFlavor.Sqlite"/>)
/// <see cref="Flags"/> is always empty: every option is embedded in the pattern text, so
/// <see cref="Pattern.ToSqlCondition"/> can always emit <c>column OPERATOR 'pattern'</c>.
/// </para>
/// <para>
/// Acceptance and extent are reported separately: <see cref="Warnings"/> (and <see cref="IsExact"/>) cover
/// whether a string matches; <see cref="MatchExtentWarnings"/> (and <see cref="PreservesMatchExtent"/>)
/// cover which substring is matched and what each group captures, which matters for extraction and
/// replacement.
/// </para>
/// </remarks>
public sealed class FlavorConversion
{
    private const char Slash = '/';
    private const string EmptyJavaScriptPattern = "(?:)";
    private const int FirstPrintable = 0x20;

    internal FlavorConversion(
        RegexFlavor flavor,
        string pattern,
        string flags,
        IReadOnlyList<string> warnings,
        IReadOnlyDictionary<string, int> groupNumbers)
        : this(flavor, pattern, flags, warnings, groupNumbers, new List<string>())
    {
    }

    internal FlavorConversion(
        RegexFlavor flavor,
        string pattern,
        string flags,
        IReadOnlyList<string> warnings,
        IReadOnlyDictionary<string, int> groupNumbers,
        IReadOnlyList<string> matchExtentWarnings)
    {
        Flavor = flavor;
        Pattern = pattern;
        Flags = flags;
        Warnings = warnings;
        GroupNumbers = groupNumbers;
        MatchExtentWarnings = matchExtentWarnings;
    }

    /// <summary>Gets the target dialect.</summary>
    public RegexFlavor Flavor { get; }

    /// <summary>Gets the translated pattern source, without delimiters or flags.</summary>
    public string Pattern { get; }

    /// <summary>
    /// Gets the flags to compile the pattern with, such as <c>iu</c>. Always empty for JSON Schema
    /// and for every SQL flavor, whose options are embedded in <see cref="Pattern"/>.
    /// </summary>
    public string Flags { get; }

    /// <summary>
    /// Gets descriptions of every difference in which strings match (acceptance). Empty when the
    /// translation is exact. Differences that only change which substring is matched or what a group
    /// captures are listed in <see cref="MatchExtentWarnings"/> instead.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Gets descriptions of differences that change which substring is matched or what a group captures,
    /// but not whether a string matches: they matter when the translation is used to extract or replace
    /// text (<c>regexp_substr</c>, <c>REGEXP_REPLACE</c>, JavaScript's <c>exec</c> or <c>replace</c>),
    /// not when it is only used as a test. Never null; empty when every match covers the same substring
    /// and every group captures the same text as in .NET. The analysis is conservative: an entry means a
    /// difference is possible, not that it occurs for every input.
    /// </summary>
    /// <example>
    /// Pattern.FromRegex("(a|ab)").ToFlavor(RegexFlavor.PostgreSql).MatchExtentWarnings;
    /// // PostgreSQL prefers the longest match ("ab"), .NET the first alternative ("a").
    /// </example>
    public IReadOnlyList<string> MatchExtentWarnings { get; }

    /// <summary>
    /// Gets a value indicating whether every match covers the same substring and every group captures the
    /// same text as in .NET, for the strings both accept: true when <see cref="MatchExtentWarnings"/> is empty.
    /// </summary>
    public bool PreservesMatchExtent => MatchExtentWarnings.Count == 0;

    /// <summary>
    /// Gets the 1-based number of every .NET capture group in the converted regex, keyed by the group's
    /// name and by its .NET number written as a string. Numbers can differ because .NET numbers unnamed
    /// groups before named ones, while every other engine numbers groups left to right; PostgreSQL also
    /// has no named groups at all. A .NET group inside a lookaround is absent for PostgreSQL, where
    /// parentheses inside lookarounds never capture. The whole match (.NET group 0) is intentionally
    /// absent: it is always group 0 in every engine.
    /// </summary>
    /// <example>
    /// Pattern.FromRegex("(?&lt;year&gt;\d{4})-(\d{2})").ToFlavor(RegexFlavor.PostgreSql).GroupNumbers;
    /// // { ["year"] = 1, ["2"] = 1, ["1"] = 2 }
    /// </example>
    public IReadOnlyDictionary<string, int> GroupNumbers { get; }

    /// <summary>
    /// Gets a value indicating whether the translation accepts exactly the strings .NET accepts (no
    /// <see cref="Warnings"/>). See <see cref="PreservesMatchExtent"/> for the matched substring and captures.
    /// </summary>
    public bool IsExact => Warnings.Count == 0;

    /// <summary>Gets a value indicating whether <see cref="Flavor"/> is one of the SQL dialects.</summary>
    public bool IsSql => SqlDialect.IsSql(Flavor);

    /// <summary>
    /// Returns the translation as source code: a regex literal such as <c>/^\p{Nd}+$/u</c> for
    /// JavaScript, a quoted JSON string for JSON Schema, or a single-quoted SQL string literal for the
    /// SQL flavors (quotes doubled; backslashes doubled for MySQL and MariaDB, whose string literals
    /// process C escapes; PostgreSQL output assumes <c>standard_conforming_strings = on</c>).
    /// </summary>
    /// <returns>The literal.</returns>
    public string ToLiteral()
    {
        if (Flavor == RegexFlavor.JsonSchema)
        {
            return JsonString(Pattern);
        }

        if (IsSql)
        {
            return SqlDialect.Quote(Pattern, Flavor);
        }

        string body = Pattern.Length == 0 ? EmptyJavaScriptPattern : Pattern;
        return $"{Slash}{body}{Slash}{Flags}";
    }

    /// <summary>Returns <see cref="ToLiteral"/>.</summary>
    public override string ToString() => ToLiteral();

    private static string JsonString(string value)
    {
        StringBuilder sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            if (c == '"' || c == '\\')
            {
                sb.Append('\\').Append(c);
            }
            else if (c < FirstPrintable)
            {
                sb.Append($"\\u{((int)c).ToString("x4", CultureInfo.InvariantCulture)}");
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.Append('"').ToString();
    }
}
