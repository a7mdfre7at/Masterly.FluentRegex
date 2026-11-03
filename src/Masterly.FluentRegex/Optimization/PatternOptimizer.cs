#nullable enable

using System;
using System.Collections.Generic;
using Masterly.FluentRegex.Automata;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Optimization;

/// <summary>
/// Applies order-preserving rewrites one at a time. Each rewrite is kept only when the rewritten
/// regex behaves exactly like the original on sample inputs and, when the pattern is regular,
/// language algebra proves both match the same strings.
/// </summary>
internal sealed class PatternOptimizer
{
    private const int MaxAppliedRewrites = 100;
    private const int MaxVerifications = 400;

    private readonly Pattern _original;
    private readonly MatchBehaviorChecker _behavior;
    private readonly EquivalenceProver? _prover;
    private readonly HashSet<string> _tried = new HashSet<string>(StringComparer.Ordinal);
    private int _verifications;

    private PatternOptimizer(Pattern original, RegexTree tree)
    {
        _original = original;
        _behavior = new MatchBehaviorChecker(original);
        _prover = TryCreateProver(tree);
    }

    /// <summary>Optimizes <paramref name="original"/>.</summary>
    public static OptimizationResult Optimize(Pattern original)
    {
        PatternOptimizer optimizer = new PatternOptimizer(original, original.ParseTree());
        return optimizer.Run();
    }

    private OptimizationResult Run()
    {
        string current = _original.ToString();
        _tried.Add(current);
        List<string> applied = new List<string>();
        while (applied.Count < MaxAppliedRewrites && _verifications < MaxVerifications)
        {
            RegexRewrite? accepted = FindSafeRewrite(current);
            if (accepted == null)
            {
                break;
            }
            current = accepted.Apply(current);
            applied.Add(accepted.Description);
        }

        Pattern optimized = applied.Count == 0
            ? _original
            : Pattern.FromRegex(current, _original.Options).WithTimeout(_original.Timeout);
        return new OptimizationResult(_original, optimized, applied, _prover != null);
    }

    private RegexRewrite? FindSafeRewrite(string current)
    {
        RegexTree tree = RegexParser.Parse(current, _original.Options);
        foreach (RegexRewrite rewrite in RewriteFinder.Find(tree))
        {
            string candidate = rewrite.Apply(current);
            if (!_tried.Add(candidate))
            {
                continue;
            }
            if (_verifications++ >= MaxVerifications)
            {
                return null;
            }
            if (IsSafe(candidate))
            {
                return rewrite;
            }
        }
        return null;
    }

    private bool IsSafe(string candidate)
    {
        if (!RegexParser.TryParse(candidate, _original.Options, out RegexTree candidateTree) ||
            !_behavior.BehavesLikeOriginal(candidate))
        {
            return false;
        }
        return _prover == null || IsProvenEquivalent(candidateTree);
    }

    private bool IsProvenEquivalent(RegexTree candidate)
    {
        try
        {
            return _prover != null && _prover.IsEquivalent(candidate);
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static EquivalenceProver? TryCreateProver(RegexTree tree)
    {
        try
        {
            return new EquivalenceProver(tree);
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
