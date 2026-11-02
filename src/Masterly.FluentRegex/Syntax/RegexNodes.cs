using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Syntax
{
    internal enum RegexNodeKind
    {
        Empty,
        Sequence,
        Alternation,
        Literal,
        CharClass,
        Anchor,
        Group,
        Quantifier,
        Backreference,
        Conditional,
        InlineOptions,
        Comment
    }

    internal enum ClassKind
    {
        Any,
        Digit,
        NotDigit,
        Word,
        NotWord,
        Space,
        NotSpace,
        Category,
        NotCategory,
        Bracket
    }

    internal enum AnchorKind
    {
        StartOfLine,
        EndOfLine,
        StartOfInput,
        EndOfInput,
        EndOfInputOrNewline,
        WordBoundary,
        NonWordBoundary,
        ContiguousMatch
    }

    internal enum GroupKind
    {
        Capturing,
        Named,
        NonCapturing,
        Atomic,
        Lookahead,
        NegativeLookahead,
        Lookbehind,
        NegativeLookbehind,
        Options,
        Balancing
    }

    /// <summary>
    /// A node of the regex syntax tree. Every node remembers its exact span in the source pattern,
    /// so the tree can be written back out, wrapped, or cut into prefixes without changing meaning.
    /// </summary>
    internal abstract class RegexNode
    {
        protected RegexNode(string pattern, int start, int end, RegexOptions options)
        {
            Pattern = pattern;
            Start = start;
            End = end;
            Options = options;
        }

        public abstract RegexNodeKind Kind { get; }

        /// <summary>The full source pattern this node belongs to.</summary>
        public string Pattern { get; }

        public int Start { get; internal set; }

        public int End { get; internal set; }

        /// <summary>The options in effect at this node, including inline options.</summary>
        public RegexOptions Options { get; }

        public int Length => End - Start;

        public string Source => Pattern.Substring(Start, End - Start);

        public virtual IEnumerable<RegexNode> Children
        {
            get { yield break; }
        }

        public override string ToString() => Kind + ": " + Source;
    }

    internal sealed class EmptyNode : RegexNode
    {
        public EmptyNode(string pattern, int position, RegexOptions options) : base(pattern, position, position, options) { }

        public override RegexNodeKind Kind => RegexNodeKind.Empty;
    }

    internal sealed class SequenceNode : RegexNode
    {
        public SequenceNode(string pattern, int start, int end, RegexOptions options, List<RegexNode> items)
            : base(pattern, start, end, options) => Items = items;

        public override RegexNodeKind Kind => RegexNodeKind.Sequence;

        public List<RegexNode> Items { get; }

        public override IEnumerable<RegexNode> Children => Items;
    }

    internal sealed class AlternationNode : RegexNode
    {
        public AlternationNode(string pattern, int start, int end, RegexOptions options, List<RegexNode> branches)
            : base(pattern, start, end, options) => Branches = branches;

        public override RegexNodeKind Kind => RegexNodeKind.Alternation;

        public List<RegexNode> Branches { get; }

        public override IEnumerable<RegexNode> Children => Branches;
    }

    internal sealed class LiteralNode : RegexNode
    {
        public LiteralNode(string pattern, int start, int end, RegexOptions options, char value)
            : base(pattern, start, end, options) => Value = value;

        public override RegexNodeKind Kind => RegexNodeKind.Literal;

        public char Value { get; }
    }

    /// <summary>An item inside a bracketed character class.</summary>
    internal sealed class ClassItem
    {
        private ClassItem() { }

        public static ClassItem Char(char c, string source) => new ClassItem { IsRange = false, Low = c, High = c, Source = source };

        public static ClassItem Range(char low, char high, string source) => new ClassItem { IsRange = true, Low = low, High = high, Source = source };

        public static ClassItem Shorthand(ClassKind kind, string category, string source) =>
            new ClassItem { ShorthandKind = kind, Category = category, Source = source };

        public bool IsRange { get; private set; }

        public char Low { get; private set; }

        public char High { get; private set; }

        /// <summary>Set for <c>\d</c>, <c>\w</c>, <c>\p{..}</c> and similar items.</summary>
        public ClassKind? ShorthandKind { get; private set; }

        public string Category { get; private set; }

        public string Source { get; private set; }

        public bool IsSingleChar => ShorthandKind == null && !IsRange;
    }

    internal sealed class BracketClass
    {
        public bool Negated { get; set; }

        public List<ClassItem> Items { get; } = new List<ClassItem>();

        public BracketClass Subtraction { get; set; }
    }

    internal sealed class CharClassNode : RegexNode
    {
        public CharClassNode(string pattern, int start, int end, RegexOptions options, ClassKind classKind, string category = null, BracketClass bracket = null)
            : base(pattern, start, end, options)
        {
            ClassKind = classKind;
            Category = category;
            Bracket = bracket;
        }

        public override RegexNodeKind Kind => RegexNodeKind.CharClass;

        public ClassKind ClassKind { get; }

        /// <summary>The Unicode category or block name for <c>\p{..}</c> and <c>\P{..}</c>.</summary>
        public string Category { get; }

        public BracketClass Bracket { get; }
    }

    internal sealed class AnchorNode : RegexNode
    {
        public AnchorNode(string pattern, int start, int end, RegexOptions options, AnchorKind anchorKind)
            : base(pattern, start, end, options) => AnchorKind = anchorKind;

        public override RegexNodeKind Kind => RegexNodeKind.Anchor;

        public AnchorKind AnchorKind { get; }
    }

    internal sealed class GroupNode : RegexNode
    {
        public GroupNode(string pattern, int start, int end, RegexOptions options, GroupKind groupKind, string openText, RegexNode body)
            : base(pattern, start, end, options)
        {
            GroupKind = groupKind;
            OpenText = openText;
            Body = body;
        }

        public override RegexNodeKind Kind => RegexNodeKind.Group;

        public GroupKind GroupKind { get; }

        /// <summary>The opening text, such as <c>(</c>, <c>(?&lt;year&gt;</c> or <c>(?i:</c>.</summary>
        public string OpenText { get; }

        public RegexNode Body { get; internal set; }

        public string Name { get; internal set; }

        /// <summary>The group popped by a balancing group: <c>(?&lt;name-popped&gt;...)</c>.</summary>
        public string BalancingName { get; internal set; }

        /// <summary>The capture number, or 0 when the group does not capture.</summary>
        public int Number { get; internal set; }

        public string OptionsOn { get; internal set; } = string.Empty;

        public string OptionsOff { get; internal set; } = string.Empty;

        public bool IsCapturing => Number > 0;

        public bool IsLookaround =>
            GroupKind == GroupKind.Lookahead || GroupKind == GroupKind.NegativeLookahead ||
            GroupKind == GroupKind.Lookbehind || GroupKind == GroupKind.NegativeLookbehind;

        public override IEnumerable<RegexNode> Children
        {
            get { yield return Body; }
        }
    }

    internal sealed class QuantifierNode : RegexNode
    {
        public const int Unbounded = -1;

        public QuantifierNode(string pattern, int start, int end, RegexOptions options, RegexNode body, int min, int max, bool lazy, string quantifierText)
            : base(pattern, start, end, options)
        {
            Body = body;
            Min = min;
            Max = max;
            Lazy = lazy;
            QuantifierText = quantifierText;
        }

        public override RegexNodeKind Kind => RegexNodeKind.Quantifier;

        public RegexNode Body { get; }

        public int Min { get; }

        /// <summary>The maximum count, or <see cref="Unbounded"/>.</summary>
        public int Max { get; }

        public bool Lazy { get; }

        /// <summary>The quantifier text including a lazy marker, such as <c>+</c> or <c>{2,5}?</c>.</summary>
        public string QuantifierText { get; }

        public bool IsUnbounded => Max == Unbounded;

        public override IEnumerable<RegexNode> Children
        {
            get { yield return Body; }
        }
    }

    internal sealed class BackreferenceNode : RegexNode
    {
        public BackreferenceNode(string pattern, int start, int end, RegexOptions options, string name, int number)
            : base(pattern, start, end, options)
        {
            Name = name;
            Number = number;
        }

        public override RegexNodeKind Kind => RegexNodeKind.Backreference;

        /// <summary>The referenced group name, or null for a numbered reference.</summary>
        public string Name { get; }

        public int Number { get; internal set; }
    }

    internal sealed class ConditionalNode : RegexNode
    {
        public ConditionalNode(string pattern, int start, int end, RegexOptions options, string openText, string groupName, RegexNode condition, RegexNode yes, RegexNode no)
            : base(pattern, start, end, options)
        {
            OpenText = openText;
            GroupName = groupName;
            Condition = condition;
            Yes = yes;
            No = no;
        }

        public override RegexNodeKind Kind => RegexNodeKind.Conditional;

        /// <summary>The opening text up to and including the condition, such as <c>(?(name)</c>.</summary>
        public string OpenText { get; }

        /// <summary>The tested group name or number, or null when the condition is an expression.</summary>
        public string GroupName { get; }

        /// <summary>The condition expression, or null when a group is tested.</summary>
        public RegexNode Condition { get; }

        public RegexNode Yes { get; }

        /// <summary>The no-branch, or null when absent.</summary>
        public RegexNode No { get; }

        public override IEnumerable<RegexNode> Children
        {
            get
            {
                if (Condition != null) yield return Condition;
                yield return Yes;
                if (No != null) yield return No;
            }
        }
    }

    internal sealed class InlineOptionsNode : RegexNode
    {
        public InlineOptionsNode(string pattern, int start, int end, RegexOptions options, string on, string off)
            : base(pattern, start, end, options)
        {
            OptionsOn = on;
            OptionsOff = off;
        }

        public override RegexNodeKind Kind => RegexNodeKind.InlineOptions;

        public string OptionsOn { get; }

        public string OptionsOff { get; }
    }

    internal sealed class CommentNode : RegexNode
    {
        public CommentNode(string pattern, int start, int end, RegexOptions options) : base(pattern, start, end, options) { }

        public override RegexNodeKind Kind => RegexNodeKind.Comment;
    }

    /// <summary>The result of parsing a pattern.</summary>
    internal sealed class RegexTree
    {
        public RegexTree(string pattern, RegexOptions options, RegexNode root, List<GroupNode> groups)
        {
            Pattern = pattern;
            Options = options;
            Root = root;
            Groups = groups;
        }

        public string Pattern { get; }

        public RegexOptions Options { get; }

        public RegexNode Root { get; }

        /// <summary>All groups in source order, capturing or not.</summary>
        public List<GroupNode> Groups { get; }
    }
}
