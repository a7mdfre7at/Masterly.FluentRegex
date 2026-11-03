#nullable enable

using System;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Hard limits that keep language algebra bounded. Exceeding one throws an
/// <see cref="InvalidOperationException"/> instead of consuming unbounded time or memory.
/// </summary>
internal static class AlgebraLimits
{
    /// <summary>The largest repeat bound expanded into automaton states.</summary>
    public const int MaxRepeatBound = 1000;

    /// <summary>The largest number of NFA states built for one pattern.</summary>
    public const int MaxNfaStates = 200_000;

    /// <summary>The largest number of states of any DFA, including product automata.</summary>
    public const int MaxDfaStates = 50_000;

    /// <summary>The largest regex term built while converting an automaton back to a regex.</summary>
    public const int MaxTermSize = 200_000;

    /// <summary>The time the engine may spend verifying one witness string.</summary>
    public static readonly TimeSpan OracleTimeout = TimeSpan.FromSeconds(5);
}
