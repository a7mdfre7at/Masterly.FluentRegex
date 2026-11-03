extern alias analyzers;

using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.CodeFixes;
using Masterly.FluentRegex.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using PatternGenerator = analyzers::Masterly.FluentRegex.Analyzers.PatternGenerator;

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

    private static AnalyzerHarness.GeneratorResult GenerateIn(string typeHeader, string members) =>
        AnalyzerHarness.RunGenerator(Usings + typeHeader + "\n{\n" + members + "\n}");

    private static AnalyzerHarness.GeneratorResult Generate(string members) => GenerateIn("public partial class C", members);

    [Theory]
    [InlineData("[GeneratePattern] public static Pattern P => Pattern.With.Word!;")]
    [InlineData("[GeneratePattern] public static Pattern P => (Pattern.With.Word!);")]
    [InlineData("static Pattern Helper => Pattern.With.Word!;\n[GeneratePattern] public static Pattern P => Helper;")]
    public void Generate_NullForgivingExpression_IsEvaluatedThroughTheOperator(string members)
    {
        AnalyzerHarness.GeneratorResult result = Generate(members);

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.GeneratedSources.Single().ShouldContain(@"public const string PSource = @""\w"";");
    }

    [Fact]
    public void Generate_OneMemberThatCannotBeEvaluated_StillGeneratesTheOthers()
    {
        AnalyzerHarness.GeneratorResult result = Generate(
            "[GeneratePattern] public static Pattern Bad => Pattern.With.Digit.WithTimeout(TimeSpan.Parse(\"abc\"));\n" +
            "[GeneratePattern] public static Pattern Good => Pattern.With.Digit;");

        result.GeneratorDiagnostics.Single().Id.ShouldBe("FRX001");
        result.GeneratedSources.Single().ShouldContain("GoodSource");
    }

    [Fact]
    public void Generate_GeneratedCodeAttribute_CarriesThePackageVersion()
    {
        string version = typeof(Pattern).Assembly.GetName().Version!.ToString(3);

        AnalyzerHarness.GeneratorResult result = Generate("[GeneratePattern] public static Pattern P => Pattern.With.Digit;");

        result.GeneratedSources.Single().ShouldContain(
            $"[global::System.CodeDom.Compiler.GeneratedCode(\"Masterly.FluentRegex.Analyzers\", \"{version}\")]");
    }

    [Fact]
    public void Generate_Diagnostic_IsLocatedInTheUserSyntaxTree_SoPragmaAndEditorConfigApply()
    {
        AnalyzerHarness.GeneratorResult result = Generate("[GeneratePattern] public Pattern Instance => Pattern.With.Digit;");

        Diagnostic diagnostic = result.GeneratorDiagnostics.Single();
        diagnostic.Location.IsInSource.ShouldBeTrue();
        result.Output.SyntaxTrees.ShouldContain(diagnostic.Location.SourceTree!);
    }

    [Theory]
    [InlineData("public", "public static global::System.Text.RegularExpressions.Regex PRegex() =>")]
    [InlineData("internal", "internal static global::System.Text.RegularExpressions.Regex PRegex() =>")]
    [InlineData("protected", "protected static global::System.Text.RegularExpressions.Regex PRegex() =>")]
    [InlineData("protected internal", "protected internal static global::System.Text.RegularExpressions.Regex PRegex() =>")]
    [InlineData("private protected", "private protected static global::System.Text.RegularExpressions.Regex PRegex() =>")]
    [InlineData("private", "private static global::System.Text.RegularExpressions.Regex PRegex() =>")]
    public void Generate_MemberAccessibility_IsCopiedToGeneratedMethod(string accessibility, string expectedSignature)
    {
        AnalyzerHarness.GeneratorResult result = Generate("[GeneratePattern] " + accessibility + " static Pattern P => Pattern.With.Digit;");

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.GeneratedSources.Single().ShouldContain(expectedSignature);
        Should.NotThrow(() => AnalyzerHarness.Emit(result.Output));
    }

    [Theory]
    [InlineData("public partial struct S", "partial struct S")]
    [InlineData("public partial record struct S", "partial record struct S")]
    [InlineData("public partial record class S", "partial record S")]
    [InlineData("public partial interface S", "partial interface S")]
    [InlineData("public static partial class S<T>", "static partial class S<T>")]
    public void Generate_ContainingTypeKind_EmitsMatchingPartialDeclaration(string typeHeader, string expectedHeader)
    {
        AnalyzerHarness.GeneratorResult result = GenerateIn(typeHeader, "[GeneratePattern] public static Pattern P => Pattern.With.Digit;");

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.GeneratedSources.Single().ShouldContain(expectedHeader + Environment.NewLine);
        Should.NotThrow(() => AnalyzerHarness.Emit(result.Output));
    }

    [Fact]
    public void Generate_NoTimeout_UsesInfiniteMatchTimeout()
    {
        AnalyzerHarness.GeneratorResult result = Generate("[GeneratePattern] public static Pattern P => Pattern.With.Digit;");

        result.GeneratedSources.Single().ShouldContain(
            "(global::System.Text.RegularExpressions.RegexOptions)8, global::System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);");
    }

    [Theory]
    [InlineData("public static Pattern P { get; } = Pattern.With.Digit;")]
    [InlineData("public static Pattern P { get => Pattern.With.Digit; }")]
    [InlineData("public static Pattern P { get { return Pattern.With.Digit; } }")]
    [InlineData("public static readonly Pattern P = Pattern.With.Digit;")]
    public void Generate_SingleExpressionMemberForms_AreEvaluated(string member)
    {
        AnalyzerHarness.GeneratorResult result = Generate("[GeneratePattern] " + member);

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.GeneratedSources.Single().ShouldContain(@"public const string PSource = @""\d"";");
    }

    [Theory]
    [InlineData("public static Pattern P { get; }")]
    [InlineData("public static Pattern P;")]
    [InlineData("public static Pattern P { get { Pattern p = Pattern.With.Digit; return p; } }")]
    [InlineData("public static Pattern P { get { throw new InvalidOperationException(); } }")]
    [InlineData("public static Pattern P { set { } }")]
    public void Generate_MemberWithoutSingleExpression_ReportsCannotEvaluate(string member)
    {
        AnalyzerHarness.GeneratorResult result = Generate("[GeneratePattern] " + member);

        Diagnostic diagnostic = result.GeneratorDiagnostics.Single();
        diagnostic.Id.ShouldBe("FRX001");
        diagnostic.GetMessage().ShouldBe("The pattern for 'P' cannot be evaluated at compile time: it has no single-expression initializer or body");
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("static Pattern X { get { return Pattern.With.Digit; } }", "Pattern.With.Then(X)")]
    [InlineData("static Pattern X { get => Pattern.With.Digit; }", "Pattern.With.Then(X)")]
    [InlineData("static Pattern X { get; } = Pattern.With.Digit;", "Pattern.With.Then(X)")]
    [InlineData("static Pattern Make() { return Pattern.With.Digit; }", "Pattern.With.Then(Make())")]
    [InlineData("static readonly Pattern X = Pattern.With.Digit;", "X")]
    public void Generate_ReferencedStaticMember_FollowsItsBody(string helper, string expression)
    {
        AnalyzerHarness.GeneratorResult result = Generate(helper + "\n[GeneratePattern] public static Pattern P => " + expression + ";");

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.GeneratedSources.Single().ShouldContain(@"public const string PSource = @""\d"";");
    }

    [Theory]
    [InlineData("new Pattern().Digit", @"PSource = @""\d"";")]
    [InlineData("Pattern.With.OneOf(\"cat\", \"dog\")", @"PSource = @""(?:cat|dog)"";")]
    [InlineData("Pattern.With.OneOf(new[] { Pattern.With.Digit, Pattern.With.Word })", @"PSource = @""(?:\d|\w)"";")]
    [InlineData("Pattern.With.Literal(new char[] { 'a', '.' })", @"PSource = @""a\."";")]
    [InlineData("Pattern.With.Digit.Repeat.Times(Count)", @"PSource = @""\d{3}"";")]
    [InlineData("Pattern.With.Range((char)Code, 'c')", @"PSource = @""[a-c]"";")]
    [InlineData("Pattern.With.Digit.WithOptions((RegexOptions)Flags)", "(global::System.Text.RegularExpressions.RegexOptions)9")]
    [InlineData("Pattern.With.Unicode(System.Globalization.UnicodeCategory.UppercaseLetter)", @"PSource = @""\p{Lu}"";")]
    [InlineData("CommonPatterns.Integer(DigitMode.Ascii)", "[0-9]")]
    public void Generate_ConstantInputs_AreConvertedAndEvaluated(string expression, string expectedFragment)
    {
        const string Helpers = "static readonly short Count = 3;\nstatic readonly int Code = 97;\nstatic readonly int Flags = 1;";

        AnalyzerHarness.GeneratorResult result = Generate(Helpers + "\n[GeneratePattern] public static Pattern P => " + expression + ";");

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.GeneratedSources.Single().ShouldContain(expectedFragment);
    }

    [Theory]
    [InlineData("TimeSpan.FromDays(1.0)", "864000000000")]
    [InlineData("TimeSpan.FromHours(1.0)", "36000000000")]
    [InlineData("TimeSpan.FromMinutes(1.0)", "600000000")]
    [InlineData("TimeSpan.FromSeconds(1.5)", "15000000")]
    [InlineData("TimeSpan.FromSeconds(Seconds)", "15000000")]
    [InlineData("TimeSpan.FromMilliseconds(1.5)", "15000")]
    [InlineData("TimeSpan.FromMilliseconds(Milliseconds)", "2500000")]
    [InlineData("TimeSpan.FromMicroseconds(1500.0)", "15000")]
    [InlineData("TimeSpan.FromTicks(15000)", "15000")]
    public void Generate_TimeSpanFactories_AreEvaluatedToTicks(string timeout, string expectedTicks)
    {
        const string Helpers = "static readonly float Seconds = 1.5f;\nstatic readonly int Milliseconds = 250;";

        AnalyzerHarness.GeneratorResult result = Generate(Helpers + "\n[GeneratePattern] public static Pattern P => Pattern.With.Digit.WithTimeout(" + timeout + ");");

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.GeneratedSources.Single().ShouldContain("global::System.TimeSpan.FromTicks(" + expectedTicks + "L)");
    }

    [Theory]
    [InlineData("Pattern Inst = Pattern.With.Digit;", "new C().Inst", "instance member 'Inst' is not a compile-time constant")]
    [InlineData("Pattern InstProp => Pattern.With.Digit;", "new C().InstProp", "instance member 'InstProp' is not a compile-time constant")]
    [InlineData("", "Pattern.With.Then(P)", "'P' refers to itself")]
    [InlineData("static Pattern A => B;\nstatic Pattern B => A;", "A", "'A' refers to itself")]
    [InlineData("static string Text(int n) => \"x\";", "Pattern.With.Literal(Text(1))", "method 'Text' is not part of the fluent API")]
    [InlineData("", "Pattern.With.Digit.WithTimeout(TimeSpan.FromSeconds(1.0).Duration())", "method 'Duration' is not part of the fluent API")]
    [InlineData("", "Pattern.With.Digit.WithTimeout(TimeSpan.Zero)", "'TimeSpan.Zero' is not a compile-time constant")]
    [InlineData("", "Pattern.With.Digit.WithTimeout(TimeSpan.Parse(\"5\"))", "TimeSpan.Parse is not evaluated at compile time; use a factory such as TimeSpan.FromSeconds")]
    [InlineData("", "Pattern.With.Digit.WithTimeout(TimeSpan.Parse(\"abc\"))", "TimeSpan.Parse is not evaluated at compile time; use a factory such as TimeSpan.FromSeconds")]
    [InlineData("", "Pattern.With.Digit.WithTimeout(TimeSpan.Parse(\"00:00:01\"))", "TimeSpan.Parse is not evaluated at compile time; use a factory such as TimeSpan.FromSeconds")]
    [InlineData("", "null!", "the expression does not produce a Pattern")]
    [InlineData("", "new Repeat(Pattern.With.Digit).OneOrMore", "'new Repeat(Pattern.With.Digit)' is not a compile-time constant")]
    [InlineData("static readonly int Count = 2;", "Pattern.With.Digit.Repeat.Times(Count + 1)", "'Count + 1' is not a compile-time constant")]
    [InlineData("", "Pattern.With.AsciiDigit", "'AsciiDigit' cannot be evaluated at compile time")]
    [InlineData("", "Pattern.With.Digit.IgnoreDiacritics()", "'IgnoreDiacritics' cannot be evaluated at compile time")]
    [InlineData("public class MyPattern : Pattern { }", "Pattern.With.OneOf(new MyPattern[0])", "type 'C.MyPattern' cannot be evaluated at compile time")]
    [InlineData("static Pattern Make() { Pattern p = Pattern.With.Digit; return p; }", "Make()", "'Make' has no single-expression initializer or body")]
    [InlineData("static Pattern X { get { Pattern p = Pattern.With.Digit; return p; } }", "X", "'X' has no single-expression initializer or body")]
    [InlineData("static Pattern X { get; }", "X", "'X' has no single-expression initializer or body")]
    [InlineData("static Pattern X;", "X", "'X' has no single-expression initializer or body")]
    [InlineData("static Pattern Make() { throw new InvalidOperationException(); }", "Make()", "'Make' has no single-expression initializer or body")]
    [InlineData("static extern Pattern Make();", "Make()", "'Make' has no single-expression initializer or body")]
    [InlineData("", "null", "the expression does not produce a Pattern")]
    public void Generate_NonConstantChain_ReportsCannotEvaluateWithReason(string helpers, string expression, string expectedReason)
    {
        AnalyzerHarness.GeneratorResult result = Generate(helpers + "\n[GeneratePattern] public static Pattern P => " + expression + ";");

        Diagnostic diagnostic = result.GeneratorDiagnostics.Single();
        diagnostic.Id.ShouldBe("FRX001");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe("The pattern for 'P' cannot be evaluated at compile time: " + expectedReason);
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("new long[] { 1 }")]
    [InlineData("new bool[] { true }")]
    [InlineData("new double[] { 1.5 }")]
    [InlineData("new object[] { \"x\" }")]
    [InlineData("new int?[] { 1 }")]
    [InlineData("default(int?)")]
    [InlineData("Pattern.With.Digit.AnalyzeSafety()")]
    public void Generate_NonPatternValueCastToPattern_ReportsItDoesNotProduceAPattern(string value)
    {
        string expression = "(Pattern)(object)" + value;
        string source = Usings + "public partial class C\n{\n[GeneratePattern] public static Pattern P => " + expression + ";\n}";

        AnalyzerHarness.GeneratorResult result = AnalyzerHarness.RunGenerator(source);

        Diagnostic diagnostic = result.GeneratorDiagnostics.Single();
        diagnostic.Id.ShouldBe("FRX001");
        diagnostic.GetMessage().ShouldBe("The pattern for 'P' cannot be evaluated at compile time: the expression does not produce a Pattern");
        diagnostic.Location.SourceSpan.ShouldBe(new TextSpan(source.IndexOf(expression, StringComparison.Ordinal), expression.Length));
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Generate_PatternConstructionThrows_EmitsNothingAndLeavesTheErrorToTheAnalyzer()
    {
        AnalyzerHarness.GeneratorResult result = Generate("[GeneratePattern] public static Pattern P => Pattern.With.Digit.Lazy;");

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Generate_NestedInNonPartialOuterType_ReportsMustBePartialForTheOuterType()
    {
        AnalyzerHarness.GeneratorResult result = GenerateIn("public class Outer", """
            public partial class Inner
            {
                [GeneratePattern] public static Pattern P => Pattern.With.Digit;
            }
            """);

        Diagnostic diagnostic = result.GeneratorDiagnostics.Single();
        diagnostic.Id.ShouldBe("FRX002");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe("Type 'Outer' must be declared partial to use [GeneratePattern]");
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Generate_InstanceMember_ReportsMustBeStaticAtTheMember()
    {
        string source = Usings + "public partial class C\n{\n[GeneratePattern] public Pattern Instance => Pattern.With.Digit;\n}";

        AnalyzerHarness.GeneratorResult result = AnalyzerHarness.RunGenerator(source);

        Diagnostic diagnostic = result.GeneratorDiagnostics.Single();
        diagnostic.GetMessage().ShouldBe("'Instance' must be static to use [GeneratePattern]");
        diagnostic.Location.GetLineSpan().Path.ShouldBe("UserCode.cs");
        diagnostic.Location.SourceSpan.ShouldBe(new TextSpan(source.IndexOf("Instance", StringComparison.Ordinal), "Instance".Length));
    }

    [Theory]
    [InlineData("Pattern.With.RegEx(\"a\\tb\")", "PSource = \"a\\tb\";", "a\tb")]
    [InlineData("Pattern.With.Literal(\"say \\\"hi\\\"\")", "PSource = @\"say \"\"hi\"\"\";", "say \"hi\"")]
    public void Generate_RegexText_IsEmittedAsAnEquivalentStringLiteral(string expression, string expectedDeclaration, string expectedValue)
    {
        AnalyzerHarness.GeneratorResult result = Generate("[GeneratePattern] public static Pattern P => " + expression + ";");

        result.GeneratedSources.Single().ShouldContain(expectedDeclaration);
        Assembly assembly = AnalyzerHarness.Emit(result.Output);
        assembly.GetType("C")!.GetField("PSource")!.GetValue(null).ShouldBe(expectedValue);
    }

    [Fact]
    public void Generate_RegexWithXmlCharacters_EscapesTheDocumentationComment()
    {
        AnalyzerHarness.GeneratorResult result = Generate("[GeneratePattern] public static Pattern P => Pattern.With.Literal(\"<a&b>\");");

        result.GeneratedSources.Single().ShouldContain("/// <c>&lt;a&amp;b&gt;</c>");
    }

    // The outputs are the generated source, then (only for a member with a diagnostic) the diagnostic. The diagnostic
    // is reported against the current compilation, so that output runs again after every edit.
    [Theory]
    [InlineData("[GeneratePattern] public static Pattern P => Pattern.With.Digit;", "[GeneratePattern] public static Pattern P => Pattern.With.Digit; // edited",
        IncrementalStepRunReason.Cached)]
    [InlineData("[GeneratePattern] public Pattern P => Pattern.With.Digit;", "[GeneratePattern] public Pattern P => Pattern.With.Digit; // edited",
        IncrementalStepRunReason.Cached, IncrementalStepRunReason.Modified)]
    [InlineData("[GeneratePattern] public static Pattern P => Pattern.With.Digit;", "[GeneratePattern] public static Pattern P => Pattern.With.Word;",
        IncrementalStepRunReason.Modified)]
    [InlineData("[GeneratePattern] public static Pattern P => Pattern.With.Literal(Environment.MachineName);",
        "[GeneratePattern] public static Pattern P => Pattern.With.Literal(Environment.CommandLine);",
        IncrementalStepRunReason.Unchanged, IncrementalStepRunReason.Modified)]
    public void Generate_RerunAfterEdit_ReusesOutputOnlyWhenTheModelIsEqual(
        string originalMember, string editedMember, params IncrementalStepRunReason[] expected)
    {
        CSharpParseOptions parseOptions = new(LanguageVersion.Latest);
        CSharpCompilation compilation = AnalyzerHarness.CreateCompilation(Usings + "public partial class C\n{\n" + originalMember + "\n}");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new PatternGenerator().AsSourceGenerator() },
            parseOptions: parseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        SyntaxTree edited = CSharpSyntaxTree.ParseText(Usings + "public partial class C\n{\n" + editedMember + "\n}", parseOptions, path: "UserCode.cs", cancellationToken: TestContext.Current.CancellationToken);

        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), edited), TestContext.Current.CancellationToken);

        GeneratorRunResult run = driver.GetRunResult().Results.Single();
        IncrementalStepRunReason[] reasons = run.TrackedOutputSteps["SourceOutput"]
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ToArray();

        reasons.ShouldBe(expected);
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
    [InlineData("Pattern.With.Then(null)", "ArgumentNullException")]
    [InlineData("Pattern.With.Then(default(Pattern))", "ArgumentNullException")]
    [InlineData("Pattern.With.OneOf(new string[0])", "ArgumentException")]
    [InlineData("Pattern.With.Digit.WithTimeout(default(TimeSpan))", "ArgumentOutOfRangeException")]
    [InlineData("Pattern.With.Repeat.OneOrMore", "InvalidOperationException")]
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

    [Theory]
    [InlineData("int Height", "so extracting throws InvalidOperationException")]
    [InlineData("int? Height", "so it is always null")]
    [InlineData("string Height", "so it is always null")]
    public async Task Extract_MissingGroup_StatesWhatHappensAtRunTime(string parameter, string expectedConsequence)
    {
        var diagnostics = await AnalyzerHarness.AnalyzeAsync(Usings + "public record Size(int Width, " + parameter + ");\n" + """
            public static class C
            {
                static Size M(string s) => Pattern.With.NamedGroup("width", Pattern.With.Digit).Extract<Size>(s);
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX008");
        diagnostic.GetMessage().ShouldContain("'Size.Height' has no matching group 'Height' in the pattern, " + expectedConsequence + ".");
    }

    [Fact]
    public async Task NonBacktrackingWithAnUnsupportedConstruct_IsReportedInsteadOfCrashing()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeAsync(Usings + """
            public static class C
            {
                static readonly Pattern P = Pattern.With.PositiveLookbehind(Pattern.With.Literal("$")).Digit
                    .WithOptions(RegexOptions.NonBacktracking);
            }
            """);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX003");
        diagnostic.GetMessage().ShouldContain("NonBacktracking");
    }

    [Fact]
    public async Task Extract_OnANonBacktrackingPatternWithAnUnsupportedConstruct_DoesNotCrash()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeAsync(Usings + """
            public record Price(int Amount);
            public static class C
            {
                static Price M(string s) => Pattern.With.PositiveLookbehind(Pattern.With.Literal("$"))
                    .NamedGroup("amount", Pattern.With.Digit)
                    .WithOptions(RegexOptions.NonBacktracking)
                    .Extract<Price>(s);
            }
            """);

        diagnostics.ShouldNotContain(diagnostic => diagnostic.Id == "AD0001");
    }

    [Fact]
    public async Task GeneratedRegexOpportunity_AfterTheCodeFixWasApplied_IsNotSuggestedAgain()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeAsync(Usings + """
            public static partial class C
            {
                static readonly Pattern Number = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;

                [GeneratedRegex(@"^\d+$")]
                private static partial Regex NumberRegex();
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task GeneratedRegexOpportunity_WithDifferentOptionsInTheExistingMethod_IsStillSuggested()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeAsync(Usings + """
            public static partial class C
            {
                static readonly Pattern Number = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;

                [GeneratedRegex(@"^\d+$", RegexOptions.IgnoreCase)]
                private static partial Regex NumberRegex();
            }
            """);

        diagnostics.Single().Id.ShouldBe("FRX007");
    }

    [Fact]
    public async Task GeneratedRegexOpportunity_InsideANonPartialOuterType_IsNotSuggested()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeAsync(Usings + """
            public class Outer
            {
                public static partial class Inner
                {
                    static readonly Pattern Number = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;
                }
            }
            """);

        diagnostics.ShouldNotContain(diagnostic => diagnostic.Id == "FRX007");
    }

    [Fact]
    public async Task GeneratedRegexOpportunity_OnAGeneratePatternMember_IsNotSuggested()
    {
        var diagnostics = await AnalyzerHarness.AnalyzeAsync(Usings + """
            public static partial class C
            {
                [GeneratePattern]
                static Pattern Number => Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;
            }
            """);

        diagnostics.ShouldNotContain(diagnostic => diagnostic.Id == "FRX007");
    }

    private static string TextAt(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    private static async Task<List<Diagnostic>> AnalyzePartial(string body) =>
        (await AnalyzerHarness.AnalyzeAsync(Usings + "public static partial class C\n{\n" + body + "\n}")).ToList();

    private static async Task<List<Diagnostic>> AnalyzeExtract(string types, string method) =>
        (await AnalyzerHarness.AnalyzeAsync(Usings + types + "\npublic static class C\n{\n" + method + "\n}")).ToList();

    [Fact]
    public async Task Analyze_ConstructionError_IsReportedAtTheThrowingCall()
    {
        List<Diagnostic> diagnostics = await Analyze("""static Pattern P = Pattern.With.Digit.Repeat.Times(5, 2).Literal("x");""");

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX006");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        TextAt(diagnostic).ShouldBe("Pattern.With.Digit.Repeat.Times(5, 2)");
    }

    [Theory]
    [InlineData("""static object Boxed = Pattern.With.Digit.Backreference("missing");""", """Pattern.With.Digit.Backreference("missing")""")]
    [InlineData("""static string Joined = string.Concat(Pattern.With.Digit.Backreference("missing"), "x");""", """Pattern.With.Digit.Backreference("missing")""")]
    [InlineData("""static bool Empty = Pattern.With.Digit.Backreference("missing").IsEmpty;""", """Pattern.With.Digit.Backreference("missing")""")]
    [InlineData("""static bool Same = Pattern.With.Digit.Backreference("missing") == null;""", """Pattern.With.Digit.Backreference("missing")""")]
    [InlineData("""static Pattern[] All = { Pattern.With.Digit.Backreference("missing") };""", """Pattern.With.Digit.Backreference("missing")""")]
    [InlineData("""static Pattern Sum = Pattern.With.Digit.Backreference("missing") + Pattern.With.Word;""", """Pattern.With.Digit.Backreference("missing") + Pattern.With.Word""")]
    [InlineData("""static Pattern Choice = Pattern.With.OneOf(Pattern.With.Digit.Backreference("missing"), Pattern.With.Word);""", """Pattern.With.OneOf(Pattern.With.Digit.Backreference("missing"), Pattern.With.Word)""")]
    public async Task Analyze_ChainRoot_IsTheOutermostPatternExpression(string declaration, string expectedLocation)
    {
        List<Diagnostic> diagnostics = await Analyze(declaration);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX003");
        TextAt(diagnostic).ShouldBe(expectedLocation);
    }

    [Fact]
    public async Task Analyze_ReDoSInsideParamsArgument_IsReportedOnceAtTheRoot()
    {
        List<Diagnostic> diagnostics = await Analyze(
            "static Pattern P = Pattern.With.OneOf(Pattern.With.StartOfLine.Group(Pattern.With.Digit.Repeat.OneOrMore).Repeat.OneOrMore.EndOfLine, Pattern.With.Word);");

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX004");
        TextAt(diagnostic).ShouldStartWith("Pattern.With.OneOf(");
    }

    [Fact]
    public async Task Analyze_LocalVariableInChain_IsTreatedAsDynamicAndIgnored()
    {
        List<Diagnostic> diagnostics = await Analyze("""
            static bool M()
            {
                string name = "missing";
                return Pattern.With.Digit.Backreference(name).IsEmpty;
            }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Analyze_UnparseableTimeSpanArgument_IsTreatedAsDynamicAndIgnored()
    {
        List<Diagnostic> diagnostics = await Analyze("static Pattern P = Pattern.With.Digit.WithTimeout(TimeSpan.Parse(\"5\"));");

        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("static object M(string s) => Pattern.With.NamedGroup(\"Width\", Pattern.With.Digit).Extract<Size>(s);", "Extract<Size>")]
    [InlineData("static object M(string s) => Pattern.With.NamedGroup(\"Width\", Pattern.With.Digit).TryExtract<Size>(s, out Size _);", "TryExtract<Size>")]
    [InlineData("static object M(string s) => Pattern.With.NamedGroup(\"Width\", Pattern.With.Digit).ExtractAll<Size>(s);", "ExtractAll<Size>")]
    public async Task Analyze_EveryExtractOverload_ReportsMissingGroupAtTheMethodName(string method, string expectedLocation)
    {
        List<Diagnostic> diagnostics = await AnalyzeExtract("public record Size(int Width, int Height);", method);

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX008");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe("'Size.Height' has no matching group 'Height' in the pattern, so extracting throws InvalidOperationException. Available groups: 0, Width.");
        TextAt(diagnostic).ShouldBe(expectedLocation);
    }

    [Theory]
    [InlineData("public record Size(int Width);", "static Size M(string s) => Pattern.With.Literal(s).Extract<Size>(s);")]
    [InlineData("public record Size(int Width);", "static T M<T>(string s) => Pattern.With.Digit.Extract<T>(s);")]
    [InlineData("public class Hidden { private Hidden() { } }", "static Hidden M(string s) => Pattern.With.Digit.Extract<Hidden>(s);")]
    [InlineData("public record Size([property: RegexGroup(\"w\")] int Width);", "static Size M(string s) => Pattern.With.NamedGroup(\"w\", Pattern.With.Digit).Extract<Size>(s);")]
    [InlineData("public record Size(int Width);", "static Size M(string s) => Pattern.With.NamedGroup(\"WIDTH\", Pattern.With.Digit).Extract<Size>(s);")]
    [InlineData("public record Size(int Width, int Height = 1);", "static Size M(string s) => Pattern.With.NamedGroup(\"Width\", Pattern.With.Digit).Extract<Size>(s);")]
    public async Task Analyze_ExtractWithBindableOrUncheckableTarget_ReportsNothing(string types, string method)
    {
        List<Diagnostic> diagnostics = await AnalyzeExtract(types, method);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Analyze_ExtractOnInvalidRegex_ReportsOnlyTheInvalidRegex()
    {
        List<Diagnostic> diagnostics = await AnalyzeExtract(
            "public record Size(int Width);",
            "static Size M(string s) => Pattern.With.Digit.Backreference(\"missing\").Extract<Size>(s);");

        diagnostics.Select(d => d.Id).ShouldBe(new[] { "FRX003" });
    }

    [Theory]
    [InlineData("[System.ComponentModel.Description(\"w\")]")]
    [InlineData("[Other.RegexGroup(\"w\")]")]
    public async Task Analyze_ExtractParameterWithUnrelatedAttribute_UsesTheParameterName(string attribute)
    {
        const string OtherAttribute = "namespace Other { public sealed class RegexGroupAttribute : System.Attribute { public RegexGroupAttribute(string name) { } } }";

        List<Diagnostic> diagnostics = await AnalyzeExtract(
            OtherAttribute + "\npublic record Size(" + attribute + " int Width);",
            "static Size M(string s) => Pattern.With.NamedGroup(\"w\", Pattern.With.Digit).Extract<Size>(s);");

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX008");
        diagnostic.GetMessage().ShouldContain("'Size.Width' has no matching group 'Width'");
    }

    [Fact]
    public async Task Analyze_GeneratedRegexOpportunityOnProperty_IsSuggestedAtTheIdentifier()
    {
        List<Diagnostic> diagnostics = await AnalyzePartial("static Pattern Number => Pattern.With.Digit;");

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX007");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        diagnostic.GetMessage().ShouldBe("'Number' is a compile-time constant pattern and can be backed by a [GeneratedRegex] method");
        TextAt(diagnostic).ShouldBe("Number");
    }

    [Theory]
    [InlineData("Pattern.With.Digit", @"\d", "0", "")]
    [InlineData("Pattern.With.Digit.WithOptions(RegexOptions.Multiline).WithTimeout(TimeSpan.FromSeconds(2.0))", @"\d", "2", "2000")]
    [InlineData("Pattern.With.Digit.WithOptions(RegexOptions.IgnoreCase | RegexOptions.Compiled)", @"\d", "9", "")]
    public async Task Analyze_GeneratedRegexOpportunity_CarriesPatternOptionsAndTimeout(string expression, string pattern, string options, string timeout)
    {
        List<Diagnostic> diagnostics = await AnalyzePartial("static readonly Pattern Number = " + expression + ";");

        ImmutableDictionary<string, string?> properties = diagnostics.Single().Properties;
        properties["Pattern"].ShouldBe(pattern);
        properties["Options"].ShouldBe(options);
        properties["TimeoutMilliseconds"].ShouldBe(timeout);
    }

    [Fact]
    public async Task Analyze_MultipleDeclarators_SuggestsOnlyInitializedOnes()
    {
        List<Diagnostic> diagnostics = await AnalyzePartial("static readonly Pattern Unset, Number = Pattern.With.Digit;");

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX007");
        TextAt(diagnostic).ShouldBe("Number");
    }

    [Theory]
    [InlineData("Pattern Instance = Pattern.With.Digit;")]
    [InlineData("static readonly string Text = \"x\";")]
    [InlineData("static Pattern Dynamic => Pattern.With.Literal(Environment.MachineName);")]
    [InlineData("static Pattern Auto { get; }")]
    [InlineData("static Pattern Block { get { Pattern p = Pattern.With.Digit; return p; } }")]
    public async Task Analyze_MemberThatIsNotAConstantStaticPattern_IsNotSuggested(string member)
    {
        List<Diagnostic> diagnostics = await AnalyzePartial(member);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Analyze_GenericHelperReturningDefaultOfTypeParameter_ReportsTheNullArgument()
    {
        List<Diagnostic> diagnostics = await Analyze("static Pattern? Make<T>() => default(T) as Pattern;\nstatic Pattern P = Pattern.With.Then(Make<string>()!);");

        Diagnostic diagnostic = diagnostics.Single();
        diagnostic.Id.ShouldBe("FRX006");
        diagnostic.GetMessage().ShouldStartWith("This pattern throws ArgumentNullException when it is built:");
        TextAt(diagnostic).ShouldBe("Pattern.With.Then(Make<string>()!)");
    }

    [Fact]
    public async Task Analyze_InvalidStaticPatternInPartialType_ReportsOnlyTheInvalidRegex()
    {
        List<Diagnostic> diagnostics = await AnalyzePartial("static readonly Pattern Bad = Pattern.With.Digit.Backreference(\"missing\");");

        diagnostics.Select(d => d.Id).ShouldBe(new[] { "FRX003" });
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

    private const string Usings = "using System;\nusing System.Text.RegularExpressions;\nusing Masterly.FluentRegex;\n";

    private static Task<string> FixAsync(string members) =>
        AnalyzerHarness.ApplyCodeFixAsync(Usings + "public static partial class C\n{\n" + members + "\n}", "FRX007", new GeneratedRegexCodeFix());

    /// <summary>Offers fixes for a hand-made FRX007 diagnostic placed on <paramref name="identifier"/>.</summary>
    private static async Task<List<CodeAction>> OfferedFixesAsync(string source, string identifier, ImmutableDictionary<string, string?> properties)
    {
        using AdhocWorkspace workspace = new();
        Document document = workspace.AddProject("UserCode", LanguageNames.CSharp).AddDocument("UserCode.cs", SourceText.From(source));
        SyntaxTree tree = (await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken))!; // a C# document always has a syntax tree
        TextSpan span = new(source.IndexOf(identifier, StringComparison.Ordinal), identifier.Length);
        DiagnosticDescriptor descriptor = new("FRX007", "title", "message", "category", DiagnosticSeverity.Info, isEnabledByDefault: true);
        Diagnostic diagnostic = Diagnostic.Create(descriptor, Location.Create(tree, span), properties);
        List<CodeAction> actions = new();
        CodeFixContext context = new(document, diagnostic, (action, _) => actions.Add(action), TestContext.Current.CancellationToken);

        await new GeneratedRegexCodeFix().RegisterCodeFixesAsync(context);

        return actions;
    }

    private static ImmutableDictionary<string, string?> PatternProperties(string? pattern) =>
        ImmutableDictionary<string, string?>.Empty.Add("Pattern", pattern).Add("Options", "0").Add("TimeoutMilliseconds", string.Empty);

    [Fact]
    public void FixableDiagnosticIds_ContainsOnlyTheGeneratedRegexSuggestion()
    {
        GeneratedRegexCodeFix fix = new();

        fix.FixableDiagnosticIds.ShouldBe(new[] { "FRX007" });
    }

    [Fact]
    public void GetFixAllProvider_ReturnsTheBatchFixer()
    {
        GeneratedRegexCodeFix fix = new();

        fix.GetFixAllProvider().ShouldBeSameAs(WellKnownFixAllProviders.BatchFixer);
    }

    [Theory]
    [InlineData("Pattern.With.Digit", @"GeneratedRegex(@""\d"")]")]
    [InlineData("Pattern.With.Digit.WithOptions(RegexOptions.Compiled)", @"GeneratedRegex(@""\d"")]")]
    [InlineData("Pattern.With.Digit.WithOptions(RegexOptions.Multiline | RegexOptions.Singleline)",
        @"GeneratedRegex(@""\d"", global::System.Text.RegularExpressions.RegexOptions.Multiline | global::System.Text.RegularExpressions.RegexOptions.Singleline)]")]
    [InlineData("Pattern.With.Digit.WithOptions(RegexOptions.ExplicitCapture)", @"GeneratedRegex(@""\d"", global::System.Text.RegularExpressions.RegexOptions.ExplicitCapture)]")]
    [InlineData("Pattern.With.Digit.WithOptions(RegexOptions.IgnorePatternWhitespace)", @"GeneratedRegex(@""\d"", global::System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace)]")]
    [InlineData("Pattern.With.Digit.WithOptions(RegexOptions.RightToLeft)", @"GeneratedRegex(@""\d"", global::System.Text.RegularExpressions.RegexOptions.RightToLeft)]")]
    [InlineData("Pattern.With.Digit.WithOptions(RegexOptions.ECMAScript)", @"GeneratedRegex(@""\d"", global::System.Text.RegularExpressions.RegexOptions.ECMAScript)]")]
    [InlineData("Pattern.With.Digit.WithOptions(RegexOptions.CultureInvariant)", @"GeneratedRegex(@""\d"", global::System.Text.RegularExpressions.RegexOptions.CultureInvariant)]")]
    [InlineData("Pattern.With.Digit.WithOptions(RegexOptions.NonBacktracking)", @"GeneratedRegex(@""\d"", global::System.Text.RegularExpressions.RegexOptions.NonBacktracking)]")]
    [InlineData("Pattern.With.Digit.WithTimeout(TimeSpan.FromSeconds(2.0))", @"GeneratedRegex(@""\d"", global::System.Text.RegularExpressions.RegexOptions.None, 2000)]")]
    [InlineData("Pattern.With.Digit.WithOptions(RegexOptions.IgnoreCase).WithTimeout(TimeSpan.FromMilliseconds(500.0))",
        @"GeneratedRegex(@""\d"", global::System.Text.RegularExpressions.RegexOptions.IgnoreCase, 500)]")]
    [InlineData("Pattern.With.RegEx(\"a\\tb\")", "GeneratedRegex(\"a\\tb\")]")]
    [InlineData("Pattern.With.Literal(\"\\\"\")", "GeneratedRegex(@\"\"\"\")]")]
    public async Task GeneratedRegexFix_PatternSettings_AreTranslatedToAttributeArguments(string expression, string expectedAttribute)
    {
        string fixedSource = await FixAsync("static readonly Pattern Number = " + expression + ";");

        fixedSource.ShouldContain("[global::System.Text.RegularExpressions." + expectedAttribute);
    }

    [Fact]
    public async Task GeneratedRegexFix_OnProperty_AddsMethodAfterTheProperty()
    {
        string fixedSource = await FixAsync("static Pattern Number => Pattern.With.Digit;");

        int property = fixedSource.IndexOf("static Pattern Number =>", StringComparison.Ordinal);
        int method = fixedSource.IndexOf("private static partial global::System.Text.RegularExpressions.Regex NumberRegex();", StringComparison.Ordinal);
        property.ShouldBeGreaterThanOrEqualTo(0);
        method.ShouldBeGreaterThan(property);
        fixedSource.ShouldContain("/// <summary>Generated by the .NET regex source generator from <see cref=\"Number\"/>.</summary>");
    }

    [Fact]
    public async Task GeneratedRegexFix_NameAlreadyTaken_AppendsTheFirstFreeNumber()
    {
        string fixedSource = await FixAsync("""
            static readonly Pattern Number = Pattern.With.Digit;
            static int NumberRegex() => 1;
            static int NumberRegex2() => 2;
            """);

        fixedSource.ShouldContain("private static partial global::System.Text.RegularExpressions.Regex NumberRegex3();");
    }

    [Fact]
    public async Task RegisterCodeFixes_DiagnosticOnPatternField_OffersOneTitledAction()
    {
        const string Source = "using Masterly.FluentRegex;\npublic static partial class C\n{\n    static readonly Pattern Number = Pattern.With.Digit;\n}";

        List<CodeAction> actions = await OfferedFixesAsync(Source, "Number", PatternProperties(@"\d"));

        CodeAction action = actions.Single();
        action.Title.ShouldBe("Add [GeneratedRegex] method 'NumberRegex()'");
        action.EquivalenceKey.ShouldBe("FRX007");
    }

    [Fact]
    public async Task RegisterCodeFixes_DiagnosticWithoutPatternProperty_OffersNothing()
    {
        const string Source = "using Masterly.FluentRegex;\npublic static partial class C\n{\n    static readonly Pattern Number = Pattern.With.Digit;\n}";

        List<CodeAction> actions = await OfferedFixesAsync(Source, "Number", ImmutableDictionary<string, string?>.Empty);

        actions.ShouldBeEmpty();
    }

    [Fact]
    public async Task RegisterCodeFixes_DiagnosticWithNullPatternProperty_OffersNothing()
    {
        const string Source = "using Masterly.FluentRegex;\npublic static partial class C\n{\n    static readonly Pattern Number = Pattern.With.Digit;\n}";

        List<CodeAction> actions = await OfferedFixesAsync(Source, "Number", PatternProperties(null));

        actions.ShouldBeEmpty();
    }

    [Fact]
    public async Task RegisterCodeFixes_DiagnosticOutsideFieldOrProperty_OffersNothing()
    {
        const string Source = "using Masterly.FluentRegex;\npublic static partial class C\n{\n    static Pattern Build() => Pattern.With.Digit;\n}";

        List<CodeAction> actions = await OfferedFixesAsync(Source, "Build", PatternProperties(@"\d"));

        actions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyFix_OptionsWithUndefinedAndCompiledBits_EmitsOnlyNamedFlags()
    {
        const string Source = "using Masterly.FluentRegex;\npublic static partial class C\n{\n    static readonly Pattern Number = Pattern.With.Digit;\n}";
        ImmutableDictionary<string, string?> properties = PatternProperties(@"\d").SetItem("Options", "137");
        CodeAction action = (await OfferedFixesAsync(Source, "Number", properties)).Single();

        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(TestContext.Current.CancellationToken);

        Solution changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        string fixedSource = (await changed.Projects.Single().Documents.Single().GetTextAsync(TestContext.Current.CancellationToken)).ToString();
        fixedSource.ShouldContain(@"[global::System.Text.RegularExpressions.GeneratedRegex(@""\d"", global::System.Text.RegularExpressions.RegexOptions.IgnoreCase)]");
    }
}
