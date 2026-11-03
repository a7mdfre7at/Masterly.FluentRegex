#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Learning;

/// <summary>
/// The examples a candidate must reject, and how candidates are anchored and verified against them.
/// </summary>
internal sealed class LearningContext
{
    private const char Newline = '\n';

    private readonly bool _useEndOfInput;

    public LearningContext(IReadOnlyList<string> negatives)
    {
        Negatives = negatives;

        // '$' also matches before a final newline, so "abc\n" would pass for "abc". Only when such a
        // negative exists is the stricter '\z' needed; otherwise the familiar '$' reads better.
        _useEndOfInput = negatives.Any(n => n.Length > 0 && n[n.Length - 1] == Newline);
    }

    public IReadOnlyList<string> Negatives { get; }

    /// <summary>Anchors a single alternative or an alternation of several.</summary>
    public Pattern Anchor(IReadOnlyList<IReadOnlyList<LearnedElement>> alternatives)
    {
        Pattern pattern = Pattern.With.StartOfLine;
        if (alternatives.Count == 1)
        {
            foreach (LearnedElement element in LearnedElement.Simplify(alternatives[0]))
            {
                pattern = element.AppendTo(pattern);
            }
        }
        else
        {
            pattern = pattern.OneOf(alternatives.Select(LearnedElement.ToPattern).ToArray());
        }

        return _useEndOfInput ? pattern.EndOfInput : pattern.EndOfLine;
    }

    /// <summary>Compiles one anchored alternative for verification.</summary>
    public Regex Compile(IReadOnlyList<LearnedElement> elements) =>
        Anchor(new[] { elements }).ToRegex();

    /// <summary>Returns the negatives that <paramref name="regex"/> wrongly accepts.</summary>
    public IReadOnlyList<string> AcceptedNegatives(Regex regex) =>
        Negatives.Where(n => regex.IsMatch(n)).ToList();

    /// <summary>Returns true when <paramref name="regex"/> accepts every example.</summary>
    public static bool AcceptsAll(Regex regex, IEnumerable<string> examples) => examples.All(regex.IsMatch);
}
