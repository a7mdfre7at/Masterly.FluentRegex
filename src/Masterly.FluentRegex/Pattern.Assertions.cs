namespace Masterly.FluentRegex
{
    /// <summary>
    /// Partial class containing zero-width assertions (lookahead and lookbehind).
    /// Zero-width assertions match a position in the string without consuming any characters.
    /// They are useful for validating context around a match without including that context in the result.
    /// </summary>
    public partial class Pattern
    {
        /// <summary>
        /// Positive lookahead assertion. Matches if the pattern follows the current position,
        /// without consuming any characters. The match position remains unchanged.
        /// </summary>
        /// <param name="pattern">The pattern that must follow the current position.</param>
        /// <returns>Pattern object for chaining.</returns>
        /// <remarks>
        /// Produces regex: (?=pattern)
        ///
        /// Use cases:
        /// - Password validation (must contain digit, letter, etc.)
        /// - Finding words followed by specific punctuation
        /// - Matching only if certain context follows
        /// </remarks>
        /// <example>
        /// // Match 'q' only if followed by 'u'
        /// Pattern.With.Literal("q").PositiveLookahead(Pattern.With.Literal("u"))
        /// // Produces: q(?=u)
        /// // In "queen", matches "q" at position 0
        /// // In "iraq", no match (q not followed by u)
        ///
        /// // Password must contain at least one digit
        /// Pattern.With
        ///     .StartOfLine
        ///     .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Digit)
        ///     .Anything.Repeat.AtLeast(8)
        ///     .EndOfLine
        /// </example>
        public Pattern PositiveLookahead(Pattern pattern)
        {
            _regexStringBuilder.AppendFormat("(?={0})", pattern);
            return this;
        }

        /// <summary>
        /// Negative lookahead assertion. Matches if the pattern does NOT follow the current position.
        /// The match position remains unchanged.
        /// </summary>
        /// <param name="pattern">The pattern that must NOT follow the current position.</param>
        /// <returns>Pattern object for chaining.</returns>
        /// <remarks>
        /// Produces regex: (?!pattern)
        ///
        /// Use cases:
        /// - Excluding specific patterns from matches
        /// - Matching words not followed by certain characters
        /// - Preventing unwanted matches
        /// </remarks>
        /// <example>
        /// // Match 'q' only if NOT followed by 'u'
        /// Pattern.With.Literal("q").NegativeLookahead(Pattern.With.Literal("u"))
        /// // Produces: q(?!u)
        /// // In "iraq", matches "q" at position 3
        /// // In "queen", no match (q is followed by u)
        ///
        /// // Match 'foo' only if not followed by 'bar'
        /// Pattern.With.Literal("foo").NegativeLookahead(Pattern.With.Literal("bar"))
        /// </example>
        public Pattern NegativeLookahead(Pattern pattern)
        {
            _regexStringBuilder.AppendFormat("(?!{0})", pattern);
            return this;
        }

        /// <summary>
        /// Positive lookbehind assertion. Matches if the pattern precedes the current position,
        /// without consuming any characters. The pattern that precedes is not included in the match.
        /// </summary>
        /// <param name="pattern">The pattern that must precede the current position.</param>
        /// <returns>Pattern object for chaining.</returns>
        /// <remarks>
        /// Produces regex: (?&lt;=pattern)
        ///
        /// Use cases:
        /// - Extracting values after specific prefixes
        /// - Matching content preceded by specific markers
        /// - Currency extraction (match digits after $)
        ///
        /// Note: In .NET, lookbehind patterns can be variable-length.
        /// Some regex engines require fixed-length lookbehind patterns.
        /// </remarks>
        /// <example>
        /// // Match digits preceded by '$' (extract price without the $)
        /// Pattern.With.PositiveLookbehind(Pattern.With.Literal("$")).Digit.Repeat.OneOrMore
        /// // Produces: (?&lt;=\$)\d+
        /// // In "Price: $100", matches "100" (not "$100")
        ///
        /// // Match word after "Name: "
        /// Pattern.With.PositiveLookbehind(Pattern.With.Literal("Name: ")).Word.Repeat.OneOrMore
        /// </example>
        public Pattern PositiveLookbehind(Pattern pattern)
        {
            _regexStringBuilder.AppendFormat("(?<={0})", pattern);
            return this;
        }

        /// <summary>
        /// Negative lookbehind assertion. Matches if the pattern does NOT precede the current position.
        /// The match position remains unchanged.
        /// </summary>
        /// <param name="pattern">The pattern that must NOT precede the current position.</param>
        /// <returns>Pattern object for chaining.</returns>
        /// <remarks>
        /// Produces regex: (?&lt;!pattern)
        ///
        /// Use cases:
        /// - Matching content not preceded by specific markers
        /// - Excluding prefixed content from matches
        /// - Finding standalone occurrences
        /// </remarks>
        /// <example>
        /// // Match digits NOT preceded by '-' (exclude negative numbers)
        /// Pattern.With.NegativeLookbehind(Pattern.With.Literal("-")).Digit.Repeat.OneOrMore
        /// // Produces: (?&lt;!-)\d+
        /// // In "123", matches "123"
        /// // In "-456", matches "56" (skips the "4" which is preceded by "-")
        ///
        /// // Match 'test' not preceded by 'unit'
        /// Pattern.With.NegativeLookbehind(Pattern.With.Literal("unit")).Literal("test")
        /// </example>
        public Pattern NegativeLookbehind(Pattern pattern)
        {
            _regexStringBuilder.AppendFormat("(?<!{0})", pattern);
            return this;
        }
    }
}
