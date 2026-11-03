#nullable enable

using System;
using System.Collections.Generic;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// A deterministic finite automaton over minterms. State 0 is the start state, and a
/// transition to <see cref="Dead"/> means the input can no longer be accepted.
/// </summary>
internal sealed class Dfa
{
    /// <summary>The implicit rejecting sink state.</summary>
    public const int Dead = -1;

    private readonly int[] _transitions;
    private readonly bool[] _accepting;

    public Dfa(int mintermCount, int[] transitions, bool[] accepting)
    {
        MintermCount = mintermCount;
        _transitions = transitions;
        _accepting = accepting;
    }

    /// <summary>The size of the alphabet.</summary>
    public int MintermCount { get; }

    /// <summary>The number of states.</summary>
    public int StateCount => _accepting.Length;

    /// <summary>Returns the state reached from <paramref name="state"/> on <paramref name="minterm"/>.</summary>
    public int Next(int state, int minterm) => state == Dead ? Dead : _transitions[(state * MintermCount) + minterm];

    /// <summary>Returns true when <paramref name="state"/> accepts.</summary>
    public bool IsAccepting(int state) => state != Dead && _accepting[state];

    /// <summary>Returns true when no string is accepted.</summary>
    public bool IsEmpty()
    {
        bool[] visited = new bool[StateCount];
        Queue<int> queue = new Queue<int>();
        visited[0] = true;
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int state = queue.Dequeue();
            if (_accepting[state])
            {
                return false;
            }
            for (int m = 0; m < MintermCount; m++)
            {
                int next = Next(state, m);
                if (next != Dead && !visited[next])
                {
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }
        }
        return true;
    }

    /// <summary>Throws when an automaton grows beyond <see cref="AlgebraLimits.MaxDfaStates"/>.</summary>
    public static void CheckSize(int stateCount)
    {
        if (stateCount > AlgebraLimits.MaxDfaStates)
        {
            throw new InvalidOperationException(
                $"The automaton grew beyond {AlgebraLimits.MaxDfaStates} states; the pattern is too complex for language algebra.");
        }
    }
}
