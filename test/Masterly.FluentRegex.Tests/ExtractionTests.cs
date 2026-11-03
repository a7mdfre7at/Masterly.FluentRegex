using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex.Tests;

public enum Severity { Debug, Info, Warn, Error }

public record LogEntry(DateTime Timestamp, Severity Level, string Message);

public record Dimensions(int Width, int Height, string? Unit = "px");

public record Renamed([RegexGroup("w")] int Width, [RegexGroup("h")] int Height);

public class MutableProduct
{
    public string Sku { get; set; } = "";
    public decimal Price { get; set; }
    public int? Stock { get; set; }

    [RegexGroup("cat")]
    public string Category { get; set; } = "";
}

public record Money(decimal Amount, string Currency);

public record WithMoney(Money Total);

public class ExtractionTests
{
    private static readonly Pattern Log = Pattern.With
        .StartOfLine
        .NamedGroup("timestamp", Pattern.With.NonWhitespace.Repeat.OneOrMore)
        .Whitespace
        .NamedGroup("level", Pattern.With.Word.Repeat.OneOrMore)
        .Whitespace
        .NamedGroup("message", Pattern.With.Anything.Repeat.OneOrMore)
        .EndOfLine;

    private static readonly Pattern Size = Pattern.With
        .NamedGroup("width", Pattern.With.Digit.Repeat.OneOrMore)
        .Literal("x")
        .NamedGroup("height", Pattern.With.Digit.Repeat.OneOrMore)
        .NamedGroup("unit", Pattern.With.Letter.Repeat.OneOrMore).Repeat.Optional;

    [Fact]
    public void Extract_BindsRecordConstructor_WithTypeConversion()
    {
        LogEntry entry = Log.Extract<LogEntry>("2024-05-17T10:30:00 ERROR Disk is full");

        entry.Timestamp.ShouldBe(new DateTime(2024, 5, 17, 10, 30, 0));
        entry.Level.ShouldBe(Severity.Error);
        entry.Message.ShouldBe("Disk is full");
    }

    [Fact]
    public void Extract_UsesDefaultValue_WhenGroupDidNotParticipate()
    {
        Size.Extract<Dimensions>("640x480").ShouldBe(new Dimensions(640, 480, "px"));
        Size.Extract<Dimensions>("10x20cm").ShouldBe(new Dimensions(10, 20, "cm"));
    }

    [Fact]
    public void Extract_HonorsRegexGroupAttribute()
    {
        Pattern pattern = Pattern.With.NamedGroup("w", Pattern.With.Digit.Repeat.OneOrMore).Literal("x").NamedGroup("h", Pattern.With.Digit.Repeat.OneOrMore);
        pattern.Extract<Renamed>("3x4").ShouldBe(new Renamed(3, 4));
    }

    [Fact]
    public void Extract_BindsSettableProperties()
    {
        Pattern pattern = Pattern.With
            .NamedGroup("sku", Pattern.With.Word.Repeat.OneOrMore).Literal(";")
            .NamedGroup("price", Pattern.With.Digit.Repeat.OneOrMore.Literal(".").Digit.Repeat.Times(2)).Literal(";")
            .NamedGroup("stock", Pattern.With.Digit.Repeat.ZeroOrMore).Literal(";")
            .NamedGroup("cat", Pattern.With.Word.Repeat.OneOrMore);

        MutableProduct product = pattern.Extract<MutableProduct>("AB12;19.99;;tools");

        product.Sku.ShouldBe("AB12");
        product.Price.ShouldBe(19.99m);
        product.Stock.ShouldBeNull(); // empty capture binds to null for nullable types
        product.Category.ShouldBe("tools");
    }

    [Fact]
    public void Extract_ThrowsFormatException_OnNoMatch()
    {
        var ex = Should.Throw<FormatException>(() => Size.Extract<Dimensions>("nope"));
        ex.Message.ShouldContain("Trace(input)");
    }

    [Fact]
    public void Extract_ThrowsFormatException_OnBadValue()
    {
        Pattern pattern = Pattern.With.NamedGroup("width", Pattern.With.Word.Repeat.OneOrMore).Literal("x").NamedGroup("height", Pattern.With.Digit.Repeat.OneOrMore);
        var ex = Should.Throw<FormatException>(() => pattern.Extract<Dimensions>("abcx5"));
        ex.Message.ShouldContain("Group 'width'");
        ex.Message.ShouldContain("Int32");
    }

    [Fact]
    public void Extract_ExplainsMissingGroups()
    {
        Pattern pattern = Pattern.With.NamedGroup("width", Pattern.With.Digit.Repeat.OneOrMore);
        var ex = Should.Throw<InvalidOperationException>(() => pattern.Extract<Dimensions>("5"));
        ex.Message.ShouldContain("Height (group 'Height')");
        ex.Message.ShouldContain("Available groups: 0, width");
    }

    [Fact]
    public void TryExtract_ReturnsFalse_InsteadOfThrowing()
    {
        Size.TryExtract("nope", out Dimensions? _).ShouldBeFalse();
        Size.TryExtract("5x6", out Dimensions? result).ShouldBeTrue();
        result!.Width.ShouldBe(5);
    }

    [Fact]
    public void ExtractAll_IsLazyAndReturnsEveryMatch()
    {
        var all = Size.ExtractAll<Dimensions>("icons: 16x16, 32x32px and 64x64em").ToList();

        all.Select(d => d.Width).ShouldBe(new[] { 16, 32, 64 });
        all[1].Unit.ShouldBe("px");
    }

    [Fact]
    public void Extract_UsesProvider_ForCultureSensitiveValues()
    {
        Pattern pattern = Pattern.With.NamedGroup("amount", Pattern.With.Digit.Repeat.OneOrMore.Literal(",").Digit.Repeat.Times(2))
            .Whitespace.NamedGroup("currency", Pattern.With.UppercaseLetter.Repeat.Times(3));

        Money money = pattern.Extract<Money>("12,50 EUR", new CultureInfo("de-DE"));
        money.Amount.ShouldBe(12.50m);
    }

    [Fact]
    public void CustomConverters_CanBeRegistered()
    {
        ExtractionConverters.Register<Money>(text => new Money(decimal.Parse(text.TrimStart('$'), CultureInfo.InvariantCulture), "USD"));
        try
        {
            Pattern pattern = Pattern.With.Literal("total=").NamedGroup("total", Pattern.With.Literal("$").Digit.Repeat.OneOrMore);
            pattern.Extract<WithMoney>("total=$42").Total.ShouldBe(new Money(42m, "USD"));
        }
        finally
        {
            ExtractionConverters.Unregister<Money>();
        }
    }

    [Fact]
    public void Extract_SupportsCommonTypes()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<Id>\S+) (?<Flag>\S+) (?<Duration>\S+) (?<Ratio>\S+) (?<Ver>\S+) (?<Day>\S+)");
        AllTypes values = pattern.Extract<AllTypes>("d3b07384-d113-4ec4-a6a1-1f7d7f3b4c5a yes 01:30:00 0.25 1.2.3 2024-02-29");

        values.Id.ShouldBe(Guid.Parse("d3b07384-d113-4ec4-a6a1-1f7d7f3b4c5a"));
        values.Flag.ShouldBeTrue();
        values.Duration.ShouldBe(TimeSpan.FromMinutes(90));
        values.Ratio.ShouldBe(0.25);
        values.Ver.ShouldBe(new Version(1, 2, 3));
        values.Day.ShouldBe(new DateOnly(2024, 2, 29));
    }

    public record AllTypes(Guid Id, bool Flag, TimeSpan Duration, double Ratio, Version Ver, DateOnly Day);
}

public class TypedExtractionTests
{
    public record Box<TValue>(TValue Value);

    public record IntegerKinds(short S, byte B, sbyte Sb, uint Ui, ulong Ul, ushort Us);

    public record OptionalPair(int First, int Second);

    public record Annotated(int Id, string? Note, int? Count);

    public readonly record struct Celsius(decimal Degrees);

    public sealed record Fragile(string Text);

    public sealed record Unregistered(string Text);

    public sealed class NoPublicConstructor
    {
        private NoPublicConstructor()
        {
        }
    }

    public readonly struct GridCell
    {
        public GridCell(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public int Row { get; }

        public int Column { get; }
    }

    public readonly struct Opaque
    {
        public int Value => 0;
    }

    public sealed class NumberRange
    {
        public NumberRange(int low, int high)
        {
            Low = low;
            High = high;
        }

        public NumberRange(int low)
            : this(low, low)
        {
        }

        public int Low { get; }

        public int High { get; }
    }

    public struct MeasuredRow
    {
        public MeasuredRow(int row)
        {
            Row = row;
            Unit = string.Empty;
        }

        public int Row { get; }

        public string Unit { get; set; }
    }

    public struct PlainPoint
    {
        public int X { get; set; }

        public int Y { get; set; }
    }

    public sealed class Coordinates
    {
        private readonly Dictionary<int, string> _notes = new Dictionary<int, string>();

        public Coordinates(int latitude)
        {
            Latitude = latitude;
        }

        [RegexGroup("lat")]
        public int Latitude { get; }

        public string Label { get; private set; } = "unset";

        public string Computed => "fixed";

        public string? Extra { get; set; }

        public string this[int index]
        {
            get => _notes[index];
            set => _notes[index] = value;
        }
    }

    private static readonly Pattern AnyValue = Pattern.FromRegex(@"^(?<Value>.*)$");

    private static TValue ExtractValue<TValue>(string text) => AnyValue.Extract<Box<TValue>>(text).Value;

    [Fact]
    public void Extract_SmallAndUnsignedIntegers_ConvertToTheirTypes()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<S>\S+) (?<B>\S+) (?<Sb>\S+) (?<Ui>\S+) (?<Ul>\S+) (?<Us>\S+)");

        IntegerKinds values = pattern.Extract<IntegerKinds>("-12 ٢٥٥ -128 4000000000 18446744073709551615 65535");

        values.ShouldBe(new IntegerKinds(-12, 255, -128, 4000000000u, 18446744073709551615ul, 65535));
    }

    [Fact]
    public void Extract_ValueOutOfRange_ThrowsFormatExceptionWrappingTheOverflow()
    {
        FormatException exception = Should.Throw<FormatException>(() => ExtractValue<byte>("300"));

        exception.Message.ShouldStartWith("Group 'Value' value \"300\" cannot be converted to Byte: ");
        exception.InnerException.ShouldBeOfType<OverflowException>();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData(" Yes ", true)]
    [InlineData("y", true)]
    [InlineData("ON", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    [InlineData("N", false)]
    [InlineData("off", false)]
    [InlineData("0", false)]
    public void Extract_BooleanWords_ConvertToTheValue(string text, bool expected)
    {
        bool value = ExtractValue<bool>(text);

        value.ShouldBe(expected);
    }

    [Fact]
    public void Extract_UnknownBooleanWord_ThrowsFormatException()
    {
        FormatException exception = Should.Throw<FormatException>(() => ExtractValue<bool>("maybe"));

        exception.Message.ShouldBe("Group 'Value' value \"maybe\" cannot be converted to Boolean: 'maybe' is not a boolean.");
    }

    [Fact]
    public void Extract_SingleCharacter_ConvertsToChar()
    {
        char value = ExtractValue<char>("x");

        value.ShouldBe('x');
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("")]
    public void Extract_CharFromTextThatIsNotOneCharacter_ThrowsFormatException(string text)
    {
        FormatException exception = Should.Throw<FormatException>(() => ExtractValue<char>(text));

        exception.Message.ShouldEndWith("cannot be converted to Char: Expected exactly one character.");
    }

    [Fact]
    public void Extract_DateTimeOffset_KeepsTheOffset()
    {
        DateTimeOffset value = ExtractValue<DateTimeOffset>("2024-05-17T10:30:00+02:00");

        value.ShouldBe(new DateTimeOffset(2024, 5, 17, 10, 30, 0, TimeSpan.FromHours(2)));
        value.Offset.ShouldBe(TimeSpan.FromHours(2));
    }

    [Fact]
    public void Extract_TimeOnly_ConvertsToTheTime()
    {
        TimeOnly value = ExtractValue<TimeOnly>("10:30");

        value.ShouldBe(new TimeOnly(10, 30));
    }

    [Fact]
    public void Extract_RelativeUri_ConvertsToARelativeUri()
    {
        Uri value = ExtractValue<Uri>("docs/readme.md");

        value.IsAbsoluteUri.ShouldBeFalse();
        value.OriginalString.ShouldBe("docs/readme.md");
    }

    [Fact]
    public void Extract_TypeWithoutConverter_ThrowsFormatExceptionNamingTheType()
    {
        FormatException exception = Should.Throw<FormatException>(() => ExtractValue<StringBuilder>("x"));

        exception.InnerException.ShouldBeOfType<ArgumentException>()
            .Message.ShouldBe("No converter for type 'System.Text.StringBuilder'. Register one with ExtractionConverters.Register.");
    }

    [Fact]
    public void Extract_UnboundValueTypeParameterWithoutDefault_UsesTheTypeDefault()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<First>\d+)(?:-(?<Second>\d+))?");

        OptionalPair pair = pattern.Extract<OptionalPair>("5");

        pair.ShouldBe(new OptionalPair(5, 0));
    }

    [Fact]
    public void Extract_NullableParametersWithoutGroups_BindToNull()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<Id>\d+)");

        Annotated annotated = pattern.Extract<Annotated>("7");

        annotated.ShouldBe(new Annotated(7, null, null));
    }

    [Fact]
    public void Extract_TypeWithoutPublicConstructor_ThrowsInvalidOperationException()
    {
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => AnyValue.Extract<NoPublicConstructor>("x"));

        exception.Message.ShouldBe($"Type '{typeof(NoPublicConstructor).FullName}' has no public constructor.");
    }

    [Fact]
    public void Extract_ValueTypeWithoutConstructorOrSettableProperties_ThrowsInvalidOperationException()
    {
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => AnyValue.Extract<Opaque>("x"));

        exception.Message.ShouldBe($"Value type '{typeof(Opaque).FullName}' needs a public constructor or settable properties.");
    }

    [Theory]
    [InlineData("1-5", 1, 5)]
    [InlineData("7", 7, 0)]
    public void Extract_SeveralConstructors_BindsTheOneWithTheMostParameters(string input, int expectedLow, int expectedHigh)
    {
        Pattern pattern = Pattern.FromRegex(@"(?<low>\d+)(?:-(?<high>\d+))?");

        NumberRange range = pattern.Extract<NumberRange>(input);

        range.Low.ShouldBe(expectedLow);
        range.High.ShouldBe(expectedHigh);
    }

    [Fact]
    public void Extract_StructWithConstructor_BindsTheConstructorParameters()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<row>\d+):(?<column>\d+)");

        GridCell cell = pattern.Extract<GridCell>("3:4");

        cell.Row.ShouldBe(3);
        cell.Column.ShouldBe(4);
    }

    [Fact]
    public void Extract_StructWithOnlySettableProperties_SetsTheProperties()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<x>\d+),(?<y>\d+)");

        PlainPoint point = pattern.Extract<PlainPoint>("3,4");

        point.X.ShouldBe(3);
        point.Y.ShouldBe(4);
    }

    [Fact]
    public void Extract_StructWithConstructorAndSettableProperty_SetsBoth()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<row>\d+)(?<unit>[a-z]+)");

        MeasuredRow row = pattern.Extract<MeasuredRow>("12cm");

        row.Row.ShouldBe(12);
        row.Unit.ShouldBe("cm");
    }

    [Fact]
    public void ExtractAll_StructWithOnlySettableProperties_SetsThePropertiesOfEveryMatch()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<x>\d+),(?<y>\d+)");

        List<PlainPoint> points = pattern.ExtractAll<PlainPoint>("1,2 3,4").ToList();

        points.Select(point => (point.X, point.Y)).ShouldBe(new[] { (1, 2), (3, 4) });
    }

    [Fact]
    public void Extract_PropertyAttributeAndUnsettableProperties_BindOnlyWhatCanBeSet()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<lat>\d+);(?<Label>\w+);(?<Computed>\w+);(?<Item>\w+)");

        Coordinates coordinates = pattern.Extract<Coordinates>("12;north;x;y");

        coordinates.Latitude.ShouldBe(12);
        coordinates.Label.ShouldBe("unset");
        coordinates.Computed.ShouldBe("fixed");
        coordinates.Extra.ShouldBeNull();
    }

    [Fact]
    public void Extract_NullableOfCustomType_UsesTheConverterOfTheUnderlyingType()
    {
        ExtractionConverters.Register<Celsius>((text, provider) => new Celsius(decimal.Parse(text, NumberStyles.Number, provider)));
        try
        {
            Celsius? value = AnyValue.Extract<Box<Celsius?>>("12,5", new CultureInfo("de-DE")).Value;

            value.ShouldBe(new Celsius(12.5m));
        }
        finally
        {
            ExtractionConverters.Unregister<Celsius>();
        }
    }

    [Fact]
    public void Extract_ConverterThrowsUnexpectedException_PropagatesItUnwrapped()
    {
        ExtractionConverters.Register<Fragile>(_ => throw new InvalidOperationException("converter failed"));
        try
        {
            InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => ExtractValue<Fragile>("x"));

            exception.Message.ShouldBe("converter failed");
        }
        finally
        {
            ExtractionConverters.Unregister<Fragile>();
        }
    }

    [Fact]
    public void Register_NullConverter_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => ExtractionConverters.Register((Func<string, IFormatProvider, Celsius>)null!))
            .ParamName.ShouldBe("converter");
        Should.Throw<ArgumentNullException>(() => ExtractionConverters.Register((Func<string, Celsius>)null!))
            .ParamName.ShouldBe("converter");
    }

    [Fact]
    public void Unregister_TypeWithoutConverter_ReturnsFalse()
    {
        bool removed = ExtractionConverters.Unregister<Unregistered>();

        removed.ShouldBeFalse();
    }

    [Fact]
    public void RegexGroupAttribute_NullName_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new RegexGroupAttribute(null!)).ParamName.ShouldBe("name");
    }

    [Fact]
    public void Extract_NullInput_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => AnyValue.Extract<Box<int>>(null!)).ParamName.ShouldBe("input");
    }

    [Fact]
    public void ExtractAll_NullInput_ThrowsBeforeEnumeration()
    {
        Should.Throw<ArgumentNullException>(() => AnyValue.ExtractAll<Box<int>>(null!)).ParamName.ShouldBe("input");
    }

    [Fact]
    public void TryExtract_NullInput_ReturnsFalseAndDefault()
    {
        bool extracted = AnyValue.TryExtract(null!, out Box<int>? result);

        extracted.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void TryExtract_ValueThatCannotBeConverted_ReturnsFalseAndDefault()
    {
        bool extracted = AnyValue.TryExtract("not a number", out Box<int>? result);

        extracted.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void GetMembers_RecordParameters_ReportGroupNamesAndRequiredness()
    {
        IReadOnlyList<BindableMember> members = TypeBinder<Annotated>.GetMembers();

        members.Select(member => member.GroupName).ShouldBe(new[] { "Id", "Note", "Count" });
        members.Select(member => member.IsRequired).ShouldBe(new[] { true, false, false });
        members.Select(member => member.Type).ShouldBe(new[] { typeof(int), typeof(string), typeof(int?) });
    }

    [Fact]
    public void GetMembers_PropertyAttributeAndUnsettableProperties_ListsOnlyBindableMembers()
    {
        IReadOnlyList<BindableMember> members = TypeBinder<Coordinates>.GetMembers();

        members.Select(member => member.GroupName).ShouldBe(new[] { "lat", "Extra" });
        members.Select(member => member.Property?.Name).ShouldBe(new[] { "Latitude", "Extra" });
        members.Select(member => member.IsRequired).ShouldBe(new[] { true, false });
    }

    [Fact]
    public void GetMembers_ParameterWithDefaultValue_IsNotRequired()
    {
        IReadOnlyList<BindableMember> members = TypeBinder<Dimensions>.GetMembers();

        members.Select(member => member.GroupName).ShouldBe(new[] { "Width", "Height", "Unit" });
        members.Select(member => member.IsRequired).ShouldBe(new[] { true, true, false });
    }

    [Fact]
    public void GetMembers_SeveralConstructors_DescribesTheOneWithTheMostParameters()
    {
        IReadOnlyList<BindableMember> members = TypeBinder<NumberRange>.GetMembers();

        members.Select(member => member.GroupName).ShouldBe(new[] { "low", "high" });
    }

    [Fact]
    public void GetMembers_ValueTypeWithoutConstructor_ListsSettableProperties()
    {
        IReadOnlyList<BindableMember> members = TypeBinder<PlainPoint>.GetMembers();

        members.Select(member => member.GroupName).ShouldBe(new[] { "X", "Y" });
        members.Select(member => member.IsRequired).ShouldBe(new[] { false, false });
    }
}

public class ReplacementTests
{
    private static readonly Pattern Date = Pattern.With
        .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4)).Literal("-")
        .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2)).Literal("-")
        .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2));

    [Fact]
    public void Replace_WithFluentBuilder()
    {
        Date.Replace("Due 2024-05-17.", r => r.Group("day").Literal("/").Group("month").Literal("/").Group("year"))
            .ShouldBe("Due 17/05/2024.");
    }

    [Fact]
    public void Replace_EscapesDollarSignsInLiterals()
    {
        Date.Replace("2024-05-17", r => r.Literal("$").Group("year").Literal("$1"))
            .ShouldBe("$2024$1");
    }

    [Fact]
    public void Replace_WithTransform()
    {
        string[] months = CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames;
        Date.Replace("2024-05-17", r => r.Group("month", m => months[int.Parse(m) - 1]).Literal(" ").Group("day", d => d.TrimStart('0')).Literal(", ").Group("year"))
            .ShouldBe("May 17, 2024");
    }

    [Fact]
    public void Replace_ValidatesGroupNames_BeforeReplacing()
    {
        var ex = Should.Throw<ArgumentException>(() => Date.Replace("2024-05-17", r => r.Group("yaer")));
        ex.Message.ShouldContain("Group 'yaer' does not exist");
        ex.Message.ShouldContain("year");

        Should.Throw<ArgumentException>(() => Date.Replace("2024-05-17", r => r.Group(7)));
    }

    [Fact]
    public void Replace_SpecialTokens()
    {
        Pattern.With.Literal("b").Replace("abc", r => r.Literal("[").BeforeMatch().Literal("|").WholeMatch(s => s.ToUpperInvariant()).Literal("|").AfterMatch().Literal("]"))
            .ShouldBe("a[a|B|c]c");
    }

    [Fact]
    public void Replace_Computed_And_Count()
    {
        Pattern number = Pattern.With.Digit.Repeat.OneOrMore;
        number.Replace("1 2 3", r => r.Computed(m => (int.Parse(m.Value) * 10).ToString()), count: 2).ShouldBe("10 20 3");
    }

    [Fact]
    public void Replacement_ToString_ShowsDotNetSyntax()
    {
        Replacement.With.Group("year").Literal("-$").Group(2).WholeMatch().ToString().ShouldBe("${year}-$$${2}$&");
    }
}
