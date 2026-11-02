using Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;
using Shouldly;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

public sealed class ReplacementConverterTests
{
    // Shaped like FlavorConversion.GroupNumbers: names and .NET numbers, never group 0 (the whole match).
    private static readonly IReadOnlyDictionary<string, int> Groups = new Dictionary<string, int>
    {
        ["1"] = 1,
        ["2"] = 2,
        ["number"] = 2
    };

    // (?<year>\d{4})-(\d{2}): .NET numbers the unnamed group 1 and "year" 2; the engines number left to right.
    private static readonly IReadOnlyDictionary<string, int> Renumbered = new Dictionary<string, int>
    {
        ["year"] = 1,
        ["2"] = 1,
        ["1"] = 2
    };

    [Theory]
    [InlineData(SqlDialect.PostgreSql, "$2/$1", @"\2/\1")]
    [InlineData(SqlDialect.MariaDb, "$2/$1", @"\2/\1")]
    [InlineData(SqlDialect.SqlServer2025, "$2/$1", @"\2/\1")]
    [InlineData(SqlDialect.MySql, "$2/$1", "$2/$1")]
    [InlineData(SqlDialect.PostgreSql, "${number}-${1}", @"\2-\1")]
    [InlineData(SqlDialect.PostgreSql, "$$1", "$1")]
    [InlineData(SqlDialect.MySql, "$$1", @"\$1")]
    [InlineData(SqlDialect.PostgreSql, @"a\b", @"a\\b")]
    [InlineData(SqlDialect.MySql, @"a\b", @"a\\b")]
    [InlineData(SqlDialect.PostgreSql, "$12", @"\12")]
    [InlineData(SqlDialect.PostgreSql, "$x", "$x")]
    [InlineData(SqlDialect.PostgreSql, "$0", @"\&")]
    [InlineData(SqlDialect.SqlServer2025, "$0", @"\0")]
    [InlineData(SqlDialect.MySql, "$0", "$0")]
    public void Convert_RewritesDotNetSubstitutions(SqlDialect dialect, string replacement, string expected)
    {
        ReplacementConverter.Convert(replacement, dialect, Groups).ShouldBe(expected);
    }

    [Fact]
    public void Convert_UnknownNumberedGroup_IsLiteralLikeDotNet()
    {
        ReplacementConverter.Convert("$7", SqlDialect.PostgreSql, Groups).ShouldBe("$7");
    }

    [Fact]
    public void Convert_DotNetNumbers_AreMappedToTheDialectNumbers()
    {
        ReplacementConverter.Convert("$1|${1}|$2|${year}", SqlDialect.PostgreSql, Renumbered).ShouldBe(@"\2|\2|\1|\1");
    }

    [Fact]
    public void Convert_WithoutGroupKnowledge_AcceptsOnlyTheWholeMatch()
    {
        ReplacementConverter.Convert("[$0]", SqlDialect.PostgreSql, null).ShouldBe(@"[\&]");
        Should.Throw<InvalidOperationException>(() => ReplacementConverter.Convert("$1", SqlDialect.PostgreSql, null)).Message.ShouldContain("static readonly");
        Should.Throw<InvalidOperationException>(() => ReplacementConverter.Convert("${number}", SqlDialect.PostgreSql, null)).Message.ShouldContain("static readonly");
    }

    [Theory]
    [InlineData("$&")]
    [InlineData("$`")]
    [InlineData("$'")]
    [InlineData("$+")]
    [InlineData("$_")]
    public void Convert_UnsupportedSubstitution_ThrowsNotSupported(string replacement)
    {
        Should.Throw<NotSupportedException>(() => ReplacementConverter.Convert(replacement, SqlDialect.PostgreSql, Groups));
    }

    [Fact]
    public void Convert_GroupAboveNine_ThrowsOnBackslashDialects()
    {
        Dictionary<string, int> many = new Dictionary<string, int> { ["12"] = 12 };

        Should.Throw<NotSupportedException>(() => ReplacementConverter.Convert("$12", SqlDialect.PostgreSql, many));
        ReplacementConverter.Convert("$12", SqlDialect.MySql, many).ShouldBe("$12");
    }

    [Fact]
    public void Convert_WholeMatchOnMariaDb_ThrowsNotSupported()
    {
        Should.Throw<NotSupportedException>(() => ReplacementConverter.Convert("$0", SqlDialect.MariaDb, Groups));
    }

    [Fact]
    public void Convert_UnknownNamedGroup_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => ReplacementConverter.Convert("${missing}", SqlDialect.PostgreSql, Groups));
    }
}
