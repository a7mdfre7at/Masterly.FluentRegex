#nullable enable

using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Partitions the code units used by a group of automata into minterms: maximal sets of
/// characters that no transition can tell apart. Automata over minterms have a small
/// alphabet while staying exact for all 65,536 UTF-16 code units.
/// </summary>
internal sealed class MintermPartition
{
    private readonly Dictionary<CharSet, int[]> _cache = new Dictionary<CharSet, int[]>(CharSetComparer.Instance);
    private readonly char[] _representatives;

    private MintermPartition(List<CharSet> minterms)
    {
        Minterms = minterms;
        _representatives = new char[minterms.Count];
        for (int i = 0; i < minterms.Count; i++)
        {
            _representatives[i] = ReadableChars.Pick(minterms[i]);
        }
        ReadableOrder = BuildReadableOrder();
    }

    /// <summary>The minterms. Their union is the union of all input sets.</summary>
    public IReadOnlyList<CharSet> Minterms { get; }

    /// <summary>Minterm indexes ordered by how readable their representative character is.</summary>
    public IReadOnlyList<int> ReadableOrder { get; }

    /// <summary>The number of minterms.</summary>
    public int Count => Minterms.Count;

    /// <summary>Builds the coarsest partition that refines every given set.</summary>
    public static MintermPartition Build(IEnumerable<CharSet> sets)
    {
        HashSet<CharSet> distinct = new HashSet<CharSet>(CharSetComparer.Instance);
        CharSet universe = CharSet.Empty;
        foreach (CharSet set in sets)
        {
            if (!set.IsEmpty && distinct.Add(set))
            {
                universe = universe.Union(set);
            }
        }

        List<CharSet> blocks = new List<CharSet>();
        if (!universe.IsEmpty)
        {
            blocks.Add(universe);
        }
        foreach (CharSet set in distinct)
        {
            blocks = Refine(blocks, set);
        }
        return new MintermPartition(blocks);
    }

    private static List<CharSet> Refine(List<CharSet> blocks, CharSet set)
    {
        List<CharSet> refined = new List<CharSet>(blocks.Count + 1);
        foreach (CharSet block in blocks)
        {
            CharSet inside = block.Intersect(set);
            if (inside.IsEmpty || inside.Count == block.Count)
            {
                refined.Add(block);
                continue;
            }
            refined.Add(inside);
            refined.Add(block.Except(set));
        }
        return refined;
    }

    /// <summary>Returns the minterms that together make up <paramref name="set"/>.</summary>
    public int[] MintermsOf(CharSet set)
    {
        if (_cache.TryGetValue(set, out int[]? cached))
        {
            return cached;
        }

        List<int> indexes = new List<int>();
        for (int i = 0; i < Minterms.Count; i++)
        {
            if (Minterms[i].Overlaps(set))
            {
                indexes.Add(i);
            }
        }
        int[] result = indexes.ToArray();
        _cache[set] = result;
        return result;
    }

    /// <summary>Returns the union of the given minterms.</summary>
    public CharSet UnionOf(IEnumerable<int> minterms)
    {
        CharSet result = CharSet.Empty;
        foreach (int minterm in minterms)
        {
            result = result.Union(Minterms[minterm]);
        }
        return result;
    }

    /// <summary>Returns the readable character chosen to stand for a minterm in witness strings.</summary>
    public char Representative(int minterm) => _representatives[minterm];

    private int[] BuildReadableOrder()
    {
        List<int> order = new List<int>(Minterms.Count);
        for (int i = 0; i < Minterms.Count; i++)
        {
            order.Add(i);
        }
        order.Sort((a, b) =>
        {
            int byRank = ReadableChars.Rank(_representatives[a]).CompareTo(ReadableChars.Rank(_representatives[b]));
            return byRank != 0 ? byRank : _representatives[a].CompareTo(_representatives[b]);
        });
        return order.ToArray();
    }
}
