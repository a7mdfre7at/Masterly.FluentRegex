using System;
using Masterly.FluentRegex.Tracing;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Match tracing: see which construct consumed which characters, or where the match died.
    /// </summary>
    public partial class Pattern
    {
        /// <summary>
        /// Matches the pattern against <paramref name="input"/> and explains the result step by step.
        /// </summary>
        /// <param name="input">The text to match.</param>
        /// <returns>
        /// For a match, the span and text consumed by every construct, including each iteration of
        /// repeated groups. For a failure, the deepest construct that could not match, its position,
        /// what was found there, and the constructs that matched before it.
        /// </returns>
        /// <remarks>
        /// Spans come from the real .NET engine, so they are exact. The failure position is found by
        /// matching ever-longer prefixes of the pattern, which pinpoints the first construct that
        /// cannot be satisfied.
        /// </remarks>
        /// <example>
        /// Console.WriteLine(CommonPatterns.DateIso().Trace("2024-13-5"));
        /// </example>
        public MatchTrace Trace(string input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return MatchTracer.Trace(ParseTree(), _options, _timeout, input);
        }
    }
}
