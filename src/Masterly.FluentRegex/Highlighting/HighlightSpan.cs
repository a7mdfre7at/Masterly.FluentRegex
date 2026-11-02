#nullable enable
using System.Collections.Generic;

namespace Masterly.FluentRegex.Highlighting;

/// <summary>A capture group of the highlighted pattern, with its label and color.</summary>
internal sealed class HighlightGroup
{
    public HighlightGroup(int number, string label, int ordinal)
    {
        Number = number;
        Label = label;
        Ordinal = ordinal;
    }

    /// <summary>The group number in the regex.</summary>
    public int Number { get; }

    /// <summary>The group name, or its number for unnamed groups.</summary>
    public string Label { get; }

    /// <summary>The group's position among all groups, used to pick a color.</summary>
    public int Ordinal { get; }
}

/// <summary>A highlighted region: a whole match, or a group capture nested inside one.</summary>
internal sealed class HighlightSpan
{
    public HighlightSpan(int start, int end, HighlightGroup? group)
    {
        Start = start;
        End = end;
        Group = group;
    }

    public int Start { get; }

    public int End { get; set; }

    /// <summary>The capture group, or null for the whole match.</summary>
    public HighlightGroup? Group { get; }

    public bool IsEmpty => Start == End;

    public List<HighlightSpan> Children { get; } = new List<HighlightSpan>();
}
