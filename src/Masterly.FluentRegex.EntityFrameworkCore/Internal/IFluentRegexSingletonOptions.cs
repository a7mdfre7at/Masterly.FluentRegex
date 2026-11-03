using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>The resolved FluentRegex settings of one EF Core internal service provider.</summary>
internal interface IFluentRegexSingletonOptions : ISingletonOptions
{
    /// <summary>Gets the dialect, configured or detected from the provider.</summary>
    SqlDialect Dialect { get; }

    /// <summary>Gets whether proven LIKE/length conjuncts are added next to the regex predicate.</summary>
    bool IndexHints { get; }

    /// <summary>Gets how patterns are translated on SQL Server without regex support.</summary>
    LikeFallback LikeFallback { get; }

    /// <summary>Gets what the database's collation does to <c>LIKE</c>, which decides when LIKE is proven exact.</summary>
    LikeCollation LikeCollation { get; }

    /// <summary>Gets whether <c>[MatchesPattern]</c> attributes produce CHECK constraints.</summary>
    bool CheckConstraintsFromAttributes { get; }

    /// <summary>
    /// Gets a value indicating whether the provider writes string literals without escaping backslashes although
    /// the server reads them as escapes (Oracle's MySql.EntityFrameworkCore provider).
    /// </summary>
    bool ProviderLeavesBackslashesUnescaped { get; }

    /// <summary>Gets the provider assembly, used to create provider-specific SQL expressions; null when unknown.</summary>
    Assembly? ProviderAssembly { get; }
}
