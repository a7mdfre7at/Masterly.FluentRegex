#nullable enable
using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Profiling;

/// <summary>A pattern compiled into backtracking instructions, plus the constructs steps are charged to.</summary>
internal sealed class CompiledProgram
{
    public CompiledProgram(IReadOnlyList<Instruction> code, IReadOnlyList<RegexNode> hotspots, int slotCount)
    {
        Code = code;
        Hotspots = hotspots;
        SlotCount = slotCount;
    }

    public IReadOnlyList<Instruction> Code { get; }

    /// <summary>The syntax nodes that steps and backtracks are attributed to, by hotspot index.</summary>
    public IReadOnlyList<RegexNode> Hotspots { get; }

    /// <summary>The number of distinct capture groups.</summary>
    public int SlotCount { get; }
}
