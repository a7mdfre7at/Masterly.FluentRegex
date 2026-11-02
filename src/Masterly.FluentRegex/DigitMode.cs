#nullable enable

namespace Masterly.FluentRegex;

/// <summary>
/// Chooses which characters count as a digit for <c>\d</c> and <c>\D</c>.
/// </summary>
/// <remarks>
/// .NET's <c>\d</c> matches every Unicode decimal digit (category Nd), such as the Arabic-Indic
/// digits ٠-٩ and the Devanagari digits ०-९, not just 0-9. That suits free text in any language,
/// but formats defined over ASCII, such as IPv4 addresses, ISO 8601 dates or card numbers, usually
/// need <see cref="Ascii"/>.
/// </remarks>
public enum DigitMode
{
    /// <summary>
    /// <c>\d</c> matches any Unicode decimal digit. This is the .NET default and the behavior of the
    /// parameterless <see cref="CommonPatterns"/> methods.
    /// </summary>
    Unicode,

    /// <summary><c>\d</c> matches only the ASCII digits 0-9, and <c>\D</c> everything else.</summary>
    Ascii
}
