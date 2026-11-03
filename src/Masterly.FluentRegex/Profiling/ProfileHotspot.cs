#nullable enable
using System;
using Masterly.FluentRegex.Profiling;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex;

/// <summary>One construct of a profiled pattern and the work the backtracking matcher spent on it.</summary>
/// <remarks>
/// Single characters, classes, anchors and backreferences that are directly quantified are reported
/// together with their quantifier, so <c>a+</c> is one hotspot rather than <c>a</c> and <c>+</c>.
/// </remarks>
public sealed class ProfileHotspot
{
    internal ProfileHotspot(RegexNode node, long steps, long backtracks, double share)
    {
        Fragment = node.Source;
        Start = node.Start;
        Length = node.Length;
        Steps = steps;
        Backtracks = backtracks;
        Share = share;
    }

    /// <summary>Gets the regex text of the construct, such as <c>a+</c> or <c>(?=\d)</c>.</summary>
    public string Fragment { get; }

    /// <summary>Gets the zero-based position of the construct in the regex string.</summary>
    public int Start { get; }

    /// <summary>Gets the length of the construct in the regex string.</summary>
    public int Length { get; }

    /// <summary>Gets how many times the matcher tried this construct at some position.</summary>
    public long Steps { get; }

    /// <summary>Gets how many times the matcher returned to this construct to try another alternative.</summary>
    public long Backtracks { get; }

    /// <summary>Gets this construct's fraction of all steps, from 0 to 1.</summary>
    public double Share { get; }

    /// <summary>Returns a one-line summary of the hotspot.</summary>
    /// <returns>A string such as <c>'a+' at 1: 66.7% of steps, 1,000 steps, 400 backtracks</c>.</returns>
    public override string ToString() =>
        FormattableString.Invariant($"'{Fragment}' at {Start}: {Share:0.0%} of steps, ") +
        $"{ProfileText.Counted(Steps, "step")}, {ProfileText.Counted(Backtracks, "backtrack")}";
}
