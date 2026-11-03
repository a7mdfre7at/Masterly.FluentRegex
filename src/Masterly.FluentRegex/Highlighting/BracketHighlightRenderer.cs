#nullable enable
using System.Text;

namespace Masterly.FluentRegex.Highlighting;

/// <summary>
/// Plain-text markers: <c>[match]</c> and <c>[group:text]</c>. Brackets already in the text are not escaped,
/// so the output is meant for reading, assertions and logs, not for parsing.
/// </summary>
internal sealed class BracketHighlightRenderer : HighlightRenderer
{
    private const char OpenMarker = '[';
    private const char CloseMarker = ']';
    private const char LabelSeparator = ':';

    protected override void Open(StringBuilder output, HighlightSpan span)
    {
        output.Append(OpenMarker);
        if (span.Group != null)
        {
            output.Append(span.Group.Label).Append(LabelSeparator);
        }
    }

    protected override void Close(StringBuilder output, HighlightSpan span) => output.Append(CloseMarker);
}
