using System;
using System.Collections.Generic;
using System.Text;

namespace Masterly.FluentRegex.Syntax
{
    /// <summary>
    /// Writes a syntax tree back to regex text. An optional hook can wrap any node's output,
    /// which is how tracing instruments a pattern with extra capture groups.
    /// </summary>
    internal static class RegexWriter
    {
        public static string Write(RegexNode node, Func<RegexNode, string, string> wrap = null)
        {
            var sb = new StringBuilder();
            WriteTo(sb, node, wrap);
            return sb.ToString();
        }

        public static string WriteSequence(IEnumerable<RegexNode> nodes, Func<RegexNode, string, string> wrap = null)
        {
            var sb = new StringBuilder();
            foreach (RegexNode node in nodes) WriteTo(sb, node, wrap);
            return sb.ToString();
        }

        private static void WriteTo(StringBuilder sb, RegexNode node, Func<RegexNode, string, string> wrap)
        {
            if (wrap == null)
            {
                WriteCore(sb, node, null);
                return;
            }

            var inner = new StringBuilder();
            WriteCore(inner, node, wrap);
            sb.Append(wrap(node, inner.ToString()));
        }

        private static void WriteCore(StringBuilder sb, RegexNode node, Func<RegexNode, string, string> wrap)
        {
            switch (node)
            {
                case EmptyNode _:
                    return;
                case SequenceNode sequence:
                    foreach (RegexNode item in sequence.Items) WriteTo(sb, item, wrap);
                    return;
                case AlternationNode alternation:
                    for (int i = 0; i < alternation.Branches.Count; i++)
                    {
                        if (i > 0) sb.Append('|');
                        WriteTo(sb, alternation.Branches[i], wrap);
                    }
                    return;
                case GroupNode group:
                    sb.Append(group.OpenText);
                    // Lookarounds and balancing groups are written verbatim: instrumenting their
                    // insides would add captures that change nothing a trace can report.
                    if (group.IsLookaround || group.GroupKind == GroupKind.Balancing) WriteCore(sb, group.Body, null);
                    else WriteTo(sb, group.Body, wrap);
                    sb.Append(')');
                    return;
                case QuantifierNode quantifier:
                    WriteTo(sb, quantifier.Body, wrap);
                    sb.Append(quantifier.QuantifierText);
                    return;
                case ConditionalNode conditional:
                    sb.Append(conditional.OpenText);
                    WriteCore(sb, conditional.Yes, null);
                    if (conditional.No != null)
                    {
                        sb.Append('|');
                        WriteCore(sb, conditional.No, null);
                    }
                    sb.Append(')');
                    return;
                default:
                    sb.Append(node.Source);
                    return;
            }
        }

        /// <summary>Returns the items of a sequence, or the node itself as a single item.</summary>
        public static IReadOnlyList<RegexNode> ItemsOf(RegexNode node)
        {
            if (node is SequenceNode sequence) return sequence.Items;
            if (node is EmptyNode) return Array.Empty<RegexNode>();
            return new[] { node };
        }
    }
}
