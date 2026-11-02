#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Masterly.FluentRegex.Profiling;

/// <summary>Renders a <see cref="ProfileReport"/> as a text heatmap.</summary>
internal static class ProfileReportFormatter
{
    private const string HeatGlyphs = " ▁▂▃▄▅▆▇█";
    private const char ControlPlaceholder = '·';
    private const int LabelWidth = 9;
    private const int MaxTableRows = 5;
    private const int ShareWidth = 7;
    private const int CountWidth = 13;
    private const double CheapStepsPerChar = 10;
    private const double HeavyStepsPerChar = 100;

    public static string Format(ProfileRun run, IReadOnlyList<ProfileHotspot> hotspots)
    {
        StringBuilder text = new StringBuilder();
        AppendField(text, "Pattern", Visible(run.Pattern));
        AppendField(text, "Heat", HeatRow(run.Pattern.Length, hotspots));
        AppendField(text, "Input", FormattableString.Invariant($"{run.InputLength:N0} characters"));
        AppendField(text, "Result", ResultLine(run));
        AppendField(text, "Engine", EngineLine(run));
        AppendHotspotTable(text, hotspots);
        AppendField(text, "Verdict", Verdict(run, hotspots));
        return text.ToString();
    }

    private static void AppendField(StringBuilder text, string label, string value) =>
        text.Append(label.PadRight(LabelWidth)).AppendLine(value);

    private static string Visible(string pattern)
    {
        StringBuilder visible = new StringBuilder(pattern.Length);
        foreach (char c in pattern)
        {
            visible.Append(char.IsControl(c) ? ControlPlaceholder : c);
        }

        return visible.ToString();
    }

    /// <summary>Shades each pattern character by the steps of the innermost hotspot covering it.</summary>
    private static string HeatRow(int patternLength, IReadOnlyList<ProfileHotspot> hotspots)
    {
        long[] heat = new long[patternLength];
        long hottest = 0;
        List<ProfileHotspot> outermostFirst = new List<ProfileHotspot>(hotspots);
        outermostFirst.Sort((left, right) => right.Length.CompareTo(left.Length));
        foreach (ProfileHotspot hotspot in outermostFirst)
        {
            hottest = Math.Max(hottest, hotspot.Steps);
            int end = Math.Min(patternLength, hotspot.Start + hotspot.Length);
            for (int i = hotspot.Start; i < end; i++)
            {
                heat[i] = hotspot.Steps;
            }
        }

        StringBuilder row = new StringBuilder(patternLength);
        foreach (long steps in heat)
        {
            row.Append(HeatGlyphs[GlyphIndex(steps, hottest)]);
        }

        return row.ToString().TrimEnd();
    }

    private static int GlyphIndex(long steps, long hottest)
    {
        if (steps == 0 || hottest == 0)
        {
            return 0;
        }

        int levels = HeatGlyphs.Length - 2;
        return 1 + (int)Math.Round((double)steps / hottest * levels);
    }

    private static string ResultLine(ProfileRun run)
    {
        InterpreterResult result = run.Interpreter;
        if (result.BudgetExhausted)
        {
            string budget = FormattableString.Invariant($"{run.StepBudget:N0}");
            return $"gave up: the step budget of {budget} ran out after {Counted(result.Backtracks, "backtrack")}";
        }

        string work = $"{Counted(result.Steps, "step")}, {Counted(result.Backtracks, "backtrack")}";
        return result.Match.IsMatch
            ? FormattableString.Invariant($"match at {result.Match.Index}, length {result.Match.Length}: {work}")
            : $"no match: {work}";
    }

    private static string Counted(long count, string noun) => ProfileText.Counted(count, noun);

    private static string EngineLine(ProfileRun run)
    {
        if (!run.Engine.HasValue)
        {
            return "not checked, because the step budget ran out";
        }

        return run.Engine.Value.SameAs(run.Interpreter.Match)
            ? "agrees with the .NET engine"
            : $"DISAGREES: the .NET engine found {run.Engine.Value}";
    }

    private static void AppendHotspotTable(StringBuilder text, IReadOnlyList<ProfileHotspot> hotspots)
    {
        text.AppendLine();
        text.Append("  ").Append("Share".PadLeft(ShareWidth)).Append("Steps".PadLeft(CountWidth))
            .Append("Backtracks".PadLeft(CountWidth)).AppendLine("  Construct");
        int rows = Math.Min(MaxTableRows, hotspots.Count);
        for (int i = 0; i < rows; i++)
        {
            AppendHotspotRow(text, hotspots[i]);
        }

        text.AppendLine();
    }

    private static void AppendHotspotRow(StringBuilder text, ProfileHotspot hotspot)
    {
        text.Append("  ")
            .Append(FormattableString.Invariant($"{hotspot.Share:0.0%}").PadLeft(ShareWidth))
            .Append(FormattableString.Invariant($"{hotspot.Steps:N0}").PadLeft(CountWidth))
            .Append(FormattableString.Invariant($"{hotspot.Backtracks:N0}").PadLeft(CountWidth))
            .AppendLine(FormattableString.Invariant($"  {Visible(hotspot.Fragment)}  (at {hotspot.Start})"));
    }

    private static string Verdict(ProfileRun run, IReadOnlyList<ProfileHotspot> hotspots)
    {
        string hottest = hotspots.Count > 0 ? $"'{Visible(hotspots[0].Fragment)}'" : "the pattern";
        if (run.Interpreter.BudgetExhausted)
        {
            return $"catastrophic backtracking, mostly in {hottest}. Make it atomic or possessive, " +
                   "or remove the nested or overlapping repetition.";
        }

        if (!run.Engine.HasValue || !run.Engine.Value.SameAs(run.Interpreter.Match))
        {
            return "the interpreter and the .NET engine disagree, so these counts may not describe the real engine.";
        }

        double perChar = (double)run.Interpreter.Steps / Math.Max(1, run.InputLength);
        string rate = FormattableString.Invariant($"{perChar:0.#} steps per input character");
        if (perChar <= CheapStepsPerChar)
        {
            return $"cheap: {rate}.";
        }

        return perChar <= HeavyStepsPerChar
            ? $"moderate: {rate}, mostly in {hottest}."
            : $"heavy backtracking: {rate}, mostly in {hottest}. Consider an atomic group or a narrower class.";
    }
}
