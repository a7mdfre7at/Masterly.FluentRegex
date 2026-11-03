#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Optimization;

/// <summary>
/// Compares how two regexes actually behave on sample inputs: every match in sequence, with its
/// index, length, and the value and captures of every group.
/// </summary>
internal sealed class MatchBehaviorChecker
{
    private const int MatchingSamples = 25;
    private const int NonMatchingSamples = 25;
    private const int MatchingSeed = 7;
    private const int NonMatchingSeed = 11;
    private const int MaxMatchesPerInput = 20;
    private const string Filler = "x";

    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(1);

    private readonly Regex _original;
    private readonly RegexOptions _options;
    private readonly List<string> _inputs;
    private readonly int[] _groupNumbers;

    public MatchBehaviorChecker(Pattern original)
    {
        _options = original.Options & ~RegexOptions.Compiled;
        _original = new Regex(original.ToString(), _options, CheckTimeout);
        _inputs = BuildInputs(original);
        _groupNumbers = _original.GetGroupNumbers();
    }

    /// <summary>The inputs used for comparison.</summary>
    public IReadOnlyList<string> Inputs => _inputs;

    /// <summary>Returns true when <paramref name="candidate"/> behaves exactly like the original on every input.</summary>
    public bool BehavesLikeOriginal(string candidate)
    {
        Regex rewritten;
        try
        {
            rewritten = new Regex(candidate, _options, CheckTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (!SameGroups(_original, rewritten))
        {
            return false;
        }

        try
        {
            return _inputs.TrueForAll(input => SameMatches(rewritten, input));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static List<string> BuildInputs(Pattern original)
    {
        HashSet<string> inputs = new HashSet<string>(StringComparer.Ordinal) { string.Empty, " ", "\n" };
        string[] matching = original.GenerateMatching(MatchingSamples, MatchingSeed);
        foreach (string sample in matching)
        {
            inputs.UnionWith(Mutations(sample));
        }
        inputs.UnionWith(original.GenerateNonMatching(NonMatchingSamples, NonMatchingSeed));
        return new List<string>(inputs);
    }

    private static IEnumerable<string> Mutations(string sample)
    {
        yield return sample;
        yield return $"{Filler}{sample}{Filler}";
        yield return $"{sample}{sample}";
        yield return $"{sample}\n";
        yield return sample.ToUpperInvariant();
        if (sample.Length > 0)
        {
            yield return sample.Substring(1);
            yield return sample.Substring(0, sample.Length - 1);
        }
    }

    private static bool SameGroups(Regex left, Regex right)
    {
        return SameSequence(left.GetGroupNumbers(), right.GetGroupNumbers()) &&
               SameSequence(left.GetGroupNames(), right.GetGroupNames());
    }

    private static bool SameSequence<T>(T[] left, T[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }
        for (int i = 0; i < left.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
            {
                return false;
            }
        }
        return true;
    }

    private bool SameMatches(Regex rewritten, string input)
    {
        Match leftMatch = _original.Match(input);
        Match rightMatch = rewritten.Match(input);
        for (int count = 0; count < MaxMatchesPerInput; count++)
        {
            if (!SameMatch(leftMatch, rightMatch))
            {
                return false;
            }
            if (!leftMatch.Success)
            {
                return true;
            }
            leftMatch = leftMatch.NextMatch();
            rightMatch = rightMatch.NextMatch();
        }
        return true;
    }

    private bool SameMatch(Match left, Match right)
    {
        if (left.Success != right.Success || left.Groups.Count != right.Groups.Count)
        {
            return false;
        }
        foreach (int number in _groupNumbers)
        {
            if (!SameGroup(left.Groups[number], right.Groups[number]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool SameGroup(Group left, Group right)
    {
        if (left.Success != right.Success || left.Index != right.Index || left.Length != right.Length ||
            left.Captures.Count != right.Captures.Count)
        {
            return false;
        }
        for (int i = 0; i < left.Captures.Count; i++)
        {
            if (left.Captures[i].Index != right.Captures[i].Index || left.Captures[i].Length != right.Captures[i].Length)
            {
                return false;
            }
        }
        return true;
    }
}
