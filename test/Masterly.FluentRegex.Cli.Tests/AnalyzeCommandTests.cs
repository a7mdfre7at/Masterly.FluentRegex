using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class AnalyzeCommandTests : CliTestBase
{
    [Fact]
    public void Analyze_HighRisk_ReportsIssueAndReturnsFindings()
    {
        CliResult result = Run("analyze", "(a+)+$");

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldStartWith("Pattern: (a+)+$");
        result.Output.ShouldContain("Risk:    High");
        result.Output.ShouldContain("[High] NestedQuantifier at position 0: (a+)+");
        result.Output.ShouldContain("Attack input (33 chars)");
        result.Output.ShouldNotContain("CONFIRMED");
    }

    [Fact]
    public void Analyze_HighRiskWithConfirm_ProvesTheRiskWithATimedAttack()
    {
        CliResult result = Run("analyze", "(a+)+$", "--confirm");

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldContain("CONFIRMED: matching did not finish within");
    }

    [Fact]
    public void Analyze_MediumRisk_ReportsIssueButReturnsOk()
    {
        CliResult result = Run("analyze", @"\d+\d+");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("Risk:    Medium");
        result.Output.ShouldContain("[Medium] AdjacentOverlappingQuantifiers");
    }

    [Fact]
    public void Analyze_LowRisk_ReturnsOk()
    {
        CliResult result = Run("analyze", @"(\w|\d)+$");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("Risk:    Low");
        result.Output.ShouldContain("[Low] OverlappingAlternation");
    }

    [Fact]
    public void Analyze_SafeRegex_ReportsNoRisks()
    {
        CliResult result = Run("analyze", "abc");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("Risk:    None");
        result.Output.ShouldContain("No catastrophic backtracking risks found.");
        result.Error.ShouldBeEmpty();
    }
}
