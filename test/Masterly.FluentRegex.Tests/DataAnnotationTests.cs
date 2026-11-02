using System.ComponentModel.DataAnnotations;

namespace Masterly.FluentRegex.Tests;

public static class AnnotationPatterns
{
    public static readonly Pattern SkuField = Pattern.With.UppercaseLetter.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);

    public static Pattern Sku => SkuField;

    public static ConstrainedPattern Card => CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");

    public static string NotAPattern => "ABC-1234";

    public static Pattern? Nothing => null;

    public static Pattern Broken => throw new InvalidOperationException("boom");

    public static Pattern SkuMethod() => SkuField;
}

public class DataAnnotationTests
{
    private sealed class Product
    {
        [MatchesPattern(typeof(AnnotationPatterns), nameof(AnnotationPatterns.Sku))]
        public string? Sku { get; set; }

        [MatchesPattern(typeof(AnnotationPatterns), nameof(AnnotationPatterns.Card))]
        public string? Card { get; set; }

        [Display(Name = "Stock code")]
        [MatchesPattern(typeof(AnnotationPatterns), nameof(AnnotationPatterns.SkuField))]
        public object? Code { get; set; }

        [MatchesPattern(typeof(AnnotationPatterns), nameof(AnnotationPatterns.Card), ErrorMessage = "Bad card in {0}.")]
        public string? BackupCard { get; set; }
    }

    private static List<ValidationResult> Validate(Product product)
    {
        List<ValidationResult> results = new List<ValidationResult>();
        Validator.TryValidateObject(product, new ValidationContext(product), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Validate_ValidValues_ProducesNoErrors()
    {
        Product product = new Product { Sku = "ABC-1234", Card = "4111 1111 1111 1111", Code = "XYZ-0001" };

        List<ValidationResult> results = Validate(product);

        results.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ABC-12345")]
    [InlineData("xABC-1234")]
    [InlineData("abc-1234")]
    public void Validate_PartialOrWrongMatch_FailsBecauseTheWholeValueMustMatch(string sku)
    {
        Product product = new Product { Sku = sku };

        ValidationResult result = Validate(product).ShouldHaveSingleItem();

        result.ErrorMessage.ShouldBe("The field Sku does not match the required pattern.");
        result.MemberNames.ShouldBe(new[] { "Sku" });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_NullOrEmpty_IsValid(string? sku)
    {
        Product product = new Product { Sku = sku };

        List<ValidationResult> results = Validate(product);

        results.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_NonStringValue_ReportsAClearError()
    {
        Product product = new Product { Code = 42 };

        ValidationResult result = Validate(product).ShouldHaveSingleItem();

        result.ErrorMessage.ShouldBe("The field Stock code must be a string to be matched against a pattern.");
    }

    [Fact]
    public void Validate_ConstrainedPatternRuleFails_UsesTheRuleDescription()
    {
        Product product = new Product { Card = "4111 1111 1111 1112" };

        ValidationResult result = Validate(product).ShouldHaveSingleItem();

        result.ErrorMessage.ShouldBe("The field Card is invalid: Luhn checksum.");
    }

    [Fact]
    public void Validate_ConstrainedPatternShapeFails_UsesTheDefaultMessage()
    {
        Product product = new Product { Card = "4111" };

        ValidationResult result = Validate(product).ShouldHaveSingleItem();

        result.ErrorMessage.ShouldBe("The field Card does not match the required pattern.");
    }

    [Fact]
    public void Validate_CustomErrorMessage_WinsOverTheRuleDescription()
    {
        Product product = new Product { BackupCard = "4111 1111 1111 1112" };

        ValidationResult result = Validate(product).ShouldHaveSingleItem();

        result.ErrorMessage.ShouldBe("Bad card in BackupCard.");
    }

    [Theory]
    [InlineData(nameof(AnnotationPatterns.Sku))]
    [InlineData(nameof(AnnotationPatterns.SkuField))]
    [InlineData(nameof(AnnotationPatterns.SkuMethod))]
    public void IsValid_ResolvesPropertiesFieldsAndMethods(string member)
    {
        MatchesPatternAttribute attribute = new MatchesPatternAttribute(typeof(AnnotationPatterns), member);

        bool valid = attribute.IsValid("ABC-1234");
        bool invalid = attribute.IsValid("ABC-12");

        valid.ShouldBeTrue();
        invalid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_MissingMember_ThrowsInvalidOperationExceptionNamingTypeAndMember()
    {
        MatchesPatternAttribute attribute = new MatchesPatternAttribute(typeof(AnnotationPatterns), "Missing");

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => attribute.IsValid("x"));

        exception.Message.ShouldContain(typeof(AnnotationPatterns).FullName!);
        exception.Message.ShouldContain("'Missing'");
    }

    [Fact]
    public void IsValid_MemberOfTheWrongType_ThrowsInvalidOperationException()
    {
        MatchesPatternAttribute attribute = new MatchesPatternAttribute(typeof(AnnotationPatterns), nameof(AnnotationPatterns.NotAPattern));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => attribute.IsValid("x"));

        exception.Message.ShouldContain("System.String");
        exception.Message.ShouldContain("NotAPattern");
    }

    [Fact]
    public void IsValid_MemberReturnsNull_ThrowsInvalidOperationException()
    {
        MatchesPatternAttribute attribute = new MatchesPatternAttribute(typeof(AnnotationPatterns), nameof(AnnotationPatterns.Nothing));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => attribute.IsValid("x"));

        exception.Message.ShouldContain("returned null");
    }

    [Fact]
    public void IsValid_MemberThrows_WrapsTheException()
    {
        MatchesPatternAttribute attribute = new MatchesPatternAttribute(typeof(AnnotationPatterns), nameof(AnnotationPatterns.Broken));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => attribute.IsValid("x"));

        exception.Message.ShouldContain("Broken");
        exception.InnerException.ShouldNotBeNull();
        exception.InnerException!.Message.ShouldBe("boom");
    }

    [Fact]
    public void IsValid_RepeatedCalls_ReuseTheResolvedPattern()
    {
        MatchesPatternAttribute first = new MatchesPatternAttribute(typeof(AnnotationPatterns), nameof(AnnotationPatterns.Sku));
        MatchesPatternAttribute second = new MatchesPatternAttribute(typeof(AnnotationPatterns), nameof(AnnotationPatterns.Sku));

        bool[] results = { first.IsValid("ABC-1234"), first.IsValid("ABC-1234"), second.IsValid("ABC-1234") };

        results.ShouldAllBe(result => result);
    }

    [Fact]
    public void Constructor_InvalidArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new MatchesPatternAttribute(null!, "Sku")).ParamName.ShouldBe("patternType");
        Should.Throw<ArgumentNullException>(() => new MatchesPatternAttribute(typeof(AnnotationPatterns), null!)).ParamName.ShouldBe("memberName");
        Should.Throw<ArgumentException>(() => new MatchesPatternAttribute(typeof(AnnotationPatterns), " ")).ParamName.ShouldBe("memberName");
    }

    [Fact]
    public void Properties_ExposeTheConfiguredMember()
    {
        MatchesPatternAttribute attribute = new MatchesPatternAttribute(typeof(AnnotationPatterns), nameof(AnnotationPatterns.Sku));

        attribute.PatternType.ShouldBe(typeof(AnnotationPatterns));
        attribute.MemberName.ShouldBe("Sku");
    }
}
