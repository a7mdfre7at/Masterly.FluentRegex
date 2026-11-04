#nullable enable
using System.Net;
using System.Text;

namespace Masterly.FluentRegex.Highlighting;

/// <summary>
/// HTML markup: <c>&lt;mark class="frx-match"&gt;</c> for matches and
/// <c>&lt;span class="frx-group frx-group-{name}" title="{name}"&gt;</c> for groups. Text and names are
/// HTML-encoded, so untrusted input cannot inject markup.
/// </summary>
internal sealed class HtmlHighlightRenderer : HighlightRenderer
{
    private const string MatchOpen = "<mark class=\"frx-match\">";
    private const string MatchClose = "</mark>";
    private const string GroupClose = "</span>";

    protected override void Open(StringBuilder output, HighlightSpan span)
    {
        if (span.Group == null)
        {
            output.Append(MatchOpen);
            return;
        }

        string name = WebUtility.HtmlEncode(span.Group.Label);
        output.Append($"<span class=\"frx-group frx-group-{name}\" title=\"{name}\">");
    }

    protected override void Close(StringBuilder output, HighlightSpan span) =>
        output.Append(span.Group == null ? MatchClose : GroupClose);

    protected override void AppendText(StringBuilder output, string text) =>
        output.Append(WebUtility.HtmlEncode(WithVisibleControls(text)));

    /// <summary>
    /// Replaces control characters other than tab, line feed and carriage return with their Unicode control
    /// pictures (ESC becomes U+241B), so the input cannot put raw control characters into the page.
    /// </summary>
    private static string WithVisibleControls(string text)
    {
        StringBuilder? visible = null;
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            bool replace = (current < ' ' && current != '\t' && current != '\n' && current != '\r') || current == DeleteCharacter;
            if (replace && visible == null)
            {
                visible = new StringBuilder(text.Length).Append(text, 0, index);
            }

            if (visible != null)
            {
                visible.Append(replace ? (char)(current == DeleteCharacter ? DeletePicture : ControlPictures + current) : current);
            }
        }

        return visible?.ToString() ?? text;
    }

    private const char DeleteCharacter = (char)0x7F;
    private const int ControlPictures = 0x2400;
    private const char DeletePicture = (char)0x2421;
}
