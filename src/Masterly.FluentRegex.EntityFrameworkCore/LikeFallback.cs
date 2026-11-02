namespace Masterly.FluentRegex.EntityFrameworkCore;

/// <summary>
/// How a pattern is translated on <see cref="SqlDialect.SqlServer"/>, which has no regular expression support.
/// </summary>
public enum LikeFallback
{
    /// <summary>
    /// Translate to <c>LIKE</c> only when the <c>LIKE</c> expression accepts exactly the strings the pattern
    /// accepts; otherwise throw an <see cref="System.InvalidOperationException"/> that names the pattern and
    /// suggests <c>MatchingAsync</c>, <see cref="SqlDialect.SqlServer2025"/> or <see cref="AllowSuperset"/>.
    /// This is the default.
    /// </summary>
    ExactOnly,

    /// <summary>
    /// Translate to a <c>LIKE</c> expression that accepts every string the pattern accepts and possibly more,
    /// and log a warning through EF Core's logger (<see cref="FluentRegexEventId.LikeSupersetFallback"/>).
    /// </summary>
    AllowSuperset,

    /// <summary>Never translate to <c>LIKE</c>: every pattern in a query throws.</summary>
    Never
}
