using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class ExportTests
{
    private const string Word = @"[\p{L}\p{Mn}\p{Nd}\p{Pc}]";
    private const string Space = @"[\t\n\v\f\r\x85\p{Z}]";

    // ------------------------------------------------------------------
    // Construct-by-construct translation
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(@"(?'year'\d{4})", @"(?<year>\p{Nd}{4})")]
    [InlineData(@"\Aabc\z", "^abc$")]
    [InlineData(@"abc\Z", @"abc(?=\n?$)")]
    [InlineData("^abc$", @"^abc(?=\n?$)")]
    [InlineData(@"\d\D", @"\p{Nd}\P{Nd}")]
    [InlineData(@"\w+", Word + "+")]
    [InlineData(@"\W", @"[^\p{L}\p{Mn}\p{Nd}\p{Pc}]")]
    [InlineData(@"\s", Space)]
    [InlineData(@"\S", @"[^\t\n\v\f\r\x85\p{Z}]")]
    [InlineData(@"[\w-]", @"[\p{L}\p{Mn}\p{Nd}\p{Pc}\-]")]
    [InlineData(@"[^\d\s]", @"[^\p{Nd}\t\n\v\f\r\x85\p{Z}]")]
    [InlineData(@"[^\W]", Word)]
    [InlineData(@"[^\W\d]", @"(?:(?![\p{Nd}])[\p{L}\p{Mn}\p{Nd}\p{Pc}])")]
    [InlineData(@"[\W\d]", @"(?:[\p{Nd}]|[^\p{L}\p{Mn}\p{Nd}\p{Pc}])")]
    [InlineData(@"[\s\S]", @"[\s\S]")]
    [InlineData("a.b", @"a[^\n]b")]
    [InlineData("a(?s:.)b.", @"a(?:[\s\S])b[^\n]")]
    [InlineData("(?#note)ab", "ab")]
    [InlineData("a/b[/]", @"a\/b[\/]")]
    [InlineData(@"a\-b\#c", "a-b#c")]
    [InlineData(@"[a-z-[aeiou]]", "[b-df-hj-np-tv-z]")]
    [InlineData(@"\p{IsBasicLatin}", @"[\x00-\x7F]")]
    [InlineData(@"\p{Lu}\P{Ll}", @"\p{Lu}\P{Ll}")]
    [InlineData(@"(a)(?<n>b)(c)\1\2\3", @"(a)(?<n>b)(c)\1\3\k<n>")]
    [InlineData(@"(a)\1\x30", @"(a)\1(?:)0")]
    [InlineData(@"x{2,}y{3}z{1,2}?w*?", "x{2,}y{3}z{1,2}?w*?")]
    [InlineData("(?=a)*b", "(?:(?=a))*b")]
    [InlineData("(?<=a)b(?<!c)(?!d)", "(?<=a)b(?<!c)(?!d)")]
    [InlineData("a{,3}", @"a\{,3\}")]
    [InlineData(@"\t\x00", @"\t\x00")]
    [InlineData("ab(?i:cd)ef", "ab(?:[Cc][Dd])ef")]
    [InlineData("(?i)a(?-i:b)c", "[Aa](?:b)[Cc]")]
    [InlineData("(?n)(a)(?<x>b)", "(?:a)(?<x>b)")]
    [InlineData("a|b|", "a|b|")]
    [InlineData(@"[ab-[b]]", "a")]
    [InlineData(@"[a-a-[a]]", "[]")]
    [InlineData(@"[\s\S-[a-[a]]]", @"[\s\S]")]
    [InlineData("[\\x00-\\uFFFF-[a]]", "[^a]")]
    [InlineData("[\\x00-\\uFFFF-[\\uE000]]", "[^\\uE000]")]
    [InlineData("\\v\\f\\xA0\\u0100", "\\v\\f\\xA0\\u0100")]
    [InlineData(@"[^\W\S]", @"(?:(?=[\p{L}\p{Mn}\p{Nd}\p{Pc}])[\t\n\v\f\r\x85\p{Z}])")]
    [InlineData("[a\\p{IsGreek}]", "[a\\u0370-\\u03FF]")]
    [InlineData(@"[a\p{Lu}\P{Ll}]", @"[a\p{Lu}\P{Ll}]")]
    [InlineData(@"[a\D]", @"[a\P{Nd}]")]
    [InlineData(@"[\W]", @"[^\p{L}\p{Mn}\p{Nd}\p{Pc}]")]
    [InlineData(@"\B", @"(?:(?<=[\p{L}\p{Mn}\p{Nd}\p{Pc}\u{200C}\u{200D}])(?=[\p{L}\p{Mn}\p{Nd}\p{Pc}\u{200C}\u{200D}])|(?<![\p{L}\p{Mn}\p{Nd}\p{Pc}\u{200C}\u{200D}])(?![\p{L}\p{Mn}\p{Nd}\p{Pc}\u{200C}\u{200D}]))")]
    [InlineData("^*a", "(?:^)*a")]
    [InlineData("(?:)*a", "(?:)*a")]
    [InlineData("(?i:[a-c])x", "(?:[A-Ca-c])x")]
    [InlineData("\uD83D\uDE00", "\\uD83D\\uDE00")]
    public void ToFlavor_JavaScript_TranslatesEachConstruct(string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.JavaScript);

        conversion.Pattern.ShouldBe(expected);
    }

    [Fact]
    public void ToJavaScript_Email_ProducesAnExactUnicodeLiteral()
    {
        string literal = CommonPatterns.Email().ToJavaScript();

        literal.ShouldBe(@"/^[a-zA-Z\p{Nd}._\-]+@[a-zA-Z\p{Nd}.\-]+\.[a-zA-Z]{2,}(?=\n?$)/u");
    }

    [Fact]
    public void ToFlavor_WordBoundary_UsesUnicodeWordCharactersIncludingJoiners()
    {
        FlavorConversion conversion = Pattern.FromRegex(@"\bcat").ToFlavor(RegexFlavor.JavaScript);

        conversion.Pattern.ShouldStartWith(@"(?:(?<=[\p{L}\p{Mn}\p{Nd}\p{Pc}\u{200C}\u{200D}])(?![");
        conversion.Pattern.ShouldEndWith("cat");
        conversion.IsExact.ShouldBeTrue();
    }

    [Theory]
    [InlineData(RegexOptions.IgnoreCase, "^abc", "^abc", "iu")]
    [InlineData(RegexOptions.Singleline, "a.c", "a.c", "su")]
    [InlineData(RegexOptions.Multiline, "^ab$", @"(?<=^|\n)ab(?=\n|$)", "u")]
    [InlineData(RegexOptions.IgnorePatternWhitespace, "a b # comment\n c", "abc", "u")]
    [InlineData(RegexOptions.ExplicitCapture, "(a)", "(?:a)", "u")]
    [InlineData(RegexOptions.ECMAScript, @"\d\w\s", @"\d\w[ \f\n\r\t\v]", "u")]
    [InlineData(RegexOptions.ECMAScript, @"\S\D\W", @"[^ \f\n\r\t\v]\D\W", "u")]
    [InlineData(RegexOptions.ECMAScript, @"[\d\w\s]", @"[\d\w \f\n\r\t\v]", "u")]
    [InlineData(RegexOptions.ECMAScript, @"[a\S]", @"(?:[a]|[^ \f\n\r\t\v])", "u")]
    [InlineData(RegexOptions.ECMAScript, @"[a\W]", @"[a\W]", "u")]
    [InlineData(RegexOptions.ECMAScript, @"\b\B", @"\b\B", "u")]
    public void ToFlavor_JavaScript_TranslatesOptions(RegexOptions options, string regex, string expectedPattern, string expectedFlags)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).WithOptions(options).ToFlavor(RegexFlavor.JavaScript);

        conversion.Pattern.ShouldBe(expectedPattern);
        conversion.Flags.ShouldBe(expectedFlags);
    }

    [Fact]
    public void ToFlavor_LeadingInlineIgnoreCase_BecomesTheFlag()
    {
        FlavorConversion conversion = Pattern.FromRegex("(?i)hello").ToFlavor(RegexFlavor.JavaScript);

        conversion.ToLiteral().ShouldBe("/hello/iu");
        conversion.Warnings.ShouldHaveSingleItem().ShouldContain("case folding");
    }

    [Fact]
    public void ToFlavor_PreserveLineSemanticsOff_UsesPlainAnchorsAndTheMultilineFlag()
    {
        FlavorOptions options = new FlavorOptions { PreserveLineSemantics = false };

        FlavorConversion multiline = Pattern.FromRegex(@"(?m)^\Aab$").ToFlavor(RegexFlavor.JavaScript, options);
        FlavorConversion single = Pattern.FromRegex("^a.b$").ToFlavor(RegexFlavor.JavaScript, options);

        multiline.ToLiteral().ShouldBe(@"/^(?<![\s\S])ab$/mu");
        multiline.Warnings.ShouldHaveSingleItem().ShouldContain("'m' flag");
        single.ToLiteral().ShouldBe("/^a.b$/u");
        single.Warnings.Count.ShouldBe(3);
    }

    [Fact]
    public void ToFlavor_PreserveUnicodeSemanticsOff_KeepsAsciiShorthandsWithWarnings()
    {
        FlavorOptions options = new FlavorOptions { PreserveUnicodeSemantics = false };

        FlavorConversion conversion = Pattern.FromRegex(@"\b\d+[\w\s]\b").ToFlavor(RegexFlavor.JavaScript, options);

        conversion.Pattern.ShouldBe(@"\b\d+[\w\s]\b");
        conversion.IsExact.ShouldBeFalse();
        conversion.Warnings.Count.ShouldBe(3);
        conversion.Warnings.ShouldContain(w => w.Contains("ASCII digits"));
    }

    [Fact]
    public void ToFlavor_PreserveUnicodeSemanticsOff_NegatedShorthandsWarnOncePerKind()
    {
        FlavorOptions options = new FlavorOptions { PreserveUnicodeSemantics = false };

        FlavorConversion conversion = Pattern.FromRegex(@"\D\W\S[\D\W\S]").ToFlavor(RegexFlavor.JavaScript, options);

        conversion.Pattern.ShouldBe(@"\D\W\S[\D\W\S]");
        conversion.Warnings.Count.ShouldBe(4);
        conversion.Warnings.ShouldContain(w => w.Contains("ASCII digits"));
        conversion.Warnings.ShouldContain(w => w.Contains("ASCII word characters"));
        conversion.Warnings.ShouldContain(w => w.Contains("U+FEFF"));
        conversion.Warnings.ShouldContain(w => w.StartsWith("Supplementary characters"));
    }

    [Fact]
    public void ToFlavor_PreserveUnicodeSemanticsOff_NonWordBoundaryIsAsciiWithWarning()
    {
        FlavorOptions options = new FlavorOptions { PreserveUnicodeSemantics = false };

        FlavorConversion conversion = Pattern.FromRegex(@"\B").ToFlavor(RegexFlavor.JavaScript, options);

        conversion.Pattern.ShouldBe(@"\B");
        conversion.Warnings.ShouldHaveSingleItem().ShouldContain("ASCII word characters");
    }

    [Fact]
    public void ToFlavor_PreserveLineSemanticsOffWithMultilineFlag_WritesEndOfInputAsLookahead()
    {
        FlavorOptions options = new FlavorOptions { PreserveLineSemantics = false };

        FlavorConversion conversion = Pattern.FromRegex(@"(?m)^a\z").ToFlavor(RegexFlavor.JavaScript, options);

        conversion.ToLiteral().ShouldBe(@"/^a(?![\s\S])/mu");
        conversion.Warnings.ShouldHaveSingleItem().ShouldContain("'m' flag");
    }

    [Fact]
    public void ToFlavor_IgnoreCaseFlagWithUnicodeBlock_WritesTheBlockCaseSensitively()
    {
        FlavorConversion conversion = Pattern.FromRegex(@"(?i)a\p{IsBasicLatin}").ToFlavor(RegexFlavor.JavaScript);

        conversion.ToLiteral().ShouldBe(@"/a[\x00-\x7F]/iu");
    }

    [Fact]
    public void FlavorOptions_DefaultToExactTranslation()
    {
        FlavorOptions options = new FlavorOptions();

        options.PreserveUnicodeSemantics.ShouldBeTrue();
        options.PreserveLineSemantics.ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    // Warnings
    // ------------------------------------------------------------------

    [Fact]
    public void ToFlavor_BackreferenceToOptionalGroup_WarnsAboutEmptyMatch()
    {
        FlavorConversion conversion = Pattern.FromRegex(@"(?:(a)|b)\1").ToFlavor(RegexFlavor.JavaScript);

        conversion.IsExact.ShouldBeFalse();
        conversion.Warnings.ShouldHaveSingleItem().ShouldContain("position 9");
    }

    [Theory]
    [InlineData(@"\1(a)", 0)]
    [InlineData(@"\k<x>(?<x>a)", 0)]
    [InlineData(@"(a\1)", 2)]
    [InlineData(@"(a)?\1", 4)]
    [InlineData(@"(a)*\1", 4)]
    [InlineData(@"(a)|\1", 4)]
    [InlineData(@"(?!(a))\1", 7)]
    [InlineData(@"(?<!(a))b\1", 9)]
    public void ToFlavor_BackreferenceToGroupThatMayNotHaveParticipated_Warns(string regex, int position)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.JavaScript);

        conversion.Warnings.ShouldHaveSingleItem().ShouldStartWith($"The backreference at position {position} refers to a group that may not have participated");
    }

    [Theory]
    [InlineData(@"(a){2}\1", @"(a){2}\1")]
    [InlineData(@"(?:(a))\1", @"(?:(a))\1")]
    [InlineData(@"((a))\2", @"((a))\2")]
    [InlineData(@"(?:(a)b)\1", @"(?:(a)b)\1")]
    [InlineData(@"(?:a)(?<n>b)\k<n>", @"(?:a)(?<n>b)\k<n>")]
    [InlineData(@"(?=(a))\1", @"(?=(a))\1")]
    public void ToFlavor_BackreferenceToGroupThatAlwaysParticipates_IsExact(string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.JavaScript);

        conversion.Pattern.ShouldBe(expected);
        conversion.IsExact.ShouldBeTrue();
    }

    [Fact]
    public void ToFlavor_BackreferenceToOptionalGroup_JsonSchemaWarnsToo()
    {
        FlavorConversion conversion = Pattern.FromRegex(@"(a)?\1").ToFlavor(RegexFlavor.JsonSchema);

        conversion.ToLiteral().ShouldBe(@"""(a)?\\1""");
        conversion.Warnings.ShouldHaveSingleItem().ShouldContain("position 4");
    }

    [Fact]
    public void ToFlavor_BackreferenceToMandatoryGroup_IsExact()
    {
        FlavorConversion conversion = CommonPatterns.MacAddress().ToFlavor(RegexFlavor.JavaScript);

        conversion.IsExact.ShouldBeTrue();
        conversion.Pattern.ShouldContain(@"\k<sep>");
    }

    [Fact]
    public void ToFlavor_NamedGroupBeforeUnnamedGroup_WarnsThatNumbersDiffer()
    {
        FlavorConversion conversion = Pattern.FromRegex("(?<a>x)(y)").ToFlavor(RegexFlavor.JavaScript);

        conversion.Warnings.ShouldHaveSingleItem().ShouldContain("Capture group numbers differ");
    }

    [Theory]
    [InlineData("^.{1,3}$", "position 1")]
    [InlineData(@"\S+@\S+", "positions 0, 4")]
    [InlineData(@"\P{IsBasicLatin}", "position 0")]
    [InlineData("\uD83D\uDE00", "positions 0, 1 match")]
    [InlineData(".........", "positions 0, 1, 2, 3, 4, 5, 6, 7, ... match")]
    public void ToFlavor_ConstructThatCanMatchHalfASurrogatePair_WarnsAboutSupplementaryCharacters(string regex, string positions)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.JavaScript);

        conversion.IsExact.ShouldBeFalse();
        conversion.Warnings.ShouldHaveSingleItem().ShouldContain(positions);
    }

    // ------------------------------------------------------------------
    // JSON Schema
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(RegexOptions.IgnoreCase, "^abc1", @"^[Aa][Bb][Cc]1")]
    [InlineData(RegexOptions.Singleline, "a.c", @"a[\s\S]c")]
    [InlineData(RegexOptions.Multiline, "^a$", @"(?<=^|\n)a(?=\n|$)")]
    [InlineData(RegexOptions.None, "a/b", "a/b")]
    public void ToFlavor_JsonSchema_ExpressesOptionsWithoutFlags(RegexOptions options, string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).WithOptions(options).ToFlavor(RegexFlavor.JsonSchema);

        conversion.Pattern.ShouldBe(expected);
        conversion.Flags.ShouldBeEmpty();
        conversion.Flavor.ShouldBe(RegexFlavor.JsonSchema);
    }

    [Fact]
    public void ToLiteral_JsonSchema_IsAQuotedJsonString()
    {
        FlavorConversion conversion = Pattern.FromRegex(@"^\d+""$").ToFlavor(RegexFlavor.JsonSchema);

        string literal = conversion.ToLiteral();

        literal.ShouldBe(@"""^\\p{Nd}+\""(?=\\n?$)""");
        conversion.ToString().ShouldBe(literal);
    }

    [Fact]
    public void ToLiteral_JsonSchemaPatternWithControlCharacters_EscapesThemAsUnicode()
    {
        FlavorConversion conversion = new FlavorConversion(
            RegexFlavor.JsonSchema, "a\u0001\u001F ", string.Empty, Array.Empty<string>(), new Dictionary<string, int>());

        string literal = conversion.ToLiteral();

        literal.ShouldBe("\"a\\u0001\\u001f \"");
    }

    [Fact]
    public void ToLiteral_EmptyJavaScriptPattern_UsesAnEmptyGroup()
    {
        string literal = Pattern.With.ToJavaScript();

        literal.ShouldBe("/(?:)/u");
    }

    // ------------------------------------------------------------------
    // Unsupported constructs
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("(?>a+)b", "atomic group", 0)]
    [InlineData("(?<x>a)(?(x)b|c)", "conditional", 7)]
    [InlineData("(?<o>a)(?<c-o>b)", "balancing group", 7)]
    [InlineData(@"a\Gb", @"'\G'", 1)]
    [InlineData("(?<x>a)(?<x>b)", "duplicate group name 'x'", 7)]
    [InlineData("(?<5>a)", "group name '5'", 0)]
    public void ToFlavor_UnsupportedConstruct_ThrowsNotSupportedExceptionWithPosition(string regex, string construct, int position)
    {
        Pattern pattern = Pattern.FromRegex(regex);

        NotSupportedException exception = Should.Throw<NotSupportedException>(() => pattern.ToFlavor(RegexFlavor.JavaScript));

        exception.Message.ShouldContain(construct);
        exception.Message.ShouldContain($"at position {position}");
    }

    [Fact]
    public void ToFlavor_SeveralUnsupportedConstructs_ListsAllOfThem()
    {
        Pattern pattern = Pattern.FromRegex(@"(a)(?>a)\G(?(1)b|c)");

        NotSupportedException exception = Should.Throw<NotSupportedException>(() => pattern.ToFlavor(RegexFlavor.JavaScript));

        exception.Message.ShouldStartWith("The pattern cannot be converted to JavaScript");
        exception.Message.ShouldContain("atomic group '(?>...)' at position 3");
        exception.Message.ShouldContain(@"'\G' (contiguous-match anchor) at position 8");
        exception.Message.ShouldContain("conditional '(?(1)...)' at position 10");
    }

    [Fact]
    public void ToFlavor_RightToLeft_IsNotSupported()
    {
        Pattern pattern = Pattern.FromRegex("abc").WithOptions(RegexOptions.RightToLeft);

        NotSupportedException exception = Should.Throw<NotSupportedException>(() => pattern.ToFlavor(RegexFlavor.JsonSchema));

        exception.Message.ShouldContain("RegexOptions.RightToLeft");
        exception.Message.ShouldContain("JSON Schema");
    }

    [Fact]
    public void ToFlavor_CaseInsensitiveBackreference_IsSupportedWithTheFlagButNotInJsonSchema()
    {
        Pattern pattern = Pattern.FromRegex(@"(?i)(a)\1");

        FlavorConversion javaScript = pattern.ToFlavor(RegexFlavor.JavaScript);
        NotSupportedException exception = Should.Throw<NotSupportedException>(() => pattern.ToFlavor(RegexFlavor.JsonSchema));

        javaScript.ToLiteral().ShouldBe(@"/(a)\1/iu");
        exception.Message.ShouldContain("case-insensitive backreference");
    }

    [Fact]
    public void ToFlavor_CaseInsensitiveBackreferenceInMixedCasePattern_IsNotSupportedInJavaScript()
    {
        Pattern pattern = Pattern.FromRegex(@"(a)(?i:\1)b");

        NotSupportedException exception = Should.Throw<NotSupportedException>(() => pattern.ToFlavor(RegexFlavor.JavaScript));

        exception.Message.ShouldContain("case-insensitive backreference (cannot be expressed in a pattern that mixes case sensitivity) at position 7");
    }

    [Fact]
    public void ToFlavor_GroupInsideConditional_ReportsOnlyTheConditional()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<x>z)(?(x)(a)|b)\1");

        NotSupportedException exception = Should.Throw<NotSupportedException>(() => pattern.ToFlavor(RegexFlavor.JavaScript));

        exception.Message.ShouldBe(
            "The pattern cannot be converted to JavaScript without changing its meaning:\n" +
            "  - conditional '(?(x)...)' at position 7");
    }

    [Theory]
    [InlineData(RegexFlavor.JavaScript)]
    [InlineData(RegexFlavor.JsonSchema)]
    public void ToFlavor_BackreferenceToGroupZero_IsNotSupported(RegexFlavor flavor)
    {
        Pattern pattern = Pattern.FromRegex(@"(a)\k<0>");

        NotSupportedException exception = Should.Throw<NotSupportedException>(() => pattern.ToFlavor(flavor));

        exception.Message.ShouldEndWith(":\n  - backreference to an undefined group at position 3");
    }

    [Fact]
    public void ToFlavor_UndefinedFlavor_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Pattern.With.Digit.ToFlavor((RegexFlavor)42)).ParamName.ShouldBe("flavor");
    }

    // ------------------------------------------------------------------
    // CommonPatterns
    // ------------------------------------------------------------------

    private static readonly Dictionary<string, Func<Pattern>> CommonPatternFactories = new Dictionary<string, Func<Pattern>>
    {
        ["Email"] = () => CommonPatterns.Email(),
        ["Url"] = () => CommonPatterns.Url(),
        ["PhoneNumber"] = () => CommonPatterns.PhoneNumber(),
        ["IPv4"] = () => CommonPatterns.IPv4(),
        ["IPv6"] = () => CommonPatterns.IPv6(),
        ["Guid"] = () => CommonPatterns.Guid(),
        ["HexColor"] = () => CommonPatterns.HexColor(),
        ["DateIso"] = () => CommonPatterns.DateIso(),
        ["Date"] = () => CommonPatterns.Date(),
        ["Time24"] = () => CommonPatterns.Time24(),
        ["Time12"] = () => CommonPatterns.Time12(),
        ["CreditCard"] = () => CommonPatterns.CreditCard(),
        ["SocialSecurityNumber"] = () => CommonPatterns.SocialSecurityNumber(),
        ["ZipCode"] = () => CommonPatterns.ZipCode(),
        ["Username"] = () => CommonPatterns.Username(),
        ["StrongPassword"] = () => CommonPatterns.StrongPassword(),
        ["FilePath"] = () => CommonPatterns.FilePath(),
        ["HtmlTag"] = () => CommonPatterns.HtmlTag(),
        ["Integer"] = () => CommonPatterns.Integer(),
        ["Decimal"] = () => CommonPatterns.Decimal(),
        ["ScientificNotation"] = () => CommonPatterns.ScientificNotation(),
        ["Email.Loose"] = () => CommonPatterns.Email(PatternStrictness.Loose),
        ["Email.Strict"] = () => CommonPatterns.Email(PatternStrictness.Strict),
        ["Url.Loose"] = () => CommonPatterns.Url(PatternStrictness.Loose),
        ["Url.Strict"] = () => CommonPatterns.Url(PatternStrictness.Strict),
        ["PhoneNumberE164"] = () => CommonPatterns.PhoneNumberE164(),
        ["Iban"] = () => CommonPatterns.Iban(),
        ["SemVer"] = () => CommonPatterns.SemVer(),
        ["Jwt"] = () => CommonPatterns.Jwt(),
        ["MacAddress"] = () => CommonPatterns.MacAddress(),
        ["Base64"] = () => CommonPatterns.Base64(),
        ["Slug"] = () => CommonPatterns.Slug(),
        ["DateTimeIso"] = () => CommonPatterns.DateTimeIso(),
        ["MarkdownLink"] = () => CommonPatterns.MarkdownLink(),
    };

    public static TheoryData<string, bool> CommonPatternCases() => new TheoryData<string, bool>
    {
        { "Email", true },
        { "Url", true },
        { "PhoneNumber", true },
        { "IPv4", true },
        { "IPv6", true },
        { "Guid", true },
        { "HexColor", true },
        { "DateIso", true },
        { "Date", true },
        { "Time24", true },
        { "Time12", true },
        { "CreditCard", true },
        { "SocialSecurityNumber", true },
        { "ZipCode", true },
        { "Username", true },
        { "StrongPassword", false },
        { "FilePath", true },
        { "HtmlTag", false },
        { "Integer", true },
        { "Decimal", true },
        { "ScientificNotation", true },
        { "Email.Loose", false },
        { "Email.Strict", false },
        { "Url.Loose", false },
        { "Url.Strict", true },
        { "PhoneNumberE164", true },
        { "Iban", true },
        { "SemVer", true },
        { "Jwt", true },
        { "MacAddress", true },
        { "Base64", false },
        { "Slug", true },
        { "DateTimeIso", true },
        { "MarkdownLink", false },
    };

    [Theory]
    [MemberData(nameof(CommonPatternCases))]
    public void ToFlavor_EveryCommonPattern_ConvertsToJavaScript(string name, bool exact)
    {
        Pattern pattern = CommonPatternFactories[name]();

        FlavorConversion conversion = pattern.ToFlavor(RegexFlavor.JavaScript);

        conversion.IsExact.ShouldBe(exact, name);
        conversion.Flags.ShouldBe("u", name);
    }

    [Theory]
    [MemberData(nameof(CommonPatternCases))]
    public void ToFlavor_EveryCommonPattern_ConvertsToJsonSchema(string name, bool exact)
    {
        Pattern pattern = CommonPatternFactories[name]();

        FlavorConversion conversion = pattern.ToFlavor(RegexFlavor.JsonSchema);

        conversion.IsExact.ShouldBe(exact, name);
        conversion.Flags.ShouldBeEmpty(name);
    }

    [Fact]
    public void ToFlavor_InexactCommonPatterns_OnlyDifferForSupplementaryCharacters()
    {
        IEnumerable<string> warnings = CommonPatternFactories.Values
            .SelectMany(factory => factory().ToFlavor(RegexFlavor.JavaScript).Warnings);

        CommonPatternFactories.Count.ShouldBe(34);
        warnings.ShouldAllBe(warning => warning.StartsWith("Supplementary characters"));
    }

    // ------------------------------------------------------------------
    // TypeScript
    // ------------------------------------------------------------------

    [Fact]
    public void ToTypeScript_WritesOneDocumentedConstantPerPatternInNameOrder()
    {
        Dictionary<string, Pattern> patterns = new Dictionary<string, Pattern>
        {
            ["zip"] = CommonPatterns.ZipCode(),
            ["anyChar"] = Pattern.FromRegex("."),
        };

        string module = PatternExport.ToTypeScript(patterns);

        module.ShouldBe(
            "// Generated by Masterly.FluentRegex. Do not edit by hand.\n" +
            "\n" +
            "/**\n" +
            " * .NET: .\n" +
            " * Warning: Supplementary characters such as emoji: the construct(s) at position 0 match one UTF-16 code unit in .NET but a whole code point in JavaScript with the 'u' flag.\n" +
            " */\n" +
            @"export const anyChar = /[^\n]/u;" + "\n" +
            "/**\n" +
            @" * .NET: ^\d{5}(?:-\d{4})?$" + "\n" +
            " */\n" +
            @"export const zip = /^\p{Nd}{5}(?:-\p{Nd}{4})?(?=\n?$)/u;" + "\n");
    }

    [Fact]
    public void ToTypeScript_CustomHeader_ReplacesTheDefaultNotice()
    {
        Dictionary<string, Pattern> patterns = new Dictionary<string, Pattern> { ["digits"] = Pattern.FromRegex(@"\d") };

        string custom = PatternExport.ToTypeScript(patterns, "Shared rules\nv1");
        string none = PatternExport.ToTypeScript(patterns, string.Empty);

        custom.ShouldStartWith("// Shared rules\n// v1\n\n/**");
        none.ShouldStartWith("/**");
    }

    [Fact]
    public void ToTypeScript_HeaderWithWindowsLineEndings_WritesOneCommentPerLine()
    {
        Dictionary<string, Pattern> patterns = new Dictionary<string, Pattern> { ["digits"] = Pattern.FromRegex(@"\d") };

        string module = PatternExport.ToTypeScript(patterns, "Shared rules\r\nv1");

        module.ShouldStartWith("// Shared rules\n// v1\n\n/**");
    }

    [Fact]
    public void ToTypeScript_NoPatterns_WritesOnlyTheHeader()
    {
        Dictionary<string, Pattern> patterns = new Dictionary<string, Pattern>();

        string module = PatternExport.ToTypeScript(patterns);

        module.ShouldBe("// Generated by Masterly.FluentRegex. Do not edit by hand.\n\n");
    }

    [Fact]
    public void ToTypeScript_SourceContainingCommentTerminator_IsEscaped()
    {
        Dictionary<string, Pattern> patterns = new Dictionary<string, Pattern> { ["stars"] = Pattern.FromRegex("a*/") };

        string module = PatternExport.ToTypeScript(patterns, string.Empty);

        module.ShouldContain(@" * .NET: a*\/");
        module.ShouldContain(@"export const stars = /a*\//u;");
    }

    [Theory]
    [InlineData("1abc")]
    [InlineData("has-dash")]
    [InlineData("class")]
    [InlineData("")]
    public void ToTypeScript_InvalidIdentifier_ThrowsArgumentException(string name)
    {
        Dictionary<string, Pattern> patterns = new Dictionary<string, Pattern> { [name] = Pattern.With.Digit };

        ArgumentException exception = Should.Throw<ArgumentException>(() => PatternExport.ToTypeScript(patterns));

        exception.Message.ShouldContain($"'{name}'");
        exception.ParamName.ShouldBe("patterns");
    }

    [Theory]
    [InlineData("$x")]
    [InlineData("_x")]
    [InlineData("\u216Bx")]
    [InlineData("a\u0301")]
    [InlineData("a\u093E")]
    [InlineData("a\u203F")]
    [InlineData("a1")]
    public void ToTypeScript_ValidIdentifier_BecomesTheConstantName(string name)
    {
        Dictionary<string, Pattern> patterns = new Dictionary<string, Pattern> { [name] = Pattern.With.Digit };

        string module = PatternExport.ToTypeScript(patterns, string.Empty);

        module.ShouldContain($"export const {name} = /\\p{{Nd}}/u;");
    }

    [Fact]
    public void ToTypeScript_UnsupportedPattern_NamesTheConstant()
    {
        Dictionary<string, Pattern> patterns = new Dictionary<string, Pattern> { ["atomic"] = Pattern.FromRegex("(?>a)") };

        NotSupportedException exception = Should.Throw<NotSupportedException>(() => PatternExport.ToTypeScript(patterns));

        exception.Message.ShouldStartWith("The pattern 'atomic' cannot be exported");
        exception.InnerException.ShouldBeOfType<NotSupportedException>();
    }

    [Fact]
    public void ToTypeScript_InvalidArguments_Throw()
    {
        Dictionary<string, Pattern> withNull = new Dictionary<string, Pattern> { ["empty"] = null! };

        Should.Throw<ArgumentNullException>(() => PatternExport.ToTypeScript(null!)).ParamName.ShouldBe("patterns");
        Should.Throw<ArgumentException>(() => PatternExport.ToTypeScript(withNull)).Message.ShouldContain("'empty'");
    }
}
