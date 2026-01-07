using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Partial class containing validation and debugging functionality.
    /// Provides methods to validate regex syntax, inspect group information,
    /// and generate human-readable explanations of patterns.
    /// </summary>
    public partial class Pattern
    {
        /// <summary>
        /// Validates that the pattern is a valid regular expression.
        /// Attempts to compile the pattern and returns whether it succeeded.
        /// </summary>
        /// <returns>True if the pattern is valid regex syntax, false otherwise.</returns>
        /// <remarks>
        /// This method catches ArgumentException thrown by the Regex constructor
        /// when the pattern contains invalid syntax. Use TryValidate to get
        /// detailed error information when validation fails.
        /// </remarks>
        /// <example>
        /// Pattern.With.Digit.Repeat.OneOrMore.IsValid();  // Returns true
        /// Pattern.With.RegEx("[invalid").IsValid();       // Returns false
        /// </example>
        public bool IsValid()
        {
            try
            {
                _ = new Regex(ToString());
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Validates the pattern and returns detailed error information if invalid.
        /// </summary>
        /// <param name="errorMessage">
        /// When this method returns false, contains the error message describing why
        /// the pattern is invalid. When this method returns true, this is null.
        /// </param>
        /// <returns>True if the pattern is valid regex syntax, false otherwise.</returns>
        /// <remarks>
        /// Use this method when you need to display validation errors to users
        /// or log detailed information about why a pattern failed to compile.
        /// </remarks>
        /// <example>
        /// var pattern = Pattern.With.RegEx("(unclosed");
        /// if (!pattern.TryValidate(out string error))
        /// {
        ///     Console.WriteLine($"Invalid pattern: {error}");
        /// }
        /// </example>
        public bool TryValidate(out string errorMessage)
        {
            try
            {
                _ = new Regex(ToString());
                errorMessage = null;
                return true;
            }
            catch (ArgumentException ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Gets the names of all named capturing groups in the pattern.
        /// </summary>
        /// <returns>
        /// An array of group names. Only includes named groups created with NamedGroup(),
        /// not numbered groups created with Group().
        /// </returns>
        /// <remarks>
        /// Named groups are created using the NamedGroup method which produces
        /// the regex syntax (?&lt;name&gt;pattern). This method filters out the
        /// numbered groups (0, 1, 2, etc.) returned by the underlying Regex API.
        /// </remarks>
        /// <example>
        /// var pattern = Pattern.With
        ///     .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
        ///     .Literal("-")
        ///     .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4));
        ///
        /// string[] names = pattern.GetGroupNames();
        /// // names contains ["area", "number"]
        /// </example>
        public string[] GetGroupNames()
        {
            var regex = ToRegex();
            var names = regex.GetGroupNames();
            var result = new List<string>();

            foreach (var name in names)
            {
                // Filter out numeric groups (unnamed)
                if (!int.TryParse(name, out _))
                {
                    result.Add(name);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Gets the total count of capturing groups in the pattern.
        /// </summary>
        /// <returns>
        /// The number of capturing groups, excluding group 0 (the entire match).
        /// Includes both named groups (NamedGroup) and numbered groups (Group).
        /// Does not count non-capturing groups (NonCapturingGroup).
        /// </returns>
        /// <remarks>
        /// Group 0 always represents the entire match and is not counted.
        /// Non-capturing groups created with NonCapturingGroup() do not contribute to the count.
        /// </remarks>
        /// <example>
        /// var pattern = Pattern.With
        ///     .Group(Pattern.With.Digit)                    // Group 1
        ///     .NonCapturingGroup(Pattern.With.Whitespace)   // Not counted
        ///     .NamedGroup("word", Pattern.With.Word);       // Group 2 (named)
        ///
        /// pattern.GetGroupCount();  // Returns 2
        /// </example>
        public int GetGroupCount()
        {
            var regex = ToRegex();
            return regex.GetGroupNumbers().Length - 1; // Exclude group 0 (entire match)
        }

        /// <summary>
        /// Provides a human-readable explanation of the pattern.
        /// Parses the regex string and describes each component in plain English.
        /// </summary>
        /// <returns>
        /// A multi-line string containing:
        /// - The raw pattern string
        /// - A line-by-line explanation of each regex construct
        /// </returns>
        /// <remarks>
        /// This method is useful for debugging complex patterns or for
        /// documentation purposes. The explanation includes:
        /// - Anchors (^, $)
        /// - Character classes and their contents
        /// - Quantifiers with their meanings
        /// - Groups (capturing, non-capturing, named, lookahead, lookbehind)
        /// - Escape sequences
        /// - Literal characters
        ///
        /// The output is indented to show group nesting.
        /// </remarks>
        /// <example>
        /// var pattern = Pattern.With
        ///     .StartOfLine
        ///     .NamedGroup("digits", Pattern.With.Digit.Repeat.OneOrMore)
        ///     .EndOfLine;
        ///
        /// Console.WriteLine(pattern.Explain());
        /// // Output:
        /// // Pattern: ^(?&lt;digits&gt;\d+)$
        /// //
        /// // Explanation:
        /// //   - Start of line/string
        /// //   - Named capturing group 'digits'
        /// //     - Digit [0-9]
        /// //     - One or more
        /// //   - End of group
        /// //   - End of line/string
        /// </example>
        public string Explain()
        {
            var pattern = ToString();
            var sb = new StringBuilder();
            sb.AppendLine("Pattern: " + pattern);
            sb.AppendLine();
            sb.AppendLine("Explanation:");

            int i = 0;
            int indentLevel = 0;

            while (i < pattern.Length)
            {
                var (explanation, newIndent) = ExplainConstruct(pattern, ref i, indentLevel);
                if (!string.IsNullOrEmpty(explanation))
                {
                    var indent = new string(' ', indentLevel * 2);
                    sb.AppendLine($"  {indent}- {explanation}");
                }
                indentLevel += newIndent;
                if (indentLevel < 0) indentLevel = 0;
            }

            return sb.ToString();
        }

        private static (string explanation, int indentChange) ExplainConstruct(string pattern, ref int index, int currentIndent)
        {
            if (index >= pattern.Length) return (null, 0);

            char c = pattern[index];

            switch (c)
            {
                case '^':
                    index++;
                    return ("Start of line/string", 0);

                case '$':
                    index++;
                    return ("End of line/string", 0);

                case '.':
                    index++;
                    return ("Any character (except newline)", 0);

                case '*':
                    index++;
                    if (index < pattern.Length && pattern[index] == '?')
                    {
                        index++;
                        return ("Zero or more (lazy)", 0);
                    }
                    return ("Zero or more", 0);

                case '+':
                    index++;
                    if (index < pattern.Length && pattern[index] == '?')
                    {
                        index++;
                        return ("One or more (lazy)", 0);
                    }
                    return ("One or more", 0);

                case '?':
                    index++;
                    return ("Optional (zero or one)", 0);

                case '\\':
                    return ExplainEscape(pattern, ref index);

                case '[':
                    return ExplainCharacterClass(pattern, ref index);

                case '(':
                    return ExplainGroup(pattern, ref index);

                case ')':
                    index++;
                    return ("End of group", -1);

                case '{':
                    return ExplainQuantifier(pattern, ref index);

                case '|':
                    index++;
                    return ("OR (alternation)", 0);

                default:
                    index++;
                    if (char.IsLetterOrDigit(c))
                        return ($"Literal '{c}'", 0);
                    return ($"Character '{c}'", 0);
            }
        }

        private static (string explanation, int indentChange) ExplainEscape(string pattern, ref int index)
        {
            index++; // Skip backslash
            if (index >= pattern.Length)
                return ("Escape character", 0);

            char next = pattern[index];
            index++;

            return next switch
            {
                'd' => ("Digit [0-9]", 0),
                'D' => ("Non-digit [^0-9]", 0),
                'w' => ("Word character [a-zA-Z0-9_]", 0),
                'W' => ("Non-word character", 0),
                's' => ("Whitespace", 0),
                'S' => ("Non-whitespace", 0),
                'b' => ("Word boundary", 0),
                'B' => ("Non-word boundary", 0),
                't' => ("Tab", 0),
                'n' => ("Newline", 0),
                'r' => ("Carriage return", 0),
                'k' => ExplainNamedBackreference(pattern, ref index),
                _ when char.IsDigit(next) => ($"Backreference to group {next}", 0),
                _ => ($"Escaped literal '{next}'", 0)
            };
        }

        private static (string explanation, int indentChange) ExplainNamedBackreference(string pattern, ref int index)
        {
            if (index < pattern.Length && pattern[index] == '<')
            {
                var end = pattern.IndexOf('>', index);
                if (end > index)
                {
                    var name = pattern.Substring(index + 1, end - index - 1);
                    index = end + 1;
                    return ($"Backreference to named group '{name}'", 0);
                }
            }
            return ("Backreference", 0);
        }

        private static (string explanation, int indentChange) ExplainCharacterClass(string pattern, ref int index)
        {
            var start = index;
            index++; // Skip opening bracket

            bool negated = false;
            if (index < pattern.Length && pattern[index] == '^')
            {
                negated = true;
                index++;
            }

            var end = pattern.IndexOf(']', index);
            if (end > index)
            {
                var content = pattern.Substring(index, end - index);
                index = end + 1;

                var prefix = negated ? "Any character NOT in" : "Any character in";
                return ($"{prefix}: [{content}]", 0);
            }

            index = start + 1;
            return ("Character class start", 0);
        }

        private static (string explanation, int indentChange) ExplainGroup(string pattern, ref int index)
        {
            index++; // Skip opening paren

            if (index < pattern.Length && pattern[index] == '?')
            {
                index++;
                if (index >= pattern.Length)
                    return ("Group start", 1);

                switch (pattern[index])
                {
                    case ':':
                        index++;
                        return ("Non-capturing group", 1);

                    case '=':
                        index++;
                        return ("Positive lookahead (must follow)", 1);

                    case '!':
                        index++;
                        return ("Negative lookahead (must NOT follow)", 1);

                    case '>':
                        index++;
                        return ("Atomic group (no backtracking)", 1);

                    case '<':
                        index++;
                        if (index < pattern.Length)
                        {
                            if (pattern[index] == '=')
                            {
                                index++;
                                return ("Positive lookbehind (must precede)", 1);
                            }
                            else if (pattern[index] == '!')
                            {
                                index++;
                                return ("Negative lookbehind (must NOT precede)", 1);
                            }
                            else
                            {
                                // Named group
                                var nameEnd = pattern.IndexOf('>', index);
                                if (nameEnd > index)
                                {
                                    var name = pattern.Substring(index, nameEnd - index);
                                    index = nameEnd + 1;
                                    return ($"Named capturing group '{name}'", 1);
                                }
                            }
                        }
                        return ("Lookbehind or named group", 1);

                    default:
                        return ("Special group", 1);
                }
            }

            return ("Capturing group", 1);
        }

        private static (string explanation, int indentChange) ExplainQuantifier(string pattern, ref int index)
        {
            index++; // Skip opening brace
            var end = pattern.IndexOf('}', index);

            if (end > index)
            {
                var content = pattern.Substring(index, end - index);
                index = end + 1;

                // Check for lazy quantifier
                bool lazy = false;
                if (index < pattern.Length && pattern[index] == '?')
                {
                    lazy = true;
                    index++;
                }

                var lazyStr = lazy ? " (lazy)" : "";

                if (content.Contains(","))
                {
                    var parts = content.Split(',');
                    if (string.IsNullOrEmpty(parts[1]))
                        return ($"At least {parts[0]} times{lazyStr}", 0);
                    if (string.IsNullOrEmpty(parts[0]))
                        return ($"At most {parts[1]} times{lazyStr}", 0);
                    return ($"Between {parts[0]} and {parts[1]} times{lazyStr}", 0);
                }

                return ($"Exactly {content} times{lazyStr}", 0);
            }

            return ("Quantifier", 0);
        }
    }
}
