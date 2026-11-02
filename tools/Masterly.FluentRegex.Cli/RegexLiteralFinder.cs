using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Masterly.FluentRegex.Cli;

/// <summary>A regex string literal found in C# source.</summary>
internal sealed record RegexLiteral(string File, int Line, int Column, string Pattern, RegexOptions Options, string Kind);

/// <summary>
/// Finds regex string literals in C# source with Roslyn: <c>new Regex(...)</c>, the static
/// <c>Regex</c> methods, <c>[GeneratedRegex]</c> and <c>[RegularExpression]</c>.
/// </summary>
internal static class RegexLiteralFinder
{
    private static readonly HashSet<string> StaticMethods = new(StringComparer.Ordinal)
    {
        "IsMatch", "Match", "Matches", "Replace", "Split", "Count", "EnumerateMatches", "EnumerateSplits"
    };

    public static IEnumerable<string> CSharpFiles(string path)
    {
        if (File.Exists(path)) return new[] { path };
        if (!Directory.Exists(path)) throw new FileNotFoundException("No such file or directory: " + path);

        return Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part is "bin" or "obj" or ".git" or "node_modules"))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<RegexLiteral> Find(string file)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
        SyntaxNode root = tree.GetRoot();

        foreach (SyntaxNode node in root.DescendantNodes())
        {
            switch (node)
            {
                case BaseObjectCreationExpressionSyntax creation when IsRegexType(creation):
                    if (Literal(creation.ArgumentList, 0) is { } created)
                        yield return Create(file, created, Options(creation.ArgumentList, 1), "new Regex");
                    break;

                case InvocationExpressionSyntax invocation
                    when invocation.Expression is MemberAccessExpressionSyntax access &&
                         access.Expression.ToString() is "Regex" or "System.Text.RegularExpressions.Regex" &&
                         StaticMethods.Contains(access.Name.Identifier.ValueText):
                    if (Literal(invocation.ArgumentList, 1) is { } called)
                        yield return Create(file, called, Options(invocation.ArgumentList, access.Name.Identifier.ValueText == "Replace" ? 3 : 2), "Regex." + access.Name.Identifier.ValueText);
                    break;

                case AttributeSyntax attribute when AttributeName(attribute) is "GeneratedRegex" or "RegularExpression":
                    if (attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax attributed &&
                        attributed.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        RegexOptions options = attribute.ArgumentList.Arguments.Count > 1 ? ParseOptions(attribute.ArgumentList.Arguments[1].Expression) : RegexOptions.None;
                        yield return Create(file, attributed, options, "[" + AttributeName(attribute) + "]");
                    }
                    break;
            }
        }
    }

    private static bool IsRegexType(BaseObjectCreationExpressionSyntax creation) =>
        creation is ObjectCreationExpressionSyntax { Type: var type } && type.ToString() is "Regex" or "System.Text.RegularExpressions.Regex";

    private static string AttributeName(AttributeSyntax attribute)
    {
        string name = attribute.Name.ToString();
        int dot = name.LastIndexOf('.');
        if (dot >= 0) name = name[(dot + 1)..];
        return name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^"Attribute".Length] : name;
    }

    private static LiteralExpressionSyntax? Literal(ArgumentListSyntax? arguments, int index) =>
        arguments != null && arguments.Arguments.Count > index &&
        arguments.Arguments[index].Expression is LiteralExpressionSyntax literal &&
        literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal
            : null;

    private static RegexOptions Options(ArgumentListSyntax? arguments, int index) =>
        arguments != null && arguments.Arguments.Count > index ? ParseOptions(arguments.Arguments[index].Expression) : RegexOptions.None;

    /// <summary>Reads options written as <c>RegexOptions.A | RegexOptions.B</c>.</summary>
    private static RegexOptions ParseOptions(ExpressionSyntax expression)
    {
        RegexOptions options = RegexOptions.None;
        foreach (MemberAccessExpressionSyntax access in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            if (access.Expression.ToString().EndsWith("RegexOptions", StringComparison.Ordinal) &&
                Enum.TryParse(access.Name.Identifier.ValueText, out RegexOptions flag))
                options |= flag;
        }
        return options;
    }

    private static RegexLiteral Create(string file, LiteralExpressionSyntax literal, RegexOptions options, string kind)
    {
        FileLinePositionSpan span = literal.GetLocation().GetLineSpan();
        return new RegexLiteral(file, span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1, literal.Token.ValueText, options, kind);
    }
}
