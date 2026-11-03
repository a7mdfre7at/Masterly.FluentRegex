using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>A pattern translated to a SQL regex dialect.</summary>
/// <param name="RegexText">The regex source in the dialect's syntax, options embedded inline.</param>
/// <param name="IsExact">True when the dialect accepts exactly the strings the .NET pattern accepts.</param>
/// <param name="Warnings">Behaviors that differ from .NET; empty when <paramref name="IsExact"/> is true.</param>
/// <param name="GroupNumbers">Capture group names (and numbers as text) mapped to their numbers in the dialect regex.</param>
/// <param name="MatchExtentWarnings">
/// Differences in which substring is matched or what a group captures; empty when they equal .NET's, which
/// <c>Group</c> and <c>Replace</c> require.
/// </param>
internal sealed record DialectRegex(
    string RegexText,
    bool IsExact,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<string, int> GroupNumbers,
    IReadOnlyList<string> MatchExtentWarnings)
{
    /// <summary>Gets a value indicating whether matched substrings and captures equal .NET's.</summary>
    public bool PreservesMatchExtent => MatchExtentWarnings.Count == 0;
}

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
/// The single seam between this package and the core library's SQL knowledge: <see cref="Pattern.ToFlavor"/>
/// for the regex dialects, <see cref="Pattern.ToLike"/> for LIKE with its proof and
/// <see cref="Pattern.GetLanguageFacts"/> for index hints. Results are memoized per pattern (patterns have
/// value equality), because the proofs run language algebra and EF Core may translate the same constant
/// pattern for many queries.
/// </summary>
internal static class CoreDialectAdapter
{
    private const int CacheCapacity = 512;
    private const string AnyCulture = "";

    /// <summary>
    /// One culture per case table .NET's regex engine uses for <see cref="RegexOptions.IgnoreCase"/>: invariant,
    /// Turkish and Azerbaijani (dotted and dotless i), and every other culture.
    /// </summary>
    private static readonly CultureInfo[] CaseTableCultures =
    {
        CultureInfo.InvariantCulture,
        CultureInfo.GetCultureInfo("tr-TR"),
        CultureInfo.GetCultureInfo("en-US")
    };

    private static readonly RegexFlavor[] ProbeFlavors = { RegexFlavor.PostgreSql, RegexFlavor.MySql, RegexFlavor.SqlServer };

    private static readonly ConcurrentDictionary<(Pattern Pattern, SqlDialect Dialect, string Culture), DialectRegex> RegexCache = new();
    private static readonly ConcurrentDictionary<(Pattern Pattern, LikeDialect Dialect, MatchScope Scope, LikeCollation Collation, string Culture), DialectLike> LikeCache = new();
    private static readonly ConcurrentDictionary<(Pattern Pattern, MatchScope Scope, string Culture), DialectFacts> FactsCache = new();
    private static readonly ConcurrentDictionary<Pattern, bool> CultureDependenceCache = new();

    /// <summary>
    /// Translates <paramref name="pattern"/> to the regex syntax of <paramref name="dialect"/> with the exact
    /// .NET meaning, or throws when the engine cannot express it.
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

        return Cached(
            RegexCache,
            (Pattern: pattern, Dialect: dialect, Culture: CultureOf(pattern)),
            key => Translate(key.Pattern, RegexFlavorOf(key.Dialect)));
    }

    /// <summary>
    /// Compiles <paramref name="pattern"/> to a <c>LIKE</c> expression for <paramref name="dialect"/> that accepts
    /// every string the pattern accepts, with a proof of whether it accepts exactly those strings.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="dialect">The target dialect.</param>
    /// <param name="substring">True for <c>IsMatch</c> semantics (a match anywhere), false for whole-string semantics.</param>
    /// <param name="caseInsensitiveCollation">True when the column's collation makes <c>LIKE</c> case-insensitive.</param>
    public static DialectLike ToDialectLike(Pattern pattern, SqlDialect dialect, bool substring, bool caseInsensitiveCollation)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        LikeCollation collation = caseInsensitiveCollation ? LikeCollation.CaseInsensitive : LikeCollation.Binary;
        return Cached(
            LikeCache,
            (Pattern: pattern, Dialect: LikeDialectOf(dialect), Scope: ScopeOf(substring), Collation: collation, Culture: CultureOf(pattern)),
            key => Compile(key.Pattern, key.Dialect, key.Scope, key.Collation));
    }

    /// <summary>
    /// Returns proven facts about the strings <paramref name="pattern"/> accepts.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="substring">True for <c>IsMatch</c> semantics (strings containing a match), false for whole-string semantics.</param>
    public static DialectFacts GetFacts(Pattern pattern, bool substring)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return Cached(
            FactsCache,
            (Pattern: pattern, Scope: ScopeOf(substring), Culture: CultureOf(pattern)),
            key => Facts(key.Pattern, key.Scope));
    }

    /// <summary>
    /// Throws when the SQL for <paramref name="pattern"/> would depend on the culture of the thread that translates
    /// it. EF Core compiles a query once and reuses its SQL on every thread, so a constant pattern must translate the
    /// same way under every culture: a case-insensitive <c>i</c> matches <c>I</c> in most cultures but U+0130 in
    /// Turkish.
    /// </summary>
    /// <exception cref="NotSupportedException">The translation differs between cultures.</exception>
    public static void RequireCultureIndependent(Pattern pattern)
    {
        if (TranslationDependsOnCulture(pattern))
        {
            throw new NotSupportedException(
                $"The case-insensitive pattern '{pattern}' matches differently in Turkish and Azerbaijani cultures (dotted " +
                "and dotless i), so its SQL would depend on the culture of the thread that first runs the query. Add " +
                "RegexOptions.CultureInvariant to the pattern, or hold it in a variable so that it is translated on every execution.");
        }
    }

    /// <summary>
    /// Gets a value indicating whether the SQL for <paramref name="pattern"/> differs between cultures, so that a
    /// cached translation must not be reused on a thread with another culture.
    /// </summary>
    public static bool TranslationDependsOnCulture(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return CultureDependenceCache.GetOrAdd(pattern, DependsOnCulture);
    }

    /// <summary>
    /// Compares the pattern's case-insensitive expansion under each case table. Every IgnoreCase expansion in the
    /// core library (regex flavors, LIKE and facts) uses the same case tables, so one flavor is a faithful probe.
    /// A pattern no flavor can express is treated as culture-dependent, because nothing proves otherwise.
    /// </summary>
    private static bool DependsOnCulture(Pattern pattern)
    {
        if (!IsCultureSensitive(pattern))
        {
            return false;
        }

        HashSet<string> translations = new HashSet<string>(StringComparer.Ordinal);
        foreach (CultureInfo culture in CaseTableCultures)
        {
            string? translation = ProbeUnder(culture, pattern);
            if (translation == null)
            {
                return true;
            }

            translations.Add(translation);
        }

        return translations.Count > 1;
    }

    private static string? ProbeUnder(CultureInfo culture, Pattern pattern)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            return FirstExpressibleTranslation(pattern);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static string? FirstExpressibleTranslation(Pattern pattern)
    {
        foreach (RegexFlavor flavor in ProbeFlavors)
        {
            try
            {
                return pattern.ToFlavor(flavor).Pattern;
            }
            catch (NotSupportedException)
            {
                // This flavor cannot express the pattern; the next one may.
            }
        }

        return null;
    }

    private static bool IsCultureSensitive(Pattern pattern) =>
        (pattern.Options & RegexOptions.IgnoreCase) != 0 && (pattern.Options & RegexOptions.CultureInvariant) == 0;

    /// <summary>The cache key part for the culture: culture-sensitive patterns translate per thread culture.</summary>
    private static string CultureOf(Pattern pattern) => IsCultureSensitive(pattern) ? CultureInfo.CurrentCulture.Name : AnyCulture;

    private static DialectRegex Translate(Pattern pattern, RegexFlavor flavor)
    {
        FlavorConversion conversion = pattern.ToFlavor(flavor);
        return new DialectRegex(
            conversion.Pattern, conversion.IsExact, conversion.Warnings, conversion.GroupNumbers, conversion.MatchExtentWarnings);
    }

    private static DialectLike Compile(Pattern pattern, LikeDialect dialect, MatchScope scope, LikeCollation collation)
    {
        LikeConversion conversion = pattern.ToLike(dialect, scope, collation);
        return new DialectLike(conversion.Expression, conversion.EscapeCharacter, conversion.IsExact, conversion.IsSuperset);
    }

    private static DialectFacts Facts(Pattern pattern, MatchScope scope)
    {
        LanguageFacts facts = pattern.GetLanguageFacts(scope);
        return new DialectFacts(facts.MinLength, facts.MaxLength, facts.RequiredPrefix, facts.RequiredSuffix, facts.IsExact);
    }

    private static RegexFlavor RegexFlavorOf(SqlDialect dialect) => dialect switch
    {
        SqlDialect.PostgreSql => RegexFlavor.PostgreSql,
        SqlDialect.MySql => RegexFlavor.MySql,
        SqlDialect.MariaDb => RegexFlavor.MariaDb,
        SqlDialect.SqlServer2025 => RegexFlavor.SqlServer,
        SqlDialect.Sqlite => RegexFlavor.Sqlite,
        _ => throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "The dialect has no regex flavor.")
    };

    private static LikeDialect LikeDialectOf(SqlDialect dialect) => dialect switch
    {
        SqlDialect.PostgreSql => LikeDialect.PostgreSql,
        SqlDialect.MySql or SqlDialect.MariaDb => LikeDialect.MySql,
        SqlDialect.SqlServer or SqlDialect.SqlServer2025 => LikeDialect.SqlServer,
        SqlDialect.Sqlite => LikeDialect.Sqlite,
        _ => throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "Unknown SQL dialect.")
    };

    private static MatchScope ScopeOf(bool substring) => substring ? MatchScope.Substring : MatchScope.FullString;

    /// <summary>Memoizes <paramref name="compute"/>; the cache is cleared, not evicted, when it fills up.</summary>
    private static TValue Cached<TKey, TValue>(ConcurrentDictionary<TKey, TValue> cache, TKey key, Func<TKey, TValue> compute)
        where TKey : notnull
    {
        if (cache.TryGetValue(key, out TValue? cached))
        {
            return cached;
        }

        TValue value = compute(key);
        if (cache.Count >= CacheCapacity)
        {
            cache.Clear();
        }

        cache[key] = value;
        return value;
    }
}
