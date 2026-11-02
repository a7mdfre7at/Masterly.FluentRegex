#nullable enable

namespace Masterly.FluentRegex.Optimization;

/// <summary>A candidate rewrite: replace one span of the pattern text with new text.</summary>
internal sealed class RegexRewrite
{
    public RegexRewrite(int start, int end, string replacement, string description)
    {
        Start = start;
        End = end;
        Replacement = replacement;
        Description = description;
    }

    /// <summary>The first replaced position.</summary>
    public int Start { get; }

    /// <summary>The position just after the replaced span.</summary>
    public int End { get; }

    /// <summary>The new text.</summary>
    public string Replacement { get; }

    /// <summary>A human-readable description of the rewrite.</summary>
    public string Description { get; }

    /// <summary>Returns <paramref name="pattern"/> with the rewrite applied.</summary>
    public string Apply(string pattern) =>
        $"{pattern.Substring(0, Start)}{Replacement}{pattern.Substring(End)}";
}
