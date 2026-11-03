#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Masterly.FluentRegex.Coverage;

namespace Masterly.FluentRegex;

/// <summary>
/// The coverage of a pattern's decision points (alternation branches, optional parts and bounded
/// repetitions) by a set of inputs.
/// </summary>
public sealed class CoverageReport
{
    private const double FullCoverage = 100.0;
    private const string Indent = "  ";

    private readonly string _pattern;

    internal CoverageReport(string pattern, IReadOnlyList<CoverageItem> items, int inputsRecorded)
    {
        _pattern = pattern;
        Items = items;
        Uncovered = items.Where(item => !item.IsCovered).ToList();
        InputsRecorded = inputsRecorded;
    }

    /// <summary>
    /// Gets the percentage of items exercised by at least one match, from 0 to 100.
    /// A pattern without decision points is reported as 100% covered.
    /// </summary>
    public double Percentage =>
        Items.Count == 0 ? FullCoverage : FullCoverage * (Items.Count - Uncovered.Count) / Items.Count;

    /// <summary>Gets every tracked item, in pattern order.</summary>
    public IReadOnlyList<CoverageItem> Items { get; }

    /// <summary>Gets the items that no match exercised.</summary>
    public IReadOnlyList<CoverageItem> Uncovered { get; }

    /// <summary>Gets the number of inputs recorded, whether they matched or not.</summary>
    public int InputsRecorded { get; }

    /// <summary>
    /// Returns a readable report: a summary line followed by one line per item.
    /// </summary>
    public override string ToString()
    {
        StringBuilder text = new StringBuilder();
        string covered = (Items.Count - Uncovered.Count).ToString(CultureInfo.InvariantCulture);
        string total = Items.Count.ToString(CultureInfo.InvariantCulture);
        text.AppendLine($"Coverage for {_pattern}: {CoverageText.Percent(Percentage)}% ({covered}/{total}), {CoverageText.Count(InputsRecorded, "input")}");

        if (Items.Count == 0)
        {
            text.AppendLine($"{Indent}(no alternation branches, optional parts or bounded repetitions to cover)");
        }

        foreach (CoverageItem item in Items)
        {
            text.AppendLine($"{Indent}{CoverageText.Line(item)}");
        }

        return text.ToString();
    }
}
