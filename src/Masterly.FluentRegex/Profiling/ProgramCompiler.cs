#nullable enable
using System;
using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Profiling;

/// <summary>
/// Compiles a syntax tree into a backtracking program that follows .NET's matching order:
/// alternatives left to right, greedy loops iterate before exiting, lazy loops exit before iterating,
/// and lookbehinds run right to left from the current position.
/// </summary>
internal sealed class ProgramCompiler
{
    private readonly List<Instruction> _code = new List<Instruction>();
    private readonly List<RegexNode> _hotspots = new List<RegexNode>();
    private readonly Dictionary<RegexNode, int> _hotspotIndexes = new Dictionary<RegexNode, int>();
    private readonly Dictionary<int, int> _slotsByNumber = new Dictionary<int, int>();
    private readonly Dictionary<string, int> _numbersByName = new Dictionary<string, int>(StringComparer.Ordinal);

    private ProgramCompiler(RegexTree tree)
    {
        foreach (GroupNode group in tree.Groups)
        {
            RegisterGroup(group);
        }
    }

    /// <summary>Compiles <paramref name="tree"/>. Call <see cref="UnsupportedConstructs.Ensure"/> first.</summary>
    public static CompiledProgram Compile(RegexTree tree)
    {
        ProgramCompiler compiler = new ProgramCompiler(tree);
        compiler.Emit(tree.Root, rightToLeft: false, owner: null);
        compiler.Add(Instruction.Simple(OpCode.Match));
        return new CompiledProgram(compiler._code, compiler._hotspots, compiler._slotsByNumber.Count);
    }

    private void RegisterGroup(GroupNode group)
    {
        if (!group.IsCapturing)
        {
            return;
        }

        if (!_slotsByNumber.ContainsKey(group.Number))
        {
            _slotsByNumber[group.Number] = _slotsByNumber.Count;
        }

        if (group.Name != null && !_numbersByName.ContainsKey(group.Name))
        {
            _numbersByName[group.Name] = group.Number;
        }
    }

    /// <param name="node">The node to compile.</param>
    /// <param name="rightToLeft">True inside a lookbehind.</param>
    /// <param name="owner">The quantifier whose single-atom body this is, so steps are charged to it.</param>
    private void Emit(RegexNode node, bool rightToLeft, RegexNode? owner)
    {
        switch (node)
        {
            case SequenceNode sequence:
                EmitSequence(sequence, rightToLeft);
                break;
            case AlternationNode alternation:
                EmitAlternation(alternation, rightToLeft);
                break;
            case LiteralNode or CharClassNode:
                Add(Instruction.ForChar(CharSet.ForNode(node), rightToLeft, Hotspot(owner ?? node)));
                break;
            case AnchorNode anchor:
                Add(Instruction.ForAnchor(anchor, Hotspot(owner ?? anchor)));
                break;
            case BackreferenceNode reference:
                int slot = SlotOf(reference);
                Add(Instruction.ForBackreference(slot, reference, rightToLeft, Hotspot(owner ?? reference)));
                break;
            case GroupNode group:
                EmitGroup(group, rightToLeft);
                break;
            case QuantifierNode quantifier:
                EmitLoop(quantifier, rightToLeft);
                break;
            case ConditionalNode conditional:
                throw UnsupportedConstructs.Conditional(conditional);
            default:
                // Empty, comment and inline-option nodes match the empty string.
                break;
        }
    }

    private void EmitSequence(SequenceNode sequence, bool rightToLeft)
    {
        int count = sequence.Items.Count;
        for (int i = 0; i < count; i++)
        {
            Emit(sequence.Items[rightToLeft ? count - 1 - i : i], rightToLeft, owner: null);
        }
    }

    private void EmitAlternation(AlternationNode alternation, bool rightToLeft)
    {
        int hotspot = Hotspot(alternation);
        List<int> exitJumps = new List<int>();
        int last = alternation.Branches.Count - 1;
        for (int i = 0; i < last; i++)
        {
            int split = Add(Instruction.ForSplit(hotspot));
            Emit(alternation.Branches[i], rightToLeft, owner: null);
            exitJumps.Add(Add(Instruction.ForJump(0)));
            _code[split].Target = _code.Count;
        }

        Emit(alternation.Branches[last], rightToLeft, owner: null);
        foreach (int jump in exitJumps)
        {
            _code[jump].Target = _code.Count;
        }
    }

    private void EmitGroup(GroupNode group, bool rightToLeft)
    {
        switch (group.GroupKind)
        {
            case GroupKind.Capturing:
            case GroupKind.Named:
                EmitCapture(group, rightToLeft);
                break;
            case GroupKind.Atomic:
                EmitBarrier(group, BarrierKind.Atomic, rightToLeft);
                break;
            case GroupKind.Lookahead:
                EmitBarrier(group, BarrierKind.PositiveLookaround, bodyRightToLeft: false);
                break;
            case GroupKind.NegativeLookahead:
                EmitBarrier(group, BarrierKind.NegativeLookaround, bodyRightToLeft: false);
                break;
            case GroupKind.Lookbehind:
                EmitBarrier(group, BarrierKind.PositiveLookaround, bodyRightToLeft: true);
                break;
            case GroupKind.NegativeLookbehind:
                EmitBarrier(group, BarrierKind.NegativeLookaround, bodyRightToLeft: true);
                break;
            case GroupKind.Balancing:
                throw UnsupportedConstructs.Balancing(group);
            default:
                Emit(group.Body, rightToLeft, owner: null);
                break;
        }
    }

    private void EmitCapture(GroupNode group, bool rightToLeft)
    {
        int slot = _slotsByNumber[group.Number];
        Add(Instruction.ForGroup(OpCode.GroupOpen, slot));
        Emit(group.Body, rightToLeft, owner: null);
        Add(Instruction.ForGroup(OpCode.GroupClose, slot));
    }

    private void EmitBarrier(GroupNode group, BarrierKind kind, bool bodyRightToLeft)
    {
        int open = Add(Instruction.ForBarrier(kind, Hotspot(group)));
        Emit(group.Body, bodyRightToLeft, owner: null);
        Add(Instruction.Simple(OpCode.BarrierClose));
        _code[open].Target = _code.Count;
    }

    private void EmitLoop(QuantifierNode quantifier, bool rightToLeft)
    {
        RegexNode? owner = IsAtom(quantifier.Body) ? quantifier : null;
        Add(Instruction.Simple(OpCode.LoopEnter));
        int test = Add(Instruction.ForLoopTest(quantifier, Hotspot(quantifier)));
        Add(Instruction.Simple(OpCode.LoopIterate));
        Emit(quantifier.Body, rightToLeft, owner);
        Add(Instruction.ForJump(test));
        _code[test].Target = Add(Instruction.Simple(OpCode.LoopExit));
    }

    private static bool IsAtom(RegexNode node) =>
        node is LiteralNode || node is CharClassNode || node is AnchorNode || node is BackreferenceNode;

    private int SlotOf(BackreferenceNode reference)
    {
        int number = reference.Number;
        if (reference.Name != null && !_numbersByName.TryGetValue(reference.Name, out number))
        {
            throw new NotSupportedException($"The backreference '{reference.Source}' refers to an unknown group.");
        }

        if (!_slotsByNumber.TryGetValue(number, out int slot))
        {
            throw new NotSupportedException($"The backreference '{reference.Source}' refers to an unknown group.");
        }

        return slot;
    }

    private int Hotspot(RegexNode node)
    {
        if (!_hotspotIndexes.TryGetValue(node, out int index))
        {
            index = _hotspots.Count;
            _hotspots.Add(node);
            _hotspotIndexes[node] = index;
        }

        return index;
    }

    private int Add(Instruction instruction)
    {
        _code.Add(instruction);
        return _code.Count - 1;
    }
}
