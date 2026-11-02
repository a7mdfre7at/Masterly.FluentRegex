using System.Globalization;

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
