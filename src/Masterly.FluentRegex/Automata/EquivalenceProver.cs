#nullable enable

using System;
using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Proves candidates equivalent to one fixed pattern, reusing the fixed pattern's automaton
/// whenever a candidate's character sets fit its alphabet, which is the common case for rewrites.
/// </summary>
internal sealed class EquivalenceProver
{
    private readonly RegexTree _reference;
    private readonly MintermPartition _partition;
    private readonly CharSet _universe;
    private readonly Dfa _referenceDfa;

    /// <summary>Compiles the reference pattern.</summary>
    /// <exception cref="NotSupportedException">The pattern is not regular.</exception>
    /// <exception cref="InvalidOperationException">The pattern is too large.</exception>
    public EquivalenceProver(RegexTree reference)
    {
        _reference = reference;
        PatternLanguage language = PatternLanguage.From(reference);
        _partition = MintermPartition.Build(language.CharSets());
        _universe = _partition.UnionOf(AllMinterms(_partition.Count));
        _referenceDfa = language.ToDfa(_partition);
    }

    /// <summary>Returns true when <paramref name="candidate"/> has exactly the reference language.</summary>
    public bool IsEquivalent(RegexTree candidate)
    {
        PatternLanguage language = PatternLanguage.From(candidate);
        if (FitsAlphabet(language))
        {
            Dfa candidateDfa = language.ToDfa(_partition);
            return AreEqual(_referenceDfa, candidateDfa, _partition);
        }

        LanguageContext context = new LanguageContext(_reference, candidate);
        return AreEqual(context[0], context[1], context.Partition);
    }

    private static bool AreEqual(Dfa left, Dfa right, MintermPartition partition)
    {
        ComparisonWitnesses witnesses = WitnessSearch.Compare(left, right, partition);
        return witnesses.OnlyLeft == null && witnesses.OnlyRight == null;
    }

    private bool FitsAlphabet(PatternLanguage language)
    {
        foreach (CharSet set in language.CharSets())
        {
            if (!set.IsSubsetOf(_universe) || !IsUnionOfMinterms(set))
            {
                return false;
            }
        }
        return true;
    }

    private bool IsUnionOfMinterms(CharSet set)
    {
        foreach (CharSet minterm in _partition.Minterms)
        {
            if (minterm.Overlaps(set) && !minterm.IsSubsetOf(set))
            {
                return false;
            }
        }
        return true;
    }

    private static IEnumerable<int> AllMinterms(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
        }
    }
}
