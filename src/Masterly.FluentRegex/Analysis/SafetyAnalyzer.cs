using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Generation;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Analysis
{
    /// <summary>
    /// Static detection of catastrophic backtracking (ReDoS) on a regex syntax tree.
    /// </summary>
    /// <remarks>
    /// The analysis looks for the three structures behind almost every real-world ReDoS:
    /// a repetition that can match the same text through an inner repetition (exponential),
    /// overlapping alternatives inside a repetition (exponential), and neighbouring repetitions
    /// that compete for the same characters (polynomial). Atomic groups and possessive
    /// quantifiers are understood and suppress findings inside them.
    /// </remarks>
    internal sealed class SafetyAnalyzer
    {
        // RegexOptions.NonBacktracking, which the netstandard builds do not define.
        private const RegexOptions NonBacktrackingOption = (RegexOptions)1024;

        private const int LargeBound = 10;
        private const int ExponentialAttackLength = 32;
        private const int PolynomialAttackLength = 30000;

        private readonly RegexTree _tree;
        private readonly Dictionary<RegexNode, bool> _nullable = new Dictionary<RegexNode, bool>();
        private readonly Dictionary<RegexNode, CharSet> _first = new Dictionary<RegexNode, CharSet>();
        private readonly List<SafetyIssue> _issues = new List<SafetyIssue>();
        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);

        private SafetyAnalyzer(RegexTree tree) => _tree = tree;

        public static SafetyReport Analyze(RegexTree tree, RegexOptions options, bool confirm, TimeSpan confirmationBudget)
        {
            // The non-backtracking engine runs in time linear in the input, so no construct can backtrack at all.
            if ((options & NonBacktrackingOption) != 0)
            {
                return new SafetyReport(tree.Pattern, Array.Empty<SafetyIssue>());
            }

            var analyzer = new SafetyAnalyzer(tree);
            analyzer.WalkRoot();
            analyzer._issues.Sort((a, b) => b.Risk != a.Risk ? b.Risk.CompareTo(a.Risk) : a.Position.CompareTo(b.Position));

            if (confirm)
            {
                foreach (SafetyIssue issue in analyzer._issues) Confirm(tree.Pattern, options, issue, confirmationBudget);
            }
            return new SafetyReport(tree.Pattern, analyzer._issues);
        }

        // ------------------------------------------------------------------
        // Traversal
        // ------------------------------------------------------------------

        // The top-level index identifies which root item contains a node, so attack inputs can
        // start with a valid prefix that reaches the vulnerable construct.
        private void WalkRoot()
        {
            if (_tree.Root is SequenceNode root)
            {
                CheckAdjacent(root.Items, -1);
                for (int i = 0; i < root.Items.Count; i++) Walk(root.Items[i], false, i);
            }
            else
            {
                Walk(_tree.Root, false, 0);
            }
        }

        private void Walk(RegexNode node, bool insideAtomic, int topLevelIndex)
        {
            if (node is GroupNode group && group.GroupKind == GroupKind.Atomic) insideAtomic = true;

            if (!insideAtomic)
            {
                if (node is QuantifierNode quantifier) CheckQuantifier(quantifier, topLevelIndex);
                if (node is SequenceNode sequence) CheckAdjacent(sequence.Items, topLevelIndex);
            }

            foreach (RegexNode child in node.Children) Walk(child, insideAtomic, topLevelIndex);
        }

        private void CheckQuantifier(QuantifierNode outer, int topLevelIndex)
        {
            bool outerLarge = outer.IsUnbounded || outer.Max >= LargeBound;
            if (!outerLarge) return;

            // Rule 1: nested repetition that can match the whole iteration on its own.
            foreach (QuantifierNode inner in DominantQuantifiers(outer.Body))
            {
                CharSet innerFirst = First(inner.Body);
                if (innerFirst.IsEmpty) continue;

                RiskLevel risk = outer.IsUnbounded && (inner.IsUnbounded || inner.Max >= LargeBound) ? RiskLevel.High : RiskLevel.Medium;
                string attack = BuildAttack(topLevelIndex, innerFirst, ExponentialAttackLength);
                Report(new SafetyIssue(
                    SafetyIssueKind.NestedQuantifier, risk, outer.Start, outer.Source,
                    "The repetition '" + inner.Source + "' is nested inside the repetition '" + outer.QuantifierText +
                    "'. The same text can be split between iterations in exponentially many ways, so a failing match backtracks through all of them.",
                    "Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. " +
                    "For untrusted input also use WithTimeout(...) or UseNonBacktracking().",
                    attack));
                return;
            }

            if (!outer.IsUnbounded) return;

            // Rule 1b: two competing repetitions inside a repetition, such as (x+x+)+.
            // Each iteration can be split in many ways, and the outer loop multiplies them.
            QuantifierNode[] competing = FindCompetingPair(outer.Body);
            if (competing != null)
            {
                CharSet overlap = First(competing[0].Body).Intersect(First(competing[1].Body));
                Report(new SafetyIssue(
                    SafetyIssueKind.NestedQuantifier, RiskLevel.High, outer.Start, outer.Source,
                    "The repetitions '" + competing[0].Source + "' and '" + competing[1].Source + "' compete for the same characters inside the repetition '" +
                    outer.QuantifierText + "'. Every iteration can be divided in several ways, so a failing match backtracks exponentially.",
                    "Merge the inner repetitions into one, or wrap the body in AtomicGroup(...).",
                    BuildAttack(topLevelIndex, overlap, ExponentialAttackLength)));
                return;
            }

            // Rule 2: overlapping alternatives inside a repetition.
            foreach (AlternationNode alternation in DominantAlternations(outer.Body))
            {
                List<RegexNode> branches = alternation.Branches;
                string overlapping = null;
                bool onlySingleChars = true;
                for (int i = 0; i < branches.Count; i++)
                {
                    for (int j = i + 1; j < branches.Count; j++)
                    {
                        if (!First(branches[i]).Overlaps(First(branches[j]))) continue;
                        overlapping = overlapping ?? "'" + branches[i].Source + "' and '" + branches[j].Source + "'";
                        if (!IsSingleChar(branches[i]) || !IsSingleChar(branches[j])) onlySingleChars = false;
                    }
                }
                if (overlapping == null) continue;

                // An alternation is ambiguous when one branch's text can also be built by repeating
                // the other branches: then each repetition multiplies the ways to match.
                for (int i = 0; i < branches.Count; i++)
                {
                    var others = new List<RegexNode>(branches);
                    others.RemoveAt(i);
                    string pump = Pump(branches[i], others);
                    if (pump == null) continue;

                    Report(new SafetyIssue(
                        SafetyIssueKind.OverlappingAlternation, RiskLevel.High, alternation.Start, alternation.Source,
                        "The text '" + pump + "' can be matched by the alternative '" + branches[i].Source + "' or by a combination of the other alternatives inside the repetition '" +
                        outer.QuantifierText + "', so the number of ways to match grows exponentially.",
                        "Make the alternatives mutually exclusive, wrap the alternation in AtomicGroup(...), or use UseNonBacktracking().",
                        BuildPumpAttack(topLevelIndex, pump)));
                    return;
                }

                if (onlySingleChars)
                {
                    // .NET merges single-character alternatives into one set, so this is redundant, not dangerous.
                    Report(new SafetyIssue(
                        SafetyIssueKind.OverlappingAlternation, RiskLevel.Low, alternation.Start, alternation.Source,
                        "The alternatives " + overlapping + " overlap. .NET merges single-character alternatives into one set, so this is redundant rather than dangerous.",
                        "Combine the alternatives into a single character set.",
                        null));
                    return;
                }
            }
        }

        private QuantifierNode[] FindCompetingPair(RegexNode body)
        {
            foreach (RegexNode node in DominantSequences(body))
            {
                if (!(node is SequenceNode sequence)) continue;
                List<RegexNode> items = sequence.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!(items[i] is QuantifierNode first) || !first.IsUnbounded) continue;
                    for (int j = i + 1; j < items.Count; j++)
                    {
                        if (items[j] is QuantifierNode second && second.IsUnbounded &&
                            First(first.Body).Overlaps(First(second.Body)))
                            return new[] { first, second };
                        if (!Nullable(items[j])) break;
                    }
                }
            }
            return null;
        }

        private IEnumerable<RegexNode> DominantSequences(RegexNode node)
        {
            while (node is GroupNode group && !group.IsLookaround && group.GroupKind != GroupKind.Atomic) node = group.Body;
            if (node is AlternationNode alternation)
            {
                foreach (RegexNode branch in alternation.Branches)
                    foreach (RegexNode inner in DominantSequences(branch)) yield return inner;
            }
            else
            {
                yield return node;
            }
        }

        /// <summary>
        /// Returns a string that one alternative matches whole and the other alternatives match by repetition,
        /// proving the alternation is ambiguous; or null when no such string is found.
        /// </summary>
        private string Pump(RegexNode whole, List<RegexNode> others)
        {
            string wholeSource = RegexWriter.Write(whole);
            var parts = new List<string>();
            foreach (RegexNode other in others) parts.Add(RegexWriter.Write(other));
            string repeatedSource = string.Join("|", parts);
            try
            {
                RegexOptions options = _tree.Options & ~(RegexOptions.Compiled | RegexOptions.RightToLeft);
                RegexTree wholeTree = RegexParser.Parse("^(?:" + wholeSource + ")$", options);
                var repeatedRegex = new Regex("^(?:" + repeatedSource + ")+$", options, TimeSpan.FromMilliseconds(100));
                foreach (string sample in new SampleGenerator(wholeTree, options, 3).GenerateMatching(6))
                {
                    if (sample.Length > 1 && repeatedRegex.IsMatch(sample)) return sample;
                }
            }
            catch (Exception)
            {
                // Branches that reference outer groups cannot be tested in isolation.
            }
            return null;
        }

        private string BuildPumpAttack(int topLevelIndex, string pump)
        {
            var sb = new StringBuilder(Prefix(topLevelIndex));
            // Each ambiguous repetition doubles the work; 30 of them is about a billion paths.
            for (int i = 0; i < 30; i++) sb.Append(pump);
            sb.Append('!');
            return sb.ToString();
        }

        private void CheckAdjacent(List<RegexNode> items, int topLevelIndex)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (!(items[i] is QuantifierNode first) || !first.IsUnbounded || !IsSingleChar(first.Body)) continue;
                CharSet firstSet = First(first.Body);

                for (int j = i + 1; j < items.Count; j++)
                {
                    RegexNode next = items[j];
                    if (next is QuantifierNode second && second.IsUnbounded)
                    {
                        CharSet overlap = firstSet.Intersect(First(second.Body));
                        if (!overlap.IsEmpty)
                        {
                            int start = first.Start;
                            string fragment = first.Pattern.Substring(start, second.End - start);
                            Report(new SafetyIssue(
                                SafetyIssueKind.AdjacentOverlappingQuantifiers, RiskLevel.Medium, start, fragment,
                                "The repetitions '" + first.Source + "' and '" + second.Source + "' can match the same characters, " +
                                "so a failing match tries every way of dividing them (polynomial time).",
                                @"Merge the repetitions into one, for example \d+\d+ into \d{2,}, or make their character sets disjoint.",
                                BuildAttack(topLevelIndex >= 0 ? topLevelIndex : i, overlap, PolynomialAttackLength)));
                            break;
                        }
                    }
                    if (!Nullable(next)) break;
                }
            }
        }

        private void Report(SafetyIssue issue)
        {
            if (_reported.Add(issue.Kind + ":" + issue.Position)) _issues.Add(issue);
        }

        // ------------------------------------------------------------------
        // Structural helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Repetitions inside <paramref name="node"/> that can match the node's entire text by
        /// themselves, because everything around them can match empty.
        /// </summary>
        private List<QuantifierNode> DominantQuantifiers(RegexNode node)
        {
            var result = new List<QuantifierNode>();
            CollectDominant(node, result, null);
            return result;
        }

        private List<AlternationNode> DominantAlternations(RegexNode node)
        {
            var result = new List<AlternationNode>();
            CollectDominant(node, null, result);
            return result;
        }

        private void CollectDominant(RegexNode node, List<QuantifierNode> quantifiers, List<AlternationNode> alternations)
        {
            switch (node)
            {
                case SequenceNode sequence:
                    for (int i = 0; i < sequence.Items.Count; i++)
                    {
                        bool othersNullable = true;
                        for (int j = 0; j < sequence.Items.Count && othersNullable; j++)
                        {
                            if (j != i && !Nullable(sequence.Items[j])) othersNullable = false;
                        }
                        if (othersNullable) CollectDominant(sequence.Items[i], quantifiers, alternations);
                    }
                    return;
                case AlternationNode alternation:
                    alternations?.Add(alternation);
                    foreach (RegexNode branch in alternation.Branches) CollectDominant(branch, quantifiers, alternations);
                    return;
                case GroupNode group:
                    if (group.GroupKind == GroupKind.Atomic || group.IsLookaround) return;
                    CollectDominant(group.Body, quantifiers, alternations);
                    return;
                case QuantifierNode quantifier:
                    // A fixed count such as {4} matches text in exactly one way, so only variable repetitions are ambiguous.
                    if (quantifiers != null && quantifier.Min != quantifier.Max && (quantifier.IsUnbounded || quantifier.Max > 1)) quantifiers.Add(quantifier);
                    if (quantifier.Max != 0) CollectDominant(quantifier.Body, quantifiers, alternations);
                    return;
            }
        }

        private static bool IsSingleChar(RegexNode node)
        {
            while (node is GroupNode group && !group.IsLookaround && group.GroupKind != GroupKind.Atomic) node = group.Body;
            return node is LiteralNode || node is CharClassNode;
        }

        private bool Nullable(RegexNode node)
        {
            if (_nullable.TryGetValue(node, out bool cached)) return cached;
            bool result;
            switch (node)
            {
                case LiteralNode _:
                case CharClassNode _:
                    result = false;
                    break;
                case SequenceNode sequence:
                    result = true;
                    foreach (RegexNode item in sequence.Items)
                    {
                        if (!Nullable(item))
                        {
                            result = false;
                            break;
                        }
                    }
                    break;
                case AlternationNode alternation:
                    result = false;
                    foreach (RegexNode branch in alternation.Branches)
                    {
                        if (Nullable(branch))
                        {
                            result = true;
                            break;
                        }
                    }
                    break;
                case GroupNode group:
                    result = group.IsLookaround || Nullable(group.Body);
                    break;
                case QuantifierNode quantifier:
                    result = quantifier.Min == 0 || Nullable(quantifier.Body);
                    break;
                case ConditionalNode conditional:
                    result = Nullable(conditional.Yes) || conditional.No == null || Nullable(conditional.No);
                    break;
                default:
                    result = true; // anchors, backreferences, options, comments, empty
                    break;
            }
            _nullable[node] = result;
            return result;
        }

        private CharSet First(RegexNode node)
        {
            if (_first.TryGetValue(node, out CharSet cached)) return cached;
            CharSet result;
            switch (node)
            {
                case LiteralNode _:
                case CharClassNode _:
                    result = CharSet.ForNode(node);
                    break;
                case SequenceNode sequence:
                    result = CharSet.Empty;
                    foreach (RegexNode item in sequence.Items)
                    {
                        result = result.Union(First(item));
                        if (!Nullable(item)) break;
                    }
                    break;
                case AlternationNode alternation:
                    result = CharSet.Empty;
                    foreach (RegexNode branch in alternation.Branches) result = result.Union(First(branch));
                    break;
                case GroupNode group:
                    result = group.IsLookaround ? CharSet.Empty : First(group.Body);
                    break;
                case QuantifierNode quantifier:
                    result = quantifier.Max == 0 ? CharSet.Empty : First(quantifier.Body);
                    break;
                case ConditionalNode conditional:
                    result = First(conditional.Yes);
                    if (conditional.No != null) result = result.Union(First(conditional.No));
                    break;
                default:
                    result = CharSet.Empty;
                    break;
            }
            _first[node] = result;
            return result;
        }

        // ------------------------------------------------------------------
        // Attack inputs
        // ------------------------------------------------------------------

        private string BuildAttack(int topLevelIndex, CharSet pumpSet, int length)
        {
            if (pumpSet.IsEmpty) return null;
            char pump = pumpSet.First();
            string prefix = Prefix(topLevelIndex);

            char killer = '!';
            foreach (char candidate in new[] { '!', '#', '\u0000', '\n', 'X', '0', ' ' })
            {
                if (!pumpSet.Contains(candidate))
                {
                    killer = candidate;
                    break;
                }
            }

            var sb = new StringBuilder(prefix.Length + length + 1);
            sb.Append(prefix).Append(pump, length).Append(killer);
            return sb.ToString();
        }

        /// <summary>A string that matches every root item before the vulnerable one.</summary>
        private string Prefix(int topLevelIndex)
        {
            if (topLevelIndex <= 0 || !(_tree.Root is SequenceNode root)) return string.Empty;
            var before = new List<RegexNode>();
            for (int i = 0; i < topLevelIndex && i < root.Items.Count; i++) before.Add(root.Items[i]);
            return SamplePrefix(RegexWriter.WriteSequence(before), _tree.Options) ?? string.Empty;
        }

        private static string SamplePrefix(string regex, RegexOptions options)
        {
            if (regex.Length == 0) return string.Empty;
            try
            {
                RegexTree tree = RegexParser.Parse(regex, options);
                List<string> samples = new SampleGenerator(tree, options, 1).GenerateMatching(1);
                return samples.Count > 0 ? samples[0] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Confirm(string pattern, RegexOptions options, SafetyIssue issue, TimeSpan budget)
        {
            if (issue.AttackInput == null) return;
            const RegexOptions unsupportedForConfirmation = RegexOptions.Compiled;
            var regex = new Regex(pattern, options & ~unsupportedForConfirmation, budget);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                regex.IsMatch(issue.AttackInput);
                issue.Confirmed = false;
            }
            catch (RegexMatchTimeoutException)
            {
                issue.Confirmed = true;
            }
            issue.ConfirmationTime = stopwatch.Elapsed;
        }
    }
}
