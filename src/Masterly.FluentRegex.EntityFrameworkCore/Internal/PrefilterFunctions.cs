using System;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>
/// The SQL prefilter behind <c>MatchingAsync</c>: a predicate that is a proven superset of the pattern.
/// In SQL it becomes the exact dialect regex when the translation is exact, otherwise the superset LIKE
/// and length bound from the pattern's facts, otherwise only a NULL check.
/// </summary>
internal static class PrefilterFunctions
{
    /// <summary>
    /// True when <paramref name="value"/> may match <paramref name="pattern"/>. Evaluated in .NET (for example by
    /// a non-relational provider) it only excludes null, which is always a superset.
    /// </summary>
    public static bool IsCandidate(string? value, Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return value != null;
    }
}
