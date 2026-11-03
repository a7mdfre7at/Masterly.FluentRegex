#nullable enable
using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Profiling;

/// <summary>Enumerates a syntax tree without recursion, so deeply nested patterns cannot overflow the stack.</summary>
internal static class RegexNodeWalker
{
    /// <summary>Returns <paramref name="root"/> and every node below it, parents before children.</summary>
    public static IEnumerable<RegexNode> DescendantsAndSelf(RegexNode root)
    {
        Stack<RegexNode> pending = new Stack<RegexNode>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            RegexNode node = pending.Pop();
            yield return node;
            foreach (RegexNode child in node.Children)
            {
                if (child != null)
                {
                    pending.Push(child);
                }
            }
        }
    }
}
