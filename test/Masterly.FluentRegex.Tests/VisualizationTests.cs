using System.Xml.Linq;

namespace Masterly.FluentRegex.Tests;

public class VisualizationTests
{
    private static readonly Pattern Sample = Pattern.With
        .StartOfLine
        .NamedGroup("user", Pattern.With.Set(Pattern.With.Word.Literal(".")).Repeat.OneOrMore)
        .Literal("@")
        .OneOf(Pattern.With.Literal("gmail"), Pattern.With.Literal("outlook"))
        .Literal(".com")
        .Digit.Repeat.Optional
        .EndOfLine;

    [Fact]
    public void RailroadSvg_IsWellFormedXml()
    {
        string svg = Sample.ToRailroadSvg();

        XDocument document = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";
        document.Root!.Name.ShouldBe(ns + "svg");
        document.Descendants(ns + "text").Select(t => t.Value).ShouldContain("\"@\"");
        document.Descendants(ns + "text").Select(t => t.Value).ShouldContain("group 'user'");
        document.Descendants(ns + "text").Select(t => t.Value).ShouldContain("\"gmail\"");
        double.Parse(document.Root.Attribute("width")!.Value, System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThan(100);
    }

    [Fact]
    public void RailroadSvg_EscapesMarkup()
    {
        string svg = Pattern.With.Literal("<a&b>").ToRailroadSvg();
        Should.NotThrow(() => XDocument.Parse(svg));
        svg.ShouldContain("&lt;a&amp;b&gt;");
    }

    [Fact]
    public void RailroadSvg_RendersEveryCommonPattern()
    {
        foreach (Pattern pattern in new[] { CommonPatterns.Email(), CommonPatterns.Url(), CommonPatterns.IPv4(), CommonPatterns.StrongPassword(), CommonPatterns.HtmlTag(), CommonPatterns.Time12() })
            Should.NotThrow(() => XDocument.Parse(pattern.ToRailroadSvg()));
    }

    [Fact]
    public void Mermaid_ProducesFlowchart()
    {
        string mermaid = Sample.ToMermaid();

        mermaid.ShouldStartWith("flowchart LR");
        mermaid.ShouldContain("start([start])");
        mermaid.ShouldContain("finish([match])");
        mermaid.ShouldContain("subgraph");
        mermaid.ShouldContain("subgraph g1[\"group 'user'\"]");
        mermaid.ShouldContain("· 1+ times");
        mermaid.ShouldContain("#quot;@#quot;");
        mermaid.ShouldContain("digit · optional");
        mermaid.ShouldContain("classDef literal");
    }

    [Fact]
    public void Mermaid_AlternationForksAndJoins()
    {
        string mermaid = Pattern.FromRegex("a(b|c)d").ToMermaid();
        var lines = mermaid.Split('\n').Select(l => l.Trim()).ToList();

        string join = lines.Single(l => l.EndsWith("((\" \"))")).Split('(')[0];
        lines.Count(l => l.EndsWith("--> " + join)).ShouldBe(2);
        mermaid.ShouldContain("group #35;1");
    }

    [Fact]
    public void Mermaid_EscapesSpecialCharacters()
    {
        string mermaid = Pattern.With.Literal("\"#<>").ToMermaid();
        mermaid.ShouldContain("#quot;#quot;#35;#lt;#gt;#quot;");
    }
}
