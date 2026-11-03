using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class ConvertCommandTests : CliTestBase
{
    private const string Source = """
        using System.Text.RegularExpressions;
        class Sample
        {
            Regex digits = new Regex(@"\d{3}", RegexOptions.IgnoreCase);
            Regex broken = new Regex("(");
            Regex dynamic = new Regex(pattern);
        }
        """;

    [Fact]
    public void Convert_Regex_PrintsFluentCode()
    {
        CliResult result = Run("convert", @"\d{3}-[a-z]+");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBe(
            "Pattern.With\n    .Digit.Repeat.Times(3)\n    .Literal(\"-\")\n    .LowercaseLetter.Repeat.OneOrMore" + Environment.NewLine);
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Convert_WithOptions_AppendsWithOptions()
    {
        CliResult result = Run("convert", "a", "--options", "IgnoreCase,Multiline");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBe("Pattern.With.Literal(\"a\").WithOptions(RegexOptions.IgnoreCase | RegexOptions.Multiline)" + Environment.NewLine);
    }

    [Fact]
    public void Convert_InvalidRegex_ReturnsUsageError()
    {
        CliResult result = Run("convert", "[a-");

        result.ExitCode.ShouldBe(UsageError);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldStartWith("error: The regular expression is invalid");
    }

    [Fact]
    public void Convert_File_ConvertsEachLiteralAndSkipsInvalidOnes()
    {
        string path = WriteFile("Sample.cs", Source);

        CliResult result = Run("convert", "--file", path);

        result.ExitCode.ShouldBe(Ok);
        result.OutputLines.Length.ShouldBe(5);
        result.OutputLines[0].ShouldBe($"// {path}(4,30) new Regex: \\d{{3}}");
        result.OutputLines[1].ShouldBe("Pattern.With.Digit.Repeat.Times(3).WithOptions(RegexOptions.IgnoreCase);");
        result.OutputLines[2].ShouldBeEmpty();
        result.OutputLines[3].ShouldBe($"// {path}(5,30) new Regex: (");
        result.OutputLines[4].ShouldStartWith("// skipped: The regular expression is invalid");
        result.Error.ShouldBe("1 regex literal(s) converted. 1 skipped as invalid." + Environment.NewLine);
    }

    [Fact]
    public void Convert_FileInsideCurrentDirectory_PrintsRelativePath()
    {
        WriteFile(Path.Combine("src", "Sample.cs"), Source);
        UseTempDirectoryAsCurrentDirectory();

        CliResult result = Run("convert", "--file", "src");

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldStartWith($"// {Path.Combine("src", "Sample.cs")}(4,30) new Regex: ");
    }

    [Fact]
    public void Convert_FolderWithoutLiterals_ReportsZeroConverted()
    {
        WriteFile("Empty.cs", "class Empty { }");

        CliResult result = Run("convert", "--file", TempDirectory);

        result.ExitCode.ShouldBe(Ok);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldBe("0 regex literal(s) converted." + Environment.NewLine);
    }

    [Fact]
    public void Convert_MissingFile_ReturnsUsageError()
    {
        string missing = Path.Combine(TempDirectory, "nope");

        CliResult result = Run("convert", "--file", missing);

        result.ExitCode.ShouldBe(UsageError);
        result.Error.ShouldBe($"error: No such file or directory: {missing}{Environment.NewLine}");
    }
}
