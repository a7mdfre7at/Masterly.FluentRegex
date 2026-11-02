#nullable enable
using System.Collections.Generic;
using System.Text;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Like;

/// <summary>
/// Reads a LIKE pattern back into parts, so the proof works from the text that will actually be sent
/// to the database rather than from the parts it was written from.
/// </summary>
internal static class LikeExpressionParser
{
    private const char Percent = '%';
    private const char Underscore = '_';
    private const char OpenSet = '[';
    private const char CloseSet = ']';
    private const char Negation = '^';
    private const char RangeDash = '-';

    /// <summary>Parses <paramref name="expression"/> as the dialect reads it.</summary>
    public static IReadOnlyList<LikePart> Parse(string expression, char? escapeCharacter, LikeDialect dialect)
    {
        char? escape = escapeCharacter ?? (LikeWriter.HasDefaultBackslashEscape(dialect) ? LikeWriter.Escape : (char?)null);
        List<LikePart> parts = new List<LikePart>();
        StringBuilder text = new StringBuilder();
        int i = 0;
        while (i < expression.Length)
        {
            char c = expression[i];
            if (escape != null && c == escape.Value && i + 1 < expression.Length)
            {
                text.Append(expression[i + 1]);
                i += 2;
            }
            else if (c == Percent || c == Underscore)
            {
                FlushText(parts, text);
                parts.Add(c == Percent ? LikePart.AnyString : LikePart.AnyChar);
                i++;
            }
            else if (c == OpenSet && dialect == LikeDialect.SqlServer)
            {
                FlushText(parts, text);
                parts.Add(LikePart.OfSet(ReadSet(expression, ref i)));
            }
            else
            {
                text.Append(c);
                i++;
            }
        }

        FlushText(parts, text);
        return parts;
    }

    private static void FlushText(List<LikePart> parts, StringBuilder text)
    {
        if (text.Length > 0)
        {
            parts.Add(LikePart.OfText(text.ToString()));
            text.Clear();
        }
    }

    /// <summary>Reads <c>[...]</c> or <c>[^...]</c> starting at the opening bracket; the first <c>]</c> closes it.</summary>
    private static CharSet ReadSet(string expression, ref int i)
    {
        i++; // [
        bool negated = i < expression.Length && expression[i] == Negation;
        if (negated)
        {
            i++;
        }

        CharSet members = CharSet.Empty;
        while (i < expression.Length && expression[i] != CloseSet)
        {
            char low = expression[i];
            bool isRange = i + 2 < expression.Length && expression[i + 1] == RangeDash && expression[i + 2] != CloseSet;
            char high = isRange ? expression[i + 2] : low;
            members = members.Union(CharSet.Range(low, high));
            i += isRange ? 3 : 1;
        }

        i++; // ]
        return negated ? members.Complement() : members;
    }
}
