using System.Text.RegularExpressions;
using Masterly.FluentRegex.Tests.Infrastructure;

namespace Masterly.FluentRegex.Tests;

public class CodeGenerationTests
{
    public static IEnumerable<object[]> RoundTripCorpus() => new[]
    {
        @"^\d{3}-\d{2}-\d{4}$",
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})",
        @"(cat|dog)s?",
        @"(?:x|y)*z{2,}",
        @"\bword\b",
        @"^(?=.*\d)(?=.*[a-z])(?!.*\s).{8,}$",
        @"(?<=\$)\d+(?:\.\d{2})?",
        @"(?>\d+)\d",
        @"(\w+)\s\1",
        @"(?<w>\w+)\s\k<w>",
        @"(\()?\d+(?(1)\))",
        @"(?i:hello) world",
        @"(?-i:X)y",
        @"[^\]\\\-]+",
        @"[a-z-[aeiou]]",
        @"[\p{L}\d_]+",
        @"\p{Lu}\P{Nd}\p{IsGreek}",
        @"\x41\t\n",
        @"a.+?b",
        @"\d{2,5}?",
        @"[0-9a-fA-F]{6}",
        @"[abc]|[^xyz]",
        @"\A\w+\z",
        @"(?<open>\()[^()]*(?<close-open>\))",
        @"#[a-z]+",
        @"[.]\.",
        @"""quoted""",
    }.Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(RoundTripCorpus))]
    public void GeneratedCode_Compiles_AndMatchesTheSameStrings(string regex)
    {
        Pattern original = Pattern.FromRegex(regex);
        string code = original.ToFluentCode();

        Pattern rebuilt = RoslynCompiler.Evaluate(code);
        var originalRegex = new Regex(regex);
        var rebuiltRegex = rebuilt.ToRegex();

        var samples = original.GenerateMatching(25, seed: 7)
            .Concat(original.GenerateNonMatching(25, seed: 7))
            .ToList();
        samples.ShouldNotBeEmpty();

        foreach (string sample in samples)
        {
            rebuiltRegex.IsMatch(sample).ShouldBe(originalRegex.IsMatch(sample),
                $"Sample '{sample}' disagrees.\nOriginal: {regex}\nRebuilt:  {rebuilt}\nCode:\n{code}");
        }
    }

    [Theory]
    [MemberData(nameof(RoundTripCorpus))]
    public void GeneratedCode_IsAFixpoint(string regex)
    {
        string code = Pattern.FromRegex(regex).ToFluentCode();
        Pattern rebuilt = RoslynCompiler.Evaluate(code);

        rebuilt.ToFluentCode().ShouldBe(code);
    }

    [Fact]
    public void ToFluentCode_ProducesReadableCode()
    {
        string code = Pattern.FromRegex(@"^(?<user>[\w.]+)@(?<host>[\w-]+\.\w{2,})$").ToFluentCode();

        code.ShouldBe(
            "Pattern.With\n" +
            "    .StartOfLine\n" +
            "    .NamedGroup(\"user\", Pattern.With.Set(Pattern.With.Word.Literal(\".\")).Repeat.OneOrMore)\n" +
            "    .Literal(\"@\")\n" +
            "    .NamedGroup(\n" +
            "        \"host\",\n" +
            "        Pattern.With\n" +
            "            .Set(Pattern.With.Word.Literal(\"-\")).Repeat.OneOrMore\n" +
            "            .Literal(\".\")\n" +
            "            .Word.Repeat.AtLeast(2))\n" +
            "    .EndOfLine");
    }

    [Fact]
    public void ToFluentCode_ShortPatternsStayOnOneLine()
    {
        Pattern.FromRegex(@"\d+").ToFluentCode().ShouldBe("Pattern.With.Digit.Repeat.OneOrMore");
    }

    [Fact]
    public void ToFluentCode_UsesFriendlyShorthands()
    {
        Pattern.FromRegex("[a-zA-Z]").ToFluentCode().ShouldBe("Pattern.With.Letter");
        Pattern.FromRegex("[0-9a-fA-F]").ToFluentCode().ShouldBe("Pattern.With.HexDigit");
        Pattern.FromRegex("[a-f]").ToFluentCode().ShouldBe("Pattern.With.Range('a', 'f')");
        Pattern.FromRegex("[aeiou]").ToFluentCode().ShouldBe("Pattern.With.AnyOf(\"aeiou\")");
        Pattern.FromRegex("[^aeiou]").ToFluentCode().ShouldBe("Pattern.With.NoneOf(\"aeiou\")");
        Pattern.FromRegex("(?:a|b)").ToFluentCode().ShouldBe("Pattern.With.OneOf(Pattern.With.Literal(\"a\"), Pattern.With.Literal(\"b\"))");
        Pattern.FromRegex("(a|b)").ToFluentCode().ShouldBe("Pattern.With.Choice(Pattern.With.Literal(\"a\"), Pattern.With.Literal(\"b\"))");
    }

    [Fact]
    public void ToFluentCode_IncludesOptionsAndTimeout()
    {
        string code = Pattern.FromRegex("abc", RegexOptions.IgnoreCase | RegexOptions.Multiline)
            .WithTimeout(TimeSpan.FromMilliseconds(250))
            .ToFluentCode();

        code.ShouldContain(".WithOptions(RegexOptions.IgnoreCase | RegexOptions.Multiline)");
        code.ShouldContain(".WithTimeout(TimeSpan.FromMilliseconds(250))");
    }

    [Fact]
    public void ToFluentCode_HandlesIgnorePatternWhitespace()
    {
        Pattern pattern = Pattern.FromRegex("\\d+  # digits\n - \\w", RegexOptions.IgnorePatternWhitespace);
        string code = pattern.ToFluentCode();

        Pattern rebuilt = RoslynCompiler.Evaluate(code);
        rebuilt.IsMatch("12-a").ShouldBeTrue();
        rebuilt.IsMatch("12 - a").ShouldBeFalse();
    }

    [Fact]
    public void FromRegex_RejectsInvalidRegex()
    {
        var ex = Should.Throw<ArgumentException>(() => Pattern.FromRegex("(abc"));
        ex.Message.ShouldContain("invalid");
    }

    [Fact]
    public void FromRegex_KeepsTextExactly()
    {
        Pattern.FromRegex(@"a{,3}").ToString().ShouldBe(@"a{,3}");
    }

    [Fact]
    public void FluentPatterns_CanBeConvertedToo()
    {
        Pattern pattern = CommonPatterns.IPv4();
        Pattern rebuilt = RoslynCompiler.Evaluate(pattern.ToFluentCode());
        rebuilt.ToString().ShouldBe(pattern.ToString());
    }
}
