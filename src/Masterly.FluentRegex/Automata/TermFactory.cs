#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>
/// Builds hash-consed <see cref="RegexTerm"/>s and simplifies them on construction so that
/// regexes produced from automata stay short and readable. Every rule preserves the language.
/// </summary>
internal sealed class TermFactory
{
    private readonly Dictionary<CharSet, int> _setIds = new Dictionary<CharSet, int>(CharSetComparer.Instance);
    private readonly Dictionary<string, RegexTerm> _interned = new Dictionary<string, RegexTerm>(StringComparer.Ordinal);

    public TermFactory()
    {
        Nothing = new RegexTerm(TermKind.Nothing, "!", 1);
        Empty = new RegexTerm(TermKind.Empty, "e", 1);
    }

    /// <summary>The term that matches nothing.</summary>
    public RegexTerm Nothing { get; }

    /// <summary>The term that matches only the empty string.</summary>
    public RegexTerm Empty { get; }

    /// <summary>Returns the term matching one character of <paramref name="set"/>.</summary>
    public RegexTerm Class(CharSet set)
    {
        if (set.IsEmpty)
        {
            return Nothing;
        }
        if (!_setIds.TryGetValue(set, out int id))
        {
            id = _setIds.Count;
            _setIds[set] = id;
        }
        return Intern($"c{id.ToString(CultureInfo.InvariantCulture)}", key => new RegexTerm(TermKind.Class, key, 1, set));
    }

    /// <summary>Returns the concatenation of the given terms.</summary>
    public RegexTerm Concat(params RegexTerm[] parts) => Concat((IEnumerable<RegexTerm>)parts);

    /// <summary>Returns the concatenation of the given terms.</summary>
    public RegexTerm Concat(IEnumerable<RegexTerm> parts)
    {
        List<RegexTerm> flat = new List<RegexTerm>();
        foreach (RegexTerm part in parts)
        {
            if (part.Kind == TermKind.Nothing)
            {
                return Nothing;
            }
            IEnumerable<RegexTerm> pieces = part.Kind == TermKind.Concat ? part.Parts : new[] { part };
            foreach (RegexTerm piece in pieces)
            {
                AppendMerged(flat, piece);
            }
        }

        if (flat.Count == 0)
        {
            return Empty;
        }
        return flat.Count == 1 ? flat[0] : Make(TermKind.Concat, flat);
    }

    /// <summary>Returns the alternation of the given terms.</summary>
    public RegexTerm Alternation(params RegexTerm[] options) => Alternation((IEnumerable<RegexTerm>)options);

    /// <summary>Returns the alternation of the given terms.</summary>
    public RegexTerm Alternation(IEnumerable<RegexTerm> options)
    {
        List<RegexTerm> flat = new List<RegexTerm>();
        HashSet<RegexTerm> seen = new HashSet<RegexTerm>();
        bool hasEmpty = false;
        foreach (RegexTerm option in options)
        {
            IEnumerable<RegexTerm> pieces = option.Kind == TermKind.Alternation ? option.Parts : new[] { option };
            foreach (RegexTerm piece in pieces)
            {
                hasEmpty |= piece.Kind == TermKind.Empty;
                if (piece.Kind != TermKind.Nothing && piece.Kind != TermKind.Empty && seen.Add(piece))
                {
                    flat.Add(piece);
                }
            }
        }

        List<RegexTerm> simplified = FactorSuffixes(FactorPrefixes(MergeClasses(flat)));
        RegexTerm core = simplified.Count == 0 ? (hasEmpty ? Empty : Nothing)
            : simplified.Count == 1 ? simplified[0]
            : Make(TermKind.Alternation, simplified);
        return hasEmpty && !core.IsNullable ? Repeat(core, 0, 1) : core;
    }

    /// <summary>Returns <paramref name="body"/> repeated between <paramref name="min"/> and <paramref name="max"/> times.</summary>
    public RegexTerm Repeat(RegexTerm body, int min, int max)
    {
        if (body.Kind == TermKind.Nothing)
        {
            return min == 0 ? Empty : Nothing;
        }
        if (body.Kind == TermKind.Empty || max == 0)
        {
            return Empty;
        }
        if (min == 1 && max == 1)
        {
            return body;
        }
        if (body.Kind == TermKind.Repeat)
        {
            RegexTerm? collapsed = TryCollapseNested(body, min, max);
            if (collapsed != null)
            {
                return collapsed;
            }
        }

        string key = $"r{body.Key}:{min.ToString(CultureInfo.InvariantCulture)},{max.ToString(CultureInfo.InvariantCulture)}";
        return Intern(key, k => new RegexTerm(TermKind.Repeat, k, CheckedSize(body.Size + 1), parts: new[] { body }, min: min, max: max));
    }

    /// <summary>Returns the term for "zero or more" repetitions.</summary>
    public RegexTerm Star(RegexTerm body) => Repeat(body, 0, RegexTerm.Unbounded);

    // ------------------------------------------------------------------
    // Simplification rules
    // ------------------------------------------------------------------

    private void AppendMerged(List<RegexTerm> flat, RegexTerm next)
    {
        if (next.Kind == TermKind.Empty)
        {
            return;
        }
        if (flat.Count > 0)
        {
            RegexTerm? merged = TryMergeRun(flat[flat.Count - 1], next);
            if (merged != null)
            {
                flat[flat.Count - 1] = merged;
                return;
            }
        }
        flat.Add(next);
        MergeSequenceRun(flat);
    }

    /// <summary>
    /// Merges <c>ab(?:ab)*</c> and <c>(?:ab)*ab</c> into <c>(?:ab)+</c>: a repeated sequence next
    /// to one more copy of itself.
    /// </summary>
    private void MergeSequenceRun(List<RegexTerm> flat)
    {
        int last = flat.Count - 1;
        RegexTerm candidate = flat[last];
        if (IsSequenceRepeat(candidate) && EndsWith(flat, last, candidate.Body.Parts))
        {
            ReplaceRun(flat, last - candidate.Body.Parts.Count, candidate);
            return;
        }

        for (int index = last - 1; index >= 0; index--)
        {
            RegexTerm repeat = flat[index];
            if (IsSequenceRepeat(repeat) && index + repeat.Body.Parts.Count == last && EndsWith(flat, flat.Count, repeat.Body.Parts))
            {
                ReplaceRun(flat, index, repeat);
                return;
            }
        }
    }

    private void ReplaceRun(List<RegexTerm> flat, int start, RegexTerm repeat)
    {
        int max = repeat.Max == RegexTerm.Unbounded ? RegexTerm.Unbounded : repeat.Max + 1;
        RegexTerm merged = Repeat(repeat.Body, repeat.Min + 1, max);
        flat.RemoveRange(start, flat.Count - start);
        flat.Add(merged);
    }

    private static bool IsSequenceRepeat(RegexTerm term) =>
        term.Kind == TermKind.Repeat && term.Body.Kind == TermKind.Concat;

    /// <summary>Returns true when the items just before <paramref name="end"/> are exactly <paramref name="parts"/>.</summary>
    private static bool EndsWith(List<RegexTerm> flat, int end, IReadOnlyList<RegexTerm> parts)
    {
        int start = end - parts.Count;
        if (start < 0)
        {
            return false;
        }
        for (int i = 0; i < parts.Count; i++)
        {
            if (!ReferenceEquals(flat[start + i], parts[i]))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Merges <c>x{a,b} x{c,d}</c> into <c>x{a+c,b+d}</c>.</summary>
    private RegexTerm? TryMergeRun(RegexTerm previous, RegexTerm next)
    {
        RegexTerm previousBody = previous.Kind == TermKind.Repeat ? previous.Body : previous;
        RegexTerm nextBody = next.Kind == TermKind.Repeat ? next.Body : next;
        if (!ReferenceEquals(previousBody, nextBody))
        {
            return null;
        }

        int min = previous.Min + next.Min;
        bool unbounded = previous.Max == RegexTerm.Unbounded || next.Max == RegexTerm.Unbounded;
        int max = unbounded ? RegexTerm.Unbounded : previous.Max + next.Max;
        return Repeat(previousBody, min, max);
    }

    /// <summary>Collapses <c>(x?)*</c>, <c>(x+)?</c>, <c>(x{2}){3}</c> and similar nestings.</summary>
    private RegexTerm? TryCollapseNested(RegexTerm inner, int min, int max)
    {
        bool innerSimple = inner.Min <= 1 && (inner.Max == 1 || inner.Max == RegexTerm.Unbounded);
        bool outerSimple = min <= 1 && (max == 1 || max == RegexTerm.Unbounded);
        if (innerSimple && outerSimple)
        {
            int collapsedMin = inner.Min == 0 || min == 0 ? 0 : 1;
            bool unbounded = inner.Max == RegexTerm.Unbounded || max == RegexTerm.Unbounded;
            return Repeat(inner.Body, collapsedMin, unbounded ? RegexTerm.Unbounded : 1);
        }

        bool bothFixed = inner.Min == inner.Max && min == max;
        return bothFixed ? Repeat(inner.Body, inner.Min * min, inner.Min * min) : null;
    }

    private List<RegexTerm> MergeClasses(List<RegexTerm> options)
    {
        int classCount = 0;
        CharSet union = CharSet.Empty;
        foreach (RegexTerm option in options)
        {
            if (option.Kind == TermKind.Class)
            {
                classCount++;
                union = union.Union(option.Set);
            }
        }
        if (classCount < 2)
        {
            return options;
        }

        List<RegexTerm> merged = new List<RegexTerm>(options.Count);
        bool placed = false;
        foreach (RegexTerm option in options)
        {
            if (option.Kind != TermKind.Class)
            {
                merged.Add(option);
            }
            else if (!placed)
            {
                merged.Add(Class(union));
                placed = true;
            }
        }
        return merged;
    }

    private List<RegexTerm> FactorPrefixes(List<RegexTerm> options) =>
        Factor(options, Head, Tail, (common, rests) => Concat(common, Alternation(rests)));

    private List<RegexTerm> FactorSuffixes(List<RegexTerm> options) =>
        Factor(options, Last, Init, (common, rests) => Concat(Alternation(rests), common));

    private static List<RegexTerm> Factor(
        List<RegexTerm> options,
        Func<RegexTerm, RegexTerm> common,
        Func<RegexTerm, RegexTerm> rest,
        Func<RegexTerm, List<RegexTerm>, RegexTerm> rebuild)
    {
        if (options.Count < 2)
        {
            return options;
        }

        Dictionary<RegexTerm, List<RegexTerm>> groups = new Dictionary<RegexTerm, List<RegexTerm>>();
        List<RegexTerm> order = new List<RegexTerm>();
        foreach (RegexTerm option in options)
        {
            RegexTerm key = common(option);
            if (!groups.TryGetValue(key, out List<RegexTerm>? members))
            {
                members = new List<RegexTerm>();
                groups[key] = members;
                order.Add(key);
            }
            members.Add(option);
        }
        if (order.Count == options.Count)
        {
            return options;
        }

        List<RegexTerm> result = new List<RegexTerm>(order.Count);
        foreach (RegexTerm key in order)
        {
            List<RegexTerm> members = groups[key];
            result.Add(members.Count == 1 ? members[0] : rebuild(key, members.ConvertAll(new Converter<RegexTerm, RegexTerm>(rest))));
        }
        return result;
    }

    private static RegexTerm Head(RegexTerm term) => term.Kind == TermKind.Concat ? term.Parts[0] : term;

    private static RegexTerm Last(RegexTerm term) => term.Kind == TermKind.Concat ? term.Parts[term.Parts.Count - 1] : term;

    private RegexTerm Tail(RegexTerm term) => term.Kind == TermKind.Concat ? Concat(Slice(term.Parts, 1, term.Parts.Count)) : Empty;

    private RegexTerm Init(RegexTerm term) => term.Kind == TermKind.Concat ? Concat(Slice(term.Parts, 0, term.Parts.Count - 1)) : Empty;

    private static IEnumerable<RegexTerm> Slice(IReadOnlyList<RegexTerm> parts, int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            yield return parts[i];
        }
    }

    // ------------------------------------------------------------------
    // Interning
    // ------------------------------------------------------------------

    private RegexTerm Make(TermKind kind, List<RegexTerm> parts)
    {
        StringBuilder key = new StringBuilder(kind == TermKind.Concat ? "(" : "[");
        int size = 1;
        foreach (RegexTerm part in parts)
        {
            key.Append(part.Key).Append(' ');
            size += part.Size;
        }
        int checkedSize = CheckedSize(size);
        return Intern(key.ToString(), k => new RegexTerm(kind, k, checkedSize, parts: parts.ToArray()));
    }

    private RegexTerm Intern(string signature, Func<string, RegexTerm> create)
    {
        if (_interned.TryGetValue(signature, out RegexTerm? existing))
        {
            return existing;
        }
        string key = $"#{_interned.Count.ToString(CultureInfo.InvariantCulture)}";
        RegexTerm term = create(key);
        _interned[signature] = term;
        return term;
    }

    private static int CheckedSize(int size)
    {
        if (size > AlgebraLimits.MaxTermSize)
        {
            throw new InvalidOperationException("The resulting language is too complex to express as a readable regular expression.");
        }
        return size;
    }
}
