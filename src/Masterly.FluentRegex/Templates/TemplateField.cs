#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Templates;

/// <summary>A placeholder bound to a member of the target type.</summary>
internal sealed class TemplateField
{
    private readonly RegexOptions _options;
    private Regex? _validator;

    public TemplateField(PlaceholderSegment placeholder, BindableMember member, FieldCodec codec, RegexOptions options)
    {
        Placeholder = placeholder;
        Member = member;
        Codec = codec;
        _options = options;
    }

    public PlaceholderSegment Placeholder { get; }

    public BindableMember Member { get; }

    public FieldCodec Codec { get; }

    public string GroupName => Member.GroupName;

    /// <summary>Returns true when <paramref name="text"/> is exactly what the field's fragment matches.</summary>
    public bool CanRepresent(string text)
    {
        Regex validator = _validator ??= new Regex($@"\A(?:{Codec.Fragment})\z", _options);
        return validator.IsMatch(text);
    }
}

/// <summary>One piece of a compiled template: literal text or a field, with its regex.</summary>
internal sealed class TemplatePart
{
    private TemplatePart(string? literal, TemplateField? field, string regex)
    {
        Literal = literal;
        Field = field;
        Regex = regex;
    }

    /// <summary>Gets the literal text, or null for a field.</summary>
    public string? Literal { get; }

    /// <summary>Gets the field, or null for literal text.</summary>
    public TemplateField? Field { get; }

    /// <summary>Gets the regex that matches this part.</summary>
    public string Regex { get; }

    public static TemplatePart ForLiteral(string text) => new TemplatePart(text, null, RegexText.Escape(text));

    public static TemplatePart ForField(TemplateField field, string regex) => new TemplatePart(null, field, regex);

    /// <summary>Describes the part for error messages.</summary>
    public string Describe() => Field != null ? $"placeholder {Field.Placeholder.Source}" : $"the text \"{Literal}\"";
}

/// <summary>The result of compiling a template against a target type.</summary>
internal sealed class CompiledTemplate
{
    public CompiledTemplate(
        IReadOnlyList<TemplatePart> parts,
        IReadOnlyDictionary<string, TemplateField> fieldsByGroup,
        RegexOptions options)
    {
        Parts = parts;
        FieldsByGroup = fieldsByGroup;
        Options = options;
        Body = string.Concat(parts.Select(part => part.Regex));
    }

    public IReadOnlyList<TemplatePart> Parts { get; }

    /// <summary>Gets the fields by group name, case-insensitively.</summary>
    public IReadOnlyDictionary<string, TemplateField> FieldsByGroup { get; }

    public RegexOptions Options { get; }

    /// <summary>Gets the unanchored regex of the whole template.</summary>
    public string Body { get; }
}
