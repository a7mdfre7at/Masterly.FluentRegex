using System;
using System.Linq;

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

    [Theory]
    [InlineData(".", "Any character (except newline)")]
    [InlineData("*", "Zero or more")]
    [InlineData("*?", "Zero or more (lazy)")]
    [InlineData("+?", "One or more (lazy)")]
    [InlineData(@"\", "Escape character")]
    [InlineData(@"\D", "Non-digit [^0-9]")]
    [InlineData(@"\w", "Word character [a-zA-Z0-9_]")]
    [InlineData(@"\W", "Non-word character")]
    [InlineData(@"\s", "Whitespace")]
    [InlineData(@"\S", "Non-whitespace")]
    [InlineData(@"\b", "Word boundary")]
    [InlineData(@"\B", "Non-word boundary")]
    [InlineData(@"\t", "Tab")]
    [InlineData(@"\n", "Newline")]
    [InlineData(@"\r", "Carriage return")]
    [InlineData(@"\.", "Escaped literal '.'")]
    [InlineData(@"\2", "Backreference to group 2")]
    [InlineData(@"\k", "Backreference")]
    [InlineData(@"\k<name>", "Backreference to named group 'name'")]
    [InlineData("[^a-z]", "Any character NOT in: [a-z]")]
    [InlineData("[", "Character class start")]
    [InlineData("(", "Capturing group")]
    [InlineData("(?", "Group start")]
    [InlineData("(?:", "Non-capturing group")]
    [InlineData("(?>", "Atomic group (no backtracking)")]
    [InlineData("(?<", "Lookbehind or named group")]
    [InlineData("(?<=", "Positive lookbehind (must precede)")]
    [InlineData("(?<!", "Negative lookbehind (must NOT precede)")]
    [InlineData("(?<word>", "Named capturing group 'word'")]
    [InlineData("{", "Quantifier")]
    [InlineData("{3}", "Exactly 3 times")]
    [InlineData("{3}?", "Exactly 3 times (lazy)")]
    [InlineData("{2,5}?", "Between 2 and 5 times (lazy)")]
    [InlineData("{2,}?", "At least 2 times (lazy)")]
    [InlineData("{,3}", "At most 3 times")]
    [InlineData("|", "OR (alternation)")]
    [InlineData("a", "Literal 'a'")]
    [InlineData("7", "Literal '7'")]
    [InlineData("-", "Character '-'")]
    [InlineData(" ", "Character ' '")]
    public void Explain_SingleConstruct_ProducesExactExplanation(string regex, string expectedLine)
    {
        Pattern pattern = Pattern.With.RegEx(regex);

        string explanation = pattern.Explain();

        explanation.ShouldBe(ExpectedExplanation(regex, $"  - {expectedLine}"));
    }

    public static TheoryData<string, string[]> MalformedConstructs => new TheoryData<string, string[]>
    {
        { "[]", new[] { "  - Character class start", "  - Character ']'" } },
        { "[abc", new[] { "  - Character class start", "  - Literal 'a'", "  - Literal 'b'", "  - Literal 'c'" } },
        { "{}", new[] { "  - Quantifier", "  - Character '}'" } },
        { "{3", new[] { "  - Quantifier", "  - Literal '3'" } },
        { @"\k<x", new[] { "  - Backreference", "  - Character '<'", "  - Literal 'x'" } },
        { "(?<ab", new[] { "  - Lookbehind or named group", "    - Literal 'a'", "    - Literal 'b'" } },
        { "(?<>", new[] { "  - Lookbehind or named group", "    - Character '>'" } },
        { "(?i", new[] { "  - Special group", "    - Literal 'i'" } },
        { ")a", new[] { "  - End of group", "  - Literal 'a'" } },
        { "))a", new[] { "  - End of group", "  - End of group", "  - Literal 'a'" } },
    };

    [Theory]
    [MemberData(nameof(MalformedConstructs))]
    public void Explain_MalformedOrUnbalancedConstruct_FallsBackWithoutThrowing(string regex, string[] expectedLines)
    {
        Pattern pattern = Pattern.With.RegEx(regex);

        string explanation = pattern.Explain();

        explanation.ShouldBe(ExpectedExplanation(regex, expectedLines));
    }

    [Fact]
    public void Explain_EmptyPattern_ReturnsHeaderOnly()
    {
        Pattern pattern = Pattern.With;

        string explanation = pattern.Explain();

        explanation.ShouldBe(ExpectedExplanation(string.Empty));
    }

    [Fact]
    public void Explain_NestedGroups_IndentsInnerConstructsPerLevel()
    {
        Pattern pattern = Pattern.With
            .AtomicGroup(Pattern.With.NonCapturingGroup(Pattern.With.Anything))
            .NonDigit;

        string explanation = pattern.Explain();

        explanation.ShouldContain($"{Environment.NewLine}  - Atomic group (no backtracking){Environment.NewLine}");
        explanation.ShouldContain($"{Environment.NewLine}    - Non-capturing group{Environment.NewLine}");
        explanation.ShouldContain($"{Environment.NewLine}      - Any character (except newline){Environment.NewLine}");
        explanation.ShouldEndWith($"{Environment.NewLine}  - Non-digit [^0-9]{Environment.NewLine}");
    }

    [Fact]
    public void Explain_EscapedSpecialCharacterFromLiteral_DescribesEscapedLiteral()
    {
        Pattern pattern = Pattern.With.Literal("{");

        string explanation = pattern.Explain();

        explanation.ShouldBe(ExpectedExplanation(@"\{", "  - Escaped literal '{'"));
    }

    [Fact]
    public void Explain_InvalidPattern_ExplainsCharactersWithoutThrowing()
    {
        Pattern pattern = Pattern.With.RegEx("[x(");

        string explanation = pattern.Explain();

        explanation.ShouldBe(ExpectedExplanation("[x(", "  - Character class start", "  - Literal 'x'", "  - Capturing group"));
    }

    #endregion

    #region Validation Edge Cases

    [Fact]
    public void IsValid_EmptyPattern_ReturnsTrue()
    {
        Pattern pattern = Pattern.With;

        bool isValid = pattern.IsValid();

        isValid.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_TrailingBackslash_ReturnsFalse()
    {
        Pattern pattern = Pattern.With.Digit.RegEx(@"\");

        bool isValid = pattern.IsValid();

        isValid.ShouldBeFalse();
    }

    [Fact]
    public void GetGroupNames_InvalidPattern_ThrowsArgumentException()
    {
        Pattern pattern = Pattern.With.RegEx("(unclosed");

        Should.Throw<ArgumentException>(() => pattern.GetGroupNames());
    }

    [Fact]
    public void GetGroupCount_InvalidPattern_ThrowsArgumentException()
    {
        Pattern pattern = Pattern.With.RegEx("(unclosed");

        Should.Throw<ArgumentException>(() => pattern.GetGroupCount());
    }

    [Fact]
    public void GetGroupNames_MultipleNamedGroups_ReturnsNamesInDeclarationOrder()
    {
        Pattern pattern = Pattern.With
            .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
            .Group(Pattern.With.Literal("-"))
            .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2));

        string[] names = pattern.GetGroupNames();

        names.ShouldBe(new[] { "year", "month" });
    }

    [Fact]
    public void GetGroupCount_LookaroundsAndAtomicGroups_AreNotCounted()
    {
        Pattern pattern = Pattern.With
            .PositiveLookahead(Pattern.With.Digit)
            .NegativeLookbehind(Pattern.With.Word)
            .AtomicGroup(Pattern.With.Digit)
            .Group(Pattern.With.Digit);

        int count = pattern.GetGroupCount();

        count.ShouldBe(1);
    }

    #endregion

    private static string ExpectedExplanation(string regex, params string[] lines)
    {
        string body = string.Concat(lines.Select(line => line + Environment.NewLine));
        return $"Pattern: {regex}{Environment.NewLine}{Environment.NewLine}Explanation:{Environment.NewLine}{body}";
    }
}
