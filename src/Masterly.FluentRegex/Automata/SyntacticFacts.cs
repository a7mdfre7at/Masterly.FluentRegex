#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Export;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Sound approximations of language facts read from the syntax tree, for patterns the automaton
/// cannot model. The required prefix is the run of literal characters at the start (skipping
/// zero-width constructs, which never consume input), the suffix the run at the end, and the
/// minimum length a sum over the tree that never overestimates.
/// </summary>
internal static class SyntacticFacts
{
    private const int LengthCap = int.MaxValue / 4;

    /// <summary>Computes approximate facts for <paramref name="tree"/> in the given scope.</summary>
    public static LanguageFacts Compute(RegexTree tree, MatchScope scope, Func<bool> acceptsEmpty)
    {
        bool prefixAllowed = scope == MatchScope.FullString || ScopeAnalysis.IsStartAnchored(tree.Root);
        bool suffixAllowed = (scope == MatchScope.FullString || ScopeAnalysis.IsEndAnchored(tree.Root)) &&
                             !ScopeAnalysis.EndsWithLenientAnchor(tree.Root);
        string prefix = prefixAllowed ? Edge(tree.Root, fromStart: true) : string.Empty;
        string suffix = suffixAllowed ? Edge(tree.Root, fromStart: false) : string.Empty;
        return new LanguageFacts(scope, MinLength(tree.Root), null, prefix, suffix, acceptsEmpty(), isEmptyLanguage: false, isExact: false);
    }

    // ------------------------------------------------------------------
    // Literal edges
    // ------------------------------------------------------------------

    private static string Edge(RegexNode root, bool fromStart)
    {
        StringBuilder text = new StringBuilder();
        Collect(root, fromStart, text);
        return fromStart ? text.ToString() : Reverse(text.ToString());
    }

    /// <summary>Appends the literals at the edge of <paramref name="node"/>; returns true when the whole node was literal.</summary>
    private static bool Collect(RegexNode node, bool fromStart, StringBuilder text)
    {
        switch (node)
        {
            case LiteralNode literal:
                return CollectLiteral(literal, text);
            case CharClassNode charClass:
                return CollectClass(charClass, text);
            case SequenceNode sequence:
                return CollectSequence(sequence.Items, fromStart, text);
            case AlternationNode alternation:
                text.Append(CommonEdge(alternation, fromStart));
                return false;
            case GroupNode group:
                return CollectGroup(group, fromStart, text);
            case QuantifierNode quantifier:
                return CollectQuantifier(quantifier, fromStart, text);
            case BackreferenceNode _:
            case ConditionalNode _:
                return false;
            default:
                return true; // anchors, inline options, comments and empty nodes consume nothing
        }
    }

    private static bool CollectLiteral(LiteralNode literal, StringBuilder text)
    {
        if ((literal.Options & RegexOptions.IgnoreCase) != 0 && TranslationPlan.IsCaseSensitive(literal))
        {
            return false;
        }

        text.Append(literal.Value);
        return true;
    }

    private static bool CollectClass(CharClassNode charClass, StringBuilder text)
    {
        CharSet? set = CharSet.ForNode(charClass);
        if (set == null || set.Count != 1)
        {
            return false;
        }

        text.Append(set.First());
        return true;
    }

    private static bool CollectSequence(List<RegexNode> items, bool fromStart, StringBuilder text)
    {
        for (int i = 0; i < items.Count; i++)
        {
            RegexNode item = items[fromStart ? i : items.Count - 1 - i];
            if (!Collect(item, fromStart, text))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CollectGroup(GroupNode group, bool fromStart, StringBuilder text)
    {
        if (group.IsLookaround)
        {
            return true;
        }

        return group.GroupKind != GroupKind.Balancing && Collect(group.Body, fromStart, text);
    }

    private static bool CollectQuantifier(QuantifierNode quantifier, bool fromStart, StringBuilder text)
    {
        if (quantifier.Min == 0)
        {
            return false;
        }

        StringBuilder once = new StringBuilder();
        bool complete = Collect(quantifier.Body, fromStart, once);
        text.Append(once);
        if (!complete || quantifier.Min != quantifier.Max)
        {
            return false;
        }

        for (int i = 1; i < quantifier.Min; i++)
        {
            text.Append(once);
        }

        return true;
    }

    /// <summary>The longest text shared by the edges of every branch.</summary>
    private static string CommonEdge(AlternationNode alternation, bool fromStart)
    {
        string? common = null;
        foreach (RegexNode branch in alternation.Branches)
        {
            StringBuilder edge = new StringBuilder();
            Collect(branch, fromStart, edge);
            common = common == null ? edge.ToString() : CommonPrefix(common, edge.ToString());
        }

        return common ?? string.Empty;
    }

    private static string CommonPrefix(string left, string right)
    {
        int length = 0;
        while (length < left.Length && length < right.Length && left[length] == right[length])
        {
            length++;
        }

        return left.Substring(0, length);
    }

    private static string Reverse(string text)
    {
        char[] chars = text.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    // ------------------------------------------------------------------
    // Minimum length
    // ------------------------------------------------------------------

    private static int MinLength(RegexNode node)
    {
        switch (node)
        {
            case LiteralNode _:
            case CharClassNode _:
                return 1;
            case SequenceNode sequence:
                return Sum(sequence.Items);
            case AlternationNode alternation:
                return Minimum(alternation.Branches);
            case GroupNode group:
                return group.IsLookaround ? 0 : MinLength(group.Body);
            case QuantifierNode quantifier:
                return Capped((long)MinLength(quantifier.Body) * quantifier.Min);
            case ConditionalNode conditional:
                return conditional.No == null ? 0 : Math.Min(MinLength(conditional.Yes), MinLength(conditional.No));
            default:
                return 0; // anchors, backreferences (may be empty), options, comments
        }
    }

    private static int Sum(List<RegexNode> items)
    {
        long total = 0;
        foreach (RegexNode item in items)
        {
            total += MinLength(item);
        }

        return Capped(total);
    }

    private static int Minimum(List<RegexNode> branches)
    {
        int minimum = int.MaxValue;
        foreach (RegexNode branch in branches)
        {
            minimum = Math.Min(minimum, MinLength(branch));
        }

        return minimum;
    }

    private static int Capped(long value) => value > LengthCap ? LengthCap : (int)value;
}
