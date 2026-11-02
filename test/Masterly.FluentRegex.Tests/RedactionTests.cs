using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class RedactionTests
{
    private const string ValidCard = "4111 1111 1111 1111";
    private const string InvalidCard = "4111 1111 1111 1112";

    [Fact]
    public void Mask_EmailInText_MasksLettersAndDigitsAndKeepsPunctuation()
    {
        Redactor redactor = Redactor.For(CommonPatterns.Email());

        string masked = redactor.Mask("Contact bob@example.com today.");

        masked.ShouldBe("Contact ***@*******.*** today.");
    }

    [Theory]
    [InlineData("SSN 123-45-6789.", "SSN ***-**-****.")]
    [InlineData("123-45-6789", "***-**-****")]
    [InlineData("ids 123-45-6789 and 987-65-4321", "ids ***-**-**** and ***-**-****")]
    [InlineData("x123-45-6789", "x123-45-6789")]
    public void Mask_AnchoredValidationPattern_FindsWholeTokensInText(string text, string expected)
    {
        Redactor redactor = Redactor.For(CommonPatterns.SocialSecurityNumber());

        string masked = redactor.Mask(text);

        masked.ShouldBe(expected);
    }

    [Fact]
    public void Mask_PhoneNumberE164_KeepsThePlusSign()
    {
        Redactor redactor = Redactor.For(CommonPatterns.PhoneNumberE164());

        string masked = redactor.Mask("Call +14155552671 now");

        masked.ShouldBe("Call +*********** now");
    }

    [Fact]
    public void Add_ConstrainedPattern_RedactsOnlyValuesThatSatisfyTheRules()
    {
        Redactor redactor = Redactor.For()
            .Add(CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn"), RedactionStyle.KeepLast(4));

        string masked = redactor.Mask($"paid with {ValidCard}, declined {InvalidCard}");

        masked.ShouldBe($"paid with **** **** **** 1111, declined {InvalidCard}");
    }

    [Fact]
    public void KeepFirst_KeepsLeadingLettersAndDigits()
    {
        Redactor redactor = Redactor.For().Add(CommonPatterns.Iban(), RedactionStyle.KeepFirst(4));

        string masked = redactor.Mask("IBAN GB82 WEST 1234 5698 7654 32");

        masked.ShouldBe("IBAN GB82 **** **** **** **** **");
    }

    [Fact]
    public void KeepLast_ValueNotLongerThanTheCount_IsMaskedCompletely()
    {
        Redactor redactor = Redactor.For().Add(Pattern.With.Digit.Repeat.Times(4), RedactionStyle.KeepLast(4));

        string masked = redactor.Mask("PIN 1234");

        masked.ShouldBe("PIN ****");
    }

    [Fact]
    public void Label_ReplacesTheWholeValue()
    {
        Redactor redactor = Redactor.For().Add(CommonPatterns.Email(), RedactionStyle.Label("[EMAIL]"));

        string masked = redactor.Mask("from alice@example.com to bob@example.org");

        masked.ShouldBe("from [EMAIL] to [EMAIL]");
    }

    [Theory]
    [InlineData("alice@example.com", "a***@example.com")]
    [InlineData("b@x.io", "b***@x.io")]
    public void Email_KeepsFirstCharacterAndDomain(string address, string expected)
    {
        Redactor redactor = Redactor.For().Add(CommonPatterns.Email(), RedactionStyle.Email);

        string masked = redactor.Mask(address);

        masked.ShouldBe(expected);
    }

    [Fact]
    public void Email_ValueWithoutAtSign_IsMasked()
    {
        Redactor redactor = Redactor.For().Add(Pattern.With.Literal("secret42"), RedactionStyle.Email);

        string masked = redactor.Mask("token secret42");

        masked.ShouldBe("token ********");
    }

    [Fact]
    public void Custom_ComputesTheReplacementFromTheMatch()
    {
        Redactor redactor = Redactor.For().Add(CommonPatterns.Email(), RedactionStyle.Custom(m => $"<{m.Length} chars>"));

        string masked = redactor.Mask("mail bob@example.com");

        masked.ShouldBe("mail <15 chars>");
    }

    [Fact]
    public void Custom_ReturningNull_ThrowsInvalidOperationException()
    {
        Redactor redactor = Redactor.For().Add(CommonPatterns.Email(), RedactionStyle.Custom(_ => null!));

        Should.Throw<InvalidOperationException>(() => redactor.Mask("bob@example.com"));
    }

    [Fact]
    public void Mask_SupplementaryLetter_IsMaskedAsOneCharacter()
    {
        string astralLetter = char.ConvertFromUtf32(0x20000);
        Redactor redactor = Redactor.For(Pattern.With.Literal($"a{astralLetter}b"));

        string masked = redactor.Mask($"x a{astralLetter}b");

        masked.ShouldBe("x ***");
    }

    [Fact]
    public void Redact_Report_DescribesSpansWithoutTheOriginalValues()
    {
        Redactor redactor = Redactor.For(CommonPatterns.SocialSecurityNumber())
            .Add(CommonPatterns.Email(), RedactionStyle.Label("[EMAIL]"));

        RedactionReport report = redactor.Redact("ssn 123-45-6789, mail bob@example.com");

        report.Text.ShouldBe("ssn ***-**-****, mail [EMAIL]");
        report.HasRedactions.ShouldBeTrue();
        report.Spans.Select(s => (s.Start, s.Length, s.Style, s.PatternIndex)).ShouldBe(new[]
        {
            (4, 11, "Mask", 0),
            (22, 15, "Label", 1)
        });
        report.Spans[1].Label.ShouldBe("[EMAIL]");
        report.Spans[1].RedactedStart.ShouldBe(22);
        report.Spans[1].RedactedLength.ShouldBe(7);
        report.ToString().ShouldBe("Redacted 2 span(s): [4..15) Mask, [22..37) Label [EMAIL]");
        report.ToString().ShouldNotContain("6789");
        report.ToString().ShouldNotContain("bob");
    }

    [Fact]
    public void Redact_NoSensitiveValues_ReturnsTheTextUnchanged()
    {
        Redactor redactor = Redactor.For(CommonPatterns.Email());

        RedactionReport report = redactor.Redact("nothing to see");

        report.Text.ShouldBe("nothing to see");
        report.HasRedactions.ShouldBeFalse();
        report.ToString().ShouldBe("Redacted 0 span(s)");
    }

    [Fact]
    public void Redact_OverlappingHits_EarliestStartWins()
    {
        Redactor redactor = Redactor.For(Pattern.With.Literal("bcd"), Pattern.With.Literal("abc"));

        RedactionReport report = redactor.Redact("abcd");

        report.Text.ShouldBe("***d");
        report.Spans.Single().PatternIndex.ShouldBe(1);
    }

    [Fact]
    public void Redact_HitsWithTheSameStart_LongestWins()
    {
        Redactor redactor = Redactor.For(Pattern.With.Literal("ab"), Pattern.With.Literal("abc"));

        RedactionReport report = redactor.Redact("abcx");

        report.Text.ShouldBe("***x");
        report.Spans.Single().PatternIndex.ShouldBe(1);
    }

    [Fact]
    public void Redact_IdenticalHits_FirstRegisteredPatternWins()
    {
        Redactor redactor = Redactor.For()
            .Add(Pattern.With.Literal("ab"), RedactionStyle.Label("[A]"))
            .Add(Pattern.With.Literal("ab"), RedactionStyle.Label("[B]"));

        string masked = redactor.Mask("ab");

        masked.ShouldBe("[A]");
    }

    [Fact]
    public void Redact_EmptyMatches_AreIgnored()
    {
        Redactor redactor = Redactor.For(Pattern.With.Digit.Repeat.ZeroOrMore);

        RedactionReport report = redactor.Redact("a1b");

        report.Text.ShouldBe("a*b");
        report.Spans.Count.ShouldBe(1);
    }

    [Fact]
    public void Add_ReturnsANewRedactor_AndLeavesTheOriginalUnchanged()
    {
        Redactor original = Redactor.For(CommonPatterns.Email());

        Redactor extended = original.Add(CommonPatterns.SocialSecurityNumber());

        original.PatternCount.ShouldBe(1);
        extended.PatternCount.ShouldBe(2);
        original.Mask("123-45-6789").ShouldBe("123-45-6789");
        extended.Mask("123-45-6789").ShouldBe("***-**-****");
    }

    [Fact]
    public void Mask_ConcurrentCalls_ProduceTheSameResult()
    {
        Redactor redactor = Redactor.For(CommonPatterns.Email()).Add(CommonPatterns.CreditCard().Must(Validators.Luhn));

        IEnumerable<string> results = Enumerable.Range(0, 64).AsParallel()
            .Select(_ => redactor.Mask($"bob@example.com {ValidCard}"))
            .Distinct()
            .ToList();

        results.ShouldHaveSingleItem().ShouldBe("***@*******.*** **** **** **** ****");
    }

    [Fact]
    public void Mask_MultilinePattern_KeepsLineAnchors()
    {
        Redactor redactor = Redactor.For(Pattern.FromRegex("^secret$", RegexOptions.Multiline));

        string masked = redactor.Mask("a\nsecret\nb secret");

        masked.ShouldBe("a\n******\nb secret");
    }

    [Theory]
    [InlineData(@"\A\d{3}\z", "id 123 and x1234", "id *** and x1234")]
    [InlineData(@"^\d{3}", "a 123 4567", "a *** ***7")]
    [InlineData(@"\d{3}$", "1234 56", "1*** 56")]
    [InlineData(@"\d{3}\Z", "1234 56", "1*** 56")]
    [InlineData(@"\d{3}", "1234", "***4")]
    [InlineData(@"\b\d{3}\b", "1234 567", "1234 ***")]
    public void Mask_WrappingAnchors_AreRewrittenToTokenBoundaries(string regex, string text, string expected)
    {
        Redactor redactor = Redactor.For(Pattern.FromRegex(regex));

        string masked = redactor.Mask(text);

        masked.ShouldBe(expected);
    }

    [Theory]
    [InlineData("123", "***")]
    [InlineData("id 123", "id 123")]
    public void Mask_RightToLeftAnchoredPattern_KeepsTheAnchors(string text, string expected)
    {
        Redactor redactor = Redactor.For(Pattern.FromRegex(@"^\d{3}$", RegexOptions.RightToLeft));

        string masked = redactor.Mask(text);

        masked.ShouldBe(expected);
    }

    [Fact]
    public void Add_ConstrainedPatternWithAnchors_FindsValuesInsideText()
    {
        ConstrainedPattern evenNumber = Pattern.FromRegex(@"^(?<n>\d{2})$").Must("n", value => "02468".Contains(value[1]), "even");
        Redactor redactor = Redactor.For().Add(evenNumber, RedactionStyle.Label("[N]"));

        string masked = redactor.Mask("12 13 14");

        masked.ShouldBe("[N] 13 [N]");
    }

    [Fact]
    public void StyleNames_DescribeTheStyle()
    {
        RedactionStyle.Mask.Name.ShouldBe("Mask");
        RedactionStyle.Email.Name.ShouldBe("Email");
        RedactionStyle.KeepLast(4).Name.ShouldBe("KeepLast(4)");
        RedactionStyle.KeepFirst(2).ToString().ShouldBe("KeepFirst(2)");
        RedactionStyle.Label("[X]").LabelText.ShouldBe("[X]");
        RedactionStyle.Custom(m => m.Value).LabelText.ShouldBeNull();
    }

    [Fact]
    public void Factories_InvalidArguments_Throw()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RedactionStyle.KeepLast(-1)).ParamName.ShouldBe("count");
        Should.Throw<ArgumentOutOfRangeException>(() => RedactionStyle.KeepFirst(-1)).ParamName.ShouldBe("count");
        Should.Throw<ArgumentNullException>(() => RedactionStyle.Label(null!)).ParamName.ShouldBe("label");
        Should.Throw<ArgumentException>(() => RedactionStyle.Label(string.Empty)).ParamName.ShouldBe("label");
        Should.Throw<ArgumentNullException>(() => RedactionStyle.Custom(null!)).ParamName.ShouldBe("replacement");
    }

    [Fact]
    public void Redactor_NullArguments_Throw()
    {
        Redactor redactor = Redactor.For();

        Should.Throw<ArgumentNullException>(() => Redactor.For(null!)).ParamName.ShouldBe("patterns");
        Should.Throw<ArgumentNullException>(() => Redactor.For(CommonPatterns.Email(), null!)).ParamName.ShouldBe("patterns");
        Should.Throw<ArgumentNullException>(() => redactor.Add((Pattern)null!)).ParamName.ShouldBe("pattern");
        Should.Throw<ArgumentNullException>(() => redactor.Add((ConstrainedPattern)null!)).ParamName.ShouldBe("pattern");
        Should.Throw<ArgumentNullException>(() => redactor.Mask(null!)).ParamName.ShouldBe("text");
        Should.Throw<ArgumentNullException>(() => redactor.Redact(null!)).ParamName.ShouldBe("text");
    }
}
