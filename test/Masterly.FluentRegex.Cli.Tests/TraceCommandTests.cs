using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class TraceCommandTests : CliTestBase
{
    [Fact]
    public void Trace_MatchingInput_ShowsWhichPartMatched()
    {
        CliResult result = Run("trace", @"\d+", "ab12");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("Input:   \"ab12\"");
        result.Output.ShouldContain("Result:  MATCH at 2, length 2: \"12\"");
        result.Output.ShouldContain("a digit, one or more times");
    }

    [Fact]
    public void Trace_NonMatchingInput_ShowsWhereItFailedAndStillReturnsOk()
    {
        CliResult result = Run("trace", @"\d+", "abc");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("Result:  NO MATCH");
        result.Output.ShouldContain(@"Failed at position 0: expected \d+");
    }

    [Fact]
    public void Trace_IgnoreCaseOption_AppliesToTheMatch()
    {
        CliResult result = Run("trace", "abc", "xABC", "--options", "IgnoreCase");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("Result:  MATCH at 1, length 3: \"ABC\"");
    }
}
