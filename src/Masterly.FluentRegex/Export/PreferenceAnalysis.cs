#nullable enable
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Export;

/// <summary>How a regex resolves the choice between a shorter and a longer match.</summary>
internal enum MatchPreference
{
    /// <summary>The construct has no preference of its own.</summary>
    None,

    /// <summary>Prefers the longer match (greedy repeats, PostgreSQL's longest-match rule).</summary>
    Longest,

    /// <summary>Prefers the shorter match (lazy repeats, PostgreSQL's shortest-match rule).</summary>
    Shortest
}

/// <summary>
/// Decides whether the substring a backtracking engine (.NET) matches, and what each group captures, is
/// determined by the input alone, apart from where the match stops. The pattern is walked like an LL(1)
/// grammar: at every alternation and every variable repeat the next character must select one option.
/// The only choice allowed is between stopping the match and consuming more, which .NET resolves by
/// the repeat's greediness; <see cref="StopChoices"/> records those. When the walk finds no other
/// ambiguity, the possible matches from a start position are a chain of ever longer matches, each with
/// a single parse, so an engine that prefers the longest (or shortest) match agrees with .NET whenever
/// every stop choice prefers the same.
/// </summary>
internal sealed class PreferenceAnalysis
{
    private readonly HashSet<MatchPreference> _stopChoices = new HashSet<MatchPreference>();

    private PreferenceAnalysis(RegexNode root)
    {
        Check(root, new Lookahead(CharSet.Empty, true));
    }

    /// <summary>Gets a value indicating whether two parses can compete for the same text.</summary>
    public bool IsAmbiguous { get; private set; }

    /// <summary>Gets the preferences .NET applies when it chooses between stopping and consuming more.</summary>
    public IReadOnlyCollection<MatchPreference> StopChoices => _stopChoices;

    /// <summary>Analyzes the tree rooted at <paramref name="root"/>.</summary>
    public static PreferenceAnalysis Of(RegexNode root) => new PreferenceAnalysis(root);

    /// <summary>
    /// Gets a value indicating whether an engine that always takes the <paramref name="engine"/> match
    /// from the leftmost start picks the same substring and parse as .NET.
    /// </summary>
    public bool AgreesWith(MatchPreference engine)
    {
        if (IsAmbiguous)
        {
            return false;
        }

        foreach (MatchPreference choice in _stopChoices)
        {
            if (choice != engine)
            {
                return false;
            }
        }

        return true;
    }

    private void Check(RegexNode node, Lookahead follow)
    {
        switch (node)
        {
            case SequenceNode sequence:
                CheckSequence(sequence, follow);
                break;
            case AlternationNode alternation:
                CheckAlternation(alternation, follow);
                break;
            case GroupNode group:
                CheckGroup(group, follow);
                break;
            case QuantifierNode quantifier:
                CheckQuantifier(quantifier, follow);
                break;
            case ConditionalNode _:
                IsAmbiguous = true;
                break;
        }
    }

    private void CheckSequence(SequenceNode sequence, Lookahead follow)
    {
        Lookahead current = follow;
        for (int i = sequence.Items.Count - 1; i >= 0; i--)
        {
            RegexNode item = sequence.Items[i];
            Check(item, current);
            current = Lookahead.Before(item, current);
        }
    }

    private void CheckGroup(GroupNode group, Lookahead follow)
    {
        if (group.IsLookaround)
        {
            return; // zero-width; captures inside lookarounds are handled by the engine rules
        }

        Check(group.Body, follow);
    }

    private void CheckAlternation(AlternationNode alternation, Lookahead follow)
    {
        List<Lookahead> options = new List<Lookahead>();
        foreach (RegexNode branch in alternation.Branches)
        {
            options.Add(Lookahead.Before(branch, follow));
            Check(branch, follow);
        }

        CheckOptionsDisjoint(options);
        int stopping = options.FindIndex(option => option.CanStop);
        if (stopping >= 0 && options.Exists(option => !option.Characters.IsEmpty))
        {
            RecordStopChoice(options, stopping);
        }
    }

    private void CheckOptionsDisjoint(List<Lookahead> options)
    {
        CharSet seen = CharSet.Empty;
        int stoppers = 0;
        foreach (Lookahead option in options)
        {
            IsAmbiguous |= seen.Overlaps(option.Characters);
            seen = seen.Union(option.Characters);
            stoppers += option.CanStop ? 1 : 0;
        }

        IsAmbiguous |= stoppers > 1;
    }

    /// <summary>.NET tries alternatives in order: a stopping branch after every consuming one acts like a greedy repeat.</summary>
    private void RecordStopChoice(List<Lookahead> options, int stopping)
    {
        bool consumerBefore = options.GetRange(0, stopping).Exists(option => !option.Characters.IsEmpty);
        bool consumerAfter = options.GetRange(stopping + 1, options.Count - stopping - 1).Exists(option => !option.Characters.IsEmpty);
        if (consumerBefore)
        {
            _stopChoices.Add(MatchPreference.Longest);
        }

        if (consumerAfter)
        {
            _stopChoices.Add(MatchPreference.Shortest);
        }
    }

    private void CheckQuantifier(QuantifierNode quantifier, Lookahead follow)
    {
        if (quantifier.Max == 0)
        {
            return;
        }

        CharSet first = MatchShape.First(quantifier.Body);
        bool repeats = quantifier.IsUnbounded || quantifier.Max > 1;
        Lookahead bodyFollow = repeats ? new Lookahead(first.Union(follow.Characters), follow.CanStop) : follow;
        Check(quantifier.Body, bodyFollow);
        if (quantifier.Min == quantifier.Max)
        {
            return;
        }

        if (MatchShape.IsNullable(quantifier.Body))
        {
            IsAmbiguous = true; // an iteration can match empty text, so the iteration count is not determined
            return;
        }

        IsAmbiguous |= first.Overlaps(follow.Characters);
        if (follow.CanStop && !first.IsEmpty)
        {
            _stopChoices.Add(quantifier.Lazy ? MatchPreference.Shortest : MatchPreference.Longest);
        }
    }

    /// <summary>
    /// What can come next: the characters the rest of the pattern can consume first, and whether the
    /// rest can match empty text so that the match may stop here.
    /// </summary>
    private readonly struct Lookahead
    {
        public Lookahead(CharSet characters, bool canStop)
        {
            Characters = characters;
            CanStop = canStop;
        }

        public CharSet Characters { get; }

        public bool CanStop { get; }

        /// <summary>Gets what can come next before <paramref name="node"/>, given what can come after it.</summary>
        public static Lookahead Before(RegexNode node, Lookahead after)
        {
            bool nullable = MatchShape.IsNullable(node);
            CharSet first = MatchShape.First(node);
            return nullable
                ? new Lookahead(first.Union(after.Characters), after.CanStop)
                : new Lookahead(first, false);
        }
    }
}
