using System;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class MatchingTests
{
    [Fact]
    public void ToRegex_ReturnsCompiledRegex()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        Regex regex = pattern.ToRegex();

        regex.ShouldNotBeNull();
        regex.ToString().ShouldBe(@"\d+");
    }

    [Fact]
    public void ToRegex_WithOptions_AppliesOptions()
    {
        Pattern pattern = Pattern.With.Literal("abc");
        Regex regex = pattern.ToRegex(RegexOptions.IgnoreCase);

        regex.Options.ShouldBe(RegexOptions.IgnoreCase);
    }

    [Fact]
    public void ToRegex_WithMultipleOptions_AppliesAllOptions()
    {
        Pattern pattern = Pattern.With.Literal("abc");
        Regex regex = pattern.ToRegex(RegexOptions.IgnoreCase | RegexOptions.Multiline);

        (regex.Options.HasFlag(RegexOptions.IgnoreCase)).ShouldBeTrue();
        (regex.Options.HasFlag(RegexOptions.Multiline)).ShouldBeTrue();
    }

    [Fact]
    public void IsMatch_ReturnsTrue_WhenPatternMatches()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        pattern.IsMatch("123").ShouldBeTrue();
    }

    [Fact]
    public void IsMatch_ReturnsTrue_WhenPatternMatchesPartially()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        pattern.IsMatch("abc123def").ShouldBeTrue();
    }

    [Fact]
    public void IsMatch_ReturnsFalse_WhenPatternDoesNotMatch()
    {
        Pattern pattern = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;
        pattern.IsMatch("abc").ShouldBeFalse();
    }

    [Fact]
    public void IsMatch_WithOptions_AppliesOptions()
    {
        Pattern pattern = Pattern.With.Literal("abc");
        pattern.IsMatch("ABC", RegexOptions.IgnoreCase).ShouldBeTrue();
        pattern.IsMatch("ABC").ShouldBeFalse(); // Without IgnoreCase
    }

    [Fact]
    public void WithOptions_SetsDefaultOptions()
    {
        Pattern pattern = Pattern.With
            .Literal("abc")
            .WithOptions(RegexOptions.IgnoreCase);

        pattern.IsMatch("ABC").ShouldBeTrue();
        pattern.IsMatch("abc").ShouldBeTrue();
        pattern.IsMatch("AbC").ShouldBeTrue();
    }

    [Fact]
    public void Match_ReturnsFirstMatch()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        Match match = pattern.Match("abc123def456");

        match.Success.ShouldBeTrue();
        match.Value.ShouldBe("123");
        match.Index.ShouldBe(3);
    }

    [Fact]
    public void Match_WithStartAt_StartsFromPosition()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        Match match = pattern.Match("abc123def456", 6);

        match.Success.ShouldBeTrue();
        match.Value.ShouldBe("456");
    }

    [Fact]
    public void Match_ReturnsUnsuccessful_WhenNoMatch()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        Match match = pattern.Match("abcdef");

        match.Success.ShouldBeFalse();
    }

    [Fact]
    public void Matches_ReturnsAllMatches()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        MatchCollection matches = pattern.Matches("abc123def456ghi789");

        matches.Count.ShouldBe(3);
        matches[0].Value.ShouldBe("123");
        matches[1].Value.ShouldBe("456");
        matches[2].Value.ShouldBe("789");
    }

    [Fact]
    public void Matches_ReturnsEmpty_WhenNoMatches()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        MatchCollection matches = pattern.Matches("abcdef");

        matches.Count.ShouldBe(0);
    }

    [Fact]
    public void Replace_ReplacesAllOccurrences()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        string result = pattern.Replace("abc123def456", "X");

        result.ShouldBe("abcXdefX");
    }

    [Fact]
    public void Replace_WithCount_ReplacesLimitedOccurrences()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        string result = pattern.Replace("a1b2c3d4", "X", 2);

        result.ShouldBe("aXbXc3d4");
    }

    [Fact]
    public void Replace_WithEvaluator_UsesCustomReplacement()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        string result = pattern.Replace("a1b2c3", m => (int.Parse(m.Value) * 2).ToString());

        result.ShouldBe("a2b4c6");
    }

    [Fact]
    public void Replace_WithEvaluatorAndCount_LimitsReplacements()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        string result = pattern.Replace("a1b2c3", m => "X", 2);

        result.ShouldBe("aXbXc3");
    }

    [Fact]
    public void Replace_WithGroupReference()
    {
        Pattern pattern = Pattern.With
            .NamedGroup("num", Pattern.With.Digit.Repeat.OneOrMore);
        string result = pattern.Replace("a123b", "[${num}]");

        result.ShouldBe("a[123]b");
    }

    [Fact]
    public void Split_SplitsAtPatternMatches()
    {
        Pattern pattern = Pattern.With.Literal(",");
        string[] parts = pattern.Split("a,b,c");

        parts.Length.ShouldBe(3);
        parts[0].ShouldBe("a");
        parts[1].ShouldBe("b");
        parts[2].ShouldBe("c");
    }

    [Fact]
    public void Split_WithCount_LimitsSplits()
    {
        Pattern pattern = Pattern.With.Literal(",");
        string[] parts = pattern.Split("a,b,c,d", 2);

        parts.Length.ShouldBe(2);
        parts[0].ShouldBe("a");
        parts[1].ShouldBe("b,c,d");
    }

    [Fact]
    public void Split_WithRegexPattern()
    {
        Pattern pattern = Pattern.With.Whitespace.Repeat.OneOrMore;
        string[] parts = pattern.Split("hello   world\ttest");

        parts.Length.ShouldBe(3);
        parts[0].ShouldBe("hello");
        parts[1].ShouldBe("world");
        parts[2].ShouldBe("test");
    }

    [Fact]
    public void MatchWithNamedGroups_ExtractsGroupValues()
    {
        Pattern pattern = Pattern.With
            .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
            .Literal("-")
            .NamedGroup("exchange", Pattern.With.Digit.Repeat.Times(3))
            .Literal("-")
            .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4));

        Match match = pattern.Match("555-123-4567");

        match.Success.ShouldBeTrue();
        match.Groups["area"].Value.ShouldBe("555");
        match.Groups["exchange"].Value.ShouldBe("123");
        match.Groups["number"].Value.ShouldBe("4567");
    }

    [Fact]
    public void CachedRegex_IsCached()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;

        // First call creates the cache
        pattern.IsMatch("123");
        // Second call should use the cache
        pattern.IsMatch("456");

        // Both should work correctly
        pattern.IsMatch("abc").ShouldBeFalse();
        pattern.IsMatch("789").ShouldBeTrue();
    }

    [Fact]
    public void WithOptions_ReturnsNewPattern_AndLeavesOriginalUnchanged()
    {
        Pattern pattern = Pattern.With.Literal("abc");

        pattern.IsMatch("ABC").ShouldBeFalse();

        Pattern ignoreCase = pattern.WithOptions(RegexOptions.IgnoreCase);

        ignoreCase.IsMatch("ABC").ShouldBeTrue();
        pattern.IsMatch("ABC").ShouldBeFalse();
    }

    [Fact]
    public void Cache_NeverGoesStale_BecausePatternsAreImmutable()
    {
        Pattern digit = Pattern.With.Digit;
        digit.IsMatch("1").ShouldBeTrue();

        Pattern extended = digit.Literal("x");

        extended.IsMatch("1").ShouldBeFalse();
        extended.IsMatch("1x").ShouldBeTrue();
        digit.ToString().ShouldBe(@"\d");
    }

    [Fact]
    public void WithOptions_Multiline_AnchorsMatchAtLineBoundaries()
    {
        Pattern pattern = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine
            .WithOptions(RegexOptions.Multiline);

        MatchCollection matches = pattern.Matches("12\n34\nab");

        matches.Count.ShouldBe(2);
        matches[1].Value.ShouldBe("34");
    }

    [Fact]
    public void IsMatch_WithOptionsOverload_DoesNotChangeDefaultOptions()
    {
        Pattern pattern = Pattern.With.Literal("abc");
        bool withIgnoreCase = pattern.IsMatch("ABC", RegexOptions.IgnoreCase);

        bool withDefaults = pattern.IsMatch("ABC");

        withIgnoreCase.ShouldBeTrue();
        withDefaults.ShouldBeFalse();
    }

    [Fact]
    public void ToRegex_CalledTwice_ReturnsNewInstanceEachTime()
    {
        Pattern pattern = Pattern.With.Digit;

        Regex first = pattern.ToRegex();
        Regex second = pattern.ToRegex();

        first.ShouldNotBeSameAs(second);
    }

    [Fact]
    public void ToRegex_InvalidPattern_ThrowsArgumentException()
    {
        Pattern pattern = Pattern.With.RegEx("(unclosed");

        Should.Throw<ArgumentException>(() => pattern.ToRegex());
    }

    [Fact]
    public void IsMatch_InvalidPattern_ThrowsArgumentException()
    {
        Pattern pattern = Pattern.With.RegEx("[unclosed");

        Should.Throw<ArgumentException>(() => pattern.IsMatch("abc"));
    }

    [Fact]
    public void IsMatch_NullInput_ThrowsArgumentNullException()
    {
        Pattern pattern = Pattern.With.Digit;

        Should.Throw<ArgumentNullException>(() => pattern.IsMatch(null));
    }

    [Fact]
    public void Match_NullInput_ThrowsArgumentNullException()
    {
        Pattern pattern = Pattern.With.Digit;

        Should.Throw<ArgumentNullException>(() => pattern.Match(null));
    }

    [Fact]
    public void Replace_NullInput_ThrowsArgumentNullException()
    {
        Pattern pattern = Pattern.With.Digit;

        Should.Throw<ArgumentNullException>(() => pattern.Replace(null, "X"));
    }

    [Fact]
    public void Split_NullInput_ThrowsArgumentNullException()
    {
        Pattern pattern = Pattern.With.Digit;

        Should.Throw<ArgumentNullException>(() => pattern.Split(null));
    }

    [Fact]
    public void Match_StartAtBeyondInputLength_ThrowsArgumentOutOfRangeException()
    {
        Pattern pattern = Pattern.With.Digit;

        Should.Throw<ArgumentOutOfRangeException>(() => pattern.Match("123", 4));
    }

    [Fact]
    public void Match_StartAtInputLength_ReturnsUnsuccessfulMatch()
    {
        Pattern pattern = Pattern.With.Digit;

        Match match = pattern.Match("123", 3);

        match.Success.ShouldBeFalse();
    }

    [Fact]
    public void Matches_ReturnsMatchIndexes()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;

        MatchCollection matches = pattern.Matches("abc123def456");

        matches.Count.ShouldBe(2);
        matches[0].Index.ShouldBe(3);
        matches[1].Index.ShouldBe(9);
    }

    [Fact]
    public void Replace_CountMinusOne_ReplacesAllOccurrences()
    {
        Pattern pattern = Pattern.With.Digit;

        string result = pattern.Replace("a1b2c3d4", "X", -1);

        result.ShouldBe("aXbXcXdX");
    }

    [Fact]
    public void Replace_CountZero_ReturnsInputUnchanged()
    {
        Pattern pattern = Pattern.With.Digit;

        string result = pattern.Replace("a1b2", "X", 0);

        result.ShouldBe("a1b2");
    }

    [Fact]
    public void Replace_NoMatch_ReturnsInputUnchanged()
    {
        Pattern pattern = Pattern.With.Digit;

        string result = pattern.Replace("abc", "X");

        result.ShouldBe("abc");
    }

    [Fact]
    public void Replace_NamedGroupSubstitution_SwapsGroups()
    {
        Pattern pattern = Pattern.With
            .NamedGroup("first", Pattern.With.Word.Repeat.OneOrMore)
            .Whitespace
            .NamedGroup("last", Pattern.With.Word.Repeat.OneOrMore);

        string result = pattern.Replace("John Doe", "${last}, ${first}");

        result.ShouldBe("Doe, John");
    }

    [Fact]
    public void Replace_EvaluatorWithGroups_ReordersDateParts()
    {
        Pattern pattern = Pattern.With
            .Group(Pattern.With.Digit.Repeat.Times(4))
            .Literal("-")
            .Group(Pattern.With.Digit.Repeat.Times(2))
            .Literal("-")
            .Group(Pattern.With.Digit.Repeat.Times(2));

        string result = pattern.Replace("2024-01-15", m => $"{m.Groups[2]}/{m.Groups[3]}/{m.Groups[1]}");

        result.ShouldBe("01/15/2024");
    }

    [Fact]
    public void Split_CapturingGroupDelimiter_IncludesDelimitersInResult()
    {
        Pattern pattern = Pattern.With.Group(Pattern.With.Literal(","));

        string[] parts = pattern.Split("a,b,c");

        parts.ShouldBe(new[] { "a", ",", "b", ",", "c" });
    }

    [Fact]
    public void Split_NoMatch_ReturnsWholeInput()
    {
        Pattern pattern = Pattern.With.Literal(",");

        string[] parts = pattern.Split("abc");

        parts.ShouldBe(new[] { "abc" });
    }

    [Fact]
    public void Split_CountOne_ReturnsWholeInput()
    {
        Pattern pattern = Pattern.With.Literal(",");

        string[] parts = pattern.Split("a,b,c", 1);

        parts.ShouldBe(new[] { "a,b,c" });
    }

    [Fact]
    public void Split_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        Pattern pattern = Pattern.With.Literal(",");

        Should.Throw<ArgumentOutOfRangeException>(() => pattern.Split("a,b", -1));
    }
}
