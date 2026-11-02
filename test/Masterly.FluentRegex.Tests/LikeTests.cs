using System.Text.RegularExpressions;
using Masterly.FluentRegex.Automata;
using Masterly.FluentRegex.Like;

namespace Masterly.FluentRegex.Tests;

public class LikeTests
{
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
        like.ToSql("[Number]").ShouldBe("[Number] LIKE 'INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'");
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
    [InlineData(@"(?s)^a.c\z", "a_c", true)]
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
    [InlineData(@"^a.c\z", "a[^\n]c", true)]
    [InlineData(@"^(?:a|b)x\z", "[a-b]x", true)]
    [InlineData(@"^[a-z]{3}\z", "[a-z][a-z][a-z]", true)]
    [InlineData(@"(?i)^abc\z", "[Aa][Bb][Cc]", true)]
    [InlineData(@"^[^a-z]\z", "[^a-z]", true)]
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
    [InlineData(LikeDialect.SqlServer, @"a%b_c[d]", @"[c] LIKE '%a\%b\_c[d]%' ESCAPE '\'")]
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

    private static void AssertSuperset(Pattern pattern, LikeDialect dialect, LikeCollation collation)
    {
        MatchScope scope = MatchScope.Substring;
        LikeConversion like = pattern.ToLike(dialect, scope, collation);
        Pattern scoped = Pattern.FromRegex(ScopeAnalysis.ScopedText(pattern.ParseTree(), scope), pattern.Options);
        Pattern likeRegex = Pattern.FromRegex(like.RegexText, collation == LikeCollation.CaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None);
        Regex likeMatcher = new Regex($@"\A(?:{like.RegexText})\z", likeRegex.Options);
        string[] samples = pattern.GenerateMatching(30, seed: 5).Select((s, i) => Pad(i) + s + Pad(i + 1)).Where(pattern.IsMatch).ToArray();

        like.IsSuperset.ShouldBeTrue();
        samples.ShouldAllBe(sample => likeMatcher.IsMatch(sample), like.ToString());
        ProvenSubset(scoped, likeRegex).ShouldNotBe(false, like.ToString());
        (ProvenEquivalent(scoped, likeRegex) ?? like.IsExact).ShouldBe(like.IsExact, like.ToString());
    }

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

    private static readonly string[] Paddings = { "", "zz", "\n#9 " };

    private static string Pad(int seed) => Paddings[seed % Paddings.Length];
}
