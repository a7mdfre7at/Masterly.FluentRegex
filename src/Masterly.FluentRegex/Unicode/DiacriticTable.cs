#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex.Unicode;

/// <summary>
/// Maps every BMP letter to its diacritic variants: the characters whose canonical decomposition
/// (NFD) is that letter followed only by combining marks. For example <c>e</c> maps to
/// <c>é è ê ë ...</c> and Arabic <c>ا</c> maps to <c>أ إ آ</c>.
/// </summary>
internal sealed class DiacriticTable
{
    private const int BmpSize = 0x10000;
    private const char FirstSurrogate = '\uD800';
    private const char LastSurrogate = '\uDFFF';

    private static readonly Lazy<DiacriticTable> Shared = new Lazy<DiacriticTable>(Build);
    private static readonly char[] NoVariants = Array.Empty<char>();

    private readonly char[] _baseOf = new char[BmpSize];
    private readonly Dictionary<char, List<char>> _variants = new Dictionary<char, List<char>>();

    private DiacriticTable()
    {
    }

    /// <summary>Gets the process-wide table, built on first use.</summary>
    public static DiacriticTable Instance => Shared.Value;

    /// <summary>Returns the base letter of <paramref name="c"/>, or <paramref name="c"/> itself.</summary>
    public char BaseOf(char c) => _baseOf[c];

    /// <summary>Returns the precomposed variants of a base letter, excluding the letter itself.</summary>
    public IReadOnlyList<char> VariantsOf(char baseLetter) =>
        _variants.TryGetValue(baseLetter, out List<char>? variants) ? variants : (IReadOnlyList<char>)NoVariants;

    private static DiacriticTable Build()
    {
        DiacriticTable table = new DiacriticTable();
        for (int code = 0; code < BmpSize; code++)
        {
            char c = (char)code;
            table._baseOf[code] = c;
            if (c < FirstSurrogate || c > LastSurrogate)
            {
                table.Register(c);
            }
        }

        return table;
    }

    private void Register(char c)
    {
        string decomposed = Decompose(c);
        if (decomposed.Length < 2 || !char.IsLetter(decomposed[0]) || !AllMarks(decomposed))
        {
            return;
        }

        char baseLetter = decomposed[0];
        _baseOf[c] = baseLetter;
        if (!_variants.TryGetValue(baseLetter, out List<char>? variants))
        {
            variants = new List<char>();
            _variants[baseLetter] = variants;
        }

        variants.Add(c);
    }

    private static string Decompose(char c)
    {
        try
        {
            return c.ToString().Normalize(NormalizationForm.FormD);
        }
        catch (ArgumentException)
        {
            // Noncharacters the platform refuses to normalize have no canonical decomposition.
            return c.ToString();
        }
    }

    private static bool AllMarks(string decomposed)
    {
        for (int i = 1; i < decomposed.Length; i++)
        {
            if (!IsMark(decomposed[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns true for combining marks (categories Mn, Mc and Me).</summary>
    public static bool IsMark(char c)
    {
        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category == UnicodeCategory.NonSpacingMark ||
               category == UnicodeCategory.SpacingCombiningMark ||
               category == UnicodeCategory.EnclosingMark;
    }
}
