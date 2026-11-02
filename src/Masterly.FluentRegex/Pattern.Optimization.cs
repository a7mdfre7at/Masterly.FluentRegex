#nullable enable

using Masterly.FluentRegex.Optimization;

namespace Masterly.FluentRegex;

/// <summary>
/// Optimization: semantics-preserving rewrites that make a pattern shorter and faster.
/// </summary>
public partial class Pattern
{
    /// <summary>
    /// Rewrites the pattern into a simpler one with identical .NET match behavior: the same
    /// leftmost-first matches, the same groups and captures, and the same options. Branch order is
    /// never changed.
    /// </summary>
    /// <remarks>
    /// Rewrites include merging single-character branches into a class (<c>a|b|c</c> to <c>[abc]</c>),
    /// factoring common prefixes of adjacent branches (<c>cat|car|cab</c> to <c>ca[trb]</c>), removing
    /// later duplicate branches, normalizing quantifiers (<c>x{0,1}</c> to <c>x?</c>), merging runs of
    /// the same atom (<c>\d\d\d</c> to <c>\d{3}</c>) and unwrapping redundant non-capturing groups.
    /// Each rewrite is kept only if the rewritten regex produces identical matches and groups on
    /// generated sample inputs and, for regular patterns, language algebra proves equivalence.
    /// </remarks>
    /// <returns>The optimized pattern and the list of applied rewrites.</returns>
    /// <example>
    /// OptimizationResult result = Pattern.FromRegex("cat|car|cab").Optimize();
    /// // result.Optimized.ToString() == "ca[trb]", result.VerifiedEquivalent == true
    /// </example>
    public OptimizationResult Optimize() => PatternOptimizer.Optimize(this);
}
