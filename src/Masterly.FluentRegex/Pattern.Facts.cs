#nullable enable
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Automata;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex;

/// <summary>
/// Language facts: what every string the pattern accepts has in common, proven from the automaton.
/// </summary>
public partial class Pattern
{
    /// <summary>
    /// Computes facts that hold for every string this pattern accepts: the shortest and longest
    /// accepted lengths, the text every accepted string starts and ends with, and whether the empty
    /// string or nothing at all is accepted.
    /// </summary>
    /// <param name="scope">
    /// <see cref="MatchScope.FullString"/> for the strings matched entirely (as in language algebra), or
    /// <see cref="MatchScope.Substring"/> for the strings <see cref="IsMatch(string)"/> accepts, which is
    /// the language of <c>[\s\S]*?(?:pattern)[\s\S]*</c> minus the padding on an anchored side.
    /// </param>
    /// <returns>
    /// Facts computed from the minimal automaton when the pattern is regular (<see cref="LanguageFacts.IsExact"/>
    /// true), or a sound approximation read from the syntax tree otherwise. Never throws for a valid pattern.
    /// </returns>
    /// <example>
    /// LanguageFacts facts = Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z").GetLanguageFacts();
    /// facts.RequiredPrefix; // "INV-"
    /// facts.MinLength;      // 13
    /// facts.MaxLength;      // 13
    /// </example>
    public LanguageFacts GetLanguageFacts(MatchScope scope = MatchScope.FullString)
    {
        if (scope != MatchScope.FullString && scope != MatchScope.Substring)
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown match scope.");
        }

        RegexTree tree = ParseTree();
        try
        {
            return FactsExtractor.Compute(ScopeAnalysis.ScopedTree(tree, scope, _options), scope);
        }
        catch (NotSupportedException)
        {
            return SyntacticFacts.Compute(tree, scope, () => AcceptsEmptyString(scope));
        }
        catch (InvalidOperationException)
        {
            return SyntacticFacts.Compute(tree, scope, () => AcceptsEmptyString(scope));
        }
    }

    /// <summary>Asks the engine itself whether the empty string is accepted in the given scope.</summary>
    internal bool AcceptsEmptyString(MatchScope scope)
    {
        try
        {
            if (scope == MatchScope.Substring)
            {
                return IsMatch(string.Empty);
            }

            return new Regex($"\\A(?:{ToString()})\\z", _options, _timeout).IsMatch(string.Empty);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }
}
