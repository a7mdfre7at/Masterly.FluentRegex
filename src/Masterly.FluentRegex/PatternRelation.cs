#nullable enable

namespace Masterly.FluentRegex;

/// <summary>
/// How the languages of two patterns relate. The language of a pattern is the set of strings
/// it matches in full.
/// </summary>
public enum PatternRelation
{
    /// <summary>Both patterns match exactly the same strings.</summary>
    Equivalent,

    /// <summary>Every string the left pattern matches, the right one matches too, but not the other way round.</summary>
    ProperSubset,

    /// <summary>Every string the right pattern matches, the left one matches too, but not the other way round.</summary>
    ProperSuperset,

    /// <summary>Some strings match both patterns, and each pattern also matches strings the other rejects.</summary>
    Overlapping,

    /// <summary>No string matches both patterns.</summary>
    Disjoint
}
