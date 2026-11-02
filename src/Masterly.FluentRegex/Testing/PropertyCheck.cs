#nullable enable
using System;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Testing;

/// <summary>
/// Evaluates a user property against one input and decides whether a candidate string still
/// belongs to the same side of the pattern (matching or non-matching) as the original samples.
/// </summary>
internal sealed class PropertyCheck
{
    private static readonly TimeSpan MembershipTimeout = TimeSpan.FromSeconds(1);

    private readonly Regex _membership;
    private readonly bool _expectMatch;
    private readonly Func<string, bool> _property;

    public PropertyCheck(Pattern pattern, bool expectMatch, Func<string, bool> property)
    {
        // Same membership rule as the sample generator: Regex.IsMatch with the pattern's options.
        _membership = new Regex(pattern.ToString(), pattern.Options & ~RegexOptions.Compiled, MembershipTimeout);
        _expectMatch = expectMatch;
        _property = property;
    }

    public bool HasExpectedMembership(string candidate)
    {
        try
        {
            return _membership.IsMatch(candidate) == _expectMatch;
        }
        catch (RegexMatchTimeoutException)
        {
            // A candidate whose membership cannot be decided is never a valid shrink.
            return false;
        }
    }

    public PropertyOutcome Evaluate(string input)
    {
        try
        {
            return _property(input) ? PropertyOutcome.Pass : PropertyOutcome.Fail(null);
        }
        catch (Exception ex)
        {
            return PropertyOutcome.Fail(ex);
        }
    }
}

/// <summary>The outcome of evaluating a property once.</summary>
internal readonly struct PropertyOutcome
{
    private PropertyOutcome(bool failed, Exception? exception)
    {
        Failed = failed;
        Exception = exception;
    }

    public static PropertyOutcome Pass => new PropertyOutcome(false, null);

    public bool Failed { get; }

    public Exception? Exception { get; }

    public static PropertyOutcome Fail(Exception? exception) => new PropertyOutcome(true, exception);
}
