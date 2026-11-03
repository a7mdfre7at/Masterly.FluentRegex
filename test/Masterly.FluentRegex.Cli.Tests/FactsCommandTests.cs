using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class FactsCommandTests : CliTestBase
{
    [Fact]
    public void Facts_AnchoredPattern_PrintsTheProvenFullStringFacts()
    {
        CliResult result = Run("facts", @"^INV-[0-9]{4}-[0-9]{4}\z");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBe("FullString: length 13..13, prefix \"INV-\", suffix \"\", accepts empty: false (exact)" + Environment.NewLine);
    }

    [Fact]
    public void Facts_SubstringScope_AccountsForTheFinalNewlineThatDollarAccepts()
    {
        CliResult result = Run("facts", "^ABC-[0-9]{4}$", "--scope", "substring");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith("Substring: length 8..9, prefix \"ABC-\"");
    }

    [Fact]
    public void Facts_NonRegularPattern_ReportsApproximateFacts()
    {
        CliResult result = Run("facts", @"^(a)\1$");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("(approximate)");
    }

    [Fact]
    public void Facts_UnknownScope_ReturnsUsageError()
    {
        CliResult result = Run("facts", "a", "--scope", "everywhere");

        result.ExitCode.ShouldBe(UsageError);
        result.Error.ShouldContain("Unknown --scope 'everywhere'");
    }
}
