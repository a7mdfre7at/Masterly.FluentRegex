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

    [Theory]
    [InlineData(@"\d", "n0[\"digit\"]", "class n0 charclass")]
    [InlineData(@"\1(a)", "n0[\"same as #35;1\"]", "class n0 charclass")]
    [InlineData("(?i)a", "n0([\"(?i)\"])", "class n0 anchor")]
    [InlineData("(?(?=a)a|b)", "n0[\"(?(?=a)a|b)\"]", "class n0 anchor")]
    [InlineData("(?(?=a)a|b)+", "n0[\"(?(?=a)a|b) · 1+ times\"]", "class n0 anchor")]
    [InlineData("a+", "n0[\"#quot;a#quot; · 1+ times\"]", "class n0 literal")]
    [InlineData(@"\d+", "n0[\"digit · 1+ times\"]", "class n0 charclass")]
    public void Mermaid_Leaf_HasLabelAndStyleClass(string regex, string node, string styleLine)
    {
        string mermaid = Pattern.FromRegex(regex).ToMermaid();

        mermaid.ShouldContain(node);
        mermaid.ShouldContain(styleLine);
    }

    [Theory]
    [InlineData("a{3}", "3 times")]
    [InlineData("a{2,4}", "2-4 times")]
    [InlineData("a{2,}", "2+ times")]
    [InlineData("a*", "0+ times")]
    [InlineData("a?", "optional")]
    [InlineData("a??", "optional, lazy")]
    [InlineData("a+?", "1+ times, lazy")]
    [InlineData("a{2,}?", "2+ times, lazy")]
    [InlineData("a{2,4}?", "2-4 times, lazy")]
    public void Mermaid_QuantifierCount_IsDescribed(string regex, string count)
    {
        string mermaid = Pattern.FromRegex(regex).ToMermaid();

        mermaid.ShouldContain("n0[\"#quot;a#quot; · " + count + "\"]");
    }

    [Theory]
    [InlineData("(a)+", "subgraph g0[\"group #35;1 · 1+ times\"]")]
    [InlineData("(?:ab)+", "subgraph g0[\"1+ times\"]")]
    [InlineData("(?=a)", "subgraph g0[\"followed by\"]")]
    [InlineData("(?>a)", "subgraph g0[\"atomic group (no backtracking)\"]")]
    [InlineData("(?i:a)", "subgraph g0[\"group with options i\"]")]
    public void Mermaid_Group_IsRenderedAsTitledSubgraph(string regex, string subgraph)
    {
        string mermaid = Pattern.FromRegex(regex).ToMermaid();

        mermaid.ShouldContain(subgraph);
        mermaid.ShouldContain("direction LR");
    }

    [Fact]
    public void Mermaid_NonCapturingGroupWithoutQuantifier_IsInlined()
    {
        string mermaid = Pattern.FromRegex("(?:a)b").ToMermaid();

        mermaid.ShouldNotContain("subgraph");
        mermaid.ShouldContain("n0[\"#quot;a#quot;\"]");
    }

    [Fact]
    public void Mermaid_Comment_IsSkipped()
    {
        string mermaid = Pattern.FromRegex("a(?#c)b").ToMermaid();

        mermaid.ShouldContain("n0 --> n1");
        mermaid.ShouldNotContain("(?#c)");
        mermaid.ShouldContain("class n0,n1 literal");
    }

    [Fact]
    public void Mermaid_EmptyAlternative_ConnectsStartStraightToJoin()
    {
        string mermaid = Pattern.FromRegex("a|").ToMermaid();

        mermaid.ShouldContain("start --> j0");
        mermaid.ShouldContain("n1 --> j0");
    }

    [Fact]
    public void Mermaid_OnlyUsedStyleClasses_AreAssigned()
    {
        string mermaid = Pattern.FromRegex("ab").ToMermaid();

        mermaid.Split('\n').Count(l => l.TrimStart().StartsWith("class ", StringComparison.Ordinal)).ShouldBe(1);
        mermaid.ShouldEndWith("class n0 literal");
    }

    [Theory]
    [InlineData("a{3}", "3 times")]
    [InlineData("a{2,4}", "2-4 times")]
    [InlineData("a{2,}", "at least 2")]
    [InlineData("a+?", "lazy")]
    [InlineData("a*?", "lazy")]
    [InlineData("a{2,}?", "at least 2, lazy")]
    [InlineData("a{2}?", "2 times, lazy")]
    [InlineData("a{2,4}?", "2-4 times, lazy")]
    public void RailroadSvg_LoopLabel_DescribesRepetition(string regex, string label)
    {
        XDocument document = XDocument.Parse(Pattern.FromRegex(regex).ToRailroadSvg());

        LabelTexts(document).ShouldBe(new[] { label });
    }

    [Theory]
    [InlineData("a+")]
    [InlineData("a*")]
    [InlineData("a?")]
    [InlineData("a{1}")]
    public void RailroadSvg_PlainRepetition_HasNoLoopLabel(string regex)
    {
        XDocument document = XDocument.Parse(Pattern.FromRegex(regex).ToRailroadSvg());

        LabelTexts(document).ShouldBeEmpty();
        Texts(document).ShouldBe(new[] { "\"a\"" });
    }

    [Fact]
    public void RailroadSvg_ZeroRepetition_DrawsNothing()
    {
        XDocument document = XDocument.Parse(Pattern.FromRegex("a{0}").ToRailroadSvg());

        Texts(document).ShouldBeEmpty();
        document.Root!.Attribute("width")!.Value.ShouldBe("80");
    }

    [Fact]
    public void RailroadSvg_Comment_IsSkipped()
    {
        XDocument document = XDocument.Parse(Pattern.FromRegex("a(?#c)b").ToRailroadSvg());

        Texts(document).ShouldBe(new[] { "\"a\"", "\"b\"" });
    }

    [Fact]
    public void RailroadSvg_EmptyAlternative_RendersChoiceWithBypass()
    {
        XDocument document = XDocument.Parse(Pattern.FromRegex("a|").ToRailroadSvg());

        Texts(document).ShouldBe(new[] { "\"a\"" });
        document.Descendants(Svg + "path").Count().ShouldBe(6);
        document.Root!.Attribute("width")!.Value.ShouldBe("162.2");
    }

    [Theory]
    [InlineData("(?(?=a)a|b)", "anchor", "(?(?=a)a|b)")]
    [InlineData(@"\1(a)", "charclass", "same as #1")]
    [InlineData("(?i)a", "anchor", "(?i)")]
    [InlineData(@"\b", "anchor", "word boundary")]
    public void RailroadSvg_Terminal_UsesStyleClassAndLabel(string regex, string cssClass, string text)
    {
        XDocument document = XDocument.Parse(Pattern.FromRegex(regex).ToRailroadSvg());

        document.Descendants(Svg + "g")
            .Where(g => (string?)g.Attribute("class") == cssClass)
            .Select(g => g.Element(Svg + "text")!.Value)
            .ShouldContain(text);
    }

    [Theory]
    [InlineData("(?=a)", "followed by")]
    [InlineData("(?>a)", "atomic group (no backtracking)")]
    [InlineData("(a)", "group #1")]
    public void RailroadSvg_Group_IsBoxedWithLabel(string regex, string label)
    {
        XDocument document = XDocument.Parse(Pattern.FromRegex(regex).ToRailroadSvg());

        LabelTexts(document).ShouldBe(new[] { label });
    }

    [Fact]
    public void RailroadSvg_ControlCharacterInPattern_IsEncodedInTitle()
    {
        string svg = Pattern.FromRegex("a\tb").ToRailroadSvg();

        svg.ShouldContain("<title>a&#x9;b</title>");
        XDocument.Parse(svg).Root!.Element(Svg + "title")!.Value.ShouldBe("a\tb");
    }

    [Fact]
    public void RailroadSvg_LongTerminalText_IsTruncated()
    {
        string literal = new string('x', 60);

        XDocument document = XDocument.Parse(Pattern.With.Literal(literal).ToRailroadSvg());

        Texts(document).Single().ShouldBe("\"" + new string('x', 44) + "...");
    }

    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static string[] Texts(XDocument document) =>
        document.Descendants(Svg + "text").Where(t => t.Attribute("class") == null).Select(t => t.Value).ToArray();

    private static string[] LabelTexts(XDocument document) =>
        document.Descendants(Svg + "text").Where(t => (string?)t.Attribute("class") == "label").Select(t => t.Value).ToArray();

    [Fact]
    public void Mermaid_EscapesSpecialCharacters()
    {
        string mermaid = Pattern.With.Literal("\"#<>").ToMermaid();
        mermaid.ShouldContain("#quot;#quot;#35;#lt;#gt;#quot;");
    }
}
