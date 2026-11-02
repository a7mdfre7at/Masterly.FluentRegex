#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Export;

/// <summary>
/// The facts a translation needs before emitting anything: the tree's nodes and parents, which
/// options can become global ECMAScript flags, and how .NET group numbers map to ECMAScript ones.
/// </summary>
internal sealed class TranslationPlan
{
    private readonly Dictionary<RegexNode, RegexNode> _parents = new Dictionary<RegexNode, RegexNode>();
    private readonly List<RegexNode> _nodes = new List<RegexNode>();
    private readonly Dictionary<int, GroupNode> _groupsByNumber = new Dictionary<int, GroupNode>();
    private readonly Dictionary<GroupNode, int> _ordinals = new Dictionary<GroupNode, int>();

    public TranslationPlan(RegexTree tree, RegexFlavor flavor, FlavorOptions options)
    {
        Collect(tree.Root, null);
        NumberGroups(tree);
        bool flagsAllowed = flavor == RegexFlavor.JavaScript;
        IgnoreCaseFlag = flagsAllowed && IsUniformIgnoreCase();
        SinglelineFlag = flagsAllowed && IsUniform(IsDot, RegexOptions.Singleline);
        MultilineFlag = flagsAllowed && !options.PreserveLineSemantics && IsUniform(IsLineAnchor, RegexOptions.Multiline);
    }

    /// <summary>Gets every node of the tree in pre-order.</summary>
    public IReadOnlyList<RegexNode> Nodes => _nodes;

    /// <summary>Gets a value indicating whether the <c>i</c> flag replaces per-node case expansion.</summary>
    public bool IgnoreCaseFlag { get; }

    /// <summary>Gets a value indicating whether the <c>s</c> flag is used for <c>.</c>.</summary>
    public bool SinglelineFlag { get; }

    /// <summary>Gets a value indicating whether the <c>m</c> flag is used for <c>^</c> and <c>$</c>.</summary>
    public bool MultilineFlag { get; }

    /// <summary>Gets a value indicating whether any capture number differs between .NET and ECMAScript.</summary>
    public bool NumberingDiffers { get; private set; }

    /// <summary>Gets the parent of a node, or null for the root.</summary>
    public RegexNode? ParentOf(RegexNode node) => _parents.TryGetValue(node, out RegexNode? parent) ? parent : null;

    /// <summary>Finds the capturing group with a .NET group number.</summary>
    public GroupNode? GroupByNumber(int number) => _groupsByNumber.TryGetValue(number, out GroupNode? group) ? group : null;

    /// <summary>Gets the ECMAScript number of a capturing group: its position among all capturing groups.</summary>
    public int OrdinalOf(GroupNode group) => _ordinals[group];

    /// <summary>Maps every capturing group's name and .NET number to its ECMAScript number.</summary>
    public IReadOnlyDictionary<string, int> GroupNumbers() => BuildGroupNumbers(_ordinals);

    /// <summary>
    /// Builds the <see cref="FlavorConversion.GroupNumbers"/> dictionary from the target number of
    /// each capturing group: the group's name (when it has one) and its .NET number, as a string.
    /// </summary>
    public static IReadOnlyDictionary<string, int> BuildGroupNumbers(IEnumerable<KeyValuePair<GroupNode, int>> numbers)
    {
        Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (KeyValuePair<GroupNode, int> entry in numbers)
        {
            GroupNode group = entry.Key;
            if (group.Name != null)
            {
                result[group.Name] = entry.Value;
            }

            result[group.Number.ToString(CultureInfo.InvariantCulture)] = entry.Value;
        }

        return result;
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="group"/> has certainly captured by the time
    /// <paramref name="reference"/> is matched: the group ends before the reference and nothing between
    /// them and their closest common ancestor (an alternative, an optional repeat, a conditional or a
    /// negative lookaround) can skip it.
    /// </summary>
    public bool AlwaysParticipatesBefore(GroupNode group, RegexNode reference)
    {
        if (group.End > reference.Start)
        {
            return false;
        }

        HashSet<RegexNode> referenceAncestors = new HashSet<RegexNode>();
        for (RegexNode? node = reference; node != null; node = ParentOf(node))
        {
            referenceAncestors.Add(node);
        }

        for (RegexNode? node = ParentOf(group); node != null; node = ParentOf(node))
        {
            if (referenceAncestors.Contains(node))
            {
                return !(node is AlternationNode);
            }

            if (MayBeSkipped(node))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets a value indicating whether an iteration of an enclosing repeat that can run more than once
    /// may skip <paramref name="group"/>. .NET then keeps the capture of an earlier iteration, while
    /// PostgreSQL and JavaScript reset the group at the start of every iteration: on <c>ab</c>,
    /// <c>(?:(a)|b)+</c> leaves group 1 at <c>a</c> in .NET and unset in those engines.
    /// </summary>
    public bool MayLoseCaptureBetweenIterations(GroupNode group)
    {
        bool skippable = false;
        for (RegexNode? node = ParentOf(group); node != null; node = ParentOf(node))
        {
            if (node is QuantifierNode quantifier && skippable && (quantifier.IsUnbounded || quantifier.Max > 1))
            {
                return true;
            }

            skippable |= MayBeSkipped(node);
        }

        return false;
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="group"/> sits in a repeat that can run more than once
    /// and whose body can match empty text. .NET then runs one more, empty iteration that captures empty
    /// text, which RE2 and ECMAScript refuse: on <c>aa</c>, <c>(a?)+</c> leaves group 1 empty in .NET and
    /// <c>a</c> in those engines.
    /// </summary>
    public bool MayCaptureInEmptyIteration(GroupNode group)
    {
        for (RegexNode? node = ParentOf(group); node != null; node = ParentOf(node))
        {
            if (node is QuantifierNode quantifier && (quantifier.IsUnbounded || quantifier.Max > 1) && MatchShape.IsNullable(quantifier.Body))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets a value indicating whether a match can pass <paramref name="node"/> without matching all of its children.</summary>
    public static bool MayBeSkipped(RegexNode node)
    {
        switch (node)
        {
            case AlternationNode alternation:
                return alternation.Branches.Count > 1;
            case QuantifierNode quantifier:
                return quantifier.Min == 0;
            case ConditionalNode _:
                return true;
            case GroupNode group:
                return group.GroupKind == GroupKind.NegativeLookahead || group.GroupKind == GroupKind.NegativeLookbehind;
            default:
                return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether case-insensitivity changes what the node matches. Digits,
    /// punctuation and <c>\d</c> are unaffected, so they never force per-node expansion.
    /// </summary>
    public static bool IsCaseSensitive(RegexNode node)
    {
        if (node is BackreferenceNode)
        {
            return true;
        }

        CharSet? withCase = CharSetWith(node, node.Options | RegexOptions.IgnoreCase);
        CharSet? withoutCase = CharSetWithout(node, RegexOptions.IgnoreCase);
        return withCase != null && withoutCase != null && !SameMembers(withCase, withoutCase);
    }

    /// <summary>Computes the node's character set as if <paramref name="removed"/> were off.</summary>
    public static CharSet? CharSetWithout(RegexNode node, RegexOptions removed) => CharSetWith(node, node.Options & ~removed);

    private static CharSet? CharSetWith(RegexNode node, RegexOptions options)
    {
        switch (node)
        {
            case LiteralNode literal:
                return CharSet.ForSource(LiteralSource(literal.Value), options);
            case CharClassNode charClass:
                return CharSet.ForSource(charClass.Source, options);
            default:
                return null;
        }
    }

    private static string LiteralSource(char c) => $"\\u{((int)c).ToString("X4", CultureInfo.InvariantCulture)}";

    private static bool SameMembers(CharSet left, CharSet right) =>
        left.Count == right.Count && left.Intersect(right).Count == left.Count;

    private static bool IsDot(RegexNode node) => node is CharClassNode charClass && charClass.ClassKind == ClassKind.Any;

    private static bool IsLineAnchor(RegexNode node) =>
        node is AnchorNode anchor &&
        (anchor.AnchorKind == AnchorKind.StartOfLine || anchor.AnchorKind == AnchorKind.EndOfLine);

    private bool IsUniform(Func<RegexNode, bool> selector, RegexOptions option)
    {
        bool any = false;
        foreach (RegexNode node in _nodes)
        {
            if (!selector(node))
            {
                continue;
            }

            if ((node.Options & option) == 0)
            {
                return false;
            }

            any = true;
        }

        return any;
    }

    private bool IsUniformIgnoreCase()
    {
        bool any = false;
        foreach (RegexNode node in _nodes)
        {
            if (!IsCaseSensitive(node))
            {
                continue;
            }

            if ((node.Options & RegexOptions.IgnoreCase) == 0)
            {
                return false;
            }

            any = true;
        }

        return any;
    }

    private void Collect(RegexNode node, RegexNode? parent)
    {
        _nodes.Add(node);
        if (parent != null)
        {
            _parents[node] = parent;
        }

        foreach (RegexNode child in node.Children)
        {
            Collect(child, node);
        }
    }

    private void NumberGroups(RegexTree tree)
    {
        List<GroupNode> capturing = new List<GroupNode>();
        foreach (GroupNode group in tree.Groups)
        {
            if (group.IsCapturing)
            {
                capturing.Add(group);
            }
        }

        capturing.Sort((left, right) => left.Start.CompareTo(right.Start));
        for (int i = 0; i < capturing.Count; i++)
        {
            GroupNode group = capturing[i];
            _ordinals[group] = i + 1;
            NumberingDiffers |= group.Number != i + 1;
            if (!_groupsByNumber.ContainsKey(group.Number))
            {
                _groupsByNumber[group.Number] = group;
            }
        }
    }
}
