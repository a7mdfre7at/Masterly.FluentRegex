#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Profiling;

/// <summary>
/// Executes a <see cref="CompiledProgram"/> with an explicit backtracking trail instead of recursion,
/// so long inputs and exponential patterns cannot overflow the call stack. Every tested instruction is a
/// step and every resumed choice point is a backtrack, both charged to the construct that caused them.
/// </summary>
internal sealed class BacktrackingInterpreter
{
    private const int NoPosition = -1;

    private readonly IReadOnlyList<Instruction> _code;
    private readonly string _input;
    private readonly long _budget;
    private readonly List<TrailEntry> _trail = new List<TrailEntry>();
    private readonly List<LoopFrame> _frames = new List<LoopFrame>();
    private readonly int[] _openPositions;
    private readonly int[] _captureStarts;
    private readonly int[] _captureEnds;
    private readonly long[] _hotspotSteps;
    private readonly long[] _hotspotBacktracks;

    private long _steps;
    private long _backtracks;
    private int _pc;
    private int _position;

    public BacktrackingInterpreter(CompiledProgram program, string input, long budget)
    {
        _code = program.Code;
        _input = input;
        _budget = budget;
        _openPositions = new int[program.SlotCount];
        _captureStarts = new int[program.SlotCount];
        _captureEnds = new int[program.SlotCount];
        _hotspotSteps = new long[program.Hotspots.Count];
        _hotspotBacktracks = new long[program.Hotspots.Count];
    }

    private enum AttemptOutcome
    {
        Matched,
        Failed,
        BudgetExhausted
    }

    private enum StepResult
    {
        Continue,
        Fail,
        Matched
    }

    /// <summary>Searches for the leftmost match, trying each start position in turn.</summary>
    public InterpreterResult Run()
    {
        for (int start = 0; start <= _input.Length; start++)
        {
            AttemptOutcome outcome = Attempt(start);
            if (outcome == AttemptOutcome.Matched)
            {
                return Result(new MatchSpan(true, start, _position - start), budgetExhausted: false);
            }

            if (outcome == AttemptOutcome.BudgetExhausted)
            {
                return Result(MatchSpan.None, budgetExhausted: true);
            }
        }

        return Result(MatchSpan.None, budgetExhausted: false);
    }

    private InterpreterResult Result(MatchSpan span, bool budgetExhausted) =>
        new InterpreterResult(span, _steps, _backtracks, budgetExhausted, _hotspotSteps, _hotspotBacktracks);

    private AttemptOutcome Attempt(int start)
    {
        Reset(start);
        while (true)
        {
            Instruction instruction = _code[_pc];
            if (instruction.CountsStep && !TryCountStep(instruction.Hotspot))
            {
                return AttemptOutcome.BudgetExhausted;
            }

            StepResult result = Execute(instruction);
            if (result == StepResult.Matched)
            {
                return AttemptOutcome.Matched;
            }

            if (result == StepResult.Fail && !Backtrack())
            {
                return AttemptOutcome.Failed;
            }
        }
    }

    private void Reset(int start)
    {
        _trail.Clear();
        _frames.Clear();
        Array.Fill(_openPositions, NoPosition);
        Array.Fill(_captureStarts, NoPosition);
        Array.Fill(_captureEnds, NoPosition);
        _pc = 0;
        _position = start;
    }

    private bool TryCountStep(int hotspot)
    {
        if (_steps >= _budget)
        {
            return false;
        }

        _steps++;
        _hotspotSteps[hotspot]++;
        return true;
    }

    private StepResult Execute(Instruction instruction)
    {
        switch (instruction.Op)
        {
            case OpCode.Char:
                return MatchChar(instruction);
            case OpCode.Anchor:
                return NextIf(AnchorEvaluator.IsSatisfied(instruction.Anchor, instruction.Options, _input, _position));
            case OpCode.Backreference:
                return MatchBackreference(instruction);
            case OpCode.Split:
                PushBranch(instruction.Target, instruction.Hotspot);
                return Next();
            case OpCode.Jump:
                _pc = instruction.Target;
                return StepResult.Continue;
            case OpCode.GroupOpen:
                SetOpen(instruction.Slot, _position);
                return Next();
            case OpCode.GroupClose:
                CloseGroup(instruction.Slot);
                return Next();
            default:
                return ExecuteStructure(instruction);
        }
    }

    private StepResult ExecuteStructure(Instruction instruction)
    {
        switch (instruction.Op)
        {
            case OpCode.LoopEnter:
                PushFrame(new LoopFrame(0, LoopFrame.NoMark));
                return Next();
            case OpCode.LoopTest:
                return TestLoop(instruction);
            case OpCode.LoopIterate:
                SetTopFrame(new LoopFrame(TopFrame().Count + 1, _position));
                return Next();
            case OpCode.LoopExit:
                PopFrame();
                return Next();
            case OpCode.BarrierOpen:
                _trail.Add(new TrailEntry(TrailKind.Barrier, _pc, _position, 0));
                return Next();
            case OpCode.BarrierClose:
                return CloseBarrier();
            default:
                return StepResult.Matched;
        }
    }

    private StepResult Next()
    {
        _pc++;
        return StepResult.Continue;
    }

    private StepResult NextIf(bool condition) => condition ? Next() : StepResult.Fail;

    private StepResult MatchChar(Instruction instruction)
    {
        int index = instruction.RightToLeft ? _position - 1 : _position;
        if (index < 0 || index >= _input.Length || !instruction.Set.Contains(_input[index]))
        {
            return StepResult.Fail;
        }

        _position = instruction.RightToLeft ? index : index + 1;
        return Next();
    }

    private StepResult MatchBackreference(Instruction instruction)
    {
        int start = _captureStarts[instruction.Slot];
        if (start == NoPosition)
        {
            // ECMAScript treats a reference to a group that has not captured as matching the empty string.
            return NextIf((instruction.Options & RegexOptions.ECMAScript) != 0);
        }

        int length = _captureEnds[instruction.Slot] - start;
        int from = instruction.RightToLeft ? _position - length : _position;
        if (from < 0 || from + length > _input.Length || !TextEquals(start, from, length, instruction.Options))
        {
            return StepResult.Fail;
        }

        _position = instruction.RightToLeft ? from : from + length;
        return Next();
    }

    private bool TextEquals(int first, int second, int length, RegexOptions options)
    {
        bool ignoreCase = (options & RegexOptions.IgnoreCase) != 0;
        for (int i = 0; i < length; i++)
        {
            char expected = _input[first + i];
            char actual = _input[second + i];
            if (expected != actual && !(ignoreCase && CaseEquivalent(expected, actual, options)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CaseEquivalent(char expected, char actual, RegexOptions options) =>
        CharSet.ForSource($@"\u{(int)expected:X4}", options).Contains(actual);

    private StepResult TestLoop(Instruction instruction)
    {
        LoopFrame frame = TopFrame();
        if (frame.Count < instruction.Min)
        {
            return Next();
        }

        // Like .NET, a loop stops once an iteration beyond the minimum matched the empty string.
        bool emptyIteration = frame.Count > 0 && frame.Mark == _position;
        bool atMaximum = instruction.Max != Instruction.Unbounded && frame.Count >= instruction.Max;
        if (atMaximum || emptyIteration)
        {
            _pc = instruction.Target;
            return StepResult.Continue;
        }

        if (instruction.Lazy)
        {
            PushBranch(_pc + 1, instruction.Hotspot);
            _pc = instruction.Target;
            return StepResult.Continue;
        }

        PushBranch(instruction.Target, instruction.Hotspot);
        return Next();
    }

    private StepResult CloseBarrier()
    {
        int index = TopBarrierIndex();
        TrailEntry barrier = _trail[index];
        Instruction open = _code[barrier.A];
        if (open.Barrier == BarrierKind.NegativeLookaround)
        {
            UnwindTo(index);
            return StepResult.Fail;
        }

        DiscardChoicePoints(index);
        if (open.Barrier == BarrierKind.PositiveLookaround)
        {
            _position = barrier.B;
        }

        return Next();
    }

    private int TopBarrierIndex()
    {
        for (int i = _trail.Count - 1; i >= 0; i--)
        {
            if (_trail[i].Kind == TrailKind.Barrier)
            {
                return i;
            }
        }

        throw new InvalidOperationException("The profiling program closed a group that was never opened.");
    }

    /// <summary>
    /// Drops the choice points of a completed atomic group or lookaround but keeps its state changes,
    /// so backtracking past the group still reverts the captures it made.
    /// </summary>
    private void DiscardChoicePoints(int barrierIndex)
    {
        int write = barrierIndex;
        for (int read = barrierIndex; read < _trail.Count; read++)
        {
            if (_trail[read].IsUndo)
            {
                _trail[write] = _trail[read];
                write++;
            }
        }

        _trail.RemoveRange(write, _trail.Count - write);
    }

    private void UnwindTo(int index)
    {
        while (_trail.Count > index)
        {
            TrailEntry entry = Pop();
            if (entry.IsUndo)
            {
                Undo(entry);
            }
        }
    }

    private bool Backtrack()
    {
        while (_trail.Count > 0)
        {
            TrailEntry entry = Pop();
            if (entry.Kind == TrailKind.Branch)
            {
                Resume(entry);
                return true;
            }

            if (entry.Kind == TrailKind.Barrier)
            {
                if (TrySucceedNegativeLookaround(entry))
                {
                    return true;
                }
            }
            else
            {
                Undo(entry);
            }
        }

        return false;
    }

    private void Resume(TrailEntry branch)
    {
        _pc = branch.A;
        _position = branch.B;
        _backtracks++;
        _hotspotBacktracks[branch.C]++;
    }

    /// <summary>A negative lookaround whose body ran out of alternatives succeeds at its start position.</summary>
    private bool TrySucceedNegativeLookaround(TrailEntry barrier)
    {
        Instruction open = _code[barrier.A];
        if (open.Barrier != BarrierKind.NegativeLookaround)
        {
            return false;
        }

        _position = barrier.B;
        _pc = open.Target;
        return true;
    }

    private void Undo(TrailEntry entry)
    {
        switch (entry.Kind)
        {
            case TrailKind.UndoCapture:
                _captureStarts[entry.A] = entry.B;
                _captureEnds[entry.A] = entry.C;
                break;
            case TrailKind.UndoOpen:
                _openPositions[entry.A] = entry.B;
                break;
            case TrailKind.UndoFramePush:
                _frames.RemoveAt(_frames.Count - 1);
                break;
            case TrailKind.UndoFramePop:
                _frames.Add(new LoopFrame(entry.A, entry.B));
                break;
            default:
                _frames[_frames.Count - 1] = new LoopFrame(entry.A, entry.B);
                break;
        }
    }

    private TrailEntry Pop()
    {
        int last = _trail.Count - 1;
        TrailEntry entry = _trail[last];
        _trail.RemoveAt(last);
        return entry;
    }

    private void PushBranch(int target, int hotspot) =>
        _trail.Add(new TrailEntry(TrailKind.Branch, target, _position, hotspot));

    private void SetOpen(int slot, int position)
    {
        _trail.Add(new TrailEntry(TrailKind.UndoOpen, slot, _openPositions[slot], 0));
        _openPositions[slot] = position;
    }

    private void CloseGroup(int slot)
    {
        // Inside a lookbehind the group opens at its right edge, so order the two positions.
        int open = _openPositions[slot];
        _trail.Add(new TrailEntry(TrailKind.UndoCapture, slot, _captureStarts[slot], _captureEnds[slot]));
        _captureStarts[slot] = Math.Min(open, _position);
        _captureEnds[slot] = Math.Max(open, _position);
    }

    private LoopFrame TopFrame() => _frames[_frames.Count - 1];

    private void PushFrame(LoopFrame frame)
    {
        _frames.Add(frame);
        _trail.Add(new TrailEntry(TrailKind.UndoFramePush, 0, 0, 0));
    }

    private void PopFrame()
    {
        LoopFrame frame = TopFrame();
        _frames.RemoveAt(_frames.Count - 1);
        _trail.Add(new TrailEntry(TrailKind.UndoFramePop, frame.Count, frame.Mark, 0));
    }

    private void SetTopFrame(LoopFrame frame)
    {
        LoopFrame previous = TopFrame();
        _trail.Add(new TrailEntry(TrailKind.UndoFrameSet, previous.Count, previous.Mark, 0));
        _frames[_frames.Count - 1] = frame;
    }
}
