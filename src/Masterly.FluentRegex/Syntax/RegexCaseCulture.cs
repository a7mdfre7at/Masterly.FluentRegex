#nullable enable
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Syntax;

/// <summary>
/// The culture-dependent part of .NET's case-insensitive matching. A <see cref="Regex"/> built with
/// <see cref="RegexOptions.IgnoreCase"/> captures <see cref="CultureInfo.CurrentCulture"/> when it is
/// constructed, unless <see cref="RegexOptions.CultureInvariant"/> is set. Since .NET 7 the culture
/// only selects one of three case-equivalence tables (measured on net8.0 and net10.0 across many
/// cultures): the invariant table, the Turkish and Azerbaijani table (<c>i</c> pairs with U+0130 and
/// <c>I</c> with U+0131, and <c>i</c> no longer pairs with <c>I</c>), and the table every other
/// culture uses (U+0130 additionally pairs with <c>i</c> and <c>I</c>). Only those four letters differ.
/// </summary>
internal static class RegexCaseCulture
{
    private const string InvariantKey = "inv";
    private const string TurkishKey = "tr";
    private const string DefaultKey = "def";
    private const string Turkish = "tr";
    private const string Azerbaijani = "az";

    /// <summary>The only characters whose case equivalents depend on the culture.</summary>
    public const string CultureSensitiveLetters = "iIİı";

    /// <summary>
    /// Gets the case-equivalence table a regex created now with <paramref name="options"/> would use, as a
    /// cache key: the same key means the same case-insensitive matching.
    /// </summary>
    public static string Key(RegexOptions options)
    {
        if ((options & RegexOptions.IgnoreCase) == 0 || (options & RegexOptions.CultureInvariant) != 0)
        {
            return InvariantKey;
        }

#if FLUENTREGEX_ANALYZER
        // Analyzers may not read the current culture (RS1035); inside the compiler it never changes.
        return DefaultKey;
#else
        CultureInfo culture = CultureInfo.CurrentCulture;
        if (culture.Name.Length == 0)
        {
            return InvariantKey;
        }

        string language = culture.TwoLetterISOLanguageName;
        bool turkish = string.Equals(language, Turkish, StringComparison.Ordinal) ||
                       string.Equals(language, Azerbaijani, StringComparison.Ordinal);
        return turkish ? TurkishKey : DefaultKey;
#endif
    }
}
