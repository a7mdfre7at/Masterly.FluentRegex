#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Masterly.FluentRegex.Learning;

/// <summary>The shape of one learned element.</summary>
internal enum LearnedElementKind
{
    Literal,
    Class,
    Set,
    Choice
}

/// <summary>
/// One element of a learned pattern: literal text, a counted class or set, or a choice between texts.
/// </summary>
internal sealed class LearnedElement
{
    private LearnedElement(LearnedElementKind kind, string text, LearnedClass learnedClass, int min, int max, IReadOnlyList<string> choices)
    {
        Kind = kind;
        Text = text;
        Class = learnedClass;
        Min = min;
        Max = max;
        Choices = choices;
    }

    public LearnedElementKind Kind { get; }

    /// <summary>The literal text, or the characters of a set.</summary>
    public string Text { get; }

    public LearnedClass Class { get; }

    public int Min { get; }

    public int Max { get; }

    public IReadOnlyList<string> Choices { get; }

    public static LearnedElement Literal(string text) =>
        new LearnedElement(LearnedElementKind.Literal, text, LearnedClass.Word, 1, 1, Array.Empty<string>());

    public static LearnedElement OfClass(LearnedClass learnedClass, int min, int max) =>
        new LearnedElement(LearnedElementKind.Class, string.Empty, learnedClass, min, max, Array.Empty<string>());

    public static LearnedElement Set(string characters, int min, int max) =>
        new LearnedElement(LearnedElementKind.Set, characters, LearnedClass.Word, min, max, Array.Empty<string>());

    public static LearnedElement Choice(IReadOnlyList<string> choices) =>
        new LearnedElement(LearnedElementKind.Choice, string.Empty, LearnedClass.Word, 1, 1, choices);

    /// <summary>
    /// Returns the element that matches this element followed by <paramref name="next"/>, when one exists:
    /// adjacent literals concatenate and adjacent counted copies of the same class add their counts.
    /// </summary>
    public LearnedElement? TryMergeWith(LearnedElement next)
    {
        if (Kind != next.Kind)
        {
            return null;
        }

        switch (Kind)
        {
            case LearnedElementKind.Literal:
                return Literal($"{Text}{next.Text}");
            case LearnedElementKind.Class when Class == next.Class:
                return OfClass(Class, Min + next.Min, Max + next.Max);
            case LearnedElementKind.Set when string.Equals(Text, next.Text, StringComparison.Ordinal):
                return Set(Text, Min + next.Min, Max + next.Max);
            default:
                return null;
        }
    }

    /// <summary>Appends this element to <paramref name="pattern"/> with the fluent API.</summary>
    public Pattern AppendTo(Pattern pattern)
    {
        switch (Kind)
        {
            case LearnedElementKind.Literal:
                return pattern.Literal(Text);
            case LearnedElementKind.Choice:
                return pattern.OneOf(Choices.ToArray());
            case LearnedElementKind.Set:
                return ApplyCount(pattern.AnyOf(Text));
            default:
                return ApplyCount(LearnedClasses.AppendTo(pattern, Class));
        }
    }

    private Pattern ApplyCount(Pattern pattern)
    {
        if (Min == Max)
        {
            return Min == 1 ? pattern : pattern.Repeat.Times(Min);
        }

        return pattern.Repeat.Times(Min, Max);
    }

    /// <summary>Merges adjacent elements where possible, so the learned pattern stays short.</summary>
    public static IReadOnlyList<LearnedElement> Simplify(IEnumerable<LearnedElement> elements)
    {
        List<LearnedElement> simplified = new List<LearnedElement>();
        foreach (LearnedElement element in elements)
        {
            LearnedElement? merged = simplified.Count == 0 ? null : simplified[simplified.Count - 1].TryMergeWith(element);
            if (merged == null)
            {
                simplified.Add(element);
            }
            else
            {
                simplified[simplified.Count - 1] = merged;
            }
        }

        return simplified;
    }

    /// <summary>Builds a pattern from a sequence of elements, without anchors.</summary>
    public static Pattern ToPattern(IEnumerable<LearnedElement> elements)
    {
        Pattern pattern = Pattern.With;
        foreach (LearnedElement element in Simplify(elements))
        {
            pattern = element.AppendTo(pattern);
        }

        return pattern;
    }
}
