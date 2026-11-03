#nullable enable
using System;

namespace Masterly.FluentRegex;

/// <summary>Limits for <see cref="Pattern.Profile(string, ProfileOptions?)"/>.</summary>
public sealed class ProfileOptions
{
    private const long DefaultStepBudget = 1_000_000;

    private long _stepBudget = DefaultStepBudget;

    /// <summary>
    /// Gets or sets the maximum number of steps the interpreter may take before it gives up and reports
    /// <see cref="ProfileReport.BudgetExhausted"/>. Defaults to 1,000,000.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public long StepBudget
    {
        get => _stepBudget;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(StepBudget), value, "StepBudget must be positive.");
            }

            _stepBudget = value;
        }
    }
}
