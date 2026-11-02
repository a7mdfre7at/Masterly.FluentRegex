#nullable enable

using System;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Templates;

/// <summary>Chooses the codec, and so the regex fragment, for a placeholder from its member type and format.</summary>
internal static class FieldCodecFactory
{
    private const string RegexPrefix = "re:";
    private const string SingleChar = @"[^\r\n]";

    /// <summary>Creates the codec for a placeholder.</summary>
    /// <param name="type">The member type; <see cref="Nullable{T}"/> makes the field optional.</param>
    /// <param name="format">The placeholder format, or null.</param>
    /// <param name="culture">The template culture.</param>
    /// <param name="isLast">True when the placeholder ends the template, so a string may be greedy.</param>
    /// <exception cref="ArgumentException">
    /// The format is not supported for the type, or a <c>re:</c> regex is invalid.
    /// </exception>
    public static FieldCodec Create(Type type, string? format, IFormatProvider culture, bool isLast)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        FieldCodec codec = CreateCore(underlying ?? type, format, culture, isLast);
        return underlying != null ? new OptionalCodec(codec) : codec;
    }

    private static FieldCodec CreateCore(Type type, string? format, IFormatProvider culture, bool isLast)
    {
        if (format != null && format.StartsWith(RegexPrefix, StringComparison.Ordinal))
        {
            string regex = ValidateRegex(format.Substring(RegexPrefix.Length));
            return new RegexCodec(CreateCore(type, null, culture, isLast), regex);
        }

        if (ExtractionConverters.TryGet(type, out _))
        {
            return WithoutFormat(type, format, new ConverterCodec(type, culture, isLast, null));
        }

        return CreateBuiltIn(type, format, culture, isLast);
    }

    private static FieldCodec CreateBuiltIn(Type type, string? format, IFormatProvider culture, bool isLast)
    {
        if (type == typeof(string) || type == typeof(object))
        {
            return WithoutFormat(type, format, new StringCodec(isLast));
        }

        if (type.IsEnum)
        {
            return WithoutFormat(type, format, new EnumCodec(type));
        }

        if (IntegerCodec.Handles(type))
        {
            return new IntegerCodec(type, format, culture);
        }

        if (FloatingCodec.Handles(type))
        {
            return new FloatingCodec(type, format, culture);
        }

        return CreateOther(type, format, culture, isLast);
    }

    private static FieldCodec CreateOther(Type type, string? format, IFormatProvider culture, bool isLast)
    {
        if (type == typeof(bool))
        {
            return WithoutFormat(type, format, new BoolCodec());
        }

        if (type == typeof(Guid))
        {
            return new GuidCodec(format);
        }

        if (type == typeof(char))
        {
            FieldCodec converter = new ConverterCodec(type, culture, isLast, null);
            return WithoutFormat(type, format, new RegexCodec(converter, SingleChar));
        }

        if (TemporalCodec.Handles(type))
        {
            return format == null
                ? new ConverterCodec(type, culture, isLast, TemporalCodec.DefaultRenderFormat(type))
                : new TemporalCodec(type, format, culture);
        }

        return WithoutFormat(type, format, new ConverterCodec(type, culture, isLast, null));
    }

    private static FieldCodec WithoutFormat(Type type, string? format, FieldCodec codec)
    {
        if (format != null)
        {
            throw TemplateErrors.Invalid(
                $"{type.Name} placeholders do not take a format ('{format}'). " +
                "Use {Name:re:...} to give an explicit regex.");
        }

        return codec;
    }

    private static string ValidateRegex(string regex)
    {
        if (regex.Length == 0)
        {
            throw TemplateErrors.Invalid("The 're:' format needs a regex, for example {Sku:re:[A-Z]{3}\\d+}.");
        }

        try
        {
            _ = new Regex(regex);
        }
        catch (ArgumentException ex)
        {
            string message = $"The placeholder regex '{regex}' is invalid: {ex.Message}";
            throw new ArgumentException(message, TemplateErrors.ParameterName, ex);
        }

        return regex;
    }
}
