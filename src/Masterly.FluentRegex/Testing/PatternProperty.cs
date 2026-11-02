#nullable enable
using System;
using System.Collections.Generic;
using Masterly.FluentRegex.Testing;

namespace Masterly.FluentRegex;

/// <summary>
/// Property-based testing driven by a pattern: check that a property holds for every string the
/// pattern generates, and get the smallest failing input when it does not.
/// </summary>
/// <remarks>
/// Samples come from <see cref="Pattern.GenerateMatching"/> or <see cref="Pattern.GenerateNonMatching"/>,
/// so membership follows <see cref="System.Text.RegularExpressions.Regex.IsMatch(string)"/> with the
/// pattern's options. A property that throws counts as a failure. Failing inputs are shrunk by greedy
/// delta debugging: removing chunks, then replacing characters with simpler ones, keeping a candidate
/// only when it is still on the same side of the pattern and still fails. Runs are deterministic for a
/// given <see cref="PropertyOptions.Seed"/>.
/// </remarks>
/// <example>
/// PatternProperty.ForMatching(CommonPatterns.IPv4(), ip => IPAddress.TryParse(ip, out _))
///     .ThrowIfFailed();
/// </example>
public static class PatternProperty
{
    /// <summary>
    /// Checks that <paramref name="property"/> holds for strings that <paramref name="pattern"/> matches.
    /// </summary>
    /// <param name="pattern">The pattern that generates the inputs.</param>
    /// <param name="property">The property; return false or throw to signal a failure.</param>
    /// <param name="options">Sample count, seed and shrink budget. Null uses the defaults.</param>
    /// <returns>The result, with a shrunk counterexample when the property failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> or <paramref name="property"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The pattern produced no matching samples to test.</exception>
    public static PropertyResult ForMatching(Pattern pattern, Func<string, bool> property, PropertyOptions? options = null) =>
        Run(pattern, property, options, expectMatch: true);

    /// <summary>
    /// Checks that <paramref name="property"/> holds for strings that <paramref name="pattern"/> does not match,
    /// mostly near misses one edit away from a valid sample.
    /// </summary>
    /// <param name="pattern">The pattern whose near misses are the inputs.</param>
    /// <param name="property">The property; return false or throw to signal a failure.</param>
    /// <param name="options">Sample count, seed and shrink budget. Null uses the defaults.</param>
    /// <returns>The result, with a shrunk counterexample when the property failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> or <paramref name="property"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The pattern produced no non-matching samples to test.</exception>
    public static PropertyResult ForNonMatching(Pattern pattern, Func<string, bool> property, PropertyOptions? options = null) =>
        Run(pattern, property, options, expectMatch: false);

    private static PropertyResult Run(Pattern pattern, Func<string, bool> property, PropertyOptions? options, bool expectMatch)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        PropertyOptions settings = options ?? new PropertyOptions();
        string[] samples = GenerateSamples(pattern, settings, expectMatch);
        PropertyCheck check = new PropertyCheck(pattern, expectMatch, property);
        string patternText = pattern.ToString();

        for (int index = 0; index < samples.Length; index++)
        {
            PropertyOutcome outcome = check.Evaluate(samples[index]);
            if (outcome.Failed)
            {
                PropertyResult.FailureDetails failure = Shrink(check, settings, samples, index, outcome.Exception);
                return PropertyResult.Failure(patternText, expectMatch, settings.Samples, index + 1, settings.Seed, failure);
            }
        }

        return PropertyResult.Success(patternText, expectMatch, settings.Samples, samples.Length, settings.Seed);
    }

    private static string[] GenerateSamples(Pattern pattern, PropertyOptions settings, bool expectMatch)
    {
        string[] samples = expectMatch
            ? pattern.GenerateMatching(settings.Samples, settings.Seed)
            : pattern.GenerateNonMatching(settings.Samples, settings.Seed);

        if (samples.Length == 0)
        {
            string side = expectMatch ? "matching" : "non-matching";
            throw new InvalidOperationException(
                $"The pattern {StringLiteral.Quote(pattern.ToString())} produced no {side} samples, so the property cannot be tested.");
        }

        return samples;
    }

    private static PropertyResult.FailureDetails Shrink(
        PropertyCheck check,
        PropertyOptions settings,
        string[] samples,
        int failingIndex,
        Exception? exception)
    {
        string original = samples[failingIndex];
        Shrinker shrinker = new Shrinker(check, settings.MaxShrinkSteps, original, exception);
        shrinker.Shrink(Untested(samples, failingIndex));
        return new PropertyResult.FailureDetails(shrinker.Current, original, shrinker.Steps, shrinker.CurrentException);
    }

    private static IEnumerable<string> Untested(string[] samples, int failingIndex)
    {
        for (int index = failingIndex + 1; index < samples.Length; index++)
        {
            yield return samples[index];
        }
    }
}
