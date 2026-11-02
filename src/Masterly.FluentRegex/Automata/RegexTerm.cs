#nullable enable

using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>The shape of a <see cref="RegexTerm"/>.</summary>
internal enum TermKind
{
    /// <summary>Matches nothing at all.</summary>
    Nothing,

    /// <summary>Matches only the empty string.</summary>
    Empty,

    /// <summary>Matches one character from a set.</summary>
    Class,

    /// <summary>Matches its parts one after another.</summary>
    Concat,

    /// <summary>Matches any one of its options.</summary>
    Alternation,

    /// <summary>Matches its body a bounded or unbounded number of times.</summary>
    Repeat
}

/// <summary>
/// An immutable regular expression used when an automaton is turned back into regex text.
/// Only its language matters, so the simplifier may reorder and factor freely.
/// </summary>
internal sealed class RegexTerm
{
    /// <summary>The marker for an unbounded maximum.</summary>
    public const int Unbounded = -1;

    private static readonly IReadOnlyList<RegexTerm> NoParts = new RegexTerm[0];

    internal RegexTerm(TermKind kind, string key, int size, CharSet? set = null, IReadOnlyList<RegexTerm>? parts = null, int min = 1, int max = 1)
    {
        Kind = kind;
        Key = key;
        Size = size;
        Set = set;
        Parts = parts ?? NoParts;
        Min = min;
        Max = max;
    }

    /// <summary>The shape of the term.</summary>
    public TermKind Kind { get; }

    /// <summary>A structural key: equal keys mean equal terms.</summary>
    public string Key { get; }

    /// <summary>The number of nodes in the term.</summary>
    public int Size { get; }

    /// <summary>The character set of a <see cref="TermKind.Class"/> term.</summary>
    public CharSet? Set { get; }

    /// <summary>The parts of a concatenation or alternation, or the single body of a repeat.</summary>
    public IReadOnlyList<RegexTerm> Parts { get; }

    /// <summary>The minimum count of a repeat.</summary>
    public int Min { get; }

    /// <summary>The maximum count of a repeat, or <see cref="Unbounded"/>.</summary>
    public int Max { get; }

    /// <summary>The body of a repeat.</summary>
    public RegexTerm Body => Parts[0];

    /// <summary>Whether the term matches the empty string.</summary>
    public bool IsNullable
    {
        get
        {
            switch (Kind)
            {
                case TermKind.Empty:
                    return true;
                case TermKind.Concat:
                    return AllNullable(Parts);
                case TermKind.Alternation:
                    return AnyNullable(Parts);
                case TermKind.Repeat:
                    return Min == 0 || Body.IsNullable;
                default:
                    return false;
            }
        }
    }

    private static bool AllNullable(IReadOnlyList<RegexTerm> parts)
    {
        foreach (RegexTerm part in parts)
        {
            if (!part.IsNullable)
            {
                return false;
            }
        }
        return true;
    }

    private static bool AnyNullable(IReadOnlyList<RegexTerm> parts)
    {
        foreach (RegexTerm part in parts)
        {
            if (part.IsNullable)
            {
                return true;
            }
        }
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Key;
}
