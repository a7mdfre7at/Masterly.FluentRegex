using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Generation
{
    /// <summary>
    /// Generates strings that match, or nearly match, a pattern. Candidates are produced by walking
    /// the syntax tree and are always verified with the real .NET engine before being returned.
    /// </summary>
    internal sealed class SampleGenerator
    {
        private const int MaxSampleLength = 2000;
        private const int UnboundedExtra = 4;

        private readonly RegexTree _tree;
        private readonly Regex _verifier;
        private readonly Random _random;
        private readonly Dictionary<string, int> _numberByName = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly char[] _alphabet;

        public SampleGenerator(RegexTree tree, RegexOptions options, int seed)
        {
            _tree = tree;
            _random = new Random(seed);
            _verifier = new Regex(tree.Pattern, options & ~RegexOptions.Compiled, TimeSpan.FromSeconds(1));
            foreach (GroupNode group in tree.Groups)
            {
                if (group.Name != null && group.IsCapturing) _numberByName[group.Name] = group.Number;
            }
            _alphabet = BuildAlphabet(tree);
        }

        // ------------------------------------------------------------------
        // Matching samples
        // ------------------------------------------------------------------

        public List<string> GenerateMatching(int count)
        {
            var results = new List<string>(count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int attempts = 0;
            int maxAttempts = count * 60 + 200;

            while (results.Count < count && attempts < maxAttempts)
            {
                attempts++;
                string candidate = TryGenerate();
                if (candidate == null || seen.Contains(candidate)) continue;
                seen.Add(candidate);
                if (IsMatch(candidate) == true) results.Add(candidate);
            }
            return results;
        }

        private string TryGenerate()
        {
            var state = new State();
            try
            {
                Generate(_tree.Root, state);
                return state.Text.ToString();
            }
            catch (GenerationFailedException)
            {
                return null;
            }
        }

        private void Generate(RegexNode node, State state)
        {
            if (state.Text.Length > MaxSampleLength) throw new GenerationFailedException();

            switch (node)
            {
                case EmptyNode _:
                case AnchorNode _:
                case InlineOptionsNode _:
                case CommentNode _:
                    return;

                case SequenceNode sequence:
                    foreach (RegexNode item in sequence.Items) Generate(item, state);
                    return;

                case AlternationNode alternation:
                    Generate(alternation.Branches[_random.Next(alternation.Branches.Count)], state);
                    return;

                case LiteralNode literal:
                {
                    char c = literal.Value;
                    if ((literal.Options & RegexOptions.IgnoreCase) != 0 && _random.Next(4) == 0)
                        c = char.IsUpper(c) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c);
                    state.Text.Append(c);
                    return;
                }

                case CharClassNode charClass:
                {
                    CharSet set = CharSet.ForNode(charClass);
                    if (set == null || set.IsEmpty) throw new GenerationFailedException();
                    state.Text.Append(set.Pick(_random));
                    return;
                }

                case GroupNode group:
                {
                    // Zero-width assertions consume nothing; verification enforces them.
                    if (group.IsLookaround) return;

                    int start = state.Text.Length;
                    Generate(group.Body, state);
                    if (group.IsCapturing) state.Captures[group.Number] = state.Text.ToString(start, state.Text.Length - start);
                    if (group.GroupKind == GroupKind.Balancing && group.BalancingName != null)
                        state.Captures.Remove(ResolveNumber(group.BalancingName));
                    return;
                }

                case QuantifierNode quantifier:
                {
                    int repetitions = ChooseCount(quantifier);
                    for (int i = 0; i < repetitions; i++) Generate(quantifier.Body, state);
                    return;
                }

                case BackreferenceNode backreference:
                {
                    int number = backreference.Name != null ? ResolveNumber(backreference.Name) : backreference.Number;
                    if (state.Captures.TryGetValue(number, out string captured)) state.Text.Append(captured);
                    return;
                }

                case ConditionalNode conditional:
                {
                    bool takeYes;
                    if (conditional.GroupName != null) takeYes = state.Captures.ContainsKey(ResolveNumber(conditional.GroupName));
                    else takeYes = _random.Next(2) == 0;

                    if (takeYes) Generate(conditional.Yes, state);
                    else if (conditional.No != null) Generate(conditional.No, state);
                    return;
                }

                default:
                    throw new GenerationFailedException();
            }
        }

        private int ResolveNumber(string nameOrNumber)
        {
            if (int.TryParse(nameOrNumber, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number))
                return number;
            return _numberByName.TryGetValue(nameOrNumber, out number) ? number : -1;
        }

        private int ChooseCount(QuantifierNode quantifier)
        {
            int min = quantifier.Min;
            int spread = quantifier.IsUnbounded ? UnboundedExtra : Math.Min(quantifier.Max - min, 6);
            if (spread <= 0) return min;

            // Favor the boundaries: they are where real bugs live.
            int roll = _random.Next(10);
            if (roll == 0) return min;
            if (roll == 1) return min + spread;
            return min + _random.Next(spread + 1);
        }

        // ------------------------------------------------------------------
        // Non-matching samples
        // ------------------------------------------------------------------

        public List<string> GenerateNonMatching(int count)
        {
            var results = new List<string>(count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            List<string> seeds = GenerateMatching(Math.Max(5, Math.Min(count * 2, 40)));
            var seedSet = new HashSet<string>(seeds, StringComparer.Ordinal);

            void Consider(string candidate)
            {
                if (results.Count >= count || candidate == null || seedSet.Contains(candidate) || !seen.Add(candidate)) return;
                if (IsMatch(candidate) == false) results.Add(candidate);
            }

            // 1. Near misses: one small mutation away from a valid sample.
            int attempts = 0;
            int maxAttempts = count * 80 + 200;
            while (results.Count < count && seeds.Count > 0 && attempts < maxAttempts)
            {
                attempts++;
                string seed = seeds[_random.Next(seeds.Count)];
                Consider(Mutate(seed));
            }

            // 2. Simple edge cases.
            Consider(string.Empty);
            Consider(" ");

            // 3. Random strings from the pattern's own alphabet.
            attempts = 0;
            while (results.Count < count && attempts < maxAttempts)
            {
                attempts++;
                int length = _random.Next(0, 12);
                var sb = new StringBuilder(length);
                for (int i = 0; i < length; i++) sb.Append(_alphabet[_random.Next(_alphabet.Length)]);
                Consider(sb.ToString());
            }

            return results;
        }

        private string Mutate(string sample)
        {
            var sb = new StringBuilder(sample);
            int position = sb.Length == 0 ? 0 : _random.Next(sb.Length);
            switch (_random.Next(sb.Length == 0 ? 1 : 8))
            {
                case 0: // insert a character
                    sb.Insert(sb.Length == 0 ? 0 : _random.Next(sb.Length + 1), RandomChar());
                    break;
                case 1: // delete a character
                    sb.Remove(position, 1);
                    break;
                case 2: // replace a character
                    sb[position] = RandomChar();
                    break;
                case 3: // duplicate a character
                    sb.Insert(position, sb[position]);
                    break;
                case 4: // swap two neighbours
                    if (sb.Length > 1)
                    {
                        int other = position == sb.Length - 1 ? position - 1 : position + 1;
                        char tmp = sb[position];
                        sb[position] = sb[other];
                        sb[other] = tmp;
                    }
                    else
                    {
                        sb.Append(RandomChar());
                    }
                    break;
                case 5: // truncate
                    sb.Length = position;
                    break;
                case 6: // append
                    sb.Append(RandomChar());
                    break;
                default: // prepend
                    sb.Insert(0, RandomChar());
                    break;
            }
            return sb.ToString();
        }

        private char RandomChar() => _alphabet[_random.Next(_alphabet.Length)];

        private static char[] BuildAlphabet(RegexTree tree)
        {
            var chars = new HashSet<char>();
            CollectLiterals(tree.Root, chars);
            const string common = "aZ09 -_.@/:;,!#?xQ\t";
            foreach (char c in common) chars.Add(c);
            chars.Add('é');
            var list = new List<char>(chars);
            list.Sort();
            return list.ToArray();
        }

        private static void CollectLiterals(RegexNode node, HashSet<char> chars)
        {
            if (node is LiteralNode literal) chars.Add(literal.Value);
            foreach (RegexNode child in node.Children) CollectLiterals(child, chars);
        }

        private bool? IsMatch(string candidate)
        {
            try
            {
                return _verifier.IsMatch(candidate);
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        }

        private sealed class State
        {
            public StringBuilder Text { get; } = new StringBuilder();

            public Dictionary<int, string> Captures { get; } = new Dictionary<int, string>();
        }

        private sealed class GenerationFailedException : Exception
        {
        }
    }
}
