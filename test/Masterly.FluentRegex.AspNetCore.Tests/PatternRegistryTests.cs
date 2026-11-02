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
    public void Add_ReturnsTheSameRegistryForChaining()
    {
        PatternRegistry registry = new PatternRegistry();

        PatternRegistry fromPattern = registry.Add("digits", Digits);
        PatternRegistry fromConstrained = registry.Add("card", CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum"));

        fromPattern.ShouldBeSameAs(registry);
        fromConstrained.ShouldBeSameAs(registry);
    }

    [Fact]
    public void Add_DuplicateName_ReportsTheNameAndKeepsTheOriginalPattern()
    {
        Pattern letters = Pattern.With.Letter.Repeat.OneOrMore;
        PatternRegistry registry = new PatternRegistry().Add("digits", Digits);

        ArgumentException exception = Should.Throw<ArgumentException>(() => registry.Add("DIGITS", letters));

        exception.Message.ShouldStartWith("A pattern named 'DIGITS' is already registered.");
        registry.Get("digits").Pattern.ShouldBeSameAs(Digits);
        registry.Names.ShouldBe(new[] { "digits" });
    }

    [Fact]
    public void Add_DuplicateConstrainedPatternName_ThrowsArgumentException()
    {
        ConstrainedPattern card = CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");
        PatternRegistry registry = new PatternRegistry().Add("card", card);

        ArgumentException exception = Should.Throw<ArgumentException>(() => registry.Add("Card", card));

        exception.ParamName.ShouldBe("name");
        exception.Message.ShouldStartWith("A pattern named 'Card' is already registered.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t")]
    public void Add_ConstrainedPatternWithBlankName_ThrowsArgumentException(string name)
    {
        PatternRegistry registry = new PatternRegistry();
        ConstrainedPattern card = CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");

        ArgumentException exception = Should.Throw<ArgumentException>(() => registry.Add(name, card));

        exception.ParamName.ShouldBe("name");
        registry.Names.ShouldBeEmpty();
    }

    [Fact]
    public void Add_NameWithSurroundingWhitespace_IsStoredVerbatim()
    {
        PatternRegistry registry = new PatternRegistry().Add(" digits ", Digits);

        registry.Names.ShouldBe(new[] { " digits " });
        registry.Contains(" digits ").ShouldBeTrue();
        registry.Contains("digits").ShouldBeFalse();
    }

    [Fact]
    public void Add_PlainPattern_IsWrappedWithoutRules()
    {
        PatternRegistry registry = new PatternRegistry().Add("digits", Digits);

        ConstrainedPattern pattern = registry.Get("digits");

        pattern.Pattern.ShouldBeSameAs(Digits);
        pattern.IsValid("123").ShouldBeTrue();
        pattern.IsValid("12a").ShouldBeFalse();
    }

    [Fact]
    public async Task Add_ConcurrentDistinctNames_RegistersEveryName()
    {
        PatternRegistry registry = new PatternRegistry();
        string[] names = Enumerable.Range(0, 64).Select(index => $"p{index:D2}").ToArray();

        await Task.WhenAll(names.Select(name => Task.Run(() => registry.Add(name, Digits), TestContext.Current.CancellationToken)));

        registry.Names.ShouldBe(names);
    }

    [Fact]
    public void Names_EmptyRegistry_IsEmpty()
    {
        PatternRegistry registry = new PatternRegistry();

        IReadOnlyList<string> names = registry.Names;

        names.ShouldBeEmpty();
    }

    [Fact]
    public void Names_AreSortedOrdinallyAndKeepTheirRegisteredCasing()
    {
        PatternRegistry registry = new PatternRegistry().Add("beta", Digits).Add("Alpha", Digits).Add("Zeta", Digits);

        IReadOnlyList<string> names = registry.Names;

        names.ShouldBe(new[] { "Alpha", "Zeta", "beta" });
    }

    [Fact]
    public void Names_IsASnapshotThatDoesNotChangeAfterLaterAdds()
    {
        PatternRegistry registry = new PatternRegistry().Add("digits", Digits);
        IReadOnlyList<string> before = registry.Names;

        registry.Add("more", Digits);

        before.ShouldBe(new[] { "digits" });
        registry.Names.ShouldBe(new[] { "digits", "more" });
    }

    [Theory]
    [InlineData("digits", true)]
    [InlineData("DIGITS", true)]
    [InlineData("Digits", true)]
    [InlineData("digit", false)]
    [InlineData("", false)]
    public void Contains_Name_ReportsRegistrationCaseInsensitively(string name, bool expected)
    {
        PatternRegistry registry = new PatternRegistry().Add("digits", Digits);

        bool contains = registry.Contains(name);

        contains.ShouldBe(expected);
    }

    [Fact]
    public void Contains_EmptyRegistry_ReturnsFalse()
    {
        PatternRegistry registry = new PatternRegistry();

        bool contains = registry.Contains("digits");

        contains.ShouldBeFalse();
    }

    [Fact]
    public void TryGet_RegisteredNameInAnyCase_ReturnsTrueAndThePattern()
    {
        ConstrainedPattern card = CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");
        PatternRegistry registry = new PatternRegistry().Add("card", card);

        bool found = registry.TryGet("CARD", out ConstrainedPattern? pattern);

        found.ShouldBeTrue();
        pattern.ShouldBeSameAs(card);
    }

    [Fact]
    public void TryGet_EmptyName_ReturnsFalse()
    {
        PatternRegistry registry = new PatternRegistry().Add("digits", Digits);

        bool found = registry.TryGet(string.Empty, out ConstrainedPattern? pattern);

        found.ShouldBeFalse();
        pattern.ShouldBeNull();
    }

    [Fact]
    public void Get_EmptyRegistry_ThrowsAndSaysNoneAreRegistered()
    {
        PatternRegistry registry = new PatternRegistry();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => registry.Get("sku"));

        exception.Message.ShouldBe(
            "No fluent pattern named 'sku' is registered (registered: none). " +
            "Register it with services.AddFluentPatterns(registry => registry.Add(\"sku\", pattern)).");
    }

    [Fact]
    public void Get_UnknownName_ListsRegisteredNamesInOrdinalOrder()
    {
        PatternRegistry registry = new PatternRegistry().Add("zip", Digits).Add("beta", Digits).Add("Card", Digits);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => registry.Get("sku"));

        exception.Message.ShouldContain("(registered: Card, beta, zip)");
    }

    [Fact]
    public void Get_NullName_ThrowsArgumentNullException()
    {
        PatternRegistry registry = new PatternRegistry().Add("digits", Digits);

        ArgumentNullException exception = Should.Throw<ArgumentNullException>(() => registry.Get(null!));

        exception.ParamName.ShouldBe("name");
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
