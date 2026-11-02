#nullable enable
using System;

namespace Masterly.FluentRegex;

/// <summary>
/// Thrown by <see cref="PropertyResult.ThrowIfFailed"/> when a property does not hold. The message
/// contains the shrunk counterexample, the original counterexample, the seed and how to reproduce.
/// </summary>
public sealed class PropertyFailedException : Exception
{
    /// <summary>Initializes a new instance with a default message.</summary>
    public PropertyFailedException()
        : base("The property failed.")
    {
    }

    /// <summary>Initializes a new instance with the given message.</summary>
    /// <param name="message">The failure description.</param>
    public PropertyFailedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with the given message and inner exception.</summary>
    /// <param name="message">The failure description.</param>
    /// <param name="innerException">The exception the property threw, if any.</param>
    public PropertyFailedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    internal PropertyFailedException(string message, PropertyResult result)
        : base(message, result.Exception)
    {
        Result = result;
    }

    /// <summary>
    /// Gets the failed result, including the counterexample and seed, or null when the exception was
    /// constructed directly.
    /// </summary>
    public PropertyResult? Result { get; }
}
