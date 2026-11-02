#nullable enable

using System;

namespace Masterly.FluentRegex.Templates;

/// <summary>Builds the argument errors raised while compiling a template.</summary>
internal static class TemplateErrors
{
    /// <summary>The parameter name reported for every template compilation error.</summary>
    public const string ParameterName = "template";

    /// <summary>Creates an error for a template problem.</summary>
    public static ArgumentException Invalid(string message) => new ArgumentException(message, ParameterName);

    /// <summary>Creates an error for a placeholder format the member type does not support.</summary>
    public static ArgumentException UnsupportedFormat(Type type, string? format, string supportedFormats) =>
        Invalid($"The format '{format}' is not supported for {type.Name} placeholders. Use {supportedFormats}.");
}
