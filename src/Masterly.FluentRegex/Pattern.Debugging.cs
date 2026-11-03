using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// //     - Digit (any Unicode decimal digit)
        /// //     - One or more
        /// //   - End of group
        /// //   - End of line/string
        /// </example>
        public string Explain()
        {
            string pattern = ToString();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Pattern: " + pattern);
            builder.AppendLine();
            builder.AppendLine("Explanation:");

            ExplainContext context = new ExplainContext(pattern, _options);
            int index = 0;
            int indentLevel = 0;

            while (index < pattern.Length)
            {
                (string explanation, int indentChange) = ExplainNext(pattern, ref index, context);
                if (!string.IsNullOrEmpty(explanation))
                {
                    string indent = new string(' ', indentLevel * 2);
                    builder.AppendLine($"  {indent}- {explanation}");
                }

                indentLevel += indentChange;
                if (indentLevel < 0)
                {
                    indentLevel = 0;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// What the explainer knows about the whole pattern while it walks it: the capture groups the .NET engine
        /// sees (needed to tell a backreference such as \12 from an octal escape, and a group condition from an
        /// expression condition), which open groups are conditionals (whose | means "otherwise"), and whether
        /// ECMAScript semantics narrow \d and \w to ASCII.
        /// </summary>
        private sealed class ExplainContext
        {
            // Only the options that change how the pattern parses. NonBacktracking would reject backreferences and
            // conditionals, and Compiled would only cost time.
            private const RegexOptions ParseOptions = RegexOptions.IgnoreCase | RegexOptions.Multiline
                | RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace
                | RegexOptions.RightToLeft | RegexOptions.ECMAScript | RegexOptions.CultureInvariant;

            private readonly HashSet<int> _groupNumbers = new HashSet<int>();
            private readonly HashSet<string> _groupNames = new HashSet<string>(StringComparer.Ordinal);
            private readonly Stack<bool> _openGroups = new Stack<bool>();

            public ExplainContext(string pattern, RegexOptions options)
            {
                IsEcmaScript = (options & RegexOptions.ECMAScript) != 0;
                try
                {
                    Regex regex = new Regex(pattern, options & ParseOptions);
                    foreach (int number in regex.GetGroupNumbers())
                    {
                        _groupNumbers.Add(number);
                    }

                    foreach (string name in regex.GetGroupNames())
                    {
                        _groupNames.Add(name);
                    }
                }
                catch (ArgumentException)
                {
                    // An invalid pattern is still explained token by token; it just has no known groups.
                }
            }

            public bool IsEcmaScript { get; }

            public bool InConditional => _openGroups.Count > 0 && _openGroups.Peek();

            public bool HasGroup(int number) => _groupNumbers.Contains(number);

            public bool HasGroup(string name) => _groupNames.Contains(name);

            public void OpenGroup(bool isConditional) => _openGroups.Push(isConditional);

            public void CloseGroup()
            {
                if (_openGroups.Count > 0)
                {
                    _openGroups.Pop();
                }
            }
        }

        private static (string explanation, int indentChange) ExplainNext(string pattern, ref int index, ExplainContext context)
        {
            char current = pattern[index];
            switch (current)
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
                    return ExplainLazySuffix(pattern, ref index, "Zero or more");

                case '+':
                    index++;
                    return ExplainLazySuffix(pattern, ref index, "One or more");

                case '?':
                    index++;
                    if (index < pattern.Length && pattern[index] == '?')
                    {
                        index++;
                        return ("Optional (lazy)", 0);
                    }

                    return ("Optional (zero or one)", 0);

                case '\\':
                    return ExplainEscape(pattern, ref index, context);

                case '[':
                    return ExplainCharacterClass(pattern, ref index);

                case '(':
                    return ExplainGroup(pattern, ref index, context);

                case ')':
                    index++;
                    context.CloseGroup();
                    return ("End of group", -1);

                case '{':
                    return ExplainQuantifier(pattern, ref index);

                case '|':
                    index++;
                    return context.InConditional ? ("Otherwise", 0) : ("OR (alternation)", 0);

                default:
                    index++;
                    return char.IsLetterOrDigit(current) ? ($"Literal '{current}'", 0) : ($"Character '{current}'", 0);
            }
        }

        private static (string explanation, int indentChange) ExplainLazySuffix(string pattern, ref int index, string text)
        {
            if (index < pattern.Length && pattern[index] == '?')
            {
                index++;
                return ($"{text} (lazy)", 0);
            }

            return (text, 0);
        }

        private static (string explanation, int indentChange) ExplainEscape(string pattern, ref int index, ExplainContext context)
        {
            index++; // Skip the backslash.
            if (index >= pattern.Length)
            {
                return ("Escape character", 0);
            }

            char next = pattern[index];
            index++;

            switch (next)
            {
                // Without ECMAScript, .NET's \d and \w are Unicode-aware: \d also matches digits such as Arabic-Indic.
                case 'd': return (context.IsEcmaScript ? "Digit [0-9]" : "Digit (any Unicode decimal digit)", 0);
                case 'D': return (context.IsEcmaScript ? "Non-digit [^0-9]" : "Non-digit", 0);
                case 'w': return (context.IsEcmaScript ? "Word character [a-zA-Z0-9_]" : "Word character (any Unicode letter, digit or _)", 0);
                case 'W': return ("Non-word character", 0);
                case 's': return ("Whitespace", 0);
                case 'S': return ("Non-whitespace", 0);
                case 'b': return ("Word boundary", 0);
                case 'B': return ("Non-word boundary", 0);
                case 'A': return ("Start of string", 0);
                case 'z': return ("End of string", 0);
                case 'Z': return ("End of string (or before a final newline)", 0);
                case 'G': return ("Where the previous match ended", 0);
                case 't': return ("Tab", 0);
                case 'n': return ("Newline", 0);
                case 'r': return ("Carriage return", 0);
                case 'f': return ("Form feed", 0);
                case 'v': return ("Vertical tab", 0);
                case 'a': return ("Bell character", 0);
                case 'e': return ("Escape character (U+001B)", 0);
                case 'x': return ExplainHexEscape(pattern, ref index, 2);
                case 'u': return ExplainHexEscape(pattern, ref index, 4);
                case 'c': return ExplainControlEscape(pattern, ref index);
                case 'p': return ExplainUnicodeCategory(pattern, ref index, negated: false);
                case 'P': return ExplainUnicodeCategory(pattern, ref index, negated: true);
                case 'k': return ExplainNamedBackreference(pattern, ref index);
                case '0': return ExplainOctalEscape(pattern, ref index, index - 1);
                default:
                    if (next >= '1' && next <= '9')
                    {
                        return ExplainNumberedBackreference(pattern, ref index, context);
                    }

                    return ($"Escaped literal '{next}'", 0);
            }
        }

        private static (string explanation, int indentChange) ExplainHexEscape(string pattern, ref int index, int length)
        {
            char kind = pattern[index - 1];
            if (index + length <= pattern.Length && IsHex(pattern, index, length))
            {
                int code = Convert.ToInt32(pattern.Substring(index, length), 16);
                index += length;
                return ($"Character {DescribeCodePoint(code)}", 0);
            }

            return ($"Escaped literal '{kind}'", 0);
        }

        private static (string explanation, int indentChange) ExplainControlEscape(string pattern, ref int index)
        {
            if (index < pattern.Length && char.IsLetter(pattern[index]))
            {
                char letter = char.ToUpperInvariant(pattern[index]);
                index++;
                return ($"Control character Ctrl+{letter}", 0);
            }

            return ("Escaped literal 'c'", 0);
        }

        private static (string explanation, int indentChange) ExplainUnicodeCategory(string pattern, ref int index, bool negated)
        {
            char kind = negated ? 'P' : 'p';
            if (index < pattern.Length && pattern[index] == '{')
            {
                int end = pattern.IndexOf('}', index);
                if (end > index + 1)
                {
                    string name = pattern.Substring(index + 1, end - index - 1);
                    index = end + 1;
                    return negated
                        ? ($"Any character NOT in Unicode category or block '{name}'", 0)
                        : ($"Any character in Unicode category or block '{name}'", 0);
                }
            }

            return ($"Escaped literal '{kind}'", 0);
        }

        private static (string explanation, int indentChange) ExplainOctalEscape(string pattern, ref int index, int digitsStart)
        {
            // \0 is followed by up to two more octal digits: \0, \07, \012.
            int end = digitsStart + 1;
            while (end < pattern.Length && end < digitsStart + 3 && pattern[end] >= '0' && pattern[end] <= '7')
            {
                end++;
            }

            int code = Convert.ToInt32(pattern.Substring(digitsStart, end - digitsStart), 8);
            index = end;
            return ($"Character {DescribeCodePoint(code)}", 0);
        }

        private static (string explanation, int indentChange) ExplainNumberedBackreference(string pattern, ref int index, ExplainContext context)
        {
            // .NET reads every following digit. The number is a backreference when that group exists, and a single
            // digit always is (an undefined one makes the pattern invalid). Otherwise the digits are an octal escape.
            int start = index - 1;
            int end = index;
            while (end < pattern.Length && pattern[end] >= '0' && pattern[end] <= '9')
            {
                end++;
            }

            string digits = pattern.Substring(start, end - start);
            if (int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                && (context.HasGroup(number) || digits.Length == 1))
            {
                index = end;
                return ($"Backreference to group {number}", 0);
            }

            int octalEnd = start;
            while (octalEnd < pattern.Length && octalEnd < start + 3 && pattern[octalEnd] >= '0' && pattern[octalEnd] <= '7')
            {
                octalEnd++;
            }

            if (octalEnd == start)
            {
                index = start + 1;
                return ($"Escaped literal '{pattern[start]}'", 0);
            }

            int code = Convert.ToInt32(pattern.Substring(start, octalEnd - start), 8);
            index = octalEnd;
            return ($"Character {DescribeCodePoint(code)}", 0);
        }

        private static (string explanation, int indentChange) ExplainNamedBackreference(string pattern, ref int index)
        {
            if (index < pattern.Length && (pattern[index] == '<' || pattern[index] == '\''))
            {
                char close = pattern[index] == '<' ? '>' : '\'';
                int end = pattern.IndexOf(close, index + 1);
                if (end > index + 1)
                {
                    string name = pattern.Substring(index + 1, end - index - 1);
                    index = end + 1;
                    return ($"Backreference to named group '{name}'", 0);
                }
            }

            return ("Backreference", 0);
        }

        private static (string explanation, int indentChange) ExplainCharacterClass(string pattern, ref int index)
        {
            int start = index;
            int position = index + 1;
            bool negated = position < pattern.Length && pattern[position] == '^';
            if (negated)
            {
                position++;
            }

            int contentStart = position;
            int end = FindCharacterClassEnd(pattern, position);
            if (end < 0)
            {
                index = start + 1;
                return ("Character class start", 0);
            }

            string content = pattern.Substring(contentStart, end - contentStart);
            index = end + 1;
            string prefix = negated ? "Any character NOT in" : "Any character in";
            return ($"{prefix}: [{content}]", 0);
        }

        /// <summary>
        /// Returns the index of the ] that closes a set whose content starts at <paramref name="position"/>, or -1.
        /// A ] right after the opening bracket is a literal, escapes are skipped, and a subtraction such as
        /// [a-z-[aeiou]] is nested.
        /// </summary>
        private static int FindCharacterClassEnd(string pattern, int position)
        {
            if (position < pattern.Length && pattern[position] == ']')
            {
                position++;
            }

            while (position < pattern.Length)
            {
                char current = pattern[position];
                if (current == '\\')
                {
                    position += 2;
                    continue;
                }

                if (current == '-' && position + 1 < pattern.Length && pattern[position + 1] == '[')
                {
                    int nestedEnd = FindCharacterClassEnd(pattern, position + 2);
                    if (nestedEnd < 0)
                    {
                        return -1;
                    }

                    position = nestedEnd + 1;
                    continue;
                }

                if (current == ']')
                {
                    return position;
                }

                position++;
            }

            return -1;
        }

        private static (string explanation, int indentChange) ExplainGroup(string pattern, ref int index, ExplainContext context)
        {
            index++; // Skip the opening parenthesis.
            if (index >= pattern.Length || pattern[index] != '?')
            {
                context.OpenGroup(isConditional: false);
                return ("Capturing group", 1);
            }

            index++;
            if (index >= pattern.Length)
            {
                context.OpenGroup(isConditional: false);
                return ("Group start", 1);
            }

            switch (pattern[index])
            {
                case ':':
                    index++;
                    context.OpenGroup(isConditional: false);
                    return ("Non-capturing group", 1);

                case '=':
                    index++;
                    context.OpenGroup(isConditional: false);
                    return ("Positive lookahead (must follow)", 1);

                case '!':
                    index++;
                    context.OpenGroup(isConditional: false);
                    return ("Negative lookahead (must NOT follow)", 1);

                case '>':
                    index++;
                    context.OpenGroup(isConditional: false);
                    return ("Atomic group (no backtracking)", 1);

                case '#':
                    return ExplainComment(pattern, ref index);

                case '(':
                    return ExplainConditional(pattern, ref index, context);

                case '\'':
                    return ExplainNamedGroup(pattern, ref index, '\'', context);

                case '<':
                    if (index + 1 < pattern.Length && pattern[index + 1] == '=')
                    {
                        index += 2;
                        context.OpenGroup(isConditional: false);
                        return ("Positive lookbehind (must precede)", 1);
                    }

                    if (index + 1 < pattern.Length && pattern[index + 1] == '!')
                    {
                        index += 2;
                        context.OpenGroup(isConditional: false);
                        return ("Negative lookbehind (must NOT precede)", 1);
                    }

                    return ExplainNamedGroup(pattern, ref index, '>', context);

                default:
                    return ExplainInlineOptions(pattern, ref index, context);
            }
        }

        private static (string explanation, int indentChange) ExplainNamedGroup(string pattern, ref int index, char close, ExplainContext context)
        {
            index++; // Skip < or the opening quote.
            context.OpenGroup(isConditional: false);
            int nameEnd = pattern.IndexOf(close, index);
            if (nameEnd <= index)
            {
                return ("Lookbehind or named group", 1);
            }

            string name = pattern.Substring(index, nameEnd - index);
            index = nameEnd + 1;

            int dash = name.IndexOf('-');
            if (dash < 0)
            {
                return ($"Named capturing group '{name}'", 1);
            }

            string captured = name.Substring(0, dash);
            string popped = name.Substring(dash + 1);
            return captured.Length == 0
                ? ($"Balancing group (removes the last capture of '{popped}')", 1)
                : ($"Balancing group '{captured}' (captures the text since the last capture of '{popped}' and removes it)", 1);
        }

        private static (string explanation, int indentChange) ExplainComment(string pattern, ref int index)
        {
            int end = pattern.IndexOf(')', index);
            if (end < 0)
            {
                index = pattern.Length;
                return ("Comment", 0);
            }

            string text = pattern.Substring(index + 1, end - index - 1);
            index = end + 1;
            return (text.Length == 0 ? "Comment" : $"Comment: {text}", 0);
        }

        private static (string explanation, int indentChange) ExplainConditional(string pattern, ref int index, ExplainContext context)
        {
            // index is at the ( that opens the condition.
            context.OpenGroup(isConditional: true);
            if (index + 1 < pattern.Length && pattern[index + 1] == '?')
            {
                // An expression condition such as (?(?=\d)...): the lookaround is explained next, nested.
                return ("Conditional: if the lookaround below matches, use the first branch", 1);
            }

            int end = pattern.IndexOf(')', index);
            if (end < 0)
            {
                index++;
                return ("Conditional", 1);
            }

            string condition = pattern.Substring(index + 1, end - index - 1);
            index = end + 1;

            if (int.TryParse(condition, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                && context.HasGroup(number))
            {
                return ($"Conditional: if group {number} has matched, use the first branch", 1);
            }

            if (context.HasGroup(condition))
            {
                return ($"Conditional: if group '{condition}' has matched, use the first branch", 1);
            }

            return ($"Conditional: if '{condition}' matches next, use the first branch", 1);
        }

        private static (string explanation, int indentChange) ExplainInlineOptions(string pattern, ref int index, ExplainContext context)
        {
            int end = index;
            while (end < pattern.Length && InlineOptionFlags.IndexOf(pattern[end]) >= 0)
            {
                end++;
            }

            if (end == index || end >= pattern.Length || (pattern[end] != ')' && pattern[end] != ':'))
            {
                context.OpenGroup(isConditional: false);
                return ("Special group", 1);
            }

            string description = DescribeInlineOptions(pattern.Substring(index, end - index));
            bool scoped = pattern[end] == ':';
            index = end + 1;
            if (scoped)
            {
                context.OpenGroup(isConditional: false);
                return ($"Group with inline options ({description})", 1);
            }

            return ($"Inline options for the rest of the group ({description})", 0);
        }

        private const string InlineOptionFlags = "imnsxIMNSX-";

        private static string DescribeInlineOptions(string flags)
        {
            List<string> on = new List<string>();
            List<string> off = new List<string>();
            List<string> target = on;
            foreach (char flag in flags)
            {
                if (flag == '-')
                {
                    target = off;
                    continue;
                }

                target.Add(DescribeInlineOption(flag));
            }

            List<string> parts = new List<string>();
            if (on.Count > 0)
            {
                parts.Add("on: " + string.Join(", ", on));
            }

            if (off.Count > 0)
            {
                parts.Add("off: " + string.Join(", ", off));
            }

            return string.Join("; ", parts);
        }

        private static string DescribeInlineOption(char flag)
        {
            switch (char.ToLowerInvariant(flag))
            {
                case 'i': return "ignore case";
                case 'm': return "multiline";
                case 'n': return "explicit capture";
                case 's': return "single line";
                default: return "ignore pattern whitespace";
            }
        }

        private static (string explanation, int indentChange) ExplainQuantifier(string pattern, ref int index)
        {
            // .NET accepts {n}, {n,} and {n,m}; any other { is a literal brace.
            Match quantifier = BraceQuantifier.Match(pattern, index);
            if (!quantifier.Success)
            {
                index++;
                return ("Character '{'", 0);
            }

            index += quantifier.Length;
            bool lazy = index < pattern.Length && pattern[index] == '?';
            if (lazy)
            {
                index++;
            }

            string lazyText = lazy ? " (lazy)" : string.Empty;
            string minimum = quantifier.Groups[1].Value;
            if (!quantifier.Groups[2].Success)
            {
                return ($"Exactly {minimum} times{lazyText}", 0);
            }

            string maximum = quantifier.Groups[3].Value;
            return maximum.Length == 0
                ? ($"At least {minimum} times{lazyText}", 0)
                : ($"Between {minimum} and {maximum} times{lazyText}", 0);
        }

        private static readonly Regex BraceQuantifier = new Regex(@"\G\{([0-9]+)(,([0-9]*))?\}", RegexOptions.CultureInvariant);

        private static bool IsHex(string text, int start, int length)
        {
            for (int position = start; position < start + length; position++)
            {
                if (!Uri.IsHexDigit(text[position]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string DescribeCodePoint(int code)
        {
            string hex = "U+" + code.ToString("X4", CultureInfo.InvariantCulture);
            char character = (char)code;
            return char.IsControl(character) || char.IsWhiteSpace(character) ? hex : $"'{character}' ({hex})";
        }
    }
}
