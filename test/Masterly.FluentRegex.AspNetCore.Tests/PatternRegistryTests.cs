using Shouldly;
using Xunit;

namespace Masterly.FluentRegex.AspNetCore.Tests;

public sealed class PatternRegistryTests
{
    private static readonly Pattern Digits = Pattern.With.Digit.Repeat.OneOrMore;

    [Fact]
    public void Add_Pattern_IsRetrievableCaseInsensitively()
    {
        PatternRegistry registry = new PatternRegistry().Add("Digits", Digits);

        ConstrainedPattern pattern = registry.Get("DIGITS");

        pattern.Pattern.ShouldBeSameAs(Digits);
        pattern.RuleDescriptions.ShouldBeEmpty();
        registry.Contains("digits").ShouldBeTrue();
    }

    [Fact]
    public void Add_ConstrainedPattern_KeepsItsRules()
    {
        ConstrainedPattern card = CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");

        PatternRegistry registry = new PatternRegistry().Add("card", card);

        registry.Get("card").ShouldBeSameAs(card);
    }

    [Fact]
    public void Names_AreSortedOrdinally()
    {
        PatternRegistry registry = new PatternRegistry().Add("zip", Digits).Add("card", Digits).Add("iban", Digits);

        IReadOnlyList<string> names = registry.Names;

        names.ShouldBe(new[] { "card", "iban", "zip" });
    }

    [Fact]
    public void TryGet_UnknownName_ReturnsFalse()
    {
        PatternRegistry registry = new PatternRegistry().Add("digits", Digits);

        bool found = registry.TryGet("letters", out ConstrainedPattern? pattern);

        found.ShouldBeFalse();
        pattern.ShouldBeNull();
    }

    [Fact]
    public void Get_UnknownName_ThrowsAndListsRegisteredNames()
    {
        PatternRegistry registry = new PatternRegistry().Add("digits", Digits).Add("card", Digits);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => registry.Get("sku"));

        exception.Message.ShouldStartWith("No fluent pattern named 'sku' is registered (registered: card, digits).");
        exception.Message.ShouldContain("registry.Add(\"sku\", pattern)");
    }

    [Fact]
    public void Add_DuplicateName_ThrowsArgumentException()
    {
        PatternRegistry registry = new PatternRegistry().Add("digits", Digits);

        ArgumentException exception = Should.Throw<ArgumentException>(() => registry.Add("DIGITS", Digits));

        exception.ParamName.ShouldBe("name");
        exception.Message.ShouldContain("already registered");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Add_BlankName_ThrowsArgumentException(string name)
    {
        PatternRegistry registry = new PatternRegistry();

        Should.Throw<ArgumentException>(() => registry.Add(name, Digits)).ParamName.ShouldBe("name");
    }

    [Fact]
    public void Members_NullArguments_Throw()
    {
        PatternRegistry registry = new PatternRegistry();

        Should.Throw<ArgumentNullException>(() => registry.Add(null!, Digits)).ParamName.ShouldBe("name");
        Should.Throw<ArgumentNullException>(() => registry.Add("x", (Pattern)null!)).ParamName.ShouldBe("pattern");
        Should.Throw<ArgumentNullException>(() => registry.Add("x", (ConstrainedPattern)null!)).ParamName.ShouldBe("pattern");
        Should.Throw<ArgumentNullException>(() => registry.Contains(null!)).ParamName.ShouldBe("name");
        Should.Throw<ArgumentNullException>(() => registry.TryGet(null!, out _)).ParamName.ShouldBe("name");
    }
}
