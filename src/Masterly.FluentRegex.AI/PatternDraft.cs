using System.Text;

namespace Masterly.FluentRegex.AI;

/// <summary>
/// A regex drafted by <see cref="PatternAssistant"/> together with its local verification.
/// When no draft passed, this is the best failed attempt and <see cref="Passed"/> is false.
/// </summary>
public sealed class PatternDraft
{
    internal PatternDraft(Pattern? pattern, string regex, string explanation, int attempts, PatternVerification verification)
    {
        Pattern = pattern;
        Regex = regex;
        Explanation = explanation;
        Attempts = attempts;
        Verification = verification;
    }

    /// <summary>Gets the drafted pattern, or null when the model never produced valid syntax.</summary>
    public Pattern? Pattern { get; }

    /// <summary>Gets the drafted regular expression text. Empty when the model never produced a usable reply.</summary>
    public string Regex { get; }

    /// <summary>Gets the model's explanation of the regex. Treat it as a description, not as a guarantee.</summary>
    public string Explanation { get; }

    /// <summary>Gets a value indicating whether the draft passed every local check.</summary>
    public bool Passed => Verification.Passed;

    /// <summary>Gets how many drafts were requested from the model.</summary>
    public int Attempts { get; }

    /// <summary>Gets the local verification of <see cref="Regex"/>.</summary>
    public PatternVerification Verification { get; }

    /// <summary>Returns the regex, the explanation and the verification summary.</summary>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder();
        string status = Passed ? "passed" : "did NOT pass";
        builder.AppendLine($"Draft {status} verification after {Attempts} attempt(s).");
        builder.AppendLine($"Regex: {Regex}");
        if (Explanation.Length > 0)
        {
            builder.AppendLine($"Explanation: {Explanation}");
        }

        builder.Append(Verification);
        return builder.ToString();
    }
}
