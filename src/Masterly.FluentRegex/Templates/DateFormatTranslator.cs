#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Masterly.FluentRegex.Templates;

/// <summary>The date and time types a template can format.</summary>
internal enum TemporalKind
{
    DateTime,
    DateTimeOffset,
    DateOnly,
    TimeOnly
}

/// <summary>
/// Translates a .NET date and time format string (custom, or a single-letter standard format) into
/// a regex that matches exactly what <c>ToString(format, culture)</c> produces.
/// </summary>
internal sealed class DateFormatTranslator
{
    private const string OneOrTwoDigits = @"\d{1,2}";
    private const string TwoDigits = @"\d{2}";
    private const int MaxFractionDigits = 7;
    private const int AbbreviatedNameLength = 3;
    private const int FullYearLength = 4;

    private readonly string _format;
    private readonly DateTimeFormatInfo _info;
    private readonly StringBuilder _sb = new StringBuilder();
    private int _position;

    private DateFormatTranslator(string format, DateTimeFormatInfo info)
    {
        _format = format;
        _info = info;
    }

    /// <summary>Returns the regex for <paramref name="format"/>.</summary>
    /// <exception cref="ArgumentException">The format uses an unsupported specifier.</exception>
    public static string ToRegex(string format, TemporalKind kind, IFormatProvider culture)
    {
        DateTimeFormatInfo info = DateTimeFormatInfo.GetInstance(culture);
        if (format.Length == 1)
        {
            return ToRegexStandard(format[0], kind, info);
        }

        return new DateFormatTranslator(format, info).Translate();
    }

    private static string ToRegexStandard(char specifier, TemporalKind kind, DateTimeFormatInfo info)
    {
        DateTimeFormatInfo invariant = DateTimeFormatInfo.InvariantInfo;
        switch (specifier)
        {
            case 'o':
            case 'O':
                return new DateFormatTranslator(RoundTripPattern(kind), invariant).Translate();
            case 'r':
            case 'R':
                return new DateFormatTranslator(Rfc1123Pattern(kind), invariant).Translate();
            case 's':
                return new DateFormatTranslator(invariant.SortableDateTimePattern, invariant).Translate();
            case 'u':
                return new DateFormatTranslator(invariant.UniversalSortableDateTimePattern, invariant).Translate();
            default:
                return new DateFormatTranslator(CulturePattern(specifier, info), info).Translate();
        }
    }

    private static string RoundTripPattern(TemporalKind kind)
    {
        switch (kind)
        {
            case TemporalKind.DateOnly: return "yyyy'-'MM'-'dd";
            case TemporalKind.TimeOnly: return "HH':'mm':'ss'.'fffffff";
            case TemporalKind.DateTimeOffset: return "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffzzz";
            default: return "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK";
        }
    }

    private static string Rfc1123Pattern(TemporalKind kind)
    {
        switch (kind)
        {
            case TemporalKind.DateOnly: return "ddd, dd MMM yyyy";
            case TemporalKind.TimeOnly: return "HH':'mm':'ss";
            default: return DateTimeFormatInfo.InvariantInfo.RFC1123Pattern;
        }
    }

    private static string CulturePattern(char specifier, DateTimeFormatInfo info)
    {
        switch (specifier)
        {
            case 'd': return info.ShortDatePattern;
            case 'D': return info.LongDatePattern;
            case 'f': return $"{info.LongDatePattern} {info.ShortTimePattern}";
            case 'F': return info.FullDateTimePattern;
            case 'g': return $"{info.ShortDatePattern} {info.ShortTimePattern}";
            case 'G': return $"{info.ShortDatePattern} {info.LongTimePattern}";
            case 'm':
            case 'M': return info.MonthDayPattern;
            case 't': return info.ShortTimePattern;
            case 'T': return info.LongTimePattern;
            case 'y':
            case 'Y': return info.YearMonthPattern;
            default: throw Unsupported($"the standard format '{specifier}'");
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
                AppendLiteral(ReadQuoted(c));
                return;
            case '\\':
                AppendLiteral(ReadEscaped());
                return;
            case '%':
                _position++;
                return;
            case ':':
                AppendLiteral(_info.TimeSeparator);
                _position++;
                return;
            case '/':
                AppendLiteral(_info.DateSeparator);
                _position++;
                return;
            default:
                TranslateSpecifierOrLiteral(c);
                return;
        }
    }

    private void TranslateSpecifierOrLiteral(char c)
    {
        if ("yMdHhmsftzKFg".IndexOf(c) < 0)
        {
            AppendLiteral(c.ToString());
            _position++;
            return;
        }

        int count = CountRepeats(c);
        _sb.Append(Specifier(c, count));
        _position += count;
    }

    private string Specifier(char c, int count)
    {
        switch (c)
        {
            case 'y': return Year(count);
            case 'M': return count <= 2 ? Digits(count) : MonthNames(count);
            case 'd': return count <= 2 ? Digits(count) : Names(count, _info.AbbreviatedDayNames, _info.DayNames);
            case 'H':
            case 'h':
            case 'm':
            case 's': return Digits(Math.Min(count, 2));
            case 'f': return Fraction(count);
            case 't': return Designator(count);
            case 'z': return Offset(count);
            case 'K': return @"(?:Z|[+-]\d{2}:\d{2})?";
            default: throw Unsupported($"the specifier '{new string(c, count)}'");
        }
    }

    private static string Digits(int count) => count == 1 ? OneOrTwoDigits : TwoDigits;

    private static string Year(int count)
    {
        switch (count)
        {
            case 1: return OneOrTwoDigits;
            case 2: return TwoDigits;
            case 3: return @"\d{3,4}";
            default: return $@"\d{{{Math.Max(count, FullYearLength).ToString(CultureInfo.InvariantCulture)}}}";
        }
    }

    // Genitive names are included because .NET uses them when a day number is in the same format.
    private string MonthNames(int count) =>
        Names(
            count,
            _info.AbbreviatedMonthNames.Concat(_info.AbbreviatedMonthGenitiveNames),
            _info.MonthNames.Concat(_info.MonthGenitiveNames));

    private static string Names(int count, IEnumerable<string> abbreviated, IEnumerable<string> full) =>
        RegexText.WordAlternation(count == AbbreviatedNameLength ? abbreviated : full);

    private static string Fraction(int count)
    {
        if (count > MaxFractionDigits)
        {
            throw Unsupported($"'{new string('f', count)}' (at most {MaxFractionDigits} fraction digits)");
        }

        return $@"\d{{{count.ToString(CultureInfo.InvariantCulture)}}}";
    }

    private string Designator(int count)
    {
        string[] designators = { _info.AMDesignator, _info.PMDesignator };
        IEnumerable<string> words = count == 1
            ? designators.Where(d => d.Length > 0).Select(d => d.Substring(0, 1))
            : designators;
        string alternation = RegexText.WordAlternation(words);
        bool optional = designators.Any(d => d.Length == 0);
        return optional ? $"(?:{alternation})?" : alternation;
    }

    private static string Offset(int count)
    {
        switch (count)
        {
            case 1: return @"[+-]\d{1,2}";
            case 2: return @"[+-]\d{2}";
            default: return @"[+-]\d{2}:\d{2}";
        }
    }

    private int CountRepeats(char c)
    {
        int count = 0;
        while (_position + count < _format.Length && _format[_position + count] == c)
        {
            count++;
        }

        return count;
    }

    private string ReadQuoted(char quote)
    {
        StringBuilder literal = new StringBuilder();
        _position++;
        while (_position < _format.Length && _format[_position] != quote)
        {
            if (_format[_position] == '\\' && _position + 1 < _format.Length)
            {
                _position++;
            }

            literal.Append(_format[_position]);
            _position++;
        }

        if (_position >= _format.Length)
        {
            throw Unsupported($"an unterminated quoted literal in \"{_format}\"");
        }

        _position++;
        return literal.ToString();
    }

    private string ReadEscaped()
    {
        if (_position + 1 >= _format.Length)
        {
            throw Unsupported($"a trailing backslash in \"{_format}\"");
        }

        _position += 2;
        return _format[_position - 1].ToString();
    }

    private void AppendLiteral(string text) => _sb.Append(RegexText.Escape(text));

    private const string SupportedSpecifiers =
        "y, yy, yyyy, M, MM, MMM, MMMM, d, dd, ddd, dddd, H, HH, h, hh, m, mm, s, ss, f..fffffff, t, tt, " +
        "z, zz, zzz, K, quoted literals, \\ escapes and the standard formats d D f F g G m o r s t T u y";

    private static ArgumentException Unsupported(string what) =>
        TemplateErrors.Invalid(
            $"The date/time format uses {what}, which templates do not support. Supported: {SupportedSpecifiers}.");
}
