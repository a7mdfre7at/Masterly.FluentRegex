#nullable enable
using System.Globalization;

namespace Masterly.FluentRegex;

/// <summary>
/// The verdict of <see cref="ConstrainedPattern.Check"/>: whether the whole input is valid and,
/// when it is not, whether the pattern or a semantic rule rejected it.
/// </summary>
/// <remarks>
/// <see cref="ToString"/> never includes the validated input, so a result can be logged safely.
/// </remarks>
public sealed class ConstraintResult
{
    private ConstraintResult(bool isValid, string? failedRule, MatchTrace? trace)
    {
        IsValid = isValid;
        FailedRule = failedRule;
        Trace = trace;
    }

    /// <summary>Gets a value indicating whether the input matched the pattern and satisfied every rule.</summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the description of the first rule that rejected the input, or null when the input is
    /// valid or the pattern itself did not match.
    /// </summary>
    public string? FailedRule { get; }

    /// <summary>
    /// Gets the trace of the whole-input pattern when the pattern itself did not match, otherwise null.
    /// Its <see cref="MatchTrace.Failure"/> pinpoints the construct and position where matching stopped.
    /// </summary>
    public MatchTrace? Trace { get; }

    /// <summary>Gets a value indicating whether the pattern itself rejected the input.</summary>
    public bool PatternFailed => Trace != null;

    internal static ConstraintResult Valid { get; } = new ConstraintResult(true, null, null);

    internal static ConstraintResult RuleFailure(string description) => new ConstraintResult(false, description, null);

    internal static ConstraintResult PatternMismatch(MatchTrace trace) => new ConstraintResult(false, null, trace);

    /// <summary>Returns a one-line summary that does not include the validated input.</summary>
    public override string ToString()
    {
        if (IsValid)
        {
            return "Valid";
        }

        if (FailedRule != null)
        {
            return $"Invalid: the rule '{FailedRule}' rejected the value";
        }

        TraceFailure? failure = Trace?.Failure;
        if (failure == null)
        {
            return "Invalid: the value does not match the pattern";
        }

        string position = failure.Position.ToString(CultureInfo.InvariantCulture);
        return $"Invalid: the value does not match the pattern at position {position}: expected {failure.Construct} ({failure.Description})";
    }
}
