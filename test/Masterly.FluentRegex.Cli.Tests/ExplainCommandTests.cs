using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class ExplainCommandTests : CliTestBase
{
    [Fact]
    public void Explain_Regex_PrintsPlainEnglishSteps()
    {
        CliResult result = Run("explain", @"\d+");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith(@"Pattern: \d+");
        result.Output.ShouldContain("Explanation:");
        result.Output.ShouldContain("  - Digit [0-9]");
        result.Output.ShouldContain("  - One or more");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Explain_InvalidRegex_ReturnsUsageError()
    {
        CliResult result = Run("explain", "a{2,1}");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldStartWith("error: The regular expression is invalid");
        result.Error.ShouldContain("Illegal {x,y} with x > y.");
    }
}
