#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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

    protected override void CheckBackreference(BackreferenceNode reference)
    {
        if (InsideLookaround)
        {
            Unsupported("backreference inside a lookaround (PostgreSQL forbids it)", reference);
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

    protected override void AddSummaryWarnings()
    {
        List<QuantifierNode> quantifiers = Plan.Nodes.OfType<QuantifierNode>().Where(q => q.Min != q.Max).ToList();
        if (quantifiers.Any(q => q.Lazy) && quantifiers.Any(q => !q.Lazy))
        {
            Warn("The pattern mixes greedy and lazy quantifiers. PostgreSQL decides whether the whole match is " +
                 "longest or shortest from the first quantifier, so the extent of a match (and what groups capture) " +
                 "can differ from .NET. Whether a string matches at all is unaffected.");
        }
    }
}
