using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Writes this package's warnings through EF Core's diagnostics logger, so that they honor
/// <c>ConfigureWarnings</c>, <c>LogTo</c> and <c>DiagnosticSource</c> like EF Core's own events.
/// One instance lives per internal service provider, which has a single <see cref="ILoggingOptions"/>.
/// </summary>
internal sealed class FluentRegexLogger
{
    private const string EventIdCodePrefix = nameof(FluentRegexEventId) + ".";

    private const string LikeSupersetMessage =
        "The pattern '{Pattern}' was translated to the superset LIKE expression '{Like}' because SQL Server before 2025 " +
        "has no regular expression support; rows that do not match the pattern can be returned.";

    private const string TranslationWarningMessage =
        "The pattern '{Pattern}' is translated to {Dialect} with differences from .NET: {Warnings}";

    private EventDefinition<string, string>? _likeSuperset;
    private EventDefinition<string, string, string>? _translationWarning;

    /// <summary>Logs <see cref="FluentRegexEventId.LikeSupersetFallback"/>.</summary>
    public void LikeSupersetFallback(IDiagnosticsLogger<DbLoggerCategory.Query> logger, Pattern pattern, string like)
    {
        EventDefinition<string, string> definition = _likeSuperset ??= new EventDefinition<string, string>(
            logger.Options,
            FluentRegexEventId.LikeSupersetFallback,
            LogLevel.Warning,
            EventIdCodePrefix + nameof(FluentRegexEventId.LikeSupersetFallback),
            level => LoggerMessage.Define<string, string>(level, FluentRegexEventId.LikeSupersetFallback, LikeSupersetMessage));

        string patternText = pattern.ToString();
        if (logger.ShouldLog(definition))
        {
            definition.Log(logger, patternText, like);
        }

        if (logger.NeedsEventData(definition, out bool diagnosticSourceEnabled, out bool simpleLogEnabled))
        {
            EventData eventData = new EventData(
                definition,
                (eventDefinition, _) => ((EventDefinition<string, string>)eventDefinition).GenerateMessage(patternText, like));
            logger.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
        }
    }

    /// <summary>
    /// Logs <see cref="FluentRegexEventId.TranslationWarning"/> when the translation reported differences. The built-in
    /// translators never do; the path stays so a translator that reports differences is logged, not silently dropped.
    /// </summary>
#pragma warning disable CS0618 // TranslationWarning is obsolete for callers because the built-in translators never raise it.
    public void TranslationWarnings(IDiagnosticsLogger<DbLoggerCategory.Query> logger, Pattern pattern, SqlDialect dialect, DialectRegex regex)
    {
        if (regex.Warnings.Count == 0)
        {
            return;
        }

        EventDefinition<string, string, string> definition = _translationWarning ??= new EventDefinition<string, string, string>(
            logger.Options,
            FluentRegexEventId.TranslationWarning,
            LogLevel.Warning,
            EventIdCodePrefix + nameof(FluentRegexEventId.TranslationWarning),
            level => LoggerMessage.Define<string, string, string>(level, FluentRegexEventId.TranslationWarning, TranslationWarningMessage));

        string patternText = pattern.ToString();
        string dialectText = dialect.ToString();
        string warnings = string.Join("; ", regex.Warnings);
        if (logger.ShouldLog(definition))
        {
            definition.Log(logger, patternText, dialectText, warnings);
        }

        if (logger.NeedsEventData(definition, out bool diagnosticSourceEnabled, out bool simpleLogEnabled))
        {
            EventData eventData = new EventData(
                definition,
                (eventDefinition, _) => ((EventDefinition<string, string, string>)eventDefinition).GenerateMessage(patternText, dialectText, warnings));
            logger.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
        }
    }
#pragma warning restore CS0618
}
