#nullable enable

using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>Compares character sets by content rather than by reference.</summary>
internal sealed class CharSetComparer : IEqualityComparer<CharSet>
{
    private CharSetComparer()
    {
    }

    /// <summary>The shared instance.</summary>
    public static CharSetComparer Instance { get; } = new CharSetComparer();

    /// <inheritdoc />
    public bool Equals(CharSet? x, CharSet? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }
        return x != null && x.SetEquals(y);
    }

    /// <inheritdoc />
    public int GetHashCode(CharSet obj) => obj.ContentHash();
}
