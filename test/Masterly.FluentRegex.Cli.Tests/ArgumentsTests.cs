namespace Masterly.FluentRegex.Cli.Tests;

public sealed class ArgumentsTests
{
    [Fact]
    public void Positional_ValuesInterleavedWithOptions_KeepsTheirOrder()
    {
        Arguments arguments = new Arguments(new[] { "first", "--format", "svg", "second" });

        arguments.Positional(0, "a").ShouldBe("first");
        arguments.Positional(1, "b").ShouldBe("second");
    }

    [Fact]
    public void Positional_IndexBeyondValues_ThrowsWithArgumentName()
    {
        Arguments arguments = new Arguments(new[] { "only" });

        ArgumentException exception = Should.Throw<ArgumentException>(() => arguments.Positional(1, "input"));

        exception.Message.ShouldBe("Missing <input> argument. Run 'fluentregex --help'.");
    }

    [Fact]
    public void Option_GivenValue_ReturnsIt()
    {
        Arguments arguments = new Arguments(new[] { "--count", "5" });

        arguments.Option("count").ShouldBe("5");
    }

    [Fact]
    public void Option_NameDiffersInCase_StillFound()
    {
        Arguments arguments = new Arguments(new[] { "--Count", "5" });

        arguments.Option("COUNT").ShouldBe("5");
    }

    [Fact]
    public void Option_Absent_ReturnsNull()
    {
        Arguments arguments = new Arguments(new[] { "value" });

        arguments.Option("count").ShouldBeNull();
    }

    [Fact]
    public void Option_ValueStartingWithDashes_IsConsumedAsTheValue()
    {
        Arguments arguments = new Arguments(new[] { "--name", "--other" });

        arguments.Option("name").ShouldBe("--other");
        arguments.Flag("other").ShouldBeFalse();
    }

    [Fact]
    public void Option_RepeatedOption_LastValueWins()
    {
        Arguments arguments = new Arguments(new[] { "--to", "js", "--to", "typescript" });

        arguments.Option("to").ShouldBe("typescript");
    }

    [Fact]
    public void Constructor_OptionAsLastArgumentWithoutValue_Throws()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => new Arguments(new[] { "a", "--seed" }));

        exception.Message.ShouldBe("Option '--seed' needs a value.");
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("non-matching")]
    [InlineData("ascii-digits")]
    public void Flag_KnownSwitch_TakesNoValue(string flag)
    {
        Arguments arguments = new Arguments(new[] { "--" + flag, "regex" });

        arguments.Flag(flag).ShouldBeTrue();
        arguments.Option(flag).ShouldBeNull();
        arguments.Positional(0, "regex").ShouldBe("regex");
    }

    [Fact]
    public void Flag_SwitchAsLastArgument_DoesNotNeedAValue()
    {
        Arguments arguments = new Arguments(new[] { "regex", "--confirm" });

        arguments.Flag("confirm").ShouldBeTrue();
    }

    [Fact]
    public void Flag_Absent_ReturnsFalse()
    {
        Arguments arguments = new Arguments(new[] { "regex" });

        arguments.Flag("confirm").ShouldBeFalse();
    }

    [Theory]
    [InlineData("--")]
    [InlineData("-")]
    [InlineData("-x")]
    public void Positional_ShortDashValues_AreNotOptions(string value)
    {
        Arguments arguments = new Arguments(new[] { value });

        arguments.Positional(0, "value").ShouldBe(value);
    }
}
