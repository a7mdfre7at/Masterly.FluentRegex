#nullable enable

using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Optimization;

/// <summary>
/// Finds candidate rewrites in a syntax tree. Every candidate keeps the order in which the
/// backtracking engine tries alternatives, so leftmost-first results and captures are preserved;
/// the optimizer still verifies each one before applying it.
/// </summary>
internal sealed class RewriteFinder
{
    private readonly string _text;
    private readonly List<RegexRewrite> _found = new List<RegexRewrite>();

    private RewriteFinder(string text)
    {
        _text = text;
    }

    /// <summary>Returns every candidate rewrite, outermost first.</summary>
    public static IReadOnlyList<RegexRewrite> Find(RegexTree tree)
    {
        RewriteFinder finder = new RewriteFinder(tree.Pattern);
        finder.Visit(tree.Root, null);
        return finder._found;
    }

    private void Visit(RegexNode node, RegexNode? parent)
    {
        switch (node)
        {
            case ConditionalNode _:
                return;
            case GroupNode group when group.GroupKind == GroupKind.Balancing:
                return;
            case AlternationNode alternation:
                AlternationRewrites.Collect(alternation, _text, _found);
                break;
            case SequenceNode sequence:
                SequenceRewrites.Collect(sequence, _text, _found);
                break;
            case QuantifierNode quantifier:
                CollectQuantifier(quantifier);
                break;
            case GroupNode group:
                GroupRewrites.Collect(group, parent, _text, _found);
                break;
        }

        foreach (RegexNode child in node.Children)
        {
            Visit(child, node);
        }
    }

    private void CollectQuantifier(QuantifierNode quantifier)
    {
        if (quantifier.Max == 0)
        {
            return;
        }

        string canonical = SyntaxFacts.Quantifier(quantifier.Min, quantifier.Max, quantifier.Lazy && quantifier.Min != quantifier.Max);
        if (canonical == quantifier.QuantifierText)
        {
            return;
        }

        string body = _text.Substring(quantifier.Body.Start, quantifier.Body.Length);
        string replacement = $"{body}{canonical}";
        string description = canonical.Length == 0
            ? $"Removed redundant quantifier '{quantifier.QuantifierText}' from '{quantifier.Source}'"
            : $"Simplified quantifier '{quantifier.Source}' to '{replacement}'";
        _found.Add(new RegexRewrite(quantifier.Start, quantifier.End, replacement, description));
    }
}
