#nullable enable

using System;
using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex.Templates;

/// <summary>
/// Translates a <see cref="TimeSpan"/> format string (custom, or the standard c, g, G) into a regex.
/// In custom TimeSpan formats every literal must be quoted or escaped, exactly as .NET requires.
/// </summary>
internal sealed class TimeSpanFormatTranslator
{
    private const string Constant = @"-?(?:\d+\.)?\d{2}:\d{2}:\d{2}(?:\.\d{7})?";
    private const int MaxDayDigits = 8;
    private const int MaxFractionDigits = 7;
    private const int MaxClockDigits = 2;

    private readonly string _format;
    private readonly StringBuilder _sb = new StringBuilder();
    private int _position;

    private TimeSpanFormatTranslator(string format) => _format = format;

    /// <summary>Returns the regex for <paramref name="format"/>.</summary>
    /// <exception cref="ArgumentException">The format is not a supported TimeSpan format.</exception>
    public static string ToRegex(string format, IFormatProvider culture)
    {
        if (format.Length == 1)
        {
            return Standard(format[0], NumberFormatInfo.GetInstance(culture));
        }

        return new TimeSpanFormatTranslator(format).Translate();
    }

    private static string Standard(char specifier, NumberFormatInfo numbers)
    {
        string sign = $"(?:{RegexText.Escape(numbers.NegativeSign)})?";
        string separator = RegexText.Escape(numbers.NumberDecimalSeparator);
        switch (specifier)
        {
            case 'c':
            case 't':
            case 'T':
                return Constant;
            case 'g':
                return $@"{sign}(?:\d+:)?\d{{1,2}}:\d{{2}}:\d{{2}}(?:{separator}\d{{1,7}})?";
            case 'G':
                return $@"{sign}\d+:\d{{2}}:\d{{2}}:\d{{2}}{separator}\d{{7}}";
            default:
                throw Unsupported($"the standard format '{specifier}'");
        }
    }

    private string Translate()
    {
        while (_position < _format.Length)
        {
            TranslateNext();
        }

        return _sb.ToString();
    }

    private void TranslateNext()
    {
        char c = _format[_position];
        switch (c)
        {
            case '\'':
            case '"':
                ReadQuoted(c);
                return;
            case '\\':
                ReadEscaped();
                return;
            case '%':
                _position++;
                return;
            case 'd':
            case 'h':
            case 'm':
            case 's':
            case 'f':
                TranslateSpecifier(c);
                return;
            default:
                throw Unsupported($"the unescaped character '{c}' (quote it or escape it with \\, as in hh\\:mm)");
        }
    }

    private void TranslateSpecifier(char c)
    {
        int count = 0;
        while (_position + count < _format.Length && _format[_position + count] == c)
        {
            count++;
        }

        _sb.Append(Specifier(c, count));
        _position += count;
    }

    private static string Specifier(char c, int count)
    {
        string digits = count.ToString(CultureInfo.InvariantCulture);
        if (c == 'd' && count <= MaxDayDigits)
        {
            return count == 1 ? @"\d+" : $@"\d{{{digits},}}";
        }

        if (c == 'f' && count <= MaxFractionDigits)
        {
            return $@"\d{{{digits}}}";
        }

        if (c != 'd' && c != 'f' && count <= MaxClockDigits)
        {
            return count == 1 ? @"\d{1,2}" : @"\d{2}";
        }

        throw Unsupported($"the specifier '{new string(c, count)}'");
    }

    private void ReadQuoted(char quote)
    {
        int end = _format.IndexOf(quote, _position + 1);
        if (end < 0)
        {
            throw Unsupported($"an unterminated quoted literal in \"{_format}\"");
        }

        _sb.Append(RegexText.Escape(_format.Substring(_position + 1, end - _position - 1)));
        _position = end + 1;
    }

    private void ReadEscaped()
    {
        if (_position + 1 >= _format.Length)
        {
            throw Unsupported($"a trailing backslash in \"{_format}\"");
        }

        _sb.Append(RegexText.Escape(_format[_position + 1].ToString()));
        _position += 2;
    }

    private const string SupportedSpecifiers =
        "d..dddddddd, h, hh, m, mm, s, ss, f..fffffff, quoted literals, \\ escapes and the standard formats c, g and G";

    private static ArgumentException Unsupported(string what) =>
        TemplateErrors.Invalid(
            $"The TimeSpan format uses {what}, which templates do not support. Supported: {SupportedSpecifiers}.");
}
