#nullable enable

using System;
using System.Collections.Generic;

namespace Masterly.FluentRegex.Templates;

/// <summary>A <see cref="bool"/> member: <c>true</c> or <c>false</c> in any case, written in lower case.</summary>
internal sealed class BoolCodec : FieldCodec
{
    private const string TrueText = "true";
    private const string FalseText = "false";

    public override string Fragment => $"(?i:{TrueText}|{FalseText})";

    public override object? Parse(string text) => bool.Parse(text);

    public override string Format(object? value) => value is bool flag && flag ? TrueText : FalseText;
}

/// <summary>A <see cref="Guid"/> member in format D (default), N, B or P.</summary>
internal sealed class GuidCodec : FieldCodec
{
    private const string Hex = "[0-9A-Fa-f]";

    private static readonly string Hyphenated = $"{Hex}{{8}}-{Hex}{{4}}-{Hex}{{4}}-{Hex}{{4}}-{Hex}{{12}}";

    private static readonly Dictionary<string, string> Fragments =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["D"] = Hyphenated,
        ["N"] = $"{Hex}{{32}}",
        ["B"] = $@"\{{{Hyphenated}\}}",
        ["P"] = $@"\({Hyphenated}\)"
    };

    private readonly string _format;

    public GuidCodec(string? format)
    {
        _format = format ?? "D";
        if (!Fragments.ContainsKey(_format))
        {
            throw TemplateErrors.UnsupportedFormat(typeof(Guid), format, "D, N, B or P");
        }
    }

    public override string Fragment => Fragments[_format];

    public override object? Parse(string text) => Guid.ParseExact(text, _format);

    public override string Format(object? value) => value is Guid guid ? guid.ToString(_format) : string.Empty;
}

/// <summary>An enum member: one of the member names, in any case.</summary>
internal sealed class EnumCodec : FieldCodec
{
    private readonly Type _type;

    public EnumCodec(Type type)
    {
        _type = type;
        Fragment = RegexText.WordAlternation(Enum.GetNames(type));
    }

    public override string Fragment { get; }

    public override object? Parse(string text) => Enum.Parse(_type, text, ignoreCase: true);

    public override string Format(object? value)
    {
        if (value == null || !Enum.IsDefined(_type, value))
        {
            string names = string.Join(", ", Enum.GetNames(_type));
            throw new ArgumentException(
                $"The {_type.Name} value '{value}' is not a single named member, " +
                $"so it cannot be written as one of: {names}.",
                nameof(value));
        }

        return value.ToString() ?? string.Empty;
    }
}
