using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

/// <summary>
/// Under RegexOptions.IgnorePatternWhitespace the engine ignores unescaped whitespace and treats '#' as the
/// start of a comment, so literal text must escape them to keep matching itself.
/// </summary>
public class FreeSpacingTests
{
    private const RegexOptions FreeSpacing = RegexOptions.IgnorePatternWhitespace;

    [Theory]
    [InlineData("a c")]
    [InlineData("order #42")]
    [InlineData("tab\tand space")]
    public void Literal_WithIgnorePatternWhitespace_StillMatchesItsOwnText(string text)
    {
        Pattern pattern = Pattern.With.StartOfInput.Literal(text).EndOfInput.WithOptions(FreeSpacing);

        bool isMatch = pattern.IsMatch(text);

        isMatch.ShouldBeTrue(pattern.ToString());
        pattern.IsMatch(text.Substring(0, text.IndexOfAny(new[] { ' ', '#', '\t' }))).ShouldBeFalse(pattern.ToString());
    }

    [Fact]
    public void Literal_WithIgnorePatternWhitespace_EscapesSpaceAndHash()
    {
        Pattern pattern = Pattern.With.Literal("order #42").WithOptions(FreeSpacing);

        string regex = pattern.ToString();

        regex.ShouldBe(@"order\ \#42");
    }

    [Fact]
    public void Literal_WithoutIgnorePatternWhitespace_IsUnchanged()
    {
        Pattern pattern = Pattern.With.Literal("order #42");

        string regex = pattern.ToString();

        regex.ShouldBe("order #42");
    }

    [Fact]
    public void NestedGroupsAndAlternatives_WithIgnorePatternWhitespace_EscapeTheirLiterals()
    {
        Pattern pattern = Pattern.With
            .StartOfInput
            .NamedGroup("name", Pattern.With.Literal("John Doe"))
            .Literal(" ")
            .OneOf("a b", "c #")
            .EndOfInput
            .WithOptions(FreeSpacing);

        pattern.IsMatch("John Doe a b").ShouldBeTrue(pattern.ToString());
        pattern.IsMatch("John Doe c #").ShouldBeTrue(pattern.ToString());
        pattern.Match("John Doe a b").Groups["name"].Value.ShouldBe("John Doe");
    }

    [Fact]
    public void InlineFreeSpacingScope_EscapesTheLiteralsInside()
    {
        Pattern pattern = Pattern.With
            .StartOfInput
            .InlineOptions(RegexOptions.IgnorePatternWhitespace, Pattern.With.Literal("a b #1"))
            .EndOfInput;

        pattern.ToString().ShouldBe(@"\A(?x:a\ b\ \#1)\z");
        pattern.IsMatch("a b #1").ShouldBeTrue();
        pattern.IsMatch("ab").ShouldBeFalse();
    }

    [Fact]
    public void CharacterSet_WithIgnorePatternWhitespace_KeepsSpaceAndHashLiteral()
    {
        // Inside [...] .NET never ignores whitespace, so the set needs no extra escaping.
        Pattern pattern = Pattern.With.Set(Pattern.With.Literal(" #")).WithOptions(FreeSpacing);

        pattern.ToString().ShouldBe("[ #]");
        pattern.IsMatch(" ").ShouldBeTrue();
        pattern.IsMatch("#").ShouldBeTrue();
    }

    [Fact]
    public void ToFluentCode_OfAnEscapedSpaceInFreeSpacingMode_RebuildsAnEquivalentPattern()
    {
        Pattern original = Pattern.FromRegex(@"^order\ \#\d+$", FreeSpacing);

        Pattern rebuilt = Pattern.With.StartOfLine.Literal("order #").Digit.Repeat.OneOrMore.EndOfLine.WithOptions(FreeSpacing);

        original.IsMatch("order #42").ShouldBeTrue();
        original.IsMatch("order").ShouldBeFalse();
        rebuilt.IsMatch("order #42").ShouldBeTrue(rebuilt.ToString());
        rebuilt.IsMatch("order").ShouldBeFalse(rebuilt.ToString());
        original.ToFluentCode().ShouldContain("Literal(\"order #\")");
    }
}
