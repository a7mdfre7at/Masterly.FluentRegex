using System.Text.RegularExpressions;
using Masterly.FluentRegex.Analysis;
using Masterly.FluentRegex.Tracing;

namespace Masterly.FluentRegex.Playground.Tests;

public class PatternWorkbenchTests
{
    private const string TimeoutMessage = "Matching took longer than 1 s and was stopped. Check the Safety panel for catastrophic backtracking.";
    private const string CatastrophicRegex = "^(a+)+$";
    private const string InvalidRegex = "(";
    private const int SampleCount = 10;
    private const int SampleSeed = 1;

    private static readonly string CatastrophicInput = new string('a', 40) + "!";

    /// <summary>Every panel, keyed by name, projected to its untyped result.</summary>
    private static readonly Dictionary<string, Func<PatternWorkbench, IPanelView>> Panels = new Dictionary<string, Func<PatternWorkbench, IPanelView>>
    {
        ["Matches"] = bench => new PanelView<IReadOnlyList<MatchView>>(bench.Matches),
        ["HighlightedHtml"] = bench => new PanelView<string>(bench.HighlightedHtml),
        ["Optimization"] = bench => new PanelView<OptimizationResult>(bench.Optimization),
        ["JavaScript"] = bench => new PanelView<FlavorConversion>(bench.JavaScript),
        ["JsonSchema"] = bench => new PanelView<FlavorConversion>(bench.JsonSchema),
        ["Coverage"] = bench => new PanelView<CoverageReport>(bench.Coverage),
        ["Explanation"] = bench => new PanelView<string>(bench.Explanation),
        ["FluentCode"] = bench => new PanelView<string>(bench.FluentCode),
        ["RailroadSvg"] = bench => new PanelView<string>(bench.RailroadSvg),
        ["Mermaid"] = bench => new PanelView<string>(bench.Mermaid),
        ["MatchingSamples"] = bench => new PanelView<string[]>(bench.MatchingSamples),
        ["NonMatchingSamples"] = bench => new PanelView<string[]>(bench.NonMatchingSamples),
        ["Safety"] = bench => new PanelView<SafetyReport>(bench.Safety),
        ["Trace"] = bench => new PanelView<MatchTrace>(bench.Trace("ahmad@example.com")),
        ["Profile"] = bench => new PanelView<ProfileReport>(bench.Profile("ahmad@example.com")),
        ["Compare"] = bench => new PanelView<PatternComparison>(bench.Compare(@"[\w.+-]+@[\w-]+\.[\w.-]+")),
    };

    private interface IPanelView
    {
        object Result { get; }

        object? Value { get; }

        string? Error { get; }

        bool Succeeded { get; }
    }

    public static TheoryData<string> PanelNames => new TheoryData<string>(Panels.Keys);

    // ---------------------------------------------------------------- construction

    [Fact]
    public void Constructor_ValidState_BuildsThePatternWithTheStateRegexAndOptions()
    {
        PlaygroundState state = new PlaygroundState();

        PatternWorkbench bench = new PatternWorkbench(state);

        bench.State.ShouldBeSameAs(state);
        bench.BuildError.ShouldBeNull();
        bench.Pattern.ShouldNotBeNull();
        bench.Pattern.ToString().ShouldBe(state.Regex);
        bench.Pattern.ToRegex().Options.ShouldBe(RegexOptions.Multiline);
    }

    [Fact]
    public void Constructor_ValidState_AppliesTheOneSecondMatchTimeout()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState());

        bench.Pattern.ShouldNotBeNull();
        bench.Pattern.ToRegex().MatchTimeout.ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(@"\d+", false, @"\d+")]
    [InlineData(@"\d+", true, "[0-9]+")]
    [InlineData("abc", true, "abc")]
    public void Constructor_AsciiDigits_RewritesDigitClassesOnlyWhenEnabled(string regex, bool asciiDigits, string expected)
    {
        PlaygroundState state = new PlaygroundState { Regex = regex, Options = RegexOptions.None, AsciiDigits = asciiDigits };

        PatternWorkbench bench = new PatternWorkbench(state);

        bench.Pattern.ShouldNotBeNull();
        bench.Pattern.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("(", RegexOptions.None)]
    [InlineData("a{2,1}", RegexOptions.None)]
    [InlineData(@"\k<missing>", RegexOptions.None)]
    [InlineData("a", RegexOptions.ECMAScript | RegexOptions.Singleline)]
    [InlineData("a", (RegexOptions)int.MaxValue)]
    public void Constructor_InvalidRegexOrOptions_CapturesTheBuildError(string regex, RegexOptions options)
    {
        PlaygroundState state = new PlaygroundState { Regex = regex, Options = options };

        PatternWorkbench bench = new PatternWorkbench(state);

        bench.Pattern.ShouldBeNull();
        bench.BuildError.ShouldNotBeNull();
        bench.BuildError.ShouldStartWith("The regular expression is invalid: ");
    }

    [Fact]
    public void Constructor_InvalidOptionsFromUri_CapturesTheBuildError()
    {
        PlaygroundState state = PlaygroundState.FromUri("https://example.test/#o=99999999");

        PatternWorkbench bench = new PatternWorkbench(state);

        bench.Pattern.ShouldBeNull();
        bench.BuildError.ShouldNotBeNull();
        bench.BuildError.ShouldContain("options");
    }

    [Theory]
    [MemberData(nameof(PanelNames))]
    public void Panel_InvalidRegex_ReportsTheBuildErrorWithoutAValue(string panel)
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState { Regex = InvalidRegex });

        IPanelView result = Panels[panel](bench);

        result.Succeeded.ShouldBeFalse();
        result.Value.ShouldBeNull();
        result.Error.ShouldBe(bench.BuildError);
        result.Error.ShouldStartWith("The regular expression is invalid: Invalid pattern '(' at offset 1.");
    }

    [Theory]
    [MemberData(nameof(PanelNames))]
    public void Panel_RegularPattern_Succeeds(string panel)
    {
        // Anchored with \A and \z rather than ^ and $ under Multiline, so language algebra (Compare) supports it too.
        PlaygroundState state = new PlaygroundState
        {
            Regex = @"\A(?<user>[\w.+-]+)@(?<domain>[\w-]+(?:\.[\w-]+)+)\z",
            Options = RegexOptions.None,
            TestText = "ahmad@example.com",
        };
        PatternWorkbench bench = new PatternWorkbench(state);

        IPanelView result = Panels[panel](bench);

        result.Error.ShouldBeNull();
        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }

    // ---------------------------------------------------------------- caching

    [Theory]
    [MemberData(nameof(PanelNames))]
    public void Panel_ReadTwice_ReturnsTheCachedResult(string panel)
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState());

        object first = Panels[panel](bench).Result;
        object second = Panels[panel](bench).Result;

        second.ShouldBeSameAs(first);
    }

    [Theory]
    [MemberData(nameof(PanelNames))]
    public void Panel_ReadTwiceWithBuildError_ReturnsTheCachedFailure(string panel)
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState { Regex = InvalidRegex });

        object first = Panels[panel](bench).Result;
        object second = Panels[panel](bench).Result;

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void Panel_DifferentWorkbenches_DoNotShareTheCache()
    {
        PlaygroundState state = new PlaygroundState();

        PanelResult<string> first = new PatternWorkbench(state).Explanation;
        PanelResult<string> second = new PatternWorkbench(state).Explanation;

        second.ShouldNotBeSameAs(first);
        second.ShouldBe(first);
    }

    [Fact]
    public void Trace_DifferentInputs_AreCachedSeparately()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState());

        PanelResult<MatchTrace> match = bench.Trace("ahmad@example.com");
        PanelResult<MatchTrace> miss = bench.Trace("not an email");

        match.Value.ShouldNotBeNull();
        match.Value.IsMatch.ShouldBeTrue();
        miss.Value.ShouldNotBeNull();
        miss.Value.IsMatch.ShouldBeFalse();
        bench.Trace("ahmad@example.com").ShouldBeSameAs(match);
        bench.Trace("not an email").ShouldBeSameAs(miss);
    }

    [Fact]
    public void Profile_DifferentInputs_AreCachedSeparately()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState());

        PanelResult<ProfileReport> match = bench.Profile("ahmad@example.com");
        PanelResult<ProfileReport> miss = bench.Profile("nope");

        match.Value.ShouldNotBeNull();
        match.Value.IsMatch.ShouldBeTrue();
        miss.Value.ShouldNotBeNull();
        miss.Value.IsMatch.ShouldBeFalse();
        bench.Profile("nope").ShouldBeSameAs(miss);
    }

    [Fact]
    public void TraceAndProfile_SameInput_UseSeparateCacheEntries()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState());

        PanelResult<MatchTrace> trace = bench.Trace("ahmad@example.com");
        PanelResult<ProfileReport> profile = bench.Profile("ahmad@example.com");
        PanelResult<PatternComparison> comparison = bench.Compare("ahmad@example.com");

        trace.Succeeded.ShouldBeTrue();
        profile.Succeeded.ShouldBeTrue();
        comparison.Error.ShouldNotBeNull();
        comparison.Error.ShouldStartWith("Language algebra does not support '^' with RegexOptions.Multiline");
    }

    [Fact]
    public void Compare_FailedThenValidRegex_EachResultIsCachedUnderItsOwnKey()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState { Regex = @"\Aa\z", Options = RegexOptions.None });

        PanelResult<PatternComparison> invalid = bench.Compare(InvalidRegex);
        PanelResult<PatternComparison> valid = bench.Compare(@"\Aa\z");

        invalid.Succeeded.ShouldBeFalse();
        valid.Value.ShouldNotBeNull();
        valid.Value.Relation.ShouldBe(PatternRelation.Equivalent);
        bench.Compare(InvalidRegex).ShouldBeSameAs(invalid);
    }

    // ---------------------------------------------------------------- matches

    [Fact]
    public void Matches_DefaultState_ListsEachMatchWithItsNamedGroups()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState());

        IReadOnlyList<MatchView>? matches = bench.Matches.Value;

        matches.ShouldNotBeNull();
        matches.Count.ShouldBe(2);
        matches[0].Index.ShouldBe(0);
        matches[0].Value.ShouldBe("ahmad@example.com");
        matches[0].Groups.ShouldBe(new[] { new GroupView("user", 0, "ahmad"), new GroupView("domain", 6, "example.com") });
        matches[1].Index.ShouldBe(31);
        matches[1].Value.ShouldBe("support@masterly.dev");
        matches[1].Groups.ShouldBe(new[] { new GroupView("user", 31, "support"), new GroupView("domain", 39, "masterly.dev") });
    }

    [Fact]
    public void Matches_GroupThatDidNotParticipate_IsLeftOut()
    {
        PlaygroundState state = new PlaygroundState { Regex = "(a)|(b)", Options = RegexOptions.None, TestText = "ba" };
        PatternWorkbench bench = new PatternWorkbench(state);

        IReadOnlyList<MatchView>? matches = bench.Matches.Value;

        matches.ShouldNotBeNull();
        matches.Count.ShouldBe(2);
        matches[0].Value.ShouldBe("b");
        matches[0].Groups.ShouldBe(new[] { new GroupView("2", 0, "b") });
        matches[1].Value.ShouldBe("a");
        matches[1].Groups.ShouldBe(new[] { new GroupView("1", 1, "a") });
    }

    [Fact]
    public void Matches_PatternWithoutGroups_HasNoGroupViews()
    {
        PlaygroundState state = new PlaygroundState { Regex = "o", Options = RegexOptions.None, TestText = "foo" };
        PatternWorkbench bench = new PatternWorkbench(state);

        IReadOnlyList<MatchView>? matches = bench.Matches.Value;

        matches.ShouldNotBeNull();
        matches.Select(match => match.Index).ShouldBe(new[] { 1, 2 });
        matches.ShouldAllBe(match => match.Groups.Count == 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("xyz")]
    public void Matches_NoMatch_ReturnsAnEmptyList(string testText)
    {
        PlaygroundState state = new PlaygroundState { Regex = "a", Options = RegexOptions.None, TestText = testText };
        PatternWorkbench bench = new PatternWorkbench(state);

        PanelResult<IReadOnlyList<MatchView>> result = bench.Matches;

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(499, 499)]
    [InlineData(500, 500)]
    [InlineData(501, 500)]
    [InlineData(2000, 500)]
    public void Matches_ManyMatches_ShowsAtMostFiveHundred(int matchCount, int expectedShown)
    {
        PlaygroundState state = new PlaygroundState { Regex = "a", Options = RegexOptions.None, TestText = new string('a', matchCount) };
        PatternWorkbench bench = new PatternWorkbench(state);

        IReadOnlyList<MatchView>? matches = bench.Matches.Value;

        matches.ShouldNotBeNull();
        matches.Count.ShouldBe(expectedShown);
        matches[expectedShown - 1].Index.ShouldBe(expectedShown - 1);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public void Matches_AsciiDigits_ControlsWhetherNonAsciiDigitsMatch(bool asciiDigits, int expectedMatches)
    {
        PlaygroundState state = new PlaygroundState { Regex = @"\d+", Options = RegexOptions.None, TestText = "١٢٣ 456", AsciiDigits = asciiDigits };
        PatternWorkbench bench = new PatternWorkbench(state);

        IReadOnlyList<MatchView>? matches = bench.Matches.Value;

        matches.ShouldNotBeNull();
        matches.Count.ShouldBe(expectedMatches);
        matches[^1].Value.ShouldBe("456");
    }

    [Fact]
    public void Matches_OptionsFromState_AreApplied()
    {
        PlaygroundState state = new PlaygroundState { Regex = "abc", Options = RegexOptions.IgnoreCase, TestText = "ABC abc" };
        PatternWorkbench bench = new PatternWorkbench(state);

        IReadOnlyList<MatchView>? matches = bench.Matches.Value;

        matches.ShouldNotBeNull();
        matches.Select(match => match.Value).ShouldBe(new[] { "ABC", "abc" });
    }

    // ---------------------------------------------------------------- text panels

    [Fact]
    public void HighlightedHtml_TextWithMarkup_MarksMatchesAndEncodesText()
    {
        PlaygroundState state = new PlaygroundState { Regex = "(?<tag>b)", Options = RegexOptions.None, TestText = "<b>" };
        PatternWorkbench bench = new PatternWorkbench(state);
        string expected = Pattern.FromRegex("(?<tag>b)").Highlight("<b>", HighlightFormat.Html);

        string? html = bench.HighlightedHtml.Value;

        html.ShouldBe(expected);
        html.ShouldNotBeNull();
        html.ShouldStartWith("&lt;");
        html.ShouldContain("<mark");
        html.ShouldEndWith("&gt;");
    }

    [Fact]
    public void Explanation_DefaultState_IsTheLibraryExplanation()
    {
        PlaygroundState state = new PlaygroundState();
        PatternWorkbench bench = new PatternWorkbench(state);

        PanelResult<string> result = bench.Explanation;

        result.Value.ShouldBe(DirectPattern(state).Explain());
    }

    [Fact]
    public void FluentCode_DefaultState_IsTheLibraryFluentCode()
    {
        PlaygroundState state = new PlaygroundState();
        PatternWorkbench bench = new PatternWorkbench(state);

        PanelResult<string> result = bench.FluentCode;

        result.Value.ShouldNotBeNull();
        result.Value.ShouldBe(DirectPattern(state).ToFluentCode());
        result.Value.ShouldContain("NamedGroup(\"user\"");
    }

    [Fact]
    public void RailroadSvg_DefaultState_IsTheLibrarySvg()
    {
        PlaygroundState state = new PlaygroundState();
        PatternWorkbench bench = new PatternWorkbench(state);

        PanelResult<string> result = bench.RailroadSvg;

        result.Value.ShouldBe(DirectPattern(state).ToRailroadSvg());
        result.Value.ShouldStartWith("<svg");
    }

    [Fact]
    public void Mermaid_DefaultState_IsTheLibraryFlowchart()
    {
        PlaygroundState state = new PlaygroundState();
        PatternWorkbench bench = new PatternWorkbench(state);

        PanelResult<string> result = bench.Mermaid;

        result.Value.ShouldBe(DirectPattern(state).ToMermaid());
    }

    // ---------------------------------------------------------------- analysis panels

    [Fact]
    public void Optimization_RedundantAlternation_ReturnsAVerifiedRewrite()
    {
        PlaygroundState state = new PlaygroundState { Regex = "a|b|c", Options = RegexOptions.None };
        PatternWorkbench bench = new PatternWorkbench(state);
        OptimizationResult expected = DirectPattern(state).Optimize();

        OptimizationResult? result = bench.Optimization.Value;

        result.ShouldNotBeNull();
        result.Original.ToString().ShouldBe("a|b|c");
        result.Optimized.ToString().ShouldBe(expected.Optimized.ToString());
        result.IsChanged.ShouldBeTrue();
        result.VerifiedEquivalent.ShouldBeTrue();
    }

    [Theory]
    [InlineData("abc", RegexOptions.None)]
    [InlineData(@"^\d{3}$", RegexOptions.IgnoreCase)]
    public void JavaScript_SupportedPattern_IsAnExactJavaScriptConversion(string regex, RegexOptions options)
    {
        PlaygroundState state = new PlaygroundState { Regex = regex, Options = options };
        PatternWorkbench bench = new PatternWorkbench(state);

        FlavorConversion? conversion = bench.JavaScript.Value;

        conversion.ShouldNotBeNull();
        conversion.Flavor.ShouldBe(RegexFlavor.JavaScript);
        conversion.IsExact.ShouldBeTrue();
        conversion.ToLiteral().ShouldBe(DirectPattern(state).ToFlavor(RegexFlavor.JavaScript).ToLiteral());
    }

    [Fact]
    public void JsonSchema_SupportedPattern_IsAJsonSchemaConversion()
    {
        PlaygroundState state = new PlaygroundState { Regex = "abc", Options = RegexOptions.None };
        PatternWorkbench bench = new PatternWorkbench(state);

        FlavorConversion? conversion = bench.JsonSchema.Value;

        conversion.ShouldNotBeNull();
        conversion.Flavor.ShouldBe(RegexFlavor.JsonSchema);
        conversion.Pattern.ShouldBe(DirectPattern(state).ToFlavor(RegexFlavor.JsonSchema).Pattern);
    }

    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\r\nb")]
    public void Coverage_EachLineIsAnInput_WithWindowsLineEndingsNormalized(string testText)
    {
        PlaygroundState state = new PlaygroundState { Regex = "^(?:a|b)$", Options = RegexOptions.None, TestText = testText };
        PatternWorkbench bench = new PatternWorkbench(state);

        CoverageReport? report = bench.Coverage.Value;

        report.ShouldNotBeNull();
        report.InputsRecorded.ShouldBe(2);
        report.Uncovered.ShouldBeEmpty();
        report.Percentage.ShouldBe(100d);
    }

    [Fact]
    public void Coverage_UnexercisedBranch_IsReportedAsUncovered()
    {
        PlaygroundState state = new PlaygroundState { Regex = "^(?:a|b)$", Options = RegexOptions.None, TestText = "a" };
        PatternWorkbench bench = new PatternWorkbench(state);

        CoverageReport? report = bench.Coverage.Value;

        report.ShouldNotBeNull();
        report.InputsRecorded.ShouldBe(1);
        report.Uncovered.ShouldNotBeEmpty();
    }

    [Fact]
    public void MatchingSamples_DefaultState_AreTenSeededSamplesThatMatch()
    {
        PlaygroundState state = new PlaygroundState();
        PatternWorkbench bench = new PatternWorkbench(state);
        Regex regex = new Regex(state.Regex, state.Options);

        string[]? samples = bench.MatchingSamples.Value;

        samples.ShouldNotBeNull();
        samples.Length.ShouldBe(SampleCount);
        samples.ShouldAllBe(sample => regex.IsMatch(sample));
        samples.ShouldBe(DirectPattern(state).GenerateMatching(SampleCount, SampleSeed));
    }

    [Fact]
    public void NonMatchingSamples_DefaultState_AreTenSeededSamplesThatDoNotMatch()
    {
        PlaygroundState state = new PlaygroundState();
        PatternWorkbench bench = new PatternWorkbench(state);
        Regex regex = new Regex(state.Regex, state.Options);

        string[]? samples = bench.NonMatchingSamples.Value;

        samples.ShouldNotBeNull();
        samples.Length.ShouldBe(SampleCount);
        samples.ShouldAllBe(sample => !regex.IsMatch(sample));
        samples.ShouldBe(DirectPattern(state).GenerateNonMatching(SampleCount, SampleSeed));
    }

    [Fact]
    public void Safety_CatastrophicPattern_IsReportedUnsafeAndConfirmedByRunningTheAttack()
    {
        PlaygroundState state = new PlaygroundState { Regex = CatastrophicRegex, Options = RegexOptions.None };
        PatternWorkbench bench = new PatternWorkbench(state);

        SafetyReport? report = bench.Safety.Value;

        report.ShouldNotBeNull();
        report.IsSafe.ShouldBeFalse();
        report.Risk.ShouldBe(RiskLevel.High);
        report.Issues.ShouldNotBeEmpty();
        // The panel asks for confirmation, so the attack input is actually run; without it Confirmed stays null.
        report.Issues[0].Confirmed.ShouldBe(true, report.ToString());
    }

    [Fact]
    public void Safety_SimplePattern_IsReportedSafe()
    {
        PlaygroundState state = new PlaygroundState { Regex = "^abc$", Options = RegexOptions.None };
        PatternWorkbench bench = new PatternWorkbench(state);

        SafetyReport? report = bench.Safety.Value;

        report.ShouldNotBeNull();
        report.IsSafe.ShouldBeTrue();
        report.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Trace_MatchingInput_ReportsTheMatchedSpan()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState());

        MatchTrace? trace = bench.Trace("ahmad@example.com").Value;

        trace.ShouldNotBeNull();
        trace.IsMatch.ShouldBeTrue();
        trace.Input.ShouldBe("ahmad@example.com");
        trace.Value.ShouldBe("ahmad@example.com");
    }

    [Fact]
    public void Trace_NonMatchingInput_ReportsTheFailure()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState());

        MatchTrace? trace = bench.Trace("not an email").Value;

        trace.ShouldNotBeNull();
        trace.IsMatch.ShouldBeFalse();
        trace.Failure.ShouldNotBeNull();
    }

    [Fact]
    public void Profile_MatchingInput_ReportsTheMatchAndAgreesWithTheEngine()
    {
        PlaygroundState state = new PlaygroundState { Regex = "b+", Options = RegexOptions.None };
        PatternWorkbench bench = new PatternWorkbench(state);

        ProfileReport? report = bench.Profile("abbc").Value;

        report.ShouldNotBeNull();
        report.IsMatch.ShouldBeTrue();
        report.MatchIndex.ShouldBe(1);
        report.MatchLength.ShouldBe(2);
        report.AgreesWithEngine.ShouldBeTrue();
        report.BudgetExhausted.ShouldBeFalse();
    }

    [Theory]
    [InlineData(@"\Aa\z", RegexOptions.IgnoreCase, @"\AA\z", PatternRelation.Equivalent)]
    [InlineData(@"\Aa\z", RegexOptions.None, @"\AA\z", PatternRelation.Disjoint)]
    [InlineData(@"\Aa+\z", RegexOptions.None, @"\Aa\z", PatternRelation.ProperSuperset)]
    [InlineData(@"\Aa\z", RegexOptions.None, @"\Aa+\z", PatternRelation.ProperSubset)]
    public void Compare_OtherRegex_UsesTheStateOptions(string regex, RegexOptions options, string otherRegex, PatternRelation expected)
    {
        PlaygroundState state = new PlaygroundState { Regex = regex, Options = options };
        PatternWorkbench bench = new PatternWorkbench(state);

        PatternComparison? comparison = bench.Compare(otherRegex).Value;

        comparison.ShouldNotBeNull();
        comparison.Relation.ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"\A\d\z", @"\A[0-9]\z", true, PatternRelation.Equivalent)]
    [InlineData(@"\A\d\z", @"\A[0-9]\z", false, PatternRelation.ProperSuperset)]
    [InlineData(@"\A[0-9]\z", @"\A\d\z", true, PatternRelation.Equivalent)]
    [InlineData(@"\A[0-9]\z", @"\A\d\z", false, PatternRelation.ProperSubset)]
    public void Compare_OtherRegex_UsesTheStateDigitModeForBothSides(string regex, string otherRegex, bool asciiDigits, PatternRelation expected)
    {
        PlaygroundState state = new PlaygroundState { Regex = regex, Options = RegexOptions.None, AsciiDigits = asciiDigits };
        PatternWorkbench bench = new PatternWorkbench(state);

        PatternComparison? comparison = bench.Compare(otherRegex).Value;

        comparison.ShouldNotBeNull();
        comparison.Relation.ShouldBe(expected);
    }

    // ---------------------------------------------------------------- per-panel error capture

    [Theory]
    [InlineData("Matches")]
    [InlineData("HighlightedHtml")]
    [InlineData("Coverage")]
    [InlineData("Trace")]
    public void Panel_CatastrophicBacktracking_ReportsTheTimeout(string panel)
    {
        PlaygroundState state = new PlaygroundState { Regex = CatastrophicRegex, Options = RegexOptions.None, TestText = CatastrophicInput };
        PatternWorkbench bench = new PatternWorkbench(state);
        Dictionary<string, Func<PanelResultSummary>> panels = new Dictionary<string, Func<PanelResultSummary>>
        {
            ["Matches"] = () => PanelResultSummary.Of(bench.Matches),
            ["HighlightedHtml"] = () => PanelResultSummary.Of(bench.HighlightedHtml),
            ["Coverage"] = () => PanelResultSummary.Of(bench.Coverage),
            ["Trace"] = () => PanelResultSummary.Of(bench.Trace(CatastrophicInput)),
        };

        PanelResultSummary result = panels[panel]();

        result.Succeeded.ShouldBeFalse();
        result.HasValue.ShouldBeFalse();
        result.Error.ShouldBe(TimeoutMessage);
    }

    [Fact]
    public void Panel_TimeoutInOnePanel_DoesNotBreakTheOthers()
    {
        PlaygroundState state = new PlaygroundState { Regex = CatastrophicRegex, Options = RegexOptions.None, TestText = CatastrophicInput };
        PatternWorkbench bench = new PatternWorkbench(state);

        PanelResult<IReadOnlyList<MatchView>> matches = bench.Matches;
        PanelResult<string> explanation = bench.Explanation;

        matches.Error.ShouldBe(TimeoutMessage);
        explanation.Succeeded.ShouldBeTrue();
        explanation.Value.ShouldBe(DirectPattern(state).Explain());
    }

    [Theory]
    [InlineData("JavaScript", "The pattern cannot be converted to JavaScript without changing its meaning:")]
    [InlineData("JsonSchema", "The pattern cannot be converted to JSON Schema without changing its meaning:")]
    public void Flavor_UnsupportedConstruct_CapturesTheNotSupportedMessage(string panel, string expectedStart)
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState { Regex = "(?>a)b", Options = RegexOptions.None });

        IPanelView result = Panels[panel](bench);

        result.Succeeded.ShouldBeFalse();
        result.Value.ShouldBeNull();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldStartWith(expectedStart);
        result.Error.ShouldContain("atomic group '(?>...)' at position 0");
    }

    [Fact]
    public void Flavor_UnsupportedConstruct_DoesNotBreakOtherPanels()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState { Regex = "(?>a)b", Options = RegexOptions.None, TestText = "ab" });

        bool javaScriptSucceeded = bench.JavaScript.Succeeded;
        IReadOnlyList<MatchView>? matches = bench.Matches.Value;

        javaScriptSucceeded.ShouldBeFalse();
        matches.ShouldNotBeNull();
        matches.Single().Value.ShouldBe("ab");
    }

    [Fact]
    public void Profile_UnsupportedConstruct_CapturesTheNotSupportedMessage()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState { Regex = "(?(a)b|c)", Options = RegexOptions.None });

        PanelResult<ProfileReport> result = bench.Profile("ab");

        result.Succeeded.ShouldBeFalse();
        result.Value.ShouldBeNull();
        result.Error.ShouldBe("The backtracking profiler does not emulate the conditional '(?(a)b|c)' at position 0.");
    }

    [Fact]
    public void Profile_RightToLeft_CapturesTheNotSupportedMessage()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState { Regex = "a", Options = RegexOptions.RightToLeft });

        PanelResult<ProfileReport> result = bench.Profile("a");

        result.Error.ShouldBe("The backtracking profiler does not emulate RegexOptions.RightToLeft.");
    }

    [Fact]
    public void Compare_NonRegularConstruct_CapturesTheNotSupportedMessage()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState { Regex = @"(a)\1", Options = RegexOptions.None });

        PanelResult<PatternComparison> result = bench.Compare("aa");

        result.Succeeded.ShouldBeFalse();
        result.Value.ShouldBeNull();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldStartWith(@"Language algebra does not support backreferences: '\1' at position 3.");
    }

    [Fact]
    public void Compare_RepeatBeyondTheAlgebraLimit_CapturesTheInvalidOperationMessage()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState { Regex = "a", Options = RegexOptions.None });

        PanelResult<PatternComparison> result = bench.Compare("a{100000}");

        result.Succeeded.ShouldBeFalse();
        result.Value.ShouldBeNull();
        result.Error.ShouldBe("The repeat '{100000}' at position 0 exceeds the language algebra limit of 1000 repetitions.");
    }

    [Theory]
    [InlineData("(", "The regular expression is invalid: Invalid pattern '(' at offset 1. Not enough )'s. (Parameter 'regex')")]
    [InlineData("a{2,1}", "The regular expression is invalid: Invalid pattern 'a{2,1}' at offset 6. Illegal {x,y} with x > y. (Parameter 'regex')")]
    public void Compare_InvalidOtherRegex_CapturesTheArgumentMessage(string otherRegex, string expected)
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState { Regex = "a", Options = RegexOptions.None });

        PanelResult<PatternComparison> result = bench.Compare(otherRegex);

        result.Succeeded.ShouldBeFalse();
        result.Value.ShouldBeNull();
        result.Error.ShouldBe(expected);
    }

    [Fact]
    public void Trace_NullInput_CapturesTheArgumentNullMessage()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState());

        // The page always passes a bound string; null exercises the panel's ArgumentException capture.
        PanelResult<MatchTrace> result = bench.Trace(null!);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldBe("Value cannot be null. (Parameter 'input')");
    }

    [Fact]
    public void Profile_NullInput_CapturesTheArgumentNullMessage()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState());

        // The page always passes a bound string; null exercises the panel's ArgumentException capture.
        PanelResult<ProfileReport> result = bench.Profile(null!);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldBe("Value cannot be null. (Parameter 'input')");
    }

    [Fact]
    public void Compare_NullOtherRegex_CapturesTheArgumentNullMessage()
    {
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState());

        // The page always passes a bound string; null exercises the panel's ArgumentException capture.
        PanelResult<PatternComparison> result = bench.Compare(null!);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldBe("Value cannot be null. (Parameter 'regex')");
    }

    [Fact]
    public void Panel_UnexpectedExceptionType_IsNotSwallowed()
    {
        // A null test text violates the state's non-nullable contract; the resulting NullReferenceException is not one
        // of the expected failure types, so the panel must let it surface instead of hiding a programming error.
        PatternWorkbench bench = new PatternWorkbench(new PlaygroundState { TestText = null! });

        Should.Throw<NullReferenceException>(() => bench.Coverage);
    }

    // ---------------------------------------------------------------- PanelResult

    [Theory]
    [InlineData("value", null, true)]
    [InlineData(null, null, true)]
    [InlineData(null, "boom", false)]
    [InlineData("value", "", false)]
    public void PanelResult_Succeeded_IsTrueOnlyWithoutAnError(string? value, string? error, bool expected)
    {
        PanelResult<string> result = new PanelResult<string>(value, error);

        result.Succeeded.ShouldBe(expected);
        result.Value.ShouldBe(value);
        result.Error.ShouldBe(error);
    }

    private static Pattern DirectPattern(PlaygroundState state) =>
        Pattern.FromRegex(state.Regex, state.Options)
            .WithDigits(state.AsciiDigits ? DigitMode.Ascii : DigitMode.Unicode)
            .WithTimeout(TimeSpan.FromSeconds(1));

    private sealed class PanelView<T> : IPanelView
    {
        private readonly PanelResult<T> _result;

        public PanelView(PanelResult<T> result)
        {
            _result = result;
        }

        public object Result => _result;

        public object? Value => _result.Value;

        public string? Error => _result.Error;

        public bool Succeeded => _result.Succeeded;
    }

    private sealed record PanelResultSummary(bool Succeeded, bool HasValue, string? Error)
    {
        public static PanelResultSummary Of<T>(PanelResult<T> result) => new PanelResultSummary(result.Succeeded, result.Value != null, result.Error);
    }
}
