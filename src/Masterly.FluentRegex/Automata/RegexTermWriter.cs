#nullable enable

using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>Writes <see cref="RegexTerm"/>s as .NET regex text.</summary>
internal sealed class RegexTermWriter
{
    private const string MatchNothing = @"[^\s\S]";
    private const string FinalNewline = "\n";

    private readonly CharClassFormatter _classes;
    private readonly RegexOptions _options;

    public RegexTermWriter(RegexOptions options)
    {
        _options = options;
        _classes = new CharClassFormatter(options);
    }

    /// <summary>
    /// Writes a full-match regex for <paramref name="term"/>: anchored at both ends so that
    /// <c>IsMatch</c> agrees with membership in the language.
    /// </summary>
    public string WriteAnchored(RegexTerm term, TermFactory terms)
    {
        bool multiline = (_options & RegexOptions.Multiline) != 0;
        string start = multiline ? @"\A" : "^";
        RegexTerm? body = multiline ? null : WithoutOptionalFinalNewline(term, terms);
        if (body != null)
        {
            return $"{start}{WriteGrouped(body)}$";
        }
        return $"{start}{WriteGrouped(term)}\\z";
    }

    /// <summary>Writes a term without anchors.</summary>
    public string Write(RegexTerm term)
    {
        StringBuilder builder = new StringBuilder();
        Append(builder, term);
        return builder.ToString();
    }

    private string WriteGrouped(RegexTerm term)
    {
        string text = Write(term);
        return term.Kind == TermKind.Alternation ? $"(?:{text})" : text;
    }

    /// <summary>Recognizes <c>x\n?</c>, which is what <c>x$</c> means, and returns <c>x</c>.</summary>
    private static RegexTerm? WithoutOptionalFinalNewline(RegexTerm term, TermFactory terms)
    {
        if (IsOptionalNewline(term))
        {
            return terms.Empty;
        }
        if (term.Kind != TermKind.Concat || !IsOptionalNewline(term.Parts[term.Parts.Count - 1]))
        {
            return null;
        }

        RegexTerm[] parts = new RegexTerm[term.Parts.Count - 1];
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = term.Parts[i];
        }
        return terms.Concat(parts);
    }

    private static bool IsOptionalNewline(RegexTerm term)
    {
        return term.Kind == TermKind.Repeat && term.Min == 0 && term.Max == 1 &&
               term.Body.Kind == TermKind.Class && SetOf(term.Body).Count == 1 &&
               SetOf(term.Body).Contains(FinalNewline[0]);
    }

    private static CharSet SetOf(RegexTerm term) =>
        term.Set ?? throw new InvalidOperationException("A class term has no character set.");

    private void Append(StringBuilder builder, RegexTerm term)
    {
        switch (term.Kind)
        {
            case TermKind.Nothing:
                builder.Append(MatchNothing);
                return;
            case TermKind.Empty:
                return;
            case TermKind.Class:
                builder.Append(_classes.Format(SetOf(term)));
                return;
            case TermKind.Concat:
                AppendConcat(builder, term);
                return;
            case TermKind.Alternation:
                AppendAlternation(builder, term);
                return;
            default:
                AppendRepeat(builder, term);
                return;
        }
    }

    private void AppendConcat(StringBuilder builder, RegexTerm term)
    {
        foreach (RegexTerm part in term.Parts)
        {
            AppendWrapped(builder, part, part.Kind == TermKind.Alternation);
        }
    }

    private void AppendAlternation(StringBuilder builder, RegexTerm term)
    {
        for (int i = 0; i < term.Parts.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('|');
            }
            Append(builder, term.Parts[i]);
        }
    }

    private void AppendRepeat(StringBuilder builder, RegexTerm term)
    {
        bool atomic = term.Body.Kind == TermKind.Class || term.Body.Kind == TermKind.Nothing;
        AppendWrapped(builder, term.Body, !atomic);
        builder.Append(Quantifier(term.Min, term.Max));
    }

    private void AppendWrapped(StringBuilder builder, RegexTerm term, bool wrap)
    {
        if (wrap)
        {
            builder.Append("(?:");
        }
        Append(builder, term);
        if (wrap)
        {
            builder.Append(')');
        }
    }

    private static string Quantifier(int min, int max)
    {
        string low = min.ToString(CultureInfo.InvariantCulture);
        if (max == RegexTerm.Unbounded)
        {
            return min == 0 ? "*" : min == 1 ? "+" : $"{{{low},}}";
        }
        if (min == 0 && max == 1)
        {
            return "?";
        }
        string high = max.ToString(CultureInfo.InvariantCulture);
        return min == max ? $"{{{low}}}" : $"{{{low},{high}}}";
    }
}
