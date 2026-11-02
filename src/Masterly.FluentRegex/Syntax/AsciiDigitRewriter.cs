#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Syntax;

/// <summary>
/// Rewrites <c>\d</c> and <c>\D</c> so they mean ASCII digits only, leaving every other construct
/// byte-for-byte unchanged. Each replacement matches exactly the same characters as the
/// ASCII meaning of the shorthand it replaces, in every context: standalone, inside a set,
/// inside a negated set and inside a class subtraction.
/// </summary>
internal static class AsciiDigitRewriter
{
    private const string AsciiDigitClass = "[0-9]";
    private const string NonAsciiDigitClass = "[^0-9]";
    private const string AsciiDigitItems = "0-9";
    private const string NonDigitCategory = @"\P{Nd}";
    private const string UnicodeEscapeFormat = "X4";

    // Inside a set, ASCII \D is "not a decimal digit, or a decimal digit outside 0-9".
    // Single characters are written as one-character ranges so a following '-' can never start a range.
    private static readonly Lazy<string> NonAsciiDigitItems = new Lazy<string>(BuildNonAsciiDigitItems);

    /// <summary>Returns the pattern text with ASCII-only digit shorthands.</summary>
    /// <param name="tree">The parsed pattern.</param>
    /// <returns>The rewritten pattern text, or the original text when nothing needed rewriting.</returns>
    public static string Rewrite(RegexTree tree)
    {
        if (tree == null)
        {
            throw new ArgumentNullException(nameof(tree));
        }

        // ECMAScript mode already gives \d its ASCII meaning.
        if ((tree.Options & RegexOptions.ECMAScript) != 0)
        {
            return tree.Pattern;
        }

        List<KeyValuePair<RegexNode, string>> edits = new List<KeyValuePair<RegexNode, string>>();
        CollectEdits(tree.Root, edits);
        return Apply(tree.Pattern, edits);
    }

    private static void CollectEdits(RegexNode node, List<KeyValuePair<RegexNode, string>> edits)
    {
        if (node is CharClassNode charClass)
        {
            string? replacement = ReplacementFor(charClass);
            if (replacement != null)
            {
                edits.Add(new KeyValuePair<RegexNode, string>(charClass, replacement));
            }
            return;
        }

        foreach (RegexNode child in node.Children)
        {
            CollectEdits(child, edits);
        }
    }

    private static string? ReplacementFor(CharClassNode node)
    {
        switch (node.ClassKind)
        {
            case ClassKind.Digit:
                return AsciiDigitClass;
            case ClassKind.NotDigit:
                return NonAsciiDigitClass;
            case ClassKind.Bracket:
                return ContainsDigitShorthand(node.Bracket) ? WriteBracket(node.Bracket) : null;
            default:
                return null;
        }
    }

    private static bool ContainsDigitShorthand(BracketClass? bracket)
    {
        if (bracket == null)
        {
            return false;
        }

        bool hasShorthand = bracket.Items.Any(item => item.ShorthandKind == ClassKind.Digit || item.ShorthandKind == ClassKind.NotDigit);
        return hasShorthand || ContainsDigitShorthand(bracket.Subtraction);
    }

    private static string WriteBracket(BracketClass bracket)
    {
        StringBuilder sb = new StringBuilder();
        WriteBracket(sb, bracket);
        return sb.ToString();
    }

    private static void WriteBracket(StringBuilder sb, BracketClass bracket)
    {
        sb.Append('[');
        if (bracket.Negated)
        {
            sb.Append('^');
        }

        foreach (ClassItem item in bracket.Items)
        {
            sb.Append(ItemText(item));
        }

        if (bracket.Subtraction != null)
        {
            sb.Append('-');
            WriteBracket(sb, bracket.Subtraction);
        }

        sb.Append(']');
    }

    private static string ItemText(ClassItem item)
    {
        if (item.ShorthandKind == ClassKind.Digit)
        {
            return AsciiDigitItems;
        }

        if (item.ShorthandKind == ClassKind.NotDigit)
        {
            return NonAsciiDigitItems.Value;
        }

        return item.Source;
    }

    private static string Apply(string pattern, List<KeyValuePair<RegexNode, string>> edits)
    {
        if (edits.Count == 0)
        {
            return pattern;
        }

        StringBuilder sb = new StringBuilder(pattern.Length + edits.Count);
        int position = 0;
        foreach (KeyValuePair<RegexNode, string> edit in edits.OrderBy(e => e.Key.Start))
        {
            sb.Append(pattern, position, edit.Key.Start - position);
            sb.Append(edit.Value);
            position = edit.Key.End;
        }

        sb.Append(pattern, position, pattern.Length - position);
        return sb.ToString();
    }

    private static string BuildNonAsciiDigitItems()
    {
        CharSet unicodeDigits = CharSet.ForSource(@"\d", RegexOptions.None);
        CharSet asciiDigits = CharSet.ForSource(AsciiDigitClass, RegexOptions.None);

        StringBuilder sb = new StringBuilder(NonDigitCategory);
        foreach (KeyValuePair<char, char> range in unicodeDigits.Except(asciiDigits).Ranges())
        {
            sb.Append($@"\u{((int)range.Key).ToString(UnicodeEscapeFormat, CultureInfo.InvariantCulture)}");
            sb.Append('-');
            sb.Append($@"\u{((int)range.Value).ToString(UnicodeEscapeFormat, CultureInfo.InvariantCulture)}");
        }

        return sb.ToString();
    }
}
