#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Highlighting;

/// <summary>
/// Turns the matches of a regex into properly nested spans: one per match, with every group capture inside it.
/// </summary>
/// <remarks>
/// Captures are clipped to their match, so a capture made inside a lookahead that extends past the match is
/// cut at the match end, and one made entirely outside it (inside a lookbehind) is dropped. A capture that
/// crosses the end of an enclosing capture is cut at that end. When two captures cover exactly the same text,
/// or an empty capture sits at the end of another, the pattern's group structure decides which is outer.
/// </remarks>
internal sealed class HighlightTreeBuilder
{
    private const int WholeMatchGroup = 0;

    private readonly Regex _regex;
    private readonly List<HighlightGroup> _groups = new List<HighlightGroup>();
    private readonly Dictionary<int, GroupNode> _groupNodes = new Dictionary<int, GroupNode>();

    /// <param name="regex">The regex to run.</param>
    /// <param name="tree">The parsed pattern, used to order identical spans; null when it could not be parsed.</param>
    public HighlightTreeBuilder(Regex regex, RegexTree? tree)
    {
        _regex = regex;
        foreach (int number in regex.GetGroupNumbers())
        {
            if (number != WholeMatchGroup)
            {
                _groups.Add(new HighlightGroup(number, regex.GroupNameFromNumber(number), _groups.Count));
            }
        }

        IndexGroupNodes(tree);
    }

    /// <summary>Gets every capture group of the pattern in number order.</summary>
    public IReadOnlyList<HighlightGroup> Groups => _groups;

    /// <summary>Finds every match in <paramref name="text"/>, in text order.</summary>
    public IReadOnlyList<HighlightSpan> Build(string text)
    {
        List<HighlightSpan> matches = new List<HighlightSpan>();
        for (Match match = _regex.Match(text); match.Success; match = match.NextMatch())
        {
            matches.Add(BuildMatch(match));
        }

        // RightToLeft patterns find matches from last to first.
        matches.Sort((left, right) => left.Start.CompareTo(right.Start));
        return matches;
    }

    private void IndexGroupNodes(RegexTree? tree)
    {
        if (tree == null)
        {
            return;
        }

        foreach (GroupNode group in tree.Groups)
        {
            if (group.IsCapturing && !_groupNodes.ContainsKey(group.Number))
            {
                _groupNodes[group.Number] = group;
            }
        }
    }

    private HighlightSpan BuildMatch(Match match)
    {
        HighlightSpan root = new HighlightSpan(match.Index, match.Index + match.Length, null);
        List<HighlightSpan> captures = CollectCaptures(match, root);
        captures.Sort(CompareSpans);
        Nest(root, captures);
        SortForRendering(root);
        return root;
    }

    private List<HighlightSpan> CollectCaptures(Match match, HighlightSpan root)
    {
        List<HighlightSpan> captures = new List<HighlightSpan>();
        foreach (HighlightGroup group in _groups)
        {
            foreach (Capture capture in match.Groups[group.Number].Captures)
            {
                HighlightSpan? span = ClipToMatch(capture, root, group);
                if (span != null)
                {
                    captures.Add(span);
                }
            }
        }

        return captures;
    }

    private static HighlightSpan? ClipToMatch(Capture capture, HighlightSpan root, HighlightGroup group)
    {
        int start = Math.Max(capture.Index, root.Start);
        int end = Math.Min(capture.Index + capture.Length, root.End);
        bool emptyInside = capture.Length == 0 && capture.Index >= root.Start && capture.Index <= root.End;
        return end > start || emptyInside ? new HighlightSpan(start, end, group) : null;
    }

    private int CompareSpans(HighlightSpan left, HighlightSpan right)
    {
        int byStart = left.Start.CompareTo(right.Start);
        if (byStart != 0)
        {
            return byStart;
        }

        int byEnd = right.End.CompareTo(left.End);
        if (byEnd != 0)
        {
            return byEnd;
        }

        int bySource = SourceStart(left.Group).CompareTo(SourceStart(right.Group));
        return bySource != 0 ? bySource : GroupNumber(left).CompareTo(GroupNumber(right));
    }

    private void Nest(HighlightSpan root, List<HighlightSpan> captures)
    {
        Stack<HighlightSpan> open = new Stack<HighlightSpan>();
        open.Push(root);
        foreach (HighlightSpan span in captures)
        {
            while (open.Count > 1 && !Encloses(open.Peek(), span))
            {
                open.Pop();
            }

            HighlightSpan parent = open.Peek();
            span.End = Math.Min(span.End, parent.End);
            parent.Children.Add(span);
            open.Push(span);
        }
    }

    /// <summary>
    /// Orders siblings by position. An empty capture at the start of a sibling comes first, because the
    /// sibling's text follows it.
    /// </summary>
    private void SortForRendering(HighlightSpan root)
    {
        Stack<HighlightSpan> pending = new Stack<HighlightSpan>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            HighlightSpan span = pending.Pop();
            span.Children.Sort(CompareSiblings);
            foreach (HighlightSpan child in span.Children)
            {
                pending.Push(child);
            }
        }
    }

    private int CompareSiblings(HighlightSpan left, HighlightSpan right)
    {
        int byStart = left.Start.CompareTo(right.Start);
        if (byStart != 0)
        {
            return byStart;
        }

        int byEnd = left.End.CompareTo(right.End);
        return byEnd != 0 ? byEnd : SourceStart(left.Group).CompareTo(SourceStart(right.Group));
    }

    /// <summary>
    /// Decides by position, falling back to the pattern's group structure when positions are ambiguous:
    /// identical text, or an empty capture at the start or end of another capture.
    /// </summary>
    private bool Encloses(HighlightSpan parent, HighlightSpan span)
    {
        bool startsInside = span.Start >= parent.Start && span.Start < parent.End;
        bool emptyAtEnd = span.IsEmpty && span.Start == parent.End;
        if (!startsInside && !emptyAtEnd)
        {
            return false;
        }

        bool emptyAtStart = span.IsEmpty && span.Start == parent.Start;
        bool sameText = span.Start == parent.Start && span.End == parent.End;
        bool ambiguous = emptyAtEnd || emptyAtStart || sameText;
        return !ambiguous || IsStructurallyInside(span.Group, parent.Group);
    }

    private bool IsStructurallyInside(HighlightGroup? child, HighlightGroup? parent)
    {
        if (parent == null)
        {
            return true;
        }

        if (child == null || child.Number == parent.Number)
        {
            return false;
        }

        if (!_groupNodes.TryGetValue(child.Number, out GroupNode? inner) ||
            !_groupNodes.TryGetValue(parent.Number, out GroupNode? outer))
        {
            return true;
        }

        return outer.Start <= inner.Start && inner.End <= outer.End;
    }

    private int SourceStart(HighlightGroup? group)
    {
        if (group == null)
        {
            return -1;
        }

        return _groupNodes.TryGetValue(group.Number, out GroupNode? node) ? node.Start : int.MaxValue;
    }

    private static int GroupNumber(HighlightSpan span) => span.Group?.Number ?? WholeMatchGroup;
}
