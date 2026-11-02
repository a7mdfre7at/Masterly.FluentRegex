using System;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>Adds <see cref="PatternCheckConstraintConvention"/> to the model building conventions.</summary>
internal sealed class FluentRegexConventionSetPlugin : IConventionSetPlugin
{
    private readonly IFluentRegexSingletonOptions _options;
    private readonly ISqlGenerationHelper _sqlGenerationHelper;

    /// <summary>Creates the plugin from the services of the internal provider.</summary>
    public FluentRegexConventionSetPlugin(IFluentRegexSingletonOptions options, ISqlGenerationHelper sqlGenerationHelper)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sqlGenerationHelper);

        _options = options;
        _sqlGenerationHelper = sqlGenerationHelper;
    }

    /// <inheritdoc />
    public ConventionSet ModifyConventions(ConventionSet conventionSet)
    {
        ArgumentNullException.ThrowIfNull(conventionSet);

        conventionSet.ModelFinalizingConventions.Add(new PatternCheckConstraintConvention(_options, _sqlGenerationHelper));
        return conventionSet;
    }
}
