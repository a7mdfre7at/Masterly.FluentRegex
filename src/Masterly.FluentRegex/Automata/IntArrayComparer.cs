#nullable enable

using System.Collections.Generic;

namespace Masterly.FluentRegex.Automata;

/// <summary>Compares integer arrays element by element.</summary>
internal sealed class IntArrayComparer : IEqualityComparer<int[]>
{
    private const int HashSeed = 17;
    private const int HashMultiplier = 31;

    private IntArrayComparer()
    {
    }

    /// <summary>The shared instance.</summary>
    public static IntArrayComparer Instance { get; } = new IntArrayComparer();

    /// <inheritdoc />
    public bool Equals(int[]? x, int[]? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }
        if (x == null || y == null || x.Length != y.Length)
        {
            return false;
        }
        for (int i = 0; i < x.Length; i++)
        {
            if (x[i] != y[i])
            {
                return false;
            }
        }
        return true;
    }

    /// <inheritdoc />
    public int GetHashCode(int[] obj)
    {
        unchecked
        {
            int hash = HashSeed;
            foreach (int value in obj)
            {
                hash = (hash * HashMultiplier) + value;
            }
            return hash;
        }
    }
}
