#nullable enable

using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>A character transition of an NFA, labelled with the exact set of code units it accepts.</summary>
internal readonly struct NfaCharEdge
{
    public NfaCharEdge(CharSet set, int target)
    {
        Set = set;
        Target = target;
    }

    /// <summary>The code units that take this transition.</summary>
    public CharSet Set { get; }

    /// <summary>The state the transition leads to.</summary>
    public int Target { get; }
}

/// <summary>A state of a Thompson NFA.</summary>
internal sealed class NfaState
{
    /// <summary>States reachable without consuming input.</summary>
    public List<int> Epsilon { get; } = new List<int>();

    /// <summary>Transitions that consume one code unit.</summary>
    public List<NfaCharEdge> CharEdges { get; } = new List<NfaCharEdge>();

    /// <summary>Whether reaching this state at the end of the input accepts it.</summary>
    public bool IsAccepting { get; set; }
}

/// <summary>A nondeterministic finite automaton over UTF-16 code units.</summary>
internal sealed class Nfa
{
    public Nfa(IReadOnlyList<NfaState> states, int start)
    {
        States = states;
        Start = start;
    }

    /// <summary>All states, indexed by number.</summary>
    public IReadOnlyList<NfaState> States { get; }

    /// <summary>The initial state.</summary>
    public int Start { get; }

    /// <summary>Enumerates the character sets used by any transition.</summary>
    public IEnumerable<CharSet> CharSets()
    {
        foreach (NfaState state in States)
        {
            foreach (NfaCharEdge edge in state.CharEdges)
            {
                yield return edge.Set;
            }
        }
    }
}
