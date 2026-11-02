#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Builds a Thompson NFA for the full-match language of a regex syntax tree. Anchors are kept
/// as special edges while building and resolved afterwards, once it is known whether each
/// one really sits at the very start or the very end of the pattern.
/// </summary>
internal sealed class NfaBuilder
{
    private const int MaxSourceInMessage = 40;

    private readonly List<NfaState> _states = new List<NfaState>();
    private readonly List<AnchorEdge> _anchors = new List<AnchorEdge>();

    private NfaBuilder()
    {
    }

    /// <summary>
    /// Builds the NFA of a sequence of items.
    /// </summary>
    /// <param name="items">The items, matched one after another.</param>
    /// <param name="openEnd">
    /// True when anything may follow the items (a lookahead body); false when the input must end
    /// right after them, unless an explicit end anchor says otherwise.
    /// </param>
    public static Nfa Build(IReadOnlyList<RegexNode> items, bool openEnd)
    {
        NfaBuilder builder = new NfaBuilder();
        Fragment body = builder.BuildSequence(items);
        return builder.Finish(body, openEnd);
    }

    /// <summary>Creates the exception thrown for a construct outside the regular subset.</summary>
    public static NotSupportedException Unsupported(string construct, RegexNode node)
    {
        string source = node.Source;
        if (source.Length > MaxSourceInMessage)
        {
            source = $"{source.Substring(0, MaxSourceInMessage)}...";
        }
        return new NotSupportedException(
            $"Language algebra does not support {construct}: '{source}' at position {node.Start}. " +
            "Only regular constructs (literals, classes, groups, alternation, quantifiers, and anchors " +
            "at the very start or end) can be compared.");
    }

    private Nfa Finish(Fragment body, bool openEnd)
    {
        int closedAccept = NewState();
        _states[closedAccept].IsAccepting = true;
        _states[body.End].IsAccepting = true;
        ResolveAnchors(body.Start, body.End, closedAccept);
        if (openEnd)
        {
            _states[body.End].CharEdges.Add(new NfaCharEdge(CharSet.All, body.End));
        }
        return new Nfa(_states, body.Start);
    }

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    private int NewState()
    {
        if (_states.Count >= AlgebraLimits.MaxNfaStates)
        {
            throw new InvalidOperationException(
                $"The pattern needs more than {AlgebraLimits.MaxNfaStates} automaton states; it is too large for language algebra.");
        }
        _states.Add(new NfaState());
        return _states.Count - 1;
    }

    private void Link(int from, int to) => _states[from].Epsilon.Add(to);

    private Fragment Epsilon()
    {
        int start = NewState();
        int end = NewState();
        Link(start, end);
        return new Fragment(start, end);
    }

    private Fragment BuildSequence(IReadOnlyList<RegexNode> items)
    {
        if (items.Count == 0)
        {
            return Epsilon();
        }

        Fragment first = BuildNode(items[0]);
        int end = first.End;
        for (int i = 1; i < items.Count; i++)
        {
            Fragment next = BuildNode(items[i]);
            Link(end, next.Start);
            end = next.End;
        }
        return new Fragment(first.Start, end);
    }

    private Fragment BuildNode(RegexNode node)
    {
        switch (node)
        {
            case EmptyNode _:
            case InlineOptionsNode _:
            case CommentNode _:
                return Epsilon();
            case SequenceNode sequence:
                return BuildSequence(sequence.Items);
            case AlternationNode alternation:
                return BuildAlternation(alternation);
            case LiteralNode _:
            case CharClassNode _:
                return BuildChar(node);
            case GroupNode group:
                return BuildGroup(group);
            case QuantifierNode quantifier:
                return BuildQuantifier(quantifier);
            case AnchorNode anchor:
                return BuildAnchor(anchor);
            case BackreferenceNode _:
                throw Unsupported("backreferences", node);
            case ConditionalNode _:
                throw Unsupported("conditionals", node);
            default:
                throw Unsupported("this construct", node);
        }
    }

    private Fragment BuildAlternation(AlternationNode alternation)
    {
        int start = NewState();
        int end = NewState();
        foreach (RegexNode branch in alternation.Branches)
        {
            Fragment fragment = BuildNode(branch);
            Link(start, fragment.Start);
            Link(fragment.End, end);
        }
        return new Fragment(start, end);
    }

    private Fragment BuildChar(RegexNode node)
    {
        CharSet set = CharSet.ForNode(node) ?? throw new InvalidOperationException($"No character set for '{node.Source}'.");
        int start = NewState();
        int end = NewState();
        if (!set.IsEmpty)
        {
            _states[start].CharEdges.Add(new NfaCharEdge(set, end));
        }
        return new Fragment(start, end);
    }

    private Fragment BuildGroup(GroupNode group)
    {
        switch (group.GroupKind)
        {
            case GroupKind.Capturing:
            case GroupKind.Named:
            case GroupKind.NonCapturing:
            case GroupKind.Options:
                return BuildNode(group.Body);
            case GroupKind.Atomic:
                throw Unsupported("atomic groups", group);
            case GroupKind.Balancing:
                throw Unsupported("balancing groups", group);
            case GroupKind.Lookahead:
            case GroupKind.NegativeLookahead:
                throw Unsupported("lookaheads except at the very start of the pattern", group);
            default:
                throw Unsupported("lookbehinds", group);
        }
    }

    private Fragment BuildQuantifier(QuantifierNode quantifier)
    {
        CheckBound(quantifier);
        int start = NewState();
        int current = start;
        for (int i = 0; i < quantifier.Min; i++)
        {
            Fragment copy = BuildNode(quantifier.Body);
            Link(current, copy.Start);
            current = copy.End;
        }

        return quantifier.IsUnbounded
            ? FinishUnbounded(quantifier, start, current)
            : FinishBounded(quantifier, start, current);
    }

    private Fragment FinishUnbounded(QuantifierNode quantifier, int start, int current)
    {
        int loop = NewState();
        int end = NewState();
        Fragment copy = BuildNode(quantifier.Body);
        Link(current, loop);
        Link(loop, copy.Start);
        Link(copy.End, loop);
        Link(loop, end);
        return new Fragment(start, end);
    }

    private Fragment FinishBounded(QuantifierNode quantifier, int start, int current)
    {
        int end = NewState();
        for (int i = quantifier.Min; i < quantifier.Max; i++)
        {
            Fragment copy = BuildNode(quantifier.Body);
            Link(current, end);
            Link(current, copy.Start);
            current = copy.End;
        }
        Link(current, end);
        return new Fragment(start, end);
    }

    private static void CheckBound(QuantifierNode quantifier)
    {
        bool tooLarge = quantifier.Min > AlgebraLimits.MaxRepeatBound ||
                        (!quantifier.IsUnbounded && quantifier.Max > AlgebraLimits.MaxRepeatBound);
        if (tooLarge)
        {
            throw new InvalidOperationException(
                $"The repeat '{quantifier.QuantifierText}' at position {quantifier.Start} exceeds the language algebra " +
                $"limit of {AlgebraLimits.MaxRepeatBound} repetitions.");
        }
    }

    private Fragment BuildAnchor(AnchorNode anchor)
    {
        AnchorRole role = RoleOf(anchor);
        int start = NewState();
        int end = NewState();
        _anchors.Add(new AnchorEdge(start, end, role, anchor));
        return new Fragment(start, end);
    }

    private static AnchorRole RoleOf(AnchorNode anchor)
    {
        bool multiline = (anchor.Options & RegexOptions.Multiline) != 0;
        switch (anchor.AnchorKind)
        {
            case AnchorKind.StartOfLine when multiline:
                throw Unsupported("'^' with RegexOptions.Multiline (use \\A instead)", anchor);
            case AnchorKind.EndOfLine when multiline:
                throw Unsupported("'$' with RegexOptions.Multiline (use \\z instead)", anchor);
            case AnchorKind.StartOfLine:
            case AnchorKind.StartOfInput:
                return AnchorRole.Start;
            case AnchorKind.EndOfLine:
            case AnchorKind.EndOfInputOrNewline:
                return AnchorRole.EndOrFinalNewline;
            case AnchorKind.EndOfInput:
                return AnchorRole.End;
            case AnchorKind.WordBoundary:
                throw Unsupported("word boundaries", anchor);
            case AnchorKind.NonWordBoundary:
                throw Unsupported("non-word boundaries", anchor);
            default:
                throw Unsupported("the contiguous-match anchor \\G", anchor);
        }
    }

    // ------------------------------------------------------------------
    // Anchor resolution
    // ------------------------------------------------------------------

    private void ResolveAnchors(int start, int implicitAccept, int closedAccept)
    {
        if (_anchors.Count == 0)
        {
            return;
        }

        Dictionary<int, List<AnchorEdge>> anchorsFrom = IndexAnchors();
        HashSet<int> afterConsumption = StatesAfterConsumption(start, anchorsFrom);
        List<KeyValuePair<AnchorEdge, EndResolution>> resolutions = new List<KeyValuePair<AnchorEdge, EndResolution>>();
        foreach (AnchorEdge anchor in _anchors)
        {
            EndResolution resolution = Validate(anchor, afterConsumption, anchorsFrom, implicitAccept);
            resolutions.Add(new KeyValuePair<AnchorEdge, EndResolution>(anchor, resolution));
        }

        foreach (KeyValuePair<AnchorEdge, EndResolution> pair in resolutions)
        {
            Apply(pair.Key, pair.Value, closedAccept);
        }
    }

    private EndResolution Validate(AnchorEdge anchor, HashSet<int> afterConsumption, Dictionary<int, List<AnchorEdge>> anchorsFrom, int implicitAccept)
    {
        if (anchor.Role == AnchorRole.Start)
        {
            if (afterConsumption.Contains(anchor.From))
            {
                throw Unsupported("start anchors anywhere but the very start of the pattern", anchor.Node);
            }
            return EndResolution.None;
        }

        if (CanConsumeFrom(anchor.To, anchorsFrom))
        {
            throw Unsupported("end anchors anywhere but the very end of the pattern", anchor.Node);
        }

        if (!CanReach(anchor.To, implicitAccept, anchorsFrom, allowStrict: true))
        {
            return EndResolution.None;
        }

        bool lenient = anchor.Role == AnchorRole.EndOrFinalNewline &&
                       CanReach(anchor.To, implicitAccept, anchorsFrom, allowStrict: false);
        return lenient ? EndResolution.EndOrFinalNewline : EndResolution.End;
    }

    private void Apply(AnchorEdge anchor, EndResolution resolution, int closedAccept)
    {
        if (anchor.Role == AnchorRole.Start)
        {
            Link(anchor.From, anchor.To);
            return;
        }

        if (resolution == EndResolution.None)
        {
            return;
        }

        Link(anchor.From, closedAccept);
        if (resolution == EndResolution.EndOrFinalNewline)
        {
            _states[anchor.From].CharEdges.Add(new NfaCharEdge(CharSet.Single('\n'), closedAccept));
        }
    }

    private Dictionary<int, List<AnchorEdge>> IndexAnchors()
    {
        Dictionary<int, List<AnchorEdge>> index = new Dictionary<int, List<AnchorEdge>>();
        foreach (AnchorEdge anchor in _anchors)
        {
            if (!index.TryGetValue(anchor.From, out List<AnchorEdge>? list))
            {
                list = new List<AnchorEdge>();
                index[anchor.From] = list;
            }
            list.Add(anchor);
        }
        return index;
    }

    /// <summary>Returns every state that can be reached after at least one character was consumed.</summary>
    private HashSet<int> StatesAfterConsumption(int start, Dictionary<int, List<AnchorEdge>> anchorsFrom)
    {
        HashSet<int> reachable = Traverse(new[] { start }, anchorsFrom, includeChars: true);
        List<int> seeds = new List<int>();
        foreach (int state in reachable)
        {
            foreach (NfaCharEdge edge in _states[state].CharEdges)
            {
                seeds.Add(edge.Target);
            }
        }
        return Traverse(seeds, anchorsFrom, includeChars: true);
    }

    private bool CanConsumeFrom(int state, Dictionary<int, List<AnchorEdge>> anchorsFrom)
    {
        foreach (int reached in Traverse(new[] { state }, anchorsFrom, includeChars: false))
        {
            if (_states[reached].CharEdges.Count > 0)
            {
                return true;
            }
        }
        return false;
    }

    private bool CanReach(int from, int target, Dictionary<int, List<AnchorEdge>> anchorsFrom, bool allowStrict)
    {
        HashSet<int> visited = new HashSet<int> { from };
        Stack<int> stack = new Stack<int>();
        stack.Push(from);
        while (stack.Count > 0)
        {
            int state = stack.Pop();
            if (state == target)
            {
                return true;
            }
            foreach (int next in EpsilonSuccessors(state, anchorsFrom, allowStrict))
            {
                if (visited.Add(next))
                {
                    stack.Push(next);
                }
            }
        }
        return false;
    }

    private IEnumerable<int> EpsilonSuccessors(int state, Dictionary<int, List<AnchorEdge>> anchorsFrom, bool allowStrict)
    {
        foreach (int next in _states[state].Epsilon)
        {
            yield return next;
        }
        if (anchorsFrom.TryGetValue(state, out List<AnchorEdge>? anchors))
        {
            foreach (AnchorEdge anchor in anchors)
            {
                if (allowStrict || anchor.Role != AnchorRole.End)
                {
                    yield return anchor.To;
                }
            }
        }
    }

    private HashSet<int> Traverse(IEnumerable<int> seeds, Dictionary<int, List<AnchorEdge>> anchorsFrom, bool includeChars)
    {
        HashSet<int> visited = new HashSet<int>();
        Stack<int> stack = new Stack<int>();
        foreach (int seed in seeds)
        {
            if (visited.Add(seed))
            {
                stack.Push(seed);
            }
        }

        while (stack.Count > 0)
        {
            int state = stack.Pop();
            foreach (int next in Successors(state, anchorsFrom, includeChars))
            {
                if (visited.Add(next))
                {
                    stack.Push(next);
                }
            }
        }
        return visited;
    }

    private IEnumerable<int> Successors(int state, Dictionary<int, List<AnchorEdge>> anchorsFrom, bool includeChars)
    {
        foreach (int next in EpsilonSuccessors(state, anchorsFrom, allowStrict: true))
        {
            yield return next;
        }
        if (includeChars)
        {
            foreach (NfaCharEdge edge in _states[state].CharEdges)
            {
                yield return edge.Target;
            }
        }
    }

    private readonly struct Fragment
    {
        public Fragment(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }

        public int End { get; }
    }

    private enum AnchorRole
    {
        Start,
        End,
        EndOrFinalNewline
    }

    private enum EndResolution
    {
        None,
        End,
        EndOrFinalNewline
    }

    private sealed class AnchorEdge
    {
        public AnchorEdge(int from, int to, AnchorRole role, AnchorNode node)
        {
            From = from;
            To = to;
            Role = role;
            Node = node;
        }

        public int From { get; }

        public int To { get; }

        public AnchorRole Role { get; }

        public AnchorNode Node { get; }
    }
}
