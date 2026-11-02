using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.CodeGen;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Reverse engineering: turn existing regular expressions into readable fluent code.
    /// </summary>
    public partial class Pattern
    {
        /// <summary>
        /// Wraps an existing regular expression in a <see cref="Pattern"/>, after validating it.
        /// The result can be chained, analyzed, traced, visualized, or converted with <see cref="ToFluentCode"/>.
        /// </summary>
        /// <param name="regex">The regular expression.</param>
        /// <param name="options">The options the expression is used with.</param>
        /// <returns>A pattern that renders exactly <paramref name="regex"/>.</returns>
        /// <exception cref="ArgumentException">The expression is not valid .NET regex syntax.</exception>
        /// <example>
        /// string code = Pattern.FromRegex(@"^(?&lt;user&gt;[\w.]+)@(?&lt;host&gt;[\w-]+\.\w{2,})$").ToFluentCode();
        /// </example>
        public static Pattern FromRegex(
#if NET7_0_OR_GREATER
            [System.Diagnostics.CodeAnalysis.StringSyntax(System.Diagnostics.CodeAnalysis.StringSyntaxAttribute.Regex, nameof(options))]
#endif
            string regex,
            RegexOptions options = RegexOptions.None)
        {
            if (regex == null) throw new ArgumentNullException(nameof(regex));
            try
            {
                _ = new Regex(regex, options);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException("The regular expression is invalid: " + ex.Message, nameof(regex), ex);
            }
            return With.RegEx(regex).WithOptions(options);
        }

        /// <summary>
        /// Generates C# source code that rebuilds this pattern with the fluent API.
        /// </summary>
        /// <param name="indentSize">Spaces per indentation level for multi-line output.</param>
        /// <returns>A C# expression of type <see cref="Pattern"/>.</returns>
        /// <remarks>
        /// The generated code is semantically equivalent to the pattern. Constructs without a fluent
        /// equivalent, such as balancing groups or class subtraction, are emitted with <see cref="RegEx"/>.
        /// </remarks>
        public string ToFluentCode(int indentSize = 4)
        {
            if (indentSize < 0) throw new ArgumentOutOfRangeException(nameof(indentSize));
            RegexTree tree = ParseTree();
            return FluentCodeEmitter.Emit(tree, _options, _timeout, indentSize);
        }

        internal RegexTree ParseTree()
        {
            try
            {
                return RegexParser.Parse(ToString(), _options);
            }
            catch (RegexSyntaxException ex)
            {
                throw new InvalidOperationException("The pattern is not valid regex syntax: " + ex.Message, ex);
            }
        }
    }
}
