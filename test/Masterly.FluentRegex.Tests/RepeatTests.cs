namespace Masterly.FluentRegex.Tests;

public class RepeatTests
{
    [Fact]
    public void OneOrMore()
    {
        Pattern pattern = Pattern.With.Literal("a").Repeat.OneOrMore;

        pattern.ToString().ShouldBe("a+");
    }

    [Fact]
    public void ZeroOrMore()
    {
        Pattern pattern = Pattern.With.Literal("a").Repeat.ZeroOrMore;

        pattern.ToString().ShouldBe("a*");
    }

    [Fact]
    public void Optional()
    {
        Pattern pattern = Pattern.With.Literal("a").Repeat.Optional;

        pattern.ToString().ShouldBe("a?");
    }

    [Fact]
    public void Times_Exactly()
    {
        Pattern pattern = Pattern.With.Literal("a").Repeat.Times(3);

        pattern.ToString().ShouldBe("a{3}");
    }

    [Fact]
    public void Times_Range()
    {
        Pattern pattern = Pattern.With.Literal("a").Repeat.Times(3, 5);

        pattern.ToString().ShouldBe("a{3,5}");
    }

    [Fact]
    public void AtLeast()
    {
        Pattern pattern = Pattern.With.Literal("a").Repeat.AtLeast(3);

        pattern.ToString().ShouldBe("a{3,}");
    }

    [Fact]
    public void AtMost()
    {
        Pattern pattern = Pattern.With.Literal("a").Repeat.AtMost(3);

        pattern.ToString().ShouldBe("a{0,3}");
        pattern.Match("aaaaa").Value.ShouldBe("aaa");
        pattern.IsMatch("a{,3}").ShouldBeTrue(); // matches the empty string, no longer only the literal text
    }

    [Fact]
    public void Times_Zero_ProducesZeroQuantifier()
    {
        Pattern pattern = Pattern.With.StartOfLine.Literal("a").Repeat.Times(0).EndOfLine;

        bool matchesEmpty = pattern.IsMatch(string.Empty);

        pattern.ToString().ShouldBe("^a{0}$");
        matchesEmpty.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("a", true)]
    [InlineData("aaaa", true)]
    public void OneOrMore_Input_RequiresAtLeastOneOccurrence(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.Literal("a").Repeat.OneOrMore.EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("aaa", true)]
    [InlineData("b", false)]
    public void ZeroOrMore_Input_AllowsAnyNumberOfOccurrences(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.Literal("a").Repeat.ZeroOrMore.EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("a", true)]
    [InlineData("aa", false)]
    public void Optional_Input_AllowsZeroOrOneOccurrence(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.Literal("a").Repeat.Optional.EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("aa", false)]
    [InlineData("aaa", true)]
    [InlineData("aaaa", false)]
    public void Times_Exactly_Input_RequiresExactCount(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.Literal("a").Repeat.Times(3).EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a", false)]
    [InlineData("aa", true)]
    [InlineData("aaa", true)]
    [InlineData("aaaa", false)]
    public void Times_Range_Input_RequiresCountWithinBounds(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.Literal("a").Repeat.Times(2, 3).EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a", false)]
    [InlineData("aa", true)]
    [InlineData("aaaaaaaa", true)]
    public void AtLeast_Input_RequiresMinimumCount(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.Literal("a").Repeat.AtLeast(2).EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Fact]
    public void OneOrMore_AfterGroup_RepeatsWholeGroup()
    {
        Pattern pattern = Pattern.With.StartOfLine.NonCapturingGroup(Pattern.With.Literal("ab")).Repeat.OneOrMore.EndOfLine;

        bool matchesRepeated = pattern.IsMatch("ababab");
        bool matchesPartial = pattern.IsMatch("abb");

        matchesRepeated.ShouldBeTrue();
        matchesPartial.ShouldBeFalse();
    }
}
