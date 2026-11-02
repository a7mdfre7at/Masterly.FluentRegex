#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Export;

/// <summary>Character-level translation: literals, classes and anchors.</summary>
internal sealed partial class EcmaScriptTranslator
{
    // .NET definitions, verified against the .NET engine: \d = \p{Nd}; \w = [\p{L}\p{Mn}\p{Nd}\p{Pc}];
    // \s = [\f\n\r\t\v\x85\p{Z}]; \b additionally treats U+200C and U+200D as word characters.
    private const string UnicodeDigit = @"\p{Nd}";
    private const string UnicodeNonDigit = @"\P{Nd}";
    private const string UnicodeWordMembers = @"\p{L}\p{Mn}\p{Nd}\p{Pc}";
    private const string UnicodeSpaceMembers = @"\t\n\v\f\r\x85\p{Z}";
    private const string BoundaryWordClass = @"[\p{L}\p{Mn}\p{Nd}\p{Pc}\u{200C}\u{200D}]";
    private const int BmpSize = 0x10000;
    private const string EcmaSpaceMembers = @" \f\n\r\t\v";
    private const string AnyCharacter = @"[\s\S]";
    private const string AnyButNewline = @"[^\n]";
    private const string BlockPrefix = "Is";

    private const string DigitWarning = @"'\d' matches only ASCII digits in JavaScript; .NET also matches other Unicode decimal digits.";
    private const string WordWarning = @"'\w' and '\b' use ASCII word characters in JavaScript; .NET uses Unicode letters, digits and connectors.";
    private const string SpaceWarning = @"'\s' differs: JavaScript includes U+FEFF and excludes U+0085, .NET the opposite.";

    // ------------------------------------------------------------------
    // Literals and sets
    // ------------------------------------------------------------------

    private string EmitLiteral(LiteralNode literal)
    {
        if (char.IsSurrogate(literal.Value))
        {
            _astralPositions.Add(literal.Start);
        }

        if (NeedsCaseExpansion(literal))
        {
            return EmitSet(CharSet.ForNode(literal), literal);
        }

        return EcmaEscaper.Literal(literal.Value, EscapeSlash);
    }

    private bool NeedsCaseExpansion(RegexNode node) =>
        (node.Options & RegexOptions.IgnoreCase) != 0 && !_plan.IgnoreCaseFlag && TranslationPlan.IsCaseSensitive(node);

    private string EmitSet(CharSet set, RegexNode node)
    {
        if (EcmaEscaper.ContainsSurrogate(set))
        {
            _astralPositions.Add(node.Start);
        }

        return EcmaEscaper.Set(set, EscapeSlash);
    }

    /// <summary>
    /// The exact .NET set of a node. With the <c>i</c> flag the set is computed case-sensitively,
    /// because the JavaScript engine applies case folding itself.
    /// </summary>
    private CharSet SetToEmit(RegexNode node)
    {
        CharSet? set = _plan.IgnoreCaseFlag
            ? TranslationPlan.CharSetWithout(node, RegexOptions.IgnoreCase)
            : CharSet.ForNode(node);
        return set ?? CharSet.Empty;
    }

    // ------------------------------------------------------------------
    // Character classes
    // ------------------------------------------------------------------

    private string EmitCharClass(CharClassNode node)
    {
        CharSet? exact = CharSet.ForNode(node);
        if (exact != null && EcmaEscaper.ContainsSurrogate(exact))
        {
            _astralPositions.Add(node.Start);
        }

        if (NeedsCaseExpansion(node))
        {
            return EcmaEscaper.Set(exact ?? CharSet.Empty, EscapeSlash);
        }

        switch (node.ClassKind)
        {
            case ClassKind.Any:
                return EmitDot(node);
            case ClassKind.Category:
            case ClassKind.NotCategory:
                return IsBlock(node.Category) ? EmitSet(SetToEmit(node), node) : CategoryText(node.ClassKind, node.Category);
            case ClassKind.Bracket:
                return EmitBracket(node);
            default:
                return StandaloneShorthand(node.ClassKind, node);
        }
    }

    private string EmitDot(CharClassNode node)
    {
        bool singleline = (node.Options & RegexOptions.Singleline) != 0;
        if (singleline)
        {
            return _plan.SinglelineFlag ? "." : AnyCharacter;
        }

        if (PreserveLines)
        {
            return AnyButNewline;
        }

        Warn($"'.' {At(node)} also stops at \\r, U+2028 and U+2029 in JavaScript; .NET only stops at \\n.");
        return ".";
    }

    private static bool IsBlock(string category) => category.StartsWith(BlockPrefix, StringComparison.Ordinal);

    private static string CategoryText(ClassKind kind, string category) =>
        kind == ClassKind.Category ? $"\\p{{{category}}}" : $"\\P{{{category}}}";

    private string StandaloneShorthand(ClassKind kind, RegexNode node)
    {
        if ((node.Options & RegexOptions.ECMAScript) != 0)
        {
            return EcmaShorthand(kind);
        }

        if (!PreserveUnicode)
        {
            WarnAsciiShorthand(kind);
            return AsciiShorthand(kind);
        }

        switch (kind)
        {
            case ClassKind.Digit:
                return UnicodeDigit;
            case ClassKind.NotDigit:
                return UnicodeNonDigit;
            case ClassKind.Word:
                return $"[{UnicodeWordMembers}]";
            case ClassKind.NotWord:
                return $"[^{UnicodeWordMembers}]";
            case ClassKind.Space:
                return $"[{UnicodeSpaceMembers}]";
            default:
                return $"[^{UnicodeSpaceMembers}]";
        }
    }

    /// <summary>.NET's ECMAScript option already gives \d and \w their JavaScript meaning; \s is ASCII there.</summary>
    private static string EcmaShorthand(ClassKind kind)
    {
        switch (kind)
        {
            case ClassKind.Space:
                return $"[{EcmaSpaceMembers}]";
            case ClassKind.NotSpace:
                return $"[^{EcmaSpaceMembers}]";
            default:
                return AsciiShorthand(kind);
        }
    }

    private static string AsciiShorthand(ClassKind kind)
    {
        switch (kind)
        {
            case ClassKind.Digit:
                return @"\d";
            case ClassKind.NotDigit:
                return @"\D";
            case ClassKind.Word:
                return @"\w";
            case ClassKind.NotWord:
                return @"\W";
            case ClassKind.Space:
                return @"\s";
            default:
                return @"\S";
        }
    }

    private void WarnAsciiShorthand(ClassKind kind) => WarnAsciiShorthand(kind, _warnings);

    private void WarnAsciiShorthand(ClassKind kind, List<string> warnings)
    {
        string warning = kind == ClassKind.Digit || kind == ClassKind.NotDigit ? DigitWarning
            : kind == ClassKind.Word || kind == ClassKind.NotWord ? WordWarning
            : SpaceWarning;
        if (!warnings.Contains(warning))
        {
            warnings.Add(warning);
        }
    }

    private string EmitBracket(CharClassNode node)
    {
        BracketClass bracket = node.Bracket;
        List<string> pendingWarnings = new List<string>();
        BracketParts? parts = bracket.Subtraction == null ? SplitBracket(bracket, node, pendingWarnings) : null;
        if (parts == null)
        {
            // Subtraction or a Unicode block: write the exact .NET set instead.
            return EmitSet(SetToEmit(node), node);
        }

        foreach (string warning in pendingWarnings)
        {
            Warn(warning);
        }

        if (parts.Complements.Count == 0)
        {
            return bracket.Negated ? $"[^{parts.Members}]" : $"[{parts.Members}]";
        }

        CharSet? exact = CharSet.ForNode(node);
        if (exact != null && exact.Count == BmpSize)
        {
            return AnyCharacter;
        }

        return bracket.Negated ? NegatedUnion(parts) : Union(parts);
    }

    /// <summary>[a\W] is "a member, or not a word character": an alternation of classes.</summary>
    private static string Union(BracketParts parts)
    {
        List<string> alternatives = new List<string>();
        if (parts.Members.Length > 0)
        {
            alternatives.Add($"[{parts.Members}]");
        }

        alternatives.AddRange(parts.Complements.Select(complement => $"[^{complement}]"));
        return alternatives.Count == 1 ? alternatives[0] : $"(?:{string.Join("|", alternatives)})";
    }

    /// <summary>[^a\W] is "not a member, and a word character": lookaheads, then one class.</summary>
    private static string NegatedUnion(BracketParts parts)
    {
        StringBuilder sb = new StringBuilder();
        if (parts.Members.Length > 0)
        {
            sb.Append($"(?![{parts.Members}])");
        }

        for (int i = 0; i < parts.Complements.Count - 1; i++)
        {
            sb.Append($"(?=[{parts.Complements[i]}])");
        }

        sb.Append($"[{parts.Complements[parts.Complements.Count - 1]}]");
        return parts.Complements.Count == 1 && parts.Members.Length == 0 ? sb.ToString() : $"(?:{sb})";
    }

    private BracketParts? SplitBracket(BracketClass bracket, RegexNode node, List<string> warnings)
    {
        StringBuilder members = new StringBuilder();
        List<string> complements = new List<string>();
        foreach (ClassItem item in bracket.Items)
        {
            string? member = ClassMemberText(item, node, warnings);
            if (member != null)
            {
                members.Append(member);
                continue;
            }

            string? complement = ComplementMembers(item, node);
            if (complement == null)
            {
                return null;
            }

            complements.Add(complement);
        }

        return new BracketParts(members.ToString(), complements);
    }

    /// <summary>The positive members of a negated multi-property shorthand such as \W or \S.</summary>
    private static string? ComplementMembers(ClassItem item, RegexNode node)
    {
        bool ecma = (node.Options & RegexOptions.ECMAScript) != 0;
        switch (item.ShorthandKind)
        {
            case ClassKind.NotWord:
                return UnicodeWordMembers;
            case ClassKind.NotSpace:
                return ecma ? EcmaSpaceMembers : UnicodeSpaceMembers;
            default:
                return null;
        }
    }

    private sealed class BracketParts
    {
        public BracketParts(string members, List<string> complements)
        {
            Members = members;
            Complements = complements;
        }

        public string Members { get; }

        public List<string> Complements { get; }
    }

    private string? ClassMemberText(ClassItem item, RegexNode node, List<string> warnings)
    {
        if (item.ShorthandKind == null)
        {
            return EcmaEscaper.ClassRange(item.Low, item.High, EscapeSlash);
        }

        ClassKind kind = item.ShorthandKind.Value;
        if (kind == ClassKind.Category || kind == ClassKind.NotCategory)
        {
            return IsBlock(item.Category) ? null : CategoryText(kind, item.Category);
        }

        if ((node.Options & RegexOptions.ECMAScript) != 0)
        {
            return kind == ClassKind.NotSpace ? null : kind == ClassKind.Space ? EcmaSpaceMembers : AsciiShorthand(kind);
        }

        if (!PreserveUnicode)
        {
            WarnAsciiShorthand(kind, warnings);
            return AsciiShorthand(kind);
        }

        return UnicodeClassMember(kind);
    }

    /// <summary>Inline members for a class; negated multi-property shorthands have none.</summary>
    private static string? UnicodeClassMember(ClassKind kind)
    {
        switch (kind)
        {
            case ClassKind.Digit:
                return UnicodeDigit;
            case ClassKind.NotDigit:
                return UnicodeNonDigit;
            case ClassKind.Word:
                return UnicodeWordMembers;
            case ClassKind.Space:
                return UnicodeSpaceMembers;
            default:
                return null;
        }
    }

    // ------------------------------------------------------------------
    // Anchors
    // ------------------------------------------------------------------

    private string EmitAnchor(AnchorNode anchor)
    {
        bool multiline = (anchor.Options & RegexOptions.Multiline) != 0;
        switch (anchor.AnchorKind)
        {
            case AnchorKind.StartOfLine:
                return multiline ? MultilineStart() : StartOfInput();
            case AnchorKind.EndOfLine:
                return multiline ? MultilineEnd() : EndOfLine(anchor);
            case AnchorKind.StartOfInput:
                return StartOfInput();
            case AnchorKind.EndOfInput:
                return EndOfInput();
            case AnchorKind.EndOfInputOrNewline:
                return $"(?=\\n?{EndOfInput()})";
            case AnchorKind.WordBoundary:
                return WordBoundary(anchor, negated: false);
            case AnchorKind.NonWordBoundary:
                return WordBoundary(anchor, negated: true);
            default:
                Unsupported(@"'\G' (contiguous-match anchor)", anchor);
                return string.Empty;
        }
    }

    // With the m flag, ^ and $ mean line positions, so input positions use lookarounds instead.
    private string StartOfInput() => _plan.MultilineFlag ? @"(?<![\s\S])" : "^";

    private string EndOfInput() => _plan.MultilineFlag ? @"(?![\s\S])" : "$";

    private string EndOfLine(AnchorNode anchor)
    {
        if (PreserveLines)
        {
            return $"(?=\\n?{EndOfInput()})";
        }

        Warn($"'$' {At(anchor)}: .NET also matches before a final '\\n'; JavaScript only at the very end.");
        return EndOfInput();
    }

    private string MultilineStart()
    {
        if (_plan.MultilineFlag)
        {
            WarnLineTerminators();
            return "^";
        }

        return @"(?<=^|\n)";
    }

    private string MultilineEnd()
    {
        if (_plan.MultilineFlag)
        {
            WarnLineTerminators();
            return "$";
        }

        return @"(?=\n|$)";
    }

    private void WarnLineTerminators() =>
        Warn("With the 'm' flag JavaScript's '^' and '$' also treat \\r, U+2028 and U+2029 as line breaks; .NET only uses '\\n'.");

    private string WordBoundary(AnchorNode anchor, bool negated)
    {
        if ((anchor.Options & RegexOptions.ECMAScript) != 0)
        {
            return negated ? @"\B" : @"\b";
        }

        if (!PreserveUnicode)
        {
            Warn(WordWarning);
            return negated ? @"\B" : @"\b";
        }

        string w = BoundaryWordClass;
        return negated
            ? $"(?:(?<={w})(?={w})|(?<!{w})(?!{w}))"
            : $"(?:(?<={w})(?!{w})|(?<!{w})(?={w}))";
    }
}
