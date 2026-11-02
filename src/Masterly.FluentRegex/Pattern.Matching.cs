using System;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Matching, replacing and splitting. The compiled <see cref="Regex"/> is created on first use
    /// and cached. Because patterns are immutable, the cache can never go stale.
    /// </summary>
    public partial class Pattern
    {
        private Regex _compiledRegex;

        /// <summary>
        /// Creates a <see cref="Regex"/> that uses this pattern's options and timeout.
        /// </summary>
        /// <returns>A new <see cref="Regex"/> instance.</returns>
        public Regex ToRegex() => new Regex(ToString(), _options, _timeout);

        /// <summary>
        /// Creates a <see cref="Regex"/> with explicit options. The pattern's timeout still applies.
        /// </summary>
        /// <param name="options">The options to use instead of the pattern's own options.</param>
        /// <returns>A new <see cref="Regex"/> instance.</returns>
        public Regex ToRegex(RegexOptions options) => new Regex(ToString(), options, _timeout);

        /// <summary>
        /// Gets the cached compiled regex that uses this pattern's options and timeout.
        /// </summary>
        internal Regex CachedRegex
        {
            get
            {
                Regex regex = _compiledRegex;
                if (regex == null)
                {
                    regex = ToRegex();
                    _compiledRegex = regex;
                }
                return regex;
            }
        }

        /// <summary>Tests whether the pattern matches anywhere in <paramref name="input"/>.</summary>
        public bool IsMatch(string input) => CachedRegex.IsMatch(input);

        /// <summary>Tests whether the pattern matches using explicit options.</summary>
        public bool IsMatch(string input, RegexOptions options) => ToRegex(options).IsMatch(input);

        /// <summary>Finds the first match in <paramref name="input"/>.</summary>
        public Match Match(string input) => CachedRegex.Match(input);

        /// <summary>Finds the first match at or after <paramref name="startAt"/>.</summary>
        public Match Match(string input, int startAt) => CachedRegex.Match(input, startAt);

        /// <summary>Finds all non-overlapping matches.</summary>
        public MatchCollection Matches(string input) => CachedRegex.Matches(input);

        /// <summary>Replaces all matches with a replacement string (supports <c>$1</c> and <c>${name}</c>).</summary>
        public string Replace(string input, string replacement) => CachedRegex.Replace(input, replacement);

        /// <summary>Replaces up to <paramref name="count"/> matches with a replacement string.</summary>
        public string Replace(string input, string replacement, int count) => CachedRegex.Replace(input, replacement, count);

        /// <summary>Replaces all matches with the result of <paramref name="evaluator"/>.</summary>
        public string Replace(string input, MatchEvaluator evaluator) => CachedRegex.Replace(input, evaluator);

        /// <summary>Replaces up to <paramref name="count"/> matches with the result of <paramref name="evaluator"/>.</summary>
        public string Replace(string input, MatchEvaluator evaluator, int count) => CachedRegex.Replace(input, evaluator, count);

        /// <summary>Splits the input at every match.</summary>
        public string[] Split(string input) => CachedRegex.Split(input);

        /// <summary>Splits the input into at most <paramref name="count"/> parts.</summary>
        public string[] Split(string input, int count) => CachedRegex.Split(input, count);

        /// <summary>Counts the matches in <paramref name="input"/>.</summary>
        public int Count(string input)
        {
#if NET7_0_OR_GREATER
            return CachedRegex.Count(input);
#else
            if (input == null) throw new ArgumentNullException(nameof(input));
            int count = 0;
            for (Match m = CachedRegex.Match(input); m.Success; m = m.NextMatch()) count++;
            return count;
#endif
        }

#if NET7_0_OR_GREATER
        /// <summary>
        /// Tests whether the pattern matches anywhere in a span, without allocating.
        /// </summary>
        public bool IsMatch(ReadOnlySpan<char> input) => CachedRegex.IsMatch(input);

        /// <summary>
        /// Counts the matches in a span, without allocating.
        /// </summary>
        public int Count(ReadOnlySpan<char> input) => CachedRegex.Count(input);

        /// <summary>
        /// Enumerates match positions in a span without allocating <see cref="System.Text.RegularExpressions.Match"/> objects.
        /// </summary>
        /// <example>
        /// foreach (ValueMatch m in Pattern.With.Digit.Repeat.OneOrMore.EnumerateMatches(text))
        ///     Console.WriteLine(text.AsSpan(m.Index, m.Length).ToString());
        /// </example>
        public Regex.ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<char> input) => CachedRegex.EnumerateMatches(input);
#endif

#if NET9_0_OR_GREATER
        /// <summary>
        /// Enumerates the ranges between matches in a span, without allocating strings.
        /// </summary>
        public Regex.ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<char> input) => CachedRegex.EnumerateSplits(input);
#endif
    }
}
