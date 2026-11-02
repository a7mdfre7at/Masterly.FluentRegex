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
        RequiredPrefix = requiredPrefix;
        RequiredSuffix = requiredSuffix;
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
    /// variants can be required. When <see cref="IsExact"/> is false the real required prefix may be longer.
    /// </summary>
    public string RequiredPrefix { get; }

    /// <summary>
    /// Gets the text every accepted string ends with; empty when nothing is required. A pattern ending
    /// in <c>$</c> or <c>\Z</c> requires no suffix, because it also accepts a trailing newline; use
    /// <c>\z</c> to require one.
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
}
