#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>A lookahead at the very start of a pattern, which constrains the whole input.</summary>
internal sealed class LookaheadConstraint
{
    public LookaheadConstraint(Nfa nfa, bool negated)
    {
        Nfa = nfa;
        Negated = negated;
    }

    /// <summary>The automaton of the lookahead body followed by anything.</summary>
    public Nfa Nfa { get; }

    /// <summary>True for a negative lookahead.</summary>
    public bool Negated { get; }
}

/// <summary>
/// The full-match language of one parsed pattern, as an NFA plus the constraints of any
/// lookaheads at its very start.
/// </summary>
internal sealed class PatternLanguage
{
    private PatternLanguage(RegexTree tree, Nfa main, IReadOnlyList<LookaheadConstraint> constraints)
    {
        Tree = tree;
        Main = main;
        Constraints = constraints;
    }

    /// <summary>The parsed pattern.</summary>
    public RegexTree Tree { get; }

    /// <summary>The automaton of the pattern without its leading lookaheads.</summary>
    public Nfa Main { get; }

    /// <summary>The leading lookaheads.</summary>
    public IReadOnlyList<LookaheadConstraint> Constraints { get; }

    /// <summary>Builds the language of a parsed pattern.</summary>
    /// <exception cref="NotSupportedException">The pattern uses a non-regular construct.</exception>
    public static PatternLanguage From(RegexTree tree)
    {
        if ((tree.Options & RegexOptions.RightToLeft) != 0)
        {
            throw new NotSupportedException("Language algebra does not support RegexOptions.RightToLeft.");
        }

        List<RegexNode> main = new List<RegexNode>();
        List<LookaheadConstraint> constraints = new List<LookaheadConstraint>();
        bool leading = true;
        foreach (RegexNode item in RegexWriter.ItemsOf(tree.Root))
        {
            GroupNode? lookahead = leading ? AsLookahead(item) : null;
            if (lookahead != null)
            {
                Nfa body = NfaBuilder.Build(RegexWriter.ItemsOf(lookahead.Body), openEnd: true);
                constraints.Add(new LookaheadConstraint(body, lookahead.GroupKind == GroupKind.NegativeLookahead));
                continue;
            }
            leading = leading && IsZeroWidthPrefix(item);
            main.Add(item);
        }
        return new PatternLanguage(tree, NfaBuilder.Build(main, openEnd: false), constraints);
    }

    /// <summary>Enumerates every character set used by the pattern's automata.</summary>
    public IEnumerable<CharSet> CharSets()
    {
        foreach (CharSet set in Main.CharSets())
        {
            yield return set;
        }
        foreach (LookaheadConstraint constraint in Constraints)
        {
            foreach (CharSet set in constraint.Nfa.CharSets())
            {
                yield return set;
            }
        }
    }

    /// <summary>Builds the minimal DFA of the language over the given alphabet.</summary>
    public Dfa ToDfa(MintermPartition partition)
    {
        Dfa result = DfaOperations.Minimize(Determinizer.Determinize(Main, partition));
        foreach (LookaheadConstraint constraint in Constraints)
        {
            Dfa constraintDfa = DfaOperations.Minimize(Determinizer.Determinize(constraint.Nfa, partition));
            ProductOperation operation = constraint.Negated ? ProductOperation.Except : ProductOperation.Intersect;
            result = DfaOperations.Product(result, constraintDfa, operation);
        }
        return result;
    }

    private static GroupNode? AsLookahead(RegexNode item)
    {
        bool isLookahead = item is GroupNode group &&
                           (group.GroupKind == GroupKind.Lookahead || group.GroupKind == GroupKind.NegativeLookahead);
        return isLookahead ? (GroupNode)item : null;
    }

    private static bool IsZeroWidthPrefix(RegexNode item)
    {
        if (item is AnchorNode anchor)
        {
            return anchor.AnchorKind == AnchorKind.StartOfInput || anchor.AnchorKind == AnchorKind.StartOfLine;
        }
        return item is InlineOptionsNode || item is CommentNode || item is EmptyNode;
    }
}
