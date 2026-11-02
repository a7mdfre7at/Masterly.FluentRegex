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
/// The translation shared by the database regex engines. Every option is written into the pattern
/// text: case-insensitivity is expanded into exact character classes (no engine folds case the way
/// .NET does), <c>.</c> and the anchors are written in forms with the .NET meaning, and
/// <c>\d</c>, <c>\w</c>, <c>\s</c> and <c>\b</c> are spelled out so the engine's own, differing
/// definitions are never used. Constructs without an equivalent are collected and reported together.
/// </summary>
internal abstract class SqlRegexTranslator
{
    protected const string UnicodeWordMembers = @"\p{L}\p{Mn}\p{Nd}\p{Pc}";
    protected const string BoundaryWordSource = @"[\p{L}\p{Mn}\p{Nd}\p{Pc}\u200C\u200D]";
    protected const string EcmaWordSource = "[0-9A-Za-z_]";
    private const string UnicodeDigit = @"\p{Nd}";
    private const string UnicodeNonDigit = @"\P{Nd}";
    private const string UnassignedCategory = "Cn";
    private const string BlockPrefix = "Is";
    private const string LineFeedEscape = @"\n";

    private readonly List<string> _warnings = new List<string>();
    private readonly List<string> _issues = new List<string>();
    private readonly HashSet<string> _groupNames = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<GroupNode, int> _numbers = new Dictionary<GroupNode, int>();
    private int _lookaroundDepth;

    protected SqlRegexTranslator(RegexTree tree, RegexFlavor flavor, HexEscapeStyle style)
    {
        Tree = tree;
        Flavor = flavor;
        Plan = new TranslationPlan(tree, flavor, new FlavorOptions());
        Chars = new SqlCharWriter(style, SupportsUnicodeCategories);
    }

    /// <summary>The parsed pattern.</summary>
    protected RegexTree Tree { get; }

    /// <summary>The target flavor.</summary>
    protected RegexFlavor Flavor { get; }

    /// <summary>Node parents, case-sensitivity analysis and .NET group lookup.</summary>
    protected TranslationPlan Plan { get; }

    /// <summary>Writes characters in the engine's escape syntax.</summary>
    protected SqlCharWriter Chars { get; }

    /// <summary>True while emitting the body of a lookaround.</summary>
    protected bool InsideLookaround => _lookaroundDepth > 0;

    // ------------------------------------------------------------------
    // Engine capabilities
    // ------------------------------------------------------------------

    /// <summary>Whether <c>\p{..}</c> and <c>\P{..}</c> general categories can be used.</summary>
    protected abstract bool SupportsUnicodeCategories { get; }

    /// <summary>Whether lookahead and lookbehind exist.</summary>
    protected abstract bool SupportsLookaround { get; }

    /// <summary>Whether backreferences exist.</summary>
    protected abstract bool SupportsBackreferences { get; }

    /// <summary>Whether atomic groups exist.</summary>
    protected abstract bool SupportsAtomicGroups { get; }

    /// <summary>The largest value allowed in a <c>{m,n}</c> bound.</summary>
    protected abstract int MaxRepeatBound { get; }

    /// <summary>Text placed before the pattern, such as <c>(?-i)</c>.</summary>
    protected virtual string Prefix => string.Empty;

    /// <summary>The atom that matches any single character, including a newline.</summary>
    protected abstract string AnyCharacter { get; }

    /// <summary>The atom that matches any single character except <c>\n</c>.</summary>
    protected abstract string AnyButNewline { get; }

    /// <summary>The anchor for the start of the input.</summary>
    protected abstract string StartOfInput { get; }

    /// <summary>The anchor for the very end of the input (<c>\z</c>).</summary>
    protected abstract string EndOfInput { get; }

    /// <summary>The text for .NET's <c>$</c> and <c>\Z</c>: end of input or before a final newline.</summary>
    protected abstract string EndOrFinalNewline(AnchorNode anchor);

    /// <summary>The text for a multiline <c>^</c>.</summary>
    protected abstract string MultilineStart(AnchorNode anchor);

    /// <summary>The text for a multiline <c>$</c>.</summary>
    protected abstract string MultilineEnd(AnchorNode anchor);

    /// <summary>The text for <c>\b</c> or <c>\B</c>, given the exact .NET word class as an atom.</summary>
    protected abstract string WordBoundary(AnchorNode anchor, bool negated, string wordClass);

    /// <summary>The opening text of a named capturing group, or null to capture without a name.</summary>
    protected abstract string? NamedGroupOpen(string name);

    /// <summary>The text of a backreference to the group with the given engine number.</summary>
    protected abstract string BackreferenceText(GroupNode group, int number);

    /// <summary>Whether a .NET capturing group captures in the engine (false inside PostgreSQL lookarounds).</summary>
    protected virtual bool Captures(GroupNode group) => true;

    /// <summary>Reports engine-specific limits of a lookbehind body.</summary>
    protected virtual void CheckLookbehind(GroupNode group)
    {
    }

    /// <summary>Reports engine-specific limits of a backreference's position.</summary>
    protected virtual void CheckBackreference(BackreferenceNode reference)
    {
    }

    /// <summary>Writes a repeat whose bound exceeds <see cref="MaxRepeatBound"/>, or null when impossible.</summary>
    protected virtual string? SplitLargeRepeat(QuantifierNode quantifier, string body) => null;

    /// <summary>Adds warnings that depend on the whole pattern.</summary>
    protected virtual void AddSummaryWarnings()
    {
    }

    // ------------------------------------------------------------------
    // Entry points
    // ------------------------------------------------------------------

    /// <summary>Translates <paramref name="tree"/> to a SQL flavor, or throws listing every obstacle.</summary>
    public static FlavorConversion Translate(RegexTree tree, RegexOptions options, RegexFlavor flavor)
    {
        SqlRegexTranslator translator = Create(tree, flavor);
        return translator.Run(options);
    }

    /// <summary>Creates the exception that lists every unsupported construct.</summary>
    public static NotSupportedException UnsupportedException(string engine, IEnumerable<string> issues)
    {
        string list = string.Join("\n", issues.Select(issue => $"  - {issue}"));
        return new NotSupportedException($"The pattern cannot be converted to {engine} without changing its meaning:\n{list}");
    }

    private static SqlRegexTranslator Create(RegexTree tree, RegexFlavor flavor)
    {
        switch (flavor)
        {
            case RegexFlavor.PostgreSql:
                return new PostgreSqlTranslator(tree);
            case RegexFlavor.MySql:
                return new MySqlTranslator(tree);
            case RegexFlavor.MariaDb:
                return new MariaDbTranslator(tree);
            case RegexFlavor.SqlServer:
                return new SqlServerTranslator(tree);
            default:
                throw new ArgumentOutOfRangeException(nameof(flavor), flavor, "Not a regex-engine SQL flavor.");
        }
    }

    private FlavorConversion Run(RegexOptions options)
    {
        if ((options & RegexOptions.RightToLeft) != 0)
        {
            _issues.Add($"RegexOptions.RightToLeft ({EngineName} only matches left to right)");
        }

        NumberGroups();
        string body = Emit(Tree.Root);
        AddSummaryWarnings();
        if (_issues.Count > 0)
        {
            throw UnsupportedException(EngineName, _issues);
        }

        return new FlavorConversion(Flavor, Prefix + body, string.Empty, _warnings, TranslationPlan.BuildGroupNumbers(_numbers));
    }

    protected string EngineName => SqlDialect.Name(Flavor);

    private void NumberGroups()
    {
        int next = 1;
        foreach (GroupNode group in Tree.Groups.Where(g => g.IsCapturing).OrderBy(g => g.Start))
        {
            if (Captures(group))
            {
                _numbers[group] = next++;
            }
        }
    }

    /// <summary>Gets a value indicating whether the node sits inside a lookaround.</summary>
    protected bool IsInsideLookaround(RegexNode node)
    {
        for (RegexNode? parent = Plan.ParentOf(node); parent != null; parent = Plan.ParentOf(parent))
        {
            if (parent is GroupNode group && group.IsLookaround)
            {
                return true;
            }
        }

        return false;
    }

    // ------------------------------------------------------------------
    // Reporting
    // ------------------------------------------------------------------

    protected void Warn(string warning)
    {
        if (!_warnings.Contains(warning))
        {
            _warnings.Add(warning);
        }
    }

    protected void Unsupported(string construct, RegexNode node) =>
        _issues.Add($"{construct} at position {node.Start.ToString(CultureInfo.InvariantCulture)}");

    protected static string At(RegexNode node) => $"at position {node.Start.ToString(CultureInfo.InvariantCulture)}";

    // ------------------------------------------------------------------
    // Structure
    // ------------------------------------------------------------------

    protected string Emit(RegexNode node)
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
        IReadOnlyList<RegexNode> items = sequence.Items;
        for (int i = 0; i < items.Count; i++)
        {
            RegexNode item = items[i];
            string text;
            if (IsSurrogatePair(items, i))
            {
                text = EmitSupplementary((LiteralNode)item, (LiteralNode)items[i + 1]);
                i++;
            }
            else
            {
                text = Emit(item);
            }

            if (afterNumericReference && text.Length > 0 && IsAsciiDigit(text[0]))
            {
                text = Chars.Hex(text[0]) + text.Substring(1); // keeps \1 followed by 0 from reading as \10
            }

            sb.Append(text);
            afterNumericReference = item is BackreferenceNode && text.Length > 0 && IsAsciiDigit(text[text.Length - 1]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a value indicating whether the items at <paramref name="index"/> and the next index are the two
    /// halves of a supplementary character written literally, such as an emoji in the pattern text.
    /// </summary>
    private static bool IsSurrogatePair(IReadOnlyList<RegexNode> items, int index) =>
        index + 1 < items.Count
        && items[index] is LiteralNode high && char.IsHighSurrogate(high.Value)
        && items[index + 1] is LiteralNode low && char.IsLowSurrogate(low.Value);

    /// <summary>
    /// Writes a literal surrogate pair as one code point. The engines match code points, so the two
    /// halves written separately would be rejected (PCRE2) or could never match (a half is not a character).
    /// </summary>
    private string EmitSupplementary(LiteralNode high, LiteralNode low) =>
        Chars.CodePoint(char.ConvertToUtf32(high.Value, low.Value));

    private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

    private string EmitQuantifier(QuantifierNode quantifier)
    {
        if (IsZeroWidth(quantifier.Body))
        {
            Unsupported($"quantifier '{quantifier.QuantifierText}' on a zero-width construct", quantifier);
            return string.Empty;
        }

        string body = Emit(quantifier.Body);
        if (ExceedsBound(quantifier))
        {
            return SplitLargeRepeat(quantifier, body) ?? ReportBound(quantifier);
        }

        return $"{body}{QuantifierText(quantifier)}";
    }

    private static bool IsZeroWidth(RegexNode node)
    {
        switch (node)
        {
            case AnchorNode _:
            case EmptyNode _:
            case InlineOptionsNode _:
            case CommentNode _:
                return true;
            case GroupNode group:
                return group.IsLookaround;
            default:
                return false;
        }
    }

    private bool ExceedsBound(QuantifierNode quantifier) =>
        quantifier.Min > MaxRepeatBound || (!quantifier.IsUnbounded && quantifier.Max > MaxRepeatBound);

    private string ReportBound(QuantifierNode quantifier)
    {
        Unsupported($"repeat '{quantifier.QuantifierText}' (bounds above {MaxRepeatBound.ToString(CultureInfo.InvariantCulture)})", quantifier);
        return string.Empty;
    }

    /// <summary>Writes a quantifier for the given bounds, with a lazy marker when requested.</summary>
    protected static string QuantifierText(int min, int max, bool lazy)
    {
        string lazyText = lazy ? "?" : string.Empty;
        string minText = min.ToString(CultureInfo.InvariantCulture);
        if (max == QuantifierNode.Unbounded)
        {
            string unbounded = min == 0 ? "*" : min == 1 ? "+" : $"{{{minText},}}";
            return $"{unbounded}{lazyText}";
        }

        if (min == 0 && max == 1)
        {
            return $"?{lazyText}";
        }

        string maxText = max.ToString(CultureInfo.InvariantCulture);
        string bounded = min == max ? $"{{{minText}}}" : $"{{{minText},{maxText}}}";
        return $"{bounded}{lazyText}";
    }

    private static string QuantifierText(QuantifierNode quantifier) =>
        QuantifierText(quantifier.Min, quantifier.Max, quantifier.Lazy);

    private string EmitGroup(GroupNode group)
    {
        switch (group.GroupKind)
        {
            case GroupKind.Capturing:
            case GroupKind.Named:
                return $"{CaptureOpen(group)}{Emit(group.Body)})";
            case GroupKind.Lookahead:
                return EmitLookaround(group, "(?=");
            case GroupKind.NegativeLookahead:
                return EmitLookaround(group, "(?!");
            case GroupKind.Lookbehind:
                CheckLookbehind(group);
                return EmitLookaround(group, "(?<=");
            case GroupKind.NegativeLookbehind:
                CheckLookbehind(group);
                return EmitLookaround(group, "(?<!");
            case GroupKind.Atomic:
                return EmitAtomic(group);
            case GroupKind.Balancing:
                Unsupported($"balancing group '{group.OpenText}...)'", group);
                return Emit(group.Body);
            default:
                return $"(?:{Emit(group.Body)})"; // non-capturing and scoped-option groups; options already live on the nodes
        }
    }

    private string CaptureOpen(GroupNode group)
    {
        if (!_numbers.ContainsKey(group))
        {
            return "(?:";
        }

        if (group.Name == null)
        {
            return "(";
        }

        if (!_groupNames.Add(group.Name))
        {
            Unsupported($"duplicate group name '{group.Name}'", group);
        }

        return NamedGroupOpen(group.Name) ?? "(";
    }

    private string EmitLookaround(GroupNode group, string open)
    {
        if (!SupportsLookaround)
        {
            Unsupported($"lookaround '{group.OpenText}...)'", group);
        }

        _lookaroundDepth++;
        string body = Emit(group.Body);
        _lookaroundDepth--;
        return $"{open}{body})";
    }

    private string EmitAtomic(GroupNode group)
    {
        if (!SupportsAtomicGroups)
        {
            Unsupported("atomic group '(?>...)'", group);
        }

        return $"(?>{Emit(group.Body)})";
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
        if (!SupportsBackreferences)
        {
            Unsupported("backreference", reference);
            return string.Empty;
        }

        if ((reference.Options & RegexOptions.IgnoreCase) != 0)
        {
            Unsupported($"case-insensitive backreference (no SQL engine folds case like .NET does)", reference);
        }

        GroupNode? group = ResolveGroup(reference);
        if (group == null)
        {
            Unsupported("backreference to an undefined group", reference);
            return string.Empty;
        }

        if (group.End > reference.Start)
        {
            Unsupported("backreference to a group that does not entirely precede it", reference);
        }

        if (!_numbers.TryGetValue(group, out int number))
        {
            Unsupported("backreference to a group that cannot capture in this engine", reference);
            return string.Empty;
        }

        CheckBackreference(reference);
        return BackreferenceText(group, number);
    }

    private GroupNode? ResolveGroup(BackreferenceNode reference)
    {
        if (reference.Name != null)
        {
            return Tree.Groups.FirstOrDefault(g => g.IsCapturing && g.Name == reference.Name);
        }

        return Plan.GroupByNumber(reference.Number);
    }

    // ------------------------------------------------------------------
    // Characters
    // ------------------------------------------------------------------

    private bool NeedsCaseExpansion(RegexNode node) =>
        (node.Options & RegexOptions.IgnoreCase) != 0 && TranslationPlan.IsCaseSensitive(node);

    private string EmitLiteral(LiteralNode literal)
    {
        if (char.IsSurrogate(literal.Value))
        {
            Unsupported("a lone surrogate (the engine matches whole code points, never half of one)", literal);
            return string.Empty;
        }

        if (NeedsCaseExpansion(literal))
        {
            return EmitSet(CharSet.ForNode(literal) ?? CharSet.Empty, literal);
        }

        return Chars.Literal(literal.Value);
    }

    /// <summary>Writes an exact set, reporting sets that cannot be written at all.</summary>
    protected string EmitSet(CharSet set, RegexNode node)
    {
        if (SqlCharWriter.IsEverything(set))
        {
            return AnyCharacter;
        }

        string? text = Chars.Set(set);
        if (text == null)
        {
            Unsupported("a character class that matches nothing (or only lone surrogates)", node);
            return string.Empty;
        }

        return text;
    }

    private string EmitCharClass(CharClassNode node)
    {
        if (node.ClassKind == ClassKind.Any)
        {
            return (node.Options & RegexOptions.Singleline) != 0 ? AnyCharacter : AnyButNewline;
        }

        CharSet exact = CharSet.ForNode(node) ?? CharSet.Empty;
        bool mustExpand = NeedsCaseExpansion(node) || !SupportsUnicodeCategories || (node.Options & RegexOptions.ECMAScript) != 0;
        if (mustExpand)
        {
            return EmitSet(exact, node);
        }

        switch (node.ClassKind)
        {
            case ClassKind.Category:
            case ClassKind.NotCategory:
                return IsNativeCategory(node.Category) ? CategoryText(node.ClassKind, node.Category) : EmitSet(exact, node);
            case ClassKind.Bracket:
                return EmitBracket(node, exact);
            default:
                return StandaloneShorthand(node.ClassKind);
        }
    }

    private static bool IsNativeCategory(string category) =>
        !category.StartsWith(BlockPrefix, StringComparison.Ordinal) && category != UnassignedCategory;

    private static string CategoryText(ClassKind kind, string category) =>
        kind == ClassKind.Category ? $"\\p{{{category}}}" : $"\\P{{{category}}}";

    private string SpaceMembers => $"\\t{LineFeedEscape}{Chars.Hex('\v')}\\f\\r{Chars.Hex('\u0085')}\\p{{Z}}";

    private string StandaloneShorthand(ClassKind kind)
    {
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
                return $"[{SpaceMembers}]";
            default:
                return $"[^{SpaceMembers}]";
        }
    }

    private string EmitBracket(CharClassNode node, CharSet exact)
    {
        BracketClass bracket = node.Bracket;
        if (bracket.Subtraction != null)
        {
            return EmitSet(exact, node);
        }

        StringBuilder members = new StringBuilder();
        foreach (ClassItem item in bracket.Items)
        {
            string? member = ClassMemberText(item);
            if (member == null)
            {
                return EmitSet(exact, node); // \W, \S or a block: write the exact set instead
            }

            members.Append(member);
        }

        return bracket.Negated ? $"[^{members}]" : $"[{members}]";
    }

    private string? ClassMemberText(ClassItem item)
    {
        if (item.ShorthandKind == null)
        {
            // A range that touches the surrogates is written from the exact set, which never names a surrogate.
            return SqlCharWriter.OverlapsSurrogates(item.Low, item.High) ? null : Chars.ClassRange(item.Low, item.High);
        }

        switch (item.ShorthandKind.Value)
        {
            case ClassKind.Digit:
                return UnicodeDigit;
            case ClassKind.NotDigit:
                return UnicodeNonDigit;
            case ClassKind.Word:
                return UnicodeWordMembers;
            case ClassKind.Space:
                return SpaceMembers;
            case ClassKind.Category:
            case ClassKind.NotCategory:
                return IsNativeCategory(item.Category) ? CategoryText(item.ShorthandKind.Value, item.Category) : null;
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
                return multiline ? MultilineStart(anchor) : StartOfInput;
            case AnchorKind.EndOfLine:
                return multiline ? MultilineEnd(anchor) : EndOrFinalNewline(anchor);
            case AnchorKind.StartOfInput:
                return StartOfInput;
            case AnchorKind.EndOfInput:
                return EndOfInput;
            case AnchorKind.EndOfInputOrNewline:
                return EndOrFinalNewline(anchor);
            case AnchorKind.WordBoundary:
                return WordBoundary(anchor, negated: false, WordClass(anchor));
            case AnchorKind.NonWordBoundary:
                return WordBoundary(anchor, negated: true, WordClass(anchor));
            default:
                Unsupported(@"'\G' (contiguous-match anchor)", anchor);
                return string.Empty;
        }
    }

    /// <summary>The exact .NET word class of <c>\b</c> as one atom (ASCII under RegexOptions.ECMAScript).</summary>
    private string WordClass(AnchorNode anchor)
    {
        bool ecma = (anchor.Options & RegexOptions.ECMAScript) != 0;
        if (SupportsUnicodeCategories && !ecma)
        {
            return $"[{UnicodeWordMembers}{Chars.Hex('\u200C')}{Chars.Hex('\u200D')}]";
        }

        CharSet set = CharSet.ForSource(ecma ? EcmaWordSource : BoundaryWordSource, RegexOptions.None);
        return Chars.Set(set) ?? string.Empty;
    }

    /// <summary>Writes <c>\b</c> (or <c>\B</c>) as lookarounds over the word class, which every engine with lookaround supports.</summary>
    protected static string LookaroundBoundary(bool negated, string wordClass) =>
        negated
            ? $"(?:(?<={wordClass})(?={wordClass})|(?<!{wordClass})(?!{wordClass}))"
            : $"(?:(?<={wordClass})(?!{wordClass})|(?<!{wordClass})(?={wordClass}))";

    /// <summary>Gets a value indicating whether nothing in the pattern can be matched after <paramref name="node"/>.</summary>
    protected bool IsTrailing(RegexNode node)
    {
        RegexNode current = node;
        for (RegexNode? parent = Plan.ParentOf(node); parent != null; current = parent, parent = Plan.ParentOf(parent))
        {
            if (!IsLastWithin(current, parent))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLastWithin(RegexNode child, RegexNode parent)
    {
        switch (parent)
        {
            case SequenceNode sequence:
                return sequence.Items[sequence.Items.Count - 1] == child;
            case AlternationNode _:
                return true;
            case GroupNode group:
                return !group.IsLookaround && group.GroupKind != GroupKind.Balancing;
            default:
                return false; // quantifiers and conditionals can run the body again
        }
    }

    /// <summary>Sums the fixed length of a lookbehind body in code points, or returns null when it can vary.</summary>
    protected static int? FixedLength(RegexNode node)
    {
        switch (node)
        {
            case LiteralNode literal:
                return char.IsHighSurrogate(literal.Value) ? 0 : 1; // a surrogate pair is one code point in the engine
            case CharClassNode _:
                return 1;
            case SequenceNode sequence:
                return SumFixed(sequence.Items);
            case AlternationNode alternation:
                return AllSameFixed(alternation.Branches);
            case GroupNode group:
                return group.IsLookaround ? 0 : FixedLength(group.Body);
            case QuantifierNode quantifier:
                return quantifier.Min == quantifier.Max ? FixedLength(quantifier.Body) * quantifier.Min : null;
            case BackreferenceNode _:
            case ConditionalNode _:
                return null;
            default:
                return 0;
        }
    }

    private static int? SumFixed(IEnumerable<RegexNode> items)
    {
        int total = 0;
        foreach (RegexNode item in items)
        {
            int? length = FixedLength(item);
            if (length == null)
            {
                return null;
            }

            total += length.Value;
        }

        return total;
    }

    private static int? AllSameFixed(IEnumerable<RegexNode> branches)
    {
        int? common = null;
        foreach (RegexNode branch in branches)
        {
            int? length = FixedLength(branch);
            if (length == null || (common != null && common != length))
            {
                return null;
            }

            common = length;
        }

        return common;
    }

    /// <summary>Gets a value indicating whether any quantifier under <paramref name="node"/> is unbounded.</summary>
    protected static bool HasUnboundedRepeat(RegexNode node)
    {
        if (node is QuantifierNode quantifier && quantifier.IsUnbounded)
        {
            return true;
        }

        return node.Children.Any(HasUnboundedRepeat);
    }
}
