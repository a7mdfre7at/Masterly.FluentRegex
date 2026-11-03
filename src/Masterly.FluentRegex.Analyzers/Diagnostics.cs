using Microsoft.CodeAnalysis;

namespace Masterly.FluentRegex.Analyzers
{
    internal static class Diagnostics
    {
        private const string Category = "Masterly.FluentRegex";
        private const string HelpLink = "https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/compile-time-checks";

        public static readonly DiagnosticDescriptor CannotEvaluate = new DiagnosticDescriptor(
            "FRX001",
            "Pattern cannot be evaluated at compile time",
            "The pattern for '{0}' cannot be evaluated at compile time: {1}",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "[GeneratePattern] needs a fluent chain built only from constants, other static patterns and CommonPatterns.",
            helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor MustBePartial = new DiagnosticDescriptor(
            "FRX002",
            "Containing type must be partial",
            "Type '{0}' must be declared partial to use [GeneratePattern]",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true, helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor InvalidRegex = new DiagnosticDescriptor(
            "FRX003",
            "Pattern produces an invalid regular expression",
            "The pattern produces an invalid regular expression '{0}': {1}",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true, helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor ReDoSHigh = new DiagnosticDescriptor(
            "FRX004",
            "Pattern is vulnerable to catastrophic backtracking",
            "ReDoS risk in '{0}': {1} Fix: {2}",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "Exponential backtracking lets a short hostile input hang the process.",
            helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor ReDoSMedium = new DiagnosticDescriptor(
            "FRX005",
            "Pattern may backtrack excessively",
            "Polynomial backtracking in '{0}': {1} Fix: {2}",
            Category, DiagnosticSeverity.Info, isEnabledByDefault: true, helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor ConstructionThrows = new DiagnosticDescriptor(
            "FRX006",
            "Pattern construction throws",
            "This pattern throws {0} when it is built: {1}",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true, helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor UseGeneratedRegex = new DiagnosticDescriptor(
            "FRX007",
            "Pattern can use [GeneratedRegex]",
            "'{0}' is a compile-time constant pattern and can be backed by a [GeneratedRegex] method",
            Category, DiagnosticSeverity.Info, isEnabledByDefault: true,
            description: "The .NET regex source generator emits matching code at build time, which is faster to start and to run.",
            helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor ExtractMissingGroup = new DiagnosticDescriptor(
            "FRX008",
            "Extract target member has no matching group",
            "'{0}.{1}' has no matching group '{2}' in the pattern, so {4}. Available groups: {3}.",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor MustBeStatic = new DiagnosticDescriptor(
            "FRX009",
            "[GeneratePattern] member must be static",
            "'{0}' must be static to use [GeneratePattern]",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true, helpLinkUri: HelpLink);
    }
}
