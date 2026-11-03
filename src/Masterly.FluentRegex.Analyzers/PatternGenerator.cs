using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Masterly.FluentRegex.Analyzers
{
    /// <summary>
    /// Evaluates <c>[GeneratePattern]</c> members at compile time and emits a constant with the regex
    /// text plus a method returning a cached, compiled <see cref="Regex"/>.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class PatternGenerator : IIncrementalGenerator
    {
        private const string AttributeName = "Masterly.FluentRegex.GeneratePatternAttribute";

        // The analyzer project takes its version from the core library project, so this matches the package.
        private static readonly string GeneratorVersion = typeof(PatternGenerator).Assembly.GetName().Version.ToString(3);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<GenerationModel> models = context.SyntaxProvider.ForAttributeWithMetadataName(
                AttributeName,
                static (node, _) => node is VariableDeclaratorSyntax || node is PropertyDeclarationSyntax,
                static (ctx, ct) => CreateModel(ctx, ct));

            context.RegisterSourceOutput(models, static (spc, model) => Emit(spc, model));

            // Diagnostics are resolved against the compilation's own syntax trees, so #pragma warning disable and
            // .editorconfig severities apply to them. Only members with a diagnostic pay for the compilation input.
            IncrementalValuesProvider<DiagnosticInfo> diagnostics = models.SelectMany(static (model, _) => model.Diagnostics);
            context.RegisterSourceOutput(
                diagnostics.Combine(context.CompilationProvider),
                static (spc, pair) => spc.ReportDiagnostic(pair.Left.ToDiagnostic(pair.Right)));
        }

        private static GenerationModel CreateModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            ISymbol member = context.TargetSymbol;
            var model = new GenerationModel { MemberName = member.Name };

            AttributeData attribute = context.Attributes[0];
            string methodName = attribute.ConstructorArguments.Length == 1 ? attribute.ConstructorArguments[0].Value as string : null;
            model.MethodName = string.IsNullOrEmpty(methodName) ? member.Name + "Regex" : methodName;
            model.Accessibility = AccessibilityKeyword(member.DeclaredAccessibility);

            Location location = member.Locations.FirstOrDefault() ?? Location.None;

            if (!member.IsStatic)
            {
                model.Diagnostics.Add(DiagnosticInfo.Create(Diagnostics.MustBeStatic, location, member.Name));
                return model;
            }

            // Collect the containing type chain and require every type to be partial.
            for (INamedTypeSymbol type = member.ContainingType; type != null; type = type.ContainingType)
            {
                var declaration = type.DeclaringSyntaxReferences.Select(r => r.GetSyntax(cancellationToken)).OfType<TypeDeclarationSyntax>().FirstOrDefault();
                if (declaration == null || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    model.Diagnostics.Add(DiagnosticInfo.Create(Diagnostics.MustBePartial, location, type.Name));
                    return model;
                }
                model.Types.Insert(0, TypeHeader(declaration, type));
            }
            model.Namespace = member.ContainingType.ContainingNamespace.IsGlobalNamespace ? null : member.ContainingType.ContainingNamespace.ToDisplayString();

            ExpressionSyntax expression = ChainEvaluator.BodyExpression(context.TargetNode);
            if (expression == null)
            {
                model.Diagnostics.Add(DiagnosticInfo.Create(Diagnostics.CannotEvaluate, location, member.Name, "it has no single-expression initializer or body"));
                return model;
            }

            IOperation operation = context.SemanticModel.GetOperation(expression, cancellationToken);
            try
            {
                Pattern pattern = new ChainEvaluator(context.SemanticModel.Compilation, cancellationToken).EvaluatePattern(operation);
                model.Regex = pattern.ToString();
                model.Options = (int)pattern.Options;
                model.TimeoutTicks = pattern.Timeout.Ticks;
            }
            catch (NotEvaluableException ex)
            {
                model.Diagnostics.Add(DiagnosticInfo.Create(Diagnostics.CannotEvaluate, ex.Operation?.Syntax.GetLocation() ?? location, member.Name, ex.Message));
            }
            catch (PatternConstructionException)
            {
                // Reported by the analyzer (FRX006); nothing can be generated.
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                // An unexpected evaluation failure must not stop generation for every other member in the project.
                model.Diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.CannotEvaluate, location, member.Name, "evaluation failed with " + ex.GetType().Name + ": " + ex.Message));
            }
            return model;
        }

        private static string TypeHeader(TypeDeclarationSyntax declaration, INamedTypeSymbol type)
        {
            string keyword;
            switch (declaration)
            {
                case RecordDeclarationSyntax record:
                    keyword = record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record";
                    break;
                case StructDeclarationSyntax _:
                    keyword = "struct";
                    break;
                case InterfaceDeclarationSyntax _:
                    keyword = "interface";
                    break;
                default:
                    keyword = "class";
                    break;
            }
            string modifiers = type.IsStatic ? "static partial " : "partial ";
            string typeParameters = type.TypeParameters.Length > 0 ? "<" + string.Join(", ", type.TypeParameters.Select(t => t.Name)) + ">" : string.Empty;
            return modifiers + keyword + " " + type.Name + typeParameters;
        }

        private static string AccessibilityKeyword(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.Public: return "public";
                case Accessibility.Internal: return "internal";
                case Accessibility.Protected: return "protected";
                case Accessibility.ProtectedOrInternal: return "protected internal";
                case Accessibility.ProtectedAndInternal: return "private protected";
                default: return "private";
            }
        }

        private static void Emit(SourceProductionContext context, GenerationModel model)
        {
            if (model.Regex == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();

            string indent = string.Empty;
            if (model.Namespace != null)
            {
                sb.Append("namespace ").AppendLine(model.Namespace);
                sb.AppendLine("{");
                indent = "    ";
            }

            foreach (string header in model.Types)
            {
                sb.Append(indent).AppendLine(header);
                sb.Append(indent).AppendLine("{");
                indent += "    ";
            }

            int options = model.Options | (int)RegexOptions.Compiled;
            string field = "__fluentRegex_" + model.MemberName;

            sb.Append(indent).AppendLine("/// <summary>");
            sb.Append(indent).Append("/// The regular expression built by <see cref=\"").Append(model.MemberName).AppendLine("\"/>, evaluated at compile time.");
            sb.Append(indent).AppendLine("/// </summary>");
            sb.Append(indent).Append("public const string ").Append(model.MemberName).Append("Source = ").Append(StringLiteral(model.Regex)).AppendLine(";");
            sb.AppendLine();
            sb.Append(indent).Append("private static global::System.Text.RegularExpressions.Regex? ").Append(field).AppendLine(";");
            sb.AppendLine();
            sb.Append(indent).AppendLine("/// <summary>");
            sb.Append(indent).Append("/// A cached, compiled regex for <see cref=\"").Append(model.MemberName).AppendLine("\"/>:");
            sb.Append(indent).Append("/// <c>").Append(XmlEscape(model.Regex)).AppendLine("</c>");
            sb.Append(indent).AppendLine("/// </summary>");
            sb.Append(indent).Append("[global::System.CodeDom.Compiler.GeneratedCode(\"Masterly.FluentRegex.Analyzers\", \"")
              .Append(GeneratorVersion).AppendLine("\")]");
            sb.Append(indent).Append(model.Accessibility).Append(" static global::System.Text.RegularExpressions.Regex ").Append(model.MethodName).AppendLine("() =>");
            sb.Append(indent).Append("    ").Append(field).Append(" ??= new global::System.Text.RegularExpressions.Regex(")
              .Append(model.MemberName).Append("Source, (global::System.Text.RegularExpressions.RegexOptions)").Append(options.ToString(CultureInfo.InvariantCulture))
              .Append(", ").Append(model.TimeoutTicks == Regex.InfiniteMatchTimeout.Ticks
                  ? "global::System.Text.RegularExpressions.Regex.InfiniteMatchTimeout"
                  : "global::System.TimeSpan.FromTicks(" + model.TimeoutTicks.ToString(CultureInfo.InvariantCulture) + "L)")
              .AppendLine(");");

            for (int i = model.Types.Count - 1; i >= 0; i--)
            {
                indent = indent.Substring(4);
                sb.Append(indent).AppendLine("}");
            }
            if (model.Namespace != null) sb.AppendLine("}");

            string hint = (model.Namespace != null ? model.Namespace + "." : string.Empty) + string.Join(".", model.Types.Select(t => t.Split(' ').Last().Split('<')[0])) + "." + model.MemberName + ".g.cs";
            context.AddSource(hint, SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        internal static string StringLiteral(string value)
        {
            foreach (char c in value)
            {
                if (char.IsControl(c)) return SymbolDisplay.FormatLiteral(value, quote: true);
            }
            return "@\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string XmlEscape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        /// <summary>A value-equatable model so the incremental pipeline can cache results.</summary>
        private sealed class GenerationModel : IEquatable<GenerationModel>
        {
            public string Namespace;
            public List<string> Types = new List<string>();
            public string MemberName;
            public string MethodName;
            public string Accessibility;
            public string Regex;
            public int Options;
            public long TimeoutTicks;
            public List<DiagnosticInfo> Diagnostics = new List<DiagnosticInfo>();

            public bool Equals(GenerationModel other) =>
                other != null && Namespace == other.Namespace && MemberName == other.MemberName && MethodName == other.MethodName &&
                Accessibility == other.Accessibility && Regex == other.Regex && Options == other.Options && TimeoutTicks == other.TimeoutTicks &&
                Types.SequenceEqual(other.Types) && Diagnostics.SequenceEqual(other.Diagnostics);

            public override bool Equals(object obj) => Equals(obj as GenerationModel);

            public override int GetHashCode() => (MemberName ?? string.Empty).GetHashCode() ^ (Regex ?? string.Empty).GetHashCode();
        }
    }

    /// <summary>A diagnostic captured as plain values, so it can be cached by the incremental pipeline.</summary>
    internal sealed class DiagnosticInfo : IEquatable<DiagnosticInfo>
    {
        private DiagnosticDescriptor _descriptor;
        private string _path;
        private TextSpan _span;
        private LinePositionSpan _lineSpan;
        private string[] _arguments;

        public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location location, params string[] arguments)
        {
            FileLinePositionSpan lineSpan = location.GetLineSpan();
            return new DiagnosticInfo
            {
                _descriptor = descriptor,
                _path = location.SourceTree?.FilePath ?? string.Empty,
                _span = location.SourceSpan,
                _lineSpan = lineSpan.Span,
                _arguments = arguments
            };
        }

        /// <summary>
        /// Creates the diagnostic at its place in <paramref name="compilation"/>. A location in one of the compilation's
        /// syntax trees is what lets #pragma and .editorconfig change or suppress the diagnostic.
        /// </summary>
        public Diagnostic ToDiagnostic(Compilation compilation)
        {
            Location location;
            if (_path.Length == 0 && _span.IsEmpty)
            {
                location = Location.None;
            }
            else
            {
                SyntaxTree tree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == _path);
                location = tree != null ? Location.Create(tree, _span) : Location.Create(_path, _span, _lineSpan);
            }

            return Diagnostic.Create(_descriptor, location, _arguments.Cast<object>().ToArray());
        }

        public bool Equals(DiagnosticInfo other) =>
            other != null && _descriptor.Id == other._descriptor.Id && _path == other._path && _span == other._span && _arguments.SequenceEqual(other._arguments);

        public override bool Equals(object obj) => Equals(obj as DiagnosticInfo);

        public override int GetHashCode() => _descriptor.Id.GetHashCode() ^ _span.GetHashCode();
    }
}
