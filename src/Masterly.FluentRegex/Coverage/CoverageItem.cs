#nullable enable

using Masterly.FluentRegex.Coverage;

namespace Masterly.FluentRegex;

/// <summary>The kind of decision point a <see cref="CoverageItem"/> tracks.</summary>
public enum CoverageItemKind
{
    /// <summary>A branch of an alternation (<c>a|b</c>) or of a conditional was taken.</summary>
    AlternationBranch,

    /// <summary>The body of a quantifier with a minimum of zero (<c>?</c>, <c>*</c>, <c>{0,n}</c>) ran at least once.</summary>
    OptionalPartTaken,

    /// <summary>A bounded repetition such as <c>{2,5}</c> ran its maximum number of times in one match.</summary>
    UpperBoundReached
}

/// <summary>
/// One decision point of a pattern and how often the recorded inputs exercised it.
/// </summary>
public sealed class CoverageItem
{
    internal CoverageItem(CoverageItemKind kind, string description, int start, int length, int hitCount)
    {
        Kind = kind;
        Description = description;
        Start = start;
        Length = length;
        HitCount = hitCount;
    }

    /// <summary>Gets the kind of decision point.</summary>
    public CoverageItemKind Kind { get; }

    /// <summary>Gets a short description, such as <c>branch "Mr"</c> or <c>upper bound 3 of "\d{1,3}"</c>.</summary>
    public string Description { get; }

    /// <summary>Gets the start of the construct in the pattern's regex text.</summary>
    public int Start { get; }

    /// <summary>Gets the length of the construct in the pattern's regex text. Empty branches have length 0.</summary>
    public int Length { get; }

    /// <summary>Gets the number of successful matches that exercised this item.</summary>
    public int HitCount { get; }

    /// <summary>Gets a value indicating whether at least one match exercised this item.</summary>
    public bool IsCovered => HitCount > 0;

    /// <summary>Returns the description and hit count.</summary>
    public override string ToString() => CoverageText.Line(this);
}
