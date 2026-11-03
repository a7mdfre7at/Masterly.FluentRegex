using System.Text.Json;

namespace Masterly.FluentRegex.AI;

/// <summary>Turns the model's text reply into a <see cref="DraftPayload"/>, or explains why it cannot.</summary>
internal static class ReplyParser
{
    private const string MaxTokensStopReason = "max_tokens";

    /// <summary>Parses the reply.</summary>
    /// <returns>Null on success, otherwise a description of the problem to send back to the model.</returns>
    public static string? TryParse(ModelReply reply, out DraftPayload payload)
    {
        payload = new DraftPayload();
        if (reply.StopReason == MaxTokensStopReason)
        {
            return "it was cut off by the token limit before the JSON object was complete.";
        }

        string json = ExtractObject(reply.Text);
        if (json.Length == 0)
        {
            return "it did not contain a JSON object.";
        }

        DraftPayload? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(json, AssistantJsonContext.Default.DraftPayload);
        }
        catch (JsonException)
        {
            return "it was not valid JSON.";
        }

        if (parsed?.Regex == null)
        {
            return "the JSON object had no string property \"regex\".";
        }

        payload = parsed;
        return null;
    }

    /// <summary>
    /// Structured outputs return a bare object; tolerate surrounding prose or code fences from gateways
    /// that do not enforce the schema by taking the outermost braces.
    /// </summary>
    private static string ExtractObject(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text.Substring(start, end - start + 1) : string.Empty;
    }
}
