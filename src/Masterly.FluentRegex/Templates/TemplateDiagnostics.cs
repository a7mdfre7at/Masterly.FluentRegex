#nullable enable

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Templates;

/// <summary>Explains why an input does not match a template, using <see cref="Pattern.Trace(string)"/>.</summary>
internal static class TemplateDiagnostics
{
    private const string StartAnchor = @"\A";

    /// <summary>Builds the message for a failed parse.</summary>
    public static string Explain(string template, CompiledTemplate compiled, Pattern anchored, string input)
    {
        string part = FailingPart(compiled, anchored.Timeout, input);
        TraceFailure? failure = anchored.Trace(input).Failure;
        if (failure == null)
        {
            return $"The input does not match the template \"{template}\": it fails at {part}.";
        }

        return $"The input does not match the template \"{template}\": at position {failure.Position}, " +
               $"while reading {part}, expected {failure.Description} (regex {failure.Construct}) " +
               $"but found {failure.Found}.";
    }

    // Matches ever-longer prefixes of the template; the first part that cannot be added is the culprit.
    private static string FailingPart(CompiledTemplate compiled, TimeSpan timeout, string input)
    {
        StringBuilder prefix = new StringBuilder(StartAnchor);
        foreach (TemplatePart part in compiled.Parts)
        {
            prefix.Append(part.Regex);
            if (!new Regex(prefix.ToString(), compiled.Options, timeout).IsMatch(input))
            {
                return part.Describe();
            }
        }

        return "the end of the template (the input has extra text after it)";
    }
}
