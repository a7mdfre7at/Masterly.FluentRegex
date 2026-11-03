using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Composition: lazy and possessive quantifiers, alternation, concatenation, operators
    /// and inline option scopes.
    /// </summary>
    public partial class Pattern
    {
        /// <summary>
        /// Makes the preceding quantifier lazy (non-greedy), so it matches as few characters as possible.
        /// </summary>
        /// <example>
        /// Pattern.With.Literal("&lt;").Anything.Repeat.OneOrMore.Lazy.Literal("&gt;")
        /// // Produces: &lt;.+?&gt;
        /// </example>
        /// <exception cref="InvalidOperationException">The last element is not quantified.</exception>
        public Pattern Lazy => ReplaceLast(RequireQuantifier(nameof(Lazy)).WithMode(QuantifierMode.Lazy));

        /// <summary>
        /// Makes the preceding quantifier possessive: once it has matched, the engine never gives
        /// characters back. This eliminates catastrophic backtracking for that element.
        /// </summary>
        /// <remarks>
        /// .NET has no possessive quantifier syntax, so this renders the exact equivalent atomic
        /// group. For example <c>\d+</c> becomes <c>(?&gt;\d+)</c>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The last element is not quantified.</exception>
        public Pattern Possessive
        {
            get
            {
                QuantifiedToken quantified = RequireQuantifier(nameof(Possessive));

                // A raw regex fragment such as "abc" may hold several atoms. Only the last atom
                // is quantified, so it alone moves into the atomic group.
                if (quantified.Atom is RawToken raw &&
                    Syntax.RegexParser.TrySplitLastAtom(raw.Text, out string prefix, out string lastAtom) &&
                    prefix.Length > 0)
                {
                    Pattern trimmed = ReplaceLast(new RawToken(prefix));
                    return trimmed.Append(new QuantifiedToken(new RawToken(lastAtom), quantified.Quantifier, QuantifierMode.Possessive));
                }

                return ReplaceLast(quantified.WithMode(QuantifierMode.Possessive));
            }
        }

        private QuantifiedToken RequireQuantifier(string member)
        {
            if (LastToken is QuantifiedToken quantified) return quantified;
            throw new InvalidOperationException(
                member + " must directly follow a quantifier, for example Pattern.With.Digit.Repeat.OneOrMore." + member + ".");
        }

        /// <summary>
        /// Matches any one of the given alternatives inside a non-capturing group: <c>(?:a|b|c)</c>.
        /// Unlike <see cref="Choice"/>, this does not create a numbered group.
        /// </summary>
        /// <param name="alternatives">The alternatives. At least one is required.</param>
        /// <returns>A new pattern.</returns>
        public Pattern OneOf(params Pattern[] alternatives)
        {
            if (alternatives == null || alternatives.Length == 0)
                throw new ArgumentException("At least one alternative is required.", nameof(alternatives));
            var branches = new List<Pattern>(alternatives.Length);
            foreach (Pattern alternative in alternatives)
                branches.Add(Require(alternative, nameof(alternatives)));
            return Append(new AlternationToken("(?:", branches, mergeable: false));
        }

        /// <summary>
        /// Matches any one of the given literal strings: <c>(?:cat|dog)</c>. Special characters are escaped.
        /// </summary>
        /// <param name="words">The literal alternatives. At least one is required.</param>
        /// <returns>A new pattern.</returns>
        public Pattern OneOf(params string[] words)
        {
            if (words == null || words.Length == 0)
                throw new ArgumentException("At least one alternative is required.", nameof(words));
            var patterns = new Pattern[words.Length];
            for (int i = 0; i < words.Length; i++)
                patterns[i] = With.Literal(words[i] ?? throw new ArgumentNullException(nameof(words)));
            return OneOf(patterns);
        }

        /// <summary>
        /// Appends all elements of another pattern. Quantifiers that follow apply to the last
        /// element of <paramref name="pattern"/>, exactly as if it had been written inline.
        /// </summary>
        /// <param name="pattern">The pattern to append.</param>
        /// <returns>A new pattern.</returns>
        public Pattern Then(Pattern pattern) => AppendAll(Require(pattern, nameof(pattern)));

        /// <summary>
        /// Concatenates two patterns. Equivalent to <c>left.Then(right)</c>.
        /// </summary>
        public static Pattern operator +(Pattern left, Pattern right) =>
            Require(left, nameof(left)).Then(Require(right, nameof(right)));

        /// <summary>
        /// Creates a non-capturing alternation. Chains flatten, so <c>a | b | c</c> renders <c>(?:a|b|c)</c>.
        /// Like <c>+</c>, the result takes its options and timeout from the left operand.
        /// </summary>
        public static Pattern operator |(Pattern left, Pattern right)
        {
            Require(left, nameof(left));
            Require(right, nameof(right));

            var branches = new List<Pattern>();
            AddBranches(branches, left);
            AddBranches(branches, right);
            return With.Append(new AlternationToken("(?:", branches, mergeable: true)).WithSettings(left._options, left._timeout);
        }

        private static void AddBranches(List<Pattern> branches, Pattern pattern)
        {
            if (pattern._tail != null && pattern._tail.Count == 1 &&
                pattern._tail.Token is AlternationToken alternation && alternation.Mergeable)
            {
                branches.AddRange(alternation.Branches);
            }
            else
            {
                branches.Add(pattern);
            }
        }

        /// <summary>
        /// Matches the inner pattern case-insensitively, regardless of the outer options: <c>(?i:...)</c>.
        /// </summary>
        public Pattern IgnoreCase(Pattern pattern) => InlineOptions(RegexOptions.IgnoreCase, pattern);

        /// <summary>
        /// Matches the inner pattern case-sensitively, regardless of the outer options: <c>(?-i:...)</c>.
        /// </summary>
        public Pattern CaseSensitive(Pattern pattern) => InlineOptions(RegexOptions.None, pattern, RegexOptions.IgnoreCase);

        /// <summary>
        /// Applies inline options to the inner pattern only: <c>(?imnsx-imnsx:...)</c>.
        /// </summary>
        /// <param name="enable">Options to turn on inside the scope.</param>
        /// <param name="pattern">The inner pattern.</param>
        /// <param name="disable">Options to turn off inside the scope.</param>
        /// <returns>A new pattern.</returns>
        /// <exception cref="ArgumentException">An option cannot be expressed inline.</exception>
        public Pattern InlineOptions(RegexOptions enable, Pattern pattern, RegexOptions disable = RegexOptions.None)
        {
            Require(pattern, nameof(pattern));
            var sb = new StringBuilder("(?");
            sb.Append(InlineFlags(enable, nameof(enable)));
            string off = InlineFlags(disable, nameof(disable));
            if (off.Length > 0) sb.Append('-').Append(off);
            sb.Append(':');

            // Inside (?x:...) the engine ignores unescaped whitespace and '#', so the inner literals escape them.
            RenderContext innerContext = (enable & RegexOptions.IgnorePatternWhitespace) != 0
                ? RenderContext.FreeSpacing
                : RenderContext.Normal;
            return Append(new WrapToken(sb.ToString(), pattern, ")", innerContext));
        }

        private static string InlineFlags(RegexOptions options, string parameterName)
        {
            const RegexOptions supported = RegexOptions.IgnoreCase | RegexOptions.Multiline |
                                           RegexOptions.ExplicitCapture | RegexOptions.Singleline |
                                           RegexOptions.IgnorePatternWhitespace;
            if ((options & ~supported) != 0)
                throw new ArgumentException("Only IgnoreCase, Multiline, ExplicitCapture, Singleline and IgnorePatternWhitespace can be applied inline.", parameterName);

            var sb = new StringBuilder();
            if ((options & RegexOptions.IgnoreCase) != 0) sb.Append('i');
            if ((options & RegexOptions.Multiline) != 0) sb.Append('m');
            if ((options & RegexOptions.ExplicitCapture) != 0) sb.Append('n');
            if ((options & RegexOptions.Singleline) != 0) sb.Append('s');
            if ((options & RegexOptions.IgnorePatternWhitespace) != 0) sb.Append('x');
            return sb.ToString();
        }

        internal static string ValidateGroupName(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A group name cannot be null or empty.", nameof(name));
            foreach (char c in name)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                    throw new ArgumentException("Group name '" + name + "' may only contain letters, digits and underscores.", nameof(name));
            }
            return name;
        }
    }
}
