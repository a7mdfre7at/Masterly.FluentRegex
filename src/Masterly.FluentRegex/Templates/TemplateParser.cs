#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace Masterly.FluentRegex.Templates;

/// <summary>A literal run or a placeholder of a template string.</summary>
internal abstract class TemplateSegment
{
}

/// <summary>Literal template text, with <c>{{</c> and <c>}}</c> already unescaped.</summary>
internal sealed class LiteralSegment : TemplateSegment
{
    public LiteralSegment(string text) => Text = text;

    public string Text { get; }
}

/// <summary>A <c>{Name}</c> or <c>{Name:format}</c> placeholder.</summary>
internal sealed class PlaceholderSegment : TemplateSegment
{
    public PlaceholderSegment(string name, string? format, int position, string source)
    {
        Name = name;
        Format = format;
        Position = position;
        Source = source;
    }

    public string Name { get; }

    /// <summary>Gets the text after the first colon, or null when there is none.</summary>
    public string? Format { get; }

    /// <summary>Gets the index of the opening brace in the template.</summary>
    public int Position { get; }

    /// <summary>Gets the placeholder as written, braces included.</summary>
    public string Source { get; }
}

/// <summary>
/// Splits a template such as <c>{Date:yyyy-MM-dd} [{Level}]</c> into literal and placeholder segments.
/// </summary>
internal sealed class TemplateParser
{
    private const char Open = '{';
    private const char Close = '}';
    private const char FormatSeparator = ':';
    private const char Escape = '\\';

    private readonly string _template;
    private readonly List<TemplateSegment> _segments = new List<TemplateSegment>();
    private readonly StringBuilder _literal = new StringBuilder();
    private int _position;

    private TemplateParser(string template) => _template = template;

    /// <summary>Parses the template.</summary>
    /// <exception cref="ArgumentException">A brace is unbalanced or a placeholder is malformed.</exception>
    public static IReadOnlyList<TemplateSegment> Parse(string template)
    {
        TemplateParser parser = new TemplateParser(template);
        parser.Run();
        return parser._segments;
    }

    private void Run()
    {
        while (_position < _template.Length)
        {
            char c = _template[_position];
            if (IsDoubled(c))
            {
                _literal.Append(c);
                _position += 2;
            }
            else if (c == Open)
            {
                ReadPlaceholder();
            }
            else if (c == Close)
            {
                throw Error($"Unexpected '}}' at position {_position}. Write '}}}}' for a literal brace.");
            }
            else
            {
                _literal.Append(c);
                _position++;
            }
        }

        FlushLiteral();
    }

    private bool IsDoubled(char c) =>
        (c == Open || c == Close) && _position + 1 < _template.Length && _template[_position + 1] == c;

    private void ReadPlaceholder()
    {
        FlushLiteral();
        int start = _position;
        int end = FindClose(start);
        string content = _template.Substring(start + 1, end - start - 1);
        int colon = content.IndexOf(FormatSeparator);
        string name = colon < 0 ? content : content.Substring(0, colon);
        string? format = colon < 0 ? null : content.Substring(colon + 1);
        ValidateName(name, start);
        if (format != null && format.Length == 0)
        {
            throw Error($"The placeholder at position {start} has an empty format. Remove the ':' or add a format.");
        }

        _segments.Add(new PlaceholderSegment(name, format, start, _template.Substring(start, end - start + 1)));
        _position = end + 1;
    }

    // Formats may contain braces (for example a regex quantifier {3}), so nesting is counted
    // and backslash-escaped characters are skipped.
    private int FindClose(int start)
    {
        int depth = 0;
        for (int i = start; i < _template.Length; i++)
        {
            char c = _template[i];
            if (c == Escape)
            {
                i++;
            }
            else if (c == Open)
            {
                depth++;
            }
            else if (c == Close && --depth == 0)
            {
                return i;
            }
        }

        throw Error($"The placeholder starting at position {start} is not closed. Write '{{{{' for a literal brace.");
    }

    private static void ValidateName(string name, int position)
    {
        bool valid = name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_');
        foreach (char c in name)
        {
            valid &= char.IsLetterOrDigit(c) || c == '_';
        }

        if (!valid)
        {
            throw Error(
                $"Invalid placeholder name '{name}' at position {position}. " +
                "Use a member name such as {Date} or {Date:yyyy-MM-dd}.");
        }
    }

    private void FlushLiteral()
    {
        if (_literal.Length > 0)
        {
            _segments.Add(new LiteralSegment(_literal.ToString()));
            _literal.Clear();
        }
    }

    private static ArgumentException Error(string message) => TemplateErrors.Invalid(message);
}
