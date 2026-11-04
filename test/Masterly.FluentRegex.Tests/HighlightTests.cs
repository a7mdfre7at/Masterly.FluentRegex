using System.Text.RegularExpressions;
using Masterly.FluentRegex.Highlighting;

namespace Masterly.FluentRegex.Tests;

public class HighlightTests
{
    [Fact]
    public void Html_ControlCharactersInTheInput_AreShownAsVisibleSymbols()
    {
        string input = "a\u001B[31mb\u0007\tc\u007F";

        string html = Pattern.With.Literal("b").Highlight(input, HighlightFormat.Html);

        html.ShouldNotContain("\u001B");
        html.ShouldNotContain("\u0007");
        html.ShouldNotContain("\u007F");
        html.ShouldBe("a␛[31m<mark class=\"frx-match\">b</mark>␇\tc␡");
    }

    private const string Esc = "\u001b";

    private static readonly Pattern Phone = Pattern.With.Digit.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);

    private static readonly Pattern YearMonth = Pattern.With
        .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
        .Literal("-")
        .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2));

    [Fact]
    public void Brackets_SimpleMatch_WrapsTheMatch()
    {
        Phone.Highlight("Call 555-1234 now", HighlightFormat.Brackets).ShouldBe("Call [555-1234] now");
    }

    [Fact]
    public void Brackets_NamedGroups_AreLabelledInsideTheirMatch()
    {
        string text = YearMonth.Highlight("Due 2026-05, paid 2026-06.", HighlightFormat.Brackets);

        text.ShouldBe("Due [[year:2026]-[month:05]], paid [[year:2026]-[month:06]].");
    }

    [Fact]
    public void Brackets_NoMatch_ReturnsTextUnchanged()
    {
        Phone.Highlight("no phone here", HighlightFormat.Brackets).ShouldBe("no phone here");
    }

    [Theory]
    [InlineData(@"(?<word>(\w+))", "hi", "[[word:[1:hi]]]")]
    [InlineData(@"(?<outer>a(?<inner>b)c)", "abc", "[[outer:a[inner:b]c]]")]
    [InlineData(@"(a)(b)", "ab", "[[1:a][2:b]]")]
    [InlineData(@"(a)()(b)", "ab", "[[1:a][2:][3:b]]")]
    [InlineData(@"(a())", "a", "[[1:a[2:]]]")]
    [InlineData(@"(\d)+", "12", "[[1:1][1:2]]")]
    [InlineData(@"(x)?(y)", "y", "[[2:y]]")]
    [InlineData(@"(?=(\w+))\w", "ab", "[[1:a]][[1:b]]")]
    [InlineData(@"(?<=(a))b", "ab", "a[b]")]
    [InlineData(@"(?<x>a)(?<x>)", "a", "[[x:a][x:]]")]
    [InlineData(@"(?<a>)(?<b>)", "", "[[a:][b:]]")]
    [InlineData(@"(?<y>(?<x>a))(?<x>b)", "ab", "[[y:[x:a]][x:b]]")]
    public void Brackets_GroupLayouts_NestAndOrderCorrectly(string regex, string input, string expected)
    {
        Pattern.FromRegex(regex).Highlight(input, HighlightFormat.Brackets).ShouldBe(expected);
    }

    [Fact]
    public void Brackets_EmptyMatches_RenderAsEmptyMarkers()
    {
        Pattern.FromRegex("a*").Highlight("bab", HighlightFormat.Brackets).ShouldBe("[]b[a][]b[]");
    }

    [Fact]
    public void Brackets_RightToLeftPattern_RendersInTextOrder()
    {
        Pattern pattern = Pattern.FromRegex(@"\d+", RegexOptions.RightToLeft);

        pattern.Highlight("1 22 333", HighlightFormat.Brackets).ShouldBe("[1] [22] [333]");
    }

    [Fact]
    public void Html_MatchesAndGroups_UseMarkAndSpanElements()
    {
        string html = YearMonth.Highlight("Due 2026-05", HighlightFormat.Html);

        html.ShouldBe(
            "Due <mark class=\"frx-match\"><span class=\"frx-group frx-group-year\" title=\"year\">2026</span>-" +
            "<span class=\"frx-group frx-group-month\" title=\"month\">05</span></mark>");
    }

    [Fact]
    public void Html_UntrustedText_IsEncoded()
    {
        Pattern tag = Pattern.FromRegex(@"(?<tag><\w+>)");

        string html = tag.Highlight("<script>alert('x' & 1)</script>", HighlightFormat.Html);

        html.ShouldNotContain("<script>");
        html.ShouldBe(
            "<mark class=\"frx-match\"><span class=\"frx-group frx-group-tag\" title=\"tag\">" +
            "&lt;script&gt;</span></mark>alert(&#39;x&#39; &amp; 1)&lt;/script&gt;");
    }

    [Fact]
    public void Html_EmptyMatch_RendersEmptyMark()
    {
        Pattern.FromRegex("^").Highlight("ab", HighlightFormat.Html).ShouldBe("<mark class=\"frx-match\"></mark>ab");
    }

    [Fact]
    public void Ansi_NestedGroups_InnermostColorWinsAndLegendFollows()
    {
        string ansi = Pattern.FromRegex(@"(?<outer>a(?<inner>b)c)").Highlight("xabc");

        ansi.ShouldBe(
            $"x{Esc}[44;93ma{Esc}[44;92mb{Esc}[44;93mc{Esc}[0m{Environment.NewLine}" +
            $"Legend: {Esc}[44;97m match {Esc}[0m {Esc}[44;93m outer {Esc}[0m {Esc}[44;92m inner {Esc}[0m");
    }

    [Fact]
    public void Ansi_MatchWithoutGroups_UsesMatchBackground()
    {
        string ansi = Phone.Highlight("Call 555-1234", HighlightFormat.Ansi);

        ansi.ShouldStartWith($"Call {Esc}[44;97m555-1234{Esc}[0m");
        ansi.ShouldEndWith($"Legend: {Esc}[44;97m match {Esc}[0m");
    }

    [Fact]
    public void Ansi_UnnamedGroups_AreNamedByNumberInTheLegend()
    {
        string ansi = Pattern.FromRegex(@"(\d)").Highlight("1");

        ansi.ShouldContain($"{Esc}[44;93m group 1 {Esc}[0m");
    }

    [Fact]
    public void Ansi_EscapeCharactersInInput_CannotInjectTerminalCodes()
    {
        string ansi = Phone.Highlight($"{Esc}[31m555-1234");

        ansi.ShouldNotContain($"{Esc}[31m");
        ansi.ShouldStartWith($"{(char)0x241B}[31m");
    }

    [Fact]
    public void Ansi_EmptyMatches_ProduceNoColorCodes()
    {
        string ansi = Pattern.FromRegex("x*").Highlight("ab");

        ansi.ShouldStartWith($"ab{Environment.NewLine}Legend:");
    }

    [Fact]
    public void Highlight_DefaultFormat_IsAnsi()
    {
        Phone.Highlight("555-1234").ShouldBe(Phone.Highlight("555-1234", HighlightFormat.Ansi));
    }

    [Fact]
    public void Highlight_NullText_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => Phone.Highlight(null!));
    }

    [Fact]
    public void Highlight_UndefinedFormat_ThrowsArgumentOutOfRange()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Phone.Highlight("x", (HighlightFormat)42));
    }

    [Theory]
    [InlineData(@"((a))", "a", "[[1:[2:a]]]")]
    [InlineData(@"(?<outer>(?<inner>b))", "b", "[[outer:[inner:b]]]")]
    public void Build_WithoutParsedTree_NestsIdenticalSpansByGroupNumber(string regex, string input, string expected)
    {
        HighlightTreeBuilder builder = new HighlightTreeBuilder(new Regex(regex), null);
        HighlightRenderer renderer = HighlightRenderer.Create(HighlightFormat.Brackets);

        string rendered = renderer.Render(input, builder.Build(input), builder.Groups);

        rendered.ShouldBe(expected);
    }

    [Fact]
    public void Build_WithoutParsedTree_StillListsEveryCaptureGroup()
    {
        HighlightTreeBuilder builder = new HighlightTreeBuilder(new Regex(@"(?<year>\d{4})-(\d{2})"), null);

        IReadOnlyList<HighlightGroup> groups = builder.Groups;

        groups.Select(group => group.Label).ShouldBe(new[] { "1", "year" });
        groups.Select(group => group.Number).ShouldBe(new[] { 1, 2 });
    }
}
