#nullable enable
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Profiling;

/// <summary>Rejects constructs the profiling interpreter does not emulate, naming the construct.</summary>
internal static class UnsupportedConstructs
{
    private const string Prefix = "The backtracking profiler does not emulate";

    /// <summary>Throws <see cref="NotSupportedException"/> for the first construct that cannot be profiled.</summary>
    public static void Ensure(RegexTree tree, RegexOptions options)
    {
        if ((options & RegexOptions.RightToLeft) != 0)
        {
            throw new NotSupportedException($"{Prefix} RegexOptions.RightToLeft.");
        }

        foreach (RegexNode node in RegexNodeWalker.DescendantsAndSelf(tree.Root))
        {
            NotSupportedException? problem = Check(node);
            if (problem != null)
            {
                throw problem;
            }
        }
    }

    public static NotSupportedException Conditional(ConditionalNode node) =>
        new NotSupportedException($"{Prefix} the conditional '{node.Source}' at position {node.Start}.");

    public static NotSupportedException Balancing(GroupNode node) =>
        new NotSupportedException($"{Prefix} the balancing group '{node.OpenText}' at position {node.Start}.");

    private static NotSupportedException? Check(RegexNode node)
    {
        switch (node)
        {
            case ConditionalNode conditional:
                return Conditional(conditional);
            case GroupNode group when group.GroupKind == GroupKind.Balancing:
                return Balancing(group);
            case AnchorNode anchor when anchor.AnchorKind == AnchorKind.ContiguousMatch:
                return new NotSupportedException($@"{Prefix} the \G anchor at position {anchor.Start}.");
            default:
                return null;
        }
    }
}
