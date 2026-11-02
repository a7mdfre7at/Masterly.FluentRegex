#nullable enable

using System;
using System.Globalization;

namespace Masterly.FluentRegex.Coverage;

/// <summary>Formats coverage items and reports as text.</summary>
internal static class CoverageText
{
    private const string CoveredMark = "✓";
    private const string UncoveredMark = "✗";

    /// <summary>Formats an item, for example <c>✓ branch "Mr" (4 hits)</c>.</summary>
    public static string Line(CoverageItem item) =>
        item.IsCovered
            ? $"{CoveredMark} {item.Description} ({Count(item.HitCount, "hit")})"
            : $"{UncoveredMark} {item.Description} {NeverPhrase(item.Kind)}";

    /// <summary>Formats a percentage rounded to a whole number.</summary>
    public static string Percent(double percentage) =>
        Math.Round(percentage, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);

    /// <summary>Formats a count with a singular or plural noun, for example <c>1 input</c> or <c>5 inputs</c>.</summary>
    public static string Count(int count, string noun)
    {
        string number = count.ToString(CultureInfo.InvariantCulture);
        return count == 1 ? $"{number} {noun}" : $"{number} {noun}s";
    }

    private static string NeverPhrase(CoverageItemKind kind)
    {
        switch (kind)
        {
            case CoverageItemKind.AlternationBranch:
                return "was never matched";
            case CoverageItemKind.OptionalPartTaken:
                return "was never taken";
            default:
                return "was never reached";
        }
    }
}
