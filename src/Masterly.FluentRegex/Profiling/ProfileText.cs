#nullable enable
using System;

namespace Masterly.FluentRegex.Profiling;

/// <summary>Shared wording for profiling output.</summary>
internal static class ProfileText
{
    /// <summary>Formats a count with its noun, such as <c>1 step</c> or <c>1,024 backtracks</c>.</summary>
    public static string Counted(long count, string noun) =>
        FormattableString.Invariant($"{count:N0} {noun}{(count == 1 ? string.Empty : "s")}");
}
