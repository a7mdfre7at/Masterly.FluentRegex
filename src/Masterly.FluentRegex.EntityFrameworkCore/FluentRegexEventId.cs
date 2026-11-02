using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Masterly.FluentRegex.EntityFrameworkCore;

/// <summary>
/// Event IDs for events logged by Masterly.FluentRegex.EntityFrameworkCore. Use them with
/// <c>optionsBuilder.ConfigureWarnings(...)</c> to throw on, ignore or re-level an event.
/// </summary>
public static class FluentRegexEventId
{
    private const int BaseId = 79_000;

    private static readonly string EventPrefix = DbLoggerCategory.Query.Name + ".";

    private enum Id
    {
        LikeSupersetFallback = BaseId,
        TranslationWarning
    }

    /// <summary>
    /// A pattern was translated to a <c>LIKE</c> expression that accepts more strings than the pattern,
    /// because <see cref="LikeFallback.AllowSuperset"/> is configured on a database without regex support.
    /// </summary>
    public static readonly EventId LikeSupersetFallback = MakeEventId(Id.LikeSupersetFallback);

    /// <summary>
    /// The regex dialect translation of a pattern reported behaviors that differ from .NET;
    /// the warning text lists each difference.
    /// </summary>
    public static readonly EventId TranslationWarning = MakeEventId(Id.TranslationWarning);

    private static EventId MakeEventId(Id id) => new EventId((int)id, EventPrefix + id);
}
