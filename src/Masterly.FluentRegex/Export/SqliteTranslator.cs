#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Export;

/// <summary>
/// SQLite through the EF Core provider. The provider registers <c>regexp()</c> as
/// <c>Regex.IsMatch(input, pattern, RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(1000))</c>
/// (src/EFCore.Sqlite.Core/Storage/Internal/SqliteRelationalConnection.cs) and translates only the
/// two-argument <c>Regex.IsMatch</c>, so the pattern is the .NET pattern itself with its options
/// written as an inline prefix. Options without an inline form (CultureInvariant, RightToLeft,
/// ECMAScript, NonBacktracking) and constructs the non-backtracking engine rejects (backreferences,
/// lookarounds, atomic groups, conditionals, balancing groups, <c>\G</c>) are reported as unsupported
/// rather than failing at query time. SQLite string literals do not process backslashes
/// (https://www.sqlite.org/lang_expr.html), so only quotes are doubled.
/// </summary>
internal static class SqliteTranslator
{
    private const RegexOptions InlineOptions =
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline |
        RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace;

    private const RegexOptions IgnoredOptions = RegexOptions.Compiled;

    /// <summary>Translates <paramref name="tree"/>, or throws listing every obstacle.</summary>
    public static FlavorConversion Translate(RegexTree tree, RegexOptions options)
    {
        List<string> issues = new List<string>();
        CheckOptions(options & ~IgnoredOptions, issues);
        CheckConstructs(tree.Root, issues);
        if (issues.Count > 0)
        {
            throw SqlRegexTranslator.UnsupportedException(SqlDialect.Name(RegexFlavor.Sqlite), issues);
        }

        string pattern = $"{InlinePrefix(options)}{tree.Pattern}";
        return new FlavorConversion(RegexFlavor.Sqlite, pattern, string.Empty, new List<string>(), GroupNumbers(tree));
    }

    private static void CheckOptions(RegexOptions options, List<string> issues)
    {
        RegexOptions rest = options & ~InlineOptions;
        if (rest != RegexOptions.None)
        {
            issues.Add($"RegexOptions.{rest} (has no inline form; the EF Core SQLite regexp() function only receives the pattern text)");
        }
    }

    private static void CheckConstructs(RegexNode node, List<string> issues)
    {
        string? construct = NonBacktrackingObstacle(node);
        if (construct != null)
        {
            issues.Add($"{construct} at position {node.Start.ToString(CultureInfo.InvariantCulture)} (rejected by RegexOptions.NonBacktracking, which the EF Core SQLite regexp() function uses)");
        }

        foreach (RegexNode child in node.Children)
        {
            CheckConstructs(child, issues);
        }
    }

    private static string? NonBacktrackingObstacle(RegexNode node)
    {
        switch (node)
        {
            case BackreferenceNode _:
                return "backreference";
            case ConditionalNode conditional:
                return $"conditional '{conditional.OpenText}...)'";
            case AnchorNode anchor:
                return anchor.AnchorKind == AnchorKind.ContiguousMatch ? @"'\G'" : null;
            case GroupNode group:
                return GroupObstacle(group);
            default:
                return null;
        }
    }

    private static string? GroupObstacle(GroupNode group)
    {
        switch (group.GroupKind)
        {
            case GroupKind.Atomic:
                return "atomic group '(?>...)'";
            case GroupKind.Balancing:
                return $"balancing group '{group.OpenText}...)'";
            case GroupKind.Lookahead:
            case GroupKind.NegativeLookahead:
            case GroupKind.Lookbehind:
            case GroupKind.NegativeLookbehind:
                return $"lookaround '{group.OpenText}...)'";
            default:
                return null;
        }
    }

    private static string InlinePrefix(RegexOptions options)
    {
        StringBuilder letters = new StringBuilder();
        AppendLetter(letters, options, RegexOptions.IgnoreCase, 'i');
        AppendLetter(letters, options, RegexOptions.Multiline, 'm');
        AppendLetter(letters, options, RegexOptions.Singleline, 's');
        AppendLetter(letters, options, RegexOptions.ExplicitCapture, 'n');
        AppendLetter(letters, options, RegexOptions.IgnorePatternWhitespace, 'x');
        return letters.Length == 0 ? string.Empty : $"(?{letters})";
    }

    private static void AppendLetter(StringBuilder letters, RegexOptions options, RegexOptions option, char letter)
    {
        if ((options & option) != 0)
        {
            letters.Append(letter);
        }
    }

    /// <summary>The same engine numbers groups the same way, so every group maps to its own .NET number.</summary>
    private static IReadOnlyDictionary<string, int> GroupNumbers(RegexTree tree)
    {
        List<KeyValuePair<GroupNode, int>> numbers = new List<KeyValuePair<GroupNode, int>>();
        foreach (GroupNode group in tree.Groups)
        {
            if (group.IsCapturing)
            {
                numbers.Add(new KeyValuePair<GroupNode, int>(group, group.Number));
            }
        }

        return TranslationPlan.BuildGroupNumbers(numbers);
    }
}
