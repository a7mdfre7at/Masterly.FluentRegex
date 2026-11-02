using System.Net;
using System.Text;
using System.Text.Json;

namespace Masterly.FluentRegex.Integrations.Tests.AI;

/// <summary>
/// An in-memory stand-in for the Messages API. Replies are queued in order; every request is captured.
/// Tests never reach the network.
/// </summary>
internal sealed class FakeAnthropicHandler : HttpMessageHandler
{
    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _replies = new();

    public List<CapturedRequest> Requests { get; } = new();

    public FakeAnthropicHandler EnqueueDraft(string regex, string explanation = "Explained.") =>
        EnqueueText(JsonSerializer.Serialize(new { regex, explanation }));

    public FakeAnthropicHandler EnqueueText(string text, string stopReason = "end_turn")
    {
        object body = new
        {
            id = "msg_test",
            type = "message",
            role = "assistant",
            model = "claude-opus-5-5",
            stop_reason = stopReason,
            content = new object[]
            {
                new { type = "thinking", thinking = string.Empty, signature = "sig-test" },
                new { type = "text", text },
            },
        };
        return EnqueueJson(HttpStatusCode.OK, JsonSerializer.Serialize(body));
    }

    public FakeAnthropicHandler EnqueueJson(HttpStatusCode status, string json, Action<HttpResponseMessage>? configure = null)
    {
        _replies.Enqueue(_ =>
        {
            HttpResponseMessage response = new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            configure?.Invoke(response);
            return Task.FromResult(response);
        });
        return this;
    }

    public FakeAnthropicHandler EnqueueError(HttpStatusCode status, string errorType, string message, Action<HttpResponseMessage>? configure = null) =>
        EnqueueJson(status, JsonSerializer.Serialize(new { type = "error", error = new { type = errorType, message } }), configure);

    public FakeAnthropicHandler EnqueueHang()
    {
        _replies.Enqueue(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        });
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(request.RequestUri!, CopyHeaders(request), body));
        Func<CancellationToken, Task<HttpResponseMessage>> reply = _replies.Count > 0
            ? _replies.Dequeue()
            : throw new InvalidOperationException("The fake received more requests than were queued.");
        return await reply(cancellationToken);
    }

    private static Dictionary<string, string> CopyHeaders(HttpRequestMessage request)
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        return headers;
    }
}

/// <summary>A request the fake received.</summary>
internal sealed record CapturedRequest(Uri Uri, IReadOnlyDictionary<string, string> Headers, string Body)
{
    public JsonElement Json => JsonDocument.Parse(Body).RootElement;

    public JsonElement Messages => Json.GetProperty("messages");

    /// <summary>Gets the text of the last user turn, which carries the verification feedback on retries.</summary>
    public string LastUserText => Messages[Messages.GetArrayLength() - 1].GetProperty("content").GetString()!;
}
