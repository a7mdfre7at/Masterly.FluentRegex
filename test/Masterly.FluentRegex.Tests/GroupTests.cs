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
}
