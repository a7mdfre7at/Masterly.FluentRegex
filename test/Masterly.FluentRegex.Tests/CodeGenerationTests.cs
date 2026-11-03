using System.Text.RegularExpressions;
using Masterly.FluentRegex.Tests.Infrastructure;

namespace Masterly.FluentRegex.Tests;

public class CodeGenerationTests
{
    [Fact]
    public void ToFluentCode_OctalEscapeWithoutSuchGroup_IsALiteralNotABackreference()
    {
        Pattern pattern = Pattern.FromRegex(@"^\101$");

        string code = pattern.ToFluentCode();

        pattern.IsMatch("A").ShouldBeTrue();
        code.ShouldNotContain("Backreference");
        code.ShouldContain("Literal(\"A\")");
    }

    [Fact]
    public void ToFluentCode_TwoDigitNumberThatNamesAGroup_StaysABackreference()
    {
        Pattern pattern = Pattern.FromRegex(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)(l)\12");

        string code = pattern.ToFluentCode();

        code.ShouldContain("Backreference(12)");
    }

    [Fact]
    public void ToFluentCode_TwoDigitNumberWithFewerGroups_IsAnOctalCharacter()
    {
        Pattern pattern = Pattern.FromRegex(@"(a)\12");

        string code = pattern.ToFluentCode();

        pattern.IsMatch("a\n").ShouldBeTrue();
        code.ShouldNotContain("Backreference");
    }

    public static IEnumerable<object[]> RoundTripCorpus() => new[]
    {
        @"^\d{3}-\d{2}-\d{4}$",
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})",
        @"(cat|dog)s?",
        @"(?:x|y)*z{2,}",
        @"\bword\b",
        @"^(?=.*\d)(?=.*[a-z])(?!.*\s).{8,}$",
        @"(?<=\$)\d+(?:\.\d{2})?",
        @"(?>\d+)\d",
        @"(\w+)\s\1",
        @"(?<w>\w+)\s\k<w>",
        @"(\()?\d+(?(1)\))",
        @"(?i:hello) world",
        @"(?-i:X)y",
        @"[^\]\\\-]+",
        @"[a-z-[aeiou]]",
        @"[\p{L}\d_]+",
        @"\p{Lu}\P{Nd}\p{IsGreek}",
        @"\x41\t\n",
        @"a.+?b",
        @"\d{2,5}?",
        @"[0-9a-fA-F]{6}",
        @"[abc]|[^xyz]",
        @"\A\w+\z",
        @"(?<open>\()[^()]*(?<close-open>\))",
        @"#[a-z]+",
        @"[.]\.",
        @"""quoted""",
        @"\t\d\n\d\r\d",
        @"\B\w+\Z",
        @"(?<!a)b",
        @"(?m:^a)",
        @"(?i-m:a)b",
        @"(?is:a.)",
        @"(?-im:a)",
        @"\D\W\S",
        @"\P{IsGreek}",
        @"[\P{IsGreek}a]",
        @"[ab0-9]",
        @"[a-z_]",
        @"[A-Z_]",
        @"[_\D\W\s\S]",
        @"[\P{L}a]",
        @"[\P{Lu}x]",
        @"[^a\d]",
        @"\p{M}\p{N}\p{P}\p{S}\p{Z}\p{C}",
        @"(?<x>a)?(?(x)b|c)",
        @"(?<x>a)(?(x)b)",
        @"(?(?=a)ab|c)",
        @"(?i)abc",
        @"a(?#note)b",
        @"\d{0,3}",
        @"[a-z]",
        @"[A-Z]",
        @"a\0b\x01",
        @"a\rb",
        @"(|a)",
        @"a||b",
        @"a()b",
    }.Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(RoundTripCorpus))]
    public void GeneratedCode_Compiles_AndMatchesTheSameStrings(string regex)
    {
        Pattern original = Pattern.FromRegex(regex);
        string code = original.ToFluentCode();

        Pattern rebuilt = RoslynCompiler.Evaluate(code);
        var originalRegex = new Regex(regex);
        var rebuiltRegex = rebuilt.ToRegex();

        var samples = original.GenerateMatching(25, seed: 7)
            .Concat(original.GenerateNonMatching(25, seed: 7))
            .ToList();
        samples.ShouldNotBeEmpty();

        foreach (string sample in samples)
        {
            rebuiltRegex.IsMatch(sample).ShouldBe(originalRegex.IsMatch(sample),
                $"Sample '{sample}' disagrees.\nOriginal: {regex}\nRebuilt:  {rebuilt}\nCode:\n{code}");
        }
    }

    [Theory]
    [MemberData(nameof(RoundTripCorpus))]
    public void GeneratedCode_IsAFixpoint(string regex)
    {
        string code = Pattern.FromRegex(regex).ToFluentCode();
        Pattern rebuilt = RoslynCompiler.Evaluate(code);

        rebuilt.ToFluentCode().ShouldBe(code);
    }

    [Fact]
    public void ToFluentCode_ProducesReadableCode()
    {
        string code = Pattern.FromRegex(@"^(?<user>[\w.]+)@(?<host>[\w-]+\.\w{2,})$").ToFluentCode();

        code.ShouldBe(
            "Pattern.With\n" +
            "    .StartOfLine\n" +
            "    .NamedGroup(\"user\", Pattern.With.Set(Pattern.With.Word.Literal(\".\")).Repeat.OneOrMore)\n" +
            "    .Literal(\"@\")\n" +
            "    .NamedGroup(\n" +
            "        \"host\",\n" +
            "        Pattern.With\n" +
            "            .Set(Pattern.With.Word.Literal(\"-\")).Repeat.OneOrMore\n" +
            "            .Literal(\".\")\n" +
            "            .Word.Repeat.AtLeast(2))\n" +
            "    .EndOfLine");
    }

    [Fact]
    public void ToFluentCode_ShortPatternsStayOnOneLine()
    {
        Pattern.FromRegex(@"\d+").ToFluentCode().ShouldBe("Pattern.With.Digit.Repeat.OneOrMore");
    }

    [Fact]
    public void ToFluentCode_UsesFriendlyShorthands()
    {
        Pattern.FromRegex("[a-zA-Z]").ToFluentCode().ShouldBe("Pattern.With.Letter");
        Pattern.FromRegex("[0-9a-fA-F]").ToFluentCode().ShouldBe("Pattern.With.HexDigit");
        Pattern.FromRegex("[a-f]").ToFluentCode().ShouldBe("Pattern.With.Range('a', 'f')");
        Pattern.FromRegex("[aeiou]").ToFluentCode().ShouldBe("Pattern.With.AnyOf(\"aeiou\")");
        Pattern.FromRegex("[^aeiou]").ToFluentCode().ShouldBe("Pattern.With.NoneOf(\"aeiou\")");
        Pattern.FromRegex("(?:a|b)").ToFluentCode().ShouldBe("Pattern.With.OneOf(Pattern.With.Literal(\"a\"), Pattern.With.Literal(\"b\"))");
        Pattern.FromRegex("(a|b)").ToFluentCode().ShouldBe("Pattern.With.Choice(Pattern.With.Literal(\"a\"), Pattern.With.Literal(\"b\"))");
    }

    [Theory]
    [InlineData(@"\t\d", "Pattern.With.Tab.Digit")]
    [InlineData(@"\n\d", "Pattern.With.Newline.Digit")]
    [InlineData(@"\r\d", "Pattern.With.CarriageReturn.Digit")]
    [InlineData(@"\D\W\S", "Pattern.With.NonDigit.NonWord.NonWhitespace")]
    [InlineData(@"\B", "Pattern.With.NonWordBoundary")]
    [InlineData(@"(?<!a)b", "Pattern.With.NegativeLookbehind(Pattern.With.Literal(\"a\")).Literal(\"b\")")]
    [InlineData(@"\d{0,3}", "Pattern.With.Digit.Repeat.AtMost(3)")]
    [InlineData("[a-z]", "Pattern.With.LowercaseLetter")]
    [InlineData("[A-Z]", "Pattern.With.UppercaseLetter")]
    [InlineData(@"\P{M}", "Pattern.With.NotUnicode(UnicodeGroup.Mark)")]
    [InlineData(@"[\p{IsGreek}]", "Pattern.With.Set(Pattern.With.UnicodeBlock(\"Greek\"))")]
    [InlineData("(?:)", "Pattern.With.NonCapturingGroup(Pattern.With)")]
    [InlineData("(?=)", "Pattern.With.PositiveLookahead(Pattern.With)")]
    [InlineData("a()b", "Pattern.With.Literal(\"a\").Group(Pattern.With).Literal(\"b\")")]
    [InlineData("(|a)", "Pattern.With.Choice(Pattern.With, Pattern.With.Literal(\"a\"))")]
    [InlineData("a||b", "Pattern.With.OneOf(Pattern.With.Literal(\"a\"), Pattern.With, Pattern.With.Literal(\"b\"))")]
    [InlineData("(?n:a)", "Pattern.With.InlineOptions(RegexOptions.ExplicitCapture, Pattern.With.Literal(\"a\"))")]
    [InlineData("(?m:^a)", "Pattern.With.InlineOptions(RegexOptions.Multiline, Pattern.With.StartOfLine.Literal(\"a\"))")]
    public void ToFluentCode_ConstructWithAFluentMember_UsesThatMember(string regex, string expected)
    {
        string code = Pattern.FromRegex(regex).ToFluentCode();

        code.ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"[ab0-9]", "Pattern.With.Set(Pattern.With.Literal(\"ab\").Range('0', '9'))")]
    [InlineData(@"[a-z_]", "Pattern.With.Set(Pattern.With.LowercaseLetter.Literal(\"_\"))")]
    [InlineData(@"[A-Z_]", "Pattern.With.Set(Pattern.With.UppercaseLetter.Literal(\"_\"))")]
    [InlineData(@"[_\D\W\s\S]", "Pattern.With.Set(Pattern.With.Literal(\"_\").NonDigit.NonWord.Whitespace.NonWhitespace)")]
    [InlineData(@"[\P{L}a]", "Pattern.With.Set(Pattern.With.NotUnicode(UnicodeGroup.Letter).Literal(\"a\"))")]
    [InlineData(@"[\P{IsGreek}a]", "Pattern.With.Set(Pattern.With.RegEx(@\"\\P{IsGreek}\").Literal(\"a\"))")]
    [InlineData(@"[^a\d]", "Pattern.With.NegatedSet(Pattern.With.Literal(\"a\").Digit)")]
    [InlineData("[']", "Pattern.With.AnyOf(\"'\")")]
    public void ToFluentCode_MixedCharacterClass_BuildsASetMemberByMember(string regex, string expected)
    {
        string code = Pattern.FromRegex(regex).ToFluentCode();

        code.ShouldBe(expected);
    }

    [Fact]
    public void ToFluentCode_UnicodeGroups_UseTheUnicodeGroupEnum()
    {
        string code = Pattern.FromRegex(@"\p{M}\p{N}\p{P}\p{S}\p{Z}\p{C}").ToFluentCode();

        code.ShouldBe(
            "Pattern.With\n" +
            "    .Unicode(UnicodeGroup.Mark)\n" +
            "    .Unicode(UnicodeGroup.Number)\n" +
            "    .Unicode(UnicodeGroup.Punctuation)\n" +
            "    .Unicode(UnicodeGroup.Symbol)\n" +
            "    .Unicode(UnicodeGroup.Separator)\n" +
            "    .Unicode(UnicodeGroup.Other)");
    }

    [Fact]
    public void ToFluentCode_NegatedCategoryInSet_UsesTheUnicodeCategoryEnum()
    {
        string code = Pattern.FromRegex(@"[\P{Lu}x]").ToFluentCode();

        code.ShouldBe(
            "Pattern.With\n" +
            "    .Set(\n" +
            "        Pattern.With\n" +
            "            .NotUnicode(System.Globalization.UnicodeCategory.UppercaseLetter)\n" +
            "            .Literal(\"x\"))");
    }

    [Theory]
    [InlineData(@"\P{IsGreek}", "Pattern.With.RegEx(@\"\\P{IsGreek}\")")]
    [InlineData(@"\Z\G", "Pattern.With.RegEx(@\"\\Z\").RegEx(@\"\\G\")")]
    [InlineData("(?i)abc", "Pattern.With.RegEx(@\"(?i)\").Literal(\"abc\")")]
    [InlineData("a(?#note)b", "Pattern.With.Literal(\"a\").RegEx(@\"(?#note)\").Literal(\"b\")")]
    [InlineData("(?(?=a)ab|c)", "Pattern.With.RegEx(@\"(?(?=a)ab|c)\")")]
    public void ToFluentCode_ConstructWithoutAFluentMember_FallsBackToRegEx(string regex, string expected)
    {
        string code = Pattern.FromRegex(regex).ToFluentCode();

        code.ShouldBe(expected);
    }

    [Fact]
    public void ToFluentCode_RawFallbackWithControlCharacter_UsesAnEscapedRegularString()
    {
        Pattern pattern = Pattern.FromRegex("(?<o>a)(?<c-o>\u0001)");

        string code = pattern.ToFluentCode();
        Pattern rebuilt = RoslynCompiler.Evaluate(code);

        code.ShouldBe("Pattern.With.NamedGroup(\"o\", Pattern.With.Literal(\"a\")).RegEx(\"(?<c-o>\\u0001)\")");
        rebuilt.ToString().ShouldBe(pattern.ToString());
    }

    [Theory]
    [InlineData("a\rb", "Pattern.With.Literal(\"a\\rb\")")]
    [InlineData("a\0b", "Pattern.With.Literal(\"a\\0b\")")]
    [InlineData("\u0001a", "Pattern.With.Literal(\"\\u0001a\")")]
    [InlineData("\uD83D\uDE00", "Pattern.With.Literal(\"\\uD83D\\uDE00\")")]
    [InlineData("a\"b'", "Pattern.With.Literal(\"a\\\"b'\")")]
    public void ToFluentCode_SpecialCharactersInLiterals_AreEscapedForCSharp(string regex, string expected)
    {
        string code = Pattern.FromRegex(regex).ToFluentCode();

        code.ShouldBe(expected);
    }

    [Theory]
    [InlineData("(?<x>a)?(?(x)b|c)",
        "Pattern.With\n" +
        "    .NamedGroup(\"x\", Pattern.With.Literal(\"a\")).Repeat.Optional\n" +
        "    .Conditional(\"x\", Pattern.With.Literal(\"b\"), Pattern.With.Literal(\"c\"))")]
    [InlineData("(?<x>a)(?(x)b)",
        "Pattern.With\n" +
        "    .NamedGroup(\"x\", Pattern.With.Literal(\"a\"))\n" +
        "    .Conditional(\"x\", Pattern.With.Literal(\"b\"), Pattern.With)")]
    public void ToFluentCode_ConditionalOnNamedGroup_UsesTheGroupName(string regex, string expected)
    {
        string code = Pattern.FromRegex(regex).ToFluentCode();

        code.ShouldBe(expected);
    }

    [Theory]
    [InlineData("(?i-m:a)", "Pattern.With\n    .InlineOptions(RegexOptions.IgnoreCase, Pattern.With.Literal(\"a\"), RegexOptions.Multiline)")]
    [InlineData("(?is:a)", "Pattern.With\n    .InlineOptions(RegexOptions.IgnoreCase | RegexOptions.Singleline, Pattern.With.Literal(\"a\"))")]
    [InlineData("(?-m:a)", "Pattern.With\n    .InlineOptions(RegexOptions.None, Pattern.With.Literal(\"a\"), RegexOptions.Multiline)")]
    [InlineData("(?-im:a)",
        "Pattern.With\n" +
        "    .InlineOptions(\n" +
        "        RegexOptions.None,\n" +
        "        Pattern.With.Literal(\"a\"),\n" +
        "        RegexOptions.IgnoreCase | RegexOptions.Multiline)")]
    public void ToFluentCode_ScopedOptionsOtherThanCase_UseInlineOptions(string regex, string expected)
    {
        string code = Pattern.FromRegex(regex).ToFluentCode();

        code.ShouldBe(expected);
    }

    [Theory]
    [InlineData(RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant,
        ".WithOptions(RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)")]
    [InlineData(RegexOptions.RightToLeft, ".WithOptions(RegexOptions.RightToLeft)")]
    [InlineData(RegexOptions.ECMAScript, ".WithOptions(RegexOptions.ECMAScript)")]
    [InlineData(RegexOptions.NonBacktracking, ".WithOptions(RegexOptions.NonBacktracking)")]
    public void ToFluentCode_EveryRegexOption_IsNamed(RegexOptions options, string expected)
    {
        string code = Pattern.FromRegex("a", options).ToFluentCode();

        code.ShouldEndWith(expected);
    }

    [Fact]
    public void ToFluentCode_CustomIndentSize_IndentsNestedLinesByThatSize()
    {
        string code = Pattern.FromRegex(@"^(?<user>[\w.]+)@(?<host>[\w-]+\.\w{2,})$").ToFluentCode(indentSize: 2);

        code.ShouldBe(
            "Pattern.With\n" +
            "  .StartOfLine\n" +
            "  .NamedGroup(\"user\", Pattern.With.Set(Pattern.With.Word.Literal(\".\")).Repeat.OneOrMore)\n" +
            "  .Literal(\"@\")\n" +
            "  .NamedGroup(\n" +
            "    \"host\",\n" +
            "    Pattern.With\n" +
            "      .Set(Pattern.With.Word.Literal(\"-\")).Repeat.OneOrMore\n" +
            "      .Literal(\".\")\n" +
            "      .Word.Repeat.AtLeast(2))\n" +
            "  .EndOfLine");
    }

    [Fact]
    public void ToFluentCode_NegativeIndentSize_ThrowsArgumentOutOfRangeException()
    {
        Pattern pattern = Pattern.FromRegex("a");

        ArgumentOutOfRangeException exception = Should.Throw<ArgumentOutOfRangeException>(() => pattern.ToFluentCode(-1));

        exception.ParamName.ShouldBe("indentSize");
    }

    [Fact]
    public void FromRegex_Null_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(() => Pattern.FromRegex(null!));

        exception.ParamName.ShouldBe("regex");
    }

    [Fact]
    public void FromRegex_InvalidRegex_NamesTheParameterAndKeepsTheCause()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(() => Pattern.FromRegex("(abc"));

        exception.ParamName.ShouldBe("regex");
        exception.Message.ShouldStartWith("The regular expression is invalid: ");
        exception.InnerException.ShouldBeAssignableTo<ArgumentException>();
    }

    [Fact]
    public void FromRegex_KeepsTheOptions()
    {
        Pattern pattern = Pattern.FromRegex("a", RegexOptions.IgnoreCase);

        pattern.Options.ShouldBe(RegexOptions.IgnoreCase);
        pattern.IsMatch("A").ShouldBeTrue();
    }

    [Fact]
    public void FromRegex_OptionsTheEngineRejects_ThrowArgumentException()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => Pattern.FromRegex("a", RegexOptions.ECMAScript | RegexOptions.Singleline));

        exception.ParamName.ShouldBe("regex");
        exception.Message.ShouldStartWith("The regular expression is invalid: ");
        exception.InnerException.ShouldBeOfType<ArgumentOutOfRangeException>().ParamName.ShouldBe("options");
    }

    [Fact]
    public void ToFluentCode_IncludesOptionsAndTimeout()
    {
        string code = Pattern.FromRegex("abc", RegexOptions.IgnoreCase | RegexOptions.Multiline)
            .WithTimeout(TimeSpan.FromMilliseconds(250))
            .ToFluentCode();

        code.ShouldContain(".WithOptions(RegexOptions.IgnoreCase | RegexOptions.Multiline)");
        code.ShouldContain(".WithTimeout(TimeSpan.FromMilliseconds(250))");
    }

    [Fact]
    public void ToFluentCode_HandlesIgnorePatternWhitespace()
    {
        Pattern pattern = Pattern.FromRegex("\\d+  # digits\n - \\w", RegexOptions.IgnorePatternWhitespace);
        string code = pattern.ToFluentCode();

        Pattern rebuilt = RoslynCompiler.Evaluate(code);
        rebuilt.IsMatch("12-a").ShouldBeTrue();
        rebuilt.IsMatch("12 - a").ShouldBeFalse();
    }

    [Fact]
    public void FromRegex_RejectsInvalidRegex()
    {
        var ex = Should.Throw<ArgumentException>(() => Pattern.FromRegex("(abc"));
        ex.Message.ShouldContain("invalid");
    }

    [Fact]
    public void FromRegex_KeepsTextExactly()
    {
        Pattern.FromRegex(@"a{,3}").ToString().ShouldBe(@"a{,3}");
    }

    [Fact]
    public void FluentPatterns_CanBeConvertedToo()
    {
        Pattern pattern = CommonPatterns.IPv4();
        Pattern rebuilt = RoslynCompiler.Evaluate(pattern.ToFluentCode());
        rebuilt.ToString().ShouldBe(pattern.ToString());
    }
}
