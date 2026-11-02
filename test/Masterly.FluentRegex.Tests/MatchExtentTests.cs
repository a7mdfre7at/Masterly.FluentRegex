using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

/// <summary>
/// <see cref="FlavorConversion.MatchExtentWarnings"/>: differences in which substring a match covers
/// and what groups capture. Every "differs" case was observed on the live engine (PostgreSQL 17,
/// MySQL 8.4, MariaDB 11.4, SQL Server 2025) or in Node.js; every "same" case was compared there too.
/// </summary>
public class MatchExtentTests
{
    public static TheoryData<RegexFlavor> AllFlavors() => new TheoryData<RegexFlavor>
    {
        RegexFlavor.JavaScript, RegexFlavor.JsonSchema, RegexFlavor.PostgreSql, RegexFlavor.MySql,
        RegexFlavor.MariaDb, RegexFlavor.SqlServer, RegexFlavor.Sqlite,
    };

    [Theory]
    [MemberData(nameof(AllFlavors))]
    public void MatchExtentWarnings_PlainLiteral_IsEmptyForEveryFlavor(RegexFlavor flavor)
    {
        FlavorConversion conversion = Pattern.FromRegex("abc").ToFlavor(flavor);

        conversion.MatchExtentWarnings.ShouldNotBeNull();
        conversion.MatchExtentWarnings.ShouldBeEmpty();
        conversion.PreservesMatchExtent.ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    // PostgreSQL: longest/shortest match and per-iteration resets
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("(a|ab)", "longest match")]
    [InlineData("^(a|ab)(b?)$", "longest match")]
    [InlineData("(x|xy)(y?)z?", "longest match")]
    [InlineData("x(?:|b)", "longest match")]
    [InlineData("(a+?)(a*)", "shortest match")]
    [InlineData("(?m)^a+?", "longest match")]
    [InlineData(@"\ba+?", "longest match")]
    [InlineData("^(?:a|a\n)$", "longest match")]
    [InlineData("(?:(a)|b)+", "resets it on every iteration")]
    [InlineData("(?:(a)?b)+", "resets it on every iteration")]
    public void ToFlavor_PostgreSql_PatternWithAPreferenceDifference_ReportsIt(string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.PostgreSql);

        conversion.IsExact.ShouldBeTrue();
        conversion.PreservesMatchExtent.ShouldBeFalse();
        conversion.MatchExtentWarnings.ShouldContain(warning => warning.Contains(expected));
    }

    [Theory]
    [InlineData(@"(\d+)-(\d+)")]
    [InlineData(@"\d+")]
    [InlineData(@"\d+?")]
    [InlineData(@"[a-z]+@[a-z]+\.com")]
    [InlineData(@"^[a-z.]+@[a-z.]+\.[a-z]{2,}$")]
    [InlineData(@"^\d+$")]
    [InlineData(@"(\d+)$")]
    [InlineData(@"^[\s\S]*?$")]
    [InlineData("x(?:b|)")]
    [InlineData("(a|b)*?c")]
    [InlineData("a*")]
    public void ToFlavor_PostgreSql_PatternWithoutAPreferenceChoice_PreservesTheMatchExtent(string regex)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.PostgreSql);

        conversion.MatchExtentWarnings.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------
    // MySQL and MariaDB: empty matches in REGEXP_REPLACE
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(RegexFlavor.MariaDb, "a*", "never replaces an empty match")]
    [InlineData(RegexFlavor.MariaDb, "^", "never replaces an empty match")]
    [InlineData(RegexFlavor.MariaDb, @"\ba?", "never replaces an empty match")]
    [InlineData(RegexFlavor.MariaDb, "(?=a)a??", "never replaces an empty match")]
    [InlineData(RegexFlavor.MySql, "x*", "returns an empty input unchanged")]
    [InlineData(RegexFlavor.MySql, "$", "returns an empty input unchanged")]
    public void ToFlavor_PatternThatCanMatchEmptyText_ReportsTheReplaceDifference(RegexFlavor flavor, string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(flavor);

        conversion.MatchExtentWarnings.ShouldHaveSingleItem().ShouldContain(expected);
    }

    [Theory]
    [InlineData(RegexFlavor.MariaDb, "(a|ab)(c|bcd)(d*)")]
    [InlineData(RegexFlavor.MariaDb, "(?:(a)|b)+")]
    [InlineData(RegexFlavor.MySql, "(a|ab)")]
    [InlineData(RegexFlavor.MySql, "a+?b*")]
    [InlineData(RegexFlavor.MySql, "(a|b)+")]
    public void ToFlavor_BacktrackingEngineWithNonEmptyMatches_PreservesTheMatchExtent(RegexFlavor flavor, string regex)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(flavor);

        conversion.PreservesMatchExtent.ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    // SQL Server: the '$' rewrite and empty iterations
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("a$", "consumes a final newline")]
    [InlineData(@"^\d+\Z", "consumes a final newline")]
    [InlineData("(a?)+", "SQL Server can keep an earlier capture")]
    [InlineData("(a*)*", "SQL Server can keep an earlier capture")]
    public void ToFlavor_SqlServer_PatternWithAnExtentDifference_ReportsIt(string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.SqlServer);

        conversion.IsExact.ShouldBeTrue();
        conversion.MatchExtentWarnings.ShouldHaveSingleItem().ShouldContain(expected);
    }

    [Theory]
    [InlineData(@"a\z")]
    [InlineData("(a|ab)")]
    [InlineData("(?:(a)|b)+")]
    [InlineData("(?m)a$")]
    [InlineData("a+?b*")]
    public void ToFlavor_SqlServer_LeftmostFirstPattern_PreservesTheMatchExtent(string regex)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.SqlServer);

        conversion.PreservesMatchExtent.ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    // SQLite and JavaScript
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("(a|ab)")]
    [InlineData("(?:(a)|b)+")]
    [InlineData("a*$")]
    public void ToFlavor_Sqlite_RunsDotNetItself_PreservesTheMatchExtent(string regex)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.Sqlite);

        conversion.PreservesMatchExtent.ShouldBeTrue();
    }

    [Theory]
    [InlineData(RegexFlavor.JavaScript, "(?:(a)|b)+", "resets it on every iteration")]
    [InlineData(RegexFlavor.JavaScript, "(a?)+", "refuses empty iterations")]
    [InlineData(RegexFlavor.JsonSchema, "(?:(a)?b)+", "resets it on every iteration")]
    public void ToFlavor_EcmaScript_GroupInARepeat_ReportsTheCaptureDifference(RegexFlavor flavor, string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(flavor);

        conversion.MatchExtentWarnings.ShouldHaveSingleItem().ShouldContain(expected);
    }

    [Theory]
    [InlineData("(a|ab)")]
    [InlineData("(a|b)+")]
    [InlineData(@"(\d+)-(\d+)")]
    public void ToFlavor_JavaScript_BacktrackingLikeDotNet_PreservesTheMatchExtent(string regex)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.JavaScript);

        conversion.PreservesMatchExtent.ShouldBeTrue();
    }

    [Fact]
    public void GroupNumbers_NeverContainTheWholeMatch()
    {
        FlavorConversion conversion = Pattern.FromRegex("(a)(b)").ToFlavor(RegexFlavor.PostgreSql);

        conversion.GroupNumbers.Keys.ShouldNotContain("0");
    }

    [Fact]
    public void ToFlavor_MariaDb_OptionalOnlyPattern_HasAZeroSyntacticWidth()
    {
        FlavorConversion conversion = Pattern.FromRegex("a?", RegexOptions.None).ToFlavor(RegexFlavor.MariaDb);

        conversion.PreservesMatchExtent.ShouldBeFalse();
    }
}
