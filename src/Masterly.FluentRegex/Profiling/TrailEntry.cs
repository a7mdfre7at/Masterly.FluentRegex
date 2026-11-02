#nullable enable
namespace Masterly.FluentRegex.Profiling;

/// <summary>The kinds of entry on the backtracking trail.</summary>
internal enum TrailKind
{
    /// <summary>A choice point: resume at address <c>A</c>, position <c>B</c>; charged to hotspot <c>C</c>.</summary>
    Branch,

    /// <summary>An open atomic group or lookaround: opened by instruction <c>A</c> at position <c>B</c>.</summary>
    Barrier,

    /// <summary>Undo a capture: slot <c>A</c> had start <c>B</c> and end <c>C</c>.</summary>
    UndoCapture,

    /// <summary>Undo a group open: slot <c>A</c> had open position <c>B</c>.</summary>
    UndoOpen,

    /// <summary>Undo pushing a loop frame.</summary>
    UndoFramePush,

    /// <summary>Undo popping a loop frame with count <c>A</c> and mark <c>B</c>.</summary>
    UndoFramePop,

    /// <summary>Undo changing the top loop frame, which had count <c>A</c> and mark <c>B</c>.</summary>
    UndoFrameSet
}

/// <summary>
/// One entry of the trail: either a choice point to resume, or a record of a state change to revert.
/// Reverting every change above a choice point restores the exact state the choice was made in.
/// </summary>
internal readonly struct TrailEntry
{
    public TrailEntry(TrailKind kind, int a, int b, int c)
    {
        Kind = kind;
        A = a;
        B = b;
        C = c;
    }

    public TrailKind Kind { get; }

    public int A { get; }

    public int B { get; }

    public int C { get; }

    /// <summary>True for state changes that must survive when an atomic group discards its choice points.</summary>
    public bool IsUndo => Kind >= TrailKind.UndoCapture;
}

/// <summary>The state of one active loop: completed iterations and where the last one started.</summary>
internal readonly struct LoopFrame
{
    /// <summary>The mark of a loop that has not completed an iteration yet.</summary>
    public const int NoMark = -1;

    public LoopFrame(int count, int mark)
    {
        Count = count;
        Mark = mark;
    }

    public int Count { get; }

    public int Mark { get; }
}

/// <summary>The outcome of one match attempt or search, without timing.</summary>
internal readonly struct MatchSpan
{
    public MatchSpan(bool isMatch, int index, int length)
    {
        IsMatch = isMatch;
        Index = index;
        Length = length;
    }

    public static MatchSpan None { get; } = new MatchSpan(false, -1, 0);

    public bool IsMatch { get; }

    public int Index { get; }

    public int Length { get; }

    public bool SameAs(MatchSpan other) =>
        IsMatch == other.IsMatch && (!IsMatch || (Index == other.Index && Length == other.Length));

    public override string ToString() => IsMatch ? $"match at {Index}, length {Length}" : "no match";
}
