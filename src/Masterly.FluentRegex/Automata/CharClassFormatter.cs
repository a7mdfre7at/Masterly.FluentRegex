#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Writes the shortest regex text that matches exactly a given character set under given
/// options. Every candidate is checked against the real engine before it is used.
/// </summary>
internal sealed class CharClassFormatter
{
    private const int MaxRangesInCandidate = 64;
    private const char FirstPrintable = ' ';
    private const char LastPrintable = '~';
    private const string ClassMetaCharacters = "\\]^-[";
    private const string MetaCharacters = "\\*+?|{}[]()^$.# ";
    private const string CaseSensitivePrefix = "(?-i:";
    private const string UpperCaseLetters = @"[\p{Lu}\p{Lt}]";

    private static readonly string[] Shorthands = { ".", @"[\s\S]", @"\d", @"\D", @"\w", @"\W", @"\s", @"\S" };
    private static readonly string[] ClassShorthands = { @"\d", @"\w", @"\s", @"\p{L}" };

    private readonly RegexOptions _options;
    private readonly Dictionary<CharSet, string> _cache = new Dictionary<CharSet, string>(CharSetComparer.Instance);

    public CharClassFormatter(RegexOptions options)
    {
        _options = options;
    }

    /// <summary>Returns regex text for exactly <paramref name="set"/>.</summary>
    public string Format(CharSet set)
    {
        if (_cache.TryGetValue(set, out string? cached))
        {
            return cached;
        }

        string result = Best(set, _options)
                        ?? CaseSensitive(set)
                        ?? throw new InvalidOperationException("No regex text represents the character set exactly.");
        _cache[set] = result;
        return result;
    }

    /// <summary>Escapes a character for use outside a character class.</summary>
    public static string EscapeLiteral(char c)
    {
        return MetaCharacters.IndexOf(c) >= 0 ? $"\\{c}" : EscapeCommon(c);
    }

    private static string? Best(CharSet set, RegexOptions options)
    {
        List<string> candidates = Candidates(set, options);
        candidates.Sort((a, b) => a.Length.CompareTo(b.Length));
        foreach (string candidate in candidates)
        {
            if (IsExact(candidate, set, options))
            {
                return candidate;
            }
        }

        string? full = FullBracket(set);
        return full != null && IsExact(full, set, options) ? full : null;
    }

    /// <summary>
    /// Under IgnoreCase a set that is not closed under case folding can only be written inside a
    /// case-sensitive group, such as <c>(?-i:[a-z])</c>.
    /// </summary>
    private string? CaseSensitive(CharSet set)
    {
        if ((_options & RegexOptions.IgnoreCase) == 0)
        {
            return null;
        }

        RegexOptions sensitive = _options & ~RegexOptions.IgnoreCase;
        string? inner = Best(set, sensitive) ?? FullBracket(set);
        string wrapped = $"{CaseSensitivePrefix}{inner})";
        return inner != null && IsExact(wrapped, set, _options) ? wrapped : null;
    }

    private static bool IsExact(string candidate, CharSet set, RegexOptions options) =>
        CharSet.ForSource(candidate, options).SetEquals(set);

    private static string? FullBracket(CharSet set)
    {
        string? ranges = RangesText(set, int.MaxValue);
        return string.IsNullOrEmpty(ranges) ? null : $"[{ranges}]";
    }

    private static List<string> Candidates(CharSet set, RegexOptions options)
    {
        List<string> candidates = new List<string>();
        if (set.Count == 1)
        {
            candidates.Add(EscapeLiteral(ReadableChars.Pick(set)));
        }
        candidates.AddRange(Shorthands);

        CharSet complement = set.Complement();
        AddBracket(candidates, string.Empty, string.Empty, set);
        AddBracket(candidates, "^", string.Empty, complement);
        foreach (string shorthand in ClassShorthands)
        {
            CharSet members = CharSet.ForSource(shorthand, options);
            AddShorthandCandidates(candidates, shorthand, members, set, complement);
        }
        if ((options & RegexOptions.IgnoreCase) != 0)
        {
            AddCaseFoldedCandidates(candidates, set);
        }
        return candidates;
    }

    /// <summary>
    /// Under IgnoreCase, <c>[a-z]</c> already matches the upper case letters, so a set closed under
    /// case folding is best written with its non-upper-case members only.
    /// </summary>
    private static void AddCaseFoldedCandidates(List<string> candidates, CharSet set)
    {
        CharSet folded = set.Except(CharSet.ForSource(UpperCaseLetters, RegexOptions.None));
        if (folded.IsEmpty)
        {
            return;
        }
        if (folded.Count == 1)
        {
            candidates.Add(EscapeLiteral(ReadableChars.Pick(folded)));
        }
        AddBracket(candidates, string.Empty, string.Empty, folded);
    }

    private static void AddShorthandCandidates(List<string> candidates, string shorthand, CharSet members, CharSet set, CharSet complement)
    {
        if (members.IsEmpty)
        {
            return;
        }
        if (members.IsSubsetOf(set))
        {
            AddBracket(candidates, string.Empty, shorthand, set.Except(members));
        }
        if (members.IsSubsetOf(complement))
        {
            AddBracket(candidates, "^", shorthand, complement.Except(members));
        }
        if (set.IsSubsetOf(members))
        {
            AddSubtraction(candidates, shorthand, members.Except(set));
        }
    }

    private static void AddBracket(List<string> candidates, string negation, string prefix, CharSet rest)
    {
        string? ranges = RangesText(rest, MaxRangesInCandidate);
        if (ranges != null && (prefix.Length > 0 || ranges.Length > 0))
        {
            candidates.Add($"[{negation}{prefix}{ranges}]");
        }
    }

    private static void AddSubtraction(List<string> candidates, string shorthand, CharSet removed)
    {
        string? ranges = RangesText(removed, MaxRangesInCandidate);
        if (ranges != null && ranges.Length > 0)
        {
            candidates.Add($"[{shorthand}-[{ranges}]]");
        }
    }

    private static string? RangesText(CharSet set, int maxRanges)
    {
        StringBuilder builder = new StringBuilder();
        int count = 0;
        foreach (KeyValuePair<char, char> range in set.Ranges())
        {
            if (++count > maxRanges)
            {
                return null;
            }
            builder.Append(EscapeInClass(range.Key));
            if (range.Value == range.Key + 1)
            {
                builder.Append(EscapeInClass(range.Value));
            }
            else if (range.Value > range.Key)
            {
                builder.Append('-').Append(EscapeInClass(range.Value));
            }
        }
        return builder.ToString();
    }

    /// <summary>Escapes a character for use inside a character class.</summary>
    public static string EscapeInClass(char c)
    {
        return ClassMetaCharacters.IndexOf(c) >= 0 ? $"\\{c}" : EscapeCommon(c);
    }

    private static string EscapeCommon(char c)
    {
        switch (c)
        {
            case '\t':
                return @"\t";
            case '\n':
                return @"\n";
            case '\r':
                return @"\r";
            case '\f':
                return @"\f";
            case '\v':
                return @"\v";
        }
        if (c >= FirstPrintable && c <= LastPrintable)
        {
            return c.ToString();
        }
        return $"\\u{((int)c).ToString("X4", CultureInfo.InvariantCulture)}";
    }
}
