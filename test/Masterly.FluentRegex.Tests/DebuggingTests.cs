using System;
using System.Linq;
using System.Text.RegularExpressions;

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

    [Fact]
    public void IsValid_WithIgnorePatternWhitespace_TreatsTheHashAsAComment()
    {
        Pattern pattern = Pattern.With.RegEx("a # [ comment").WithOptions(RegexOptions.IgnorePatternWhitespace);

        bool isValid = pattern.IsValid();

        isValid.ShouldBeTrue();
        pattern.IsMatch("a").ShouldBeTrue();
    }

    [Fact]
    public void IsValid_WithNonBacktrackingAndABackreference_IsFalse()
    {
        Pattern pattern = Pattern.FromRegex(@"(\w)\1").WithOptions(RegexOptions.NonBacktracking);

        bool isValid = pattern.TryValidate(out string error);

        isValid.ShouldBeFalse();
        error.ShouldNotBeNullOrEmpty();
        Should.Throw<NotSupportedException>(() => pattern.IsMatch("aa"));
    }

    [Fact]
    public void IsValid_WithEcmaScriptAndSingleline_IsFalse()
    {
        Pattern pattern = Pattern.With.Digit.WithOptions(RegexOptions.ECMAScript | RegexOptions.Singleline);

        bool isValid = pattern.IsValid();

        isValid.ShouldBeFalse();
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
    [InlineData(@"\D", "Non-digit")]
    [InlineData(@"\w", "Word character (any Unicode letter, digit or _)")]
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
    [InlineData("{", "Character '{'")]
    [InlineData("{3}", "Exactly 3 times")]
    [InlineData("{3}?", "Exactly 3 times (lazy)")]
    [InlineData("{2,5}?", "Between 2 and 5 times (lazy)")]
    [InlineData("{2,}?", "At least 2 times (lazy)")]
    [InlineData("??", "Optional (lazy)")]
    [InlineData(@"\A", "Start of string")]
    [InlineData(@"\z", "End of string")]
    [InlineData(@"\Z", "End of string (or before a final newline)")]
    [InlineData(@"\G", "Where the previous match ended")]
    [InlineData(@"\f", "Form feed")]
    [InlineData(@"\v", "Vertical tab")]
    [InlineData(@"\a", "Bell character")]
    [InlineData(@"\e", "Escape character (U+001B)")]
    [InlineData(@"\x41", "Character 'A' (U+0041)")]
    [InlineData("\\u00e9", "Character 'é' (U+00E9)")]
    [InlineData(@"\x0A", "Character U+000A")]
    [InlineData(@"\cM", "Control character Ctrl+M")]
    [InlineData(@"\0", "Character U+0000")]
    [InlineData(@"\101", "Character 'A' (U+0041)")]
    [InlineData(@"\p{Lu}", "Any character in Unicode category or block 'Lu'")]
    [InlineData(@"\P{IsArabic}", "Any character NOT in Unicode category or block 'IsArabic'")]
    [InlineData(@"\k'name'", "Backreference to named group 'name'")]
    [InlineData(@"[\]a]", @"Any character in: [\]a]")]
    [InlineData("[]a]", "Any character in: []a]")]
    [InlineData("[a-z-[aeiou]]", "Any character in: [a-z-[aeiou]]")]
    [InlineData("(?#note)", "Comment: note")]
    [InlineData("(?i)", "Inline options for the rest of the group (on: ignore case)")]
    [InlineData("(?s-im)", "Inline options for the rest of the group (on: single line; off: ignore case, multiline)")]
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
        { "{}", new[] { "  - Character '{'", "  - Character '}'" } },
        { "{3", new[] { "  - Character '{'", "  - Literal '3'" } },
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
    public void Explain_DigitAndWordClasses_WithoutEcmaScript_AreDescribedAsUnicodeAware()
    {
        Pattern pattern = Pattern.FromRegex(@"\d\D\w");

        string explanation = pattern.Explain();

        explanation.ShouldBe(ExpectedExplanation(
            @"\d\D\w",
            "  - Digit (any Unicode decimal digit)",
            "  - Non-digit",
            "  - Word character (any Unicode letter, digit or _)"));
    }

    [Fact]
    public void Explain_DigitAndWordClasses_WithEcmaScript_AreDescribedAsAscii()
    {
        Pattern pattern = Pattern.FromRegex(@"\d\D\w").WithOptions(RegexOptions.ECMAScript);

        string explanation = pattern.Explain();

        explanation.ShouldBe(ExpectedExplanation(
            @"\d\D\w",
            "  - Digit [0-9]",
            "  - Non-digit [^0-9]",
            "  - Word character [a-zA-Z0-9_]"));
    }

    [Fact]
    public void Explain_GroupConditional_DescribesConditionAndOtherwiseBranch()
    {
        Pattern pattern = Pattern.FromRegex(@"(\()?(?(1)\)|x)");

        string explanation = pattern.Explain();

        explanation.ShouldBe(ExpectedExplanation(
            @"(\()?(?(1)\)|x)",
            "  - Capturing group",
            "    - Escaped literal '('",
            "    - End of group",
            "  - Optional (zero or one)",
            "  - Conditional: if group 1 has matched, use the first branch",
            "    - Escaped literal ')'",
            "    - Otherwise",
            "    - Literal 'x'",
            "    - End of group"));
    }

    [Fact]
    public void Explain_NamedGroupConditional_NamesTheGroup()
    {
        Pattern pattern = Pattern.FromRegex("(?<open>a)?(?(open)b|c)");

        string explanation = pattern.Explain();

        explanation.ShouldContain("  - Conditional: if group 'open' has matched, use the first branch");
        explanation.ShouldContain("    - Otherwise");
        explanation.ShouldNotContain("Special group");
        explanation.ShouldNotContain("OR (alternation)");
    }

    [Fact]
    public void Explain_ExpressionConditional_ExplainsTheLookaroundAsTheCondition()
    {
        Pattern pattern = Pattern.FromRegex(@"(?(?=\d)\d{3}|[a-z]+)");

        string explanation = pattern.Explain();

        explanation.ShouldBe(ExpectedExplanation(
            @"(?(?=\d)\d{3}|[a-z]+)",
            "  - Conditional: if the lookaround below matches, use the first branch",
            "    - Positive lookahead (must follow)",
            "      - Digit (any Unicode decimal digit)",
            "      - End of group",
            "    - Digit (any Unicode decimal digit)",
            "    - Exactly 3 times",
            "    - Otherwise",
            "    - Any character in: [a-z]",
            "    - One or more",
            "    - End of group"));
    }

    [Fact]
    public void Explain_AlternationInsideAGroupWithinAConditional_StaysAnAlternation()
    {
        Pattern pattern = Pattern.FromRegex("(a)?(?(1)(?:b|c)|d)");

        string explanation = pattern.Explain();

        explanation.ShouldContain("      - OR (alternation)");
        explanation.ShouldContain("    - Otherwise");
    }

    [Fact]
    public void Explain_ScopedInlineOptions_IsAGroupWithOptions()
    {
        Pattern pattern = Pattern.FromRegex("(?i:ab)c");

        string explanation = pattern.Explain();

        explanation.ShouldBe(ExpectedExplanation(
            "(?i:ab)c",
            "  - Group with inline options (on: ignore case)",
            "    - Literal 'a'",
            "    - Literal 'b'",
            "    - End of group",
            "  - Literal 'c'"));
    }

    [Fact]
    public void Explain_QuotedNamedAndBalancingGroups_AreNamed()
    {
        Pattern pattern = Pattern.FromRegex("(?'open'<)(?<close-open>>)");

        string explanation = pattern.Explain();

        explanation.ShouldContain("  - Named capturing group 'open'");
        explanation.ShouldContain(
            "  - Balancing group 'close' (captures the text since the last capture of 'open' and removes it)");
    }

    [Fact]
    public void Explain_TwoDigitBackreference_WhenTheGroupExists_IsOneBackreference()
    {
        Pattern pattern = Pattern.FromRegex(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)(l)\12");

        string explanation = pattern.Explain();

        explanation.ShouldEndWith($"  - Backreference to group 12{Environment.NewLine}");
    }

    [Fact]
    public void Explain_TwoDigitEscape_WhenTheGroupDoesNotExist_IsAnOctalCharacter()
    {
        Pattern pattern = Pattern.FromRegex(@"(a)\12");

        string explanation = pattern.Explain();

        explanation.ShouldEndWith($"  - Character U+000A{Environment.NewLine}");
    }

    [Fact]
    public void Explain_UnicodeCategoryBraces_AreNotAQuantifier()
    {
        Pattern pattern = Pattern.FromRegex(@"\p{Lu}{2}");

        string explanation = pattern.Explain();

        explanation.ShouldBe(ExpectedExplanation(
            @"\p{Lu}{2}",
            "  - Any character in Unicode category or block 'Lu'",
            "  - Exactly 2 times"));
    }

    [Fact]
    public void Explain_BracesThatAreNotAQuantifier_AreLiteralCharacters()
    {
        Pattern pattern = Pattern.FromRegex("a{,3}");

        string explanation = pattern.Explain();

        explanation.ShouldBe(ExpectedExplanation(
            "a{,3}",
            "  - Literal 'a'",
            "  - Character '{'",
            "  - Character ','",
            "  - Literal '3'",
            "  - Character '}'"));
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
        explanation.ShouldEndWith($"{Environment.NewLine}  - Non-digit{Environment.NewLine}");
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
