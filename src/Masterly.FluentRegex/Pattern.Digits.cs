#nullable enable

using System;
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex;

public partial class Pattern
{
    /// <summary>
    /// Returns a copy of this pattern in which <c>\d</c> matches only the ASCII digits 0-9 and
    /// <c>\D</c> matches everything else. Every other construct, the options and the timeout are unchanged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// .NET's <c>\d</c> matches every Unicode decimal digit, so <c>\d{4}</c> also accepts "٢٠٢٦".
    /// Use this method when a format is defined over ASCII digits, or pass <see cref="DigitMode.Ascii"/>
    /// to a <see cref="CommonPatterns"/> method.
    /// </para>
    /// <para>
    /// Shorthands are rewritten everywhere they appear, including inside sets, negated sets,
    /// class subtractions and lookarounds. Explicit categories such as <c>\p{Nd}</c> and the word
    /// class <c>\w</c>, which also contains Unicode digits, are left as written. Patterns using
    /// <see cref="System.Text.RegularExpressions.RegexOptions.ECMAScript"/> already have ASCII
    /// digits and are returned unchanged.
    /// </para>
    /// </remarks>
    /// <returns>A pattern with ASCII-only digits, or this pattern when it has no digit shorthands.</returns>
    /// <example>
    /// Pattern.FromRegex(@"^\d{4}$").IsMatch("٢٠٢٦");                    // true
    /// Pattern.FromRegex(@"^\d{4}$").WithAsciiDigits().IsMatch("٢٠٢٦");  // false
    /// Pattern.FromRegex(@"^\d{4}$").WithAsciiDigits().ToString();        // ^[0-9]{4}$
    /// </example>
    public Pattern WithAsciiDigits()
    {
        if (IsEmpty)
        {
            return this;
        }

        string original = ToString();
        string rewritten = AsciiDigitRewriter.Rewrite(ParseTree());
        if (string.Equals(original, rewritten, StringComparison.Ordinal))
        {
            return this;
        }

        return With.RegEx(rewritten).WithOptions(_options).WithTimeout(_timeout);
    }

    /// <summary>
    /// Returns this pattern unchanged for <see cref="DigitMode.Unicode"/>, or <see cref="WithAsciiDigits"/>
    /// for <see cref="DigitMode.Ascii"/>.
    /// </summary>
    /// <param name="digits">Which characters count as a digit.</param>
    /// <returns>The pattern with the requested digit meaning.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="digits"/> is not a defined value.</exception>
    public Pattern WithDigits(DigitMode digits)
    {
        switch (digits)
        {
            case DigitMode.Unicode:
                return this;
            case DigitMode.Ascii:
                return WithAsciiDigits();
            default:
                throw new ArgumentOutOfRangeException(nameof(digits), digits, "Unknown digit mode.");
        }
    }
}
