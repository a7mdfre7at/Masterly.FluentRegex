using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class DiagramCommandTests : CliTestBase
{
    [Fact]
    public void Diagram_DefaultFormat_PrintsSvg()
    {
        CliResult result = Run("diagram", "a");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith("<svg xmlns=\"http://www.w3.org/2000/svg\"");
        result.Output.ShouldContain("<title>a</title>");
        result.Error.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("mermaid")]
    [InlineData("MERMAID")]
    public void Diagram_MermaidFormat_PrintsFlowchart(string format)
    {
        CliResult result = Run("diagram", "a|b", "--format", format);

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith("flowchart LR");
        result.Output.ShouldContain("start([start])");
        result.Output.ShouldContain("finish([match])");
    }

    [Fact]
    public void Diagram_SvgFormatExplicitly_PrintsSvg()
    {
        CliResult result = Run("diagram", "a", "--format", "SVG");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith("<svg");
    }

    [Fact]
    public void Diagram_UnknownFormat_ReturnsUsageError()
    {
        CliResult result = Run("diagram", "a", "--format", "png");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBe("error: Unknown --format 'png'. Use svg or mermaid." + Environment.NewLine);
    }

    [Fact]
    public void Diagram_OutputFile_WritesTheDiagramAndReportsThePath()
    {
        string file = Path.Combine(TempDirectory, "diagram.mmd");

        CliResult result = Run("diagram", "a", "--format", "mermaid", "--output", file);

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBe("Wrote " + file + Environment.NewLine);
        File.ReadAllText(file).ShouldStartWith("flowchart LR");
    }
}
