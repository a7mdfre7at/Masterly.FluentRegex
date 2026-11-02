extern alias analyzers;

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using PatternAnalyzer = analyzers::Masterly.FluentRegex.Analyzers.PatternAnalyzer;
using PatternGenerator = analyzers::Masterly.FluentRegex.Analyzers.PatternGenerator;

namespace Masterly.FluentRegex.Tests.Infrastructure;

/// <summary>Runs the FluentRegex generator, analyzer and code fix on in-memory source.</summary>
internal static class AnalyzerHarness
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    public static CSharpCompilation CreateCompilation(string source) =>
        CSharpCompilation.Create(
            "UserCode",
            new[] { CSharpSyntaxTree.ParseText(source, ParseOptions, path: "UserCode.cs") },
            RoslynCompiler.MetadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    public sealed record GeneratorResult(Compilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics, string[] GeneratedSources);

    public static GeneratorResult RunGenerator(string source, params ISourceGenerator[] extraGenerators)
    {
        CSharpCompilation compilation = CreateCompilation(source);
        var generators = new List<ISourceGenerator> { new PatternGenerator().AsSourceGenerator() };
        generators.AddRange(extraGenerators);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> diagnostics);

        string[] generated = driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()).ToArray();
        return new GeneratorResult(output, diagnostics, generated);
    }

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        CSharpCompilation compilation = CreateCompilation(source);
        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new PatternAnalyzer()));
        ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        return diagnostics.OrderBy(d => d.Location.SourceSpan.Start).ToImmutableArray();
    }

    public static Assembly Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException("Emit failed:\n" + string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return Assembly.Load(stream.ToArray());
    }

    /// <summary>Applies the first code fix offered for the first diagnostic with <paramref name="diagnosticId"/>.</summary>
    public static async Task<string> ApplyCodeFixAsync(string source, string diagnosticId, CodeFixProvider fix)
    {
        using var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);
        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "UserCode", "UserCode", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, ParseOptions)
            .AddMetadataReferences(projectId, RoslynCompiler.MetadataReferences)
            .AddDocument(documentId, "UserCode.cs", SourceText.From(source));

        Document document = solution.GetDocument(documentId)!;
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new PatternAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Diagnostic diagnostic = diagnostics.First(d => d.Id == diagnosticId);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await fix.RegisterCodeFixesAsync(context);
        if (actions.Count == 0) throw new InvalidOperationException("No code fix was offered.");

        ImmutableArray<CodeActionOperation> operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        Solution changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        SourceText text = await changed.GetDocument(documentId)!.GetTextAsync();
        return text.ToString();
    }

    /// <summary>Loads the .NET regex source generator from the installed targeting pack, if present.</summary>
    public static ISourceGenerator? LoadDotNetRegexGenerator()
    {
        string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string dotnetRoot = Path.GetFullPath(Path.Combine(runtimeDirectory, "..", "..", ".."));
        string packs = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
        if (!Directory.Exists(packs)) return null;

        string major = Environment.Version.Major + ".";
        string? path = Directory.GetDirectories(packs)
            .Where(d => Path.GetFileName(d).StartsWith(major, StringComparison.Ordinal))
            .Select(d => Path.Combine(d, "analyzers", "dotnet", "cs", "System.Text.RegularExpressions.Generator.dll"))
            .FirstOrDefault(File.Exists);
        if (path == null) return null;

        Assembly assembly = Assembly.LoadFrom(path);
        Type type = assembly.GetTypes().First(t => typeof(IIncrementalGenerator).IsAssignableFrom(t) && !t.IsAbstract);
        return ((IIncrementalGenerator)Activator.CreateInstance(type)!).AsSourceGenerator();
    }
}
