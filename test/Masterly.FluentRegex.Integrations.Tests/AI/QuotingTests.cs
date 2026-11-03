using Masterly.FluentRegex.AI;

namespace Masterly.FluentRegex.Integrations.Tests.AI;

public class QuotingTests
{
    [Theory]
    [InlineData("", "\"\"")]
    [InlineData("abc", "\"abc\"")]
    [InlineData("a+b<c>", "\"a+b<c>\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("C:\\dir", "\"C:\\\\dir\"")]
    [InlineData("line\nnext", "\"line\\nnext\"")]
    [InlineData("cr\rlf", "\"cr\\rlf\"")]
    [InlineData("tab\there", "\"tab\\there\"")]
    [InlineData("bell\u0007", "\"bell\\u0007\"")]
    [InlineData("\u001F", "\"\\u001F\"")]
    [InlineData("del\u007F", "\"del\\u007F\"")]
    [InlineData("nel\u0085", "\"nel\\u0085\"")]
    [InlineData(" é€", "\" é€\"")]
    public void Quote_Value_EscapesOnlyQuotesBackslashesAndControlCharacters(string value, string expected)
    {
        string quoted = Quoting.Quote(value);

        quoted.ShouldBe(expected);
    }

    [Fact]
    public async Task DraftAsync_ExampleWithControlCharacters_IsQuotedInPrompt()
    {
        FakeAnthropicHandler handler = new FakeAnthropicHandler().EnqueueDraft("^abc$");
        PatternAssistant assistant = new PatternAssistant(new HttpClient(handler), new PatternAssistantOptions { ApiKey = "sk-ant-test" });

        await assistant.DraftAsync("abc", shouldNotMatch: new[] { "a\tb\u0001\"c\"" }, cancellationToken: TestContext.Current.CancellationToken);

        handler.Requests[0].LastUserText.ShouldEndWith("Strings that must not match:\n  \"a\\tb\\u0001\\\"c\\\"\"".Replace("\n", Environment.NewLine, StringComparison.Ordinal));
    }
}
