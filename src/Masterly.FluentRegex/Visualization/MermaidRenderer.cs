using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Visualization
{
    /// <summary>Renders a regex syntax tree as a Mermaid flowchart.</summary>
    internal sealed class MermaidRenderer
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly List<string> _literalNodes = new List<string>();
        private readonly List<string> _classNodes = new List<string>();
        private readonly List<string> _anchorNodes = new List<string>();
        private int _next;
        private int _indent = 1;

        public static string Render(RegexTree tree)
        {
            var renderer = new MermaidRenderer();
            renderer._sb.AppendLine("flowchart LR");
            renderer.Line("start([start])");
            string last = renderer.Emit(tree.Root, "start");
            renderer.Line("finish([match])");
            renderer.Line(last + " --> finish");

            renderer._sb.AppendLine("    classDef literal fill:#e8f1fd,stroke:#3b6fc4,color:#10233f");
            renderer._sb.AppendLine("    classDef charclass fill:#fdf1e3,stroke:#c4802b,color:#3f2508");
            renderer._sb.AppendLine("    classDef anchor fill:#eeeeee,stroke:#888888,color:#333333");
            renderer.AppendClass("literal", renderer._literalNodes);
            renderer.AppendClass("charclass", renderer._classNodes);
            renderer.AppendClass("anchor", renderer._anchorNodes);
            return renderer._sb.ToString().TrimEnd();
        }

        private void AppendClass(string name, List<string> nodes)
        {
            if (nodes.Count > 0) _sb.Append("    class ").Append(string.Join(",", nodes)).Append(' ').AppendLine(name);
        }

        private void Line(string text) => _sb.Append(' ', _indent * 4).AppendLine(text);

        private string NewId(string prefix = "n") => prefix + (_next++).ToString(CultureInfo.InvariantCulture);

        /// <summary>Emits <paramref name="node"/> after <paramref name="from"/> and returns its exit node.</summary>
        private string Emit(RegexNode node, string from)
        {
            switch (node)
            {
                case EmptyNode _:
                case CommentNode _:
                    return from;

                case SequenceNode sequence:
                    return EmitItems(sequence.Items, from);

                case AlternationNode alternation:
                {
                    string join = NewId("j");
                    var exits = new List<string>();
                    foreach (RegexNode branch in alternation.Branches) exits.Add(Emit(branch, from));
                    Line(join + "((\" \"))");
                    foreach (string exit in exits) Line(exit + " --> " + join);
                    return join;
                }

                case LiteralNode literal:
                    return Leaf(from, "\"" + NodeDescriber.Printable(literal.Value) + "\"", _literalNodes, null);

                case CharClassNode _:
                case BackreferenceNode _:
                    return Leaf(from, NodeDescriber.Label(node), _classNodes, null);

                case AnchorNode _:
                case InlineOptionsNode _:
                    return Leaf(from, NodeDescriber.Label(node), _anchorNodes, null, stadium: true);

                case QuantifierNode quantifier:
                    return EmitQuantifier(quantifier, from);

                case GroupNode group:
                    return EmitGroup(group, from, null);

                default:
                    return Leaf(from, node.Source, _anchorNodes, null);
            }
        }

        private string EmitItems(IReadOnlyList<RegexNode> items, string from)
        {
            string current = from;
            int i = 0;
            while (i < items.Count)
            {
                if (items[i] is LiteralNode)
                {
                    var text = new StringBuilder();
                    while (i < items.Count && items[i] is LiteralNode literal)
                    {
                        text.Append(NodeDescriber.Printable(literal.Value));
                        i++;
                    }
                    current = Leaf(current, "\"" + text + "\"", _literalNodes, null);
                    continue;
                }
                current = Emit(items[i], current);
                i++;
            }
            return current;
        }

        private string EmitQuantifier(QuantifierNode quantifier, string from)
        {
            string count = Count(quantifier);
            RegexNode body = quantifier.Body;

            switch (body)
            {
                case LiteralNode literal:
                    return Leaf(from, "\"" + NodeDescriber.Printable(literal.Value) + "\"", _literalNodes, count);
                case CharClassNode _:
                case BackreferenceNode _:
                    return Leaf(from, NodeDescriber.Label(body), _classNodes, count);
                case GroupNode group:
                    return EmitGroup(group, from, count);
                default:
                    return Leaf(from, body.Source, _anchorNodes, count);
            }
        }

        private string EmitGroup(GroupNode group, string from, string count)
        {
            string title = GroupTitle(group);
            if (count != null) title = title.Length > 0 ? title + " · " + count : count;

            if (title.Length == 0) return Emit(group.Body, from);

            string id = NewId("g");
            Line("subgraph " + id + "[\"" + Escape(title) + "\"]");
            _indent++;
            Line("direction LR");
            string exit = Emit(group.Body, from);
            _indent--;
            Line("end");
            return exit;
        }

        private static string GroupTitle(GroupNode group)
        {
            switch (group.GroupKind)
            {
                case GroupKind.NonCapturing:
                    return string.Empty;
                case GroupKind.Capturing:
                    return "group #" + group.Number.ToString(CultureInfo.InvariantCulture);
                case GroupKind.Named:
                    return "group '" + group.Name + "'";
                default:
                    return NodeDescriber.Describe(group);
            }
        }

        private string Leaf(string from, string label, List<string> styleList, string count, bool stadium = false)
        {
            string id = NewId();
            string text = Escape(count == null ? label : label + " · " + count);
            Line(stadium ? id + "([\"" + text + "\"])" : id + "[\"" + text + "\"]");
            Line(from + " --> " + id);
            styleList.Add(id);
            return id;
        }

        private static string Count(QuantifierNode q)
        {
            string text;
            if (q.Min == 0 && q.Max == 1) text = "optional";
            else if (q.IsUnbounded) text = q.Min.ToString(CultureInfo.InvariantCulture) + "+ times";
            else if (q.Min == q.Max) text = q.Min.ToString(CultureInfo.InvariantCulture) + " times";
            else text = q.Min.ToString(CultureInfo.InvariantCulture) + "-" + q.Max.ToString(CultureInfo.InvariantCulture) + " times";
            return q.Lazy ? text + ", lazy" : text;
        }

        private static string Escape(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("#quot;"); break;
                    case '#': sb.Append("#35;"); break;
                    case '<': sb.Append("#lt;"); break;
                    case '>': sb.Append("#gt;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
