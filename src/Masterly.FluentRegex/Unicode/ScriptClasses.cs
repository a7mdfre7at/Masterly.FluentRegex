#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex.Unicode;

/// <summary>
/// Builds the character-class tokens behind <see cref="Pattern.Script(UnicodeScript)"/>,
/// <see cref="Pattern.LetterIn(UnicodeScript)"/> and <see cref="Pattern.DigitIn(UnicodeScript)"/>.
/// </summary>
internal static class ScriptClasses
{
    private const string LetterSubtraction = @"-[\P{L}]";
    private const char RangeSeparator = '-';
    private const char ListSeparator = ' ';
    private const int HexBase = 16;
    private const char LastAscii = '\u007F';

    // BMP blocks per script, as space-separated hex code points or ranges.
    private static readonly Dictionary<UnicodeScript, string> BlockRanges = new Dictionary<UnicodeScript, string>
    {
        [UnicodeScript.Latin] = "0041-005A 0061-007A 00AA 00BA 00C0-00D6 00D8-00F6 00F8-02AF 0300-036F " +
                                "1D00-1DBF 1E00-1EFF 2C60-2C7F A720-A7FF AB30-AB6F FB00-FB06 FF21-FF3A FF41-FF5A",
        [UnicodeScript.Greek] = "0300-036F 0370-03E1 03F0-03FF 1F00-1FFF",
        [UnicodeScript.Cyrillic] = "0300-036F 0400-052F 1C80-1C8F 2DE0-2DFF A640-A69F",
        [UnicodeScript.Armenian] = "0530-058F FB13-FB17",
        [UnicodeScript.Hebrew] = "0590-05FF FB1D-FB4F",
        [UnicodeScript.Arabic] = "0600-06FF 0750-077F 0870-08FF FB50-FDFF FE70-FEFF",
        [UnicodeScript.Syriac] = "0700-074F 0860-086F",
        [UnicodeScript.Thaana] = "0780-07BF",
        [UnicodeScript.Devanagari] = "0900-097F A8E0-A8FF",
        [UnicodeScript.Bengali] = "0980-09FF",
        [UnicodeScript.Gurmukhi] = "0A00-0A7F",
        [UnicodeScript.Gujarati] = "0A80-0AFF",
        [UnicodeScript.Tamil] = "0B80-0BFF",
        [UnicodeScript.Telugu] = "0C00-0C7F",
        [UnicodeScript.Kannada] = "0C80-0CFF",
        [UnicodeScript.Malayalam] = "0D00-0D7F",
        [UnicodeScript.Thai] = "0E00-0E7F",
        [UnicodeScript.Lao] = "0E80-0EFF",
        [UnicodeScript.Tibetan] = "0F00-0FFF",
        [UnicodeScript.Georgian] = "10A0-10FF 1C90-1CBF 2D00-2D2F",
        [UnicodeScript.Hangul] = "1100-11FF 3130-318F A960-A97F AC00-D7FF FFA0-FFDF",
        [UnicodeScript.Ethiopic] = "1200-139F 2D80-2DDF AB00-AB2F",
        [UnicodeScript.Khmer] = "1780-17FF 19E0-19FF",
        [UnicodeScript.Mongolian] = "1800-18AF",
        [UnicodeScript.Hiragana] = "3040-309F",
        [UnicodeScript.Katakana] = "30A0-30FF 31F0-31FF FF66-FF9F",
        [UnicodeScript.Han] = "2E80-2FDF 3005 3007 3021-3029 3038-303B 3400-4DBF 4E00-9FFF F900-FAFF"
    };

    // Decimal digits (Unicode category Nd) per script. Scripts not listed have no digits of their own.
    private static readonly Dictionary<UnicodeScript, string> DigitRanges = new Dictionary<UnicodeScript, string>
    {
        [UnicodeScript.Latin] = "0030-0039",
        [UnicodeScript.Arabic] = "0660-0669 06F0-06F9",
        [UnicodeScript.Devanagari] = "0966-096F",
        [UnicodeScript.Bengali] = "09E6-09EF",
        [UnicodeScript.Gurmukhi] = "0A66-0A6F",
        [UnicodeScript.Gujarati] = "0AE6-0AEF",
        [UnicodeScript.Tamil] = "0BE6-0BEF",
        [UnicodeScript.Telugu] = "0C66-0C6F",
        [UnicodeScript.Kannada] = "0CE6-0CEF",
        [UnicodeScript.Malayalam] = "0D66-0D6F",
        [UnicodeScript.Thai] = "0E50-0E59",
        [UnicodeScript.Lao] = "0ED0-0ED9",
        [UnicodeScript.Tibetan] = "0F20-0F29",
        [UnicodeScript.Khmer] = "17E0-17E9",
        [UnicodeScript.Mongolian] = "1810-1819"
    };

    private static readonly ConcurrentDictionary<UnicodeScript, ClassToken> ScriptTokens =
        new ConcurrentDictionary<UnicodeScript, ClassToken>();

    private static readonly ConcurrentDictionary<UnicodeScript, ClassToken> LetterTokens =
        new ConcurrentDictionary<UnicodeScript, ClassToken>();

    private static readonly ConcurrentDictionary<UnicodeScript, ClassToken> DigitTokens =
        new ConcurrentDictionary<UnicodeScript, ClassToken>();

    /// <summary>Gets a class matching any BMP character of the script's blocks.</summary>
    public static ClassToken Script(UnicodeScript script)
    {
        string ranges = RangesOf(script);
        return ScriptTokens.GetOrAdd(script, _ => ClassToken.Bracketed(RenderRanges(ParseRanges(ranges))));
    }

    /// <summary>
    /// Gets a class matching the letters (category L) of the script. Standalone it renders a class
    /// subtraction; inside a set, where subtraction is not allowed, it renders the letter ranges.
    /// </summary>
    public static ClassToken Letters(UnicodeScript script)
    {
        string ranges = RangesOf(script);
        return LetterTokens.GetOrAdd(script, _ => CreateLetterToken(ParseRanges(ranges)));
    }

    /// <summary>Gets a class matching the script's own decimal digits.</summary>
    /// <exception cref="ArgumentException">The script has no decimal digits of its own.</exception>
    public static ClassToken Digits(UnicodeScript script)
    {
        RangesOf(script);
        if (!DigitRanges.TryGetValue(script, out string? ranges))
        {
            throw new ArgumentException(
                $"The {script} script has no decimal digits of its own. " +
                $"Scripts with digits: {string.Join(", ", DigitRanges.Keys)}.",
                nameof(script));
        }

        return DigitTokens.GetOrAdd(script, _ => ClassToken.Bracketed(RenderRanges(ParseRanges(ranges))));
    }

    private static string RangesOf(UnicodeScript script)
    {
        if (!BlockRanges.TryGetValue(script, out string? ranges))
        {
            throw new ArgumentOutOfRangeException(nameof(script), script, "Unknown Unicode script.");
        }

        return ranges;
    }

    private static ClassToken CreateLetterToken(IReadOnlyList<CodeRange> ranges)
    {
        string standalone = $"[{RenderRanges(ranges)}{LetterSubtraction}]";
        string classBody = RenderRanges(LetterRuns(ranges));
        return new ClassToken(classBody, standalone);
    }

    private static List<CodeRange> LetterRuns(IReadOnlyList<CodeRange> ranges)
    {
        List<CodeRange> runs = new List<CodeRange>();
        foreach (CodeRange range in ranges)
        {
            int runStart = -1;
            for (int code = range.Low; code <= range.High + 1; code++)
            {
                bool isLetter = code <= range.High && char.IsLetter((char)code);
                if (isLetter && runStart < 0)
                {
                    runStart = code;
                }
                else if (!isLetter && runStart >= 0)
                {
                    runs.Add(new CodeRange((char)runStart, (char)(code - 1)));
                    runStart = -1;
                }
            }
        }

        return runs;
    }

    private static List<CodeRange> ParseRanges(string table)
    {
        List<CodeRange> ranges = new List<CodeRange>();
        foreach (string item in table.Split(ListSeparator))
        {
            int separator = item.IndexOf(RangeSeparator);
            char low = ParseHex(separator < 0 ? item : item.Substring(0, separator));
            char high = separator < 0 ? low : ParseHex(item.Substring(separator + 1));
            ranges.Add(new CodeRange(low, high));
        }

        return ranges;
    }

    private static char ParseHex(string hex) => (char)Convert.ToInt32(hex, HexBase);

    internal static string RenderRanges(IReadOnlyList<CodeRange> ranges)
    {
        StringBuilder sb = new StringBuilder();
        foreach (CodeRange range in ranges)
        {
            AppendClassChar(sb, range.Low);
            if (range.High != range.Low)
            {
                sb.Append(RangeSeparator);
                AppendClassChar(sb, range.High);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Appends a character for use inside a character class: ASCII letters and digits as they are,
    /// everything else as a <c>\uXXXX</c> escape, which is valid in every regex mode.
    /// </summary>
    internal static void AppendClassChar(StringBuilder sb, char c)
    {
        if (c <= LastAscii && char.IsLetterOrDigit(c))
        {
            sb.Append(c);
            return;
        }

        sb.Append(@"\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
    }
}

/// <summary>An inclusive range of UTF-16 code units.</summary>
internal readonly struct CodeRange
{
    public CodeRange(char low, char high)
    {
        Low = low;
        High = high;
    }

    public char Low { get; }

    public char High { get; }
}
