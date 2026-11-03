#nullable enable
using System.Globalization;
using System.Text;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Export;

/// <summary>
/// Writes characters and character sets in ECMAScript regex syntax that is valid with the
/// <c>u</c> flag, where only syntax characters may be escaped with a backslash.
/// </summary>
internal static class EcmaEscaper
{
    private const string SyntaxCharacters = @"^$\.*+?()[]{}|";
    private const string ClassSyntaxCharacters = @"\]-[^";
    private const char Slash = '/';
    private const char FirstPrintable = ' ';
    private const char LastPrintable = '~';
    private const int ByteLimit = 0xFF;
    private const int BmpSize = 0x10000;
    private const char FirstSurrogate = '\uD800';
    private const char LastSurrogate = '\uDFFF';
    private const string MatchAnything = @"[\s\S]";
    private const string MatchNothing = "[]";

    /// <summary>Escapes a character for use outside a character class.</summary>
    public static string Literal(char c, bool escapeSlash)
    {
        if (SyntaxCharacters.IndexOf(c) >= 0 || (escapeSlash && c == Slash))
        {
            return $"\\{c}";
        }

        return Plain(c);
    }

    /// <summary>Escapes a character for use inside a character class.</summary>
    public static string ClassMember(char c, bool escapeSlash)
    {
        if (ClassSyntaxCharacters.IndexOf(c) >= 0 || (escapeSlash && c == Slash))
        {
            return $"\\{c}";
        }

        return Plain(c);
    }

    /// <summary>Writes a range inside a character class, such as <c>a-z</c>.</summary>
    public static string ClassRange(char low, char high, bool escapeSlash)
    {
        if (low == high)
        {
            return ClassMember(low, escapeSlash);
        }

        string separator = high == low + 1 ? string.Empty : "-";
        return $"{ClassMember(low, escapeSlash)}{separator}{ClassMember(high, escapeSlash)}";
    }

    /// <summary>
    /// Writes an exact set of UTF-16 code units as a single atom. A set that contains every
    /// surrogate is written as the negation of its complement, so it also covers whole
    /// supplementary code points the way <c>[^...]</c> does.
    /// </summary>
    public static string Set(CharSet set, bool escapeSlash)
    {
        if (set.IsEmpty)
        {
            return MatchNothing;
        }

        if (ContainsAllSurrogates(set))
        {
            string complement = Ranges(set, member: false, escapeSlash);
            return complement.Length == 0 ? MatchAnything : $"[^{complement}]";
        }

        if (set.Count == 1)
        {
            return Literal(SingleMember(set), escapeSlash);
        }

        return $"[{Ranges(set, member: true, escapeSlash)}]";
    }

    /// <summary>Gets a value indicating whether the set can match half of a surrogate pair.</summary>
    public static bool ContainsSurrogate(CharSet set)
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

    private static bool ContainsAllSurrogates(CharSet set)
    {
        for (int c = FirstSurrogate; c <= LastSurrogate; c++)
        {
            if (!set.Contains((char)c))
            {
                return false;
            }
        }

        return true;
    }

    private static char SingleMember(CharSet set)
    {
        for (int c = 0; c < BmpSize; c++)
        {
            if (set.Contains((char)c))
            {
                return (char)c;
            }
        }

        return char.MinValue;
    }

    /// <summary>Writes the ranges of members (or of non-members, excluding surrogates).</summary>
    private static string Ranges(CharSet set, bool member, bool escapeSlash)
    {
        StringBuilder sb = new StringBuilder();
        int start = -1;
        for (int c = 0; c <= BmpSize; c++)
        {
            bool inRange = c < BmpSize && set.Contains((char)c) == member && (member || !IsSurrogate(c));
            if (inRange && start < 0)
            {
                start = c;
            }
            else if (!inRange && start >= 0)
            {
                sb.Append(ClassRange((char)start, (char)(c - 1), escapeSlash));
                start = -1;
            }
        }

        return sb.ToString();
    }

    private static bool IsSurrogate(int c) => c >= FirstSurrogate && c <= LastSurrogate;

    private static string Plain(char c)
    {
        switch (c)
        {
            case '\t':
                return @"\t";
            case '\n':
                return @"\n";
            case '\v':
                return @"\v";
            case '\f':
                return @"\f";
            case '\r':
                return @"\r";
        }

        if (c >= FirstPrintable && c <= LastPrintable)
        {
            return c.ToString();
        }

        int code = c;
        return code <= ByteLimit
            ? $"\\x{code.ToString("X2", CultureInfo.InvariantCulture)}"
            : $"\\u{code.ToString("X4", CultureInfo.InvariantCulture)}";
    }
}
