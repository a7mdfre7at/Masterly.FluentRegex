using System.Globalization;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class TemplateTests
{
    public enum LogLevel { Debug, Info, Warn, Error }

    public enum Mode { In, Inside, Out }

    public record LogLine(DateTime Date, LogLevel Level, string User, string Message);

    public record Invoice(int Number);

    public record Delta(int Change, uint Count);

    public record Color(int Rgb);

    public record Measure(decimal Price, double Ratio, float Weight);

    public record Price(decimal Amount);

    public record Flag(bool Enabled);

    public record Reference(Guid Id);

    public record ModeLine(Mode Mode, string Rest);

    public record Stamp(DateTimeOffset When);

    public record Moment(DateTime When);

    public record Slot(DateOnly Day, TimeOnly Start);

    public record Duration(TimeSpan Elapsed);

    public record Event(DateTime At, string Name);

    public record Product(string Sku, int Quantity);

    public record Reading(int? Value, string Unit);

    public record Sized([RegexGroup("w")] int Width);

    public record Pair(string Word);

    public record Year(int Value);

    public record Big(long Value);

    public record Item(int Id);

    public record Note(string Text);

    public record Link(Uri Url, Version Release);

    public record Initial(char Letter, string Rest);

    public class Endpoint
    {
        public string Host { get; set; } = "";

        public int Port { get; set; }
    }

    public class Opaque
    {
        public Opaque(int secret)
        {
            _ = secret;
        }
    }

    private const string LogTemplate = "{Date:yyyy-MM-dd} [{Level}] {User}: {Message}";

    private static readonly Template<LogLine> Log = Template.Of<LogLine>(LogTemplate);

    // ------------------------------------------------------------------
    // The headline example
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_LogLineExample_BindsEveryMember()
    {
        LogLine line = Log.Parse("2026-11-02 [WARN] ahmad: disk at 91%");

        line.ShouldBe(new LogLine(new DateTime(2026, 11, 2), LogLevel.Warn, "ahmad", "disk at 91%"));
    }

    [Fact]
    public void Format_LogLine_RendersWithTheSameFormats()
    {
        LogLine line = new LogLine(new DateTime(2026, 11, 2), LogLevel.Error, "sara", "out of memory");

        string text = Log.Format(line);

        text.ShouldBe("2026-11-02 [Error] sara: out of memory");
    }

    [Fact]
    public void ParseFormat_LogLine_RoundTrips()
    {
        LogLine original = new LogLine(new DateTime(2026, 1, 31), LogLevel.Info, "omar", "user: logged in [web]");

        LogLine parsed = Log.Parse(Log.Format(original));

        parsed.ShouldBe(original);
    }

    [Fact]
    public void Parse_MessageContainingTheSeparator_KeepsItInTheLastPlaceholder()
    {
        LogLine line = Log.Parse("2026-11-02 [INFO] ahmad: a: b: c");

        line.User.ShouldBe("ahmad");
        line.Message.ShouldBe("a: b: c");
    }

    // ------------------------------------------------------------------
    // Parse failures
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_WrongDateSeparator_ThrowsFormatExceptionNamingPlaceholderAndPosition()
    {
        FormatException ex = Should.Throw<FormatException>(() => Log.Parse("2026/11/02 [WARN] ahmad: disk at 91%"));

        ex.Message.ShouldContain("{Date:yyyy-MM-dd}");
        ex.Message.ShouldContain("position 4");
        ex.Message.ShouldContain(LogTemplate);
    }

    [Fact]
    public void Parse_UnknownEnumName_ThrowsFormatExceptionNamingLevel()
    {
        FormatException ex = Should.Throw<FormatException>(() => Log.Parse("2026-11-02 [FATAL] ahmad: boom"));

        ex.Message.ShouldContain("{Level}");
    }

    [Fact]
    public void Parse_TrailingTextOnAnchoredTemplate_ThrowsFormatException()
    {
        Template<Invoice> template = Template.Of<Invoice>("INV-{Number:D4}");

        FormatException ex = Should.Throw<FormatException>(() => template.Parse("INV-0042 extra"));

        ex.Message.ShouldContain("extra text");
    }

    [Fact]
    public void Parse_ValueOverflowsMemberType_ThrowsFormatException()
    {
        Template<Year> template = Template.Of<Year>("{Value}");

        FormatException ex = Should.Throw<FormatException>(() => template.Parse("99999999999"));

        ex.Message.ShouldContain("Value");
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrueAndValue()
    {
        bool parsed = Log.TryParse("2026-11-02 [DEBUG] bot: ok", out LogLine? line);

        parsed.ShouldBeTrue();
        line!.Level.ShouldBe(LogLevel.Debug);
    }

    [Theory]
    [InlineData("2026-11-02 [NOPE] bot: ok")]
    [InlineData("not a log line")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_InvalidInput_ReturnsFalseAndDefault(string? input)
    {
        bool parsed = Log.TryParse(input!, out LogLine? line);

        parsed.ShouldBeFalse();
        line.ShouldBeNull();
    }

    [Fact]
    public void TryParse_ValueOverflow_ReturnsFalse()
    {
        Template<Year> template = Template.Of<Year>("{Value}");

        bool parsed = template.TryParse("99999999999", out Year? year);

        parsed.ShouldBeFalse();
        year.ShouldBeNull();
    }

    // ------------------------------------------------------------------
    // ParseAll
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ParseAll_MultiLineLog_ReturnsOneValuePerLine(string newline)
    {
        string text = string.Join(newline, "2026-11-01 [INFO] a: started", "noise", "2026-11-02 [ERROR] b: crashed");

        List<LogLine> lines = Log.ParseAll(text).ToList();

        lines.Select(l => l.Message).ShouldBe(new[] { "started", "crashed" });
        lines.Select(l => l.Level).ShouldBe(new[] { LogLevel.Info, LogLevel.Error });
    }

    [Fact]
    public void ParseAll_NoOccurrences_ReturnsEmpty()
    {
        IEnumerable<LogLine> lines = Log.ParseAll("nothing here");

        lines.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------
    // Argument validation and template syntax
    // ------------------------------------------------------------------

    [Fact]
    public void Of_NullTemplate_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Template.Of<LogLine>(null!));
    }

    [Fact]
    public void Of_EmptyTemplate_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Template.Of<LogLine>(""));
    }

    [Fact]
    public void Parse_NullInput_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Log.Parse(null!));
    }

    [Fact]
    public void ParseAll_NullText_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Log.ParseAll(null!));
    }

    [Fact]
    public void Format_NullValue_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Log.Format(null!));
    }

    [Fact]
    public void Of_UnknownPlaceholder_ThrowsListingAvailableMembers()
    {
        const string template = "{Date:yyyy-MM-dd} {Severity} {User}: {Message}";

        ArgumentException ex = Should.Throw<ArgumentException>(() => Template.Of<LogLine>(template));

        ex.Message.ShouldContain("{Severity}");
        ex.Message.ShouldContain("Available: {Date}, {Level}, {Message}, {User}");
    }

    [Fact]
    public void Of_MissingRequiredConstructorParameter_ThrowsArgumentException()
    {
        const string template = "{Date:yyyy-MM-dd} {User}: {Message}";

        ArgumentException ex = Should.Throw<ArgumentException>(() => Template.Of<LogLine>(template));

        ex.Message.ShouldContain("{Level}");
    }

    [Theory]
    [InlineData("{Text")]
    [InlineData("Text}")]
    [InlineData("{}")]
    [InlineData("{1Text}")]
    [InlineData("{Te xt}")]
    [InlineData("{Text:}")]
    public void Of_MalformedTemplate_ThrowsArgumentException(string template)
    {
        Should.Throw<ArgumentException>(() => Template.Of<Note>(template));
    }

    [Fact]
    public void ParseFormat_EscapedBraces_AreLiteral()
    {
        Template<Item> template = Template.Of<Item>("{{{Id}}}");

        template.Parse("{42}").ShouldBe(new Item(42));
        template.Format(new Item(7)).ShouldBe("{7}");
    }

    [Fact]
    public void Parse_PlaceholderNameCase_IsIgnored()
    {
        Template<Item> template = Template.Of<Item>("#{id}");

        Item item = template.Parse("#5");

        item.Id.ShouldBe(5);
    }

    [Fact]
    public void Of_RegexGroupAttribute_RenamesThePlaceholder()
    {
        Template<Sized> template = Template.Of<Sized>("{w}px");

        template.Parse("640px").Width.ShouldBe(640);
        Should.Throw<ArgumentException>(() => Template.Of<Sized>("{Width}px"));
    }

    [Fact]
    public void ParseFormat_SettableProperties_BindAndRender()
    {
        Template<Endpoint> template = Template.Of<Endpoint>("{Host}:{Port}");

        Endpoint endpoint = template.Parse("example.com:8080");

        endpoint.Host.ShouldBe("example.com");
        endpoint.Port.ShouldBe(8080);
        template.Format(endpoint).ShouldBe("example.com:8080");
    }

    [Fact]
    public void Format_ConstructorParameterWithoutProperty_ThrowsInvalidOperation()
    {
        Template<Opaque> template = Template.Of<Opaque>("{secret}");

        template.Parse("3").ShouldNotBeNull();
        Should.Throw<InvalidOperationException>(() => template.Format(new Opaque(3)));
    }

    [Fact]
    public void ParseFormat_RepeatedPlaceholder_MustReadTheSameText()
    {
        Template<Pair> template = Template.Of<Pair>("{Word}-{Word}");

        template.Parse("ab-ab").Word.ShouldBe("ab");
        Should.Throw<FormatException>(() => template.Parse("ab-cd"));
        template.Format(new Pair("xy")).ShouldBe("xy-xy");
    }

    [Fact]
    public void Of_RepeatedPlaceholderWithDifferentFormat_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Template.Of<Invoice>("{Number:D4}/{Number:D2}"));
    }

    // ------------------------------------------------------------------
    // Options and the Pattern property
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_IgnoreCaseOption_MatchesLiteralTextInAnyCase()
    {
        Template<Item> template = Template.Of<Item>("id={Id}", new TemplateOptions { IgnoreCase = true });

        template.Parse("ID=5").Id.ShouldBe(5);
    }

    [Fact]
    public void Parse_WithoutIgnoreCase_LiteralTextIsCaseSensitive()
    {
        Template<Item> template = Template.Of<Item>("id={Id}");

        template.TryParse("ID=5", out _).ShouldBeFalse();
    }

    [Fact]
    public void Of_TimeoutOption_IsAppliedToThePattern()
    {
        TimeSpan timeout = TimeSpan.FromSeconds(3);

        Template<Item> template = Template.Of<Item>("{Id}", new TemplateOptions { Timeout = timeout });

        template.Pattern.Timeout.ShouldBe(timeout);
    }

    [Fact]
    public void Of_InvalidTimeout_ThrowsArgumentOutOfRange()
    {
        TemplateOptions options = new TemplateOptions { Timeout = TimeSpan.Zero };

        Should.Throw<ArgumentOutOfRangeException>(() => Template.Of<Item>("{Id}", options));
    }

    [Fact]
    public void Pattern_IsUnanchoredAndHasNamedGroups()
    {
        Pattern pattern = Log.Pattern;

        pattern.IsMatch("prefix 2026-11-02 [INFO] a: b").ShouldBeTrue();
        pattern.ToRegex().GetGroupNames().ShouldContain("Date");
        pattern.Extract<LogLine>("2026-11-02 [INFO] a: b").User.ShouldBe("a");
    }

    [Fact]
    public void ToString_ReturnsTheTemplateText()
    {
        string text = Log.ToString();

        text.ShouldBe(LogTemplate);
    }

    // ------------------------------------------------------------------
    // Integers
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("INV-0042", 42)]
    [InlineData("INV-9999", 9999)]
    public void Parse_IntegerWithD4_ReadsExactlyFourDigits(string input, int expected)
    {
        Template<Invoice> template = Template.Of<Invoice>("INV-{Number:D4}");

        Invoice invoice = template.Parse(input);

        invoice.Number.ShouldBe(expected);
    }

    [Theory]
    [InlineData("INV-42")]
    [InlineData("INV-00042")]
    public void Parse_IntegerWithD4_RejectsOtherWidths(string input)
    {
        Template<Invoice> template = Template.Of<Invoice>("INV-{Number:D4}");

        Should.Throw<FormatException>(() => template.Parse(input));
    }

    [Fact]
    public void Format_IntegerWithD4_PadsAndRejectsWiderValues()
    {
        Template<Invoice> template = Template.Of<Invoice>("INV-{Number:D4}");

        template.Format(new Invoice(7)).ShouldBe("INV-0007");
        Should.Throw<ArgumentException>(() => template.Format(new Invoice(12345)));
    }

    [Fact]
    public void Parse_SignedAndUnsigned_OnlySignedAcceptsMinus()
    {
        Template<Delta> template = Template.Of<Delta>("{Change}/{Count}");

        template.Parse("-5/3").ShouldBe(new Delta(-5, 3));
        Should.Throw<FormatException>(() => template.Parse("5/-3"));
    }

    [Fact]
    public void ParseFormat_HexFormat_RoundTrips()
    {
        Template<Color> template = Template.Of<Color>("#{Rgb:X6}");

        template.Parse("#FF8800").Rgb.ShouldBe(0xFF8800);
        template.Format(new Color(0x00AA11)).ShouldBe("#00AA11");
    }

    [Theory]
    [InlineData("{Number:N0}")]
    [InlineData("{Number:Q}")]
    [InlineData("{Number:Dx}")]
    public void Of_UnsupportedIntegerFormat_ThrowsArgumentException(string template)
    {
        Should.Throw<ArgumentException>(() => Template.Of<Invoice>(template));
    }

    // ------------------------------------------------------------------
    // Floating point
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_FloatingPointMembers_ReadSignsFractionsAndExponents()
    {
        Template<Measure> template = Template.Of<Measure>("{Price}|{Ratio}|{Weight}");

        Measure measure = template.Parse("12.50|-0.25|1E+20");

        measure.ShouldBe(new Measure(12.50m, -0.25, 1e20f));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1.0 / 3.0)]
    [InlineData(-1e-7)]
    public void ParseFormat_DoubleSpecialAndRoundTripValues_RoundTrip(double ratio)
    {
        Template<Measure> template = Template.Of<Measure>("{Price}|{Ratio}|{Weight}");
        Measure original = new Measure(1m, ratio, 2.5f);

        Measure parsed = template.Parse(template.Format(original));

        parsed.ShouldBe(original);
    }

    [Fact]
    public void ParseFormat_FixedDecimals_RequireAndRenderExactlyThatMany()
    {
        Template<Price> template = Template.Of<Price>("{Amount:F2} USD");

        template.Parse("9.99 USD").Amount.ShouldBe(9.99m);
        Should.Throw<FormatException>(() => template.Parse("9.9 USD"));
        template.Format(new Price(9.5m)).ShouldBe("9.50 USD");
    }

    [Fact]
    public void ParseFormat_GermanCulture_UsesCommaDecimalSeparator()
    {
        TemplateOptions german = new TemplateOptions { Culture = new CultureInfo("de-DE") };
        Template<Price> template = Template.Of<Price>("{Amount} EUR", german);

        template.Parse("12,50 EUR").Amount.ShouldBe(12.50m);
        template.Format(new Price(3.75m)).ShouldBe("3,75 EUR");
    }

    [Fact]
    public void Of_UnsupportedFloatingFormat_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Template.Of<Price>("{Amount:C}"));
    }

    // ------------------------------------------------------------------
    // Bool, Guid, enums, strings
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("enabled=TRUE", true)]
    [InlineData("enabled=false", false)]
    public void Parse_Bool_IgnoresCase(string input, bool expected)
    {
        Template<Flag> template = Template.Of<Flag>("enabled={Enabled}");

        bool enabled = template.Parse(input).Enabled;

        enabled.ShouldBe(expected);
    }

    [Fact]
    public void Format_Bool_RendersLowerCase()
    {
        Template<Flag> template = Template.Of<Flag>("enabled={Enabled}");

        string text = template.Format(new Flag(true));

        text.ShouldBe("enabled=true");
    }

    [Theory]
    [InlineData("{Id}", "3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [InlineData("{Id:N}", "3f2504e04f8911d39a0c0305e82c3301")]
    [InlineData("{Id:B}", "{3f2504e0-4f89-11d3-9a0c-0305e82c3301}")]
    [InlineData("{Id:P}", "(3f2504e0-4f89-11d3-9a0c-0305e82c3301)")]
    public void ParseFormat_GuidFormats_RoundTrip(string placeholder, string text)
    {
        Template<Reference> template = Template.Of<Reference>(placeholder);

        Reference reference = template.Parse(text);

        reference.Id.ShouldBe(new Guid("3f2504e0-4f89-11d3-9a0c-0305e82c3301"));
        template.Format(reference).ShouldBe(text);
    }

    [Fact]
    public void Of_UnsupportedGuidFormat_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Template.Of<Reference>("{Id:X}"));
    }

    [Fact]
    public void Parse_EnumNamesSharingAPrefix_PrefersTheLongestName()
    {
        Template<ModeLine> template = Template.Of<ModeLine>("{Mode}{Rest}");

        ModeLine line = template.Parse("insidex");

        line.ShouldBe(new ModeLine(Mode.Inside, "x"));
    }

    [Fact]
    public void Format_UndefinedEnumValue_ThrowsArgumentException()
    {
        Template<ModeLine> template = Template.Of<ModeLine>("{Mode}:{Rest}");

        Should.Throw<ArgumentException>(() => template.Format(new ModeLine((Mode)42, "x")));
    }

    [Fact]
    public void Of_EnumWithFormat_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Template.Of<ModeLine>("{Mode:G}{Rest}"));
    }

    [Fact]
    public void Of_StringWithNonRegexFormat_ThrowsArgumentException()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() => Template.Of<Note>("{Text:upper}"));

        ex.Message.ShouldContain("re:");
    }

    [Fact]
    public void ParseFormat_RegexFormat_ConstrainsTheString()
    {
        Template<Product> template = Template.Of<Product>(@"{Sku:re:[A-Z]{3}\d+} x{Quantity}");

        template.Parse("ABC123 x2").ShouldBe(new Product("ABC123", 2));
        Should.Throw<FormatException>(() => template.Parse("ab123 x2"));
        template.Format(new Product("XYZ9", 4)).ShouldBe("XYZ9 x4");
    }

    [Fact]
    public void Format_ValueNotMatchingRegexFormat_ThrowsArgumentException()
    {
        Template<Product> template = Template.Of<Product>(@"{Sku:re:[A-Z]{3}\d+} x{Quantity}");

        ArgumentException ex = Should.Throw<ArgumentException>(() => template.Format(new Product("bad", 1)));

        ex.Message.ShouldContain("{Sku:re:");
    }

    [Theory]
    [InlineData("{Text:re:[A-Z}")]
    [InlineData("{Text:re:}")]
    public void Of_InvalidRegexFormat_ThrowsArgumentException(string template)
    {
        Should.Throw<ArgumentException>(() => Template.Of<Note>(template));
    }

    [Fact]
    public void Format_StringWithLineBreak_ThrowsArgumentException()
    {
        Template<Note> template = Template.Of<Note>("note: {Text}");

        Should.Throw<ArgumentException>(() => template.Format(new Note("two\nlines")));
    }

    [Fact]
    public void ParseFormat_NullableMember_IsOptional()
    {
        Template<Reading> template = Template.Of<Reading>("{Value}{Unit}");

        template.Parse("42kg").ShouldBe(new Reading(42, "kg"));
        template.Parse("kg").ShouldBe(new Reading(null, "kg"));
        template.Format(new Reading(null, "kg")).ShouldBe("kg");
    }

    // ------------------------------------------------------------------
    // Dates and times
    // ------------------------------------------------------------------

    [Fact]
    public void ParseFormat_DateTimeOffsetWithZzz_KeepsTheOffset()
    {
        Template<Stamp> template = Template.Of<Stamp>("at {When:yyyy-MM-ddTHH:mm:sszzz}");

        Stamp stamp = template.Parse("at 2026-11-02T10:30:00+03:00");

        stamp.When.ShouldBe(new DateTimeOffset(2026, 11, 2, 10, 30, 0, TimeSpan.FromHours(3)));
        template.Format(stamp).ShouldBe("at 2026-11-02T10:30:00+03:00");
    }

    [Fact]
    public void Parse_DateTimeOffsetWithoutOffset_IsReadAsUtc()
    {
        Template<Stamp> template = Template.Of<Stamp>("{When:yyyy-MM-dd HH:mm}");

        Stamp stamp = template.Parse("2026-11-02 10:30");

        stamp.When.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Parse_TwelveHourClockWithFractionAndDesignator_ReadsTheTime()
    {
        Template<Moment> template = Template.Of<Moment>("{When:yyyy-MM-dd hh:mm:ss.fff tt}");

        Moment moment = template.Parse("2026-11-02 09:05:07.123 PM");

        moment.When.ShouldBe(new DateTime(2026, 11, 2, 21, 5, 7, 123));
    }

    [Theory]
    [InlineData("{When:dd MMM yyyy}", "02 Nov 2026")]
    [InlineData("{When:dddd, MMMM d, yyyy}", "Monday, November 2, 2026")]
    [InlineData("{When:yyyy-MM-dd'T'HH}", "2026-11-02T00")]
    [InlineData(@"{When:yyyy\.MM\.dd}", "2026.11.02")]
    [InlineData("{When:d}", "11/02/2026")]
    [InlineData("{When:s}", "2026-11-02T00:00:00")]
    public void ParseFormat_DateFormats_RoundTrip(string template, string text)
    {
        Template<Moment> compiled = Template.Of<Moment>(template);

        Moment moment = compiled.Parse(text);

        moment.When.Date.ShouldBe(new DateTime(2026, 11, 2));
        compiled.Format(moment).ShouldBe(text);
    }

    [Fact]
    public void ParseFormat_RoundTripFormatO_PreservesKindAndTicks()
    {
        Template<Moment> template = Template.Of<Moment>("{When:o}");
        Moment original = new Moment(new DateTime(2026, 11, 2, 10, 30, 15, DateTimeKind.Utc).AddTicks(1234567));

        Moment parsed = template.Parse(template.Format(original));

        parsed.When.ShouldBe(original.When);
        parsed.When.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void ParseFormat_FrenchCulture_UsesFrenchMonthNames()
    {
        TemplateOptions french = new TemplateOptions { Culture = new CultureInfo("fr-FR") };
        Template<Moment> template = Template.Of<Moment>("{When:d MMMM yyyy}", french);

        Moment moment = template.Parse("2 novembre 2026");

        moment.When.ShouldBe(new DateTime(2026, 11, 2));
        template.Format(moment).ShouldBe("2 novembre 2026");
    }

    [Fact]
    public void ParseFormat_DateOnlyAndTimeOnly_RoundTrip()
    {
        Template<Slot> template = Template.Of<Slot>("{Day:yyyy-MM-dd} {Start:HH:mm}");

        Slot slot = template.Parse("2026-11-02 14:45");

        slot.ShouldBe(new Slot(new DateOnly(2026, 11, 2), new TimeOnly(14, 45)));
        template.Format(slot).ShouldBe("2026-11-02 14:45");
    }

    [Theory]
    [InlineData(@"took {Elapsed:hh\:mm\:ss}", "took 01:02:03")]
    [InlineData("took {Elapsed:c}", "took 01:02:03")]
    [InlineData("took {Elapsed:%d'd 'hh'h'}", "took 3d 04h")]
    public void ParseFormat_TimeSpanFormats_RoundTrip(string template, string text)
    {
        Template<Duration> compiled = Template.Of<Duration>(template);

        Duration duration = compiled.Parse(text);

        compiled.Format(duration).ShouldBe(text);
    }

    [Fact]
    public void Of_TimeSpanFormatWithUnescapedLiteral_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Template.Of<Duration>("{Elapsed:hh:mm}"));
    }

    [Theory]
    [InlineData("{When:yyyy gg}")]
    [InlineData("{When:HH:mm:ss.FFF}")]
    [InlineData("{When:ffffffff}")]
    [InlineData("{When:U}")]
    [InlineData("{When:'open}")]
    public void Of_UnsupportedDateFormat_ThrowsArgumentException(string template)
    {
        Should.Throw<ArgumentException>(() => Template.Of<Moment>(template));
    }

    [Fact]
    public void ParseFormat_DateTimeWithoutFormat_UsesTheExtractionConverterAndRoundTripFormat()
    {
        Template<Event> template = Template.Of<Event>("{At} {Name}");
        Event original = new Event(new DateTime(2026, 11, 2, 8, 0, 0), "deploy");

        string text = template.Format(original);

        text.ShouldBe("2026-11-02T08:00:00.0000000 deploy");
        template.Parse(text).ShouldBe(original);
        template.Parse("2026-11-02 deploy").At.ShouldBe(new DateTime(2026, 11, 2));
    }

    // ------------------------------------------------------------------
    // Other types
    // ------------------------------------------------------------------

    [Fact]
    public void ParseFormat_UriAndVersion_UseTheExtractionConverters()
    {
        Template<Link> template = Template.Of<Link>("{Url} v{Release}");

        Link link = template.Parse("https://example.com/download v1.2.3");

        link.ShouldBe(new Link(new Uri("https://example.com/download"), new Version(1, 2, 3)));
        template.Format(link).ShouldBe("https://example.com/download v1.2.3");
    }

    [Fact]
    public void ParseFormat_Char_ReadsExactlyOneCharacter()
    {
        Template<Initial> template = Template.Of<Initial>("{Letter}.{Rest}");

        Initial initial = template.Parse("A.lfred");

        initial.ShouldBe(new Initial('A', "lfred"));
        template.Format(initial).ShouldBe("A.lfred");
    }

    // ------------------------------------------------------------------
    // Unicode digits
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("٢٠٢٦", 2026)]
    [InlineData("۲۰۲۶", 2026)]
    [InlineData("२०२६", 2026)]
    public void Parse_IntegerWithUnicodeDigits_ConvertsToTheValue(string input, int expected)
    {
        Template<Year> template = Template.Of<Year>("{Value}");

        int value = template.Parse(input).Value;

        value.ShouldBe(expected);
    }

    [Fact]
    public void Parse_LongWithUnicodeDigits_ConvertsToTheValue()
    {
        Template<Big> template = Template.Of<Big>("{Value}");

        long value = template.Parse("٩٠٠٧١٩٩٢٥٤٧٤٠٩٩٣").Value;

        value.ShouldBe(9007199254740993L);
    }

    [Theory]
    [InlineData("١٢.٥", 12.5)]
    [InlineData("١٢\u066B٥", 12.5)]
    public void Parse_DecimalWithUnicodeDigits_ConvertsToTheValue(string input, double expected)
    {
        Template<Price> template = Template.Of<Price>("{Amount:re:[\\d.\u066B]+}");

        decimal amount = template.Parse(input).Amount;

        amount.ShouldBe((decimal)expected);
    }

    [Fact]
    public void Parse_DateWithArabicIndicDigits_ConvertsToTheDate()
    {
        Template<Moment> template = Template.Of<Moment>("{When:yyyy-MM-dd}");

        DateTime when = template.Parse("٢٠٢٦-١١-٠٢").When;

        when.ShouldBe(new DateTime(2026, 11, 2));
    }
}
