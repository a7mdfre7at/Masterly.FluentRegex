using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class ScanCommandTests : CliTestBase
{
    private const string HighRiskSource = """
        class High
        {
            Regex risky = new Regex("(a+)+$");
        }
        """;

    private const string MediumRiskSource = """
        class Medium
        {
            Regex slow = new Regex(@"\d+\d+");
        }
        """;

    private const string SafeSource = """
        class Safe
        {
            bool ok = Regex.IsMatch(input, "^abc$");
        }
        """;

    private const string InvalidSource = """
        class Invalid
        {
            Regex broken = new Regex("(");
        }
        """;

    [Fact]
    public void Scan_HighRiskLiteral_ReportsWarningAndReturnsFindings()
    {
        string path = WriteFile("High.cs", HighRiskSource);

        CliResult result = Run("scan", TempDirectory);

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldContain($"{path}(3,29): warning FRX-NestedQuantifier: new Regex \"(a+)+$\"{Environment.NewLine}");
        result.Output.ShouldContain("    The repetition 'a+' is nested inside the repetition '+'.");
        result.Output.ShouldContain("    Fix: Make the inner repetition possessive");
        result.Output.ShouldEndWith($"{Environment.NewLine}Scanned 1 regex literal(s): 1 high, 0 medium risk finding(s).{Environment.NewLine}");
    }

    [Fact]
    public void Scan_HighRiskWithConfirm_MarksTheFindingConfirmed()
    {
        WriteFile("High.cs", HighRiskSource);

        CliResult result = Run("scan", TempDirectory, "--confirm");

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldContain("new Regex \"(a+)+$\" [CONFIRMED by timeout]");
    }

    [Fact]
    public void Scan_MediumRiskWithDefaultFailOn_ReportsInfoAndReturnsOk()
    {
        string path = WriteFile("Medium.cs", MediumRiskSource);

        CliResult result = Run("scan", path);

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain($"{path}(3,28): info FRX-AdjacentOverlappingQuantifiers: new Regex \"\\d+\\d+\"");
        result.Output.ShouldContain("Scanned 1 regex literal(s): 0 high, 1 medium risk finding(s).");
    }

    [Theory]
    [InlineData("medium")]
    [InlineData("MEDIUM")]
    public void Scan_MediumRiskWithFailOnMedium_ReturnsFindings(string failOn)
    {
        WriteFile("Medium.cs", MediumRiskSource);

        CliResult result = Run("scan", TempDirectory, "--fail-on", failOn);

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldContain("0 high, 1 medium risk finding(s).");
    }

    [Fact]
    public void Scan_HighRiskWithFailOnMedium_ReturnsFindings()
    {
        WriteFile("High.cs", HighRiskSource);

        CliResult result = Run("scan", TempDirectory, "--fail-on", "medium");

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldContain("1 high, 0 medium risk finding(s).");
    }

    [Fact]
    public void Scan_HighRiskWithFailOnHigh_ReturnsFindings()
    {
        WriteFile("High.cs", HighRiskSource);

        CliResult result = Run("scan", TempDirectory, "--fail-on", "high");

        result.ExitCode.ShouldBe(Findings);
    }

    [Fact]
    public void Scan_HighRiskWithFailOnNone_StillReportsButReturnsOk()
    {
        WriteFile("High.cs", HighRiskSource);
        WriteFile("Medium.cs", MediumRiskSource);

        CliResult result = Run("scan", TempDirectory, "--fail-on", "none");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("warning FRX-NestedQuantifier");
        result.Output.ShouldContain("info FRX-AdjacentOverlappingQuantifiers");
        result.Output.ShouldContain("Scanned 2 regex literal(s): 1 high, 1 medium risk finding(s).");
    }

    [Fact]
    public void Scan_UnknownFailOnLevel_ReturnsUsageError()
    {
        WriteFile("High.cs", HighRiskSource);

        CliResult result = Run("scan", TempDirectory, "--fail-on", "low");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBe("error: Unknown --fail-on value 'low'. Use high, medium or none." + Environment.NewLine);
    }

    [Fact]
    public void Scan_SafeLiterals_ReportsNothingAndReturnsOk()
    {
        WriteFile("Safe.cs", SafeSource);

        CliResult result = Run("scan", TempDirectory);

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBe($"{Environment.NewLine}Scanned 1 regex literal(s): 0 high, 0 medium risk finding(s).{Environment.NewLine}");
    }

    [Fact]
    public void Scan_LowRiskLiteral_IsNotReported()
    {
        WriteFile("Low.cs", "class Low { Regex r = new Regex(@\"(\\w|\\d)+$\"); }");

        CliResult result = Run("scan", TempDirectory);

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldNotContain("OverlappingAlternation");
        result.Output.ShouldContain("Scanned 1 regex literal(s): 0 high, 0 medium risk finding(s).");
    }

    [Fact]
    public void Scan_InvalidLiteral_ReportsErrorAndCountsItAsHigh()
    {
        string path = WriteFile("Invalid.cs", InvalidSource);

        CliResult result = Run("scan", TempDirectory);

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldContain($"{path}(3,30): error: invalid regex: The regular expression is invalid");
        result.Output.ShouldContain("Scanned 1 regex literal(s): 1 high, 0 medium risk finding(s).");
    }

    [Fact]
    public void Scan_InvalidLiteralWithFailOnNone_ReturnsOk()
    {
        WriteFile("Invalid.cs", InvalidSource);

        CliResult result = Run("scan", TempDirectory, "--fail-on", "none");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("error: invalid regex");
    }

    [Fact]
    public void Scan_EmptyFolder_ScansNothing()
    {
        CliResult result = Run("scan", TempDirectory);

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("Scanned 0 regex literal(s): 0 high, 0 medium risk finding(s).");
    }

    [Fact]
    public void Scan_FileInsideCurrentDirectory_PrintsRelativePath()
    {
        WriteFile(Path.Combine("src", "High.cs"), HighRiskSource);
        UseTempDirectoryAsCurrentDirectory();

        CliResult result = Run("scan", "src");

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldStartWith($"{Path.Combine("src", "High.cs")}(3,29): warning FRX-NestedQuantifier");
    }

    [Fact]
    public void Scan_FileOutsideCurrentDirectory_PrintsFullPath()
    {
        string path = WriteFile(Path.Combine("src", "High.cs"), HighRiskSource);
        string workingDirectory = Path.Combine(TempDirectory, "work");
        Directory.CreateDirectory(workingDirectory);
        Environment.CurrentDirectory = workingDirectory;

        CliResult result = Run("scan", path);

        result.ExitCode.ShouldBe(Findings);
        result.Output.ShouldStartWith($"{path}(3,29): warning FRX-NestedQuantifier");
    }

    [Fact]
    public void Scan_BuildOutputFolders_AreSkipped()
    {
        WriteFile(Path.Combine("bin", "High.cs"), HighRiskSource);
        WriteFile(Path.Combine("obj", "High.cs"), HighRiskSource);
        WriteFile(Path.Combine(".git", "High.cs"), HighRiskSource);
        WriteFile(Path.Combine("node_modules", "High.cs"), HighRiskSource);

        CliResult result = Run("scan", TempDirectory);

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldContain("Scanned 0 regex literal(s)");
    }

    [Fact]
    public void Scan_MissingPath_ReturnsUsageError()
    {
        string missing = Path.Combine(TempDirectory, "missing");

        CliResult result = Run("scan", missing);

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBe($"error: No such file or directory: {missing}{Environment.NewLine}");
    }
}
