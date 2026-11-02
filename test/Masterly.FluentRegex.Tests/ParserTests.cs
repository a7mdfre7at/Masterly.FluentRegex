using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Tests;

public class ParserTests
{
    public static readonly string[] Corpus =
    {
        @"",
        @"abc",
        @"^\d{3}-\d{2}-\d{4}$",
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})",
        @"(a|b|c)+?",
        @"(?:x|y)*z{2,}",
        @"\bword\b\B",
        @"(?=.*\d)(?!.*\s)(?<=a)(?<!b)",
        @"(?>\d+)\d",
        @"(\w+)\s\1",
        @"(?<w>\w+)\s\k<w>",
        @"(?'w'\w+)\s\k'w'",
        @"(a)?(?(1)b|c)",
        @"[\--/]",
        @"(?(name)yes|no)",
        @"(?(?=\d)\d+|\w+)",
        @"(?i)abc(?-i)def",
        @"(?im:abc)",
        @"(?#a comment)x",
        @"[^\]\\\-]",
        @"[a-z-[aeiou]]",
        @"[\p{L}\d_]",
        @"\p{Lu}\P{IsGreek}",
        @"\x41\u0042\t\n\cA\0",
        @"a{,3}",
        @"a{3",
        @"{x}",
        @"[]a]",
        @"\A\z\Z\G",
        @"(?<open>\()(?<close-open>\))",
        @"((a)(?<n>b)(c))",
        @"a|",
        @"|a",
        @"(|)",
        @"[a\-z]",
        @"[\d-x]",
        @"x*?y+?z??w{1,2}?",
    };

    [Theory]
    [MemberData(nameof(CorpusData))]
    public void Writer_RoundTripsSourceExactly(string pattern)
    {
        RegexTree tree = RegexParser.Parse(pattern);
        RegexWriter.Write(tree.Root).ShouldBe(pattern);
    }

    [Theory]
    [MemberData(nameof(CorpusData))]
    public void GroupNumbers_MatchDotNet(string pattern)
    {
        RegexTree tree = RegexParser.Parse(pattern);
        var regex = new Regex(pattern);

        var expected = regex.GetGroupNumbers().Where(n => n > 0).OrderBy(n => n).ToArray();
        var actual = tree.Groups.Where(g => g.IsCapturing).Select(g => g.Number).Distinct().OrderBy(n => n).ToArray();
        actual.ShouldBe(expected);

        foreach (GroupNode group in tree.Groups.Where(g => g.Name != null && g.IsCapturing))
            regex.GroupNumberFromName(group.Name).ShouldBe(group.Number);
    }

    public static IEnumerable<object[]> CorpusData() => Corpus.Select(p => new object[] { p });

    [Theory]
    [InlineData(@"(abc")]
    [InlineData(@"abc)")]
    [InlineData(@"*a")]
    [InlineData(@"a**")]
    [InlineData(@"[abc")]
    [InlineData(@"\")]
    [InlineData(@"[z-a]")]
    [InlineData(@"a{5,2}")]
    [InlineData(@"(?<>a)")]
    [InlineData(@"\q")]
    [InlineData(@"(?(1)a|b|c)")]
    public void InvalidPatterns_AreRejected_LikeDotNet(string pattern)
    {
        Should.Throw<ArgumentException>(() => new Regex(pattern));
        Should.Throw<RegexSyntaxException>(() => RegexParser.Parse(pattern));
    }

    [Fact]
    public void Quantifier_IsParsed()
    {
        var q = RegexParser.Parse(@"\d{2,5}?").Root.ShouldBeOfType<QuantifierNode>();
        q.Min.ShouldBe(2);
        q.Max.ShouldBe(5);
        q.Lazy.ShouldBeTrue();
        q.Body.ShouldBeOfType<CharClassNode>().ClassKind.ShouldBe(ClassKind.Digit);
    }

    [Fact]
    public void IgnorePatternWhitespace_SkipsWhitespaceAndComments()
    {
        RegexTree tree = RegexParser.Parse("\\d + # digits\n - \\w", RegexOptions.IgnorePatternWhitespace);
        var items = RegexWriter.ItemsOf(tree.Root);
        items.Count.ShouldBe(3);
        RegexWriter.Write(tree.Root).ShouldBe(@"\d+-\w");
    }

    [Fact]
    public void ExplicitCapture_MakesUnnamedGroupsNonCapturing()
    {
        RegexTree tree = RegexParser.Parse(@"(a)(?<n>b)", RegexOptions.ExplicitCapture);
        tree.Groups.Count(g => g.IsCapturing).ShouldBe(1);
    }

    [Fact]
    public void InlineOptions_ApplyToFollowingNodes()
    {
        RegexTree tree = RegexParser.Parse(@"a(?i)b");
        var items = RegexWriter.ItemsOf(tree.Root);
        items[0].Options.HasFlag(RegexOptions.IgnoreCase).ShouldBeFalse();
        items[2].Options.HasFlag(RegexOptions.IgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void BracketClass_ParsesItems()
    {
        var node = RegexParser.Parse(@"[^a-f\d_]").Root.ShouldBeOfType<CharClassNode>();
        node.Bracket.Negated.ShouldBeTrue();
        node.Bracket.Items.Count.ShouldBe(3);
        node.Bracket.Items[0].IsRange.ShouldBeTrue();
        node.Bracket.Items[1].ShorthandKind.ShouldBe(ClassKind.Digit);
        node.Bracket.Items[2].Low.ShouldBe('_');
    }

    [Fact]
    public void CharSet_UsesRealEngineSemantics()
    {
        CharSet digits = CharSet.ForSource(@"\d", RegexOptions.None);
        digits.Contains('5').ShouldBeTrue();
        digits.Contains('٣').ShouldBeTrue(); // Arabic-Indic digit: \d is Unicode-aware in .NET
        digits.Contains('a').ShouldBeFalse();

        CharSet letters = CharSet.ForSource("[a-z]", RegexOptions.IgnoreCase);
        letters.Contains('Q').ShouldBeTrue();

        digits.Overlaps(CharSet.ForSource(@"\w", RegexOptions.None)).ShouldBeTrue();
        digits.Overlaps(CharSet.ForSource("[a-z]", RegexOptions.None)).ShouldBeFalse();
    }

    [Fact]
    public void SplitLastAtom()
    {
        RegexParser.TrySplitLastAtom(@"ab\d", out string prefix, out string last).ShouldBeTrue();
        prefix.ShouldBe("ab");
        last.ShouldBe(@"\d");
    }
}
