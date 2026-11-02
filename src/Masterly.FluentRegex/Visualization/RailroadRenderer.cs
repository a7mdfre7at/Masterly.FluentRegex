using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Visualization
{
    /// <summary>
    /// Renders a regex syntax tree as a self-contained railroad diagram in SVG.
    /// </summary>
    /// <remarks>
    /// Every item measures its width and its height above (<c>Up</c>) and below (<c>Down</c>) the
    /// track it sits on, then draws itself from its left track point to its right track point.
    /// </remarks>
    internal static class RailroadRenderer
    {
        private const double ArcRadius = 10;
        private const double VerticalSeparation = 10;
        private const double CharWidth = 7.4;
        private const double BoxHalfHeight = 12;
        private const double Margin = 20;

        public static string Render(RegexTree tree)
        {
            Item root = Build(tree.Root);
            double width = root.Width + 2 * Margin + 40;
            double height = root.Up + root.Down + 2 * Margin;
            double y = Margin + root.Up;

            var svg = new StringBuilder();
            svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(N(width))
               .Append("\" height=\"").Append(N(height)).Append("\" viewBox=\"0 0 ").Append(N(width)).Append(' ').Append(N(height)).AppendLine("\">");
            svg.AppendLine("<title>" + Xml(tree.Pattern) + "</title>");
            svg.AppendLine("<style>");
            svg.AppendLine("  .track { fill: none; stroke: #4b5563; stroke-width: 2; }");
            svg.AppendLine("  .literal rect { fill: #e8f1fd; stroke: #3b6fc4; stroke-width: 1.5; }");
            svg.AppendLine("  .charclass rect { fill: #fdf1e3; stroke: #c4802b; stroke-width: 1.5; }");
            svg.AppendLine("  .anchor rect { fill: #f1f1f1; stroke: #888888; stroke-width: 1.5; }");
            svg.AppendLine("  .group > rect { fill: none; stroke: #7c8796; stroke-width: 1; stroke-dasharray: 4 3; }");
            svg.AppendLine("  text { font: 12px ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; fill: #1f2937; }");
            svg.AppendLine("  .label { font-size: 11px; fill: #4b5563; }");
            svg.AppendLine("  .marker { fill: #4b5563; }");
            svg.AppendLine("</style>");

            double x = Margin;
            svg.Append("<circle class=\"marker\" cx=\"").Append(N(x + 5)).Append("\" cy=\"").Append(N(y)).AppendLine("\" r=\"5\"/>");
            Path(svg, "M" + N(x + 5) + " " + N(y) + " h15");
            root.Draw(svg, x + 20, y);
            double end = x + 20 + root.Width;
            Path(svg, "M" + N(end) + " " + N(y) + " h15");
            svg.Append("<circle class=\"marker\" cx=\"").Append(N(end + 15)).Append("\" cy=\"").Append(N(y)).AppendLine("\" r=\"5\"/>");
            svg.AppendLine("</svg>");
            return svg.ToString();
        }

        // ------------------------------------------------------------------
        // Tree to items
        // ------------------------------------------------------------------

        private static Item Build(RegexNode node)
        {
            switch (node)
            {
                case EmptyNode _:
                case CommentNode _:
                    return new Skip();

                case SequenceNode sequence:
                    return BuildItems(sequence.Items);

                case AlternationNode alternation:
                {
                    var branches = new List<Item>();
                    foreach (RegexNode branch in alternation.Branches) branches.Add(Build(branch));
                    return new Choice(branches);
                }

                case LiteralNode literal:
                    return new Terminal("\"" + NodeDescriber.Printable(literal.Value) + "\"", "literal", 10);

                case CharClassNode _:
                case BackreferenceNode _:
                    return new Terminal(NodeDescriber.Label(node), "charclass", 3);

                case AnchorNode _:
                case InlineOptionsNode _:
                case ConditionalNode _:
                    return new Terminal(node is ConditionalNode ? node.Source : NodeDescriber.Label(node), "anchor", 3);

                case QuantifierNode quantifier:
                    return BuildQuantifier(quantifier);

                case GroupNode group:
                {
                    Item body = Build(group.Body);
                    string label = GroupLabel(group);
                    return label == null ? body : new Box(body, label);
                }

                default:
                    return new Terminal(node.Source, "anchor", 3);
            }
        }

        private static Item BuildItems(IReadOnlyList<RegexNode> items)
        {
            var built = new List<Item>();
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
                    built.Add(new Terminal("\"" + text + "\"", "literal", 10));
                    continue;
                }
                built.Add(Build(items[i]));
                i++;
            }
            return built.Count == 1 ? built[0] : new Sequence(built);
        }

        private static Item BuildQuantifier(QuantifierNode q)
        {
            Item body = Build(q.Body);
            string lazy = q.Lazy ? ", lazy" : string.Empty;

            if (q.Max == 0) return new Skip();
            if (q.Min == 0 && q.Max == 1) return new Optional(body);
            if (q.Min == 1 && q.Max == 1) return body;

            string label;
            if (q.IsUnbounded) label = q.Min <= 1 ? (q.Lazy ? "lazy" : null) : "at least " + q.Min.ToString(CultureInfo.InvariantCulture) + lazy;
            else if (q.Min == q.Max) label = q.Min.ToString(CultureInfo.InvariantCulture) + " times" + lazy;
            else label = q.Min.ToString(CultureInfo.InvariantCulture) + "-" + q.Max.ToString(CultureInfo.InvariantCulture) + " times" + lazy;

            Item loop = new Loop(body, label);
            return q.Min == 0 ? new Optional(loop) : loop;
        }

        private static string GroupLabel(GroupNode group)
        {
            switch (group.GroupKind)
            {
                case GroupKind.NonCapturing: return null;
                case GroupKind.Capturing: return "group #" + group.Number.ToString(CultureInfo.InvariantCulture);
                case GroupKind.Named: return "group '" + group.Name + "'";
                default: return NodeDescriber.Describe(group);
            }
        }

        // ------------------------------------------------------------------
        // Layout items
        // ------------------------------------------------------------------

        private abstract class Item
        {
            public double Width;
            public double Up;
            public double Down;

            public abstract void Draw(StringBuilder svg, double x, double y);
        }

        private sealed class Skip : Item
        {
            public override void Draw(StringBuilder svg, double x, double y) { }
        }

        private sealed class Terminal : Item
        {
            private readonly string _text;
            private readonly string _cssClass;
            private readonly double _radius;

            public Terminal(string text, string cssClass, double radius)
            {
                _text = text.Length > 48 ? text.Substring(0, 45) + "..." : text;
                _cssClass = cssClass;
                _radius = radius;
                Width = _text.Length * CharWidth + 20;
                Up = BoxHalfHeight;
                Down = BoxHalfHeight;
            }

            public override void Draw(StringBuilder svg, double x, double y)
            {
                svg.Append("<g class=\"").Append(_cssClass).Append("\"><rect x=\"").Append(N(x)).Append("\" y=\"").Append(N(y - BoxHalfHeight))
                   .Append("\" width=\"").Append(N(Width)).Append("\" height=\"").Append(N(2 * BoxHalfHeight))
                   .Append("\" rx=\"").Append(N(_radius)).Append("\" ry=\"").Append(N(_radius)).Append("\"/>")
                   .Append("<text x=\"").Append(N(x + Width / 2)).Append("\" y=\"").Append(N(y + 4)).Append("\" text-anchor=\"middle\">")
                   .Append(Xml(_text)).AppendLine("</text></g>");
            }
        }

        private sealed class Sequence : Item
        {
            private const double Gap = 12;
            private readonly List<Item> _items;

            public Sequence(List<Item> items)
            {
                _items = items;
                foreach (Item item in items)
                {
                    Width += item.Width;
                    Up = Math.Max(Up, item.Up);
                    Down = Math.Max(Down, item.Down);
                }
                Width += Gap * Math.Max(0, items.Count - 1);
            }

            public override void Draw(StringBuilder svg, double x, double y)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (i > 0)
                    {
                        Path(svg, "M" + N(x) + " " + N(y) + " h" + N(Gap));
                        x += Gap;
                    }
                    _items[i].Draw(svg, x, y);
                    x += _items[i].Width;
                }
            }
        }

        private sealed class Choice : Item
        {
            private readonly List<Item> _items;
            private readonly double[] _offsets;

            public Choice(List<Item> items)
            {
                _items = items;
                _offsets = new double[items.Count];
                double inner = 0;
                foreach (Item item in items) inner = Math.Max(inner, item.Width);
                Width = inner + 4 * ArcRadius;
                Up = items[0].Up;

                double offset = 0;
                for (int i = 1; i < items.Count; i++)
                {
                    offset += Math.Max(items[i - 1].Down + VerticalSeparation + items[i].Up, 2 * ArcRadius);
                    _offsets[i] = offset;
                }
                Down = offset + items[items.Count - 1].Down;
                if (items.Count == 1) Down = items[0].Down;
            }

            public override void Draw(StringBuilder svg, double x, double y)
            {
                double inner = Width - 4 * ArcRadius;
                double right = x + Width;

                Item first = _items[0];
                Path(svg, "M" + N(x) + " " + N(y) + " h" + N(2 * ArcRadius));
                first.Draw(svg, x + 2 * ArcRadius, y);
                Path(svg, "M" + N(x + 2 * ArcRadius + first.Width) + " " + N(y) + " H" + N(right));

                for (int i = 1; i < _items.Count; i++)
                {
                    Item item = _items[i];
                    double by = y + _offsets[i];
                    Path(svg,
                        "M" + N(x) + " " + N(y) +
                        " a" + N(ArcRadius) + " " + N(ArcRadius) + " 0 0 1 " + N(ArcRadius) + " " + N(ArcRadius) +
                        " V" + N(by - ArcRadius) +
                        " a" + N(ArcRadius) + " " + N(ArcRadius) + " 0 0 0 " + N(ArcRadius) + " " + N(ArcRadius));
                    item.Draw(svg, x + 2 * ArcRadius, by);
                    Path(svg,
                        "M" + N(x + 2 * ArcRadius + item.Width) + " " + N(by) +
                        " H" + N(x + 2 * ArcRadius + inner) +
                        " a" + N(ArcRadius) + " " + N(ArcRadius) + " 0 0 0 " + N(ArcRadius) + " " + N(-ArcRadius) +
                        " V" + N(y + ArcRadius) +
                        " a" + N(ArcRadius) + " " + N(ArcRadius) + " 0 0 1 " + N(ArcRadius) + " " + N(-ArcRadius));
                }
            }
        }

        /// <summary>An item that can be skipped. The bypass is drawn above, by railroad convention.</summary>
        private sealed class Optional : Item
        {
            private readonly Item _item;
            private readonly double _rise;

            public Optional(Item item)
            {
                _item = item;
                _rise = Math.Max(item.Up + VerticalSeparation, 2 * ArcRadius);
                Width = item.Width + 4 * ArcRadius;
                Up = _rise;
                Down = item.Down;
            }

            public override void Draw(StringBuilder svg, double x, double y)
            {
                double right = x + Width;
                double top = y - _rise;

                Path(svg, "M" + N(x) + " " + N(y) + " h" + N(2 * ArcRadius));
                _item.Draw(svg, x + 2 * ArcRadius, y);
                Path(svg, "M" + N(x + 2 * ArcRadius + _item.Width) + " " + N(y) + " H" + N(right));

                Path(svg,
                    "M" + N(x) + " " + N(y) +
                    " a" + N(ArcRadius) + " " + N(ArcRadius) + " 0 0 0 " + N(ArcRadius) + " " + N(-ArcRadius) +
                    " V" + N(top + ArcRadius) +
                    " a" + N(ArcRadius) + " " + N(ArcRadius) + " 0 0 1 " + N(ArcRadius) + " " + N(-ArcRadius) +
                    " H" + N(right - 2 * ArcRadius) +
                    " a" + N(ArcRadius) + " " + N(ArcRadius) + " 0 0 1 " + N(ArcRadius) + " " + N(ArcRadius) +
                    " V" + N(y - ArcRadius) +
                    " a" + N(ArcRadius) + " " + N(ArcRadius) + " 0 0 0 " + N(ArcRadius) + " " + N(ArcRadius));
            }
        }

        private sealed class Loop : Item
        {
            private readonly Item _item;
            private readonly string _label;

            public Loop(Item item, string label)
            {
                _item = item;
                _label = label;
                Width = item.Width + 2 * ArcRadius;
                if (label != null) Width = Math.Max(Width, label.Length * CharWidth * 0.9 + 2 * ArcRadius);
                Up = item.Up;
                Down = item.Down + VerticalSeparation + 2 * ArcRadius + (label != null ? 14 : 0);
            }

            public override void Draw(StringBuilder svg, double x, double y)
            {
                double itemX = x + (Width - _item.Width) / 2;
                double right = x + Width;
                Path(svg, "M" + N(x) + " " + N(y) + " H" + N(itemX));
                _item.Draw(svg, itemX, y);
                Path(svg, "M" + N(itemX + _item.Width) + " " + N(y) + " H" + N(right));

                double returnY = y + _item.Down + VerticalSeparation + ArcRadius;
                Path(svg,
                    "M" + N(right - ArcRadius) + " " + N(y) +
                    " a" + N(ArcRadius) + " " + N(ArcRadius) + " 0 0 1 " + N(ArcRadius) + " " + N(ArcRadius) +
                    " V" + N(returnY - ArcRadius) +
                    " a" + N(ArcRadius) + " " + N(ArcRadius) + " 0 0 1 " + N(-ArcRadius) + " " + N(ArcRadius) +
                    " H" + N(x + ArcRadius) +
                    " a" + N(ArcRadius) + " " + N(ArcRadius) + " 0 0 1 " + N(-ArcRadius) + " " + N(-ArcRadius) +
                    " V" + N(y + ArcRadius) +
                    " a" + N(ArcRadius) + " " + N(ArcRadius) + " 0 0 1 " + N(ArcRadius) + " " + N(-ArcRadius));

                if (_label != null)
                {
                    svg.Append("<text class=\"label\" x=\"").Append(N(x + Width / 2)).Append("\" y=\"").Append(N(returnY + 14))
                       .Append("\" text-anchor=\"middle\">").Append(Xml(_label)).AppendLine("</text>");
                }
            }
        }

        private sealed class Box : Item
        {
            private const double Padding = 10;
            private const double LabelHeight = 16;
            private readonly Item _item;
            private readonly string _label;

            public Box(Item item, string label)
            {
                _item = item;
                _label = label;
                Width = Math.Max(item.Width + 2 * Padding, label.Length * CharWidth * 0.9 + 2 * Padding);
                Up = item.Up + Padding + LabelHeight;
                Down = item.Down + Padding;
            }

            public override void Draw(StringBuilder svg, double x, double y)
            {
                double itemX = x + (Width - _item.Width) / 2;
                svg.Append("<g class=\"group\"><rect x=\"").Append(N(x)).Append("\" y=\"").Append(N(y - Up))
                   .Append("\" width=\"").Append(N(Width)).Append("\" height=\"").Append(N(Up + Down)).Append("\" rx=\"6\" ry=\"6\"/>")
                   .Append("<text class=\"label\" x=\"").Append(N(x + 6)).Append("\" y=\"").Append(N(y - Up + 12)).Append("\">")
                   .Append(Xml(_label)).AppendLine("</text></g>");
                Path(svg, "M" + N(x) + " " + N(y) + " H" + N(itemX));
                _item.Draw(svg, itemX, y);
                Path(svg, "M" + N(itemX + _item.Width) + " " + N(y) + " H" + N(x + Width));
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static void Path(StringBuilder svg, string d) =>
            svg.Append("<path class=\"track\" d=\"").Append(d).AppendLine("\"/>");

        private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Xml(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default:
                        if (char.IsControl(c)) sb.Append("&#x").Append(((int)c).ToString("X", CultureInfo.InvariantCulture)).Append(';');
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
