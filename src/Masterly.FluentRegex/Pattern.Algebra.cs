#nullable enable

using System;
using Masterly.FluentRegex.Automata;

namespace Masterly.FluentRegex;

/// <summary>
/// Language algebra: prove relations between patterns and combine them.
/// </summary>
/// <remarks>
/// The language of a pattern is the set of strings it matches in full, as if it were wrapped in
/// <c>\A(?:...)\z</c>, honoring its options. An explicit <c>$</c> or <c>\Z</c> at the end keeps its
/// .NET meaning and also accepts a single trailing <c>\n</c>. Groups, lazy quantifiers and inline
/// options are supported, as are lookaheads at the very start of the pattern. Non-regular
/// constructs (backreferences, lookbehinds, other lookarounds, conditionals, balancing and atomic
/// groups, <c>\b</c>, <c>\B</c>, <c>\G</c>, multiline anchors, RightToLeft) throw
/// <see cref="NotSupportedException"/>.
/// </remarks>
public partial class Pattern
{
    /// <summary>
    /// Proves how the languages of two patterns relate and finds the shortest strings that show it.
    /// </summary>
    /// <param name="left">The first pattern.</param>
    /// <param name="right">The second pattern.</param>
    /// <returns>The relation, with shortest examples verified by the .NET engine.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NotSupportedException">A pattern uses a non-regular construct.</exception>
    /// <exception cref="InvalidOperationException">A pattern is too large to analyze.</exception>
    /// <example>
    /// PatternComparison result = Pattern.Compare(Pattern.FromRegex(@"^\d+$"), Pattern.FromRegex(@"\A\d+\z"));
    /// // result.Relation == PatternRelation.ProperSuperset, result.OnlyLeftExample == "0\n"
    /// </example>
    public static PatternComparison Compare(Pattern left, Pattern right)
    {
        Pattern checkedLeft = left ?? throw new ArgumentNullException(nameof(left));
        Pattern checkedRight = right ?? throw new ArgumentNullException(nameof(right));
        return LanguageAlgebra.Compare(checkedLeft.ParseTree(), checkedRight.ParseTree());
    }

    /// <summary>Returns true when both patterns match exactly the same strings.</summary>
    /// <param name="other">The pattern to compare with.</param>
    /// <returns>True when the languages are equal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public bool IsEquivalentTo(Pattern other) => Compare(this, CheckedOther(other)).Relation == PatternRelation.Equivalent;

    /// <summary>Returns true when every string this pattern matches is also matched by <paramref name="other"/>.</summary>
    /// <param name="other">The candidate superset.</param>
    /// <returns>True when this language is contained in the other.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public bool IsSubsetOf(Pattern other) => Compare(this, CheckedOther(other)).OnlyLeftExample == null;

    /// <summary>Returns true when at least one string is matched by both patterns.</summary>
    /// <param name="other">The pattern to compare with.</param>
    /// <returns>True when the languages intersect.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public bool Overlaps(Pattern other) => Compare(this, CheckedOther(other)).CommonExample != null;

    // Reports the caller's parameter name, rather than Compare's "right".
    private static Pattern CheckedOther(Pattern other) => other ?? throw new ArgumentNullException(nameof(other));

    /// <summary>Returns true when no string at all matches this pattern in full.</summary>
    /// <returns>True when the language is empty.</returns>
    public bool MatchesNothing() => ShortestMatch() == null;

    /// <summary>Returns the shortest string this pattern matches in full, or null when it matches nothing.</summary>
    /// <returns>The shortest member of the language, preferring readable characters.</returns>
    /// <example>
    /// Pattern.FromRegex(@"[a-z]{2,}\d").ShortestMatch(); // "aa0"
    /// </example>
    public string? ShortestMatch() => LanguageAlgebra.ShortestMatch(ParseTree());

    /// <summary>Returns a pattern that matches exactly the strings both patterns match.</summary>
    /// <param name="other">The other pattern.</param>
    /// <returns>An anchored pattern with this pattern's options and timeout.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    /// <example>
    /// Pattern.FromRegex(@"[a-z]+").Intersect(Pattern.FromRegex(@"\w{3}")).ToString(); // "^[a-z]{3}\z"
    /// </example>
    public Pattern Intersect(Pattern other) => Combine(other, ProductOperation.Intersect);

    /// <summary>Returns a pattern that matches the strings this pattern matches and <paramref name="other"/> rejects.</summary>
    /// <param name="other">The pattern whose strings are removed.</param>
    /// <returns>An anchored pattern with this pattern's options and timeout.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public Pattern Except(Pattern other) => Combine(other, ProductOperation.Except);

    /// <summary>Returns a pattern that matches the strings either pattern matches.</summary>
    /// <param name="other">The other pattern.</param>
    /// <returns>A pattern with this pattern's options and timeout.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public Pattern Union(Pattern other) => Combine(other, ProductOperation.Union);

    private Pattern Combine(Pattern other, ProductOperation operation)
    {
        Pattern checkedOther = other ?? throw new ArgumentNullException(nameof(other));
        string regex = LanguageAlgebra.Combine(ParseTree(), checkedOther.ParseTree(), operation);
        return FromRegex(regex, _options).WithTimeout(_timeout);
    }
}
