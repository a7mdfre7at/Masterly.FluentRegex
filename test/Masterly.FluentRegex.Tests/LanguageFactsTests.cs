using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class LanguageFactsTests
{
    // ------------------------------------------------------------------
    // Exact facts from the automaton
    // ------------------------------------------------------------------

    [Fact]
    public void GetLanguageFacts_AnchoredInvoiceNumber_ReportsLengthsAndPrefix()
    {
        LanguageFacts facts = Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z").GetLanguageFacts();

        facts.Scope.ShouldBe(MatchScope.FullString);
        facts.MinLength.ShouldBe(13);
        facts.MaxLength.ShouldBe(13);
        facts.RequiredPrefix.ShouldBe("INV-");
        facts.RequiredSuffix.ShouldBe("");
        facts.AcceptsEmpty.ShouldBeFalse();
        facts.IsEmptyLanguage.ShouldBeFalse();
        facts.IsExact.ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"^abc", MatchScope.FullString, 3, 3, "abc", "abc")]
    [InlineData(@"^abc", MatchScope.Substring, 3, null, "abc", "")]
    [InlineData(@"xyz$", MatchScope.FullString, 3, 4, "xyz", "")]
    [InlineData(@"xyz$", MatchScope.Substring, 3, null, "", "")]
    [InlineData(@"xyz\z", MatchScope.Substring, 3, null, "", "xyz")]
    [InlineData(@"^ab?ba\z", MatchScope.FullString, 3, 4, "ab", "ba")]
    [InlineData(@"^a\d*b\z", MatchScope.FullString, 2, null, "a", "b")]
    [InlineData(@"^(?:abc)+\z", MatchScope.FullString, 3, null, "abc", "abc")]
    [InlineData(@"^(?:ab|ac)\z", MatchScope.FullString, 2, 2, "a", "")]
    [InlineData(@"a|b", MatchScope.Substring, 1, null, "", "")]
    [InlineData(@"^(?=.*\d).{8,}$", MatchScope.FullString, 8, null, "", "")]
    [InlineData(@"^(?=.*\d).{8,}$", MatchScope.Substring, 8, null, "", "")]
    [InlineData(@"\d+", MatchScope.FullString, 1, null, "", "")]
    public void GetLanguageFacts_RegularPattern_IsExact(string regex, MatchScope scope, int min, int? max, string prefix, string suffix)
    {
        LanguageFacts facts = Pattern.FromRegex(regex).GetLanguageFacts(scope);

        facts.IsExact.ShouldBeTrue(facts.ToString());
        facts.MinLength.ShouldBe(min);
        facts.MaxLength.ShouldBe(max);
        facts.RequiredPrefix.ShouldBe(prefix);
        facts.RequiredSuffix.ShouldBe(suffix);
    }

    [Fact]
    public void GetLanguageFacts_IgnoreCase_RequiresOnlyCaselessCharacters()
    {
        LanguageFacts facts = Pattern.FromRegex(@"^ab-12\z", RegexOptions.IgnoreCase).GetLanguageFacts();

        facts.RequiredPrefix.ShouldBe("");
        facts.RequiredSuffix.ShouldBe("-12");
        facts.MinLength.ShouldBe(5);
        facts.MaxLength.ShouldBe(5);
    }

    [Fact]
    public void GetLanguageFacts_EmptyLanguage_IsReported()
    {
        LanguageFacts facts = Pattern.FromRegex(@"[^\s\S]").GetLanguageFacts();

        facts.IsEmptyLanguage.ShouldBeTrue();
        facts.MinLength.ShouldBe(0);
        facts.MaxLength.ShouldBe(0);
        facts.AcceptsEmpty.ShouldBeFalse();
        facts.ToString().ShouldBe("FullString: matches nothing (exact)");
    }

    [Theory]
    [InlineData(@"^x*\z", true)]
    [InlineData(@"", true)]
    [InlineData(@"^$", true)]
    [InlineData(@"^x+\z", false)]
    public void GetLanguageFacts_AcceptsEmpty_MatchesTheEngine(string regex, bool expected)
    {
        LanguageFacts facts = Pattern.FromRegex(regex).GetLanguageFacts();

        facts.AcceptsEmpty.ShouldBe(expected);
    }

    [Fact]
    public void GetLanguageFacts_EmptyPatternInSubstringScope_IsUnbounded()
    {
        LanguageFacts facts = Pattern.With.GetLanguageFacts(MatchScope.Substring);

        facts.MinLength.ShouldBe(0);
        facts.MaxLength.ShouldBeNull();
        facts.AcceptsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void ToString_DescribesTheFacts()
    {
        LanguageFacts facts = Pattern.FromRegex(@"^INV-\d{4}\z").GetLanguageFacts();

        facts.ToString().ShouldBe("FullString: length 8..8, prefix \"INV-\", suffix \"\", accepts empty: false (exact)");
    }

    // ------------------------------------------------------------------
    // Sound approximations outside the regular subset
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(@"(\w)\1", MatchScope.FullString, 1, "", "")]
    [InlineData(@"^abc(\w)\1def\z", MatchScope.FullString, 7, "abc", "def")]
    [InlineData(@"^abc(\w)\1def\z", MatchScope.Substring, 7, "abc", "def")]
    [InlineData(@"abc(\w)\1def", MatchScope.Substring, 7, "", "")]
    [InlineData(@"^ab\bc(?<=c)d$", MatchScope.FullString, 4, "abcd", "")]
    [InlineData(@"^ab(?:cd|ce)(?<=e)\z", MatchScope.FullString, 4, "abc", "")]
    [InlineData(@"(?m)^abc$", MatchScope.FullString, 3, "abc", "abc")]
    [InlineData(@"(?m)^abc$", MatchScope.Substring, 3, "", "")]
    [InlineData(@"^a{3}(?=b)b\z", MatchScope.FullString, 4, "aaab", "aaab")]
    public void GetLanguageFacts_NonRegularPattern_IsASoundApproximation(string regex, MatchScope scope, int min, string prefix, string suffix)
    {
        LanguageFacts facts = Pattern.FromRegex(regex).GetLanguageFacts(scope);

        facts.IsExact.ShouldBeFalse();
        facts.MinLength.ShouldBe(min);
        facts.MaxLength.ShouldBeNull();
        facts.RequiredPrefix.ShouldBe(prefix);
        facts.RequiredSuffix.ShouldBe(suffix);
        facts.IsEmptyLanguage.ShouldBeFalse();
    }

    [Theory]
    [InlineData(@"x?(?=a)", MatchScope.FullString, false)]
    [InlineData(@"x?(?=a)", MatchScope.Substring, false)]
    [InlineData(@"(?!a)", MatchScope.Substring, true)]
    [InlineData(@"(a)\1|", MatchScope.FullString, true)]
    public void GetLanguageFacts_NonRegularPattern_AsksTheEngineAboutTheEmptyString(string regex, MatchScope scope, bool expected)
    {
        LanguageFacts facts = Pattern.FromRegex(regex).GetLanguageFacts(scope);

        facts.IsExact.ShouldBeFalse();
        facts.AcceptsEmpty.ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"(a)\1(?:abc$){1}")]
    [InlineData(@"(a)\1(?:abc\Z)+")]
    [InlineData(@"(a)\1(?:x|abc$)")]
    public void GetLanguageFacts_LenientEndAnchorInsideAQuantifierOrGroup_RequiresNoSuffix(string regex)
    {
        Pattern pattern = Pattern.FromRegex(regex);

        LanguageFacts facts = pattern.GetLanguageFacts();

        facts.IsExact.ShouldBeFalse();
        facts.RequiredSuffix.ShouldBe("");
        Regex.IsMatch("aaabc\n", $@"\A(?:{regex})").ShouldBeTrue(); // the anchor leaves the final newline over
    }

    // ------------------------------------------------------------------
    // Free-spacing comments and surrogates
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("^INV-[0-9]{4} # invoice number", RegexOptions.IgnorePatternWhitespace, "INV-1234-X")]
    [InlineData("abc #c", RegexOptions.IgnorePatternWhitespace, "abcd")]
    [InlineData("a(?x) b #c", RegexOptions.None, "abz")]
    [InlineData("(?x) a | b #c", RegexOptions.None, "bz")]
    public void GetLanguageFacts_TrailingFreeSpacingComment_KeepsTheSubstringPadding(string regex, RegexOptions options, string longerMatch)
    {
        Pattern pattern = Pattern.FromRegex(regex, options);

        LanguageFacts facts = pattern.GetLanguageFacts(MatchScope.Substring);

        pattern.IsMatch(longerMatch).ShouldBeTrue();
        facts.MaxLength.ShouldBeNull(facts.ToString());
        facts.RequiredSuffix.ShouldBe("");
        Respects(facts, longerMatch).ShouldBeTrue(facts.ToString());
    }

    [Fact]
    public void GetLanguageFacts_TrailingCommentAfterAnAlternation_DoesNotThrow()
    {
        Pattern pattern = Pattern.FromRegex("a|b #c", RegexOptions.IgnorePatternWhitespace);

        LanguageFacts facts = pattern.GetLanguageFacts(MatchScope.Substring);

        facts.IsExact.ShouldBeTrue();
        facts.MinLength.ShouldBe(1);
        facts.MaxLength.ShouldBeNull();
    }

    [Fact]
    public void GetLanguageFacts_NonRegularPatternWithTrailingComment_AsksTheEngineWithoutThrowing()
    {
        Pattern pattern = Pattern.FromRegex(@"(a)?\1? #c", RegexOptions.IgnorePatternWhitespace);

        LanguageFacts facts = pattern.GetLanguageFacts();

        facts.IsExact.ShouldBeFalse();
        facts.AcceptsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void GetLanguageFacts_ScopedFreeSpacingGroup_KeepsLaterWhitespaceLiteral()
    {
        Pattern pattern = Pattern.FromRegex("^a(?x: b #c\n) c");

        LanguageFacts facts = pattern.GetLanguageFacts(MatchScope.Substring);

        facts.MinLength.ShouldBe(4);
        facts.RequiredPrefix.ShouldBe("ab c");
        facts.MaxLength.ShouldBeNull();
    }

    [Fact]
    public void GetLanguageFacts_BranchesSharingOnlyAHighSurrogate_RequireNoHalfCharacter()
    {
        Pattern pattern = Pattern.FromRegex("^(?:\\uD83D\\uDE00|\\uD83D\\uDE03)x(?:\\uD83D\\uDE00|\\uD83C\\uDE00)\\z");

        LanguageFacts facts = pattern.GetLanguageFacts();

        facts.RequiredPrefix.ShouldBe("");
        facts.RequiredSuffix.ShouldBe("");
        facts.MinLength.ShouldBe(5);
    }

    [Fact]
    public void GetLanguageFacts_WholeSurrogatePairs_StayRequired()
    {
        LanguageFacts facts = Pattern.FromRegex("^\\uD83D\\uDE00ab\\uD83D\\uDE03\\z").GetLanguageFacts();

        facts.RequiredPrefix.ShouldBe("\uD83D\uDE00ab\uD83D\uDE03");
        facts.RequiredSuffix.ShouldBe("\uD83D\uDE00ab\uD83D\uDE03");
    }

    [Fact]
    public void GetLanguageFacts_TooLargeForTheAlgebra_FallsBackInsteadOfThrowing()
    {
        LanguageFacts facts = Pattern.FromRegex(@"^ab[ab]*a[ab]{18}\z").GetLanguageFacts();

        facts.IsExact.ShouldBeFalse();
        facts.RequiredPrefix.ShouldBe("ab");
        facts.MinLength.ShouldBe(21);
    }

    [Fact]
    public void GetLanguageFacts_UndefinedScope_ThrowsArgumentOutOfRange()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Pattern.With.Digit.GetLanguageFacts((MatchScope)7)).ParamName.ShouldBe("scope");
    }

    // ------------------------------------------------------------------
    // Property: generated samples respect the facts
    // ------------------------------------------------------------------

    public static TheoryData<string> FactPatterns()
    {
        TheoryData<string> data = new TheoryData<string>();
        foreach (string name in SqlFlavorTests.CommonPatternNames)
        {
            data.Add(name);
        }

        return data;
    }

    public static TheoryData<string> HandpickedPatterns() => new TheoryData<string>
    {
        @"^INV-\d{4}-\d{4}$", @"^INV-\d{4}-\d{4}\z", @"^abc", @"xyz$", @"xyz\z", @"^aba$", @"^ab?ba\z", @"\d+", @"(?i)^abc\z",
        @"^a.c\z", @"(?s)^a.c\z", @"a|b", @"^(?:ab|ac)\z", @"^x*\z", @"^$", @"^a\d*b\z", @"^(?:abc)+\z", @"^(?=.*\d).{8,}$",
        @"(\w)\1", @"^abc(\w)\1def\z", @"(?m)^abc$", @"\bcat\b", @"^(?!abc)[a-c]{3}\z", @"ab(?<=b)cd", @"(?i)k+\z",
    };

    [Theory]
    [MemberData(nameof(FactPatterns))]
    public void GetLanguageFacts_EveryCommonPattern_HoldsForGeneratedSamples(string name)
    {
        Pattern pattern = SqlFlavorTests.CommonPattern(name);

        AssertFactsHold(pattern);
    }

    [Theory]
    [MemberData(nameof(HandpickedPatterns))]
    public void GetLanguageFacts_HandpickedPattern_HoldsForGeneratedSamples(string regex)
    {
        Pattern pattern = Pattern.FromRegex(regex);

        AssertFactsHold(pattern);
    }

    private static void AssertFactsHold(Pattern pattern)
    {
        LanguageFacts full = pattern.GetLanguageFacts(MatchScope.FullString);
        LanguageFacts substring = pattern.GetLanguageFacts(MatchScope.Substring);
        Regex fullMatch = new Regex($@"\A(?:{pattern})\z", pattern.Options);
        string[] samples = pattern.GenerateMatching(40, seed: 11);
        string[] fullSamples = samples.Where(sample => fullMatch.IsMatch(sample)).ToArray();
        string[] embedded = samples.Select((sample, i) => Junk(i) + sample + Junk(i + 1)).Where(pattern.IsMatch).ToArray();

        fullSamples.ShouldAllBe(sample => Respects(full, sample), full.ToString());
        samples.Where(pattern.IsMatch).ShouldAllBe(sample => Respects(substring, sample), substring.ToString());
        embedded.ShouldAllBe(sample => Respects(substring, sample), substring.ToString());
    }

    private static bool Respects(LanguageFacts facts, string sample) =>
        sample.Length >= facts.MinLength &&
        (facts.MaxLength == null || sample.Length <= facts.MaxLength.Value) &&
        sample.StartsWith(facts.RequiredPrefix, StringComparison.Ordinal) &&
        sample.EndsWith(facts.RequiredSuffix, StringComparison.Ordinal) &&
        (sample.Length > 0 || facts.AcceptsEmpty) &&
        !facts.IsEmptyLanguage;

    private static readonly string[] JunkTexts = { "", "zz", "\n#9 " };

    private static string Junk(int seed) => JunkTexts[seed % JunkTexts.Length];
}
