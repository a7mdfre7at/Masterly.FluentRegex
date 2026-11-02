#nullable enable
using System;

namespace Masterly.FluentRegex;

/// <summary>
/// Controls how <see cref="Pattern.Matches(System.IO.TextReader, StreamMatchOptions?)"/> and
/// <see cref="Pattern.MatchesAsync(System.IO.TextReader, StreamMatchOptions?, System.Threading.CancellationToken)"/>
/// buffer their input.
/// </summary>
/// <remarks>
/// Streamed results are identical to <see cref="Pattern.Matches(string)"/> on the whole text whenever every
/// match, and every character the engine looks ahead at while trying a match, fits within
/// <see cref="MaxMatchLength"/> characters of the position where the attempt starts, and lookbehinds,
/// <c>\b</c> and <c>^</c> need at most <see cref="LookbehindContext"/> characters before it.
/// </remarks>
public sealed class StreamMatchOptions
{
    private const int DefaultMaxMatchLength = 4096;
    private const int DefaultLookbehindContext = 256;
    private const int DefaultBufferSize = 65536;

    private int _maxMatchLength = DefaultMaxMatchLength;
    private int _lookbehindContext = DefaultLookbehindContext;
    private int _bufferSize = DefaultBufferSize;

    /// <summary>
    /// Gets or sets the longest match, including the engine's lookahead, that is guaranteed to be found exactly.
    /// A longer match that is not at the end of the input raises an <see cref="InvalidOperationException"/>
    /// instead of being emitted, because it could have been cut off. Defaults to 4096.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxMatchLength
    {
        get => _maxMatchLength;
        set => _maxMatchLength = RequirePositive(value, nameof(MaxMatchLength));
    }

    /// <summary>
    /// Gets or sets how many characters before the scan position are kept, so lookbehinds, <c>\b</c> and
    /// <c>^</c> with <see cref="System.Text.RegularExpressions.RegexOptions.Multiline"/> can see earlier text.
    /// Defaults to 256.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int LookbehindContext
    {
        get => _lookbehindContext;
        set => _lookbehindContext = RequirePositive(value, nameof(LookbehindContext));
    }

    /// <summary>
    /// Gets or sets how many characters are read from the input at a time. Defaults to 65536.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int BufferSize
    {
        get => _bufferSize;
        set => _bufferSize = RequirePositive(value, nameof(BufferSize));
    }

    private static int RequirePositive(int value, string propertyName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(propertyName, value, $"{propertyName} must be positive.");
        }

        return value;
    }
}
