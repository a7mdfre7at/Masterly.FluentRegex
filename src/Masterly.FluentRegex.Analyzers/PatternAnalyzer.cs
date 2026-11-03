using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Masterly.FluentRegex.Analyzers
{
    /// <summary>
    /// Evaluates every compile-time-constant fluent pattern in the code and reports invalid regexes,
    /// ReDoS risks, construction errors, <c>Extract&lt;T&gt;</c> group mismatches and
    /// <c>[GeneratedRegex]</c> opportunities.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PatternAnalyzer : DiagnosticAnalyzer
    {
        public const string PatternProperty = "Pattern";
        public const string OptionsProperty = "Options";
        public const string TimeoutProperty = "TimeoutMilliseconds";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            Diagnostics.InvalidRegex,
            Diagnostics.ReDoSHigh,
            Diagnostics.ReDoSMedium,
            Diagnostics.ConstructionThrows,
            Diagnostics.UseGeneratedRegex,
            Diagnostics.ExtractMissingGroup);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(start =>
            {
                if (start.Compilation.GetTypeByMetadataName("Masterly.FluentRegex.Pattern") == null) return;
                bool hasGeneratedRegex = start.Compilation.GetTypeByMetadataName("System.Text.RegularExpressions.GeneratedRegexAttribute") != null;

                start.RegisterOperationAction(AnalyzeChainRoot, OperationKind.Invocation, OperationKind.PropertyReference, OperationKind.Binary);
                start.RegisterOperationAction(AnalyzeExtract, OperationKind.Invocation);
                if (hasGeneratedRegex)
                    start.RegisterSyntaxNodeAction(AnalyzeMember, SyntaxKind.FieldDeclaration, SyntaxKind.PropertyDeclaration);
            });
        }

        // ------------------------------------------------------------------
        // Every fluent chain
        // ------------------------------------------------------------------

        private static void AnalyzeChainRoot(OperationAnalysisContext context)
        {
            IOperation operation = context.Operation;
            if (!ChainEvaluator.IsPatternType(operation.Type) || !IsChainRoot(operation)) return;

            Pattern pattern;
            try
            {
                pattern = new ChainEvaluator(context.Compilation, context.CancellationToken).EvaluatePattern(operation);
            }
            catch (NotEvaluableException)
            {
                return; // dynamic patterns are checked at runtime
            }
            catch (PatternConstructionException ex)
            {
                Location location = (ex.Operation ?? operation).Syntax.GetLocation();
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ConstructionThrows, location, ex.InnerException.GetType().Name, ex.Message));
                return;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                return; // an expression the evaluator cannot handle is treated like a dynamic pattern, never as a crash
            }

            ReportPatternProblems(pattern, operation.Syntax.GetLocation(), context.ReportDiagnostic);
        }

        internal static void ReportPatternProblems(Pattern pattern, Location location, Action<Diagnostic> report)
        {
            string text = pattern.ToString();
            try
            {
                _ = new Regex(text, pattern.Options & ~RegexOptions.Compiled);
            }
            catch (ArgumentException ex)
            {
                report(Diagnostic.Create(Diagnostics.InvalidRegex, location, text, ex.Message));
                return;
            }

            SafetyReport safety;
            try
            {
                safety = pattern.AnalyzeSafety();
            }
            catch (Exception)
            {
                return;
            }

            foreach (SafetyIssue issue in safety.Issues)
            {
                if (issue.Risk == RiskLevel.High)
                    report(Diagnostic.Create(Diagnostics.ReDoSHigh, location, issue.Fragment, issue.Description, issue.Suggestion));
                else if (issue.Risk == RiskLevel.Medium)
                    report(Diagnostic.Create(Diagnostics.ReDoSMedium, location, issue.Fragment, issue.Description, issue.Suggestion));
            }
        }

        /// <summary>
        /// A chain root is the outermost Pattern-valued expression: it is not the receiver of another
        /// fluent call, an argument to one, or an operand of a pattern operator.
        /// </summary>
        private static bool IsChainRoot(IOperation operation)
        {
            IOperation current = operation;
            IOperation parent = current.Parent;
            while (parent is IConversionOperation || parent is IParenthesizedOperation)
            {
                current = parent;
                parent = parent.Parent;
            }

            switch (parent)
            {
                case IPropertyReferenceOperation property when property.Instance == current && IsFluentType(property.Type):
                    return false;
                case IInvocationOperation invocation when invocation.Instance == current && IsFluentType(invocation.Type):
                    return false;
                case IArgumentOperation argument when argument.Parent is IInvocationOperation owner && IsFluentType(owner.Type):
                    return false;
                case IArrayElementReferenceOperation _:
                    return false;
                case IArrayInitializerOperation initializer when initializer.Parent?.Parent is IArgumentOperation:
                    return false;
                case IBinaryOperation binary when binary.OperatorMethod != null && ChainEvaluator.IsPatternType(binary.OperatorMethod.ContainingType):
                    return false;
                default:
                    return true;
            }
        }

        private static bool IsFluentType(ITypeSymbol type) =>
            ChainEvaluator.IsPatternType(type) || ChainEvaluator.IsLibraryType(type, "Repeat");

        // ------------------------------------------------------------------
        // Extract<T> group checks
        // ------------------------------------------------------------------

        private static void AnalyzeExtract(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;
            IMethodSymbol method = invocation.TargetMethod;
            if (!ChainEvaluator.IsPatternType(method.ContainingType) || !method.IsGenericMethod || invocation.Instance == null) return;
            if (method.Name != "Extract" && method.Name != "TryExtract" && method.Name != "ExtractAll") return;
            if (!(method.TypeArguments[0] is INamedTypeSymbol target)) return;

            Pattern pattern;
            try
            {
                pattern = new ChainEvaluator(context.Compilation, context.CancellationToken).EvaluatePattern(invocation.Instance);
            }
            catch (Exception)
            {
                return;
            }

            string[] groups;
            try
            {
                groups = new Regex(pattern.ToString(), pattern.Options & ~RegexOptions.Compiled).GetGroupNames();
            }
            catch (ArgumentException)
            {
                return;
            }
            var available = new HashSet<string>(groups, StringComparer.OrdinalIgnoreCase);
            string availableText = string.Join(", ", groups);

            IMethodSymbol constructor = target.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public)
                .OrderByDescending(c => c.Parameters.Length)
                .FirstOrDefault();
            if (constructor == null) return;

            Location location = invocation.Syntax is InvocationExpressionSyntax syntax && syntax.Expression is MemberAccessExpressionSyntax access
                ? access.Name.GetLocation()
                : invocation.Syntax.GetLocation();

            foreach (IParameterSymbol parameter in constructor.Parameters)
            {
                if (parameter.HasExplicitDefaultValue) continue;
                string wanted = GroupNameOf(parameter) ?? GroupNameOf(target.GetMembers(parameter.Name).OfType<IPropertySymbol>().FirstOrDefault()) ?? parameter.Name;
                if (!available.Contains(wanted))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.ExtractMissingGroup, location, target.Name, parameter.Name, wanted, availableText, MissingGroupConsequence(parameter.Type)));
                }
            }
        }

        /// <summary>What the binder does at run time with a constructor parameter whose group is missing.</summary>
        private static string MissingGroupConsequence(ITypeSymbol type) =>
            type.IsReferenceType || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                ? "it is always null"
                : "extracting throws InvalidOperationException";

        private static string GroupNameOf(ISymbol symbol)
        {
            AttributeData attribute = symbol?.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.Name == "RegexGroupAttribute" && a.AttributeClass.ContainingNamespace?.ToDisplayString() == ChainEvaluator.LibraryNamespace);
            return attribute?.ConstructorArguments.Length == 1 ? attribute.ConstructorArguments[0].Value as string : null;
        }

        // ------------------------------------------------------------------
        // [GeneratedRegex] opportunities
        // ------------------------------------------------------------------

        private static void AnalyzeMember(SyntaxNodeAnalysisContext context)
        {
            SyntaxTokenList modifiers;
            TypeSyntax type;
            IEnumerable<(SyntaxToken Identifier, SyntaxNode Declaration)> declarations;

            switch (context.Node)
            {
                case FieldDeclarationSyntax field:
                    modifiers = field.Modifiers;
                    type = field.Declaration.Type;
                    declarations = field.Declaration.Variables.Select(v => (v.Identifier, (SyntaxNode)v));
                    break;
                case PropertyDeclarationSyntax property:
                    modifiers = property.Modifiers;
                    type = property.Type;
                    declarations = new[] { (property.Identifier, (SyntaxNode)property) };
                    break;
                default:
                    return;
            }

            if (!modifiers.Any(SyntaxKind.StaticKeyword)) return;
            if (!(context.Node.Parent is TypeDeclarationSyntax containing)) return;

            // The code fix adds a partial method, which compiles only when every enclosing type is partial.
            for (SyntaxNode node = containing; node is TypeDeclarationSyntax typeDeclaration; node = node.Parent)
            {
                if (!typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)) return;
            }
            if (!ChainEvaluator.IsPatternType(context.SemanticModel.GetTypeInfo(type, context.CancellationToken).Type)) return;

            INamedTypeSymbol containingType = context.SemanticModel.GetDeclaredSymbol(containing, context.CancellationToken);
            foreach ((SyntaxToken identifier, SyntaxNode declaration) in declarations)
            {
                // [GeneratePattern] already generates a cached regex for the member.
                ISymbol member = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
                if (member != null && HasAttribute(member, "GeneratePatternAttribute", ChainEvaluator.LibraryNamespace)) continue;

                ExpressionSyntax expression = ChainEvaluator.BodyExpression(declaration);
                if (expression == null) continue;
                IOperation operation = context.SemanticModel.GetOperation(expression, context.CancellationToken);
                if (operation == null) continue;

                Pattern pattern;
                try
                {
                    pattern = new ChainEvaluator(context.Compilation, context.CancellationToken).EvaluatePattern(operation);
                    _ = new Regex(pattern.ToString(), pattern.Options & ~RegexOptions.Compiled);
                }
                catch (Exception)
                {
                    continue;
                }

                // The code fix was already applied: the type has a [GeneratedRegex] method for this exact regex.
                if (containingType != null && HasGeneratedRegexFor(containingType, pattern)) continue;

                ImmutableDictionary<string, string> properties = ImmutableDictionary<string, string>.Empty
                    .Add(PatternProperty, pattern.ToString())
                    .Add(OptionsProperty, ((int)pattern.Options).ToString(CultureInfo.InvariantCulture))
                    .Add(TimeoutProperty, pattern.Timeout == Regex.InfiniteMatchTimeout
                        ? string.Empty
                        : ((long)pattern.Timeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture));

                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.UseGeneratedRegex, identifier.GetLocation(), properties, identifier.Text));
            }
        }

        private static bool HasAttribute(ISymbol symbol, string name, string containingNamespace) =>
            symbol.GetAttributes().Any(a => a.AttributeClass?.Name == name && a.AttributeClass.ContainingNamespace?.ToDisplayString() == containingNamespace);

        /// <summary>
        /// True when <paramref name="type"/> has a <c>[GeneratedRegex]</c> method with the same regex text and the same
        /// options (ignoring Compiled, which the .NET generator implies), such as the one the FRX007 code fix adds.
        /// </summary>
        private static bool HasGeneratedRegexFor(INamedTypeSymbol type, Pattern pattern)
        {
            string text = pattern.ToString();
            int options = (int)(pattern.Options & ~RegexOptions.Compiled);
            foreach (IMethodSymbol method in type.GetMembers().OfType<IMethodSymbol>())
            {
                foreach (AttributeData attribute in method.GetAttributes())
                {
                    if (attribute.AttributeClass?.Name != "GeneratedRegexAttribute"
                        || attribute.AttributeClass.ContainingNamespace?.ToDisplayString() != "System.Text.RegularExpressions"
                        || attribute.ConstructorArguments.Length == 0
                        || !(attribute.ConstructorArguments[0].Value is string generatedText)
                        || generatedText != text)
                    {
                        continue;
                    }

                    int generatedOptions = attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[1].Value is int value
                        ? value & ~(int)RegexOptions.Compiled
                        : 0;
                    if (generatedOptions == options) return true;
                }
            }

            return false;
        }
    }
}
