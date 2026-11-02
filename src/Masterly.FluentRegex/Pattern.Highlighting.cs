#nullable enable
using System;
using Masterly.FluentRegex.Highlighting;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex;

/// <summary>
/// Match highlighting: show where a pattern matches in a text, and what each group captured.
/// </summary>
public partial class Pattern
{
    /// <summary>
    /// Renders <paramref name="text"/> with every match and every group capture marked.
    /// </summary>
    /// <param name="text">The text to search and render.</param>
    /// <param name="format">
    /// <see cref="HighlightFormat.Ansi"/> for terminals (the default), <see cref="HighlightFormat.Html"/> for
    /// web pages, or <see cref="HighlightFormat.Brackets"/> for tests and logs.
    /// </param>
    /// <returns>The marked-up text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is not a defined value.</exception>
    /// <remarks>
    /// <para>
    /// Groups that did not participate are not marked. Every capture of a repeated group is marked, so
    /// <c>(\d)+</c> on <c>12</c> renders <c>[[1:1][1:2]]</c>. Nested groups nest; with ANSI colors the
    /// innermost group's color wins. Captures are clipped to their match, so text a lookaround captured outside
    /// the match is not marked.
    /// </para>
    /// <para>
    /// Empty matches and empty captures render as empty markers (<c>[]</c>, an empty <c>&lt;mark&gt;</c>);
    /// with ANSI colors they are invisible because there is no character to color.
    /// </para>
    /// </remarks>
    /// <example>
    /// Pattern.With.Digit.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4)
    ///     .Highlight("Call 555-1234 now", HighlightFormat.Brackets);
    /// // "Call [555-1234] now"
    /// </example>
    public string Highlight(string text, HighlightFormat format = HighlightFormat.Ansi)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        HighlightRenderer renderer = HighlightRenderer.Create(format);
        RegexParser.TryParse(ToString(), _options, out RegexTree tree);
        HighlightTreeBuilder builder = new HighlightTreeBuilder(CachedRegex, tree);
        return renderer.Render(text, builder.Build(text), builder.Groups);
    }
}
