namespace Masterly.FluentRegex.Tests;

public class SafetyTests
{
    [Theory]
    [InlineData(@"^(a+)+$")]
    [InlineData(@"^(\w+\s?)*$")]
    [InlineData(@"(x+x+)+y")]
    [InlineData(@"^(\d*)*$")]
    [InlineData(@"^([a-zA-Z0-9]+[._-]?)*@example\.com$")]
    public void NestedQuantifiers_AreHighRisk_AndConfirmed(string regex)
    {
        SafetyReport report = Pattern.FromRegex(regex).AnalyzeSafety(confirm: true);

        report.Risk.ShouldBe(RiskLevel.High, report.ToString());
        report.IsSafe.ShouldBeFalse();
        SafetyIssue issue = report.Issues[0];
        issue.Kind.ShouldBe(SafetyIssueKind.NestedQuantifier);
        issue.AttackInput.ShouldNotBeNull();
        issue.Confirmed.ShouldBe(true, report.ToString());
    }

    [Theory]
    [InlineData(@"^(a|aa)+$")]
    [InlineData(@"^(ab|a|b)*c$")]
    [InlineData(@"^(?:\d|\d\d)+$")]
    public void AmbiguousAlternation_IsHighRisk_AndConfirmed(string regex)
    {
        SafetyReport report = Pattern.FromRegex(regex).AnalyzeSafety(confirm: true);

        report.Risk.ShouldBe(RiskLevel.High, report.ToString());
        SafetyIssue issue = report.Issues.First(i => i.Kind == SafetyIssueKind.OverlappingAlternation);
        issue.Confirmed.ShouldBe(true, report.ToString());
    }

    [Theory]
    [InlineData(@"^(\w|\d)+$")]
    [InlineData(@"^(a|a)*$")]
    public void OverlappingSingleCharAlternation_IsLowRisk_BecauseDotNetMergesIt(string regex)
    {
        SafetyReport report = Pattern.FromRegex(regex).AnalyzeSafety(confirm: true);

        report.Risk.ShouldBe(RiskLevel.Low, report.ToString());
        report.IsSafe.ShouldBeTrue();

        // Proof that the downgrade is right: the engine finishes quickly on a hostile input.
        Pattern.FromRegex(regex).WithTimeout(TimeSpan.FromMilliseconds(250)).IsMatch(new string('a', 5000) + "!").ShouldBeFalse();
    }

    [Theory]
    [InlineData(@"^(ab|ac)+$")]
    [InlineData(@"^(?:cat|dog)+$")]
    public void UnambiguousAlternation_IsSafe(string regex)
    {
        Pattern.FromRegex(regex).AnalyzeSafety().Risk.ShouldBe(RiskLevel.None);
    }

    [Fact]
    public void Url_HostNoLongerHasPolynomialBacktracking()
    {
        // 2.x allowed dots in the first host label, which competed with the following labels.
        Pattern url = CommonPatterns.Url().WithTimeout(TimeSpan.FromMilliseconds(500));
        Should.NotThrow(() => url.IsMatch("http://a" + new string('.', 30000) + "!"));
        url.IsMatch("https://sub.example.co.uk/path?q=1").ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"^\d+\d+$")]
    [InlineData(@"^\s*.*\s*=$")]
    [InlineData(@"a.*.*b")]
    public void AdjacentOverlappingQuantifiers_AreMediumRisk(string regex)
    {
        SafetyReport report = Pattern.FromRegex(regex).AnalyzeSafety();

        report.Risk.ShouldBe(RiskLevel.Medium, report.ToString());
        report.Issues.ShouldContain(i => i.Kind == SafetyIssueKind.AdjacentOverlappingQuantifiers);
    }

    [Theory]
    [InlineData(@"^\d+$")]
    [InlineData(@"^(\d+\.)+\d+$")]
    [InlineData(@"^(a+b)+$")]
    [InlineData(@"^(?>a+)+$")]
    [InlineData(@"^\w+@\w+\.\w+$")]
    [InlineData(@"^(\d{3}-)?\d{4}$")]
    [InlineData(@"^[a-z]+\d+$")]
    [InlineData(@"(cat|dog)+")]
    [InlineData(@"^(?:[A-Za-z0-9+/]{4})*$")]
    [InlineData(@"^(?:d{3}-)+$")]
    public void SafePatterns_HaveNoIssues(string regex)
    {
        SafetyReport report = Pattern.FromRegex(regex).AnalyzeSafety();
        report.Risk.ShouldBe(RiskLevel.None, report.ToString());
        report.IsSafe.ShouldBeTrue();
    }

    [Fact]
    public void Possessive_FixesNestedQuantifier()
    {
        Pattern vulnerable = Pattern.With.StartOfLine.Group(Pattern.With.Literal("a").Repeat.OneOrMore).Repeat.OneOrMore.EndOfLine;
        Pattern fixedPattern = Pattern.With.StartOfLine.Group(Pattern.With.Literal("a").Repeat.OneOrMore.Possessive).Repeat.OneOrMore.EndOfLine;

        vulnerable.AnalyzeSafety().Risk.ShouldBe(RiskLevel.High);
        fixedPattern.AnalyzeSafety().Risk.ShouldBe(RiskLevel.None);
    }

    [Fact]
    public void AllCommonPatterns_AreSafe()
    {
        Pattern[] patterns =
        {
            CommonPatterns.Email(), CommonPatterns.Url(), CommonPatterns.PhoneNumber(), CommonPatterns.IPv4(),
            CommonPatterns.IPv6(), CommonPatterns.Guid(), CommonPatterns.HexColor(), CommonPatterns.DateIso(),
            CommonPatterns.Date(), CommonPatterns.Time24(), CommonPatterns.Time12(), CommonPatterns.CreditCard(),
            CommonPatterns.SocialSecurityNumber(), CommonPatterns.ZipCode(), CommonPatterns.Username(),
            CommonPatterns.StrongPassword(), CommonPatterns.FilePath(), CommonPatterns.HtmlTag(),
            CommonPatterns.Integer(), CommonPatterns.Decimal(), CommonPatterns.ScientificNotation()
        };

        foreach (Pattern pattern in patterns)
        {
            SafetyReport report = pattern.AnalyzeSafety();
            report.IsSafe.ShouldBeTrue(report.ToString());
        }
    }

    [Fact]
    public void Report_DescribesIssue()
    {
        SafetyReport report = Pattern.FromRegex(@"^(a+)+$").AnalyzeSafety();
        string text = report.ToString();

        text.ShouldContain("High");
        text.ShouldContain("NestedQuantifier");
        text.ShouldContain("Fix:");
        report.Issues[0].Position.ShouldBe(1);
        report.Issues[0].Fragment.ShouldBe("(a+)+");
    }

    [Fact]
    public void AttackInput_StartsWithAValidPrefix()
    {
        SafetyReport report = Pattern.FromRegex(@"^id:(\d+)+$").AnalyzeSafety(confirm: true);

        report.Issues[0].AttackInput.ShouldStartWith("id:");
        report.Issues[0].Confirmed.ShouldBe(true);
    }
}
