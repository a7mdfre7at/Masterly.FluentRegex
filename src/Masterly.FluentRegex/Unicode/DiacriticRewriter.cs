#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Unicode;

/// <summary>
/// Rewrites a parsed pattern so it matches text with or without diacritics. Each literal letter
/// becomes a class of its base letter and every precomposed variant, followed by a run of
/// non-spacing marks and Arabic tatweel, so both NFC and NFD input match.
/// </summary>
internal sealed class DiacriticRewriter
{
    private const string MarkRun = @"[\p{Mn}\u0640]*";
    private const string AtomicMarkRun = "(?>" + MarkRun + ")";
    private const string MarkSource = @"[\p{Mn}\u0640]";
    private const string LetterSource = @"\p{L}";
    private const char Tatweel = '\u0640';
    private const char ClassClose = ']';

    private readonly StringBuilder _sb = new StringBuilder();
    private readonly DiacriticTable _table = DiacriticTable.Instance;
    private readonly bool _nonBacktracking;

    private DiacriticRewriter(bool nonBacktracking)
    {
        _nonBacktracking = nonBacktracking;
    }

    /// <summary>Returns the diacritic-insensitive regex text for <paramref name="tree"/>.</summary>
    /// <exception cref="InvalidOperationException">The pattern uses <see cref="RegexOptions.ECMAScript"/>.</exception>
    public static string Rewrite(RegexTree tree)
    {
        if ((tree.Options & RegexOptions.ECMAScript) != 0)
        {
            throw new InvalidOperationException(
                "IgnoreDiacritics needs Unicode categories, which RegexOptions.ECMAScript does not support.");
        }

        DiacriticRewriter rewriter = new DiacriticRewriter(IsNonBacktracking(tree.Options));
        rewriter.Write(tree.Root);
        return rewriter._sb.ToString();
    }

    private static bool IsNonBacktracking(RegexOptions options)
    {
#if NET7_0_OR_GREATER
        return (options & RegexOptions.NonBacktracking) != 0;
#else
        return false;
#endif
    }

    private void Write(RegexNode node)
    {
        switch (node)
        {
            case SequenceNode sequence:
                WriteAll(sequence.Items);
                break;
            case AlternationNode alternation:
                WriteAlternation(alternation);
                break;
            case GroupNode group:
                _sb.Append(group.OpenText);
                Write(group.Body);
                _sb.Append(')');
                break;
            case QuantifierNode quantifier:
                WriteQuantifier(quantifier);
                break;
            case ConditionalNode conditional:
                WriteConditional(conditional);
                break;
            case LiteralNode literal:
                WriteLiteral(literal);
                break;
            case CharClassNode charClass:
                WriteClass(charClass);
                break;
            default:
                _sb.Append(node.Source);
                break;
        }
    }

    private void WriteAll(IEnumerable<RegexNode> nodes)
    {
        foreach (RegexNode node in nodes)
        {
            Write(node);
        }
    }

    private void WriteAlternation(AlternationNode alternation)
    {
        for (int i = 0; i < alternation.Branches.Count; i++)
        {
            if (i > 0)
            {
                _sb.Append('|');
            }

            Write(alternation.Branches[i]);
        }
    }

    private void WriteConditional(ConditionalNode conditional)
    {
        _sb.Append(conditional.OpenText);
        Write(conditional.Yes);
        if (conditional.No != null)
        {
            _sb.Append('|');
            Write(conditional.No);
        }

        _sb.Append(')');
    }

    private void WriteQuantifier(QuantifierNode quantifier)
    {
        if (quantifier.Body is LiteralNode literal && IsCoveredByMarkRun(literal.Value))
        {
            // A repeated mark is already matched by the mark run after the preceding letter.
            return;
        }

        int start = _sb.Length;
        Write(quantifier.Body);
        string rendered = _sb.ToString(start, _sb.Length - start);
        bool isAtom = quantifier.Body is LiteralNode || quantifier.Body is CharClassNode;
        if (isAtom && rendered != quantifier.Body.Source)
        {
            _sb.Insert(start, "(?:").Append(')');
        }

        _sb.Append(quantifier.QuantifierText);
    }

    private void WriteLiteral(LiteralNode literal)
    {
        char c = literal.Value;
        if (IsCoveredByMarkRun(c))
        {
            return;
        }

        if (DiacriticTable.IsMark(c))
        {
            _sb.Append("(?:").Append(literal.Source).Append(")?");
            return;
        }

        char baseLetter = _table.BaseOf(c);
        if (!char.IsLetter(baseLetter))
        {
            _sb.Append(literal.Source);
            return;
        }

        SortedSet<char> members = new SortedSet<char>();
        AddFamily(members, baseLetter, IgnoresCase(literal));
        AppendMembers(members);
        _sb.Append(MarkRun);
    }

    private void WriteClass(CharClassNode charClass)
    {
        _sb.Append(ExpandBracket(charClass) ?? charClass.Source);

        CharSet? set = CharSet.ForNode(charClass);
        if (set == null || !set.Overlaps(CharSet.ForSource(LetterSource, RegexOptions.None)))
        {
            return;
        }

        // A class that can itself match a mark would make the mark run ambiguous, so the run is
        // made atomic to keep matching linear. The non-backtracking engine needs no such guard.
        bool ambiguous = set.Overlaps(CharSet.ForSource(MarkSource, RegexOptions.None));
        _sb.Append(ambiguous && !_nonBacktracking ? AtomicMarkRun : MarkRun);
    }

    private string? ExpandBracket(CharClassNode charClass)
    {
        BracketClass? bracket = charClass.Bracket;
        string source = charClass.Source;
        bool expandable = bracket != null && !bracket.Negated && bracket.Subtraction == null &&
                          source[source.Length - 1] == ClassClose;
        if (!expandable)
        {
            return null;
        }

        // The null-forgiving operator is safe: expandable is false whenever bracket is null.
        SortedSet<char> extras = CollectExtras(bracket!, IgnoresCase(charClass));
        if (extras.Count == 0)
        {
            return null;
        }

        return $"{source.Substring(0, source.Length - 1)}{ScriptClasses.RenderRanges(ToRanges(extras))}{ClassClose}";
    }

    private SortedSet<char> CollectExtras(BracketClass bracket, bool ignoreCase)
    {
        SortedSet<char> extras = new SortedSet<char>();
        foreach (ClassItem item in bracket.Items)
        {
            if (item.ShorthandKind != null)
            {
                continue;
            }

            for (int code = item.Low; code <= item.High; code++)
            {
                AddLetterFamily(extras, (char)code, ignoreCase);
            }
        }

        extras.RemoveWhere(c => Covers(bracket, c));
        return extras;
    }

    private void AddLetterFamily(SortedSet<char> members, char c, bool ignoreCase)
    {
        char baseLetter = _table.BaseOf(c);
        if (char.IsLetter(baseLetter))
        {
            AddFamily(members, baseLetter, ignoreCase);
        }
    }

    private void AddFamily(SortedSet<char> members, char baseLetter, bool ignoreCase)
    {
        AddVariants(members, baseLetter);
        if (ignoreCase)
        {
            AddVariants(members, char.ToUpperInvariant(baseLetter));
            AddVariants(members, char.ToLowerInvariant(baseLetter));
        }
    }

    private void AddVariants(SortedSet<char> members, char baseLetter)
    {
        members.Add(baseLetter);
        members.UnionWith(_table.VariantsOf(baseLetter));
    }

    private void AppendMembers(SortedSet<char> members)
    {
        if (members.Count == 1)
        {
            AppendChar(members.Min);
            return;
        }

        _sb.Append('[').Append(ScriptClasses.RenderRanges(ToRanges(members))).Append(ClassClose);
    }

    private void AppendChar(char c) => ScriptClasses.AppendClassChar(_sb, c);

    private static List<CodeRange> ToRanges(SortedSet<char> members)
    {
        List<CodeRange> ranges = new List<CodeRange>();
        foreach (char c in members)
        {
            int last = ranges.Count - 1;
            if (last >= 0 && ranges[last].High + 1 == c)
            {
                ranges[last] = new CodeRange(ranges[last].Low, c);
            }
            else
            {
                ranges.Add(new CodeRange(c, c));
            }
        }

        return ranges;
    }

    private static bool Covers(BracketClass bracket, char c)
    {
        foreach (ClassItem item in bracket.Items)
        {
            if (item.ShorthandKind == null && c >= item.Low && c <= item.High)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IgnoresCase(RegexNode node) => (node.Options & RegexOptions.IgnoreCase) != 0;

    private static bool IsCoveredByMarkRun(char c) =>
        c == Tatweel || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark;
}
