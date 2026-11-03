using System;
using System.Diagnostics.CodeAnalysis;

namespace Masterly.FluentRegex.Xunit;

/// <summary>
/// Supplies a theory with strings that a pattern matches, generated with
/// <see cref="Pattern.GenerateMatching"/>.
/// </summary>
/// <example>
/// [Theory, MatchingData(typeof(Patterns), nameof(Patterns.Iban), Count = 50, Seed = 42)]
/// public void Accepts_valid_iban(string value) => Validator.IsIban(value).ShouldBeTrue();
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class MatchingDataAttribute : PatternDataAttribute
{
    /// <summary>Initializes a new instance that reads the pattern from <paramref name="memberName"/> on <paramref name="patternSource"/>.</summary>
    /// <param name="patternSource">The type that declares the pattern member.</param>
    /// <param name="memberName">The name of a public static property, field or parameterless method returning <see cref="Pattern"/>.</param>
    public MatchingDataAttribute([DynamicallyAccessedMembers(PatternMembers)] Type patternSource, string memberName)
        : base(patternSource, memberName)
    {
    }

    /// <inheritdoc />
    protected override string SampleKind => "matching";

    /// <inheritdoc />
    protected override string[] Generate(Pattern pattern, int count, int seed) => pattern.GenerateMatching(count, seed);
}
