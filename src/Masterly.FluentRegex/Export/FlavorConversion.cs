#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex;

/// <summary>
/// A pattern translated to another regex dialect by <see cref="Pattern.ToFlavor"/>.
/// </summary>
/// <remarks>
/// <see cref="IsExact"/> is true when no warning was reported, meaning the translation accepts the
/// same strings as the .NET pattern for text in the Basic Multilingual Plane. Positive Unicode classes
/// such as <c>\p{L}</c> can additionally match supplementary letters (outside U+0000-U+FFFF) in
/// JavaScript, which .NET sees as two surrogate code units; constructs that can match a lone surrogate
/// in .NET, such as <c>.</c> or <c>[^a]</c>, always produce a warning.
/// </remarks>
public sealed class FlavorConversion
{
    private const char Slash = '/';
    private const string EmptyJavaScriptPattern = "(?:)";
    private const int FirstPrintable = 0x20;

    internal FlavorConversion(RegexFlavor flavor, string pattern, string flags, IReadOnlyList<string> warnings)
    {
        Flavor = flavor;
        Pattern = pattern;
        Flags = flags;
        Warnings = warnings;
    }

    /// <summary>Gets the target dialect.</summary>
    public RegexFlavor Flavor { get; }

    /// <summary>Gets the translated pattern source, without delimiters or flags.</summary>
    public string Pattern { get; }

    /// <summary>Gets the flags to compile the pattern with, such as <c>iu</c>. Always empty for JSON Schema.</summary>
    public string Flags { get; }

    /// <summary>Gets descriptions of every behavior that differs from .NET. Empty when the translation is exact.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Gets a value indicating whether the translation reported no semantic differences.</summary>
    public bool IsExact => Warnings.Count == 0;

    /// <summary>
    /// Returns the translation as source code: a regex literal such as <c>/^\p{Nd}+$/u</c> for
    /// JavaScript, or a quoted JSON string for JSON Schema.
    /// </summary>
    /// <returns>The literal.</returns>
    public string ToLiteral()
    {
        if (Flavor == RegexFlavor.JsonSchema)
        {
            return JsonString(Pattern);
        }

        string body = Pattern.Length == 0 ? EmptyJavaScriptPattern : Pattern;
        return $"{Slash}{body}{Slash}{Flags}";
    }

    /// <summary>Returns <see cref="ToLiteral"/>.</summary>
    public override string ToString() => ToLiteral();

    private static string JsonString(string value)
    {
        StringBuilder sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            if (c == '"' || c == '\\')
            {
                sb.Append('\\').Append(c);
            }
            else if (c < FirstPrintable)
            {
                sb.Append($"\\u{((int)c).ToString("x4", CultureInfo.InvariantCulture)}");
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.Append('"').ToString();
    }
}
