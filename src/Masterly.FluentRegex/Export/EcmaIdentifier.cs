#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Masterly.FluentRegex.Export;

/// <summary>Validates JavaScript identifiers for group names and exported constants.</summary>
internal static class EcmaIdentifier
{
    private static readonly HashSet<string> ReservedWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "await", "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete",
        "do", "else", "enum", "export", "extends", "false", "finally", "for", "function", "if", "implements",
        "import", "in", "instanceof", "interface", "let", "new", "null", "package", "private", "protected",
        "public", "return", "static", "super", "switch", "this", "throw", "true", "try", "typeof", "var",
        "void", "while", "with", "yield"
    };

    /// <summary>Tests whether <paramref name="name"/> is an identifier (reserved words allowed, as in group names).</summary>
    public static bool IsValid(string? name)
    {
        if (name is null || name.Length == 0 || !IsStart(name[0]))
        {
            return false;
        }

        for (int i = 1; i < name.Length; i++)
        {
            if (!IsPart(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Tests whether <paramref name="name"/> can name a <c>const</c> binding.</summary>
    public static bool IsBindingName(string? name) => name is not null && IsValid(name) && !ReservedWords.Contains(name);

    private static bool IsStart(char c) => c == '$' || c == '_' || char.IsLetter(c) ||
                                           CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.LetterNumber;

    private static bool IsPart(char c)
    {
        if (IsStart(c) || char.IsDigit(c))
        {
            return true;
        }

        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark ||
               category == UnicodeCategory.ConnectorPunctuation;
    }
}
