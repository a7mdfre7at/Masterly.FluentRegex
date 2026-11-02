#nullable enable
using System;
using System.Globalization;
using System.Text;
using Masterly.FluentRegex.Testing;

namespace Masterly.FluentRegex;

/// <summary>
/// The outcome of a property test run by <see cref="PatternProperty"/>.
/// </summary>
public sealed class PropertyResult
{
    private readonly string _patternText;
    private readonly bool _matching;
    private readonly int _samplesRequested;

    private PropertyResult(
        string patternText,
        bool matching,
        int samplesRequested,
        int samplesTested,
        int seed)
    {
        _patternText = patternText;
        _matching = matching;
        _samplesRequested = samplesRequested;
        SamplesTested = samplesTested;
        Seed = seed;
        Passed = true;
    }

    private PropertyResult(
        string patternText,
        bool matching,
        int samplesRequested,
        int samplesTested,
        int seed,
        FailureDetails failure)
        : this(patternText, matching, samplesRequested, samplesTested, seed)
    {
        Passed = false;
        Counterexample = failure.Counterexample;
        OriginalCounterexample = failure.OriginalCounterexample;
        ShrinkSteps = failure.ShrinkSteps;
        Exception = failure.Exception;
    }

    /// <summary>Gets a value indicating whether the property held for every sample.</summary>
    public bool Passed { get; }

    /// <summary>
    /// Gets the smallest failing input found by shrinking, or null when the property passed.
    /// </summary>
    public string? Counterexample { get; }

    /// <summary>
    /// Gets the first generated input the property failed for, before shrinking, or null when it passed.
    /// </summary>
    public string? OriginalCounterexample { get; }

    /// <summary>Gets how many generated samples were checked, including the first failing one.</summary>
    public int SamplesTested { get; }

    /// <summary>Gets how many candidate strings were evaluated while shrinking the counterexample.</summary>
    public int ShrinkSteps { get; }

    /// <summary>
    /// Gets the exception the property threw for <see cref="Counterexample"/>, or null when it returned false
    /// or passed.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>Gets the seed used for sample generation. Re-run with the same seed to reproduce.</summary>
    public int Seed { get; }

    /// <summary>
    /// Throws a <see cref="PropertyFailedException"/> describing the counterexample when the property failed.
    /// Does nothing when it passed.
    /// </summary>
    /// <exception cref="PropertyFailedException">The property failed.</exception>
    public void ThrowIfFailed()
    {
        if (Passed)
        {
            return;
        }

        throw new PropertyFailedException(DescribeFailure(), this);
    }

    /// <summary>Returns a one-line summary, or the full failure report when the property failed.</summary>
    public override string ToString()
    {
        return Passed
            ? $"Property passed for {SamplesTested.ToString(CultureInfo.InvariantCulture)} {SideName} sample(s) (seed {Seed.ToString(CultureInfo.InvariantCulture)})."
            : DescribeFailure();
    }

    internal static PropertyResult Success(string patternText, bool matching, int samplesRequested, int samplesTested, int seed) =>
        new PropertyResult(patternText, matching, samplesRequested, samplesTested, seed);

    internal static PropertyResult Failure(
        string patternText,
        bool matching,
        int samplesRequested,
        int samplesTested,
        int seed,
        FailureDetails failure) =>
        new PropertyResult(patternText, matching, samplesRequested, samplesTested, seed, failure);

    private string SideName => _matching ? "matching" : "non-matching";

    private string DescribeFailure()
    {
        string counterexample = StringLiteral.Quote(Counterexample ?? string.Empty);
        string original = StringLiteral.Quote(OriginalCounterexample ?? string.Empty);
        string seed = Seed.ToString(CultureInfo.InvariantCulture);
        string samples = _samplesRequested.ToString(CultureInfo.InvariantCulture);
        string method = _matching ? nameof(PatternProperty.ForMatching) : nameof(PatternProperty.ForNonMatching);

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Property failed for a {SideName} sample of pattern {StringLiteral.Quote(_patternText)}.");
        builder.AppendLine($"  Counterexample:          {counterexample}");
        builder.AppendLine($"  Original counterexample: {original}");
        builder.AppendLine($"  Samples tested:          {SamplesTested.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"  Shrink steps:            {ShrinkSteps.ToString(CultureInfo.InvariantCulture)}");
        if (Exception != null)
        {
            builder.AppendLine($"  Exception:               {Exception.GetType().Name}: {Exception.Message}");
        }

        builder.AppendLine($"  Seed:                    {seed}");
        builder.Append($"Reproduce with PatternProperty.{method}(pattern, property, new PropertyOptions {{ Seed = {seed}, Samples = {samples} }}), ");
        builder.Append($"or call the property directly with {counterexample}.");
        return builder.ToString();
    }

    /// <summary>The details of a failed run, grouped to keep constructor signatures small.</summary>
    internal readonly struct FailureDetails
    {
        public FailureDetails(string counterexample, string originalCounterexample, int shrinkSteps, Exception? exception)
        {
            Counterexample = counterexample;
            OriginalCounterexample = originalCounterexample;
            ShrinkSteps = shrinkSteps;
            Exception = exception;
        }

        public string Counterexample { get; }

        public string OriginalCounterexample { get; }

        public int ShrinkSteps { get; }

        public Exception? Exception { get; }
    }
}
