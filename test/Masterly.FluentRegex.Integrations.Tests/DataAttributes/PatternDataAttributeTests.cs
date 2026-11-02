using System.Reflection;
using Masterly.FluentRegex.Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Masterly.FluentRegex.Integrations.Tests.DataAttributes;

public class PatternDataAttributeTests
{
    private static readonly MethodInfo StringTheory = typeof(PatternDataAttributeTests).GetMethod(nameof(TakesString), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo IntTheory = typeof(PatternDataAttributeTests).GetMethod(nameof(TakesInt), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo NoParameterTheory = typeof(PatternDataAttributeTests).GetMethod(nameof(TakesNothing), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ObjectTheory = typeof(PatternDataAttributeTests).GetMethod(nameof(TakesObject), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void TakesString(string value)
    {
    }

    private static void TakesInt(int value)
    {
    }

    private static void TakesNothing()
    {
    }

    private static void TakesObject(object value)
    {
    }

    [Fact]
    public async Task GetData_MatchingField_ReturnsCountRowsThatMatch()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.FiveDigitsField)) { Count = 12 };

        string[] values = await ValuesOf(attribute);

        values.Length.ShouldBe(12);
        values.ShouldAllBe(v => TestPatterns.FiveDigitsField.IsMatch(v));
        values.Distinct().Count().ShouldBe(12);
    }

    [Fact]
    public async Task GetData_NonMatchingProperty_ReturnsRowsThatDoNotMatch()
    {
        NonMatchingDataAttribute attribute = new NonMatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.HexColor)) { Count = 15 };

        string[] values = await ValuesOf(attribute);

        values.Length.ShouldBe(15);
        values.ShouldAllBe(v => !CommonPatterns.HexColor().IsMatch(v));
    }

    [Fact]
    public async Task GetData_Method_ResolvesParameterlessMethod()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.IsoDate)) { Count = 3 };

        string[] values = await ValuesOf(attribute);

        values.ShouldAllBe(v => CommonPatterns.DateIso().IsMatch(v));
    }

    [Fact]
    public async Task GetData_InheritedMember_Resolves()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(DerivedPatternSource), nameof(DerivedPatternSource.Lowercase)) { Count = 4 };

        string[] values = await ValuesOf(attribute);

        values.Length.ShouldBe(4);
    }

    [Fact]
    public async Task GetData_DefaultSeed_IsDeterministic()
    {
        MatchingDataAttribute first = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.HexColor));
        MatchingDataAttribute second = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.HexColor));

        string[] firstValues = await ValuesOf(first);
        string[] secondValues = await ValuesOf(second);

        first.Seed.ShouldBe(0);
        first.Count.ShouldBe(25);
        secondValues.ShouldBe(firstValues);
    }

    [Fact]
    public async Task GetData_DifferentSeed_ProducesDifferentRows()
    {
        MatchingDataAttribute seeded = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.FiveDigitsField)) { Count = 10, Seed = 1 };
        MatchingDataAttribute other = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.FiveDigitsField)) { Count = 10, Seed = 2 };

        string[] seededValues = await ValuesOf(seeded);
        string[] otherValues = await ValuesOf(other);

        otherValues.ShouldNotBe(seededValues);
    }

    [Fact]
    public async Task GetData_PatternWithFewerSamples_ReturnsAvailableRows()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.SingleValue)) { Count = 10 };

        string[] values = await ValuesOf(attribute);

        values.ShouldBe(new[] { "x" });
    }

    [Fact]
    public void SupportsDiscoveryEnumeration_ReturnsTrue()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.HexColor));

        attribute.SupportsDiscoveryEnumeration().ShouldBeTrue();
    }

    [Fact]
    public async Task GetData_MissingMember_ThrowsNamingTypeAndMember()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), "DoesNotExist");

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => ValuesOf(attribute));

        exception.Message.ShouldContain(typeof(TestPatterns).FullName!);
        exception.Message.ShouldContain("'DoesNotExist'");
    }

    [Fact]
    public async Task GetData_MemberOfWrongType_ThrowsNamingActualType()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.NotAPatternField));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => ValuesOf(attribute));

        exception.Message.ShouldContain("System.String");
        exception.Message.ShouldContain(nameof(TestPatterns.NotAPatternField));
    }

    [Fact]
    public async Task GetData_MethodWithParameters_IsNotResolved()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.RequiresArgument));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => ValuesOf(attribute));

        exception.Message.ShouldContain("parameterless method");
    }

    [Fact]
    public async Task GetData_MemberReturnsNull_Throws()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.NullPattern));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => ValuesOf(attribute));

        exception.Message.ShouldContain("returned null");
    }

    [Fact]
    public async Task GetData_CountBelowOne_Throws()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.HexColor)) { Count = 0 };

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => ValuesOf(attribute));

        exception.Message.ShouldContain("Count = 0");
    }

    [Fact]
    public async Task GetData_TheoryWithoutStringParameter_Throws()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.HexColor));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await attribute.GetData(IntTheory, new DisposalTracker()));

        exception.Message.ShouldContain(nameof(TakesInt));
    }

    [Fact]
    public async Task GetData_NoNonMatchingSamples_Throws()
    {
        NonMatchingDataAttribute attribute = new NonMatchingDataAttribute(typeof(EverythingPattern), nameof(EverythingPattern.Anything));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => ValuesOf(attribute));

        exception.Message.ShouldContain("no non-matching samples");
    }

    [Fact]
    public void Constructor_NullType_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => new MatchingDataAttribute(null!, "Member"));
    }

    [Fact]
    public void Constructor_BlankMemberName_ThrowsArgument()
    {
        Should.Throw<ArgumentException>(() => new NonMatchingDataAttribute(typeof(TestPatterns), " "));
    }

    [Fact]
    public async Task GetData_NoMatchingSamples_ThrowsDescribingAttributePatternAndTheory()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.NeverMatches));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => ValuesOf(attribute));

        exception.Message.ShouldBe("[MatchingData(TestPatterns.NeverMatches)] produced no matching samples for pattern '(?!)', so theory 'TakesString' has no data.");
    }

    [Fact]
    public async Task GetData_TheoryWithoutParameters_Throws()
    {
        NonMatchingDataAttribute attribute = new NonMatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.HexColor));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await attribute.GetData(NoParameterTheory, new DisposalTracker()));

        exception.Message.ShouldBe("[NonMatchingData(TestPatterns.HexColor)] supplies one string per row, but theory 'TakesNothing' does not take a string as its first parameter.");
    }

    [Fact]
    public async Task GetData_FirstParameterAcceptsString_ReturnsRows()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.SingleValue));

        IReadOnlyCollection<ITheoryDataRow> rows = await attribute.GetData(ObjectTheory, new DisposalTracker());

        rows.Select(row => row.GetData()[0]).ShouldBe(new object?[] { "x" });
    }

    [Fact]
    public async Task GetData_NullTestMethod_ThrowsArgumentNull()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.HexColor));

        ArgumentNullException exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await attribute.GetData(null!, new DisposalTracker()));

        exception.ParamName.ShouldBe("testMethod");
    }

    [Fact]
    public async Task GetData_NegativeCount_ThrowsBeforeResolvingPattern()
    {
        NonMatchingDataAttribute attribute = new NonMatchingDataAttribute(typeof(TestPatterns), "DoesNotExist") { Count = -3 };

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => ValuesOf(attribute));

        exception.Message.ShouldBe("[NonMatchingData(TestPatterns.DoesNotExist)] has Count = -3; it must be at least 1.");
    }

    [Theory]
    [InlineData(nameof(TestPatterns.NotAPatternProperty), "System.String")]
    [InlineData(nameof(TestPatterns.NotAPatternMethod), "System.Int32")]
    public async Task GetData_NonPatternPropertyOrMethod_ThrowsNamingActualType(string memberName, string actualType)
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), memberName);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => ValuesOf(attribute));

        exception.Message.ShouldBe(
            $"[MatchingData(TestPatterns.{memberName})]: member '{typeof(TestPatterns).FullName}.{memberName}' is of type '{actualType}', not 'Masterly.FluentRegex.Pattern'.");
    }

    [Theory]
    [InlineData(nameof(TestPatterns.NullPatternField))]
    [InlineData(nameof(TestPatterns.NullPatternMethod))]
    public async Task GetData_NullFieldOrMethod_ThrowsReturnedNull(string memberName)
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), memberName);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => ValuesOf(attribute));

        exception.Message.ShouldBe($"[MatchingData(TestPatterns.{memberName})]: member '{typeof(TestPatterns).FullName}.{memberName}' returned null.");
    }

    [Fact]
    public async Task GetData_WriteOnlyProperty_IsNotResolved()
    {
        MatchingDataAttribute attribute = new MatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.WriteOnly));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => ValuesOf(attribute));

        exception.Message.ShouldBe(
            $"[MatchingData(TestPatterns.WriteOnly)]: type '{typeof(TestPatterns).FullName}' has no public static property, field or parameterless method named 'WriteOnly'.");
    }

    [Fact]
    public async Task GetData_NonMatchingWithSeed_ReturnsSeededNonMatchingSamples()
    {
        NonMatchingDataAttribute first = new NonMatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.FiveDigitsField)) { Count = 8, Seed = 7 };
        NonMatchingDataAttribute second = new NonMatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.FiveDigitsField)) { Count = 8, Seed = 7 };

        string[] firstValues = await ValuesOf(first);
        string[] secondValues = await ValuesOf(second);

        firstValues.ShouldBe(TestPatterns.FiveDigitsField.GenerateNonMatching(8, 7));
        secondValues.ShouldBe(firstValues);
        firstValues.ShouldAllBe(value => !TestPatterns.FiveDigitsField.IsMatch(value));
    }

    [Fact]
    public void Constructor_StoresSourceAndMemberName()
    {
        NonMatchingDataAttribute attribute = new NonMatchingDataAttribute(typeof(TestPatterns), nameof(TestPatterns.HexColor));

        attribute.PatternSource.ShouldBe(typeof(TestPatterns));
        attribute.MemberName.ShouldBe(nameof(TestPatterns.HexColor));
    }

    [Fact]
    public void Constructor_NullMemberName_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(() => new MatchingDataAttribute(typeof(TestPatterns), null!));

        exception.ParamName.ShouldBe("memberName");
    }

    [Fact]
    public void Constructor_EmptyMemberName_ThrowsArgument()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => new MatchingDataAttribute(typeof(TestPatterns), string.Empty));

        exception.ParamName.ShouldBe("memberName");
    }

    [Fact]
    public void Constructor_NullTypeOnNonMatching_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(() => new NonMatchingDataAttribute(null!, "Member"));

        exception.ParamName.ShouldBe("patternSource");
    }

    private static async Task<string[]> ValuesOf(DataAttribute attribute)
    {
        IReadOnlyCollection<ITheoryDataRow> rows = await attribute.GetData(StringTheory, new DisposalTracker());
        return rows.Select(row => (string)row.GetData()[0]!).ToArray();
    }

    /// <summary>A pattern that matches every string, so it has no non-matching samples.</summary>
    public static class EverythingPattern
    {
        public static Pattern Anything => Pattern.FromRegex(".*");
    }
}
