using System.Text.RegularExpressions;
using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

[Collection(CliCollection.Name)]
public sealed class RegexLiteralFinderTests : CliTestBase
{
    public static TheoryData<string, string, RegexOptions, string> DetectedLiterals => new TheoryData<string, string, RegexOptions, string>
    {
        { "Regex r = new Regex(\"abc\");", "abc", RegexOptions.None, "new Regex" },
        { "Regex r = new Regex(@\"\\d+\", RegexOptions.IgnoreCase);", @"\d+", RegexOptions.IgnoreCase, "new Regex" },
        { "Regex r = new System.Text.RegularExpressions.Regex(\"abc\");", "abc", RegexOptions.None, "new Regex" },
        { "Regex r = new Regex(\"\"\"x\"y\"\"\");", "x\"y", RegexOptions.None, "new Regex" },
        { "bool b = Regex.IsMatch(s, \"a\");", "a", RegexOptions.None, "Regex.IsMatch" },
        { "Match m = Regex.Match(s, \"a\", RegexOptions.Multiline);", "a", RegexOptions.Multiline, "Regex.Match" },
        { "var m = Regex.Matches(s, \"a\", RegexOptions.IgnoreCase | RegexOptions.Singleline);", "a", RegexOptions.IgnoreCase | RegexOptions.Singleline, "Regex.Matches" },
        { "string t = Regex.Replace(s, \"a\", \"b\", RegexOptions.IgnoreCase);", "a", RegexOptions.IgnoreCase, "Regex.Replace" },
        { "string t = Regex.Replace(s, \"a\", \"b\");", "a", RegexOptions.None, "Regex.Replace" },
        { "var p = Regex.Split(s, \",\");", ",", RegexOptions.None, "Regex.Split" },
        { "int n = Regex.Count(s, \"a\");", "a", RegexOptions.None, "Regex.Count" },
        { "var e = Regex.EnumerateMatches(s, \"a\");", "a", RegexOptions.None, "Regex.EnumerateMatches" },
        { "var e = Regex.EnumerateSplits(s, \"a\");", "a", RegexOptions.None, "Regex.EnumerateSplits" },
        { "bool b = System.Text.RegularExpressions.Regex.IsMatch(s, \"a\");", "a", RegexOptions.None, "Regex.IsMatch" },
        { "[GeneratedRegex(\"g+\")] static partial Regex G();", "g+", RegexOptions.None, "[GeneratedRegex]" },
        { "[GeneratedRegex(\"g\", RegexOptions.Compiled | RegexOptions.ExplicitCapture)] static partial Regex G();", "g", RegexOptions.Compiled | RegexOptions.ExplicitCapture, "[GeneratedRegex]" },
        { "[GeneratedRegexAttribute(\"g\")] static partial Regex G();", "g", RegexOptions.None, "[GeneratedRegex]" },
        { "[System.Text.RegularExpressions.GeneratedRegex(\"g\")] static partial Regex G();", "g", RegexOptions.None, "[GeneratedRegex]" },
        { "[RegularExpression(@\"^\\w+$\")] string P { get; set; }", @"^\w+$", RegexOptions.None, "[RegularExpression]" },
        { "[System.ComponentModel.DataAnnotations.RegularExpressionAttribute(\"q\")] string P { get; set; }", "q", RegexOptions.None, "[RegularExpression]" },
        { "Regex r = new Regex(\"a\", System.Text.RegularExpressions.RegexOptions.Multiline);", "a", RegexOptions.Multiline, "new Regex" },
        { "Regex r = new Regex(\"a\", RegexOptions.IgnoreCase | RegexOptions.NoSuchOption);", "a", RegexOptions.IgnoreCase, "new Regex" },
        { "Regex r = new Regex(\"a\", Settings.Multiline);", "a", RegexOptions.None, "new Regex" },
        { "Regex r = new Regex(\"a\", options);", "a", RegexOptions.None, "new Regex" },
    };

    public static TheoryData<string> IgnoredSnippets => new TheoryData<string>
    {
        "Regex r = new Regex(pattern);",
        "Regex r = new Regex($\"a{x}\");",
        "Regex r = new Regex(5);",
        "Regex r = new Regex();",
        "Regex r = new Regex { };",
        "StringBuilder b = new StringBuilder(\"abc\");",
        "Regex r = new(\"abc\");",
        "bool b = Regex.IsMatch(s, pattern);",
        "bool b = Regex.IsMatch(s);",
        "bool b = Regex.IsMatch(s, 5);",
        "string e = Regex.Escape(\"a.b\");",
        "string t = Other.Replace(s, \"a\");",
        "string t = s.Replace(\"a\", \"b\");",
        "bool b = Check(s, \"a\");",
        "[GeneratedRegex(Constant)] static partial Regex G();",
        "[GeneratedRegex(5)] static partial Regex G();",
        "[GeneratedRegex()] static partial Regex G();",
        "[GeneratedRegex] static partial Regex G();",
        "[Obsolete(\"abc\")] void M() { }",
    };

    [Theory]
    [MemberData(nameof(DetectedLiterals))]
    public void Find_SupportedConstruct_ReturnsTheLiteral(string member, string pattern, RegexOptions options, string kind)
    {
        string file = WriteFile("Sample.cs", "class Sample\n{\n    " + member + "\n}\n");

        RegexLiteral[] literals = RegexLiteralFinder.Find(file).ToArray();

        literals.Length.ShouldBe(1);
        literals[0].File.ShouldBe(file);
        literals[0].Line.ShouldBe(3);
        literals[0].Pattern.ShouldBe(pattern);
        literals[0].Options.ShouldBe(options);
        literals[0].Kind.ShouldBe(kind);
    }

    [Theory]
    [MemberData(nameof(IgnoredSnippets))]
    public void Find_UnsupportedOrNonLiteralConstruct_ReturnsNothing(string member)
    {
        string file = WriteFile("Sample.cs", "class Sample\n{\n    " + member + "\n}\n");

        RegexLiteralFinder.Find(file).ShouldBeEmpty();
    }

    [Fact]
    public void Find_Literal_ReportsOneBasedLineAndColumnOfTheString()
    {
        string file = WriteFile("Sample.cs", "class Sample\n{\n    Regex r = new Regex(\"abc\");\n}\n");

        RegexLiteral literal = RegexLiteralFinder.Find(file).Single();

        literal.Line.ShouldBe(3);
        literal.Column.ShouldBe(25);
    }

    [Fact]
    public void Find_SeveralLiterals_ReturnsThemInSourceOrder()
    {
        string file = WriteFile("Sample.cs", """
            class Sample
            {
                [GeneratedRegex("first")] static partial Regex G();
                Regex second = new Regex("second");
                bool third = Regex.IsMatch(s, "third");
            }
            """);

        string[] patterns = RegexLiteralFinder.Find(file).Select(literal => literal.Pattern).ToArray();

        patterns.ShouldBe(new[] { "first", "second", "third" });
    }

    [Fact]
    public void Find_MissingFile_Throws()
    {
        string missing = Path.Combine(TempDirectory, "Missing.cs");

        Should.Throw<FileNotFoundException>(() => RegexLiteralFinder.Find(missing).ToArray());
    }

    [Fact]
    public void CSharpFiles_ExistingFile_ReturnsJustThatFile()
    {
        string file = WriteFile("notes.txt", "not C#");

        RegexLiteralFinder.CSharpFiles(file).ShouldBe(new[] { file });
    }

    [Fact]
    public void CSharpFiles_Folder_ReturnsCSharpFilesRecursivelyInOrdinalIgnoreCaseOrder()
    {
        string beta = WriteFile("beta.cs", string.Empty);
        string alpha = WriteFile("Alpha.cs", string.Empty);
        string nested = WriteFile(Path.Combine("sub", "Nested.cs"), string.Empty);
        WriteFile("readme.txt", string.Empty);

        string[] files = RegexLiteralFinder.CSharpFiles(TempDirectory).ToArray();

        files.ShouldBe(new[] { alpha, beta, nested });
    }

    [Theory]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData(".git")]
    [InlineData("node_modules")]
    public void CSharpFiles_Folder_SkipsBuildAndToolingFolders(string excluded)
    {
        string kept = WriteFile("Kept.cs", string.Empty);
        WriteFile(Path.Combine(excluded, "Skipped.cs"), string.Empty);
        WriteFile(Path.Combine("deep", excluded, "Skipped.cs"), string.Empty);

        string[] files = RegexLiteralFinder.CSharpFiles(TempDirectory).ToArray();

        files.ShouldBe(new[] { kept });
    }

    [Fact]
    public void CSharpFiles_FolderNameOnlyContainingExcludedWord_IsKept()
    {
        string file = WriteFile(Path.Combine("binary", "Kept.cs"), string.Empty);

        RegexLiteralFinder.CSharpFiles(TempDirectory).ShouldBe(new[] { file });
    }

    [Fact]
    public void CSharpFiles_MissingPath_ThrowsFileNotFound()
    {
        string missing = Path.Combine(TempDirectory, "missing");

        FileNotFoundException exception = Should.Throw<FileNotFoundException>(() => RegexLiteralFinder.CSharpFiles(missing));

        exception.Message.ShouldBe("No such file or directory: " + missing);
    }
}
