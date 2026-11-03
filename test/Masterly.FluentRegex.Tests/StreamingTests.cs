using System.Text;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class StreamingTests
{
    private const int SeedsPerPattern = 20;
    private const int RandomInputLength = 400;

    private static readonly Dictionary<string, string[]> TokensByPattern = new Dictionary<string, string[]>
    {
        [@"\d+"] = new[] { "1", "22", "a", " ", "\n", "x9" },
        [@"\bfoo\b"] = new[] { "foo", "foox", "xfoo", " ", "\n", "f", "o", "_" },
        [@"^abc"] = new[] { "abc", "\n", "x", "ab", "c", "\r\n" },
        [@"(?<=x)y"] = new[] { "x", "y", "xy", "z", "yy" },
        [@"a*"] = new[] { "a", "aa", "b", "\n" },
        [@"(?<key>[a-z]+)=(?<value>\d+)?(;)?"] = new[] { "k", "=", "1", "22", ";", " ", "ab" },
        [@"\w+$"] = new[] { "ab", "\n", " ", "c", "\r" },
        [@"(?:ab)+?c|b"] = new[] { "ab", "c", "b", "a" },
    };

    private static readonly StreamMatchOptions TinyBuffers = new StreamMatchOptions
    {
        MaxMatchLength = 64,
        LookbehindContext = 8,
        BufferSize = 8
    };

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    public static TheoryData<string, RegexOptions, int> EquivalenceCases()
    {
        TheoryData<string, RegexOptions, int> cases = new TheoryData<string, RegexOptions, int>();
        foreach (string pattern in TokensByPattern.Keys)
        {
            bool lineAnchored = pattern.StartsWith("^", StringComparison.Ordinal) ||
                                pattern.EndsWith("$", StringComparison.Ordinal);
            RegexOptions options = lineAnchored ? RegexOptions.Multiline : RegexOptions.None;
            for (int seed = 0; seed < SeedsPerPattern; seed++)
            {
                cases.Add(pattern, options, seed);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(EquivalenceCases))]
    public void Matches_TinyReadsAndSmallBuffer_EqualWholeTextMatches(string regex, RegexOptions options, int seed)
    {
        Pattern pattern = Pattern.FromRegex(regex, options);
        string text = RandomText(TokensByPattern[regex], seed);
        using TinyReader reader = new TinyReader(text, seed);

        string[] streamed = pattern.Matches(reader, TinyBuffers).Select(Signature).ToArray();

        streamed.ShouldBe(WholeTextSignatures(pattern, text));
    }

    [Theory]
    [MemberData(nameof(EquivalenceCases))]
    public async Task MatchesAsync_TinyReadsAndSmallBuffer_EqualWholeTextMatches(
        string regex,
        RegexOptions options,
        int seed)
    {
        Pattern pattern = Pattern.FromRegex(regex, options);
        string text = RandomText(TokensByPattern[regex], seed);
        using TinyReader reader = new TinyReader(text, seed);

        List<StreamMatch> streamed = await CollectAsync(pattern.MatchesAsync(reader, TinyBuffers, TestToken));

        streamed.Select(Signature).ToArray().ShouldBe(WholeTextSignatures(pattern, text));
    }

    [Fact]
    public void Matches_LargeInputWithDefaultOptions_EqualWholeTextMatches()
    {
        Pattern pattern = Pattern.FromRegex(@"\b\d{2,5}\b");
        string text = RandomText(new[] { "12", "345", "6789", " ", "x", "\n", "00000" }, seed: 99, length: 300_000);
        using StringReader reader = new StringReader(text);

        string[] streamed = pattern.Matches(reader).Select(Signature).ToArray();

        streamed.ShouldBe(WholeTextSignatures(pattern, text));
    }

    [Fact]
    public void Matches_IndexesCountFromStartOfWholeInput()
    {
        Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;
        string text = new string('x', 100_000) + "42" + new string('y', 70_000) + "7";
        using StringReader reader = new StringReader(text);

        StreamMatch[] matches = pattern.Matches(reader).ToArray();

        matches.Select(m => m.Index).ShouldBe(new long[] { 100_000, 170_002 });
        matches.Select(m => m.Value).ShouldBe(new[] { "42", "7" });
        matches.Select(m => m.Length).ShouldBe(new[] { 2, 1 });
    }

    [Theory]
    [InlineData("a*", 1)]
    [InlineData(@"\d+", 0)]
    [InlineData("^", 1)]
    public void Matches_EmptyInput_MatchesLikeWholeText(string regex, int expectedCount)
    {
        using StringReader reader = new StringReader(string.Empty);

        StreamMatch[] matches = Pattern.FromRegex(regex).Matches(reader).ToArray();

        matches.Length.ShouldBe(expectedCount);
    }

    [Fact]
    public void Matches_Groups_ContainOnlyParticipatingNamedAndNumberedGroups()
    {
        Pattern pattern = Pattern.FromRegex(@"(?<key>[a-z]+)=(?<value>\d+)?(;)?");
        using StringReader reader = new StringReader("a=1; b=");

        StreamMatch[] matches = pattern.Matches(reader).ToArray();

        matches[0].Groups.OrderBy(g => g.Key, StringComparer.Ordinal).ShouldBe(new Dictionary<string, string>
        {
            ["1"] = ";",
            ["key"] = "a",
            ["value"] = "1"
        });
        matches[1].Groups.ShouldBe(new Dictionary<string, string> { ["key"] = "b" });
        matches[1].Groups.ContainsKey("0").ShouldBeFalse();
    }

    [Fact]
    public void Matches_MatchLongerThanMaxMatchLength_ThrowsInsteadOfTruncating()
    {
        string text = new string('1', 100) + " tail " + new string('x', 200);
        StreamMatchOptions options = new StreamMatchOptions { MaxMatchLength = 10, BufferSize = 16 };
        using StringReader reader = new StringReader(text);

        InvalidOperationException error = Should.Throw<InvalidOperationException>(
            () => Pattern.With.Digit.Repeat.OneOrMore.Matches(reader, options).ToList());

        error.Message.ShouldContain("MaxMatchLength");
    }

    [Fact]
    public void Matches_LongMatchAtEndOfInput_IsEmittedExactly()
    {
        string text = "x" + new string('1', 100);
        StreamMatchOptions options = new StreamMatchOptions { MaxMatchLength = 10, BufferSize = 1000 };
        using StringReader reader = new StringReader(text);

        StreamMatch match = Pattern.With.Digit.Repeat.OneOrMore.Matches(reader, options).Single();

        match.Index.ShouldBe(1);
        match.Length.ShouldBe(100);
    }

    [Fact]
    public void Matches_IsLazy_ReadsNothingUntilEnumerated()
    {
        using TinyReader reader = new TinyReader("123", seed: 1);

        IEnumerable<StreamMatch> matches = Pattern.With.Digit.Matches(reader);

        reader.ReadCalls.ShouldBe(0);
        matches.Count().ShouldBe(3);
        reader.ReadCalls.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Matches_PatternTimeout_IsHonored()
    {
        Pattern pattern = Pattern.FromRegex(@"(a+)+$").WithTimeout(TimeSpan.FromMilliseconds(50));
        using StringReader reader = new StringReader(new string('a', 40) + "!");

        Should.Throw<RegexMatchTimeoutException>(() => pattern.Matches(reader).ToList());
    }

    [Fact]
    public async Task MatchesAsync_Utf8Stream_ReportsCharacterIndexesAndLeavesStreamOpen()
    {
        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("héllo 12 wörld 345"));

        Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

        List<StreamMatch> matches = await CollectAsync(digits.MatchesAsync(stream, cancellationToken: TestToken));

        matches.Select(m => m.Value).ShouldBe(new[] { "12", "345" });
        matches.Select(m => m.Index).ShouldBe(new long[] { 6, 15 });
        stream.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task MatchesAsync_StreamWithExplicitEncoding_DecodesWithIt()
    {
        using MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes("id=7, id=88"));

        Pattern ids = Pattern.FromRegex(@"id=(?<n>\d+)");

        List<StreamMatch> matches = await CollectAsync(ids.MatchesAsync(stream, Encoding.Unicode, null, TestToken));

        matches.Select(m => m.Groups["n"]).ShouldBe(new[] { "7", "88" });
    }

    [Fact]
    public async Task MatchesAsync_CanceledToken_ThrowsOperationCanceled()
    {
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using StringReader reader = new StringReader("1 2 3");

        await Should.ThrowAsync<OperationCanceledException>(
            () => CollectAsync(Pattern.With.Digit.MatchesAsync(reader, cancellationToken: cancellation.Token)));
    }

    [Fact]
    public async Task MatchesAsync_CanceledDuringEnumeration_StopsWithOperationCanceled()
    {
        using StringReader reader = new StringReader("1 2 3 4 5");

        await Should.ThrowAsync<OperationCanceledException>(
            () => ConsumeAndCancelAfterFirstMatchAsync(Pattern.With.Digit.MatchesAsync(reader)));
    }

    [Fact]
    public void Matches_NullReader_ThrowsImmediately()
    {
        Should.Throw<ArgumentNullException>(() => Pattern.With.Digit.Matches((TextReader)null!));
    }

    [Fact]
    public void MatchesAsync_NullReader_ThrowsImmediately()
    {
        Should.Throw<ArgumentNullException>(() => Pattern.With.Digit.MatchesAsync((TextReader)null!));
    }

    [Fact]
    public void MatchesAsync_NullStream_ThrowsImmediately()
    {
        Should.Throw<ArgumentNullException>(() => Pattern.With.Digit.MatchesAsync((Stream)null!));
    }

    [Fact]
    public void MatchesAsync_UnreadableStream_ThrowsArgumentException()
    {
        MemoryStream stream = new MemoryStream();
        stream.Dispose();

        Should.Throw<ArgumentException>(() => Pattern.With.Digit.MatchesAsync(stream));
    }

    [Fact]
    public void Matches_RightToLeftPattern_ThrowsNotSupported()
    {
        using StringReader reader = new StringReader("123");

        NotSupportedException error = Should.Throw<NotSupportedException>(
            () => Pattern.FromRegex(@"\d", RegexOptions.RightToLeft).Matches(reader));

        error.Message.ShouldContain("RightToLeft");
    }

    [Fact]
    public void Matches_ContiguousMatchAnchor_ThrowsNotSupported()
    {
        using StringReader reader = new StringReader("123");

        NotSupportedException error = Should.Throw<NotSupportedException>(
            () => Pattern.FromRegex(@"\G\d").Matches(reader));

        error.Message.ShouldContain(@"\G");
    }

    [Fact]
    public void Matches_UppercaseInlineOptionLetter_StreamsTheMatches()
    {
        using StringReader reader = new StringReader("ab CD e");

        string[] values = Pattern.FromRegex("(?I)[a-z]{2}").Matches(reader).Select(match => match.Value).ToArray();

        values.ShouldBe(new[] { "ab", "CD" });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void StreamMatchOptions_NonPositiveValues_Throw(int value)
    {
        StreamMatchOptions options = new StreamMatchOptions();

        Should.Throw<ArgumentOutOfRangeException>(() => options.MaxMatchLength = value);
        Should.Throw<ArgumentOutOfRangeException>(() => options.LookbehindContext = value);
        Should.Throw<ArgumentOutOfRangeException>(() => options.BufferSize = value);
    }

    [Fact]
    public void StreamMatchOptions_Defaults_AreDocumentedValues()
    {
        StreamMatchOptions options = new StreamMatchOptions();

        options.MaxMatchLength.ShouldBe(4096);
        options.LookbehindContext.ShouldBe(256);
        options.BufferSize.ShouldBe(65536);
    }

    [Fact]
    public void StreamMatch_ToString_ShowsIndexAndValue()
    {
        using StringReader reader = new StringReader("ab 42");

        StreamMatch match = Pattern.With.Digit.Repeat.OneOrMore.Matches(reader).Single();

        match.ToString().ShouldBe("3: \"42\"");
    }

    private static string RandomText(string[] tokens, int seed, int length = RandomInputLength)
    {
        Random random = new Random(seed);
        StringBuilder text = new StringBuilder(length + 8);
        while (text.Length < length)
        {
            text.Append(tokens[random.Next(tokens.Length)]);
        }

        return text.ToString();
    }

    private static string[] WholeTextSignatures(Pattern pattern, string text)
    {
        Regex regex = pattern.ToRegex();
        string[] names = regex.GetGroupNames().Where(name => name != "0").ToArray();
        return pattern.Matches(text).Select(m => Signature(m.Index, m.Value, GroupValues(m, names))).ToArray();
    }

    private static IEnumerable<KeyValuePair<string, string>> GroupValues(Match match, string[] names) =>
        names.Where(name => match.Groups[name].Success)
            .Select(name => new KeyValuePair<string, string>(name, match.Groups[name].Value));

    private static string Signature(StreamMatch match) => Signature(match.Index, match.Value, match.Groups);

    private static string Signature(long index, string value, IEnumerable<KeyValuePair<string, string>> groups)
    {
        IEnumerable<string> pairs = groups
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}={g.Value}");
        string groupText = string.Join(",", pairs);
        return $"{index}|{value}|{groupText}";
    }

    private static async Task<List<StreamMatch>> CollectAsync(IAsyncEnumerable<StreamMatch> matches)
    {
        List<StreamMatch> collected = new List<StreamMatch>();
        await foreach (StreamMatch match in matches)
        {
            collected.Add(match);
        }

        return collected;
    }

    private static async Task ConsumeAndCancelAfterFirstMatchAsync(IAsyncEnumerable<StreamMatch> matches)
    {
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        await foreach (StreamMatch match in matches.WithCancellation(cancellation.Token))
        {
            cancellation.Cancel();
        }
    }

    /// <summary>A reader that returns 1 to 7 characters per read, like a slow network stream.</summary>
    private sealed class TinyReader : TextReader
    {
        private const int MaxReadSize = 7;

        private readonly string _text;
        private readonly Random _random;
        private int _position;

        public TinyReader(string text, int seed)
        {
            _text = text;
            _random = new Random(seed);
        }

        public int ReadCalls { get; private set; }

        public override int Peek() => _position < _text.Length ? _text[_position] : -1;

        public override int Read() => _position < _text.Length ? _text[_position++] : -1;

        public override int Read(char[] buffer, int index, int count) => Read(buffer.AsSpan(index, count));

        public override int Read(Span<char> buffer)
        {
            ReadCalls++;
            int size = Math.Min(Math.Min(buffer.Length, _random.Next(1, MaxReadSize + 1)), _text.Length - _position);
            _text.AsSpan(_position, size).CopyTo(buffer);
            _position += size;
            return size;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<char> buffer,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return Read(buffer.Span);
        }
    }
}
