using System.Text;

namespace Masterly.FluentRegex
{
    public class Pattern
	{
		private const string SPECIAL_CHARS = @"^$.|{}[]()*+?\";

		private readonly StringBuilder _regexStringBuilder = new StringBuilder();

		private Pattern AppendToRegexBuilder(string str)
		{
			_regexStringBuilder.Append(str);
			return this;
		}

		private Pattern AppendFormatToRegexBuilder(string format, Pattern pattern)
		{
			_regexStringBuilder.AppendFormat(format, pattern);
			return this;
		}

		/// <summary>
		/// Static method for getting new Pattern instance.
		/// </summary>
		public static Pattern With => new Pattern();

        /// <summary>
        /// Compiles the pattern into a regular expression string.
        /// </summary>
        /// <returns>Returns the regular expression string.</returns>
        public override string ToString() => _regexStringBuilder.ToString();

        /// <summary>
        /// Adds an explicit regex to the pattern.
        /// </summary>
        /// <param name="regex">Regex to add.</param>
        public Pattern RegEx(string regex) => AppendToRegexBuilder(regex);

        public Pattern StartOfLine => AppendToRegexBuilder("^");

        public Pattern EndOfLine => AppendToRegexBuilder("$");

		public Pattern Anything => AppendToRegexBuilder(".");

		/// <summary>
		/// Use any of regex characters as literal characters 
		/// </summary>
		/// <param name="charecters"></param>
		/// <returns>Pattern object</returns>
		public Pattern Literal(params char[] charecters)
		{
			// handle special chars
			foreach (char c in charecters)
			{
				if (SPECIAL_CHARS.IndexOf(c) >= 0)
					_regexStringBuilder.AppendFormat(@"\{0}", c);
				else
					_regexStringBuilder.Append(c);
			}

			return this;
		}

        /// <summary>
        /// Use any of regex characters as literal characters 
        /// </summary>
        /// <param name="charecters"></param>
        /// <returns>Pattern object</returns>
        public Pattern Literal(string literalChars) => Literal(literalChars.ToCharArray());

        public Pattern Digit => AppendToRegexBuilder("\\d");

		public Pattern NonDigit => AppendToRegexBuilder("\\D");

		public Pattern Word => AppendToRegexBuilder("\\w");

		public Pattern NonWord => AppendToRegexBuilder("\\W");

		public Pattern WordBoundary => AppendToRegexBuilder("\\b");

		public Pattern Letter => AppendToRegexBuilder("a-zA-Z");

		public Pattern LowercaseLetter => AppendToRegexBuilder("a-z");

		public Pattern UppercaseLetter => AppendToRegexBuilder("A-Z");

		public Pattern Whitespace => AppendToRegexBuilder("\\s");

		public Pattern NonWhitespace => AppendToRegexBuilder("\\S");

		public Pattern Tab => AppendToRegexBuilder("\\t");

		public Pattern CarriageReturn => AppendToRegexBuilder("\\r");

		public Pattern Newline => AppendToRegexBuilder("\\n");

		public Pattern Group(Pattern pattern) => AppendFormatToRegexBuilder("({0})", pattern);

		/// <summary>
		/// Tell the regex engine to match only one out of several characters (character class)
		/// </summary>
		/// <param name="pattern"></param>
		/// <returns>Pattern object</returns>
		public Pattern Set(Pattern pattern) => AppendFormatToRegexBuilder("[{0}]", pattern);

		/// <summary>
		/// Tell the regex engine to match ny character that is not in the character class
		/// </summary>
		/// <param name="pattern"></param>
		/// <returns>Pattern object</returns>
		public Pattern NegatedSet(Pattern pattern) => AppendFormatToRegexBuilder("[^{0}]", pattern);

		/// <summary>
		/// Match one of a choice of regular expressions
		/// </summary>
		/// <param name="firstChoice"></param>
		/// <param name="secondChoice"></param>
		/// <param name="additionalChoices"></param>
		/// <returns></returns>
		public Pattern Choice(Pattern firstChoice, Pattern secondChoice, params Pattern[] additionalChoices)
		{
			_regexStringBuilder.AppendFormat("({0}|{1}", firstChoice, secondChoice);

			foreach (Pattern choice in additionalChoices)
				_regexStringBuilder.AppendFormat("|{0}", choice);

			_regexStringBuilder.Append(")");
			return this;
		}

        public Repeat Repeat => new Repeat(this);
    }
}
