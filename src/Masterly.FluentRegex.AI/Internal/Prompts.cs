using System.Collections.Generic;
using System.Text;

namespace Masterly.FluentRegex.AI;

/// <summary>The system prompt, the first request and the verification feedback sent to the model.</summary>
internal static class Prompts
{
    public const string ResponseSchema = """
        {
          "type": "object",
          "properties": {
            "regex": { "type": "string", "description": "The .NET regular expression, without delimiters or flags." },
            "explanation": { "type": "string", "description": "One or two sentences on what the regex accepts." }
          },
          "required": ["regex", "explanation"],
          "additionalProperties": false
        }
        """;

    public const string System = """
        You write regular expressions for the .NET System.Text.RegularExpressions engine.
        Your regex is compiled with RegexOptions.None and checked with Regex.IsMatch, so anchor it with ^ and $ to validate the whole input string.
        Use only .NET regex syntax. Do not use inline comments or the x (ignore whitespace) option.
        Avoid constructs that can backtrack catastrophically: nested quantifiers such as (a+)+, overlapping alternatives inside a repetition such as (\w|\d)+, and adjacent quantifiers competing for the same characters such as \d+\d+.
        Prefer a simple, readable pattern that satisfies the description and every example.
        Every draft is verified locally: syntax, ReDoS analysis, and the example strings. If verification fails you receive the exact failures; fix them all.
        Example strings are written as JSON string literals, so \" is a quote and \\ is one backslash.
        Respond with a JSON object with "regex" and "explanation".
        """;

    private const int MaxExamplesInFeedback = 20;

    public static string Initial(string description, IReadOnlyList<string> shouldMatch, IReadOnlyList<string> shouldNotMatch)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Write a .NET regular expression for: {description}");
        AppendList(builder, "Strings that must match (the entire string):", shouldMatch);
        AppendList(builder, "Strings that must not match:", shouldNotMatch);
        return builder.ToString().TrimEnd();
    }

    public static string InvalidReply(string problem) =>
        $"Your reply could not be used: {problem} Respond with only a JSON object with string properties \"regex\" and \"explanation\".";

    public static string Feedback(string regex, PatternVerification verification)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Your regex failed local verification: {regex}");
        if (!verification.IsValidSyntax)
        {
            builder.AppendLine($"- Syntax error: {verification.SyntaxError}");
        }

        AppendSafety(builder, verification.SafetyReport);
        AppendFailures(builder, "These strings must match the entire string but did not:", verification.RejectedPositives);
        AppendFailures(builder, "These strings must not match but did:", verification.AcceptedNegatives);
        builder.Append("Fix every problem above and respond with the corrected JSON object.");
        return builder.ToString();
    }

    private static void AppendSafety(StringBuilder builder, SafetyReport? report)
    {
        if (report == null)
        {
            return;
        }

        foreach (SafetyIssue issue in report.Issues)
        {
            builder.AppendLine($"- ReDoS risk ({issue.Risk} {issue.Kind}) at position {issue.Position} in {issue.Fragment}: {issue.Description} Fix: {issue.Suggestion}");
        }
    }

    private static void AppendFailures(StringBuilder builder, string heading, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        builder.AppendLine($"- {heading}");
        AppendItems(builder, values);
    }

    private static void AppendList(StringBuilder builder, string heading, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(heading);
        AppendItems(builder, values);
    }

    private static void AppendItems(StringBuilder builder, IReadOnlyList<string> values)
    {
        for (int index = 0; index < values.Count && index < MaxExamplesInFeedback; index++)
        {
            builder.AppendLine($"  {Quoting.Quote(values[index])}");
        }

        if (values.Count > MaxExamplesInFeedback)
        {
            builder.AppendLine($"  (and {values.Count - MaxExamplesInFeedback} more)");
        }
    }
}
