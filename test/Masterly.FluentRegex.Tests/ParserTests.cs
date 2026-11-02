using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Tests;

public class ParserTests
{
    public static readonly string[] Corpus =
    {
        @"",
        @"abc",
        @"^\d{3}-\d{2}-\d{4}$",
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})",
        @"(a|b|c)+?",
        @"(?:x|y)*z{2,}",
        @"\bword\b\B",
        @"(?=.*\d)(?!.*\s)(?<=a)(?<!b)",
        @"(?>\d+)\d",
        @"(\w+)\s\1",
        @"(?<w>\w+)\s\k<w>",
        @"(?'w'\w+)\s\k'w'",
        @"(a)?(?(1)b|c)",
        @"[\--/]",
        @"(?(name)yes|no)",
        @"(?(?=\d)\d+|\w+)",
        @"(?i)abc(?-i)def",
        @"(?im:abc)",
        @"(?#a comment)x",
        @"[^\]\\\-]",
        @"[a-z-[aeiou]]",
        @"[\p{L}\d_]",
        @"\p{Lu}\P{IsGreek}",
        @"\x41\u0042\t\n\cA\0",
        @"a{,3}",
        @"a{3",
        @"{x}",
        @"[]a]",
        @"\A\z\Z\G",
        @"(?<open>\()(?<close-open>\))",
        @"((a)(?<n>b)(c))",
        @"a|",
        @"|a",
        @"(|)",
        @"[a\-z]",
        @"[\d-x]",
        @"x*?y+?z??w{1,2}?",
    };

    [Theory]
    [MemberData(nameof(CorpusData))]
    public void Writer_RoundTripsSourceExactly(string pattern)
    {
        RegexTree tree = RegexParser.Parse(pattern);
        RegexWriter.Write(tree.Root).ShouldBe(pattern);
    }

    [Theory]
    [MemberData(nameof(CorpusData))]
    public void GroupNumbers_MatchDotNet(string pattern)
    {
        RegexTree tree = RegexParser.Parse(pattern);
        var regex = new Regex(pattern);

        var expected = regex.GetGroupNumbers().Where(n => n > 0).OrderBy(n => n).ToArray();
        var actual = tree.Groups.Where(g => g.IsCapturing).Select(g => g.Number).Distinct().OrderBy(n => n).ToArray();
        actual.ShouldBe(expected);

        foreach (GroupNode group in tree.Groups.Where(g => g.Name != null && g.IsCapturing))
            regex.GroupNumberFromName(group.Name).ShouldBe(group.Number);
    }

    public static IEnumerable<object[]> CorpusData() => Corpus.Select(p => new object[] { p });

    [Theory]
    [InlineData(@"(abc")]
    [InlineData(@"abc)")]
    [InlineData(@"*a")]
    [InlineData(@"a**")]
    [InlineData(@"[abc")]
    [InlineData(@"\")]
    [InlineData(@"[z-a]")]
    [InlineData(@"a{5,2}")]
    [InlineData(@"(?<>a)")]
    [InlineData(@"\q")]
    [InlineData(@"(?(1)a|b|c)")]
    public void InvalidPatterns_AreRejected_LikeDotNet(string pattern)
    {
        Should.Throw<ArgumentException>(() => new Regex(pattern));
        Should.Throw<RegexSyntaxException>(() => RegexParser.Parse(pattern));
    }

    [Fact]
    public void Quantifier_IsParsed()
    {
        var q = RegexParser.Parse(@"\d{2,5}?").Root.ShouldBeOfType<QuantifierNode>();
        q.Min.ShouldBe(2);
        q.Max.ShouldBe(5);
        q.Lazy.ShouldBeTrue();
        q.Body.ShouldBeOfType<CharClassNode>().ClassKind.ShouldBe(ClassKind.Digit);
    }

    [Fact]
    public void IgnorePatternWhitespace_SkipsWhitespaceAndComments()
    {
        RegexTree tree = RegexParser.Parse("\\d + # digits\n - \\w", RegexOptions.IgnorePatternWhitespace);
        var items = RegexWriter.ItemsOf(tree.Root);
        items.Count.ShouldBe(3);
        RegexWriter.Write(tree.Root).ShouldBe(@"\d+-\w");
    }

    [Fact]
    public void ExplicitCapture_MakesUnnamedGroupsNonCapturing()
    {
        RegexTree tree = RegexParser.Parse(@"(a)(?<n>b)", RegexOptions.ExplicitCapture);
        tree.Groups.Count(g => g.IsCapturing).ShouldBe(1);
    }

    [Fact]
    public void InlineOptions_ApplyToFollowingNodes()
    {
        RegexTree tree = RegexParser.Parse(@"a(?i)b");
        var items = RegexWriter.ItemsOf(tree.Root);
        items[0].Options.HasFlag(RegexOptions.IgnoreCase).ShouldBeFalse();
        items[2].Options.HasFlag(RegexOptions.IgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void BracketClass_ParsesItems()
    {
        var node = RegexParser.Parse(@"[^a-f\d_]").Root.ShouldBeOfType<CharClassNode>();
        node.Bracket.Negated.ShouldBeTrue();
        node.Bracket.Items.Count.ShouldBe(3);
        node.Bracket.Items[0].IsRange.ShouldBeTrue();
        node.Bracket.Items[1].ShorthandKind.ShouldBe(ClassKind.Digit);
        node.Bracket.Items[2].Low.ShouldBe('_');
    }

    [Fact]
    public void CharSet_UsesRealEngineSemantics()
    {
        CharSet digits = CharSet.ForSource(@"\d", RegexOptions.None);
        digits.Contains('5').ShouldBeTrue();
        digits.Contains('٣').ShouldBeTrue(); // Arabic-Indic digit: \d is Unicode-aware in .NET
        digits.Contains('a').ShouldBeFalse();

        CharSet letters = CharSet.ForSource("[a-z]", RegexOptions.IgnoreCase);
        letters.Contains('Q').ShouldBeTrue();

        digits.Overlaps(CharSet.ForSource(@"\w", RegexOptions.None)).ShouldBeTrue();
        digits.Overlaps(CharSet.ForSource("[a-z]", RegexOptions.None)).ShouldBeFalse();
    }

    [Fact]
    public void SplitLastAtom()
    {
        RegexParser.TrySplitLastAtom(@"ab\d", out string prefix, out string last).ShouldBeTrue();
        prefix.ShouldBe("ab");
        last.ShouldBe(@"\d");
    }

    [Theory]
    [InlineData("a|bc", "a|b", "c")]
    [InlineData("(ab)", "", "(ab)")]
    [InlineData(@"x[a-z]", "x", "[a-z]")]
    public void TrySplitLastAtom_EndsWithAtom_SplitsBeforeFinalAtom(string text, string expectedPrefix, string expectedAtom)
    {
        bool split = RegexParser.TrySplitLastAtom(text, out string prefix, out string lastAtom);

        split.ShouldBeTrue();
        prefix.ShouldBe(expectedPrefix);
        lastAtom.ShouldBe(expectedAtom);
    }

    [Theory]
    [InlineData("ab+")]
    [InlineData("")]
    [InlineData("a|")]
    [InlineData("(")]
    public void TrySplitLastAtom_NoQuantifiableFinalAtom_ReturnsFalseAndWholeText(string text)
    {
        bool split = RegexParser.TrySplitLastAtom(text, out string prefix, out string lastAtom);

        split.ShouldBeFalse();
        prefix.ShouldBe(string.Empty);
        lastAtom.ShouldBe(text);
    }

    [Fact]
    public void Parse_NullPattern_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(() => RegexParser.Parse(null!));

        exception.ParamName.ShouldBe("pattern");
    }

    [Fact]
    public void TryParse_NullPattern_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(() => RegexParser.TryParse(null!, RegexOptions.None, out RegexTree _));

        exception.ParamName.ShouldBe("pattern");
    }

    [Fact]
    public void Parse_ClassEndsRightAfterSubtraction_ThrowsSyntaxExceptionAtEnd()
    {
        RegexSyntaxException exception = Should.Throw<RegexSyntaxException>(() => RegexParser.Parse("[a-[b]"));

        exception.Position.ShouldBe(6);
        Should.Throw<ArgumentException>(() => new Regex("[a-[b]"));
    }

    [Fact]
    public void TryParse_InvalidPattern_ReturnsFalseAndNullTree()
    {
        bool parsed = RegexParser.TryParse("(abc", RegexOptions.None, out RegexTree tree);

        parsed.ShouldBeFalse();
        tree.ShouldBeNull();
    }

    [Fact]
    public void TryParse_ValidPattern_ReturnsTrueAndTree()
    {
        bool parsed = RegexParser.TryParse("a+", RegexOptions.IgnoreCase, out RegexTree tree);

        parsed.ShouldBeTrue();
        tree.Pattern.ShouldBe("a+");
        tree.Options.ShouldBe(RegexOptions.IgnoreCase);
        tree.Root.ShouldBeOfType<QuantifierNode>();
    }

    [Theory]
    [InlineData("abc)", "Too many ')'", 3)]
    [InlineData("+", "Quantifier '+' following nothing", 0)]
    [InlineData("{2}", "Quantifier '{' following nothing", 0)]
    [InlineData("a*{2}", "Nested quantifier '{'", 2)]
    [InlineData("a{2}{3}", "Nested quantifier '{'", 4)]
    [InlineData("a+??", "Nested quantifier '?'", 3)]
    [InlineData("a{5,2}", "Illegal {x,y} with x > y", 1)]
    [InlineData(@"\", @"Illegal \ at end of pattern", 0)]
    [InlineData(@"\k", @"Malformed \k<...> named back reference", 0)]
    [InlineData(@"\kx", @"Malformed \k<...> named back reference", 0)]
    [InlineData(@"a\k<abc", @"Malformed \k<...> named back reference", 1)]
    [InlineData(@"\p", @"Malformed \p{X} character escape", 0)]
    [InlineData(@"\pL", @"Malformed \p{X} character escape", 0)]
    [InlineData(@"\p{L", @"Incomplete \p{X} character escape", 0)]
    [InlineData(@"\c", "Missing control character", 0)]
    [InlineData(@"\c1", "Unrecognized control character", 0)]
    [InlineData(@"\x4", "Insufficient hexadecimal digits", 0)]
    [InlineData(@"\xZZ", "Insufficient hexadecimal digits", 0)]
    [InlineData(@"\u12G4", "Insufficient hexadecimal digits", 0)]
    [InlineData(@"\q", @"Unrecognized escape sequence \q", 0)]
    [InlineData(@"[\8]", @"Unrecognized escape sequence \8", 1)]
    [InlineData("[", "Unterminated [] set", 0)]
    [InlineData("[^", "Unterminated [] set", 0)]
    [InlineData(@"[a\", @"Illegal \ at end of pattern", 2)]
    [InlineData("[a-[b]c]", "A subtraction must be the last element in a character class", 6)]
    [InlineData(@"[a-\d]", @"Cannot include class \d in character range", 1)]
    [InlineData("[z-a]", "[x-y] range in reverse order", 1)]
    [InlineData("(abc", "Not enough )'s", 0)]
    [InlineData("(?", "Unrecognized grouping construct", 0)]
    [InlineData("(?z)", "Unrecognized grouping construct", 0)]
    [InlineData("(?i", "Unrecognized grouping construct", 0)]
    [InlineData("(?i;x)", "Unrecognized grouping construct", 0)]
    [InlineData("(?#abc", "Unterminated (?#...) comment", 0)]
    [InlineData("(?<", "Invalid group name", 0)]
    [InlineData("(?<abc", "Invalid group name", 0)]
    [InlineData("(?<>a)", "Invalid group name", 0)]
    [InlineData("(?<a->x)", "Invalid group name", 0)]
    [InlineData("(?<a-b c>x)", "Invalid group name", 0)]
    [InlineData("(?<a b>x)", "Invalid group name: Group names must begin with a word character", 0)]
    [InlineData("(?(", "Not enough )'s", 2)]
    [InlineData("(?(a+", "Not enough )'s", 2)]
    [InlineData("(?(1)a", "Not enough )'s", 0)]
    [InlineData("(?(1)a|b|c)", "Too many | in (?()|)", 5)]
    public void Parse_InvalidPattern_ThrowsSyntaxExceptionWithMessageAndPosition(string pattern, string message, int position)
    {
        RegexSyntaxException exception = Should.Throw<RegexSyntaxException>(() => RegexParser.Parse(pattern));

        exception.Message.ShouldBe($"{message} (at position {position})");
        exception.Position.ShouldBe(position);
    }

    [Theory]
    [InlineData(@"\t", '\t')]
    [InlineData(@"\n", '\n')]
    [InlineData(@"\r", '\r')]
    [InlineData(@"\f", '\f')]
    [InlineData(@"\v", '\v')]
    [InlineData(@"\e", '\u001B')]
    [InlineData(@"\a", '\u0007')]
    [InlineData(@"\x41", 'A')]
    [InlineData(@"é", 'é')]
    [InlineData(@"\cA", '\u0001')]
    [InlineData(@"\ca", '\u0001')]
    [InlineData(@"\c@", '\u0000')]
    [InlineData(@"\c_", '\u001F')]
    [InlineData(@"\0", '\u0000')]
    [InlineData(@"\012", '\n')]
    [InlineData(@"\.", '.')]
    [InlineData("{", '{')]
    public void Parse_EscapedCharacter_ProducesLiteralWithValue(string pattern, char expected)
    {
        LiteralNode literal = RegexParser.Parse(pattern).Root.ShouldBeOfType<LiteralNode>();

        literal.Value.ShouldBe(expected);
        new Regex($"^{pattern}$").IsMatch(expected.ToString()).ShouldBeTrue();
    }

    [Fact]
    public void Parse_OctalEscapeReadsAtMostThreeDigits_LeavesRestAsLiterals()
    {
        SequenceNode sequence = RegexParser.Parse(@"\0123").Root.ShouldBeOfType<SequenceNode>();

        sequence.Items.Count.ShouldBe(2);
        sequence.Items[0].ShouldBeOfType<LiteralNode>().Value.ShouldBe('\n');
        sequence.Items[1].ShouldBeOfType<LiteralNode>().Value.ShouldBe('3');
    }

    [Theory]
    [InlineData(@"[\b]", '\b')]
    [InlineData(@"[\101]", 'A')]
    [InlineData(@"[\1]", '\u0001')]
    [InlineData(@"[\0]", '\u0000')]
    [InlineData(@"[\-]", '-')]
    [InlineData(@"[\x41]", 'A')]
    public void Parse_EscapeInsideClass_ProducesSingleCharacterItem(string pattern, char expected)
    {
        CharClassNode node = RegexParser.Parse(pattern).Root.ShouldBeOfType<CharClassNode>();

        ClassItem item = node.Bracket.Items.ShouldHaveSingleItem();
        item.IsSingleChar.ShouldBeTrue();
        item.Low.ShouldBe(expected);
        new Regex($"^{pattern}$").IsMatch(expected.ToString()).ShouldBeTrue();
    }

    [Fact]
    public void Parse_CategoryInsideClass_ProducesShorthandItemWithCategory()
    {
        CharClassNode node = RegexParser.Parse(@"[\P{Lu}x]").Root.ShouldBeOfType<CharClassNode>();

        node.Bracket.Items.Count.ShouldBe(2);
        node.Bracket.Items[0].ShorthandKind.ShouldBe(ClassKind.NotCategory);
        node.Bracket.Items[0].Category.ShouldBe("Lu");
        node.Bracket.Items[0].Source.ShouldBe(@"\P{Lu}");
        node.Bracket.Items[0].IsSingleChar.ShouldBeFalse();
    }

    [Fact]
    public void Parse_ClassSubtraction_RecordsSubtractedSet()
    {
        CharClassNode node = RegexParser.Parse("[a-z-[aeiou]]").Root.ShouldBeOfType<CharClassNode>();

        node.Bracket.Subtraction.ShouldNotBeNull();
        node.Bracket.Subtraction.Items.Count.ShouldBe(5);
        node.Bracket.Subtraction.Negated.ShouldBeFalse();
    }

    [Fact]
    public void Parse_EscapedHyphenBeforeDash_IsNotARangeStartLikeDotNet()
    {
        CharClassNode node = RegexParser.Parse(@"[\--/]").Root.ShouldBeOfType<CharClassNode>();

        node.Bracket.Items.Select(item => item.Low).ShouldBe(new[] { '-', '-', '/' });
        node.Bracket.Items.ShouldAllBe(item => item.IsSingleChar);
        Regex.IsMatch(".", @"^[\--/]$").ShouldBeFalse();
    }

    [Theory]
    [InlineData("a{2,x}", 6)]
    [InlineData("a{2,", 4)]
    [InlineData("a{x}", 4)]
    public void Parse_BraceThatIsNotAQuantifier_IsLiteralText(string pattern, int expectedItems)
    {
        SequenceNode sequence = RegexParser.Parse(pattern).Root.ShouldBeOfType<SequenceNode>();

        sequence.Items.Count.ShouldBe(expectedItems);
        sequence.Items.ShouldAllBe(item => item is LiteralNode);
        new Regex(pattern).IsMatch(pattern).ShouldBeTrue();
    }

    [Theory]
    [InlineData("a{3}", 3, 3)]
    [InlineData("a{2,}", 2, QuantifierNode.Unbounded)]
    [InlineData("a{0,4}", 0, 4)]
    [InlineData("a*", 0, QuantifierNode.Unbounded)]
    [InlineData("a+", 1, QuantifierNode.Unbounded)]
    [InlineData("a?", 0, 1)]
    public void Parse_Quantifier_RecordsBounds(string pattern, int expectedMin, int expectedMax)
    {
        QuantifierNode quantifier = RegexParser.Parse(pattern).Root.ShouldBeOfType<QuantifierNode>();

        quantifier.Min.ShouldBe(expectedMin);
        quantifier.Max.ShouldBe(expectedMax);
        quantifier.IsUnbounded.ShouldBe(expectedMax == QuantifierNode.Unbounded);
        quantifier.QuantifierText.ShouldBe(pattern.Substring(1));
    }

    [Theory]
    [InlineData("^*")]
    [InlineData(@"\b+")]
    public void Parse_QuantifiedAnchor_KeepsAnchorAsQuantifierBody(string pattern)
    {
        QuantifierNode quantifier = RegexParser.Parse(pattern).Root.ShouldBeOfType<QuantifierNode>();

        quantifier.Body.ShouldBeOfType<AnchorNode>();
        Should.NotThrow(() => new Regex(pattern));
    }

    [Theory]
    [InlineData(@"\k<1>", null, 1)]
    [InlineData(@"\k'name'", "name", 0)]
    [InlineData(@"\12", null, 12)]
    public void Parse_Backreference_RecordsNameOrNumber(string pattern, string? expectedName, int expectedNumber)
    {
        BackreferenceNode backreference = RegexParser.Parse(pattern).Root.ShouldBeOfType<BackreferenceNode>();

        backreference.Name.ShouldBe(expectedName);
        backreference.Number.ShouldBe(expectedNumber);
    }

    [Theory]
    [InlineData("(?<a‿b>x)", "a‿b")]
    [InlineData("(?<á>x)", "á")]
    [InlineData("(?'q_1'x)", "q_1")]
    public void Parse_NamedGroupWithWordLikeCharacters_AcceptsNameLikeDotNet(string pattern, string expectedName)
    {
        GroupNode group = RegexParser.Parse(pattern).Root.ShouldBeOfType<GroupNode>();

        group.GroupKind.ShouldBe(GroupKind.Named);
        group.Name.ShouldBe(expectedName);
        new Regex(pattern).GroupNumberFromName(expectedName).ShouldBe(group.Number);
    }

    [Fact]
    public void Parse_BalancingGroupWithName_CapturesAndRecordsPoppedGroup()
    {
        RegexTree tree = RegexParser.Parse("(?<open>a)(?<close-open>b)");

        GroupNode balancing = tree.Groups[1];
        balancing.GroupKind.ShouldBe(GroupKind.Balancing);
        balancing.Name.ShouldBe("close");
        balancing.BalancingName.ShouldBe("open");
        balancing.IsCapturing.ShouldBeTrue();
    }

    [Fact]
    public void Parse_BalancingGroupWithoutName_DoesNotCapture()
    {
        RegexTree tree = RegexParser.Parse("(?<open>a)(?<-open>b)");

        GroupNode balancing = tree.Groups[1];
        balancing.GroupKind.ShouldBe(GroupKind.Balancing);
        balancing.Name.ShouldBeNull();
        balancing.BalancingName.ShouldBe("open");
        balancing.IsCapturing.ShouldBeFalse();
        balancing.Number.ShouldBe(0);
    }

    [Fact]
    public void Parse_OptionsGroup_RecordsFlagsAndAppliesThemToBody()
    {
        GroupNode group = RegexParser.Parse("(?i-m:a)", RegexOptions.Multiline).Root.ShouldBeOfType<GroupNode>();

        group.GroupKind.ShouldBe(GroupKind.Options);
        group.OpenText.ShouldBe("(?i-m:");
        group.OptionsOn.ShouldBe("i");
        group.OptionsOff.ShouldBe("m");
        group.Options.ShouldBe(RegexOptions.Multiline);
        group.Body.Options.ShouldBe(RegexOptions.IgnoreCase);
    }

    [Fact]
    public void Parse_InlineOptions_RecordsFlagsOnNode()
    {
        InlineOptionsNode node = RegexParser.Parse("(?s-i)").Root.ShouldBeOfType<InlineOptionsNode>();

        node.OptionsOn.ShouldBe("s");
        node.OptionsOff.ShouldBe("i");
    }

    [Fact]
    public void Parse_InlineIgnoreWhitespaceOption_SkipsWhitespaceInRestOfPattern()
    {
        RegexTree tree = RegexParser.Parse("(?x) a b");

        SequenceNode sequence = tree.Root.ShouldBeOfType<SequenceNode>();
        sequence.Items.Count.ShouldBe(3);
        sequence.Items[1].ShouldBeOfType<LiteralNode>().Value.ShouldBe('a');
        sequence.Items[2].ShouldBeOfType<LiteralNode>().Value.ShouldBe('b');
    }

    [Fact]
    public void Parse_InlineExplicitCaptureOption_MakesFollowingGroupsNonCapturing()
    {
        RegexTree tree = RegexParser.Parse("(a)(?n)(b)");

        tree.Groups[0].IsCapturing.ShouldBeTrue();
        tree.Groups[1].IsCapturing.ShouldBeFalse();
        tree.Groups[1].GroupKind.ShouldBe(GroupKind.NonCapturing);
    }

    [Fact]
    public void Parse_ConditionalOnGroupName_HasNoConditionNode()
    {
        ConditionalNode conditional = RegexParser.Parse("(?(n)a|b)").Root.ShouldBeOfType<ConditionalNode>();

        conditional.GroupName.ShouldBe("n");
        conditional.Condition.ShouldBeNull();
        conditional.OpenText.ShouldBe("(?(n)");
        conditional.Children.Select(child => child.Source).ShouldBe(new[] { "a", "b" });
    }

    [Fact]
    public void Parse_ConditionalOnLookahead_KeepsConditionGroup()
    {
        ConditionalNode conditional = RegexParser.Parse("(?(?=x)a)").Root.ShouldBeOfType<ConditionalNode>();

        conditional.GroupName.ShouldBeNull();
        conditional.Condition.ShouldBeOfType<GroupNode>().GroupKind.ShouldBe(GroupKind.Lookahead);
        conditional.No.ShouldBeNull();
        conditional.Children.Select(child => child.Source).ShouldBe(new[] { "(?=x)", "a" });
    }

    [Fact]
    public void Parse_ConditionalOnExpression_ParsesExpressionAsCondition()
    {
        ConditionalNode conditional = RegexParser.Parse("(?(a+)b|c)").Root.ShouldBeOfType<ConditionalNode>();

        conditional.GroupName.ShouldBeNull();
        conditional.Condition.ShouldBeOfType<QuantifierNode>().Source.ShouldBe("a+");
        conditional.OpenText.ShouldBe("(?(a+)");
        conditional.Yes.Source.ShouldBe("b");
        conditional.No.Source.ShouldBe("c");
        conditional.Children.Select(child => child.Source).ShouldBe(new[] { "a+", "b", "c" });
    }

    [Theory]
    [InlineData("", nameof(RegexNodeKind.Empty))]
    [InlineData("ab", nameof(RegexNodeKind.Sequence))]
    [InlineData("a|b", nameof(RegexNodeKind.Alternation))]
    [InlineData("a", nameof(RegexNodeKind.Literal))]
    [InlineData(@"\d", nameof(RegexNodeKind.CharClass))]
    [InlineData("^", nameof(RegexNodeKind.Anchor))]
    [InlineData("(a)", nameof(RegexNodeKind.Group))]
    [InlineData("a+", nameof(RegexNodeKind.Quantifier))]
    [InlineData(@"\1", nameof(RegexNodeKind.Backreference))]
    [InlineData("(?(n)a)", nameof(RegexNodeKind.Conditional))]
    [InlineData("(?i)", nameof(RegexNodeKind.InlineOptions))]
    [InlineData("(?#c)", nameof(RegexNodeKind.Comment))]
    public void Kind_RootNode_ReportsNodeKind(string pattern, string expected)
    {
        RegexNode root = RegexParser.Parse(pattern).Root;

        root.Kind.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("a+", "Quantifier: a+")]
    [InlineData("", "Empty: ")]
    [InlineData("x|y", "Alternation: x|y")]
    public void ToString_Node_ShowsKindAndSource(string pattern, string expected)
    {
        RegexNode root = RegexParser.Parse(pattern).Root;

        string text = root.ToString();

        text.ShouldBe(expected);
    }

    [Fact]
    public void Node_Span_ReportsStartEndAndLength()
    {
        SequenceNode sequence = RegexParser.Parse("ab(cd)").Root.ShouldBeOfType<SequenceNode>();

        RegexNode group = sequence.Items[2];
        group.Start.ShouldBe(2);
        group.End.ShouldBe(6);
        group.Length.ShouldBe(4);
        group.Pattern.ShouldBe("ab(cd)");
        sequence.Items[0].Children.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(RegexOptions.None, "imnsx", "", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace)]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.Singleline, "", "is", RegexOptions.None)]
    [InlineData(RegexOptions.Multiline, "x", "m", RegexOptions.IgnorePatternWhitespace)]
    [InlineData(RegexOptions.Compiled, "q", "z", RegexOptions.Compiled)]
    public void ApplyFlags_InlineFlagLetters_SetsAndClearsOptions(RegexOptions options, string on, string off, RegexOptions expected)
    {
        RegexOptions result = RegexParser.ApplyFlags(options, on, off);

        result.ShouldBe(expected);
    }

    [Fact]
    public void CharSet_NullOther_IsTreatedAsEmptySet()
    {
        CharSet letters = CharSet.ForSource("[a-c]", RegexOptions.None);

        letters.Overlaps(null).ShouldBeFalse();
        letters.Union(null).SetEquals(letters).ShouldBeTrue();
        letters.Intersect(null).IsEmpty.ShouldBeTrue();
        letters.Except(null).SetEquals(letters).ShouldBeTrue();
        letters.SetEquals(null).ShouldBeFalse();
        letters.IsSubsetOf(null).ShouldBeFalse();
        CharSet.Empty.IsSubsetOf(null).ShouldBeTrue();
    }

    [Fact]
    public void CharSet_SetOperations_ComputeExpectedMembers()
    {
        CharSet abc = CharSet.ForSource("[a-c]", RegexOptions.None);
        CharSet bcd = CharSet.ForSource("[b-d]", RegexOptions.None);

        abc.Union(bcd).SetEquals(CharSet.ForSource("[a-d]", RegexOptions.None)).ShouldBeTrue();
        abc.Intersect(bcd).SetEquals(CharSet.ForSource("[bc]", RegexOptions.None)).ShouldBeTrue();
        abc.Except(bcd).SetEquals(CharSet.Single('a')).ShouldBeTrue();
        abc.IsSubsetOf(CharSet.ForSource("[a-z]", RegexOptions.None)).ShouldBeTrue();
        abc.IsSubsetOf(bcd).ShouldBeFalse();
    }

    [Fact]
    public void CharSet_SetEquals_SameCountDifferentMembers_ReturnsFalse()
    {
        CharSet a = CharSet.Single('a');
        CharSet b = CharSet.Single('b');

        a.SetEquals(b).ShouldBeFalse();
        a.SetEquals(CharSet.ForSource("[ab]", RegexOptions.None)).ShouldBeFalse();
        a.SetEquals(CharSet.ForSource("a", RegexOptions.None)).ShouldBeTrue();
    }

    [Fact]
    public void CharSet_ContentHash_EqualForSetsWithSameMembers()
    {
        CharSet listed = CharSet.ForSource("[abc]", RegexOptions.None);
        CharSet ranged = CharSet.ForSource("[a-c]", RegexOptions.None);

        listed.ShouldNotBeSameAs(ranged);
        listed.ContentHash().ShouldBe(ranged.ContentHash());
    }

    [Fact]
    public void CharSet_ContentHash_SameSizeDifferentMembers_Differs()
    {
        CharSet abc = CharSet.ForSource("[a-c]", RegexOptions.None);
        CharSet bcd = CharSet.ForSource("[b-d]", RegexOptions.None);

        int abcHash = abc.ContentHash();

        abcHash.ShouldNotBe(bcd.ContentHash());
    }

    [Fact]
    public void CharSet_AllAndComplement_CoverEveryCodeUnit()
    {
        CharSet notA = CharSet.Single('a').Complement();

        CharSet.All.Count.ShouldBe(0x10000);
        CharSet.Empty.IsEmpty.ShouldBeTrue();
        notA.Count.ShouldBe(0xFFFF);
        notA.Contains('a').ShouldBeFalse();
        notA.Contains('b').ShouldBeTrue();
    }

    [Fact]
    public void CharSet_Ranges_ReturnsMaximalRangesLowestFirst()
    {
        CharSet set = CharSet.ForSource("[x-za-c￾￿]", RegexOptions.None);

        KeyValuePair<char, char>[] ranges = set.Ranges().ToArray();

        ranges.ShouldBe(new[]
        {
            new KeyValuePair<char, char>('a', 'c'),
            new KeyValuePair<char, char>('x', 'z'),
            new KeyValuePair<char, char>('￾', '￿'),
        });
    }

    [Theory]
    [InlineData(0.5, 0, 'a')]
    [InlineData(0.95, 1, 'é')]
    public void CharSet_Pick_PrefersPrintableAsciiButSometimesPicksAnyMember(double roll, int index, char expected)
    {
        CharSet set = CharSet.ForSource("[aé]", RegexOptions.None);

        char picked = set.Pick(new FixedRandom(roll, index));

        picked.ShouldBe(expected);
    }

    [Fact]
    public void CharSet_Pick_NoPrintableAsciiMember_PicksFromAllMembers()
    {
        CharSet set = CharSet.Single('é');

        char picked = set.Pick(new FixedRandom(0.1, 0));

        picked.ShouldBe('é');
    }

    [Fact]
    public void CharSet_Pick_LoneSurrogateMember_IsNeverSampled()
    {
        CharSet set = CharSet.ForSource(@"[é\uD800]", RegexOptions.None);

        char picked = set.Pick(new LastMemberRandom());

        set.Count.ShouldBe(2);
        picked.ShouldBe('é');
    }

    [Fact]
    public void CharSet_Pick_EmptySet_ThrowsInvalidOperationException()
    {
        CharSet empty = CharSet.Single('a').Except(CharSet.Single('a'));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => empty.Pick(new FixedRandom(0.1, 0)));

        exception.Message.ShouldBe("The character set is empty.");
    }

    [Theory]
    [InlineData("[é-ëb]", 'b')]
    [InlineData("[é-ë]", 'é')]
    public void CharSet_First_PrefersPrintableAscii(string source, char expected)
    {
        CharSet set = CharSet.ForSource(source, RegexOptions.None);

        char first = set.First();

        first.ShouldBe(expected);
    }

    [Fact]
    public void CharSet_First_EmptySet_ThrowsInvalidOperationException()
    {
        CharSet empty = CharSet.Single('a').Except(CharSet.Single('a'));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => empty.First());

        exception.Message.ShouldBe("The character set is empty.");
    }

    [Fact]
    public void CharSet_PreferredMembers_ReturnsOnlyPrintableAscii()
    {
        CharSet set = CharSet.ForSource("[a-cé\t]", RegexOptions.None);

        char[] preferred = set.PreferredMembers().ToArray();

        preferred.ShouldBe(new[] { 'a', 'b', 'c' });
    }

    [Fact]
    public void CharSet_ForNode_LiteralUsesNodeOptions()
    {
        RegexNode literal = RegexParser.Parse("a", RegexOptions.IgnoreCase).Root;

        CharSet set = CharSet.ForNode(literal);

        set.Count.ShouldBe(2);
        set.Contains('a').ShouldBeTrue();
        set.Contains('A').ShouldBeTrue();
    }

    [Fact]
    public void CharSet_ForNode_CharacterClassComputesMembers()
    {
        RegexNode charClass = RegexParser.Parse("[x-z]").Root;

        CharSet set = CharSet.ForNode(charClass);

        set.SetEquals(CharSet.ForSource("[xyz]", RegexOptions.None)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("^")]
    [InlineData("(a)")]
    [InlineData("ab")]
    public void CharSet_ForNode_NotSingleCharacterConstruct_ReturnsNull(string pattern)
    {
        RegexNode node = RegexParser.Parse(pattern).Root;

        CharSet set = CharSet.ForNode(node);

        set.ShouldBeNull();
    }

    [Fact]
    public void CharSet_ForSource_InvalidSource_ReturnsEmptySet()
    {
        CharSet set = CharSet.ForSource("[", RegexOptions.None);

        set.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void CharSet_ForSource_IrrelevantOptions_ShareCachedInstance()
    {
        CharSet plain = CharSet.ForSource("[q]", RegexOptions.None);

        CharSet multiline = CharSet.ForSource("[q]", RegexOptions.Multiline | RegexOptions.ExplicitCapture);

        multiline.ShouldBeSameAs(plain);
    }

    private sealed class FixedRandom : Random
    {
        private readonly double _roll;
        private readonly int _index;

        public FixedRandom(double roll, int index)
        {
            _roll = roll;
            _index = index;
        }

        public override double NextDouble() => _roll;

        public override int Next(int maxValue) => _index;
    }

    /// <summary>Never takes the printable-ASCII shortcut and always picks the highest-indexed member.</summary>
    private sealed class LastMemberRandom : Random
    {
        public override double NextDouble() => 0.99;

        public override int Next(int maxValue) => maxValue - 1;
    }
}
