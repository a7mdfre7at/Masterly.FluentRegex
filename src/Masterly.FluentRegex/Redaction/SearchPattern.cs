#nullable enable
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Redaction;

/// <summary>
/// Turns a whole-value validation pattern such as <c>^\d{3}-\d{2}-\d{4}$</c> into a pattern that
/// finds the same values inside free text.
/// </summary>
/// <remarks>
/// Only anchors that wrap the entire pattern are rewritten: a leading <c>^</c> or <c>\A</c> becomes
/// <c>(?&lt;!\w)</c> and a trailing <c>$</c>, <c>\Z</c> or <c>\z</c> becomes <c>(?!\w)</c>, so a value
/// is still only found as a whole token. Multiline anchors and anchors anywhere else are kept.
/// </remarks>
internal static class SearchPattern
{
    private const string LeadingBoundary = @"(?<!\w)";
    private const string TrailingBoundary = @"(?!\w)";

    /// <summary>Returns the searchable form of <paramref name="pattern"/>, or the pattern itself.</summary>
    public static Pattern For(Pattern pattern)
    {
        string source = pattern.ToString();
        if ((pattern.Options & RegexOptions.RightToLeft) != 0 ||
            !RegexParser.TryParse(source, pattern.Options, out RegexTree tree) ||
            !(tree.Root is SequenceNode sequence))
        {
            return pattern;
        }

        RegexNode first = sequence.Items[0];
        RegexNode last = sequence.Items[sequence.Items.Count - 1];
        bool leading = IsLeadingAnchor(first);
        bool trailing = IsTrailingAnchor(last);
        if (!leading && !trailing)
        {
            return pattern;
        }

        int bodyStart = leading ? first.End : 0;
        int bodyEnd = trailing ? last.Start : source.Length;
        string body = source.Substring(bodyStart, bodyEnd - bodyStart);
        string searchable = $"{(leading ? LeadingBoundary : string.Empty)}{body}{(trailing ? TrailingBoundary : string.Empty)}";
        return Pattern.FromRegex(searchable, pattern.Options).WithTimeout(pattern.Timeout);
    }

    private static bool IsLeadingAnchor(RegexNode node) =>
        node is AnchorNode anchor &&
        (anchor.AnchorKind == AnchorKind.StartOfInput ||
         (anchor.AnchorKind == AnchorKind.StartOfLine && !IsMultiline(anchor)));

    private static bool IsTrailingAnchor(RegexNode node) =>
        node is AnchorNode anchor &&
        (anchor.AnchorKind == AnchorKind.EndOfInput ||
         anchor.AnchorKind == AnchorKind.EndOfInputOrNewline ||
         (anchor.AnchorKind == AnchorKind.EndOfLine && !IsMultiline(anchor)));

    private static bool IsMultiline(RegexNode node) => (node.Options & RegexOptions.Multiline) != 0;
}
