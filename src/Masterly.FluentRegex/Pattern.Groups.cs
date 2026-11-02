using System;
using System.Globalization;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Partial class containing named groups and backreference functionality.
    /// Provides methods for creating named capturing groups, non-capturing groups,
    /// atomic groups, backreferences, and conditional patterns.
    /// </summary>
    public partial class Pattern
    {
        /// <summary>
        /// Creates a named capturing group.
        /// </summary>
        /// <param name="name">The name of the group.</param>
        /// <param name="pattern">The pattern to capture.</param>
        /// <returns>Pattern object for chaining.</returns>
        /// <example>
        /// Pattern.With.NamedGroup("digits", Pattern.With.Digit.Repeat.OneOrMore)
        /// // Produces: (?&lt;digits&gt;\d+)
        /// </example>
        public Pattern NamedGroup(string name, Pattern pattern)
        {
            return Append(new WrapToken("(?<" + ValidateGroupName(name) + ">", Require(pattern, nameof(pattern)), ")"));
        }

        /// <summary>
        /// Creates a non-capturing group. Useful for grouping without creating a backreference.
        /// </summary>
        /// <param name="pattern">The pattern to group without capturing.</param>
        /// <returns>Pattern object for chaining.</returns>
        /// <example>
        /// Pattern.With.NonCapturingGroup(Pattern.With.Literal("abc"))
        /// // Produces: (?:abc)
        /// </example>
        public Pattern NonCapturingGroup(Pattern pattern)
        {
            return Append(new WrapToken("(?:", Require(pattern, nameof(pattern)), ")"));
        }

        /// <summary>
        /// Creates an atomic group (non-backtracking). Once matched, the engine won't backtrack into it.
        /// </summary>
        /// <param name="pattern">The pattern for the atomic group.</param>
        /// <returns>Pattern object for chaining.</returns>
        /// <example>
        /// Pattern.With.AtomicGroup(Pattern.With.Digit.Repeat.OneOrMore)
        /// // Produces: (?&gt;\d+)
        /// </example>
        public Pattern AtomicGroup(Pattern pattern)
        {
            return Append(new WrapToken("(?>", Require(pattern, nameof(pattern)), ")"));
        }

        /// <summary>
        /// Creates a backreference to a named group.
        /// </summary>
        /// <param name="name">The name of the group to reference.</param>
        /// <returns>Pattern object for chaining.</returns>
        /// <example>
        /// Pattern.With.NamedGroup("word", Pattern.With.Word.Repeat.OneOrMore)
        ///             .Whitespace
        ///             .Backreference("word")
        /// // Matches repeated words like "the the"
        /// </example>
        public Pattern Backreference(string name)
        {
            return Append(new RawToken(@"\k<" + ValidateGroupName(name) + ">"));
        }

        /// <summary>
        /// Creates a backreference to a numbered capturing group.
        /// </summary>
        /// <param name="groupNumber">The group number (1-based).</param>
        /// <returns>Pattern object for chaining.</returns>
        /// <example>
        /// Pattern.With.Group(Pattern.With.Word.Repeat.OneOrMore)
        ///             .Whitespace
        ///             .Backreference(1)
        /// // Matches repeated words using group number
        /// </example>
        public Pattern Backreference(int groupNumber)
        {
            if (groupNumber < 1) throw new ArgumentOutOfRangeException(nameof(groupNumber), "Group numbers start at 1.");
            return Append(new RawToken(@"\" + groupNumber.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Creates a conditional pattern that matches based on whether a named group was matched.
        /// If the named group participated in the match, the truePattern is used; otherwise falsePattern.
        /// </summary>
        /// <param name="groupName">The name of the group to check.</param>
        /// <param name="truePattern">Pattern to match if the group was matched.</param>
        /// <param name="falsePattern">Pattern to match if the group was not matched.</param>
        /// <returns>Pattern object for chaining.</returns>
        /// <remarks>
        /// Produces regex: (?(groupName)truePattern|falsePattern)
        /// </remarks>
        /// <example>
        /// // Match optional prefix, then require suffix only if prefix was present
        /// Pattern.With
        ///     .NamedGroup("prefix", Pattern.With.Literal("Mr.")).Repeat.Optional
        ///     .Conditional("prefix", Pattern.With.Whitespace, Pattern.With.RegEx(""))
        /// </example>
        public Pattern Conditional(string groupName, Pattern truePattern, Pattern falsePattern)
        {
            return Append(new ConditionalToken(ValidateGroupName(groupName), Require(truePattern, nameof(truePattern)), falsePattern ?? With));
        }

        /// <summary>
        /// Creates a conditional pattern that matches based on whether a numbered group was matched.
        /// If the numbered group participated in the match, the truePattern is used; otherwise falsePattern.
        /// </summary>
        /// <param name="groupNumber">The group number to check (1-based).</param>
        /// <param name="truePattern">Pattern to match if the group was matched.</param>
        /// <param name="falsePattern">Pattern to match if the group was not matched.</param>
        /// <returns>Pattern object for chaining.</returns>
        /// <remarks>
        /// Produces regex: (?(groupNumber)truePattern|falsePattern)
        /// </remarks>
        /// <example>
        /// // Match optional opening paren, content, then closing paren only if opening was present
        /// Pattern.With
        ///     .Group(Pattern.With.Literal("(")).Repeat.Optional
        ///     .Word.Repeat.OneOrMore
        ///     .Conditional(1, Pattern.With.Literal(")"), Pattern.With.RegEx(""))
        /// </example>
        public Pattern Conditional(int groupNumber, Pattern truePattern, Pattern falsePattern)
        {
            if (groupNumber < 1) throw new ArgumentOutOfRangeException(nameof(groupNumber), "Group numbers start at 1.");
            return Append(new ConditionalToken(groupNumber.ToString(CultureInfo.InvariantCulture), Require(truePattern, nameof(truePattern)), falsePattern ?? With));
        }
    }
}
