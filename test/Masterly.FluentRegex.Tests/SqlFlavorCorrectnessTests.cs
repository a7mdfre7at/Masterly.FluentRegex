using System.Globalization;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

/// <summary>
/// Regressions for SQL translations that were accepted as exact but meant something else on the real
/// engines (each case was reproduced on PostgreSQL 17, MySQL 8.4, MariaDB 11.4 or SQL Server 2025).
/// </summary>
public class SqlFlavorCorrectnessTests
{
    private const string CaseSensitivePrefix = "(?-i)";
    private const int SqlServerPatternLimit = 4000;

    // ------------------------------------------------------------------
    // SQL Server: national literals and the 4,000-character pattern limit
    // ------------------------------------------------------------------

    [Fact]
    public void ToSqlCondition_SqlServer_WritesANationalLiteral()
    {
        string condition = Pattern.FromRegex("^a$").ToSqlCondition("s", RegexFlavor.SqlServer);

        condition.ShouldBe("REGEXP_LIKE(s, N'\\Aa\\n?\\z')");
    }

    [Fact]
    public void ToFlavor_SqlServer_PatternOfExactlyTheLimit_IsKept()
    {
        Pattern pattern = Pattern.FromRegex(new string('x', SqlServerPatternLimit));

        FlavorConversion conversion = pattern.ToFlavor(RegexFlavor.SqlServer);

        conversion.Pattern.Length.ShouldBe(SqlServerPatternLimit);
        conversion.ToLiteral().ShouldStartWith("N'xxx");
    }

    [Theory]
    [InlineData("long literal")]
    [InlineData("class subtraction")]
    [InlineData("unassigned category")]
    public void ToFlavor_SqlServer_PatternLongerThanTheLimit_ThrowsInsteadOfBeingTruncated(string kind)
    {
        Pattern pattern = Pattern.FromRegex(LongPatterns[kind]);

        NotSupportedException exception = Should.Throw<NotSupportedException>(() => pattern.ToFlavor(RegexFlavor.SqlServer));

        exception.Message.ShouldContain("SQL Server accepts at most 4000");
    }

    private static readonly Dictionary<string, string> LongPatterns = new Dictionary<string, string>
    {
        ["long literal"] = "^" + new string('x', 4100) + "$",
        ["class subtraction"] = @"[\p{L}-[a-z]]",
        ["unassigned category"] = @"\p{Cn}",
    };

    [Fact]
    public void ToFlavor_PostgreSql_LongPattern_HasNoLengthLimit()
    {
        Pattern pattern = Pattern.FromRegex(new string('x', 5000));

        FlavorConversion conversion = pattern.ToFlavor(RegexFlavor.PostgreSql);

        conversion.Pattern.Length.ShouldBe(5000);
    }

    // ------------------------------------------------------------------
    // Classes that look like POSIX classes or collating elements
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("^[:alpha:]$", "\\A[\\:alpha\\:](?=\\n?\\z)")]
    [InlineData("^[a:b]$", "\\A[a\\:b](?=\\n?\\z)")]
    [InlineData("^[=a=]$", "\\A[\\=a\\=](?=\\n?\\z)")]
    [InlineData("^[.a.]$", "\\A[\\.a\\.](?=\\n?\\z)")]
    [InlineData("^[[:alpha:]]$", "\\A[\\[\\:alpha\\:]\\](?=\\n?\\z)")]
    public void ToFlavor_MySql_ColonEqualsAndDotInAClass_AreEscaped(string regex, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.MySql);

        conversion.Pattern.ShouldBe(CaseSensitivePrefix + expected);
        conversion.IsExact.ShouldBeTrue();
    }

    [Theory]
    [InlineData(RegexFlavor.PostgreSql, "[\\:ahlp]")]
    [InlineData(RegexFlavor.MariaDb, "[\\:alpha\\:]")]
    [InlineData(RegexFlavor.SqlServer, "[\\:alpha\\:]")]
    public void ToFlavor_ClassStartingWithAColon_IsNeverAPosixClass(RegexFlavor flavor, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex("[:alpha:]").ToFlavor(flavor);

        conversion.Pattern.ShouldEndWith(expected);
    }

    // ------------------------------------------------------------------
    // Backreferences: ECMAScript's empty match and PostgreSQL's per-iteration reset
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(RegexFlavor.PostgreSql, @"^(a)?\1b$")]
    [InlineData(RegexFlavor.MySql, @"^(a)?\1b$")]
    [InlineData(RegexFlavor.MariaDb, @"^(?:(a)|b)\1b$")]
    public void ToFlavor_EcmaScriptBackreferenceToAGroupThatMayNotParticipate_ThrowsNotSupported(RegexFlavor flavor, string regex)
    {
        Pattern pattern = Pattern.FromRegex(regex, RegexOptions.ECMAScript);

        NotSupportedException exception = Should.Throw<NotSupportedException>(() => pattern.ToFlavor(flavor));

        exception.Message.ShouldContain("may not have participated, under RegexOptions.ECMAScript");
    }

    [Fact]
    public void ToFlavor_EcmaScriptBackreferenceToAGroupThatAlwaysParticipates_IsExact()
    {
        Pattern pattern = Pattern.FromRegex(@"^(a)\1b$", RegexOptions.ECMAScript);

        FlavorConversion conversion = pattern.ToFlavor(RegexFlavor.PostgreSql);

        conversion.Pattern.ShouldBe(@"\A(a)\1b(?=\n?\Z)");
        conversion.IsExact.ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"^(?:(a)|(b))+\1\2$", 13)]
    [InlineData(@"^(?:(a)?b)+\1$", 11)]
    [InlineData(@"^(?:(a)|b\1)+$", 9)]
    [InlineData(@"^(?:(?:(a))*c)+\1$", 15)]
    public void ToFlavor_PostgreSql_BackreferenceToAGroupAnIterationCanSkip_ThrowsNotSupported(string regex, int position)
    {
        Pattern pattern = Pattern.FromRegex(regex);

        NotSupportedException exception = Should.Throw<NotSupportedException>(() => pattern.ToFlavor(RegexFlavor.PostgreSql));

        exception.Message.ShouldContain($"resets the group on every iteration; .NET keeps the earlier capture) at position {position}");
    }

    [Theory]
    [InlineData(@"^(?:(a)b)+\1$")]
    [InlineData(@"^(a)*\1$")]
    [InlineData(@"^(a)?(?:b\1)+$")]
    public void ToFlavor_PostgreSql_BackreferenceToAGroupEveryIterationSets_IsExact(string regex)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex).ToFlavor(RegexFlavor.PostgreSql);

        conversion.IsExact.ShouldBeTrue();
    }

    [Theory]
    [InlineData(RegexFlavor.MySql)]
    [InlineData(RegexFlavor.MariaDb)]
    public void ToFlavor_BacktrackingEngine_KeepsCapturesAcrossIterationsLikeDotNet(RegexFlavor flavor)
    {
        FlavorConversion conversion = Pattern.FromRegex(@"^(?:(a)|(b))+\1\2$").ToFlavor(flavor);

        conversion.IsExact.ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    // IgnoreCase follows the culture .NET's Regex would use
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("en-US", RegexOptions.IgnoreCase, "\\A[Ii\\u0130](?=\\n?\\Z)")]
    [InlineData("tr-TR", RegexOptions.IgnoreCase, "\\A[i\\u0130](?=\\n?\\Z)")]
    [InlineData("az-Latn-AZ", RegexOptions.IgnoreCase, "\\A[i\\u0130](?=\\n?\\Z)")]
    [InlineData("", RegexOptions.IgnoreCase, "\\A[Ii](?=\\n?\\Z)")]
    [InlineData("tr-TR", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "\\A[Ii](?=\\n?\\Z)")]
    [InlineData("en-US", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "\\A[Ii](?=\\n?\\Z)")]
    public void ToFlavor_PostgreSql_IgnoreCase_UsesTheCurrentCultureUnlessCultureInvariant(string culture, RegexOptions options, string expected)
    {
        Pattern pattern = Pattern.FromRegex("^i$", options);

        string translated = InCulture(culture, () => pattern.ToFlavor(RegexFlavor.PostgreSql).Pattern);

        translated.ShouldBe(expected);
    }

    [Theory]
    [InlineData("en-US", "I", true)]
    [InlineData("tr-TR", "I", false)]
    [InlineData("tr-TR", "İ", true)]
    public void IsMatch_IgnoreCase_UsesTheSameCultureRuleAsTheTranslation(string culture, string input, bool expected)
    {
        bool matches = InCulture(culture, () => Pattern.FromRegex("^i$", RegexOptions.IgnoreCase).IsMatch(input));

        matches.ShouldBe(expected);
    }

    [Theory]
    [InlineData("abc", RegexOptions.CultureInvariant, "abc")]
    [InlineData("abc", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, "(?i)abc")]
    [InlineData("[a-h]x.", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, "(?i)[a-h]x.")]
    public void ToFlavor_Sqlite_CultureInvariantThatCannotChangeTheMeaning_IsDropped(string regex, RegexOptions options, string expected)
    {
        FlavorConversion conversion = Pattern.FromRegex(regex, options).ToFlavor(RegexFlavor.Sqlite);

        conversion.Pattern.ShouldBe(expected);
        conversion.IsExact.ShouldBeTrue();
    }

    [Theory]
    [InlineData("i")]
    [InlineData("[a-z]")]
    [InlineData("[^x]")]
    public void ToFlavor_Sqlite_CultureInvariantWithCultureDependentLetters_ThrowsNotSupported(string regex)
    {
        Pattern pattern = Pattern.FromRegex(regex, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        Should.Throw<NotSupportedException>(() => pattern.ToFlavor(RegexFlavor.Sqlite)).Message.ShouldContain("CultureInvariant");
    }

    private static T InCulture<T>(string culture, Func<T> action)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
