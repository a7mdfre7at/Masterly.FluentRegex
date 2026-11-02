using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class AlgebraTests
{
    private const char ArabicIndicZero = '٠';

    // ------------------------------------------------------------------
    // Compare
    // ------------------------------------------------------------------

    [Fact]
    public void Compare_UnicodeDigitVersusAsciiRange_FindsNonAsciiDigitCounterexample()
    {
        Pattern unicodeDigits = Pattern.FromRegex(@"\d{3}");
        Pattern asciiDigits = Pattern.FromRegex(@"[0-9]{3}");

        PatternComparison result = Pattern.Compare(unicodeDigits, asciiDigits);

        result.Relation.ShouldBe(PatternRelation.ProperSuperset);
        result.OnlyLeftExample.ShouldNotBeNull();
        result.OnlyLeftExample.ShouldContain(ArabicIndicZero);
        Regex.IsMatch(result.OnlyLeftExample, @"\A\d{3}\z").ShouldBeTrue();
        Regex.IsMatch(result.OnlyLeftExample, @"\A[0-9]{3}\z").ShouldBeFalse();
        result.OnlyRightExample.ShouldBeNull();
        result.CommonExample.ShouldBe("000");
    }

    [Theory]
    [InlineData("a|ab", "ab?")]
    [InlineData("(a|b)*", "[ab]*")]
    [InlineData("a+?", "a+")]
    [InlineData("(?:x)(y)", "xy")]
    [InlineData("a{2,3}", "aaa?")]
    [InlineData(@"[\s\S]", "(?s:.)")]
    [InlineData(".", @"[^\n]")]
    [InlineData("a(?i)b", "a[bB]")]
    [InlineData("(?<n>a)+", "a+")]
    [InlineData(@"\A\d+\z", @"^\d+\z")]
    [InlineData("", "(?:)")]
    [InlineData("[a-c]|d", "[a-d]")]
    [InlineData("(?:ab)+", "a(?:ba)*b")]
    public void Compare_EquivalentForms_AreEquivalent(string left, string right)
    {
        PatternComparison result = Pattern.Compare(Pattern.FromRegex(left), Pattern.FromRegex(right));

        result.Relation.ShouldBe(PatternRelation.Equivalent, result.ToString());
        result.AreEquivalent.ShouldBeTrue();
        result.OnlyLeftExample.ShouldBeNull();
        result.OnlyRightExample.ShouldBeNull();
    }

    [Fact]
    public void Compare_DollarVersusEndOfInput_DollarAlsoAcceptsFinalNewline()
    {
        PatternComparison result = Pattern.Compare(Pattern.FromRegex(@"^\d+$"), Pattern.FromRegex(@"\A\d+\z"));

        result.Relation.ShouldBe(PatternRelation.ProperSuperset);
        result.OnlyLeftExample.ShouldBe("0\n");
        result.CommonExample.ShouldBe("0");
    }

    [Fact]
    public void Compare_StrictEmailVersusLooseEmail_StrictIsProperSubset()
    {
        Pattern strict = CommonPatterns.Email(PatternStrictness.Strict);
        Pattern loose = CommonPatterns.Email(PatternStrictness.Loose);

        PatternComparison result = Pattern.Compare(strict, loose);

        result.Relation.ShouldBe(PatternRelation.ProperSubset, result.ToString());
        result.OnlyRightExample.ShouldBe("a@a.a");
        loose.IsMatch(result.OnlyRightExample).ShouldBeTrue();
        strict.IsMatch(result.OnlyRightExample).ShouldBeFalse();
    }

    [Fact]
    public void Compare_StandardEmailVersusLooseEmail_Overlap()
    {
        PatternComparison result = Pattern.Compare(CommonPatterns.Email(), CommonPatterns.Email(PatternStrictness.Loose));

        result.Relation.ShouldBe(PatternRelation.Overlapping, result.ToString());
        result.OnlyLeftExample.ShouldNotBeNull();
        result.OnlyRightExample.ShouldNotBeNull();
        result.CommonExample.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_BothIgnoreCase_DifferentCaseLiteralsAreEquivalent()
    {
        Pattern lower = Pattern.FromRegex("abc", RegexOptions.IgnoreCase);
        Pattern upper = Pattern.FromRegex("ABC", RegexOptions.IgnoreCase);

        Pattern.Compare(lower, upper).Relation.ShouldBe(PatternRelation.Equivalent);
    }

    [Fact]
    public void Compare_CaseSensitiveVersusIgnoreCase_CaseSensitiveIsProperSubset()
    {
        Pattern sensitive = Pattern.FromRegex("abc");
        Pattern insensitive = Pattern.FromRegex("ABC", RegexOptions.IgnoreCase);

        PatternComparison result = Pattern.Compare(sensitive, insensitive);

        result.Relation.ShouldBe(PatternRelation.ProperSubset);
        result.CommonExample.ShouldBe("abc");
        insensitive.IsMatch(result.OnlyRightExample).ShouldBeTrue();
        result.OnlyRightExample.ShouldNotBe("abc");
    }

    [Fact]
    public void Compare_EcmaScriptDigits_EquivalentToAsciiRange()
    {
        Pattern ecmaDigits = Pattern.FromRegex(@"\d+", RegexOptions.ECMAScript);
        Pattern asciiDigits = Pattern.FromRegex("[0-9]+");

        Pattern.Compare(ecmaDigits, asciiDigits).Relation.ShouldBe(PatternRelation.Equivalent);
    }

    [Fact]
    public void Compare_NoCommonString_IsDisjoint()
    {
        PatternComparison result = Pattern.Compare(Pattern.FromRegex("abc"), Pattern.FromRegex("xyz"));

        result.Relation.ShouldBe(PatternRelation.Disjoint);
        result.OnlyLeftExample.ShouldBe("abc");
        result.OnlyRightExample.ShouldBe("xyz");
        result.CommonExample.ShouldBeNull();
    }

    [Fact]
    public void Compare_PartialOverlap_ReturnsAllThreeShortestExamples()
    {
        PatternComparison result = Pattern.Compare(Pattern.FromRegex("[ab]+"), Pattern.FromRegex("[bc]+"));

        result.Relation.ShouldBe(PatternRelation.Overlapping);
        result.OnlyLeftExample.ShouldBe("a");
        result.OnlyRightExample.ShouldBe("c");
        result.CommonExample.ShouldBe("b");
    }

    [Fact]
    public void Compare_FluentPatternVersusRegex_AreEquivalent()
    {
        Pattern fluent = Pattern.With.Digit.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);

        fluent.IsEquivalentTo(Pattern.FromRegex(@"\d\d\d-\d{4}")).ShouldBeTrue();
    }

    [Fact]
    public void Compare_LeadingLookaheads_AreModeledAsConstraints()
    {
        Pattern password = Pattern.FromRegex(@"^(?=.*\d)(?=.*[a-z]).{8,}$");
        Pattern anyEight = Pattern.FromRegex(@"^.{8,}$");

        PatternComparison result = Pattern.Compare(password, anyEight);

        result.Relation.ShouldBe(PatternRelation.ProperSubset);
        result.OnlyRightExample.ShouldBe("aaaaaaaa");
        result.CommonExample.ShouldBe("aaaaaaa0");
    }

    [Fact]
    public void Compare_LeadingNegativeLookahead_ExcludesItsPrefix()
    {
        PatternComparison result = Pattern.Compare(Pattern.FromRegex(@"^(?!abc)[a-c]{3}\z"), Pattern.FromRegex("[a-c]{3}"));

        result.Relation.ShouldBe(PatternRelation.ProperSubset);
        result.OnlyRightExample.ShouldBe("abc");
    }

    [Fact]
    public void ToString_Equivalent_SaysEquivalent()
    {
        PatternComparison result = Pattern.Compare(Pattern.FromRegex("a|ab"), Pattern.FromRegex("ab?"));

        result.ToString().ShouldStartWith("Equivalent");
    }

    [Fact]
    public void ToString_NotEquivalent_ShowsEscapedShortestCounterexample()
    {
        PatternComparison result = Pattern.Compare(Pattern.FromRegex(@"^\d+$"), Pattern.FromRegex(@"\A\d+\z"));

        result.ToString().ShouldStartWith("Not equivalent (left is a proper superset of right).");
        result.ToString().ShouldContain("Shortest counterexample: \"0\\n\" (left accepts it, right rejects it)");
    }

    // ------------------------------------------------------------------
    // Predicates
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("a|ab", "ab?", true)]
    [InlineData("a+", "a*", false)]
    public void IsEquivalentTo_ReturnsExpected(string left, string right, bool expected)
    {
        Pattern.FromRegex(left).IsEquivalentTo(Pattern.FromRegex(right)).ShouldBe(expected);
    }

    [Theory]
    [InlineData("a+", "a*", true)]
    [InlineData("a*", "a+", false)]
    [InlineData("abc", "abc", true)]
    [InlineData("[0-9]{3}", @"\d{3}", true)]
    public void IsSubsetOf_ReturnsExpected(string left, string right, bool expected)
    {
        Pattern.FromRegex(left).IsSubsetOf(Pattern.FromRegex(right)).ShouldBe(expected);
    }

    [Theory]
    [InlineData("[ab]+", "[bc]+", true)]
    [InlineData("abc", "xyz", false)]
    [InlineData("a*", "b*", true)]
    public void Overlaps_ReturnsExpected(string left, string right, bool expected)
    {
        Pattern.FromRegex(left).Overlaps(Pattern.FromRegex(right)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"[^\s\S]", true)]
    [InlineData(@"^(?!a)a", true)]
    [InlineData(@"a[^\s\S]|b[^\w\W]", true)]
    [InlineData("a", false)]
    [InlineData("a{0}", false)]
    public void MatchesNothing_ReturnsExpected(string regex, bool expected)
    {
        Pattern.FromRegex(regex).MatchesNothing().ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"[a-z]{2,}\d", "aa0")]
    [InlineData("(?:abc)+", "abc")]
    [InlineData("a*", "")]
    [InlineData(@"^\d+$", "0")]
    [InlineData("x|yy|zzz", "x")]
    public void ShortestMatch_ReturnsShortestReadableMember(string regex, string expected)
    {
        Pattern.FromRegex(regex).ShortestMatch().ShouldBe(expected);
    }

    [Fact]
    public void ShortestMatch_EmptyLanguage_ReturnsNull()
    {
        Pattern.FromRegex(@"[^\s\S]+").ShortestMatch().ShouldBeNull();
    }

    // ------------------------------------------------------------------
    // Intersect, Except, Union
    // ------------------------------------------------------------------

    [Fact]
    public void Intersect_LettersAndThreeWordChars_IsThreeLetters()
    {
        Pattern result = Pattern.FromRegex("[a-z]+").Intersect(Pattern.FromRegex(@"\w{3}"));

        result.ToString().ShouldBe(@"^[a-z]{3}\z");
    }

    [Fact]
    public void Except_UnicodeDigitsMinusAscii_KeepsOnlyNonAsciiDigitStrings()
    {
        Pattern result = Pattern.FromRegex(@"\d{3}").Except(Pattern.FromRegex("[0-9]{3}"));

        result.ToString().ShouldContain(@"[\d-[0-9]]");
        result.IsMatch("123").ShouldBeFalse();
        result.IsMatch("12٠").ShouldBeTrue();
    }

    [Fact]
    public void Intersect_EndAnchoredPatterns_KeepsDollarSemantics()
    {
        Pattern result = Pattern.FromRegex(@"^\d+$").Intersect(Pattern.FromRegex(@"^\d{3}$"));

        result.ToString().ShouldBe(@"^\d{3}$");
    }

    [Fact]
    public void Except_DisjointResult_MatchesNothing()
    {
        Pattern result = Pattern.FromRegex("(?:ab)+").Except(Pattern.FromRegex("a(?:ba)*b"));

        result.MatchesNothing().ShouldBeTrue();
    }

    [Theory]
    [InlineData("[a-z]+", "[a-z]*q[a-z]*")]
    [InlineData(@"\w+@\w+", "a.*")]
    [InlineData(@"\d{1,4}", "[0-9]{2}")]
    [InlineData("(?:ab|c)+", "c*")]
    public void Intersect_AgreesWithMembershipOnGeneratedSamples(string leftRegex, string rightRegex)
    {
        Pattern left = Pattern.FromRegex(leftRegex);
        Pattern right = Pattern.FromRegex(rightRegex);

        Pattern result = left.Intersect(right);

        Samples(left, right).ShouldAllBe(s => result.IsMatch(s) == (FullMatch(left, s) && FullMatch(right, s)));
    }

    [Theory]
    [InlineData("[a-z]+", "[a-z]*q[a-z]*")]
    [InlineData(@"\w+@\w+", "a.*")]
    [InlineData(@"\d{1,4}", "[0-9]{2}")]
    [InlineData("(?:ab|c)+", "c*")]
    public void Except_AgreesWithMembershipOnGeneratedSamples(string leftRegex, string rightRegex)
    {
        Pattern left = Pattern.FromRegex(leftRegex);
        Pattern right = Pattern.FromRegex(rightRegex);

        Pattern result = left.Except(right);

        Samples(left, right).ShouldAllBe(s => result.IsMatch(s) == (FullMatch(left, s) && !FullMatch(right, s)));
    }

    [Theory]
    [InlineData("[a-z]+", "[0-9]+")]
    [InlineData("abc", "xyz")]
    public void Union_AgreesWithMembershipOnGeneratedSamples(string leftRegex, string rightRegex)
    {
        Pattern left = Pattern.FromRegex(leftRegex);
        Pattern right = Pattern.FromRegex(rightRegex);

        Pattern result = left.Union(right);

        Samples(left, right).ShouldAllBe(s => FullMatch(result, s) == (FullMatch(left, s) || FullMatch(right, s)));
        left.IsSubsetOf(result).ShouldBeTrue();
        right.IsSubsetOf(result).ShouldBeTrue();
    }

    [Fact]
    public void Union_DifferentOptions_SynthesizesExactLanguage()
    {
        Pattern left = Pattern.FromRegex("abc");
        Pattern right = Pattern.FromRegex("XYZ", RegexOptions.IgnoreCase);

        Pattern result = left.Union(right);

        result.IsMatch("abc").ShouldBeTrue();
        result.IsMatch("xYz").ShouldBeTrue();
        result.IsMatch("ABC").ShouldBeFalse();
    }

    [Fact]
    public void Intersect_PreservesLeftOptionsAndTimeout()
    {
        TimeSpan timeout = TimeSpan.FromSeconds(2);
        Pattern left = Pattern.FromRegex("abc", RegexOptions.IgnoreCase).WithTimeout(timeout);

        Pattern result = left.Intersect(Pattern.FromRegex("[a-c]+", RegexOptions.IgnoreCase));

        result.Options.ShouldBe(RegexOptions.IgnoreCase);
        result.Timeout.ShouldBe(timeout);
        result.ToString().ShouldBe(@"^abc\z");
        result.IsMatch("AbC").ShouldBeTrue();
    }

    [Fact]
    public void Intersect_IgnoreCaseLeftWithCaseSensitiveRight_StaysExact()
    {
        Pattern left = Pattern.FromRegex("[a-z]+", RegexOptions.IgnoreCase);

        Pattern result = left.Intersect(Pattern.FromRegex("[a-z]+"));

        result.IsMatch("abc").ShouldBeTrue();
        result.IsMatch("ABC").ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    // Limits and validation
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(@"(a)\1", "backreferences")]
    [InlineData("a(?=b)", "lookaheads")]
    [InlineData("(?<=a)b", "lookbehinds")]
    [InlineData("(?>a)", "atomic groups")]
    [InlineData(@"\ba", "word boundaries")]
    [InlineData(@"a\B", "non-word boundaries")]
    [InlineData(@"\Ga", @"\G")]
    [InlineData("a^b", "start anchors")]
    [InlineData("a$b", "end anchors")]
    [InlineData("(?:a|)^b", "start anchors")]
    [InlineData("(a)?(?(1)b|c)", "conditionals")]
    [InlineData("(?<o>a)(?<c-o>b)", "balancing groups")]
    public void Compare_NonRegularConstruct_ThrowsNotSupportedNamingConstructAndPosition(string regex, string construct)
    {
        NotSupportedException ex = Should.Throw<NotSupportedException>(() => Pattern.Compare(Pattern.FromRegex(regex), Pattern.FromRegex("a")));

        ex.Message.ShouldContain(construct);
        ex.Message.ShouldContain("position");
    }

    [Theory]
    [InlineData(RegexOptions.Multiline)]
    [InlineData(RegexOptions.RightToLeft)]
    public void Compare_UnsupportedOptions_ThrowNotSupported(RegexOptions options)
    {
        Pattern pattern = Pattern.FromRegex("^a$", options);

        Should.Throw<NotSupportedException>(() => pattern.ShortestMatch());
    }

    [Theory]
    [InlineData("a{1001}")]
    [InlineData("a{2,5000}")]
    [InlineData("((a{100}){100}){100}")]
    [InlineData("[ab]*a[ab]{18}")]
    public void Compare_TooLargeForAlgebra_ThrowsInvalidOperation(string regex)
    {
        Should.Throw<InvalidOperationException>(() => Pattern.FromRegex(regex).IsEquivalentTo(Pattern.FromRegex("a")));
    }

    [Fact]
    public void Compare_NullLeft_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => Pattern.Compare(null, Pattern.FromRegex("a")));
    }

    [Fact]
    public void Compare_NullRight_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => Pattern.Compare(Pattern.FromRegex("a"), null));
    }

    [Theory]
    [MemberData(nameof(BinaryOperations))]
    public void BinaryOperation_NullOther_ThrowsArgumentNull(string name, Action<Pattern, Pattern> operation)
    {
        Pattern pattern = Pattern.FromRegex("a");

        Should.Throw<ArgumentNullException>(() => operation(pattern, null), name);
    }

    public static IEnumerable<object[]> BinaryOperations() => new (string Name, Action<Pattern, Pattern> Operation)[]
    {
        ("IsEquivalentTo", (p, o) => p.IsEquivalentTo(o)),
        ("IsSubsetOf", (p, o) => p.IsSubsetOf(o)),
        ("Overlaps", (p, o) => p.Overlaps(o)),
        ("Intersect", (p, o) => p.Intersect(o)),
        ("Except", (p, o) => p.Except(o)),
        ("Union", (p, o) => p.Union(o)),
    }.Select(x => new object[] { x.Name, x.Operation });

    private static bool FullMatch(Pattern pattern, string input) =>
        Regex.IsMatch(input, $@"\A(?:{pattern})\z", pattern.Options);

    private static string[] Samples(Pattern left, Pattern right) =>
        left.GenerateMatching(30, seed: 1)
            .Concat(right.GenerateMatching(30, seed: 2))
            .Concat(left.GenerateNonMatching(30, seed: 3))
            .Concat(right.GenerateNonMatching(30, seed: 4))
            .Distinct()
            .ToArray();
}
