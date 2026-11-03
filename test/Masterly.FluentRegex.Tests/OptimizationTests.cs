using System.Text.RegularExpressions;
using Masterly.FluentRegex.Optimization;
using Masterly.FluentRegex.Syntax;

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

    [Theory]
    [InlineData("(?:)x", "x")]
    [InlineData("x(?:)", "x")]
    [InlineData("1(?:ab)", "1ab")]
    [InlineData("(?:ab),", "ab,")]
    [InlineData("(?:a)+", "a+")]
    [InlineData("(?:(?:a))", "a")]
    [InlineData("(?:a|b)|c", "[abc]")]
    [InlineData("((?:a|b))", "([ab])")]
    [InlineData("x(?:a|b)", "x[ab]")]
    [InlineData("(?:a|b)+", "[ab]+")]
    [InlineData("(?:a|bc)", "a|bc")]
    [InlineData("((?:a|bc))", "(a|bc)")]
    public void Optimize_RedundantNonCapturingGroup_IsUnwrapped(string regex, string expected)
    {
        OptimizationResult result = Pattern.FromRegex(regex).Optimize();

        result.Optimized.ToString().ShouldBe(expected);
        result.AppliedRewrites.ShouldContain(rewrite => rewrite.StartsWith("Unwrapped redundant group"));
        result.VerifiedEquivalent.ShouldBeTrue();
        result.Optimized.IsEquivalentTo(result.Original).ShouldBeTrue();
    }

    [Fact]
    public void Optimize_NestedRedundantGroups_UnwrapsOuterGroupFirst()
    {
        OptimizationResult result = Pattern.FromRegex("(?:(?:a))").Optimize();

        result.AppliedRewrites.ShouldBe(new[]
        {
            "Unwrapped redundant group '(?:(?:a))'",
            "Unwrapped redundant group '(?:a)'"
        });
    }

    [Theory]
    [InlineData(@"(a)\1(?:0)")]
    [InlineData("a(?:{)2}")]
    [InlineData("x{(?:2})")]
    [InlineData(@"(a)\1(?:)0")]
    [InlineData(@"(a)(?:b\1)0")]
    public void Optimize_GroupWhoseRemovalWouldFuseWithNeighbours_IsKept(string regex)
    {
        OptimizationResult result = Pattern.FromRegex(regex).Optimize();

        result.IsChanged.ShouldBeFalse();
        result.Optimized.ToString().ShouldBe(regex);
    }

    [Theory]
    [InlineData("x(a)|x(a)")]
    [InlineData("(?<a>x)(?:(?<-a>y)|(?<-a>y))")]
    public void Optimize_DuplicateBranchContainingCapture_IsKept(string regex)
    {
        OptimizationResult result = Pattern.FromRegex(regex).Optimize();

        result.IsChanged.ShouldBeFalse();
        result.Optimized.ToString().ShouldBe(regex);
    }

    [Theory]
    [InlineData(".|a")]
    [InlineData("[^a]|b")]
    [InlineData("[a-z-[aeiou]]|0")]
    [InlineData("[a-]|b")]
    [InlineData("ab|a$")]
    public void Optimize_BranchesThatCannotFormAnExactClass_AreKept(string regex)
    {
        OptimizationResult result = Pattern.FromRegex(regex).Optimize();

        result.IsChanged.ShouldBeFalse();
        result.Optimized.ToString().ShouldBe(regex);
    }

    [Theory]
    [InlineData("(?:^)a")]
    [InlineData("a{0,0}b")]
    [InlineData("(?<o>a)(?<-o>b{1})")]
    [InlineData("a(?i)|a(?i)")]
    public void Optimize_ConstructOutsideRewriteRules_IsLeftAlone(string regex)
    {
        OptimizationResult result = Pattern.FromRegex(regex).Optimize();

        result.IsChanged.ShouldBeFalse();
        result.Optimized.ToString().ShouldBe(regex);
    }

    [Theory]
    [InlineData(@"(a)\1(?:)0")]
    [InlineData(@"(a)(?:b\1)0")]
    [InlineData("x{(?:2})")]
    [InlineData("a(?:{)2}")]
    [InlineData("(?:ab)+")]
    [InlineData("(?:a(?i))b")]
    [InlineData("x(a)|x(a)")]
    [InlineData("a(?i)|a(?i)")]
    [InlineData(".|a")]
    [InlineData("[^a]|b")]
    [InlineData("[a-z-[aeiou]]|0")]
    [InlineData("[a-]|b")]
    public void RewriteFinder_Find_RewriteThatWouldChangeMeaning_IsNotOffered(string regex)
    {
        // The optimizer re-verifies every candidate; this checks the rewrite rules themselves never propose these.
        RegexTree tree = RegexParser.Parse(regex);

        IReadOnlyList<RegexRewrite> rewrites = RewriteFinder.Find(tree);

        rewrites.ShouldBeEmpty();
    }

    [Fact]
    public void Optimize_Conditional_RewritesOutsideButNotInsideIt()
    {
        OptimizationResult result = Pattern.FromRegex("x{1}(?(y)a{1}|b)").Optimize();

        result.Optimized.ToString().ShouldBe("x(?(y)a{1}|b)");
        result.AppliedRewrites.ShouldBe(new[] { "Removed redundant quantifier '{1}' from 'x{1}'" });
    }

    [Theory]
    [InlineData("[ab]|c", "[abc]")]
    [InlineData(@"\d|x", @"[\dx]")]
    [InlineData(@"\p{L}|0", @"[\p{L}0]")]
    [InlineData(@"ab|a\p{L}", @"a[b\p{L}]")]
    public void Optimize_ClassBranches_MergeIntoOneClass(string regex, string expected)
    {
        OptimizationResult result = Pattern.FromRegex(regex).Optimize();

        result.Optimized.ToString().ShouldBe(expected);
        result.VerifiedEquivalent.ShouldBeTrue();
        result.Optimized.IsEquivalentTo(result.Original).ShouldBeTrue();
    }

    [Fact]
    public void Optimize_CommonPrefixWithMultiCharacterRemainders_FactorsThePrefix()
    {
        OptimizationResult result = Pattern.FromRegex("abcdx1|abcdy2").Optimize();

        result.Optimized.ToString().ShouldBe("abcd(?:x1|y2)");
        result.AppliedRewrites.ShouldBe(new[]
        {
            "Factored common prefix 'abcd' out of 'abcdx1|abcdy2' into 'abcd(?:x1|y2)'"
        });
        result.VerifiedEquivalent.ShouldBeTrue();
    }

    [Theory]
    [InlineData("(?:a#c\n)b", "ab")]
    [InlineData("a|a#c\n|b", "[ab]")]
    [InlineData("x#c\n|a|b", "[xab]")]
    public void Optimize_IgnorePatternWhitespaceWithComments_DropsTheComments(string regex, string expected)
    {
        Pattern original = Pattern.FromRegex(regex, RegexOptions.IgnorePatternWhitespace);

        OptimizationResult result = original.Optimize();

        result.Optimized.ToString().ShouldBe(expected);
        result.Optimized.Options.ShouldBe(RegexOptions.IgnorePatternWhitespace);
        result.VerifiedEquivalent.ShouldBeTrue();
    }

    [Fact]
    public void Optimize_RightToLeft_AppliesSampleCheckedRewritesWithoutProof()
    {
        OptimizationResult result = Pattern.FromRegex("a|b", RegexOptions.RightToLeft).Optimize();

        result.Optimized.ToString().ShouldBe("[ab]");
        result.Optimized.Options.ShouldBe(RegexOptions.RightToLeft);
        result.VerifiedEquivalent.ShouldBeFalse();
    }

    [Fact]
    public void Optimize_PatternTooLargeForLanguageAlgebra_AppliesRewritesWithoutProof()
    {
        OptimizationResult result = Pattern.FromRegex("x{1}a{2000}").Optimize();

        result.Optimized.ToString().ShouldBe("xa{2000}");
        result.AppliedRewrites.ShouldBe(new[] { "Removed redundant quantifier '{1}' from 'x{1}'" });
        result.VerifiedEquivalent.ShouldBeFalse();
        result.ToString().ShouldContain("checked on samples, not proven");
    }

    [Fact]
    public void Optimize_MergeBeyondLanguageAlgebraRepeatLimit_IsRejected()
    {
        Pattern original = Pattern.FromRegex("a{999}a{2}");

        OptimizationResult result = original.Optimize();

        result.Optimized.ShouldBeSameAs(original);
        result.IsChanged.ShouldBeFalse();
        result.VerifiedEquivalent.ShouldBeTrue();
    }

    [Fact]
    public void Optimize_UnprovableMergeOfferedAgainInLaterRound_AppliesOnlyTheSafeRewrite()
    {
        OptimizationResult result = Pattern.FromRegex("a{999}a{1}a").Optimize();

        result.Optimized.ToString().ShouldBe("a{999}aa");
        result.AppliedRewrites.ShouldBe(new[] { "Removed redundant quantifier '{1}' from 'a{1}'" });
        result.VerifiedEquivalent.ShouldBeTrue();
    }

    [Fact]
    public void Optimize_RewriteThatChangesMatches_IsRejected()
    {
        // Dropping '{1}' would turn the backreference '\1' and the following '0' into the octal escape '\10'.
        Pattern original = Pattern.FromRegex(@"(a)\1{1}0");

        OptimizationResult result = original.Optimize();

        result.Optimized.ShouldBeSameAs(original);
        result.IsChanged.ShouldBeFalse();
        result.Optimized.IsMatch("aa0").ShouldBeTrue();
    }

    [Fact]
    public void Optimize_MoreThanHundredRewritesAvailable_StopsAfterHundred()
    {
        string regex = string.Concat(Enumerable.Repeat("a{1}b{1}", 51));

        OptimizationResult result = Pattern.FromRegex(regex).Optimize();

        result.AppliedRewrites.Count.ShouldBe(100);
        result.Optimized.ToString().ShouldBe($"{string.Concat(Enumerable.Repeat("ab", 50))}a{{1}}b{{1}}");
    }

    [Fact]
    public void Optimize_VerificationBudgetRunsOutBetweenRounds_KeepsRewritesAppliedSoFar()
    {
        // Each round re-verifies four rejected '\1{1}0' rewrites before accepting one '{1}' removal:
        // five verifications per round exhaust the budget of 400 after exactly 80 rounds.
        string rejected = string.Concat(Enumerable.Repeat(@"\1{1}0", 4));
        string regex = $"(a){rejected}{string.Concat(Enumerable.Repeat("b{1}c{1}", 50))}";

        OptimizationResult result = Pattern.FromRegex(regex).Optimize();

        result.AppliedRewrites.Count.ShouldBe(80);
        result.Optimized.ToString().ShouldBe(
            $"(a){rejected}{string.Concat(Enumerable.Repeat("bc", 40))}{string.Concat(Enumerable.Repeat("b{1}c{1}", 10))}");
    }

    [Fact]
    public void Optimize_VerificationBudgetRunsOutMidRound_KeepsRewritesAppliedSoFar()
    {
        // Seven verifications per round: the budget of 400 runs out inside the 58th round.
        string rejected = string.Concat(Enumerable.Repeat(@"\1{1}0", 6));
        string regex = $"(a){rejected}{string.Concat(Enumerable.Repeat("b{1}c{1}", 40))}";

        OptimizationResult result = Pattern.FromRegex(regex).Optimize();

        result.AppliedRewrites.Count.ShouldBe(57);
        result.Optimized.ToString().ShouldBe(
            $"(a){rejected}{string.Concat(Enumerable.Repeat("bc", 28))}bc{{1}}{string.Concat(Enumerable.Repeat("b{1}c{1}", 11))}");
    }

    [Fact]
    public void MatchBehaviorChecker_Inputs_IncludeBlanksAndMutationsOfMatchingSamples()
    {
        MatchBehaviorChecker checker = new MatchBehaviorChecker(Pattern.FromRegex("ab"));

        IReadOnlyList<string> inputs = checker.Inputs;

        new[] { string.Empty, " ", "\n", "ab", "xabx", "abab", "ab\n", "AB", "b", "a" }.ShouldBeSubsetOf(inputs);
        inputs.ShouldBeUnique();
    }

    [Theory]
    [InlineData("ab", "ab", true)]
    [InlineData("ab", "(?:a)b", true)]
    [InlineData("ab", "(", false)]
    [InlineData("(a)b", "ab", false)]
    [InlineData("(?<x>a)b", "(?<y>a)b", false)]
    [InlineData("ab", "zz", false)]
    [InlineData("ab", "a", false)]
    [InlineData("(a)+", "a*(a)", false)]
    [InlineData("(?<g>a)x?(?<g>b)", "(?<g>ax?)(?<g>b)", false)]
    [InlineData("(?<g>a)xy(?<g>b)", "a(?<g>x)y(?<g>b)", false)]
    public void MatchBehaviorChecker_BehavesLikeOriginal_ComparesMatchesGroupsAndCaptures(string original, string candidate, bool expected)
    {
        MatchBehaviorChecker checker = new MatchBehaviorChecker(Pattern.FromRegex(original));

        bool behavesAlike = checker.BehavesLikeOriginal(candidate);

        behavesAlike.ShouldBe(expected);
    }

    [Fact]
    public void MatchBehaviorChecker_BehavesLikeOriginal_DifferenceAfterTwentiethMatch_IsNotCompared()
    {
        // On the sample "XXX...X" (25 characters) the original finds 25 one-character matches; the candidate
        // stops after 20. Only the first 20 matches of an input are compared, so the two count as alike.
        MatchBehaviorChecker checker = new MatchBehaviorChecker(Pattern.FromRegex("x{25}|."));

        bool behavesAlike = checker.BehavesLikeOriginal("x{25}|(?<![^x]{20}).");

        behavesAlike.ShouldBeTrue();
    }

    [Fact]
    public void MatchBehaviorChecker_BehavesLikeOriginal_CandidateTimesOut_ReturnsFalse()
    {
        // The candidate accepts the same strings, but backtracks exponentially on a long run of 'a' without 'b'.
        MatchBehaviorChecker checker = new MatchBehaviorChecker(Pattern.FromRegex("a{60,}b"));

        bool behavesAlike = checker.BehavesLikeOriginal("(?=a{60})(?:a|aa)*b");

        behavesAlike.ShouldBeFalse();
    }
}
