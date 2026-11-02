#nullable enable
using System.Collections.Generic;

namespace Masterly.FluentRegex.Like;

/// <summary>
/// Keeps compiled LIKE parts a superset when the database counts characters differently from .NET.
/// A .NET regex sees UTF-16 code units, so a supplementary character such as an emoji is two
/// characters to <c>.</c>; PostgreSQL, MySQL, SQLite and SQL Server's supplementary-character-aware
/// collations (<c>_SC</c>, UTF-8) count it as one, so <c>__</c> rejects an emoji that <c>^..\z</c>
/// accepts. Two adjacent positions that can each match a surrogate code unit may therefore be one
/// database character: a run of <c>k</c> such positions spans between <c>ceil(k/2)</c> and <c>k</c>
/// characters and is written as <c>ceil(k/2)</c> times <c>_</c> followed by <c>%</c>. A single such
/// position next to <c>%</c>, text or a position that cannot match a surrogate cannot share a
/// character with its neighbour (a <c>%</c> simply gives the other half up), so it stays as it is.
/// SQL Server's case-insensitive legacy collations (version 80, such as
/// <c>SQL_Latin1_General_CP1_CI_AS</c>) give supplementary characters no weight at all, so <c>_</c>
/// never matches one there; under <see cref="LikeCollation.CaseInsensitive"/> on SQL Server every such
/// position becomes <c>%</c>.
/// </summary>
internal static class LikeCodePoints
{
    /// <summary>
    /// Rewrites runs of surrogate-capable positions; lone surrogates in text (which a UTF-8 database
    /// cannot even receive) become such positions first. <paramref name="widened"/> tells whether anything changed.
    /// </summary>
    public static IReadOnlyList<LikePart> Widen(IReadOnlyList<LikePart> parts, bool noWeightCollation, out bool widened)
    {
        List<LikePart> positions = SplitLoneSurrogates(parts, out widened);
        List<LikePart> result = new List<LikePart>();
        int i = 0;
        while (i < positions.Count)
        {
            int run = RunLength(positions, i);
            if (run == 0)
            {
                result.Add(positions[i]);
                i++;
                continue;
            }

            widened |= AddRun(result, positions, i, run, noWeightCollation);
            i += run;
        }

        return result;
    }

    private static bool AddRun(List<LikePart> result, List<LikePart> positions, int start, int run, bool noWeightCollation)
    {
        if (noWeightCollation)
        {
            result.Add(LikePart.AnyString);
            return true;
        }

        if (run == 1)
        {
            result.Add(positions[start]);
            return false;
        }

        int characters = (run + 1) / 2;
        for (int k = 0; k < characters; k++)
        {
            result.Add(LikePart.AnyChar);
        }

        result.Add(LikePart.AnyString);
        return true;
    }

    private static int RunLength(List<LikePart> positions, int start)
    {
        int end = start;
        while (end < positions.Count && positions[end].CanMatchSurrogate)
        {
            end++;
        }

        return end - start;
    }

    /// <summary>Splits text parts so every lone surrogate becomes a <c>_</c> that can match a surrogate.</summary>
    private static List<LikePart> SplitLoneSurrogates(IReadOnlyList<LikePart> parts, out bool split)
    {
        List<LikePart> result = new List<LikePart>();
        split = false;
        foreach (LikePart part in parts)
        {
            if (part.Kind == LikePartKind.Text)
            {
                split |= SplitText(result, part.Text);
            }
            else
            {
                result.Add(part);
            }
        }

        return result;
    }

    /// <summary>Adds <paramref name="text"/> with its lone surrogates as <c>_</c>; returns true when there were any.</summary>
    private static bool SplitText(List<LikePart> result, string text)
    {
        int start = 0;
        int i = 0;
        while (i < text.Length)
        {
            if (char.IsSurrogatePair(text, i))
            {
                i += 2;
            }
            else if (char.IsSurrogate(text[i]))
            {
                AddText(result, text, start, i);
                result.Add(LikePart.AnyChar);
                start = ++i;
            }
            else
            {
                i++;
            }
        }

        if (start == 0)
        {
            result.Add(LikePart.OfText(text)); // nothing was split
            return false;
        }

        AddText(result, text, start, text.Length);
        return true;
    }

    private static void AddText(List<LikePart> result, string text, int start, int end)
    {
        if (end > start)
        {
            result.Add(LikePart.OfText(text.Substring(start, end - start)));
        }
    }
}
