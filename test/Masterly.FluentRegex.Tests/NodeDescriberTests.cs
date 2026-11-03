using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Tests;

public class NodeDescriberTests
{
    [Theory]
    [InlineData("", "nothing (empty)")]
    [InlineData("a", "the character 'a'")]
    [InlineData(" ", "the character '␣'")]
    [InlineData("abc", "the text \"abc\"")]
    [InlineData(@"a\d", "a sequence of 2 elements")]
    [InlineData("a|b|c", "one of 3 alternatives")]
    [InlineData(@"\1", "the same text captured by group #1")]
    [InlineData(@"\k<year>", "the same text captured by group 'year'")]
    [InlineData("(?(name)a|b)", "if group 'name' matched, then one branch, otherwise the other")]
    [InlineData("(?(?=a)a|b)", "if the condition matches here, then one branch, otherwise the other")]
    [InlineData("(?i)", "turn options i on for the rest of the group")]
    [InlineData("(?i-s)", "turn options i-s on for the rest of the group")]
    [InlineData("(?#note)", "a comment")]
    [InlineData("a+", "the character 'a', one or more times")]
    public void Describe_RootNode_ReturnsEnglishDescription(string regex, string expected)
    {
        RegexNode root = RegexParser.Parse(regex).Root;

        string description = NodeDescriber.Describe(root);

        description.ShouldBe(expected);
    }

    [Theory]
    [InlineData(".", RegexOptions.None, "any character except newline")]
    [InlineData(".", RegexOptions.Singleline, "any character")]
    [InlineData(@"\d", RegexOptions.None, "a digit")]
    [InlineData(@"\D", RegexOptions.None, "a non-digit")]
    [InlineData(@"\w", RegexOptions.None, "a word character")]
    [InlineData(@"\W", RegexOptions.None, "a non-word character")]
    [InlineData(@"\s", RegexOptions.None, "a whitespace character")]
    [InlineData(@"\S", RegexOptions.None, "a non-whitespace character")]
    [InlineData(@"\p{Lu}", RegexOptions.None, "a character in Unicode uppercase letter")]
    [InlineData(@"\P{IsGreek}", RegexOptions.None, "a character not in Unicode block Greek")]
    [InlineData(@"\p{Pd}", RegexOptions.None, "a character in Unicode category Pd")]
    [InlineData("[a-c]", RegexOptions.None, "one of [a-c]")]
    [InlineData(@"[^a\d ]", RegexOptions.None, "any character except [a \\d ␣]")]
    [InlineData("[a-z-[aeiou]]", RegexOptions.None, "one of [a-z] minus [a e i o u]")]
    [InlineData("[\t\n]", RegexOptions.None, "one of [\\t \\n]")]
    public void Describe_CharacterClass_ReturnsEnglishDescription(string regex, RegexOptions options, string expected)
    {
        RegexNode root = RegexParser.Parse(regex, options).Root;

        string description = NodeDescriber.Describe(root);

        description.ShouldBe(expected);
    }

    [Theory]
    [InlineData("^", RegexOptions.None, "start of input")]
    [InlineData("^", RegexOptions.Multiline, "start of line")]
    [InlineData("$", RegexOptions.None, "end of input")]
    [InlineData("$", RegexOptions.Multiline, "end of line")]
    [InlineData(@"\A", RegexOptions.Multiline, "start of input")]
    [InlineData(@"\z", RegexOptions.Multiline, "end of input")]
    [InlineData(@"\Z", RegexOptions.None, "end of input (or before a final newline)")]
    [InlineData(@"\b", RegexOptions.None, "word boundary")]
    [InlineData(@"\B", RegexOptions.None, "not a word boundary")]
    [InlineData(@"\G", RegexOptions.None, "where the previous match ended")]
    public void Describe_Anchor_ReturnsEnglishDescription(string regex, RegexOptions options, string expected)
    {
        RegexNode root = RegexParser.Parse(regex, options).Root;

        string description = NodeDescriber.Describe(root);

        description.ShouldBe(expected);
    }

    [Theory]
    [InlineData("(a)", "capture group #1")]
    [InlineData("(?<year>a)", "group 'year'")]
    [InlineData("(?:a)", "group")]
    [InlineData("(?>a)", "atomic group (no backtracking)")]
    [InlineData("(?=a)", "followed by")]
    [InlineData("(?!a)", "not followed by")]
    [InlineData("(?<=a)", "preceded by")]
    [InlineData("(?<!a)", "not preceded by")]
    [InlineData("(?i:a)", "group with options i")]
    [InlineData("(?i-m:a)", "group with options i-m")]
    [InlineData("(?-i:a)", "group with options -i")]
    [InlineData("(?<close-open>a)", "balancing group 'close' popping 'open'")]
    [InlineData("(?<-open>a)", "balancing group popping 'open'")]
    public void Describe_Group_ReturnsEnglishDescription(string regex, string expected)
    {
        RegexNode root = RegexParser.Parse(regex).Root;

        string description = NodeDescriber.Describe(root);

        description.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a?", "optional")]
    [InlineData("a{0,1}", "optional")]
    [InlineData("a*", "zero or more times")]
    [InlineData("a{0,}", "zero or more times")]
    [InlineData("a+", "one or more times")]
    [InlineData("a{2,}", "at least 2 times")]
    [InlineData("a{3}", "exactly 3 times")]
    [InlineData("a{0,4}", "at most 4 times")]
    [InlineData("a{2,5}", "between 2 and 5 times")]
    [InlineData("a+?", "one or more times (as few as possible)")]
    [InlineData("a{2,5}?", "between 2 and 5 times (as few as possible)")]
    public void DescribeCount_Quantifier_ReturnsEnglishCount(string regex, string expected)
    {
        QuantifierNode quantifier = RegexParser.Parse(regex).Root.ShouldBeOfType<QuantifierNode>();

        string description = NodeDescriber.DescribeCount(quantifier);

        description.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a*", "0+")]
    [InlineData("a+", "1+")]
    [InlineData("a{3}", "3×")]
    [InlineData("a{2,5}", "2-5")]
    [InlineData("a?", "0-1")]
    [InlineData("a??", "0-1 lazy")]
    [InlineData("a*?", "0+ lazy")]
    public void CountLabel_Quantifier_ReturnsCompactLabel(string regex, string expected)
    {
        QuantifierNode quantifier = RegexParser.Parse(regex).Root.ShouldBeOfType<QuantifierNode>();

        string label = NodeDescriber.CountLabel(quantifier);

        label.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a", "\"a\"")]
    [InlineData(@"\t", "\"\\t\"")]
    [InlineData(".", "any char")]
    [InlineData(@"\d", "digit")]
    [InlineData(@"\D", "non-digit")]
    [InlineData(@"\w", "word char")]
    [InlineData(@"\W", "non-word char")]
    [InlineData(@"\s", "whitespace")]
    [InlineData(@"\S", "non-whitespace")]
    [InlineData(@"\p{L}", "letter")]
    [InlineData(@"\P{L}", "not letter")]
    [InlineData("[a-c_]", "one of [a-c _]")]
    [InlineData("[^a]", "none of [a]")]
    [InlineData("^", "start of input")]
    [InlineData(@"\b", "word boundary")]
    [InlineData(@"\2", "same as #2")]
    [InlineData(@"\k'name'", "same as 'name'")]
    [InlineData("(ab)", "(ab)")]
    [InlineData("a+", "a+")]
    public void Label_Node_ReturnsCompactDiagramLabel(string regex, string expected)
    {
        RegexNode root = RegexParser.Parse(regex).Root;

        string label = NodeDescriber.Label(root);

        label.ShouldBe(expected);
    }

    [Theory]
    [InlineData("L", "letter")]
    [InlineData("Lu", "uppercase letter")]
    [InlineData("Ll", "lowercase letter")]
    [InlineData("Lt", "titlecase letter")]
    [InlineData("Lm", "modifier letter")]
    [InlineData("Lo", "other letter")]
    [InlineData("M", "mark")]
    [InlineData("N", "number")]
    [InlineData("Nd", "decimal digit")]
    [InlineData("P", "punctuation")]
    [InlineData("S", "symbol")]
    [InlineData("Sc", "currency symbol")]
    [InlineData("Sm", "math symbol")]
    [InlineData("Z", "separator")]
    [InlineData("Zs", "space separator")]
    [InlineData("C", "control/other")]
    [InlineData("Cc", "control character")]
    [InlineData("IsBasicLatin", "block BasicLatin")]
    [InlineData("Mn", "category Mn")]
    public void Label_UnicodeCategory_ReturnsReadableName(string category, string expected)
    {
        RegexNode root = RegexParser.Parse($@"\p{{{category}}}").Root;

        string label = NodeDescriber.Label(root);

        label.ShouldBe(expected);
    }

    [Theory]
    [InlineData('\t', @"\t")]
    [InlineData('\n', @"\n")]
    [InlineData('\r', @"\r")]
    [InlineData(' ', "␣")]
    [InlineData('\u0001', @"\u0001")]
    [InlineData('\u007F', @"\u007F")]
    [InlineData('x', "x")]
    [InlineData('é', "é")]
    public void Printable_Character_ReturnsVisibleText(char value, string expected)
    {
        string printable = NodeDescriber.Printable(value);

        printable.ShouldBe(expected);
    }

    [Theory]
    [InlineData('a', "'a'")]
    [InlineData('\n', @"'\n'")]
    public void Quote_Character_WrapsPrintableTextInQuotes(char value, string expected)
    {
        string quoted = NodeDescriber.Quote(value);

        quoted.ShouldBe(expected);
    }

    [Fact]
    public void Trace_FailingDigit_DescribesExpectedElementInEnglish()
    {
        Pattern pattern = Pattern.FromRegex(@"^\d{2}x$");

        TraceFailure failure = pattern.Trace("1y").Failure;

        failure.Position.ShouldBe(0);
        failure.Construct.ShouldBe(@"\d{2}");
        failure.Description.ShouldBe("a digit, exactly 2 times");
    }
}
