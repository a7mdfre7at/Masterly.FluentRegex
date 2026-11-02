#nullable enable

using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Decides full-match membership with the real .NET engine. The pattern is anchored with
/// <c>\A</c> and, unless an explicit end anchor such as <c>$</c> ended the match, with <c>\z</c>,
/// so <c>$</c> keeps its .NET meaning of "end, or before a final newline".
/// </summary>
internal sealed class MembershipOracle
{
    private const string MarkerBaseName = "fluentRegexEnd";
    private const RegexOptions IgnoredOptions = RegexOptions.Compiled
#if NET7_0_OR_GREATER
                                                | RegexOptions.NonBacktracking
#endif
        ;

    private readonly Regex _regex;

    public MembershipOracle(RegexTree tree)
    {
        string marker = UniqueMarker(tree);
        bool sawEndAnchor = false;
        string body = RegexWriter.Write(tree.Root, (node, inner) =>
        {
            if (node is AnchorNode anchor && IsEndAnchor(anchor))
            {
                sawEndAnchor = true;
                return $"{inner}(?<{marker}>)";
            }
            return inner;
        });
        string tail = sawEndAnchor ? $"(?({marker})|\\z)" : "\\z";
        _regex = new Regex($"\\A(?:{body}){tail}", tree.Options & ~IgnoredOptions, AlgebraLimits.OracleTimeout);
    }

    /// <summary>Returns true when the pattern matches <paramref name="input"/> in full.</summary>
    public bool Accepts(string input)
    {
        try
        {
            return _regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException ex)
        {
            throw new InvalidOperationException("The .NET engine timed out while verifying a witness string.", ex);
        }
    }

    private static bool IsEndAnchor(AnchorNode anchor) =>
        anchor.AnchorKind == AnchorKind.EndOfLine ||
        anchor.AnchorKind == AnchorKind.EndOfInput ||
        anchor.AnchorKind == AnchorKind.EndOfInputOrNewline;

    private static string UniqueMarker(RegexTree tree)
    {
        string marker = MarkerBaseName;
        int suffix = 0;
        while (tree.Groups.Exists(g => string.Equals(g.Name, marker, StringComparison.Ordinal)))
        {
            suffix++;
            marker = $"{MarkerBaseName}{suffix}";
        }
        return marker;
    }
}
