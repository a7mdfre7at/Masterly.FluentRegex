using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class ConstraintTests
{
    private static readonly ConstrainedPattern Card = CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");

    private static readonly ConstrainedPattern IsoDate = CommonPatterns.DateIso().Must(Validators.IsoDate, "real calendar date");

    private static readonly Pattern Age = Pattern.With.NamedGroup("age", Pattern.With.Digit.Repeat.Times(1, 3));

    // ------------------------------------------------------------------
    // ConstrainedPattern
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("4111-1111-1111-1111", true)]
    [InlineData("4111111111111111", true)]
    [InlineData("4111 1111 1111 1112", false)]
    [InlineData("4111 1111 1111", false)]
    public void IsMatch_CreditCardWithLuhn_AcceptsOnlyValidChecksums(string input, bool expected)
    {
        bool result = Card.IsMatch(input);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("2024-02-29", true)]
    [InlineData("2023-02-29", false)]
    [InlineData("2026-02-30", false)]
    [InlineData("2026-12-31", true)]
    public void Check_IsoDateWithCalendarRule_RejectsImpossibleDates(string input, bool expected)
    {
        ConstraintResult result = IsoDate.Check(input);

        result.IsValid.ShouldBe(expected);
    }

    [Fact]
    public void Check_ValidValue_IsValidWithoutDiagnostics()
    {
        ConstraintResult result = Card.Check("4111 1111 1111 1111");

        result.IsValid.ShouldBeTrue();
        result.FailedRule.ShouldBeNull();
        result.Trace.ShouldBeNull();
        result.PatternFailed.ShouldBeFalse();
        result.ToString().ShouldBe("Valid");
    }

    [Fact]
    public void Check_RuleRejectsValue_ReportsRuleDescription()
    {
        ConstraintResult result = Card.Check("4111 1111 1111 1112");

        result.IsValid.ShouldBeFalse();
        result.FailedRule.ShouldBe("Luhn checksum");
        result.PatternFailed.ShouldBeFalse();
        result.Trace.ShouldBeNull();
    }

    [Fact]
    public void Check_PatternRejectsValue_ReportsTraceOfFailure()
    {
        ConstraintResult result = Card.Check("4111 1111 11x1 1111");

        result.IsValid.ShouldBeFalse();
        result.FailedRule.ShouldBeNull();
        result.PatternFailed.ShouldBeTrue();
        result.Trace.ShouldNotBeNull();
        result.Trace!.IsMatch.ShouldBeFalse();
        result.Trace.Failure.ShouldNotBeNull();
        result.Trace.Failure!.Position.ShouldBe(4);
    }

    [Fact]
    public void Check_ToString_NeverContainsTheValidatedInput()
    {
        string patternFailure = Card.Check("4111 1111 11x1 1111").ToString();
        string ruleFailure = Card.Check("4111 1111 1111 1112").ToString();

        patternFailure.ShouldStartWith("Invalid: the value does not match the pattern at position 4");
        patternFailure.ShouldNotContain("4111");
        ruleFailure.ShouldBe("Invalid: the rule 'Luhn checksum' rejected the value");
    }

    [Fact]
    public void Check_UnanchoredPattern_RequiresTheWholeInputToMatch()
    {
        ConstrainedPattern threeDigits = Pattern.With.Digit.Repeat.Times(3).Must(_ => true);

        bool search = threeDigits.IsMatch("1234");
        ConstraintResult whole = threeDigits.Check("1234");

        search.ShouldBeTrue();
        whole.IsValid.ShouldBeFalse();
        whole.PatternFailed.ShouldBeTrue();
    }

    [Fact]
    public void Check_AlternationWithShorterFirstBranch_FindsTheFullLengthAlternative()
    {
        ConstrainedPattern pattern = Pattern.With.OneOf(Pattern.With.Literal("a"), Pattern.With.Literal("ab")).Must(_ => true);

        ConstraintResult result = pattern.Check("ab");

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("123", true)]
    [InlineData("123a", false)]
    public void Check_TrailingWhitespaceModeComment_StillValidatesTheWholeInput(string input, bool expected)
    {
        ConstrainedPattern digits = Pattern.FromRegex(@"\d+ # digits only", RegexOptions.IgnorePatternWhitespace).Must(_ => true);

        bool result = digits.Check(input).IsValid;

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("4111 1111 1111 1112", false)]
    [InlineData("x4111 1111 1111 1111", false)]
    public void IsValid_MatchesCheckWithoutDiagnostics(string input, bool expected)
    {
        bool result = Card.IsValid(input);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("age 21", true)]
    [InlineData("age 17", false)]
    public void Must_GroupRule_EvaluatesTheGroupValue(string input, bool expected)
    {
        ConstrainedPattern adult = Age.Must("age", age => int.Parse(age) >= 18, "at least 18");

        bool result = adult.IsMatch(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Must_GroupDidNotParticipate_SkipsTheRule()
    {
        Pattern optionalSuffix = Pattern.With.Literal("id").NamedGroup("suffix", Pattern.With.Digit).Repeat.Optional;
        ConstrainedPattern pattern = optionalSuffix.Must("suffix", _ => false, "never");

        ConstraintResult withoutSuffix = pattern.Check("id");
        ConstraintResult withSuffix = pattern.Check("id7");

        withoutSuffix.IsValid.ShouldBeTrue();
        withSuffix.FailedRule.ShouldBe("never");
    }

    [Fact]
    public void Must_UnknownGroup_ThrowsArgumentException()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => Age.Must("year", _ => true));

        exception.ParamName.ShouldBe("groupName");
        exception.Message.ShouldContain("'year'");
    }

    [Fact]
    public void Must_NullArguments_ThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Age.Must((Func<string, bool>)null!)).ParamName.ShouldBe("rule");
        Should.Throw<ArgumentNullException>(() => Age.Must(null!, _ => true)).ParamName.ShouldBe("groupName");
        Should.Throw<ArgumentNullException>(() => Age.Must("age", null!)).ParamName.ShouldBe("rule");
    }

    [Fact]
    public void Must_IsImmutableAndChainable()
    {
        ConstrainedPattern first = Age.Must(_ => true, "first");

        ConstrainedPattern second = first.Must("age", _ => true);

        first.RuleDescriptions.ShouldBe(new[] { "first" });
        second.RuleDescriptions.ShouldBe(new[] { "first", "rule 2 on group 'age'" });
        second.Pattern.ShouldBeSameAs(Age);
    }

    [Fact]
    public void Must_WithoutDescription_NumbersTheRule()
    {
        ConstrainedPattern pattern = Age.Must(_ => false);

        ConstraintResult result = pattern.Check("5");

        result.FailedRule.ShouldBe("rule 1");
    }

    [Fact]
    public void Check_SeveralRules_ReportsTheFirstThatFails()
    {
        ConstrainedPattern pattern = Age.Must(_ => true, "passes").Must(_ => false, "fails").Must(_ => false, "also fails");

        ConstraintResult result = pattern.Check("5");

        result.FailedRule.ShouldBe("fails");
    }

    [Fact]
    public void Matches_RejectedCandidate_ResumesAtNextIndexToFindOverlappingMatch()
    {
        ConstrainedPattern notStartingWithOne = Pattern.With.Digit.Repeat.Times(3).Must(value => value[0] != '1');

        IEnumerable<Match> matches = notStartingWithOne.Matches("1234");

        matches.Select(m => m.Value).ShouldBe(new[] { "234" });
    }

    [Fact]
    public void Matches_AcceptedMatches_DoNotOverlap()
    {
        ConstrainedPattern triples = Pattern.With.Digit.Repeat.Times(3).Must(_ => true);

        IEnumerable<Match> matches = triples.Matches("1234567");

        matches.Select(m => m.Value).ShouldBe(new[] { "123", "456" });
    }

    [Fact]
    public void Matches_CardsInText_ReturnsOnlyLuhnValidNumbers()
    {
        ConstrainedPattern card = Pattern.With.Digit.Repeat.Times(16).Must(Validators.Luhn);

        IEnumerable<Match> matches = card.Matches("4111111111111112 4111111111111111");

        matches.Select(m => m.Index).ShouldBe(new[] { 17 });
    }

    [Fact]
    public void Matches_EmptyMatches_AdvanceThroughTheInput()
    {
        ConstrainedPattern empty = Pattern.With.Digit.Repeat.ZeroOrMore.Must(_ => true);

        IEnumerable<Match> matches = empty.Matches("ab");

        matches.Select(m => m.Index).ShouldBe(new[] { 0, 1, 2 });
    }

    [Fact]
    public void Matches_RightToLeftPattern_FiltersTheRegularMatches()
    {
        ConstrainedPattern pattern = Pattern.With.Digit.Repeat.Times(2).WithOptions(RegexOptions.RightToLeft).Must(value => value != "34");

        IEnumerable<Match> matches = pattern.Matches("1234");

        matches.Select(m => m.Value).ShouldBe(new[] { "12" });
    }

    [Fact]
    public void Match_NoCandidateSatisfiesTheRules_ReturnsNull()
    {
        Match? match = Card.Match("4111 1111 1111 1112");

        match.ShouldBeNull();
    }

    [Fact]
    public void Match_ValidCandidate_ReturnsIt()
    {
        Match? match = Pattern.With.Digit.Repeat.Times(16).Must(Validators.Luhn).Match("card: 4111111111111111.");

        match.ShouldNotBeNull();
        match!.Value.ShouldBe("4111111111111111");
    }

    [Fact]
    public void Members_NullInput_ThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Card.Matches(null!)).ParamName.ShouldBe("input");
        Should.Throw<ArgumentNullException>(() => Card.IsMatch(null!)).ParamName.ShouldBe("input");
        Should.Throw<ArgumentNullException>(() => Card.Check(null!)).ParamName.ShouldBe("input");
        Should.Throw<ArgumentNullException>(() => Card.IsValid(null!)).ParamName.ShouldBe("input");
    }

    [Fact]
    public void Constructor_NullPattern_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new ConstrainedPattern(null!)).ParamName.ShouldBe("pattern");
    }

    [Fact]
    public void Constructor_WithoutRules_ValidatesTheWholeValue()
    {
        ConstrainedPattern zip = new ConstrainedPattern(CommonPatterns.ZipCode());

        zip.RuleDescriptions.ShouldBeEmpty();
        zip.IsValid("12345-6789").ShouldBeTrue();
        zip.IsValid("1234").ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    // Validators
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("4111111111111111", true)]
    [InlineData("4111-1111 1111-1111", true)]
    [InlineData("79927398713", true)]
    [InlineData("79927398710", false)]
    [InlineData("4111a11111111111", false)]
    [InlineData("0", false)]
    [InlineData(" - ", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Luhn_ValidatesTheCheckDigit(string? value, bool expected)
    {
        bool result = Validators.Luhn(value!);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("GB82 WEST 1234 5698 7654 32", true)]
    [InlineData("DE89370400440532013000", true)]
    [InlineData("gb82west12345698765432", true)]
    [InlineData("FR1420041010050500013M02606", true)]
    [InlineData("GB82 WEST 1234 5698 7654 33", false)]
    [InlineData("1B82WEST12345698765432", false)]
    [InlineData("GB82WEST1234569876543!", false)]
    [InlineData("GB8", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IbanChecksum_ValidatesMod97(string? value, bool expected)
    {
        bool result = Validators.IbanChecksum(value!);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("2024-02-29", true)]
    [InlineData("0001-01-01", true)]
    [InlineData("2023-02-29", false)]
    [InlineData("2026-02-30", false)]
    [InlineData("2026-13-01", false)]
    [InlineData("2026-1-01", false)]
    [InlineData(" 2026-01-01", false)]
    [InlineData("0000-01-01", false)]
    [InlineData("2026/01/01", false)]
    [InlineData(null, false)]
    public void IsoDate_AcceptsOnlyRealCalendarDates(string? value, bool expected)
    {
        bool result = Validators.IsoDate(value!);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("192.168.0.1", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("010.001.1.1", true)]
    [InlineData("256.1.1.1", false)]
    [InlineData("1.1.1", false)]
    [InlineData("1.1.1.1.1", false)]
    [InlineData("1..1.1", false)]
    [InlineData("a.b.c.d", false)]
    [InlineData("1.1.1.1234", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IPv4_RequiresFourOctetsInRange(string? value, bool expected)
    {
        bool result = Validators.IPv4(value!);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("999.168.1.1", false)]
    public void Must_IPv4Validator_GuardsALoosePattern(string input, bool expected)
    {
        Pattern dottedQuad = Pattern.FromRegex(@"^\d{1,3}(?:\.\d{1,3}){3}$");

        bool result = dottedQuad.Must(Validators.IPv4, "octets 0-255").Check(input).IsValid;

        result.ShouldBe(expected);
    }
}
