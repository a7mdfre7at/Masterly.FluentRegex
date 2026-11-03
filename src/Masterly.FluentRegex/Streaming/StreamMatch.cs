#nullable enable
using System.Collections.Generic;

namespace Masterly.FluentRegex;

/// <summary>
/// A match found while streaming text through
/// <see cref="Pattern.Matches(System.IO.TextReader, StreamMatchOptions?)"/> or its async overloads.
/// </summary>
/// <remarks>
/// Unlike <see cref="System.Text.RegularExpressions.Match"/>, a stream match holds only its own text and
/// group values, so the buffered input it came from can be released.
/// </remarks>
public sealed class StreamMatch
{
    internal StreamMatch(long index, string value, IReadOnlyDictionary<string, string> groups)
    {
        Index = index;
        Value = value;
        Groups = groups;
    }

    /// <summary>Gets the zero-based position of the match in the whole input, in characters.</summary>
    public long Index { get; }

    /// <summary>Gets the length of the match in characters.</summary>
    public int Length => Value.Length;

    /// <summary>Gets the matched text.</summary>
    public string Value { get; }

    /// <summary>
    /// Gets the value of every capture group that participated in the match, keyed by group name for named
    /// groups and by number (such as <c>"1"</c>) for unnamed groups. Group 0, the whole match, is not
    /// included; use <see cref="Value"/>. Groups that did not participate are absent.
    /// </summary>
    public IReadOnlyDictionary<string, string> Groups { get; }

    /// <summary>Returns the position and text of the match, for diagnostics.</summary>
    /// <returns>A string such as <c>42: "555-1234"</c>.</returns>
    public override string ToString() => $"{Index}: \"{Value}\"";
}
