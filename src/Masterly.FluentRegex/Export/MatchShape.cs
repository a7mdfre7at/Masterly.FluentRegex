#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Export;

/// <summary>
/// Syntactic facts about how a pattern can match, used to decide whether an engine with different
/// match preferences can pick a different substring or capture. Every fact over-approximates the
/// possible matches (zero-width assertions are treated as always true, a backreference as any text),
/// so conclusions drawn from them stay sound.
/// </summary>
internal static class MatchShape
{
    /// <summary>
    /// Gets the fewest characters a match of <paramref name="node"/> can consume, counting every
    /// zero-width construct (anchors, <c>\b</c>, lookarounds) as 0, a backreference as 0 and each repeat
    /// at its minimum. Zero means the node can produce an empty match somewhere in some input.
    /// </summary>
    public static int MinWidth(RegexNode node) => (int)Math.Min(int.MaxValue, Width(node));

    private static long Width(RegexNode node)
    {
        switch (node)
        {
            case LiteralNode literal:
                return char.IsLowSurrogate(literal.Value) ? 0 : 1; // a surrogate pair counts as one character
            case CharClassNode _:
                return 1;
            case SequenceNode sequence:
                return Math.Min(int.MaxValue, sequence.Items.Sum(Width));
            case AlternationNode alternation:
                return alternation.Branches.Min(Width);
            case GroupNode group:
                return group.IsLookaround ? 0 : Width(group.Body);
            case QuantifierNode quantifier:
                return Math.Min(int.MaxValue, quantifier.Min * Width(quantifier.Body));
            default:
                return 0; // anchors, backreferences, conditionals, empty, options and comments
        }
    }

    /// <summary>Gets a value indicating whether <paramref name="node"/> can match without consuming a character.</summary>
    public static bool IsNullable(RegexNode node)
    {
        switch (node)
        {
            case LiteralNode _:
            case CharClassNode _:
                return false;
            case SequenceNode sequence:
                return sequence.Items.All(IsNullable);
            case AlternationNode alternation:
                return alternation.Branches.Any(IsNullable);
            case GroupNode group:
                return group.IsLookaround || IsNullable(group.Body);
            case QuantifierNode quantifier:
                return quantifier.Min == 0 || IsNullable(quantifier.Body);
            default:
                return true;
        }
    }

    /// <summary>Gets every character a non-empty match of <paramref name="node"/> can start with.</summary>
    public static CharSet First(RegexNode node) => Edge(node, fromStart: true);

    /// <summary>Gets every character a non-empty match of <paramref name="node"/> can end with.</summary>
    public static CharSet Last(RegexNode node) => Edge(node, fromStart: false);

    private static CharSet Edge(RegexNode node, bool fromStart)
    {
        switch (node)
        {
            case LiteralNode _:
            case CharClassNode _:
                return CharSet.ForNode(node) ?? CharSet.All;
            case SequenceNode sequence:
                return EdgeOfSequence(fromStart ? sequence.Items : Enumerable.Reverse(sequence.Items), fromStart);
            case AlternationNode alternation:
                return alternation.Branches.Aggregate(CharSet.Empty, (set, branch) => set.Union(Edge(branch, fromStart)));
            case GroupNode group:
                return group.IsLookaround ? CharSet.Empty : Edge(group.Body, fromStart);
            case QuantifierNode quantifier:
                return quantifier.Max == 0 ? CharSet.Empty : Edge(quantifier.Body, fromStart);
            case BackreferenceNode _:
            case ConditionalNode _:
                return CharSet.All;
            default:
                return CharSet.Empty;
        }
    }

    private static CharSet EdgeOfSequence(IEnumerable<RegexNode> items, bool fromStart)
    {
        CharSet result = CharSet.Empty;
        foreach (RegexNode item in items)
        {
            result = result.Union(Edge(item, fromStart));
            if (!IsNullable(item))
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets a value indicating whether every match of <paramref name="node"/> that starts at a given
    /// position must end at one and the same position: every way through it ends with <c>\z</c>, or with
    /// a non-multiline <c>$</c> or <c>\Z</c> after something that cannot consume a final newline.
    /// </summary>
    public static bool HasForcedEnd(RegexNode node)
    {
        switch (node)
        {
            case SequenceNode sequence:
                return SequenceHasForcedEnd(sequence.Items);
            case AlternationNode alternation:
                return alternation.Branches.All(HasForcedEnd);
            case GroupNode group:
                return !group.IsLookaround && group.GroupKind != GroupKind.Balancing && HasForcedEnd(group.Body);
            case AnchorNode anchor:
                return anchor.AnchorKind == AnchorKind.EndOfInput;
            default:
                return false;
        }
    }

    private static bool SequenceHasForcedEnd(List<RegexNode> items)
    {
        int last = items.FindLastIndex(item => !IsInvisible(item));
        if (last < 0)
        {
            return false;
        }

        RegexNode final = items[last];
        if (final is AnchorNode anchor && IsEndOrFinalNewline(anchor))
        {
            List<RegexNode> before = items.GetRange(0, last);
            SequenceNode prefix = new SequenceNode(anchor.Pattern, anchor.Start, anchor.Start, anchor.Options, before);
            return !IsNullable(prefix) && !Last(prefix).Contains('\n');
        }

        return HasForcedEnd(final);
    }

    /// <summary>Gets a value indicating whether the anchor is .NET's "end, or before a final newline".</summary>
    public static bool IsEndOrFinalNewline(AnchorNode anchor) =>
        anchor.AnchorKind == AnchorKind.EndOfInputOrNewline ||
        (anchor.AnchorKind == AnchorKind.EndOfLine && (anchor.Options & RegexOptions.Multiline) == 0);

    private static bool IsInvisible(RegexNode node) =>
        node is EmptyNode || node is CommentNode || node is InlineOptionsNode;
}
