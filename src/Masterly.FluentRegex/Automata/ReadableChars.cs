#nullable enable

using System;
using System.Collections.Generic;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Automata;

/// <summary>Chooses characters that make witness strings easy to read.</summary>
internal static class ReadableChars
{
    private const int LowercaseRank = 0;
    private const int UppercaseRank = 1;
    private const int DigitRank = 2;
    private const int PrintableAsciiRank = 3;
    private const int OtherRank = 4;
    private const int SurrogateRank = 5;

    private const char FirstPrintable = ' ';
    private const char LastPrintable = '~';
    private const char FirstSurrogate = '\uD800';
    private const char LastSurrogate = '\uDFFF';

    private static readonly string[] PreferredRanges = { "az", "AZ", "09" };

    /// <summary>Returns the most readable member of a non-empty set.</summary>
    public static char Pick(CharSet set)
    {
        foreach (string range in PreferredRanges)
        {
            for (char c = range[0]; c <= range[1]; c++)
            {
                if (set.Contains(c))
                {
                    return c;
                }
            }
        }
        return FirstNonSurrogate(set);
    }

    private static char FirstNonSurrogate(CharSet set)
    {
        for (char c = FirstPrintable; c <= LastPrintable; c++)
        {
            if (set.Contains(c))
            {
                return c;
            }
        }

        char? fallback = null;
        foreach (KeyValuePair<char, char> range in set.Ranges())
        {
            fallback ??= range.Key;
            if (range.Key < FirstSurrogate || range.Key > LastSurrogate)
            {
                return range.Key;
            }
            if (range.Value > LastSurrogate)
            {
                return (char)(LastSurrogate + 1);
            }
        }
        return fallback ?? throw new InvalidOperationException("The character set is empty.");
    }

    /// <summary>Ranks a character by readability: lower is better.</summary>
    public static int Rank(char c)
    {
        if (c >= 'a' && c <= 'z')
        {
            return LowercaseRank;
        }
        if (c >= 'A' && c <= 'Z')
        {
            return UppercaseRank;
        }
        if (c >= '0' && c <= '9')
        {
            return DigitRank;
        }
        if (c >= FirstPrintable && c <= LastPrintable)
        {
            return PrintableAsciiRank;
        }
        return c >= FirstSurrogate && c <= LastSurrogate ? SurrogateRank : OtherRank;
    }
}
