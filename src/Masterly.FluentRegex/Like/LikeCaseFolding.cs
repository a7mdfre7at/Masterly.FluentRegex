#nullable enable
using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Like;

/// <summary>
/// What a case-insensitive collation is guaranteed to fold. Every case-insensitive collation of SQL
/// Server, MySQL, MariaDB and SQLite's LIKE folds an ASCII letter with its ASCII partner, except that
/// Turkish and Azerbaijani collations do not fold <c>i</c> with <c>I</c>. Nothing beyond that is
/// guaranteed: .NET also folds <c>k</c> with U+212A KELVIN SIGN and <c>i</c> with U+0130, which SQL
/// Server's <c>Latin1_General_CI_AS</c>, MySQL's <c>utf8mb4_general_ci</c> and SQLite do not.
/// </summary>
internal static class LikeCaseFolding
{
    private const char DottedI = 'i';
    private const char UpperI = 'I';
    private const int CaseOffset = 'a' - 'A';

    /// <summary>The characters every case-insensitive collation matches against <paramref name="c"/>.</summary>
    public static CharSet Guaranteed(char c)
    {
        CharSet self = CharSet.Single(c);
        char? partner = AsciiPartner(c);
        return partner == null ? self : self.Union(CharSet.Single(partner.Value));
    }

    /// <summary>The characters every case-insensitive collation matches against some member of <paramref name="members"/>.</summary>
    public static CharSet Guaranteed(CharSet members)
    {
        CharSet result = members;
        foreach (KeyValuePair<char, char> range in members.Ranges())
        {
            result = result.Union(PartnersIn(range));
        }

        return result;
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="c"/> written as LIKE text under a case-insensitive
    /// collation is guaranteed to match every character of <paramref name="variants"/> (its .NET case
    /// variants), so the text is a superset of the case-insensitive regex literal.
    /// </summary>
    public static bool CoversVariants(char c, CharSet variants) => variants.IsSubsetOf(Guaranteed(c));

    private static CharSet PartnersIn(KeyValuePair<char, char> range)
    {
        CharSet partners = CharSet.Empty;
        for (int c = range.Key; c <= range.Value && c <= 'z'; c++)
        {
            char? partner = AsciiPartner((char)c);
            if (partner != null)
            {
                partners = partners.Union(CharSet.Single(partner.Value));
            }
        }

        return partners;
    }

    private static char? AsciiPartner(char c)
    {
        if (c == DottedI || c == UpperI)
        {
            return null; // Turkish and Azerbaijani collations keep i and I apart
        }

        if (c >= 'a' && c <= 'z')
        {
            return (char)(c - CaseOffset);
        }

        return c >= 'A' && c <= 'Z' ? (char)(c + CaseOffset) : (char?)null;
    }
}
