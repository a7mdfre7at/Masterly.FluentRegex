using System.Text.RegularExpressions;
using Masterly.FluentRegex.Profiling;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Tests;

public class ProfilerTests
{
    private const string HostileInput = "aaaaaaaaaaaaaaaaaaaaaaaa!";
    private const int SamplesPerKind = 4;
    private const int SampleSeed = 11;

    private static readonly Dictionary<string, Func<Pattern>> CommonPatternFactories = new()
    {
        ["Email"] = CommonPatterns.Email,
        ["Email.Strict"] = () => CommonPatterns.Email(PatternStrictness.Strict),
        ["Url"] = CommonPatterns.Url,
        ["Url.Strict"] = () => CommonPatterns.Url(PatternStrictness.Strict),
        ["PhoneNumber"] = CommonPatterns.PhoneNumber,
        ["IPv4"] = CommonPatterns.IPv4,
        ["IPv6"] = CommonPatterns.IPv6,
        ["Guid"] = CommonPatterns.Guid,
        ["HexColor"] = CommonPatterns.HexColor,
        ["DateIso"] = CommonPatterns.DateIso,
        ["Date"] = CommonPatterns.Date,
        ["Time24"] = CommonPatterns.Time24,
        ["Time12"] = CommonPatterns.Time12,
        ["CreditCard"] = CommonPatterns.CreditCard,
        ["SocialSecurityNumber"] = CommonPatterns.SocialSecurityNumber,
        ["ZipCode"] = CommonPatterns.ZipCode,
        ["Username"] = CommonPatterns.Username,
        ["StrongPassword"] = CommonPatterns.StrongPassword,
        ["FilePath"] = CommonPatterns.FilePath,
        ["HtmlTag"] = CommonPatterns.HtmlTag,
        ["Integer"] = CommonPatterns.Integer,
        ["Decimal"] = CommonPatterns.Decimal,
        ["ScientificNotation"] = CommonPatterns.ScientificNotation,
        ["PhoneNumberE164"] = CommonPatterns.PhoneNumberE164,
        ["Iban"] = CommonPatterns.Iban,
        ["SemVer"] = CommonPatterns.SemVer,
        ["Jwt"] = CommonPatterns.Jwt,
        ["MacAddress"] = CommonPatterns.MacAddress,
        ["Base64"] = CommonPatterns.Base64,
        ["Slug"] = CommonPatterns.Slug,
        ["DateTimeIso"] = CommonPatterns.DateTimeIso,
        ["MarkdownLink"] = CommonPatterns.MarkdownLink,
    };

    public static TheoryData<string, RegexOptions, string> AgreementCases() => new()
    {
        { @"(a|ab)(c|bcd)(d*)", RegexOptions.None, "abcd" },
        { @"(a*)*b", RegexOptions.None, "aaab" },
        { @"(a*)+$", RegexOptions.None, "aaaa" },
        { @"(?:a|b)*?c", RegexOptions.None, "ababc" },
        { @"a{2,3}?", RegexOptions.None, "aaaa" },
        { @"a{2,}", RegexOptions.None, "a aa aaaa" },
        { @"(a?){3}b", RegexOptions.None, "aab" },
        { @"(a{0,2}){2,3}c", RegexOptions.None, "aaaaac" },
        { @"(a|)+b", RegexOptions.None, "aab" },
        { @"((a)|b)*c", RegexOptions.None, "abac" },
        { @"x*", RegexOptions.None, "yyy" },
        { @"(\w+)\s\1", RegexOptions.None, "hello hello world" },
        { @"(?<x>\w)\k<x>", RegexOptions.None, "abccd" },
        { @"(?i)(ab)\1", RegexOptions.None, "abAB" },
        { @"(\d+)-\1", RegexOptions.None, "12-13 7-7" },
        { @"(a)|\1", RegexOptions.ECMAScript, "x" },
        { @"(a)|\1", RegexOptions.None, "x" },
        { @"(?<=\d{3})x", RegexOptions.None, "12x 123x" },
        { @"(?<!a)b", RegexOptions.None, "abcb" },
        { @"(?<=a+)b", RegexOptions.None, "aaab" },
        { @"(?<=(a)b)c\1", RegexOptions.None, "abca" },
        { @"(?<=(?<x>a)\k<x>)b", RegexOptions.None, "aab" },
        { @"(?=(\w+))\w", RegexOptions.None, "abc" },
        { @"\w+(?=!)", RegexOptions.None, "hey you!" },
        { @"\b(?!un)\w+", RegexOptions.None, "undo redo" },
        { @"(?>a+)b", RegexOptions.None, "aaab" },
        { @"(?>a+)ab", RegexOptions.None, "aaab" },
        { @"(?>(a)|ab)c", RegexOptions.None, "abc" },
        { @"^abc$", RegexOptions.Multiline, "x\nabc\ny" },
        { @"^abc$", RegexOptions.None, "x\nabc\ny" },
        { @"\Aab\z", RegexOptions.None, "ab" },
        { @"ab\Z", RegexOptions.None, "ab\n" },
        { @"$", RegexOptions.None, "ab\n" },
        { @"(?m)$", RegexOptions.None, "ab\ncd" },
        { @"\bfoo\b", RegexOptions.None, "afoo foo" },
        { @"\B\w", RegexOptions.None, " ab" },
        { @"(?s).+", RegexOptions.None, "a\nb" },
        { @".+", RegexOptions.None, "a\nb" },
        { @"(?i)straße", RegexOptions.None, "STRASSE straße" },
        { @"(?i)HeLLo", RegexOptions.None, "say hello" },
        { @"(?i:ab)c", RegexOptions.None, "ABc ABC" },
        { @"\p{Lu}\p{Ll}+", RegexOptions.None, "the Quick fox" },
        { @"[\w-[aeiou]]+", RegexOptions.None, "audio cbd" },
        { @"[^\s]+@\w+\.com", RegexOptions.None, "mail me@x.com now" },
        { @"(a)(?:b)(c)", RegexOptions.ExplicitCapture, "abc" },
        { @"a b # comment", RegexOptions.IgnorePatternWhitespace, "xab" },
        { @"(?:a|ab)*c", RegexOptions.None, "ababc" },
        { @"(?:x|xy|xyz)+z", RegexOptions.None, "xyzxyz" },
        { @"<.+?>", RegexOptions.None, "<a><b>" },
        { @"<.+>", RegexOptions.None, "<a><b>" },
        { @"\d+", RegexOptions.None, "no digits" },
        { @"^$", RegexOptions.None, string.Empty },
        { @"(?:)", RegexOptions.None, "abc" },
        { @"\bx", RegexOptions.None, "‍x x" },
        { @"\bx", RegexOptions.None, "‌x" },
        { @"\Bx", RegexOptions.None, "‍x" },
        { @"\bx", RegexOptions.ECMAScript, "‍x" },
        { @"(?m)^b", RegexOptions.None, "ab\nb" },
        { @"b$", RegexOptions.Multiline, "ab" },
        { @"(?!(a)b)\w", RegexOptions.None, "ab ac" },
        { @"(ab)(?<=\1)c", RegexOptions.None, "abc" },
        { @"(\w{3})(?<=\1\1)|x", RegexOptions.None, "abc" },
        { @"(?<=\1(a))b", RegexOptions.None, "aab" },
        { @"(?<1>a)\k<1>", RegexOptions.None, "aa" },
        { @"(a)?\1b", RegexOptions.None, "b" },
    };

    public static TheoryData<string, string> CommonPatternSamples()
    {
        TheoryData<string, string> cases = new TheoryData<string, string>();
        foreach (KeyValuePair<string, Func<Pattern>> entry in CommonPatternFactories)
        {
            Pattern pattern = entry.Value();
            IEnumerable<string> samples = pattern.GenerateMatching(SamplesPerKind, SampleSeed)
                .Concat(pattern.GenerateNonMatching(SamplesPerKind, SampleSeed));
            foreach (string sample in samples)
            {
                cases.Add(entry.Key, sample);
            }
        }

        return cases;
    }

    [Fact]
    public void Profile_NestedQuantifierOnHostileInput_ExhaustsBudgetAndBlamesInnerLoop()
    {
        ProfileReport report = Pattern.FromRegex(@"(a+)+$").Profile(HostileInput);

        report.BudgetExhausted.ShouldBeTrue();
        report.Steps.ShouldBe(1_000_000);
        report.IsMatch.ShouldBeFalse();
        report.AgreesWithEngine.ShouldBeFalse();
        report.Hotspots[0].Fragment.ShouldBe("a+");
        report.Hotspots[0].Start.ShouldBe(1);
        report.Hotspots[0].Length.ShouldBe(2);
    }

    [Fact]
    public void Profile_NestedQuantifier_StepsGrowExponentiallyWithInputLength()
    {
        Pattern pattern = Pattern.FromRegex(@"(a+)+$");
        ProfileOptions unlimited = new ProfileOptions { StepBudget = long.MaxValue };

        long[] steps = new[] { 8, 10, 12 }
            .Select(n => pattern.Profile(new string('a', n) + "!", unlimited).Steps)
            .ToArray();

        (steps[1] / (double)steps[0]).ShouldBeGreaterThan(3.5);
        (steps[2] / (double)steps[1]).ShouldBeGreaterThan(3.5);
    }

    [Fact]
    public void Profile_SimpleDigits_IsCheapAndAgreesWithEngine()
    {
        ProfileReport report = Pattern.With.Digit.Repeat.OneOrMore.Profile("order 12345 shipped");

        report.IsMatch.ShouldBeTrue();
        report.MatchIndex.ShouldBe(6);
        report.MatchLength.ShouldBe(5);
        report.AgreesWithEngine.ShouldBeTrue();
        report.BudgetExhausted.ShouldBeFalse();
        report.Steps.ShouldBeLessThan(50);
        report.Hotspots.Single().Fragment.ShouldBe(@"\d+");
    }

    [Theory]
    [MemberData(nameof(AgreementCases))]
    public void Profile_EmulatedConstruct_AgreesWithEngine(string regex, RegexOptions options, string input)
    {
        Match expected = new Regex(regex, options).Match(input);

        ProfileReport report = Pattern.FromRegex(regex, options).Profile(input);

        report.AgreesWithEngine.ShouldBeTrue(report.ToString());
        report.IsMatch.ShouldBe(expected.Success);
        report.MatchLength.ShouldBe(expected.Length);
    }

    [Theory]
    [MemberData(nameof(CommonPatternSamples))]
    public void Profile_CommonPatternOnGeneratedSample_AgreesWithEngine(string name, string sample)
    {
        ProfileReport report = CommonPatternFactories[name]().Profile(sample);

        report.AgreesWithEngine.ShouldBeTrue(report.ToString());
    }

    [Fact]
    public void Profile_LongInput_DoesNotOverflowTheStack()
    {
        string input = new string('a', 100_000) + "c";

        ProfileOptions options = new ProfileOptions { StepBudget = 10_000_000 };

        ProfileReport report = Pattern.FromRegex(@"(?:(a)|b)*c").Profile(input, options);

        report.IsMatch.ShouldBeTrue();
        report.MatchLength.ShouldBe(input.Length);
        report.AgreesWithEngine.ShouldBeTrue();
    }

    [Fact]
    public void Profile_SmallBudget_StopsAtTheBudget()
    {
        ProfileOptions options = new ProfileOptions { StepBudget = 5 };

        ProfileReport report = Pattern.With.Digit.Repeat.OneOrMore.Profile("abcdef123", options);

        report.BudgetExhausted.ShouldBeTrue();
        report.Steps.ShouldBe(5);
        report.IsMatch.ShouldBeFalse();
        report.MatchIndex.ShouldBe(-1);
        report.MatchLength.ShouldBe(0);
    }

    [Fact]
    public void Profile_Backtracking_CountsBacktracksOnTheQuantifier()
    {
        ProfileReport report = Pattern.FromRegex(@"a*ab").Profile("aaaab");

        report.Backtracks.ShouldBeGreaterThan(0);
        report.Hotspots.Single(h => h.Fragment == "a*").Backtracks.ShouldBe(report.Backtracks);
    }

    [Fact]
    public void Profile_Hotspots_AreOrderedBySteps()
    {
        ProfileReport report = Pattern.FromRegex(@"(\w+\s?)+$").Profile("hello big world!");

        report.Hotspots.Select(h => h.Steps).ShouldBeInOrder(SortDirection.Descending);
        report.Hotspots.Sum(h => h.Share).ShouldBe(1.0, tolerance: 1e-9);
        report.Hotspots.Sum(h => h.Steps).ShouldBe(report.Steps);
    }

    [Fact]
    public void ToString_CatastrophicPattern_RendersHeatmapTableAndVerdict()
    {
        string text = Pattern.FromRegex(@"(a+)+$").Profile(HostileInput).ToString();

        text.ShouldContain("Pattern  (a+)+$");
        text.ShouldContain("Heat     ▃██▃▃▃");
        text.ShouldContain("Result   gave up: the step budget of 1,000,000 ran out");
        text.ShouldContain("a+  (at 1)");
        text.ShouldContain("Verdict  catastrophic backtracking, mostly in 'a+'");
    }

    [Fact]
    public void ToString_CheapPattern_ReportsAgreementAndCheapVerdict()
    {
        string text = Pattern.With.Digit.Repeat.OneOrMore.Profile("order 12345 shipped").ToString();

        text.ShouldContain("Result   match at 6, length 5: 24 steps, 1 backtrack");
        text.ShouldContain("Engine   agrees with the .NET engine");
        text.ShouldContain("Verdict  cheap: 1.3 steps per input character.");
    }

    [Fact]
    public void ToString_OneStepPerCharacter_UsesTheSingular()
    {
        string text = Pattern.FromRegex("abc").Profile("abc").ToString();

        text.ShouldContain("Verdict  cheap: 1 step per input character.");
    }

    [Fact]
    public void ProfileHotspot_ToString_SummarizesTheConstruct()
    {
        ProfileHotspot hotspot = Pattern.With.Digit.Repeat.OneOrMore.Profile("12").Hotspots.Single();

        hotspot.ToString().ShouldBe(@"'\d+' at 0: 100.0% of steps, 6 steps, 1 backtrack");
    }

    [Theory]
    [InlineData(@"(?(x)a|b)", "conditional")]
    [InlineData(@"(?<o>a)(?<-o>b)", "balancing group")]
    [InlineData(@"\Ga", @"\G anchor")]
    public void Profile_UnsupportedConstruct_ThrowsNotSupportedNamingIt(string regex, string construct)
    {
        NotSupportedException error = Should.Throw<NotSupportedException>(() => Pattern.FromRegex(regex).Profile("ab"));

        error.Message.ShouldContain(construct);
    }

    [Fact]
    public void Profile_RightToLeft_ThrowsNotSupported()
    {
        NotSupportedException error = Should.Throw<NotSupportedException>(
            () => Pattern.FromRegex("a", RegexOptions.RightToLeft).Profile("a"));

        error.Message.ShouldContain("RightToLeft");
    }

    [Fact]
    public void Profile_NullInput_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => Pattern.With.Digit.Profile(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ProfileOptions_NonPositiveBudget_Throws(long budget)
    {
        ProfileOptions options = new ProfileOptions();

        Should.Throw<ArgumentOutOfRangeException>(() => options.StepBudget = budget);
    }

    [Fact]
    public void Profile_NonBacktrackingPattern_IsCheckedAgainstTheBacktrackingEngine()
    {
        ProfileReport report = Pattern.FromRegex("a+", RegexOptions.NonBacktracking).Profile("baa");

        report.AgreesWithEngine.ShouldBeTrue(report.ToString());
        report.MatchIndex.ShouldBe(1);
        report.MatchLength.ShouldBe(2);
    }

    [Fact]
    public void ToString_ModerateWork_ReportsModerateVerdict()
    {
        string text = Pattern.FromRegex(@"\w+x").Profile(new string('a', 20)).ToString();

        text.ShouldContain("Result   no match: 672 steps, 210 backtracks");
        text.ShouldContain("Verdict  moderate: 33.6 steps per input character, mostly in '\\w+'.");
    }

    [Fact]
    public void ToString_HeavyWork_ReportsHeavyVerdict()
    {
        string text = Pattern.FromRegex(@"\w*\w*\w*\w*x").Profile(new string('a', 14)).ToString();

        text.ShouldContain("Engine   agrees with the .NET engine");
        text.ShouldContain("Verdict  heavy backtracking: 3043.1 steps per input character, mostly in '\\w*'. Consider an atomic group or a narrower class.");
    }

    [Fact]
    public void ToString_ManyHotspots_ListsOnlyTheTopFive()
    {
        ProfileReport report = Pattern.FromRegex(@"\w*\w*\w*\w*\w*x").Profile(new string('a', 6));

        string text = report.ToString();

        report.Hotspots.Count.ShouldBe(6);
        text.Split('\n').Count(line => line.Contains("  (at ", StringComparison.Ordinal)).ShouldBe(5);
        text.ShouldNotContain($"{report.Hotspots[5].Fragment}  (at {report.Hotspots[5].Start})");
    }

    [Fact]
    public void ToString_ControlCharacterInPattern_IsShownAsPlaceholder()
    {
        string text = Pattern.FromRegex("a\tb").Profile("a\tb").ToString();

        text.ShouldContain("Pattern  a·b");
        text.ShouldContain("  ·  (at 1)");
    }

    [Fact]
    public void ToString_BudgetExhausted_SaysEngineWasNotChecked()
    {
        string text = Pattern.With.Digit.Repeat.OneOrMore.Profile("abcdef123", new ProfileOptions { StepBudget = 5 }).ToString();

        text.ShouldContain("Engine   not checked, because the step budget ran out");
        text.ShouldContain("ran out after 0 backtracks");
    }

    [Theory]
    [InlineData(true, 2, 1, "DISAGREES: the .NET engine found match at 2, length 1")]
    [InlineData(true, 0, 3, "DISAGREES: the .NET engine found match at 0, length 3")]
    [InlineData(false, -1, 0, "DISAGREES: the .NET engine found no match")]
    public void ToString_EngineDisagrees_ReportsEngineResultAndWarns(bool engineMatched, int engineIndex, int engineLength, string engineLine)
    {
        InterpreterResult interpreter = new InterpreterResult(new MatchSpan(true, 0, 1), 4, 0, false, new long[0], new long[0]);
        ProfileRun run = new ProfileRun("a", 3, 100, interpreter, new MatchSpan(engineMatched, engineIndex, engineLength));
        ProfileReport report = new ProfileReport(run, Array.Empty<ProfileHotspot>());

        string text = report.ToString();

        report.AgreesWithEngine.ShouldBeFalse();
        text.ShouldContain("Engine   " + engineLine);
        text.ShouldContain("Verdict  the interpreter and the .NET engine disagree, so these counts may not describe the real engine.");
    }

    [Fact]
    public void ToString_BudgetExhaustedWithoutHotspots_BlamesThePattern()
    {
        InterpreterResult interpreter = new InterpreterResult(MatchSpan.None, 5, 0, true, new long[0], new long[0]);
        ProfileReport report = new ProfileReport(new ProfileRun("a", 3, 5, interpreter, null), Array.Empty<ProfileHotspot>());

        report.ToString().ShouldContain("Verdict  catastrophic backtracking, mostly in the pattern.");
    }

    [Theory]
    [InlineData("(?(x)a|b)", "conditional '(?(x)a|b)' at position 0")]
    [InlineData("(?<o>a)(?<-o>b)", "balancing group '(?<-o>' at position 7")]
    [InlineData(@"\k<nope>", @"The backreference '\k<nope>' refers to an unknown group.")]
    [InlineData(@"\5", @"The backreference '\5' refers to an unknown group.")]
    public void ProgramCompiler_ConstructRejectedBeforeCompilation_ThrowsNotSupportedWhenCompiledDirectly(string regex, string message)
    {
        RegexTree tree = RegexParser.Parse(regex);

        NotSupportedException error = Should.Throw<NotSupportedException>(() => ProgramCompiler.Compile(tree));

        error.Message.ShouldContain(message);
    }

    [Fact]
    public void AnchorEvaluator_ContiguousMatchAnchor_ThrowsNotSupported()
    {
        NotSupportedException error = Should.Throw<NotSupportedException>(
            () => AnchorEvaluator.IsSatisfied(AnchorKind.ContiguousMatch, RegexOptions.None, "a", 0));

        error.Message.ShouldBe("The backtracking profiler does not emulate the anchor ContiguousMatch.");
    }

    [Fact]
    public void MatchSpan_ToString_DescribesMatchOrNoMatch()
    {
        new MatchSpan(true, 2, 3).ToString().ShouldBe("match at 2, length 3");
        MatchSpan.None.ToString().ShouldBe("no match");
    }

    [Fact]
    public void ProfileOptions_DefaultBudget_IsOneMillion()
    {
        new ProfileOptions().StepBudget.ShouldBe(1_000_000);
    }
}
