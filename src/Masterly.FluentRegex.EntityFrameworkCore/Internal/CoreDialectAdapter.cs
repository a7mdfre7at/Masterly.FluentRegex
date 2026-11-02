// TEMPORARY SHIM — replaced by the core API at merge time.
//
// The core library is concurrently gaining SQL regex flavors (Pattern.ToFlavor(RegexFlavor.PostgreSql | MySql |
// MariaDb | SqlServer | Sqlite)), Pattern.GetLanguageFacts(MatchScope) and Pattern.ToLike(LikeDialect, MatchScope,
// LikeCollation). Every use of those APIs in this package goes through this adapter so that the bodies below can be
// swapped for the real core calls without touching the translator, the conventions or SqlRegex.
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>A pattern translated to a SQL regex dialect.</summary>
/// <param name="RegexText">The regex source in the dialect's syntax, options embedded inline.</param>
/// <param name="IsExact">True when the dialect accepts exactly the strings the .NET pattern accepts.</param>
/// <param name="Warnings">Behaviors that differ from .NET; empty when <paramref name="IsExact"/> is true.</param>
/// <param name="GroupNumbers">Capture group names (and numbers as text) mapped to their numbers in the dialect regex.</param>
internal sealed record DialectRegex(string RegexText, bool IsExact, IReadOnlyList<string> Warnings, IReadOnlyDictionary<string, int> GroupNumbers);

/// <summary>A pattern translated to a SQL <c>LIKE</c> expression.</summary>
/// <param name="Expression">The <c>LIKE</c> pattern, without quotes.</param>
/// <param name="EscapeCharacter">The character declared with <c>ESCAPE</c>, or null when none is needed.</param>
/// <param name="IsExact">True when <c>LIKE</c> accepts exactly the strings the pattern accepts.</param>
/// <param name="IsSuperset">True when <c>LIKE</c> accepts every string the pattern accepts (and possibly more).</param>
internal sealed record DialectLike(string Expression, char? EscapeCharacter, bool IsExact, bool IsSuperset);

/// <summary>Proven facts about every string a pattern accepts, in UTF-16 code units.</summary>
/// <param name="MinLength">The shortest accepted string.</param>
/// <param name="MaxLength">The longest accepted string, or null when unbounded.</param>
/// <param name="RequiredPrefix">Text every accepted string starts with (empty when none is known).</param>
/// <param name="RequiredSuffix">Text every accepted string ends with (empty when none is known).</param>
/// <param name="IsExact">True when the facts describe the language exactly rather than a superset.</param>
internal sealed record DialectFacts(int MinLength, int? MaxLength, string RequiredPrefix, string RequiredSuffix, bool IsExact);

/// <summary>
/// The single seam between this package and the core library's SQL dialect knowledge.
/// </summary>
internal static class CoreDialectAdapter
{
    private const string PostgreSqlEndOfString = @"\Z";
    private const string MariaDbCaseSensitive = "(?-i)";

    /// <summary>
    /// Test hook: when set, <see cref="GetFacts"/> returns its result instead of the real facts.
    /// The hook must only describe facts that are true for the pattern it is given.
    /// </summary>
    internal static Func<Pattern, bool, DialectFacts>? FactsOverride { get; set; }

    /// <summary>
    /// Translates <paramref name="pattern"/> to the regex syntax of <paramref name="dialect"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// <paramref name="dialect"/> is <see cref="SqlDialect.SqlServer"/>, which has no regex support, or the
    /// pattern uses an option or construct the dialect cannot express.
    /// </exception>
    public static DialectRegex ToDialectRegex(Pattern pattern, SqlDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (dialect == SqlDialect.SqlServer)
        {
            throw new NotSupportedException(
                "SQL Server before 2025 has no regular expression support. Use the LIKE translation instead.");
        }

        // TEMPORARY SHIM: the .NET regex text with inline options. The real core API translates each construct.
        string text = InlineOptions(pattern) + pattern.ToString();
        if (dialect == SqlDialect.MariaDb && (pattern.Options & RegexOptions.IgnoreCase) == 0)
        {
            // MariaDB's REGEXP follows the (usually case-insensitive) column collation and has no match-type argument;
            // the inline (?-i) restores .NET's default (verified on MariaDB 11.4). The real translator must keep this.
            text = MariaDbCaseSensitive + text;
        }

        if (dialect == SqlDialect.PostgreSql && text.EndsWith(WholeStringPattern.Suffix, StringComparison.Ordinal))
        {
            // PostgreSQL spells the end-of-string anchor \Z; only the whole-string wrapper is fixed up here so that
            // CHECK constraints created during container tests are valid before the real translator lands.
            text = text.Substring(0, text.Length - WholeStringPattern.Suffix.Length) + ")" + PostgreSqlEndOfString;
        }

        return new DialectRegex(text, dialect == SqlDialect.Sqlite, Array.Empty<string>(), GroupNumbers(pattern));
    }

    /// <summary>
    /// Translates <paramref name="pattern"/> to a <c>LIKE</c> expression for <paramref name="dialect"/>.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="dialect">The target dialect.</param>
    /// <param name="substring">True for <c>IsMatch</c> semantics (a match anywhere), false for whole-string semantics.</param>
    /// <param name="caseInsensitiveCollation">True when the column's collation makes <c>LIKE</c> case-insensitive.</param>
    public static DialectLike ToDialectLike(Pattern pattern, SqlDialect dialect, bool substring, bool caseInsensitiveCollation)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        // TEMPORARY SHIM: "%" accepts every non-null string, which is a superset of any pattern and never exact.
        return new DialectLike("%", null, IsExact: false, IsSuperset: true);
    }

    /// <summary>
    /// Returns proven facts about the strings <paramref name="pattern"/> accepts.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="substring">True for <c>IsMatch</c> semantics (strings containing a match), false for whole-string semantics.</param>
    public static DialectFacts GetFacts(Pattern pattern, bool substring)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        Func<Pattern, bool, DialectFacts>? factsOverride = FactsOverride;
        if (factsOverride != null)
        {
            return factsOverride(pattern, substring);
        }

        // TEMPORARY SHIM: no facts are known, so no index hint is ever emitted.
        return new DialectFacts(0, null, string.Empty, string.Empty, false);
    }

    private static string InlineOptions(Pattern pattern)
    {
        RegexOptions options = pattern.Options;
        RejectOption(options, RegexOptions.CultureInvariant, pattern);
        RejectOption(options, RegexOptions.RightToLeft, pattern);
        RejectOption(options, RegexOptions.ECMAScript, pattern);
        RejectOption(options, RegexOptions.NonBacktracking, pattern);

        StringBuilder flags = new StringBuilder();
        AppendFlag(flags, options, RegexOptions.IgnoreCase, 'i');
        AppendFlag(flags, options, RegexOptions.Multiline, 'm');
        AppendFlag(flags, options, RegexOptions.Singleline, 's');
        AppendFlag(flags, options, RegexOptions.IgnorePatternWhitespace, 'x');
        AppendFlag(flags, options, RegexOptions.ExplicitCapture, 'n');
        return flags.Length == 0 ? string.Empty : $"(?{flags})";
    }

    private static void AppendFlag(StringBuilder flags, RegexOptions options, RegexOptions flag, char letter)
    {
        if ((options & flag) != 0)
        {
            flags.Append(letter);
        }
    }

    private static void RejectOption(RegexOptions options, RegexOptions rejected, Pattern pattern)
    {
        if ((options & rejected) != 0)
        {
            throw new NotSupportedException(
                $"RegexOptions.{rejected} cannot be expressed in a SQL regex dialect (pattern '{pattern}').");
        }
    }

    private static IReadOnlyDictionary<string, int> GroupNumbers(Pattern pattern)
    {
        Regex regex = pattern.ToRegex();
        Dictionary<string, int> numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (int number in regex.GetGroupNumbers())
        {
            numbers[regex.GroupNameFromNumber(number)] = number;
        }

        return numbers;
    }
}
