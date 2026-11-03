#nullable enable

using System.Collections.Generic;

namespace Masterly.FluentRegex.Automata;

/// <summary>How a product automaton combines the acceptance of its two operands.</summary>
internal enum ProductOperation
{
    /// <summary>Accepts what both operands accept.</summary>
    Intersect,

    /// <summary>Accepts what the left operand accepts and the right one rejects.</summary>
    Except,

    /// <summary>Accepts what either operand accepts.</summary>
    Union
}

/// <summary>Minimization and boolean combination of DFAs that share a minterm alphabet.</summary>
internal static class DfaOperations
{
    private const int RejectingClass = 0;
    private const int AcceptingClass = 1;

    /// <summary>Returns the minimal DFA for the same language, with useless states removed.</summary>
    public static Dfa Minimize(Dfa dfa)
    {
        bool[] live = CoReachable(dfa);
        if (!live[0])
        {
            return EmptyLanguage(dfa.MintermCount);
        }

        int[] classes = InitialClasses(dfa, live);
        int classCount = CountClasses(classes);
        while (true)
        {
            int[] refined = Refine(dfa, live, classes, out int refinedCount);
            classes = refined;
            if (refinedCount == classCount)
            {
                break;
            }
            classCount = refinedCount;
        }
        return Rebuild(dfa, live, classes);
    }

    /// <summary>Returns the DFA that accepts no string.</summary>
    public static Dfa EmptyLanguage(int mintermCount)
    {
        int[] transitions = new int[mintermCount];
        for (int m = 0; m < mintermCount; m++)
        {
            transitions[m] = Dfa.Dead;
        }
        return new Dfa(mintermCount, transitions, new[] { false });
    }

    /// <summary>Builds the minimal product automaton of two DFAs.</summary>
    public static Dfa Product(Dfa left, Dfa right, ProductOperation operation)
    {
        int mintermCount = left.MintermCount;
        Dictionary<long, int> ids = new Dictionary<long, int>();
        List<KeyValuePair<int, int>> pairs = new List<KeyValuePair<int, int>>();
        List<int> transitions = new List<int>();
        List<bool> accepting = new List<bool>();

        PairId(0, 0, ids, pairs);
        for (int index = 0; index < pairs.Count; index++)
        {
            int l = pairs[index].Key;
            int r = pairs[index].Value;
            accepting.Add(Accepts(left.IsAccepting(l), right.IsAccepting(r), operation));
            for (int m = 0; m < mintermCount; m++)
            {
                int nl = left.Next(l, m);
                int nr = right.Next(r, m);
                transitions.Add(IsDead(nl, nr, operation) ? Dfa.Dead : PairId(nl, nr, ids, pairs));
            }
        }
        return Minimize(new Dfa(mintermCount, transitions.ToArray(), accepting.ToArray()));
    }

    private static bool Accepts(bool left, bool right, ProductOperation operation)
    {
        switch (operation)
        {
            case ProductOperation.Intersect:
                return left && right;
            case ProductOperation.Except:
                return left && !right;
            default:
                return left || right;
        }
    }

    private static bool IsDead(int left, int right, ProductOperation operation)
    {
        switch (operation)
        {
            case ProductOperation.Intersect:
                return left == Dfa.Dead || right == Dfa.Dead;
            case ProductOperation.Except:
                return left == Dfa.Dead;
            default:
                return left == Dfa.Dead && right == Dfa.Dead;
        }
    }

    private static int PairId(int left, int right, Dictionary<long, int> ids, List<KeyValuePair<int, int>> pairs)
    {
        long key = ((long)(left + 1) << 32) | (uint)(right + 1);
        if (ids.TryGetValue(key, out int id))
        {
            return id;
        }
        id = pairs.Count;
        Dfa.CheckSize(id + 1);
        ids[key] = id;
        pairs.Add(new KeyValuePair<int, int>(left, right));
        return id;
    }

    private static bool[] CoReachable(Dfa dfa)
    {
        List<int>[] predecessors = new List<int>[dfa.StateCount];
        bool[] live = new bool[dfa.StateCount];
        Queue<int> queue = new Queue<int>();
        for (int s = 0; s < dfa.StateCount; s++)
        {
            predecessors[s] = new List<int>();
        }
        for (int s = 0; s < dfa.StateCount; s++)
        {
            for (int m = 0; m < dfa.MintermCount; m++)
            {
                int next = dfa.Next(s, m);
                if (next != Dfa.Dead)
                {
                    predecessors[next].Add(s);
                }
            }
            if (dfa.IsAccepting(s))
            {
                live[s] = true;
                queue.Enqueue(s);
            }
        }

        while (queue.Count > 0)
        {
            foreach (int previous in predecessors[queue.Dequeue()])
            {
                if (!live[previous])
                {
                    live[previous] = true;
                    queue.Enqueue(previous);
                }
            }
        }
        return live;
    }

    private static int[] InitialClasses(Dfa dfa, bool[] live)
    {
        int[] classes = new int[dfa.StateCount];
        for (int s = 0; s < dfa.StateCount; s++)
        {
            classes[s] = !live[s] ? Dfa.Dead : dfa.IsAccepting(s) ? AcceptingClass : RejectingClass;
        }
        return classes;
    }

    private static int CountClasses(int[] classes)
    {
        HashSet<int> distinct = new HashSet<int>();
        foreach (int c in classes)
        {
            if (c != Dfa.Dead)
            {
                distinct.Add(c);
            }
        }
        return distinct.Count;
    }

    private static int[] Refine(Dfa dfa, bool[] live, int[] classes, out int classCount)
    {
        Dictionary<int[], int> ids = new Dictionary<int[], int>(IntArrayComparer.Instance);
        int[] refined = new int[dfa.StateCount];
        for (int s = 0; s < dfa.StateCount; s++)
        {
            if (!live[s])
            {
                refined[s] = Dfa.Dead;
                continue;
            }

            int[] signature = new int[dfa.MintermCount + 1];
            signature[0] = classes[s];
            for (int m = 0; m < dfa.MintermCount; m++)
            {
                int next = dfa.Next(s, m);
                signature[m + 1] = next == Dfa.Dead ? Dfa.Dead : classes[next];
            }
            if (!ids.TryGetValue(signature, out int id))
            {
                id = ids.Count;
                ids[signature] = id;
            }
            refined[s] = id;
        }
        classCount = ids.Count;
        return refined;
    }

    private static Dfa Rebuild(Dfa dfa, bool[] live, int[] classes)
    {
        Dictionary<int, int> newIndex = new Dictionary<int, int>();
        List<int> representatives = new List<int>();
        Queue<int> queue = new Queue<int>();
        newIndex[classes[0]] = 0;
        representatives.Add(0);
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int state = queue.Dequeue();
            for (int m = 0; m < dfa.MintermCount; m++)
            {
                int next = dfa.Next(state, m);
                if (next == Dfa.Dead || !live[next] || newIndex.ContainsKey(classes[next]))
                {
                    continue;
                }
                newIndex[classes[next]] = representatives.Count;
                representatives.Add(next);
                queue.Enqueue(next);
            }
        }

        int[] transitions = new int[representatives.Count * dfa.MintermCount];
        bool[] accepting = new bool[representatives.Count];
        for (int i = 0; i < representatives.Count; i++)
        {
            int state = representatives[i];
            accepting[i] = dfa.IsAccepting(state);
            for (int m = 0; m < dfa.MintermCount; m++)
            {
                int next = dfa.Next(state, m);
                transitions[(i * dfa.MintermCount) + m] = next == Dfa.Dead || !live[next] ? Dfa.Dead : newIndex[classes[next]];
            }
        }
        return new Dfa(dfa.MintermCount, transitions, accepting);
    }
}
