using System.Text;
using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class RunTests : CliTestBase
{
    private const string UsageHeader = "fluentregex - companion tool for Masterly.FluentRegex";

    [Fact]
    public void Run_NoArguments_PrintsUsageAndReturnsUsageError()
    {
        CliResult result = Run();

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldStartWith(UsageHeader);
        result.Output.ShouldContain("1  a finding: analyze (High risk), scan (findings at or above --fail-on), compare (not");
        result.Error.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void Run_HelpSwitch_PrintsUsageAndReturnsOk(string helpSwitch)
    {
        CliResult result = Run(helpSwitch);

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith(UsageHeader);
        result.Output.ShouldContain("fluentregex export   <regex> [--to js|json-schema|typescript|postgresql|mysql|mariadb|sqlserver|sqlite]");
        result.Output.ShouldContain("fluentregex facts    <regex>");
        result.Output.ShouldContain("--ascii-digits");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Run_HelpWithExtraArguments_IgnoresTheRestAndPrintsUsage()
    {
        CliResult result = Run("help", "convert", "--bogus");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith(UsageHeader);
    }

    [Fact]
    public void Run_Version_PrintsLibraryVersionWithThreeParts()
    {
        string expected = typeof(Pattern).Assembly.GetName().Version!.ToString(3); // The library assembly always carries a version.

        CliResult result = Run("--version");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBe(expected + Environment.NewLine);
        result.Output.ShouldMatch(@"^\d+\.\d+\.\d+\r?\n$");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Run_UnknownCommand_ReportsErrorAndReturnsUsageError()
    {
        CliResult result = Run("bogus", "a");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBe("error: Unknown command 'bogus'. Run 'fluentregex --help'." + Environment.NewLine);
    }

    [Fact]
    public void Run_CommandNamesAreCaseSensitive_UppercaseCommandIsUnknown()
    {
        CliResult result = Run("CONVERT", "a");

        result.ExitCode.ShouldBe(UsageError);
        result.Error.ShouldContain("Unknown command 'CONVERT'");
    }

    [Fact]
    public void Run_Always_SwitchesConsoleOutputToUtf8()
    {
        Console.OutputEncoding = Encoding.ASCII;

        Run("--version");

        Console.OutputEncoding.CodePage.ShouldBe(Encoding.UTF8.CodePage);
    }

    [Fact]
    public void Run_ArgumentException_IsReportedAsUsageError()
    {
        CliResult result = Run("convert", "(");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldStartWith("error: ");
        result.Error.ShouldContain("Not enough )'s");
    }

    [Fact]
    public void Run_FileNotFoundException_IsReportedAsUsageError()
    {
        string missing = Path.Combine(TempDirectory, "missing.txt");

        CliResult result = Run("highlight", "a", missing);

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldStartWith("error: ");
        result.Error.ShouldContain(missing);
    }

    [Fact]
    public void Run_InvalidOperationException_IsReportedAsUsageError()
    {
        CliResult result = Run("compare", "a{1001}", "a");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldContain("exceeds the language algebra limit of 1000 repetitions");
    }

    [Fact]
    public void Run_FormatException_IsReportedAsUsageError()
    {
        CliResult result = Run("generate", "a", "--count", "many");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldStartWith("error: ");
        result.Error.ShouldContain("'many'");
    }

    [Fact]
    public void Run_NotSupportedException_IsReportedAsUsageError()
    {
        CliResult result = Run("export", @"\G");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldContain("cannot be converted to JavaScript without changing its meaning");
        result.Error.ShouldContain(@"'\G' (contiguous-match anchor)");
    }

    [Fact]
    public void Run_OptionWithoutValue_ReportsMissingValue()
    {
        CliResult result = Run("convert", "a", "--options");

        result.ExitCode.ShouldBe(UsageError);
        result.Error.ShouldBe("error: Option '--options' needs a value." + Environment.NewLine);
    }

    [Theory]
    [InlineData("convert", "regex")]
    [InlineData("analyze", "regex")]
    [InlineData("scan", "path")]
    [InlineData("explain", "regex")]
    [InlineData("generate", "regex")]
    [InlineData("trace", "regex")]
    [InlineData("diagram", "regex")]
    [InlineData("compare", "regexA")]
    [InlineData("optimize", "regex")]
    [InlineData("profile", "regex")]
    [InlineData("highlight", "regex")]
    [InlineData("coverage", "regex")]
    [InlineData("export", "regex")]
    public void Run_CommandWithoutPositionalArguments_ReportsTheMissingArgument(string command, string argumentName)
    {
        CliResult result = Run(command);

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBe($"error: Missing <{argumentName}> argument. Run 'fluentregex --help'.{Environment.NewLine}");
    }

    [Theory]
    [InlineData("trace", "input")]
    [InlineData("compare", "regexB")]
    [InlineData("profile", "input")]
    [InlineData("highlight", "file")]
    [InlineData("coverage", "file")]
    public void Run_CommandWithoutSecondPositionalArgument_ReportsTheMissingArgument(string command, string argumentName)
    {
        CliResult result = Run(command, "a");

        result.ExitCode.ShouldBe(UsageError);
        result.Error.ShouldBe($"error: Missing <{argumentName}> argument. Run 'fluentregex --help'.{Environment.NewLine}");
    }

    [Fact]
    public void Run_UnknownRegexOption_ReportsTheOptionName()
    {
        CliResult result = Run("explain", "a", "--options", "IgnoreCase,Bogus");

        result.ExitCode.ShouldBe(UsageError);
        result.Error.ShouldBe("error: Unknown regex option 'Bogus'." + Environment.NewLine);
    }

    [Fact]
    public void Run_RegexOptionsList_IsCaseInsensitiveAndIgnoresBlankEntries()
    {
        CliResult result = Run("convert", "a", "--options", " ignorecase , ,MULTILINE ");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBe("Pattern.With.Literal(\"a\").WithOptions(RegexOptions.IgnoreCase | RegexOptions.Multiline)" + Environment.NewLine);
    }

    [Fact]
    public void Run_OptionNames_AreCaseInsensitive()
    {
        CliResult result = Run("convert", "a", "--OPTIONS", "IgnoreCase");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("WithOptions(RegexOptions.IgnoreCase)");
    }

    [Fact]
    public void Run_AsciiDigitsFlag_RestrictsDigitsToZeroThroughNine()
    {
        CliResult result = Run("convert", @"\d", "--ascii-digits");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBe("Pattern.With.Range('0', '9')" + Environment.NewLine);
    }

    [Fact]
    public void Run_WithoutAsciiDigitsFlag_KeepsUnicodeDigits()
    {
        CliResult result = Run("convert", @"\d");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBe("Pattern.With.Digit" + Environment.NewLine);
    }
}
