using System;
using System.Diagnostics.CodeAnalysis;

namespace Masterly.FluentRegex.Xunit;

/// <summary>
/// Supplies a theory with strings that a pattern does not match, generated with
/// <see cref="Pattern.GenerateNonMatching"/>. Most are near misses one edit away from a valid sample.
/// </summary>
/// <example>
/// [Theory, NonMatchingData(typeof(Patterns), nameof(Patterns.Iban), Count = 50)]
/// public void Rejects_invalid_iban(string value) => Validator.IsIban(value).ShouldBeFalse();
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class NonMatchingDataAttribute : PatternDataAttribute
{
    /// <summary>Initializes a new instance that reads the pattern from <paramref name="memberName"/> on <paramref name="patternSource"/>.</summary>
    /// <param name="patternSource">The type that declares the pattern member.</param>
    /// <param name="memberName">The name of a public static property, field or parameterless method returning <see cref="Pattern"/>.</param>
    public NonMatchingDataAttribute([DynamicallyAccessedMembers(PatternMembers)] Type patternSource, string memberName)
        : base(patternSource, memberName)
    {
    }

    /// <inheritdoc />
    protected override string SampleKind => "non-matching";

    /// <inheritdoc />
    protected override string[] Generate(Pattern pattern, int count, int seed) => pattern.GenerateNonMatching(count, seed);
}
