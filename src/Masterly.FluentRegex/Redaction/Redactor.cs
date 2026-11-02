#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Redaction;

namespace Masterly.FluentRegex;

/// <summary>
/// Finds sensitive values such as e-mail addresses, card numbers or IBANs in free text and
/// replaces them, for logs, support tickets and analytics exports.
/// </summary>
/// <remarks>
/// <para>
/// Patterns written for whole-value validation, such as <see cref="CommonPatterns"/>, are found
/// inside text: anchors that wrap the whole pattern (<c>^...$</c>, <c>\A...\z</c>) are treated as
/// token boundaries, <c>(?&lt;!\w)...(?!\w)</c>. Rules of a <see cref="ConstrainedPattern"/>
/// filter the hits, so <c>CreditCard().Must(Validators.Luhn)</c> only redacts real card numbers.
/// </para>
/// <para>
/// When hits of different patterns overlap, the earliest start wins, then the longest hit, then
/// the pattern registered first. A redactor is immutable and thread-safe: <see cref="Add(Pattern, RedactionStyle?)"/>
/// returns a new redactor.
/// </para>
/// </remarks>
/// <example>
/// Redactor redactor = Redactor.For(CommonPatterns.Email())
///     .Add(CommonPatterns.CreditCard().Must(Validators.Luhn), RedactionStyle.KeepLast(4));
/// redactor.Mask("Card 4111 1111 1111 1111, mail bob@example.com");
/// // "Card **** **** **** 1111, mail ***@*******.***"
/// </example>
public sealed class Redactor
{
    private readonly Rule[] _rules;

    private Redactor(Rule[] rules)
    {
        _rules = rules;
    }

    /// <summary>Gets the number of registered patterns.</summary>
    public int PatternCount => _rules.Length;

    /// <summary>Creates a redactor that masks every value found by <paramref name="patterns"/> with <see cref="RedactionStyle.Mask"/>.</summary>
    /// <param name="patterns">The patterns of the sensitive values.</param>
    /// <returns>A new redactor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="patterns"/> or one of its elements is null.</exception>
    public static Redactor For(params Pattern[] patterns)
    {
        if (patterns == null)
        {
            throw new ArgumentNullException(nameof(patterns));
        }

        Redactor redactor = new Redactor(Array.Empty<Rule>());
        foreach (Pattern pattern in patterns)
        {
            if (pattern == null)
            {
                throw new ArgumentNullException(nameof(patterns), "The patterns must not contain null.");
            }

            redactor = redactor.Add(pattern);
        }

        return redactor;
    }

    /// <summary>Returns a redactor that also redacts the values found by <paramref name="pattern"/>.</summary>
    /// <param name="pattern">The pattern of the sensitive value.</param>
    /// <param name="style">The redaction style; <see cref="RedactionStyle.Mask"/> when null.</param>
    /// <returns>A new redactor. This redactor is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    public Redactor Add(Pattern pattern, RedactionStyle? style = null)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        return Append(new ConstrainedPattern(SearchPattern.For(pattern)), style);
    }

    /// <summary>
    /// Returns a redactor that also redacts the values found by <paramref name="pattern"/> that
    /// satisfy all of its rules.
    /// </summary>
    /// <param name="pattern">The constrained pattern of the sensitive value.</param>
    /// <param name="style">The redaction style; <see cref="RedactionStyle.Mask"/> when null.</param>
    /// <returns>A new redactor. This redactor is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    public Redactor Add(ConstrainedPattern pattern, RedactionStyle? style = null)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        return Append(pattern.WithPattern(SearchPattern.For(pattern.Pattern)), style);
    }

    /// <summary>Returns <paramref name="text"/> with every sensitive value redacted.</summary>
    /// <param name="text">The text to redact.</param>
    /// <returns>The redacted text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public string Mask(string text) => Redact(text).Text;

    /// <summary>Redacts <paramref name="text"/> and reports where values were redacted.</summary>
    /// <param name="text">The text to redact.</param>
    /// <returns>The redacted text and its spans. The report never contains the original values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public RedactionReport Redact(string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        List<Hit> selected = SelectNonOverlapping(CollectHits(text));
        return BuildReport(text, selected);
    }

    private Redactor Append(ConstrainedPattern searcher, RedactionStyle? style)
    {
        Rule[] rules = new Rule[_rules.Length + 1];
        Array.Copy(_rules, rules, _rules.Length);
        rules[_rules.Length] = new Rule(searcher, style ?? RedactionStyle.Mask);
        return new Redactor(rules);
    }

    private List<Hit> CollectHits(string text)
    {
        List<Hit> hits = new List<Hit>();
        for (int index = 0; index < _rules.Length; index++)
        {
            foreach (Match match in _rules[index].Searcher.Matches(text))
            {
                if (match.Length > 0)
                {
                    hits.Add(new Hit(match, index));
                }
            }
        }

        hits.Sort(CompareHits);
        return hits;
    }

    private static int CompareHits(Hit left, Hit right)
    {
        int byStart = left.Match.Index.CompareTo(right.Match.Index);
        if (byStart != 0)
        {
            return byStart;
        }

        int byLength = right.Match.Length.CompareTo(left.Match.Length);
        return byLength != 0 ? byLength : left.RuleIndex.CompareTo(right.RuleIndex);
    }

    private static List<Hit> SelectNonOverlapping(List<Hit> sortedHits)
    {
        List<Hit> selected = new List<Hit>();
        int covered = 0;
        foreach (Hit hit in sortedHits)
        {
            if (hit.Match.Index >= covered)
            {
                selected.Add(hit);
                covered = hit.Match.Index + hit.Match.Length;
            }
        }

        return selected;
    }

    private RedactionReport BuildReport(string text, List<Hit> selected)
    {
        StringBuilder sb = new StringBuilder(text.Length);
        List<RedactedSpan> spans = new List<RedactedSpan>(selected.Count);
        int copied = 0;
        foreach (Hit hit in selected)
        {
            Match match = hit.Match;
            RedactionStyle style = _rules[hit.RuleIndex].Style;
            sb.Append(text, copied, match.Index - copied);
            int redactedStart = sb.Length;
            sb.Append(style.Apply(match));
            spans.Add(new RedactedSpan(match.Index, match.Length, redactedStart, sb.Length - redactedStart, style, hit.RuleIndex));
            copied = match.Index + match.Length;
        }

        sb.Append(text, copied, text.Length - copied);
        return new RedactionReport(sb.ToString(), spans);
    }

    private sealed class Rule
    {
        public Rule(ConstrainedPattern searcher, RedactionStyle style)
        {
            Searcher = searcher;
            Style = style;
        }

        public ConstrainedPattern Searcher { get; }

        public RedactionStyle Style { get; }
    }

    private readonly struct Hit
    {
        public Hit(Match match, int ruleIndex)
        {
            Match = match;
            RuleIndex = ruleIndex;
        }

        public Match Match { get; }

        public int RuleIndex { get; }
    }
}
