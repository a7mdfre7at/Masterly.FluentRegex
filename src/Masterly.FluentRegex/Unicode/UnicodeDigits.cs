#nullable enable

using System;
using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex.Unicode;

/// <summary>
/// Converts Unicode decimal digits to ASCII before numeric parsing. The .NET regex class <c>\d</c>
/// matches every decimal digit (category Nd), such as Arabic-Indic <c>٢٠٢٦</c>, but the numeric
/// parsers of .NET only accept ASCII digits.
/// </summary>
internal static class UnicodeDigits
{
    private const char AsciiZero = '0';
    private const char LastAscii = '\u007F';
    private const char ArabicDecimalSeparator = '\u066B';
    private const char ArabicThousandsSeparator = '\u066C';

    /// <summary>Replaces every non-ASCII decimal digit with its ASCII equivalent.</summary>
    public static string ToAscii(string text) => Normalize(text, null);

    /// <summary>
    /// Replaces non-ASCII decimal digits with ASCII digits, and the Arabic decimal and thousands
    /// separators with those of <paramref name="provider"/>.
    /// </summary>
    public static string ToAsciiNumber(string text, IFormatProvider? provider) =>
        Normalize(text, NumberFormatInfo.GetInstance(provider));

    private static string Normalize(string text, NumberFormatInfo? separators)
    {
        if (IsAscii(text))
        {
            return text;
        }

        StringBuilder sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            AppendMapped(sb, c, separators);
        }

        return sb.ToString();
    }

    private static void AppendMapped(StringBuilder sb, char c, NumberFormatInfo? separators)
    {
        if (c > LastAscii && CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.DecimalDigitNumber)
        {
            sb.Append((char)(AsciiZero + CharUnicodeInfo.GetDecimalDigitValue(c)));
        }
        else if (separators != null && c == ArabicDecimalSeparator)
        {
            sb.Append(separators.NumberDecimalSeparator);
        }
        else if (separators != null && c == ArabicThousandsSeparator)
        {
            sb.Append(separators.NumberGroupSeparator);
        }
        else
        {
            sb.Append(c);
        }
    }

    private static bool IsAscii(string text)
    {
        foreach (char c in text)
        {
            if (c > LastAscii)
            {
                return false;
            }
        }

        return true;
    }
}
