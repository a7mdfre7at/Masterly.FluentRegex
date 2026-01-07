namespace Masterly.FluentRegex.Tests;

public class DebuggingTests
{
    #region IsValid Tests

    [Fact]
    public void IsValid_ReturnsTrue_ForValidPattern()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        pattern.IsValid().ShouldBeTrue();
    }

    [Fact]
    public void IsValid_ReturnsTrue_ForComplexValidPattern()
    {
        Pattern pattern = Pattern.With
            .StartOfLine
            .NamedGroup("test", Pattern.With.Word.Repeat.OneOrMore)
            .PositiveLookahead(Pattern.With.Digit)
            .EndOfLine;
        pattern.IsValid().ShouldBeTrue();
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForInvalidPattern()
    {
        Pattern pattern = Pattern.With.RegEx("[invalid");
        pattern.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForUnclosedGroup()
    {
        Pattern pattern = Pattern.With.RegEx("(unclosed");
        pattern.IsValid().ShouldBeFalse();
    }

    #endregion

    #region TryValidate Tests

    [Fact]
    public void TryValidate_ReturnsTrue_AndNullError_ForValidPattern()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;

        bool isValid = pattern.TryValidate(out string error);

        isValid.ShouldBeTrue();
        error.ShouldBeNull();
    }

    [Fact]
    public void TryValidate_ReturnsFalse_AndErrorMessage_ForInvalidPattern()
    {
        Pattern pattern = Pattern.With.RegEx("[invalid");

        bool isValid = pattern.TryValidate(out string error);

        isValid.ShouldBeFalse();
        error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void TryValidate_ErrorMessage_ContainsUsefulInfo()
    {
        Pattern pattern = Pattern.With.RegEx("(unclosed");

        pattern.TryValidate(out string error);

        error.ShouldContain("("); // Should mention the problematic character/construct
    }

    #endregion

    #region GetGroupNames Tests

    [Fact]
    public void GetGroupNames_ReturnsNamedGroups()
    {
        Pattern pattern = Pattern.With
            .NamedGroup("first", Pattern.With.Digit)
            .NamedGroup("second", Pattern.With.Word);

        string[] names = pattern.GetGroupNames();

        names.ShouldContain("first");
        names.ShouldContain("second");
        names.Length.ShouldBe(2);
    }

    [Fact]
    public void GetGroupNames_ExcludesNumberedGroups()
    {
        Pattern pattern = Pattern.With
            .Group(Pattern.With.Digit)
            .NamedGroup("named", Pattern.With.Word);

        string[] names = pattern.GetGroupNames();

        names.ShouldContain("named");
        names.Length.ShouldBe(1); // Only the named group
    }

    [Fact]
    public void GetGroupNames_ReturnsEmpty_WhenNoNamedGroups()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;

        string[] names = pattern.GetGroupNames();

        names.ShouldBeEmpty();
    }

    #endregion

    #region GetGroupCount Tests

    [Fact]
    public void GetGroupCount_ReturnsCorrectCount()
    {
        Pattern pattern = Pattern.With
            .Group(Pattern.With.Digit)
            .Group(Pattern.With.Word)
            .Group(Pattern.With.Whitespace);

        pattern.GetGroupCount().ShouldBe(3);
    }

    [Fact]
    public void GetGroupCount_IncludesNamedGroups()
    {
        Pattern pattern = Pattern.With
            .Group(Pattern.With.Digit)
            .NamedGroup("word", Pattern.With.Word);

        pattern.GetGroupCount().ShouldBe(2);
    }

    [Fact]
    public void GetGroupCount_ExcludesNonCapturingGroups()
    {
        Pattern pattern = Pattern.With
            .Group(Pattern.With.Digit)
            .NonCapturingGroup(Pattern.With.Word);

        pattern.GetGroupCount().ShouldBe(1);
    }

    [Fact]
    public void GetGroupCount_ReturnsZero_WhenNoGroups()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;

        pattern.GetGroupCount().ShouldBe(0);
    }

    #endregion

    #region Explain Tests

    [Fact]
    public void Explain_ReturnsPatternInOutput()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;

        string explanation = pattern.Explain();

        explanation.ShouldContain(@"Pattern: \d+");
    }

    [Fact]
    public void Explain_ExplainsDigit()
    {
        Pattern pattern = Pattern.With.Digit;

        string explanation = pattern.Explain();

        explanation.ShouldContain("Digit");
    }

    [Fact]
    public void Explain_ExplainsStartOfLine()
    {
        Pattern pattern = Pattern.With.StartOfLine;

        string explanation = pattern.Explain();

        explanation.ShouldContain("Start of line");
    }

    [Fact]
    public void Explain_ExplainsEndOfLine()
    {
        Pattern pattern = Pattern.With.EndOfLine;

        string explanation = pattern.Explain();

        explanation.ShouldContain("End of line");
    }

    [Fact]
    public void Explain_ExplainsQuantifiers()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;

        string explanation = pattern.Explain();

        explanation.ShouldContain("One or more");
    }

    [Fact]
    public void Explain_ExplainsZeroOrMore()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.ZeroOrMore;

        string explanation = pattern.Explain();

        explanation.ShouldContain("Zero or more");
    }

    [Fact]
    public void Explain_ExplainsOptional()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.Optional;

        string explanation = pattern.Explain();

        explanation.ShouldContain("Optional");
    }

    [Fact]
    public void Explain_ExplainsCapturingGroup()
    {
        Pattern pattern = Pattern.With.Group(Pattern.With.Digit);

        string explanation = pattern.Explain();

        explanation.ShouldContain("Capturing group");
    }

    [Fact]
    public void Explain_ExplainsNonCapturingGroup()
    {
        Pattern pattern = Pattern.With.NonCapturingGroup(Pattern.With.Digit);

        string explanation = pattern.Explain();

        explanation.ShouldContain("Non-capturing group");
    }

    [Fact]
    public void Explain_ExplainsNamedGroup()
    {
        Pattern pattern = Pattern.With.NamedGroup("test", Pattern.With.Digit);

        string explanation = pattern.Explain();

        explanation.ShouldContain("Named capturing group");
        explanation.ShouldContain("test");
    }

    [Fact]
    public void Explain_ExplainsPositiveLookahead()
    {
        Pattern pattern = Pattern.With.PositiveLookahead(Pattern.With.Digit);

        string explanation = pattern.Explain();

        explanation.ShouldContain("Positive lookahead");
    }

    [Fact]
    public void Explain_ExplainsNegativeLookahead()
    {
        Pattern pattern = Pattern.With.NegativeLookahead(Pattern.With.Digit);

        string explanation = pattern.Explain();

        explanation.ShouldContain("Negative lookahead");
    }

    [Fact]
    public void Explain_ExplainsPositiveLookbehind()
    {
        Pattern pattern = Pattern.With.PositiveLookbehind(Pattern.With.Digit);

        string explanation = pattern.Explain();

        explanation.ShouldContain("Positive lookbehind");
    }

    [Fact]
    public void Explain_ExplainsNegativeLookbehind()
    {
        Pattern pattern = Pattern.With.NegativeLookbehind(Pattern.With.Digit);

        string explanation = pattern.Explain();

        explanation.ShouldContain("Negative lookbehind");
    }

    [Fact]
    public void Explain_ExplainsCharacterClass()
    {
        Pattern pattern = Pattern.With.Set(Pattern.With.Digit.Letter);

        string explanation = pattern.Explain();

        explanation.ShouldContain("Any character in");
    }

    [Fact]
    public void Explain_ExplainsNegatedCharacterClass()
    {
        Pattern pattern = Pattern.With.NegatedSet(Pattern.With.Digit);

        string explanation = pattern.Explain();

        explanation.ShouldContain("Any character NOT in");
    }

    [Fact]
    public void Explain_ExplainsAlternation()
    {
        Pattern pattern = Pattern.With.Choice(Pattern.With.Digit, Pattern.With.Word);

        string explanation = pattern.Explain();

        explanation.ShouldContain("OR");
    }

    [Fact]
    public void Explain_ExplainsBackreference()
    {
        Pattern pattern = Pattern.With.Backreference(1);

        string explanation = pattern.Explain();

        explanation.ShouldContain("Backreference");
    }

    [Fact]
    public void Explain_ExplainsNamedBackreference()
    {
        Pattern pattern = Pattern.With.Backreference("test");

        string explanation = pattern.Explain();

        explanation.ShouldContain("Backreference");
        explanation.ShouldContain("test");
    }

    [Fact]
    public void Explain_ExplainsExactQuantifier()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.Times(3);

        string explanation = pattern.Explain();

        explanation.ShouldContain("Exactly 3 times");
    }

    [Fact]
    public void Explain_ExplainsRangeQuantifier()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.Times(2, 5);

        string explanation = pattern.Explain();

        explanation.ShouldContain("Between 2 and 5 times");
    }

    [Fact]
    public void Explain_ExplainsAtLeastQuantifier()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.AtLeast(3);

        string explanation = pattern.Explain();

        explanation.ShouldContain("At least 3 times");
    }

    [Fact]
    public void Explain_ComplexPattern_ContainsAllParts()
    {
        Pattern pattern = Pattern.With
            .StartOfLine
            .NamedGroup("digits", Pattern.With.Digit.Repeat.OneOrMore)
            .Literal("-")
            .Word.Repeat.ZeroOrMore
            .EndOfLine;

        string explanation = pattern.Explain();

        explanation.ShouldContain("Start of line");
        explanation.ShouldContain("Named capturing group");
        explanation.ShouldContain("digits");
        explanation.ShouldContain("Digit");
        explanation.ShouldContain("One or more");
        explanation.ShouldContain("End of line");
    }

    #endregion
}
