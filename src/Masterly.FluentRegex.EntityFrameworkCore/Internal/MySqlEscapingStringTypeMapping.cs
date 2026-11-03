using Microsoft.EntityFrameworkCore.Storage;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>
/// A string mapping whose literals double backslashes as well as quotes. Oracle's MySql.EntityFrameworkCore provider
/// writes string literals without escaping backslashes, but MySQL's default sql_mode reads a backslash in a literal as
/// an escape character, so the provider's own literal turns the regex <c>\A</c> into <c>A</c> and makes
/// <c>ESCAPE '\'</c> a syntax error. Only the literals this package creates use this mapping.
/// </summary>
internal sealed class MySqlEscapingStringTypeMapping : StringTypeMapping
{
    /// <summary>Creates the mapping with the store type of the provider's string mapping.</summary>
    /// <param name="providerMapping">The provider's string mapping.</param>
    public MySqlEscapingStringTypeMapping(RelationalTypeMapping providerMapping)
        : base(providerMapping.StoreType, providerMapping.DbType, unicode: true)
    {
    }

    private MySqlEscapingStringTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    /// <inheritdoc />
    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters) =>
        new MySqlEscapingStringTypeMapping(parameters);

    /// <inheritdoc />
    protected override string GenerateNonNullSqlLiteral(object value) => SqlLiteral.String((string)value, SqlDialect.MySql);
}
