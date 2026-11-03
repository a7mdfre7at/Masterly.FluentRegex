using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.AI;

/// <summary>
/// Verifies a drafted regex locally. Positive examples must match the entire string; negative examples
/// must not match anywhere (<see cref="Regex.IsMatch(string)"/>), which is how the returned pattern behaves.
/// </summary>
internal sealed class PatternVerifier
{
    private const int SampleCount = 5;
    private const int SampleSeed = 0;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private readonly IReadOnlyList<string> _shouldMatch;
    private readonly IReadOnlyList<string> _shouldNotMatch;

    public PatternVerifier(IReadOnlyList<string> shouldMatch, IReadOnlyList<string> shouldNotMatch)
    {
        _shouldMatch = shouldMatch;
        _shouldNotMatch = shouldNotMatch;
    }

    public static PatternVerification Unusable(string reason) =>
        new PatternVerification(reason, null, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    public VerifiedDraft Verify(string regex)
    {
        Pattern pattern;
        SafetyReport safety;
        try
        {
            pattern = Pattern.FromRegex(regex);
            safety = pattern.AnalyzeSafety();
        }
        catch (ArgumentException ex)
        {
            return new VerifiedDraft(null, Unusable(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return new VerifiedDraft(null, Unusable(ex.Message));
        }

        Regex anywhere = new Regex(regex, RegexOptions.None, MatchTimeout);
        Regex? whole = TryBuildWholeMatch(regex);
        List<string> rejected = new List<string>();
        foreach (string positive in _shouldMatch)
        {
            if (!MatchesWhole(anywhere, whole, positive))
            {
                rejected.Add(positive);
            }
        }

        List<string> accepted = new List<string>();
        foreach (string negative in _shouldNotMatch)
        {
            if (MatchesAnywhere(anywhere, negative))
            {
                accepted.Add(negative);
            }
        }

        PatternVerification verification = new PatternVerification(null, safety, rejected, accepted, GenerateSamples(pattern));
        return new VerifiedDraft(pattern, verification);
    }

    private static Regex? TryBuildWholeMatch(string regex)
    {
        try
        {
            return new Regex($"\\A(?:{regex})\\z", RegexOptions.None, MatchTimeout);
        }
        catch (ArgumentException)
        {
            // Wrapping can fail for exotic input such as a trailing (?x) comment; fall back to span checks.
            return null;
        }
    }

    private static bool MatchesWhole(Regex anywhere, Regex? whole, string input)
    {
        try
        {
            if (whole != null)
            {
                return whole.IsMatch(input);
            }

            Match match = anywhere.Match(input);
            return match.Success && match.Index == 0 && match.Length == input.Length;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool MatchesAnywhere(Regex anywhere, string input)
    {
        try
        {
            return anywhere.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            // An input that cannot be decided in time is treated as a failed rejection.
            return true;
        }
    }

    private static IReadOnlyList<string> GenerateSamples(Pattern pattern)
    {
        try
        {
            return pattern.GenerateMatching(SampleCount, SampleSeed);
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<string>();
        }
    }
}

/// <summary>A verification together with the parsed pattern, when the syntax was valid.</summary>
internal readonly struct VerifiedDraft
{
    public VerifiedDraft(Pattern? pattern, PatternVerification verification)
    {
        Pattern = pattern;
        Verification = verification;
    }

    public Pattern? Pattern { get; }

    public PatternVerification Verification { get; }
}
