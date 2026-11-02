#nullable enable

using System;
using System.Collections.Generic;

namespace Masterly.FluentRegex.Learning;

/// <summary>
/// The character classes the learner can generalize to, ordered from the most specific to the most general.
/// </summary>
internal enum LearnedClass
{
    Digit,
    Upper,
    Lower,
    Letter,
    Word,
    Space
}

/// <summary>
/// Classifies characters and picks the narrowest class that covers a set of characters.
/// </summary>
internal static class LearnedClasses
{
    /// <summary>
    /// The candidate classes for word characters, from the most specific to the most general.
    /// </summary>
    private static readonly LearnedClass[] WordLadder =
    {
        LearnedClass.Digit,
        LearnedClass.Upper,
        LearnedClass.Lower,
        LearnedClass.Letter,
        LearnedClass.Word
    };

    /// <summary>Returns true for an ASCII digit.</summary>
    public static bool IsDigit(char c) => c >= '0' && c <= '9';

    /// <summary>Returns true for an ASCII uppercase letter.</summary>
    public static bool IsUpper(char c) => c >= 'A' && c <= 'Z';

    /// <summary>Returns true for an ASCII lowercase letter.</summary>
    public static bool IsLower(char c) => c >= 'a' && c <= 'z';

    /// <summary>Returns true for an ASCII letter.</summary>
    public static bool IsLetter(char c) => IsUpper(c) || IsLower(c);

    /// <summary>Returns true for an ASCII word character: a letter, a digit or an underscore.</summary>
    public static bool IsWordChar(char c) => IsLetter(c) || IsDigit(c) || c == '_';

    /// <summary>Returns true for a character that <c>\s</c> matches.</summary>
    public static bool IsSpace(char c) => char.IsWhiteSpace(c);

    /// <summary>
    /// Finds the narrowest class that contains every character of every text.
    /// </summary>
    /// <param name="texts">The texts to cover.</param>
    /// <param name="covering">The narrowest covering class.</param>
    /// <returns>False when no single class covers all characters.</returns>
    public static bool TryCover(IEnumerable<string> texts, out LearnedClass covering)
    {
        if (AllChars(texts, IsSpace))
        {
            covering = LearnedClass.Space;
            return true;
        }

        foreach (LearnedClass candidate in WordLadder)
        {
            if (AllChars(texts, c => Contains(candidate, c)))
            {
                covering = candidate;
                return true;
            }
        }

        covering = LearnedClass.Word;
        return false;
    }

    /// <summary>Returns true when <paramref name="learnedClass"/> matches <paramref name="c"/>.</summary>
    public static bool Contains(LearnedClass learnedClass, char c)
    {
        switch (learnedClass)
        {
            case LearnedClass.Digit:
                return IsDigit(c);
            case LearnedClass.Upper:
                return IsUpper(c);
            case LearnedClass.Lower:
                return IsLower(c);
            case LearnedClass.Letter:
                return IsLetter(c);
            case LearnedClass.Word:
                return IsWordChar(c);
            default:
                return IsSpace(c);
        }
    }

    /// <summary>Appends the fluent element for <paramref name="learnedClass"/>.</summary>
    public static Pattern AppendTo(Pattern pattern, LearnedClass learnedClass)
    {
        switch (learnedClass)
        {
            case LearnedClass.Digit:
                return pattern.Digit;
            case LearnedClass.Upper:
                return pattern.UppercaseLetter;
            case LearnedClass.Lower:
                return pattern.LowercaseLetter;
            case LearnedClass.Letter:
                return pattern.Letter;
            case LearnedClass.Word:
                return pattern.Word;
            default:
                return pattern.Whitespace;
        }
    }

    private static bool AllChars(IEnumerable<string> texts, Func<char, bool> predicate)
    {
        foreach (string text in texts)
        {
            foreach (char c in text)
            {
                if (!predicate(c))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
