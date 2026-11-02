#nullable enable

namespace Masterly.FluentRegex.Coverage;

/// <summary>
/// One coverage item of an instrumented pattern: the marker group that observes it and the number of
/// marker captures a single match needs for the item to count as exercised.
/// </summary>
internal sealed class CoverageProbe
{
    public CoverageProbe(CoverageItemKind kind, string description, int start, int length, int markerGroup, int requiredCaptures)
    {
        Kind = kind;
        Description = description;
        Start = start;
        Length = length;
        MarkerGroup = markerGroup;
        RequiredCaptures = requiredCaptures;
    }

    public CoverageItemKind Kind { get; }

    public string Description { get; }

    public int Start { get; }

    public int Length { get; }

    /// <summary>The number of the empty marker group inserted for this item.</summary>
    public int MarkerGroup { get; }

    /// <summary>The marker captures one match needs: 1, or the upper bound of a repetition.</summary>
    public int RequiredCaptures { get; }
}
