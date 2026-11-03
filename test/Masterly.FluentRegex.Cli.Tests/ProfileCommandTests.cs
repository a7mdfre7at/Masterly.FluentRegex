using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class ProfileCommandTests : CliTestBase
{
    [Fact]
    public void Profile_CheapMatch_ReportsStepsAndReturnsOk()
    {
        CliResult result = Run("profile", "abc", "abc");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith("Pattern  abc");
        result.Output.ShouldContain("Result   match at 0, length 3: 3 steps, 0 backtracks");
        result.Output.ShouldContain("Engine   agrees with the .NET engine");
        result.Output.ShouldContain("Verdict  cheap");
    }

    [Fact]
    public void Profile_CatastrophicBacktracking_ExhaustsBudgetAndReturnsFindings()
    {
        CliResult result = Run("profile", "(a+)+$", new string('a', 30) + "b");

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldContain("gave up: the step budget of 1,000,000 ran out");
        result.Output.ShouldContain("Verdict  catastrophic backtracking, mostly in 'a+'.");
    }

    [Fact]
    public void Profile_RightToLeft_IsRejectedAsUnsupported()
    {
        CliResult result = Run("profile", "a", "a", "--options", "RightToLeft");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBe("error: The backtracking profiler does not emulate RegexOptions.RightToLeft." + Environment.NewLine);
    }
}
