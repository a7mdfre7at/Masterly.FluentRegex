using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.FluentRegex.AI;

/// <summary>
/// A minimal client for the Claude Messages API (<c>POST /v1/messages</c>) built on <see cref="HttpClient"/>.
/// </summary>
internal sealed class MessagesClient
{
    public const string ApiVersion = "2026-10-21";
    public const string FallbackBeta = "server-side-fallback-2026-07-01";
    public const string FallbackMode = "default";

    private const string MessagesPath = "v1/messages";
    private const string JsonMediaType = "application/json";
    private const string RequestIdHeader = "request-id";
    private const string RefusalStopReason = "refusal";
    private const int MaxErrorMessageLength = 500;
    private const int RequestTimeoutStatus = 408;
    private const int ConflictStatus = 409;
    private const int TooManyRequestsStatus = 429;
    private const int FirstServerErrorStatus = 500;

    private static readonly JsonElement Schema = ParseSchema();

    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _effort;
    private readonly int _maxTokens;
    private readonly TimeSpan _timeout;
    private readonly bool _useFallback;

    public MessagesClient(HttpClient httpClient, PatternAssistantOptions options)
    {
        _httpClient = httpClient;
        _endpoint = new Uri(options.BaseAddress, MessagesPath);
        _apiKey = options.ApiKey;
        _model = options.Model;
        _effort = options.Effort;
        _maxTokens = options.MaxTokens;
        _timeout = options.Timeout;
        _useFallback = options.UseServerSideFallback;
    }

    public async Task<ModelReply> SendAsync(List<MessageParam> messages, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            using HttpRequestMessage request = BuildRequest(messages);
            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token)
                .ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpError(response, body);
            }

            return ParseReply(body);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PatternAssistantException(
                $"The Claude API did not respond within {_timeout}.", ApiFailure.Transient, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new PatternAssistantException(
                $"Could not reach the Claude API: {Redact(ex.Message)}", ApiFailure.Transient, ex);
        }
    }

    private HttpRequestMessage BuildRequest(List<MessageParam> messages)
    {
        MessagesRequest payload = new MessagesRequest
        {
            Model = _model,
            MaxTokens = _maxTokens,
            System = Prompts.System,
            Messages = messages,
            OutputConfig = new OutputConfig { Effort = _effort, Format = new OutputFormat { Schema = Schema } },
            Fallbacks = _useFallback ? FallbackMode : null,
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload, AssistantJsonContext.Default.MessagesRequest);
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new ByteArrayContent(json),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(JsonMediaType);
        request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);
        if (_useFallback)
        {
            request.Headers.TryAddWithoutValidation("anthropic-beta", FallbackBeta);
        }

        return request;
    }

    private static ModelReply ParseReply(string body)
    {
        MessagesResponse? response;
        try
        {
            response = JsonSerializer.Deserialize(body, AssistantJsonContext.Default.MessagesResponse);
        }
        catch (JsonException ex)
        {
            throw new PatternAssistantException("The Claude API returned a response that is not valid JSON.", ApiFailure.Permanent, ex);
        }

        if (response == null || response.Content.ValueKind != JsonValueKind.Array)
        {
            throw new PatternAssistantException("The Claude API response has no content array.", ApiFailure.Permanent);
        }

        if (string.Equals(response.StopReason, RefusalStopReason, StringComparison.Ordinal))
        {
            throw new PatternAssistantException("The model declined to draft this pattern (stop_reason \"refusal\").", ApiFailure.Permanent);
        }

        return new ModelReply(ExtractText(response.Content), response.Content.Clone(), response.StopReason);
    }

    private static string ExtractText(JsonElement content)
    {
        StringBuilder text = new StringBuilder();
        foreach (JsonElement block in content.EnumerateArray())
        {
            if (block.ValueKind == JsonValueKind.Object
                && block.TryGetProperty("type", out JsonElement type)
                && type.ValueEquals("text")
                && block.TryGetProperty("text", out JsonElement value)
                && value.ValueKind == JsonValueKind.String)
            {
                text.Append(value.GetString());
            }
        }

        return text.ToString();
    }

    private PatternAssistantException CreateHttpError(HttpResponseMessage response, string body)
    {
        int status = (int)response.StatusCode;
        ApiErrorBody? error = TryParseError(body);
        string? requestId = response.Headers.TryGetValues(RequestIdHeader, out IEnumerable<string>? values)
            ? string.Join(",", values)
            : null;
        ApiFailure failure = new ApiFailure(response.StatusCode, error?.Type, requestId, ReadRetryAfter(response), IsTransient(status));

        string detail = error?.Message is { Length: > 0 } message ? $": {Truncate(Redact(message))}" : string.Empty;
        string kind = error?.Type is { Length: > 0 } type ? $" {type}" : string.Empty;
        string request = requestId != null ? $" (request-id {requestId})" : string.Empty;
        return new PatternAssistantException($"The Claude API returned HTTP {status}{kind}{detail}{request}.", failure);
    }

    private static ApiErrorBody? TryParseError(string body)
    {
        try
        {
            return JsonSerializer.Deserialize(body, AssistantJsonContext.Default.ApiErrorEnvelope)?.Error;
        }
        catch (JsonException)
        {
            // Not an API error envelope (for example an HTML page from a proxy); report the status only.
            return null;
        }
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta)
        {
            return delta;
        }

        if (retryAfter?.Date is DateTimeOffset date)
        {
            TimeSpan remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return null;
    }

    private static bool IsTransient(int status) =>
        status == RequestTimeoutStatus || status == ConflictStatus || status == TooManyRequestsStatus || status >= FirstServerErrorStatus;

    private string Redact(string text) =>
        _apiKey.Length == 0 ? text : text.Replace(_apiKey, "<redacted>", StringComparison.Ordinal);

    private static string Truncate(string text) =>
        text.Length <= MaxErrorMessageLength ? text : $"{text.Substring(0, MaxErrorMessageLength)}...";

    private static JsonElement ParseSchema()
    {
        using JsonDocument document = JsonDocument.Parse(Prompts.ResponseSchema);
        return document.RootElement.Clone();
    }
}

/// <summary>The text of a model reply plus its raw content blocks, kept to echo back unchanged.</summary>
internal readonly struct ModelReply
{
    public ModelReply(string text, JsonElement content, string? stopReason)
    {
        Text = text;
        Content = content;
        StopReason = stopReason;
    }

    public string Text { get; }

    public JsonElement Content { get; }

    public string? StopReason { get; }
}
