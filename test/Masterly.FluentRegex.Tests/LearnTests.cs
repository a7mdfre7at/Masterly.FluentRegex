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
        { new[] { string.Empty }, Array.Empty<string>(), "^$" }
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
}
