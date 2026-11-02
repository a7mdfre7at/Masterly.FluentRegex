#nullable enable
using System.Globalization;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Export;

/// <summary>
/// MySQL 8.0+ regular expressions, which are ICU regular expressions
/// (https://dev.mysql.com/doc/refman/8.4/en/regexp.html, https://unicode-org.github.io/icu/userguide/strings/regexp.html).
/// Verified against a mysql:8.4 container. Facts this translation relies on: matching is
/// case-insensitive whenever the collation is (under <c>utf8mb4_0900_ai_ci</c> "E" matches "e", the
/// Kelvin sign matches "k" and "ß" matches "ss"), so the pattern starts with <c>(?-i)</c>, which turns
/// that off; accent-insensitivity of the collation never reaches REGEXP ("é" does not match "e"); ICU's <c>\w</c> is <c>[\p{Alphabetic}\p{Mark}\p{Decimal_Number}\p{Connector_Punctuation}‌‍]</c>,
/// its <c>\s</c> is <c>[\t\n\f\r\p{Z}]</c> and <c>\b</c> derives from them, all different from .NET;
/// <c>.</c>, <c>^</c> and <c>$</c> treat CR, VT, FF, NEL, LS and PS as line terminators, not only LF;
/// the <c>i</c> flag uses full case folding ("fussball" matches "fußball"); named groups
/// <c>(?&lt;name&gt;...)</c>, <c>\k&lt;name&gt;</c>, atomic groups, possessive quantifiers and lookarounds
/// exist, but a lookbehind must have a bounded length; quantifiers on zero-width tests are rejected;
/// repeat counts go up to 16777215; conditionals and balancing groups do not exist. MySQL string literals process C escapes, so
/// <see cref="FlavorConversion.ToLiteral"/> doubles backslashes (sql_mode without NO_BACKSLASH_ESCAPES).
/// </summary>
internal sealed class MySqlTranslator : SqlRegexTranslator
{
    private const string CaseSensitivePrefix = "(?-i)";

    /// <summary>ICU stores repeat counts in 24 bits: MySQL 8.4 rejects 16777216 ("Decimal number ... is too large").</summary>
    private const int IcuMaxBound = 0xFFFFFF;

    public MySqlTranslator(RegexTree tree) : base(tree, RegexFlavor.MySql, HexEscapeStyle.Braced)
    {
    }

    protected override bool SupportsUnicodeCategories => true;

    protected override bool SupportsLookaround => true;

    protected override bool SupportsBackreferences => true;

    protected override bool SupportsAtomicGroups => true;

    protected override int MaxRepeatBound => IcuMaxBound;

    protected override string Prefix => CaseSensitivePrefix;

    protected override string AnyCharacter => "(?s:.)";

    protected override string AnyButNewline => @"[^\n]";

    protected override string StartOfInput => @"\A";

    protected override string EndOfInput => @"\z";

    protected override string EndOrFinalNewline(AnchorNode anchor) => @"(?=\n?\z)";

    protected override string MultilineStart(AnchorNode anchor) => @"(?:\A|(?<=\n))";

    protected override string MultilineEnd(AnchorNode anchor) => @"(?=\n|\z)";

    protected override string WordBoundary(AnchorNode anchor, bool negated, string wordClass) =>
        LookaroundBoundary(negated, wordClass);

    /// <summary>ICU group names are ASCII letters and digits, starting with a letter.</summary>
    protected override string? NamedGroupOpen(string name) => IsIcuName(name) ? $"(?<{name}>" : null;

    protected override string BackreferenceText(GroupNode group, int number) =>
        group.Name != null && IsIcuName(group.Name)
            ? $"\\k<{group.Name}>"
            : $"\\{number.ToString(CultureInfo.InvariantCulture)}";

    protected override void CheckLookbehind(GroupNode group)
    {
        if (HasUnboundedRepeat(group.Body))
        {
            Unsupported("lookbehind with an unbounded length (ICU requires a bounded lookbehind)", group);
        }
    }

    private static bool IsIcuName(string name)
    {
        if (name.Length == 0 || !IsAsciiLetter(name[0]))
        {
            return false;
        }

        foreach (char c in name)
        {
            if (!IsAsciiLetter(c) && !(c >= '0' && c <= '9'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
}
