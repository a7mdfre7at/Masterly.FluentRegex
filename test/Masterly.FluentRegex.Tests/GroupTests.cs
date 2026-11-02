using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class GroupTests
{
    [Fact]
    public void NamedGroup_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With.NamedGroup("digits", Pattern.With.Digit.Repeat.OneOrMore);
        pattern.ToString().ShouldBe(@"(?<digits>\d+)");
    }

    [Fact]
    public void NamedGroup_WithWord_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With.NamedGroup("word", Pattern.With.Word.Repeat.OneOrMore);
        pattern.ToString().ShouldBe(@"(?<word>\w+)");
    }

    [Fact]
    public void NonCapturingGroup_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With.NonCapturingGroup(Pattern.With.Literal("abc"));
        pattern.ToString().ShouldBe("(?:abc)");
    }

    [Fact]
    public void NonCapturingGroup_WithRepeat_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With.NonCapturingGroup(Pattern.With.Digit.Repeat.Times(3)).Repeat.OneOrMore;
        pattern.ToString().ShouldBe(@"(?:\d{3})+");
    }

    [Fact]
    public void AtomicGroup_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With.AtomicGroup(Pattern.With.Digit.Repeat.OneOrMore);
        pattern.ToString().ShouldBe(@"(?>\d+)");
    }

    [Fact]
    public void Backreference_ByName_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With.Backreference("word");
        pattern.ToString().ShouldBe(@"\k<word>");
    }

    [Fact]
    public void Backreference_ByNumber_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With.Backreference(1);
        pattern.ToString().ShouldBe(@"\1");
    }

    [Fact]
    public void Backreference_ByNumber_Two_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With.Backreference(2);
        pattern.ToString().ShouldBe(@"\2");
    }

    [Fact]
    public void NamedGroup_WithBackreference_MatchesDuplicateWords()
    {
        Pattern pattern = Pattern.With
            .NamedGroup("word", Pattern.With.Word.Repeat.OneOrMore)
            .Whitespace.Repeat.OneOrMore
            .Backreference("word");
        pattern.ToString().ShouldBe(@"(?<word>\w+)\s+\k<word>");
    }

    [Fact]
    public void NamedGroup_WithBackreference_ActuallyMatches()
    {
        Pattern pattern = Pattern.With
            .WordBoundary
            .NamedGroup("word", Pattern.With.Word.Repeat.OneOrMore)
            .Whitespace.Repeat.OneOrMore
            .Backreference("word")
            .WordBoundary;

        pattern.IsMatch("the the").ShouldBeTrue();
        pattern.IsMatch("hello hello").ShouldBeTrue();
        pattern.IsMatch("hello world").ShouldBeFalse();
    }

    [Fact]
    public void Group_WithBackreferenceNumber_ActuallyMatches()
    {
        Pattern pattern = Pattern.With
            .WordBoundary
            .Group(Pattern.With.Word.Repeat.OneOrMore)
            .Whitespace.Repeat.OneOrMore
            .Backreference(1)
            .WordBoundary;

        pattern.IsMatch("the the").ShouldBeTrue();
        pattern.IsMatch("abc abc").ShouldBeTrue();
        pattern.IsMatch("abc def").ShouldBeFalse();
    }

    [Fact]
    public void Conditional_ByName_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With
            .Conditional("prefix", Pattern.With.Literal("yes"), Pattern.With.Literal("no"));
        pattern.ToString().ShouldBe("(?(prefix)yes|no)");
    }

    [Fact]
    public void Conditional_ByNumber_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With
            .Conditional(1, Pattern.With.Literal("yes"), Pattern.With.Literal("no"));
        pattern.ToString().ShouldBe("(?(1)yes|no)");
    }

    [Fact]
    public void MultipleNamedGroups_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With
            .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
            .Literal("-")
            .NamedGroup("exchange", Pattern.With.Digit.Repeat.Times(3))
            .Literal("-")
            .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4));

        pattern.ToString().ShouldBe(@"(?<area>\d{3})-(?<exchange>\d{3})-(?<number>\d{4})");
    }

    [Fact]
    public void NestedGroups_WorkCorrectly()
    {
        Pattern pattern = Pattern.With
            .Group(
                Pattern.With
                    .NamedGroup("inner", Pattern.With.Digit.Repeat.OneOrMore)
            );

        pattern.ToString().ShouldBe(@"((?<inner>\d+))");
    }

    [Fact]
    public void AtomicGroup_GreedyContentFollowedBySameToken_DoesNotBacktrack()
    {
        Pattern atomic = Pattern.With.StartOfLine.AtomicGroup(Pattern.With.Digit.Repeat.OneOrMore).Digit.EndOfLine;
        Pattern capturing = Pattern.With.StartOfLine.Group(Pattern.With.Digit.Repeat.OneOrMore).Digit.EndOfLine;

        bool atomicMatches = atomic.IsMatch("123");
        bool capturingMatches = capturing.IsMatch("123");

        atomicMatches.ShouldBeFalse();
        capturingMatches.ShouldBeTrue();
    }

    [Fact]
    public void NonCapturingGroup_Match_DoesNotCreateGroup()
    {
        Pattern pattern = Pattern.With.NonCapturingGroup(Pattern.With.Literal("ab")).Digit;

        Match match = pattern.Match("ab1");

        match.Success.ShouldBeTrue();
        match.Groups.Count.ShouldBe(1);
    }

    [Fact]
    public void NamedGroup_Match_CapturesValueByName()
    {
        Pattern pattern = Pattern.With.Literal("v").NamedGroup("version", Pattern.With.Digit.Repeat.OneOrMore);

        Match match = pattern.Match("release v42");

        match.Groups["version"].Value.ShouldBe("42");
    }

    [Theory]
    [InlineData("<123>", true)]
    [InlineData("123", true)]
    [InlineData("<123", false)]
    [InlineData("123>", false)]
    public void Conditional_ByName_AppliesBranchBasedOnGroupCapture(string input, bool expected)
    {
        Pattern pattern = Pattern.With
            .StartOfLine
            .NamedGroup("open", Pattern.With.Literal("<")).Repeat.Optional
            .Digit.Repeat.OneOrMore
            .Conditional("open", Pattern.With.Literal(">"), Pattern.With)
            .EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("(42)", true)]
    [InlineData("42", true)]
    [InlineData("(42", false)]
    [InlineData("42)", false)]
    public void Conditional_ByNumber_AppliesBranchBasedOnGroupCapture(string input, bool expected)
    {
        Pattern pattern = Pattern.With
            .StartOfLine
            .Group(Pattern.With.Literal("(")).Repeat.Optional
            .Digit.Repeat.OneOrMore
            .Conditional(1, Pattern.With.Literal(")"), Pattern.With)
            .EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Fact]
    public void Backreference_ByNumber_RequiresIdenticalRepeat()
    {
        Pattern pattern = Pattern.With.StartOfLine.Group(Pattern.With.Word).Backreference(1).EndOfLine;

        bool matchesDouble = pattern.IsMatch("aa");
        bool matchesDifferent = pattern.IsMatch("ab");

        matchesDouble.ShouldBeTrue();
        matchesDifferent.ShouldBeFalse();
    }

    [Fact]
    public void Backreference_ToUndefinedGroup_ProducesInvalidPattern()
    {
        Pattern pattern = Pattern.With.Digit.Backreference("missing");

        bool isValid = pattern.IsValid();

        isValid.ShouldBeFalse();
    }
}
