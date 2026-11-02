namespace Masterly.FluentRegex.Tests;

public class TraceTests
{
    private static readonly Pattern Date = Pattern.With
        .StartOfLine
        .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
        .Literal("-")
        .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2))
        .Literal("-")
        .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2))
        .EndOfLine;

    [Fact]
    public void Match_ReportsEveryConstructWithExactSpans()
    {
        MatchTrace trace = Date.Trace("2024-05-17");

        trace.IsMatch.ShouldBeTrue();
        trace.Value.ShouldBe("2024-05-17");
        trace.Steps.Select(s => s.Construct).ShouldBe(new[] { "^", "(?<year>\\d{4})", "-", "(?<month>\\d{2})", "-", "(?<day>\\d{2})", "$" });

        TraceStep year = trace.Steps[1];
        year.Value.ShouldBe("2024");
        year.Index.ShouldBe(0);
        year.Description.ShouldBe("group 'year'");
        year.Children.Single().Construct.ShouldBe(@"\d{4}");

        TraceStep month = trace.Steps[3];
        month.Index.ShouldBe(5);
        month.Value.ShouldBe("05");
    }

    [Fact]
    public void RepeatedGroup_HasOneChildPerIteration()
    {
        Pattern pattern = Pattern.With.NonCapturingGroup(Pattern.With.Digit.Repeat.Times(3).Literal("-")).Repeat.OneOrMore.Digit.Repeat.Times(4);

        MatchTrace trace = pattern.Trace("555-123-4567");

        TraceStep repetition = trace.Steps[0];
        repetition.Value.ShouldBe("555-123-");
        repetition.Children.Select(c => c.Value).ShouldBe(new[] { "555-", "123-" });
        repetition.Children[0].Children.Select(c => c.Value).ShouldBe(new[] { "555", "-" });
    }

    [Fact]
    public void Alternation_ShowsOnlyTheBranchThatMatched()
    {
        Pattern pattern = Pattern.With.Choice(Pattern.With.Literal("cat"), Pattern.With.Literal("dog")).Literal("s");

        MatchTrace trace = pattern.Trace("dogs");

        trace.Steps[0].Children.Single().Value.ShouldBe("dog");
    }

    [Fact]
    public void Failure_PinpointsTheConstructAndPosition()
    {
        MatchTrace trace = Date.Trace("2024-5-17");

        trace.IsMatch.ShouldBeFalse();
        trace.Failure.ShouldNotBeNull();
        trace.Failure.Position.ShouldBe(5);
        trace.Failure.Construct.ShouldBe(@"\d{2}");
        trace.Failure.Description.ShouldBe("a digit, exactly 2 times");
        trace.Failure.MatchedSoFar.ShouldBe("2024-");
        trace.Failure.Found.ShouldBe("\"5-17\"");
        trace.Failure.MatchedSteps.Select(s => s.Value).ShouldBe(new[] { "", "2024", "-" });
    }

    [Fact]
    public void Failure_AtEndOfInput()
    {
        MatchTrace trace = Date.Trace("2024-05");

        trace.Failure!.Position.ShouldBe(7);
        trace.Failure.Construct.ShouldBe("-");
        trace.Failure.Found.ShouldBe("the end of the input");
    }

    [Fact]
    public void Failure_InTrailingAnchor()
    {
        MatchTrace trace = Date.Trace("2024-05-17x");

        trace.Failure!.Construct.ShouldBe("$");
        trace.Failure.Position.ShouldBe(10);
    }

    [Fact]
    public void ToString_RendersReadableReport()
    {
        string match = Date.Trace("2024-05-17").ToString();
        match.ShouldContain("Result:  MATCH at 0, length 10");
        match.ShouldContain("group 'year'");

        string failure = Date.Trace("2024-5-17").ToString();
        failure.ShouldContain("NO MATCH");
        failure.ShouldContain("Failed at position 5");
        failure.ShouldContain("  2024-5-17\n       ^".Replace("\n", Environment.NewLine));
    }

    [Fact]
    public void Trace_WorksWithBackreferencesToUnnamedGroups()
    {
        Pattern pattern = Pattern.With.Group(Pattern.With.Word.Repeat.OneOrMore).Whitespace.Backreference(1);

        MatchTrace trace = pattern.Trace("hello hello");

        trace.IsMatch.ShouldBeTrue();
        trace.Steps.Last().Construct.ShouldBe(@"\1");
        trace.Steps.Last().Value.ShouldBe("hello");
    }

    [Fact]
    public void Trace_NullInput_ThrowsArgumentNull()
    {
        ArgumentNullException error = Should.Throw<ArgumentNullException>(() => Date.Trace(null!));

        error.ParamName.ShouldBe("input");
    }

    [Fact]
    public void Trace_Failure_HasNoValueAndNoSteps()
    {
        MatchTrace trace = Date.Trace("nope");

        trace.IsMatch.ShouldBeFalse();
        trace.Value.ShouldBeNull();
        trace.Index.ShouldBe(-1);
        trace.Length.ShouldBe(0);
        trace.Steps.ShouldBeEmpty();
        trace.Input.ShouldBe("nope");
        trace.Pattern.ShouldBe(Date.ToString());
    }

    [Fact]
    public void TraceStep_ToString_ShowsConstructValueAndIndex()
    {
        TraceStep year = Date.Trace("2024-05-17").Steps[1];

        year.ToString().ShouldBe("(?<year>\\d{4}) => \"2024\" at 0");
    }

    [Fact]
    public void Trace_EmptyPattern_MatchesEmptyTextWithoutSteps()
    {
        MatchTrace trace = Pattern.FromRegex(string.Empty).Trace("abc");

        trace.IsMatch.ShouldBeTrue();
        trace.Index.ShouldBe(0);
        trace.Value.ShouldBe(string.Empty);
        trace.Steps.ShouldBeEmpty();
    }

    [Fact]
    public void Trace_EmptyAlternativeTaken_ReportsEmptyGroupStep()
    {
        MatchTrace trace = Pattern.FromRegex("(?:a|)b").Trace("b");

        trace.Steps.Select(s => s.Construct).ShouldBe(new[] { "(?:a|)", "b" });
        trace.Steps[0].Value.ShouldBe(string.Empty);
        trace.Steps[0].Children.ShouldBeEmpty();
        trace.Steps[1].Description.ShouldBe("the character 'b'");
    }

    [Fact]
    public void Trace_Comment_IsReportedAsEmptyStep()
    {
        MatchTrace trace = Pattern.FromRegex("a(?#c)b").Trace("ab");

        trace.Steps.Select(s => s.Construct).ShouldBe(new[] { "a", "(?#c)", "b" });
        trace.Steps[1].Index.ShouldBe(1);
        trace.Steps[1].Length.ShouldBe(0);
        trace.Steps[1].Description.ShouldBe("a comment");
    }

    [Theory]
    [InlineData("ab|(?:ac)")]
    [InlineData("(?:ac)|ab")]
    public void Failure_SeveralBranchesReachSamePosition_PrefersTheDeeperConstruct(string regex)
    {
        MatchTrace trace = Pattern.FromRegex(regex).Trace("ad");

        trace.Failure!.Position.ShouldBe(1);
        trace.Failure.Construct.ShouldBe("c");
        trace.Failure.MatchedSoFar.ShouldBe("a");
        trace.Failure.Found.ShouldBe("\"d\"");
    }

    [Theory]
    [InlineData("abc|x")]
    [InlineData("x|abc")]
    public void Failure_SeveralBranches_BlamesTheBranchThatGotFurthest(string regex)
    {
        MatchTrace trace = Pattern.FromRegex(regex).Trace("abd");

        trace.Failure!.Position.ShouldBe(2);
        trace.Failure.Construct.ShouldBe("c");
        trace.Failure.MatchedSoFar.ShouldBe("ab");
        trace.Failure.MatchedSteps.Select(s => s.Value).ShouldBe(new[] { "ab" });
    }

    [Fact]
    public void Failure_AtFirstConstruct_HasNoMatchedSteps()
    {
        MatchTrace trace = Pattern.FromRegex("x|y").Trace("z");

        trace.Failure!.Position.ShouldBe(0);
        trace.Failure.Construct.ShouldBe("x");
        trace.Failure.MatchedSoFar.ShouldBe(string.Empty);
        trace.Failure.MatchedSteps.ShouldBeEmpty();
        trace.ToString().ShouldNotContain("Matched before the failure:");
    }

    [Fact]
    public void Failure_LongRemainingInput_IsTruncatedInFound()
    {
        MatchTrace trace = Pattern.FromRegex("ab").Trace("a0123456789xbcdefghij");

        trace.Failure!.Found.ShouldBe("\"0123456789xbcdef...\"");
    }

    [Theory]
    [InlineData("a0123456789xyzuvw", "\"0123456789xyzuvw\"")]
    [InlineData("a0123456789xyzuvwq", "\"0123456789xyzuvw...\"")]
    public void Failure_RemainingInputAroundSixteenChars_IsTruncatedOnlyAboveSixteen(string input, string expectedFound)
    {
        MatchTrace trace = Pattern.FromRegex("ab").Trace(input);

        trace.Failure!.Found.ShouldBe(expectedFound);
    }

    [Fact]
    public void Failure_PrefixThatTimesOut_IsSkippedAndShorterPrefixIsBlamed()
    {
        // The full pattern fails instantly (the input is shorter than its minimum length),
        // but the prefix ^(a+)+$ backtracks catastrophically and hits the timeout.
        Pattern pattern = Pattern.FromRegex(@"^(a+)+$.{40}").WithTimeout(TimeSpan.FromMilliseconds(100));

        MatchTrace trace = pattern.Trace(new string('a', 30) + "!");

        trace.IsMatch.ShouldBeFalse();
        trace.Failure!.Construct.ShouldBe("$");
        trace.Failure.Position.ShouldBe(30);
        trace.Failure.Found.ShouldBe("\"!\"");
    }

    [Fact]
    public void ToString_ControlCharacters_AreEscapedAndCaretIsAligned()
    {
        string text = Pattern.FromRegex("a\tb").Trace("a\t\r\n\u0001c").ToString();

        text.ShouldContain("Input:   \"a\\t\\r\\n\\u0001c\"");
        text.ShouldContain("  a\\t\\r\\n\\u0001c" + Environment.NewLine + "     ^" + Environment.NewLine);
    }

    [Fact]
    public void ToString_LongConstruct_IsTruncated()
    {
        const string Text = "abcdefghijklmnopqrstuvwxyz0123456789";

        string report = Pattern.FromRegex("(" + Text + ")").Trace(Text).ToString();

        report.ShouldContain("(abcdefghijklmnopqrstuvwxyz01... ");
        report.ShouldNotContain("(" + Text + ")  ");
    }

    [Fact]
    public void Trace_OfCommonPatterns_DoesNotThrow()
    {
        foreach (Pattern pattern in new[] { CommonPatterns.Email(), CommonPatterns.Url(), CommonPatterns.IPv4(), CommonPatterns.StrongPassword(), CommonPatterns.HtmlTag() })
        {
            foreach (string sample in pattern.GenerateMatching(3, seed: 4))
            {
                MatchTrace trace = pattern.Trace(sample);
                trace.IsMatch.ShouldBeTrue();
                trace.Steps.ShouldNotBeEmpty();
            }
            foreach (string sample in pattern.GenerateNonMatching(3, seed: 4))
                pattern.Trace(sample).IsMatch.ShouldBeFalse();
        }
    }
}
