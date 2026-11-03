#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Streaming;

/// <summary>
/// Finds matches in text that arrives in chunks, producing exactly the sequence
/// <see cref="Regex.Matches(string)"/> would produce on the whole text, within the limits of
/// <see cref="StreamMatchOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// The buffered text starts at global offset <c>_offset</c>. Scanning resumes at <c>_position</c> with
/// <see cref="Regex.Match(string, int)"/>, which lets lookbehinds, <c>\b</c> and <c>^</c> see the context that
/// is kept before it. Because at least one character of context is always kept once the buffer has been
/// trimmed, a scan never starts at buffer index 0 mid-stream, so <c>^</c> and <c>\A</c> cannot match falsely.
/// </para>
/// <para>
/// A match is only emitted once it starts more than <c>MaxMatchLength</c> characters before the end of the
/// buffered data, or once the input is complete. Every attempt that failed at an earlier position had all
/// the text it needed, so those failures are final and the scan frontier can move past them.
/// </para>
/// </remarks>
internal sealed class StreamMatchScanner
{
    private const int WholeMatchGroup = 0;
    private const long GrowthFactor = 2;
    private const long HalfDivisor = 2;

    private static readonly IReadOnlyDictionary<string, string> NoGroups =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    private readonly Regex _regex;
    private readonly int _maxMatchLength;
    private readonly int _lookbehindContext;
    private readonly int[] _groupNumbers;
    private readonly string[] _groupNames;

    private char[] _chars;
    private int _length;
    private string? _text;
    private long _offset;
    private int _position;
    private bool _inputComplete;
    private bool _enumerationStarted;

    /// <summary>Creates a scanner that snapshots the given options.</summary>
    /// <param name="regex">The compiled regex to run.</param>
    /// <param name="options">The buffering options; their values are copied.</param>
    public StreamMatchScanner(Regex regex, StreamMatchOptions options)
    {
        _regex = regex;
        _maxMatchLength = options.MaxMatchLength;
        _lookbehindContext = options.LookbehindContext;
        BufferSize = options.BufferSize;
        _chars = new char[BufferSize];
        _groupNumbers = CapturingGroupNumbers(regex);
        _groupNames = Array.ConvertAll(_groupNumbers, regex.GroupNameFromNumber);
    }

    /// <summary>Gets how many characters the caller should read per chunk.</summary>
    public int BufferSize { get; }

    /// <summary>
    /// Marks the start of the one enumeration this scanner serves. The reader behind it cannot be rewound, so a
    /// second enumeration would silently continue in the middle of the text; it throws instead.
    /// </summary>
    /// <exception cref="InvalidOperationException">The matches were already enumerated.</exception>
    public void BeginEnumeration()
    {
        if (_enumerationStarted)
        {
            throw new InvalidOperationException(
                "Matches read from a TextReader or Stream can be enumerated only once, because the text cannot be read again. " +
                "Call ToList() to keep them, or call Matches or MatchesAsync again with a new reader.");
        }

        _enumerationStarted = true;
    }

    /// <summary>Gets a value indicating whether every match has been returned.</summary>
    public bool IsFinished => _inputComplete && _position > _length;

    /// <summary>Appends a chunk of input to the buffer, first discarding text that is no longer needed.</summary>
    /// <param name="source">The characters read.</param>
    /// <param name="count">How many characters of <paramref name="source"/> are valid.</param>
    public void Append(char[] source, int count)
    {
        if (count == 0)
        {
            return;
        }

        TrimConsumedText();
        EnsureCapacity((long)_length + count);
        Array.Copy(source, 0, _chars, _length, count);
        _length += count;
        _text = null;
    }

    /// <summary>Signals that no more input will arrive.</summary>
    public void CompleteInput() => _inputComplete = true;

    /// <summary>
    /// Returns the next match, or null when more input is needed or <see cref="IsFinished"/> is true.
    /// </summary>
    /// <returns>The next match in input order, or null.</returns>
    /// <exception cref="InvalidOperationException">A match is longer than <c>MaxMatchLength</c>.</exception>
    public StreamMatch? Next()
    {
        if (_position > _length || !HasEnoughLookahead())
        {
            return null;
        }

        Match match = _regex.Match(CurrentText(), _position);
        if (!match.Success)
        {
            OnNoMatch();
            return null;
        }

        if (!IsFinal(match))
        {
            AdvanceFrontier();
            return null;
        }

        return Emit(match);
    }

    private bool HasEnoughLookahead() => _inputComplete || _length - _position > _maxMatchLength;

    private void OnNoMatch()
    {
        if (_inputComplete)
        {
            _position = _length + 1;
            return;
        }

        AdvanceFrontier();
    }

    private bool IsFinal(Match match)
    {
        if (_inputComplete)
        {
            return true;
        }

        if ((long)match.Index + _maxMatchLength >= _length)
        {
            return false;
        }

        if (match.Length > _maxMatchLength)
        {
            throw new InvalidOperationException(
                $"A match at index {_offset + match.Index} is at least {match.Length} characters long, more than " +
                $"StreamMatchOptions.MaxMatchLength ({_maxMatchLength}), so it may have been cut off at the end " +
                "of the buffered input. Increase MaxMatchLength to at least the longest match you expect.");
        }

        return true;
    }

    /// <summary>
    /// Moves the scan position past every start position whose attempt had all the text it needed:
    /// those start more than <c>MaxMatchLength</c> characters before the end of the buffer.
    /// </summary>
    private void AdvanceFrontier()
    {
        int frontier = _length - _maxMatchLength;
        if (frontier > _position)
        {
            _position = frontier;
        }
    }

    private StreamMatch Emit(Match match)
    {
        StreamMatch result = new StreamMatch(_offset + match.Index, match.Value, ParticipatingGroups(match));

        // Like Regex.Matches: after an empty match the next attempt starts one character later.
        _position = match.Length == 0 ? match.Index + 1 : match.Index + match.Length;
        return result;
    }

    private IReadOnlyDictionary<string, string> ParticipatingGroups(Match match)
    {
        Dictionary<string, string>? groups = null;
        for (int i = 0; i < _groupNumbers.Length; i++)
        {
            Group group = match.Groups[_groupNumbers[i]];
            if (group.Success)
            {
                groups ??= new Dictionary<string, string>(StringComparer.Ordinal);
                groups[_groupNames[i]] = group.Value;
            }
        }

        return groups == null ? NoGroups : new ReadOnlyDictionary<string, string>(groups);
    }

    private string CurrentText() => _text ??= new string(_chars, 0, _length);

    private void TrimConsumedText()
    {
        int discard = _position - _lookbehindContext;
        bool worthCopying = discard >= BufferSize || discard * HalfDivisor >= _length;
        if (discard <= 0 || !worthCopying)
        {
            return;
        }

        Array.Copy(_chars, discard, _chars, 0, _length - discard);
        _length -= discard;
        _position -= discard;
        _offset += discard;
        _text = null;
    }

    private void EnsureCapacity(long required)
    {
        if (required <= _chars.Length)
        {
            return;
        }

        if (required > int.MaxValue)
        {
            throw new InvalidOperationException("The streaming buffer cannot grow beyond 2 GB of characters.");
        }

        long grown = Math.Min(int.MaxValue, Math.Max(required, _chars.Length * GrowthFactor));
        char[] larger = new char[grown];
        Array.Copy(_chars, larger, _length);
        _chars = larger;
    }

    private static int[] CapturingGroupNumbers(Regex regex)
    {
        List<int> numbers = new List<int>();
        foreach (int number in regex.GetGroupNumbers())
        {
            if (number != WholeMatchGroup)
            {
                numbers.Add(number);
            }
        }

        return numbers.ToArray();
    }
}
