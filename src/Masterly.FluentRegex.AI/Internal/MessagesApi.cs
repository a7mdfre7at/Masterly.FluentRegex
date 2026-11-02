using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Masterly.FluentRegex.AI;

/// <summary>The body of a <c>POST /v1/messages</c> request.</summary>
internal sealed class MessagesRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("system")]
    public string System { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<MessageParam> Messages { get; set; } = new List<MessageParam>();

    [JsonPropertyName("output_config")]
    public OutputConfig OutputConfig { get; set; } = new OutputConfig();

    [JsonPropertyName("fallbacks")]
    public string? Fallbacks { get; set; }
}

/// <summary>One conversation turn. Content is raw JSON so assistant turns are echoed back verbatim.</summary>
internal sealed class MessageParam
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public JsonElement Content { get; set; }
}

/// <summary>The <c>output_config</c> object: effort and the structured output format.</summary>
internal sealed class OutputConfig
{
    [JsonPropertyName("effort")]
    public string Effort { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public OutputFormat Format { get; set; } = new OutputFormat();
}

/// <summary>A JSON schema output format.</summary>
internal sealed class OutputFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_schema";

    [JsonPropertyName("schema")]
    public JsonElement Schema { get; set; }
}

/// <summary>The parts of a Messages API response the assistant reads.</summary>
internal sealed class MessagesResponse
{
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("content")]
    public JsonElement Content { get; set; }
}

/// <summary>The Messages API error envelope: <c>{"type":"error","error":{...}}</c>.</summary>
internal sealed class ApiErrorEnvelope
{
    [JsonPropertyName("error")]
    public ApiErrorBody? Error { get; set; }
}

/// <summary>The error details inside <see cref="ApiErrorEnvelope"/>.</summary>
internal sealed class ApiErrorBody
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>The JSON object the model is asked to return.</summary>
internal sealed class DraftPayload
{
    [JsonPropertyName("regex")]
    public string? Regex { get; set; }

    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }
}

/// <summary>Source-generated serialization metadata, so the package stays trim and AOT safe.</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MessagesRequest))]
[JsonSerializable(typeof(MessagesResponse))]
[JsonSerializable(typeof(ApiErrorEnvelope))]
[JsonSerializable(typeof(DraftPayload))]
[JsonSerializable(typeof(string))]
internal sealed partial class AssistantJsonContext : JsonSerializerContext
{
}
