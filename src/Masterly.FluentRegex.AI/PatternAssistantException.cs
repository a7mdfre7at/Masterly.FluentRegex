using System;
using System.Net;

namespace Masterly.FluentRegex.AI;

/// <summary>
/// Thrown when the Claude API cannot produce a draft: an HTTP error, a timeout, a network failure or a
/// declined request. Never contains the API key.
/// </summary>
public sealed class PatternAssistantException : Exception
{
    /// <summary>Initializes a new instance with a default message.</summary>
    public PatternAssistantException()
        : base("The pattern assistant request failed.")
    {
    }

    /// <summary>Initializes a new instance with the given message.</summary>
    /// <param name="message">The error description.</param>
    public PatternAssistantException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with the given message and inner exception.</summary>
    /// <param name="message">The error description.</param>
    /// <param name="innerException">The underlying failure.</param>
    public PatternAssistantException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    internal PatternAssistantException(string message, ApiFailure failure, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = failure.StatusCode;
        ErrorType = failure.ErrorType;
        RequestId = failure.RequestId;
        RetryAfter = failure.RetryAfter;
        IsTransient = failure.IsTransient;
    }

    /// <summary>Gets the HTTP status code, or null when no response was received.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Gets the API error type, such as <c>authentication_error</c> or <c>rate_limit_error</c>.</summary>
    public string? ErrorType { get; }

    /// <summary>Gets the <c>request-id</c> response header, useful when contacting support.</summary>
    public string? RequestId { get; }

    /// <summary>Gets the delay the server asked for before retrying, when it sent <c>Retry-After</c>.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Gets a value indicating whether retrying later may succeed (rate limits, overload, server errors,
    /// timeouts and network failures). Authentication and request errors are not transient.
    /// </summary>
    public bool IsTransient { get; }
}

/// <summary>The structured details of an API failure.</summary>
internal readonly struct ApiFailure
{
    public ApiFailure(HttpStatusCode? statusCode, string? errorType, string? requestId, TimeSpan? retryAfter, bool isTransient)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
        RequestId = requestId;
        RetryAfter = retryAfter;
        IsTransient = isTransient;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? ErrorType { get; }

    public string? RequestId { get; }

    public TimeSpan? RetryAfter { get; }

    public bool IsTransient { get; }

    public static ApiFailure Transient => new ApiFailure(null, null, null, null, isTransient: true);

    public static ApiFailure Permanent => new ApiFailure(null, null, null, null, isTransient: false);
}
