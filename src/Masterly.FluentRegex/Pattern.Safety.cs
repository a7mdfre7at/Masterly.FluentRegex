using System;
using Masterly.FluentRegex.Analysis;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// ReDoS (regular expression denial of service) analysis.
    /// </summary>
    public partial class Pattern
    {
        /// <summary>
        /// Statically analyzes the pattern for catastrophic backtracking risks.
        /// </summary>
        /// <param name="confirm">
        /// When true, each finding's attack input is executed with a short timeout to prove the
        /// slowdown is real. This costs up to <paramref name="confirmationBudget"/> per finding.
        /// </param>
        /// <param name="confirmationBudget">The timeout per confirmation run. Defaults to 250 ms.</param>
        /// <returns>
        /// A report listing each risky construct, its severity, a fix and an attack input. A pattern that uses
        /// <c>RegexOptions.NonBacktracking</c> runs in linear time, so its report has no issues.
        /// </returns>
        /// <example>
        /// var report = Pattern.FromRegex(@"^(\w+\s?)*$").AnalyzeSafety(confirm: true);
        /// // report.Risk == RiskLevel.High
        /// // report.Issues[0].Confirmed == true
        /// </example>
        public SafetyReport AnalyzeSafety(bool confirm = false, TimeSpan? confirmationBudget = null)
        {
            TimeSpan budget = confirmationBudget ?? TimeSpan.FromMilliseconds(250);
            if (budget <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(confirmationBudget));
            return SafetyAnalyzer.Analyze(ParseTree(), _options, confirm, budget);
        }
    }
}
