#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Masterly.FluentRegex.Coverage;

namespace Masterly.FluentRegex;

/// <summary>
/// Records which decision points of a pattern are exercised while it is used, for example by a test suite.
/// Create one with <see cref="Pattern.TrackCoverage"/>, match through it, then call <see cref="Report"/>.
/// </summary>
/// <remarks>
/// <para>
/// The recorder is thread-safe: counters are updated with <see cref="Interlocked"/>, so one instance can live
/// in a static field and be shared by tests that run in parallel. A report taken while other threads are still
/// recording reflects some of their inputs but not necessarily all.
/// </para>
/// <para>
/// Every match in an input counts, not just the first. Only the final successful match is observed: a branch
/// the engine tried and then backtracked out of does not count as covered.
/// </para>
/// </remarks>
public sealed class PatternCoverage
{
    private readonly Regex _original;
    private readonly Regex _instrumented;
    private readonly IReadOnlyList<CoverageProbe> _probes;
    private readonly string _patternText;
    private readonly int[] _hits;
    private int _inputsRecorded;

    internal PatternCoverage(Pattern pattern, Regex original, Regex instrumented, IReadOnlyList<CoverageProbe> probes)
    {
        Pattern = pattern;
        _original = original;
        _instrumented = instrumented;
        _probes = probes;
        _patternText = pattern.ToString();
        _hits = new int[probes.Count];
    }

    /// <summary>Gets the pattern whose coverage is recorded.</summary>
    public Pattern Pattern { get; }

    /// <summary>
    /// Tests whether the pattern matches anywhere in <paramref name="input"/>, and records every match.
    /// </summary>
    /// <param name="input">The text to match.</param>
    /// <returns>True when the pattern matches, exactly as <see cref="FluentRegex.Pattern.IsMatch(string)"/> would.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public bool IsMatch(string input) => RecordMatches(input) > 0;

    /// <summary>
    /// Finds the first match in <paramref name="input"/>, and records every match.
    /// </summary>
    /// <param name="input">The text to match.</param>
    /// <returns>
    /// The match from the original, uninstrumented pattern, so its groups and values are exactly those of
    /// <see cref="FluentRegex.Pattern.Match(string)"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public Match Match(string input)
    {
        RecordMatches(input);
        return _original.Match(input);
    }

    /// <summary>Records every match in <paramref name="input"/> without returning a result.</summary>
    /// <param name="input">The text to match.</param>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public void Record(string input) => RecordMatches(input);

    /// <summary>Creates a report of the coverage recorded so far.</summary>
    /// <returns>A snapshot; later recordings do not change it.</returns>
    public CoverageReport Report()
    {
        List<CoverageItem> items = new List<CoverageItem>(_probes.Count);
        for (int i = 0; i < _probes.Count; i++)
        {
            CoverageProbe probe = _probes[i];
            items.Add(new CoverageItem(probe.Kind, probe.Description, probe.Start, probe.Length, Volatile.Read(ref _hits[i])));
        }

        return new CoverageReport(_patternText, items, Volatile.Read(ref _inputsRecorded));
    }

    private int RecordMatches(string input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        Interlocked.Increment(ref _inputsRecorded);
        int matches = 0;
        for (Match match = _instrumented.Match(input); match.Success; match = match.NextMatch())
        {
            RecordMatch(match);
            matches++;
        }

        return matches;
    }

    private void RecordMatch(Match match)
    {
        for (int i = 0; i < _probes.Count; i++)
        {
            CoverageProbe probe = _probes[i];
            if (match.Groups[probe.MarkerGroup].Captures.Count >= probe.RequiredCaptures)
            {
                Interlocked.Increment(ref _hits[i]);
            }
        }
    }
}
