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
        pattern.ToString().ShouldBe("a-zA-Z");
    }

    [Fact]
    public void LowercaseLetter()
    {
        Pattern pattern = Pattern.With.LowercaseLetter;
        pattern.ToString().ShouldBe("a-z");
    }

    [Fact]
    public void UppercaseLetter()
    {
        Pattern pattern = Pattern.With.UppercaseLetter;
        pattern.ToString().ShouldBe("A-Z");
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
}
