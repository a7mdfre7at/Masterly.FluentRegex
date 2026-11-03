#nullable enable

using System;
using System.Collections.Generic;

namespace Masterly.FluentRegex.Automata;

/// <summary>Converts an NFA into a DFA over minterms with the subset construction.</summary>
internal sealed class Determinizer
{
    private readonly Nfa _nfa;
    private readonly MintermPartition _partition;
    private readonly int[] _marks;
    private readonly Stack<int> _stack = new Stack<int>();
    private readonly Dictionary<int[], int> _ids = new Dictionary<int[], int>(IntArrayComparer.Instance);
    private readonly List<int[]> _subsets = new List<int[]>();
    private int _generation;

    private Determinizer(Nfa nfa, MintermPartition partition)
    {
        _nfa = nfa;
        _partition = partition;
        _marks = new int[nfa.States.Count];
    }

    /// <summary>Builds the DFA of <paramref name="nfa"/>.</summary>
    public static Dfa Determinize(Nfa nfa, MintermPartition partition)
    {
        return new Determinizer(nfa, partition).Run();
    }

    private Dfa Run()
    {
        int mintermCount = _partition.Count;
        List<int> transitions = new List<int>();
        List<bool> accepting = new List<bool>();
        List<int>[] buckets = new List<int>[mintermCount];
        for (int m = 0; m < mintermCount; m++)
        {
            buckets[m] = new List<int>();
        }

        IdOf(Closure(new List<int> { _nfa.Start }));
        for (int index = 0; index < _subsets.Count; index++)
        {
            int[] subset = _subsets[index];
            accepting.Add(IsAccepting(subset));
            FillBuckets(subset, buckets);
            for (int m = 0; m < mintermCount; m++)
            {
                transitions.Add(buckets[m].Count == 0 ? Dfa.Dead : IdOf(Closure(buckets[m])));
            }
        }
        return new Dfa(mintermCount, transitions.ToArray(), accepting.ToArray());
    }

    private bool IsAccepting(int[] subset)
    {
        foreach (int state in subset)
        {
            if (_nfa.States[state].IsAccepting)
            {
                return true;
            }
        }
        return false;
    }

    private void FillBuckets(int[] subset, List<int>[] buckets)
    {
        foreach (List<int> bucket in buckets)
        {
            bucket.Clear();
        }
        foreach (int state in subset)
        {
            foreach (NfaCharEdge edge in _nfa.States[state].CharEdges)
            {
                foreach (int minterm in _partition.MintermsOf(edge.Set))
                {
                    buckets[minterm].Add(edge.Target);
                }
            }
        }
    }

    private int IdOf(int[] subset)
    {
        if (_ids.TryGetValue(subset, out int id))
        {
            return id;
        }
        id = _subsets.Count;
        Dfa.CheckSize(id + 1);
        _ids[subset] = id;
        _subsets.Add(subset);
        return id;
    }

    /// <summary>
    /// Returns the epsilon closure of the seeds, keeping only states that matter for
    /// acceptance or transitions, sorted so equal subsets compare equal.
    /// </summary>
    private int[] Closure(List<int> seeds)
    {
        _generation++;
        if (_generation == int.MaxValue)
        {
            Array.Clear(_marks, 0, _marks.Length);
            _generation = 1;
        }

        List<int> important = new List<int>();
        foreach (int seed in seeds)
        {
            Visit(seed);
        }
        while (_stack.Count > 0)
        {
            int state = _stack.Pop();
            NfaState nfaState = _nfa.States[state];
            if (nfaState.IsAccepting || nfaState.CharEdges.Count > 0)
            {
                important.Add(state);
            }
            foreach (int next in nfaState.Epsilon)
            {
                Visit(next);
            }
        }
        important.Sort();
        return important.ToArray();
    }

    private void Visit(int state)
    {
        if (_marks[state] != _generation)
        {
            _marks[state] = _generation;
            _stack.Push(state);
        }
    }
}
