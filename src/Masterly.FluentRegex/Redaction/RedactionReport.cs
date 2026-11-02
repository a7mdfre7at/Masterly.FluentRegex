#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex;

/// <summary>
/// The result of <see cref="Redactor.Redact"/>: the redacted text and where each value was redacted.
/// </summary>
/// <remarks>
/// The report never contains the original sensitive values, so it is safe to log.
/// </remarks>
public sealed class RedactionReport
{
    internal RedactionReport(string text, IReadOnlyList<RedactedSpan> spans)
    {
        Text = text;
        Spans = spans;
    }

    /// <summary>Gets the redacted text.</summary>
    public string Text { get; }

    /// <summary>Gets the redacted spans, ordered by position.</summary>
    public IReadOnlyList<RedactedSpan> Spans { get; }

    /// <summary>Gets a value indicating whether anything was redacted.</summary>
    public bool HasRedactions => Spans.Count > 0;

    /// <summary>Returns a summary of the redacted spans, without any original value.</summary>
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"Redacted {Spans.Count.ToString(CultureInfo.InvariantCulture)} span(s)");
        string separator = ": ";
        foreach (RedactedSpan span in Spans)
        {
            sb.Append(separator).Append(span);
            separator = ", ";
        }

        return sb.ToString();
    }
}

/// <summary>One redacted value: where it was and which style replaced it. The value itself is not kept.</summary>
public sealed class RedactedSpan
{
    internal RedactedSpan(int start, int length, int redactedStart, int redactedLength, RedactionStyle style, int patternIndex)
    {
        Start = start;
        Length = length;
        RedactedStart = redactedStart;
        RedactedLength = redactedLength;
        Style = style.Name;
        Label = style.LabelText;
        PatternIndex = patternIndex;
    }

    /// <summary>Gets the start of the value in the original text.</summary>
    public int Start { get; }

    /// <summary>Gets the length of the value in the original text.</summary>
    public int Length { get; }

    /// <summary>Gets the start of the replacement in <see cref="RedactionReport.Text"/>.</summary>
    public int RedactedStart { get; }

    /// <summary>Gets the length of the replacement in <see cref="RedactionReport.Text"/>.</summary>
    public int RedactedLength { get; }

    /// <summary>Gets the name of the style that produced the replacement, such as <c>Mask</c> or <c>KeepLast(4)</c>.</summary>
    public string Style { get; }

    /// <summary>Gets the label for a <see cref="RedactionStyle.Label(string)"/> style, otherwise null.</summary>
    public string? Label { get; }

    /// <summary>Gets the zero-based registration index of the pattern that found the value.</summary>
    public int PatternIndex { get; }

    /// <summary>Returns the span and style, for example <c>[5..17) Mask</c>.</summary>
    public override string ToString()
    {
        string start = Start.ToString(CultureInfo.InvariantCulture);
        string end = (Start + Length).ToString(CultureInfo.InvariantCulture);
        return Label == null ? $"[{start}..{end}) {Style}" : $"[{start}..{end}) {Style} {Label}";
    }
}
