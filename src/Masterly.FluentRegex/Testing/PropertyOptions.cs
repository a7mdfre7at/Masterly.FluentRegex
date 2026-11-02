#nullable enable
using System;

namespace Masterly.FluentRegex;

/// <summary>
/// Settings for a property test run by <see cref="PatternProperty"/>.
/// </summary>
public sealed class PropertyOptions
{
    private const int DefaultSamples = 100;
    private const int DefaultMaxShrinkSteps = 1000;

    private int _samples = DefaultSamples;
    private int _maxShrinkSteps = DefaultMaxShrinkSteps;

    /// <summary>
    /// Gets or sets how many generated strings the property is checked against. Defaults to 100.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int Samples
    {
        get => _samples;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(Samples), value, "At least one sample is required.");
            }

            _samples = value;
        }
    }

    /// <summary>
    /// Gets or sets the seed for sample generation. The same seed always produces the same samples
    /// and the same shrunk counterexample. Defaults to 0.
    /// </summary>
    public int Seed { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of candidate strings evaluated while shrinking a counterexample.
    /// Zero disables shrinking. Defaults to 1000.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaxShrinkSteps
    {
        get => _maxShrinkSteps;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxShrinkSteps), value, "The shrink budget cannot be negative.");
            }

            _maxShrinkSteps = value;
        }
    }
}
