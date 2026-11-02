using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// A fluent, validated alternative to replacement strings such as <c>"${year}-$2"</c>.
    /// Group names and numbers are checked against the pattern before any replacement runs.
    /// </summary>
    /// <example>
    /// datePattern.Replace(input, r =&gt; r.Group("day").Literal("/").Group("month").Literal("/").Group("year"));
    /// datePattern.Replace(input, r =&gt; r.Group("month", m =&gt; MonthName(m)).Literal(" ").Group("year"));
    /// </example>
    public sealed class Replacement
    {
        private readonly List<Part> _parts = new List<Part>();

        private sealed class Part
        {
            public string Syntax;
            public string GroupName;
            public int GroupNumber = -1;
            public Func<string, string> Transform;
            public Func<Match, string> Computed;
        }

        /// <summary>Starts an empty replacement.</summary>
        public static Replacement With => new Replacement();

        /// <summary>Inserts the text captured by a named group.</summary>
        public Replacement Group(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A group name is required.", nameof(name));
            _parts.Add(new Part { Syntax = "${" + name + "}", GroupName = name });
            return this;
        }

        /// <summary>Inserts the text captured by a named group, transformed by <paramref name="transform"/>.</summary>
        public Replacement Group(string name, Func<string, string> transform)
        {
            Group(name);
            _parts[_parts.Count - 1].Transform = transform ?? throw new ArgumentNullException(nameof(transform));
            return this;
        }

        /// <summary>Inserts the text captured by a numbered group.</summary>
        public Replacement Group(int number)
        {
            if (number < 0) throw new ArgumentOutOfRangeException(nameof(number));
            _parts.Add(new Part { Syntax = "${" + number.ToString(CultureInfo.InvariantCulture) + "}", GroupNumber = number });
            return this;
        }

        /// <summary>Inserts the text captured by a numbered group, transformed by <paramref name="transform"/>.</summary>
        public Replacement Group(int number, Func<string, string> transform)
        {
            Group(number);
            _parts[_parts.Count - 1].Transform = transform ?? throw new ArgumentNullException(nameof(transform));
            return this;
        }

        /// <summary>Inserts literal text. Dollar signs are escaped automatically.</summary>
        public Replacement Literal(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            _parts.Add(new Part { Syntax = text.Replace("$", "$$") });
            return this;
        }

        /// <summary>Inserts the whole match, optionally transformed: <c>$&amp;</c>.</summary>
        public Replacement WholeMatch(Func<string, string> transform = null)
        {
            _parts.Add(new Part { Syntax = "$&", GroupNumber = 0, Transform = transform });
            return this;
        }

        /// <summary>Inserts the input text before the match: <c>$`</c>.</summary>
        public Replacement BeforeMatch()
        {
            _parts.Add(new Part { Syntax = "$`" });
            return this;
        }

        /// <summary>Inserts the input text after the match: <c>$'</c>.</summary>
        public Replacement AfterMatch()
        {
            _parts.Add(new Part { Syntax = "$'" });
            return this;
        }

        /// <summary>Inserts the last captured group: <c>$+</c>.</summary>
        public Replacement LastGroup()
        {
            _parts.Add(new Part { Syntax = "$+" });
            return this;
        }

        /// <summary>Inserts the entire input string: <c>$_</c>.</summary>
        public Replacement EntireInput()
        {
            _parts.Add(new Part { Syntax = "$_" });
            return this;
        }

        /// <summary>Inserts text computed from the match.</summary>
        public Replacement Computed(Func<Match, string> compute)
        {
            _parts.Add(new Part { Computed = compute ?? throw new ArgumentNullException(nameof(compute)) });
            return this;
        }

        internal bool NeedsEvaluator
        {
            get
            {
                foreach (Part part in _parts)
                {
                    if (part.Transform != null || part.Computed != null) return true;
                }
                return false;
            }
        }

        internal void Validate(Regex regex)
        {
            foreach (Part part in _parts)
            {
                bool missing = part.GroupName != null
                    ? regex.GroupNumberFromName(part.GroupName) < 0
                    : part.GroupNumber > 0 && regex.GroupNameFromNumber(part.GroupNumber).Length == 0;
                if (missing)
                {
                    string label = part.GroupName != null ? "'" + part.GroupName + "'" : "#" + part.GroupNumber.ToString(CultureInfo.InvariantCulture);
                    throw new ArgumentException(
                        "Group " + label + " does not exist in the pattern " + regex + ". Available groups: " + string.Join(", ", regex.GetGroupNames()) + ".");
                }
            }
        }

        internal string Evaluate(Match match)
        {
            var sb = new StringBuilder();
            foreach (Part part in _parts)
            {
                if (part.Computed != null)
                {
                    sb.Append(part.Computed(match));
                }
                else if (part.Transform != null)
                {
                    Group group = part.GroupName != null ? match.Groups[part.GroupName] : match.Groups[part.GroupNumber];
                    sb.Append(part.Transform(group.Value));
                }
                else
                {
                    sb.Append(match.Result(part.Syntax));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns the equivalent .NET replacement string. Transformed and computed parts appear as
        /// <c>{computed}</c> because they cannot be expressed in replacement syntax.
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (Part part in _parts) sb.Append(part.Transform != null || part.Computed != null ? "{computed}" : part.Syntax);
            return sb.ToString();
        }
    }

    public partial class Pattern
    {
        /// <summary>
        /// Replaces every match using a fluent replacement. Group names and numbers are validated
        /// against the pattern before anything is replaced.
        /// </summary>
        /// <exception cref="ArgumentException">The replacement refers to a group the pattern does not have.</exception>
        public string Replace(string input, Func<Replacement, Replacement> build) => Replace(input, build, -1);

        /// <summary>
        /// Replaces up to <paramref name="count"/> matches using a fluent replacement.
        /// </summary>
        public string Replace(string input, Func<Replacement, Replacement> build, int count)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (build == null) throw new ArgumentNullException(nameof(build));
            Replacement replacement = build(new Replacement()) ?? throw new InvalidOperationException("The replacement builder returned null.");

            Regex regex = CachedRegex;
            replacement.Validate(regex);
            return replacement.NeedsEvaluator
                ? regex.Replace(input, replacement.Evaluate, count)
                : regex.Replace(input, replacement.ToString(), count);
        }
    }
}
