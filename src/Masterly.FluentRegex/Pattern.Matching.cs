using System;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Partial class containing regex matching and transformation operations.
    /// Provides methods to execute the pattern against input strings, including
    /// matching, replacing, and splitting operations.
    /// </summary>
    /// <remarks>
    /// This class provides a fluent interface for regex operations, caching the compiled
    /// regex for performance when the same pattern is used multiple times.
    /// All matching operations use System.Text.RegularExpressions.Regex internally.
    /// </remarks>
    public partial class Pattern
    {
        private Regex _compiledRegex;
        private RegexOptions _defaultOptions = RegexOptions.None;

        /// <summary>
        /// Compiles the pattern into a System.Text.RegularExpressions.Regex object.
        /// </summary>
        /// <param name="options">
        /// Regex options to apply. Common options include:
        /// - IgnoreCase: Case-insensitive matching
        /// - Multiline: ^ and $ match line boundaries
        /// - Singleline: . matches newline characters
        /// - Compiled: Compile to MSIL for faster execution
        /// </param>
        /// <returns>A compiled Regex object that can be used directly with .NET regex methods.</returns>
        /// <remarks>
        /// Each call creates a new Regex instance. For repeated use of the same pattern,
        /// consider using the IsMatch, Match, and other methods which cache the compiled regex.
        /// </remarks>
        /// <example>
        /// var pattern = Pattern.With.Digit.Repeat.OneOrMore;
        /// Regex regex = pattern.ToRegex(RegexOptions.IgnoreCase | RegexOptions.Compiled);
        /// // Use regex directly with .NET methods
        /// </example>
        public Regex ToRegex(RegexOptions options = RegexOptions.None)
        {
            return new Regex(ToString(), options);
        }

        /// <summary>
        /// Gets a cached compiled Regex object with the default options.
        /// </summary>
        /// <returns>The cached Regex instance.</returns>
        private Regex GetCachedRegex()
        {
            if (_compiledRegex == null || _compiledRegex.Options != _defaultOptions)
            {
                _compiledRegex = new Regex(ToString(), _defaultOptions);
            }
            return _compiledRegex;
        }

        /// <summary>
        /// Sets default RegexOptions for all subsequent matching operations on this pattern.
        /// </summary>
        /// <param name="options">The options to use for subsequent matching operations.</param>
        /// <returns>Pattern object for chaining.</returns>
        /// <remarks>
        /// This method invalidates the cached regex to apply the new options.
        /// Options set here affect IsMatch, Match, Matches, Replace, and Split methods.
        /// </remarks>
        /// <example>
        /// var pattern = Pattern.With.Literal("hello").WithOptions(RegexOptions.IgnoreCase);
        /// pattern.IsMatch("HELLO"); // Returns true
        /// pattern.IsMatch("hello"); // Returns true
        /// pattern.IsMatch("Hello"); // Returns true
        /// </example>
        public Pattern WithOptions(RegexOptions options)
        {
            _defaultOptions = options;
            _compiledRegex = null; // Invalidate cache
            return this;
        }

        /// <summary>
        /// Tests if the pattern matches anywhere in the input string.
        /// </summary>
        /// <param name="input">The string to test.</param>
        /// <returns>True if the pattern matches at least once, false otherwise.</returns>
        /// <remarks>
        /// This method uses the cached regex for performance. The pattern does not need
        /// to match the entire string - any substring match returns true.
        /// Use StartOfLine and EndOfLine anchors for full-string matching.
        /// </remarks>
        /// <example>
        /// var pattern = Pattern.With.Digit.Repeat.OneOrMore;
        /// pattern.IsMatch("abc123");     // Returns true (partial match)
        /// pattern.IsMatch("123");        // Returns true
        /// pattern.IsMatch("abc");        // Returns false (no digits)
        ///
        /// // Full string matching with anchors
        /// var fullMatch = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;
        /// fullMatch.IsMatch("123");      // Returns true
        /// fullMatch.IsMatch("abc123");   // Returns false (doesn't start with digit)
        /// </example>
        public bool IsMatch(string input)
        {
            return GetCachedRegex().IsMatch(input);
        }

        /// <summary>
        /// Tests if the pattern matches the input string with specified options.
        /// </summary>
        /// <param name="input">The string to test.</param>
        /// <param name="options">Regex options to apply for this match only.</param>
        /// <returns>True if the pattern matches, false otherwise.</returns>
        /// <remarks>
        /// This overload creates a new Regex instance with the specified options.
        /// It does not affect the default options set via WithOptions.
        /// </remarks>
        /// <example>
        /// var pattern = Pattern.With.Literal("hello");
        /// pattern.IsMatch("HELLO");                              // Returns false
        /// pattern.IsMatch("HELLO", RegexOptions.IgnoreCase);     // Returns true
        /// </example>
        public bool IsMatch(string input, RegexOptions options)
        {
            return ToRegex(options).IsMatch(input);
        }

        /// <summary>
        /// Finds the first match of the pattern in the input string.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <returns>
        /// A Match object containing the results. Check Match.Success to determine if a match was found.
        /// Access Match.Value for the matched text, Match.Index for position, and Match.Groups for captured groups.
        /// </returns>
        /// <example>
        /// var pattern = Pattern.With.Digit.Repeat.OneOrMore;
        /// Match match = pattern.Match("abc123def456");
        /// // match.Success == true
        /// // match.Value == "123"
        /// // match.Index == 3
        ///
        /// // With named groups
        /// var phonePattern = Pattern.With
        ///     .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
        ///     .Literal("-")
        ///     .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4));
        /// Match m = phonePattern.Match("555-1234");
        /// // m.Groups["area"].Value == "555"
        /// // m.Groups["number"].Value == "1234"
        /// </example>
        public Match Match(string input)
        {
            return GetCachedRegex().Match(input);
        }

        /// <summary>
        /// Finds the first match in the input string starting at the specified character position.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <param name="startAt">The zero-based character position to start searching from.</param>
        /// <returns>A Match object containing the results.</returns>
        /// <example>
        /// var pattern = Pattern.With.Digit.Repeat.OneOrMore;
        /// Match match = pattern.Match("abc123def456", 6);
        /// // match.Value == "456" (skipped "123" by starting at position 6)
        /// </example>
        public Match Match(string input, int startAt)
        {
            return GetCachedRegex().Match(input, startAt);
        }

        /// <summary>
        /// Finds all non-overlapping matches of the pattern in the input string.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <returns>A MatchCollection containing all matches. Iterate or access by index.</returns>
        /// <example>
        /// var pattern = Pattern.With.Digit.Repeat.OneOrMore;
        /// MatchCollection matches = pattern.Matches("abc123def456ghi789");
        /// // matches.Count == 3
        /// // matches[0].Value == "123"
        /// // matches[1].Value == "456"
        /// // matches[2].Value == "789"
        ///
        /// // Iterate through matches
        /// foreach (Match m in matches)
        /// {
        ///     Console.WriteLine($"Found {m.Value} at position {m.Index}");
        /// }
        /// </example>
        public MatchCollection Matches(string input)
        {
            return GetCachedRegex().Matches(input);
        }

        /// <summary>
        /// Replaces all occurrences of the pattern with the replacement string.
        /// </summary>
        /// <param name="input">The input string to search.</param>
        /// <param name="replacement">
        /// The replacement string. Supports substitution patterns:
        /// - $0 or $&amp; - Entire match
        /// - $1, $2, etc. - Numbered group captures
        /// - ${name} - Named group captures
        /// - $` - Text before the match
        /// - $' - Text after the match
        /// - $$ - Literal $ character
        /// </param>
        /// <returns>A new string with all matches replaced.</returns>
        /// <example>
        /// // Simple replacement
        /// Pattern.With.Digit.Replace("a1b2c3", "X");  // "aXbXcX"
        ///
        /// // Using group references
        /// var pattern = Pattern.With.NamedGroup("num", Pattern.With.Digit.Repeat.OneOrMore);
        /// pattern.Replace("Value: 123", "[${num}]");  // "Value: [123]"
        ///
        /// // Swap first and last name
        /// var namePattern = Pattern.With
        ///     .NamedGroup("first", Pattern.With.Word.Repeat.OneOrMore)
        ///     .Whitespace
        ///     .NamedGroup("last", Pattern.With.Word.Repeat.OneOrMore);
        /// namePattern.Replace("John Doe", "${last}, ${first}");  // "Doe, John"
        /// </example>
        public string Replace(string input, string replacement)
        {
            return GetCachedRegex().Replace(input, replacement);
        }

        /// <summary>
        /// Replaces occurrences of the pattern up to a maximum count.
        /// </summary>
        /// <param name="input">The input string to search.</param>
        /// <param name="replacement">The replacement string (supports substitution patterns).</param>
        /// <param name="count">Maximum number of replacements to make. Use -1 for unlimited.</param>
        /// <returns>A new string with up to 'count' matches replaced.</returns>
        /// <example>
        /// var pattern = Pattern.With.Digit;
        /// pattern.Replace("a1b2c3d4", "X", 2);  // "aXbXc3d4" (only first 2 replaced)
        /// </example>
        public string Replace(string input, string replacement, int count)
        {
            return GetCachedRegex().Replace(input, replacement, count);
        }

        /// <summary>
        /// Replaces all occurrences of the pattern using a custom evaluator function.
        /// </summary>
        /// <param name="input">The input string to search.</param>
        /// <param name="evaluator">
        /// A function that takes a Match object and returns the replacement string.
        /// This allows dynamic replacement based on the matched content.
        /// </param>
        /// <returns>A new string with all matches replaced by the evaluator's output.</returns>
        /// <example>
        /// // Double all numbers
        /// var pattern = Pattern.With.Digit.Repeat.OneOrMore;
        /// pattern.Replace("a1b2c3", m => (int.Parse(m.Value) * 2).ToString());
        /// // Returns "a2b4c6"
        ///
        /// // Convert to uppercase
        /// var wordPattern = Pattern.With.Word.Repeat.OneOrMore;
        /// wordPattern.Replace("hello world", m => m.Value.ToUpper());
        /// // Returns "HELLO WORLD"
        ///
        /// // Access groups in evaluator
        /// var datePattern = Pattern.With
        ///     .Group(Pattern.With.Digit.Repeat.Times(4))  // Year
        ///     .Literal("-")
        ///     .Group(Pattern.With.Digit.Repeat.Times(2))  // Month
        ///     .Literal("-")
        ///     .Group(Pattern.With.Digit.Repeat.Times(2)); // Day
        /// datePattern.Replace("2024-01-15", m => $"{m.Groups[2]}/{m.Groups[3]}/{m.Groups[1]}");
        /// // Returns "01/15/2024"
        /// </example>
        public string Replace(string input, MatchEvaluator evaluator)
        {
            return GetCachedRegex().Replace(input, evaluator);
        }

        /// <summary>
        /// Replaces occurrences of the pattern using a match evaluator function up to a maximum count.
        /// </summary>
        /// <param name="input">The input string to search.</param>
        /// <param name="evaluator">A function that produces a replacement string for each match.</param>
        /// <param name="count">Maximum number of replacements to make.</param>
        /// <returns>A new string with up to 'count' matches replaced.</returns>
        public string Replace(string input, MatchEvaluator evaluator, int count)
        {
            return GetCachedRegex().Replace(input, evaluator, count);
        }

        /// <summary>
        /// Splits the input string at each position where the pattern matches.
        /// </summary>
        /// <param name="input">The string to split.</param>
        /// <returns>An array of substrings. The delimiters (matched text) are not included.</returns>
        /// <example>
        /// // Split on comma
        /// Pattern.With.Literal(",").Split("a,b,c");
        /// // Returns ["a", "b", "c"]
        ///
        /// // Split on whitespace
        /// Pattern.With.Whitespace.Repeat.OneOrMore.Split("hello   world\ttest");
        /// // Returns ["hello", "world", "test"]
        ///
        /// // Split on multiple delimiters
        /// Pattern.With.Set(Pattern.With.Literal(",;|")).Split("a,b;c|d");
        /// // Returns ["a", "b", "c", "d"]
        ///
        /// // With capturing groups, captured text is included in results
        /// var pattern = Pattern.With.Group(Pattern.With.Literal(","));
        /// pattern.Split("a,b,c");
        /// // Returns ["a", ",", "b", ",", "c"]
        /// </example>
        public string[] Split(string input)
        {
            return GetCachedRegex().Split(input);
        }

        /// <summary>
        /// Splits the input string at pattern matches, up to a maximum number of substrings.
        /// </summary>
        /// <param name="input">The string to split.</param>
        /// <param name="count">
        /// Maximum number of substrings to return. The last substring contains the remainder.
        /// </param>
        /// <returns>An array of up to 'count' substrings.</returns>
        /// <example>
        /// Pattern.With.Literal(",").Split("a,b,c,d,e", 3);
        /// // Returns ["a", "b", "c,d,e"] (splits twice, remainder in last element)
        /// </example>
        public string[] Split(string input, int count)
        {
            return GetCachedRegex().Split(input, count);
        }
    }
}
