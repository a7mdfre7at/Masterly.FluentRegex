#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Like;

/// <summary>
/// Writes a LIKE pattern as a .NET regex with the same full-match language: <c>_</c> becomes
/// <c>[\s\S]</c>, <c>%</c> becomes <c>[\s\S]*</c>, sets become classes and text is escaped.
/// </summary>
internal static class LikeRegex
{
    private const string AnyCharacter = @"[\s\S]";
    private const string AnyString = @"[\s\S]*";
    private const string HexFormat = "X4";

    /// <summary>Converts the LIKE text a database would receive.</summary>
    public static string FromExpression(string expression, char? escapeCharacter, LikeDialect dialect) =>
        FromParts(LikeExpressionParser.Parse(expression, escapeCharacter, dialect));

    /// <summary>Converts parts.</summary>
    public static string FromParts(IReadOnlyList<LikePart> parts)
    {
        StringBuilder sb = new StringBuilder();
        foreach (LikePart part in parts)
        {
            AppendPart(sb, part);
        }

        return sb.ToString();
    }

    private static void AppendPart(StringBuilder sb, LikePart part)
    {
        switch (part.Kind)
        {
            case LikePartKind.AnyChar:
                sb.Append(AnyCharacter);
                break;
            case LikePartKind.AnyString:
                sb.Append(AnyString);
                break;
            case LikePartKind.Set:
                AppendClass(sb, part.Set ?? CharSet.Empty);
                break;
            default:
                AppendText(sb, part.Text);
                break;
        }
    }

    private static void AppendText(StringBuilder sb, string text)
    {
        foreach (char c in text)
        {
            sb.Append(Escaped(c));
        }
    }

    private static void AppendClass(StringBuilder sb, CharSet set)
    {
        if (set.IsEmpty)
        {
            sb.Append(@"[^\s\S]");
            return;
        }

        sb.Append('[');
        foreach (KeyValuePair<char, char> range in set.Ranges())
        {
            sb.Append(Escaped(range.Key));
            if (range.Value != range.Key)
            {
                sb.Append('-').Append(Escaped(range.Value));
            }
        }

        sb.Append(']');
    }

    /// <summary>Letters and digits are written as themselves, everything else by code point so no character is ever syntax.</summary>
    private static string Escaped(char c)
    {
        bool plain = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
        return plain ? c.ToString() : $"\\u{((int)c).ToString(HexFormat, CultureInfo.InvariantCulture)}";
    }
}
