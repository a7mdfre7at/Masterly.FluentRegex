using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class OptimizationTests
{
    [Theory]
    [InlineData("cat|car|cab", "ca[trb]")]
    [InlineData("a|b|c", "[abc]")]
    [InlineData(@"\d\d\d-\d\d\d\d", @"\d{3}-\d{4}")]
    [InlineData("x{1}y{0,1}z{0,}w{1,}", "xy?z*w+")]
    [InlineData("c{2,2}", "c{2}")]
    [InlineData("aa*b", "a+b")]
    [InlineData("a*?a", "a+?")]
    [InlineData("(?:abc)d", "abcd")]
    [InlineData("foo|bar|foo", "foo|bar")]
    [InlineData("(a|b|a)", "([ab])")]
    [InlineData("^(?:(?:a|b)|c)$", "^[abc]$")]
    [InlineData("(?:a|b)*?c", "[ab]*?c")]
    [InlineData("dog|cat|car", "dog|ca[tr]")]
    public void Optimize_RegularPattern_AppliesProvenRewrites(string regex, string expected)
    {
        OptimizationResult result = Pattern.FromRegex(regex).Optimize();

        result.Optimized.ToString().ShouldBe(expected);
        result.VerifiedEquivalent.ShouldBeTrue();
        result.IsChanged.ShouldBeTrue();
        result.Optimized.IsEquivalentTo(result.Original).ShouldBeTrue();
    }

    [Fact]
    public void Optimize_CommonPrefixAlternation_DescribesTheMerge()
    {
        OptimizationResult result = Pattern.FromRegex("cat|car|cab").Optimize();

        result.AppliedRewrites.ShouldBe(new[] { "Merged alternation 'cat|car|cab' into 'ca[trb]'" });
        result.ToString().ShouldContain("proven equivalent");
        result.ToString().ShouldContain("- Merged alternation 'cat|car|cab' into 'ca[trb]'");
    }

    [Fact]
    public void Optimize_NamedGroups_KeepsGroupNumbersAndCaptures()
    {
        Pattern original = Pattern.FromRegex(@"(?<year>\d\d\d\d)-(?<month>\d\d)");

        OptimizationResult result = original.Optimize();
        Match before = original.Match("on 2024-05-17");
        Match after = result.Optimized.Match("on 2024-05-17");

        result.Optimized.ToString().ShouldBe(@"(?<year>\d{4})-(?<month>\d\d)");
        after.Groups["year"].Value.ShouldBe(before.Groups["year"].Value);
        after.Groups["month"].Value.ShouldBe(before.Groups["month"].Value);
        after.Index.ShouldBe(before.Index);
    }

    [Fact]
    public void Optimize_LeftmostFirstAlternation_NeverReordersBranches()
    {
        Pattern original = Pattern.FromRegex("a|ab");

        OptimizationResult result = original.Optimize();

        result.Optimized.Match("ab").Value.ShouldBe("a");
        result.Optimized.ToString().ShouldBe("a|ab");
        result.IsChanged.ShouldBeFalse();
    }

    [Fact]
    public void Optimize_DuplicateBranchesWithCaptures_AreKept()
    {
        Pattern original = Pattern.FromRegex("(a)|(b)|(a)");

        OptimizationResult result = original.Optimize();

        result.Optimized.ShouldBeSameAs(original);
        result.AppliedRewrites.ShouldBeEmpty();
        result.ToString().ShouldStartWith("No rewrites applied");
    }

    [Fact]
    public void Optimize_NonRegularPattern_AppliesSampleCheckedRewritesWithoutProof()
    {
        OptimizationResult result = Pattern.FromRegex(@"(cat|car)\1").Optimize();

        result.Optimized.ToString().ShouldBe(@"(ca[tr])\1");
        result.VerifiedEquivalent.ShouldBeFalse();
        result.ToString().ShouldContain("not proven");
        result.Optimized.Match("carcar").Value.ShouldBe("carcar");
        result.Optimized.IsMatch("catcar").ShouldBeFalse();
    }

    [Fact]
    public void Optimize_Lookbehind_StillRewritesInsideSafely()
    {
        OptimizationResult result = Pattern.FromRegex(@"(?<=x)(?:a|b|c)").Optimize();

        result.Optimized.ToString().ShouldBe("(?<=x)[abc]");
        result.VerifiedEquivalent.ShouldBeFalse();
    }

    [Fact]
    public void Optimize_PreservesOptionsAndTimeout()
    {
        TimeSpan timeout = TimeSpan.FromSeconds(3);
        Pattern original = Pattern.FromRegex("cat|car", RegexOptions.IgnoreCase).WithTimeout(timeout);

        OptimizationResult result = original.Optimize();

        result.Optimized.ToString().ShouldBe("ca[tr]");
        result.Optimized.Options.ShouldBe(RegexOptions.IgnoreCase);
        result.Optimized.Timeout.ShouldBe(timeout);
        result.Optimized.IsMatch("CAR").ShouldBeTrue();
    }

    [Fact]
    public void Optimize_IgnorePatternWhitespace_RewritesIgnoringLayout()
    {
        OptimizationResult result = Pattern.FromRegex("c a t | c a r", RegexOptions.IgnorePatternWhitespace).Optimize();

        result.Optimized.ToString().ShouldBe("ca[tr]");
        result.VerifiedEquivalent.ShouldBeTrue();
    }

    [Fact]
    public void Optimize_InlineOptionsInsideBranch_LeavesAlternationAlone()
    {
        OptimizationResult result = Pattern.FromRegex("(?i)cat|car").Optimize();

        result.IsChanged.ShouldBeFalse();
    }

    [Fact]
    public void Optimize_LongerRewrite_IsNotApplied()
    {
        OptimizationResult result = Pattern.FromRegex("ab|abc").Optimize();

        result.IsChanged.ShouldBeFalse();
    }

    public static IEnumerable<object[]> CommonPatternFactories() => new (string Name, Func<Pattern> Factory)[]
    {
        ("IPv4", CommonPatterns.IPv4),
        ("Email", CommonPatterns.Email),
        ("Url", CommonPatterns.Url),
        ("Guid", CommonPatterns.Guid),
        ("HexColor", CommonPatterns.HexColor),
    }.Select(x => new object[] { x.Name, x.Factory });

    [Theory]
    [MemberData(nameof(CommonPatternFactories))]
    public void Optimize_CommonPatterns_StayEquivalent(string name, Func<Pattern> factory)
    {
        Pattern original = factory();

        OptimizationResult result = original.Optimize();

        result.VerifiedEquivalent.ShouldBeTrue(name);
        result.Optimized.IsEquivalentTo(original).ShouldBeTrue(name);
        original.GenerateMatching(20, seed: 5).ShouldAllBe(s => result.Optimized.IsMatch(s));
    }
}
