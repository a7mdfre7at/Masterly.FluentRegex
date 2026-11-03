#nullable enable
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Validation;

/// <summary>
/// Builds the whole-input form of a pattern, <c>\A(?:pattern)\z</c>, which accepts an input
/// only when the pattern can consume all of it (the semantics of <c>RegularExpressionAttribute</c>).
/// </summary>
internal static class FullMatchRegex
{
    private const string Prefix = @"\A(?:";
    private const string Suffix = @")\z";
    private const char CommentStart = '#';

    /// <summary>Gets the whole-input regex text for <paramref name="pattern"/>.</summary>
    public static string SourceFor(Pattern pattern)
    {
        string source = pattern.ToString();
        string separator = EndsWithWhitespaceComment(source, pattern.Options) ? "\n" : string.Empty;
        return $"{Prefix}{source}{separator}{Suffix}";
    }

    /// <summary>Creates the compiled whole-input regex with the pattern's options and timeout.</summary>
    public static Regex Create(Pattern pattern) => new Regex(SourceFor(pattern), pattern.Options, pattern.Timeout);

    /// <summary>
    /// Detects a trailing <c># comment</c> in <see cref="RegexOptions.IgnorePatternWhitespace"/> mode,
    /// which would otherwise swallow the closing parenthesis of the wrapper.
    /// </summary>
    private static bool EndsWithWhitespaceComment(string source, RegexOptions options)
    {
        if (!RegexParser.TryParse(source, options, out RegexTree tree))
        {
            return false;
        }

        RegexNode last = LastNode(tree.Root);
        return source.IndexOf(CommentStart, last.End) >= 0;
    }

    private static RegexNode LastNode(RegexNode node)
    {
        while (true)
        {
            if (node is SequenceNode sequence && sequence.Items.Count > 0)
            {
                node = sequence.Items[sequence.Items.Count - 1];
            }
            else if (node is AlternationNode alternation)
            {
                node = alternation.Branches[alternation.Branches.Count - 1];
            }
            else
            {
                return node;
            }
        }
    }
}
