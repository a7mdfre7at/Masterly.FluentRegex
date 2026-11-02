#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Masterly.FluentRegex.Templates;

/// <summary>Small helpers for writing regex fragments.</summary>
internal static class RegexText
{
    /// <summary>Escapes literal text for use outside a character class.</summary>
    public static string Escape(string text)
    {
        StringBuilder sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            LiteralToken.AppendEscaped(sb, c, RenderContext.Normal);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds a case-insensitive alternation of the non-empty words, longest first so a word never
    /// loses to one of its prefixes.
    /// </summary>
    public static string WordAlternation(IEnumerable<string> words)
    {
        IEnumerable<string> ordered = words
            .Where(word => !string.IsNullOrEmpty(word))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(word => word.Length)
            .ThenBy(word => word, StringComparer.Ordinal)
            .Select(Escape);
        return $"(?i:{string.Join("|", ordered)})";
    }
}
