#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Masterly.FluentRegex.Learning;

/// <summary>
/// The texts that all examples have at one aligned token position, and the ladder of ever more
/// specific ways to match them. Level 0 is the most general option that is still consistent with
/// the examples; the last level enumerates the observed texts exactly.
/// </summary>
internal sealed class AlignedColumn
{
    private readonly IReadOnlyList<IReadOnlyList<LearnedElement>> _ladder;

    private AlignedColumn(IReadOnlyList<IReadOnlyList<LearnedElement>> ladder) => _ladder = ladder;

    /// <summary>The highest specialization level of this column.</summary>
    public int MaxLevel => _ladder.Count - 1;

    /// <summary>Returns the elements for a specialization level.</summary>
    public IReadOnlyList<LearnedElement> At(int level) => _ladder[level];

    /// <summary>
    /// Creates the column for the given texts, or returns null when no single class covers them.
    /// </summary>
    public static AlignedColumn? Create(IReadOnlyList<string> texts, TokenCategory category)
    {
        List<string> distinct = texts.Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToList();
        if (distinct.Count == 1)
        {
            return Single(LearnedElement.Literal(distinct[0]));
        }

        if (category == TokenCategory.Symbol)
        {
            return Single(LearnedElement.Set(string.Concat(distinct), 1, 1));
        }

        if (!LearnedClasses.TryCover(distinct, out LearnedClass covering))
        {
            return null;
        }

        return new AlignedColumn(BuildLadder(distinct, covering));
    }

    private static AlignedColumn Single(LearnedElement element) =>
        new AlignedColumn(new[] { (IReadOnlyList<LearnedElement>)new[] { element } });

    private static IReadOnlyList<IReadOnlyList<LearnedElement>> BuildLadder(IReadOnlyList<string> distinct, LearnedClass covering)
    {
        int min = distinct.Min(t => t.Length);
        int max = distinct.Max(t => t.Length);
        List<IReadOnlyList<LearnedElement>> ladder = new List<IReadOnlyList<LearnedElement>>
        {
            new[] { LearnedElement.OfClass(covering, min, max) }
        };

        if (min == max)
        {
            AddIfNew(ladder, CharacterColumns(distinct, chars => LearnedElement.OfClass(covering, 1, 1)));
            AddIfNew(ladder, CharacterColumns(distinct, chars => LearnedElement.Set(chars, 1, 1)));
        }

        AddIfNew(ladder, new[] { LearnedElement.Choice(distinct) });
        return ladder;
    }

    /// <summary>
    /// Matches equal-length texts character by character: a column where all texts agree becomes a
    /// literal, any other column becomes the element produced by <paramref name="differing"/>.
    /// </summary>
    private static IReadOnlyList<LearnedElement> CharacterColumns(IReadOnlyList<string> texts, Func<string, LearnedElement> differing)
    {
        int length = texts[0].Length;
        List<LearnedElement> elements = new List<LearnedElement>(length);
        for (int column = 0; column < length; column++)
        {
            string chars = DistinctCharsAt(texts, column);
            elements.Add(chars.Length == 1 ? LearnedElement.Literal(chars) : differing(chars));
        }

        return LearnedElement.Simplify(elements);
    }

    private static string DistinctCharsAt(IReadOnlyList<string> texts, int column)
    {
        SortedSet<char> chars = new SortedSet<char>();
        foreach (string text in texts)
        {
            chars.Add(text[column]);
        }

        StringBuilder result = new StringBuilder(chars.Count);
        foreach (char c in chars)
        {
            result.Append(c);
        }

        return result.ToString();
    }

    private static void AddIfNew(List<IReadOnlyList<LearnedElement>> ladder, IReadOnlyList<LearnedElement> level)
    {
        string rendered = Render(level);
        if (ladder.All(existing => !string.Equals(Render(existing), rendered, StringComparison.Ordinal)))
        {
            ladder.Add(level);
        }
    }

    private static string Render(IReadOnlyList<LearnedElement> elements) => LearnedElement.ToPattern(elements).ToString();
}
