using Shouldly;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

public sealed class SqlRegexTests
{
    private static readonly Pattern Quoted = Pattern.FromRegex(@"^it's [0-9]+\z");

    private static readonly Pattern ExactLikeSku = Pattern.FromRegex(@"^[A-Z]{3}-[0-9]{4}\z");

    [Theory]
    [InlineData(SqlDialect.PostgreSql, "\"Email\"", @"""Email"" ~ '\A[A-Z]{3}-[0-9]{4}(?=\n?\Z)'")]
    [InlineData(SqlDialect.MySql, "`Email`", @"REGEXP_LIKE(`Email`, '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)', 'c')")]
    [InlineData(SqlDialect.MariaDb, "`Email`", @"`Email` REGEXP '(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)'")]
    [InlineData(SqlDialect.SqlServer2025, "[Email]", @"REGEXP_LIKE([Email], N'\A[A-Z]{3}-[0-9]{4}\n?\z')")]
    [InlineData(SqlDialect.Sqlite, "\"Email\"", @"""Email"" REGEXP '^[A-Z]{3}-[0-9]{4}$'")]
    public void Condition_EmitsTheDialectPredicateWithDotNetSemantics(SqlDialect dialect, string column, string expected)
    {
        SqlRegex.Condition(column, TestPatterns.Sku, dialect).ShouldBe(expected);
    }

    [Fact]
    public void Condition_DoublesQuotesAndMySqlBackslashes()
    {
        SqlRegex.Condition("c", Quoted, SqlDialect.PostgreSql).ShouldBe(@"c ~ '\Ait''s [0-9]+\Z'");
        SqlRegex.Condition("c", Quoted, SqlDialect.MySql).ShouldBe(@"REGEXP_LIKE(c, '(?-i)\\Ait''s [0-9]+\\z', 'c')");
        SqlRegex.Condition("c", Quoted, SqlDialect.SqlServer2025).ShouldBe(@"REGEXP_LIKE(c, N'\Ait''s [0-9]+\z')");
    }

    // .NET's IgnoreCase folds under the current culture: [A-Za-z] also matches U+212A (Kelvin sign) everywhere and
    // U+0130 (dotted capital I) in cultures other than the invariant one. Regular strings with doubled backslashes are
    // used because the compiler expands \u escapes even in verbatim strings.
    [Theory]
    [InlineData("en-US", "c ~ '\\A[A-Za-z\\u0130\\u212A]{3}-[0-9]{4}(?=\\n?\\Z)'")]
    [InlineData("", "c ~ '\\A[A-Za-z\\u212A]{3}-[0-9]{4}(?=\\n?\\Z)'")]
    public void Condition_ExpandsIgnoreCaseExactlyAsDotNetFoldsIt(string culture, string expected)
    {
        using CultureScope scope = new CultureScope(culture);

        SqlRegex.Condition("c", TestPatterns.CaseInsensitiveSku, SqlDialect.PostgreSql).ShouldBe(expected);
    }

    [Fact]
    public void Condition_SqlServerWithoutExactLike_ThrowsWithGuidance()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => SqlRegex.Condition("[Email]", TestPatterns.Sku, SqlDialect.SqlServer));

        ex.Message.ShouldContain(TestPatterns.SkuRegex);
        ex.Message.ShouldContain("SqlDialect.SqlServer2025");
    }

    [Fact]
    public void Condition_SqlServerDefaultCollation_ComparesTheExactLikeUnderBin2()
    {
        string condition = SqlRegex.Condition("[Sku]", ExactLikeSku, SqlDialect.SqlServer);

        condition.ShouldBe("[Sku] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]'");
    }

    [Fact]
    public void Condition_SqlServerBinaryCollation_EmitsTheProvenExactLike()
    {
        string condition = SqlRegex.Condition("[Sku]", ExactLikeSku, SqlDialect.SqlServer, LikeCollation.Binary);

        condition.ShouldBe("[Sku] LIKE N'[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]'");
    }

    [Fact]
    public void Condition_RejectsBadArguments()
    {
        Should.Throw<ArgumentException>(() => SqlRegex.Condition(" ", TestPatterns.Sku, SqlDialect.PostgreSql));
        Should.Throw<ArgumentNullException>(() => SqlRegex.Condition("c", null!, SqlDialect.PostgreSql));
        Should.Throw<ArgumentOutOfRangeException>(() => SqlRegex.Condition("c", TestPatterns.Sku, (SqlDialect)42));
        Should.Throw<ArgumentOutOfRangeException>(() => SqlRegex.Condition("c", TestPatterns.Sku, SqlDialect.SqlServer, (LikeCollation)42));
    }
}
