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

        pattern.ToString().ShouldBe("a{,3}");
    }
}
