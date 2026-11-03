using System.Text.RegularExpressions;
using Masterly.FluentRegex.Automata;
using Masterly.FluentRegex.Like;

namespace Masterly.FluentRegex.Tests;

public class LikeTests
{
    [Theory]
    [InlineData(@"^abc\z", LikeDialect.Standard)]
    [InlineData(@"^a{3}\z", LikeDialect.SqlServer)]
    [InlineData(@"^[aeiou]\z", LikeDialect.SqlServer)]
    public void ToLike_CaseSensitivePatternUnderCaseInsensitiveCollation_SaysWhyItIsASuperset(string regex, LikeDialect dialect)
    {
        LikeConversion like = Pattern.FromRegex(regex).ToLike(dialect, MatchScope.Substring, LikeCollation.CaseInsensitive);

        like.IsExact.ShouldBeFalse();
        like.Warnings.ShouldContain(warning => warning.Contains("case-insensitive collation also matches other casings"));
    }

    [Fact]
    public void ToLike_InexactTranslation_AlwaysHasAWarning()
    {
        LikeConversion like = Pattern.FromRegex(@"^abc\z").ToLike(LikeDialect.Standard, MatchScope.Substring, LikeCollation.CaseInsensitive);

        like.IsExact.ShouldBeFalse();
        like.Warnings.ShouldNotBeEmpty();
    }

    [Fact]
    public void ToLike_IgnoreCasePatternUnderCaseInsensitiveCollation_HasNoCaseWarning()
    {
        LikeConversion like = Pattern.FromRegex(@"^abc\z", RegexOptions.IgnoreCase)
            .ToLike(LikeDialect.Standard, MatchScope.Substring, LikeCollation.CaseInsensitive);

        like.Warnings.ShouldNotContain(warning => warning.Contains("case-insensitive collation also matches other casings"));
    }

    [Fact]
    public void ToSql_WithoutCharactersToEscape_HasNoEscapeClause()
    {
        string sql = Pattern.FromRegex(@"^INV-[0-9]").ToLike(LikeDialect.SqlServer).ToSql("[Email]");

        sql.ShouldBe("[Email] LIKE N'INV-[0-9]%'");
    }

    // ------------------------------------------------------------------
    // Exactness proofs
    // ------------------------------------------------------------------

    [Fact]
    public void ToLike_AnchoredInvoiceWithAsciiDigits_IsExactOnSqlServer()
    {
        Pattern pattern = Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z").WithAsciiDigits();

        LikeConversion like = pattern.ToLike(LikeDialect.SqlServer);

        like.Expression.ShouldBe("INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]");
        like.IsExact.ShouldBeTrue();
        like.IsSuperset.ShouldBeTrue();
        like.EscapeCharacter.ShouldBeNull();
        like.Warnings.ShouldBeEmpty();
        like.ToSql("[Number]").ShouldBe("[Number] LIKE N'INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'");
    }

    [Theory]
    [InlineData(LikeDialect.Standard)]
    [InlineData(LikeDialect.PostgreSql)]
    [InlineData(LikeDialect.MySql)]
    [InlineData(LikeDialect.Sqlite)]
    public void ToLike_AnchoredInvoice_IsASupersetWithoutSets(LikeDialect dialect)
    {
        Pattern pattern = Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z");

        LikeConversion like = pattern.ToLike(dialect);

        like.Expression.ShouldBe("INV-____-____");
        like.IsExact.ShouldBeFalse();
        like.Warnings.ShouldHaveSingleItem().ShouldContain("only SQL Server LIKE can express sets");
    }

    [Fact]
    public void ToLike_DollarAnchor_AlsoAcceptsATrailingNewlineSoNeedsATrailingPercent()
    {
        Pattern pattern = Pattern.FromRegex(@"^INV-\d{4}-\d{4}$").WithAsciiDigits();

        LikeConversion like = pattern.ToLike(LikeDialect.SqlServer);

        like.Expression.ShouldBe("INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]%");
        like.IsExact.ShouldBeFalse();
        like.Warnings.ShouldHaveSingleItem().ShouldContain(@"end the pattern with '\z'");
        pattern.IsMatch("INV-1234-5678\n").ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"^abc", "abc%", true)]
    [InlineData(@"xyz\z", "%xyz", true)]
    [InlineData(@"xyz$", "%xyz%", false)]
    [InlineData(@"^aba\z", "aba", true)]
    [InlineData(@"^aba$", "aba%", false)]
    [InlineData(@"^a[\s\S]*b\z", "a%b", true)]
    [InlineData(@"(?s)^a.c\z", "a_c", false)]
    [InlineData(@"^a.c\z", "a_c", false)]
    [InlineData(@"^(?:aba|ababa)\z", "aba%", false)]
    [InlineData(@"^ab?ba\z", "a%ba", false)]
    [InlineData(@"\d+", "%_%", false)]
    [InlineData(@"^(\w)\1abc\z", "_%abc", false)]
    [InlineData(@"^\z", "", true)]
    [InlineData(@"", "%", true)]
    public void ToLike_Standard_ProvesExactnessOrReturnsATightSuperset(string regex, string expected, bool exact)
    {
        LikeConversion like = Pattern.FromRegex(regex).ToLike(LikeDialect.Standard);

        like.Expression.ShouldBe(expected);
        like.IsExact.ShouldBe(exact);
        like.IsSuperset.ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"\d+", "%[0-9]%", true)]
    [InlineData(@"^a.c\z", "a[^\n]c", false)]
    [InlineData(@"^(?:a|b)x\z", "[a-b]x", true)]
    [InlineData(@"^[a-z]{3}\z", "[a-z][a-z][a-z]", true)]
    [InlineData(@"(?i)^abc\z", "[Aa][Bb][Cc]", true)]
    [InlineData(@"^[^a-z]\z", "[^a-z]", false)]
    [InlineData(@"^(?:ab|ac)\z", "a%", false)]
    public void ToLike_SqlServer_UsesSetsForSingleCharacterClasses(string regex, string expected, bool exact)
    {
        LikeConversion like = Pattern.FromRegex(regex).WithAsciiDigits().ToLike(LikeDialect.SqlServer);

        like.Expression.ShouldBe(expected);
        like.IsExact.ShouldBe(exact);
    }

    [Fact]
    public void ToLike_SuffixOverlap_DropsTheSuffixWhenTheMinimumLengthCannotHoldBoth()
    {
        LanguageFacts facts = Pattern.FromRegex(@"^(?:aba|ababa)\z").GetLanguageFacts(MatchScope.Substring);

        LikeConversion like = Pattern.FromRegex(@"^(?:aba|ababa)\z").ToLike(LikeDialect.Standard);

        facts.RequiredPrefix.ShouldBe("aba");
        facts.RequiredSuffix.ShouldBe("aba");
        facts.MinLength.ShouldBe(3);
        like.Expression.ShouldBe("aba%");
    }

    [Fact]
    public void ToLike_FullStringScope_OmitsThePadding()
    {
        LikeConversion substring = Pattern.FromRegex(@"abc").ToLike(LikeDialect.Standard, MatchScope.Substring);
        LikeConversion full = Pattern.FromRegex(@"abc").ToLike(LikeDialect.Standard, MatchScope.FullString);

        substring.Expression.ShouldBe("%abc%");
        full.Expression.ShouldBe("abc");
        full.IsExact.ShouldBeTrue();
        full.Scope.ShouldBe(MatchScope.FullString);
    }

    // ------------------------------------------------------------------
    // Collation
    // ------------------------------------------------------------------

    [Fact]
    public void ToLike_CaseInsensitiveCollation_IgnoreCasePatternIsExactAsPlainText()
    {
        Pattern pattern = Pattern.FromRegex(@"^abc\z", RegexOptions.IgnoreCase);

        LikeConversion standard = pattern.ToLike(LikeDialect.Standard, MatchScope.Substring, LikeCollation.CaseInsensitive);
        LikeConversion binary = pattern.ToLike(LikeDialect.Standard);

        standard.Expression.ShouldBe("abc");
        standard.IsExact.ShouldBeTrue();
        standard.Collation.ShouldBe(LikeCollation.CaseInsensitive);
        binary.Expression.ShouldBe("___");
        binary.IsExact.ShouldBeFalse();
    }

    [Fact]
    public void ToLike_CaseInsensitiveCollation_CaseSensitivePatternWithLettersIsNeverExact()
    {
        LikeConversion letters = Pattern.FromRegex(@"^abc\z").ToLike(LikeDialect.SqlServer, MatchScope.Substring, LikeCollation.CaseInsensitive);
        LikeConversion digits = Pattern.FromRegex(@"^123-\z").ToLike(LikeDialect.SqlServer, MatchScope.Substring, LikeCollation.CaseInsensitive);

        letters.Expression.ShouldBe("abc");
        letters.IsExact.ShouldBeFalse();
        digits.Expression.ShouldBe("123-");
        digits.IsExact.ShouldBeTrue();
    }

    [Fact]
    public void ToLike_CaseInsensitiveCollation_ListsSetMembersInsteadOfRanges()
    {
        LikeConversion sensitive = Pattern.FromRegex(@"^[a-c]\z").ToLike(LikeDialect.SqlServer, MatchScope.Substring, LikeCollation.CaseInsensitive);
        LikeConversion insensitive = Pattern.FromRegex(@"(?i)^[a-c]\z").ToLike(LikeDialect.SqlServer, MatchScope.Substring, LikeCollation.CaseInsensitive);

        sensitive.Expression.ShouldBe("[abc]");
        sensitive.IsExact.ShouldBeFalse();
        insensitive.Expression.ShouldBe("[ABCabc]");
        insensitive.IsExact.ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    // Escaping and SQL
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(LikeDialect.SqlServer, @"a%b_c[d]", @"[c] LIKE N'%a\%b\_c[d]%' ESCAPE '\'")]
    [InlineData(LikeDialect.Standard, @"a%b_c", @"[c] LIKE '%a\%b\_c%' ESCAPE '\'")]
    [InlineData(LikeDialect.MySql, @"a%b_c", @"[c] LIKE '%a\\%b\\_c%' ESCAPE '\\'")]
    [InlineData(LikeDialect.MySql, @"a\\b", @"[c] LIKE '%a\\\\b%' ESCAPE '\\'")]
    [InlineData(LikeDialect.PostgreSql, @"a\\b", @"[c] LIKE '%a\\b%' ESCAPE '\'")]
    [InlineData(LikeDialect.Standard, @"a\\b", @"[c] LIKE '%a\b%'")]
    [InlineData(LikeDialect.Sqlite, @"a\\b%", @"[c] LIKE '%a\\b\%%' ESCAPE '\'")]
    [InlineData(LikeDialect.Standard, @"it's", @"[c] LIKE '%it''s%'")]
    public void ToSql_EscapesSpecialCharactersAndQuotes(LikeDialect dialect, string regex, string expected)
    {
        LikeConversion like = Pattern.FromRegex(regex).ToLike(dialect);

        like.ToSql("[c]").ShouldBe(expected);
        like.IsExact.ShouldBeTrue();
    }

    [Fact]
    public void ToSql_SqlServer_WritesAUnicodeLiteralSoTrailingBlanksCount()
    {
        LikeConversion like = Pattern.FromRegex(@"^abc \z").ToLike(LikeDialect.SqlServer);

        like.ToSql("[c]").ShouldBe("[c] LIKE N'abc '");
        like.IsExact.ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"^%[\\a]\z", @"\%[\\a]")]
    [InlineData(@"^%[\\a-c]\z", @"\%[\\a-c]")]
    [InlineData(@"^_[\\%]\z", @"\_[%\\]")]
    public void ToLike_SqlServerSetWithTheEscapeCharacter_DoublesItInsideTheBrackets(string regex, string expected)
    {
        LikeConversion like = Pattern.FromRegex(regex).ToLike(LikeDialect.SqlServer);

        like.Expression.ShouldBe(expected);
        like.EscapeCharacter.ShouldBe('\\');
        like.IsExact.ShouldBeTrue();
    }

    [Fact]
    public void ToLike_SqlServerSetWithTheEscapeCharacter_AcceptsTheBackslash()
    {
        Pattern pattern = Pattern.FromRegex(@"^%[\\a]\z");

        LikeConversion like = pattern.ToLike(LikeDialect.SqlServer);

        pattern.IsMatch("%\\").ShouldBeTrue();
        Regex.IsMatch("%\\", $@"\A(?:{like.RegexText})\z").ShouldBeTrue();
        Regex.IsMatch("%a", $@"\A(?:{like.RegexText})\z").ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"[\a]", "a", true)]
    [InlineData(@"[\a]", "\\", false)]
    [InlineData(@"[\\]", "\\", true)]
    [InlineData(@"[\a-c]", "-", true)]
    [InlineData(@"[\a-c]", "b", false)]
    [InlineData(@"[^\\]", "\\", false)]
    public void LikeRegex_FromExpression_ReadsTheEscapeCharacterInsideSqlServerSetsAsSqlServerDoes(string expression, string input, bool expected)
    {
        string regex = LikeRegex.FromExpression(expression, '\\', LikeDialect.SqlServer);

        Regex.IsMatch(input, $@"\A(?:{regex})\z").ShouldBe(expected);
    }

    // ------------------------------------------------------------------
    // Soundness: comments, collations, supplementary characters
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("^INV-[0-9]{4} # invoice number", RegexOptions.IgnorePatternWhitespace, "INV-[0-9][0-9][0-9][0-9]%", true)]
    [InlineData("abc #c", RegexOptions.IgnorePatternWhitespace, "%abc%", true)]
    [InlineData("a|b #c", RegexOptions.IgnorePatternWhitespace, "%[a-b]%", true)]
    [InlineData("a(?x) b #c", RegexOptions.None, "%ab%", true)]
    public void ToLike_TrailingFreeSpacingComment_KeepsTheScopePadding(string regex, RegexOptions options, string expected, bool exact)
    {
        LikeConversion like = Pattern.FromRegex(regex, options).ToLike(LikeDialect.SqlServer);

        like.Expression.ShouldBe(expected);
        like.IsExact.ShouldBe(exact);
    }

    [Theory]
    [InlineData(@"^[^a]")]
    [InlineData(@"^[^a]\z")]
    [InlineData(@"^\W\z")]
    public void ToLike_NegatedClassUnderCaseInsensitiveSqlServer_IsNeverANegatedSet(string regex)
    {
        Pattern pattern = Pattern.FromRegex(regex);

        LikeConversion like = pattern.ToLike(LikeDialect.SqlServer, MatchScope.Substring, LikeCollation.CaseInsensitive);

        like.Expression.ShouldNotContain("[^");
        like.Expression.ShouldBe("%");
        like.IsExact.ShouldBeFalse();
    }

    [Theory]
    [InlineData(LikeDialect.MySql, "_", false)]
    [InlineData(LikeDialect.Standard, "_", false)]
    [InlineData(LikeDialect.SqlServer, "[Kk\u212A]", true)]
    public void ToLike_IgnoreCaseLetterWithNonAsciiVariants_IsNotLeftToTheCollation(LikeDialect dialect, string expected, bool exact)
    {
        Pattern pattern = Pattern.FromRegex(@"(?i)^k\z");

        LikeConversion like = pattern.ToLike(dialect, MatchScope.Substring, LikeCollation.CaseInsensitive);

        pattern.IsMatch("\u212A").ShouldBeTrue();
        like.Expression.ShouldBe(expected);
        like.IsExact.ShouldBe(exact);
    }

    [Theory]
    [InlineData(@"(?i)^id\z", LikeDialect.Standard, "en-US", "_d")]
    [InlineData(@"(?i)^id\z", LikeDialect.SqlServer, "en-US", "[Ii\u0130]d")]
    [InlineData(@"(?i)^id\z", LikeDialect.SqlServer, "", "[Ii]d")]
    [InlineData(@"(?i)^abc\z", LikeDialect.Sqlite, "en-US", "abc")]
    public void ToLike_IgnoreCaseLettersUnderCaseInsensitiveCollation_StayTextOnlyWhenEveryCollationFoldsThem(
        string regex, LikeDialect dialect, string culture, string expected)
    {
        using CultureScope scope = new CultureScope(culture);

        LikeConversion like = Pattern.FromRegex(regex).ToLike(dialect, MatchScope.Substring, LikeCollation.CaseInsensitive);

        like.Expression.ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"^..\z", LikeDialect.PostgreSql, "_%")]
    [InlineData(@"^..\z", LikeDialect.MySql, "_%")]
    [InlineData(@"^..\z", LikeDialect.SqlServer, "_%")]
    [InlineData(@"^...\z", LikeDialect.Standard, "__%")]
    [InlineData(@"^a[^b][^c]d\z", LikeDialect.SqlServer, "a_%d")]
    [InlineData(@"^.x.\z", LikeDialect.Sqlite, "_x_")]
    public void ToLike_RunOfPositionsThatCanMatchSurrogates_AcceptsSupplementaryCharactersAsOne(string regex, LikeDialect dialect, string expected)
    {
        LikeConversion like = Pattern.FromRegex(regex).ToLike(dialect);

        like.Expression.ShouldBe(expected);
        like.IsExact.ShouldBeFalse();
        Regex.IsMatch("\uD83D\uDE00", $@"\A(?:{CodePointRegex(like)})\z").ShouldBe(Pattern.FromRegex(regex).IsMatch("\uD83D\uDE00"));
    }

    [Fact]
    public void ToLike_EmojiForTwoDots_IsAcceptedByTheCodePointModel()
    {
        Pattern pattern = Pattern.FromRegex(@"^..\z");

        LikeConversion like = pattern.ToLike(LikeDialect.PostgreSql);

        pattern.IsMatch("\uD83D\uDE00").ShouldBeTrue();
        Regex.IsMatch("\uD83D\uDE00", $@"\A(?:{CodePointRegex(like)})\z").ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"^\d\d\z", LikeDialect.Standard, "__")]
    [InlineData(@"^[A-Z]{3}\z", LikeDialect.SqlServer, "[A-Z][A-Z][A-Z]")]
    public void ToLike_ClassesThatCannotMatchSurrogates_KeepOnePositionEach(string regex, LikeDialect dialect, string expected)
    {
        LikeConversion like = Pattern.FromRegex(regex).ToLike(dialect);

        like.Expression.ShouldBe(expected);
    }

    [Fact]
    public void ToLike_SupplementaryCharactersUnderCaseInsensitiveSqlServer_BecomePercent()
    {
        LikeConversion like = Pattern.FromRegex(@"^a...\z").ToLike(LikeDialect.SqlServer, MatchScope.Substring, LikeCollation.CaseInsensitive);

        like.Expression.ShouldBe("a%");
        like.IsExact.ShouldBeFalse();
    }

    [Fact]
    public void ToLike_LoneSurrogateLiteral_BecomesAnyCharacter()
    {
        LikeConversion like = Pattern.FromRegex("^\\uD83Dx\\z").ToLike(LikeDialect.Standard);

        like.Expression.ShouldBe("_x");
        like.IsExact.ShouldBeFalse();
    }

    [Fact]
    public void ToLike_WholeSurrogatePairLiteral_StaysExactText()
    {
        LikeConversion like = Pattern.FromRegex("^\\uD83D\\uDE00\\z").ToLike(LikeDialect.PostgreSql);

        like.Expression.ShouldBe("\uD83D\uDE00");
        like.IsExact.ShouldBeTrue();
    }

    [Fact]
    public void ToString_DescribesTheExpressionAndItsExactness()
    {
        Pattern.FromRegex(@"^abc").ToLike(LikeDialect.Standard).ToString().ShouldBe("abc% (exact)");
        Pattern.FromRegex(@"abc$").ToLike(LikeDialect.Standard).ToString().ShouldBe("%abc% (superset)");
    }

    [Fact]
    public void ToSql_NullColumn_ThrowsArgumentNullException()
    {
        LikeConversion like = Pattern.With.Digit.ToLike(LikeDialect.Standard);

        Should.Throw<ArgumentNullException>(() => like.ToSql(null!)).ParamName.ShouldBe("columnSql");
    }

    [Fact]
    public void ToLike_UndefinedArguments_ThrowArgumentOutOfRange()
    {
        Pattern pattern = Pattern.With.Digit;

        Should.Throw<ArgumentOutOfRangeException>(() => pattern.ToLike((LikeDialect)9)).ParamName.ShouldBe("dialect");
        Should.Throw<ArgumentOutOfRangeException>(() => pattern.ToLike(LikeDialect.Standard, (MatchScope)9)).ParamName.ShouldBe("scope");
        Should.Throw<ArgumentOutOfRangeException>(() => pattern.ToLike(LikeDialect.Standard, MatchScope.Substring, (LikeCollation)9)).ParamName.ShouldBe("collation");
    }

    // ------------------------------------------------------------------
    // The back-conversion used for proofs
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(@"a\%b_c[0-9]%[^x]", LikeDialect.SqlServer, "a\\u0025b[\\s\\S]c[0-9][\\s\\S]*[\\u0000-wy-\\uFFFF]")]
    [InlineData(@"a\%b_c[0-9]%", LikeDialect.Standard, "a\\u0025b[\\s\\S]c\\u005B0\\u002D9\\u005D[\\s\\S]*")]
    public void LikeRegex_FromExpression_MapsEachLikeConstruct(string expression, LikeDialect dialect, string expected)
    {
        string regex = LikeRegex.FromExpression(expression, '\\', dialect);

        regex.ShouldBe(expected);
    }

    [Fact]
    public void LikeRegex_FromExpression_HonorsTheDefaultBackslashEscapeOfMySqlAndPostgreSql()
    {
        string mysql = LikeRegex.FromExpression(@"a\\b\_", null, LikeDialect.MySql);
        string standard = LikeRegex.FromExpression(@"a\\b\_", null, LikeDialect.Standard);

        mysql.ShouldBe("a\\u005Cb\\u005F");
        standard.ShouldBe("a\\u005C\\u005Cb\\u005C[\\s\\S]");
    }

    // ------------------------------------------------------------------
    // Property: every LIKE pattern is a superset
    // ------------------------------------------------------------------

    public static TheoryData<string, LikeDialect, LikeCollation> SupersetCases()
    {
        TheoryData<string, LikeDialect, LikeCollation> data = new TheoryData<string, LikeDialect, LikeCollation>();
        string[] regexes =
        {
            @"^INV-\d{4}-\d{4}$", @"^INV-\d{4}-\d{4}\z", @"^abc", @"xyz$", @"xyz\z", @"^aba$", @"^ab?ba\z", @"\d+", @"(?i)^abc\z",
            @"^a.c\z", @"(?s)^a.c\z", @"a|b", @"^(?:ab|ac)\z", @"^x*\z", @"^$", @"^a\d*b\z", @"^(?:abc)+\z", @"^(?=.*\d).{8,}$",
            @"(\w)\1", @"^abc(\w)\1def\z", @"(?m)^abc$", @"\bcat\b", @"^(?!abc)[a-c]{3}\z", @"ab(?<=b)cd", @"(?i)k+\z", @"a%b_c[d]\\",
            @"^[^\s\S]\z", @"[a-z-[aeiou]]+@", @"^(?:aba|ababa)\z",
            "(?x)^INV-[0-9]{4} # invoice number", "(?x)a|b #c", "a(?x) b #c", @"^[^a]", @"^..\z", @"^.{5}x\z", @"(?i)^k\z", @"(?i)^id\z",
            @"^%[\\a]\z", @"^%[\\a-c]_\z", "^\\uD83D.\\z", "^\\uD83D\\uDE00\\z", @"(a)\1(?:abc$){1}",
        };
        foreach (string regex in regexes)
        {
            data.Add(regex, LikeDialect.Standard, LikeCollation.Binary);
            data.Add(regex, LikeDialect.SqlServer, LikeCollation.Binary);
            data.Add(regex, LikeDialect.SqlServer, LikeCollation.CaseInsensitive);
            data.Add(regex, LikeDialect.MySql, LikeCollation.CaseInsensitive);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SupersetCases))]
    public void ToLike_HandpickedPattern_IsAProvenOrSampledSuperset(string regex, LikeDialect dialect, LikeCollation collation)
    {
        Pattern pattern = Pattern.FromRegex(regex);

        AssertSuperset(pattern, dialect, collation);
    }

    [Theory]
    [MemberData(nameof(CommonPatternCases))]
    public void ToLike_EveryCommonPattern_IsAProvenOrSampledSuperset(string name)
    {
        Pattern pattern = SqlFlavorTests.CommonPattern(name);

        AssertSuperset(pattern, LikeDialect.SqlServer, LikeCollation.Binary);
        AssertSuperset(pattern, LikeDialect.Standard, LikeCollation.CaseInsensitive);
    }

    public static TheoryData<string> CommonPatternCases() => SqlFlavorTests.CommonPatternCases();

    /// <summary>
    /// Checks the LIKE pattern against what every collation of the kind is guaranteed to accept (only
    /// ASCII case partners under a case-insensitive collation, see LikeCaseFolding), in the UTF-16
    /// model by proof and samples, and in the code-point model on samples with supplementary characters;
    /// exactness must agree with the algebra and with the absence of '_' and negated sets.
    /// </summary>
    private static void AssertSuperset(Pattern pattern, LikeDialect dialect, LikeCollation collation)
    {
        MatchScope scope = MatchScope.Substring;
        LikeConversion like = pattern.ToLike(dialect, scope, collation);
        Pattern scoped = Pattern.FromRegex(ScopeAnalysis.ScopedText(pattern.ParseTree(), scope), pattern.Options);
        Pattern likeRegex = Pattern.FromRegex(like.RegexText, collation == LikeCollation.CaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None);
        Pattern guaranteed = Pattern.FromRegex(GuaranteedRegex(like));
        Regex guaranteedMatcher = new Regex($@"\A(?:{guaranteed})\z");
        Regex codePointMatcher = new Regex($@"\A(?:{CodePointRegex(like)})\z");
        string[] samples = pattern.GenerateMatching(30, seed: 5).Select((s, i) => Pad(i) + s + Pad(i + 1)).Where(pattern.IsMatch).ToArray();

        like.IsSuperset.ShouldBeTrue();
        samples.ShouldAllBe(sample => guaranteedMatcher.IsMatch(sample), like.ToString());
        samples.Where(CodePointMatcherApplies).ShouldAllBe(sample => codePointMatcher.IsMatch(sample), like.ToString());
        ProvenSubset(scoped, guaranteed).ShouldNotBe(false, like.ToString());
        like.IsExact.ShouldBe((ProvenEquivalent(scoped, likeRegex) ?? like.IsExact) && (ProvenEquivalent(scoped, guaranteed) ?? true) && !MatchesOneCharacter(like), like.ToString());
    }

    private static string GuaranteedRegex(LikeConversion like) =>
        like.Collation == LikeCollation.CaseInsensitive ? LikeRegex.GuaranteedFromExpression(like.Expression, like.EscapeCharacter, like.Dialect) : like.RegexText;

    private static bool MatchesOneCharacter(LikeConversion like) =>
        LikeExpressionParser.Parse(like.Expression, like.EscapeCharacter, like.Dialect).Any(part => part.CanMatchSurrogate);

    /// <summary>The code-point model is about well-formed UTF-16, which is all a UTF-8 database can store.</summary>
    private static bool CodePointMatcherApplies(string sample) => !HasLoneSurrogate(sample);

    private static bool HasLoneSurrogate(string text) =>
        text.Where((c, i) => char.IsSurrogate(c) && !char.IsSurrogatePair(text, i) && !(i > 0 && char.IsSurrogatePair(text[i - 1], c))).Any();

    /// <summary>
    /// The LIKE pattern as a regex over well-formed UTF-16 in which '_' and a negated set match one code
    /// point (a whole surrogate pair, or one code unit that is not a surrogate), as in PostgreSQL, MySQL,
    /// SQLite and SQL Server's supplementary-character-aware collations.
    /// </summary>
    private static string CodePointRegex(LikeConversion like)
    {
        bool folds = like.Collation == LikeCollation.CaseInsensitive;
        IReadOnlyList<LikePart> parts = LikeExpressionParser.Parse(like.Expression, like.EscapeCharacter, like.Dialect);
        return string.Concat(parts.Select(part => CodePointPart(part, LikeRegex.FromParts(new[] { part }, folds))));
    }

    private static string CodePointPart(LikePart part, string unitRegex) =>
        part.CanMatchSurrogate ? $"(?:{SurrogatePair}|(?!{AnySurrogate}){unitRegex})" : unitRegex;

    private const string SurrogatePair = "[\\uD800-\\uDBFF][\\uDC00-\\uDFFF]";
    private const string AnySurrogate = "[\\uD800-\\uDFFF]";

    private static bool? ProvenSubset(Pattern scoped, Pattern likeRegex)
    {
        try
        {
            return scoped.IsSubsetOf(likeRegex);
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool? ProvenEquivalent(Pattern scoped, Pattern likeRegex)
    {
        try
        {
            return scoped.IsEquivalentTo(likeRegex);
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static readonly string[] Paddings = { "", "zz", "\n#9 ", "\uD83D\uDE00" };

    private static string Pad(int seed) => Paddings[seed % Paddings.Length];
}
