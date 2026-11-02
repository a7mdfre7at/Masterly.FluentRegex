#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Automata;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Like;

/// <summary>
/// Builds the best LIKE pattern for a <see cref="Pattern"/> and proves what it is: the syntactic
/// compilation is checked for equivalence with the scoped language, and when it is not exact it
/// competes with the <c>prefix%suffix</c> pattern derived from the language facts; the tighter
/// proven superset wins. The scoped pattern's automaton is built once per proof context. A
/// case-insensitive collation is modeled by giving the LIKE regex RegexOptions.IgnoreCase while the
/// pattern keeps its own options, so a case-sensitive pattern with cased letters is never exact.
/// </summary>
internal static class LikeTranslator
{
    private const string NotProvenWarning =
        "The pattern is outside the regular subset, so the LIKE pattern is a superset by construction but could not be proven by the algebra.";
    private const string PercentOnly = "%";

    /// <summary>Translates <paramref name="pattern"/>.</summary>
    public static LikeConversion Translate(Pattern pattern, LikeDialect dialect, MatchScope scope, LikeCollation collation)
    {
        RegexTree tree = pattern.ParseTree();
        RegexTree scoped = ScopeAnalysis.ScopedTree(tree, scope, pattern.Options);
        List<string> reasons = new List<string>();
        List<string> notes = new List<string>();

        Candidate syntactic = Candidate.Write(LikeCompiler.Compile(tree, scope, dialect, collation, reasons, notes), dialect, collation);
        Proof syntacticProof = Proof.Of(scoped, syntactic.Tree, scope);
        if (syntacticProof.Relation == PatternRelation.Equivalent)
        {
            return syntactic.ToConversion(dialect, scope, collation, isExact: true, notes);
        }

        LanguageFacts facts = syntacticProof.Facts ?? SyntacticFacts.Compute(tree, scope, () => pattern.AcceptsEmptyString(scope));
        Candidate fromFacts = Candidate.Write(FactsParts(facts), dialect, collation);
        Proof factsProof = Proof.Of(scoped, fromFacts.Tree, scope);
        if (factsProof.Relation == PatternRelation.Equivalent)
        {
            return fromFacts.ToConversion(dialect, scope, collation, isExact: true, Array.Empty<string>());
        }

        bool provable = syntacticProof.Relation != null && factsProof.Relation != null;
        Candidate chosen = provable ? Tighter(syntactic, fromFacts) : Fallback(syntactic, fromFacts);
        if (!provable)
        {
            reasons.Add(NotProvenWarning);
        }

        reasons.AddRange(notes);
        return chosen.ToConversion(dialect, scope, collation, isExact: false, reasons);
    }

    /// <summary>The <c>prefix%suffix</c> pattern the language facts justify.</summary>
    private static IReadOnlyList<LikePart> FactsParts(LanguageFacts facts)
    {
        if (facts.IsEmptyLanguage)
        {
            return new[] { LikePart.AnyString };
        }

        List<LikePart> parts = new List<LikePart>();
        string prefix = facts.RequiredPrefix;
        string suffix = facts.RequiredSuffix;
        if (prefix.Length > 0)
        {
            parts.Add(LikePart.OfText(prefix));
        }

        if (facts.MaxLength == prefix.Length)
        {
            return parts.Count == 0 ? new[] { LikePart.OfText(string.Empty) } : parts;
        }

        parts.Add(LikePart.AnyString);
        if (suffix.Length > 0 && facts.MinLength >= prefix.Length + suffix.Length)
        {
            parts.Add(LikePart.OfText(suffix));
        }

        return parts;
    }

    /// <summary>Prefers the facts candidate only when its language is proven strictly smaller.</summary>
    private static Candidate Tighter(Candidate syntactic, Candidate fromFacts)
    {
        PatternComparison? order = TryCompare(fromFacts.Tree, syntactic.Tree);
        bool factsTighter = order != null && order.Relation == PatternRelation.ProperSubset;
        return factsTighter ? fromFacts : syntactic;
    }

    /// <summary>Without proofs, both candidates are supersets by construction; prefer the one that says something.</summary>
    private static Candidate Fallback(Candidate syntactic, Candidate fromFacts) =>
        syntactic.Expression == PercentOnly ? fromFacts : syntactic;

    private static PatternComparison? TryCompare(RegexTree left, RegexTree right)
    {
        try
        {
            return LanguageAlgebra.Compare(left, right);
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>What one shared automaton context proves: the relation to a LIKE regex, and the facts.</summary>
    private sealed class Proof
    {
        private Proof(PatternRelation? relation, LanguageFacts? facts)
        {
            Relation = relation;
            Facts = facts;
        }

        /// <summary>The relation of the scoped pattern (left) to the LIKE language (right), or null when unprovable.</summary>
        public PatternRelation? Relation { get; }

        /// <summary>Exact facts of the scoped language, or null when unprovable.</summary>
        public LanguageFacts? Facts { get; }

        public static Proof Of(RegexTree scoped, RegexTree like, MatchScope scope)
        {
            try
            {
                LanguageContext context = new LanguageContext(scoped, like);
                PatternRelation relation = LanguageAlgebra.Compare(context, 0, 1, scoped, like).Relation;
                return new Proof(relation, FactsExtractor.Compute(context[0], context.Partition, scope));
            }
            catch (NotSupportedException)
            {
                return new Proof(null, null);
            }
            catch (InvalidOperationException)
            {
                return new Proof(null, null);
            }
        }
    }

    /// <summary>A LIKE pattern and its regex form.</summary>
    private sealed class Candidate
    {
        private Candidate(string expression, char? escapeCharacter, string regexText, RegexTree tree)
        {
            Expression = expression;
            EscapeCharacter = escapeCharacter;
            RegexText = regexText;
            Tree = tree;
        }

        public string Expression { get; }

        public char? EscapeCharacter { get; }

        public string RegexText { get; }

        public RegexTree Tree { get; }

        public static Candidate Write(IReadOnlyList<LikePart> parts, LikeDialect dialect, LikeCollation collation)
        {
            string expression = LikeWriter.Write(parts, dialect, collation == LikeCollation.Binary, out char? escape);
            string regexText = LikeRegex.FromExpression(expression, escape, dialect);
            RegexOptions likeOptions = collation == LikeCollation.CaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None;
            return new Candidate(expression, escape, regexText, RegexParser.Parse(regexText, likeOptions));
        }

        public LikeConversion ToConversion(LikeDialect dialect, MatchScope scope, LikeCollation collation, bool isExact, IReadOnlyList<string> warnings) =>
            new LikeConversion(dialect, scope, collation, Expression, EscapeCharacter, isExact, warnings, RegexText);
    }
}
