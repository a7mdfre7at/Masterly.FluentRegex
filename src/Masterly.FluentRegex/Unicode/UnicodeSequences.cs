#nullable enable

namespace Masterly.FluentRegex.Unicode;

/// <summary>
/// Regex text for constructs that span several UTF-16 code units.
/// </summary>
internal static class UnicodeSequences
{
    private const string SurrogatePair = @"[\uD800-\uDBFF][\uDC00-\uDFFF]";
    private const string NonSurrogate = @"[^\uD800-\uDFFF]";
    private const string LoneSurrogate = @"[\uD800-\uDFFF]";

    // Regional indicator symbols U+1F1E6..U+1F1FF; two of them form a flag.
    private const string RegionalIndicator = @"\uD83C[\uDDE6-\uDDFF]";

    // Combining marks (including variation selectors and the keycap U+20E3), ZWNJ,
    // emoji skin-tone modifiers U+1F3FB..U+1F3FF and tag characters U+E0020..U+E007F.
    private const string Extend = @"(?:[\p{M}\u200C]|\uD83C[\uDFFB-\uDFFF]|\uDB40[\uDC20-\uDC7F])";

    private const string ZeroWidthJoiner = @"\u200D";

    // Hangul syllable blocks: leading consonants, vowels, trailing consonants and precomposed syllables.
    private const string HangulLeading = @"[\u1100-\u115F\uA960-\uA97F]";
    private const string HangulVowel = @"[\u1160-\u11A7\uD7B0-\uD7C6]";
    private const string HangulTrailing = @"[\u11A8-\u11FF\uD7CB-\uD7FB]";
    private const string HangulSyllable = @"[\uAC00-\uD7A3]";

    private const string HangulBlock =
        HangulLeading + "*(?:" + HangulSyllable + "|" + HangulVowel + ")" + HangulVowel + "*" + HangulTrailing + "*";

    private const string Hangul = "(?:" + HangulBlock + "|" + HangulLeading + "+|" + HangulTrailing + "+)";

    private const string Base = "(?:" + Hangul + "|" + SurrogatePair + @"|[^\uD800-\uDFFF\r\n])";

    /// <summary>One Unicode scalar value: a surrogate pair or a single non-surrogate code unit.</summary>
    public const string CodePoint = SurrogatePair + "|" + NonSurrogate;

    /// <summary>
    /// An approximation of an extended grapheme cluster (UAX #29): CRLF, flag pairs, and a base
    /// followed by marks, modifiers and zero-width-joined continuations.
    /// </summary>
    public const string Grapheme =
        @"\r\n|[\r\n]" +
        "|" + RegionalIndicator + RegionalIndicator +
        "|" + Base + Extend + "*(?:" + ZeroWidthJoiner + Base + Extend + "*)*" + ZeroWidthJoiner + "?" +
        "|" + LoneSurrogate;
}
