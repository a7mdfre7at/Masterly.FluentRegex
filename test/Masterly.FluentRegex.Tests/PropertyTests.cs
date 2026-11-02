namespace Masterly.FluentRegex.Tests;

public class PropertyTests
{
    private static readonly Pattern ShortAlphanumeric = Pattern.FromRegex("^[a-z0-9]{1,20}$");
    private static readonly Pattern Digits = Pattern.FromRegex(@"^\d{2,8}$");

    [Fact]
    public void ForMatching_PropertyHolds_Passes()
    {
        Pattern threeDigits = Pattern.FromRegex(@"^\d{3}$");

        PropertyResult result = PatternProperty.ForMatching(threeDigits, s => s.Length == 3);

        result.Passed.ShouldBeTrue();
        result.SamplesTested.ShouldBeGreaterThan(0);
        result.Counterexample.ShouldBeNull();
        result.OriginalCounterexample.ShouldBeNull();
        result.Exception.ShouldBeNull();
        result.ShrinkSteps.ShouldBe(0);
    }

    [Fact]
    public void ForMatching_PropertyFails_ShrinksToSmallestSimplestCounterexample()
    {
        PropertyResult result = PatternProperty.ForMatching(ShortAlphanumeric, s => s.Length < 5);

        result.Passed.ShouldBeFalse();
        result.Counterexample.ShouldBe("aaaaa");
        result.OriginalCounterexample!.Length.ShouldBeGreaterThanOrEqualTo(5);
        result.ShrinkSteps.ShouldBeGreaterThan(0);
        result.Exception.ShouldBeNull();
    }

    [Fact]
    public void ForMatching_PropertyThrows_CountsAsFailureAndCapturesException()
    {
        PropertyResult result = PatternProperty.ForMatching(Digits, s => s.Contains('7') ? throw new InvalidOperationException("seven") : true);

        result.Passed.ShouldBeFalse();
        result.Counterexample!.Length.ShouldBe(2);
        result.Counterexample.ShouldContain('7');
        result.Counterexample.ShouldContain('0');
        result.Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("seven");
    }

    [Fact]
    public void ForMatching_ShrunkCounterexample_KeepsPatternMembership()
    {
        PropertyResult result = PatternProperty.ForMatching(Digits, s => s.Length < 4);

        result.Counterexample.ShouldBe("0000");
        Digits.IsMatch(result.Counterexample!).ShouldBeTrue();
    }

    [Fact]
    public void ForNonMatching_PropertyFails_ShrinksWhileStayingNonMatching()
    {
        Pattern digitsOnly = Pattern.FromRegex(@"^\d+$");

        PropertyResult result = PatternProperty.ForNonMatching(digitsOnly, s => s.Length < 3);

        result.Passed.ShouldBeFalse();
        result.Counterexample.ShouldBe("aaa");
        digitsOnly.IsMatch(result.Counterexample!).ShouldBeFalse();
    }

    [Fact]
    public void ForNonMatching_PropertyHolds_Passes()
    {
        Pattern digitsOnly = Pattern.FromRegex(@"^\d+$");

        PropertyResult result = PatternProperty.ForNonMatching(digitsOnly, s => !digitsOnly.IsMatch(s));

        result.Passed.ShouldBeTrue();
    }

    [Fact]
    public void ForMatching_SameSeed_ProducesSameResult()
    {
        PropertyOptions options = new PropertyOptions { Seed = 7, Samples = 50 };

        PropertyResult first = PatternProperty.ForMatching(ShortAlphanumeric, s => !s.Contains('z'), options);
        PropertyResult second = PatternProperty.ForMatching(ShortAlphanumeric, s => !s.Contains('z'), options);

        first.Passed.ShouldBeFalse();
        second.Counterexample.ShouldBe(first.Counterexample);
        second.OriginalCounterexample.ShouldBe(first.OriginalCounterexample);
        second.ShrinkSteps.ShouldBe(first.ShrinkSteps);
        second.SamplesTested.ShouldBe(first.SamplesTested);
        first.Seed.ShouldBe(7);
    }

    [Fact]
    public void ForMatching_ZeroShrinkBudget_ReturnsOriginalCounterexample()
    {
        PropertyOptions options = new PropertyOptions { MaxShrinkSteps = 0 };

        PropertyResult result = PatternProperty.ForMatching(ShortAlphanumeric, s => s.Length < 5, options);

        result.Counterexample.ShouldBe(result.OriginalCounterexample);
        result.ShrinkSteps.ShouldBe(0);
    }

    [Fact]
    public void ForMatching_SmallShrinkBudget_StopsAtBudget()
    {
        PropertyOptions options = new PropertyOptions { MaxShrinkSteps = 3 };

        PropertyResult result = PatternProperty.ForMatching(ShortAlphanumeric, s => s.Length < 5, options);

        result.ShrinkSteps.ShouldBeLessThanOrEqualTo(3);
        result.Passed.ShouldBeFalse();
    }

    [Fact]
    public void ThrowIfFailed_PropertyFailed_ThrowsWithEscapedCounterexampleSeedAndReproduction()
    {
        Pattern tabbed = Pattern.FromRegex(@"^a\sb$");
        PropertyResult result = PatternProperty.ForMatching(tabbed, s => !s.Contains('\t'), new PropertyOptions { Seed = 3 });

        PropertyFailedException exception = Should.Throw<PropertyFailedException>(result.ThrowIfFailed);

        result.Counterexample.ShouldBe("a\tb");
        exception.Message.ShouldContain("\"a\\tb\"");
        exception.Message.ShouldContain("Original counterexample");
        exception.Message.ShouldContain("Seed:                    3");
        exception.Message.ShouldContain("new PropertyOptions { Seed = 3, Samples = 100 }");
        exception.Result.ShouldBeSameAs(result);
    }

    [Fact]
    public void ThrowIfFailed_PropertyThrew_UsesPropertyExceptionAsInnerException()
    {
        PropertyResult result = PatternProperty.ForMatching(Digits, s => s.Contains('7') ? throw new FormatException("bad") : true);

        PropertyFailedException exception = Should.Throw<PropertyFailedException>(result.ThrowIfFailed);

        exception.InnerException.ShouldBeSameAs(result.Exception);
        exception.Message.ShouldContain("FormatException: bad");
    }

    [Fact]
    public void ThrowIfFailed_PropertyPassed_DoesNotThrow()
    {
        PropertyResult result = PatternProperty.ForMatching(Digits, s => s.Length >= 2);

        Should.NotThrow(result.ThrowIfFailed);
        result.ToString().ShouldContain("Property passed");
    }

    [Fact]
    public void ToString_PropertyFailed_ReturnsFailureReport()
    {
        PropertyResult result = PatternProperty.ForMatching(ShortAlphanumeric, s => s.Length < 5);

        result.ToString().ShouldContain("Counterexample:          \"aaaaa\"");
    }

    [Fact]
    public void ForNonMatching_PatternMatchesEverything_ThrowsInvalidOperation()
    {
        Pattern everything = Pattern.FromRegex(".*");

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => PatternProperty.ForNonMatching(everything, s => true));

        exception.Message.ShouldContain("no non-matching samples");
    }

    [Fact]
    public void ForMatching_NullPattern_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => PatternProperty.ForMatching(null!, s => true)).ParamName.ShouldBe("pattern");
    }

    [Fact]
    public void ForNonMatching_NullProperty_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => PatternProperty.ForNonMatching(Digits, null!)).ParamName.ShouldBe("property");
    }

    [Fact]
    public void PropertyOptions_NonPositiveSamples_Throws()
    {
        PropertyOptions options = new PropertyOptions();

        Should.Throw<ArgumentOutOfRangeException>(() => options.Samples = 0);
    }

    [Fact]
    public void PropertyOptions_NegativeShrinkSteps_Throws()
    {
        PropertyOptions options = new PropertyOptions();

        Should.Throw<ArgumentOutOfRangeException>(() => options.MaxShrinkSteps = -1);
    }

    [Fact]
    public void PropertyOptions_Defaults_AreDocumentedValues()
    {
        PropertyOptions options = new PropertyOptions();

        options.Samples.ShouldBe(100);
        options.Seed.ShouldBe(0);
        options.MaxShrinkSteps.ShouldBe(1000);
    }

    [Fact]
    public void PropertyFailedException_PublicConstructors_SetMessageAndInner()
    {
        InvalidOperationException inner = new InvalidOperationException("inner");

        PropertyFailedException exception = new PropertyFailedException("outer", inner);

        exception.Message.ShouldBe("outer");
        exception.InnerException.ShouldBeSameAs(inner);
        exception.Result.ShouldBeNull();
        new PropertyFailedException().Message.ShouldNotBeNullOrEmpty();
    }
}
