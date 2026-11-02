#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Validation;

namespace Masterly.FluentRegex;

/// <summary>
/// A <see cref="Masterly.FluentRegex.Pattern"/> plus semantic rules that a regular expression
/// cannot express, such as a Luhn checksum or a real calendar date. A match only counts when
/// every rule accepts it.
/// </summary>
/// <remarks>
/// Instances are immutable and thread-safe: <see cref="Must(Func{string, bool}, string?)"/>
/// returns a new instance and leaves this one unchanged.
/// </remarks>
/// <example>
/// ConstrainedPattern card = CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");
/// card.Check("4111 1111 1111 1111").IsValid; // true
/// </example>
public sealed class ConstrainedPattern
{
    private readonly PatternRule[] _rules;
    private Regex? _fullMatchRegex;
    private Pattern? _fullMatchPattern;

    /// <summary>
    /// Creates a constrained pattern without rules yet. Add rules with
    /// <see cref="Must(Func{string, bool}, string?)"/>.
    /// </summary>
    /// <param name="pattern">The pattern every match must satisfy first.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    public ConstrainedPattern(Pattern pattern)
        : this(pattern ?? throw new ArgumentNullException(nameof(pattern)), Array.Empty<PatternRule>())
    {
    }

    private ConstrainedPattern(Pattern pattern, PatternRule[] rules)
    {
        Pattern = pattern;
        _rules = rules;
    }

    /// <summary>Gets the underlying pattern.</summary>
    public Pattern Pattern { get; }

    /// <summary>Gets the descriptions of the rules, in the order they are evaluated.</summary>
    public IReadOnlyList<string> RuleDescriptions => Array.ConvertAll(_rules, rule => rule.Description);

    /// <summary>
    /// Returns a copy that additionally requires <paramref name="rule"/> to accept the whole match value.
    /// </summary>
    /// <param name="rule">A predicate over the matched text.</param>
    /// <param name="description">A description reported by <see cref="Check"/> when the rule fails.</param>
    /// <returns>A new constrained pattern.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is null.</exception>
    public ConstrainedPattern Must(Func<string, bool> rule, string? description = null)
    {
        if (rule == null)
        {
            throw new ArgumentNullException(nameof(rule));
        }

        return AddRule(null, rule, description);
    }

    /// <summary>
    /// Returns a copy that additionally requires <paramref name="rule"/> to accept the value of the
    /// named group. The rule is skipped when the group did not participate in the match.
    /// </summary>
    /// <param name="groupName">The name of a group of the pattern.</param>
    /// <param name="rule">A predicate over the group's captured text.</param>
    /// <param name="description">A description reported by <see cref="Check"/> when the rule fails.</param>
    /// <returns>A new constrained pattern.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="groupName"/> or <paramref name="rule"/> is null.</exception>
    /// <exception cref="ArgumentException">The pattern has no group named <paramref name="groupName"/>.</exception>
    public ConstrainedPattern Must(string groupName, Func<string, bool> rule, string? description = null)
    {
        if (groupName == null)
        {
            throw new ArgumentNullException(nameof(groupName));
        }

        if (rule == null)
        {
            throw new ArgumentNullException(nameof(rule));
        }

        if (Pattern.CachedRegex.GroupNumberFromName(groupName) < 0)
        {
            throw new ArgumentException($"The pattern has no group named '{groupName}'.", nameof(groupName));
        }

        return AddRule(groupName, rule, description);
    }

    /// <summary>Tests whether some match in <paramref name="input"/> satisfies every rule.</summary>
    /// <param name="input">The text to search.</param>
    /// <returns><c>true</c> when at least one match satisfies all rules.</returns>
    public bool IsMatch(string input) => Match(input) != null;

    /// <summary>Finds the first match in <paramref name="input"/> that satisfies every rule.</summary>
    /// <param name="input">The text to search.</param>
    /// <returns>The first accepted match, or null when there is none.</returns>
    public Match? Match(string input)
    {
        foreach (Match match in Matches(input))
        {
            return match;
        }

        return null;
    }

    /// <summary>
    /// Finds every match in <paramref name="input"/> that satisfies all rules, scanning lazily.
    /// </summary>
    /// <remarks>
    /// When a candidate fails a rule, the search resumes at the candidate's index + 1, so a valid
    /// candidate that overlaps a rejected one is still found. Accepted matches never overlap.
    /// For <see cref="RegexOptions.RightToLeft"/> patterns the regular matches are filtered instead.
    /// </remarks>
    /// <param name="input">The text to search.</param>
    /// <returns>The accepted matches, left to right.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public IEnumerable<Match> Matches(string input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        Regex regex = Pattern.CachedRegex;
        return regex.RightToLeft ? FilterMatches(regex, input) : ScanMatches(regex, input);
    }

    /// <summary>
    /// Tests whether the pattern matches the <em>entire</em> input and every rule accepts it.
    /// This is the cheap form of <see cref="Check"/>, without diagnostics.
    /// </summary>
    /// <param name="input">The complete value to validate.</param>
    /// <returns><c>true</c> when the whole input is a valid match.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public bool IsValid(string input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        Match match = FullMatch.Match(input);
        return match.Success && FirstFailedRule(match) == null;
    }

    /// <summary>
    /// Validates the <em>entire</em> input, like <c>RegularExpressionAttribute</c>, and explains a failure.
    /// </summary>
    /// <param name="input">The complete value to validate.</param>
    /// <returns>
    /// The verdict, the description of the first failed rule, or a <see cref="MatchTrace"/> that
    /// pinpoints where the pattern itself stopped matching.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public ConstraintResult Check(string input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        Match match = FullMatch.Match(input);
        if (!match.Success)
        {
            return ConstraintResult.PatternMismatch(FullMatchPattern.Trace(input));
        }

        PatternRule? failed = FirstFailedRule(match);
        return failed == null ? ConstraintResult.Valid : ConstraintResult.RuleFailure(failed.Description);
    }

    /// <summary>Returns a copy with the same rules over a different pattern with the same groups.</summary>
    internal ConstrainedPattern WithPattern(Pattern pattern) => new ConstrainedPattern(pattern, _rules);

    /// <summary>Tests whether a match satisfies every rule.</summary>
    internal bool Satisfies(Match match) => FirstFailedRule(match) == null;

    private Regex FullMatch => _fullMatchRegex ??= FullMatchRegex.Create(Pattern);

    private Pattern FullMatchPattern =>
        _fullMatchPattern ??= Pattern.FromRegex(FullMatchRegex.SourceFor(Pattern), Pattern.Options).WithTimeout(Pattern.Timeout);

    private ConstrainedPattern AddRule(string? groupName, Func<string, bool> rule, string? description)
    {
        PatternRule[] rules = new PatternRule[_rules.Length + 1];
        Array.Copy(_rules, rules, _rules.Length);
        rules[_rules.Length] = PatternRule.Create(groupName, rule, description, rules.Length);
        return new ConstrainedPattern(Pattern, rules);
    }

    private PatternRule? FirstFailedRule(Match match)
    {
        foreach (PatternRule rule in _rules)
        {
            if (!rule.IsSatisfiedBy(match))
            {
                return rule;
            }
        }

        return null;
    }

    private IEnumerable<Match> ScanMatches(Regex regex, string input)
    {
        int position = 0;
        while (position <= input.Length)
        {
            Match candidate = regex.Match(input, position);
            if (!candidate.Success)
            {
                yield break;
            }

            if (Satisfies(candidate))
            {
                yield return candidate;
                position = candidate.Length == 0 ? candidate.Index + 1 : candidate.Index + candidate.Length;
            }
            else
            {
                position = candidate.Index + 1;
            }
        }
    }

    private IEnumerable<Match> FilterMatches(Regex regex, string input)
    {
        foreach (Match candidate in regex.Matches(input))
        {
            if (Satisfies(candidate))
            {
                yield return candidate;
            }
        }
    }
}
