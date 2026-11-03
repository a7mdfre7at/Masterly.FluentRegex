using Masterly.FluentRegex.Learning;

namespace Masterly.FluentRegex.Tests;

public class LearnTests
{
    private static readonly string[] Invoices = { "INV-2024-0001", "INV-2025-0420" };
    private static readonly string[] InvoiceRejects = { "INV-24-1", "inv-2024-0001" };

    public static TheoryData<string[], string[], string> LearnedPatterns => new()
    {
        { new[] { "cat", "dog", "bird" }, Array.Empty<string>(), "^[a-z]{3,4}$" },
        { new[] { "cat", "dog", "bird" }, new[] { "cow" }, "^(?:bird|cat|dog)$" },
        { new[] { "2024-01-02", "2025/11/30" }, Array.Empty<string>(), @"^\d{4}[\-/]\d{2}[\-/]\d{2}$" },
        { new[] { "AB12", "A1B2" }, Array.Empty<string>(), @"^\w{4}$" },
        { new[] { "Hello", "World" }, Array.Empty<string>(), "^[a-zA-Z]{5}$" },
        { new[] { "v1.2.3", "v10.20.30" }, Array.Empty<string>(), @"^v\d{1,2}\.\d{1,2}\.\d{1,2}$" },
        { new[] { "hello world", "goodbye  moon" }, Array.Empty<string>(), @"^[a-z]{5,7}\s{1,2}[a-z]{4,5}$" },
        { new[] { "abc", "12-34" }, Array.Empty<string>(), "^(?:12-34|abc)$" },
        { new[] { "2024", "2025" }, new[] { "2026" }, "^202[45]$" },
        { new[] { "INV-2024-0001", "INV-2025-0420" }, new[] { "INV-2024-9999" }, @"^INV-\d{4}-0\d{3}$" },
        { new[] { "A-1", "B-2" }, new[] { "A-2" }, "^(?:A-1|B-2)$" },
        { new[] { "2024-01-02", "2024-03-04" }, Array.Empty<string>(), @"^2024-\d{2}-\d{2}$" },
        { new[] { "hello" }, Array.Empty<string>(), "^hello$" },
        { new[] { string.Empty }, Array.Empty<string>(), "^$" },
        { new[] { "-^", "]\\" }, Array.Empty<string>(), @"^[\-\]][\\\^]$" }
    };

    [Fact]
    public void Learn_InvoiceExamples_ReturnsDigitPattern()
    {
        Pattern pattern = Pattern.Learn(match: Invoices, reject: InvoiceRejects);

        pattern.ToString().ShouldBe(@"^INV-\d{4}-\d{4}$");
    }

    [Fact]
    public void Learn_InvoiceExamples_ProducesReadableFluentCode()
    {
        Pattern pattern = Pattern.Learn(match: Invoices, reject: InvoiceRejects);

        string code = pattern.ToFluentCode();

        code.ShouldContain(".StartOfLine");
        code.ShouldContain(".Literal(\"INV-\")");
        code.ShouldContain(".Digit.Repeat.Times(4)");
        code.ShouldContain(".EndOfLine");
    }

    [Fact]
    public void Learn_InvoiceExamples_MatchesSimilarInvoicesOnly()
    {
        Pattern pattern = Pattern.Learn(match: Invoices, reject: InvoiceRejects);

        pattern.IsMatch("INV-2030-9999").ShouldBeTrue();
        pattern.IsMatch("INV-2030-99999").ShouldBeFalse();
        pattern.IsMatch("XINV-2030-9999").ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(LearnedPatterns))]
    public void Learn_Examples_ProducesExpectedPattern(string[] match, string[] reject, string expected)
    {
        Pattern pattern = Pattern.Learn(match, reject);

        pattern.ToString().ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(LearnedPatterns))]
    public void Learn_Examples_MatchesEveryMatchAndRejectsEveryReject(string[] match, string[] reject, string expected)
    {
        Pattern pattern = Pattern.Learn(match, reject);

        match.ShouldAllBe(example => pattern.IsMatch(example), expected);
        reject.ShouldAllBe(example => !pattern.IsMatch(example), expected);
    }

    [Fact]
    public void Learn_ParamsOverload_LearnsFromMatchExamples()
    {
        Pattern pattern = Pattern.Learn("a1", "b2");

        pattern.ToString().ShouldBe(@"^[a-z]\d$");
    }

    [Fact]
    public void Learn_RejectEndingWithNewline_AnchorsAtEndOfInput()
    {
        Pattern pattern = Pattern.Learn(new[] { "abc" }, new[] { "abc\n" });

        pattern.ToString().ShouldBe(@"^abc\z");
        pattern.IsMatch("abc\n").ShouldBeFalse();
    }

    [Fact]
    public void Learn_DifferentExampleOrder_ProducesSamePattern()
    {
        Pattern first = Pattern.Learn("x-1", "abc", "y-22");

        Pattern second = Pattern.Learn("y-22", "x-1", "abc");

        second.ToString().ShouldBe(first.ToString());
    }

    [Fact]
    public void Learn_DuplicateExamples_AreIgnored()
    {
        Pattern pattern = Pattern.Learn("cat", "cat", "dog");

        pattern.ToString().ShouldBe("^[a-z]{3}$");
    }

    [Fact]
    public void Learn_NonAsciiLetters_AreKeptLiterally()
    {
        Pattern pattern = Pattern.Learn("José", "Jesús");

        pattern.ToString().ShouldBe("^(?:Jesús|José)$");
    }

    [Fact]
    public void Learn_RejectAlsoInMatch_ThrowsArgumentExceptionNamingTheExample()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => Pattern.Learn(new[] { "abc", "def" }, new[] { "def" }));

        exception.Message.ShouldContain("\"def\"");
        exception.ParamName.ShouldBe("reject");
    }

    [Fact]
    public void Learn_EmptyMatch_ThrowsArgumentException()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => Pattern.Learn(Array.Empty<string>()));

        exception.ParamName.ShouldBe("match");
    }

    [Fact]
    public void Learn_NullMatchSequence_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Pattern.Learn((IEnumerable<string>)null!, null));
    }

    [Fact]
    public void Learn_NullMatchArray_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Pattern.Learn((string[])null!));
    }

    [Fact]
    public void Learn_NullMatchExample_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(() => Pattern.Learn(new[] { "a", null! }));

        exception.ParamName.ShouldBe("match");
    }

    [Fact]
    public void Learn_NullRejectExample_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(() => Pattern.Learn(new[] { "a" }, new[] { (string)null! }));

        exception.ParamName.ShouldBe("reject");
    }

    [Fact]
    public void Learn_NullReject_IsTreatedAsNoRejects()
    {
        Pattern pattern = Pattern.Learn(new[] { "12", "345" }, null);

        pattern.ToString().ShouldBe(@"^\d{2,3}$");
    }

    [Fact]
    public void Learn_RejectNeedingSeveralSteps_RelaxesPositionsThatDidNotNeedSpecializing()
    {
        // No single step rejects "ab14", so the letters are specialized first only to make progress;
        // once the digits are enumerated exactly, the letters relax back to the general class.
        Pattern pattern = Pattern.Learn(new[] { "ab12", "cd34" }, new[] { "ab14" });

        pattern.ToString().ShouldBe("^[a-z]{2}(?:12|34)$");
    }

    [Fact]
    public void Learn_RejectThatSeveralPositionsCouldExclude_SpecializesTheLeftmostPosition()
    {
        // Narrowing either the letter to [ab] or the digit to [12] rejects "c3"; ties go to the leftmost position.
        Pattern pattern = Pattern.Learn(new[] { "a1", "b2" }, new[] { "c3" });

        pattern.ToString().ShouldBe(@"^[ab]\d$");
    }

    [Fact]
    public void Learn_SurrogatePairs_AlignsUtf16CodeUnits()
    {
        // Kept out of theory data: serializing the expected text would mangle its lone surrogates.
        Pattern pattern = Pattern.Learn("😀", "😁");

        pattern.ToString().ShouldBe("^\uD83D[\uDE00\uDE01]$");
        pattern.IsMatch("😁").ShouldBeTrue();
        pattern.IsMatch("😂").ShouldBeFalse();
    }

    [Fact]
    public void Learn_EmptyRejectExample_KeepsEndOfLineAnchor()
    {
        Pattern pattern = Pattern.Learn(new[] { "a" }, new[] { string.Empty });

        pattern.ToString().ShouldBe("^a$");
        pattern.IsMatch(string.Empty).ShouldBeFalse();
        pattern.IsMatch("a\n").ShouldBeTrue();
    }

    [Theory]
    [InlineData("12", "Digit")]
    [InlineData("AB", "Upper")]
    [InlineData("ab", "Lower")]
    [InlineData("aB", "Letter")]
    [InlineData("a1_", "Word")]
    [InlineData(" \t\n", "Space")]
    public void LearnedClasses_TryCover_CoveredCharacters_ReturnsNarrowestClass(string text, string expectedClass)
    {
        bool covered = LearnedClasses.TryCover(new[] { text }, out LearnedClass covering);

        covered.ShouldBeTrue();
        covering.ShouldBe(Enum.Parse<LearnedClass>(expectedClass));
    }

    [Theory]
    [InlineData("a-")]
    [InlineData("a b")]
    [InlineData("é")]
    public void LearnedClasses_TryCover_CharactersNoClassCovers_ReturnsFalse(string text)
    {
        bool covered = LearnedClasses.TryCover(new[] { text }, out _);

        covered.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Digit", '7', true)]
    [InlineData("Digit", 'a', false)]
    [InlineData("Upper", 'Q', true)]
    [InlineData("Upper", 'q', false)]
    [InlineData("Lower", 'q', true)]
    [InlineData("Lower", 'Q', false)]
    [InlineData("Letter", 'Z', true)]
    [InlineData("Letter", '1', false)]
    [InlineData("Word", '_', true)]
    [InlineData("Word", '-', false)]
    [InlineData("Space", '\t', true)]
    [InlineData("Space", 'a', false)]
    public void LearnedClasses_Contains_ReportsMembership(string learnedClass, char character, bool expected)
    {
        bool contains = LearnedClasses.Contains(Enum.Parse<LearnedClass>(learnedClass), character);

        contains.ShouldBe(expected);
    }
}
