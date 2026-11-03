using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Translates <see cref="Pattern.IsMatch(string)"/>, <see cref="PatternFunctions"/>,
/// <see cref="FluentRegexDbFunctionsExtensions"/> and the <c>MatchingAsync</c> prefilter to SQL.
/// </summary>
internal sealed class FluentRegexMethodCallTranslator : IMethodCallTranslator
{
    private const string LikeEscape = "\\";
    private const string LikeWildcard = "%";
    private const string LikeSpecialCharacters = "\\%_[";
    private const string PostgreSqlRegexLike = "regexp_like";
    private const string PostgreSqlRegexSubstr = "regexp_substr";
    private const string PostgreSqlRegexReplace = "regexp_replace";
    private const string PostgreSqlGlobalFlag = "g";
    private const string PostgreSqlNoFlags = "";
    private const string RegexLike = "REGEXP_LIKE";
    private const string RegexSubstr = "REGEXP_SUBSTR";
    private const string RegexReplace = "REGEXP_REPLACE";
    private const string CaseSensitiveFlag = "c";
    private const string FlagsStoreType = "varchar(8)";
    private const string SqlServerIif = "IIF";
    private const string SqliteRegexp = "regexp";
    private const string CharLength = "CHAR_LENGTH";
    private const string Len = "LEN";
    private const string Length = "length";
    private const int MatchStart = 1;
    private const int FirstOccurrence = 1;
    private const int True = 1;
    private const int False = 0;
    private const int WholeMatchNumber = 0;
    private const string WholeMatchGroup = "0";

    private static readonly MethodInfo PatternIsMatch = ReflectionHelper.GetMethod(typeof(Pattern), nameof(Pattern.IsMatch), typeof(string));
    private static readonly MethodInfo ConstrainedIsMatch = ReflectionHelper.GetMethod(typeof(ConstrainedPattern), nameof(ConstrainedPattern.IsMatch), typeof(string));
    private static readonly MethodInfo FunctionsMatches = ReflectionHelper.GetMethod(typeof(PatternFunctions), nameof(PatternFunctions.Matches), typeof(string), typeof(Pattern));
    private static readonly MethodInfo FunctionsGroupByName = ReflectionHelper.GetMethod(typeof(PatternFunctions), nameof(PatternFunctions.Group), typeof(string), typeof(Pattern), typeof(string));
    private static readonly MethodInfo FunctionsGroupByNumber = ReflectionHelper.GetMethod(typeof(PatternFunctions), nameof(PatternFunctions.Group), typeof(string), typeof(Pattern), typeof(int));
    private static readonly MethodInfo FunctionsReplace = ReflectionHelper.GetMethod(typeof(PatternFunctions), nameof(PatternFunctions.Replace), typeof(string), typeof(Pattern), typeof(string));
    private static readonly MethodInfo DbFunctionsMatches = ReflectionHelper.GetMethod(typeof(FluentRegexDbFunctionsExtensions), nameof(FluentRegexDbFunctionsExtensions.Matches), typeof(DbFunctions), typeof(string), typeof(Pattern));
    private static readonly MethodInfo DbFunctionsGroupByName = ReflectionHelper.GetMethod(typeof(FluentRegexDbFunctionsExtensions), nameof(FluentRegexDbFunctionsExtensions.Group), typeof(DbFunctions), typeof(string), typeof(Pattern), typeof(string));
    private static readonly MethodInfo DbFunctionsGroupByNumber = ReflectionHelper.GetMethod(typeof(FluentRegexDbFunctionsExtensions), nameof(FluentRegexDbFunctionsExtensions.Group), typeof(DbFunctions), typeof(string), typeof(Pattern), typeof(int));
    private static readonly MethodInfo DbFunctionsReplace = ReflectionHelper.GetMethod(typeof(FluentRegexDbFunctionsExtensions), nameof(FluentRegexDbFunctionsExtensions.Replace), typeof(DbFunctions), typeof(string), typeof(Pattern), typeof(string));
    private static readonly MethodInfo PrefilterIsCandidate = ReflectionHelper.GetMethod(typeof(PrefilterFunctions), nameof(PrefilterFunctions.IsCandidate), typeof(string), typeof(Pattern));

    private static readonly bool[] TwoNullableArguments = { true, true };
    private static readonly bool[] ThreeNullableArguments = { true, true, true };
    private static readonly bool[] OneNullableArgument = { true };

    private readonly ISqlExpressionFactory _sql;
    private readonly IFluentRegexSingletonOptions _options;
    private readonly RelationalTypeMapping _stringMapping;
    private readonly RelationalTypeMapping _literalMapping;
    private readonly RelationalTypeMapping _boolMapping;
    private readonly RelationalTypeMapping _intMapping;
    private readonly RelationalTypeMapping _flagsMapping;
    private readonly ProviderRegexExpressionFactory _providerExpressions;
    private readonly FluentRegexLogger _logger = new FluentRegexLogger();
    private RelationalTypeMapping? _patternParameterMapping;

    /// <summary>Creates the translator.</summary>
    public FluentRegexMethodCallTranslator(
        ISqlExpressionFactory sqlExpressionFactory,
        IRelationalTypeMappingSource typeMappingSource,
        IFluentRegexSingletonOptions options)
    {
        ArgumentNullException.ThrowIfNull(sqlExpressionFactory);
        ArgumentNullException.ThrowIfNull(typeMappingSource);
        ArgumentNullException.ThrowIfNull(options);

        _sql = sqlExpressionFactory;
        _options = options;
        _stringMapping = RequireMapping(typeMappingSource, typeof(string));

        // Every string literal this translator writes goes through _literalMapping (see MySqlEscapingStringTypeMapping).
        _literalMapping = options.ProviderLeavesBackslashesUnescaped ? new MySqlEscapingStringTypeMapping(_stringMapping) : _stringMapping;
        _boolMapping = RequireMapping(typeMappingSource, typeof(bool));
        _intMapping = RequireMapping(typeMappingSource, typeof(int));

        // SQL Server rejects an nvarchar flags argument (N'c') for REGEXP_SUBSTR, so flags are non-unicode literals.
        _flagsMapping = typeMappingSource.FindMapping(typeof(string), FlagsStoreType) ?? _stringMapping;
        _providerExpressions = new ProviderRegexExpressionFactory(options.ProviderAssembly);
    }

    /// <inheritdoc />
    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(logger);

        if (method == PatternIsMatch)
        {
            return instance == null ? null : TranslateMatches(arguments[0], instance, logger);
        }

        if (method == ConstrainedIsMatch)
        {
            throw new InvalidOperationException(
                "A ConstrainedPattern cannot be translated to SQL because its Must rules run only in .NET. " +
                "Query with the underlying pattern (constrained.Pattern), or use MatchingAsync(selector, constrained), " +
                "which applies a proven SQL prefilter and then checks every rule in .NET.");
        }

        if (method == FunctionsMatches)
        {
            return TranslateMatches(arguments[0], arguments[1], logger);
        }

        if (method == DbFunctionsMatches)
        {
            return TranslateMatches(arguments[1], arguments[2], logger);
        }

        if (method == FunctionsGroupByName || method == FunctionsGroupByNumber)
        {
            return TranslateGroup(arguments[0], arguments[1], arguments[2], logger);
        }

        if (method == DbFunctionsGroupByName || method == DbFunctionsGroupByNumber)
        {
            return TranslateGroup(arguments[1], arguments[2], arguments[3], logger);
        }

        if (method == FunctionsReplace)
        {
            return TranslateReplace(arguments[0], arguments[1], arguments[2], logger);
        }

        if (method == DbFunctionsReplace)
        {
            return TranslateReplace(arguments[1], arguments[2], arguments[3], logger);
        }

        if (method == PrefilterIsCandidate)
        {
            return TranslateCandidate(arguments[0], arguments[1], logger);
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Matches
    // ------------------------------------------------------------------

    private SqlExpression? TranslateMatches(SqlExpression value, SqlExpression patternExpression, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        PatternOperand? operand = PatternOperand.From(patternExpression);
        if (operand == null)
        {
            return null;
        }

        if (operand.Constant is Pattern pattern)
        {
            return NullIsNoMatch(value, TranslateConstantMatches(value, pattern, logger));
        }

        if (_options.Dialect == SqlDialect.SqlServer)
        {
            throw new InvalidOperationException(
                "A pattern held in a variable cannot be translated on SQL Server without regular expression support, " +
                "because its LIKE translation must be checked when the query is compiled. Reference the pattern through " +
                "a static readonly field or build it inline in the query, use MatchingAsync, or configure " +
                "UseDialect(SqlDialect.SqlServer2025).");
        }

        return NullIsNoMatch(value, RegexPredicate(value, RegexParameter(operand.RequireParameter())));
    }

    /// <summary>
    /// Makes a NULL value a definite non-match, as <see cref="PatternFunctions.Matches"/> is in .NET. Without the
    /// guard the predicate is NULL for NULL, and whether a negation keeps the row would depend on how EF Core
    /// rewrites the conjunction, so adding index hints could change results. EF Core removes the guard for
    /// non-nullable columns.
    /// </summary>
    private SqlExpression NullIsNoMatch(SqlExpression value, SqlExpression predicate) =>
        _sql.AndAlso(_sql.IsNotNull(value), predicate);

    private SqlExpression TranslateConstantMatches(SqlExpression value, Pattern pattern, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        CoreDialectAdapter.RequireCultureIndependent(pattern);
        SqlDialect dialect = _options.Dialect;
        if (dialect == SqlDialect.SqlServer)
        {
            return TranslateLikeFallback(value, pattern, logger);
        }

        DialectRegex regex = CoreDialectAdapter.ToDialectRegex(pattern, dialect);
        _logger.TranslationWarnings(logger, pattern, dialect, regex);
        SqlExpression predicate = RegexPredicate(value, RegexConstant(regex.RegexText));
        return _options.IndexHints ? WithIndexHints(predicate, value, pattern) : predicate;
    }

    private SqlExpression TranslateLikeFallback(SqlExpression value, Pattern pattern, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (_options.LikeFallback == LikeFallback.Never)
        {
            throw new InvalidOperationException(
                $"The pattern '{pattern}' cannot be translated: SQL Server before 2025 has no regular expression support " +
                "and UseLikeFallback(LikeFallback.Never) is configured. Use MatchingAsync, or configure " +
                "UseDialect(SqlDialect.SqlServer2025) when the database runs at compatibility level 170 or later.");
        }

        DialectLike like = CoreDialectAdapter.ToDialectLike(pattern, SqlDialect.SqlServer, substring: true, caseInsensitiveCollation: false);
        if (like.IsExact)
        {
            return LikePredicate(BinaryLikeOperand(value), like);
        }

        if (_options.LikeFallback == LikeFallback.AllowSuperset && like.IsSuperset)
        {
            _logger.LikeSupersetFallback(logger, pattern, like.Expression);
            return LikePredicate(BinaryLikeOperand(value), like);
        }

        throw new InvalidOperationException(
            $"The pattern '{pattern}' cannot be translated exactly to LIKE, and SQL Server before 2025 has no regular " +
            "expression support. Use MatchingAsync or ToMatchingListAsync to combine a proven SQL prefilter with an exact " +
            ".NET check, configure UseDialect(SqlDialect.SqlServer2025) when the database runs at compatibility level 170 " +
            "or later, or configure UseLikeFallback(LikeFallback.AllowSuperset) to accept a LIKE that can return extra rows.");
    }

    // ------------------------------------------------------------------
    // MatchingAsync prefilter
    // ------------------------------------------------------------------

    private SqlExpression? TranslateCandidate(SqlExpression value, SqlExpression patternExpression, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        PatternOperand? operand = PatternOperand.From(patternExpression);
        if (operand == null)
        {
            return null;
        }

        SqlExpression notNull = _sql.IsNotNull(value);
        if (operand.Constant is not Pattern pattern)
        {
            // Nothing is known about a parameter at compile time; NULL exclusion is the only sound filter.
            return notNull;
        }

        if (CoreDialectAdapter.TranslationDependsOnCulture(pattern))
        {
            // The compiled SQL is reused on threads of every culture, so only culture-free facts may narrow the rows;
            // the .NET check that follows applies the pattern under the caller's culture.
            return WithMaxLength(notNull, value, pattern);
        }

        SqlDialect dialect = _options.Dialect;
        DialectRegex? regex = dialect == SqlDialect.SqlServer ? null : TryDialectRegex(pattern, dialect);
        if (regex != null && regex.IsExact)
        {
            _logger.TranslationWarnings(logger, pattern, dialect, regex);
            SqlExpression predicate = RegexPredicate(value, RegexConstant(regex.RegexText));
            return _options.IndexHints ? WithIndexHints(predicate, value, pattern) : predicate;
        }

        // Compiled for a binary collation: without sets (every dialect but SQL Server) that stays a superset under a
        // case- or accent-insensitive collation, which only accepts more, and SQL Server compares in BIN2.
        SqlExpression result = notNull;
        DialectLike like = CoreDialectAdapter.ToDialectLike(pattern, dialect, substring: true, caseInsensitiveCollation: false);
        if (!string.Equals(like.Expression, LikeWildcard, StringComparison.Ordinal))
        {
            result = _sql.AndAlso(result, LikePredicate(BinaryLikeOperand(value), like));
        }

        return WithMaxLength(result, value, pattern);
    }

    /// <summary>
    /// Makes a SQL Server LIKE compare UTF-16 code units, as .NET does, unless the column is declared binary.
    /// SQL Server's linguistic collations do not compare LIKE character by character: in the version-80 collations,
    /// including the server default SQL_Latin1_General_CP1_CI_AS, a combining mark joins the letter before it, so
    /// <c>N'abc' + NCHAR(0x301) LIKE N'abc%'</c> is false although .NET's <c>^abc</c> matches; Czech collations treat
    /// <c>ch</c> as one letter; ignorable characters such as a soft hyphen are skipped. Under
    /// <c>COLLATE Latin1_General_100_BIN2</c> none of that happens (verified on SQL Server 2025), at the price of an
    /// index seek. The other dialects compare LIKE character by character under every collation.
    /// </summary>
    private SqlExpression BinaryLikeOperand(SqlExpression value) =>
        SqlServerCollation.NeedsBinaryCollate(_options.Dialect, _options.LikeCollation)
            ? new CollateExpression(value, SqlServerCollation.Binary)
            : value;

    /// <summary>
    /// Adds the proven upper length bound, when there is one. It holds under every culture (case variants of a
    /// letter have the same UTF-16 length) and every SQL length function (none counts more units than .NET).
    /// </summary>
    private SqlExpression WithMaxLength(SqlExpression predicate, SqlExpression value, Pattern pattern)
    {
        DialectFacts facts = CoreDialectAdapter.GetFacts(pattern, substring: true);
        return facts.MaxLength is int maxLength ? _sql.AndAlso(predicate, MaxLengthPredicate(value, maxLength)) : predicate;
    }

    /// <summary>
    /// Gets a value indicating whether a LIKE on the column itself, without <c>COLLATE</c>, compares character by
    /// character as the proofs assume: always on PostgreSQL, MySQL, MariaDB and SQLite, and on SQL Server only when
    /// the column is declared binary with <c>UseLikeCollation(LikeCollation.Binary)</c>. See <see cref="BinaryLikeOperand"/>.
    /// </summary>
    private bool ColumnComparesLikeByCodeUnit(SqlDialect dialect) => !SqlServerCollation.NeedsBinaryCollate(dialect, _options.LikeCollation);

    private static DialectRegex? TryDialectRegex(Pattern pattern, SqlDialect dialect)
    {
        try
        {
            return CoreDialectAdapter.ToDialectRegex(pattern, dialect);
        }
        catch (NotSupportedException)
        {
            // The dialect cannot express the pattern; the prefilter falls back to LIKE and length facts.
            return null;
        }
    }

    // ------------------------------------------------------------------
    // Group
    // ------------------------------------------------------------------

    private SqlExpression? TranslateGroup(SqlExpression value, SqlExpression patternExpression, SqlExpression groupExpression, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        PatternOperand? operand = PatternOperand.From(patternExpression);
        if (operand == null)
        {
            return null;
        }

        SqlDialect dialect = _options.Dialect;
        if (dialect != SqlDialect.PostgreSql && dialect != SqlDialect.SqlServer2025)
        {
            throw new NotSupportedException(
                $"Capture groups cannot be extracted in SQL on {dialect}. Group is translated on PostgreSQL (regexp_substr) " +
                "and SQL Server 2025 (REGEXP_SUBSTR) only; load the value and use Pattern.Match in .NET instead.");
        }

        DialectRegex regex = ResolveExtentPreservingRegex(operand, dialect, nameof(PatternFunctions.Group), logger);
        SqlExpression regexSql = RegexConstant(regex.RegexText);
        SqlExpression groupNumber = GroupNumber(groupExpression, regex, (Pattern)operand.Constant!); // ResolveExtentPreservingRegex requires a constant pattern
        RelationalTypeMapping resultMapping = value.TypeMapping ?? _stringMapping;
        SqlExpression[] arguments =
        {
            value,
            regexSql,
            _sql.Constant(MatchStart, _intMapping),
            _sql.Constant(FirstOccurrence, _intMapping),
            _sql.Constant(dialect == SqlDialect.PostgreSql ? PostgreSqlNoFlags : CaseSensitiveFlag, _flagsMapping),
            groupNumber
        };
        bool[] propagateNullability = { true, true, false, false, false, false };
        string function = dialect == SqlDialect.PostgreSql ? PostgreSqlRegexSubstr : RegexSubstr;
        return _sql.Function(function, arguments, nullable: true, propagateNullability, typeof(string), resultMapping);
    }

    /// <summary>
    /// Maps a .NET group reference to the dialect's group number. The numbers differ: .NET numbers unnamed groups
    /// before named ones, every engine numbers left to right, and PostgreSQL drops groups inside lookarounds.
    /// Group 0, the whole match, is the same everywhere.
    /// </summary>
    private SqlExpression GroupNumber(SqlExpression groupExpression, DialectRegex regex, Pattern pattern)
    {
        switch (groupExpression)
        {
            case SqlConstantExpression { Value: int number }:
                return _sql.Constant(DialectGroupNumber(number.ToString(CultureInfo.InvariantCulture), regex), _intMapping);
            case SqlConstantExpression { Value: string name }:
                return _sql.Constant(DialectGroupNumber(name, regex), _intMapping);
            case SqlParameterExpression parameter when parameter.Type == typeof(int):
                RequireUnchangedNumbering(regex, pattern);
                return _sql.ApplyTypeMapping(parameter, _intMapping);
            default:
                throw new InvalidOperationException(
                    "The group name must be a literal string; the group number may be a literal or a variable.");
        }
    }

    private static int DialectGroupNumber(string group, DialectRegex regex)
    {
        if (string.Equals(group, WholeMatchGroup, StringComparison.Ordinal))
        {
            return WholeMatchNumber;
        }

        if (!regex.GroupNumbers.TryGetValue(group, out int number))
        {
            throw new ArgumentException(
                $"The pattern has no group '{group}' that the database can return (PostgreSQL does not capture inside lookarounds).",
                nameof(group));
        }

        return number;
    }

    /// <summary>
    /// A group number held in a variable is only known when the query runs, so it can only be passed through when
    /// every .NET group keeps its number in the dialect.
    /// </summary>
    private static void RequireUnchangedNumbering(DialectRegex regex, Pattern pattern)
    {
        if (KeepsGroupNumbers(regex, pattern))
        {
            return;
        }

        throw new InvalidOperationException(
            "A group number held in a variable can only be translated when the database numbers the pattern's groups " +
            "exactly as .NET does. Use a literal group number or name, or a pattern " +
            "without named groups and without groups inside lookarounds.");
    }

    private static bool KeepsGroupNumbers(DialectRegex regex, Pattern pattern)
    {
        int[] dotNetGroups = pattern.ToRegex().GetGroupNumbers();
        foreach (int group in dotNetGroups)
        {
            bool mapped = regex.GroupNumbers.TryGetValue(group.ToString(CultureInfo.InvariantCulture), out int number);
            if (group != WholeMatchNumber && (!mapped || number != group))
            {
                return false;
            }
        }

        return true;
    }

    // ------------------------------------------------------------------
    // Replace
    // ------------------------------------------------------------------

    private SqlExpression? TranslateReplace(SqlExpression value, SqlExpression patternExpression, SqlExpression replacementExpression, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        PatternOperand? operand = PatternOperand.From(patternExpression);
        if (operand == null)
        {
            return null;
        }

        SqlDialect dialect = _options.Dialect;
        if (dialect == SqlDialect.Sqlite || dialect == SqlDialect.SqlServer)
        {
            throw new NotSupportedException(
                $"Regular expression replacement cannot be translated on {dialect}. Replace is translated on PostgreSQL, " +
                "MySQL, MariaDB and SQL Server 2025; load the value and use Pattern.Replace in .NET instead.");
        }

        if (replacementExpression is not SqlConstantExpression { Value: string replacement })
        {
            throw new InvalidOperationException(
                "The replacement must be a literal string so that .NET substitutions ($1, ${name}, $$) can be converted " +
                "to the database's replacement syntax.");
        }

        DialectRegex regex = ResolveExtentPreservingRegex(operand, dialect, nameof(PatternFunctions.Replace), logger);
        string converted = ReplacementConverter.Convert(replacement, dialect, regex.GroupNumbers);
        SqlExpression regexSql = RegexConstant(regex.RegexText);
        SqlExpression replacementSql = _sql.Constant(converted, _literalMapping);
        RelationalTypeMapping resultMapping = value.TypeMapping ?? _stringMapping;
        if (dialect == SqlDialect.PostgreSql)
        {
            SqlExpression[] arguments = { value, regexSql, replacementSql, _sql.Constant(PostgreSqlGlobalFlag, _literalMapping) };
            bool[] propagateNullability = { true, true, true, false };
            return _sql.Function(PostgreSqlRegexReplace, arguments, nullable: true, propagateNullability, typeof(string), resultMapping);
        }

        return _sql.Function(RegexReplace, new[] { value, regexSql, replacementSql }, nullable: true, ThreeNullableArguments, typeof(string), resultMapping);
    }

    // ------------------------------------------------------------------
    // Building blocks
    // ------------------------------------------------------------------

    /// <summary>
    /// Translates the pattern of a <c>Group</c> or <c>Replace</c> call. Both depend on which substring the database
    /// matches and what each group captures, not only on whether a string matches, so the pattern must be known at
    /// query compile time and its translation must preserve the match extent.
    /// </summary>
    private DialectRegex ResolveExtentPreservingRegex(PatternOperand operand, SqlDialect dialect, string function, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (operand.Constant is not Pattern pattern)
        {
            throw new InvalidOperationException(
                $"{function} can only be translated when the pattern is known at query compile time, because some patterns " +
                "match a different substring in the database than in .NET, and only a known pattern can be checked. Reference " +
                "the pattern through a static readonly field or build it inline in the query.");
        }

        CoreDialectAdapter.RequireCultureIndependent(pattern);
        DialectRegex regex = CoreDialectAdapter.ToDialectRegex(pattern, dialect);
        _logger.TranslationWarnings(logger, pattern, dialect, regex);
        if (!regex.PreservesMatchExtent)
        {
            throw new NotSupportedException(
                $"{function} cannot be translated for the pattern '{pattern}' on {dialect}, because the database would match " +
                $"a different substring or capture different text than .NET: {string.Join(" ", regex.MatchExtentWarnings)} " +
                $"Load the values and use Pattern.{(function == nameof(PatternFunctions.Group) ? "Match" : "Replace")} in .NET instead.");
        }

        return regex;
    }

    private SqlExpression RegexPredicate(SqlExpression value, SqlExpression regex)
    {
        SqlDialect dialect = _options.Dialect;
        switch (dialect)
        {
            case SqlDialect.PostgreSql:
                return _providerExpressions.TryCreate(dialect, value, regex, _boolMapping)
                    ?? BoolFunction(PostgreSqlRegexLike, value, regex);
            case SqlDialect.MySql:
                // MySQL's default collations are case-insensitive and REGEXP_LIKE follows them; the 'c' match type
                // restores .NET's default, and an inline (?i) in the regex still turns case-insensitivity back on.
                SqlExpression[] likeArguments = { value, regex, _sql.Constant(CaseSensitiveFlag, _flagsMapping) };
                bool[] likePropagation = { true, true, false };
                return _sql.Function(RegexLike, likeArguments, nullable: true, likePropagation, typeof(bool), _boolMapping);
            case SqlDialect.MariaDb:
                return _providerExpressions.TryCreate(dialect, value, regex, _boolMapping)
                    ?? throw new InvalidOperationException(
                        "The Pomelo MySQL provider in use does not expose MySqlRegexpExpression, which FluentRegex needs to " +
                        "emit the REGEXP operator on MariaDB. Please report the provider version.");
            case SqlDialect.SqlServer2025:
                // REGEXP_LIKE is a predicate, not a function: wrapped in IIF it is valid wherever EF Core places a value.
                SqlExpression[] iifArguments = { BoolFunction(RegexLike, value, regex), _sql.Constant(True, _intMapping), _sql.Constant(False, _intMapping) };
                bool[] noPropagation = { false, false, false };
                SqlExpression iif = _sql.Function(SqlServerIif, iifArguments, nullable: false, noPropagation, typeof(int), _intMapping);
                return _sql.Equal(iif, _sql.Constant(True, _intMapping));
            case SqlDialect.Sqlite:
                return _providerExpressions.TryCreate(dialect, value, regex, _boolMapping)
                    ?? BoolFunction(SqliteRegexp, regex, value);
            default:
                throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "The dialect has no regular expression predicate.");
        }
    }

    private SqlExpression BoolFunction(string name, SqlExpression first, SqlExpression second) =>
        _sql.Function(name, new[] { first, second }, nullable: true, TwoNullableArguments, typeof(bool), _boolMapping);

    private SqlExpression RegexConstant(string regexText) => _sql.Constant(regexText, _literalMapping);

    /// <summary>
    /// Keeps the pattern as a query parameter and converts it to the dialect regex when the query executes,
    /// so that a captured variable can change between executions without recompiling the query.
    /// </summary>
    private SqlExpression RegexParameter(SqlParameterExpression parameter) => parameter.ApplyTypeMapping(PatternParameterMapping());

    private RelationalTypeMapping PatternParameterMapping()
    {
        RelationalTypeMapping? mapping = _patternParameterMapping;
        if (mapping != null)
        {
            return mapping;
        }

        SqlDialect dialect = _options.Dialect;
        ValueConverter<Pattern, string> converter = new ValueConverter<Pattern, string>(
            pattern => CoreDialectAdapter.ToDialectRegex(pattern, dialect).RegexText,
            text => Pattern.FromRegex(text, RegexOptions.None));
        mapping = (RelationalTypeMapping)_stringMapping.WithComposedConverter(converter);
        _patternParameterMapping = mapping;
        return mapping;
    }

    private SqlExpression LikePredicate(SqlExpression value, DialectLike like)
    {
        SqlExpression? escape = like.EscapeCharacter is char escapeCharacter
            ? _sql.Constant(escapeCharacter.ToString(), _literalMapping)
            : null;
        return _sql.Like(value, _sql.Constant(like.Expression, _literalMapping), escape);
    }

    /// <summary>
    /// Adds conjuncts that every matching row provably satisfies: a prefix LIKE that lets a B-tree index
    /// narrow the scan, and an upper length bound. The lower bound is not emitted because SQL length functions
    /// count code points (or, with LEN, strip trailing spaces), so a .NET lower bound could exclude matches. The
    /// prefix is only emitted where <see cref="ColumnComparesLikeByCodeUnit"/> holds: with COLLATE it could not use an index.
    /// </summary>
    private SqlExpression WithIndexHints(SqlExpression predicate, SqlExpression value, Pattern pattern)
    {
        DialectFacts facts = CoreDialectAdapter.GetFacts(pattern, substring: true);
        SqlExpression result = predicate;
        if (facts.RequiredPrefix.Length > 0 && ColumnComparesLikeByCodeUnit(_options.Dialect))
        {
            SqlExpression prefix = _sql.Constant(EscapeLike(facts.RequiredPrefix) + LikeWildcard, _literalMapping);
            result = _sql.AndAlso(result, _sql.Like(value, prefix, _sql.Constant(LikeEscape, _literalMapping)));
        }

        if (facts.MaxLength is int maxLength)
        {
            result = _sql.AndAlso(result, MaxLengthPredicate(value, maxLength));
        }

        return result;
    }

    private SqlExpression MaxLengthPredicate(SqlExpression value, int maxLength) =>
        _sql.LessThanOrEqual(LengthOf(value), _sql.Constant(maxLength, _intMapping));

    private SqlExpression LengthOf(SqlExpression value)
    {
        SqlDialect dialect = _options.Dialect;
        string function = dialect switch
        {
            SqlDialect.MySql => CharLength,
            SqlDialect.MariaDb => CharLength,
            SqlDialect.SqlServer => Len,
            SqlDialect.SqlServer2025 => Len,
            _ => Length
        };
        SqlExpression length = _sql.Function(function, new[] { value }, nullable: true, OneNullableArgument, typeof(int), _intMapping);

        // LEN returns bigint for (max) columns, so SQL Server needs the same cast EF Core applies to string.Length.
        return function == Len ? _sql.Convert(length, typeof(int), _intMapping) : length;
    }

    private static string EscapeLike(string text)
    {
        StringBuilder escaped = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (LikeSpecialCharacters.IndexOf(c) >= 0)
            {
                escaped.Append(LikeEscape);
            }

            escaped.Append(c);
        }

        return escaped.ToString();
    }

    private static RelationalTypeMapping RequireMapping(IRelationalTypeMappingSource source, Type type) =>
        source.FindMapping(type) ?? throw new InvalidOperationException($"The database provider has no type mapping for {type}.");

    /// <summary>A pattern as it appears in a translated query: a compile-time constant or a runtime parameter.</summary>
    private sealed class PatternOperand
    {
        private PatternOperand(Pattern? constant, SqlParameterExpression? parameter)
        {
            Constant = constant;
            Parameter = parameter;
        }

        public Pattern? Constant { get; }

        public SqlParameterExpression? Parameter { get; }

        public static PatternOperand? From(SqlExpression expression)
        {
            switch (expression)
            {
                case SqlConstantExpression { Value: Pattern pattern }:
                    return new PatternOperand(pattern, null);
                case SqlConstantExpression { Value: null }:
                    throw new InvalidOperationException("The pattern used in the query is null.");
                case SqlParameterExpression parameter when parameter.Type == typeof(Pattern):
                    return new PatternOperand(null, parameter);
                default:
                    return null;
            }
        }

        public SqlParameterExpression RequireParameter() =>
            Parameter ?? throw new InvalidOperationException("The pattern operand is a constant, not a parameter.");
    }
}
