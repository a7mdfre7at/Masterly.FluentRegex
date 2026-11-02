using System.Reflection;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.CodeFixes;
using Masterly.FluentRegex.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace Masterly.FluentRegex.Tests;

public class SourceGeneratorTests
{
    private const string Usings = "using System;\nusing System.Text.RegularExpressions;\nusing Masterly.FluentRegex;\n";

    [Fact]
    public void GeneratesConstantAndCachedRegex()
    {
        var result = AnalyzerHarness.RunGenerator(Usings + """
            namespace Demo;
            public static partial class Validators
            {
                [GeneratePattern]
                public static Pattern IsoDate => Pattern.With.StartOfLine.Digit.Repeat.Times(4).Literal("-").Digit.Repeat.Times(2).EndOfLine;
            }
            """);

        result.GeneratorDiagnostics.ShouldBeEmpty();
        string generated = result.GeneratedSources.Single();
        generated.ShouldContain("public const string IsoDateSource = @\"^\\d{4}-\\d{2}$\";");
        generated.ShouldContain("public static global::System.Text.RegularExpressions.Regex IsoDateRegex()");
        generated.ShouldContain("static partial class Validators");

        Assembly assembly = AnalyzerHarness.Emit(result.Output);
        Type validators = assembly.GetType("Demo.Validators")!;
        var regex = (Regex)validators.GetMethod("IsoDateRegex")!.Invoke(null, null)!;
        regex.IsMatch("2024-05").ShouldBeTrue();
        regex.Options.HasFlag(RegexOptions.Compiled).ShouldBeTrue();
        ReferenceEquals(regex, validators.GetMethod("IsoDateRegex")!.Invoke(null, null)).ShouldBeTrue(); // cached
    }

    [Fact]
    public void EvaluatesComposition_CommonPatterns_Operators_OptionsAndTimeout()
    {
        var result = AnalyzerHarness.RunGenerator(Usings + """
            public partial class Rules
            {
                private static readonly Pattern Area = Pattern.With.Digit.Repeat.Times(3);
                private static Pattern Dash() => Pattern.With.Literal("-");

                [GeneratePattern("PhoneRegex")]
                internal static Pattern Phone =>
                    (Area + Dash() + Area + Dash() + Pattern.With.Digit.Repeat.Times(4))
                    .WithOptions(RegexOptions.IgnoreCase | RegexOptions.Multiline)
                    .WithTimeout(TimeSpan.FromMilliseconds(250));

                [GeneratePattern]
                internal static readonly Pattern Animal = Pattern.With.Literal("cat") | Pattern.With.Literal("dog") | CommonPatterns.Slug();
            }
            """);

        result.GeneratorDiagnostics.ShouldBeEmpty();
        string all = string.Join("\n", result.GeneratedSources);
        all.ShouldContain(@"PhoneSource = @""\d{3}-\d{3}-\d{4}"";");
        all.ShouldContain("internal static global::System.Text.RegularExpressions.Regex PhoneRegex()");
        all.ShouldContain("(global::System.Text.RegularExpressions.RegexOptions)11"); // IgnoreCase | Multiline | Compiled
        all.ShouldContain("TimeSpan.FromTicks(2500000L)");
        all.ShouldContain(@"AnimalSource = @""(?:cat|dog|^[a-z\d]+(?:-[a-z\d]+)*$)"";");

        Assembly assembly = AnalyzerHarness.Emit(result.Output);
        var regex = (Regex)assembly.GetType("Rules")!.GetMethod("PhoneRegex", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        regex.MatchTimeout.ShouldBe(TimeSpan.FromMilliseconds(250));
        regex.IsMatch("555-123-4567").ShouldBeTrue();
    }

    [Fact]
    public void SupportsNestedTypesAndRecords()
    {
        var result = AnalyzerHarness.RunGenerator(Usings + """
            namespace A.B
            {
                public partial record Outer
                {
                    internal partial struct Inner
                    {
                        [GeneratePattern] private static Pattern Word = Pattern.With.Word.Repeat.OneOrMore;
                        public static bool Check(string s) => WordRegex().IsMatch(s);
                    }
                }
            }
            """);

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.GeneratedSources.Single().ShouldContain("partial record Outer");
        Assembly assembly = AnalyzerHarness.Emit(result.Output);
        assembly.GetType("A.B.Outer+Inner")!.GetMethod("Check")!.Invoke(null, new object[] { "hello" }).ShouldBe(true);
    }

    [Fact]
    public void ReportsNonPartialType()
    {
        var result = AnalyzerHarness.RunGenerator(Usings + """
            public static class NotPartial
            {
                [GeneratePattern] public static Pattern P => Pattern.With.Digit;
            }
            """);

        result.GeneratorDiagnostics.Single().Id.ShouldBe("FRX002");
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void ReportsInstanceMember()
    {
        var result = AnalyzerHarness.RunGenerator(Usings + """
            public partial class C
            {
                [GeneratePattern] public Pattern P => Pattern.With.Digit;
            }
            """);

        result.GeneratorDiagnostics.Single().Id.ShouldBe("FRX009");
    }

    [Fact]
    public void ReportsNonConstantChain_AtTheOffendingExpression()
    {
        var result = AnalyzerHarness.RunGenerator(Usings + """
            public static partial class C
            {
                private static string prefix = Environment.MachineName;
                [GeneratePattern] public static Pattern P => Pattern.With.Literal(prefix).Digit;
            }
            """);

        Diagnostic diagnostic = result.GeneratorDiagnostics.Single();
        diagnostic.Id.ShouldBe("FRX001");
        diagnostic.GetMessage().ShouldContain("'Environment.MachineName' is not a compile-time constant");
    }
}

public class AnalyzerTests
{
    private const string Usings = "using System;\nusing System.Text.RegularExpressions;\nusing Masterly.FluentRegex;\n";

    private static async Task<List<Diagnostic>> Analyze(string body) =>
        (await AnalyzerHarness.AnalyzeAsync(Usings + "public static class C {\n" + body + "\n}")).ToList();

    [Fact]
    public async Task CleanCode_HasNoDiagnostics()
    {
        var diagnostics = await Analyze("""
            static Pattern Year = Pattern.With.Digit.Repeat.Times(4);
            static bool M(string s) => Pattern.With.StartOfLine.Then(Year).Literal("-").Digit.Repeat.Times(2).EndOfLine.IsMatch(s);
            """);
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvalidRegex_IsAnError()
    {
        var diagnostics = await Analyze("""static Pattern P = Pattern.With.Digit.Backreference("missing");""");

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX003");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("missing");
    }

    [Fact]
    public async Task ExponentialReDoS_IsAWarning()
    {
        var diagnostics = await Analyze("""static Pattern P = Pattern.With.StartOfLine.Group(Pattern.With.Digit.Repeat.OneOrMore).Repeat.OneOrMore.EndOfLine;""");

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX004");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("Possessive");
    }

    [Fact]
    public async Task PolynomialReDoS_IsInfo()
    {
        var diagnostics = await Analyze("""static Pattern P = Pattern.FromRegex(@"^\d+\d+$");""");
        diagnostics.Single().Id.ShouldBe("FRX005");
    }

    [Theory]
    [InlineData("Pattern.With.Digit.Lazy", "InvalidOperationException")]
    [InlineData("Pattern.With.Digit.Repeat.Times(5, 2)", "ArgumentOutOfRangeException")]
    [InlineData("Pattern.With.Range('z', 'a')", "ArgumentException")]
    [InlineData("Pattern.FromRegex(\"(abc\")", "ArgumentException")]
    public async Task ConstructionErrors_AreCaughtAtCompileTime(string expression, string exception)
    {
        var diagnostics = await Analyze("static Pattern P = " + expression + ";");

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX006");
        diagnostic.GetMessage().ShouldContain(exception);
    }

    [Fact]
    public async Task NestedPatterns_AreReportedOnce_AtTheRoot()
    {
        var diagnostics = await Analyze("""
            static Pattern P = Pattern.With.NamedGroup("a", Pattern.With.Group(Pattern.With.Word.Repeat.OneOrMore).Repeat.ZeroOrMore).Literal("!");
            """);
        diagnostics.Count(d => d.Id == "FRX004").ShouldBe(1);
    }

    [Fact]
    public async Task DynamicPatterns_AreIgnored()
    {
        var diagnostics = await Analyze("static Pattern M(string s) => Pattern.With.Literal(s).Repeat.OneOrMore;");
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Extract_MissingGroup_IsReported()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeAsync(Usings + """
            public record Size(int Width, int Height, string Unit = "px");
            public static class C
            {
                static Size M(string s) => Pattern.With
                    .NamedGroup("width", Pattern.With.Digit.Repeat.OneOrMore)
                    .Literal("x")
                    .NamedGroup("hieght", Pattern.With.Digit.Repeat.OneOrMore)
                    .Extract<Size>(s);
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX008");
        diagnostic.GetMessage().ShouldContain("'Size.Height' has no matching group 'Height'");
        diagnostic.GetMessage().ShouldContain("hieght");
    }

    [Fact]
    public async Task Extract_RespectsRegexGroupAttribute()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeAsync(Usings + """
            public record Size([RegexGroup("w")] int Width);
            public static class C
            {
                static Size M(string s) => Pattern.With.NamedGroup("w", Pattern.With.Digit).Extract<Size>(s);
            }
            """);
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task GeneratedRegexOpportunity_IsSuggested()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeAsync(Usings + """
            public static partial class C
            {
                static readonly Pattern Number = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX007");
        diagnostic.Properties["Pattern"].ShouldBe(@"^\d+$");
    }
}

public class CodeFixTests
{
    [Fact]
    public async Task GeneratedRegexFix_AddsMethod_ThatTheDotNetGeneratorImplements()
    {
        const string source = """
            using Masterly.FluentRegex;
            public static partial class C
            {
                static readonly Pattern Number = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine.WithOptions(System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                public static bool Check(string s) => NumberRegex().IsMatch(s);
            }
            """;

        string fixedSource = await AnalyzerHarness.ApplyCodeFixAsync(source, "FRX007", new GeneratedRegexCodeFix());

        fixedSource.ShouldContain(@"[global::System.Text.RegularExpressions.GeneratedRegex(@""^\d+$"", global::System.Text.RegularExpressions.RegexOptions.IgnoreCase)]");
        fixedSource.ShouldContain("private static partial global::System.Text.RegularExpressions.Regex NumberRegex();");

        ISourceGenerator? dotnetRegexGenerator = AnalyzerHarness.LoadDotNetRegexGenerator();
        if (dotnetRegexGenerator == null) return; // targeting pack not installed: the text checks above still apply

        var result = AnalyzerHarness.RunGenerator(fixedSource, dotnetRegexGenerator);
        Assembly assembly = AnalyzerHarness.Emit(result.Output);
        MethodInfo check = assembly.GetType("C")!.GetMethod("Check")!;
        check.Invoke(null, new object[] { "12345" }).ShouldBe(true);
        check.Invoke(null, new object[] { "12a45" }).ShouldBe(false);
    }
}
