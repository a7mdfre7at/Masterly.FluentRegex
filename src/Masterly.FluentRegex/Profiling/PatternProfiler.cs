#nullable enable
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Profiling;

/// <summary>Runs the backtracking interpreter, cross-checks it with the real engine and builds the report.</summary>
internal static class PatternProfiler
{
    public static ProfileReport Profile(Pattern pattern, string input, long stepBudget)
    {
        RegexTree tree = pattern.ParseTree();
        UnsupportedConstructs.Ensure(tree, pattern.Options);
        CompiledProgram program = ProgramCompiler.Compile(tree);
        InterpreterResult result = new BacktrackingInterpreter(program, input, stepBudget).Run();
        MatchSpan? engineSpan = result.BudgetExhausted ? null : RunEngine(pattern.ReferenceEngine(), input);
        ProfileRun run = new ProfileRun(tree.Pattern, input.Length, stepBudget, result, engineSpan);
        return new ProfileReport(run, BuildHotspots(program, result));
    }

    private static MatchSpan RunEngine(Regex engine, string input)
    {
        Match match = engine.Match(input);
        return match.Success ? new MatchSpan(true, match.Index, match.Length) : MatchSpan.None;
    }

    private static IReadOnlyList<ProfileHotspot> BuildHotspots(CompiledProgram program, InterpreterResult result)
    {
        List<ProfileHotspot> hotspots = new List<ProfileHotspot>();
        for (int i = 0; i < program.Hotspots.Count; i++)
        {
            long steps = result.HotspotSteps[i];
            long backtracks = result.HotspotBacktracks[i];
            if (steps > 0 || backtracks > 0)
            {
                double share = result.Steps == 0 ? 0 : (double)steps / result.Steps;
                hotspots.Add(new ProfileHotspot(program.Hotspots[i], steps, backtracks, share));
            }
        }

        hotspots.Sort(CompareHotspots);
        return hotspots;
    }

    private static int CompareHotspots(ProfileHotspot left, ProfileHotspot right)
    {
        int bySteps = right.Steps.CompareTo(left.Steps);
        if (bySteps != 0)
        {
            return bySteps;
        }

        int byBacktracks = right.Backtracks.CompareTo(left.Backtracks);
        return byBacktracks != 0 ? byBacktracks : left.Start.CompareTo(right.Start);
    }
}
