using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// An immutable, fluent builder for regular expressions.
    /// </summary>
    /// <remarks>
    /// Every member returns a new <see cref="Pattern"/> and leaves the original unchanged, so a
    /// pattern can be stored in a field and reused as a building block in many other patterns.
    /// Internally the pattern is a persistent list of tokens, so extending a pattern is O(1).
    /// </remarks>
    public partial class Pattern
    {
        private readonly TokenNode _tail;
        private readonly RegexOptions _options;
        private readonly TimeSpan _timeout;

        private string _text;

        /// <summary>
        /// Creates an empty pattern. Prefer <see cref="With"/> for readability.
        /// </summary>
        public Pattern() : this(null, RegexOptions.None, Regex.InfiniteMatchTimeout)
        {
        }

        private Pattern(TokenNode tail, RegexOptions options, TimeSpan timeout)
        {
            _tail = tail;
            _options = options;
            _timeout = timeout;
        }

        /// <summary>
        /// Gets a new, empty pattern to start a fluent chain.
        /// </summary>
        public static Pattern With => new Pattern();

        /// <summary>
        /// Gets the default <see cref="RegexOptions"/> applied when this pattern is executed.
        /// </summary>
        public RegexOptions Options => _options;

        /// <summary>
        /// Gets the match timeout applied when this pattern is executed.
        /// <see cref="Regex.InfiniteMatchTimeout"/> means no timeout.
        /// </summary>
        public TimeSpan Timeout => _timeout;

        /// <summary>
        /// Gets a value indicating whether the pattern contains no elements.
        /// </summary>
        public bool IsEmpty => _tail == null;

        internal TokenNode Tail => _tail;

        internal Token LastToken => _tail?.Token;

        internal Token[] Tokens => _tail == null ? Array.Empty<Token>() : _tail.ToArray();

        /// <summary>
        /// Compiles the pattern into a regular expression string.
        /// </summary>
        /// <returns>The regular expression string.</returns>
        public override string ToString()
        {
            string text = _text;
            if (text == null)
            {
                var sb = new StringBuilder();
                RenderTo(sb, RenderContext.Normal);
                text = sb.ToString();
                _text = text;
            }
            return text;
        }

        internal void RenderTo(StringBuilder sb, RenderContext context)
        {
            if (_tail == null) return;

            if (context == RenderContext.Normal && _text != null)
            {
                sb.Append(_text);
                return;
            }

            foreach (Token token in _tail.ToArray())
                token.Render(sb, context);
        }

        internal Pattern Append(Token token) => new Pattern(new TokenNode(token, _tail), _options, _timeout);

        internal Pattern AppendAll(Pattern other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            TokenNode tail = _tail;
            foreach (Token token in other.Tokens)
                tail = new TokenNode(token, tail);
            return new Pattern(tail, _options, _timeout);
        }

        internal Pattern ReplaceLast(Token token)
        {
            if (_tail == null) throw new InvalidOperationException("The pattern is empty.");
            return new Pattern(new TokenNode(token, _tail.Previous), _options, _timeout);
        }

        private Pattern WithSettings(RegexOptions options, TimeSpan timeout) => new Pattern(_tail, options, timeout);

        private static Pattern Require(Pattern pattern, string parameterName) =>
            pattern ?? throw new ArgumentNullException(parameterName);

        // ------------------------------------------------------------------
        // Execution settings
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns a copy of this pattern that uses the given <see cref="RegexOptions"/> for all
        /// matching operations.
        /// </summary>
        /// <param name="options">The options to use for matching.</param>
        /// <returns>A new pattern with the options applied. The original pattern is unchanged.</returns>
        /// <example>
        /// var pattern = Pattern.With.Literal("hello").WithOptions(RegexOptions.IgnoreCase);
        /// pattern.IsMatch("HELLO"); // true
        /// </example>
        public Pattern WithOptions(RegexOptions options) => WithSettings(options, _timeout);

        /// <summary>
        /// Returns a copy of this pattern that aborts matching with a
        /// <see cref="RegexMatchTimeoutException"/> once the timeout elapses.
        /// This is the primary runtime defense against catastrophic backtracking (ReDoS).
        /// </summary>
        /// <param name="timeout">The match timeout, or <see cref="Regex.InfiniteMatchTimeout"/>.</param>
        /// <returns>A new pattern with the timeout applied.</returns>
        public Pattern WithTimeout(TimeSpan timeout)
        {
            if (timeout != Regex.InfiniteMatchTimeout && (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue - 1))
                throw new ArgumentOutOfRangeException(nameof(timeout), "The timeout must be positive and less than about 24 days, or Regex.InfiniteMatchTimeout.");
            return WithSettings(_options, timeout);
        }

#if NET7_0_OR_GREATER
        /// <summary>
        /// Returns a copy of this pattern that runs on the non-backtracking regex engine, which
        /// guarantees matching time linear in the input length and is immune to ReDoS.
        /// </summary>
        /// <remarks>
        /// The non-backtracking engine does not support backreferences, lookarounds, atomic groups,
        /// conditionals or <see cref="RegexOptions.RightToLeft"/>. When such a pattern is executed, the first
        /// four throw <see cref="NotSupportedException"/>, and RightToLeft throws
        /// <see cref="ArgumentOutOfRangeException"/>. <see cref="IsValid"/> reports both as invalid.
        /// </remarks>
        /// <returns>A new pattern that uses <see cref="RegexOptions.NonBacktracking"/>.</returns>
        public Pattern UseNonBacktracking() => WithSettings(_options | RegexOptions.NonBacktracking, _timeout);
#endif

        // ------------------------------------------------------------------
        // Basic elements
        // ------------------------------------------------------------------

        /// <summary>
        /// Adds explicit, unescaped regex text to the pattern.
        /// </summary>
        /// <param name="regex">Regex to add.</param>
        public Pattern RegEx(
#if NET7_0_OR_GREATER
            [System.Diagnostics.CodeAnalysis.StringSyntax(System.Diagnostics.CodeAnalysis.StringSyntaxAttribute.Regex)]
#endif
            string regex) => Append(new RawToken(regex));

        /// <summary>Matches the start of the line or string: <c>^</c>.</summary>
        public Pattern StartOfLine => Append(new RawToken("^"));

        /// <summary>Matches the end of the line or string: <c>$</c>.</summary>
        public Pattern EndOfLine => Append(new RawToken("$"));

        /// <summary>Matches any character except a newline: <c>.</c>.</summary>
        public Pattern Anything => Append(new RawToken("."));

        /// <summary>
        /// Matches the given characters literally. Regex special characters are escaped automatically,
        /// both inside and outside character sets.
        /// </summary>
        /// <param name="characters">Characters to match literally.</param>
        /// <returns>A new pattern.</returns>
        public Pattern Literal(params char[] characters)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            TokenNode tail = _tail;
            foreach (char c in characters)
                tail = new TokenNode(new LiteralToken(c), tail);
            return new Pattern(tail, _options, _timeout);
        }

        /// <summary>
        /// Matches the given text literally. Regex special characters are escaped automatically,
        /// both inside and outside character sets.
        /// </summary>
        /// <param name="literalChars">Text to match literally.</param>
        /// <returns>A new pattern.</returns>
        public Pattern Literal(string literalChars)
        {
            if (literalChars == null) throw new ArgumentNullException(nameof(literalChars));
            return Literal(literalChars.ToCharArray());
        }

        /// <summary>Matches a digit: <c>\d</c>.</summary>
        public Pattern Digit => Append(ClassToken.Shorthand(@"\d"));

        /// <summary>Matches a non-digit: <c>\D</c>.</summary>
        public Pattern NonDigit => Append(ClassToken.Shorthand(@"\D"));

        /// <summary>Matches a word character: <c>\w</c>.</summary>
        public Pattern Word => Append(ClassToken.Shorthand(@"\w"));

        /// <summary>Matches a non-word character: <c>\W</c>.</summary>
        public Pattern NonWord => Append(ClassToken.Shorthand(@"\W"));

        /// <summary>Matches a word boundary: <c>\b</c>.</summary>
        public Pattern WordBoundary => Append(new RawToken(@"\b"));

        /// <summary>
        /// Matches an ASCII letter. Renders <c>[a-zA-Z]</c> on its own and <c>a-zA-Z</c> inside a set.
        /// </summary>
        public Pattern Letter => Append(ClassToken.Bracketed("a-zA-Z"));

        /// <summary>
        /// Matches a lowercase ASCII letter. Renders <c>[a-z]</c> on its own and <c>a-z</c> inside a set.
        /// </summary>
        public Pattern LowercaseLetter => Append(ClassToken.Bracketed("a-z"));

        /// <summary>
        /// Matches an uppercase ASCII letter. Renders <c>[A-Z]</c> on its own and <c>A-Z</c> inside a set.
        /// </summary>
        public Pattern UppercaseLetter => Append(ClassToken.Bracketed("A-Z"));

        /// <summary>Matches a whitespace character: <c>\s</c>.</summary>
        public Pattern Whitespace => Append(ClassToken.Shorthand(@"\s"));

        /// <summary>Matches a non-whitespace character: <c>\S</c>.</summary>
        public Pattern NonWhitespace => Append(ClassToken.Shorthand(@"\S"));

        /// <summary>Matches a tab: <c>\t</c>.</summary>
        public Pattern Tab => Append(ClassToken.Shorthand(@"\t"));

        /// <summary>Matches a carriage return: <c>\r</c>.</summary>
        public Pattern CarriageReturn => Append(ClassToken.Shorthand(@"\r"));

        /// <summary>Matches a newline: <c>\n</c>.</summary>
        public Pattern Newline => Append(ClassToken.Shorthand(@"\n"));

        /// <summary>
        /// Wraps the pattern in a numbered capturing group: <c>(...)</c>.
        /// </summary>
        public Pattern Group(Pattern pattern) => Append(new WrapToken("(", Require(pattern, nameof(pattern)), ")"));

        /// <summary>
        /// Matches one character from the set described by the pattern (character class).
        /// Literal characters inside the set are escaped, so <c>Literal("a-z")</c> matches
        /// 'a', '-' and 'z'. Use <see cref="Range(char, char)"/> for ranges.
        /// </summary>
        /// <param name="pattern">The members of the set.</param>
        /// <returns>A new pattern.</returns>
        public Pattern Set(Pattern pattern) =>
            Append(new WrapToken("[", Require(pattern, nameof(pattern)), "]", RenderContext.CharacterClass));

        /// <summary>
        /// Matches any character that is not in the set described by the pattern.
        /// </summary>
        /// <param name="pattern">The members of the excluded set.</param>
        /// <returns>A new pattern.</returns>
        public Pattern NegatedSet(Pattern pattern) =>
            Append(new WrapToken("[^", Require(pattern, nameof(pattern)), "]", RenderContext.CharacterClass));

        /// <summary>
        /// Matches one of several alternatives inside a numbered capturing group: <c>(a|b|c)</c>.
        /// Use <see cref="OneOf(Pattern[])"/> or the <c>|</c> operator for a non-capturing alternation.
        /// </summary>
        public Pattern Choice(Pattern firstChoice, Pattern secondChoice, params Pattern[] additionalChoices)
        {
            var branches = new System.Collections.Generic.List<Pattern>
            {
                Require(firstChoice, nameof(firstChoice)),
                Require(secondChoice, nameof(secondChoice))
            };
            if (additionalChoices != null)
            {
                foreach (Pattern choice in additionalChoices)
                    branches.Add(Require(choice, nameof(additionalChoices)));
            }
            return Append(new AlternationToken("(", branches, mergeable: false));
        }

        /// <summary>
        /// Starts a quantifier for the last element of the pattern.
        /// </summary>
        public Repeat Repeat => new Repeat(this);
    }
}
