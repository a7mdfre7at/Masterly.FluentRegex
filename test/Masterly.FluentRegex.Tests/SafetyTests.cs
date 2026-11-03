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
    [InlineData(@"^(ax+y?)+$")]
    [InlineData(@"^(?:\b*)+$")]
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

    private const string ThirtyTwoAs = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Theory]
    [InlineData(@"^(a{1,5}){1,20}$", "(a{1,5}){1,20}")]
    [InlineData(@"^(a{1,3})+$", "(a{1,3})+")]
    public void AnalyzeSafety_NestedRepetitionWithSmallBound_IsMediumRisk(string regex, string fragment)
    {
        SafetyReport report = Pattern.FromRegex(regex).AnalyzeSafety();

        report.Risk.ShouldBe(RiskLevel.Medium, report.ToString());
        report.Issues.Count.ShouldBe(1);
        report.Issues[0].Kind.ShouldBe(SafetyIssueKind.NestedQuantifier);
        report.Issues[0].Fragment.ShouldBe(fragment);
        report.Issues[0].AttackInput.ShouldBe(ThirtyTwoAs + "!");
    }

    [Theory]
    [InlineData(@"^\d+(?:a|)\d+$", RiskLevel.Medium)]
    [InlineData(@"^\d+(?:a|b)\d+$", RiskLevel.None)]
    [InlineData(@"^\d+(?=\d)\d+$", RiskLevel.Medium)]
    [InlineData(@"^\d+(?(?=x)x)\d+$", RiskLevel.Medium)]
    [InlineData(@"^\d+(?(?=x)x|)\d+$", RiskLevel.Medium)]
    [InlineData(@"^\d+(?(?=x)x|y)\d+$", RiskLevel.None)]
    public void AnalyzeSafety_ConstructBetweenAdjacentRepetitions_CompetesOnlyWhenItCanMatchEmpty(string regex, RiskLevel expected)
    {
        SafetyReport report = Pattern.FromRegex(regex).AnalyzeSafety();

        report.Risk.ShouldBe(expected, report.ToString());
    }

    [Theory]
    [InlineData(@"((?:a?b?)*)+")]
    [InlineData(@"((?:a|b)+)+")]
    [InlineData(@"((?:(?=a)a)*)+")]
    [InlineData(@"((?:b{0}a)*)+")]
    [InlineData(@"((?:(?(?=a)a|b))*)+")]
    [InlineData(@"((?:(?(?=a)a))*)+")]
    [InlineData(@"((?:\ba)*)+")]
    public void AnalyzeSafety_RepetitionOfCompositeBodyInsideRepetition_IsHighRiskWithPumpedAttack(string regex)
    {
        SafetyReport report = Pattern.FromRegex(regex).AnalyzeSafety();

        report.Risk.ShouldBe(RiskLevel.High, report.ToString());
        report.Issues[0].Kind.ShouldBe(SafetyIssueKind.NestedQuantifier);
        report.Issues[0].Fragment.ShouldBe(regex);
        report.Issues[0].AttackInput.ShouldBe(ThirtyTwoAs + "!");
    }

    [Fact]
    public void AnalyzeSafety_OverlappingAlternativesWithBackreference_AreNotReportedWhenUnambiguous()
    {
        SafetyReport report = Pattern.FromRegex(@"(a)(?:a\1|ab)+").AnalyzeSafety();

        report.Risk.ShouldBe(RiskLevel.None, report.ToString());
        report.Issues.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(@"\2(a)(b+)+$", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb!")]
    [InlineData(@"(?!)x(a+)+$", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!")]
    [InlineData(@"(?#c)(a+)+$", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!")]
    public void AnalyzeSafety_PrefixThatCannotBeSampled_BuildsAttackWithoutPrefix(string regex, string expectedAttack)
    {
        SafetyReport report = Pattern.FromRegex(regex).AnalyzeSafety();

        report.Issues[0].AttackInput.ShouldBe(expectedAttack);
    }

    [Fact]
    public void AnalyzeSafety_AttackForLaterRootItem_StartsWithSampleOfEarlierItems()
    {
        SafetyReport report = Pattern.FromRegex(@"x(a+)+$").AnalyzeSafety();

        report.Issues[0].AttackInput.ShouldBe("x" + ThirtyTwoAs + "!");
    }

    [Fact]
    public void AnalyzeSafety_PumpCharacterSetContainsDefaultKiller_UsesNextFreeKillerCharacter()
    {
        SafetyReport report = Pattern.FromRegex(@"^([!#]+)+$").AnalyzeSafety();

        report.Issues[0].AttackInput.ShouldBe(new string('!', 32) + "\0");
        report.ToString().ShouldContain("Attack input (33 chars): " + new string('!', 32) + "\\u0000");
    }

    [Fact]
    public void AnalyzeSafety_SeveralIssues_AreOrderedByRiskThenPosition()
    {
        SafetyReport report = Pattern.FromRegex(@"^(a+)+-(b+)+\d+\d+$").AnalyzeSafety();

        report.Issues.Select(i => i.Risk).ShouldBe(new[] { RiskLevel.High, RiskLevel.High, RiskLevel.Medium });
        report.Issues.Select(i => i.Position).ShouldBe(new[] { 1, 7, 12 });
        report.Issues[1].AttackInput.ShouldBe("aaaa-" + new string('b', 32) + "!");
    }

    [Fact]
    public void AnalyzeSafety_AttackInputThatMatchesQuickly_IsReportedAsNotConfirmed()
    {
        SafetyReport report = Pattern.FromRegex(@"(a+)+").AnalyzeSafety(confirm: true);

        SafetyIssue issue = report.Issues.Single();
        issue.Confirmed.ShouldBe(false);
        issue.ConfirmationTime.ShouldNotBeNull();
        report.ToString().ShouldContain("Not confirmed: the attack input finished quickly.");
    }

    [Fact]
    public void AnalyzeSafety_WithoutConfirmation_LeavesConfirmationUnset()
    {
        SafetyReport report = Pattern.FromRegex(@"^(a+)+$").AnalyzeSafety();

        report.Issues[0].Confirmed.ShouldBeNull();
        report.Issues[0].ConfirmationTime.ShouldBeNull();
        report.ToString().ShouldNotContain("onfirmed");
    }

    [Fact]
    public void AnalyzeSafety_CustomBudgetConfirmedIssue_ReportsTheTimeoutInText()
    {
        SafetyReport report = Pattern.FromRegex(@"^(a+)+$").AnalyzeSafety(confirm: true, confirmationBudget: TimeSpan.FromMilliseconds(50));

        SafetyIssue issue = report.Issues[0];
        issue.Confirmed.ShouldBe(true);
        issue.ConfirmationTime.ShouldNotBeNull();
        report.ToString().ShouldContain(
            FormattableString.Invariant($"CONFIRMED: matching did not finish within {issue.ConfirmationTime.Value.TotalMilliseconds:0} ms."));
    }

    [Fact]
    public void AnalyzeSafety_LowRiskIssueWithoutAttackInput_IsNotConfirmed()
    {
        SafetyReport report = Pattern.FromRegex(@"^(\w|\d)+$").AnalyzeSafety(confirm: true);

        SafetyIssue issue = report.Issues.Single();
        issue.AttackInput.ShouldBeNull();
        issue.Confirmed.ShouldBeNull();
        report.ToString().ShouldNotContain("Attack input");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnalyzeSafety_NonPositiveConfirmationBudget_ThrowsArgumentOutOfRange(int milliseconds)
    {
        Pattern pattern = Pattern.FromRegex(@"^(a+)+$");

        ArgumentOutOfRangeException error = Should.Throw<ArgumentOutOfRangeException>(
            () => pattern.AnalyzeSafety(confirm: true, confirmationBudget: TimeSpan.FromMilliseconds(milliseconds)));

        error.ParamName.ShouldBe("confirmationBudget");
    }

    [Fact]
    public void SafetyReport_ToString_WithoutIssues_SaysNoRisksFound()
    {
        string text = Pattern.FromRegex(@"^\d+$").AnalyzeSafety().ToString();

        text.ShouldBe(
            "Pattern: ^\\d+$" + Environment.NewLine +
            "Risk:    None" + Environment.NewLine +
            "No catastrophic backtracking risks found." + Environment.NewLine);
    }

    [Fact]
    public void SafetyReport_ToString_LongAttackInput_IsTruncated()
    {
        string text = Pattern.FromRegex(@"^\d+\d+$").AnalyzeSafety().ToString();

        text.ShouldContain("Attack input (30001 chars): " + new string('0', 57) + "..." + Environment.NewLine);
    }

    [Theory]
    [InlineData(@"x{27}(a+)+$", "Attack input (60 chars): xxxxxxxxxxxxxxxxxxxxxxxxxxxaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!")]
    [InlineData(@"x{28}(a+)+$", "Attack input (61 chars): xxxxxxxxxxxxxxxxxxxxxxxxxxxxaaaaaaaaaaaaaaaaaaaaaaaaaaaaa...")]
    public void SafetyReport_ToString_AttackInputAroundSixtyChars_IsTruncatedOnlyAboveSixty(string regex, string expectedLine)
    {
        string text = Pattern.FromRegex(regex).AnalyzeSafety().ToString();

        text.ShouldContain("    " + expectedLine + Environment.NewLine);
    }

    [Fact]
    public void SafetyReport_Pattern_IsTheAnalyzedRegex()
    {
        SafetyReport report = Pattern.FromRegex(@"^(a+)+$").AnalyzeSafety();

        report.Pattern.ShouldBe(@"^(a+)+$");
    }

    [Fact]
    public void SafetyIssue_ToString_SummarizesRiskKindPositionAndFragment()
    {
        SafetyIssue issue = Pattern.FromRegex(@"^(a+)+$").AnalyzeSafety().Issues[0];

        issue.ToString().ShouldBe("High NestedQuantifier at 1 '(a+)+': " + issue.Description);
        issue.Suggestion.ShouldContain("Possessive");
    }
}
