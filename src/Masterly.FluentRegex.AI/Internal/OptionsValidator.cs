using System;
using System.Threading;

namespace Masterly.FluentRegex.AI;

/// <summary>Validates <see cref="PatternAssistantOptions"/> without ever echoing the API key.</summary>
internal static class OptionsValidator
{
    private const string OptionsParameter = "options";

    public static void Validate(PatternAssistantOptions options)
    {
        ValidateApiKey(options.ApiKey);
        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new ArgumentException("A model id is required.", OptionsParameter);
        }

        if (string.IsNullOrWhiteSpace(options.Effort))
        {
            throw new ArgumentException("An effort level is required.", OptionsParameter);
        }

        if (options.BaseAddress == null || !options.BaseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException("BaseAddress must be an absolute URI.", OptionsParameter);
        }

        if (options.MaxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(OptionsParameter, options.MaxAttempts, "MaxAttempts must be at least 1.");
        }

        if (options.MaxTokens < 1)
        {
            throw new ArgumentOutOfRangeException(OptionsParameter, options.MaxTokens, "MaxTokens must be at least 1.");
        }

        if (options.Timeout <= TimeSpan.Zero && options.Timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(OptionsParameter, options.Timeout, "Timeout must be positive or Timeout.InfiniteTimeSpan.");
        }
    }

    private static void ValidateApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("An API key is required (PatternAssistantOptions.ApiKey).", OptionsParameter);
        }

        foreach (char c in apiKey)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                throw new ArgumentException("The API key contains whitespace or control characters.", OptionsParameter);
            }
        }
    }
}
