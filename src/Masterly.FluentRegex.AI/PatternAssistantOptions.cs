using System;
using System.Threading;

namespace Masterly.FluentRegex.AI;

/// <summary>
/// Settings for <see cref="PatternAssistant"/>.
/// </summary>
public sealed class PatternAssistantOptions
{
    /// <summary>The model used when none is configured.</summary>
    public const string DefaultModel = "claude-opus-5-5";

    /// <summary>The effort level used when none is configured.</summary>
    public const string DefaultEffort = "medium";

    private const int DefaultMaxAttempts = 3;
    private const int DefaultMaxTokens = 16000;
    private const int DefaultTimeoutMinutes = 5;

    /// <summary>
    /// Gets or sets the Anthropic API key, sent in the <c>x-api-key</c> header. Required.
    /// The key is never included in exception messages or <see cref="ToString"/>.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the Claude model id. Defaults to <see cref="DefaultModel"/>.</summary>
    public string Model { get; set; } = DefaultModel;

    /// <summary>
    /// Gets or sets how many drafts the model may produce. Each failed verification is sent back
    /// as feedback for the next draft. Defaults to 3.
    /// </summary>
    public int MaxAttempts { get; set; } = DefaultMaxAttempts;

    /// <summary>Gets or sets the API root. Defaults to <c>https://api.anthropic.com/</c>.</summary>
    public Uri BaseAddress { get; set; } = new Uri("https://api.anthropic.com/");

    /// <summary>
    /// Gets or sets the time limit for each API request, or <see cref="Timeout.InfiniteTimeSpan"/>.
    /// Defaults to 5 minutes. The <see cref="System.Net.Http.HttpClient.Timeout"/> also applies.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(DefaultTimeoutMinutes);

    /// <summary>Gets or sets the response token limit per request. Defaults to 16000.</summary>
    public int MaxTokens { get; set; } = DefaultMaxTokens;

    /// <summary>
    /// Gets or sets the effort level (<c>low</c>, <c>medium</c>, <c>high</c>, <c>xhigh</c> or <c>max</c>).
    /// Defaults to <see cref="DefaultEffort"/>.
    /// </summary>
    public string Effort { get; set; } = DefaultEffort;

    /// <summary>
    /// Gets or sets a value indicating whether a request the model declines is retried server-side on
    /// Anthropic's recommended fallback model (<c>fallbacks: "default"</c>). Defaults to true.
    /// Turn it off for proxies or gateways that do not support the beta.
    /// </summary>
    public bool UseServerSideFallback { get; set; } = true;

    /// <summary>Returns a description of the settings with the API key redacted.</summary>
    public override string ToString()
    {
        string key = string.IsNullOrEmpty(ApiKey) ? "<not set>" : "<redacted>";
        return $"Model = {Model}, MaxAttempts = {MaxAttempts}, BaseAddress = {BaseAddress}, Timeout = {Timeout}, MaxTokens = {MaxTokens}, Effort = {Effort}, ApiKey = {key}";
    }
}
