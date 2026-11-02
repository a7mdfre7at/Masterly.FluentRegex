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

    protected override void AppendText(StringBuilder output, string text) => output.Append(WebUtility.HtmlEncode(text));
}
