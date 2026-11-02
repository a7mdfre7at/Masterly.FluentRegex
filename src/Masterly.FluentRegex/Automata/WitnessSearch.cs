#nullable enable

using System.Collections.Generic;
using System.Text;

namespace Masterly.FluentRegex.Automata;

/// <summary>The shortest strings that separate or join two languages.</summary>
internal sealed class ComparisonWitnesses
{
    public ComparisonWitnesses(string? onlyLeft, string? onlyRight, string? common)
    {
        OnlyLeft = onlyLeft;
        OnlyRight = onlyRight;
        Common = common;
    }

    /// <summary>The shortest string in left but not right, or null.</summary>
    public string? OnlyLeft { get; }

    /// <summary>The shortest string in right but not left, or null.</summary>
    public string? OnlyRight { get; }

    /// <summary>The shortest string in both, or null.</summary>
    public string? Common { get; }
}

/// <summary>
/// Breadth-first searches that find shortest strings. Minterms are explored in readability order,
/// so among the shortest strings the most readable one is found first.
/// </summary>
internal static class WitnessSearch
{
    private const int NoParent = -1;

    /// <summary>Returns the shortest accepted string, or null when the language is empty.</summary>
    public static string? Shortest(Dfa dfa, MintermPartition partition)
    {
        Dfa empty = DfaOperations.EmptyLanguage(dfa.MintermCount);
        return Compare(dfa, empty, partition).OnlyLeft;
    }

    /// <summary>Finds the shortest witnesses for every region of the two languages at once.</summary>
    public static ComparisonWitnesses Compare(Dfa left, Dfa right, MintermPartition partition)
    {
        Dictionary<long, int> ids = new Dictionary<long, int>();
        List<SearchNode> nodes = new List<SearchNode>();
        int onlyLeft = NoParent;
        int onlyRight = NoParent;
        int common = NoParent;

        Enqueue(new SearchNode(0, 0, NoParent, NoParent), ids, nodes);
        for (int index = 0; index < nodes.Count; index++)
        {
            SearchNode node = nodes[index];
            bool inLeft = left.IsAccepting(node.Left);
            bool inRight = right.IsAccepting(node.Right);
            onlyLeft = Record(onlyLeft, inLeft && !inRight, index);
            onlyRight = Record(onlyRight, inRight && !inLeft, index);
            common = Record(common, inLeft && inRight, index);
            if (onlyLeft != NoParent && onlyRight != NoParent && common != NoParent)
            {
                break;
            }

            foreach (int minterm in partition.ReadableOrder)
            {
                int nextLeft = left.Next(node.Left, minterm);
                int nextRight = right.Next(node.Right, minterm);
                if (nextLeft != Dfa.Dead || nextRight != Dfa.Dead)
                {
                    Enqueue(new SearchNode(nextLeft, nextRight, index, minterm), ids, nodes);
                }
            }
        }

        return new ComparisonWitnesses(
            Spell(onlyLeft, nodes, partition),
            Spell(onlyRight, nodes, partition),
            Spell(common, nodes, partition));
    }

    private static int Record(int current, bool found, int index) => current == NoParent && found ? index : current;

    private static void Enqueue(SearchNode node, Dictionary<long, int> ids, List<SearchNode> nodes)
    {
        long key = ((long)(node.Left + 1) << 32) | (uint)(node.Right + 1);
        if (ids.ContainsKey(key))
        {
            return;
        }
        Dfa.CheckSize(nodes.Count + 1);
        ids[key] = nodes.Count;
        nodes.Add(node);
    }

    private static string? Spell(int index, List<SearchNode> nodes, MintermPartition partition)
    {
        if (index == NoParent)
        {
            return null;
        }

        List<char> reversed = new List<char>();
        for (int current = index; nodes[current].Parent != NoParent; current = nodes[current].Parent)
        {
            reversed.Add(partition.Representative(nodes[current].Minterm));
        }
        reversed.Reverse();
        StringBuilder builder = new StringBuilder(reversed.Count);
        foreach (char c in reversed)
        {
            builder.Append(c);
        }
        return builder.ToString();
    }

    private readonly struct SearchNode
    {
        public SearchNode(int left, int right, int parent, int minterm)
        {
            Left = left;
            Right = right;
            Parent = parent;
            Minterm = minterm;
        }

        public int Left { get; }

        public int Right { get; }

        public int Parent { get; }

        public int Minterm { get; }
    }
}
