#nullable enable
using System.Collections.Generic;

namespace Masterly.FluentRegex.Profiling;

/// <summary>What the interpreter found and how much work it took.</summary>
internal sealed class InterpreterResult
{
    public InterpreterResult(
        MatchSpan match,
        long steps,
        long backtracks,
        bool budgetExhausted,
        IReadOnlyList<long> hotspotSteps,
        IReadOnlyList<long> hotspotBacktracks)
    {
        Match = match;
        Steps = steps;
        Backtracks = backtracks;
        BudgetExhausted = budgetExhausted;
        HotspotSteps = hotspotSteps;
        HotspotBacktracks = hotspotBacktracks;
    }

    public MatchSpan Match { get; }

    public long Steps { get; }

    public long Backtracks { get; }

    public bool BudgetExhausted { get; }

    public IReadOnlyList<long> HotspotSteps { get; }

    public IReadOnlyList<long> HotspotBacktracks { get; }
}

/// <summary>Everything a <see cref="ProfileReport"/> describes, gathered in one place.</summary>
internal sealed class ProfileRun
{
    public ProfileRun(
        string pattern,
        int inputLength,
        long stepBudget,
        InterpreterResult interpreter,
        MatchSpan? engine)
    {
        Pattern = pattern;
        InputLength = inputLength;
        StepBudget = stepBudget;
        Interpreter = interpreter;
        Engine = engine;
    }

    public string Pattern { get; }

    public int InputLength { get; }

    public long StepBudget { get; }

    public InterpreterResult Interpreter { get; }

    /// <summary>The real engine's result, or null when the cross-check was skipped.</summary>
    public MatchSpan? Engine { get; }
}
