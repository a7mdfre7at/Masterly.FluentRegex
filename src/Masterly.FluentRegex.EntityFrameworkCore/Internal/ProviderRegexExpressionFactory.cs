using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Creates the provider's own regex-operator expressions (<c>~</c>, <c>REGEXP</c>) through reflection, so that
/// the provider's SQL generator prints them and this package needs no provider package reference.
/// Returns null when the provider does not expose the expression; callers then fall back to a function form.
/// </summary>
internal sealed class ProviderRegexExpressionFactory
{
    private const string NpgsqlRegexMatchExpression = "PgRegexMatchExpression";
    private const string PomeloRegexpExpression = "MySqlRegexpExpression";
    private const string SqliteRegexpExpression = "RegexpExpression";

    private readonly Assembly? _providerAssembly;
    private readonly ConcurrentDictionary<string, ConstructorInfo?> _constructors = new ConcurrentDictionary<string, ConstructorInfo?>(StringComparer.Ordinal);

    /// <summary>Creates the factory for the provider in <paramref name="providerAssembly"/>.</summary>
    public ProviderRegexExpressionFactory(Assembly? providerAssembly)
    {
        _providerAssembly = providerAssembly;
    }

    /// <summary>
    /// Creates the provider's regex predicate for <paramref name="dialect"/>, or null when it cannot be created.
    /// </summary>
    /// <param name="dialect">The dialect.</param>
    /// <param name="match">The text to search.</param>
    /// <param name="regex">The regex, as a SQL constant or parameter.</param>
    /// <param name="boolTypeMapping">The provider's <c>bool</c> type mapping.</param>
    public SqlExpression? TryCreate(SqlDialect dialect, SqlExpression match, SqlExpression regex, RelationalTypeMapping boolTypeMapping)
    {
        switch (dialect)
        {
            case SqlDialect.PostgreSql:
                // PostgreSQL regexes are single-line by default; any other RegexOptions value makes Npgsql prepend an
                // inline-flags prefix, while the adapter already embeds the pattern's options in the regex text.
                return Create(
                    NpgsqlRegexMatchExpression,
                    new[] { typeof(SqlExpression), typeof(SqlExpression), typeof(RegexOptions), typeof(RelationalTypeMapping) },
                    new object[] { match, regex, RegexOptions.Singleline, boolTypeMapping });
            case SqlDialect.MariaDb:
                return Create(
                    PomeloRegexpExpression,
                    new[] { typeof(SqlExpression), typeof(SqlExpression), typeof(RelationalTypeMapping) },
                    new object[] { match, regex, boolTypeMapping });
            case SqlDialect.Sqlite:
                return Create(
                    SqliteRegexpExpression,
                    new[] { typeof(SqlExpression), typeof(SqlExpression), typeof(RelationalTypeMapping) },
                    new object[] { match, regex, boolTypeMapping });
            default:
                return null;
        }
    }

    private SqlExpression? Create(string typeName, Type[] parameterTypes, object[] arguments)
    {
        ConstructorInfo? constructor = _constructors.GetOrAdd(typeName, name => FindConstructor(name, parameterTypes));
        return constructor == null ? null : (SqlExpression)constructor.Invoke(arguments);
    }

    private ConstructorInfo? FindConstructor(string typeName, Type[] parameterTypes)
    {
        if (_providerAssembly == null)
        {
            return null;
        }

        foreach (Type type in _providerAssembly.GetExportedTypes())
        {
            if (string.Equals(type.Name, typeName, StringComparison.Ordinal) && typeof(SqlExpression).IsAssignableFrom(type))
            {
                return type.GetConstructor(parameterTypes);
            }
        }

        return null;
    }
}
