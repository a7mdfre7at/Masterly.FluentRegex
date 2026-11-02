using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class OptimizeCommandTests : CliTestBase
{
    [Fact]
    public void Optimize_ReducibleRegex_PrintsTheProvenRewrite()
    {
        CliResult result = Run("optimize", "a|b|c");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("Optimized 'a|b|c' to '[abc]' (proven equivalent):");
        result.Output.ShouldContain("  - Merged alternation 'a|b|c' into '[abc]'");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Optimize_IrreducibleRegex_ReportsNoRewrites()
    {
        CliResult result = Run("optimize", @"(a)\1");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBe(@"No rewrites applied to '(a)\1'." + Environment.NewLine);
    }
}
