#nullable enable
using System.Globalization;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Export;

/// <summary>
/// MariaDB regular expressions, which are PCRE2 (https://mariadb.com/kb/en/pcre/,
/// https://mariadb.com/kb/en/regexp/, https://www.pcre.org/current/doc/html/pcre2syntax.html).
/// Verified against a mariadb:11.4 container (PCRE2 10.42). Facts this translation relies on: "REGEXP
/// is not case sensitive, except when used with a binary string" (under <c>utf8mb4_uca1400_ai_ci</c> "E"
/// matches "e" and the Kelvin sign matches "k"), and the MariaDB documentation recommends starting the
/// pattern with <c>(?-i)</c> to force case sensitivity, which works; accent-insensitivity of the
/// collation never reaches REGEXP ("é" does not match "e"); <c>\x{..}</c> naming a surrogate is a
/// compile error, so a literal surrogate pair is written as one code point; by default <c>\d</c>, <c>\s</c> and <c>\w</c> match only ASCII characters and
/// <c>\b</c> is defined in terms of <c>\w</c>, so they are spelled out with <c>\p{..}</c> classes;
/// <c>$</c> also matches before a final newline and the newline convention defaults to "any Unicode
/// newline", so <c>\z</c> and explicit <c>\n</c> are used instead; <c>\u</c> escapes are not
/// supported (only <c>\x{..}</c>); backreferences are written as <c>\g{n}</c> to avoid the
/// <c>\10</c> octal ambiguity; each top-level branch of a lookbehind must have a fixed length
/// (older PCRE2 releases do not accept variable-length branches); bounds must be below 65536;
/// conditionals exist but are not translated. MariaDB string literals process C escapes, so
/// <see cref="FlavorConversion.ToLiteral"/> doubles backslashes (sql_mode without NO_BACKSLASH_ESCAPES).
/// </summary>
internal sealed class MariaDbTranslator : SqlRegexTranslator
{
    private const string CaseSensitivePrefix = "(?-i)";
    private const int PcreMaxBound = 65535;
    private const int PcreMaxNameLength = 32;

    public MariaDbTranslator(RegexTree tree) : base(tree, RegexFlavor.MariaDb, HexEscapeStyle.Braced)
    {
    }

    protected override bool SupportsUnicodeCategories => true;

    protected override bool SupportsLookaround => true;

    protected override bool SupportsBackreferences => true;

    protected override bool SupportsAtomicGroups => true;

    protected override int MaxRepeatBound => PcreMaxBound;

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

    /// <summary>PCRE2 names are up to 32 ASCII letters, digits and underscores, not starting with a digit.</summary>
    protected override string? NamedGroupOpen(string name) => IsPcreName(name) ? $"(?<{name}>" : null;

    protected override string BackreferenceText(GroupNode group, int number) =>
        $"\\g{{{number.ToString(CultureInfo.InvariantCulture)}}}";

    protected override void CheckLookbehind(GroupNode group)
    {
        if (!HasFixedBranches(group.Body))
        {
            Unsupported("lookbehind whose branches do not have a fixed length (PCRE2 requires it)", group);
        }
    }

    private static bool HasFixedBranches(RegexNode body)
    {
        if (body is AlternationNode alternation)
        {
            foreach (RegexNode branch in alternation.Branches)
            {
                if (FixedLength(branch) == null)
                {
                    return false;
                }
            }

            return true;
        }

        return FixedLength(body) != null;
    }

    private static bool IsPcreName(string name)
    {
        if (name.Length == 0 || name.Length > PcreMaxNameLength || (name[0] >= '0' && name[0] <= '9'))
        {
            return false;
        }

        foreach (char c in name)
        {
            bool allowed = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}
