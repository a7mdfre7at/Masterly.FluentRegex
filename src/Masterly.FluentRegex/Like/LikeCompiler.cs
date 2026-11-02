#nullable enable
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Automata;
using Masterly.FluentRegex.Export;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Like;

/// <summary>
/// Compiles a syntax tree into LIKE parts that accept a superset of the pattern's language, by
/// construction: every construct is replaced by something at least as permissive (a class becomes
/// <c>_</c> or a set, an alternation or optional part becomes <c>%</c>, a zero-width assertion
/// becomes nothing), and LIKE concatenation preserves supersets. Whether the result is exact is
/// decided afterwards by the algebra.
/// </summary>
internal sealed class LikeCompiler
{
    private const string DollarWarning =
        "'$' (and '\\Z') also accept a trailing newline, which LIKE can only cover with a trailing '%'; end the pattern with '\\z' for an exact translation.";
    private const string AlternationWarning = "An alternation of more than single characters can only be covered by '%'.";
    private const string OptionalWarning = "A variable repeat (?, *, +, {m,n}) can only be covered by '%' after its mandatory part.";
    private const string ClassWarning = "A character class became '_' (any character); only SQL Server LIKE can express sets.";
    private const string CaseWarning = "A case-insensitive letter under a binary collation became '_'; SQL Server LIKE can express [Aa] sets.";
    private const string NonAsciiWarning = "A set contains non-ASCII characters; they are written literally in the LIKE pattern.";
    private const string NonRegularWarning = "A construct LIKE cannot express (backreference, conditional) became '%'.";
    private const string FoldingWarning =
        "A case-insensitive letter has .NET case variants (such as U+212A KELVIN SIGN for 'k' or U+0130 for 'i') that not every case-insensitive collation folds, so it became a set or '_'.";
    private const string CodePointWarning =
        "Positions that can match a surrogate (such as '.', '[^a]', '\\W', or a lone surrogate) can pair up into one supplementary character, which UTF-8 and supplementary-character-aware collations count as one character, so a run of them became one '_' per two positions plus '%' (on SQL Server under a case-insensitive collation, whose legacy versions give supplementary characters no weight, just '%').";
    private const string SetWarning =
        "A character class became '_' because it cannot be written as a SQL Server set (it would need a negated set under a case-insensitive collation, contains set syntax or surrogates, or has too many ranges).";

    private readonly LikeDialect _dialect;
    private readonly LikeCollation _collation;
    private readonly List<string> _reasons;
    private readonly List<string> _notes;
    private readonly List<LikePart> _parts = new List<LikePart>();

    private LikeCompiler(LikeDialect dialect, LikeCollation collation, List<string> reasons, List<string> notes)
    {
        _dialect = dialect;
        _collation = collation;
        _reasons = reasons;
        _notes = notes;
    }

    /// <summary>
    /// Compiles the scoped language of <paramref name="tree"/>. Reasons the result may be inexact go
    /// to <paramref name="reasons"/>; remarks that hold either way go to <paramref name="notes"/>.
    /// </summary>
    public static IReadOnlyList<LikePart> Compile(RegexTree tree, MatchScope scope, LikeDialect dialect, LikeCollation collation, List<string> reasons, List<string> notes)
    {
        LikeCompiler compiler = new LikeCompiler(dialect, collation, reasons, notes);
        if (scope == MatchScope.Substring && !ScopeAnalysis.IsStartAnchored(tree.Root))
        {
            compiler._parts.Add(LikePart.AnyString);
        }

        compiler.Add(tree.Root);
        if (scope == MatchScope.Substring && !ScopeAnalysis.IsEndAnchored(tree.Root))
        {
            compiler._parts.Add(LikePart.AnyString);
        }

        return compiler.WidenForCodePoints(Normalize(compiler._parts));
    }

    /// <summary>Merges adjacent text and collapses runs of <c>%</c>.</summary>
    public static IReadOnlyList<LikePart> Normalize(IReadOnlyList<LikePart> parts)
    {
        List<LikePart> result = new List<LikePart>();
        foreach (LikePart part in parts)
        {
            LikePart? last = result.Count > 0 ? result[result.Count - 1] : null;
            if (last != null && last.Kind == LikePartKind.AnyString && part.Kind == LikePartKind.AnyString)
            {
                continue;
            }

            if (last != null && last.Kind == LikePartKind.Text && part.Kind == LikePartKind.Text)
            {
                result[result.Count - 1] = LikePart.OfText(last.Text + part.Text);
                continue;
            }

            result.Add(part);
        }

        return result;
    }

    /// <summary>Applies <see cref="LikeCodePoints.Widen"/>, so the result stays a superset when the database counts code points.</summary>
    private IReadOnlyList<LikePart> WidenForCodePoints(IReadOnlyList<LikePart> parts)
    {
        bool noWeightCollation = _dialect == LikeDialect.SqlServer && _collation == LikeCollation.CaseInsensitive;
        IReadOnlyList<LikePart> widened = LikeCodePoints.Widen(parts, noWeightCollation, out bool changed);
        if (!changed)
        {
            return widened;
        }

        Warn(CodePointWarning);
        return Normalize(widened);
    }

    private void Warn(string warning)
    {
        if (!_reasons.Contains(warning))
        {
            _reasons.Add(warning);
        }
    }

    private void Note(string note)
    {
        if (!_notes.Contains(note))
        {
            _notes.Add(note);
        }
    }

    private void Add(RegexNode node)
    {
        switch (node)
        {
            case LiteralNode literal:
                AddLiteral(literal);
                break;
            case CharClassNode charClass:
                AddSet(CharSet.ForNode(charClass) ?? CharSet.Empty, ClassWarning);
                break;
            case SequenceNode sequence:
                AddAll(sequence.Items);
                break;
            case AlternationNode alternation:
                AddAlternation(alternation);
                break;
            case GroupNode group:
                AddGroup(group);
                break;
            case QuantifierNode quantifier:
                AddQuantifier(quantifier);
                break;
            case AnchorNode anchor:
                AddAnchor(anchor);
                break;
            case BackreferenceNode _:
            case ConditionalNode _:
                Warn(NonRegularWarning);
                _parts.Add(LikePart.AnyString);
                break;
            default:
                break; // empty nodes, inline options and comments consume nothing
        }
    }

    private void AddAll(IEnumerable<RegexNode> items)
    {
        foreach (RegexNode item in items)
        {
            Add(item);
        }
    }

    private void AddLiteral(LiteralNode literal)
    {
        bool caseVaries = (literal.Options & RegexOptions.IgnoreCase) != 0 && TranslationPlan.IsCaseSensitive(literal);
        if (!caseVaries)
        {
            _parts.Add(LikePart.OfText(literal.Value.ToString()));
            return;
        }

        CharSet variants = CharSet.ForNode(literal) ?? CharSet.Empty;
        if (_collation == LikeCollation.Binary)
        {
            AddSet(variants, CaseWarning);
            return;
        }

        if (LikeCaseFolding.CoversVariants(literal.Value, variants))
        {
            _parts.Add(LikePart.OfText(literal.Value.ToString())); // every case-insensitive collation folds these itself
            return;
        }

        AddSet(variants, FoldingWarning);
    }

    /// <summary>Adds a single-character part: the exact set when the dialect can write it, otherwise <c>_</c>.</summary>
    private void AddSet(CharSet set, string warning)
    {
        if (SqlCharWriter.IsEverything(set))
        {
            _parts.Add(LikePart.AnyCharFor(set));
            return;
        }

        if (LikeWriter.CanWriteSet(set, _dialect, _collation, out bool nonAscii))
        {
            if (nonAscii)
            {
                Note(NonAsciiWarning);
            }

            _parts.Add(LikePart.OfSet(set));
            return;
        }

        Warn(_dialect == LikeDialect.SqlServer ? SetWarning : warning);
        _parts.Add(LikePart.AnyCharFor(set));
    }

    private void AddAlternation(AlternationNode alternation)
    {
        CharSet? union = SingleCharacterUnion(alternation);
        if (union != null)
        {
            AddSet(union, ClassWarning);
            return;
        }

        Warn(AlternationWarning);
        _parts.Add(LikePart.AnyString);
    }

    /// <summary>The union of the branches when each compiles to exactly one single-character part.</summary>
    private CharSet? SingleCharacterUnion(AlternationNode alternation)
    {
        CharSet union = CharSet.Empty;
        foreach (RegexNode branch in alternation.Branches)
        {
            LikeCompiler inner = new LikeCompiler(_dialect, _collation, new List<string>(), _notes);
            inner.Add(branch);
            if (inner._parts.Count != 1 || !inner._parts[0].IsSingleCharacter)
            {
                return null;
            }

            union = union.Union(inner._parts[0].SingleCharacterSet());
        }

        return union;
    }

    private void AddGroup(GroupNode group)
    {
        if (group.IsLookaround)
        {
            return; // zero-width: dropping a constraint only widens the language
        }

        Add(group.Body);
    }

    private void AddQuantifier(QuantifierNode quantifier)
    {
        for (int i = 0; i < quantifier.Min; i++)
        {
            Add(quantifier.Body);
        }

        if (quantifier.Min == quantifier.Max)
        {
            return;
        }

        bool anyCharStar = quantifier.Min == 0 && quantifier.IsUnbounded && MatchesEverything(quantifier.Body);
        if (!anyCharStar)
        {
            Warn(OptionalWarning);
        }

        _parts.Add(LikePart.AnyString);
    }

    private static bool MatchesEverything(RegexNode body)
    {
        CharSet? set = CharSet.ForNode(body);
        return set != null && SqlCharWriter.IsEverything(set);
    }

    private void AddAnchor(AnchorNode anchor)
    {
        bool lenientEnd = anchor.AnchorKind == AnchorKind.EndOfInputOrNewline ||
                          (anchor.AnchorKind == AnchorKind.EndOfLine && (anchor.Options & RegexOptions.Multiline) == 0);
        if (lenientEnd)
        {
            Warn(DollarWarning);
            _parts.Add(LikePart.AnyString);
        }
    }
}
