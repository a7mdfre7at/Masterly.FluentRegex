using System.Text.RegularExpressions;
using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class LearnCommandTests : CliTestBase
{
    private const string LearnedDatePattern = @"^\d{4}-\d{2}-\d{2}$";

    [Fact]
    public void Learn_Examples_PrintsRegexBlankLineAndFluentCode()
    {
        CliResult result = Run("learn", "2024-01-05", "2023-12-31");

        result.ExitCode.ShouldBe(Ok);
        result.OutputLines[0].ShouldBe(LearnedDatePattern);
        result.OutputLines[1].ShouldBeEmpty();
        result.OutputLines[2].ShouldStartWith("Pattern.With\n    .StartOfLine\n    .Digit.Repeat.Times(4)");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Learn_ExamplesAndRejects_LearnsAPatternThatSeparatesThem()
    {
        CliResult result = Run("learn", "cat", "car", "--not", "dog", "cow");

        result.ExitCode.ShouldBe(Ok);
        Regex learned = new Regex(result.OutputLines[0]);
        learned.IsMatch("cat").ShouldBeTrue();
        learned.IsMatch("car").ShouldBeTrue();
        learned.IsMatch("dog").ShouldBeFalse();
        learned.IsMatch("cow").ShouldBeFalse();
    }

    [Fact]
    public void Learn_FromFile_ReadsOneExamplePerLineSkippingBlankLines()
    {
        string file = WriteFile("examples.txt", "2024-01-05\n\n2023-12-31\n");

        CliResult result = Run("learn", "--from-file", file);

        result.ExitCode.ShouldBe(Ok);
        result.OutputLines[0].ShouldBe(LearnedDatePattern);
    }

    [Fact]
    public void Learn_RejectsFromFile_AddsTheFileLinesToTheRejects()
    {
        string rejects = WriteFile("rejects.txt", "dog\ncow\n");

        CliResult result = Run("learn", "cat", "car", "--not", "--from-file", rejects);

        result.ExitCode.ShouldBe(Ok);
        Regex learned = new Regex(result.OutputLines[0]);
        learned.IsMatch("cat").ShouldBeTrue();
        learned.IsMatch("dog").ShouldBeFalse();
        learned.IsMatch("cow").ShouldBeFalse();
    }

    [Fact]
    public void Learn_SameExampleAsMatchAndReject_ReturnsUsageError()
    {
        CliResult result = Run("learn", "abc", "--not", "abc");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldContain("The example \"abc\" is listed both as a match and as a reject");
    }

    [Fact]
    public void Learn_NoExamples_ReturnsUsageError()
    {
        CliResult result = Run("learn");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBe("error: Give at least one example to learn from. Run 'fluentregex --help'." + Environment.NewLine);
    }

    [Fact]
    public void Learn_OnlyRejects_ReturnsUsageError()
    {
        CliResult result = Run("learn", "--not", "abc");

        result.ExitCode.ShouldBe(UsageError);
        result.Error.ShouldBe("error: Give at least one example to learn from. Run 'fluentregex --help'." + Environment.NewLine);
    }

    [Fact]
    public void Learn_EmptyExampleFile_ReturnsUsageError()
    {
        string file = WriteFile("empty.txt", "\n\n");

        CliResult result = Run("learn", "--from-file", file);

        result.ExitCode.ShouldBe(UsageError);
        result.Error.ShouldContain("Give at least one example to learn from.");
    }

    [Fact]
    public void Learn_MissingExampleFile_ReturnsUsageError()
    {
        string missing = Path.Combine(TempDirectory, "missing.txt");

        CliResult result = Run("learn", "--from-file", missing);

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldStartWith("error: ");
        result.Error.ShouldContain(missing);
    }
}
