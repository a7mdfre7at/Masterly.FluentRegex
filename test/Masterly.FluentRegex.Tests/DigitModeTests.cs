using System.Reflection;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Tests.Infrastructure;

namespace Masterly.FluentRegex.Tests;

public class DigitModeTests
{
    private const string ArabicIndicNine = "٩";
    private const string ArabicIndicYear = "٢٠٢٦";

    public static IEnumerable<object[]> DigitModeFactories() =>
        typeof(CommonPatterns)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetParameters().LastOrDefault()?.ParameterType == typeof(DigitMode))
            .Select(method => new object[] { Describe(method) });

    [Theory]
    [InlineData(@"^\d{4}$", "^[0-9]{4}$")]
    [InlineData(@"\D+", "[^0-9]+")]
    [InlineData(@"[\d.]+", "[0-9.]+")]
    [InlineData(@"[^\d]", "[^0-9]")]
    [InlineData(@"[\d-[5]]", "[0-9-[5]]")]
    [InlineData(@"[a-z-[\d]]", "[a-z-[0-9]]")]
    [InlineData(@"(?<=\d)x(?!\d)", "(?<=[0-9])x(?![0-9])")]
    [InlineData(@"(?<year>\d{4})-\k<year>", "(?<year>[0-9]{4})-\\k<year>")]
    public void WithAsciiDigits_DigitShorthands_AreRewritten(string regex, string expected)
    {
        Pattern pattern = Pattern.FromRegex(regex);

        string rewritten = pattern.WithAsciiDigits().ToString();

        rewritten.ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"\\d")]
    [InlineData(@"\p{Nd}+")]
    [InlineData(@"\w+")]
    [InlineData(@"[a-f]")]
    public void WithAsciiDigits_NoDigitShorthand_ReturnsSameInstance(string regex)
    {
        Pattern pattern = Pattern.FromRegex(regex);

        Pattern result = pattern.WithAsciiDigits();

        result.ShouldBeSameAs(pattern);
    }

    [Fact]
    public void WithAsciiDigits_EmptyPattern_ReturnsSameInstance()
    {
        Pattern pattern = Pattern.With;

        Pattern result = pattern.WithAsciiDigits();

        result.ShouldBeSameAs(pattern);
    }

    [Fact]
    public void WithAsciiDigits_ECMAScriptOption_ReturnsSameInstance()
    {
        Pattern pattern = Pattern.FromRegex(@"\d+", RegexOptions.ECMAScript);

        Pattern result = pattern.WithAsciiDigits();

        result.ShouldBeSameAs(pattern);
    }

    [Fact]
    public void WithAsciiDigits_RejectsUnicodeDigitsAndKeepsAscii()
    {
        Pattern year = Pattern.FromRegex(@"^\d{4}$");

        Pattern ascii = year.WithAsciiDigits();

        year.IsMatch(ArabicIndicYear).ShouldBeTrue();
        ascii.IsMatch(ArabicIndicYear).ShouldBeFalse();
        ascii.IsMatch("2026").ShouldBeTrue();
    }

    [Fact]
    public void WithAsciiDigits_PreservesOptionsAndTimeout()
    {
        TimeSpan timeout = TimeSpan.FromSeconds(2);
        Pattern pattern = Pattern.FromRegex(@"^id-\d+$", RegexOptions.IgnoreCase).WithTimeout(timeout);

        Pattern ascii = pattern.WithAsciiDigits();

        ascii.Options.ShouldBe(RegexOptions.IgnoreCase);
        ascii.Timeout.ShouldBe(timeout);
        ascii.IsMatch("ID-42").ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"[\Dx]", "(?:[^0-9]|x)")]
    [InlineData(@"[^\D5]", "[0-46-9]")]
    [InlineData(@"[\D\d]", @"[\s\S]")]
    [InlineData(@"[^\d\D]", @"[^\s\S]")]
    [InlineData(@"\D", "[^0-9]")]
    public void WithAsciiDigits_NonDigitInsideSets_MatchesExactlyTheAsciiMeaning(string regex, string asciiMeaning)
    {
        Pattern rewritten = Pattern.FromRegex(regex).WithAsciiDigits();

        bool equivalent = rewritten.IsEquivalentTo(Pattern.FromRegex(asciiMeaning));

        equivalent.ShouldBeTrue(rewritten.ToString());
    }

    [Theory]
    [InlineData("5", false)]
    [InlineData(ArabicIndicNine, true)]
    [InlineData("a", true)]
    public void WithAsciiDigits_NonDigitInsideSet_TreatsUnicodeDigitsAsNonDigits(string input, bool expected)
    {
        Pattern nonDigit = Pattern.FromRegex(@"^[\D]$").WithAsciiDigits();

        bool isMatch = nonDigit.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Fact]
    public void WithDigits_Unicode_ReturnsSameInstance()
    {
        Pattern pattern = Pattern.FromRegex(@"\d+");

        Pattern result = pattern.WithDigits(DigitMode.Unicode);

        result.ShouldBeSameAs(pattern);
    }

    [Fact]
    public void WithDigits_Ascii_EqualsWithAsciiDigits()
    {
        Pattern pattern = Pattern.FromRegex(@"\d+");

        Pattern result = pattern.WithDigits(DigitMode.Ascii);

        result.ToString().ShouldBe(pattern.WithAsciiDigits().ToString());
    }

    [Fact]
    public void WithDigits_UndefinedMode_Throws()
    {
        Pattern pattern = Pattern.FromRegex(@"\d+");

        Should.Throw<ArgumentOutOfRangeException>(() => pattern.WithDigits((DigitMode)42));
    }

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("0.0.0." + ArabicIndicNine, false)]
    public void IPv4_AsciiMode_AcceptsOnlyAsciiDigits(string input, bool expected)
    {
        Pattern ip = CommonPatterns.IPv4(DigitMode.Ascii);

        bool isMatch = ip.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Fact]
    public void IPv4_DefaultAndUnicodeMode_KeepUnicodeDigits()
    {
        string input = "0.0.0." + ArabicIndicNine;

        bool byDefault = CommonPatterns.IPv4().IsMatch(input);
        bool unicode = CommonPatterns.IPv4(DigitMode.Unicode).IsMatch(input);

        byDefault.ShouldBeTrue();
        unicode.ShouldBeTrue();
    }

    [Fact]
    public void EmailWithStrictness_AsciiMode_RejectsUnicodeDigits()
    {
        Pattern email = CommonPatterns.Email(PatternStrictness.Standard, DigitMode.Ascii);

        bool isMatch = email.IsMatch($"user{ArabicIndicNine}@example.com");

        isMatch.ShouldBeFalse();
        email.IsMatch("user9@example.com").ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(DigitModeFactories))]
    public void CommonPatterns_AsciiMode_HasNoDigitShorthandsAndIsValid(string name)
    {
        Pattern ascii = Create(name, DigitMode.Ascii);

        string text = ascii.ToString();

        text.ShouldNotContain(@"\d");
        text.ShouldNotContain(@"\D");
        ascii.IsValid().ShouldBeTrue(name);
    }

    [Theory]
    [MemberData(nameof(DigitModeFactories))]
    public void CommonPatterns_AsciiMode_AcceptsOnlyStringsTheUnicodeModeAccepts(string name)
    {
        // Negated sets and \w can still produce non-ASCII digits; only \d and \D change meaning.
        Pattern ascii = Create(name, DigitMode.Ascii);
        Pattern unicode = Create(name, DigitMode.Unicode);

        string[] samples = ascii.GenerateMatching(25, seed: 7);

        samples.ShouldAllBe(sample => unicode.IsMatch(sample), name);
    }

    [Theory]
    [MemberData(nameof(DigitModeFactories))]
    public void CommonPatterns_UnicodeMode_IsTheParameterlessPattern(string name)
    {
        Pattern unicode = Create(name, DigitMode.Unicode);

        string text = unicode.ToString();

        text.ShouldBe(CreateDefault(name).ToString());
    }

    [Fact]
    public void GeneratePattern_AsciiCommonPattern_IsEvaluatedAtCompileTime()
    {
        AnalyzerHarness.GeneratorResult result = AnalyzerHarness.RunGenerator("""
            using Masterly.FluentRegex;
            namespace Demo;
            public static partial class Network
            {
                [GeneratePattern]
                public static Pattern Address => CommonPatterns.IPv4(DigitMode.Ascii);
            }
            """);

        result.GeneratorDiagnostics.ShouldBeEmpty();
        Type network = AnalyzerHarness.Emit(result.Output).GetType("Demo.Network")!;
        Regex regex = (Regex)network.GetMethod("AddressRegex")!.Invoke(null, null)!;
        regex.ToString().ShouldNotContain(@"\d");
        regex.IsMatch("10.0.0.1").ShouldBeTrue();
        regex.IsMatch("0.0.0." + ArabicIndicNine).ShouldBeFalse();
    }

    private static string Describe(MethodInfo method) =>
        method.GetParameters().Length == 1 ? method.Name : $"{method.Name}:{nameof(PatternStrictness.Strict)}";

    private static Pattern Create(string name, DigitMode digits)
    {
        string[] parts = name.Split(':');
        return parts.Length == 1
            ? (Pattern)typeof(CommonPatterns).GetMethod(parts[0], new[] { typeof(DigitMode) })!.Invoke(null, new object[] { digits })!
            : (Pattern)typeof(CommonPatterns).GetMethod(parts[0], new[] { typeof(PatternStrictness), typeof(DigitMode) })!
                .Invoke(null, new object[] { PatternStrictness.Strict, digits })!;
    }

    private static Pattern CreateDefault(string name)
    {
        string[] parts = name.Split(':');
        return parts.Length == 1
            ? (Pattern)typeof(CommonPatterns).GetMethod(parts[0], Type.EmptyTypes)!.Invoke(null, null)!
            : (Pattern)typeof(CommonPatterns).GetMethod(parts[0], new[] { typeof(PatternStrictness) })!
                .Invoke(null, new object[] { PatternStrictness.Strict })!;
    }
}
