#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Templates;

/// <summary>
/// Compiles a template string against the members of <typeparamref name="T"/>, which bind exactly as
/// they do for <see cref="Pattern.Extract{T}(string, IFormatProvider)"/>.
/// </summary>
internal sealed class TemplateCompiler<[DynamicallyAccessedMembers(Template.BindableMembers)] T>
{
    private readonly IFormatProvider _culture;
    private readonly RegexOptions _options;
    private readonly Dictionary<string, BindableMember> _members =
        new Dictionary<string, BindableMember>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, TemplateField> _fields =
        new Dictionary<string, TemplateField>(StringComparer.OrdinalIgnoreCase);

    private readonly List<TemplatePart> _parts = new List<TemplatePart>();

    private TemplateCompiler(IFormatProvider culture, RegexOptions options)
    {
        _culture = culture;
        _options = options;
        foreach (BindableMember member in TypeBinder<T>.GetMembers())
        {
            if (!_members.ContainsKey(member.GroupName))
            {
                _members[member.GroupName] = member;
            }
        }
    }

    /// <summary>Compiles the template.</summary>
    /// <exception cref="ArgumentException">
    /// The template is malformed, names an unknown member, leaves a required constructor parameter
    /// unbound, or uses a format the member type does not support.
    /// </exception>
    public static CompiledTemplate Compile(string template, IFormatProvider culture, RegexOptions options)
    {
        TemplateCompiler<T> compiler = new TemplateCompiler<T>(culture, options);
        IReadOnlyList<TemplateSegment> segments = TemplateParser.Parse(template);
        for (int i = 0; i < segments.Count; i++)
        {
            compiler.Add(segments[i], i == segments.Count - 1);
        }

        compiler.CheckRequiredMembers();
        return new CompiledTemplate(compiler._parts, compiler._fields, options);
    }

    private void Add(TemplateSegment segment, bool isLast)
    {
        if (segment is PlaceholderSegment placeholder)
        {
            _parts.Add(CompilePlaceholder(placeholder, isLast));
            return;
        }

        _parts.Add(TemplatePart.ForLiteral(((LiteralSegment)segment).Text));
    }

    private TemplatePart CompilePlaceholder(PlaceholderSegment placeholder, bool isLast)
    {
        BindableMember member = Resolve(placeholder);
        if (_fields.TryGetValue(member.GroupName, out TemplateField? first))
        {
            // A repeated placeholder must read the same text again, so it becomes a backreference.
            RequireSameFormat(first.Placeholder, placeholder);
            return TemplatePart.ForField(first, $@"\k<{member.GroupName}>");
        }

        FieldCodec codec = FieldCodecFactory.Create(member.Type, placeholder.Format, _culture, isLast);
        TemplateField field = new TemplateField(placeholder, member, codec, _options);
        _fields[member.GroupName] = field;
        return TemplatePart.ForField(field, $"(?<{member.GroupName}>{codec.Fragment})");
    }

    private BindableMember Resolve(PlaceholderSegment placeholder)
    {
        if (_members.TryGetValue(placeholder.Name, out BindableMember? member))
        {
            return member;
        }

        throw TemplateErrors.Invalid(
            $"The placeholder {placeholder.Source} at position {placeholder.Position} does not match any member " +
            $"of {typeof(T).Name}. Available: {AvailableMembers()}.");
    }

    private static void RequireSameFormat(PlaceholderSegment first, PlaceholderSegment repeat)
    {
        if (!string.Equals(first.Format, repeat.Format, StringComparison.Ordinal))
        {
            throw TemplateErrors.Invalid(
                $"The placeholder {repeat.Source} at position {repeat.Position} repeats {first.Source} " +
                "with a different format.");
        }
    }

    private void CheckRequiredMembers()
    {
        List<string> missing = _members.Values
            .Where(member => member.IsRequired && !_fields.ContainsKey(member.GroupName))
            .Select(member => $"{{{member.GroupName}}}")
            .ToList();
        if (missing.Count > 0)
        {
            throw TemplateErrors.Invalid(
                $"The template has no placeholder for the required constructor parameter(s) " +
                $"{string.Join(", ", missing)} of {typeof(T).Name}. " +
                "Add the placeholder, or give the parameter a default value.");
        }
    }

    private string AvailableMembers()
    {
        if (_members.Count == 0)
        {
            return "(none: the type has no bindable constructor parameters or settable properties)";
        }

        IEnumerable<string> names = _members.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => $"{{{name}}}");
        return string.Join(", ", names);
    }
}
