using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>Registers <see cref="FluentRegexMethodCallTranslator"/> with EF Core's query pipeline.</summary>
internal sealed class FluentRegexMethodCallTranslatorPlugin : IMethodCallTranslatorPlugin
{
    private readonly IMethodCallTranslator[] _translators;

    /// <summary>Creates the plugin from the services of the internal provider.</summary>
    public FluentRegexMethodCallTranslatorPlugin(
        ISqlExpressionFactory sqlExpressionFactory,
        IRelationalTypeMappingSource typeMappingSource,
        IFluentRegexSingletonOptions options)
    {
        ArgumentNullException.ThrowIfNull(sqlExpressionFactory);
        ArgumentNullException.ThrowIfNull(typeMappingSource);
        ArgumentNullException.ThrowIfNull(options);

        _translators = new IMethodCallTranslator[] { new FluentRegexMethodCallTranslator(sqlExpressionFactory, typeMappingSource, options) };
    }

    /// <inheritdoc />
    public IEnumerable<IMethodCallTranslator> Translators => _translators;
}
