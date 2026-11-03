#nullable enable

using System;
using System.Globalization;
using Masterly.FluentRegex.Unicode;

namespace Masterly.FluentRegex.Templates;

/// <summary>
/// A date or time member with an explicit format: the format becomes the regex fragment, and the
/// text is read back with <c>ParseExact</c> in the template culture.
/// </summary>
/// <remarks>
/// <see cref="DateTimeOffset"/> values without an offset in the format are read as UTC, so parsing
/// never depends on the machine's time zone. Unicode digits are mapped to ASCII before parsing.
/// </remarks>
internal sealed class TemporalCodec : FieldCodec
{
    private readonly Type _type;
    private readonly string _format;
    private readonly IFormatProvider _culture;

    public TemporalCodec(Type type, string format, IFormatProvider culture)
    {
        _type = type;
        _format = format;
        _culture = culture;
        Fragment = type == typeof(TimeSpan)
            ? TimeSpanFormatTranslator.ToRegex(format, culture)
            : DateFormatTranslator.ToRegex(format, KindOf(type), culture);
    }

    public override string Fragment { get; }

    /// <summary>Returns true for the date and time types a template can format.</summary>
    public static bool Handles(Type type)
    {
#if NET6_0_OR_GREATER
        if (type == typeof(DateOnly) || type == typeof(TimeOnly))
        {
            return true;
        }
#endif
        return type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan);
    }

    /// <summary>Gets the round-trip format used to write a value when the placeholder has no format.</summary>
    public static string DefaultRenderFormat(Type type) => type == typeof(TimeSpan) ? "c" : "O";

    public override object? Parse(string text)
    {
        string ascii = UnicodeDigits.ToAscii(text);
        if (_type == typeof(DateTime))
        {
            return DateTime.ParseExact(ascii, _format, _culture, DateTimeStyles.RoundtripKind);
        }

        if (_type == typeof(DateTimeOffset))
        {
            return DateTimeOffset.ParseExact(ascii, _format, _culture, DateTimeStyles.AssumeUniversal);
        }

        if (_type == typeof(TimeSpan))
        {
            return TimeSpan.ParseExact(ascii, _format, _culture);
        }

        return ParseDateOrTime(ascii);
    }

    public override string Format(object? value) => ConverterCodec.Render(value, _format, _culture);

    private object ParseDateOrTime(string text)
    {
#if NET6_0_OR_GREATER
        if (_type == typeof(DateOnly))
        {
            return DateOnly.ParseExact(text, _format, _culture, DateTimeStyles.None);
        }

        return TimeOnly.ParseExact(text, _format, _culture, DateTimeStyles.None);
#else
        throw new NotSupportedException($"{_type.Name} is not supported on this platform.");
#endif
    }

    private static TemporalKind KindOf(Type type)
    {
#if NET6_0_OR_GREATER
        if (type == typeof(DateOnly))
        {
            return TemporalKind.DateOnly;
        }

        if (type == typeof(TimeOnly))
        {
            return TemporalKind.TimeOnly;
        }
#endif
        return type == typeof(DateTimeOffset) ? TemporalKind.DateTimeOffset : TemporalKind.DateTime;
    }
}
