#nullable enable
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Profiling;

/// <summary>The operations of the backtracking program the profiler executes.</summary>
internal enum OpCode
{
    /// <summary>Consume one character from <see cref="Instruction.Set"/>.</summary>
    Char,

    /// <summary>Test a zero-width anchor such as <c>^</c> or <c>\b</c>.</summary>
    Anchor,

    /// <summary>Match the text last captured by a group.</summary>
    Backreference,

    /// <summary>Continue with the next instruction; on backtrack resume at <see cref="Instruction.Target"/>.</summary>
    Split,

    /// <summary>Continue at <see cref="Instruction.Target"/>.</summary>
    Jump,

    /// <summary>Remember where a capture group started.</summary>
    GroupOpen,

    /// <summary>Record a capture from the remembered start to the current position.</summary>
    GroupClose,

    /// <summary>Start a loop with zero iterations completed.</summary>
    LoopEnter,

    /// <summary>Decide whether to iterate again or leave the loop at <see cref="Instruction.Target"/>.</summary>
    LoopTest,

    /// <summary>Begin an iteration: count it and mark where it started.</summary>
    LoopIterate,

    /// <summary>Leave a loop.</summary>
    LoopExit,

    /// <summary>Open an atomic group or lookaround; <see cref="Instruction.Target"/> follows its close.</summary>
    BarrierOpen,

    /// <summary>Close the innermost atomic group or lookaround.</summary>
    BarrierClose,

    /// <summary>The whole pattern matched.</summary>
    Match
}

/// <summary>The kind of construct guarded by a barrier.</summary>
internal enum BarrierKind
{
    Atomic,
    PositiveLookaround,
    NegativeLookaround
}

/// <summary>One instruction of a compiled profiling program.</summary>
internal sealed class Instruction
{
    /// <summary>The hotspot index of instructions that do not count as a step.</summary>
    public const int Uncounted = -1;

    /// <summary>The <see cref="Max"/> of a loop without an upper bound.</summary>
    public const int Unbounded = QuantifierNode.Unbounded;

    private Instruction(OpCode op, int hotspot)
    {
        Op = op;
        Hotspot = hotspot;
    }

    public OpCode Op { get; }

    /// <summary>The index of the construct a step is charged to, or <see cref="Uncounted"/>.</summary>
    public int Hotspot { get; }

    /// <summary>The jump, alternative, loop-exit or continuation address.</summary>
    public int Target { get; set; }

    /// <summary>The capture slot of a group or backreference.</summary>
    public int Slot { get; private set; }

    public int Min { get; private set; }

    public int Max { get; private set; }

    public bool Lazy { get; private set; }

    /// <summary>True when the instruction runs inside a lookbehind, which .NET matches right to left.</summary>
    public bool RightToLeft { get; private set; }

    public CharSet Set { get; private set; } = CharSet.Empty;

    public AnchorKind Anchor { get; private set; }

    public RegexOptions Options { get; private set; }

    public BarrierKind Barrier { get; private set; }

    public bool CountsStep => Hotspot != Uncounted;

    public static Instruction ForChar(CharSet set, bool rightToLeft, int hotspot) =>
        new Instruction(OpCode.Char, hotspot) { Set = set, RightToLeft = rightToLeft };

    public static Instruction ForAnchor(AnchorNode anchor, int hotspot) =>
        new Instruction(OpCode.Anchor, hotspot) { Anchor = anchor.AnchorKind, Options = anchor.Options };

    public static Instruction ForBackreference(int slot, BackreferenceNode node, bool rightToLeft, int hotspot) =>
        new Instruction(OpCode.Backreference, hotspot)
        {
            Slot = slot,
            Options = node.Options,
            RightToLeft = rightToLeft
        };

    public static Instruction ForSplit(int hotspot) => new Instruction(OpCode.Split, hotspot);

    public static Instruction ForJump(int target) => new Instruction(OpCode.Jump, Uncounted) { Target = target };

    public static Instruction ForGroup(OpCode op, int slot) => new Instruction(op, Uncounted) { Slot = slot };

    public static Instruction ForLoopTest(QuantifierNode quantifier, int hotspot) =>
        new Instruction(OpCode.LoopTest, hotspot)
        {
            Min = quantifier.Min,
            Max = quantifier.Max,
            Lazy = quantifier.Lazy
        };

    public static Instruction ForBarrier(BarrierKind kind, int hotspot) =>
        new Instruction(OpCode.BarrierOpen, hotspot) { Barrier = kind };

    public static Instruction Simple(OpCode op) => new Instruction(op, Uncounted);
}
