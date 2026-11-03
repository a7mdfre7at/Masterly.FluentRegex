using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Typed extraction: map named groups straight onto records and classes.
    /// </summary>
    public partial class Pattern
    {
        private const DynamicallyAccessedMemberTypes BindableMembers =
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties;

        /// <summary>
        /// Matches <paramref name="input"/> and binds the named groups to a new <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">
        /// A record or class. Groups bind by name (case-insensitive) to the parameters of the public
        /// constructor with the most parameters, then to remaining settable properties. Use
        /// <see cref="RegexGroupAttribute"/> to bind a member to a differently named group.
        /// </typeparam>
        /// <param name="input">The text to match.</param>
        /// <param name="provider">Culture for number and date parsing. Defaults to the invariant culture.</param>
        /// <returns>The populated instance.</returns>
        /// <exception cref="FormatException">The input does not match, or a value cannot be converted.</exception>
        /// <exception cref="InvalidOperationException">A required constructor parameter has no matching group.</exception>
        /// <example>
        /// record LogEntry(DateTime Timestamp, LogLevel Level, string Message);
        /// LogEntry entry = logPattern.Extract&lt;LogEntry&gt;("2024-05-17T10:00:00 ERROR Disk full");
        /// </example>
        public T Extract<[DynamicallyAccessedMembers(BindableMembers)] T>(string input, IFormatProvider provider = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            Match match = CachedRegex.Match(input);
            if (!match.Success)
                throw new FormatException("The input does not match the pattern " + ToString() + ". Use Trace(input) to see where it fails.");
            return TypeBinder<T>.Bind(match, CachedRegex, provider);
        }

        /// <summary>
        /// Tries to match <paramref name="input"/> and bind the named groups to a new <typeparamref name="T"/>.
        /// </summary>
        /// <returns>True when the input matched and every value converted.</returns>
        public bool TryExtract<[DynamicallyAccessedMembers(BindableMembers)] T>(string input, out T result, IFormatProvider provider = null)
        {
            result = default;
            if (input == null) return false;
            Match match = CachedRegex.Match(input);
            if (!match.Success) return false;
            try
            {
                result = TypeBinder<T>.Bind(match, CachedRegex, provider);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// Binds every match in <paramref name="input"/> to a new <typeparamref name="T"/>. Evaluated lazily.
        /// </summary>
        /// <example>
        /// foreach (var entry in logPattern.ExtractAll&lt;LogEntry&gt;(File.ReadAllText("app.log"))) { ... }
        /// </example>
        public IEnumerable<T> ExtractAll<[DynamicallyAccessedMembers(BindableMembers)] T>(string input, IFormatProvider provider = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return ExtractAllIterator<T>(input, provider);
        }

        private IEnumerable<T> ExtractAllIterator<[DynamicallyAccessedMembers(BindableMembers)] T>(string input, IFormatProvider provider)
        {
            Regex regex = CachedRegex;
            for (Match match = regex.Match(input); match.Success; match = match.NextMatch())
                yield return TypeBinder<T>.Bind(match, regex, provider);
        }
    }
}
