#nullable enable

using System.Collections.Generic;
using System.Text;

namespace Masterly.FluentRegex;

/// <summary>
/// The outcome of <see cref="Pattern.Optimize"/>: the rewritten pattern, the rewrites that were
/// applied, and whether equivalence was proven by language algebra.
/// </summary>
public sealed class OptimizationResult
{
    internal OptimizationResult(Pattern original, Pattern optimized, IReadOnlyList<string> appliedRewrites, bool verifiedEquivalent)
    {
        Original = original;
        Optimized = optimized;
        AppliedRewrites = appliedRewrites;
        VerifiedEquivalent = verifiedEquivalent;
    }

    /// <summary>Gets the pattern that was optimized.</summary>
    public Pattern Original { get; }

    /// <summary>Gets the optimized pattern, or <see cref="Original"/> when no rewrite applied.</summary>
    public Pattern Optimized { get; }

    /// <summary>Gets a human-readable description of each applied rewrite, in order.</summary>
    public IReadOnlyList<string> AppliedRewrites { get; }

    /// <summary>
    /// Gets a value indicating whether language algebra proved the optimized pattern matches exactly
    /// the same strings as the original. False when the pattern uses constructs outside the regular
    /// subset; the rewrites were then checked on sample inputs only.
    /// </summary>
    public bool VerifiedEquivalent { get; }

    /// <summary>Gets a value indicating whether any rewrite was applied.</summary>
    public bool IsChanged => AppliedRewrites.Count > 0;

    /// <summary>Describes the optimization, listing every applied rewrite.</summary>
    /// <returns>A human-readable summary.</returns>
    public override string ToString()
    {
        string verification = VerifiedEquivalent ? "proven equivalent" : "checked on samples, not proven";
        if (!IsChanged)
        {
            return $"No rewrites applied to '{Original}'.";
        }

        StringBuilder text = new StringBuilder($"Optimized '{Original}' to '{Optimized}' ({verification}):");
        foreach (string rewrite in AppliedRewrites)
        {
            text.AppendLine().Append($"  - {rewrite}");
        }
        return text.ToString();
    }
}
