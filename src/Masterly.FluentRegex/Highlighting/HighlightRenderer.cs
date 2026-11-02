#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Masterly.FluentRegex.Highlighting;

/// <summary>Writes text with its nested match and group spans in one output format.</summary>
internal abstract class HighlightRenderer
{
    private const int OutputGrowthFactor = 2;

    /// <summary>Creates the renderer for <paramref name="format"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is not a defined value.</exception>
    public static HighlightRenderer Create(HighlightFormat format)
    {
        switch (format)
        {
            case HighlightFormat.Ansi:
                return new AnsiHighlightRenderer();
            case HighlightFormat.Html:
                return new HtmlHighlightRenderer();
            case HighlightFormat.Brackets:
                return new BracketHighlightRenderer();
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown highlight format.");
        }
    }

    /// <summary>Renders <paramref name="text"/> with every match in <paramref name="matches"/> marked.</summary>
    public string Render(string text, IReadOnlyList<HighlightSpan> matches, IReadOnlyList<HighlightGroup> groups)
    {
        StringBuilder output = new StringBuilder(text.Length * OutputGrowthFactor);
        int cursor = 0;
        foreach (HighlightSpan match in matches)
        {
            AppendPlain(output, text, cursor, match.Start);
            RenderSpan(output, text, match);
            cursor = match.End;
        }

        AppendPlain(output, text, cursor, text.Length);
        AppendEpilogue(output, groups);
        return output.ToString();
    }

    protected abstract void Open(StringBuilder output, HighlightSpan span);

    protected abstract void Close(StringBuilder output, HighlightSpan span);

    /// <summary>Appends a run of input text, escaping it as the format requires.</summary>
    protected virtual void AppendText(StringBuilder output, string text) => output.Append(text);

    /// <summary>Appends anything that follows the text, such as a legend.</summary>
    protected virtual void AppendEpilogue(StringBuilder output, IReadOnlyList<HighlightGroup> groups)
    {
    }

    private void RenderSpan(StringBuilder output, string text, HighlightSpan span)
    {
        Open(output, span);
        int cursor = span.Start;
        foreach (HighlightSpan child in span.Children)
        {
            AppendPlain(output, text, cursor, child.Start);
            RenderSpan(output, text, child);
            cursor = child.End;
        }

        AppendPlain(output, text, cursor, span.End);
        Close(output, span);
    }

    private void AppendPlain(StringBuilder output, string text, int start, int end)
    {
        if (end > start)
        {
            AppendText(output, text.Substring(start, end - start));
        }
    }
}
