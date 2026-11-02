using System.Globalization;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class UnicodeTests
{
    public record IntValue(int Value);

    public record LongValue(long Value);

    public record DecimalValue(decimal Value);

    public record DoubleValue(double Value);

    private const int BmpSize = 0x10000;

    private static readonly Pattern Number = Pattern.With.NamedGroup("value", Pattern.With.Digit.Repeat.OneOrMore);

    private static readonly Pattern FractionPart = Pattern.With.AnyOf(".\u066B").Digit.Repeat.OneOrMore;

    private static readonly Pattern Fraction = Pattern.With.NamedGroup(
        "value",
        Pattern.With.Digit.Repeat.OneOrMore.NonCapturingGroup(FractionPart).Repeat.Optional);

    // ------------------------------------------------------------------
    // Script
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(UnicodeScript.Greek, "λ", true)]
    [InlineData(UnicodeScript.Greek, "a", false)]
    [InlineData(UnicodeScript.Cyrillic, "ж", true)]
    [InlineData(UnicodeScript.Arabic, "م", true)]
    [InlineData(UnicodeScript.Arabic, "\u064F", true)]
    [InlineData(UnicodeScript.Arabic, "a", false)]
    [InlineData(UnicodeScript.Hebrew, "ש", true)]
    [InlineData(UnicodeScript.Devanagari, "क", true)]
    [InlineData(UnicodeScript.Thai, "ก", true)]
    [InlineData(UnicodeScript.Hangul, "한", true)]
    [InlineData(UnicodeScript.Hiragana, "あ", true)]
    [InlineData(UnicodeScript.Katakana, "カ", true)]
    [InlineData(UnicodeScript.Han, "字", true)]
    [InlineData(UnicodeScript.Han, "あ", false)]
    [InlineData(UnicodeScript.Latin, "\u00E9", true)]
    [InlineData(UnicodeScript.Latin, "1", false)]
    [InlineData(UnicodeScript.Latin, "!", false)]
    public void Script_SingleCharacter_MatchesOnlyThatScript(UnicodeScript script, string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfInput.Script(script).EndOfInput;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Fact]
    public void Script_Arabic_MatchesWordWithHarakatAsOneRun()
    {
        Pattern pattern = Pattern.With.Script(UnicodeScript.Arabic).Repeat.OneOrMore;

        string match = pattern.Match("name: مُحَمَّد!").Value;

        match.ShouldBe("مُحَمَّد");
    }

    [Fact]
    public void Script_InsideSet_CombinesWithOtherMembers()
    {
        Pattern greekOrDigit = Pattern.With.Set(Pattern.With.Script(UnicodeScript.Greek).Digit);
        Pattern pattern = Pattern.With.StartOfInput.Group(greekOrDigit.Repeat.OneOrMore).EndOfInput;

        pattern.IsMatch("αβγ123").ShouldBeTrue();
        pattern.IsMatch("abc").ShouldBeFalse();
    }

    [Fact]
    public void Script_UndefinedValue_ThrowsArgumentOutOfRange()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Pattern.With.Script((UnicodeScript)999));
    }

    // ------------------------------------------------------------------
    // LetterIn
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(UnicodeScript.Arabic, "م", true)]
    [InlineData(UnicodeScript.Arabic, "٣", false)]
    [InlineData(UnicodeScript.Arabic, "\u064E", false)]
    [InlineData(UnicodeScript.Arabic, "،", false)]
    [InlineData(UnicodeScript.Greek, "Ω", true)]
    [InlineData(UnicodeScript.Latin, "\u0301", false)]
    [InlineData(UnicodeScript.Devanagari, "०", false)]
    public void LetterIn_SingleCharacter_MatchesOnlyLetters(UnicodeScript script, string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfInput.LetterIn(script).EndOfInput;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Fact]
    public void LetterIn_Standalone_RendersClassSubtraction()
    {
        string regex = Pattern.With.LetterIn(UnicodeScript.Arabic).ToString();

        regex.ShouldEndWith(@"-[\P{L}]]");
    }

    [Theory]
    [InlineData(UnicodeScript.Arabic)]
    [InlineData(UnicodeScript.Latin)]
    [InlineData(UnicodeScript.Han)]
    public void LetterIn_InsideSet_MatchesTheSameCharactersAsStandalone(UnicodeScript script)
    {
        Regex standalone = Pattern.With.LetterIn(script).ToRegex();
        Regex inSet = Pattern.With.Set(Pattern.With.LetterIn(script)).ToRegex();

        IEnumerable<char> mismatches = AllBmpChars()
            .Where(c => standalone.IsMatch(c.ToString()) != inSet.IsMatch(c.ToString()));

        mismatches.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------
    // DigitIn / AsciiDigit
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("٢٠٢٦", true)]
    [InlineData("۲۰۲۶", true)]
    [InlineData("2026", false)]
    public void DigitIn_Arabic_MatchesArabicIndicAndExtendedArabicIndic(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfInput.DigitIn(UnicodeScript.Arabic).Repeat.Times(4).EndOfInput;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData(UnicodeScript.Latin, 10)]
    [InlineData(UnicodeScript.Arabic, 20)]
    [InlineData(UnicodeScript.Devanagari, 10)]
    [InlineData(UnicodeScript.Bengali, 10)]
    [InlineData(UnicodeScript.Gurmukhi, 10)]
    [InlineData(UnicodeScript.Gujarati, 10)]
    [InlineData(UnicodeScript.Tamil, 10)]
    [InlineData(UnicodeScript.Telugu, 10)]
    [InlineData(UnicodeScript.Kannada, 10)]
    [InlineData(UnicodeScript.Malayalam, 10)]
    [InlineData(UnicodeScript.Thai, 10)]
    [InlineData(UnicodeScript.Lao, 10)]
    [InlineData(UnicodeScript.Tibetan, 10)]
    [InlineData(UnicodeScript.Khmer, 10)]
    [InlineData(UnicodeScript.Mongolian, 10)]
    public void DigitIn_SupportedScript_MatchesOnlyDecimalDigits(UnicodeScript script, int expectedCount)
    {
        Regex regex = Pattern.With.DigitIn(script).ToRegex();

        List<char> members = AllBmpChars().Where(c => regex.IsMatch(c.ToString())).ToList();

        members.Count.ShouldBe(expectedCount);
        members.ShouldAllBe(c => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.DecimalDigitNumber);
    }

    [Theory]
    [InlineData(UnicodeScript.Greek)]
    [InlineData(UnicodeScript.Hebrew)]
    [InlineData(UnicodeScript.Han)]
    [InlineData(UnicodeScript.Ethiopic)]
    public void DigitIn_ScriptWithoutOwnDigits_ThrowsArgumentException(UnicodeScript script)
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() => Pattern.With.DigitIn(script));

        ex.Message.ShouldContain("has no decimal digits");
    }

    [Fact]
    public void DigitIn_InsideSet_CombinesWithAsciiDigits()
    {
        Pattern digit = Pattern.With.Set(Pattern.With.AsciiDigit.DigitIn(UnicodeScript.Arabic));
        Pattern pattern = Pattern.With.StartOfInput.Group(digit.Repeat.OneOrMore).EndOfInput;

        pattern.IsMatch("12٣۴").ShouldBeTrue();
        pattern.IsMatch("१२").ShouldBeFalse();
    }

    [Theory]
    [InlineData("7", true)]
    [InlineData("٧", false)]
    [InlineData("७", false)]
    public void AsciiDigit_SingleCharacter_MatchesOnlyZeroToNine(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfInput.AsciiDigit.EndOfInput;

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Fact]
    public void Digit_ArabicIndicDigit_MatchesBecauseDotNetDigitIsUnicodeAware()
    {
        bool isMatch = Pattern.With.StartOfInput.Digit.EndOfInput.IsMatch("٧");

        isMatch.ShouldBeTrue();
    }

    [Fact]
    public void AsciiDigit_InsideSet_RendersBareRange()
    {
        string regex = Pattern.With.Set(Pattern.With.AsciiDigit.Literal("_")).ToString();

        regex.ShouldBe("[0-9_]");
    }

    // ------------------------------------------------------------------
    // CodePoint
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("a😀b", 3)]
    [InlineData("😀😀", 2)]
    [InlineData("", 0)]
    public void CodePoint_Count_CountsScalarValues(string input, int expected)
    {
        int count = Pattern.With.CodePoint.Count(input);

        count.ShouldBe(expected);
    }

    [Fact]
    public void CodePoint_LoneSurrogate_IsNotMatched()
    {
        string loneHighSurrogate = new string('\uD800', 1);

        int count = Pattern.With.CodePoint.Count(loneHighSurrogate);

        count.ShouldBe(0);
    }

    [Fact]
    public void CodePoint_Quantified_RepeatsWholeScalar()
    {
        Pattern pattern = Pattern.With.StartOfInput.CodePoint.Repeat.Times(2).EndOfInput;

        pattern.IsMatch("😀😀").ShouldBeTrue();
        pattern.IsMatch("😀😀😀").ShouldBeFalse();
    }

    [Fact]
    public void CodePoint_InsideSet_ThrowsWhenRendered()
    {
        Pattern pattern = Pattern.With.Set(Pattern.With.CodePoint);

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => pattern.ToString());

        ex.Message.ShouldContain("CodePoint");
    }

    // ------------------------------------------------------------------
    // Grapheme
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("👨\u200D👩\u200D👧", 1)]
    [InlineData("\u00E9a", 2)]
    [InlineData("e\u0301a", 2)]
    [InlineData("🇯🇵🇺🇸", 2)]
    [InlineData("1\uFE0F\u20E3", 1)]
    [InlineData("👍🏽", 1)]
    [InlineData("❤\uFE0F", 1)]
    [InlineData("\r\n", 1)]
    [InlineData("\u1112\u1161\u11AB", 1)]
    [InlineData("مُحَمَّد", 4)]
    [InlineData("abc", 3)]
    [InlineData("", 0)]
    public void Grapheme_Count_CountsUserPerceivedCharacters(string input, int expected)
    {
        int count = Pattern.With.Grapheme.Count(input);

        count.ShouldBe(expected);
    }

    [Fact]
    public void Grapheme_FamilyEmoji_IsExactlyOneMatchCoveringTheWholeSequence()
    {
        const string family = "👨\u200D👩\u200D👧";

        MatchCollection matches = Pattern.With.Grapheme.Matches(family);

        matches.Count.ShouldBe(1);
        matches[0].Value.ShouldBe(family);
    }

    [Fact]
    public void Grapheme_Quantified_RepeatsWholeClusters()
    {
        Pattern pattern = Pattern.With.StartOfInput.Grapheme.Repeat.Times(2).EndOfInput;

        pattern.IsMatch("e\u0301a").ShouldBeTrue();
        pattern.IsMatch("e\u0301ab").ShouldBeFalse();
    }

    [Fact]
    public void Grapheme_InsideSet_ThrowsWhenRendered()
    {
        Pattern pattern = Pattern.With.NegatedSet(Pattern.With.Grapheme);

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => pattern.ToString());

        ex.Message.ShouldContain("Grapheme");
    }

    // ------------------------------------------------------------------
    // IgnoreDiacritics
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("محمد", "مُحَمَّد", true)]
    [InlineData("مُحَمَّد", "محمد", true)]
    [InlineData("محمد", "م\u0640ح\u0640م\u0640د", true)]
    [InlineData("ا", "أ", true)]
    [InlineData("ا", "إ", true)]
    [InlineData("ا", "آ", true)]
    [InlineData("cafe", "caf\u00E9", true)]
    [InlineData("cafe", "cafe\u0301", true)]
    [InlineData("caf\u00E9", "cafe", true)]
    [InlineData("Ahmad", "Ahmad", true)]
    [InlineData("resume", "r\u00E9sum\u00E9", true)]
    [InlineData("cafe", "cafx", false)]
    [InlineData("cafe", "CAF\u00C9", false)]
    public void IgnoreDiacritics_Literal_MatchesWithOrWithoutDiacritics(string literal, string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfInput.Literal(literal).EndOfInput.IgnoreDiacritics();

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("CAF\u00C9")]
    [InlineData("caf\u00E9")]
    [InlineData("CAFE\u0301")]
    public void IgnoreDiacritics_IgnoreCase_MatchesVariantsOfBothCases(string input)
    {
        Pattern pattern = Pattern.With.StartOfInput.Literal("cafe").EndOfInput
            .WithOptions(RegexOptions.IgnoreCase)
            .IgnoreDiacritics();

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBeTrue();
    }

    [Fact]
    public void IgnoreDiacritics_KeepsOptionsAndTimeout()
    {
        TimeSpan timeout = TimeSpan.FromSeconds(2);
        Pattern original = Pattern.With.Literal("abc").WithOptions(RegexOptions.Multiline).WithTimeout(timeout);

        Pattern rewritten = original.IgnoreDiacritics();

        rewritten.Options.ShouldBe(RegexOptions.Multiline);
        rewritten.Timeout.ShouldBe(timeout);
    }

    [Fact]
    public void IgnoreDiacritics_LeavesOriginalPatternUnchanged()
    {
        Pattern original = Pattern.With.Literal("cafe");

        _ = original.IgnoreDiacritics();

        original.ToString().ShouldBe("cafe");
        original.IsMatch("caf\u00E9").ShouldBeFalse();
    }

    [Theory]
    [InlineData("r\u00E9sum\u00E9")]
    [InlineData("re\u0301sume\u0301")]
    [InlineData("resume")]
    public void IgnoreDiacritics_CharacterClass_MatchesPrecomposedAndDecomposedLetters(string input)
    {
        Pattern letters = Pattern.With.Set(Pattern.With.Range('a', 'z')).Repeat.OneOrMore;
        Pattern pattern = Pattern.With.StartOfInput.Group(letters).EndOfInput.IgnoreDiacritics();

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBeTrue();
    }

    [Fact]
    public void IgnoreDiacritics_QuantifiedLiteral_RepeatsLetterWithItsMarks()
    {
        Pattern pattern = Pattern.With.StartOfInput.Literal("e").Repeat.OneOrMore.EndOfInput.IgnoreDiacritics();

        pattern.IsMatch("\u00E9e\u0301\u00EA").ShouldBeTrue();
    }

    [Fact]
    public void IgnoreDiacritics_GroupsAndAlternation_AreRewrittenInside()
    {
        Pattern pattern = Pattern.FromRegex("^(?<word>cafe|the)$").IgnoreDiacritics();

        pattern.Match("caf\u00E9").Groups["word"].Value.ShouldBe("caf\u00E9");
        pattern.IsMatch("th\u00E9").ShouldBeTrue();
    }

    [Theory]
    [InlineData("محمد", true)]
    [InlineData("مـحمد", true)]
    [InlineData("مُحمد", true)]
    [InlineData("مـ", false)]
    public void IgnoreDiacritics_LiteralTatweel_IsCoveredByTheMarkRun(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfInput.Literal("مـحمد").EndOfInput.IgnoreDiacritics();

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Fact]
    public void IgnoreDiacritics_NonLetterLiterals_StayExact()
    {
        Pattern pattern = Pattern.With.StartOfInput.Literal("a=b").EndOfInput.IgnoreDiacritics();

        pattern.IsMatch("a=b").ShouldBeTrue();
        pattern.IsMatch("a\u2260b").ShouldBeFalse();
    }

    [Fact]
    public void IgnoreDiacritics_WordClassFollowedByManyMarks_FailsWithoutCatastrophicBacktracking()
    {
        Pattern pattern = Pattern.FromRegex(@"^\w+$").WithTimeout(TimeSpan.FromSeconds(2)).IgnoreDiacritics();
        string input = "a" + new string('\u0301', 40) + "!";

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBeFalse();
    }

    [Fact]
    public void IgnoreDiacritics_EmptyPattern_ReturnsEmptyPattern()
    {
        Pattern pattern = Pattern.With.IgnoreDiacritics();

        pattern.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void IgnoreDiacritics_EcmaScriptOptions_ThrowsInvalidOperation()
    {
        Pattern pattern = Pattern.With.Literal("abc").WithOptions(RegexOptions.ECMAScript);

        Should.Throw<InvalidOperationException>(() => pattern.IgnoreDiacritics());
    }

    [Fact]
    public void IgnoreDiacritics_NonBacktrackingEngine_MatchesDiacritics()
    {
        Pattern pattern = Pattern.FromRegex(@"^\w+$").UseNonBacktracking().IgnoreDiacritics();

        pattern.IsMatch("re\u0301sume\u0301").ShouldBeTrue();
    }

    [Theory]
    [InlineData("a\u00E9", true)]
    [InlineData("ae\u0301", true)]
    [InlineData("\u00F3", true)]
    [InlineData("o", true)]
    [InlineData("a\u00F3", false)]
    [InlineData("\u00E9", false)]
    public void IgnoreDiacritics_ConditionalWithNoBranch_RewritesBothBranches(string input, bool expected)
    {
        Pattern pattern = Pattern.FromRegex("^(a)?(?(1)e|o)$").IgnoreDiacritics();

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a\u00E9", true)]
    [InlineData("", true)]
    [InlineData("a", false)]
    public void IgnoreDiacritics_ConditionalWithoutNoBranch_RewritesTheYesBranch(string input, bool expected)
    {
        Pattern pattern = Pattern.FromRegex("^(a)?(?(1)e)$").IgnoreDiacritics();

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("cafe", true)]
    [InlineData("caf\u00E9", true)]
    [InlineData("cafe\u0301\u0301", true)]
    [InlineData("cafx", false)]
    public void IgnoreDiacritics_QuantifiedNonSpacingMark_IsCoveredByTheMarkRun(string input, bool expected)
    {
        Pattern pattern = Pattern.FromRegex("^cafe\u0301+$").IgnoreDiacritics();

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("\u0915", true)]
    [InlineData("\u0915\u0903", true)]
    [InlineData("\u0915\u0903\u0903", false)]
    public void IgnoreDiacritics_SpacingCombiningMarkLiteral_BecomesOptional(string input, bool expected)
    {
        Pattern pattern = Pattern.With.StartOfInput.Literal("\u0915\u0903").EndOfInput.IgnoreDiacritics();

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a==b", true)]
    [InlineData("a=b", true)]
    [InlineData("ab", false)]
    public void IgnoreDiacritics_QuantifiedNonLetterLiteral_KeepsItsQuantifier(string input, bool expected)
    {
        Pattern pattern = Pattern.FromRegex("^a=+b$").IgnoreDiacritics();

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"^[0-9]+$", "123", true)]
    [InlineData(@"^[0-9]+$", "1\u0301", false)]
    [InlineData(@"^\d+$", "1\u0301", false)]
    public void IgnoreDiacritics_ClassWithoutLetters_GetsNoMarkRun(string regex, string input, bool expected)
    {
        Pattern pattern = Pattern.FromRegex(regex).IgnoreDiacritics();

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"[0-9]")]
    [InlineData(@"[0-9]+")]
    [InlineData(@"=+")]
    [InlineData(@"\d{2}")]
    public void IgnoreDiacritics_QuantifiedOrBareAtomWithoutLetters_IsRenderedUnchanged(string regex)
    {
        Pattern pattern = Pattern.FromRegex(regex).IgnoreDiacritics();

        string rendered = pattern.ToString();

        rendered.ShouldBe(regex);
    }

    [Fact]
    public void IgnoreDiacritics_QuantifiedNonSpacingMark_AddsNothingBeyondTheMarkRun()
    {
        string withQuantifiedMark = Pattern.FromRegex("^cafe\u0301+$").IgnoreDiacritics().ToString();
        string withoutMark = Pattern.FromRegex("^cafe$").IgnoreDiacritics().ToString();

        withQuantifiedMark.ShouldBe(withoutMark);
    }

    [Theory]
    [InlineData("1\u00E1", true)]
    [InlineData("b\u0301c", true)]
    [InlineData("\u00E7", true)]
    [InlineData("x", false)]
    public void IgnoreDiacritics_ClassWithShorthandAndRange_ExpandsOnlyTheRange(string input, bool expected)
    {
        Pattern pattern = Pattern.FromRegex(@"^[\da-c]+$").IgnoreDiacritics();

        bool isMatch = pattern.IsMatch(input);

        isMatch.ShouldBe(expected);
    }

    // ------------------------------------------------------------------
    // Unicode digits in typed extraction
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("١٬٢٣٤", "", "1234")]
    [InlineData("١٬٢٣٤٫٥", "de-DE", "1234.5")]
    public void Extract_DecimalWithArabicThousandsSeparator_UsesTheProviderGroupSeparator(string input, string culture, string expected)
    {
        Pattern grouped = Pattern.With.NamedGroup("value", Pattern.With.AnyOf("0123456789٠١٢٣٤٥٦٧٨٩٫٬").Repeat.OneOrMore);

        DecimalValue extracted = grouped.Extract<DecimalValue>(input, new CultureInfo(culture));

        extracted.Value.ShouldBe(decimal.Parse(expected, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("٢٠٢٦", 2026)]
    [InlineData("۲۰۲۶", 2026)]
    [InlineData("२०२६", 2026)]
    [InlineData("2026", 2026)]
    public void Extract_IntWithUnicodeDigits_ConvertsToTheValue(string input, int expected)
    {
        IntValue extracted = Number.Extract<IntValue>(input);

        extracted.Value.ShouldBe(expected);
    }

    [Fact]
    public void Extract_LongWithArabicIndicDigits_ConvertsToTheValue()
    {
        LongValue extracted = Number.Extract<LongValue>("٩٠٠٧١٩٩٢٥٤٧٤٠٩٩٣");

        extracted.Value.ShouldBe(9007199254740993L);
    }

    [Theory]
    [InlineData("١٢\u066B٥٠", "12.50")]
    [InlineData("١٢.٥", "12.5")]
    [InlineData("۳", "3")]
    public void Extract_DecimalWithArabicIndicDigitsAndSeparator_ConvertsToTheValue(string input, string expected)
    {
        DecimalValue extracted = Fraction.Extract<DecimalValue>(input);

        extracted.Value.ShouldBe(decimal.Parse(expected, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Extract_DoubleWithDevanagariDigits_ConvertsToTheValue()
    {
        DoubleValue extracted = Fraction.Extract<DoubleValue>("३.२५");

        extracted.Value.ShouldBe(3.25);
    }

    [Fact]
    public void TryExtract_IntWithUnicodeDigits_ReturnsTrue()
    {
        bool extracted = Number.TryExtract("٤٢", out IntValue? result);

        extracted.ShouldBeTrue();
        result!.Value.ShouldBe(42);
    }

    private static IEnumerable<char> AllBmpChars() =>
        Enumerable.Range(0, BmpSize).Select(code => (char)code).Where(c => !char.IsSurrogate(c));
}
