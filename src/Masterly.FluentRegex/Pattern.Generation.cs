using System;
using Masterly.FluentRegex.Generation;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Test data generation: strings that match the pattern and near misses that do not.
    /// </summary>
    public partial class Pattern
    {
        /// <summary>
        /// Generates distinct strings that this pattern matches. Every returned string is verified
        /// against the real .NET regex engine, including lookarounds, backreferences and options.
        /// </summary>
        /// <remarks>
        /// Verification uses the pattern's options (without <c>Compiled</c>) and a fixed one-second timeout per
        /// candidate, not the pattern's own timeout. A candidate whose check times out is skipped.
        /// </remarks>
        /// <param name="count">How many samples to produce.</param>
        /// <param name="seed">A seed for reproducible output. Omit for a different set each call.</param>
        /// <returns>
        /// Up to <paramref name="count"/> matching strings. Fewer are returned when the pattern has fewer
        /// distinct matches, or when its constraints are too narrow to hit by sampling.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="InvalidOperationException">
        /// A character class has no character that can be generated, for example <c>\p{Cs}</c> (surrogates only).
        /// </exception>
        /// <example>
        /// string[] ips = CommonPatterns.IPv4().GenerateMatching(5, seed: 42);
        /// // "127.253.251.243", "254.11.253.8", "250.70.207.202", "250.235.240.250", "6.225.247.255"
        /// </example>
        public string[] GenerateMatching(int count = 10, int? seed = null)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0) return Array.Empty<string>();
            var generator = new SampleGenerator(ParseTree(), _options, seed ?? Environment.TickCount);
            return generator.GenerateMatching(count).ToArray();
        }

        /// <summary>
        /// Generates distinct strings that this pattern does not match. Most are near misses:
        /// valid samples with one character inserted, removed, replaced, duplicated or swapped,
        /// which makes them ideal negative test cases.
        /// </summary>
        /// <param name="count">How many samples to produce.</param>
        /// <param name="seed">A seed for reproducible output. Omit for a different set each call.</param>
        /// <returns>Up to <paramref name="count"/> non-matching strings.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="InvalidOperationException">
        /// A character class has no character that can be generated, for example <c>\p{Cs}</c> (surrogates only).
        /// </exception>
        /// <example>
        /// string[] bad = CommonPatterns.IPv4().GenerateNonMatching(5, seed: 42);
        /// // "25411.253.8", "255.05Z5.229.241", "x255.055.229.241", "#6.225.247.255", "250"
        /// </example>
        public string[] GenerateNonMatching(int count = 10, int? seed = null)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0) return Array.Empty<string>();
            var generator = new SampleGenerator(ParseTree(), _options, seed ?? Environment.TickCount);
            return generator.GenerateNonMatching(count).ToArray();
        }
    }
}
