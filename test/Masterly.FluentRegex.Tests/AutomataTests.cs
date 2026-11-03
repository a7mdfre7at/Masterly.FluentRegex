using System.Text.RegularExpressions;
using Masterly.FluentRegex.Automata;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Tests;

/// <summary>
/// Unit tests for the automata building blocks whose rules cannot be pinned precisely through
/// the public language-algebra API: the term simplifier, the comparers and the equivalence prover.
/// </summary>
public class AutomataTests
{
    private const int TooLargeTermDepth = 17;

    // ------------------------------------------------------------------
    // TermFactory simplification rules
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SimplifiedTerms))]
    public void TermFactory_Simplification_WritesExpectedRegex(TermCase rule, string expected)
    {
        TermFactory terms = new TermFactory();

        RegexTerm term = rule.Build(terms);

        new RegexTermWriter(RegexOptions.None).Write(term).ShouldBe(expected, rule.ToString());
    }

    public static IEnumerable<object[]> SimplifiedTerms() => new (string Rule, Func<TermFactory, RegexTerm> Build, string Expected)[]
    {
        ("empty class is nothing", t => t.Class(CharSet.Empty), @"[^\s\S]"),
        ("concat with nothing is nothing", t => t.Concat(C(t, 'a'), t.Nothing), @"[^\s\S]"),
        ("concat of no parts is empty", t => t.Concat(), string.Empty),
        ("concat of empties is empty", t => t.Concat(t.Empty, t.Empty), string.Empty),
        ("concat flattens nested concat", t => t.Concat(t.Concat(C(t, 'a'), C(t, 'b')), C(t, 'c')), "abc"),
        ("concat merges equal neighbours", t => t.Concat(C(t, 'a'), C(t, 'a')), "a{2}"),
        ("concat merges star and copy", t => t.Concat(t.Star(C(t, 'a')), C(t, 'a')), "a+"),
        ("concat adds bounded runs", t => t.Concat(t.Repeat(C(t, 'a'), 1, 2), t.Repeat(C(t, 'a'), 2, 3)), "a{3,5}"),
        ("sequence before its star", t => t.Concat(C(t, 'a'), C(t, 'b'), t.Star(AB(t))), "(?:ab)+"),
        ("sequence after its star", t => t.Concat(t.Star(AB(t)), C(t, 'a'), C(t, 'b')), "(?:ab)+"),
        ("sequence before bounded repeat", t => t.Concat(C(t, 'a'), C(t, 'b'), t.Repeat(AB(t), 0, 2)), "(?:ab){1,3}"),
        ("partial sequence before star", t => t.Concat(C(t, 'b'), t.Star(AB(t))), "b(?:ab)*"),
        ("different sequence before star", t => t.Concat(C(t, 'c'), C(t, 'b'), t.Star(AB(t))), "cb(?:ab)*"),
        ("alternation of nothing", t => t.Alternation(), @"[^\s\S]"),
        ("alternation of nothing only", t => t.Alternation(t.Nothing), @"[^\s\S]"),
        ("alternation of empty only", t => t.Alternation(t.Empty), string.Empty),
        ("alternation merges classes", t => t.Alternation(C(t, 'a'), C(t, 'b')), "[ab]"),
        ("alternation merges classes in place", t => t.Alternation(C(t, 'a'), AB(t), C(t, 'c')), "[ac]|ab"),
        ("alternation drops duplicates", t => t.Alternation(AB(t), AB(t)), "ab"),
        ("alternation flattens nested alternation", t => t.Alternation(t.Alternation(AB(t), CD(t)), C(t, 'x')), "ab|cd|x"),
        ("empty option makes optional", t => t.Alternation(t.Empty, C(t, 'a')), "a?"),
        ("empty option with nullable repeat", t => t.Alternation(t.Empty, t.Star(C(t, 'a'))), "a*"),
        ("empty option with nullable concat", t => t.Alternation(t.Empty, t.Concat(t.Star(C(t, 'a')), t.Star(C(t, 'b')))), "a*b*"),
        ("empty option with non-nullable concat", t => t.Alternation(t.Empty, t.Concat(C(t, 'a'), t.Star(C(t, 'b')))), "(?:ab*)?"),
        ("empty option with nullable alternation", t => t.Alternation(t.Empty, t.Star(AB(t)), CD(t)), "(?:ab)*|cd"),
        ("empty option with non-nullable alternation", t => t.Alternation(t.Empty, AB(t), CD(t)), "(?:ab|cd)?"),
        ("common prefix is factored", t => t.Alternation(AB(t), t.Concat(C(t, 'a'), C(t, 'c'))), "a[bc]"),
        ("prefix that is a whole option", t => t.Alternation(C(t, 'a'), AB(t)), "ab?"),
        ("prefix shared by some options", t => t.Alternation(AB(t), t.Concat(C(t, 'a'), C(t, 'c')), t.Concat(C(t, 'x'), C(t, 'y'))), "a[bc]|xy"),
        ("suffix that is a whole option", t => t.Alternation(C(t, 'b'), AB(t)), "a?b"),
        ("repeat of nothing allowing zero", t => t.Repeat(t.Nothing, 0, 3), string.Empty),
        ("repeat of nothing requiring one", t => t.Repeat(t.Nothing, 1, 3), @"[^\s\S]"),
        ("repeat of empty", t => t.Repeat(t.Empty, 2, 5), string.Empty),
        ("repeat at most zero times", t => t.Repeat(C(t, 'a'), 0, 0), string.Empty),
        ("repeat exactly once", t => t.Repeat(AB(t), 1, 1), "ab"),
        ("star of optional", t => t.Star(t.Repeat(C(t, 'a'), 0, 1)), "a*"),
        ("optional of plus", t => t.Repeat(t.Repeat(C(t, 'a'), 1, RegexTerm.Unbounded), 0, 1), "a*"),
        ("plus of plus", t => t.Repeat(t.Repeat(C(t, 'a'), 1, RegexTerm.Unbounded), 1, RegexTerm.Unbounded), "a+"),
        ("optional of optional", t => t.Repeat(t.Repeat(C(t, 'a'), 0, 1), 0, 1), "a?"),
        ("fixed of fixed", t => t.Repeat(t.Repeat(C(t, 'a'), 2, 2), 3, 3), "a{6}"),
        ("fixed of range", t => t.Repeat(t.Repeat(C(t, 'a'), 2, 3), 2, 2), "(?:a{2,3}){2}"),
        ("range of fixed", t => t.Repeat(t.Repeat(C(t, 'a'), 2, 2), 1, 2), "(?:a{2}){1,2}"),
        ("repeat of sequence", t => t.Repeat(AB(t), 2, 4), "(?:ab){2,4}"),
        ("repeat of alternation", t => t.Star(t.Alternation(AB(t), CD(t))), "(?:ab|cd)*"),
        ("concat with alternation", t => t.Concat(C(t, 'x'), t.Alternation(AB(t), CD(t))), "x(?:ab|cd)"),
    }.Select(x => new object[] { new TermCase(x.Rule, x.Build), x.Expected });

    [Fact]
    public void TermFactory_Class_SameSetTwice_ReturnsSameInstance()
    {
        TermFactory terms = new TermFactory();

        RegexTerm first = terms.Class(CharSet.Single('a'));
        RegexTerm second = terms.Class(CharSet.ForSource("[a]", RegexOptions.None));

        second.ShouldBeSameAs(first);
        first.Kind.ShouldBe(TermKind.Class);
    }

    [Fact]
    public void TermFactory_TermLargerThanLimit_ThrowsInvalidOperation()
    {
        TermFactory terms = new TermFactory();

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => Doubling(terms, TooLargeTermDepth));

        ex.Message.ShouldBe("The resulting language is too complex to express as a readable regular expression.");
    }

    [Fact]
    public void RegexTerm_ToString_StructurallyEqualTerms_ShareTheSameKey()
    {
        TermFactory terms = new TermFactory();
        RegexTerm first = terms.Concat(C(terms, 'a'), C(terms, 'b'));

        RegexTerm second = terms.Concat(C(terms, 'a'), C(terms, 'b'));
        RegexTerm reversed = terms.Concat(C(terms, 'b'), C(terms, 'a'));

        second.ToString().ShouldBe(first.ToString());
        reversed.ToString().ShouldNotBe(first.ToString());
        first.ToString().ShouldBe(first.Key);
    }

    [Theory]
    [MemberData(nameof(NullableTerms))]
    public void RegexTerm_IsNullable_ReturnsExpected(TermCase term, bool expected)
    {
        TermFactory terms = new TermFactory();

        term.Build(terms).IsNullable.ShouldBe(expected, term.ToString());
    }

    public static IEnumerable<object[]> NullableTerms() => new (string Description, Func<TermFactory, RegexTerm> Build, bool Expected)[]
    {
        ("nothing", t => t.Nothing, false),
        ("empty", t => t.Empty, true),
        ("class", t => C(t, 'a'), false),
        ("concat of stars", t => t.Concat(t.Star(C(t, 'a')), t.Star(C(t, 'b'))), true),
        ("concat with a class", t => t.Concat(t.Star(C(t, 'a')), C(t, 'b')), false),
        ("alternation with a star", t => t.Alternation(t.Star(AB(t)), CD(t)), true),
        ("alternation without empty", t => t.Alternation(AB(t), CD(t)), false),
        ("optional", t => t.Repeat(C(t, 'a'), 0, 1), true),
        ("plus of class", t => t.Repeat(C(t, 'a'), 1, RegexTerm.Unbounded), false),
    }.Select(x => new object[] { new TermCase(x.Description, x.Build), x.Expected });

    // ------------------------------------------------------------------
    // RegexTermWriter
    // ------------------------------------------------------------------

    [Fact]
    public void RegexTermWriter_WriteAnchored_OptionalFinalNewlineSuffix_WritesDollar()
    {
        TermFactory terms = new TermFactory();
        RegexTerm term = terms.Concat(AB(terms), terms.Repeat(C(terms, '\n'), 0, 1));

        string text = new RegexTermWriter(RegexOptions.None).WriteAnchored(term, terms);

        text.ShouldBe("^ab$");
    }

    [Fact]
    public void RegexTermWriter_WriteAnchored_UpToTwoFinalNewlines_KeepsExplicitEndAnchor()
    {
        TermFactory terms = new TermFactory();
        RegexTerm term = terms.Concat(AB(terms), terms.Repeat(C(terms, '\n'), 0, 2));

        string text = new RegexTermWriter(RegexOptions.None).WriteAnchored(term, terms);

        text.ShouldBe(@"^ab\n{0,2}\z");
    }

    [Fact]
    public void RegexTermWriter_WriteAnchored_MultilineOptionalFinalNewline_KeepsNewlineAndInputAnchors()
    {
        TermFactory terms = new TermFactory();
        RegexTerm term = terms.Concat(AB(terms), terms.Repeat(C(terms, '\n'), 0, 1));

        string text = new RegexTermWriter(RegexOptions.Multiline).WriteAnchored(term, terms);

        text.ShouldBe(@"\Aab\n?\z");
    }

    [Fact]
    public void RegexTermWriter_WriteAnchored_Alternation_GroupsBody()
    {
        TermFactory terms = new TermFactory();
        RegexTerm term = terms.Alternation(AB(terms), CD(terms));

        string text = new RegexTermWriter(RegexOptions.None).WriteAnchored(term, terms);

        text.ShouldBe(@"^(?:ab|cd)\z");
    }

    // ------------------------------------------------------------------
    // Comparers and readable characters
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(IntArrayPairs))]
    public void IntArrayComparer_Equals_ComparesContent(int[] left, int[] right, bool expected)
    {
        IntArrayComparer.Instance.Equals(left, right).ShouldBe(expected);
    }

    public static IEnumerable<object[]> IntArrayPairs() => new[]
    {
        new object[] { new[] { 1, 2 }, new[] { 1, 2 }, true },
        new object[] { new[] { 1, 2 }, new[] { 1, 3 }, false },
        new object[] { new[] { 1 }, new[] { 1, 2 }, false },
        new object[] { null, new[] { 1 }, false },
        new object[] { new[] { 1 }, null, false },
        new object[] { null, null, true },
    };

    [Fact]
    public void IntArrayComparer_GetHashCode_EqualContent_EqualHashes()
    {
        IntArrayComparer.Instance.GetHashCode(new[] { 3, 1, 2 })
            .ShouldBe(IntArrayComparer.Instance.GetHashCode(new[] { 3, 1, 2 }));
    }

    [Fact]
    public void CharSetComparer_Equals_SameContentDifferentInstances_ReturnsTrue()
    {
        CharSet left = CharSet.Single('a').Union(CharSet.Single('b'));
        CharSet right = CharSet.ForSource("[ab]", RegexOptions.None);

        CharSetComparer.Instance.Equals(left, right).ShouldBeTrue();
        CharSetComparer.Instance.GetHashCode(left).ShouldBe(CharSetComparer.Instance.GetHashCode(right));
    }

    [Fact]
    public void CharSetComparer_Equals_DifferentContent_ReturnsFalse()
    {
        CharSetComparer.Instance.Equals(CharSet.Single('a'), CharSet.Single('b')).ShouldBeFalse();
    }

    [Fact]
    public void CharSetComparer_Equals_NullLeft_ReturnsFalse()
    {
        CharSetComparer.Instance.Equals(null, CharSet.Single('a')).ShouldBeFalse();
    }

    [Fact]
    public void CharSetComparer_Equals_BothNull_ReturnsTrue()
    {
        CharSetComparer.Instance.Equals(null, null).ShouldBeTrue();
    }

    [Fact]
    public void ReadableChars_Pick_EmptySet_ThrowsInvalidOperation()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => ReadableChars.Pick(CharSet.Empty));

        ex.Message.ShouldBe("The character set is empty.");
    }

    [Theory]
    [InlineData('a', 'Q')]
    [InlineData('Q', '7')]
    [InlineData('7', '!')]
    [InlineData('~', '\u00E9')]
    [InlineData(' ', '\n')]
    [InlineData('\u00E9', '\uD800')]
    [InlineData('\uE000', '\uDFFF')]
    [InlineData('\uD7FF', '\uD800')]
    public void ReadableChars_Rank_MoreReadableCharacter_RanksLower(char moreReadable, char lessReadable)
    {
        ReadableChars.Rank(moreReadable).ShouldBeLessThan(ReadableChars.Rank(lessReadable));
    }

    [Theory]
    [InlineData('a', 'z')]
    [InlineData('A', 'Z')]
    [InlineData('0', '9')]
    [InlineData(' ', '~')]
    [InlineData('\n', '\u00E9')]
    [InlineData('\u007F', '\uE000')]
    [InlineData('\uD800', '\uDFFF')]
    public void ReadableChars_Rank_CharactersOfTheSameGroup_RankEqually(char first, char second)
    {
        ReadableChars.Rank(first).ShouldBe(ReadableChars.Rank(second));
    }

    // ------------------------------------------------------------------
    // EquivalenceProver
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("[a-c]+", "(?:[ab]|c)+", true)]
    [InlineData("[0-9]+", @"\d+", false)]
    [InlineData("[a-c]", "[ab]", false)]
    [InlineData("a|b", "[ab]", true)]
    [InlineData("a|b", "[ab]c", false)]
    public void EquivalenceProver_IsEquivalent_ReturnsExpected(string reference, string candidate, bool expected)
    {
        EquivalenceProver prover = new EquivalenceProver(RegexParser.Parse(reference));

        prover.IsEquivalent(RegexParser.Parse(candidate)).ShouldBe(expected);
    }

    [Fact]
    public void EquivalenceProver_NonRegularReference_ThrowsNotSupported()
    {
        Should.Throw<NotSupportedException>(() => new EquivalenceProver(RegexParser.Parse(@"(a)\1")));
    }

    // ------------------------------------------------------------------
    // Dfa, MintermPartition and PatternLanguage
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(DfaEmptiness))]
    public void Dfa_IsEmpty_ReturnsWhetherAnAcceptingStateIsReachable(
        int mintermCount, int[] transitions, bool[] accepting, bool expected)
    {
        Dfa dfa = new Dfa(mintermCount, transitions, accepting);

        dfa.IsEmpty().ShouldBe(expected);
    }

    public static IEnumerable<object[]> DfaEmptiness() => new[]
    {
        new object[] { 1, new[] { Dfa.Dead }, new[] { true }, false },
        new object[] { 1, new[] { 1, Dfa.Dead }, new[] { false, true }, false },
        new object[] { 2, new[] { 0, 1, 2, 1, Dfa.Dead, Dfa.Dead }, new[] { false, false, true }, false },
        new object[] { 1, new[] { Dfa.Dead, 1 }, new[] { false, true }, true },
        new object[] { 2, new[] { 0, 1, 1, 0 }, new[] { false, false }, true },
    };

    [Fact]
    public void MintermPartition_Build_EmptySetAmongOthers_IsIgnored()
    {
        CharSet letters = CharSet.ForSource("[ab]", RegexOptions.None);

        MintermPartition partition = MintermPartition.Build(new[] { CharSet.Empty, letters, CharSet.Single('a') });

        partition.Count.ShouldBe(2);
        partition.UnionOf(new[] { 0, 1 }).SetEquals(letters).ShouldBeTrue();
    }

    [Fact]
    public void MintermPartition_Build_OnlyEmptySets_HasNoMinterms()
    {
        MintermPartition partition = MintermPartition.Build(new[] { CharSet.Empty });

        partition.Count.ShouldBe(0);
    }

    [Fact]
    public void PatternLanguage_From_KeepsParsedTreeAndLeadingLookahead()
    {
        RegexTree tree = RegexParser.Parse("(?=a)ab");

        PatternLanguage language = PatternLanguage.From(tree);

        language.Tree.ShouldBeSameAs(tree);
        language.Constraints.Count.ShouldBe(1);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static RegexTerm C(TermFactory terms, char c) => terms.Class(CharSet.Single(c));

    private static RegexTerm AB(TermFactory terms) => terms.Concat(C(terms, 'a'), C(terms, 'b'));

    private static RegexTerm CD(TermFactory terms) => terms.Concat(C(terms, 'c'), C(terms, 'd'));

    /// <summary>Builds a term whose size roughly doubles with every level of depth.</summary>
    private static RegexTerm Doubling(TermFactory terms, int depth) =>
        depth == 0
            ? C(terms, 'a')
            : terms.Concat(
                terms.Alternation(Doubling(terms, depth - 1), C(terms, 'x')),
                terms.Alternation(Doubling(terms, depth - 1), C(terms, 'y')));

    /// <summary>A named recipe for building a term, shown by its name in test results.</summary>
    public sealed class TermCase
    {
        private readonly string _name;
        private readonly Func<TermFactory, RegexTerm> _build;

        internal TermCase(string name, Func<TermFactory, RegexTerm> build)
        {
            _name = name;
            _build = build;
        }

        internal RegexTerm Build(TermFactory terms) => _build(terms);

        /// <inheritdoc />
        public override string ToString() => _name;
    }
}
