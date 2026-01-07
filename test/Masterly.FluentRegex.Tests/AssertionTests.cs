namespace Masterly.FluentRegex.Tests;

public class AssertionTests
{
    [Fact]
    public void PositiveLookahead_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With.Literal("q").PositiveLookahead(Pattern.With.Literal("u"));
        pattern.ToString().ShouldBe("q(?=u)");
    }

    [Fact]
    public void PositiveLookahead_MatchesCorrectly()
    {
        // Match 'q' only if followed by 'u'
        Pattern pattern = Pattern.With.Literal("q").PositiveLookahead(Pattern.With.Literal("u"));

        var match = pattern.Match("queen");
        match.Success.ShouldBeTrue();
        match.Value.ShouldBe("q"); // Only 'q' is captured, not 'u'

        pattern.IsMatch("iraq").ShouldBeFalse(); // 'q' not followed by 'u'
    }

    [Fact]
    public void NegativeLookahead_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With.Literal("q").NegativeLookahead(Pattern.With.Literal("u"));
        pattern.ToString().ShouldBe("q(?!u)");
    }

    [Fact]
    public void NegativeLookahead_MatchesCorrectly()
    {
        // Match 'q' only if NOT followed by 'u'
        Pattern pattern = Pattern.With.Literal("q").NegativeLookahead(Pattern.With.Literal("u"));

        pattern.IsMatch("iraq").ShouldBeTrue();
        pattern.IsMatch("queen").ShouldBeFalse();
    }

    [Fact]
    public void PositiveLookbehind_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With.PositiveLookbehind(Pattern.With.Literal("$")).Digit.Repeat.OneOrMore;
        pattern.ToString().ShouldBe(@"(?<=\$)\d+");
    }

    [Fact]
    public void PositiveLookbehind_MatchesCorrectly()
    {
        // Match digits preceded by '$'
        Pattern pattern = Pattern.With.PositiveLookbehind(Pattern.With.Literal("$")).Digit.Repeat.OneOrMore;

        var match = pattern.Match("Price: $100");
        match.Success.ShouldBeTrue();
        match.Value.ShouldBe("100"); // '$' is not included in match

        pattern.IsMatch("Price: 100").ShouldBeFalse(); // No '$' before digits
    }

    [Fact]
    public void NegativeLookbehind_CreatesCorrectPattern()
    {
        Pattern pattern = Pattern.With.NegativeLookbehind(Pattern.With.Literal("-")).Digit.Repeat.OneOrMore;
        pattern.ToString().ShouldBe(@"(?<!-)\d+");
    }

    [Fact]
    public void NegativeLookbehind_MatchesCorrectly()
    {
        // Match digits NOT preceded by '-'
        Pattern pattern = Pattern.With.NegativeLookbehind(Pattern.With.Literal("-")).Digit.Repeat.OneOrMore;

        var match = pattern.Match("123");
        match.Success.ShouldBeTrue();
        match.Value.ShouldBe("123");

        // In "-456", only "56" matches (after the first digit which is preceded by -)
        var match2 = pattern.Match("-456");
        match2.Success.ShouldBeTrue();
        match2.Value.ShouldBe("56");
    }

    [Fact]
    public void PasswordValidation_UsingMultipleLookaheads()
    {
        // Password must have: lowercase, uppercase, digit, and be 8+ chars
        Pattern pattern = Pattern.With
            .StartOfLine
            .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.LowercaseLetter))
            .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.UppercaseLetter))
            .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Digit)
            .Anything.Repeat.AtLeast(8)
            .EndOfLine;

        pattern.IsMatch("Password1").ShouldBeTrue();
        pattern.IsMatch("StrongPass123").ShouldBeTrue();
        pattern.IsMatch("weakpass").ShouldBeFalse();   // No uppercase or digit
        pattern.IsMatch("ALLCAPS1").ShouldBeFalse();   // No lowercase
        pattern.IsMatch("Short1").ShouldBeFalse();     // Less than 8 chars
    }

    [Fact]
    public void CombinedLookaheadAndLookbehind()
    {
        // Match word characters between parentheses
        Pattern pattern = Pattern.With
            .PositiveLookbehind(Pattern.With.Literal("("))
            .Word.Repeat.OneOrMore
            .PositiveLookahead(Pattern.With.Literal(")"));

        var match = pattern.Match("Hello (World) Test");
        match.Success.ShouldBeTrue();
        match.Value.ShouldBe("World");
    }

    [Fact]
    public void LookaheadWithDigits()
    {
        // Match a digit only if followed by more digits
        Pattern pattern = Pattern.With.Digit.PositiveLookahead(Pattern.With.Digit.Repeat.OneOrMore);

        var matches = pattern.Matches("a1234b");
        matches.Count.ShouldBe(3); // 1, 2, 3 (but not 4, which isn't followed by digits)
    }

    [Fact]
    public void NegativeLookahead_NotFollowedByWord()
    {
        // Match 'foo' only if NOT followed by 'bar'
        Pattern pattern = Pattern.With.Literal("foo").NegativeLookahead(Pattern.With.Literal("bar"));

        pattern.IsMatch("foobar").ShouldBeFalse();
        pattern.IsMatch("foobaz").ShouldBeTrue();
        pattern.IsMatch("foo").ShouldBeTrue();
    }

    [Fact]
    public void MultipleLookaheads_ChainedCorrectly()
    {
        Pattern pattern = Pattern.With
            .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Literal("a"))
            .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Literal("b"))
            .Anything.Repeat.OneOrMore;

        // String must contain both 'a' and 'b' somewhere
        pattern.IsMatch("abc").ShouldBeTrue();
        pattern.IsMatch("bca").ShouldBeTrue();
        pattern.IsMatch("xyz").ShouldBeFalse();
    }
}
