using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Turns <c>HasPattern</c> annotations and, when enabled, <c>[MatchesPattern]</c> attributes on string
/// properties into table CHECK constraints in the configured dialect,
/// with <see cref="Pattern.IsMatch(string)"/> semantics.
/// NULL passes a CHECK constraint, so nullable columns stay nullable.
/// </summary>
internal sealed class PatternCheckConstraintConvention : IModelFinalizingConvention
{
    private const string ConstraintPrefix = "CK_";
    private const string ConstraintSuffix = "_Pattern";

    private readonly IFluentRegexSingletonOptions _options;
    private readonly ISqlGenerationHelper _sqlGenerationHelper;

    /// <summary>Creates the convention.</summary>
    public PatternCheckConstraintConvention(IFluentRegexSingletonOptions options, ISqlGenerationHelper sqlGenerationHelper)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sqlGenerationHelper);

        _options = options;
        _sqlGenerationHelper = sqlGenerationHelper;
    }

    /// <inheritdoc />
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (IConventionProperty property in entityType.GetDeclaredProperties())
            {
                ProcessProperty(entityType, property);
            }
        }
    }

    private void ProcessProperty(IConventionEntityType entityType, IConventionProperty property)
    {
        PatternSource? source = FindPattern(property);

        // The annotations hold a Pattern object, which a migration snapshot could not represent; they are consumed here.
        property.RemoveAnnotation(FluentRegexAnnotationNames.Pattern);
        property.RemoveAnnotation(FluentRegexAnnotationNames.ConstraintName);
        if (source == null)
        {
            return;
        }

        string? tableName = entityType.GetTableName();
        if (tableName == null)
        {
            return;
        }

        StoreObjectIdentifier table = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
        string? columnName = property.GetColumnName(table);
        if (columnName == null)
        {
            return;
        }

        string constraintName = source.ConstraintName ?? $"{ConstraintPrefix}{tableName}_{columnName}{ConstraintSuffix}";
        SqlCondition condition = BuildCondition(entityType, property, columnName, source.Pattern);
        entityType.Builder.HasCheckConstraint(constraintName, condition.Sql);
    }

    private SqlCondition BuildCondition(IConventionEntityType entityType, IConventionProperty property, string columnName, Pattern pattern)
    {
        string columnSql = _sqlGenerationHelper.DelimitIdentifier(columnName);
        try
        {
            // The constraint is written into migrations once, so it must not depend on the culture of the machine
            // that generates them.
            CoreDialectAdapter.RequireCultureIndependent(pattern);
            return SqlConditionBuilder.Build(columnSql, pattern, _options.Dialect, _options.LikeCollation);
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is NotSupportedException)
        {
            throw new InvalidOperationException(
                $"The CHECK constraint for '{entityType.DisplayName()}.{property.Name}' cannot be created on {_options.Dialect}: {ex.Message} " +
                "Remove HasPattern/[MatchesPattern] for this property or change the dialect with UseDialect(...).",
                ex);
        }
    }

    private PatternSource? FindPattern(IConventionProperty property)
    {
        if (property.FindAnnotation(FluentRegexAnnotationNames.Pattern)?.Value is Pattern configured)
        {
            string? name = property.FindAnnotation(FluentRegexAnnotationNames.ConstraintName)?.Value as string;
            return new PatternSource(configured, name);
        }

        if (!_options.CheckConstraintsFromAttributes || property.ClrType != typeof(string))
        {
            return null;
        }

        MemberInfo? member = (MemberInfo?)property.PropertyInfo ?? property.FieldInfo;
        MatchesPatternAttribute? attribute = member?.GetCustomAttribute<MatchesPatternAttribute>(inherit: true);
        return attribute == null ? null : new PatternSource(MatchesPatternResolver.Resolve(attribute), null);
    }

    private sealed record PatternSource(Pattern Pattern, string? ConstraintName);
}
