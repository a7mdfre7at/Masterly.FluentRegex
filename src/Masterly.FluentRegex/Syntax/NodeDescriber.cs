using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Syntax
{
    /// <summary>Produces short, human-readable descriptions of syntax nodes.</summary>
    internal static class NodeDescriber
    {
        public static string Describe(RegexNode node)
        {
            switch (node)
            {
                case EmptyNode _:
                    return "nothing (empty)";
                case LiteralNode literal:
                    return "the character " + Quote(literal.Value);
                case CharClassNode charClass:
                    return DescribeClass(charClass);
                case AnchorNode anchor:
                    return DescribeAnchor(anchor);
                case GroupNode group:
                    return DescribeGroup(group);
                case QuantifierNode quantifier:
                    return Describe(quantifier.Body) + ", " + DescribeCount(quantifier);
                case BackreferenceNode backreference:
                    return backreference.Name != null
                        ? "the same text captured by group '" + backreference.Name + "'"
                        : "the same text captured by group #" + backreference.Number.ToString(CultureInfo.InvariantCulture);
                case ConditionalNode conditional:
                    return conditional.GroupName != null
                        ? "if group '" + conditional.GroupName + "' matched, then one branch, otherwise the other"
                        : "if the condition matches here, then one branch, otherwise the other";
                case AlternationNode alternation:
                    return "one of " + alternation.Branches.Count.ToString(CultureInfo.InvariantCulture) + " alternatives";
                case SequenceNode sequence:
                    return DescribeSequence(sequence);
                case InlineOptionsNode options:
                    return "turn options " + OptionsText(options.OptionsOn, options.OptionsOff) + " on for the rest of the group";
                case CommentNode _:
                    return "a comment";
                default:
                    return node.Source;
            }
        }

        /// <summary>A compact label used in diagrams: the literal text or the class shorthand.</summary>
        public static string Label(RegexNode node)
        {
            switch (node)
            {
                case LiteralNode literal:
                    return "\"" + Printable(literal.Value) + "\"";
                case CharClassNode charClass:
                    return ClassLabel(charClass);
                case AnchorNode anchor:
                    return DescribeAnchor(anchor);
                case BackreferenceNode backreference:
                    return backreference.Name != null ? "same as '" + backreference.Name + "'" : "same as #" + backreference.Number.ToString(CultureInfo.InvariantCulture);
                default:
                    return node.Source;
            }
        }

        public static string DescribeCount(QuantifierNode q)
        {
            string text;
            if (q.Min == 0 && q.Max == 1) text = "optional";
            else if (q.Min == 0 && q.IsUnbounded) text = "zero or more times";
            else if (q.Min == 1 && q.IsUnbounded) text = "one or more times";
            else if (q.IsUnbounded) text = "at least " + q.Min.ToString(CultureInfo.InvariantCulture) + " times";
            else if (q.Min == q.Max) text = "exactly " + q.Min.ToString(CultureInfo.InvariantCulture) + " times";
            else if (q.Min == 0) text = "at most " + q.Max.ToString(CultureInfo.InvariantCulture) + " times";
            else text = "between " + q.Min.ToString(CultureInfo.InvariantCulture) + " and " + q.Max.ToString(CultureInfo.InvariantCulture) + " times";
            return q.Lazy ? text + " (as few as possible)" : text;
        }

        /// <summary>A short count label such as "1+", "0-1" or "2-4".</summary>
        public static string CountLabel(QuantifierNode q)
        {
            string label;
            if (q.IsUnbounded) label = q.Min.ToString(CultureInfo.InvariantCulture) + "+";
            else if (q.Min == q.Max) label = q.Min.ToString(CultureInfo.InvariantCulture) + "×";
            else label = q.Min.ToString(CultureInfo.InvariantCulture) + "-" + q.Max.ToString(CultureInfo.InvariantCulture);
            return q.Lazy ? label + " lazy" : label;
        }

        private static string DescribeSequence(SequenceNode sequence)
        {
            var literals = new StringBuilder();
            foreach (RegexNode item in sequence.Items)
            {
                if (!(item is LiteralNode literal)) return "a sequence of " + sequence.Items.Count.ToString(CultureInfo.InvariantCulture) + " elements";
                literals.Append(literal.Value);
            }
            return "the text \"" + literals + "\"";
        }

        private static string DescribeClass(CharClassNode node)
        {
            switch (node.ClassKind)
            {
                case ClassKind.Any:
                    return (node.Options & RegexOptions.Singleline) != 0 ? "any character" : "any character except newline";
                case ClassKind.Digit: return "a digit";
                case ClassKind.NotDigit: return "a non-digit";
                case ClassKind.Word: return "a word character";
                case ClassKind.NotWord: return "a non-word character";
                case ClassKind.Space: return "a whitespace character";
                case ClassKind.NotSpace: return "a non-whitespace character";
                case ClassKind.Category: return "a character in Unicode " + CategoryName(node.Category);
                case ClassKind.NotCategory: return "a character not in Unicode " + CategoryName(node.Category);
                default:
                    return (node.Bracket.Negated ? "any character except " : "one of ") + BracketItems(node.Bracket);
            }
        }

        private static string ClassLabel(CharClassNode node)
        {
            switch (node.ClassKind)
            {
                case ClassKind.Any: return "any char";
                case ClassKind.Digit: return "digit";
                case ClassKind.NotDigit: return "non-digit";
                case ClassKind.Word: return "word char";
                case ClassKind.NotWord: return "non-word char";
                case ClassKind.Space: return "whitespace";
                case ClassKind.NotSpace: return "non-whitespace";
                case ClassKind.Category: return CategoryName(node.Category);
                case ClassKind.NotCategory: return "not " + CategoryName(node.Category);
                default: return (node.Bracket.Negated ? "none of " : "one of ") + BracketItems(node.Bracket);
            }
        }

        private static string BracketItems(BracketClass bracket)
        {
            var parts = new List<string>();
            foreach (ClassItem item in bracket.Items)
            {
                if (item.ShorthandKind != null) parts.Add(item.Source);
                else if (item.IsRange) parts.Add(Printable(item.Low) + "-" + Printable(item.High));
                else parts.Add(Printable(item.Low));
            }
            string text = "[" + string.Join(" ", parts) + "]";
            if (bracket.Subtraction != null) text += " minus " + BracketItems(bracket.Subtraction);
            return text;
        }

        private static string CategoryName(string code)
        {
            switch (code)
            {
                case "L": return "letter";
                case "Lu": return "uppercase letter";
                case "Ll": return "lowercase letter";
                case "Lt": return "titlecase letter";
                case "Lm": return "modifier letter";
                case "Lo": return "other letter";
                case "M": return "mark";
                case "N": return "number";
                case "Nd": return "decimal digit";
                case "P": return "punctuation";
                case "S": return "symbol";
                case "Sc": return "currency symbol";
                case "Sm": return "math symbol";
                case "Z": return "separator";
                case "Zs": return "space separator";
                case "C": return "control/other";
                case "Cc": return "control character";
                default:
                    return code != null && code.StartsWith("Is", System.StringComparison.Ordinal) ? "block " + code.Substring(2) : "category " + code;
            }
        }

        private static string DescribeAnchor(AnchorNode anchor)
        {
            bool multiline = (anchor.Options & RegexOptions.Multiline) != 0;
            switch (anchor.AnchorKind)
            {
                case AnchorKind.StartOfLine: return multiline ? "start of line" : "start of input";
                case AnchorKind.EndOfLine: return multiline ? "end of line" : "end of input";
                case AnchorKind.StartOfInput: return "start of input";
                case AnchorKind.EndOfInput: return "end of input";
                case AnchorKind.EndOfInputOrNewline: return "end of input (or before a final newline)";
                case AnchorKind.WordBoundary: return "word boundary";
                case AnchorKind.NonWordBoundary: return "not a word boundary";
                default: return "where the previous match ended";
            }
        }

        private static string DescribeGroup(GroupNode group)
        {
            switch (group.GroupKind)
            {
                case GroupKind.Capturing: return "capture group #" + group.Number.ToString(CultureInfo.InvariantCulture);
                case GroupKind.Named: return "group '" + group.Name + "'";
                case GroupKind.NonCapturing: return "group";
                case GroupKind.Atomic: return "atomic group (no backtracking)";
                case GroupKind.Lookahead: return "followed by";
                case GroupKind.NegativeLookahead: return "not followed by";
                case GroupKind.Lookbehind: return "preceded by";
                case GroupKind.NegativeLookbehind: return "not preceded by";
                case GroupKind.Options: return "group with options " + OptionsText(group.OptionsOn, group.OptionsOff);
                default: return "balancing group" + (group.Name != null ? " '" + group.Name + "'" : string.Empty) + " popping '" + group.BalancingName + "'";
            }
        }

        private static string OptionsText(string on, string off)
        {
            string text = on;
            if (!string.IsNullOrEmpty(off)) text += "-" + off;
            return text;
        }

        public static string Quote(char c) => "'" + Printable(c) + "'";

        public static string Printable(char c)
        {
            switch (c)
            {
                case '\t': return @"\t";
                case '\n': return @"\n";
                case '\r': return @"\r";
                case ' ': return "␣";
            }
            if (char.IsControl(c)) return @"\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
            return c.ToString();
        }
    }
}
