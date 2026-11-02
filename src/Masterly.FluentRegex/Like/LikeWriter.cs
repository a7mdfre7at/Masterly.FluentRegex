#nullable enable
using System.Collections.Generic;
using System.Text;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Like;

/// <summary>Writes LIKE parts as the pattern text a database sees, choosing an escape character only when needed.</summary>
internal static class LikeWriter
{
    public const char Escape = '\\';
    private const char Percent = '%';
    private const char Underscore = '_';
    private const char OpenSet = '[';
    private const char CloseSet = ']';
    private const char Negation = '^';
    private const char RangeDash = '-';
    private const int MaxSetRanges = 100;
    private const char LastAscii = '\u007F';
    private const char FirstSurrogate = '\uD800';
    private const char LastSurrogate = '\uDFFF';

    /// <summary>Writes the parts; <paramref name="escapeCharacter"/> is set when an <c>ESCAPE</c> clause is required.</summary>
    public static string Write(IReadOnlyList<LikePart> parts, LikeDialect dialect, bool allowRanges, out char? escapeCharacter)
    {
        bool defaultBackslash = HasDefaultBackslashEscape(dialect);
        bool escaping = NeedsEscaping(parts, dialect) || (defaultBackslash && ContainsBackslash(parts));
        StringBuilder sb = new StringBuilder();
        foreach (LikePart part in parts)
        {
            AppendPart(sb, part, dialect, escaping, allowRanges);
        }

        escapeCharacter = escaping ? Escape : (char?)null;
        return sb.ToString();
    }

    /// <summary>Whether the dialect treats a backslash as the escape character even without an <c>ESCAPE</c> clause.</summary>
    public static bool HasDefaultBackslashEscape(LikeDialect dialect) =>
        dialect == LikeDialect.MySql || dialect == LikeDialect.PostgreSql;

    /// <summary>
    /// Gets a value indicating whether a set can be written for the dialect: only SQL Server has sets,
    /// members must not need escaping, ranges need a code-point collation (otherwise every member is
    /// listed), and lone surrogates and very large sets are excluded.
    /// </summary>
    public static bool CanWriteSet(CharSet set, LikeDialect dialect, bool allowRanges, out bool nonAscii)
    {
        nonAscii = false;
        if (dialect != LikeDialect.SqlServer)
        {
            return false;
        }

        CharSet members = ContainsSurrogate(set) ? set.Complement() : set;
        if (!allowRanges && members.Count > MaxSetRanges)
        {
            return false;
        }

        int ranges = 0;
        foreach (KeyValuePair<char, char> range in members.Ranges())
        {
            ranges++;
            if (ranges > MaxSetRanges || ContainsSurrogate(range) || ContainsSetSyntax(range))
            {
                return false;
            }

            nonAscii |= range.Value > LastAscii;
        }

        return ranges > 0;
    }

    private static bool ContainsSetSyntax(KeyValuePair<char, char> range)
    {
        for (int c = range.Key; c <= range.Value; c++)
        {
            if (IsSetSyntax((char)c))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NeedsEscaping(IReadOnlyList<LikePart> parts, LikeDialect dialect)
    {
        foreach (LikePart part in parts)
        {
            if (part.Kind == LikePartKind.Text && ContainsSpecial(part.Text, dialect))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSpecial(string text, LikeDialect dialect)
    {
        foreach (char c in text)
        {
            if (c == Percent || c == Underscore || (c == OpenSet && dialect == LikeDialect.SqlServer))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsBackslash(IReadOnlyList<LikePart> parts)
    {
        foreach (LikePart part in parts)
        {
            if (part.Kind == LikePartKind.Text && part.Text.IndexOf(Escape) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void AppendPart(StringBuilder sb, LikePart part, LikeDialect dialect, bool escaping, bool allowRanges)
    {
        switch (part.Kind)
        {
            case LikePartKind.AnyChar:
                sb.Append(Underscore);
                break;
            case LikePartKind.AnyString:
                sb.Append(Percent);
                break;
            case LikePartKind.Set:
                AppendSet(sb, part.Set ?? CharSet.Empty, allowRanges);
                break;
            default:
                AppendText(sb, part.Text, dialect, escaping);
                break;
        }
    }

    private static void AppendText(StringBuilder sb, string text, LikeDialect dialect, bool escaping)
    {
        foreach (char c in text)
        {
            bool special = c == Percent || c == Underscore || c == Escape || (c == OpenSet && dialect == LikeDialect.SqlServer);
            if (escaping && special)
            {
                sb.Append(Escape);
            }

            sb.Append(c);
        }
    }

    private static void AppendSet(StringBuilder sb, CharSet set, bool allowRanges)
    {
        bool negated = ContainsSurrogate(set);
        CharSet members = negated ? set.Complement() : set;
        sb.Append(OpenSet);
        if (negated)
        {
            sb.Append(Negation);
        }

        foreach (KeyValuePair<char, char> range in members.Ranges())
        {
            AppendRange(sb, range, allowRanges);
        }

        sb.Append(CloseSet);
    }

    private static void AppendRange(StringBuilder sb, KeyValuePair<char, char> range, bool allowRanges)
    {
        if (range.Key == range.Value || allowRanges)
        {
            sb.Append(range.Key);
            if (range.Value != range.Key)
            {
                sb.Append(RangeDash).Append(range.Value);
            }

            return;
        }

        for (int c = range.Key; c <= range.Value; c++)
        {
            sb.Append((char)c); // ranges follow the collation's order, so list the members instead
        }
    }

    private static bool IsSetSyntax(char c) => c == OpenSet || c == CloseSet || c == Negation || c == RangeDash;

    private static bool ContainsSurrogate(KeyValuePair<char, char> range) =>
        range.Value >= FirstSurrogate && range.Key <= LastSurrogate;

    private static bool ContainsSurrogate(CharSet set)
    {
        for (int c = FirstSurrogate; c <= LastSurrogate; c++)
        {
            if (set.Contains((char)c))
            {
                return true;
            }
        }

        return false;
    }
}
