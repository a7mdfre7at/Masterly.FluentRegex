#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Masterly.FluentRegex.Unicode;

namespace Masterly.FluentRegex.Templates;

/// <summary>
/// Integer members. No format matches any number of digits; <c>D4</c> exactly four digits;
/// <c>X</c> / <c>X8</c> hexadecimal digits.
/// </summary>
internal sealed class IntegerCodec : FieldCodec
{
    private const string SupportedFormats = "D, Dn, X or Xn (for example D4 or X8)";

    private readonly Type _type;
    private readonly string? _format;
    private readonly IFormatProvider _culture;
    private readonly bool _isHex;
    private readonly int? _width;
    private readonly string _negativeSign;

    public IntegerCodec(Type type, string? format, IFormatProvider culture)
    {
        _type = type;
        _format = format;
        _culture = culture;
        _negativeSign = RegexText.Escape(NumberFormatInfo.GetInstance(culture).NegativeSign);
        if (format != null)
        {
            char specifier = char.ToUpperInvariant(format[0]);
            if (specifier != 'D' && specifier != 'X')
            {
                throw Unsupported(type, format);
            }

            _isHex = specifier == 'X';
            _width = FormatWidth.Parse(format, type, SupportedFormats);
        }
    }

    public static bool Handles(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(sbyte) ||
        type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(byte);

    public override string Fragment
    {
        get
        {
            string quantifier = _width.HasValue ? $"{{{_width.Value.ToString(CultureInfo.InvariantCulture)}}}" : "+";
            if (_isHex)
            {
                return $"[0-9A-Fa-f]{quantifier}";
            }

            string sign = IsSigned(_type) ? $"(?:{_negativeSign})?" : string.Empty;
            return $@"{sign}\d{quantifier}";
        }
    }

    public override object? Parse(string text) =>
        _isHex ? ParseHex(UnicodeDigits.ToAscii(text)) : ValueConverter.Convert(text, _type, _culture);

    public override string Format(object? value) => ConverterCodec.Render(value, _format, _culture);

    private static bool IsSigned(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(sbyte);

    private object ParseHex(string text) => HexParsers[_type](text);

    private static readonly Dictionary<Type, Func<string, object>> HexParsers =
        new Dictionary<Type, Func<string, object>>
    {
        [typeof(int)] = text => int.Parse(text, Hex, CultureInfo.InvariantCulture),
        [typeof(long)] = text => long.Parse(text, Hex, CultureInfo.InvariantCulture),
        [typeof(short)] = text => short.Parse(text, Hex, CultureInfo.InvariantCulture),
        [typeof(sbyte)] = text => sbyte.Parse(text, Hex, CultureInfo.InvariantCulture),
        [typeof(uint)] = text => uint.Parse(text, Hex, CultureInfo.InvariantCulture),
        [typeof(ulong)] = text => ulong.Parse(text, Hex, CultureInfo.InvariantCulture),
        [typeof(ushort)] = text => ushort.Parse(text, Hex, CultureInfo.InvariantCulture),
        [typeof(byte)] = text => byte.Parse(text, Hex, CultureInfo.InvariantCulture)
    };

    private const NumberStyles Hex = NumberStyles.AllowHexSpecifier;

    private static ArgumentException Unsupported(Type type, string format) =>
        TemplateErrors.UnsupportedFormat(type, format, SupportedFormats);
}

/// <summary>
/// <see cref="decimal"/>, <see cref="double"/> and <see cref="float"/> members. No format (or G / R)
/// matches a plain number, with an exponent, NaN and infinity for binary floating point;
/// <c>F2</c> exactly two decimals.
/// </summary>
internal sealed class FloatingCodec : FieldCodec
{
    private const string SupportedFormats = "G, R, F or Fn (for example F2)";
    private const string RoundTrip = "R";

    private readonly Type _type;
    private readonly string? _format;
    private readonly IFormatProvider _culture;
    private readonly NumberFormatInfo _numbers;
    private readonly int? _decimals;

    public FloatingCodec(Type type, string? format, IFormatProvider culture)
    {
        _type = type;
        _format = format;
        _culture = culture;
        _numbers = NumberFormatInfo.GetInstance(culture);
        _decimals = ParseFixedDecimals(type, format, _numbers);
    }

    public static bool Handles(Type type) => type == typeof(decimal) || type == typeof(double) || type == typeof(float);

    private bool IsBinary => _type != typeof(decimal);

    public override string Fragment => _decimals.HasValue ? FixedFragment(_decimals.Value) : GeneralFragment();

    public override object? Parse(string text) => ValueConverter.Convert(text, _type, _culture);

    public override string Format(object? value)
    {
        string? format = _format ?? (IsBinary ? RoundTrip : null);
        return ConverterCodec.Render(value, format, _culture);
    }

    private string Sign => $"(?:{RegexText.Escape(_numbers.NegativeSign)})?";

    // The Arabic decimal separator is accepted next to the culture's own, because parsing maps it to the culture's
    // separator (as it maps Arabic-Indic and other digits that \d matches).
    private string Separator => _numbers.NumberDecimalSeparator == ArabicDecimalSeparator
        ? RegexText.Escape(ArabicDecimalSeparator)
        : $"(?:{RegexText.Escape(_numbers.NumberDecimalSeparator)}|{ArabicDecimalSeparator})";

    private const string ArabicDecimalSeparator = "٫";

    private string FixedFragment(int decimals)
    {
        if (decimals == 0)
        {
            return $@"{Sign}\d+";
        }

        string count = decimals.ToString(CultureInfo.InvariantCulture);
        return $@"{Sign}\d+{Separator}\d{{{count}}}";
    }

    private string GeneralFragment()
    {
        string number = $@"{Sign}\d+(?:{Separator}\d+)?";
        if (!IsBinary)
        {
            return number;
        }

        string positive = RegexText.Escape(_numbers.PositiveSign);
        string negative = RegexText.Escape(_numbers.NegativeSign);
        string exponentSign = $"(?:{positive}|{negative})?";
        string specials = string.Join("|",
            RegexText.Escape(_numbers.NegativeInfinitySymbol),
            RegexText.Escape(_numbers.PositiveInfinitySymbol),
            RegexText.Escape(_numbers.NaNSymbol));
        return $@"(?:{specials}|{number}(?:[eE]{exponentSign}\d+)?)";
    }

    private static int? ParseFixedDecimals(Type type, string? format, NumberFormatInfo numbers)
    {
        if (format == null)
        {
            return null;
        }

        char specifier = char.ToUpperInvariant(format[0]);
        bool general = format.Length == 1 && (specifier == 'G' || (specifier == 'R' && type != typeof(decimal)));
        if (general)
        {
            return null;
        }

        if (specifier != 'F')
        {
            throw TemplateErrors.UnsupportedFormat(type, format, SupportedFormats);
        }

        return FormatWidth.Parse(format, type, SupportedFormats) ?? numbers.NumberDecimalDigits;
    }
}

/// <summary>Reads the precision digits of a standard numeric format such as <c>D4</c>.</summary>
internal static class FormatWidth
{
    private const int MaxWidth = 99;

    /// <summary>Returns the precision after the specifier letter, or null when there is none.</summary>
    public static int? Parse(string format, Type type, string supportedFormats)
    {
        if (format.Length == 1)
        {
            return null;
        }

        bool parsed = int.TryParse(format.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int width);
        if (parsed && width <= MaxWidth)
        {
            return width;
        }

        throw TemplateErrors.UnsupportedFormat(type, format, supportedFormats);
    }
}
