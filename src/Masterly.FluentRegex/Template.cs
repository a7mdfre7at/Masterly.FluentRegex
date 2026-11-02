#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Templates;

namespace Masterly.FluentRegex;

/// <summary>
/// Typed templates: string interpolation in reverse. A template such as
/// <c>"{Date:yyyy-MM-dd} [{Level}] {User}: {Message}"</c> parses text into a typed value and formats
/// the value back into text.
/// </summary>
public static class Template
{
    internal const DynamicallyAccessedMemberTypes BindableMembers =
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties;

    /// <summary>
    /// Compiles a template for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">
    /// A record or class. Placeholders bind by name (case-insensitive) to the parameters of the public
    /// constructor with the most parameters, then to settable properties, exactly like
    /// <see cref="Pattern.Extract{T}(string, IFormatProvider)"/>; <see cref="RegexGroupAttribute"/> renames a member.
    /// </typeparam>
    /// <param name="template">
    /// Literal text with <c>{Name}</c> or <c>{Name:format}</c> placeholders. <c>{{</c> and <c>}}</c> are
    /// literal braces. The format depends on the member type: a .NET date/time format for dates,
    /// <c>D4</c> or <c>X8</c> for integers, <c>F2</c> for floating point, <c>N</c>/<c>B</c>/<c>P</c> for
    /// <see cref="Guid"/>, or <c>re:regex</c> for an explicit regex.
    /// </param>
    /// <param name="options">Culture, case sensitivity and timeout. Null uses the defaults.</param>
    /// <returns>The compiled template.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="template"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The template is empty or malformed, names an unknown member (the message lists the available
    /// ones), leaves a required constructor parameter unbound, or uses an unsupported format.
    /// </exception>
    /// <example>
    /// record LogLine(DateTime Date, LogLevel Level, string User, string Message);
    /// Template&lt;LogLine&gt; log = Template.Of&lt;LogLine&gt;("{Date:yyyy-MM-dd} [{Level}] {User}: {Message}");
    /// LogLine line = log.Parse("2026-11-02 [WARN] ahmad: disk at 91%");
    /// string text = log.Format(line); // "2026-11-02 [Warn] ahmad: disk at 91%"
    /// </example>
    public static Template<T> Of<[DynamicallyAccessedMembers(BindableMembers)] T>(
        string template,
        TemplateOptions? options = null)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        if (template.Length == 0)
        {
            throw new ArgumentException("The template is empty.", nameof(template));
        }

        return new Template<T>(template, options ?? new TemplateOptions());
    }
}

/// <summary>
/// A compiled template that parses text into <typeparamref name="T"/> and formats
/// <typeparamref name="T"/> back into text. Create one with <see cref="Template.Of{T}(string, TemplateOptions)"/>.
/// Instances are immutable and thread-safe.
/// </summary>
/// <typeparam name="T">The record or class the placeholders bind to.</typeparam>
public sealed class Template<[DynamicallyAccessedMembers(Template.BindableMembers)] T>
{
    private readonly string _template;
    private readonly CompiledTemplate _compiled;
    private readonly IFormatProvider _culture;
    private readonly Pattern _anchored;

    internal Template(string template, TemplateOptions options)
    {
        _template = template;
        _culture = options.Culture ?? CultureInfo.InvariantCulture;
        RegexOptions regexOptions = options.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        _compiled = TemplateCompiler<T>.Compile(template, _culture, regexOptions);
        Pattern = WithTimeout(Pattern.With.RegEx(_compiled.Body).WithOptions(regexOptions), options.Timeout);
        // The body never has a top-level alternation, so it can be anchored without a wrapping group;
        // that keeps each placeholder a top-level construct, which lets Trace pinpoint failures inside it.
        Pattern anchored = Pattern.With.RegEx($@"\A{_compiled.Body}\z").WithOptions(regexOptions);
        _anchored = WithTimeout(anchored, options.Timeout);
    }

    /// <summary>
    /// Gets the pattern that matches one occurrence of the template, with a named group per placeholder.
    /// It is not anchored, so it can be composed into larger patterns or used with
    /// <see cref="Pattern.Extract{T}(string, IFormatProvider)"/>.
    /// </summary>
    public Pattern Pattern { get; }

    /// <summary>
    /// Parses <paramref name="input"/>, which must match the whole template.
    /// </summary>
    /// <param name="input">The text to parse.</param>
    /// <returns>The populated value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    /// <exception cref="FormatException">
    /// The input does not match (the message says at which placeholder and position, and what was
    /// expected), or a value cannot be converted.
    /// </exception>
    public T Parse(string input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        Match match = _anchored.CachedRegex.Match(input);
        if (!match.Success)
        {
            throw new FormatException(TemplateDiagnostics.Explain(_template, _compiled, _anchored, input));
        }

        return Bind(match, _anchored.CachedRegex);
    }

    /// <summary>Tries to parse <paramref name="input"/>, which must match the whole template.</summary>
    /// <param name="input">The text to parse.</param>
    /// <param name="value">The populated value, or the default when parsing fails.</param>
    /// <returns>True when the input matched and every value converted.</returns>
    public bool TryParse(string input, [MaybeNullWhen(false)] out T value)
    {
        value = default;
        if (input == null)
        {
            return false;
        }

        Match match = _anchored.CachedRegex.Match(input);
        if (!match.Success)
        {
            return false;
        }

        try
        {
            value = Bind(match, _anchored.CachedRegex);
            return true;
        }
        catch (FormatException)
        {
            // A converter rejected a matched value; TryParse reports that as a plain failure.
            return false;
        }
    }

    /// <summary>Finds every occurrence of the template in <paramref name="text"/>. Evaluated lazily.</summary>
    /// <param name="text">The text to scan, for example a whole log file.</param>
    /// <returns>One value per non-overlapping occurrence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">An occurrence matched but a value cannot be converted.</exception>
    public IEnumerable<T> ParseAll(string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        return ParseAllIterator(text);
    }

    /// <summary>
    /// Renders <paramref name="value"/> with the template, using the same formats and culture as parsing,
    /// so <c>Parse(Format(x))</c> round-trips.
    /// </summary>
    /// <param name="value">The value to render.</param>
    /// <returns>The formatted text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// A member value cannot be written so that its placeholder reads it back, for example a string
    /// with a line break, a number wider than a <c>D4</c> format, or a value that does not match a <c>re:</c> regex.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A placeholder binds a constructor parameter that has no readable property.
    /// </exception>
    /// <remarks>
    /// Each value is checked against its own placeholder. Values that make neighboring placeholders
    /// ambiguous (such as a user name containing the literal text that follows it) are not detected.
    /// </remarks>
    public string Format(T value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        StringBuilder sb = new StringBuilder();
        foreach (TemplatePart part in _compiled.Parts)
        {
            sb.Append(part.Field == null ? part.Literal : Render(part.Field, value));
        }

        return sb.ToString();
    }

    /// <summary>Returns the template text.</summary>
    public override string ToString() => _template;

    private IEnumerable<T> ParseAllIterator(string text)
    {
        Regex regex = Pattern.CachedRegex;
        for (Match match = regex.Match(text); match.Success; match = match.NextMatch())
        {
            yield return Bind(match, regex);
        }
    }

    private T Bind(Match match, Regex regex) => TypeBinder<T>.Bind(match, regex, _culture, ConvertGroup);

    private object? ConvertGroup(string groupName, string text, Type type, IFormatProvider provider)
    {
        if (_compiled.FieldsByGroup.TryGetValue(groupName, out TemplateField? field))
        {
            return field.Codec.Parse(text);
        }

        // A named group inside a {Name:re:...} regex can bind another member with the default conversion.
        return ValueConverter.Convert(text, type, provider);
    }

    private string Render(TemplateField field, T value)
    {
        if (field.Member.Property == null || field.Member.Property.GetMethod == null)
        {
            throw new InvalidOperationException(
                $"{typeof(T).Name} has no readable public property for {field.Placeholder.Source}, " +
                "so Format cannot read its value.");
        }

        string text = field.Codec.Format(field.Member.Property.GetValue(value));
        if (!field.CanRepresent(text))
        {
            throw new ArgumentException(
                $"The value \"{text}\" cannot be written as {field.Placeholder.Source}: " +
                $"it does not match {field.Codec.Fragment}, so it would not parse back.",
                nameof(value));
        }

        return text;
    }

    private static Pattern WithTimeout(Pattern pattern, TimeSpan? timeout) =>
        timeout.HasValue ? pattern.WithTimeout(timeout.Value) : pattern;
}
