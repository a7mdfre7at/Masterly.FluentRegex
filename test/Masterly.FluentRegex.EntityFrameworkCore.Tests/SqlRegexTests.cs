using Shouldly;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

public sealed class SqlRegexTests
{
    private static readonly Pattern Quoted = Pattern.FromRegex(@"^it's \d+$");

    [Theory]
    [InlineData(SqlDialect.PostgreSql, "\"Email\" ~ '^[A-Z]{3}-[0-9]{4}$'")]
    [InlineData(SqlDialect.MySql, "REGEXP_LIKE(`Email`, '^[A-Z]{3}-[0-9]{4}$', 'c')")]
    [InlineData(SqlDialect.MariaDb, "`Email` REGEXP '(?-i)^[A-Z]{3}-[0-9]{4}$'")]
    [InlineData(SqlDialect.SqlServer2025, "REGEXP_LIKE([Email], N'^[A-Z]{3}-[0-9]{4}$')")]
    [InlineData(SqlDialect.Sqlite, "\"Email\" REGEXP '^[A-Z]{3}-[0-9]{4}$'")]
    public void Condition_EmitsTheDialectPredicate(SqlDialect dialect, string expected)
    {
        string column = dialect switch
        {
            SqlDialect.MySql => "`Email`",
            SqlDialect.MariaDb => "`Email`",
            SqlDialect.SqlServer2025 => "[Email]",
            _ => "\"Email\""
        };

        SqlRegex.Condition(column, TestPatterns.Sku, dialect).ShouldBe(expected);
    }

    [Fact]
    public void Condition_DoublesQuotesAndMySqlBackslashes()
    {
        SqlRegex.Condition("c", Quoted, SqlDialect.PostgreSql).ShouldBe(@"c ~ '^it''s \d+$'");
        SqlRegex.Condition("c", Quoted, SqlDialect.MySql).ShouldBe(@"REGEXP_LIKE(c, '^it''s \\d+$', 'c')");
        SqlRegex.Condition("c", Quoted, SqlDialect.SqlServer2025).ShouldBe(@"REGEXP_LIKE(c, N'^it''s \d+$')");
    }

    [Fact]
    public void Condition_EmbedsRegexOptionsInline()
    {
        SqlRegex.Condition("c", TestPatterns.CaseInsensitiveSku, SqlDialect.PostgreSql).ShouldBe("c ~ '(?i)^[A-Z]{3}-[0-9]{4}$'");
    }

    [Fact]
    public void Condition_SqlServerWithoutExactLike_ThrowsWithGuidance()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => SqlRegex.Condition("[Email]", TestPatterns.Sku, SqlDialect.SqlServer));

        ex.Message.ShouldContain(TestPatterns.SkuRegex);
        ex.Message.ShouldContain("SqlDialect.SqlServer2025");
    }

    [Fact]
    public void Condition_RejectsBadArguments()
    {
        Should.Throw<ArgumentException>(() => SqlRegex.Condition(" ", TestPatterns.Sku, SqlDialect.PostgreSql));
        Should.Throw<ArgumentNullException>(() => SqlRegex.Condition("c", null!, SqlDialect.PostgreSql));
        Should.Throw<ArgumentOutOfRangeException>(() => SqlRegex.Condition("c", TestPatterns.Sku, (SqlDialect)42));
    }
}
