using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex
{
    /// <summary>One construct of the pattern and the exact text it consumed.</summary>
    public sealed class TraceStep
    {
        internal TraceStep(string construct, string description, int index, int length, string value, IReadOnlyList<TraceStep> children)
        {
            Construct = construct;
            Description = description;
            Index = index;
            Length = length;
            Value = value;
            Children = children;
        }

        /// <summary>Gets the regex text of the construct.</summary>
        public string Construct { get; }

        /// <summary>Gets a plain-language description of the construct.</summary>
        public string Description { get; }

        /// <summary>Gets the position in the input where the construct started matching.</summary>
        public int Index { get; }

        /// <summary>Gets the number of characters the construct consumed.</summary>
        public int Length { get; }

        /// <summary>Gets the consumed text.</summary>
        public string Value { get; }

        /// <summary>Gets the steps of the construct's parts. Repeated groups have one child per iteration.</summary>
        public IReadOnlyList<TraceStep> Children { get; }

        /// <inheritdoc />
        public override string ToString() => Construct + " => \"" + Value + "\" at " + Index.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Where and why a pattern stopped matching.</summary>
    public sealed class TraceFailure
    {
        internal TraceFailure(int position, string construct, string description, string matchedSoFar, string found, IReadOnlyList<TraceStep> matchedSteps)
        {
            Position = position;
            Construct = construct;
            Description = description;
            MatchedSoFar = matchedSoFar;
            Found = found;
            MatchedSteps = matchedSteps;
        }

        /// <summary>Gets the input position where the pattern could not continue.</summary>
        public int Position { get; }

        /// <summary>Gets the regex text of the construct that failed.</summary>
        public string Construct { get; }

        /// <summary>Gets a plain-language description of what was expected.</summary>
        public string Description { get; }

        /// <summary>Gets the text the preceding constructs matched.</summary>
        public string MatchedSoFar { get; }

        /// <summary>Gets the quoted input text at <see cref="Position"/>, or "the end of the input".</summary>
        public string Found { get; }

        /// <summary>
        /// Gets the trace of the constructs that did match before the failure. When the failure is inside a group
        /// that had already matched some text, that group is left out; <see cref="MatchedSoFar"/> includes its text.
        /// </summary>
        public IReadOnlyList<TraceStep> MatchedSteps { get; }
    }

    /// <summary>
    /// The result of <see cref="Pattern.Trace(string)"/>: which construct consumed which characters,
    /// or where the match died.
    /// </summary>
    public sealed class MatchTrace
    {
        internal MatchTrace(string pattern, string input, bool isMatch, int index, int length, IReadOnlyList<TraceStep> steps, TraceFailure failure)
        {
            Pattern = pattern;
            Input = input;
            IsMatch = isMatch;
            Index = index;
            Length = length;
            Steps = steps;
            Failure = failure;
        }

        /// <summary>Gets the regex string.</summary>
        public string Pattern { get; }

        /// <summary>Gets the traced input.</summary>
        public string Input { get; }

        /// <summary>Gets a value indicating whether the pattern matched.</summary>
        public bool IsMatch { get; }

        /// <summary>Gets the start of the match, or -1.</summary>
        public int Index { get; }

        /// <summary>Gets the length of the match.</summary>
        public int Length { get; }

        /// <summary>Gets the matched text, or null.</summary>
        public string Value => IsMatch ? Input.Substring(Index, Length) : null;

        /// <summary>Gets the top-level steps of a successful match.</summary>
        public IReadOnlyList<TraceStep> Steps { get; }

        /// <summary>Gets the failure analysis when the pattern did not match, otherwise null.</summary>
        public TraceFailure Failure { get; }

        /// <summary>Returns a readable, indented report of the trace.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Pattern: ").AppendLine(Pattern);
            sb.Append("Input:   ").AppendLine(Quote(Input));

            if (IsMatch)
            {
                sb.Append("Result:  MATCH at ").Append(Index.ToString(CultureInfo.InvariantCulture))
                  .Append(", length ").Append(Length.ToString(CultureInfo.InvariantCulture)).Append(": ").AppendLine(Quote(Value));
                sb.AppendLine();
                AppendSteps(sb, Steps, 1);
                return sb.ToString();
            }

            sb.AppendLine("Result:  NO MATCH");
            if (Failure == null) return sb.ToString();

            sb.AppendLine();
            if (Failure.MatchedSteps.Count > 0)
            {
                sb.AppendLine("Matched before the failure:");
                AppendSteps(sb, Failure.MatchedSteps, 1);
                sb.AppendLine();
            }

            sb.Append("Failed at position ").Append(Failure.Position.ToString(CultureInfo.InvariantCulture)).Append(": expected ")
              .Append(Failure.Construct).Append(" (").Append(Failure.Description).AppendLine(")");
            sb.Append("Found:   ").AppendLine(Visible(Failure.Found));
            sb.AppendLine();
            sb.Append("  ").AppendLine(Visible(Input));
            sb.Append("  ").Append(' ', VisibleLength(Input, Failure.Position)).AppendLine("^");
            return sb.ToString();
        }

        private static void AppendSteps(StringBuilder sb, IReadOnlyList<TraceStep> steps, int depth)
        {
            foreach (TraceStep step in steps)
            {
                string span = "[" + step.Index.ToString(CultureInfo.InvariantCulture) + ".." +
                              (step.Index + step.Length).ToString(CultureInfo.InvariantCulture) + ")";
                string construct = step.Construct.Length > 32 ? step.Construct.Substring(0, 29) + "..." : step.Construct;
                string indent = new string(' ', depth * 2);
                sb.Append(indent).Append(span.PadRight(9)).Append(' ')
                  .Append(construct.PadRight(Math.Max(0, 34 - indent.Length))).Append(' ')
                  .Append(Quote(step.Value).PadRight(16)).Append(' ')
                  .AppendLine(step.Description);
                AppendSteps(sb, step.Children, depth + 1);
            }
        }

        private static string Quote(string text) => "\"" + Visible(text) + "\"";

        private static string Visible(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text) sb.Append(VisibleChar(c));
            return sb.ToString();
        }

        private static int VisibleLength(string text, int upTo)
        {
            int length = 0;
            for (int i = 0; i < upTo && i < text.Length; i++) length += VisibleChar(text[i]).Length;
            return length;
        }

        private static string VisibleChar(char c)
        {
            switch (c)
            {
                case '\t': return "\\t";
                case '\n': return "\\n";
                case '\r': return "\\r";
                default: return char.IsControl(c) ? "\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture) : c.ToString();
            }
        }
    }
}
