#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Export;

/// <summary>
/// Translates a parsed .NET pattern into ECMAScript 2018+ syntax (valid with the <c>u</c> flag),
/// node by node, so the result accepts the same strings. Constructs without an equivalent are
/// collected and reported together; constructs whose meaning shifts produce warnings.
/// </summary>
internal sealed partial class EcmaScriptTranslator
{
    private const string UnicodeFlag = "u";
    private const string IgnoreCaseFlag = "i";
    private const string MultilineFlag = "m";
    private const string SinglelineFlag = "s";
    private const string EmptyGroup = "(?:)";
    private const int MaximumListedPositions = 8;

    private readonly RegexTree _tree;
    private readonly RegexFlavor _flavor;
    private readonly FlavorOptions _options;
    private readonly TranslationPlan _plan;
    private readonly List<string> _warnings = new List<string>();
    private readonly List<string> _issues = new List<string>();
    private readonly SortedSet<int> _astralPositions = new SortedSet<int>();
    private readonly HashSet<string> _groupNames = new HashSet<string>(StringComparer.Ordinal);

    private EcmaScriptTranslator(RegexTree tree, RegexFlavor flavor, FlavorOptions options)
    {
        _tree = tree;
        _flavor = flavor;
        _options = options;
        _plan = new TranslationPlan(tree, flavor, options);
    }

    private bool EscapeSlash => _flavor == RegexFlavor.JavaScript;

    private bool PreserveUnicode => _options.PreserveUnicodeSemantics;

    private bool PreserveLines => _options.PreserveLineSemantics;

    /// <summary>Translates <paramref name="tree"/> or throws a <see cref="NotSupportedException"/> listing every obstacle.</summary>
    public static FlavorConversion Translate(RegexTree tree, RegexOptions options, RegexFlavor flavor, FlavorOptions flavorOptions)
    {
        EcmaScriptTranslator translator = new EcmaScriptTranslator(tree, flavor, flavorOptions);
        translator.CheckGlobalOptions(options);
        string pattern = translator.Emit(tree.Root);
        translator.AddSummaryWarnings();
        translator.ThrowIfUnsupported();
        return new FlavorConversion(flavor, pattern, translator.Flags(), translator._warnings, translator._plan.GroupNumbers(), translator.MatchExtentWarnings());
    }

    /// <summary>
    /// ECMAScript engines backtrack with .NET's preferences, so the matched substring is the same; but every
    /// repeat iteration resets the groups inside it (RepeatMatcher), while .NET keeps an earlier iteration's
    /// capture, and an iteration that matches empty text is refused, while .NET can end with one (verified
    /// in Node.js: <c>/(?:(a)|b)+/.exec("ab")[1]</c> is undefined, <c>/(a?)+/.exec("aa")[1]</c> is "a").
    /// </summary>
    private List<string> MatchExtentWarnings()
    {
        List<string> warnings = new List<string>();
        foreach (GroupNode group in _tree.Groups.Where(g => g.IsCapturing).OrderBy(g => g.Start))
        {
            if (_plan.MayLoseCaptureBetweenIterations(group))
            {
                warnings.Add($"The group {At(group)} can be skipped by an iteration of an enclosing repeat: .NET keeps its " +
                             "capture from an earlier iteration, JavaScript resets it on every iteration.");
            }
        }

        QuantifierNode? repeat = _plan.FirstNullableRepeat();
        if (repeat != null)
        {
            warnings.Add($"The repeat {At(repeat)} can run an iteration that matches empty text: .NET stops the repeat " +
                         "there, while JavaScript refuses empty iterations and tries the body's other ways, so the match " +
                         "and the captures can differ.");
        }

        return warnings;
    }

    private void CheckGlobalOptions(RegexOptions options)
    {
        if ((options & RegexOptions.RightToLeft) != 0)
        {
            _issues.Add("RegexOptions.RightToLeft (ECMAScript only matches left to right)");
        }
    }

    private string Flags()
    {
        if (_flavor == RegexFlavor.JsonSchema)
        {
            return string.Empty;
        }

        StringBuilder flags = new StringBuilder();
        flags.Append(_plan.IgnoreCaseFlag ? IgnoreCaseFlag : string.Empty);
        flags.Append(_plan.MultilineFlag ? MultilineFlag : string.Empty);
        flags.Append(_plan.SinglelineFlag ? SinglelineFlag : string.Empty);
        flags.Append(UnicodeFlag);
        return flags.ToString();
    }

    private void ThrowIfUnsupported()
    {
        if (_issues.Count == 0)
        {
            return;
        }

        throw SqlRegexTranslator.UnsupportedException(SqlDialect.Name(_flavor), _issues);
    }

    private void AddSummaryWarnings()
    {
        if (_plan.IgnoreCaseFlag)
        {
            Warn("Case-insensitive matching: JavaScript's 'i' flag uses Unicode simple case folding, which also " +
                 "equates characters .NET keeps distinct (for example 's' and U+017F, U+00B5 and U+03BC). " +
                 "RegexFlavor.JsonSchema expands case into exact character classes instead.");
        }

        if (_plan.NumberingDiffers)
        {
            Warn("Capture group numbers differ: .NET numbers unnamed groups before named ones, JavaScript numbers " +
                 "all groups left to right. Refer to groups by name.");
        }

        if (_astralPositions.Count > 0)
        {
            Warn($"Supplementary characters such as emoji: the construct(s) at {Positions(_astralPositions)} match " +
                 "one UTF-16 code unit in .NET but a whole code point in JavaScript with the 'u' flag.");
        }
    }

    private static string Positions(IEnumerable<int> positions)
    {
        List<int> list = positions.ToList();
        IEnumerable<string> shown = list.Take(MaximumListedPositions).Select(p => p.ToString(CultureInfo.InvariantCulture));
        string more = list.Count > MaximumListedPositions ? ", ..." : string.Empty;
        string noun = list.Count == 1 ? "position" : "positions";
        return $"{noun} {string.Join(", ", shown)}{more}";
    }

    private void Warn(string warning)
    {
        if (!_warnings.Contains(warning))
        {
            _warnings.Add(warning);
        }
    }

    private void Unsupported(string construct, RegexNode node) =>
        _issues.Add($"{construct} at position {node.Start.ToString(CultureInfo.InvariantCulture)}");

    private static string At(RegexNode node) => $"at position {node.Start.ToString(CultureInfo.InvariantCulture)}";

    // ------------------------------------------------------------------
    // Structure
    // ------------------------------------------------------------------

    private string Emit(RegexNode node)
    {
        switch (node)
        {
            case SequenceNode sequence:
                return EmitSequence(sequence);
            case AlternationNode alternation:
                return string.Join("|", alternation.Branches.Select(Emit));
            case LiteralNode literal:
                return EmitLiteral(literal);
            case CharClassNode charClass:
                return EmitCharClass(charClass);
            case AnchorNode anchor:
                return EmitAnchor(anchor);
            case GroupNode group:
                return EmitGroup(group);
            case QuantifierNode quantifier:
                return EmitQuantifier(quantifier);
            case BackreferenceNode backreference:
                return EmitBackreference(backreference);
            case ConditionalNode conditional:
                return EmitConditional(conditional);
            default:
                return string.Empty; // Empty, inline options and comments emit nothing.
        }
    }

    private string EmitSequence(SequenceNode sequence)
    {
        StringBuilder sb = new StringBuilder();
        bool afterNumericReference = false;
        foreach (RegexNode item in sequence.Items)
        {
            string text = Emit(item);
            if (afterNumericReference && text.Length > 0 && char.IsDigit(text[0]))
            {
                sb.Append(EmptyGroup); // keeps \1 followed by 0 from reading as \10
            }

            sb.Append(text);
            afterNumericReference = item is BackreferenceNode && text.Length > 1 && char.IsDigit(text[text.Length - 1]);
        }

        return sb.ToString();
    }

    private string EmitQuantifier(QuantifierNode quantifier)
    {
        string body = Emit(quantifier.Body);
        if (!IsAtom(quantifier.Body))
        {
            body = $"(?:{body})"; // ECMAScript cannot quantify assertions or empty text directly
        }

        return $"{body}{QuantifierText(quantifier)}";
    }

    private static bool IsAtom(RegexNode node)
    {
        switch (node)
        {
            case LiteralNode _:
            case CharClassNode _:
            case BackreferenceNode _:
                return true;
            case GroupNode group:
                return !group.IsLookaround;
            default:
                return false;
        }
    }

    private static string QuantifierText(QuantifierNode quantifier)
    {
        string lazy = quantifier.Lazy ? "?" : string.Empty;
        return $"{CountText(quantifier)}{lazy}";
    }

    private static string CountText(QuantifierNode quantifier)
    {
        string min = quantifier.Min.ToString(CultureInfo.InvariantCulture);
        if (quantifier.IsUnbounded)
        {
            return quantifier.Min == 0 ? "*" : quantifier.Min == 1 ? "+" : $"{{{min},}}";
        }

        if (quantifier.Min == 0 && quantifier.Max == 1)
        {
            return "?";
        }

        string max = quantifier.Max.ToString(CultureInfo.InvariantCulture);
        return quantifier.Min == quantifier.Max ? $"{{{min}}}" : $"{{{min},{max}}}";
    }

    private string EmitGroup(GroupNode group)
    {
        string body = Emit(group.Body);
        switch (group.GroupKind)
        {
            case GroupKind.Capturing:
                return $"({body})";
            case GroupKind.Named:
                return $"(?<{CheckGroupName(group)}>{body})";
            case GroupKind.Lookahead:
                return $"(?={body})";
            case GroupKind.NegativeLookahead:
                return $"(?!{body})";
            case GroupKind.Lookbehind:
                return $"(?<={body})";
            case GroupKind.NegativeLookbehind:
                return $"(?<!{body})";
            case GroupKind.Atomic:
                Unsupported("atomic group '(?>...)'", group);
                break;
            case GroupKind.Balancing:
                Unsupported($"balancing group '{group.OpenText}...)'", group);
                break;
        }

        return $"(?:{body})"; // non-capturing and scoped-option groups; options already live on the nodes
    }

    private string CheckGroupName(GroupNode group)
    {
        string name = group.Name;
        if (!EcmaIdentifier.IsValid(name))
        {
            Unsupported($"group name '{name}' (not a valid JavaScript identifier)", group);
        }
        else if (!_groupNames.Add(name))
        {
            Unsupported($"duplicate group name '{name}'", group);
        }

        return name;
    }

    private string EmitConditional(ConditionalNode conditional)
    {
        Unsupported($"conditional '{conditional.OpenText}...)'", conditional);
        foreach (RegexNode child in conditional.Children)
        {
            Emit(child); // still visit the branches so every obstacle is reported at once
        }

        return string.Empty;
    }

    private string EmitBackreference(BackreferenceNode reference)
    {
        if (NeedsCaseExpansion(reference))
        {
            string where = _flavor == RegexFlavor.JsonSchema ? "JSON Schema, which has no flags" : "a pattern that mixes case sensitivity";
            Unsupported($"case-insensitive backreference (cannot be expressed in {where})", reference);
        }

        GroupNode? group = reference.Name != null
            ? _tree.Groups.FirstOrDefault(g => g.IsCapturing && g.Name == reference.Name)
            : _plan.GroupByNumber(reference.Number);
        if (group == null)
        {
            Unsupported("backreference to an undefined group", reference);
            return string.Empty;
        }

        if (!_plan.AlwaysParticipatesBefore(group, reference))
        {
            Warn($"The backreference {At(reference)} refers to a group that may not have participated; " +
                 ".NET then fails to match, JavaScript matches the empty string.");
        }

        return group.Name != null && EcmaIdentifier.IsValid(group.Name)
            ? $"\\k<{group.Name}>"
            : $"\\{_plan.OrdinalOf(group).ToString(CultureInfo.InvariantCulture)}";
    }

}
