using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class CoverageTests
{
    private const string TitleRegex = @"^(?:Mr|Ms)\.\s\w+$";
    private const int ParallelInputs = 1000;

    private static readonly Pattern Title = Pattern.FromRegex(TitleRegex);

    [Fact]
    public void MeasureCoverage_OneBranchNeverMatched_ReportsItUncovered()
    {
        CoverageReport report = Title.MeasureCoverage(new[] { "Mr. Smith", "Mr. Jones" });

        report.Percentage.ShouldBe(50);
        report.Items.Select(i => i.Description).ShouldBe(new[] { "branch \"Mr\"", "branch \"Ms\"" });
        report.Uncovered.Single().Description.ShouldBe("branch \"Ms\"");
        report.Items[0].HitCount.ShouldBe(2);
        report.Items[0].Kind.ShouldBe(CoverageItemKind.AlternationBranch);
    }

    [Fact]
    public void MeasureCoverage_AllBranchesMatched_IsFullyCovered()
    {
        CoverageReport report = Title.MeasureCoverage(new[] { "Mr. Smith", "Ms. Jones" });

        report.Percentage.ShouldBe(100);
        report.Uncovered.ShouldBeEmpty();
    }

    [Fact]
    public void ToString_PartialCoverage_ListsEveryItem()
    {
        CoverageReport report = Title.MeasureCoverage(new[] { "Mr. A", "Mr. B", "Mr. C", "Mr. D", "Dr. X" });

        string expected = string.Join(Environment.NewLine,
            @"Coverage for ^(?:Mr|Ms)\.\s\w+$: 50% (1/2), 5 inputs",
            "  ✓ branch \"Mr\" (4 hits)",
            "  ✗ branch \"Ms\" was never matched",
            string.Empty);
        report.ToString().ShouldBe(expected);
    }

    [Fact]
    public void ToString_ThirdsCoverage_RoundsPercentage()
    {
        CoverageReport report = Pattern.FromRegex("^(?:a|b|c)$").MeasureCoverage(new[] { "a", "b" });

        report.ToString().ShouldStartWith("Coverage for ^(?:a|b|c)$: 67% (2/3), 2 inputs");
    }

    [Fact]
    public void Items_BranchSpan_PointsIntoPatternText()
    {
        CoverageReport report = Title.MeasureCoverage(new[] { "Mr. Smith" });

        CoverageItem ms = report.Items[1];
        TitleRegex.Substring(ms.Start, ms.Length).ShouldBe("Ms");
    }

    [Theory]
    [InlineData("color", false)]
    [InlineData("colour", true)]
    public void MeasureCoverage_OptionalPart_IsCoveredOnlyWhenTaken(string input, bool covered)
    {
        CoverageReport report = Pattern.FromRegex("^colou?r$").MeasureCoverage(new[] { input });

        CoverageItem optional = report.Items.Single();
        optional.Kind.ShouldBe(CoverageItemKind.OptionalPartTaken);
        optional.Description.ShouldBe("optional part \"u?\"");
        optional.IsCovered.ShouldBe(covered);
    }

    [Theory]
    [InlineData("12", false)]
    [InlineData("123", true)]
    public void MeasureCoverage_BoundedRepetition_IsCoveredOnlyAtUpperBound(string input, bool covered)
    {
        CoverageReport report = Pattern.FromRegex(@"^\d{1,3}$").MeasureCoverage(new[] { input });

        CoverageItem upper = report.Items.Single();
        upper.Kind.ShouldBe(CoverageItemKind.UpperBoundReached);
        upper.Description.ShouldBe("upper bound 3 of \"\\d{1,3}\"");
        upper.IsCovered.ShouldBe(covered);
    }

    [Fact]
    public void MeasureCoverage_OptionalBoundedRepetition_TracksBothItems()
    {
        CoverageReport report = Pattern.FromRegex("^ab{0,2}$").MeasureCoverage(new[] { "ab" });

        report.Items.Select(i => i.Kind).ShouldBe(new[] { CoverageItemKind.OptionalPartTaken, CoverageItemKind.UpperBoundReached });
        report.Items.Select(i => i.IsCovered).ShouldBe(new[] { true, false });
    }

    [Theory]
    [InlineData(@"^\d{4}$")]
    [InlineData(@"^\d+$")]
    [InlineData(@"^(?:\d{1,3}\.)+$")]
    [InlineData(@"^(?!foo|bar)\w+$")]
    [InlineData("^abc$")]
    public void MeasureCoverage_NoTrackableDecisionPoints_HasNoItemsAndFullCoverage(string regex)
    {
        CoverageReport report = Pattern.FromRegex(regex).MeasureCoverage(new[] { "1234" });

        report.Items.ShouldBeEmpty();
        report.Percentage.ShouldBe(100);
    }

    [Fact]
    public void MeasureCoverage_BranchTriedThenBacktracked_IsNotCovered()
    {
        CoverageReport report = Pattern.FromRegex("^(?:ab|a)c$").MeasureCoverage(new[] { "ac" });

        report.Uncovered.Single().Description.ShouldBe("branch \"ab\"");
    }

    [Fact]
    public void MeasureCoverage_PositiveLookahead_IsInstrumented()
    {
        CoverageReport report = Pattern.FromRegex(@"^(?=a|b)\w$").MeasureCoverage(new[] { "b" });

        report.Uncovered.Single().Description.ShouldBe("branch \"a\"");
    }

    [Fact]
    public void MeasureCoverage_Conditional_TracksBranchesButNotCondition()
    {
        CoverageReport report = Pattern.FromRegex("^(?<q>\")?\\w+(?(q)\"|)$").MeasureCoverage(new[] { "\"abc\"" });

        report.Items.Select(i => i.Description).ShouldBe(new[]
        {
            "optional part \"(?<q>\")?\"",
            "condition-true branch \"\"\"",
            "empty condition-false branch"
        });
        report.Items.Select(i => i.IsCovered).ShouldBe(new[] { true, true, false });
    }

    [Fact]
    public void MeasureCoverage_EveryMatchInInput_IsRecorded()
    {
        CoverageReport report = Pattern.FromRegex("cat|dog").MeasureCoverage(new[] { "cat and dog" });

        report.Percentage.ShouldBe(100);
        report.InputsRecorded.ShouldBe(1);
    }

    [Fact]
    public void MeasureCoverage_InsertionsAtSameOffset_NestCorrectly()
    {
        PatternCoverage coverage = Pattern.FromRegex("^(?:a{1,2}|b)$").TrackCoverage();

        coverage.IsMatch("aa").ShouldBeTrue();
        coverage.IsMatch("aaa").ShouldBeFalse();
        coverage.Report().Items.Select(i => $"{i.Description}:{i.HitCount}").ShouldBe(new[]
        {
            "branch \"a{1,2}\":1",
            "upper bound 2 of \"a{1,2}\":1",
            "branch \"b\":0"
        });
    }

    [Fact]
    public void MeasureCoverage_ExplicitCapture_StillTracksBranches()
    {
        Pattern pattern = Pattern.FromRegex("^(a|b)+$", RegexOptions.ExplicitCapture);

        CoverageReport report = pattern.MeasureCoverage(new[] { "ab" });

        report.Items.Count.ShouldBe(2);
        report.Percentage.ShouldBe(100);
    }

    [Fact]
    public void MeasureCoverage_IgnorePatternWhitespace_KeepsMeaning()
    {
        PatternCoverage coverage = Pattern.FromRegex("^ (?: cat | dog ) $", RegexOptions.IgnorePatternWhitespace).TrackCoverage();

        coverage.IsMatch("cat").ShouldBeTrue();
        coverage.IsMatch("c a t").ShouldBeFalse();
        coverage.Report().Uncovered.Single().Description.ShouldBe("branch \"dog\"");
    }

    [Fact]
    public void TrackCoverage_IgnoreCaseOption_IsKept()
    {
        PatternCoverage coverage = Pattern.FromRegex("^(?:yes|no)$", RegexOptions.IgnoreCase).TrackCoverage();

        coverage.IsMatch("YES").ShouldBeTrue();
        coverage.Report().Items[0].IsCovered.ShouldBeTrue();
    }

    [Fact]
    public void TrackCoverage_Timeout_IsKept()
    {
        PatternCoverage coverage = Pattern.FromRegex("^(a|aa)+$").WithTimeout(TimeSpan.FromMilliseconds(50)).TrackCoverage();

        Should.Throw<RegexMatchTimeoutException>(() => coverage.IsMatch($"{new string('a', 60)}!"));
    }

    [Fact]
    public void TrackCoverage_NumberedBackreferenceToNamedGroup_KeepsWorking()
    {
        PatternCoverage coverage = Pattern.FromRegex(@"^(?<letter>x|y)\1$").TrackCoverage();

        coverage.IsMatch("xx").ShouldBeTrue();
        coverage.IsMatch("xy").ShouldBeFalse();
        coverage.Report().Uncovered.Single().Description.ShouldBe("branch \"y\"");
    }

    [Fact]
    public void Match_ReturnsUninstrumentedGroups()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<year>\d{4})-(\d{2})?(?:a|b)?");
        PatternCoverage coverage = pattern.TrackCoverage();

        Match recorded = coverage.Match("2024-05a");

        Match expected = pattern.Match("2024-05a");
        recorded.Value.ShouldBe(expected.Value);
        recorded.Groups.Keys.ShouldBe(expected.Groups.Keys);
        recorded.Groups["year"].Value.ShouldBe("2024");
        recorded.Groups[1].Value.ShouldBe("05");
        coverage.Report().InputsRecorded.ShouldBe(1);
    }

    [Fact]
    public void TrackCoverage_NonBacktracking_CountsEveryIteration()
    {
        PatternCoverage coverage = Pattern.FromRegex(@"^\d{1,3}$").UseNonBacktracking().TrackCoverage();

        coverage.Record("123");

        coverage.Report().Percentage.ShouldBe(100);
    }

    [Fact]
    public void Record_NonMatchingInput_CountsAsRecordedInput()
    {
        PatternCoverage coverage = Title.TrackCoverage();

        coverage.Record("nothing to see");

        CoverageReport report = coverage.Report();
        report.InputsRecorded.ShouldBe(1);
        report.Percentage.ShouldBe(0);
    }

    [Fact]
    public void Report_IsSnapshot_NotChangedByLaterRecording()
    {
        PatternCoverage coverage = Title.TrackCoverage();
        CoverageReport before = coverage.Report();

        coverage.Record("Mr. Smith");

        before.InputsRecorded.ShouldBe(0);
        before.Items[0].HitCount.ShouldBe(0);
        coverage.Report().Items[0].HitCount.ShouldBe(1);
    }

    [Fact]
    public void TrackCoverage_Pattern_IsTheTrackedPattern()
    {
        PatternCoverage coverage = Title.TrackCoverage();

        coverage.Pattern.ShouldBeSameAs(Title);
    }

    [Fact]
    public void Record_ParallelCallers_CountsEveryInputExactly()
    {
        PatternCoverage coverage = Title.TrackCoverage();

        Parallel.For(0, ParallelInputs, i => coverage.Record(i % 2 == 0 ? "Mr. Smith" : "Ms. Jones"));

        CoverageReport report = coverage.Report();
        report.InputsRecorded.ShouldBe(ParallelInputs);
        report.Items.Select(i => i.HitCount).ShouldBe(new[] { ParallelInputs / 2, ParallelInputs / 2 });
    }

    [Fact]
    public void TrackCoverage_InvalidPattern_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() => Pattern.With.RegEx("(").TrackCoverage());
    }

    [Fact]
    public void Record_NullInput_ThrowsArgumentNullException()
    {
        PatternCoverage coverage = Title.TrackCoverage();

        Should.Throw<ArgumentNullException>(() => coverage.Record(null!));
    }

    [Fact]
    public void IsMatch_NullInput_ThrowsArgumentNullException()
    {
        PatternCoverage coverage = Title.TrackCoverage();

        Should.Throw<ArgumentNullException>(() => coverage.IsMatch(null!));
    }

    [Fact]
    public void Match_NullInput_ThrowsArgumentNullException()
    {
        PatternCoverage coverage = Title.TrackCoverage();

        Should.Throw<ArgumentNullException>(() => coverage.Match(null!));
    }

    [Fact]
    public void MeasureCoverage_NullInputs_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(() => Title.MeasureCoverage(null!));

        exception.ParamName.ShouldBe("inputs");
    }

    [Fact]
    public void MeasureCoverage_NullInputElement_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(() => Title.MeasureCoverage(new[] { "Mr. A", null! }));

        exception.ParamName.ShouldBe("inputs");
        exception.Message.ShouldStartWith("The inputs cannot contain null.");
    }

    [Fact]
    public void ToString_NoDecisionPoints_SaysThereIsNothingToCover()
    {
        CoverageReport report = Pattern.FromRegex("^abc$").MeasureCoverage(new[] { "abc" });

        string expected = string.Join(Environment.NewLine,
            "Coverage for ^abc$: 100% (0/0), 1 input",
            "  (no alternation branches, optional parts or bounded repetitions to cover)",
            string.Empty);
        report.ToString().ShouldBe(expected);
    }

    [Fact]
    public void CoverageItemToString_EachKind_DescribesHitsOrWhatWasNeverExercised()
    {
        CoverageReport report = Pattern.FromRegex(@"^(?:x|y)c?d{1,3}$").MeasureCoverage(new[] { "xd" });

        report.Items.Select(i => i.ToString()).ShouldBe(new[]
        {
            "✓ branch \"x\" (1 hit)",
            "✗ branch \"y\" was never matched",
            "✗ optional part \"c?\" was never taken",
            "✗ upper bound 3 of \"d{1,3}\" was never reached"
        });
        report.Items.Select(i => i.Kind).ShouldBe(new[]
        {
            CoverageItemKind.AlternationBranch,
            CoverageItemKind.AlternationBranch,
            CoverageItemKind.OptionalPartTaken,
            CoverageItemKind.UpperBoundReached
        });
    }

    [Fact]
    public void MeasureCoverage_BranchLongerThanFortyCharacters_TruncatesDescription()
    {
        Pattern pattern = Pattern.FromRegex("^(?:abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz|b)$");

        CoverageReport report = pattern.MeasureCoverage(new[] { "b" });

        report.Items[0].Description.ShouldBe("branch \"abcdefghijklmnopqrstuvwxyzabcdefghijklmn...\"");
        report.Items[0].Length.ShouldBe(52);
        report.Items[1].Description.ShouldBe("branch \"b\"");
    }

    [Fact]
    public void MeasureCoverage_BranchOfExactlyFortyCharacters_KeepsFullDescription()
    {
        Pattern pattern = Pattern.FromRegex("^(?:abcdefghijklmnopqrstuvwxyzabcdefghijklmn|b)$");

        CoverageReport report = pattern.MeasureCoverage(new[] { "b" });

        report.Items[0].Description.ShouldBe("branch \"abcdefghijklmnopqrstuvwxyzabcdefghijklmn\"");
    }

    [Fact]
    public void TrackCoverage_ZeroRepetitionQuantifier_IsNotInstrumented()
    {
        PatternCoverage coverage = Pattern.FromRegex("^a(?:b|c){0}$").TrackCoverage();

        coverage.IsMatch("a").ShouldBeTrue();
        coverage.IsMatch("ab").ShouldBeFalse();
        coverage.Report().Items.ShouldBeEmpty();
    }

    [Fact]
    public void TrackCoverage_ExplicitGroupNumberAboveGroupCount_KeepsBackreferenceMeaning()
    {
        PatternCoverage coverage = Pattern.FromRegex(@"^(?<5>a|z)(b|c)\5$").TrackCoverage();

        coverage.IsMatch("aba").ShouldBeTrue();
        coverage.IsMatch("abz").ShouldBeFalse();
        coverage.Report().Items.Select(i => $"{i.Description}:{i.HitCount}").ShouldBe(new[]
        {
            "branch \"a\":1",
            "branch \"z\":0",
            "branch \"b\":1",
            "branch \"c\":0"
        });
    }

    [Fact]
    public void TrackCoverage_ExplicitGroupNumberAboveMarkerRange_KeepsBackreferenceMeaning()
    {
        PatternCoverage coverage = Pattern.FromRegex(@"^(?<2000000>a)(b|c)\2000000$").TrackCoverage();

        coverage.IsMatch("aca").ShouldBeTrue();
        coverage.IsMatch("acb").ShouldBeFalse();
        coverage.Report().Items.Select(i => $"{i.Description}:{i.HitCount}").ShouldBe(new[]
        {
            "branch \"b\":0",
            "branch \"c\":1"
        });
    }

    [Fact]
    public void Match_ExplicitGroupNumberAboveMarkerRange_ReturnsOriginalGroup()
    {
        PatternCoverage coverage = Pattern.FromRegex(@"^(?<2000000>a)(b|c)$").TrackCoverage();

        Match match = coverage.Match("ab");

        match.Groups[2000000].Value.ShouldBe("a");
        match.Groups[1].Value.ShouldBe("b");
    }

    [Fact]
    public void TrackCoverage_ExplicitGroupNumberEqualToFirstMarker_StartsMarkersAboveIt()
    {
        PatternCoverage coverage = Pattern.FromRegex(@"^(?<1000000>a|z)(b|c)\1000000$").TrackCoverage();

        coverage.IsMatch("zcz").ShouldBeTrue();
        coverage.IsMatch("zca").ShouldBeFalse();
        coverage.Report().Items.Select(i => $"{i.Description}:{i.HitCount}").ShouldBe(new[]
        {
            "branch \"a\":0",
            "branch \"z\":1",
            "branch \"b\":0",
            "branch \"c\":1"
        });
    }

    [Fact]
    public void MeasureCoverage_MoreThanSixteenMarkerInsertions_CountsEveryOptionalPart()
    {
        // Twenty marker insertions take List.Sort past its insertion-sort threshold into introsort partitioning,
        // where every insertion must still land at its own offset for the markers to nest correctly.
        Pattern pattern = Pattern.FromRegex("^a?b?c?d?(?:e?f?g?h?i?)?$");

        CoverageReport report = pattern.MeasureCoverage(new[] { "ace", "bdfghi" });

        report.Items.Select(i => $"{i.Description}:{i.HitCount}").ShouldBe(new[]
        {
            "optional part \"a?\":1",
            "optional part \"b?\":1",
            "optional part \"c?\":1",
            "optional part \"d?\":1",
            "optional part \"(?:e?f?g?h?i?)?\":2",
            "optional part \"e?\":1",
            "optional part \"f?\":1",
            "optional part \"g?\":1",
            "optional part \"h?\":1",
            "optional part \"i?\":1"
        });
    }
}
