#nullable enable
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Export;

/// <summary>
/// SQL Server 2025 / Azure SQL <c>REGEXP_LIKE</c>, "based on the RE2 regular expression library"
/// (https://learn.microsoft.com/en-us/sql/t-sql/functions/regexp-like-transact-sql,
/// https://learn.microsoft.com/en-us/sql/relational-databases/regular-expressions/overview,
/// https://github.com/google/re2/wiki/Syntax). Facts this translation relies on: RE2 has no
/// lookaround, no backreferences, no atomic groups, no conditionals, no <c>\Z</c> and no
/// <c>(?#...)</c> comments; <c>$</c> matches at the end of text "like <c>\z</c>, not <c>\Z</c>", so
/// .NET's <c>$</c> is written as <c>\n?\z</c> and is only accepted when nothing follows it;
/// <c>\d</c>, <c>\s</c>, <c>\w</c> and <c>\b</c> are ASCII-only, so the classes are spelled out with
/// <c>\p{..}</c> (RE2 supports every general category except <c>Cn</c>) and <c>\b</c> is accepted only
/// under RegexOptions.ECMAScript; <c>.</c> excludes only <c>\n</c>, like .NET; named groups are
/// <c>(?P&lt;name&gt;...)</c>; repetition counts above 1000 are rejected; flags are set with
/// <c>(?flags:re)</c>; matching ignores the column collation and is case-sensitive by default.
/// </summary>
internal sealed class SqlServerTranslator : SqlRegexTranslator
{
    private const int Re2MaxBound = 1000;
    private const int Re2MaxPatternLength = 4000;

    public SqlServerTranslator(RegexTree tree) : base(tree, RegexFlavor.SqlServer, HexEscapeStyle.Braced)
    {
    }

    protected override bool SupportsUnicodeCategories => true;

    protected override bool SupportsLookaround => false;

    protected override bool SupportsBackreferences => false;

    protected override bool SupportsAtomicGroups => false;

    protected override int MaxRepeatBound => Re2MaxBound;

    /// <summary>
    /// The pattern argument is at most <c>nvarchar(4000)</c>: SQL Server 2025 rejects a longer
    /// <c>N'...'</c> literal ("Argument data type NVARCHAR(MAX) is invalid for argument 2").
    /// </summary>
    protected override int MaxPatternLength => Re2MaxPatternLength;

    protected override string AnyCharacter => "(?s:.)";

    protected override string AnyButNewline => ".";

    protected override string StartOfInput => @"\A";

    protected override string EndOfInput => @"\z";

    /// <summary>RE2 cannot look ahead, so the optional final newline is consumed; that is only equivalent when the anchor ends the match.</summary>
    protected override string EndOrFinalNewline(AnchorNode anchor)
    {
        if (!IsTrailing(anchor))
        {
            Unsupported("'$' or '\\Z' followed by more pattern (RE2 has no lookahead to express \"end or before a final newline\")", anchor);
        }

        WarnExtent($"'$' or '\\Z' {At(anchor)} is written '\\n?\\z', which consumes a final newline that .NET leaves " +
                   "outside the match: on \"a\\n\", replacing a$ gives \"X\" in SQL Server and \"X\\n\" in .NET.");
        return @"\n?\z";
    }

    /// <summary>
    /// RE2 is leftmost-first with .NET's preferences (verified on SQL Server 2025: <c>(a|ab)</c> matches
    /// <c>a</c>, a group skipped by a later iteration keeps its capture), but it refuses empty iterations,
    /// so a repeat whose body can match empty text can match and capture differently (on <c>ab</c>,
    /// <c>(?:a?|b{1,2})*</c> matches <c>a</c> in .NET and <c>ab</c> in RE2; on <c>aa</c>, <c>(a?)+</c>
    /// leaves the group empty in .NET and at <c>a</c> in RE2). The <c>$</c> rewrite is reported where it is emitted.
    /// </summary>
    protected override void AddMatchExtentWarnings()
    {
        QuantifierNode? repeat = Plan.FirstNullableRepeat();
        if (repeat != null)
        {
            WarnExtent($"The repeat {At(repeat)} can run an iteration that matches empty text: .NET stops the repeat " +
                       "there, while RE2 refuses empty iterations and tries the body's other ways, so the match and " +
                       "the captures can differ.");
        }
    }

    protected override string MultilineStart(AnchorNode anchor) => "(?m:^)";

    protected override string MultilineEnd(AnchorNode anchor) => "(?m:$)";

    protected override string WordBoundary(AnchorNode anchor, bool negated, string wordClass)
    {
        if ((anchor.Options & RegexOptions.ECMAScript) == 0)
        {
            Unsupported("Unicode word boundary (RE2's \\b is ASCII-only and it has no lookaround to express .NET's)", anchor);
        }

        return negated ? @"\B" : @"\b";
    }

    protected override string? NamedGroupOpen(string name) => IsRe2Name(name) ? $"(?P<{name}>" : null;

    protected override string BackreferenceText(GroupNode group, int number) => string.Empty;

    private static bool IsRe2Name(string name)
    {
        if (name.Length == 0 || (name[0] >= '0' && name[0] <= '9'))
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
