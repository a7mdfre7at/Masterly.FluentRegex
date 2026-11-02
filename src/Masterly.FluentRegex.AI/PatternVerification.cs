using System;
using System.Collections.Generic;
using System.Text;

namespace Masterly.FluentRegex.AI;

/// <summary>
/// The local verification of one drafted regex: syntax, ReDoS safety and the caller's examples.
/// Every check runs on this machine with the real .NET regex engine; nothing is taken on trust from the model.
/// </summary>
public sealed class PatternVerification
{
    private const int MaxExamplesInSummary = 5;

    internal PatternVerification(
        string? syntaxError,
        SafetyReport? safetyReport,
        IReadOnlyList<string> rejectedPositives,
        IReadOnlyList<string> acceptedNegatives,
        IReadOnlyList<string> samples)
    {
        SyntaxError = syntaxError;
        SafetyReport = safetyReport;
        RejectedPositives = rejectedPositives;
        AcceptedNegatives = acceptedNegatives;
        Samples = samples;
    }

    /// <summary>Gets a value indicating whether the regex is valid .NET syntax.</summary>
    public bool IsValidSyntax => SyntaxError == null;

    /// <summary>
    /// Gets why no valid regex could be checked: the syntax error, or a description of an unusable model reply.
    /// Null when the syntax is valid.
    /// </summary>
    public string? SyntaxError { get; }

    /// <summary>Gets the ReDoS analysis, or null when the syntax is invalid.</summary>
    public SafetyReport? SafetyReport { get; }

    /// <summary>Gets the strings that should have matched the entire regex but did not.</summary>
    public IReadOnlyList<string> RejectedPositives { get; }

    /// <summary>Gets the strings that should have been rejected but matched.</summary>
    public IReadOnlyList<string> AcceptedNegatives { get; }

    /// <summary>Gets up to five strings the regex matches, generated locally, for a quick sanity check.</summary>
    public IReadOnlyList<string> Samples { get; }

    /// <summary>Gets a value indicating whether the safety analysis reported any issue.</summary>
    public bool HasSafetyFindings => SafetyReport != null && SafetyReport.Issues.Count > 0;

    /// <summary>
    /// Gets a value indicating whether every check passed: valid syntax, no safety findings, no rejected
    /// positives and no accepted negatives.
    /// </summary>
    public bool Passed => IsValidSyntax && !HasSafetyFindings && RejectedPositives.Count == 0 && AcceptedNegatives.Count == 0;

    /// <summary>
    /// Gets the number of failed checks, used to pick the best of several drafts. Lower is better.
    /// </summary>
    internal int FailureScore
    {
        get
        {
            if (!IsValidSyntax)
            {
                return int.MaxValue;
            }

            int safetyFindings = SafetyReport?.Issues.Count ?? 0;
            return safetyFindings + RejectedPositives.Count + AcceptedNegatives.Count;
        }
    }

    /// <summary>Returns a multi-line summary of every check.</summary>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(Passed ? "Verification passed." : "Verification FAILED.");
        builder.AppendLine(IsValidSyntax ? "  Syntax: valid" : $"  Syntax: invalid ({SyntaxError})");
        AppendSafety(builder);
        AppendExamples(builder, "  Rejected positives", RejectedPositives);
        AppendExamples(builder, "  Accepted negatives", AcceptedNegatives);
        AppendExamples(builder, "  Samples", Samples);
        return builder.ToString().TrimEnd();
    }

    private void AppendSafety(StringBuilder builder)
    {
        if (SafetyReport == null)
        {
            builder.AppendLine("  Safety: not analyzed");
            return;
        }

        builder.AppendLine(HasSafetyFindings
            ? $"  Safety: {SafetyReport.Issues.Count} finding(s), risk {SafetyReport.Risk}"
            : "  Safety: no findings");
        foreach (SafetyIssue issue in SafetyReport.Issues)
        {
            builder.AppendLine($"    {issue}");
        }
    }

    private static void AppendExamples(StringBuilder builder, string label, IReadOnlyList<string> values)
    {
        builder.Append(label).Append(": ");
        if (values.Count == 0)
        {
            builder.AppendLine("none");
            return;
        }

        List<string> shown = new List<string>();
        for (int index = 0; index < values.Count && index < MaxExamplesInSummary; index++)
        {
            shown.Add(Quoting.Quote(values[index]));
        }

        string more = values.Count > MaxExamplesInSummary ? $" (+{values.Count - MaxExamplesInSummary} more)" : string.Empty;
        builder.AppendLine($"{string.Join(", ", shown)}{more}");
    }
}
