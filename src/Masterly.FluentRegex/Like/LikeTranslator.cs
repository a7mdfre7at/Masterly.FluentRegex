#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Automata;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Like;

/// <summary>
/// Builds the best LIKE pattern for a <see cref="Pattern"/> and proves what it is. Each candidate (the
/// syntactic compilation, then the <c>prefix%suffix</c> pattern derived from the language facts, then
/// <c>%</c>) is only accepted when the algebra proves that the scoped pattern is contained in what the
/// LIKE pattern is guaranteed to accept, or, when the algebra cannot decide, when it is a superset by
/// construction. A candidate is exact when it is also proven equivalent and none of its <c>_</c> or
/// negated sets can meet a supplementary character. A case-insensitive collation is modeled twice: the
/// containment proof uses only the folds every case-insensitive collation performs
/// (<see cref="LikeCaseFolding"/>), the exactness proof gives the LIKE regex RegexOptions.IgnoreCase,
/// so a case-sensitive pattern with cased letters is never exact.
/// </summary>
internal static class LikeTranslator
{
    private const string NotProvenWarning =
        "The pattern is outside the regular subset, so the LIKE pattern is a superset by construction but could not be proven by the algebra.";
    private const string CodePointExactnessWarning =
        "'_' and negated sets match one database character, which in UTF-8 and supplementary-character-aware collations can be a whole surrogate pair, so the LIKE pattern also accepts strings the regex counts as longer.";
    private const string RejectedWarning =
        "The direct translation is not a proven superset under this collation, so a weaker LIKE pattern was used.";
    private const string PercentOnly = "%";

    /// <summary>Translates <paramref name="pattern"/>.</summary>
    public static LikeConversion Translate(Pattern pattern, LikeDialect dialect, MatchScope scope, LikeCollation collation)
    {
        RegexTree tree = pattern.ParseTree();
        RegexTree scoped = ScopeAnalysis.ScopedTree(tree, scope, pattern.Options);
        List<string> reasons = new List<string>();
        List<string> notes = new List<string>();

        Candidate syntactic = Candidate.Write(LikeCompiler.Compile(tree, scope, dialect, collation, reasons, notes), dialect, collation);
        Proof syntacticProof = Proof.Of(scoped, syntactic, scope);
        if (syntacticProof.IsExact)
        {
            return syntactic.ToConversion(dialect, scope, collation, isExact: true, notes);
        }

        LanguageFacts facts = syntacticProof.Facts ?? SyntacticFacts.Compute(tree, scope, () => pattern.AcceptsEmptyString(scope));
        Candidate fromFacts = Candidate.Write(FactsParts(facts), dialect, collation);
        Proof factsProof = Proof.Of(scoped, fromFacts, scope);
        if (factsProof.IsExact)
        {
            return fromFacts.ToConversion(dialect, scope, collation, isExact: true, Array.Empty<string>());
        }

        AddCodePointReason(reasons, syntacticProof, factsProof);
        Candidate chosen = Choose(syntactic, syntacticProof, fromFacts, factsProof, reasons, dialect, collation);
        reasons.AddRange(notes);
        return chosen.ToConversion(dialect, scope, collation, isExact: false, reasons);
    }

    /// <summary>
    /// Keeps only candidates that are not proven to miss a string of the pattern, then prefers the tighter
    /// one; <c>%</c> is the last resort and always a superset.
    /// </summary>
    private static Candidate Choose(Candidate syntactic, Proof syntacticProof, Candidate fromFacts, Proof factsProof, List<string> reasons, LikeDialect dialect, LikeCollation collation)
    {
        bool syntacticSafe = syntacticProof.Superset != false;
        bool factsSafe = factsProof.Superset != false;
        if (!syntacticSafe)
        {
            reasons.Add(RejectedWarning);
            return factsSafe ? fromFacts : Candidate.Write(new[] { LikePart.AnyString }, dialect, collation);
        }

        if (!factsSafe)
        {
            return syntactic;
        }

        bool provable = syntacticProof.Superset != null && factsProof.Superset != null;
        if (!provable)
        {
            reasons.Add(NotProvenWarning);
            return Fallback(syntactic, fromFacts);
        }

        return Tighter(syntactic, fromFacts);
    }

    private static void AddCodePointReason(List<string> reasons, Proof syntacticProof, Proof factsProof)
    {
        if (syntacticProof.IsEquivalent || factsProof.IsEquivalent)
        {
            reasons.Add(CodePointExactnessWarning);
        }
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

    /// <summary>What one shared automaton context proves about a candidate, and the facts of the scoped pattern.</summary>
    private sealed class Proof
    {
        private Proof(bool? superset, bool isEquivalent, bool isExact, LanguageFacts? facts)
        {
            Superset = superset;
            IsEquivalent = isEquivalent;
            IsExact = isExact;
            Facts = facts;
        }

        /// <summary>
        /// Whether the scoped pattern is contained in what the LIKE pattern is guaranteed to accept: true
        /// or false when proven, null when the algebra cannot decide.
        /// </summary>
        public bool? Superset { get; }

        /// <summary>Gets a value indicating whether the languages are proven equal in the UTF-16 code-unit model.</summary>
        public bool IsEquivalent { get; }

        /// <summary>Gets a value indicating whether the LIKE pattern is proven exact for every collation of the kind.</summary>
        public bool IsExact { get; }

        /// <summary>Exact facts of the scoped language, or null when unprovable.</summary>
        public LanguageFacts? Facts { get; }

        public static Proof Of(RegexTree scoped, Candidate like, MatchScope scope)
        {
            try
            {
                bool separateModels = !ReferenceEquals(like.Tree, like.GuaranteedTree);
                LanguageContext context = separateModels
                    ? new LanguageContext(scoped, like.GuaranteedTree, like.Tree)
                    : new LanguageContext(scoped, like.GuaranteedTree);
                LanguageFacts facts = FactsExtractor.Compute(context[0], context.Partition, scope);
                PatternRelation guaranteed = LanguageAlgebra.Compare(context, 0, 1, scoped, like.GuaranteedTree).Relation;
                PatternRelation modeled = separateModels ? LanguageAlgebra.Compare(context, 0, 2, scoped, like.Tree).Relation : guaranteed;
                bool superset = guaranteed == PatternRelation.Equivalent || guaranteed == PatternRelation.ProperSubset || facts.IsEmptyLanguage;
                bool equivalent = guaranteed == PatternRelation.Equivalent && modeled == PatternRelation.Equivalent;
                return new Proof(superset, equivalent, equivalent && !like.MatchesOneCharacter, facts);
            }
            catch (NotSupportedException)
            {
                return new Proof(null, false, false, null);
            }
            catch (InvalidOperationException)
            {
                return new Proof(null, false, false, null);
            }
        }
    }

    /// <summary>A LIKE pattern and its regex forms.</summary>
    private sealed class Candidate
    {
        private Candidate(string expression, char? escapeCharacter, string regexText, RegexTree tree, RegexTree guaranteedTree, bool matchesOneCharacter)
        {
            Expression = expression;
            EscapeCharacter = escapeCharacter;
            RegexText = regexText;
            Tree = tree;
            GuaranteedTree = guaranteedTree;
            MatchesOneCharacter = matchesOneCharacter;
        }

        public string Expression { get; }

        public char? EscapeCharacter { get; }

        /// <summary>The LIKE pattern as a .NET regex, case-insensitive under a case-insensitive collation.</summary>
        public string RegexText { get; }

        /// <summary>The parsed <see cref="RegexText"/>: everything the collation may accept, as .NET folds case.</summary>
        public RegexTree Tree { get; }

        /// <summary>What every collation of the kind accepts; the same tree as <see cref="Tree"/> under a binary collation.</summary>
        public RegexTree GuaranteedTree { get; }

        /// <summary>Gets a value indicating whether the pattern has a <c>_</c> or a set that can match a surrogate.</summary>
        public bool MatchesOneCharacter { get; }

        public static Candidate Write(IReadOnlyList<LikePart> parts, LikeDialect dialect, LikeCollation collation)
        {
            string expression = LikeWriter.Write(parts, dialect, collation, out char? escape);
            string regexText = LikeRegex.FromExpression(expression, escape, dialect);
            bool caseInsensitive = collation == LikeCollation.CaseInsensitive;
            RegexTree tree = RegexParser.Parse(regexText, caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None);
            RegexTree guaranteed = caseInsensitive
                ? RegexParser.Parse(LikeRegex.GuaranteedFromExpression(expression, escape, dialect), RegexOptions.None)
                : tree;
            bool oneCharacter = MatchesOneCharacterIn(LikeExpressionParser.Parse(expression, escape, dialect));
            return new Candidate(expression, escape, regexText, tree, guaranteed, oneCharacter);
        }

        public LikeConversion ToConversion(LikeDialect dialect, MatchScope scope, LikeCollation collation, bool isExact, IReadOnlyList<string> warnings) =>
            new LikeConversion(dialect, scope, collation, Expression, EscapeCharacter, isExact, warnings, RegexText);

        private static bool MatchesOneCharacterIn(IReadOnlyList<LikePart> parts)
        {
            foreach (LikePart part in parts)
            {
                if (part.CanMatchSurrogate)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
