using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class HighlightCommandTests : CliTestBase
{
    [Fact]
    public void Highlight_BracketsFormat_MarksEachMatch()
    {
        string file = WriteFile("input.txt", "abc\nxbz\n");

        CliResult result = Run("highlight", "b", file, "--format", "brackets");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith("a[b]c\nx[b]z\n");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Highlight_RedirectedOutputWithoutFormat_DefaultsToBrackets()
    {
        string file = WriteFile("input.txt", "abc");

        CliResult result = Run("highlight", "b", file);

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBe("a[b]c" + Environment.NewLine);
    }

    [Fact]
    public void Highlight_HtmlFormat_WrapsMatchesAndNamedGroups()
    {
        string file = WriteFile("input.txt", "abc");

        CliResult result = Run("highlight", "(?<x>b)", file, "--format", "HTML");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("a<mark class=\"frx-match\"><span class=\"frx-group frx-group-x\" title=\"x\">b</span></mark>c");
    }

    [Fact]
    public void Highlight_AnsiFormat_UsesEscapeSequences()
    {
        string file = WriteFile("input.txt", "abc");

        CliResult result = Run("highlight", "b", file, "--format", "ansi");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith("a\u001b[");
        result.Output.ShouldContain("b\u001b[");
        result.Output.ShouldNotContain("[b]");
    }

    [Fact]
    public void Highlight_StandardInput_ReadsTextFromDash()
    {
        SetStandardInput("abc\r\n");

        CliResult result = Run("highlight", "b", "-", "--format", "brackets");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith("a[b]c");
    }

    [Fact]
    public void Highlight_UnknownFormat_ReturnsUsageError()
    {
        string file = WriteFile("input.txt", "abc");

        CliResult result = Run("highlight", "b", file, "--format", "bold");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBe("error: Unknown --format 'bold'. Use ansi, html or brackets." + Environment.NewLine);
    }
}
