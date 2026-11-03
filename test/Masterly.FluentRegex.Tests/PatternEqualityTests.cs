using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class PatternEqualityTests
{
    [Fact]
    public void Equals_SameRegexBuiltDifferently_IsTrue()
    {
        Pattern fluent = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;
        Pattern parsed = Pattern.FromRegex(@"^\d+$");

        bool equal = fluent.Equals(parsed);

        equal.ShouldBeTrue();
        fluent.GetHashCode().ShouldBe(parsed.GetHashCode());
    }

    [Fact]
    public void Equals_SameInstance_IsTrue()
    {
        Pattern pattern = Pattern.FromRegex("abc");

        pattern.Equals(pattern).ShouldBeTrue();
        pattern.Equals((object)pattern).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentRegexText_IsFalse()
    {
        Pattern left = Pattern.FromRegex("abc");
        Pattern right = Pattern.FromRegex("abd");

        left.Equals(right).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentOptions_IsFalse()
    {
        Pattern sensitive = Pattern.FromRegex("abc");
        Pattern insensitive = Pattern.FromRegex("abc", RegexOptions.IgnoreCase);

        sensitive.Equals(insensitive).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentTimeout_IsFalse()
    {
        Pattern unlimited = Pattern.FromRegex("abc");
        Pattern limited = Pattern.FromRegex("abc").WithTimeout(TimeSpan.FromSeconds(1));

        unlimited.Equals(limited).ShouldBeFalse();
    }

    [Fact]
    public void Equals_NullOrOtherType_IsFalse()
    {
        Pattern pattern = Pattern.FromRegex("abc");

        pattern.Equals((Pattern?)null).ShouldBeFalse();
        pattern.Equals((object?)null).ShouldBeFalse();
        pattern.Equals("abc").ShouldBeFalse();
    }

    [Fact]
    public void Equals_EmptyPatterns_AreEqual()
    {
        Pattern.With.Equals(new Pattern()).ShouldBeTrue();
        Pattern.With.GetHashCode().ShouldBe(new Pattern().GetHashCode());
    }

    [Fact]
    public void EqualityOperator_IsNotOverloaded_KeepsReferenceSemantics()
    {
        Pattern left = Pattern.FromRegex("abc");
        Pattern right = Pattern.FromRegex("abc");

#pragma warning disable CS0252 // Possible unintended reference comparison: that is exactly what is being asserted.
        bool sameReference = left == right;
#pragma warning restore CS0252

        sameReference.ShouldBeFalse();
        left.Equals(right).ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_EqualPatterns_CanKeyADictionary()
    {
        Dictionary<Pattern, string> cache = new Dictionary<Pattern, string> { [Pattern.FromRegex(@"\d+")] = "digits" };

        string value = cache[Pattern.With.Digit.Repeat.OneOrMore];

        value.ShouldBe("digits");
    }

    [Fact]
    public void GetHashCode_SameValueTwice_IsStable()
    {
        Pattern pattern = Pattern.FromRegex("abc", RegexOptions.Multiline).WithTimeout(TimeSpan.FromSeconds(2));

        pattern.GetHashCode().ShouldBe(pattern.GetHashCode());
    }
}
