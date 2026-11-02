#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex.Highlighting;

/// <summary>
/// ANSI terminal colors: a blue background for matches and a bright foreground color per group, with the
/// innermost group's color winning. A legend line follows the text. Escape characters in the input are shown
/// as the visible symbol U+241B so the input cannot inject terminal control sequences.
/// </summary>
/// <remarks>
/// A color code is written only when text in a different style follows, so empty matches and empty groups
/// produce no output, and nested spans do not leave redundant codes behind.
/// </remarks>
internal sealed class AnsiHighlightRenderer : HighlightRenderer
{
    private const string Reset = "\u001b[0m";
    private const string MatchBackground = "44";
    private const string MatchForeground = "97";
    private const char EscapeCharacter = '\u001b';
    private const char EscapeSymbol = (char)0x241B;
    private const string LegendLabel = "Legend:";
    private const string MatchLabel = "match";
    private const string UnnamedGroupPrefix = "group ";

    private static readonly string[] GroupForegrounds = { "93", "92", "96", "95", "91", "33" };

    private readonly Stack<string> _styles = new Stack<string>();
    private string? _activeStyle;

    protected override void Open(StringBuilder output, HighlightSpan span) => _styles.Push(StyleOf(span.Group));

    protected override void Close(StringBuilder output, HighlightSpan span) => _styles.Pop();

    protected override void AppendText(StringBuilder output, string text)
    {
        SwitchStyle(output, _styles.Count > 0 ? _styles.Peek() : null);
        output.Append(text.Replace(EscapeCharacter, EscapeSymbol));
    }

    protected override void AppendEpilogue(StringBuilder output, IReadOnlyList<HighlightGroup> groups)
    {
        SwitchStyle(output, null);
        output.AppendLine();
        output.Append(LegendLabel).Append(' ').Append(StyleOf(null)).Append($" {MatchLabel} ").Append(Reset);
        foreach (HighlightGroup group in groups)
        {
            output.Append(' ').Append(StyleOf(group)).Append($" {LegendName(group)} ").Append(Reset);
        }
    }

    /// <summary>Every style sets both colors, so switching between styles needs no reset in between.</summary>
    private void SwitchStyle(StringBuilder output, string? style)
    {
        if (style == _activeStyle)
        {
            return;
        }

        output.Append(style ?? Reset);
        _activeStyle = style;
    }

    private static string StyleOf(HighlightGroup? group)
    {
        string foreground = group == null ? MatchForeground : GroupForegrounds[group.Ordinal % GroupForegrounds.Length];
        return $"\u001b[{MatchBackground};{foreground}m";
    }

    private static string LegendName(HighlightGroup group) =>
        int.TryParse(group.Label, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? $"{UnnamedGroupPrefix}{group.Label}"
            : group.Label;
}
