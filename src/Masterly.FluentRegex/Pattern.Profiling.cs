#nullable enable
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Profiling;

namespace Masterly.FluentRegex;

/// <summary>
/// Backtracking profiling: count the work a backtracking matcher does on an input and find out where it goes.
/// </summary>
public partial class Pattern
{
    /// <summary>
    /// Matches <paramref name="input"/> with a step-counting backtracking interpreter that follows .NET's
    /// matching order, and reports how many steps and backtracks each construct cost.
    /// </summary>
    /// <param name="input">The text to match.</param>
    /// <param name="options">The step budget; defaults to 1,000,000 steps.</param>
    /// <returns>
    /// The match found, the total work, the hotspots ordered by cost, and whether the real .NET engine
    /// found the same match. Call <see cref="ProfileReport.ToString"/> for a text heatmap.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// The pattern uses a construct the interpreter does not emulate: conditionals, balancing groups,
    /// the <c>\G</c> anchor, or <see cref="RegexOptions.RightToLeft"/>. The message names the construct.
    /// </exception>
    /// <exception cref="InvalidOperationException">The pattern is not valid regex syntax.</exception>
    /// <remarks>
    /// The interpreter runs without .NET's optimizations, so the counts show the cost the pattern invites from
    /// a classic backtracking engine. Matching stops at <see cref="ProfileOptions.StepBudget"/>, so even
    /// catastrophic patterns return quickly with <see cref="ProfileReport.BudgetExhausted"/> set.
    /// </remarks>
    /// <example>
    /// ProfileReport report = Pattern.FromRegex(@"(a+)+$").Profile("aaaaaaaaaaaaaaaaaaaaaaaa!");
    /// // report.BudgetExhausted == true, report.Hotspots[0].Fragment == "a+"
    /// Console.WriteLine(report);
    /// </example>
    public ProfileReport Profile(string input, ProfileOptions? options = null)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        long budget = (options ?? new ProfileOptions()).StepBudget;
        return PatternProfiler.Profile(this, input, budget);
    }

    /// <summary>The backtracking engine the interpreter is checked against.</summary>
    internal Regex ReferenceEngine()
    {
#if NET7_0_OR_GREATER
        if ((_options & RegexOptions.NonBacktracking) != 0)
        {
            return new Regex(ToString(), _options & ~RegexOptions.NonBacktracking, _timeout);
        }
#endif
        return CachedRegex;
    }
}
