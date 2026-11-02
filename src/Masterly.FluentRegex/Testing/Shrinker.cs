#nullable enable
using System;
using System.Collections.Generic;

namespace Masterly.FluentRegex.Testing;

/// <summary>
/// Greedy delta-debugging shrinker. Starting from a failing input it repeatedly tries smaller and
/// simpler candidates, keeping one only when it has the same pattern membership and still fails
/// the property. Deterministic: the candidate order depends only on the input.
/// </summary>
internal sealed class Shrinker
{
    private const int RankSimplestLetter = 0;
    private const int RankSimplestDigit = 1;
    private const int RankSpace = 2;
    private const int RankSimplestUpper = 3;
    private const int RankLowerBase = 10;
    private const int RankDigitBase = 40;
    private const int RankUpperBase = 70;
    private const int RankOtherBase = 100;

    private static readonly char[] SimpleReplacements = { 'a', '0', ' ', 'A' };

    private readonly PropertyCheck _check;
    private readonly int _maxSteps;
    private readonly HashSet<string> _tried = new HashSet<string>(StringComparer.Ordinal);

    private string _current;
    private Exception? _currentException;

    public Shrinker(PropertyCheck check, int maxSteps, string counterexample, Exception? exception)
    {
        _check = check;
        _maxSteps = maxSteps;
        _current = counterexample;
        _currentException = exception;
        _tried.Add(counterexample);
    }

    public int Steps { get; private set; }

    public string Current => _current;

    public Exception? CurrentException => _currentException;

    /// <summary>Shrinks the counterexample and returns the smallest failing input found.</summary>
    /// <param name="shorterSamples">Other generated samples that may already be a smaller counterexample.</param>
    public void Shrink(IEnumerable<string> shorterSamples)
    {
        TryShorterSamples(shorterSamples);

        bool improved = true;
        while (improved && HasBudget)
        {
            bool removed = RemoveChunks();
            bool simplified = SimplifyCharacters();
            improved = removed || simplified;
        }
    }

    private bool HasBudget => Steps < _maxSteps;

    private void TryShorterSamples(IEnumerable<string> samples)
    {
        List<string> shorter = new List<string>();
        foreach (string sample in samples)
        {
            if (sample.Length < _current.Length)
            {
                shorter.Add(sample);
            }
        }

        shorter.Sort(CompareShortestFirst);
        foreach (string sample in shorter)
        {
            if (!HasBudget || sample.Length >= _current.Length)
            {
                return;
            }

            TryAccept(sample);
        }
    }

    private bool RemoveChunks()
    {
        bool removedAny = false;
        for (int chunk = _current.Length / 2; chunk >= 1; chunk /= 2)
        {
            removedAny |= RemoveChunksOfSize(chunk);
            if (!HasBudget)
            {
                break;
            }
        }

        // A string of length 1 has no half; try removing it entirely.
        if (_current.Length == 1 && HasBudget)
        {
            removedAny |= TryAccept(string.Empty);
        }

        return removedAny;
    }

    private bool RemoveChunksOfSize(int chunk)
    {
        bool removedAny = false;
        int start = 0;
        while (start + chunk <= _current.Length && HasBudget)
        {
            if (TryAccept(_current.Remove(start, chunk)))
            {
                removedAny = true;
            }
            else
            {
                start += chunk;
            }
        }

        return removedAny;
    }

    private bool SimplifyCharacters()
    {
        bool simplifiedAny = false;
        for (int index = 0; index < _current.Length && HasBudget; index++)
        {
            simplifiedAny |= SimplifyAt(index);
        }

        return simplifiedAny;
    }

    private bool SimplifyAt(int index)
    {
        char original = _current[index];
        int originalRank = Rank(original);
        foreach (char replacement in ReplacementsFor(original))
        {
            if (!HasBudget)
            {
                return false;
            }

            if (Rank(replacement) < originalRank && TryAccept(ReplaceAt(_current, index, replacement)))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<char> ReplacementsFor(char c)
    {
        yield return FirstOfClass(c);
        foreach (char simple in SimpleReplacements)
        {
            yield return simple;
        }
    }

    private bool TryAccept(string candidate)
    {
        if (!_tried.Add(candidate))
        {
            return false;
        }

        Steps++;
        if (!_check.HasExpectedMembership(candidate))
        {
            return false;
        }

        PropertyOutcome outcome = _check.Evaluate(candidate);
        if (!outcome.Failed)
        {
            return false;
        }

        _current = candidate;
        _currentException = outcome.Exception;
        return true;
    }

    private static char FirstOfClass(char c)
    {
        if (c >= 'a' && c <= 'z')
        {
            return 'a';
        }

        if (c >= 'A' && c <= 'Z')
        {
            return 'A';
        }

        if (char.IsDigit(c))
        {
            return '0';
        }

        if (char.IsWhiteSpace(c))
        {
            return ' ';
        }

        return char.IsLetter(c) ? 'a' : c;
    }

    /// <summary>Lower ranks are simpler. Replacements must strictly lower the rank, which guarantees termination.</summary>
    private static int Rank(char c)
    {
        switch (c)
        {
            case 'a':
                return RankSimplestLetter;
            case '0':
                return RankSimplestDigit;
            case ' ':
                return RankSpace;
            case 'A':
                return RankSimplestUpper;
        }

        if (c >= 'a' && c <= 'z')
        {
            return RankLowerBase + (c - 'a');
        }

        if (c >= '0' && c <= '9')
        {
            return RankDigitBase + (c - '0');
        }

        if (c >= 'A' && c <= 'Z')
        {
            return RankUpperBase + (c - 'A');
        }

        return RankOtherBase + c;
    }

    private static string ReplaceAt(string text, int index, char replacement)
    {
        char[] chars = text.ToCharArray();
        chars[index] = replacement;
        return new string(chars);
    }

    private static int CompareShortestFirst(string left, string right)
    {
        int byLength = left.Length.CompareTo(right.Length);
        return byLength != 0 ? byLength : string.CompareOrdinal(left, right);
    }
}
