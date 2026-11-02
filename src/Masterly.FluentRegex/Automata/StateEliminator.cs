#nullable enable

using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Converts a DFA into an equivalent regular expression by state elimination, removing the
/// states with the fewest connections first so the result stays small.
/// </summary>
internal sealed class StateEliminator
{
    private readonly TermFactory _terms;
    private readonly Dictionary<int, Dictionary<int, RegexTerm>> _outgoing = new Dictionary<int, Dictionary<int, RegexTerm>>();
    private readonly Dictionary<int, HashSet<int>> _incoming = new Dictionary<int, HashSet<int>>();
    private readonly int _initial;
    private readonly int _final;

    private StateEliminator(Dfa dfa, MintermPartition partition, TermFactory terms)
    {
        _terms = terms;
        _initial = dfa.StateCount;
        _final = dfa.StateCount + 1;
        for (int state = 0; state < dfa.StateCount + 2; state++)
        {
            _outgoing[state] = new Dictionary<int, RegexTerm>();
            _incoming[state] = new HashSet<int>();
        }

        AddEdge(_initial, 0, terms.Empty);
        for (int state = 0; state < dfa.StateCount; state++)
        {
            AddCharEdges(dfa, partition, state);
            if (dfa.IsAccepting(state))
            {
                AddEdge(state, _final, terms.Empty);
            }
        }
    }

    /// <summary>Returns a term whose language is the language of <paramref name="dfa"/>.</summary>
    public static RegexTerm ToTerm(Dfa dfa, MintermPartition partition, TermFactory terms)
    {
        StateEliminator eliminator = new StateEliminator(dfa, partition, terms);
        return eliminator.Run(dfa.StateCount);
    }

    private void AddCharEdges(Dfa dfa, MintermPartition partition, int state)
    {
        Dictionary<int, List<int>> mintermsByTarget = new Dictionary<int, List<int>>();
        for (int m = 0; m < dfa.MintermCount; m++)
        {
            int target = dfa.Next(state, m);
            if (target == Dfa.Dead)
            {
                continue;
            }
            if (!mintermsByTarget.TryGetValue(target, out List<int>? minterms))
            {
                minterms = new List<int>();
                mintermsByTarget[target] = minterms;
            }
            minterms.Add(m);
        }

        foreach (KeyValuePair<int, List<int>> pair in mintermsByTarget)
        {
            CharSet set = partition.UnionOf(pair.Value);
            AddEdge(state, pair.Key, _terms.Class(set));
        }
    }

    private void AddEdge(int from, int to, RegexTerm label)
    {
        if (label.Kind == TermKind.Nothing)
        {
            return;
        }
        Dictionary<int, RegexTerm> edges = _outgoing[from];
        edges[to] = edges.TryGetValue(to, out RegexTerm? existing) ? _terms.Alternation(existing, label) : label;
        _incoming[to].Add(from);
    }

    private RegexTerm Run(int stateCount)
    {
        HashSet<int> remaining = new HashSet<int>();
        for (int state = 0; state < stateCount; state++)
        {
            remaining.Add(state);
        }

        while (remaining.Count > 0)
        {
            int victim = ChooseVictim(remaining);
            Eliminate(victim);
            remaining.Remove(victim);
        }

        return _outgoing[_initial].TryGetValue(_final, out RegexTerm? result) ? result : _terms.Nothing;
    }

    private int ChooseVictim(HashSet<int> remaining)
    {
        int best = -1;
        long bestCost = long.MaxValue;
        foreach (int state in remaining)
        {
            long cost = Cost(state);
            if (cost < bestCost || (cost == bestCost && state < best))
            {
                best = state;
                bestCost = cost;
            }
        }
        return best;
    }

    private long Cost(int state)
    {
        int ins = _incoming[state].Count - (_incoming[state].Contains(state) ? 1 : 0);
        int outs = _outgoing[state].Count - (_outgoing[state].ContainsKey(state) ? 1 : 0);
        long weight = 0;
        foreach (RegexTerm label in _outgoing[state].Values)
        {
            weight += label.Size;
        }
        return ((long)ins * outs * AlgebraLimits.MaxTermSize) + weight;
    }

    private void Eliminate(int state)
    {
        Dictionary<int, RegexTerm> outgoing = _outgoing[state];
        RegexTerm loop = outgoing.TryGetValue(state, out RegexTerm? self) ? _terms.Star(self) : _terms.Empty;
        List<int> predecessors = new List<int>(_incoming[state]);
        List<KeyValuePair<int, RegexTerm>> successors = new List<KeyValuePair<int, RegexTerm>>(outgoing);

        foreach (int predecessor in predecessors)
        {
            if (predecessor == state)
            {
                continue;
            }
            RegexTerm into = _outgoing[predecessor][state];
            _outgoing[predecessor].Remove(state);
            foreach (KeyValuePair<int, RegexTerm> successor in successors)
            {
                if (successor.Key != state)
                {
                    AddEdge(predecessor, successor.Key, _terms.Concat(into, loop, successor.Value));
                }
            }
        }

        foreach (KeyValuePair<int, RegexTerm> successor in successors)
        {
            _incoming[successor.Key].Remove(state);
        }
        _outgoing.Remove(state);
        _incoming.Remove(state);
    }
}
