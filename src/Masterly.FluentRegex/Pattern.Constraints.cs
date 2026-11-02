#nullable enable
using System;

namespace Masterly.FluentRegex;

/// <summary>
/// Semantic rules: checks a regular expression cannot express, such as checksums and calendar dates.
/// </summary>
public partial class Pattern
{
    /// <summary>
    /// Returns a <see cref="ConstrainedPattern"/> whose matches must also satisfy <paramref name="rule"/>.
    /// </summary>
    /// <param name="rule">A predicate over the whole matched text, for example <see cref="Validators.Luhn"/>.</param>
    /// <param name="description">A description reported when the rule rejects a value.</param>
    /// <returns>A new constrained pattern. This pattern is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is null.</exception>
    /// <example>
    /// ConstrainedPattern card = CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");
    /// card.IsMatch("4111-1111-1111-1111"); // true
    /// card.IsMatch("4111-1111-1111-1112"); // false: well-formed, but the checksum is wrong
    /// </example>
    public ConstrainedPattern Must(Func<string, bool> rule, string? description = null) =>
        new ConstrainedPattern(this).Must(rule, description);

    /// <summary>
    /// Returns a <see cref="ConstrainedPattern"/> whose matches must also satisfy <paramref name="rule"/>
    /// for the value of the named group. The rule is skipped when the group did not participate.
    /// </summary>
    /// <param name="groupName">The name of a group of this pattern.</param>
    /// <param name="rule">A predicate over the group's captured text.</param>
    /// <param name="description">A description reported when the rule rejects a value.</param>
    /// <returns>A new constrained pattern. This pattern is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="groupName"/> or <paramref name="rule"/> is null.</exception>
    /// <exception cref="ArgumentException">The pattern has no group named <paramref name="groupName"/>.</exception>
    /// <example>
    /// ConstrainedPattern adult = Pattern.With.NamedGroup("age", Pattern.With.Digit.Repeat.Times(1, 3))
    ///     .Must("age", age => int.Parse(age) >= 18, "at least 18");
    /// </example>
    public ConstrainedPattern Must(string groupName, Func<string, bool> rule, string? description = null) =>
        new ConstrainedPattern(this).Must(groupName, rule, description);
}
