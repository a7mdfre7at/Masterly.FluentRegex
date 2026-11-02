using System.Text.RegularExpressions;
using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class GenerateCommandTests : CliTestBase
{
    [Fact]
    public void Generate_CountAndSeed_PrintsThatManyMatchingSamples()
    {
        CliResult result = Run("generate", "[ab]{3}", "--count", "3", "--seed", "1");

        result.ExitCode.ShouldBe(Ok);
        result.OutputLines.Length.ShouldBe(3);
        result.OutputLines.ShouldAllBe(line => Regex.IsMatch(line, "^[ab]{3}$"));
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Generate_Seed_PrintsTheSamplesTheLibraryGeneratesForThatSeed()
    {
        string[] expected = Pattern.FromRegex("[a-z]{6}").GenerateMatching(4, 42);

        CliResult result = Run("generate", "[a-z]{6}", "--count", "4", "--seed", "42");

        result.ExitCode.ShouldBe(Ok);
        result.OutputLines.ShouldBe(expected);
    }

    [Fact]
    public void Generate_DifferentSeeds_PrintDifferentSamples()
    {
        CliResult first = Run("generate", "[a-z]{12}", "--count", "4", "--seed", "1");
        CliResult second = Run("generate", "[a-z]{12}", "--count", "4", "--seed", "2");

        second.Output.ShouldNotBe(first.Output);
    }

    [Fact]
    public void Generate_ZeroCount_PrintsNothing()
    {
        CliResult result = Run("generate", "a", "--count", "0");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Generate_WithoutCount_PrintsTenSamples()
    {
        CliResult result = Run("generate", "[a-z]{12}", "--seed", "7");

        result.ExitCode.ShouldBe(Ok);
        result.OutputLines.Length.ShouldBe(10);
        result.OutputLines.ShouldAllBe(line => Regex.IsMatch(line, "^[a-z]{12}$"));
    }

    [Fact]
    public void Generate_WithoutSeed_StillPrintsMatchingSamples()
    {
        CliResult result = Run("generate", "x[0-9]", "--count", "2");

        result.ExitCode.ShouldBe(Ok);
        result.OutputLines.Length.ShouldBe(2);
        result.OutputLines.ShouldAllBe(line => Regex.IsMatch(line, "^x[0-9]$"));
    }

    [Fact]
    public void Generate_NonMatching_PrintsNearMissesThatFailThePattern()
    {
        CliResult result = Run("generate", "[ab]{3}", "--count", "3", "--seed", "1", "--non-matching");

        result.ExitCode.ShouldBe(Ok);
        result.OutputLines.ShouldBe(Pattern.FromRegex("[ab]{3}").GenerateNonMatching(3, 1));
        result.OutputLines.ShouldAllBe(line => !Regex.IsMatch(line, "^[ab]{3}$"));
    }

    [Fact]
    public void Generate_AsciiDigits_OnlyProducesZeroThroughNine()
    {
        CliResult result = Run("generate", @"\d{4}", "--count", "5", "--seed", "3", "--ascii-digits");

        result.ExitCode.ShouldBe(Ok);
        result.OutputLines.Length.ShouldBe(5);
        result.OutputLines.ShouldAllBe(line => Regex.IsMatch(line, "^[0-9]{4}$"));
    }

    [Fact]
    public void Generate_NegativeCount_ReturnsUsageError()
    {
        CliResult result = Run("generate", "a", "--count", "-1");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBe("error: Specified argument was out of the range of valid values. (Parameter 'count')" + Environment.NewLine);
    }

    [Fact]
    public void Generate_NonNumericSeed_ReturnsUsageError()
    {
        CliResult result = Run("generate", "a", "--seed", "abc");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldContain("'abc'");
    }
}
