using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.CodeGen
{
    /// <summary>
    /// Converts a regex syntax tree into C# source code that rebuilds the same pattern
    /// with the fluent API.
    /// </summary>
    internal static class FluentCodeEmitter
    {
        private const int MaxSingleLineLength = 90;

        public static string Emit(RegexTree tree, RegexOptions options, TimeSpan timeout, int indentSize = 4)
        {
            Chain chain = EmitChain(tree.Root);

            if (options != RegexOptions.None)
                chain.Call("WithOptions", Arg.Code(OptionsCode(options)));
            if (timeout != Regex.InfiniteMatchTimeout)
                chain.Call("WithTimeout", Arg.Code("TimeSpan.FromMilliseconds(" + timeout.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + ")"));

            return chain.Format(0, indentSize, forceMultiline: chain.Calls.Count > 3);
        }

        // ------------------------------------------------------------------
        // Tree to chain
        // ------------------------------------------------------------------

        private static Chain EmitChain(RegexNode node)
        {
            var chain = new Chain();
            EmitSequence(chain, RegexWriter.ItemsOf(node), node);
            return chain;
        }

        private static void EmitSequence(Chain chain, IReadOnlyList<RegexNode> items, RegexNode owner)
        {
            if (owner is AlternationNode alternation)
            {
                chain.Call("OneOf", BranchArgs(alternation));
                return;
            }

            var literalRun = new StringBuilder();
            foreach (RegexNode item in items)
            {
                if (item is LiteralNode literal)
                {
                    literalRun.Append(literal.Value);
                    continue;
                }

                if (item is QuantifierNode quantifier && quantifier.Body is LiteralNode quantifiedLiteral)
                {
                    // Literal("ab").Repeat.OneOrMore renders ab+, matching regex semantics exactly.
                    literalRun.Append(quantifiedLiteral.Value);
                    FlushLiteral(chain, literalRun);
                    EmitQuantifier(chain, quantifier);
                    continue;
                }

                FlushLiteral(chain, literalRun);
                EmitItem(chain, item);
            }
            FlushLiteral(chain, literalRun);
        }

        private static void FlushLiteral(Chain chain, StringBuilder run)
        {
            if (run.Length == 0) return;
            string text = run.ToString();
            run.Clear();

            if (text.Length == 1)
            {
                switch (text[0])
                {
                    case '\t': chain.Property("Tab"); return;
                    case '\n': chain.Property("Newline"); return;
                    case '\r': chain.Property("CarriageReturn"); return;
                }
            }
            chain.Call("Literal", Arg.Code(StringLiteral(text)));
        }

        private static void EmitItem(Chain chain, RegexNode node)
        {
            switch (node)
            {
                case EmptyNode _:
                    return;
                case LiteralNode literal:
                    chain.Call("Literal", Arg.Code(StringLiteral(literal.Value.ToString())));
                    return;
                case CharClassNode charClass:
                    EmitClass(chain, charClass);
                    return;
                case AnchorNode anchor:
                    EmitAnchor(chain, anchor);
                    return;
                case GroupNode group:
                    EmitGroup(chain, group);
                    return;
                case QuantifierNode quantifier:
                    EmitItem(chain, quantifier.Body);
                    EmitQuantifier(chain, quantifier);
                    return;
                case BackreferenceNode backreference:
                    if (backreference.Name != null) chain.Call("Backreference", Arg.Code(StringLiteral(backreference.Name)));
                    else chain.Call("Backreference", Arg.Code(backreference.Number.ToString(CultureInfo.InvariantCulture)));
                    return;
                case ConditionalNode conditional when conditional.GroupName != null:
                {
                    string condition = int.TryParse(conditional.GroupName, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                        ? number.ToString(CultureInfo.InvariantCulture)
                        : StringLiteral(conditional.GroupName);
                    chain.Call("Conditional",
                        Arg.Code(condition),
                        Arg.Nested(EmitChain(conditional.Yes)),
                        Arg.Nested(conditional.No != null ? EmitChain(conditional.No) : new Chain()));
                    return;
                }
                case AlternationNode alternation:
                    chain.Call("OneOf", BranchArgs(alternation));
                    return;
                case SequenceNode sequence:
                    EmitSequence(chain, sequence.Items, sequence);
                    return;
                default:
                    Raw(chain, node.Source);
                    return;
            }
        }

        private static Arg[] BranchArgs(AlternationNode alternation)
        {
            var args = new Arg[alternation.Branches.Count];
            for (int i = 0; i < args.Length; i++) args[i] = Arg.Nested(EmitChain(alternation.Branches[i]));
            return args;
        }

        private static void Raw(Chain chain, string source) => chain.Call("RegEx", Arg.Code(VerbatimLiteral(source)));

        private static void EmitQuantifier(Chain chain, QuantifierNode q)
        {
            string min = q.Min.ToString(CultureInfo.InvariantCulture);
            string max = q.Max.ToString(CultureInfo.InvariantCulture);

            if (q.Min == 1 && q.IsUnbounded) chain.Property("Repeat.OneOrMore");
            else if (q.Min == 0 && q.IsUnbounded) chain.Property("Repeat.ZeroOrMore");
            else if (q.Min == 0 && q.Max == 1) chain.Property("Repeat.Optional");
            else if (q.IsUnbounded) chain.Call("Repeat.AtLeast", Arg.Code(min));
            else if (q.Min == q.Max) chain.Call("Repeat.Times", Arg.Code(min));
            else if (q.Min == 0) chain.Call("Repeat.AtMost", Arg.Code(max));
            else chain.Call("Repeat.Times", Arg.Code(min), Arg.Code(max));

            if (q.Lazy) chain.Property("Lazy");
        }

        private static void EmitAnchor(Chain chain, AnchorNode anchor)
        {
            switch (anchor.AnchorKind)
            {
                case AnchorKind.StartOfLine: chain.Property("StartOfLine"); return;
                case AnchorKind.EndOfLine: chain.Property("EndOfLine"); return;
                case AnchorKind.StartOfInput: chain.Property("StartOfInput"); return;
                case AnchorKind.EndOfInput: chain.Property("EndOfInput"); return;
                case AnchorKind.WordBoundary: chain.Property("WordBoundary"); return;
                case AnchorKind.NonWordBoundary: chain.Property("NonWordBoundary"); return;
                default: Raw(chain, anchor.Source); return;
            }
        }

        private static void EmitGroup(Chain chain, GroupNode group)
        {
            RegexNode body = group.Body;
            switch (group.GroupKind)
            {
                case GroupKind.Capturing:
                    if (body is AlternationNode capturedAlternation && capturedAlternation.Branches.Count >= 2)
                        chain.Call("Choice", BranchArgs(capturedAlternation));
                    else
                        chain.Call("Group", Arg.Nested(EmitChain(body)));
                    return;
                case GroupKind.Named:
                    chain.Call("NamedGroup", Arg.Code(StringLiteral(group.Name)), Arg.Nested(EmitChain(body)));
                    return;
                case GroupKind.NonCapturing:
                    if (body is AlternationNode alternation)
                        chain.Call("OneOf", BranchArgs(alternation));
                    else
                        chain.Call("NonCapturingGroup", Arg.Nested(EmitChain(body)));
                    return;
                case GroupKind.Atomic:
                    chain.Call("AtomicGroup", Arg.Nested(EmitChain(body)));
                    return;
                case GroupKind.Lookahead:
                    chain.Call("PositiveLookahead", Arg.Nested(EmitChain(body)));
                    return;
                case GroupKind.NegativeLookahead:
                    chain.Call("NegativeLookahead", Arg.Nested(EmitChain(body)));
                    return;
                case GroupKind.Lookbehind:
                    chain.Call("PositiveLookbehind", Arg.Nested(EmitChain(body)));
                    return;
                case GroupKind.NegativeLookbehind:
                    chain.Call("NegativeLookbehind", Arg.Nested(EmitChain(body)));
                    return;
                case GroupKind.Options:
                    EmitOptionsGroup(chain, group);
                    return;
                default:
                    Raw(chain, group.Source);
                    return;
            }
        }

        private static void EmitOptionsGroup(Chain chain, GroupNode group)
        {
            Arg body = Arg.Nested(EmitChain(group.Body));
            if (group.OptionsOn == "i" && group.OptionsOff.Length == 0)
            {
                chain.Call("IgnoreCase", body);
                return;
            }
            if (group.OptionsOn.Length == 0 && group.OptionsOff == "i")
            {
                chain.Call("CaseSensitive", body);
                return;
            }

            RegexOptions on = RegexParser.ApplyFlags(RegexOptions.None, group.OptionsOn, string.Empty);
            RegexOptions off = RegexParser.ApplyFlags(RegexOptions.None, group.OptionsOff, string.Empty);
            if (off == RegexOptions.None) chain.Call("InlineOptions", Arg.Code(OptionsCode(on)), body);
            else chain.Call("InlineOptions", Arg.Code(OptionsCode(on)), body, Arg.Code(OptionsCode(off)));
        }

        // ------------------------------------------------------------------
        // Character classes
        // ------------------------------------------------------------------

        private static void EmitClass(Chain chain, CharClassNode node)
        {
            switch (node.ClassKind)
            {
                case ClassKind.Any: chain.Property("Anything"); return;
                case ClassKind.Digit: chain.Property("Digit"); return;
                case ClassKind.NotDigit: chain.Property("NonDigit"); return;
                case ClassKind.Word: chain.Property("Word"); return;
                case ClassKind.NotWord: chain.Property("NonWord"); return;
                case ClassKind.Space: chain.Property("Whitespace"); return;
                case ClassKind.NotSpace: chain.Property("NonWhitespace"); return;
                case ClassKind.Category:
                case ClassKind.NotCategory:
                    if (!TryEmitCategory(chain, node.Category, node.ClassKind == ClassKind.NotCategory)) Raw(chain, node.Source);
                    return;
                default:
                    EmitBracket(chain, node);
                    return;
            }
        }

        private static bool TryEmitCategory(Chain chain, string code, bool negated)
        {
            string method = negated ? "NotUnicode" : "Unicode";
            string groupName = GroupNameForCode(code);
            if (groupName != null)
            {
                chain.Call(method, Arg.Code("UnicodeGroup." + groupName));
                return true;
            }

            string categoryName = CategoryNameForCode(code);
            if (categoryName != null)
            {
                chain.Call(method, Arg.Code("System.Globalization.UnicodeCategory." + categoryName));
                return true;
            }

            if (!negated && code.StartsWith("Is", StringComparison.Ordinal))
            {
                chain.Call("UnicodeBlock", Arg.Code(StringLiteral(code.Substring(2))));
                return true;
            }
            return false;
        }

        private static void EmitBracket(Chain chain, CharClassNode node)
        {
            BracketClass bracket = node.Bracket;
            if (bracket.Subtraction != null)
            {
                Raw(chain, node.Source);
                return;
            }

            List<ClassItem> items = bracket.Items;
            if (!bracket.Negated)
            {
                if (IsRanges(items, "az", "AZ")) { chain.Property("Letter"); return; }
                if (IsRanges(items, "az")) { chain.Property("LowercaseLetter"); return; }
                if (IsRanges(items, "AZ")) { chain.Property("UppercaseLetter"); return; }
                if (IsRanges(items, "09", "af", "AF")) { chain.Property("HexDigit"); return; }
                if (items.Count == 1 && items[0].IsRange)
                {
                    chain.Call("Range", Arg.Code(CharLiteral(items[0].Low)), Arg.Code(CharLiteral(items[0].High)));
                    return;
                }
            }

            if (AllSingleChars(items))
            {
                var chars = new StringBuilder();
                foreach (ClassItem item in items) chars.Append(item.Low);
                chain.Call(bracket.Negated ? "NoneOf" : "AnyOf", Arg.Code(StringLiteral(chars.ToString())));
                return;
            }

            var inner = new Chain();
            var run = new StringBuilder();
            foreach (ClassItem item in items)
            {
                if (item.IsSingleChar)
                {
                    run.Append(item.Low);
                    continue;
                }

                if (run.Length > 0)
                {
                    inner.Call("Literal", Arg.Code(StringLiteral(run.ToString())));
                    run.Clear();
                }

                if (item.IsRange)
                {
                    if (item.Low == 'a' && item.High == 'z') inner.Property("LowercaseLetter");
                    else if (item.Low == 'A' && item.High == 'Z') inner.Property("UppercaseLetter");
                    else inner.Call("Range", Arg.Code(CharLiteral(item.Low)), Arg.Code(CharLiteral(item.High)));
                    continue;
                }

                switch (item.ShorthandKind)
                {
                    case ClassKind.Digit: inner.Property("Digit"); break;
                    case ClassKind.NotDigit: inner.Property("NonDigit"); break;
                    case ClassKind.Word: inner.Property("Word"); break;
                    case ClassKind.NotWord: inner.Property("NonWord"); break;
                    case ClassKind.Space: inner.Property("Whitespace"); break;
                    case ClassKind.NotSpace: inner.Property("NonWhitespace"); break;
                    default:
                        if (!TryEmitCategory(inner, item.Category, item.ShorthandKind == ClassKind.NotCategory))
                            inner.Call("RegEx", Arg.Code(VerbatimLiteral(item.Source)));
                        break;
                }
            }
            if (run.Length > 0) inner.Call("Literal", Arg.Code(StringLiteral(run.ToString())));

            chain.Call(bracket.Negated ? "NegatedSet" : "Set", Arg.Nested(inner));
        }

        private static bool IsRanges(List<ClassItem> items, params string[] ranges)
        {
            if (items.Count != ranges.Length) return false;
            for (int i = 0; i < ranges.Length; i++)
            {
                if (!items[i].IsRange || items[i].Low != ranges[i][0] || items[i].High != ranges[i][1]) return false;
            }
            return true;
        }

        private static bool AllSingleChars(List<ClassItem> items)
        {
            foreach (ClassItem item in items)
            {
                if (!item.IsSingleChar) return false;
            }
            return items.Count > 0;
        }

        // ------------------------------------------------------------------
        // C# literals
        // ------------------------------------------------------------------

        internal static string StringLiteral(string value)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in value) AppendEscaped(sb, c, '"');
            return sb.Append('"').ToString();
        }

        internal static string CharLiteral(char value)
        {
            var sb = new StringBuilder("'");
            AppendEscaped(sb, value, '\'');
            return sb.Append('\'').ToString();
        }

        internal static string VerbatimLiteral(string value)
        {
            foreach (char c in value)
            {
                if (char.IsControl(c)) return StringLiteral(value);
            }
            return "@\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static void AppendEscaped(StringBuilder sb, char c, char quote)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); return;
                case '\t': sb.Append(@"\t"); return;
                case '\n': sb.Append(@"\n"); return;
                case '\r': sb.Append(@"\r"); return;
                case '\0': sb.Append(@"\0"); return;
            }
            if (c == quote)
            {
                sb.Append('\\').Append(c);
                return;
            }
            if (char.IsControl(c) || char.IsSurrogate(c))
            {
                sb.Append('\\').Append('u').Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                return;
            }
            sb.Append(c);
        }

        // Explicit list instead of Enum.GetValues, which is not AOT-safe.
        private static readonly RegexOptions[] AllOptions =
        {
            RegexOptions.IgnoreCase, RegexOptions.Multiline, RegexOptions.ExplicitCapture, RegexOptions.Compiled,
            RegexOptions.Singleline, RegexOptions.IgnorePatternWhitespace, RegexOptions.RightToLeft,
            RegexOptions.ECMAScript, RegexOptions.CultureInvariant, (RegexOptions)1024 /* NonBacktracking */
        };

        private static string OptionName(RegexOptions flag)
        {
            switch (flag)
            {
                case RegexOptions.IgnoreCase: return "IgnoreCase";
                case RegexOptions.Multiline: return "Multiline";
                case RegexOptions.ExplicitCapture: return "ExplicitCapture";
                case RegexOptions.Compiled: return "Compiled";
                case RegexOptions.Singleline: return "Singleline";
                case RegexOptions.IgnorePatternWhitespace: return "IgnorePatternWhitespace";
                case RegexOptions.RightToLeft: return "RightToLeft";
                case RegexOptions.ECMAScript: return "ECMAScript";
                case RegexOptions.CultureInvariant: return "CultureInvariant";
                default: return "NonBacktracking";
            }
        }

        internal static string OptionsCode(RegexOptions options)
        {
            if (options == RegexOptions.None) return "RegexOptions.None";
            var parts = new List<string>();
            foreach (RegexOptions flag in AllOptions)
            {
                if (flag != RegexOptions.None && (options & flag) == flag) parts.Add("RegexOptions." + OptionName(flag));
            }
            return string.Join(" | ", parts);
        }

        private static string GroupNameForCode(string code)
        {
            switch (code)
            {
                case "L": return "Letter";
                case "M": return "Mark";
                case "N": return "Number";
                case "P": return "Punctuation";
                case "S": return "Symbol";
                case "Z": return "Separator";
                case "C": return "Other";
                default: return null;
            }
        }

        private static string CategoryNameForCode(string code)
        {
            // UnicodeCategory values are the contiguous range 0 (UppercaseLetter) to 29 (OtherNotAssigned).
            for (int value = 0; value <= (int)System.Globalization.UnicodeCategory.OtherNotAssigned; value++)
            {
                var category = (System.Globalization.UnicodeCategory)value;
                if (Pattern.CategoryCode(category) == code) return category.ToString();
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Chain model and formatting
        // ------------------------------------------------------------------

        private sealed class Arg
        {
            private Arg() { }

            public string CodeText { get; private set; }

            public Chain Chain { get; private set; }

            public static Arg Code(string code) => new Arg { CodeText = code };

            public static Arg Nested(Chain chain) => new Arg { Chain = chain };
        }

        private sealed class Member
        {
            public string Name;
            public Arg[] Args; // null for property access
        }

        private sealed class Chain
        {
            public List<Member> Calls { get; } = new List<Member>();

            public void Property(string name) => Calls.Add(new Member { Name = name });

            public void Call(string name, params Arg[] args) => Calls.Add(new Member { Name = name, Args = args });

            public string Format(int indent, int indentSize, bool forceMultiline = false)
            {
                string single = FormatSingleLine();
                if (!forceMultiline && single.Length + indent <= MaxSingleLineLength && single.IndexOf('\n') < 0) return single;

                var sb = new StringBuilder("Pattern.With");
                string pad = new string(' ', indent + indentSize);
                for (int i = 0; i < Calls.Count; i++)
                {
                    Member member = Calls[i];
                    // Quantifiers stay on the line of the element they repeat.
                    bool attach = i > 0 && (member.Name.StartsWith("Repeat.", StringComparison.Ordinal) ||
                                            member.Name == "Lazy" || member.Name == "Possessive");
                    if (!attach) sb.Append('\n').Append(pad);
                    sb.Append('.').Append(member.Name);
                    if (member.Args != null) sb.Append(FormatArgs(member.Args, indent + indentSize, indentSize));
                }
                return sb.ToString();
            }

            private string FormatSingleLine()
            {
                var sb = new StringBuilder("Pattern.With");
                foreach (Member member in Calls)
                {
                    sb.Append('.').Append(member.Name);
                    if (member.Args == null) continue;
                    sb.Append('(');
                    for (int i = 0; i < member.Args.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        Arg arg = member.Args[i];
                        sb.Append(arg.Chain != null ? arg.Chain.FormatSingleLine() : arg.CodeText);
                    }
                    sb.Append(')');
                }
                return sb.ToString();
            }

            private static string FormatArgs(Arg[] args, int indent, int indentSize)
            {
                var parts = new string[args.Length];
                bool multiline = false;
                int total = 0;
                for (int i = 0; i < args.Length; i++)
                {
                    parts[i] = args[i].Chain != null ? args[i].Chain.Format(indent + indentSize, indentSize) : args[i].CodeText;
                    total += parts[i].Length + 2;
                    if (parts[i].IndexOf('\n') >= 0) multiline = true;
                }

                bool hasNested = false;
                foreach (Arg arg in args) hasNested |= arg.Chain != null;

                if (!multiline && (total + indent <= MaxSingleLineLength || !hasNested))
                    return "(" + string.Join(", ", parts) + ")";

                var sb = new StringBuilder("(");
                string pad = new string(' ', indent + indentSize);
                for (int i = 0; i < parts.Length; i++)
                {
                    sb.Append('\n').Append(pad).Append(parts[i]);
                    if (i < parts.Length - 1) sb.Append(',');
                }
                return sb.Append(')').ToString();
            }
        }
    }
}
