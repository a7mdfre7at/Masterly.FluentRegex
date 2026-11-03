using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Playground.Tests;

public class PlaygroundStateTests
{
    private const string DefaultRegex = @"^(?<user>[\w.+-]+)@(?<domain>[\w-]+(?:\.[\w-]+)+)$";
    private const string DefaultTestText = "ahmad@example.com\nnot an email\nsupport@masterly.dev";
    private const string PageUri = "https://example.github.io/Masterly.FluentRegex/";

    public static TheoryData<string, RegexOptions, string, string, bool> RoundTripStates => new TheoryData<string, RegexOptions, string, string, bool>
    {
        { @"^\d{3}-\d{4}$", RegexOptions.None, "555-1234", "matches", false },
        { @"(?<word>\p{L}+)", RegexOptions.IgnoreCase | RegexOptions.Multiline, "héllo wörld\r\nمرحبا 😀", "trace", true },
        { "a&b=c#d%e+f/g", RegexOptions.Singleline | RegexOptions.ExplicitCapture, "x&y=z#%+/?", "tab with spaces & symbols=%", false },
        { string.Empty, RegexOptions.None, string.Empty, string.Empty, true },
        { ">>>???", RegexOptions.CultureInvariant, "\n\n\t", "export", true },
    };

    [Fact]
    public void Constructor_Defaults_ShowTheEmailSample()
    {
        PlaygroundState state = new PlaygroundState();

        state.Regex.ShouldBe(DefaultRegex);
        state.Options.ShouldBe(RegexOptions.Multiline);
        state.TestText.ShouldBe(DefaultTestText);
        state.Tab.ShouldBe("matches");
        state.AsciiDigits.ShouldBeFalse();
    }

    [Theory]
    [InlineData(">>>", "a", "#p=Pj4-&o=3&t=YQ&tab=a%20b")]
    [InlineData("a", "???", "#p=YQ&o=3&t=Pz8_&tab=a%20b")]
    [InlineData("ab", "abc", "#p=YWI&o=3&t=YWJj&tab=a%20b")]
    [InlineData("", "", "#p=&o=3&t=&tab=a%20b")]
    public void ToFragment_Values_AreBase64UrlEncodedWithoutPaddingAndTabIsEscaped(string regex, string testText, string expected)
    {
        PlaygroundState state = new PlaygroundState
        {
            Regex = regex,
            Options = RegexOptions.IgnoreCase | RegexOptions.Multiline,
            TestText = testText,
            Tab = "a b",
        };

        string fragment = state.ToFragment();

        fragment.ShouldBe(expected);
    }

    [Fact]
    public void ToFragment_AsciiDigitsEnabled_AppendsTheDigitsFlag()
    {
        PlaygroundState state = new PlaygroundState { Regex = "a", Options = RegexOptions.None, TestText = "a", Tab = "matches", AsciiDigits = true };

        string fragment = state.ToFragment();

        fragment.ShouldBe("#p=YQ&o=0&t=YQ&tab=matches&ad=1");
    }

    [Fact]
    public void ToFragment_AsciiDigitsDisabled_OmitsTheDigitsFlag()
    {
        PlaygroundState state = new PlaygroundState { Regex = "a", Options = RegexOptions.None, TestText = "a", Tab = "matches", AsciiDigits = false };

        string fragment = state.ToFragment();

        fragment.ShouldBe("#p=YQ&o=0&t=YQ&tab=matches");
    }

    [Theory]
    [MemberData(nameof(RoundTripStates))]
    public void FromUri_FragmentFromToFragment_RestoresEveryField(string regex, RegexOptions options, string testText, string tab, bool asciiDigits)
    {
        PlaygroundState original = new PlaygroundState { Regex = regex, Options = options, TestText = testText, Tab = tab, AsciiDigits = asciiDigits };

        PlaygroundState restored = PlaygroundState.FromUri(PageUri + original.ToFragment());

        restored.Regex.ShouldBe(regex);
        restored.Options.ShouldBe(options);
        restored.TestText.ShouldBe(testText);
        restored.Tab.ShouldBe(tab);
        restored.AsciiDigits.ShouldBe(asciiDigits);
    }

    [Fact]
    public void FromUri_DefaultStateFragment_RoundTripsToTheDefaults()
    {
        PlaygroundState restored = PlaygroundState.FromUri(PageUri + new PlaygroundState().ToFragment());

        restored.Regex.ShouldBe(DefaultRegex);
        restored.Options.ShouldBe(RegexOptions.Multiline);
        restored.TestText.ShouldBe(DefaultTestText);
        restored.Tab.ShouldBe("matches");
        restored.AsciiDigits.ShouldBeFalse();
    }

    [Theory]
    [InlineData(PageUri)]
    [InlineData(PageUri + "?p=YQ&o=0")]
    [InlineData(PageUri + "#")]
    [InlineData(PageUri + "#&&&")]
    [InlineData("")]
    public void FromUri_NoFragmentOrEmptyFragment_KeepsTheDefaults(string uri)
    {
        PlaygroundState state = PlaygroundState.FromUri(uri);

        state.Regex.ShouldBe(DefaultRegex);
        state.Options.ShouldBe(RegexOptions.Multiline);
        state.TestText.ShouldBe(DefaultTestText);
        state.Tab.ShouldBe("matches");
        state.AsciiDigits.ShouldBeFalse();
    }

    [Fact]
    public void FromUri_FullFragment_DecodesEveryField()
    {
        PlaygroundState state = PlaygroundState.FromUri(PageUri + "#p=Pj4-&o=3&t=Pz8_&tab=a%20b&ad=1");

        state.Regex.ShouldBe(">>>");
        state.Options.ShouldBe(RegexOptions.IgnoreCase | RegexOptions.Multiline);
        state.TestText.ShouldBe("???");
        state.Tab.ShouldBe("a b");
        state.AsciiDigits.ShouldBeTrue();
    }

    [Theory]
    [InlineData("#p=a&t=a")]
    [InlineData("#p=%25%25&t=!!!!")]
    [InlineData("#p=YQ=x&t=====")]
    public void FromUri_MalformedBase64_KeepsTheDefaultRegexAndText(string fragment)
    {
        PlaygroundState state = PlaygroundState.FromUri(PageUri + fragment);

        state.Regex.ShouldBe(DefaultRegex);
        state.TestText.ShouldBe(DefaultTestText);
    }

    [Theory]
    [InlineData("#o=abc")]
    [InlineData("#o=")]
    [InlineData("#o=1.5")]
    [InlineData("#o=99999999999")]
    public void FromUri_OptionsNotAnInteger_KeepsTheDefaultOptions(string fragment)
    {
        PlaygroundState state = PlaygroundState.FromUri(PageUri + fragment);

        state.Options.ShouldBe(RegexOptions.Multiline);
    }

    [Fact]
    public void FromUri_PaddedBase64_SplitsOnlyOnTheFirstEqualsSign()
    {
        PlaygroundState state = PlaygroundState.FromUri(PageUri + "#p=YQ==&t=YWI=");

        state.Regex.ShouldBe("a");
        state.TestText.ShouldBe("ab");
    }

    [Fact]
    public void FromUri_PartsWithoutKeyOrEqualsAndUnknownKeys_AreIgnored()
    {
        PlaygroundState state = PlaygroundState.FromUri(PageUri + "#junk&=YQ&x=1&P=YQ&p=Yg");

        state.Regex.ShouldBe("b");
        state.Options.ShouldBe(RegexOptions.Multiline);
        state.TestText.ShouldBe(DefaultTestText);
        state.Tab.ShouldBe("matches");
        state.AsciiDigits.ShouldBeFalse();
    }

    [Fact]
    public void FromUri_RepeatedKey_LastValueWins()
    {
        PlaygroundState state = PlaygroundState.FromUri(PageUri + "#p=YQ&tab=one&p=Yg&tab=two");

        state.Regex.ShouldBe("b");
        state.Tab.ShouldBe("two");
    }

    [Fact]
    public void FromUri_EmptyPatternValue_DecodesToAnEmptyRegex()
    {
        PlaygroundState state = PlaygroundState.FromUri(PageUri + "#p=&t=");

        state.Regex.ShouldBe(string.Empty);
        state.TestText.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData("", false)]
    public void FromUri_AsciiDigitsFlag_IsEnabledOnlyByOne(string value, bool expected)
    {
        PlaygroundState state = PlaygroundState.FromUri($"{PageUri}#ad={value}");

        state.AsciiDigits.ShouldBe(expected);
    }

    [Theory]
    [InlineData("tab=safety", "safety")]
    [InlineData("tab=a%26b%3Dc", "a&b=c")]
    [InlineData("tab=", "")]
    [InlineData("tab=%zz", "%zz")]
    public void FromUri_Tab_IsUnescaped(string part, string expected)
    {
        PlaygroundState state = PlaygroundState.FromUri($"{PageUri}#{part}");

        state.Tab.ShouldBe(expected);
    }

    [Theory]
    [InlineData("#o=0", RegexOptions.None)]
    [InlineData("#o=513", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("#o=1024", RegexOptions.NonBacktracking)]
    public void FromUri_IntegerOptions_AreCastToRegexOptions(string fragment, RegexOptions expected)
    {
        PlaygroundState state = PlaygroundState.FromUri(PageUri + fragment);

        state.Options.ShouldBe(expected);
    }
}
