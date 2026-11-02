using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex
{
    /// <summary>How likely a pattern is to suffer catastrophic backtracking (ReDoS).</summary>
    public enum RiskLevel
    {
        /// <summary>No risky constructs were found.</summary>
        None,

        /// <summary>Minor inefficiency; not exploitable in practice.</summary>
        Low,

        /// <summary>Polynomial backtracking. Long hostile inputs can be slow.</summary>
        Medium,

        /// <summary>Exponential backtracking. Short hostile inputs can hang the process.</summary>
        High
    }

    /// <summary>The kind of construct that causes a safety issue.</summary>
    public enum SafetyIssueKind
    {
        /// <summary>A repetition nested in a repetition, such as <c>(a+)+</c>.</summary>
        NestedQuantifier,

        /// <summary>Alternatives that can match the same text inside a repetition, such as <c>(\w|\d)+</c>.</summary>
        OverlappingAlternation,

        /// <summary>Neighbouring repetitions that compete for the same characters, such as <c>\d+\d+</c>.</summary>
        AdjacentOverlappingQuantifiers
    }

    /// <summary>A single construct that can cause excessive backtracking.</summary>
    public sealed class SafetyIssue
    {
        internal SafetyIssue(SafetyIssueKind kind, RiskLevel risk, int position, string fragment, string description, string suggestion, string attackInput)
        {
            Kind = kind;
            Risk = risk;
            Position = position;
            Fragment = fragment;
            Description = description;
            Suggestion = suggestion;
            AttackInput = attackInput;
        }

        /// <summary>Gets the kind of issue.</summary>
        public SafetyIssueKind Kind { get; }

        /// <summary>Gets the severity of the issue.</summary>
        public RiskLevel Risk { get; }

        /// <summary>Gets the zero-based position of the construct in the regex string.</summary>
        public int Position { get; }

        /// <summary>Gets the regex text of the offending construct.</summary>
        public string Fragment { get; }

        /// <summary>Gets a plain-language explanation.</summary>
        public string Description { get; }

        /// <summary>Gets a suggested fix.</summary>
        public string Suggestion { get; }

        /// <summary>
        /// Gets an input crafted to trigger the backtracking, or null when none could be built.
        /// </summary>
        public string AttackInput { get; }

        /// <summary>
        /// Gets whether running <see cref="AttackInput"/> actually exceeded the time budget.
        /// Null when confirmation was not requested.
        /// </summary>
        public bool? Confirmed { get; internal set; }

        /// <summary>Gets how long the confirmation run took before finishing or timing out.</summary>
        public TimeSpan? ConfirmationTime { get; internal set; }

        /// <inheritdoc />
        public override string ToString() =>
            Risk + " " + Kind + " at " + Position.ToString(CultureInfo.InvariantCulture) + " '" + Fragment + "': " + Description;
    }

    /// <summary>The result of <see cref="Pattern.AnalyzeSafety"/>.</summary>
    public sealed class SafetyReport
    {
        internal SafetyReport(string pattern, IReadOnlyList<SafetyIssue> issues)
        {
            Pattern = pattern;
            Issues = issues;
            RiskLevel risk = RiskLevel.None;
            foreach (SafetyIssue issue in issues)
            {
                if (issue.Risk > risk) risk = issue.Risk;
            }
            Risk = risk;
        }

        /// <summary>Gets the analyzed regex string.</summary>
        public string Pattern { get; }

        /// <summary>Gets the highest risk among all issues.</summary>
        public RiskLevel Risk { get; }

        /// <summary>Gets all detected issues, most severe first.</summary>
        public IReadOnlyList<SafetyIssue> Issues { get; }

        /// <summary>Gets a value indicating whether the risk is <see cref="RiskLevel.Low"/> or lower.</summary>
        public bool IsSafe => Risk <= RiskLevel.Low;

        /// <summary>Returns a human-readable report.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Pattern: ").AppendLine(Pattern);
            sb.Append("Risk:    ").AppendLine(Risk.ToString());
            if (Issues.Count == 0)
            {
                sb.AppendLine("No catastrophic backtracking risks found.");
                return sb.ToString();
            }

            foreach (SafetyIssue issue in Issues)
            {
                sb.AppendLine();
                sb.Append("  [").Append(issue.Risk).Append("] ").Append(issue.Kind).Append(" at position ")
                  .Append(issue.Position.ToString(CultureInfo.InvariantCulture)).Append(": ").AppendLine(issue.Fragment);
                sb.Append("    ").AppendLine(issue.Description);
                sb.Append("    Fix: ").AppendLine(issue.Suggestion);
                if (issue.AttackInput != null)
                {
                    string shown = issue.AttackInput.Length > 60 ? issue.AttackInput.Substring(0, 57) + "..." : issue.AttackInput;
                    sb.Append("    Attack input (").Append(issue.AttackInput.Length.ToString(CultureInfo.InvariantCulture)).Append(" chars): ")
                      .AppendLine(Visible(shown));
                }
                if (issue.Confirmed == true)
                    sb.Append("    CONFIRMED: matching did not finish within ").Append(issue.ConfirmationTime?.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)).AppendLine(" ms.");
                else if (issue.Confirmed == false)
                    sb.AppendLine("    Not confirmed: the attack input finished quickly.");
            }
            return sb.ToString();
        }

        private static string Visible(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsControl(c)) sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
