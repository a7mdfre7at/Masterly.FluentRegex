#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Optimization;

/// <summary>
/// Rewrites of alternations that never reorder branches: removing a later duplicate, merging
/// adjacent single-character branches into a class, and factoring a common literal prefix out of
/// adjacent branches.
/// </summary>
internal static class AlternationRewrites
{
    private const int MinimumRun = 2;

    /// <summary>Adds every candidate rewrite of <paramref name="alternation"/> to <paramref name="found"/>.</summary>
    public static void Collect(AlternationNode alternation, string text, List<RegexRewrite> found)
    {
        foreach (RegexNode branch in alternation.Branches)
        {
            if (branch.Options != alternation.Options || SyntaxFacts.HasInlineOptions(branch))
            {
                return;
            }
        }

        string[] branchTexts = BranchTexts(alternation, text);
        CollectDuplicates(alternation, branchTexts, found);
        CollectClassRuns(alternation, branchTexts, found);
        CollectPrefixRuns(alternation, branchTexts, found);
    }

    private static string[] BranchTexts(AlternationNode alternation, string text)
    {
        string[] texts = new string[alternation.Branches.Count];
        for (int i = 0; i < texts.Length; i++)
        {
            RegexNode branch = alternation.Branches[i];
            texts[i] = text.Substring(branch.Start, branch.Length);
        }
        return texts;
    }

    /// <summary>
    /// A later branch identical to an earlier one can never succeed where the earlier one failed:
    /// it is tried at the same position with the same state.
    /// </summary>
    private static void CollectDuplicates(AlternationNode alternation, string[] branchTexts, List<RegexRewrite> found)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < branchTexts.Length; i++)
        {
            if (seen.Add(branchTexts[i]) || SyntaxFacts.ContainsCapture(alternation.Branches[i]))
            {
                continue;
            }

            List<string> kept = new List<string>(branchTexts);
            kept.RemoveAt(i);
            string description = $"Removed duplicate alternation branch '{branchTexts[i]}'";
            found.Add(Replace(alternation, kept, description));
            return;
        }
    }

    /// <summary>Adjacent single-character branches all consume exactly one character, so their order is irrelevant.</summary>
    private static void CollectClassRuns(AlternationNode alternation, string[] branchTexts, List<RegexRewrite> found)
    {
        int start = 0;
        while (start < branchTexts.Length)
        {
            int end = start;
            while (end < branchTexts.Length && SyntaxFacts.ClassMemberText(alternation.Branches[end]) != null)
            {
                end++;
            }

            if (end - start >= MinimumRun)
            {
                AddClassRun(alternation, branchTexts, start, end, found);
            }
            start = end + 1;
        }
    }

    private static void AddClassRun(AlternationNode alternation, string[] branchTexts, int start, int end, List<RegexRewrite> found)
    {
        List<RegexNode> members = new List<RegexNode>();
        for (int i = start; i < end; i++)
        {
            members.Add(alternation.Branches[i]);
        }

        string? classText = ClassText(members);
        if (classText == null)
        {
            return;
        }

        List<string> branches = new List<string>();
        branches.AddRange(Slice(branchTexts, 0, start));
        branches.Add(classText);
        branches.AddRange(Slice(branchTexts, end, branchTexts.Length));
        string runText = string.Join("|", Slice(branchTexts, start, end));
        found.Add(Replace(alternation, branches, $"Merged alternation '{runText}' into '{classText}'"));
    }

    /// <summary>
    /// Factoring a prefix of single-character atoms out of adjacent branches keeps the order in
    /// which complete paths are tried, because the prefix itself has no choice points.
    /// </summary>
    private static void CollectPrefixRuns(AlternationNode alternation, string[] branchTexts, List<RegexRewrite> found)
    {
        IReadOnlyList<RegexNode>[] items = new IReadOnlyList<RegexNode>[branchTexts.Length];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = RegexWriter.ItemsOf(alternation.Branches[i]);
        }

        int start = 0;
        while (start < items.Length)
        {
            int end = start + 1;
            while (end < items.Length && CommonPrefixLength(items[start], items[end]) > 0)
            {
                end++;
            }

            if (end - start >= MinimumRun)
            {
                AddPrefixRun(alternation, branchTexts, items, start, end, found);
            }
            start = end;
        }
    }

    private static void AddPrefixRun(AlternationNode alternation, string[] branchTexts, IReadOnlyList<RegexNode>[] items, int start, int end, List<RegexRewrite> found)
    {
        int prefixLength = int.MaxValue;
        for (int i = start + 1; i < end; i++)
        {
            prefixLength = Math.Min(prefixLength, CommonPrefixLength(items[start], items[i]));
        }

        string prefix = ItemsText(items[start], 0, prefixLength);
        List<string> remainders = new List<string>();
        List<RegexNode> remainderNodes = new List<RegexNode>();
        for (int i = start; i < end; i++)
        {
            remainders.Add(ItemsText(items[i], prefixLength, items[i].Count));
            if (items[i].Count == prefixLength + 1)
            {
                remainderNodes.Add(items[i][prefixLength]);
            }
        }

        string? classText = remainderNodes.Count == remainders.Count ? ClassText(remainderNodes) : null;
        string combined = classText != null ? $"{prefix}{classText}" : $"{prefix}(?:{string.Join("|", remainders)})";
        string runText = string.Join("|", Slice(branchTexts, start, end));
        if (classText == null && combined.Length > runText.Length)
        {
            // Factoring that only lengthens the pattern helps neither readers nor the engine,
            // which already extracts common prefixes internally.
            return;
        }

        List<string> branches = new List<string>();
        branches.AddRange(Slice(branchTexts, 0, start));
        branches.Add(combined);
        branches.AddRange(Slice(branchTexts, end, branchTexts.Length));
        string description = classText != null
            ? $"Merged alternation '{runText}' into '{combined}'"
            : $"Factored common prefix '{prefix}' out of '{runText}' into '{combined}'";
        found.Add(Replace(alternation, branches, description));
    }

    private static int CommonPrefixLength(IReadOnlyList<RegexNode> left, IReadOnlyList<RegexNode> right)
    {
        int length = 0;
        while (length < left.Count && length < right.Count && SameAtom(left[length], right[length]))
        {
            length++;
        }
        return length;
    }

    private static bool SameAtom(RegexNode left, RegexNode right) =>
        SyntaxFacts.IsSingleChar(left) && SyntaxFacts.IsSingleChar(right) &&
        left.Options == right.Options &&
        string.Equals(left.Source, right.Source, StringComparison.Ordinal);

    private static string ItemsText(IReadOnlyList<RegexNode> items, int from, int to)
    {
        StringBuilder builder = new StringBuilder();
        for (int i = from; i < to; i++)
        {
            builder.Append(items[i].Source);
        }
        return builder.ToString();
    }

    /// <summary>Returns a bracketed class matching exactly the union of the members, or null.</summary>
    private static string? ClassText(List<RegexNode> members)
    {
        StringBuilder builder = new StringBuilder("[");
        CharSet union = CharSet.Empty;
        foreach (RegexNode member in members)
        {
            string? memberText = SyntaxFacts.ClassMemberText(member);
            CharSet? set = CharSet.ForNode(member);
            if (memberText == null || set == null)
            {
                return null;
            }
            builder.Append(memberText);
            union = union.Union(set);
        }

        string classText = builder.Append(']').ToString();
        bool exact = CharSet.ForSource(classText, members[0].Options).SetEquals(union);
        return exact ? classText : null;
    }

    private static IEnumerable<string> Slice(string[] values, int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            yield return values[i];
        }
    }

    private static RegexRewrite Replace(AlternationNode alternation, List<string> branches, string description) =>
        new RegexRewrite(alternation.Start, alternation.End, string.Join("|", branches), description);
}
