using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Tracing
{
    /// <summary>
    /// Traces a match by instrumenting the pattern with hidden named groups around each construct.
    /// The real .NET engine then reports exactly which construct consumed which characters.
    /// </summary>
    internal sealed class MatchTracer
    {
        private const string GroupPrefix = "__fr";

        private readonly RegexOptions _options;
        private readonly TimeSpan _timeout;
        private int _nextId;

        private MatchTracer(RegexOptions options, TimeSpan timeout)
        {
            _options = options & ~RegexOptions.Compiled;
            _timeout = timeout;
        }

        public static MatchTrace Trace(RegexTree tree, RegexOptions options, TimeSpan timeout, string input)
        {
            var tracer = new MatchTracer(options, timeout);
            var regex = new Regex(tree.Pattern, tracer._options, timeout);
            Match match = regex.Match(input);

            if (match.Success)
            {
                IReadOnlyList<TraceStep> steps = tracer.TraceSteps(tree.Root, input, match.Index, match.Length);
                return new MatchTrace(tree.Pattern, input, true, match.Index, match.Length, steps, null);
            }

            TraceFailure failure = tracer.Diagnose(tree, input);
            return new MatchTrace(tree.Pattern, input, false, -1, 0, Array.Empty<TraceStep>(), failure);
        }

        // ------------------------------------------------------------------
        // Successful matches
        // ------------------------------------------------------------------

        private IReadOnlyList<TraceStep> TraceSteps(RegexNode root, string input, int startAt, int expectedLength)
        {
            var targets = new List<Target>();
            string instrumented = EmitBody(root, targets);
            try
            {
                var regex = new Regex(instrumented, _options, _timeout);
                Match match = regex.Match(input, startAt);
                if (!match.Success || match.Index != startAt || match.Length != expectedLength) return Array.Empty<TraceStep>();
                return BuildSteps(targets, match, match.Index, match.Index + match.Length);
            }
            catch (ArgumentException)
            {
                // Instrumentation can collide with numbered references to named groups; fall back to no detail.
                return Array.Empty<TraceStep>();
            }
        }

        private static IReadOnlyList<TraceStep> BuildSteps(List<Target> targets, Match match, int start, int end)
        {
            var ordered = new List<KeyValuePair<int, TraceStep>>();
            for (int order = 0; order < targets.Count; order++)
            {
                Target target = targets[order];
                Group group = match.Groups[target.Name];
                if (!group.Success) continue;
                foreach (Capture capture in group.Captures)
                {
                    if (capture.Index < start || capture.Index + capture.Length > end) continue;
                    IReadOnlyList<TraceStep> children = BuildSteps(target.Children, match, capture.Index, capture.Index + capture.Length);
                    ordered.Add(new KeyValuePair<int, TraceStep>(order, new TraceStep(target.Fragment, target.Description, capture.Index, capture.Length, capture.Value, children)));
                }
            }

            // By position; constructs that start at the same place keep their order in the pattern.
            ordered.Sort((a, b) => a.Value.Index != b.Value.Index ? a.Value.Index.CompareTo(b.Value.Index) : a.Key.CompareTo(b.Key));
            var steps = new List<TraceStep>(ordered.Count);
            foreach (KeyValuePair<int, TraceStep> entry in ordered) steps.Add(entry.Value);
            return steps;
        }

        // ------------------------------------------------------------------
        // Instrumentation
        // ------------------------------------------------------------------

        private sealed class Target
        {
            public string Name;
            public string Fragment;
            public string Description;
            public List<Target> Children = new List<Target>();
        }

        private string Wrap(string fragment, string description, List<Target> output, Func<List<Target>, string> inner)
        {
            var target = new Target
            {
                Name = GroupPrefix + (_nextId++).ToString(CultureInfo.InvariantCulture),
                Fragment = fragment,
                Description = description
            };
            output.Add(target);
            return "(?<" + target.Name + ">" + inner(target.Children) + ")";
        }

        /// <summary>Emits a node's contents, adding one target per constituent.</summary>
        private string EmitBody(RegexNode node, List<Target> output)
        {
            switch (node)
            {
                case SequenceNode sequence:
                    return EmitItems(sequence.Items, output);
                case AlternationNode alternation:
                {
                    var parts = new List<string>();
                    foreach (RegexNode branch in alternation.Branches) parts.Add(EmitTraced(branch, output));
                    return string.Join("|", parts);
                }
                case EmptyNode _:
                    return string.Empty;
                default:
                    return EmitTraced(node, output);
            }
        }

        private string EmitItems(IReadOnlyList<RegexNode> items, List<Target> output)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < items.Count)
            {
                if (items[i] is LiteralNode)
                {
                    // Consecutive literal characters are traced as one piece of text.
                    var source = new StringBuilder();
                    var text = new StringBuilder();
                    while (i < items.Count && items[i] is LiteralNode literal)
                    {
                        source.Append(literal.Source);
                        text.Append(literal.Value);
                        i++;
                    }
                    string description = text.Length == 1 ? "the character " + NodeDescriber.Quote(text[0]) : "the text \"" + text + "\"";
                    sb.Append(Wrap(source.ToString(), description, output, _ => source.ToString()));
                    continue;
                }

                sb.Append(EmitTraced(items[i], output));
                i++;
            }
            return sb.ToString();
        }

        /// <summary>Emits a node wrapped in its own trace target.</summary>
        private string EmitTraced(RegexNode node, List<Target> output)
        {
            switch (node)
            {
                case SequenceNode sequence:
                    return Wrap(sequence.Source, NodeDescriber.Describe(sequence), output, children => EmitItems(sequence.Items, children));

                case EmptyNode _:
                    return string.Empty;

                case GroupNode group when !group.IsLookaround && group.GroupKind != GroupKind.Balancing:
                    return Wrap(group.Source, NodeDescriber.Describe(group), output,
                        children => group.OpenText + EmitBody(group.Body, children) + ")");

                case QuantifierNode quantifier when quantifier.Body is GroupNode bodyGroup && !bodyGroup.IsLookaround && bodyGroup.GroupKind != GroupKind.Balancing:
                    // One child step per iteration of the repeated group.
                    return Wrap(quantifier.Source, NodeDescriber.Describe(quantifier), output,
                        children => EmitTraced(bodyGroup, children) + quantifier.QuantifierText);

                default:
                    return Wrap(node.Source, NodeDescriber.Describe(node), output, _ => RegexWriter.Write(node));
            }
        }

        // ------------------------------------------------------------------
        // Failure diagnosis
        // ------------------------------------------------------------------

        private sealed class Progress
        {
            public int Depth;
            public int Position = -1;
            public int MatchStart;
            public RegexNode Failed;
            public string PrefixRegex;
        }

        private TraceFailure Diagnose(RegexTree tree, string input)
        {
            Progress best = null;
            foreach (IReadOnlyList<RegexNode> items in Alternatives(tree.Root))
            {
                Progress progress = Explore(string.Empty, items, ")", input, 0, wrapped: false);
                if (progress != null && (best == null || progress.Position > best.Position || (progress.Position == best.Position && progress.Depth > best.Depth)))
                    best = progress;
            }

            if (best == null || best.Failed == null) return null;

            string matchedSoFar = input.Substring(best.MatchStart, best.Position - best.MatchStart);
            string found = best.Position < input.Length
                ? "\"" + (input.Length - best.Position > 16 ? input.Substring(best.Position, 16) + "..." : input.Substring(best.Position)) + "\""
                : "the end of the input";

            IReadOnlyList<TraceStep> matchedSteps = Array.Empty<TraceStep>();
            if (best.PrefixRegex.Length > 0 && RegexParser.TryParse(best.PrefixRegex, _options, out RegexTree prefixTree))
                matchedSteps = TraceSteps(prefixTree.Root, input, best.MatchStart, best.Position - best.MatchStart);

            return new TraceFailure(best.Position, best.Failed.Source, NodeDescriber.Describe(best.Failed), matchedSoFar, found, matchedSteps);
        }

        private static IEnumerable<IReadOnlyList<RegexNode>> Alternatives(RegexNode node)
        {
            if (node is AlternationNode alternation)
            {
                foreach (RegexNode branch in alternation.Branches) yield return RegexWriter.ItemsOf(branch);
            }
            else
            {
                yield return RegexWriter.ItemsOf(node);
            }
        }

        /// <summary>
        /// Finds the longest prefix of <paramref name="items"/> that still matches, then descends into
        /// the first item that does not, to find the deepest construct responsible for the failure.
        /// </summary>
        private Progress Explore(string before, IReadOnlyList<RegexNode> items, string closer, string input, int depth, bool wrapped)
        {
            int matched = -1;
            Match lastMatch = null;
            for (int k = items.Count; k >= 0; k--)
            {
                string candidate = before + RegexWriter.WriteSequence(Take(items, k)) + (wrapped ? closer : string.Empty);
                Match m = TryMatch(candidate, input);
                if (m != null && m.Success)
                {
                    matched = k;
                    lastMatch = m;
                    break;
                }
            }

            if (lastMatch == null || matched == items.Count) return null;

            RegexNode failed = items[matched];
            string prefix = before + RegexWriter.WriteSequence(Take(items, matched));
            var progress = new Progress
            {
                Depth = depth,
                Position = lastMatch.Index + lastMatch.Length,
                MatchStart = lastMatch.Index,
                Failed = failed,
                PrefixRegex = wrapped ? null : prefix
            };

            // Descend into a plain group to point at the exact part that failed.
            if (failed is GroupNode group && !group.IsLookaround && group.GroupKind != GroupKind.Balancing)
            {
                foreach (IReadOnlyList<RegexNode> inner in Alternatives(group.Body))
                {
                    Progress deeper = Explore(prefix + group.OpenText, inner, ")", input, depth + 1, wrapped: true);
                    if (deeper != null && deeper.Position >= progress.Position)
                    {
                        deeper.PrefixRegex = prefix;
                        deeper.MatchStart = progress.MatchStart;
                        if (deeper.Position > progress.Position || deeper.Depth > progress.Depth) progress = deeper;
                    }
                }
            }

            if (progress.PrefixRegex == null) progress.PrefixRegex = prefix;
            return progress;
        }

        private Match TryMatch(string regex, string input)
        {
            try
            {
                return new Regex(regex, _options, _timeout).Match(input);
            }
            catch (ArgumentException)
            {
                return null; // e.g. a backreference to a group that was cut off
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        }

        private static IEnumerable<RegexNode> Take(IReadOnlyList<RegexNode> items, int count)
        {
            for (int i = 0; i < count && i < items.Count; i++) yield return items[i];
        }
    }
}
