#nullable enable
using System.Collections.Generic;
using Masterly.FluentRegex.Profiling;

namespace Masterly.FluentRegex;

/// <summary>
/// The result of <see cref="Pattern.Profile(string, ProfileOptions?)"/>: what a classic backtracking matcher
/// found, how much work it did, and which constructs did that work.
/// </summary>
/// <remarks>
/// Steps and backtracks are counted by an interpreter that follows .NET's matching order without .NET's
/// optimizations, so they show the cost a pattern invites rather than the exact work of the optimized
/// engine. <see cref="AgreesWithEngine"/> confirms the interpreter found the same match as the real engine.
/// </remarks>
public sealed class ProfileReport
{
    private readonly ProfileRun _run;

    internal ProfileReport(ProfileRun run, IReadOnlyList<ProfileHotspot> hotspots)
    {
        _run = run;
        Hotspots = hotspots;
    }

    /// <summary>Gets a value indicating whether the interpreter found a match. False when the budget ran out.</summary>
    public bool IsMatch => _run.Interpreter.Match.IsMatch;

    /// <summary>Gets the position of the match, or -1 when there is none.</summary>
    public int MatchIndex => _run.Interpreter.Match.Index;

    /// <summary>Gets the length of the match, or 0 when there is none.</summary>
    public int MatchLength => _run.Interpreter.Match.Length;

    /// <summary>Gets how many times the matcher tried a construct at a position.</summary>
    public long Steps => _run.Interpreter.Steps;

    /// <summary>Gets how many times the matcher went back to try another alternative.</summary>
    public long Backtracks => _run.Interpreter.Backtracks;

    /// <summary>
    /// Gets a value indicating whether the matcher hit <see cref="ProfileOptions.StepBudget"/> before finishing,
    /// which signals catastrophic backtracking on this input.
    /// </summary>
    public bool BudgetExhausted => _run.Interpreter.BudgetExhausted;

    /// <summary>
    /// Gets a value indicating whether the real .NET engine found the same result (match or not, index and
    /// length). False when the engine disagrees, and also when the budget ran out, because then no
    /// comparison was made.
    /// </summary>
    public bool AgreesWithEngine => _run.Engine.HasValue && _run.Engine.Value.SameAs(_run.Interpreter.Match);

    /// <summary>Gets the constructs that did any work, the most expensive first.</summary>
    public IReadOnlyList<ProfileHotspot> Hotspots { get; }

    /// <summary>
    /// Returns a text heatmap: the pattern, an intensity row under it, the result, a table of the top
    /// hotspots and a verdict.
    /// </summary>
    /// <returns>A multi-line, human-readable report.</returns>
    public override string ToString() => ProfileReportFormatter.Format(_run, Hotspots);
}
