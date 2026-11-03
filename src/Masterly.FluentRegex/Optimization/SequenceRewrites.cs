#nullable enable

using System;
using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Optimization;

/// <summary>
/// Merges runs of the same single-character atom, such as <c>\d\d\d</c> into <c>\d{3}</c> or
/// <c>aa*</c> into <c>a+</c>. Such a run tries total lengths in the same order as the merged
/// quantifier, so matches are unchanged.
/// </summary>
internal static class SequenceRewrites
{
    private const int MinimumRun = 2;

    /// <summary>Adds every candidate rewrite of <paramref name="sequence"/> to <paramref name="found"/>.</summary>
    public static void Collect(SequenceNode sequence, string text, List<RegexRewrite> found)
    {
        List<RegexNode> items = sequence.Items;
        int start = 0;
        while (start < items.Count)
        {
            int end = start;
            Repetition? run = Repetition.Of(items[start]);
            if (run == null)
            {
                start++;
                continue;
            }

            for (end = start + 1; end < items.Count; end++)
            {
                Repetition? next = Repetition.Of(items[end]);
                if (next == null || !run.CanMergeWith(next))
                {
                    break;
                }
                run = run.MergeWith(next);
            }

            if (end - start >= MinimumRun)
            {
                AddRun(items, start, end, run, text, found);
            }
            start = end;
        }
    }

    private static void AddRun(List<RegexNode> items, int start, int end, Repetition run, string text, List<RegexRewrite> found)
    {
        int spanStart = items[start].Start;
        int spanEnd = items[end - 1].End;
        string original = text.Substring(spanStart, spanEnd - spanStart);
        string replacement = $"{run.Atom}{SyntaxFacts.Quantifier(run.Min, run.Max, run.Lazy)}";
        if (replacement.Length < original.Length)
        {
            found.Add(new RegexRewrite(spanStart, spanEnd, replacement, $"Merged repeated atoms '{original}' into '{replacement}'"));
        }
    }

    /// <summary>A single-character atom with a repeat range.</summary>
    private sealed class Repetition
    {
        private Repetition(string atom, RegexNode node, int min, int max, bool? lazy)
        {
            Atom = atom;
            Node = node;
            Min = min;
            Max = max;
            Laziness = lazy;
        }

        public string Atom { get; }

        public RegexNode Node { get; }

        public int Min { get; }

        public int Max { get; }

        /// <summary>The laziness of the variable parts, or null when every part has a fixed count.</summary>
        public bool? Laziness { get; }

        public bool Lazy => Laziness == true;

        public static Repetition? Of(RegexNode item)
        {
            if (SyntaxFacts.IsSingleChar(item))
            {
                return new Repetition(item.Source, item, 1, 1, null);
            }
            if (item is QuantifierNode quantifier && SyntaxFacts.IsSingleChar(quantifier.Body) && quantifier.Max != 0)
            {
                bool? lazy = quantifier.Min == quantifier.Max ? (bool?)null : quantifier.Lazy;
                return new Repetition(quantifier.Body.Source, quantifier.Body, quantifier.Min, quantifier.Max, lazy);
            }
            return null;
        }

        public bool CanMergeWith(Repetition next)
        {
            bool sameAtom = string.Equals(Atom, next.Atom, StringComparison.Ordinal) && Node.Options == next.Node.Options;
            bool compatibleLaziness = Laziness == null || next.Laziness == null || Laziness == next.Laziness;
            return sameAtom && compatibleLaziness;
        }

        public Repetition MergeWith(Repetition next)
        {
            bool unbounded = Max == QuantifierNode.Unbounded || next.Max == QuantifierNode.Unbounded;
            int max = unbounded ? QuantifierNode.Unbounded : Max + next.Max;
            return new Repetition(Atom, Node, Min + next.Min, max, Laziness ?? next.Laziness);
        }
    }
}
