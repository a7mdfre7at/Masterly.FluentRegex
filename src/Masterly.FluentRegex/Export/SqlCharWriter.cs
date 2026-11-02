#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Export;

/// <summary>How a SQL regex engine spells a character by its code point.</summary>
internal enum HexEscapeStyle
{
    /// <summary><c>\uXXXX</c>, the PostgreSQL ARE form (exactly four hex digits).</summary>
    Unicode4,

    /// <summary><c>\x{XXXX}</c>, accepted by ICU, PCRE2 and RE2.</summary>
    Braced
}

/// <summary>
/// Writes characters and exact character sets for the SQL regex engines. Only printable ASCII is
/// written as itself; everything else becomes a code-point escape, so the output is pure ASCII and
/// safe in any string literal. Every syntax character is backslash-escaped, which all four engines
/// read as the literal character, inside and outside bracket expressions.
/// </summary>
internal sealed class SqlCharWriter
{
    private const string SyntaxCharacters = @"\^$.|?*+()[]{}";
    private const string ClassSyntaxCharacters = @"\]-[^&";
    private const char FirstPrintable = ' ';
    private const char LastPrintable = '~';
    private const int BmpSize = 0x10000;
    private const char FirstSurrogate = '\uD800';
    private const char LastSurrogate = '\uDFFF';
    private const string HexFormat = "X4";
    private const string LongHexFormat = "X8";

    // Aggregates first, then every general category except Cs (surrogates are never written) and Cn
    // (unassigned code points are the category most likely to differ between the engine's and .NET's
    // Unicode versions); C is skipped because it contains both.
    private static readonly string[] DecomposableCategories =
    {
        "L", "M", "N", "P", "S", "Z",
        "Lu", "Ll", "Lt", "Lm", "Lo", "Mn", "Mc", "Me", "Nd", "Nl", "No", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po",
        "Sm", "Sc", "Sk", "So", "Zs", "Zl", "Zp", "Cc", "Cf", "Co"
    };

    private readonly HexEscapeStyle _style;
    private readonly bool _useCategories;

    /// <summary>
    /// Creates a writer.
    /// </summary>
    /// <param name="style">The code-point escape syntax.</param>
    /// <param name="useCategories">
    /// Whether large sets may be compacted with <c>\p{..}</c>: every general category the set fully
    /// contains is written by name and only the remainder as explicit ranges, which keeps sets such as
    /// <c>[^\W\d]</c> within engine length limits.
    /// </param>
    public SqlCharWriter(HexEscapeStyle style, bool useCategories)
    {
        _style = style;
        _useCategories = useCategories;
    }

    /// <summary>Writes a character by code point.</summary>
    public string Hex(char c)
    {
        string digits = ((int)c).ToString(HexFormat, CultureInfo.InvariantCulture);
        return _style == HexEscapeStyle.Unicode4 ? $"\\u{digits}" : $"\\x{{{digits}}}";
    }

    /// <summary>
    /// Writes a supplementary code point (above U+FFFF): <c>\x{1F600}</c>, or <c>\U0001F600</c> in the
    /// PostgreSQL form, which takes exactly eight hex digits after <c>\U</c>.
    /// </summary>
    public string CodePoint(int codePoint)
    {
        if (_style == HexEscapeStyle.Unicode4)
        {
            return $"\\U{codePoint.ToString(LongHexFormat, CultureInfo.InvariantCulture)}";
        }

        return $"\\x{{{codePoint.ToString(HexFormat, CultureInfo.InvariantCulture)}}}";
    }

    /// <summary>Escapes a character for use outside a bracket expression.</summary>
    public string Literal(char c)
    {
        if (SyntaxCharacters.IndexOf(c) >= 0)
        {
            return $"\\{c}";
        }

        return Plain(c);
    }

    /// <summary>Escapes a character for use inside a bracket expression.</summary>
    public string ClassMember(char c)
    {
        if (ClassSyntaxCharacters.IndexOf(c) >= 0)
        {
            return $"\\{c}";
        }

        return Plain(c);
    }

    /// <summary>Writes a range inside a bracket expression, such as <c>a-z</c>.</summary>
    public string ClassRange(char low, char high)
    {
        if (low == high)
        {
            return ClassMember(low);
        }

        string separator = high == low + 1 ? string.Empty : "-";
        return $"{ClassMember(low)}{separator}{ClassMember(high)}";
    }

    /// <summary>
    /// Writes an exact set of code units as one atom, or null when the set has no members outside
    /// the surrogate range (such a set cannot be written in a code-point engine). A set that contains
    /// every surrogate is written as the negation of its complement, like <c>[^...]</c> in .NET.
    /// </summary>
    public string? Set(CharSet set)
    {
        if (ContainsAllSurrogates(set))
        {
            string complement = Members(set.Complement());
            return complement.Length == 0 ? null : $"[^{complement}]";
        }

        if (SingleMember(set, out char single))
        {
            return IsSurrogate(single) ? null : Literal(single);
        }

        string members = Members(set);
        return members.Length == 0 ? null : $"[{members}]";
    }

    /// <summary>Writes the members of a set: whole categories by name when allowed, the rest as ranges.</summary>
    private string Members(CharSet set)
    {
        if (!_useCategories)
        {
            return Ranges(set);
        }

        StringBuilder sb = new StringBuilder();
        CharSet remaining = set;
        foreach (string category in DecomposableCategories)
        {
            CharSet members = CharSet.ForSource($"\\p{{{category}}}", RegexOptions.None);
            if (!members.IsEmpty && members.IsSubsetOf(remaining))
            {
                sb.Append($"\\p{{{category}}}");
                remaining = remaining.Except(members);
            }
        }

        return sb.Append(Ranges(remaining)).ToString();
    }

    /// <summary>Gets a value indicating whether the range from <paramref name="low"/> to <paramref name="high"/> includes a surrogate.</summary>
    public static bool OverlapsSurrogates(char low, char high) => low <= LastSurrogate && high >= FirstSurrogate;

    /// <summary>Gets a value indicating whether <paramref name="set"/> contains every code unit.</summary>
    public static bool IsEverything(CharSet set) => set.Count == BmpSize;

    private static bool SingleMember(CharSet set, out char single)
    {
        single = char.MinValue;
        if (set.Count != 1)
        {
            return false;
        }

        foreach (KeyValuePair<char, char> range in set.Ranges())
        {
            single = range.Key;
        }

        return true;
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

    /// <summary>Writes the ranges of members, always excluding surrogates.</summary>
    private string Ranges(CharSet set)
    {
        StringBuilder sb = new StringBuilder();
        int start = -1;
        for (int c = 0; c <= BmpSize; c++)
        {
            bool inRange = c < BmpSize && set.Contains((char)c) && !IsSurrogate(c);
            if (inRange && start < 0)
            {
                start = c;
            }
            else if (!inRange && start >= 0)
            {
                sb.Append(ClassRange((char)start, (char)(c - 1)));
                start = -1;
            }
        }

        return sb.ToString();
    }

    private static bool IsSurrogate(int c) => c >= FirstSurrogate && c <= LastSurrogate;

    private string Plain(char c)
    {
        if (c >= FirstPrintable && c <= LastPrintable)
        {
            return c.ToString();
        }

        return Hex(c);
    }
}
