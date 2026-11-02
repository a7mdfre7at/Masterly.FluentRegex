#nullable enable

using System.Globalization;
using Masterly.FluentRegex.Automata;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Optimization;

/// <summary>Small structural questions about syntax nodes that the rewrites rely on.</summary>
internal static class SyntaxFacts
{
    /// <summary>Returns true for a construct that always consumes exactly one character.</summary>
    public static bool IsSingleChar(RegexNode node) => node is LiteralNode || node is CharClassNode;

    /// <summary>Returns true when the node or anything inside it is a capturing or balancing group.</summary>
    public static bool ContainsCapture(RegexNode node)
    {
        if (node is GroupNode group && (group.IsCapturing || group.GroupKind == GroupKind.Balancing))
        {
            return true;
        }
        foreach (RegexNode child in node.Children)
        {
            if (ContainsCapture(child))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Returns true when an inline option such as <c>(?i)</c> appears directly in the node.</summary>
    public static bool HasInlineOptions(RegexNode node)
    {
        foreach (RegexNode item in RegexWriter.ItemsOf(node))
        {
            if (item is InlineOptionsNode)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns the text that adds a single-character construct to a bracketed class, or null
    /// when it cannot be placed inside one.
    /// </summary>
    public static string? ClassMemberText(RegexNode node)
    {
        if (node is LiteralNode literal)
        {
            return CharClassFormatter.EscapeInClass(literal.Value);
        }
        if (!(node is CharClassNode charClass))
        {
            return null;
        }

        switch (charClass.ClassKind)
        {
            case ClassKind.Any:
                return null;
            case ClassKind.Bracket:
                bool simple = !charClass.Bracket.Negated && charClass.Bracket.Subtraction == null;
                return simple ? charClass.Source.Substring(1, charClass.Source.Length - 2) : null;
            default:
                return charClass.Source;
        }
    }

    /// <summary>Writes a quantifier in its shortest form.</summary>
    public static string Quantifier(int min, int max, bool lazy)
    {
        string low = min.ToString(CultureInfo.InvariantCulture);
        string text;
        if (max == QuantifierNode.Unbounded)
        {
            text = min == 0 ? "*" : min == 1 ? "+" : $"{{{low},}}";
        }
        else if (min == max)
        {
            return min == 1 ? string.Empty : $"{{{low}}}";
        }
        else
        {
            text = min == 0 && max == 1 ? "?" : $"{{{low},{max.ToString(CultureInfo.InvariantCulture)}}}";
        }
        return lazy ? $"{text}?" : text;
    }
}
