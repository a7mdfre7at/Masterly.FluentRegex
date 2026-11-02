#nullable enable

using System;

namespace Masterly.FluentRegex.Templates;

/// <summary>
/// Knows how one placeholder looks in text (its regex fragment), how to read it, and how to write it.
/// </summary>
internal abstract class FieldCodec
{
    /// <summary>Line text without line breaks, so strings never run into the next line.</summary>
    protected const string LineChars = @"[^\r\n]";

    /// <summary>Gets the regex fragment that matches the field's text.</summary>
    public abstract string Fragment { get; }

    /// <summary>Converts matched text to the member value.</summary>
    public abstract object? Parse(string text);

    /// <summary>Renders a member value as text.</summary>
    public abstract string Format(object? value);

    /// <summary>Returns a lazy run of line characters, or a greedy one for the final placeholder.</summary>
    protected static string Run(string quantifier, bool isLast)
    {
        string laziness = isLast ? string.Empty : "?";
        return $"{LineChars}{quantifier}{laziness}";
    }
}

/// <summary>A string member: any text on one line, lazy unless it is the last placeholder.</summary>
internal sealed class StringCodec : FieldCodec
{
    private readonly bool _isLast;

    public StringCodec(bool isLast) => _isLast = isLast;

    public override string Fragment => Run("*", _isLast);

    public override object? Parse(string text) => text;

    public override string Format(object? value) => value?.ToString() ?? string.Empty;
}

/// <summary>
/// A member parsed by the typed-extraction converters (<see cref="ExtractionConverters"/> and the
/// built-in conversions), for types without a dedicated template fragment.
/// </summary>
internal sealed class ConverterCodec : FieldCodec
{
    private readonly Type _type;
    private readonly IFormatProvider _culture;
    private readonly bool _isLast;
    private readonly string? _renderFormat;

    public ConverterCodec(Type type, IFormatProvider culture, bool isLast, string? renderFormat)
    {
        _type = type;
        _culture = culture;
        _isLast = isLast;
        _renderFormat = renderFormat;
    }

    public override string Fragment => Run("+", _isLast);

    public override object? Parse(string text) => ValueConverter.Convert(text, _type, _culture);

    public override string Format(object? value) => Render(value, _renderFormat, _culture);

    /// <summary>Renders with <see cref="IFormattable"/> when available.</summary>
    public static string Render(object? value, string? format, IFormatProvider culture)
    {
        if (value is IFormattable formattable)
        {
            return formattable.ToString(format, culture);
        }

        return value?.ToString() ?? string.Empty;
    }
}

/// <summary>A <see cref="Nullable{T}"/> member: the inner text or nothing.</summary>
internal sealed class OptionalCodec : FieldCodec
{
    private readonly FieldCodec _inner;

    public OptionalCodec(FieldCodec inner) => _inner = inner;

    public override string Fragment => $"(?:{_inner.Fragment})?";

    public override object? Parse(string text) => text.Length == 0 ? null : _inner.Parse(text);

    public override string Format(object? value) => value == null ? string.Empty : _inner.Format(value);
}

/// <summary>A member whose fragment is an explicit regex (<c>{Name:re:...}</c>).</summary>
internal sealed class RegexCodec : FieldCodec
{
    private readonly FieldCodec _inner;
    private readonly string _regex;

    public RegexCodec(FieldCodec inner, string regex)
    {
        _inner = inner;
        _regex = regex;
    }

    public override string Fragment => $"(?:{_regex})";

    public override object? Parse(string text) => _inner.Parse(text);

    public override string Format(object? value) => _inner.Format(value);
}
