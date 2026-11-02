namespace Masterly.FluentRegex.Tests;

public class SimplePatternTests
{
    [Fact]
    public void StartOfLine()
    {
        Pattern pattern = Pattern.With.StartOfLine;

        pattern.ToString().ShouldBe("^");
    }

    [Fact]
    public void EndOfLine()
    {
        Pattern pattern = Pattern.With.EndOfLine;
        pattern.ToString().ShouldBe("$");
    }

    [Fact]
    public void Anything()
    {
        Pattern pattern = Pattern.With.Anything;
        pattern.ToString().ShouldBe(".");
    }

    [Fact]
    public void Literal()
    {
        Pattern pattern = Pattern.With.Literal("a");
        pattern.ToString().ShouldBe("a");
    }

    [Fact]
    public void Digit()
    {
        Pattern pattern = Pattern.With.Digit;
        pattern.ToString().ShouldBe("\\d");
    }

    [Fact]
    public void NonDigit()
    {
        Pattern pattern = Pattern.With.NonDigit;
        pattern.ToString().ShouldBe("\\D");
    }

    [Fact]
    public void Word()
    {
        Pattern pattern = Pattern.With.Word;
        pattern.ToString().ShouldBe("\\w");
    }

    [Fact]
    public void NonWord()
    {
        Pattern pattern = Pattern.With.NonWord;
        pattern.ToString().ShouldBe("\\W");
    }

    [Fact]
    public void WordBoundary()
    {
        Pattern pattern = Pattern.With.WordBoundary;
        pattern.ToString().ShouldBe("\\b");
    }

    [Fact]
    public void Letter()
    {
        Pattern pattern = Pattern.With.Letter;
        pattern.ToString().ShouldBe("[a-zA-Z]");
        pattern.IsMatch("q").ShouldBeTrue();
        pattern.IsMatch("5").ShouldBeFalse();
    }

    [Fact]
    public void LowercaseLetter()
    {
        Pattern pattern = Pattern.With.LowercaseLetter;
        pattern.ToString().ShouldBe("[a-z]");
    }

    [Fact]
    public void UppercaseLetter()
    {
        Pattern pattern = Pattern.With.UppercaseLetter;
        pattern.ToString().ShouldBe("[A-Z]");
    }

    [Fact]
    public void Whitespace()
    {
        Pattern pattern = Pattern.With.Whitespace;
        pattern.ToString().ShouldBe("\\s");
    }

    [Fact]
    public void NonWhitespace()
    {
        Pattern pattern = Pattern.With.NonWhitespace;
        pattern.ToString().ShouldBe("\\S");
    }

    [Fact]
    public void Tab()
    {
        Pattern pattern = Pattern.With.Tab;
        pattern.ToString().ShouldBe("\\t");
    }

    [Fact]
    public void CarriageReturn()
    {
        Pattern pattern = Pattern.With.CarriageReturn;
        pattern.ToString().ShouldBe("\\r");
    }

    [Fact]
    public void Newline()
    {
        Pattern pattern = Pattern.With.Newline;
        pattern.ToString().ShouldBe("\\n");
    }

    [Fact]
    public void ToString_NewPattern_ReturnsEmptyString()
    {
        Pattern pattern = Pattern.With;

        string regex = pattern.ToString();

        regex.ShouldBeEmpty();
    }

    [Fact]
    public void With_AccessedTwice_ReturnsIndependentInstances()
    {
        Pattern first = Pattern.With;
        Pattern second = Pattern.With;

        first.Digit.Literal("x");

        first.ShouldNotBeSameAs(second);
        second.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void RegEx_RawExpression_IsAppendedVerbatim()
    {
        Pattern pattern = Pattern.With.Digit.RegEx("[a-z]+(?=!)");

        string regex = pattern.ToString();

        regex.ShouldBe(@"\d[a-z]+(?=!)");
    }

    [Fact]
    public void RegEx_EmptyString_LeavesPatternUnchanged()
    {
        Pattern pattern = Pattern.With.Digit.RegEx(string.Empty);

        string regex = pattern.ToString();

        regex.ShouldBe(@"\d");
    }

    [Theory]
    [InlineData("^", @"\^")]
    [InlineData("$", @"\$")]
    [InlineData(".", @"\.")]
    [InlineData("|", @"\|")]
    [InlineData("{", @"\{")]
    [InlineData("}", @"\}")]
    [InlineData("[", @"\[")]
    [InlineData("]", @"\]")]
    [InlineData("(", @"\(")]
    [InlineData(")", @"\)")]
    [InlineData("*", @"\*")]
    [InlineData("+", @"\+")]
    [InlineData("?", @"\?")]
    [InlineData(@"\", @"\\")]
    public void Literal_SpecialCharacter_IsEscaped(string literal, string expectedRegex)
    {
        Pattern pattern = Pattern.With.Literal(literal);

        string regex = pattern.ToString();

        regex.ShouldBe(expectedRegex);
    }

    [Theory]
    [InlineData("^")]
    [InlineData("$")]
    [InlineData(".")]
    [InlineData("|")]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("[")]
    [InlineData("]")]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData("*")]
    [InlineData("+")]
    [InlineData("?")]
    [InlineData(@"\")]
    public void Literal_SpecialCharacter_MatchesOnlyThatCharacter(string literal)
    {
        Pattern pattern = Pattern.With.StartOfLine.Literal(literal).EndOfLine;

        bool matchesItself = pattern.IsMatch(literal);
        bool matchesOther = pattern.IsMatch("a");

        matchesItself.ShouldBeTrue();
        matchesOther.ShouldBeFalse();
    }

    [Theory]
    [InlineData("-")]
    [InlineData("#")]
    [InlineData("a")]
    [InlineData("7")]
    [InlineData("/")]
    public void Literal_OrdinaryCharacter_IsAppendedUnescaped(string literal)
    {
        Pattern pattern = Pattern.With.Literal(literal);

        string regex = pattern.ToString();

        regex.ShouldBe(literal);
    }

    [Fact]
    public void Literal_MixedString_EscapesOnlySpecialCharacters()
    {
        Pattern pattern = Pattern.With.Literal("a.b*c");

        string regex = pattern.ToString();

        regex.ShouldBe(@"a\.b\*c");
    }

    [Fact]
    public void Literal_CharArray_EscapesOnlySpecialCharacters()
    {
        Pattern pattern = Pattern.With.Literal('1', '+', '1');

        string regex = pattern.ToString();

        regex.ShouldBe(@"1\+1");
    }

    [Fact]
    public void Literal_EmptyString_LeavesPatternUnchanged()
    {
        Pattern pattern = Pattern.With.Digit.Literal(string.Empty);

        string regex = pattern.ToString();

        regex.ShouldBe(@"\d");
    }

    [Fact]
    public void Literal_NoCharacters_LeavesPatternUnchanged()
    {
        Pattern pattern = Pattern.With.Digit.Literal();

        string regex = pattern.ToString();

        regex.ShouldBe(@"\d");
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("Z", true)]
    [InlineData("5", false)]
    [InlineData("_", false)]
    public void Letter_InsideSet_MatchesAsciiLetters(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.Set(Pattern.With.Letter).EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("A", false)]
    public void LowercaseLetter_InsideSet_MatchesLowercaseOnly(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.Set(Pattern.With.LowercaseLetter).EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("a", false)]
    public void UppercaseLetter_InsideSet_MatchesUppercaseOnly(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.Set(Pattern.With.UppercaseLetter).EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("\t", true)]
    [InlineData(" ", false)]
    public void Tab_Input_MatchesTabCharacterOnly(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.Tab.EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Fact]
    public void CarriageReturnNewline_Input_MatchesCrLfSequence()
    {
        Pattern pattern = Pattern.With.CarriageReturn.Newline;

        bool matchesCrLf = pattern.IsMatch("a\r\nb");
        bool matchesLf = pattern.IsMatch("a\nb");

        matchesCrLf.ShouldBeTrue();
        matchesLf.ShouldBeFalse();
    }

    [Theory]
    [InlineData("cat", true)]
    [InlineData("a cat sat", true)]
    [InlineData("concatenate", false)]
    public void WordBoundary_AroundLiteral_MatchesWholeWordOnly(string input, bool expected)
    {
        Pattern pattern = Pattern.With.WordBoundary.Literal("cat").WordBoundary;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("\n", false)]
    public void Anything_Input_MatchesAnyCharacterExceptNewline(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.Anything.EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("x", true)]
    [InlineData("5", false)]
    [InlineData(" ", true)]
    public void NonDigit_Input_MatchesNonDigitCharacters(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.NonDigit.EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("!", true)]
    [InlineData("_", false)]
    public void NonWord_Input_MatchesNonWordCharacters(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.NonWord.EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("x", true)]
    [InlineData(" ", false)]
    [InlineData("\t", false)]
    public void NonWhitespace_Input_MatchesNonWhitespaceCharacters(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfLine.NonWhitespace.EndOfLine;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }
}
