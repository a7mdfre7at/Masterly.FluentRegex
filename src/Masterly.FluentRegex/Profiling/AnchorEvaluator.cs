#nullable enable
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Profiling;

/// <summary>Evaluates zero-width anchors exactly as the .NET engine does.</summary>
internal static class AnchorEvaluator
{
    private const char Newline = '\n';
    private const char ZeroWidthJoiner = (char)0x200D;
    private const char ZeroWidthNonJoiner = (char)0x200C;
    private const string WordClass = @"\w";

    /// <summary>Returns whether <paramref name="kind"/> holds at <paramref name="position"/>.</summary>
    public static bool IsSatisfied(AnchorKind kind, RegexOptions options, string input, int position)
    {
        bool multiline = (options & RegexOptions.Multiline) != 0;
        switch (kind)
        {
            case AnchorKind.StartOfLine:
                return position == 0 || (multiline && input[position - 1] == Newline);
            case AnchorKind.EndOfLine:
                return multiline ? IsEndOfLine(input, position) : IsEndOrBeforeFinalNewline(input, position);
            case AnchorKind.StartOfInput:
                return position == 0;
            case AnchorKind.EndOfInput:
                return position == input.Length;
            case AnchorKind.EndOfInputOrNewline:
                return IsEndOrBeforeFinalNewline(input, position);
            case AnchorKind.WordBoundary:
                return IsWordBoundary(input, position, options);
            case AnchorKind.NonWordBoundary:
                return !IsWordBoundary(input, position, options);
            default:
                throw new NotSupportedException($"The backtracking profiler does not emulate the anchor {kind}.");
        }
    }

    private static bool IsEndOfLine(string input, int position) =>
        position == input.Length || input[position] == Newline;

    private static bool IsEndOrBeforeFinalNewline(string input, int position) =>
        position == input.Length || (position == input.Length - 1 && input[position] == Newline);

    private static bool IsWordBoundary(string input, int position, RegexOptions options)
    {
        bool before = position > 0 && IsBoundaryWordChar(input[position - 1], options);
        bool after = position < input.Length && IsBoundaryWordChar(input[position], options);
        return before != after;
    }

    private static bool IsBoundaryWordChar(char c, RegexOptions options)
    {
        bool ecmaScript = (options & RegexOptions.ECMAScript) != 0;
        if (!ecmaScript && (c == ZeroWidthJoiner || c == ZeroWidthNonJoiner))
        {
            return true;
        }

        return CharSet.ForSource(WordClass, options & RegexOptions.ECMAScript).Contains(c);
    }
}
