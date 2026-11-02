using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class GenerationTests
{
    public static IEnumerable<object[]> CommonPatternFactories() => new (string Name, Func<Pattern> Factory)[]
    {
        ("Email", CommonPatterns.Email),
        ("Url", CommonPatterns.Url),
        ("PhoneNumber", CommonPatterns.PhoneNumber),
        ("IPv4", CommonPatterns.IPv4),
        ("IPv6", CommonPatterns.IPv6),
        ("Guid", CommonPatterns.Guid),
        ("HexColor", CommonPatterns.HexColor),
        ("DateIso", CommonPatterns.DateIso),
        ("Time24", CommonPatterns.Time24),
        ("Time12", CommonPatterns.Time12),
        ("CreditCard", CommonPatterns.CreditCard),
        ("ZipCode", CommonPatterns.ZipCode),
        ("Username", CommonPatterns.Username),
        ("StrongPassword", CommonPatterns.StrongPassword),
        ("FilePath", CommonPatterns.FilePath),
        ("HtmlTag", CommonPatterns.HtmlTag),
        ("Decimal", CommonPatterns.Decimal),
        ("ScientificNotation", CommonPatterns.ScientificNotation),
    }.Select(x => new object[] { x.Name, x.Factory });

    [Theory]
    [MemberData(nameof(CommonPatternFactories))]
    public void GenerateMatching_ProducesOnlyMatchingDistinctStrings(string name, Func<Pattern> factory)
    {
        Pattern pattern = factory();
        string[] samples = pattern.GenerateMatching(20, seed: 1);

        samples.Length.ShouldBe(20, name);
        samples.Distinct().Count().ShouldBe(samples.Length);
        foreach (string sample in samples) pattern.IsMatch(sample).ShouldBeTrue($"{name}: {sample}");
    }

    [Theory]
    [MemberData(nameof(CommonPatternFactories))]
    public void GenerateNonMatching_ProducesOnlyNonMatchingStrings(string name, Func<Pattern> factory)
    {
        Pattern pattern = factory();
        string[] samples = pattern.GenerateNonMatching(20, seed: 1);

        samples.Length.ShouldBe(20, name);
        foreach (string sample in samples) pattern.IsMatch(sample).ShouldBeFalse($"{name}: {sample}");
    }

    [Fact]
    public void Seed_MakesOutputReproducible()
    {
        Pattern pattern = CommonPatterns.IPv4();
        pattern.GenerateMatching(10, seed: 99).ShouldBe(pattern.GenerateMatching(10, seed: 99));
        pattern.GenerateNonMatching(10, seed: 99).ShouldBe(pattern.GenerateNonMatching(10, seed: 99));
    }

    [Fact]
    public void NonMatching_AreMostlyNearMisses()
    {
        string[] samples = CommonPatterns.DateIso().GenerateNonMatching(20, seed: 3);

        // Near misses stay close to the 10-character shape of a real ISO date.
        samples.Count(s => s.Length >= 8 && s.Length <= 12).ShouldBeGreaterThan(10);
    }

    [Fact]
    public void Backreferences_AreHonored()
    {
        Pattern doubled = Pattern.With.StartOfLine
            .NamedGroup("w", Pattern.With.Letter.Repeat.Times(2, 5))
            .Literal("-")
            .Backreference("w")
            .EndOfLine;

        foreach (string sample in doubled.GenerateMatching(15, seed: 5))
        {
            string[] parts = sample.Split('-');
            parts[0].ShouldBe(parts[1]);
        }
    }

    [Fact]
    public void Lookarounds_AreSatisfiedThroughVerification()
    {
        string[] samples = CommonPatterns.StrongPassword().GenerateMatching(10, seed: 11);
        samples.Length.ShouldBe(10);
        foreach (string s in samples)
        {
            s.Any(char.IsUpper).ShouldBeTrue();
            s.Any(char.IsDigit).ShouldBeTrue();
        }
    }

    [Fact]
    public void Options_AreRespected()
    {
        Pattern pattern = Pattern.With.StartOfLine.Literal("abc").EndOfLine.WithOptions(RegexOptions.IgnoreCase);
        foreach (string sample in pattern.GenerateMatching(5, seed: 2))
            sample.ToLowerInvariant().ShouldBe("abc");
    }

    [Fact]
    public void FiniteLanguage_ReturnsFewerSamples()
    {
        Pattern pattern = Pattern.With.StartOfLine.OneOf("yes", "no").EndOfLine;
        string[] samples = pattern.GenerateMatching(10, seed: 1);
        samples.OrderBy(s => s).ShouldBe(new[] { "no", "yes" });
    }

    [Fact]
    public void ImpossiblePattern_ReturnsEmpty()
    {
        Pattern.With.NegatedSet(Pattern.With.Whitespace.NonWhitespace).GenerateMatching(3, seed: 1).ShouldBeEmpty();
    }

    [Fact]
    public void MatchEverything_HasNoNonMatchingSamples()
    {
        Pattern.With.Anything.Repeat.ZeroOrMore.GenerateNonMatching(5, seed: 1).ShouldBeEmpty();
    }

    [Fact]
    public void Count_IsValidated()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Pattern.With.Digit.GenerateMatching(-1)).ParamName.ShouldBe("count");
        Pattern.With.Digit.GenerateMatching(0).ShouldBeEmpty();
    }

    [Fact]
    public void GenerateNonMatching_NegativeCount_ThrowsArgumentOutOfRange()
    {
        ArgumentOutOfRangeException exception = Should.Throw<ArgumentOutOfRangeException>(() => Pattern.With.Digit.GenerateNonMatching(-1));

        exception.ParamName.ShouldBe("count");
    }

    [Fact]
    public void GenerateNonMatching_ZeroCount_ReturnsEmpty()
    {
        string[] samples = Pattern.With.Digit.GenerateNonMatching(0, seed: 1);

        samples.ShouldBeEmpty();
    }

    [Fact]
    public void GenerateMatching_NoSeed_StillReturnsEveryDistinctMatchOfFiniteLanguage()
    {
        Pattern pattern = Pattern.FromRegex("^(?:a|b)$");

        string[] samples = pattern.GenerateMatching(5);

        samples.OrderBy(s => s, StringComparer.Ordinal).ShouldBe(new[] { "a", "b" });
    }

    [Fact]
    public void GenerateNonMatching_NoSeed_ReturnsOnlyNonMatchingStrings()
    {
        Pattern pattern = Pattern.FromRegex("^a$");

        string[] samples = pattern.GenerateNonMatching(3);

        samples.Length.ShouldBe(3);
        samples.ShouldAllBe(s => !pattern.IsMatch(s));
    }

    [Fact]
    public void GenerateMatching_IgnoreCaseLiterals_ProducesEveryCaseVariant()
    {
        Pattern pattern = Pattern.FromRegex("(?i)^Ab$");

        string[] samples = pattern.GenerateMatching(10, seed: 1);

        samples.OrderBy(s => s, StringComparer.Ordinal).ShouldBe(new[] { "AB", "Ab", "aB", "ab" });
    }

    [Fact]
    public void GenerateMatching_BranchWithEmptyCharacterClass_UsesOtherBranches()
    {
        Pattern pattern = Pattern.FromRegex("^(?:[a-[a]]|b)$");

        string[] samples = pattern.GenerateMatching(5, seed: 1);

        samples.ShouldBe(new[] { "b" });
    }

    [Fact]
    public void GenerateMatching_BranchLongerThanSampleLimit_UsesOtherBranches()
    {
        Pattern pattern = Pattern.FromRegex("^(?:a{3000}|b)$");

        string[] samples = pattern.GenerateMatching(3, seed: 1);

        samples.ShouldBe(new[] { "b" });
    }

    [Fact]
    public void GenerateMatching_OnlyMatchesLongerThanSampleLimit_ReturnsEmpty()
    {
        string[] samples = Pattern.FromRegex("^a{2001}$").GenerateMatching(1, seed: 1);

        samples.ShouldBeEmpty();
    }

    [Fact]
    public void GenerateMatching_MatchExactlyAtSampleLimit_ReturnsIt()
    {
        string[] samples = Pattern.FromRegex("^a{2000}$").GenerateMatching(1, seed: 1);

        samples.ShouldBe(new[] { new string('a', 2000) });
    }

    [Fact]
    public void GenerateMatching_ExpressionConditionalWithoutNoBranch_ProducesBothOutcomes()
    {
        Pattern pattern = Pattern.FromRegex("^(?(?=x)xy)$");

        string[] samples = pattern.GenerateMatching(5, seed: 1);

        samples.OrderBy(s => s, StringComparer.Ordinal).ShouldBe(new[] { string.Empty, "xy" });
    }

    [Fact]
    public void GenerateMatching_ExpressionConditionalWithNoBranch_ProducesBothBranches()
    {
        Pattern pattern = Pattern.FromRegex(@"^(?(\d)\dx|y)$");

        string[] samples = pattern.GenerateMatching(5, seed: 1);

        samples.ShouldContain("y");
        samples.ShouldContain(s => s.Length == 2 && char.IsDigit(s[0]) && s[1] == 'x');
        samples.ShouldAllBe(s => pattern.IsMatch(s));
    }

    [Fact]
    public void GenerateMatching_ConditionalOnUndefinedName_ReturnsOnlyVerifiedMatches()
    {
        Pattern pattern = Pattern.FromRegex("^(?(foo)foo|b)$");

        string[] samples = pattern.GenerateMatching(5, seed: 1);

        samples.ShouldContain("b");
        samples.ShouldAllBe(s => pattern.IsMatch(s));
    }

    [Fact]
    public void GenerateMatching_VerificationTimesOut_SkipsUndecidableCandidateWithoutThrowing()
    {
        // The negative lookahead backtracks catastrophically on a run of a's, so verifying the only
        // candidate exceeds the generator's one-second budget; the candidate is dropped, not returned.
        Pattern pattern = Pattern.FromRegex("^(?!((a+)+)+b)a{30}$");

        string[] samples = pattern.GenerateMatching(1, seed: 1);

        samples.ShouldBeEmpty();
    }

    [Fact]
    public void GenerateNonMatching_VerificationTimesOut_SkipsUndecidableCandidate()
    {
        // With this seed the first near miss is thirty a's. Only on such all-a strings does the negative lookahead
        // run, and it backtracks catastrophically past the generator's one-second budget, so that candidate's
        // membership is unknown: it must be skipped rather than reported as non-matching.
        Pattern pattern = Pattern.FromRegex("^(?:a{30}x|(?=a+$)(?!((a+)+)+b)a*y|q)$");

        string[] samples = pattern.GenerateNonMatching(1, seed: 171);

        samples.ShouldBe(new[] { new string('a', 29) + "x" });
    }
}
