#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Reads language facts off a minimal DFA: shortest and longest accepted lengths, and the characters
/// every accepted string must start and end with. The prefix is the forced path from the start
/// state; the suffix is the forced path of the reversed automaton, simulated lazily as sets of
/// states so the reversal is never determinized in full.
/// </summary>
internal static class FactsExtractor
{
    private const int NoMinterm = -1;

    /// <summary>Computes exact facts for the full-match language of <paramref name="scopedTree"/>.</summary>
    /// <exception cref="System.NotSupportedException">The pattern is outside the regular subset.</exception>
    /// <exception cref="System.InvalidOperationException">The automaton is too large.</exception>
    public static LanguageFacts Compute(RegexTree scopedTree, MatchScope scope)
    {
        LanguageContext context = new LanguageContext(scopedTree);
        return Compute(context[0], context.Partition, scope);
    }

    /// <summary>Computes exact facts for the language of a minimal DFA.</summary>
    public static LanguageFacts Compute(Dfa dfa, MintermPartition partition, MatchScope scope)
    {
        if (dfa.IsEmpty())
        {
            return new LanguageFacts(scope, 0, 0, string.Empty, string.Empty, acceptsEmpty: false, isEmptyLanguage: true, isExact: true);
        }

        return new LanguageFacts(
            scope,
            ShortestLength(dfa),
            LongestLength(dfa),
            ForcedPrefix(dfa, partition),
            ForcedSuffix(dfa, partition),
            dfa.IsAccepting(0),
            isEmptyLanguage: false,
            isExact: true);
    }

    /// <summary>
    /// Follows transitions from the start state while the state is not accepting and exactly one
    /// single-character minterm leads onward; those characters begin every accepted string.
    /// </summary>
    private static string ForcedPrefix(Dfa dfa, MintermPartition partition)
    {
        StringBuilder path = new StringBuilder();
        HashSet<int> visited = new HashSet<int>();
        int state = 0;
        while (!dfa.IsAccepting(state) && visited.Add(state))
        {
            int only = OnlyLiveMinterm(dfa, state);
            if (only < 0 || partition.Minterms[only].Count != 1)
            {
                break;
            }

            path.Append(partition.Representative(only));
            state = dfa.Next(state, only);
        }

        return path.ToString();
    }

    private static int OnlyLiveMinterm(Dfa dfa, int state)
    {
        int found = NoMinterm;
        for (int m = 0; m < dfa.MintermCount; m++)
        {
            if (dfa.Next(state, m) == Dfa.Dead)
            {
                continue;
            }

            if (found != NoMinterm)
            {
                return NoMinterm;
            }

            found = m;
        }

        return found;
    }

    /// <summary>
    /// Walks backwards from the set of accepting states: while the start state is not in the set and
    /// every transition into the set carries the same single character, that character ends every
    /// accepted string and the walk continues from the sources of those transitions.
    /// </summary>
    private static string ForcedSuffix(Dfa dfa, MintermPartition partition)
    {
        List<KeyValuePair<int, int>>[] incoming = IncomingTransitions(dfa);
        HashSet<int> current = new HashSet<int>();
        for (int s = 0; s < dfa.StateCount; s++)
        {
            if (dfa.IsAccepting(s))
            {
                current.Add(s);
            }
        }

        StringBuilder reversed = new StringBuilder();
        HashSet<string> seen = new HashSet<string>();
        while (!current.Contains(0) && seen.Add(Key(current)))
        {
            int only = OnlyIncomingMinterm(incoming, current);
            if (only < 0 || partition.Minterms[only].Count != 1)
            {
                break;
            }

            reversed.Append(partition.Representative(only));
            current = Sources(incoming, current, only);
        }

        return Reverse(reversed.ToString());
    }

    private static List<KeyValuePair<int, int>>[] IncomingTransitions(Dfa dfa)
    {
        List<KeyValuePair<int, int>>[] incoming = new List<KeyValuePair<int, int>>[dfa.StateCount];
        for (int s = 0; s < dfa.StateCount; s++)
        {
            incoming[s] = new List<KeyValuePair<int, int>>();
        }

        for (int s = 0; s < dfa.StateCount; s++)
        {
            for (int m = 0; m < dfa.MintermCount; m++)
            {
                int target = dfa.Next(s, m);
                if (target != Dfa.Dead)
                {
                    incoming[target].Add(new KeyValuePair<int, int>(s, m));
                }
            }
        }

        return incoming;
    }

    private static int OnlyIncomingMinterm(List<KeyValuePair<int, int>>[] incoming, HashSet<int> targets)
    {
        int found = NoMinterm;
        foreach (int target in targets)
        {
            foreach (KeyValuePair<int, int> edge in incoming[target])
            {
                if (found != NoMinterm && edge.Value != found)
                {
                    return NoMinterm;
                }

                found = edge.Value;
            }
        }

        return found;
    }

    private static HashSet<int> Sources(List<KeyValuePair<int, int>>[] incoming, HashSet<int> targets, int minterm)
    {
        HashSet<int> sources = new HashSet<int>();
        foreach (int target in targets)
        {
            foreach (KeyValuePair<int, int> edge in incoming[target])
            {
                if (edge.Value == minterm)
                {
                    sources.Add(edge.Key);
                }
            }
        }

        return sources;
    }

    private static string Key(HashSet<int> states)
    {
        List<int> sorted = new List<int>(states);
        sorted.Sort();
        return string.Join(",", sorted);
    }

    private static string Reverse(string text)
    {
        char[] chars = text.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    private static int ShortestLength(Dfa dfa)
    {
        int[] distance = new int[dfa.StateCount];
        for (int s = 0; s < distance.Length; s++)
        {
            distance[s] = NoMinterm;
        }

        Queue<int> queue = new Queue<int>();
        distance[0] = 0;
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int state = queue.Dequeue();
            if (dfa.IsAccepting(state))
            {
                return distance[state];
            }

            for (int m = 0; m < dfa.MintermCount; m++)
            {
                int next = dfa.Next(state, m);
                if (next != Dfa.Dead && distance[next] == NoMinterm)
                {
                    distance[next] = distance[state] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// The longest path to an accepting state, or null when the automaton has a cycle (every state of
    /// a minimal DFA is live, so any cycle makes the language unbounded). Kahn's topological order
    /// detects the cycle and orders the dynamic programme without recursion.
    /// </summary>
    private static int? LongestLength(Dfa dfa)
    {
        int[] indegree = new int[dfa.StateCount];
        for (int s = 0; s < dfa.StateCount; s++)
        {
            for (int m = 0; m < dfa.MintermCount; m++)
            {
                int next = dfa.Next(s, m);
                if (next != Dfa.Dead)
                {
                    indegree[next]++;
                }
            }
        }

        List<int> order = TopologicalOrder(dfa, indegree);
        if (order.Count != dfa.StateCount)
        {
            return null;
        }

        return LongestOverOrder(dfa, order);
    }

    private static List<int> TopologicalOrder(Dfa dfa, int[] indegree)
    {
        List<int> order = new List<int>(dfa.StateCount);
        Queue<int> ready = new Queue<int>();
        for (int s = 0; s < dfa.StateCount; s++)
        {
            if (indegree[s] == 0)
            {
                ready.Enqueue(s);
            }
        }

        while (ready.Count > 0)
        {
            int state = ready.Dequeue();
            order.Add(state);
            for (int m = 0; m < dfa.MintermCount; m++)
            {
                int next = dfa.Next(state, m);
                if (next != Dfa.Dead && --indegree[next] == 0)
                {
                    ready.Enqueue(next);
                }
            }
        }

        return order;
    }

    private static int LongestOverOrder(Dfa dfa, List<int> order)
    {
        int[] longest = new int[dfa.StateCount];
        for (int i = order.Count - 1; i >= 0; i--)
        {
            int state = order[i];
            int best = dfa.IsAccepting(state) ? 0 : NoMinterm;
            for (int m = 0; m < dfa.MintermCount; m++)
            {
                int next = dfa.Next(state, m);
                if (next != Dfa.Dead)
                {
                    best = Math.Max(best, longest[next] + 1);
                }
            }

            longest[state] = best;
        }

        return longest[0];
    }
}
