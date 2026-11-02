#nullable enable

using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Compiles several patterns into DFAs over one shared minterm alphabet, so their
/// automata can be combined and compared directly.
/// </summary>
internal sealed class LanguageContext
{
    private readonly Dfa[] _dfas;

    public LanguageContext(params RegexTree[] trees)
    {
        PatternLanguage[] languages = new PatternLanguage[trees.Length];
        List<CharSet> sets = new List<CharSet>();
        for (int i = 0; i < trees.Length; i++)
        {
            languages[i] = PatternLanguage.From(trees[i]);
            sets.AddRange(languages[i].CharSets());
        }

        Partition = MintermPartition.Build(sets);
        _dfas = new Dfa[trees.Length];
        for (int i = 0; i < trees.Length; i++)
        {
            _dfas[i] = languages[i].ToDfa(Partition);
        }
    }

    /// <summary>The shared alphabet.</summary>
    public MintermPartition Partition { get; }

    /// <summary>Returns the minimal DFA of the pattern at <paramref name="index"/>.</summary>
    public Dfa this[int index] => _dfas[index];
}
