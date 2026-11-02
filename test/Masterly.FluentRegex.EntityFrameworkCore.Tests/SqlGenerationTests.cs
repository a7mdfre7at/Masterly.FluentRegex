using System.Linq.Expressions;
using System.Reflection;
using Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Shouldly;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

/// <summary>
/// SQL generated with ToQueryString() for providers that need a server, without connecting. The semantics of the
/// dialect regexes are verified against real servers by the container tests.
/// </summary>
public sealed class SqlGenerationTests
{
    private const string NpgsqlConnection = "Host=localhost;Database=fluentregex;Username=test;Password=test";
    private const string SqlServerConnection = "Server=localhost;Database=fluentregex;User Id=test;Password=test;TrustServerCertificate=True";
    private const string SqliteConnection = "DataSource=:memory:";
    private const string Replacement = "$2/${1}";

    private static CatalogContext Npgsql(Action<FluentRegexOptionsBuilder>? configure = null) =>
        new CatalogContext(new DbContextOptionsBuilder<CatalogContext>().UseNpgsql(NpgsqlConnection).UseFluentRegex(configure).Options);

    private static CatalogContext SqlServer(Action<SqlServerDbContextOptionsBuilder>? provider = null, Action<FluentRegexOptionsBuilder>? configure = null) =>
        new CatalogContext(new DbContextOptionsBuilder<CatalogContext>().UseSqlServer(SqlServerConnection, provider).UseFluentRegex(configure).Options);

    private static CatalogContext Sqlite(Action<FluentRegexOptionsBuilder>? configure = null) =>
        new CatalogContext(new DbContextOptionsBuilder<CatalogContext>().UseSqlite(SqliteConnection).UseFluentRegex(configure).Options);

    private static string ConstantMatch(CatalogContext context) =>
        context.Products.Where(product => TestPatterns.Sku.IsMatch(product.Name)).ToQueryString();

    private static string ParameterMatch(CatalogContext context)
    {
        Pattern local = TestPatterns.Sku;
        return context.Products.Where(product => local.IsMatch(product.Name)).ToQueryString();
    }

    private static string GroupByName(CatalogContext context) =>
        context.Products.Select(product => PatternFunctions.Group(product.Sku, TestPatterns.NamedSkuParts, "number")).ToQueryString();

    private static string Replace(CatalogContext context) =>
        context.Products.Select(product => EF.Functions.Replace(product.Sku, TestPatterns.SkuParts, Replacement)).ToQueryString();

    private static string HintedMatch(CatalogContext context) =>
        context.Products.WhereMatches(product => product.Sku, TestPatterns.AbcSku).ToQueryString();

    /// <summary>Builds the prefilter the way MatchingAsync does, with the pattern as a constant rather than a closure.</summary>
    private static string Prefilter(CatalogContext context, Pattern pattern)
    {
        ParameterExpression product = Expression.Parameter(typeof(Product), "product");
        MethodInfo isCandidate = typeof(PrefilterFunctions).GetMethod(nameof(PrefilterFunctions.IsCandidate), new[] { typeof(string), typeof(Pattern) })!; // the internal method always exists
        Expression call = Expression.Call(isCandidate, Expression.Property(product, nameof(Product.Sku)), Expression.Constant(pattern));
        return context.Products.Where(Expression.Lambda<Func<Product, bool>>(call, product)).ToQueryString();
    }

    // ------------------------------------------------------------------
    // PostgreSQL
    // ------------------------------------------------------------------

    [Fact]
    public void Npgsql_ConstantPattern_UsesTheTildeOperatorWithALiteral()
    {
        using CatalogContext context = Npgsql();

        string sql = ConstantMatch(context);

        sql.ShouldContain($" ~ '{TestPatterns.SkuPostgreSql}'");
    }

    [Fact]
    public void Npgsql_ParameterPattern_UsesTheTildeOperatorWithAParameter()
    {
        using CatalogContext context = Npgsql();

        string sql = ParameterMatch(context);

        sql.ShouldContain(" ~ @");
        sql.Substring(sql.IndexOf("SELECT", StringComparison.Ordinal)).ShouldNotContain(TestPatterns.SkuPostgreSql);
    }

    [Fact]
    public void Npgsql_GroupByName_UsesRegexpSubstrWithTheGroupNumber()
    {
        using CatalogContext context = Npgsql();

        string sql = GroupByName(context);

        sql.ShouldContain("regexp_substr(");
        sql.ShouldContain(", 1, 1, '', 2)");
    }

    [Fact]
    public void Npgsql_GroupByName_RemovesGroupNamesPostgreSqlCannotParse()
    {
        using CatalogContext context = Npgsql();

        string sql = GroupByName(context);

        sql.ShouldContain(@"'\A([A-Z]{3})-([0-9]{4})(?=\n?\Z)'");
    }

    [Fact]
    public void Npgsql_NullableColumn_GuardsNullSoNegationMatchesDotNet()
    {
        using CatalogContext context = Npgsql();

        string sql = context.Products.Where(product => PatternFunctions.Matches(product.Sku, TestPatterns.Sku)).ToQueryString();

        sql.ShouldContain("p.\"Sku\" IS NOT NULL");
    }

    [Fact]
    public void Npgsql_Replace_UsesRegexpReplaceWithBackslashReferencesAndTheGlobalFlag()
    {
        using CatalogContext context = Npgsql();

        string sql = Replace(context);

        sql.ShouldContain("regexp_replace(");
        sql.ShouldContain(@"'\2/\1', 'g')");
    }

    [Fact]
    public void Npgsql_IndexHints_AddPrefixLikeAndLengthConjuncts()
    {
        using CatalogContext context = Npgsql();

        string sql = HintedMatch(context);

        // The facts are proven by the core library: every match starts with "ABC-" and has at most 9 characters
        // (8, plus the final newline that $ accepts).
        sql.ShouldContain(" ~ '");
        sql.ShouldContain(@"LIKE 'ABC-%' ESCAPE '\'");
        sql.ShouldContain("length(");
        sql.ShouldContain("<= 9");
    }

    // ------------------------------------------------------------------
    // SQL Server
    // ------------------------------------------------------------------

    [Fact]
    public void SqlServer2025_ViaUseDialect_UsesRegexpLikeInsideIif()
    {
        using CatalogContext context = SqlServer(configure: fluent => fluent.UseDialect(SqlDialect.SqlServer2025));

        string sql = ConstantMatch(context);

        sql.ShouldContain("IIF(REGEXP_LIKE(");
        sql.ShouldContain($"N'{TestPatterns.SkuSqlServer}'");
        sql.ShouldContain(", 1, 0) = 1");
    }

    [Fact]
    public void SqlServer2025_ViaCompatibilityLevel170_IsDetected()
    {
        using CatalogContext context = SqlServer(provider => provider.UseCompatibilityLevel(170));

        string sql = ParameterMatch(context);

        sql.ShouldContain("REGEXP_LIKE(");
        sql.ShouldContain(", @");
    }

    [Fact]
    public void SqlServer2025_GroupByName_UsesRegexpSubstr()
    {
        using CatalogContext context = SqlServer(configure: fluent => fluent.UseDialect(SqlDialect.SqlServer2025));

        string sql = GroupByName(context);

        sql.ShouldContain("REGEXP_SUBSTR(");
        sql.ShouldContain(", 1, 1, 'c', 2)");
    }

    [Fact]
    public void SqlServer2025_Replace_UsesRegexpReplaceWithBackslashReferences()
    {
        using CatalogContext context = SqlServer(configure: fluent => fluent.UseDialect(SqlDialect.SqlServer2025));

        string sql = Replace(context);

        sql.ShouldContain("REGEXP_REPLACE(");
        sql.ShouldContain(@"N'\2/\1')");
    }

    [Fact]
    public void SqlServer2025_IndexHints_UseLenAndAUnicodePrefix()
    {
        using CatalogContext context = SqlServer(configure: fluent => fluent.UseDialect(SqlDialect.SqlServer2025));

        string sql = HintedMatch(context);

        sql.ShouldContain(@"LIKE N'ABC-%' ESCAPE N'\'");
        sql.ShouldContain("LEN(");
        sql.ShouldContain("<= 9");
    }

    [Fact]
    public void SqlServer_DefaultCompatibilityLevel_ExactOnlyThrowsTheDocumentedMessage()
    {
        using CatalogContext context = SqlServer();

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => ConstantMatch(context));

        ex.Message.ShouldContain(TestPatterns.SkuRegex);
        ex.Message.ShouldContain("MatchingAsync");
        ex.Message.ShouldContain("SqlDialect.SqlServer2025");
        ex.Message.ShouldContain("LikeFallback.AllowSuperset");
    }

    [Fact]
    public void SqlServer_CompatibilityLevel160_IsTheLikeFallbackDialect()
    {
        using CatalogContext context = SqlServer(provider => provider.UseCompatibilityLevel(160));

        Should.Throw<InvalidOperationException>(() => ConstantMatch(context)).Message.ShouldContain("MatchingAsync");
    }

    [Fact]
    public void SqlServer_AllowSuperset_EmitsTheProvenSupersetLike()
    {
        using CatalogContext context = SqlServer(configure: fluent => fluent.UseLikeFallback(LikeFallback.AllowSuperset));
        string expected = TestPatterns.Sku.ToLike(LikeDialect.SqlServer, MatchScope.Substring, LikeCollation.CaseInsensitive).Expression;

        string sql = context.Products.Where(product => PatternFunctions.Matches(product.Sku, TestPatterns.Sku)).ToQueryString();

        sql.ShouldContain($"[p].[Sku] LIKE N'{expected}'");
        sql.ShouldNotContain("REGEXP");
    }

    [Fact]
    public void SqlServer_BinaryCollation_EmitsTheProvenExactLikeUnderExactOnly()
    {
        using CatalogContext context = SqlServer(configure: fluent => fluent.UseLikeCollation(LikeCollation.Binary));

        string sql = context.Products.WhereMatches(product => product.Sku, TestPatterns.StrictSku).ToQueryString();

        sql.ShouldContain("[p].[Sku] LIKE N'[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]'");
        sql.ShouldNotContain("REGEXP");
    }

    [Fact]
    public void SqlServer_CaseInsensitiveCollation_CaseSensitivePatternIsNeverExact()
    {
        using CatalogContext context = SqlServer();

        Should.Throw<InvalidOperationException>(() => context.Products.WhereMatches(product => product.Sku, TestPatterns.StrictSku).ToQueryString());
    }

    [Fact]
    public void SqlServer_MatchingAsyncPrefilter_UsesTheSupersetLikeAndTheLengthBound()
    {
        using CatalogContext context = SqlServer();
        string expected = TestPatterns.Sku.ToLike(LikeDialect.SqlServer, MatchScope.Substring, LikeCollation.CaseInsensitive).Expression;

        string sql = Prefilter(context, TestPatterns.Sku);

        sql.ShouldContain($"LIKE N'{expected}'", customMessage: sql);
        sql.ShouldContain("LEN(");
        sql.ShouldContain("<= 9");
    }

    [Fact]
    public void Npgsql_MatchingAsyncPrefilter_ForAnAtomicGroup_UsesTheSupersetLike()
    {
        // PostgreSQL has no atomic groups, so the regex stays in .NET; the atomic group is outside the provable
        // subset, so the LIKE is a superset by construction ('ID-_%') with no length bound.
        using CatalogContext context = Npgsql();
        Pattern atomic = Pattern.FromRegex(@"^ID-(?>[0-9]+)\z");

        string sql = Prefilter(context, atomic);

        sql.ShouldContain("LIKE 'ID-_%'", customMessage: sql);
        sql.ShouldNotContain(" ~ ");
    }

    [Fact]
    public void SqlServer_Never_Throws()
    {
        using CatalogContext context = SqlServer(configure: fluent => fluent.UseLikeFallback(LikeFallback.Never));

        Should.Throw<InvalidOperationException>(() => ConstantMatch(context)).Message.ShouldContain("LikeFallback.Never");
    }

    [Fact]
    public void SqlServer_ParameterPattern_ThrowsWithGuidance()
    {
        using CatalogContext context = SqlServer(configure: fluent => fluent.UseLikeFallback(LikeFallback.AllowSuperset));

        Should.Throw<InvalidOperationException>(() => ParameterMatch(context)).Message.ShouldContain("static readonly");
    }

    [Fact]
    public void SqlServer_Group_ThrowsNotSupported()
    {
        using CatalogContext context = SqlServer();

        Should.Throw<NotSupportedException>(() => GroupByName(context)).Message.ShouldContain("SQL Server 2025");
    }

    // ------------------------------------------------------------------
    // SQLite
    // ------------------------------------------------------------------

    [Fact]
    public void Sqlite_ConstantPattern_UsesTheRegexpOperator()
    {
        using CatalogContext context = Sqlite();

        string sql = ConstantMatch(context);

        sql.ShouldContain($" REGEXP '{TestPatterns.SkuRegex}'");
    }

    [Fact]
    public void Sqlite_WithPostgreSqlDialect_FallsBackToTheRegexpLikeFunction()
    {
        // The SQLite provider does not expose Npgsql's operator expression, so the function form is used.
        using CatalogContext context = Sqlite(fluent => fluent.UseDialect(SqlDialect.PostgreSql));

        string sql = ConstantMatch(context);

        sql.ShouldContain("regexp_like(");
    }

    // ------------------------------------------------------------------
    // Errors shared by every dialect
    // ------------------------------------------------------------------

    [Fact]
    public void Replace_WholeMatchSubstitution_ThrowsNotSupported()
    {
        using CatalogContext context = Npgsql();

        Should.Throw<NotSupportedException>(
            () => context.Products.Select(product => PatternFunctions.Replace(product.Sku, TestPatterns.SkuParts, "[$&]")).ToQueryString());
    }

    [Fact]
    public void Replace_UnknownNamedGroup_ThrowsArgumentException()
    {
        using CatalogContext context = Npgsql();

        Should.Throw<ArgumentException>(
            () => context.Products.Select(product => PatternFunctions.Replace(product.Sku, TestPatterns.SkuParts, "${missing}")).ToQueryString());
    }

    [Fact]
    public void GroupByName_ParameterPattern_ThrowsWithGuidance()
    {
        using CatalogContext context = Npgsql();
        Pattern local = TestPatterns.NamedSkuParts;

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => context.Products.Select(product => PatternFunctions.Group(product.Sku, local, "number")).ToQueryString());

        ex.Message.ShouldContain("static readonly");
    }

    [Fact]
    public void GroupByNumber_ParameterPattern_IsTranslated()
    {
        using CatalogContext context = Npgsql();
        Pattern local = TestPatterns.SkuParts;

        string sql = context.Products.Select(product => PatternFunctions.Group(product.Sku, local, 2)).ToQueryString();

        sql.ShouldContain("regexp_substr(");
        sql.ShouldContain(", @");
    }

    [Fact]
    public void UnsupportedRegexOption_ThrowsNotSupportedNamingTheOption()
    {
        using CatalogContext context = Npgsql();
        Pattern rightToLeft = TestPatterns.Sku.WithOptions(System.Text.RegularExpressions.RegexOptions.RightToLeft);

        Should.Throw<NotSupportedException>(() => context.Products.WhereMatches(product => product.Sku, rightToLeft).ToQueryString())
            .Message.ShouldContain("RightToLeft");
    }
}
