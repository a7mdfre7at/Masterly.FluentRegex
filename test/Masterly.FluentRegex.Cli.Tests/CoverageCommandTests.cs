using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class CoverageCommandTests : CliTestBase
{
    [Fact]
    public void Coverage_EveryBranchExercised_ReturnsOk()
    {
        string file = WriteFile("inputs.txt", "a\nb\n");

        CliResult result = Run("coverage", "a|b", file);

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("Coverage for a|b: 100% (2/2), 2 inputs");
        result.Output.ShouldContain("✓ branch \"a\" (1 hit)");
        result.Output.ShouldContain("✓ branch \"b\" (1 hit)");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Coverage_UnusedBranch_ReportsItAndReturnsFindings()
    {
        string file = WriteFile("inputs.txt", "a\nb\n");

        CliResult result = Run("coverage", "a|b|c", file);

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldContain("Coverage for a|b|c: 67% (2/3), 2 inputs");
        result.Output.ShouldContain("✗ branch \"c\" was never matched");
    }

    [Fact]
    public void Coverage_UpperBoundNeverReached_ReturnsFindings()
    {
        string file = WriteFile("inputs.txt", "a\n");

        CliResult result = Run("coverage", "a{1,3}", file);

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldContain("✗ upper bound 3 of \"a{1,3}\" was never reached");
    }

    [Fact]
    public void Coverage_StandardInputWithWindowsLineEndings_TreatsEachLineAsOneInput()
    {
        SetStandardInput("a\r\nb\r\n");

        CliResult result = Run("coverage", "^(a|b)$", "-");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("100% (2/2), 2 inputs");
    }

    [Fact]
    public void Coverage_EmptyFile_CoversNothingAndReturnsFindings()
    {
        string file = WriteFile("empty.txt", string.Empty);

        CliResult result = Run("coverage", "a|b", file);

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldContain("Coverage for a|b: 0% (0/2)");
        result.Output.ShouldContain("✗ branch \"a\" was never matched");
    }

    [Fact]
    public void Coverage_MissingFile_ReturnsUsageError()
    {
        string missing = Path.Combine(TempDirectory, "missing.txt");

        CliResult result = Run("coverage", "a", missing);

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldStartWith("error: ");
        result.Error.ShouldContain(missing);
    }
}
