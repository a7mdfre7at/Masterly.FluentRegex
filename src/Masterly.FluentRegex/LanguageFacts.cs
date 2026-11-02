#nullable enable
using System.Globalization;

namespace Masterly.FluentRegex;

/// <summary>
/// Facts that hold for every string a pattern accepts, computed from its automaton by
/// <see cref="Pattern.GetLanguageFacts"/>. They are sound by construction: a query planner, an index
/// chooser or a LIKE translation can rely on them without ever rejecting a string the pattern accepts.
/// </summary>
public sealed class LanguageFacts
{
    private const string Unbounded = "unbounded";

    internal LanguageFacts(
        MatchScope scope,
        int minLength,
        int? maxLength,
        string requiredPrefix,
        string requiredSuffix,
        bool acceptsEmpty,
        bool isEmptyLanguage,
        bool isExact)
    {
        Scope = scope;
        MinLength = minLength;
        MaxLength = maxLength;
        RequiredPrefix = WholeCodePointPrefix(requiredPrefix);
        RequiredSuffix = WholeCodePointSuffix(requiredSuffix);
        AcceptsEmpty = acceptsEmpty;
        IsEmptyLanguage = isEmptyLanguage;
        IsExact = isExact;
    }

    /// <summary>Gets the set of strings these facts describe.</summary>
    public MatchScope Scope { get; }

    /// <summary>
    /// Gets the length of the shortest accepted string (0 when the empty string is accepted, and 0 when
    /// <see cref="IsEmptyLanguage"/> is true). When <see cref="IsExact"/> is false this is a lower bound.
    /// </summary>
    public int MinLength { get; }

    /// <summary>Gets the length of the longest accepted string, or null when there is no upper bound.</summary>
    public int? MaxLength { get; }

    /// <summary>
    /// Gets the text every accepted string starts with; empty when nothing is required. Under
    /// <see cref="System.Text.RegularExpressions.RegexOptions.IgnoreCase"/> only characters without case
    /// variants can be required. The prefix ends at a whole code point: it never ends with half of a
    /// surrogate pair (or contains a lone surrogate), so it stays a prefix for databases that store UTF-8
    /// or compare code points. When <see cref="IsExact"/> is false the real required prefix may be longer.
    /// </summary>
    public string RequiredPrefix { get; }

    /// <summary>
    /// Gets the text every accepted string ends with; empty when nothing is required. A pattern ending
    /// in <c>$</c> or <c>\Z</c> requires no suffix, because it also accepts a trailing newline; use
    /// <c>\z</c> to require one. Like <see cref="RequiredPrefix"/>, it starts at a whole code point.
    /// </summary>
    public string RequiredSuffix { get; }

    /// <summary>Gets a value indicating whether the empty string is accepted.</summary>
    public bool AcceptsEmpty { get; }

    /// <summary>Gets a value indicating whether no string at all is accepted.</summary>
    public bool IsEmptyLanguage { get; }

    /// <summary>
    /// Gets a value indicating whether the facts were computed from the automaton (true) or, for a
    /// pattern outside the regular subset (backreferences, lookarounds, conditionals, <c>\b</c>, ...),
    /// approximated from the syntax tree (false). Approximate facts are still sound: the prefix and
    /// suffix may be shorter than the truth, <see cref="MinLength"/> lower and <see cref="MaxLength"/> null.
    /// </summary>
    public bool IsExact { get; }

    /// <summary>
    /// Describes the facts, for example
    /// <c>FullString: length 13..13, prefix "INV-", suffix "", accepts empty: false (exact)</c>.
    /// </summary>
    /// <returns>A one-line description.</returns>
    public override string ToString()
    {
        string exactness = IsExact ? "exact" : "approximate";
        if (IsEmptyLanguage)
        {
            return $"{Scope}: matches nothing ({exactness})";
        }

        string max = MaxLength?.ToString(CultureInfo.InvariantCulture) ?? Unbounded;
        string min = MinLength.ToString(CultureInfo.InvariantCulture);
        string accepts = AcceptsEmpty ? "true" : "false";
        return $"{Scope}: length {min}..{max}, prefix {PatternComparison.Quote(RequiredPrefix)}, " +
               $"suffix {PatternComparison.Quote(RequiredSuffix)}, accepts empty: {accepts} ({exactness})";
    }

    /// <summary>Cuts <paramref name="text"/> before its first lone surrogate (including a final high surrogate).</summary>
    internal static string WholeCodePointPrefix(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            if (char.IsSurrogatePair(text, i))
            {
                i += 2;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return text.Substring(0, i);
            }
            else
            {
                i++;
            }
        }

        return text;
    }

    /// <summary>Keeps <paramref name="text"/> after its last lone surrogate (including a first low surrogate).</summary>
    internal static string WholeCodePointSuffix(string text)
    {
        int i = text.Length - 1;
        while (i >= 0)
        {
            if (i > 0 && char.IsSurrogatePair(text[i - 1], text[i]))
            {
                i -= 2;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return text.Substring(i + 1);
            }
            else
            {
                i--;
            }
        }

        return text;
    }
}
