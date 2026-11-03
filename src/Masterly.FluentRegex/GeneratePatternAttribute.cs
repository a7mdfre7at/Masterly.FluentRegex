using System;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Evaluates a static <see cref="Pattern"/> field or property at compile time and generates a
    /// method that returns a cached, compiled <see cref="System.Text.RegularExpressions.Regex"/>,
    /// plus a constant holding the regex text.
    /// </summary>
    /// <remarks>
    /// The containing type must be partial and the member must be static. Prefer an expression-bodied
    /// property (<c>=&gt;</c>) so the fluent chain is never executed at runtime.
    /// The build reports a diagnostic when the chain uses values that are not compile-time constants.
    /// </remarks>
    /// <example>
    /// public static partial class Validators
    /// {
    ///     [GeneratePattern]
    ///     private static Pattern IsoDate =&gt; Pattern.With.StartOfLine.Digit.Repeat.Times(4).Literal("-").Digit.Repeat.Times(2).EndOfLine;
    ///
    ///     // Generated:
    ///     // public const string IsoDateSource = @"^\d{4}-\d{2}$";
    ///     // private static Regex IsoDateRegex() { ... }
    /// }
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class GeneratePatternAttribute : Attribute
    {
        /// <summary>Generates a method named after the member with a "Regex" suffix.</summary>
        public GeneratePatternAttribute()
        {
        }

        /// <summary>Generates a method with the given name.</summary>
        /// <param name="methodName">The name of the generated method.</param>
        public GeneratePatternAttribute(string methodName) => MethodName = methodName;

        /// <summary>Gets the name of the generated method, or null for the default name.</summary>
        public string MethodName { get; }
    }
}
