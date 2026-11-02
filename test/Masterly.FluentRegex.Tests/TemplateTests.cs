using System.Globalization;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Templates;

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

    public record Day(DateOnly Date);

    public record Clock(TimeOnly Time);

    public record Hexes(long Long, short Short, sbyte SByte, uint UInt, ulong ULong, ushort UShort, byte Byte);

    public record Ratio(double Value);

    public record Labeled(string Left, string Right);

    public record Packaged(Barcode Code);

    /// <summary>A type that is not <see cref="IFormattable"/>, read by a registered extraction converter.</summary>
    public sealed class Barcode
    {
        public Barcode(string digits) => Digits = digits;

        public string Digits { get; }

        public override string ToString() => Digits;
    }

    public class Nothing
    {
    }

    public class Coded
    {
        public string Whole { get; set; } = "";

        public string? Prefix { get; set; }

        public int Number { get; set; }
    }

    public class Banner
    {
        public string Text { get; set; } = "unset";
    }

    /// <summary>The constructor takes a <see cref="Guid"/> but the property exposes it as nullable.</summary>
    public class LooseReference
    {
        public LooseReference(Guid id) => Id = id;

        public Guid? Id { get; set; }
    }

    /// <summary>The constructor takes a <see cref="LogLevel"/> but the property exposes it as nullable.</summary>
    public class LooseLevel
    {
        public LooseLevel(LogLevel level) => Level = level;

        public LogLevel? Level { get; set; }
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
        ArgumentException ex = Should.Throw<ArgumentException>(() => Template.Of<LogLine>(""));

        ex.ParamName.ShouldBe("template");
        ex.Message.ShouldStartWith("The template is empty.");
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
        ArgumentException ex = Should.Throw<ArgumentException>(() => Template.Of<Invoice>("{Number:D4}/{Number:D2}"));

        ex.ParamName.ShouldBe("template");
        ex.Message.ShouldContain("{Number:D2} at position 12 repeats {Number:D4} with a different format");
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

    [Theory]
    [InlineData(true, "enabled=true")]
    [InlineData(false, "enabled=false")]
    public void Format_Bool_RendersLowerCase(bool enabled, string expected)
    {
        Template<Flag> template = Template.Of<Flag>("enabled={Enabled}");

        string text = template.Format(new Flag(enabled));

        text.ShouldBe(expected);
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
        template.Format(new Reading(42, "kg")).ShouldBe("42kg");
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

    // ------------------------------------------------------------------
    // Template-level edge cases
    // ------------------------------------------------------------------

    [Fact]
    public void Of_NullCultureOption_FallsBackToTheInvariantCultureNotTheCurrentOne()
    {
        // Culture is declared non-nullable, but callers without nullable analysis can still pass null.
        TemplateOptions options = new TemplateOptions { Culture = null! };
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureWithThreeCommaDecimals();
        try
        {
            Template<Price> template = Template.Of<Price>("{Amount}", options);

            decimal parsed = template.Parse("2.5").Amount;
            string text = template.Format(new Price(1.5m));

            parsed.ShouldBe(2.5m);
            text.ShouldBe("1.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Parse_NamedGroupInsideRegexFormat_BindsAnotherMemberWithTheDefaultConversion()
    {
        Template<Coded> template = Template.Of<Coded>(@"{Whole:re:(?<Prefix>[A-Z]+)-(?<Number>\d+)}");

        Coded coded = template.Parse("AB-42");

        coded.Whole.ShouldBe("AB-42");
        coded.Prefix.ShouldBe("AB");
        coded.Number.ShouldBe(42);
    }

    [Fact]
    public void ParseAll_NamedGroupInsideRegexFormat_BindsEveryOccurrence()
    {
        Template<Coded> template = Template.Of<Coded>(@"{Whole:re:(?<Prefix>[A-Z]+)-(?<Number>\d+)};");

        List<Coded> codes = template.ParseAll("AB-42; CD-7;").ToList();

        codes.Select(c => c.Prefix).ShouldBe(new[] { "AB", "CD" });
        codes.Select(c => c.Number).ShouldBe(new[] { 42, 7 });
    }

    [Fact]
    public void ParseAll_OccurrenceThatCannotConvert_ThrowsFormatExceptionWhenEnumerated()
    {
        Template<Year> template = Template.Of<Year>("{Value};");
        IEnumerable<Year> years = template.ParseAll("1;99999999999;");

        FormatException ex = Should.Throw<FormatException>(() => years.ToList());

        ex.Message.ShouldContain("Value");
    }

    [Fact]
    public void Parse_LiteralTextMismatch_MessageNamesTheLiteralAndPosition()
    {
        Template<Labeled> template = Template.Of<Labeled>(@"{Left:re:\d+}<->{Right:re:\d+}");

        FormatException ex = Should.Throw<FormatException>(() => template.Parse("12<=>34"));

        ex.Message.ShouldContain("at position 3");
        ex.Message.ShouldContain("while reading the text \"<->\"");
    }

    [Fact]
    public void Parse_CatastrophicRegexFormatWithTimeout_ThrowsRegexMatchTimeoutException()
    {
        TemplateOptions options = new TemplateOptions { Timeout = TimeSpan.FromMilliseconds(50) };
        Template<Note> template = Template.Of<Note>("{Text:re:(a+)+b}", options);
        string input = $"{new string('a', 40)}!";

        Should.Throw<RegexMatchTimeoutException>(() => template.Parse(input));
    }

    [Fact]
    public void ParseFormat_LiteralOnlyTemplate_MatchesTheTextAndKeepsDefaults()
    {
        Template<Banner> template = Template.Of<Banner>("hello");

        Banner banner = template.Parse("hello");

        banner.Text.ShouldBe("unset");
        template.Format(new Banner { Text = "ignored" }).ShouldBe("hello");
        template.TryParse("goodbye", out _).ShouldBeFalse();
    }

    [Fact]
    public void Of_TypeWithoutBindableMembers_ThrowsSayingThereAreNone()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() => Template.Of<Nothing>("{Anything}"));

        ex.ParamName.ShouldBe("template");
        ex.Message.ShouldContain("{Anything} at position 0 does not match any member of Nothing");
        ex.Message.ShouldContain("Available: (none: the type has no bindable constructor parameters or settable properties)");
    }

    // ------------------------------------------------------------------
    // Strings, nullables and registered converters
    // ------------------------------------------------------------------

    [Fact]
    public void Format_NullString_RendersNothing()
    {
        Template<Note> template = Template.Of<Note>("note: {Text}");

        // Deliberately violates the non-nullable annotation to exercise the null fallback.
        string text = template.Format(new Note(null!));

        text.ShouldBe("note: ");
    }

    [Fact]
    public void Format_GuidPropertyHoldingNull_ThrowsArgumentException()
    {
        Template<LooseReference> template = Template.Of<LooseReference>("id={Id}");
        LooseReference reference = new LooseReference(Guid.Empty) { Id = null };

        ArgumentException ex = Should.Throw<ArgumentException>(() => template.Format(reference));

        ex.ParamName.ShouldBe("value");
        ex.Message.ShouldContain("The value \"\" cannot be written as {Id}");
    }

    [Fact]
    public void Format_EnumPropertyHoldingNull_ThrowsArgumentException()
    {
        Template<LooseLevel> template = Template.Of<LooseLevel>("[{Level}]");
        LooseLevel level = new LooseLevel(LogLevel.Info) { Level = null };

        ArgumentException ex = Should.Throw<ArgumentException>(() => template.Format(level));

        ex.ParamName.ShouldBe("value");
        ex.Message.ShouldContain("The LogLevel value '' is not a single named member");
        ex.Message.ShouldContain("Debug, Info, Warn, Error");
    }

    [Fact]
    public void ParseFormat_RegisteredConverterType_ParsesWithTheConverterAndRendersWithToString()
    {
        ExtractionConverters.Register<Barcode>(text => new Barcode(text.ToUpperInvariant()));
        try
        {
            Template<Packaged> template = Template.Of<Packaged>("item {Code}");

            Packaged parsed = template.Parse("item abc12");
            string text = template.Format(new Packaged(new Barcode("XY9")));

            parsed.Code.Digits.ShouldBe("ABC12");
            text.ShouldBe("item XY9");
        }
        finally
        {
            ExtractionConverters.Unregister<Barcode>();
        }
    }

    [Fact]
    public void Format_RegisteredConverterTypeWithNullValue_ThrowsArgumentException()
    {
        ExtractionConverters.Register<Barcode>(text => new Barcode(text));
        try
        {
            Template<Packaged> template = Template.Of<Packaged>("item {Code}");

            ArgumentException ex = Should.Throw<ArgumentException>(() => template.Format(new Packaged(null!)));

            ex.ParamName.ShouldBe("value");
            ex.Message.ShouldContain("The value \"\" cannot be written as {Code}");
        }
        finally
        {
            ExtractionConverters.Unregister<Barcode>();
        }
    }

    [Fact]
    public void Of_RegisteredConverterTypeWithFormat_ThrowsArgumentException()
    {
        ExtractionConverters.Register<Barcode>(text => new Barcode(text));
        try
        {
            ArgumentException ex = Should.Throw<ArgumentException>(() => Template.Of<Packaged>("item {Code:N}"));

            ex.ParamName.ShouldBe("template");
            ex.Message.ShouldContain("Barcode placeholders do not take a format ('N')");
        }
        finally
        {
            ExtractionConverters.Unregister<Barcode>();
        }
    }

    // ------------------------------------------------------------------
    // Integer and floating-point formats
    // ------------------------------------------------------------------

    [Fact]
    public void ParseFormat_HexFormatOnEveryIntegerType_RoundTrips()
    {
        Template<Hexes> template = Template.Of<Hexes>(
            "{Long:X}|{Short:X}|{SByte:X}|{UInt:X}|{ULong:X}|{UShort:X}|{Byte:X2}");
        Hexes original = new Hexes(-1L, short.MaxValue, (sbyte)-2, 0xDEADBEEF, ulong.MaxValue, (ushort)0xBEEF, (byte)0xAB);

        string text = template.Format(original);
        Hexes parsed = template.Parse(text);

        text.ShouldBe("FFFFFFFFFFFFFFFF|7FFF|FE|DEADBEEF|FFFFFFFFFFFFFFFF|BEEF|AB");
        parsed.ShouldBe(original);
    }

    [Theory]
    [InlineData("{Number:D}", 255, "255")]
    [InlineData("{Number:X}", 255, "FF")]
    [InlineData("{Number:x4}", 255, "00ff")]
    [InlineData("{Number:D}", -7, "-7")]
    public void ParseFormat_IntegerFormatWithOrWithoutWidth_RoundTrips(string placeholder, int number, string expected)
    {
        Template<Invoice> template = Template.Of<Invoice>(placeholder);

        string text = template.Format(new Invoice(number));

        text.ShouldBe(expected);
        template.Parse(text).Number.ShouldBe(number);
    }

    [Theory]
    [InlineData("{Number:D100}", "'D100'")]
    [InlineData("{Number:X100}", "'X100'")]
    [InlineData("{Number:D-1}", "'D-1'")]
    [InlineData("{Number:D 1}", "'D 1'")]
    public void Of_IntegerWidthOutOfRangeOrMalformed_ThrowsListingSupportedFormats(string placeholder, string format)
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() => Template.Of<Invoice>(placeholder));

        ex.ParamName.ShouldBe("template");
        ex.Message.ShouldContain($"The format {format} is not supported for Int32 placeholders");
        ex.Message.ShouldContain("Use D, Dn, X or Xn");
    }

    [Theory]
    [InlineData("{Amount:F0}", "42", "42")]
    [InlineData("{Amount:F}", "12.5", "12.50")]
    [InlineData("{Amount:f3}", "12.5", "12.500")]
    [InlineData("{Amount:G}", "12.5", "12.5")]
    [InlineData("{Amount:g}", "-0.25", "-0.25")]
    public void ParseFormat_DecimalFormats_RoundTrip(string placeholder, string amount, string expected)
    {
        Template<Price> template = Template.Of<Price>(placeholder);
        decimal value = decimal.Parse(amount, CultureInfo.InvariantCulture);

        string text = template.Format(new Price(value));

        text.ShouldBe(expected);
        template.Parse(text).Amount.ShouldBe(value);
    }

    [Fact]
    public void ParseFormat_FixedFormatWithoutDigits_UsesTheCultureDecimalDigitsAndSeparator()
    {
        TemplateOptions options = new TemplateOptions { Culture = CultureWithThreeCommaDecimals() };
        Template<Price> template = Template.Of<Price>("{Amount:F}", options);

        string text = template.Format(new Price(12.5m));

        text.ShouldBe("12,500");
        template.Parse("3,750").Amount.ShouldBe(3.75m);
        template.TryParse("3,75", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("{Value:G}", 0.1, "0.1")]
    [InlineData("{Value:g}", 1e20, "1e+20")]
    [InlineData("{Value:R}", -2.5, "-2.5")]
    [InlineData("{Value:r}", double.NaN, "NaN")]
    public void ParseFormat_DoubleGeneralFormats_RoundTrip(string placeholder, double value, string expected)
    {
        Template<Ratio> template = Template.Of<Ratio>(placeholder);

        string text = template.Format(new Ratio(value));

        text.ShouldBe(expected);
        template.Parse(text).Value.ShouldBe(value);
    }

    [Theory]
    [InlineData("{Amount:R}", "'R'")]
    [InlineData("{Amount:r}", "'r'")]
    [InlineData("{Amount:E}", "'E'")]
    [InlineData("{Amount:F100}", "'F100'")]
    [InlineData("{Amount:Fx}", "'Fx'")]
    public void Of_UnsupportedDecimalFormat_ThrowsListingSupportedFormats(string placeholder, string format)
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() => Template.Of<Price>(placeholder));

        ex.ParamName.ShouldBe("template");
        ex.Message.ShouldContain($"The format {format} is not supported for Decimal placeholders");
        ex.Message.ShouldContain("Use G, R, F or Fn");
    }

    // ------------------------------------------------------------------
    // Standard and custom date formats
    // ------------------------------------------------------------------

    public static TheoryData<string, string> InvariantStandardDateFormats => new TheoryData<string, string>
    {
        { "D", "Monday, 02 November 2026" },
        { "f", "Monday, 02 November 2026 21:05" },
        { "F", "Monday, 02 November 2026 21:05:07" },
        { "g", "11/02/2026 21:05" },
        { "G", "11/02/2026 21:05:07" },
        { "m", "November 02" },
        { "M", "November 02" },
        { "t", "21:05" },
        { "T", "21:05:07" },
        { "y", "2026 November" },
        { "Y", "2026 November" },
        { "u", "2026-11-02 21:05:07Z" },
        { "r", "Mon, 02 Nov 2026 21:05:07 GMT" },
        { "R", "Mon, 02 Nov 2026 21:05:07 GMT" }
    };

    [Theory]
    [MemberData(nameof(InvariantStandardDateFormats))]
    public void ParseFormat_StandardDateFormat_RendersTheCulturePatternAndReadsItBack(string format, string expected)
    {
        Template<Moment> template = Template.Of<Moment>($"{{When:{format}}}");

        string text = template.Format(new Moment(new DateTime(2026, 11, 2, 21, 5, 7)));
        string reformatted = template.Format(template.Parse(expected));

        text.ShouldBe(expected);
        reformatted.ShouldBe(expected);
    }

    [Theory]
    [InlineData("D", "lundi 2 novembre 2026")]
    [InlineData("d", "02/11/2026")]
    public void ParseFormat_StandardDateFormatInFrenchCulture_UsesTheFrenchPattern(string format, string expected)
    {
        TemplateOptions french = new TemplateOptions { Culture = new CultureInfo("fr-FR") };
        Template<Moment> template = Template.Of<Moment>($"{{When:{format}}}", french);

        string text = template.Format(new Moment(new DateTime(2026, 11, 2)));

        text.ShouldBe(expected);
        template.Parse(expected).When.ShouldBe(new DateTime(2026, 11, 2));
    }

    public static TheoryData<string, DateTime, string> CustomDateFormats => new TheoryData<string, DateTime, string>
    {
        { "yyyy-MM-%d", new DateTime(2026, 11, 2), "2026-11-2" },
        { "MM-dd-y", new DateTime(2026, 11, 2), "11-02-26" },
        { "MM-dd-yy", new DateTime(2026, 11, 2), "11-02-26" },
        { "yyy-MM-dd", new DateTime(999, 1, 2), "999-01-02" },
        { "yyyyy-MM-dd", new DateTime(2026, 1, 2), "02026-01-02" },
        { "yyyy-MM-dd h:mm t", new DateTime(2026, 11, 2, 21, 5, 0), "2026-11-02 9:05 P" },
        { "yyyy \"y\" MM", new DateTime(2026, 11, 1), "2026 y 11" },
        { @"yyyy-MM-dd'\''x", new DateTime(2026, 11, 2), "2026-11-02'x" },
        { "yyyy/MM/dd", new DateTime(2026, 11, 2), "2026/11/02" }
    };

    [Theory]
    [MemberData(nameof(CustomDateFormats))]
    public void ParseFormat_CustomDateFormat_RoundTrips(string format, DateTime value, string expected)
    {
        Template<Moment> template = Template.Of<Moment>($"{{When:{format}}}");

        string text = template.Format(new Moment(value));

        text.ShouldBe(expected);
        template.Parse(text).When.ShouldBe(value);
    }

    [Fact]
    public void ParseFormat_DateSeparatorInGermanCulture_UsesTheCultureSeparator()
    {
        TemplateOptions german = new TemplateOptions { Culture = new CultureInfo("de-DE") };
        Template<Moment> template = Template.Of<Moment>("{When:dd/MM/yyyy}", german);

        string text = template.Format(new Moment(new DateTime(2026, 11, 2)));

        text.ShouldBe("02.11.2026");
        template.TryParse("02/11/2026", out _).ShouldBeFalse();
    }

    public static TheoryData<string, DateTimeOffset, string, TimeSpan> DateTimeOffsetFormats =>
        new TheoryData<string, DateTimeOffset, string, TimeSpan>
        {
            {
                "o", new DateTimeOffset(2026, 11, 2, 21, 5, 7, 123, TimeSpan.FromHours(3)),
                "2026-11-02T21:05:07.1230000+03:00", TimeSpan.FromHours(3)
            },
            {
                "r", new DateTimeOffset(2026, 11, 2, 21, 5, 7, TimeSpan.FromHours(3)),
                "Mon, 02 Nov 2026 18:05:07 GMT", TimeSpan.Zero
            },
            {
                "u", new DateTimeOffset(2026, 11, 2, 21, 5, 7, TimeSpan.FromHours(3)),
                "2026-11-02 18:05:07Z", TimeSpan.Zero
            },
            {
                "yyyy-MM-dd HH:mm z", new DateTimeOffset(2026, 11, 2, 10, 30, 0, TimeSpan.FromHours(-5)),
                "2026-11-02 10:30 -5", TimeSpan.FromHours(-5)
            },
            {
                "yyyy-MM-dd HH:mm zz", new DateTimeOffset(2026, 11, 2, 10, 30, 0, TimeSpan.FromHours(-5)),
                "2026-11-02 10:30 -05", TimeSpan.FromHours(-5)
            }
        };

    [Theory]
    [MemberData(nameof(DateTimeOffsetFormats))]
    public void ParseFormat_DateTimeOffsetFormat_RoundTripsTheInstant(
        string format,
        DateTimeOffset value,
        string expected,
        TimeSpan expectedOffset)
    {
        Template<Stamp> template = Template.Of<Stamp>($"{{When:{format}}}");

        string text = template.Format(new Stamp(value));
        DateTimeOffset parsed = template.Parse(text).When;

        text.ShouldBe(expected);
        parsed.ShouldBe(value);
        parsed.Offset.ShouldBe(expectedOffset);
    }

    [Theory]
    [InlineData("o", "2026-11-02")]
    [InlineData("O", "2026-11-02")]
    [InlineData("r", "Mon, 02 Nov 2026")]
    public void ParseFormat_DateOnlyStandardFormat_RoundTrips(string format, string expected)
    {
        Template<Day> template = Template.Of<Day>($"{{Date:{format}}}");
        DateOnly value = new DateOnly(2026, 11, 2);

        string text = template.Format(new Day(value));

        text.ShouldBe(expected);
        template.Parse(text).Date.ShouldBe(value);
    }

    [Theory]
    [InlineData("o", 123, "21:05:07.1230000")]
    [InlineData("r", 0, "21:05:07")]
    [InlineData("R", 0, "21:05:07")]
    public void ParseFormat_TimeOnlyStandardFormat_RoundTrips(string format, int milliseconds, string expected)
    {
        Template<Clock> template = Template.Of<Clock>($"{{Time:{format}}}");
        TimeOnly value = new TimeOnly(21, 5, 7, milliseconds);

        string text = template.Format(new Clock(value));

        text.ShouldBe(expected);
        template.Parse(text).Time.ShouldBe(value);
    }

    [Theory]
    [InlineData("yyyy-MM-dd hh:mm tt", 9, "2026-11-02 09:05 ")]
    [InlineData("yyyy-MM-dd hh:mm tt", 21, "2026-11-02 09:05 PM")]
    [InlineData("yyyy-MM-dd hh:mm t", 21, "2026-11-02 09:05 P")]
    public void ParseFormat_CultureWithEmptyAmDesignator_MakesTheDesignatorOptional(
        string format,
        int hour,
        string expected)
    {
        TemplateOptions options = new TemplateOptions { Culture = CultureWithoutAmDesignator() };
        Template<Moment> template = Template.Of<Moment>($"{{When:{format}}}", options);
        DateTime value = new DateTime(2026, 11, 2, hour, 5, 0);

        string text = template.Format(new Moment(value));

        text.ShouldBe(expected);
        template.Parse(text).When.ShouldBe(value);
    }

    [Theory]
    [InlineData("{When:U}", "the standard format 'U'")]
    [InlineData("{When:ffffffff}", "'ffffffff' (at most 7 fraction digits)")]
    [InlineData("{When:yyyy gg}", "the specifier 'gg'")]
    [InlineData("{When:HH:mm:ss.FFF}", "the specifier 'FFF'")]
    [InlineData("{When:'open}", "an unterminated quoted literal in \"'open\"")]
    [InlineData("{When:yyyy \"open}", "an unterminated quoted literal in \"yyyy \"open\"")]
    public void Of_UnsupportedDateFormat_MessageNamesTheProblem(string template, string problem)
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() => Template.Of<Moment>(template));

        ex.ParamName.ShouldBe("template");
        ex.Message.ShouldContain($"The date/time format uses {problem}, which templates do not support.");
    }

    // A template can never pass a format ending in a lone backslash (it would escape the closing brace),
    // so the translator's own guards are exercised directly.
    [Theory]
    [InlineData(@"yyyy\", "a trailing backslash in \"yyyy\\\"")]
    [InlineData(@"'ab\", "an unterminated quoted literal in \"'ab\\\"")]
    public void DateFormatTranslatorToRegex_FormatEndingInBackslash_ThrowsArgumentException(
        string format,
        string problem)
    {
        ArgumentException ex = Should.Throw<ArgumentException>(
            () => DateFormatTranslator.ToRegex(format, TemporalKind.DateTime, CultureInfo.InvariantCulture));

        ex.Message.ShouldContain($"The date/time format uses {problem}");
    }

    // ------------------------------------------------------------------
    // TimeSpan formats
    // ------------------------------------------------------------------

    public static TheoryData<string, TimeSpan, string, string> TimeSpanFormats =>
        new TheoryData<string, TimeSpan, string, string>
        {
            { "g", new TimeSpan(1, 2, 3, 4, 567), "", "1:2:03:04.567" },
            { "g", -new TimeSpan(1, 2, 3, 4, 567), "", "-1:2:03:04.567" },
            { "g", new TimeSpan(0, 2, 3, 4), "", "2:03:04" },
            { "g", new TimeSpan(1, 2, 3, 4, 567), "de-DE", "1:2:03:04,567" },
            { "G", new TimeSpan(1, 2, 3, 4, 567), "", "1:02:03:04.5670000" },
            { "G", -new TimeSpan(1, 2, 3, 4, 567), "de-DE", "-1:02:03:04,5670000" },
            { "t", new TimeSpan(1, 2, 3, 4, 567), "", "1.02:03:04.5670000" },
            { "T", new TimeSpan(1, 2, 3, 4, 567), "", "1.02:03:04.5670000" },
            { @"dd\.hh\:mm\:ss\.fff", new TimeSpan(1, 2, 3, 4, 567), "", "01.02:03:04.567" },
            { @"h\:m\:s", new TimeSpan(0, 2, 3, 4), "", "2:3:4" },
            { "\"at \"hh", new TimeSpan(0, 2, 0, 0), "", "at 02" }
        };

    [Theory]
    [MemberData(nameof(TimeSpanFormats))]
    public void ParseFormat_TimeSpanFormat_RendersAndReadsBack(
        string format,
        TimeSpan value,
        string cultureName,
        string expected)
    {
        TemplateOptions options = new TemplateOptions { Culture = new CultureInfo(cultureName) };
        Template<Duration> template = Template.Of<Duration>($"{{Elapsed:{format}}}", options);

        string text = template.Format(new Duration(value));

        text.ShouldBe(expected);
        template.Parse(text).Elapsed.ShouldBe(value);
    }

    [Fact]
    public void ParseFormat_TimeSpanWithoutFormat_RendersTheConstantFormat()
    {
        Template<Duration> template = Template.Of<Duration>("{Elapsed}");
        TimeSpan value = new TimeSpan(1, 2, 3, 4, 567);

        string text = template.Format(new Duration(value));

        text.ShouldBe("1.02:03:04.5670000");
        template.Parse(text).Elapsed.ShouldBe(value);
    }

    [Theory]
    [InlineData("{Elapsed:x}", "the standard format 'x'")]
    [InlineData("{Elapsed:ddddddddd}", "the specifier 'ddddddddd'")]
    [InlineData("{Elapsed:ffffffff}", "the specifier 'ffffffff'")]
    [InlineData("{Elapsed:hhh}", "the specifier 'hhh'")]
    [InlineData("{Elapsed:mmm}", "the specifier 'mmm'")]
    [InlineData("{Elapsed:sss}", "the specifier 'sss'")]
    [InlineData("{Elapsed:'abc}", "an unterminated quoted literal in \"'abc\"")]
    [InlineData("{Elapsed:hh:mm}", @"the unescaped character ':' (quote it or escape it with \, as in hh\:mm)")]
    public void Of_UnsupportedTimeSpanFormat_MessageNamesTheProblem(string template, string problem)
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() => Template.Of<Duration>(template));

        ex.ParamName.ShouldBe("template");
        ex.Message.ShouldContain($"The TimeSpan format uses {problem}, which templates do not support.");
    }

    [Fact]
    public void TimeSpanFormatTranslatorToRegex_FormatEndingInBackslash_ThrowsArgumentException()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(
            () => TimeSpanFormatTranslator.ToRegex(@"hh\", CultureInfo.InvariantCulture));

        ex.Message.ShouldContain("The TimeSpan format uses a trailing backslash in \"hh\\\"");
    }

    private static CultureInfo CultureWithThreeCommaDecimals()
    {
        CultureInfo culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberDecimalDigits = 3;
        return culture;
    }

    private static CultureInfo CultureWithoutAmDesignator()
    {
        CultureInfo culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.AMDesignator = string.Empty;
        return culture;
    }
}
