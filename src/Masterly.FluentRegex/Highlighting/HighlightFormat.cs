#nullable enable
namespace Masterly.FluentRegex;

/// <summary>The output format of <see cref="Pattern.Highlight(string, HighlightFormat)"/>.</summary>
public enum HighlightFormat
{
    /// <summary>
    /// ANSI terminal colors: matches get a background color, capture groups a foreground color, and a
    /// one-line legend follows the text.
    /// </summary>
    Ansi,

    /// <summary>
    /// HTML: matches are wrapped in <c>&lt;mark class="frx-match"&gt;</c> and groups in
    /// <c>&lt;span class="frx-group frx-group-{name}" title="{name}"&gt;</c>. All text is HTML-encoded.
    /// </summary>
    Html,

    /// <summary>
    /// Plain text for tests and logs: matches are wrapped in <c>[...]</c> and groups in <c>[name:...]</c>,
    /// for example <c>Call [555-1234] now</c>.
    /// </summary>
    Brackets
}
