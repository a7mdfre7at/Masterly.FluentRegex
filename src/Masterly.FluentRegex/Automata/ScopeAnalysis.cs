#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Decides whether a pattern is anchored at either end, and builds the pattern whose full-match
/// language is the set of strings <see cref="Pattern.IsMatch(string)"/> accepts:
/// <c>[\s\S]*?(?:p)[\s\S]*</c> with the padding dropped on each anchored side.
/// </summary>
internal static class ScopeAnalysis
{
    private const string LeadingPadding = @"[\s\S]*?";
    private const string TrailingPadding = @"[\s\S]*";
    private const string CommentTerminator = "\n";

    /// <summary>Gets a value indicating whether every match must start at the beginning of the input.</summary>
    public static bool IsStartAnchored(RegexNode node)
    {
        switch (node)
        {
            case AnchorNode anchor:
                return anchor.AnchorKind == AnchorKind.StartOfInput ||
                       (anchor.AnchorKind == AnchorKind.StartOfLine && (anchor.Options & RegexOptions.Multiline) == 0);
            case SequenceNode sequence:
                return IsStartAnchored(FirstConsuming(sequence.Items));
            case AlternationNode alternation:
                return AllBranches(alternation, IsStartAnchored);
            case GroupNode group:
                return ConstrainsPosition(group) && IsStartAnchored(group.Body);
            default:
                return false;
        }
    }

    /// <summary>Gets a value indicating whether every match must end at the end of the input (or before a final newline).</summary>
    public static bool IsEndAnchored(RegexNode node)
    {
        switch (node)
        {
            case AnchorNode anchor:
                return IsEndAnchor(anchor);
            case SequenceNode sequence:
                return IsEndAnchored(LastConsuming(sequence.Items));
            case AlternationNode alternation:
                return AllBranches(alternation, IsEndAnchored);
            case GroupNode group:
                return ConstrainsPosition(group) && IsEndAnchored(group.Body);
            default:
                return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the pattern ends with <c>$</c> or <c>\Z</c>, which also accept
    /// one trailing newline, so no suffix can be required even though the end is anchored.
    /// </summary>
    public static bool EndsWithLenientAnchor(RegexNode node)
    {
        switch (node)
        {
            case AnchorNode anchor:
                return IsEndAnchor(anchor) && anchor.AnchorKind != AnchorKind.EndOfInput;
            case SequenceNode sequence:
                return EndsWithLenientAnchor(LastConsuming(sequence.Items));
            case AlternationNode alternation:
                return AnyBranch(alternation, EndsWithLenientAnchor);
            case GroupNode group:
                return ConstrainsPosition(group) && EndsWithLenientAnchor(group.Body);
            default:
                return false;
        }
    }

    /// <summary>Returns the regex text whose full-match language is the scoped language of <paramref name="tree"/>.</summary>
    public static string ScopedText(RegexTree tree, MatchScope scope)
    {
        if (scope == MatchScope.FullString)
        {
            return tree.Pattern;
        }

        bool startAnchored = IsStartAnchored(tree.Root);
        bool endAnchored = IsEndAnchored(tree.Root);
        if (startAnchored && endAnchored)
        {
            return tree.Pattern;
        }

        // Only an alternation needs grouping; keeping a sequence bare leaves a leading lookahead where the algebra can see it.
        string pattern = TerminatedPattern(tree);
        string body = tree.Root is AlternationNode ? $"(?:{pattern})" : pattern;
        string leading = startAnchored ? string.Empty : LeadingPadding;
        string trailing = endAnchored ? string.Empty : TrailingPadding;
        return $"{leading}{body}{trailing}";
    }

    /// <summary>
    /// Returns the pattern text, followed by a newline when free-spacing mode (<c>x</c>) is still on at
    /// its end, so text appended after it can never become part of a trailing <c>#</c> comment. In
    /// free-spacing mode the newline is ignored whitespace; otherwise nothing is added.
    /// </summary>
    public static string TerminatedPattern(RegexTree tree) =>
        EndsInFreeSpacingMode(tree) ? tree.Pattern + CommentTerminator : tree.Pattern;

    /// <summary>
    /// Gets a value indicating whether <c>x</c> is in effect at the end of the pattern: the construction
    /// options, changed by every inline <c>(?x)</c> or <c>(?-x)</c> at the top level, which apply up to
    /// the end of the pattern (across alternation branches too). Scoped <c>(?x:...)</c> groups and
    /// options inside groups end with their group.
    /// </summary>
    public static bool EndsInFreeSpacingMode(RegexTree tree)
    {
        RegexOptions options = tree.Options;
        foreach (RegexNode item in TopLevelItems(tree.Root))
        {
            if (item is InlineOptionsNode inline)
            {
                options = RegexParser.ApplyFlags(options, inline.OptionsOn, inline.OptionsOff);
            }
        }

        return (options & RegexOptions.IgnorePatternWhitespace) != 0;
    }

    private static IEnumerable<RegexNode> TopLevelItems(RegexNode root)
    {
        List<RegexNode> branches = root is AlternationNode alternation ? alternation.Branches : new List<RegexNode> { root };
        foreach (RegexNode branch in branches)
        {
            if (branch is SequenceNode sequence)
            {
                foreach (RegexNode item in sequence.Items)
                {
                    yield return item;
                }
            }
            else
            {
                yield return branch;
            }
        }
    }

    /// <summary>Parses the scoped pattern with the given options.</summary>
    public static RegexTree ScopedTree(RegexTree tree, MatchScope scope, RegexOptions options) =>
        RegexParser.Parse(ScopedText(tree, scope), options);

    private static bool IsEndAnchor(AnchorNode anchor) =>
        anchor.AnchorKind == AnchorKind.EndOfInput ||
        anchor.AnchorKind == AnchorKind.EndOfInputOrNewline ||
        (anchor.AnchorKind == AnchorKind.EndOfLine && (anchor.Options & RegexOptions.Multiline) == 0);

    /// <summary>Groups that match their body in place; lookarounds and balancing groups are excluded.</summary>
    private static bool ConstrainsPosition(GroupNode group) => !group.IsLookaround && group.GroupKind != GroupKind.Balancing;

    private static bool AllBranches(AlternationNode alternation, Func<RegexNode, bool> test)
    {
        foreach (RegexNode branch in alternation.Branches)
        {
            if (!test(branch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AnyBranch(AlternationNode alternation, Func<RegexNode, bool> test)
    {
        foreach (RegexNode branch in alternation.Branches)
        {
            if (test(branch))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The first item that is not a comment or inline option, or an empty node.</summary>
    private static RegexNode FirstConsuming(List<RegexNode> items)
    {
        foreach (RegexNode item in items)
        {
            if (!IsTransparent(item))
            {
                return item;
            }
        }

        return new EmptyNode(string.Empty, 0, RegexOptions.None);
    }

    private static RegexNode LastConsuming(List<RegexNode> items)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (!IsTransparent(items[i]))
            {
                return items[i];
            }
        }

        return new EmptyNode(string.Empty, 0, RegexOptions.None);
    }

    private static bool IsTransparent(RegexNode node) => node is InlineOptionsNode || node is CommentNode || node is EmptyNode;
}
