using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Masterly.FluentRegex.Tests.Infrastructure;

/// <summary>Compiles C# source in memory against the runtime and the FluentRegex library.</summary>
internal static class RoslynCompiler
{
    private static readonly Lazy<MetadataReference[]> References = new(() =>
    {
        // The analyzer assemblies compile in their own copy of Pattern, so only the real library may be referenced.
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(p => !Path.GetFileName(p).StartsWith("Masterly.FluentRegex", StringComparison.OrdinalIgnoreCase))
            .ToList();
        paths.Add(typeof(Pattern).Assembly.Location);
        return paths.Distinct().Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToArray();
    });

    public static IReadOnlyList<MetadataReference> MetadataReferences => References.Value;

    public static Assembly Compile(string source)
    {
        var compilation = CSharpCompilation.Create(
            "Generated_" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)) },
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            var errors = string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException("Compilation failed:" + Environment.NewLine + errors + Environment.NewLine + source);
        }
        return Assembly.Load(stream.ToArray());
    }

    /// <summary>Compiles a fluent expression and returns the pattern it builds.</summary>
    public static Pattern Evaluate(string fluentExpression)
    {
        string source = """
            using System;
            using System.Text.RegularExpressions;
            using Masterly.FluentRegex;
            public static class Generated
            {
                public static Pattern Build() =>
            """ + fluentExpression + ";\n}";
        Assembly assembly = Compile(source);
        return (Pattern)assembly.GetType("Generated")!.GetMethod("Build")!.Invoke(null, null)!;
    }
}
