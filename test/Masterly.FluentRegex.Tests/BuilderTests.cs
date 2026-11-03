using System.Globalization;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class BuilderTests
{
    [Fact]
    public void Patterns_AreImmutable_SoBuildingBlocksCanBeReused()
    {
        Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

        Pattern price = Pattern.With.Literal("$").Then(digits);
        Pattern year = digits.Literal("AD");

        digits.ToString().ShouldBe(@"\d+");
        price.ToString().ShouldBe(@"\$\d+");
        year.ToString().ShouldBe(@"\d+AD");
    }

    [Fact]
    public void Lazy_MakesQuantifierNonGreedy()
    {
        Pattern lazy = Pattern.With.Literal("<").Anything.Repeat.OneOrMore.Lazy.Literal(">");

        lazy.ToString().ShouldBe("<.+?>");
        lazy.Match("<a>text<b>").Value.ShouldBe("<a>");
    }

    [Fact]
    public void Lazy_WorksWithBoundedQuantifiers()
    {
        Pattern.With.Digit.Repeat.Times(2, 5).Lazy.ToString().ShouldBe(@"\d{2,5}?");
        Pattern.With.Digit.Repeat.Optional.Lazy.ToString().ShouldBe(@"\d??");
    }

    [Fact]
    public void Possessive_RendersAtomicGroup()
    {
        Pattern possessive = Pattern.With.Digit.Repeat.OneOrMore.Possessive.Digit;

        possessive.ToString().ShouldBe(@"(?>\d+)\d");
        possessive.IsMatch("12345").ShouldBeFalse(); // the possessive part never gives a digit back
        Pattern.With.Digit.Repeat.OneOrMore.Digit.IsMatch("12345").ShouldBeTrue();
    }

    [Fact]
    public void Possessive_OnRawRegex_OnlyWrapsTheLastAtom()
    {
        Pattern.With.RegEx("ab").Repeat.OneOrMore.Possessive.ToString().ShouldBe("a(?>b+)");
        Pattern.With.RegEx(@"x\d").Repeat.ZeroOrMore.Possessive.ToString().ShouldBe(@"x(?>\d*)");
    }

    [Fact]
    public void Possessive_OnLiteral_OnlyWrapsTheLastCharacter()
    {
        Pattern.With.Literal("ab").Repeat.OneOrMore.Possessive.ToString().ShouldBe("a(?>b+)");
    }

    [Fact]
    public void LazyAndPossessive_WithoutQuantifier_Throw()
    {
        Should.Throw<InvalidOperationException>(() => Pattern.With.Digit.Lazy);
        Should.Throw<InvalidOperationException>(() => Pattern.With.Digit.Possessive);
    }

    [Fact]
    public void Repeat_OnEmptyPattern_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Pattern.With.Repeat.OneOrMore);
    }

    [Fact]
    public void Repeat_ValidatesBounds()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Pattern.With.Digit.Repeat.Times(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => Pattern.With.Digit.Repeat.Times(5, 2));
        Should.Throw<ArgumentOutOfRangeException>(() => Pattern.With.Digit.Repeat.AtLeast(-2));
        Should.Throw<ArgumentOutOfRangeException>(() => Pattern.With.Digit.Repeat.AtMost(-2));
    }

    [Fact]
    public void Repeat_OnQuantifiedElement_WrapsInsteadOfProducingNestedQuantifierError()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.Times(2).Repeat.Times(3);

        pattern.ToString().ShouldBe(@"(?:\d{2}){3}");
        pattern.IsValid().ShouldBeTrue();
    }

    [Fact]
    public void Literal_InsideSet_EscapesHyphen()
    {
        Pattern set = Pattern.With.Set(Pattern.With.Literal("a-z"));

        set.ToString().ShouldBe(@"[a\-z]");
        set.IsMatch("-").ShouldBeTrue();
        set.IsMatch("m").ShouldBeFalse();
    }

    [Fact]
    public void Literal_OutsideSet_DoesNotEscapeHyphen()
    {
        Pattern.With.Literal("a-z").ToString().ShouldBe("a-z");
    }

    [Fact]
    public void Literal_EscapesControlCharacters()
    {
        Pattern.With.Literal("a\tb\n").ToString().ShouldBe(@"a\tb\n");
        Pattern.With.Literal("\u0001").ToString().ShouldBe(@"\u0001");
    }

    [Fact]
    public void Range_RendersStandaloneAndInsideSets()
    {
        Pattern.With.Range('a', 'f').ToString().ShouldBe("[a-f]");
        Pattern.With.Set(Pattern.With.Range('a', 'f').Range('0', '9').Literal("_")).ToString().ShouldBe("[a-f0-9_]");
        Should.Throw<ArgumentException>(() => Pattern.With.Range('z', 'a'));
    }

    [Fact]
    public void Range_EscapesSpecialBoundaries()
    {
        Pattern range = Pattern.With.Range('-', '/');
        range.ToString().ShouldBe(@"[\u002D-/]");
        range.IsMatch(".").ShouldBeTrue();
        range.IsMatch(",").ShouldBeFalse();
    }

    [Fact]
    public void HexDigit_Works()
    {
        Pattern hex = Pattern.With.StartOfLine.HexDigit.Repeat.OneOrMore.EndOfLine;
        hex.ToString().ShouldBe("^[0-9a-fA-F]+$");
        hex.IsMatch("DeadBeef01").ShouldBeTrue();
        hex.IsMatch("xyz").ShouldBeFalse();
    }

    [Fact]
    public void AnyOf_AndNoneOf_TreatEveryCharacterLiterally()
    {
        Pattern any = Pattern.With.AnyOf("^-]");
        any.ToString().ShouldBe(@"[\^\-\]]");
        any.IsMatch("^").ShouldBeTrue();
        any.IsMatch("-").ShouldBeTrue();
        any.IsMatch("]").ShouldBeTrue();
        any.IsMatch("a").ShouldBeFalse();

        Pattern none = Pattern.With.NoneOf("abc");
        none.ToString().ShouldBe("[^abc]");
        none.IsMatch("d").ShouldBeTrue();
        none.IsMatch("a").ShouldBeFalse();
    }

    [Fact]
    public void Unicode_Categories()
    {
        Pattern.With.Unicode(UnicodeCategory.UppercaseLetter).ToString().ShouldBe(@"\p{Lu}");
        Pattern.With.NotUnicode(UnicodeCategory.DecimalDigitNumber).ToString().ShouldBe(@"\P{Nd}");
        Pattern.With.Unicode(UnicodeGroup.Letter).ToString().ShouldBe(@"\p{L}");

        Pattern anyLetter = Pattern.With.StartOfLine.Unicode(UnicodeGroup.Letter).Repeat.OneOrMore.EndOfLine;
        anyLetter.IsMatch("Ωμέγα").ShouldBeTrue();
        anyLetter.IsMatch("Привет").ShouldBeTrue();
        anyLetter.IsMatch("abc1").ShouldBeFalse();
    }

    [Fact]
    public void UnicodeBlock_AddsIsPrefix()
    {
        Pattern.With.UnicodeBlock("Greek").ToString().ShouldBe(@"\p{IsGreek}");
        Pattern.With.UnicodeBlock("IsCyrillic").ToString().ShouldBe(@"\p{IsCyrillic}");
        Pattern.With.UnicodeBlock("Greek").IsMatch("λ").ShouldBeTrue();
    }

    [Theory]
    [InlineData("isGreek")]
    [InlineData("ISGreek")]
    public void UnicodeBlock_LowercaseOrUppercaseIsPrefix_IsRecognised(string name)
    {
        Pattern pattern = Pattern.With.UnicodeBlock(name);

        pattern.ToString().ShouldBe(@"\p{IsGreek}");
    }

    [Fact]
    public void UnicodeBlock_UnknownName_ThrowsWhenBuilt()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => Pattern.With.UnicodeBlock("Klingon"));

        exception.ParamName.ShouldBe("blockName");
        exception.Message.ShouldContain("Unknown Unicode block 'Klingon'");
    }

    [Fact]
    public void OrOperator_KeepsTheSharedOptionsOfItsOperands()
    {
        Pattern yes = Pattern.With.Literal("yes").WithOptions(RegexOptions.IgnoreCase);
        Pattern no = Pattern.With.Literal("no").WithOptions(RegexOptions.IgnoreCase);

        Pattern answer = yes | no;

        answer.Options.ShouldBe(RegexOptions.IgnoreCase);
        answer.IsMatch("YES").ShouldBeTrue();
    }

    [Fact]
    public void OrOperator_TakesOptionsAndTimeoutFromTheLeftOperand_LikePlus()
    {
        TimeSpan timeout = TimeSpan.FromSeconds(2);
        Pattern left = Pattern.With.Literal("a").WithOptions(RegexOptions.Multiline).WithTimeout(timeout);
        Pattern right = Pattern.With.Literal("b");

        Pattern either = left | right;
        Pattern both = left + right;

        either.Options.ShouldBe(RegexOptions.Multiline);
        either.Timeout.ShouldBe(timeout);
        both.Options.ShouldBe(either.Options);
        both.Timeout.ShouldBe(either.Timeout);
    }

    [Fact]
    public void AnyOfAndNoneOf_Null_ThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Pattern.With.AnyOf(null!)).ParamName.ShouldBe("characters");
        Should.Throw<ArgumentNullException>(() => Pattern.With.NoneOf(null!)).ParamName.ShouldBe("characters");
    }

    [Fact]
    public void AnyOfAndNoneOf_Empty_ThrowArgumentException()
    {
        Should.Throw<ArgumentException>(() => Pattern.With.AnyOf(string.Empty)).ParamName.ShouldBe("characters");
        Should.Throw<ArgumentException>(() => Pattern.With.NoneOf(string.Empty)).ParamName.ShouldBe("characters");
    }

    [Fact]
    public void LiteralCharacters_Null_NamesTheParameter()
    {
        Should.Throw<ArgumentNullException>(() => Pattern.With.Literal((char[])null!)).ParamName.ShouldBe("characters");
    }

    [Fact]
    public void Unicode_InsideSet()
    {
        Pattern.With.Set(Pattern.With.Unicode(UnicodeGroup.Letter).Digit).ToString().ShouldBe(@"[\p{L}\d]");
    }

    [Fact]
    public void Anchors()
    {
        Pattern.With.StartOfInput.Digit.EndOfInput.ToString().ShouldBe(@"\A\d\z");
        Pattern.With.NonWordBoundary.ToString().ShouldBe(@"\B");
    }

    [Fact]
    public void OneOf_IsNonCapturing()
    {
        Pattern pattern = Pattern.With.OneOf(Pattern.With.Literal("cat"), Pattern.With.Literal("dog")).Group(Pattern.With.Digit);

        pattern.ToString().ShouldBe(@"(?:cat|dog)(\d)");
        pattern.Match("dog7").Groups[1].Value.ShouldBe("7");
    }

    [Fact]
    public void OneOf_WithStrings_EscapesThem()
    {
        Pattern.With.OneOf("a.b", "c+").ToString().ShouldBe(@"(?:a\.b|c\+)");
    }

    [Fact]
    public void PlusOperator_Concatenates()
    {
        Pattern area = Pattern.With.Digit.Repeat.Times(3);
        Pattern dash = Pattern.With.Literal("-");

        (area + dash + area).ToString().ShouldBe(@"\d{3}-\d{3}");
    }

    [Fact]
    public void PlusOperator_ThenRepeat_AppliesToLastElement()
    {
        ((Pattern.With.Literal("a") + Pattern.With.Literal("b")).Repeat.OneOrMore).ToString().ShouldBe("ab+");
    }

    [Fact]
    public void OrOperator_FlattensChains()
    {
        Pattern pattern = Pattern.With.Literal("a") | Pattern.With.Literal("b") | Pattern.With.Literal("c");

        pattern.ToString().ShouldBe("(?:a|b|c)");
    }

    [Fact]
    public void OrOperator_DoesNotFlattenExplicitOneOf()
    {
        Pattern pattern = Pattern.With.OneOf(Pattern.With.Literal("a"), Pattern.With.Literal("b")) | Pattern.With.Literal("c");

        pattern.ToString().ShouldBe("(?:(?:a|b)|c)");
    }

    [Fact]
    public void StringConcatenation_StillProducesString()
    {
        // Pattern deliberately has no implicit string conversion, so logging code keeps working.
        string text = "Pattern: " + Pattern.With.Digit;
        text.ShouldBe(@"Pattern: \d");
    }

    [Fact]
    public void IgnoreCase_Scope()
    {
        Pattern pattern = Pattern.With.Literal("id-").IgnoreCase(Pattern.With.Literal("abc"));

        pattern.ToString().ShouldBe("id-(?i:abc)");
        pattern.IsMatch("id-ABC").ShouldBeTrue();
        pattern.IsMatch("ID-abc").ShouldBeFalse();
    }

    [Fact]
    public void CaseSensitive_Scope()
    {
        Pattern pattern = Pattern.With.CaseSensitive(Pattern.With.Literal("X")).WithOptions(RegexOptions.IgnoreCase);

        pattern.ToString().ShouldBe("(?-i:X)");
        pattern.IsMatch("x").ShouldBeFalse();
    }

    [Fact]
    public void InlineOptions_RendersFlags()
    {
        Pattern.With.InlineOptions(RegexOptions.IgnoreCase | RegexOptions.Singleline, Pattern.With.Anything, RegexOptions.Multiline)
            .ToString().ShouldBe("(?is-m:.)");
        Should.Throw<ArgumentException>(() => Pattern.With.InlineOptions(RegexOptions.Compiled, Pattern.With.Digit));
    }

    [Fact]
    public void NamedGroup_ValidatesName()
    {
        Should.Throw<ArgumentException>(() => Pattern.With.NamedGroup("bad name", Pattern.With.Digit));
        Should.Throw<ArgumentException>(() => Pattern.With.NamedGroup("", Pattern.With.Digit));
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => Pattern.With.Group(null));
        Should.Throw<ArgumentNullException>(() => Pattern.With.Literal((string)null));
    }

    [Fact]
    public void WithTimeout_AppliesToRegex()
    {
        Pattern pattern = Pattern.With.Digit.WithTimeout(TimeSpan.FromSeconds(2));

        pattern.Timeout.ShouldBe(TimeSpan.FromSeconds(2));
        pattern.ToRegex().MatchTimeout.ShouldBe(TimeSpan.FromSeconds(2));
        pattern.Literal("x").Timeout.ShouldBe(TimeSpan.FromSeconds(2)); // carried through the chain
        Should.Throw<ArgumentOutOfRangeException>(() => pattern.WithTimeout(TimeSpan.Zero));
    }

    [Fact]
    public void WithTimeout_StopsCatastrophicBacktracking()
    {
        Pattern evil = Pattern.With.StartOfLine
            .Group(Pattern.With.Literal("a").Repeat.OneOrMore).Repeat.OneOrMore
            .EndOfLine
            .WithTimeout(TimeSpan.FromMilliseconds(50));

        Should.Throw<RegexMatchTimeoutException>(() => evil.IsMatch(new string('a', 40) + "!"));
    }

    [Fact]
    public void Options_CarryThroughChain()
    {
        Pattern pattern = Pattern.With.Literal("a").WithOptions(RegexOptions.IgnoreCase).Literal("b");

        pattern.Options.ShouldBe(RegexOptions.IgnoreCase);
        pattern.IsMatch("AB").ShouldBeTrue();
    }

    [Fact]
    public void ToRegex_WithoutArguments_UsesPatternOptions()
    {
        Pattern.With.Literal("a").WithOptions(RegexOptions.Multiline).ToRegex().Options.ShouldBe(RegexOptions.Multiline);
    }

    [Fact]
    public void Count_CountsMatches()
    {
        Pattern.With.Digit.Repeat.OneOrMore.Count("a1 b22 c333").ShouldBe(3);
    }

    [Fact]
    public void SpanApis_Work()
    {
        Pattern number = Pattern.With.Digit.Repeat.OneOrMore;
        ReadOnlySpan<char> text = "a1 b22 c333".AsSpan();

        number.IsMatch(text).ShouldBeTrue();
        number.Count(text).ShouldBe(3);

        var found = new List<string>();
        foreach (ValueMatch m in number.EnumerateMatches(text))
            found.Add(text.Slice(m.Index, m.Length).ToString());
        found.ShouldBe(new[] { "1", "22", "333" });
    }

#if NET9_0_OR_GREATER
    [Fact]
    public void EnumerateSplits_Works()
    {
        Pattern comma = Pattern.With.Literal(",");
        ReadOnlySpan<char> text = "a,b,c".AsSpan();

        var parts = new List<string>();
        foreach (Range range in comma.EnumerateSplits(text))
            parts.Add(text[range].ToString());
        parts.ShouldBe(new[] { "a", "b", "c" });
    }
#endif

    [Fact]
    public void UseNonBacktracking_SetsOption()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore.UseNonBacktracking();

        pattern.Options.HasFlag(RegexOptions.NonBacktracking).ShouldBeTrue();
        pattern.IsMatch("123").ShouldBeTrue();
    }

    [Fact]
    public void StrongPassword_NoLongerTreatsDigitsAsSpecialCharacters()
    {
        // In 2.x the special-character set contained the range '+' to '=', which includes all digits.
        CommonPatterns.StrongPassword().IsMatch("Abcdefg1").ShouldBeFalse();
        CommonPatterns.StrongPassword().IsMatch("Abcdef1!").ShouldBeTrue();
    }

    [Fact]
    public void ThreadSafety_SharedPatternUsedConcurrently()
    {
        Pattern shared = Pattern.With.Digit.Repeat.OneOrMore;
        Parallel.For(0, 200, i =>
        {
            Pattern derived = shared.Literal("-").Digit;
            derived.IsMatch(i + "-1").ShouldBeTrue();
            shared.ToString().ShouldBe(@"\d+");
        });
    }
}
