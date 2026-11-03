using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class CompareCommandTests : CliTestBase
{
    [Fact]
    public void Compare_EquivalentPatterns_ReturnsOk()
    {
        CliResult result = Run("compare", "a|b", "[ab]");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBe("Equivalent: both patterns match exactly the same strings." + Environment.NewLine);
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_DisjointPatterns_ShowsCounterexamplesAndReturnsFindings()
    {
        CliResult result = Run("compare", "a", "b");

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldContain("Not equivalent (no string matches both).");
        result.Output.ShouldContain("Shortest counterexample: \"a\"");
    }

    [Fact]
    public void Compare_SubsetPatterns_ShowsTheMissingStringAndReturnsFindings()
    {
        CliResult result = Run("compare", "[ab]", "[abc]");

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldContain("left is a proper subset of right");
        result.Output.ShouldContain("\"c\" (right accepts it, left rejects it)");
    }

    [Fact]
    public void Compare_OptionsApplyToBothPatterns()
    {
        CliResult result = Run("compare", "a", "A", "--options", "IgnoreCase");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith("Equivalent");
    }

    [Fact]
    public void Compare_InvalidSecondPattern_ReturnsUsageError()
    {
        CliResult result = Run("compare", "a", "(");

        result.ExitCode.ShouldBe(UsageError);
        result.Error.ShouldContain("Not enough )'s");
    }

    [Fact]
    public void Compare_Backreference_IsRejectedAsUnsupported()
    {
        CliResult result = Run("compare", @"(a)\1", "aa");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldContain("Language algebra does not support backreferences");
    }

    [Fact]
    public void Compare_RightToLeft_IsRejectedAsUnsupported()
    {
        CliResult result = Run("compare", "a", "a", "--options", "RightToLeft");

        result.ExitCode.ShouldBe(UsageError);
        result.Error.ShouldBe("error: Language algebra does not support RegexOptions.RightToLeft." + Environment.NewLine);
    }
}
