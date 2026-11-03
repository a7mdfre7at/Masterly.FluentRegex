#nullable enable

using System;
using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Optimization;

/// <summary>Removes non-capturing groups that change neither grouping nor options.</summary>
internal static class GroupRewrites
{
    private const string NonCapturingOpen = "(?:";
    private const string MergingFollowers = "0123456789,}";
    private const string MergingEndings = "0123456789,{";

    /// <summary>Adds the unwrap rewrite for <paramref name="group"/>, if it is redundant.</summary>
    public static void Collect(GroupNode group, RegexNode? parent, string text, List<RegexRewrite> found)
    {
        bool plain = group.GroupKind == GroupKind.NonCapturing &&
                     string.Equals(group.OpenText, NonCapturingOpen, StringComparison.Ordinal);
        if (!plain || SyntaxFacts.HasInlineOptions(group.Body) || !IsRedundant(group, parent))
        {
            return;
        }

        string body = text.Substring(group.Body.Start, group.Body.Length);
        if (WouldMergeWithNeighbours(group, body, text))
        {
            return;
        }
        found.Add(new RegexRewrite(group.Start, group.End, body, $"Unwrapped redundant group '{group.Source}'"));
    }

    private static bool IsRedundant(GroupNode group, RegexNode? parent)
    {
        RegexNode body = group.Body;
        bool atomic = body is LiteralNode || body is CharClassNode || body is GroupNode;
        if (parent is QuantifierNode)
        {
            return atomic;
        }
        if (body is AlternationNode)
        {
            return parent == null || parent is AlternationNode || parent is GroupNode;
        }
        return atomic || body is SequenceNode || body is QuantifierNode || body is EmptyNode;
    }

    /// <summary>
    /// Guards against text that would fuse with its neighbours once the parentheses are gone,
    /// such as <c>\1(?:0)</c> becoming <c>\10</c> or <c>a(?:{)2}</c> becoming <c>a{2}</c>.
    /// </summary>
    private static bool WouldMergeWithNeighbours(GroupNode group, string body, string text)
    {
        char? before = group.Start > 0 ? text[group.Start - 1] : (char?)null;
        char? after = group.End < text.Length ? text[group.End] : (char?)null;
        if (body.Length == 0)
        {
            return before != null && after != null;
        }

        bool fusesAfter = after != null && MergingFollowers.IndexOf(after.Value) >= 0 &&
                          (MergingEndings.IndexOf(body[body.Length - 1]) >= 0 || EndsWithBackreference(group.Body));
        bool fusesBefore = before != null && MergingEndings.IndexOf(before.Value) >= 0 &&
                           MergingFollowers.IndexOf(body[0]) >= 0;
        return fusesAfter || fusesBefore;
    }

    private static bool EndsWithBackreference(RegexNode body)
    {
        IReadOnlyList<RegexNode> items = RegexWriter.ItemsOf(body);
        return items.Count > 0 && items[items.Count - 1] is BackreferenceNode;
    }
}
