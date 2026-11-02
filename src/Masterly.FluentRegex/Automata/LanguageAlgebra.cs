#nullable enable

using System;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Language algebra over parsed patterns: comparison, emptiness, shortest members and boolean
/// combinations. Every witness string is confirmed with the real .NET engine, and every
/// synthesized regex is proven equivalent to the automaton it came from.
/// </summary>
internal static class LanguageAlgebra
{
    /// <summary>Compares the full-match languages of two patterns.</summary>
    public static PatternComparison Compare(RegexTree left, RegexTree right)
    {
        LanguageContext context = new LanguageContext(left, right);
        ComparisonWitnesses witnesses = WitnessSearch.Compare(context[0], context[1], context.Partition);
        MembershipOracle leftOracle = new MembershipOracle(left);
        MembershipOracle rightOracle = new MembershipOracle(right);
        Confirm(witnesses.OnlyLeft, leftOracle, rightOracle, expectLeft: true, expectRight: false);
        Confirm(witnesses.OnlyRight, leftOracle, rightOracle, expectLeft: false, expectRight: true);
        Confirm(witnesses.Common, leftOracle, rightOracle, expectLeft: true, expectRight: true);
        return new PatternComparison(witnesses.OnlyLeft, witnesses.OnlyRight, witnesses.Common);
    }

    /// <summary>Returns the shortest string the pattern matches in full, or null when it matches nothing.</summary>
    public static string? ShortestMatch(RegexTree tree)
    {
        LanguageContext context = new LanguageContext(tree);
        string? shortest = WitnessSearch.Shortest(context[0], context.Partition);
        if (shortest != null && !new MembershipOracle(tree).Accepts(shortest))
        {
            throw Inconsistency(shortest, "the pattern accepts it");
        }
        return shortest;
    }

    /// <summary>Builds full-match regex text for a boolean combination of two languages.</summary>
    public static string Combine(RegexTree left, RegexTree right, ProductOperation operation)
    {
        if (operation == ProductOperation.Union && left.Options == right.Options)
        {
            string? alternation = TryAlternation(left, right);
            if (alternation != null)
            {
                return alternation;
            }
        }

        LanguageContext context = new LanguageContext(left, right);
        Dfa target = DfaOperations.Product(context[0], context[1], operation);
        TermFactory terms = new TermFactory();
        RegexTerm term = StateEliminator.ToTerm(target, context.Partition, terms);
        string text = new RegexTermWriter(left.Options).WriteAnchored(term, terms);
        if (!IsCombination(text, left, right, operation))
        {
            throw new InvalidOperationException(
                $"Internal inconsistency: the synthesized regex '{text}' does not match the combined language. Please report this pattern.");
        }
        return text;
    }

    private static string? TryAlternation(RegexTree left, RegexTree right)
    {
        string text = $"{AsBranch(left.Root)}|{AsBranch(right.Root)}";
        try
        {
            return IsCombination(text, left, right, ProductOperation.Union) ? text : null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static string AsBranch(RegexNode root)
    {
        string text = RegexWriter.Write(root);
        bool needsGroup = root is AlternationNode || root is EmptyNode || ContainsInlineOptions(root);
        return needsGroup ? $"(?:{text})" : text;
    }

    private static bool ContainsInlineOptions(RegexNode root)
    {
        foreach (RegexNode item in RegexWriter.ItemsOf(root))
        {
            if (item is InlineOptionsNode)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsCombination(string text, RegexTree left, RegexTree right, ProductOperation operation)
    {
        RegexTree result = RegexParser.Parse(text, left.Options);
        LanguageContext context = new LanguageContext(left, right, result);
        Dfa target = DfaOperations.Product(context[0], context[1], operation);
        ComparisonWitnesses witnesses = WitnessSearch.Compare(target, context[2], context.Partition);
        return witnesses.OnlyLeft == null && witnesses.OnlyRight == null;
    }

    private static void Confirm(string? witness, MembershipOracle left, MembershipOracle right, bool expectLeft, bool expectRight)
    {
        if (witness == null)
        {
            return;
        }
        if (left.Accepts(witness) != expectLeft)
        {
            throw Inconsistency(witness, expectLeft ? "the left pattern accepts it" : "the left pattern rejects it");
        }
        if (right.Accepts(witness) != expectRight)
        {
            throw Inconsistency(witness, expectRight ? "the right pattern accepts it" : "the right pattern rejects it");
        }
    }

    private static InvalidOperationException Inconsistency(string witness, string claim) =>
        new InvalidOperationException(
            $"Internal inconsistency: the automaton claims {claim} for {PatternComparison.Quote(witness)}, " +
            "but the .NET regex engine disagrees. Please report this pattern.");
}
