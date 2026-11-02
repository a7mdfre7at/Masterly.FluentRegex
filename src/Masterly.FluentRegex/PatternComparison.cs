#nullable enable

using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex;

/// <summary>
/// The proven relation between the languages of two patterns, with the shortest strings that
/// demonstrate it. Every example string has been checked with the real .NET regex engine.
/// </summary>
public sealed class PatternComparison
{
    private const char FirstPrintable = ' ';
    private const char LastPrintable = '~';

    internal PatternComparison(string? onlyLeftExample, string? onlyRightExample, string? commonExample)
    {
        OnlyLeftExample = onlyLeftExample;
        OnlyRightExample = onlyRightExample;
        CommonExample = commonExample;
        Relation = Classify(onlyLeftExample != null, onlyRightExample != null, commonExample != null);
    }

    /// <summary>Gets how the two languages relate.</summary>
    public PatternRelation Relation { get; }

    /// <summary>Gets the shortest string the left pattern matches and the right one rejects, or null if there is none.</summary>
    public string? OnlyLeftExample { get; }

    /// <summary>Gets the shortest string the right pattern matches and the left one rejects, or null if there is none.</summary>
    public string? OnlyRightExample { get; }

    /// <summary>Gets the shortest string both patterns match, or null if there is none.</summary>
    public string? CommonExample { get; }

    /// <summary>Gets a value indicating whether both patterns match exactly the same strings.</summary>
    public bool AreEquivalent => Relation == PatternRelation.Equivalent;

    /// <summary>
    /// Describes the relation and its shortest counterexample, for example
    /// <c>Not equivalent (left is a proper superset of right). Shortest counterexample: "0\n" (left accepts it, right rejects it)</c>.
    /// </summary>
    /// <returns>A human-readable description.</returns>
    public override string ToString()
    {
        if (Relation == PatternRelation.Equivalent)
        {
            return "Equivalent: both patterns match exactly the same strings.";
        }

        StringBuilder text = new StringBuilder($"Not equivalent ({Describe(Relation)}). Shortest counterexample: ");
        bool leftFirst = OnlyRightExample == null ||
                         (OnlyLeftExample != null && OnlyLeftExample.Length <= OnlyRightExample.Length);
        if (leftFirst)
        {
            AppendLeftOnly(text);
            AppendRightOnly(text, "; also ");
        }
        else
        {
            AppendRightOnly(text, string.Empty);
            AppendLeftOnly(text, "; also ");
        }

        if (CommonExample != null)
        {
            text.Append($". Both accept {Quote(CommonExample)}");
        }
        return text.Append('.').ToString();
    }

    /// <summary>Formats a string as a quoted, escaped literal.</summary>
    internal static string Quote(string value)
    {
        StringBuilder builder = new StringBuilder("\"");
        foreach (char c in value)
        {
            builder.Append(Escape(c));
        }
        return builder.Append('"').ToString();
    }

    private void AppendLeftOnly(StringBuilder text, string separator = "")
    {
        if (OnlyLeftExample != null)
        {
            text.Append($"{separator}{Quote(OnlyLeftExample)} (left accepts it, right rejects it)");
        }
    }

    private void AppendRightOnly(StringBuilder text, string separator)
    {
        if (OnlyRightExample != null)
        {
            text.Append($"{separator}{Quote(OnlyRightExample)} (right accepts it, left rejects it)");
        }
    }

    private static string Escape(char c)
    {
        switch (c)
        {
            case '"':
                return "\\\"";
            case '\\':
                return "\\\\";
            case '\n':
                return "\\n";
            case '\r':
                return "\\r";
            case '\t':
                return "\\t";
        }

        bool printableAscii = c >= FirstPrintable && c <= LastPrintable;
        bool printableOther = c > LastPrintable && !char.IsControl(c) && !char.IsSurrogate(c) && !char.IsWhiteSpace(c);
        if (printableAscii || printableOther)
        {
            return c.ToString();
        }
        return $"\\u{((int)c).ToString("X4", CultureInfo.InvariantCulture)}";
    }

    private static string Describe(PatternRelation relation)
    {
        switch (relation)
        {
            case PatternRelation.ProperSubset:
                return "left is a proper subset of right";
            case PatternRelation.ProperSuperset:
                return "left is a proper superset of right";
            case PatternRelation.Disjoint:
                return "no string matches both";
            default:
                return "the patterns overlap";
        }
    }

    private static PatternRelation Classify(bool hasOnlyLeft, bool hasOnlyRight, bool hasCommon)
    {
        if (!hasOnlyLeft && !hasOnlyRight)
        {
            return PatternRelation.Equivalent;
        }
        if (!hasOnlyLeft)
        {
            return PatternRelation.ProperSubset;
        }
        if (!hasOnlyRight)
        {
            return PatternRelation.ProperSuperset;
        }
        return hasCommon ? PatternRelation.Overlapping : PatternRelation.Disjoint;
    }
}
