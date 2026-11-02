using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class SqlFlavorTests
{
    private const string UnicodeWord = @"[\p{L}\p{Mn}\p{Nd}\p{Pc}]";
    private const string CaseSensitivePrefix = "(?-i)";

    // ------------------------------------------------------------------
    // Construct-by-construct translation
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(@"^[0-9]+$", @"\A[0-9]+(?=\n?\Z)")]
    [InlineData(@"^abc\z", @"\Aabc\Z")]
    [InlineData(@"abc\Z", @"abc(?=\n?\Z)")]
    [InlineData(@"(?i)hello", "[Hh][Ee][Ll][Ll][Oo]")]
    [InlineData("a.b", @"a[^\n]b")]
    [InlineData("(?s)a.b", "a.b")]
    [InlineData(@"[\s\S]", ".")]
    [InlineData("(?m)^x$", @"(?:\A|(?<=\n))x(?=\n|\Z)")]
    [InlineData(@"(a)(b)\2\1", @"(a)(b)\2\1")]
    [InlineData(@"(?<year>\d{2})-(\d{2})\k<year>", "([0-9\\u0660-")]
    [InlineData(@"(a)\1\x30", "(a)\\1\\u0030")]
    [InlineData("a{300}", "(?:a{255}a{45})")]
    [InlineData("x{2,300}?", "(?:x{2}x{0,255}?x{0,43}?)")]
    [InlineData("[a-z]{256,}", "(?:[a-z]{255}[a-z]+)")]
    [InlineData("[^a-z]", "[^a-z]")]
    [InlineData("[a-z-[aeiou]]", "[b-df-hj-np-tv-z]")]
    [InlineData("\\x00\\t\\u00e9", "\\u0000\\u0009\\u00E9")]
    [InlineData(@"[\]\[\\\-\^&]", @"[\&\-\[-\^]")]
    [InlineData("(?#note)a|b|", "a|b|")]
    [InlineData("(?=a)a(?<=a)(?!b)", "(?=a)a(?<=a)(?!b)")]
    [InlineData("(?:a)(?i:b)", "(?:a)(?:[Bb])")]
    public void ToFlavor_PostgreSql_TranslatesEachConstruct(string regex, string expectedStart)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.PostgreSql);

        conversion.Pattern.ShouldStartWith(expectedStart);
        conversion.Flags.ShouldBeEmpty();
        conversion.IsExact.ShouldBeTrue();
    }

    [Fact]
    public void ToFlavor_PostgreSql_ExpandsLocaleDependentClassesToExactRanges()
    {
        FlavorConversion digit = Pattern.FromRegex(@"\d").ToFlavor(RegexFlavor.PostgreSql);
        FlavorConversion asciiDigit = Pattern.FromRegex(@"\d").WithAsciiDigits().ToFlavor(RegexFlavor.PostgreSql);
        FlavorConversion word = Pattern.FromRegex(@"\w").ToFlavor(RegexFlavor.PostgreSql);

        digit.Pattern.ShouldStartWith("[0-9\\u0660-\\u0669");
        asciiDigit.Pattern.ShouldBe("[0-9]");
        word.Pattern.ShouldStartWith("[0-9A-Z_a-z");
        word.Pattern.ShouldNotContain(@"\p");
    }

    [Fact]
    public void ToFlavor_PostgreSql_WordBoundaryBecomesLookaroundsOverTheExactWordClass()
    {
        FlavorConversion conversion = Pattern.FromRegex(@"\bcat\B").ToFlavor(RegexFlavor.PostgreSql);

        conversion.Pattern.ShouldStartWith("(?:(?<=[0-9A-Z_a-z");
        conversion.Pattern.ShouldContain("\\u200C\\u200D");
        conversion.Pattern.ShouldContain("])(?![0-9A-Z_a-z");
        conversion.Pattern.ShouldContain("cat(?:(?<=[0-9A-Z_a-z");
        conversion.Pattern.ShouldNotContain(@"\p");
        conversion.IsExact.ShouldBeTrue();
    }

    [Fact]
    public void ToFlavor_PostgreSql_MixedGreedyAndLazyQuantifiers_ReportOnlyAMatchExtentDifference()
    {
        FlavorConversion conversion = Pattern.FromRegex("a+?b*").ToFlavor(RegexFlavor.PostgreSql);

        conversion.Pattern.ShouldBe("a+?b*");
        conversion.IsExact.ShouldBeTrue();
        conversion.PreservesMatchExtent.ShouldBeFalse();
        conversion.MatchExtentWarnings.ShouldHaveSingleItem().ShouldContain("shortest match");
    }

    [Theory]
    [InlineData(@"^[0-9]+$", @"\A[0-9]+(?=\n?\z)")]
    [InlineData(@"^\d{4}\z", @"\A\p{Nd}{4}\z")]
    [InlineData(@"\w+\W", UnicodeWord + @"+[^\p{L}\p{Mn}\p{Nd}\p{Pc}]")]
    [InlineData(@"\s\S", @"[\t\n\x{000B}\f\r\x{0085}\p{Z}][^\t\n\x{000B}\f\r\x{0085}\p{Z}]")]
    [InlineData(@"[\w-]", @"[\p{L}\p{Mn}\p{Nd}\p{Pc}\-]")]
    [InlineData(@"[^\W\d]", @"[\p{L}\p{Mn}\p{Pc}]")]
    [InlineData(@"\p{Lu}\P{Ll}[\p{Sc}]", @"\p{Lu}\P{Ll}[\p{Sc}]")]
    [InlineData(@"\p{IsBasicLatin}", @"[\x{0000}-\x{007F}]")]
    [InlineData("a.b(?s:.)", @"a[^\n]b(?:(?s:.))")]
    [InlineData("(?m)^x$", @"(?:\A|(?<=\n))x(?=\n|\z)")]
    [InlineData(@"(?<year>\d{2})-(\d{2})\k<year>", @"(?<year>\p{Nd}{2})-(\p{Nd}{2})\k<year>")]
    [InlineData(@"(?<my_group>a)\k<my_group>", @"(a)\1")]
    [InlineData(@"(a)\1\x30", @"(a)\1\x{0030}")]
    [InlineData("(?>a+)b", "(?>a+)b")]
    [InlineData("\\x00\\t\\u00e9", @"\x{0000}\x{0009}\x{00E9}")]
    [InlineData(@"\bx", @"(?:(?<=[\p{L}\p{Mn}\p{Nd}\p{Pc}\x{200C}\x{200D}])(?![\p{L}\p{Mn}\p{Nd}\p{Pc}\x{200C}\x{200D}])|(?<![\p{L}\p{Mn}\p{Nd}\p{Pc}\x{200C}\x{200D}])(?=[\p{L}\p{Mn}\p{Nd}\p{Pc}\x{200C}\x{200D}]))x")]
    public void ToFlavor_MySql_TranslatesEachConstructWithACaseSensitivePrefix(string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.MySql);

        conversion.Pattern.ShouldBe(CaseSensitivePrefix + expected);
        conversion.IsExact.ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"(?<year>\d{2})-(\d{2})\k<year>", @"(?<year>\p{Nd}{2})-(\p{Nd}{2})\g{1}")]
    [InlineData(@"(a)(b)\2\1", @"(a)(b)\g{2}\g{1}")]
    [InlineData(@"(?<my_group>a)\k<my_group>", @"(?<my_group>a)\g{1}")]
    [InlineData("(?<=ab|cde)x", "(?<=ab|cde)x")]
    [InlineData("a{65535}", "a{65535}")]
    public void ToFlavor_MariaDb_UsesPcreBackreferencesAndNames(string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.MariaDb);

        conversion.Pattern.ShouldBe(CaseSensitivePrefix + expected);
        conversion.IsExact.ShouldBeTrue();
    }

    [Fact]
    public void ToFlavor_MySql_LargestIcuRepeatCount_IsKept()
    {
        FlavorConversion conversion = Pattern.FromRegex("a{0,16777215}").ToFlavor(RegexFlavor.MySql);

        conversion.Pattern.ShouldBe(CaseSensitivePrefix + "a{0,16777215}");
        conversion.IsExact.ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    // Supplementary characters and surrogates (found by the live MySQL/MariaDB harness)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(RegexFlavor.MySql, "(?-i)x\\x{1F600}y")]
    [InlineData(RegexFlavor.MariaDb, "(?-i)x\\x{1F600}y")]
    [InlineData(RegexFlavor.SqlServer, "x\\x{1F600}y")]
    [InlineData(RegexFlavor.PostgreSql, "x\\U0001F600y")]
    public void ToFlavor_LiteralSurrogatePair_IsWrittenAsOneCodePoint(RegexFlavor flavor, string expected)
    {
        Pattern pattern = Pattern.FromRegex("x\\uD83D\\uDE00y");

        FlavorConversion conversion = pattern.ToFlavor(flavor);

        conversion.Pattern.ShouldBe(expected);
        conversion.IsExact.ShouldBeTrue();
    }

    [Fact]
    public void ToFlavor_MariaDb_SurrogatePairInLookbehind_CountsAsOneCharacter()
    {
        Pattern pattern = Pattern.FromRegex("(?<=a(?:\\uD83D\\uDE00|b))x");

        FlavorConversion conversion = pattern.ToFlavor(RegexFlavor.MariaDb);

        conversion.Pattern.ShouldBe(CaseSensitivePrefix + "(?<=a(?:\\x{1F600}|b))x");
    }

    [Theory]
    [InlineData(RegexFlavor.MySql, "^[\\x00-\\uFFFF]+$", "(?-i)\\A(?s:.)+(?=\\n?\\z)")]
    [InlineData(RegexFlavor.MariaDb, "^[a\\uD83D\\uDE00]$", "(?-i)\\A[a](?=\\n?\\z)")]
    public void ToFlavor_ClassRangeTouchingSurrogates_IsWrittenFromTheExactSet(RegexFlavor flavor, string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(flavor);

        conversion.Pattern.ShouldBe(expected);
    }

    [Fact]
    public void ToFlavor_MariaDb_NegatedSurrogateRange_NeverNamesASurrogate()
    {
        FlavorConversion conversion = Pattern.FromRegex("[^\\uD800-\\uDFFF]").ToFlavor(RegexFlavor.MariaDb);

        conversion.Pattern.ShouldNotMatch(@"\{D[89A-F][0-9A-F]{2}\}");
    }

    [Theory]
    [InlineData(@"^[0-9]+$", @"\A[0-9]+\n?\z")]
    [InlineData(@"^\d{4}\z", @"\A\p{Nd}{4}\z")]
    [InlineData("a.b(?s:.)", "a.b(?:(?s:.))")]
    [InlineData("(?m)^x$", "(?m:^)x(?m:$)")]
    [InlineData(@"(?<year>\d{2})-(\d{2})", @"(?P<year>\p{Nd}{2})-(\p{Nd}{2})")]
    [InlineData("(?i)k", "[Kk\\x{212A}]")]
    [InlineData("a{1000}", "a{1000}")]
    public void ToFlavor_SqlServer_TranslatesEachConstruct(string regex, string expectedStart)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.SqlServer);

        conversion.Pattern.ShouldStartWith(expectedStart);
        conversion.IsExact.ShouldBeTrue();
    }

    [Fact]
    public void ToFlavor_SqlServer_EcmaScriptWordBoundaryIsNative()
    {
        FlavorConversion conversion = Pattern.FromRegex(@"\bx\d\B").WithOptions(RegexOptions.ECMAScript).ToFlavor(RegexFlavor.SqlServer);

        conversion.Pattern.ShouldBe(@"\bx[0-9]\B");
    }

    [Theory]
    [InlineData(RegexOptions.None, "abc", "abc")]
    [InlineData(RegexOptions.IgnoreCase, "abc", "(?i)abc")]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace, "a b", "(?imsnx)a b")]
    [InlineData(RegexOptions.Compiled, @"\d\w\b", @"\d\w\b")]
    public void ToFlavor_Sqlite_IsTheDotNetPatternWithAnInlineOptionsPrefix(RegexOptions options, string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).WithOptions(options).ToFlavor(RegexFlavor.Sqlite);

        conversion.Pattern.ShouldBe(expected);
        conversion.Flags.ShouldBeEmpty();
        conversion.IsExact.ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    // Group numbers
    // ------------------------------------------------------------------

    [Fact]
    public void GroupNumbers_MixedNamedAndUnnamedGroups_MapToLeftToRightNumbers()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<year>\d{4})-(\d{2})-(?<day>\d{2})(x)");

        IReadOnlyDictionary<string, int> postgres = pattern.ToFlavor(RegexFlavor.PostgreSql).GroupNumbers;
        IReadOnlyDictionary<string, int> javaScript = pattern.ToFlavor(RegexFlavor.JavaScript).GroupNumbers;
        IReadOnlyDictionary<string, int> sqlite = pattern.ToFlavor(RegexFlavor.Sqlite).GroupNumbers;

        postgres.ShouldBe(new Dictionary<string, int> { ["year"] = 1, ["3"] = 1, ["1"] = 2, ["day"] = 3, ["4"] = 3, ["2"] = 4 });
        javaScript.ShouldBe(postgres);
        sqlite.ShouldBe(new Dictionary<string, int> { ["year"] = 3, ["3"] = 3, ["1"] = 1, ["day"] = 4, ["4"] = 4, ["2"] = 2 });
    }

    [Fact]
    public void GroupNumbers_PostgreSql_OmitsGroupsInsideLookaroundsWhichCannotCapture()
    {
        FlavorConversion conversion = Pattern.FromRegex(@"(?=(a))(b)").ToFlavor(RegexFlavor.PostgreSql);

        conversion.Pattern.ShouldBe("(?=(?:a))(b)");
        conversion.GroupNumbers.ShouldBe(new Dictionary<string, int> { ["2"] = 1 });
    }

    [Fact]
    public void GroupNumbers_MySql_CountsGroupsInsideLookarounds()
    {
        FlavorConversion conversion = Pattern.FromRegex(@"(?=(a))(b)").ToFlavor(RegexFlavor.MySql);

        conversion.GroupNumbers.ShouldBe(new Dictionary<string, int> { ["1"] = 1, ["2"] = 2 });
    }

    // ------------------------------------------------------------------
    // Literals and conditions
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(RegexFlavor.PostgreSql, @"it's\\", @"'it''s\\'")]
    [InlineData(RegexFlavor.SqlServer, @"it's\\", @"N'it''s\\'")]
    [InlineData(RegexFlavor.Sqlite, @"it's\\", @"'it''s\\'")]
    [InlineData(RegexFlavor.MySql, @"it's\\", @"'(?-i)it''s\\\\'")]
    [InlineData(RegexFlavor.MariaDb, @"it's\\", @"'(?-i)it''s\\\\'")]
    public void ToLiteral_SqlFlavor_IsAQuotedStringLiteral(RegexFlavor flavor, string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(flavor);

        conversion.ToLiteral().ShouldBe(expected);
        conversion.ToString().ShouldBe(expected);
        conversion.IsSql.ShouldBeTrue();
    }

    [Theory]
    [InlineData(RegexFlavor.PostgreSql, "\"Email\"", "\"Email\" ~ '\\A[0-9]+(?=\\n?\\Z)'")]
    [InlineData(RegexFlavor.MySql, "Email", "REGEXP_LIKE(Email, '(?-i)\\\\A[0-9]+(?=\\\\n?\\\\z)')")]
    [InlineData(RegexFlavor.MariaDb, "Email", "Email REGEXP '(?-i)\\\\A[0-9]+(?=\\\\n?\\\\z)'")]
    [InlineData(RegexFlavor.SqlServer, "[Email]", "REGEXP_LIKE([Email], N'\\A[0-9]+\\n?\\z')")]
    [InlineData(RegexFlavor.Sqlite, "Email", "Email REGEXP '^[0-9]+$'")]
    public void ToSqlCondition_EachSqlFlavor_UsesTheEngineOperator(RegexFlavor flavor, string column, string expected)
    {
        string condition = Pattern.FromRegex("^[0-9]+$").ToSqlCondition(column, flavor);

        condition.ShouldBe(expected);
    }

    [Fact]
    public void ToSqlCondition_NonSqlFlavor_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Pattern.With.Digit.ToSqlCondition("c", RegexFlavor.JavaScript)).ParamName.ShouldBe("flavor");
    }

    [Fact]
    public void ToSqlCondition_NullColumn_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Pattern.With.Digit.ToSqlCondition(null!, RegexFlavor.PostgreSql)).ParamName.ShouldBe("columnSql");
    }

    // ------------------------------------------------------------------
    // Unsupported constructs
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(RegexFlavor.PostgreSql, "(?>a)", "atomic group", 0)]
    [InlineData(RegexFlavor.PostgreSql, "(?=a)*b", "zero-width", 0)]
    [InlineData(RegexFlavor.PostgreSql, "(?:a){300}", "bounds above 255", 0)]
    [InlineData(RegexFlavor.PostgreSql, @"(a)(?=\1)", "inside a lookaround", 6)]
    [InlineData(RegexFlavor.PostgreSql, @"(?=(a)\1)", "cannot capture", 6)]
    [InlineData(RegexFlavor.PostgreSql, @"(?i)(a)\1", "case-insensitive backreference", 7)]
    [InlineData(RegexFlavor.PostgreSql, @"\1(a)", "does not entirely precede", 0)]
    [InlineData(RegexFlavor.PostgreSql, @"a\Gb", @"'\G'", 1)]
    [InlineData(RegexFlavor.PostgreSql, "(?<x>a)(?<x>b)", "duplicate group name", 7)]
    [InlineData(RegexFlavor.MySql, "(?<=a+)b", "bounded lookbehind", 0)]
    [InlineData(RegexFlavor.MySql, "(?(a)b|c)", "conditional", 0)]
    [InlineData(RegexFlavor.MySql, "a{16777216}", "bounds above 16777215", 0)]
    [InlineData(RegexFlavor.MySql, "a{1,2147483647}", "bounds above 16777215", 0)]
    [InlineData(RegexFlavor.MySql, "a\\uD83D", "lone surrogate", 1)]
    [InlineData(RegexFlavor.MariaDb, "\\uD83D\\uDE00+", "lone surrogate", 0)]
    [InlineData(RegexFlavor.MariaDb, "[\\uD83D]", "matches nothing (or only lone surrogates)", 0)]
    [InlineData(RegexFlavor.SqlServer, "\\uDE00", "lone surrogate", 0)]
    [InlineData(RegexFlavor.MariaDb, "(?<=a{1,2})b", "fixed length", 0)]
    [InlineData(RegexFlavor.MariaDb, "a{65536}", "bounds above 65535", 0)]
    [InlineData(RegexFlavor.MariaDb, "(?<o>a)(?<c-o>b)", "balancing group", 7)]
    [InlineData(RegexFlavor.SqlServer, "(?=a)b", "lookaround", 0)]
    [InlineData(RegexFlavor.SqlServer, @"(a)\1", "backreference", 3)]
    [InlineData(RegexFlavor.SqlServer, @"\bx", "Unicode word boundary", 0)]
    [InlineData(RegexFlavor.SqlServer, "a$b", "followed by more pattern", 1)]
    [InlineData(RegexFlavor.SqlServer, "a{1001}", "bounds above 1000", 0)]
    [InlineData(RegexFlavor.SqlServer, "(?>a)", "atomic group", 0)]
    [InlineData(RegexFlavor.Sqlite, @"(a)\1", "backreference", 3)]
    [InlineData(RegexFlavor.Sqlite, "(?<=a)b", "lookaround", 0)]
    [InlineData(RegexFlavor.Sqlite, "(?>a)", "atomic group", 0)]
    public void ToFlavor_UnsupportedConstruct_ThrowsNotSupportedExceptionWithPosition(RegexFlavor flavor, string regex, string construct, int position)
    {
        Pattern pattern = Pattern.FromRegex(regex);

        NotSupportedException exception = Should.Throw<NotSupportedException>(() => pattern.ToFlavor(flavor));

        exception.Message.ShouldContain(construct);
        exception.Message.ShouldContain($"at position {position}");
    }

    [Fact]
    public void ToFlavor_SeveralUnsupportedConstructs_ListsAllOfThem()
    {
        Pattern pattern = Pattern.FromRegex(@"(?=a)(b)\1(?>c)");

        NotSupportedException exception = Should.Throw<NotSupportedException>(() => pattern.ToFlavor(RegexFlavor.SqlServer));

        exception.Message.ShouldStartWith("The pattern cannot be converted to SQL Server");
        exception.Message.ShouldContain("lookaround '(?=...)' at position 0");
        exception.Message.ShouldContain("backreference at position 8");
        exception.Message.ShouldContain("atomic group '(?>...)' at position 10");
    }

    [Theory]
    [InlineData(RegexFlavor.PostgreSql)]
    [InlineData(RegexFlavor.MySql)]
    [InlineData(RegexFlavor.MariaDb)]
    [InlineData(RegexFlavor.SqlServer)]
    [InlineData(RegexFlavor.Sqlite)]
    public void ToFlavor_RightToLeft_IsNotSupportedByAnySqlFlavor(RegexFlavor flavor)
    {
        Pattern pattern = Pattern.FromRegex("abc").WithOptions(RegexOptions.RightToLeft);

        Should.Throw<NotSupportedException>(() => pattern.ToFlavor(flavor)).Message.ShouldContain("RightToLeft");
    }

    [Theory]
    [InlineData(RegexOptions.ECMAScript)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void ToFlavor_Sqlite_OptionWithoutInlineForm_ThrowsNotSupported(RegexOptions option)
    {
        Pattern pattern = Pattern.FromRegex("abc").WithOptions(option);

        Should.Throw<NotSupportedException>(() => pattern.ToFlavor(RegexFlavor.Sqlite)).Message.ShouldContain(option.ToString());
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

    /// <summary>The construct that keeps a pattern out of RE2 and the non-backtracking engine, or null when it converts.</summary>
    private static readonly Dictionary<string, string> Re2Obstacles = new Dictionary<string, string>
    {
        ["StrongPassword"] = "lookaround",
        ["Email.Strict"] = "lookaround",
        ["Base64"] = "lookaround",
        ["MarkdownLink"] = "lookaround",
        ["MacAddress"] = "backreference",
    };

    internal static IEnumerable<string> CommonPatternNames => CommonPatternFactories.Keys;

    internal static Pattern CommonPattern(string name) => CommonPatternFactories[name]();

    public static TheoryData<string> CommonPatternCases()
    {
        TheoryData<string> data = new TheoryData<string>();
        foreach (string name in CommonPatternFactories.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CommonPatternCases))]
    public void ToFlavor_EveryCommonPattern_ConvertsExactlyToPostgreSqlMySqlAndMariaDb(string name)
    {
        Pattern pattern = CommonPatternFactories[name]();

        FlavorConversion postgres = pattern.ToFlavor(RegexFlavor.PostgreSql);
        FlavorConversion mysql = pattern.ToFlavor(RegexFlavor.MySql);
        FlavorConversion mariadb = pattern.ToFlavor(RegexFlavor.MariaDb);

        postgres.IsExact.ShouldBeTrue(name);
        mysql.IsExact.ShouldBeTrue(name);
        mariadb.IsExact.ShouldBeTrue(name);
        mysql.Pattern.ShouldStartWith(CaseSensitivePrefix);
        mariadb.Pattern.ShouldStartWith(CaseSensitivePrefix);
    }

    [Theory]
    [MemberData(nameof(CommonPatternCases))]
    public void ToFlavor_EveryCommonPattern_ConvertsToSqlServerAndSqliteOrNamesTheObstacle(string name)
    {
        Pattern pattern = CommonPatternFactories[name]();
        string? obstacle = Re2Obstacles.TryGetValue(name, out string? construct) ? construct : null;

        string? sqlServerObstacle = Obstacle(() => pattern.ToFlavor(RegexFlavor.SqlServer));
        string? sqliteObstacle = Obstacle(() => pattern.ToFlavor(RegexFlavor.Sqlite));

        (sqlServerObstacle == null ? null : obstacle).ShouldBe(obstacle, name);
        (sqliteObstacle == null ? null : obstacle).ShouldBe(obstacle, name);
        (sqlServerObstacle ?? string.Empty).ShouldContain(obstacle ?? string.Empty, Case.Sensitive, name);
        (sqliteObstacle ?? string.Empty).ShouldContain(obstacle ?? string.Empty, Case.Sensitive, name);
    }

    private static string? Obstacle(Func<FlavorConversion> conversion)
    {
        try
        {
            conversion().IsExact.ShouldBeTrue();
            return null;
        }
        catch (NotSupportedException ex)
        {
            return ex.Message;
        }
    }
}
