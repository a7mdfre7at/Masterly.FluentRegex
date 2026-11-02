#nullable enable

using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Unicode;

namespace Masterly.FluentRegex;

/// <summary>
/// Unicode-aware building blocks: scripts, script digits, code points, grapheme clusters and
/// diacritic-insensitive matching.
/// </summary>
public partial class Pattern
{
    /// <summary>
    /// Matches any character of the given script, for example <c>Script(UnicodeScript.Arabic)</c>.
    /// Works inside and outside a <see cref="Set"/>.
    /// </summary>
    /// <param name="script">The writing system.</param>
    /// <returns>A new pattern.</returns>
    /// <remarks>
    /// The class is built from the Basic Multilingual Plane blocks of the script (see
    /// <see cref="UnicodeScript"/>), so combining marks and the few punctuation characters that live in
    /// those blocks (such as Arabic harakat and the Arabic comma) are included. Code points outside the
    /// BMP (above U+FFFF, for example CJK Extension B) are not included.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="script"/> is not a defined value.</exception>
    public Pattern Script(UnicodeScript script) => Append(ScriptClasses.Script(script));

    /// <summary>
    /// Matches a letter (Unicode category L) of the given script. Combining marks, digits and
    /// punctuation of the script are excluded. Works inside and outside a <see cref="Set"/>.
    /// </summary>
    /// <param name="script">The writing system.</param>
    /// <returns>A new pattern.</returns>
    /// <remarks>
    /// Standalone it renders a class subtraction such as <c>[\u0600-\u06FF-[\P{L}]]</c>; inside a set,
    /// where subtraction is not allowed, it renders the equivalent letter ranges. BMP only.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="script"/> is not a defined value.</exception>
    public Pattern LetterIn(UnicodeScript script) => Append(ScriptClasses.Letters(script));

    /// <summary>
    /// Matches a decimal digit of the given script, for example Arabic-Indic <c>٠-٩</c> and
    /// Extended Arabic-Indic (Persian) <c>۰-۹</c> for <see cref="UnicodeScript.Arabic"/>.
    /// <see cref="UnicodeScript.Latin"/> gives the ASCII digits <c>0-9</c>.
    /// </summary>
    /// <param name="script">The writing system.</param>
    /// <returns>A new pattern.</returns>
    /// <exception cref="ArgumentException">
    /// The script has no decimal digits of its own, such as Greek or Han.
    /// </exception>
    public Pattern DigitIn(UnicodeScript script) => Append(ScriptClasses.Digits(script));

    /// <summary>
    /// Matches an ASCII digit: <c>[0-9]</c>. Renders <c>0-9</c> inside a <see cref="Set"/>.
    /// </summary>
    /// <remarks>
    /// The .NET class <c>\d</c> (see <see cref="Digit"/>) matches every Unicode decimal digit, such as
    /// Arabic-Indic <c>٣</c> or Devanagari <c>३</c>. Use <see cref="AsciiDigit"/> when only 0-9 are valid.
    /// </remarks>
    public Pattern AsciiDigit => Append(ClassToken.Bracketed("0-9"));

    /// <summary>
    /// Matches one Unicode scalar value: a surrogate pair (code points above U+FFFF, such as most emoji)
    /// or a single non-surrogate character. Lone surrogates are not matched.
    /// </summary>
    /// <remarks>
    /// Cannot be used inside a <see cref="Set"/>; rendering it there throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    public Pattern CodePoint => Append(new SequenceToken(UnicodeSequences.CodePoint, nameof(CodePoint)));

    /// <summary>
    /// Matches one user-perceived character: an approximation of a Unicode extended grapheme cluster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Covers CRLF, a base character followed by combining marks (<c>\p{M}</c>, which includes
    /// variation selectors and the keycap mark), emoji skin-tone modifiers, tag sequences,
    /// zero-width-joiner emoji sequences, regional-indicator flag pairs and Hangul syllable sequences.
    /// </para>
    /// <para>
    /// This is an approximation of UAX #29, not a full implementation: it does not apply the
    /// Extended_Pictographic rule to zero-width joiners, the Prepend rule, Indic conjunct rules, or
    /// marks outside the BMP. For exact segmentation use <c>System.Globalization.StringInfo</c>.
    /// Cannot be used inside a <see cref="Set"/>; rendering it there throws <see cref="InvalidOperationException"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// Pattern.With.Grapheme.Count("👨‍👩‍👧"); // 1
    /// </example>
    public Pattern Grapheme => Append(new SequenceToken(UnicodeSequences.Grapheme, nameof(Grapheme)));

    /// <summary>
    /// Returns a new pattern that matches text with or without diacritics, in both precomposed (NFC)
    /// and decomposed (NFD) form. Options and timeout are kept.
    /// </summary>
    /// <returns>A new pattern. The original pattern is unchanged.</returns>
    /// <remarks>
    /// <para>
    /// Every literal letter becomes a class of its base letter plus every BMP character whose canonical
    /// decomposition is that letter followed only by combining marks, then a run of non-spacing marks
    /// and Arabic tatweel: <c>e</c> also matches <c>é è ê ë</c>, and Arabic <c>ا</c> also matches
    /// <c>أ إ آ</c>. Literal non-spacing marks in the pattern are dropped (the mark run covers them),
    /// other literal marks become optional, and character classes that can match a letter gain the
    /// same mark run. With <see cref="RegexOptions.IgnoreCase"/> the variants of both cases are added.
    /// </para>
    /// <para>
    /// Letters that differ by more than a diacritic, such as <c>ø</c>, <c>ł</c>, <c>ß</c> or Arabic
    /// <c>ة</c> and <c>ه</c>, are not folded. Backreferences still compare the captured text exactly.
    /// </para>
    /// </remarks>
    /// <example>
    /// Pattern.With.Literal("cafe").IgnoreDiacritics().IsMatch("un café noir"); // true
    /// Pattern.With.Literal("محمد").IgnoreDiacritics().IsMatch("مُحَمَّد");          // true
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// The pattern uses <see cref="RegexOptions.ECMAScript"/>.
    /// </exception>
    public Pattern IgnoreDiacritics()
    {
        if (IsEmpty)
        {
            return this;
        }

        string rewritten = DiacriticRewriter.Rewrite(ParseTree());
        return With.RegEx(rewritten).WithOptions(_options).WithTimeout(_timeout);
    }
}
