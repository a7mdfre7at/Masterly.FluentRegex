#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Export;

/// <summary>
/// PostgreSQL advanced regular expressions (ARE), as documented in
/// https://www.postgresql.org/docs/17/functions-matching.html section 9.7.3 and verified against a
/// postgres:17 container. Facts this translation relies on: <c>\b</c> is a backspace (word boundaries
/// are <c>\y</c>/<c>\m</c>/<c>\M</c>, defined by the locale); <c>$</c> matches only at the very end
/// of the string ("the lack of special treatment for a trailing newline"); <c>.</c> and <c>[^...]</c>
/// match newlines by default; embedded options such as <c>(?i)</c> may appear only at the start;
/// <c>\d</c>, <c>\s</c> and <c>\w</c> are the locale-dependent <c>[[:digit:]]</c>, <c>[[:space:]]</c>
/// and <c>[[:word:]]</c>; there are no named groups, no atomic groups and no <c>\p{..}</c>;
/// parentheses inside lookarounds never capture and lookarounds cannot contain backreferences;
/// bounds in <c>{m,n}</c> go up to 255; a quantifier cannot follow a constraint; bracket expressions
/// honor backslash escapes and <c>\uXXXX</c>; ranges are ordered by code point; a whole RE is
/// either greedy or non-greedy, decided by its first quantifier.
/// </summary>
internal sealed class PostgreSqlTranslator : SqlRegexTranslator
{
    private const int PostgresMaxBound = 255;

    public PostgreSqlTranslator(RegexTree tree) : base(tree, RegexFlavor.PostgreSql, HexEscapeStyle.Unicode4)
    {
    }

    protected override bool SupportsUnicodeCategories => false;

    protected override bool SupportsLookaround => true;

    protected override bool SupportsBackreferences => true;

    protected override bool SupportsAtomicGroups => false;

    protected override int MaxRepeatBound => PostgresMaxBound;

    protected override string AnyCharacter => ".";

    protected override string AnyButNewline => @"[^\n]";

    protected override string StartOfInput => @"\A";

    protected override string EndOfInput => @"\Z";

    protected override string EndOrFinalNewline(AnchorNode anchor) => @"(?=\n?\Z)";

    protected override string MultilineStart(AnchorNode anchor) => @"(?:\A|(?<=\n))";

    protected override string MultilineEnd(AnchorNode anchor) => @"(?=\n|\Z)";

    protected override string WordBoundary(AnchorNode anchor, bool negated, string wordClass) =>
        LookaroundBoundary(negated, wordClass);

    protected override string? NamedGroupOpen(string name) => null;

    protected override string BackreferenceText(GroupNode group, int number) =>
        $"\\{number.ToString(CultureInfo.InvariantCulture)}";

    protected override bool Captures(GroupNode group) => !IsInsideLookaround(group);

    protected override void CheckBackreference(BackreferenceNode reference, GroupNode group)
    {
        if (InsideLookaround)
        {
            Unsupported("backreference inside a lookaround (PostgreSQL forbids it)", reference);
        }

        if (Plan.MayLoseCaptureBetweenIterations(group))
        {
            Unsupported("backreference to a group that a repeat iteration can skip (PostgreSQL resets the group on every iteration; .NET keeps the earlier capture)", reference);
        }
    }

    /// <summary>Writes <c>x{300}</c> as <c>(?:x{255}x{45})</c>; only single-character atoms can be repeated this way.</summary>
    protected override string? SplitLargeRepeat(QuantifierNode quantifier, string body)
    {
        if (!(quantifier.Body is LiteralNode || quantifier.Body is CharClassNode))
        {
            return null;
        }

        StringBuilder sb = new StringBuilder("(?:");
        List<int> mandatory = Chunks(quantifier.Min);
        for (int i = 0; i < mandatory.Count; i++)
        {
            bool openEnded = quantifier.IsUnbounded && i == mandatory.Count - 1;
            int max = openEnded ? QuantifierNode.Unbounded : mandatory[i];
            sb.Append(body).Append(QuantifierText(mandatory[i], max, openEnded && quantifier.Lazy));
        }

        if (!quantifier.IsUnbounded)
        {
            foreach (int chunk in Chunks(quantifier.Max - quantifier.Min))
            {
                sb.Append(body).Append(QuantifierText(0, chunk, quantifier.Lazy));
            }
        }

        return sb.Append(')').ToString();
    }

    /// <summary>Splits a count into parts of at most 255.</summary>
    private static List<int> Chunks(int count)
    {
        List<int> chunks = new List<int>();
        for (int remaining = count; remaining > 0; remaining -= PostgresMaxBound)
        {
            chunks.Add(remaining > PostgresMaxBound ? PostgresMaxBound : remaining);
        }

        return chunks;
    }

    /// <summary>
    /// PostgreSQL picks the longest or the shortest match from the leftmost start (decided by the RE's
    /// first quantifier) and then lets earlier subexpressions take the longest or shortest text, instead of
    /// .NET's first-alternative, greedy-first backtracking order; it also resets groups on every repeat
    /// iteration. Both are reported unless the pattern provably leaves no choice.
    /// </summary>
    protected override void AddMatchExtentWarnings()
    {
        foreach (GroupNode group in CapturingGroups.Where(Plan.MayLoseCaptureBetweenIterations).OrderBy(g => g.Start))
        {
            WarnExtent($"The group {At(group)} can be skipped by an iteration of an enclosing repeat: .NET keeps its " +
                       "capture from an earlier iteration, PostgreSQL resets it on every iteration.");
        }

        bool onlyExtentMatters = CapturingGroups.Count == 0;
        if (onlyExtentMatters && MatchShape.HasForcedEnd(Tree.Root))
        {
            return; // a match that starts at the leftmost position can only end at one place
        }

        MatchPreference engine = Preference(Tree.Root) == MatchPreference.Shortest ? MatchPreference.Shortest : MatchPreference.Longest;
        if (!PreferenceAnalysis.Of(Tree.Root).AgreesWith(engine))
        {
            string rule = engine == MatchPreference.Longest ? "longest" : "shortest";
            WarnExtent($"PostgreSQL takes the {rule} match (decided by the first quantifier) and gives earlier groups " +
                       "the longest or shortest text, while .NET takes the first alternative and the greedy or lazy " +
                       "count that succeeds first: the matched substring and the captures can differ (for example, " +
                       "(a|ab) matches \"a\" of \"ab\" in .NET and \"ab\" in PostgreSQL). Whether a string matches is unaffected.");
        }
    }

    /// <summary>
    /// The PostgreSQL greediness attribute of the translated node (section 9.7.3.5): a branch takes the
    /// attribute of its first quantified atom that has one, a parenthesized RE that of its content, an RE of
    /// two or more branches is greedy, and a quantifier other than <c>{m}</c> is greedy or lazy. The
    /// translation's own alternations (multiline <c>^</c> and <c>\b</c>) count as greedy groups.
    /// </summary>
    private static MatchPreference Preference(RegexNode node)
    {
        switch (node)
        {
            case SequenceNode sequence:
                return sequence.Items.Select(Preference).FirstOrDefault(p => p != MatchPreference.None);
            case AlternationNode alternation:
                return alternation.Branches.Count > 1 ? MatchPreference.Longest : Preference(alternation.Branches[0]);
            case GroupNode group:
                return group.IsLookaround ? MatchPreference.None : Preference(group.Body);
            case QuantifierNode quantifier:
                return QuantifierPreference(quantifier);
            case AnchorNode anchor:
                return IsTranslatedAsAlternation(anchor) ? MatchPreference.Longest : MatchPreference.None;
            default:
                return MatchPreference.None;
        }
    }

    private static MatchPreference QuantifierPreference(QuantifierNode quantifier)
    {
        if (quantifier.Min == quantifier.Max)
        {
            return Preference(quantifier.Body);
        }

        return quantifier.Lazy ? MatchPreference.Shortest : MatchPreference.Longest;
    }

    private static bool IsTranslatedAsAlternation(AnchorNode anchor)
    {
        bool multilineStart = anchor.AnchorKind == AnchorKind.StartOfLine && (anchor.Options & RegexOptions.Multiline) != 0;
        bool boundary = anchor.AnchorKind == AnchorKind.WordBoundary || anchor.AnchorKind == AnchorKind.NonWordBoundary;
        return multilineStart || boundary;
    }
}
