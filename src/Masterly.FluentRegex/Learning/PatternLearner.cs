#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Learning;

/// <summary>
/// Infers an anchored pattern from examples that must match and examples that must not.
/// </summary>
internal static class PatternLearner
{
    private const string RejectParameter = "reject";

    private static readonly TokenGranularity[] AlignOrder = { TokenGranularity.Fine, TokenGranularity.Coarse };

    private static readonly TokenGranularity[] GroupOrder = { TokenGranularity.Coarse, TokenGranularity.Fine };

    public static Pattern Learn(IEnumerable<string> match, IEnumerable<string>? reject)
    {
        IReadOnlyList<string> positives = Normalize(match ?? throw new ArgumentNullException(nameof(match)), nameof(match));
        if (positives.Count == 0)
        {
            throw new ArgumentException("At least one example to match is required.", nameof(match));
        }

        IReadOnlyList<string> negatives = reject == null ? Array.Empty<string>() : Normalize(reject, nameof(reject));
        EnsureNoContradiction(positives, negatives);

        LearningContext context = new LearningContext(negatives);
        Pattern pattern = context.Anchor(LearnAlternatives(positives, context));
        EnsureConsistent(pattern, positives, negatives);
        return pattern;
    }

    /// <summary>
    /// Learns one alternative when all examples align; otherwise splits them by token signature and
    /// learns each group separately. Examples that cannot be generalized end up as exact literals.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<LearnedElement>> LearnAlternatives(IReadOnlyList<string> examples, LearningContext context)
    {
        foreach (TokenGranularity granularity in AlignOrder)
        {
            IReadOnlyList<LearnedElement>? aligned = AlignedLearner.TryLearn(examples, granularity, context);
            if (aligned != null)
            {
                return new[] { aligned };
            }
        }

        foreach (TokenGranularity granularity in GroupOrder)
        {
            IReadOnlyList<IReadOnlyList<string>> groups = GroupBySignature(examples, granularity);
            if (groups.Count > 1)
            {
                return groups.SelectMany(group => LearnAlternatives(group, context)).ToList();
            }
        }

        return examples.Select(e => (IReadOnlyList<LearnedElement>)new[] { LearnedElement.Literal(e) }).ToList();
    }

    private static IReadOnlyList<IReadOnlyList<string>> GroupBySignature(IReadOnlyList<string> examples, TokenGranularity granularity) =>
        examples
            .GroupBy(e => ExampleTokenizer.SignatureOf(ExampleTokenizer.Tokenize(e, granularity)), StringComparer.Ordinal)
            .Select(group => (IReadOnlyList<string>)group.ToList())
            .ToList();

    private static IReadOnlyList<string> Normalize(IEnumerable<string> examples, string parameterName)
    {
        List<string> list = new List<string>();
        foreach (string example in examples)
        {
            list.Add(example ?? throw new ArgumentNullException(parameterName, "The examples cannot contain null."));
        }

        return list.Distinct(StringComparer.Ordinal).OrderBy(e => e, StringComparer.Ordinal).ToList();
    }

    private static void EnsureNoContradiction(IReadOnlyList<string> positives, IReadOnlyList<string> negatives)
    {
        HashSet<string> positiveSet = new HashSet<string>(positives, StringComparer.Ordinal);
        string? contradiction = negatives.FirstOrDefault(positiveSet.Contains);
        if (contradiction != null)
        {
            throw new ArgumentException(
                $"The example \"{contradiction}\" is listed both as a match and as a reject, so no pattern can separate it.",
                RejectParameter);
        }
    }

    /// <summary>Re-checks the final pattern, so a learner bug can never return a wrong pattern silently.</summary>
    private static void EnsureConsistent(Pattern pattern, IReadOnlyList<string> positives, IReadOnlyList<string> negatives)
    {
        Regex regex = pattern.ToRegex();
        string? missed = positives.FirstOrDefault(p => !regex.IsMatch(p));
        if (missed != null)
        {
            throw new InvalidOperationException($"The learned pattern {pattern} does not match the example \"{missed}\".");
        }

        string? accepted = negatives.FirstOrDefault(regex.IsMatch);
        if (accepted != null)
        {
            throw new ArgumentException(
                $"No consistent pattern was found: the reject example \"{accepted}\" could not be separated from the match examples.",
                RejectParameter);
        }
    }
}
