#nullable enable

namespace Masterly.FluentRegex;

/// <summary>
/// Writing systems supported by <see cref="Pattern.Script(UnicodeScript)"/>,
/// <see cref="Pattern.LetterIn(UnicodeScript)"/> and <see cref="Pattern.DigitIn(UnicodeScript)"/>.
/// </summary>
/// <remarks>
/// Each script maps to the Basic Multilingual Plane (BMP) Unicode blocks the script is encoded in.
/// Supplementary planes (code points above U+FFFF, such as CJK Extension B) are not included.
/// </remarks>
public enum UnicodeScript
{
    /// <summary>Latin letters (no ASCII digits or punctuation), Latin extensions and combining diacritics.</summary>
    Latin,

    /// <summary>Greek (without the Coptic letters of the Greek and Coptic block) and Greek Extended.</summary>
    Greek,

    /// <summary>Cyrillic and its supplement and extension blocks.</summary>
    Cyrillic,

    /// <summary>Armenian, including the Armenian ligatures.</summary>
    Armenian,

    /// <summary>Hebrew, including the Hebrew presentation forms.</summary>
    Hebrew,

    /// <summary>Arabic, its supplement and extension blocks and the Arabic presentation forms.</summary>
    Arabic,

    /// <summary>Syriac and the Syriac Supplement.</summary>
    Syriac,

    /// <summary>Thaana, the script of Dhivehi.</summary>
    Thaana,

    /// <summary>Devanagari and Devanagari Extended.</summary>
    Devanagari,

    /// <summary>Bengali (Bangla).</summary>
    Bengali,

    /// <summary>Gurmukhi.</summary>
    Gurmukhi,

    /// <summary>Gujarati.</summary>
    Gujarati,

    /// <summary>Tamil.</summary>
    Tamil,

    /// <summary>Telugu.</summary>
    Telugu,

    /// <summary>Kannada.</summary>
    Kannada,

    /// <summary>Malayalam.</summary>
    Malayalam,

    /// <summary>Thai.</summary>
    Thai,

    /// <summary>Lao.</summary>
    Lao,

    /// <summary>Tibetan.</summary>
    Tibetan,

    /// <summary>Georgian, Georgian Supplement and Georgian Extended.</summary>
    Georgian,

    /// <summary>Hangul syllables, Jamo and compatibility Jamo.</summary>
    Hangul,

    /// <summary>Ethiopic (Ge'ez) and its supplement and extension blocks.</summary>
    Ethiopic,

    /// <summary>Khmer and Khmer Symbols.</summary>
    Khmer,

    /// <summary>Mongolian.</summary>
    Mongolian,

    /// <summary>Hiragana.</summary>
    Hiragana,

    /// <summary>Katakana, Katakana Phonetic Extensions and halfwidth Katakana.</summary>
    Katakana,

    /// <summary>Han ideographs (CJK Unified Ideographs, Extension A, compatibility ideographs and radicals).</summary>
    Han
}
