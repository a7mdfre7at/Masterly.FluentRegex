using System.Text.RegularExpressions;
using Masterly.FluentRegex.Xunit;

namespace Masterly.FluentRegex.Integrations.Tests.DataAttributes;

/// <summary>Real theories fed by the data attributes.</summary>
public class PatternDataTheoryTests
{
    [Theory, MatchingData(typeof(TestPatterns), nameof(TestPatterns.FiveDigitsField), Count = 30, Seed = 42)]
    public void FiveDigits_MatchingSample_IsFiveUnicodeDigits(string value)
    {
        // \d matches any Unicode decimal digit, and the generator deliberately produces some.
        value.Length.ShouldBe(5);
        value.ShouldAllBe(c => char.IsDigit(c));
    }

    [Theory, NonMatchingData(typeof(TestPatterns), nameof(TestPatterns.FiveDigitsField), Count = 30)]
    public void FiveDigits_NonMatchingSample_IsRejectedByRegex(string value)
    {
        Regex.IsMatch(value, TestPatterns.FiveDigitsRegex).ShouldBeFalse();
    }

    [Theory, MatchingData(typeof(TestPatterns), nameof(TestPatterns.HexColor), Count = 10)]
    public void HexColor_MatchingSample_StartsWithHash(string value)
    {
        value.ShouldStartWith("#");
    }

    [Theory, MatchingData(typeof(TestPatterns), nameof(TestPatterns.IsoDate), Count = 10)]
    public void IsoDate_MatchingSample_HasIsoShape(string value)
    {
        value.ShouldMatch(@"^\d{4}-\d{2}-\d{2}$");
    }

    [Theory]
    [MatchingData(typeof(TestPatterns), nameof(TestPatterns.FiveDigitsField), Count = 5)]
    [MatchingData(typeof(DerivedPatternSource), nameof(DerivedPatternSource.Lowercase), Count = 5)]
    public void MultipleAttributes_EachSample_IsNonEmpty(string value)
    {
        value.ShouldNotBeNullOrEmpty();
    }
}
