#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Coverage;

/// <summary>A pattern rewritten with coverage markers, and the items the markers observe.</summary>
internal sealed class InstrumentedPattern
{
    public InstrumentedPattern(string regex, IReadOnlyList<CoverageProbe> probes)
    {
        Regex = regex;
        Probes = probes;
    }

    public string Regex { get; }

    public IReadOnlyList<CoverageProbe> Probes { get; }
}

/// <summary>
/// Instruments a pattern for coverage by inserting empty marker groups into the original source text:
/// one at the start of every alternation branch, and one inside every relevant quantifier body, wrapped as
/// <c>(?:(?&lt;marker&gt;)body)quantifier</c>. The engine then reports, through the marker captures of the
/// final match, which branches were taken and how often each repetition ran.
/// </summary>
/// <remarks>
/// Markers use explicit group numbers far above any existing group. Named groups take the lowest free
/// numbers after the unnamed ones, so neither numbered nor named groups of the original pattern are
/// renumbered, and every backreference keeps its meaning. Explicitly numbered groups also capture under
/// <see cref="System.Text.RegularExpressions.RegexOptions.ExplicitCapture"/>.
/// </remarks>
internal sealed class CoverageInstrumenter
{
    private const int FirstMarkerNumber = 1_000_000;
    private const int MaxFragmentLength = 40;
    private const string Ellipsis = "...";
    private const int TypicalInsertionLength = 16;

    private readonly List<Insertion> _insertions = new List<Insertion>();
    private readonly List<CoverageProbe> _probes = new List<CoverageProbe>();
    private int _nextMarker;

    private CoverageInstrumenter(int firstMarker) => _nextMarker = firstMarker;

    /// <summary>Instruments the pattern described by <paramref name="tree"/>.</summary>
    public static InstrumentedPattern Instrument(RegexTree tree)
    {
        CoverageInstrumenter instrumenter = new CoverageInstrumenter(FirstFreeMarker(tree));
        instrumenter.Visit(tree.Root, insideQuantifier: false);
        return new InstrumentedPattern(instrumenter.Apply(tree.Pattern), instrumenter._probes);
    }

    private static int FirstFreeMarker(RegexTree tree)
    {
        int highest = tree.Groups.Count;
        foreach (GroupNode group in tree.Groups)
        {
            if (group.Number > highest)
            {
                highest = group.Number;
            }
        }

        return highest < FirstMarkerNumber ? FirstMarkerNumber : highest + 1;
    }

    private void Visit(RegexNode node, bool insideQuantifier)
    {
        switch (node)
        {
            case SequenceNode sequence:
                VisitAll(sequence.Items, insideQuantifier);
                return;
            case AlternationNode alternation:
                VisitBranches(alternation, insideQuantifier);
                return;
            case GroupNode group:
                VisitGroup(group, insideQuantifier);
                return;
            case QuantifierNode quantifier:
                VisitQuantifier(quantifier, insideQuantifier);
                return;
            case ConditionalNode conditional:
                VisitConditional(conditional, insideQuantifier);
                return;
            default:
                return;
        }
    }

    private void VisitAll(IEnumerable<RegexNode> nodes, bool insideQuantifier)
    {
        foreach (RegexNode node in nodes)
        {
            Visit(node, insideQuantifier);
        }
    }

    private void VisitBranches(AlternationNode alternation, bool insideQuantifier)
    {
        foreach (RegexNode branch in alternation.Branches)
        {
            AddBranch(branch, "branch", insideQuantifier);
        }
    }

    /// <summary>
    /// Negative lookarounds are left untouched: their captures never survive. Everything else, including
    /// positive lookarounds, keeps its captures and is instrumented.
    /// </summary>
    private void VisitGroup(GroupNode group, bool insideQuantifier)
    {
        if (group.GroupKind == GroupKind.NegativeLookahead || group.GroupKind == GroupKind.NegativeLookbehind)
        {
            return;
        }

        Visit(group.Body, insideQuantifier);
    }

    /// <summary>The condition itself is never instrumented; only the yes and no branches are.</summary>
    private void VisitConditional(ConditionalNode conditional, bool insideQuantifier)
    {
        AddBranch(conditional.Yes, "condition-true branch", insideQuantifier);
        if (conditional.No != null)
        {
            AddBranch(conditional.No, "condition-false branch", insideQuantifier);
        }
    }

    private void AddBranch(RegexNode branch, string label, bool insideQuantifier)
    {
        int marker = _nextMarker++;
        _insertions.Add(Insertion.Open(branch.Start, _insertions.Count, MarkerText(marker)));
        string description = branch.Length == 0 ? $"empty {label}" : $"{label} {Quote(branch.Source)}";
        _probes.Add(new CoverageProbe(CoverageItemKind.AlternationBranch, description, branch.Start, branch.Length, marker, 1));
        Visit(branch, insideQuantifier);
    }

    private void VisitQuantifier(QuantifierNode quantifier, bool insideQuantifier)
    {
        if (quantifier.Max == 0)
        {
            return; // {0} never runs its body, so there is nothing to observe inside it.
        }

        bool tracksOptional = quantifier.Min == 0;
        bool isOptionalOnly = quantifier.Min == 0 && quantifier.Max == 1;
        bool tracksUpperBound = !insideQuantifier && !quantifier.IsUnbounded && quantifier.Max > quantifier.Min && !isOptionalOnly;

        if (tracksOptional || tracksUpperBound)
        {
            AddRepetitionProbes(quantifier, tracksOptional, tracksUpperBound);
        }

        Visit(quantifier.Body, insideQuantifier: true);
    }

    private void AddRepetitionProbes(QuantifierNode quantifier, bool tracksOptional, bool tracksUpperBound)
    {
        int marker = _nextMarker++;
        RegexNode body = quantifier.Body;
        _insertions.Add(Insertion.Open(body.Start, _insertions.Count, $"(?:{MarkerText(marker)}"));
        _insertions.Add(Insertion.Close(body.End, _insertions.Count, ")"));

        string fragment = Quote(quantifier.Source);
        if (tracksOptional)
        {
            _probes.Add(new CoverageProbe(CoverageItemKind.OptionalPartTaken, $"optional part {fragment}", quantifier.Start, quantifier.Length, marker, 1));
        }

        if (tracksUpperBound)
        {
            string max = quantifier.Max.ToString(CultureInfo.InvariantCulture);
            _probes.Add(new CoverageProbe(CoverageItemKind.UpperBoundReached, $"upper bound {max} of {fragment}", quantifier.Start, quantifier.Length, marker, quantifier.Max));
        }
    }

    private string Apply(string source)
    {
        _insertions.Sort(Insertion.Compare);
        StringBuilder result = new StringBuilder(source.Length + (_insertions.Count * TypicalInsertionLength));
        int copied = 0;
        foreach (Insertion insertion in _insertions)
        {
            result.Append(source, copied, insertion.Offset - copied);
            result.Append(insertion.Text);
            copied = insertion.Offset;
        }

        result.Append(source, copied, source.Length - copied);
        return result.ToString();
    }

    private static string MarkerText(int marker) => $"(?<{marker.ToString(CultureInfo.InvariantCulture)}>)";

    private static string Quote(string fragment)
    {
        string shown = fragment.Length > MaxFragmentLength ? $"{fragment.Substring(0, MaxFragmentLength)}{Ellipsis}" : fragment;
        return $"\"{shown}\"";
    }

    /// <summary>
    /// Text inserted at an offset of the original pattern. At one offset, closing text comes first, then
    /// opening text from the outermost construct inwards, so insertions at the same place nest correctly.
    /// </summary>
    private readonly struct Insertion
    {
        private Insertion(int offset, bool isClose, int order, string text)
        {
            Offset = offset;
            IsClose = isClose;
            Order = order;
            Text = text;
        }

        public int Offset { get; }

        public bool IsClose { get; }

        /// <summary>Creation order; outer constructs are visited, and so created, before inner ones.</summary>
        public int Order { get; }

        public string Text { get; }

        public static Insertion Open(int offset, int order, string text) => new Insertion(offset, false, order, text);

        public static Insertion Close(int offset, int order, string text) => new Insertion(offset, true, order, text);

        public static int Compare(Insertion left, Insertion right)
        {
            if (left.Offset != right.Offset)
            {
                return left.Offset.CompareTo(right.Offset);
            }

            if (left.IsClose != right.IsClose)
            {
                return left.IsClose ? -1 : 1;
            }

            return left.IsClose ? right.Order.CompareTo(left.Order) : left.Order.CompareTo(right.Order);
        }
    }
}
