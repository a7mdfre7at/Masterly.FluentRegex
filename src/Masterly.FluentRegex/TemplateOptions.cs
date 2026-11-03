#nullable enable

using System;
using System.Globalization;

namespace Masterly.FluentRegex;

/// <summary>
/// Settings for <see cref="Template.Of{T}(string, TemplateOptions)"/>.
/// </summary>
public sealed class TemplateOptions
{
    /// <summary>
    /// Gets the culture used to match, parse and format numbers, dates, month and day names and
    /// AM/PM designators. Defaults to <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public IFormatProvider Culture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets a value indicating whether literal text in the template matches case-insensitively.
    /// Enum names and booleans always match case-insensitively.
    /// </summary>
    public bool IgnoreCase { get; init; }

    /// <summary>
    /// Gets the match timeout, the runtime defense against catastrophic backtracking.
    /// Null means no timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}
