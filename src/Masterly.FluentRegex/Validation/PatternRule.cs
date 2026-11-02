#nullable enable
using System;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Validation;

/// <summary>
/// One semantic rule of a <see cref="ConstrainedPattern"/>: a predicate over the whole match
/// value or over the value of one named group.
/// </summary>
internal sealed class PatternRule
{
    private readonly Func<string, bool> _predicate;

    private PatternRule(string? groupName, Func<string, bool> predicate, string description)
    {
        GroupName = groupName;
        _predicate = predicate;
        Description = description;
    }

    /// <summary>Gets the group the rule inspects, or null for the whole match.</summary>
    public string? GroupName { get; }

    /// <summary>Gets the human-readable description reported when the rule fails.</summary>
    public string Description { get; }

    /// <summary>Creates a rule; <paramref name="ordinal"/> numbers rules from 1 for the default description.</summary>
    public static PatternRule Create(string? groupName, Func<string, bool> predicate, string? description, int ordinal)
    {
        string text = description is null || description.Trim().Length == 0
            ? DefaultDescription(groupName, ordinal)
            : description;
        return new PatternRule(groupName, predicate, text);
    }

    /// <summary>
    /// Evaluates the rule. A rule on a group that did not participate in the match is not evaluated,
    /// so optional parts of a pattern stay optional.
    /// </summary>
    public bool IsSatisfiedBy(Match match)
    {
        if (GroupName == null)
        {
            return _predicate(match.Value);
        }

        Group group = match.Groups[GroupName];
        return !group.Success || _predicate(group.Value);
    }

    private static string DefaultDescription(string? groupName, int ordinal) =>
        groupName == null ? $"rule {ordinal}" : $"rule {ordinal} on group '{groupName}'";
}
