#nullable enable
using System;
using System.Globalization;

namespace Masterly.FluentRegex;

/// <summary>
/// Ready-made semantic rules for <see cref="Pattern.Must(Func{string, bool}, string?)"/>.
/// Every validator returns <c>false</c> for null instead of throwing. A digit may be any Unicode decimal digit,
/// such as the Arabic-Indic '٣', because that is what <c>\d</c> matches by default.
/// </summary>
public static class Validators
{
    private const int LuhnMinimumDigits = 2;
    private const int LuhnModulus = 10;
    private const int LuhnDoubledDigitLimit = 9;
    private const int IbanMinimumLength = 5;
    private const int IbanMaximumLength = 34;
    private const int IbanPrefixLength = 4;
    private const int IbanModulus = 97;
    private const int IbanExpectedRemainder = 1;
    private const int LetterValueOffset = 10;
    private const string IsoDateFormat = "yyyy-MM-dd";
    private const int IPv4OctetCount = 4;
    private const int IPv4MaximumOctetDigits = 3;
    private const int IPv4MaximumOctet = 255;
    private const int DecimalBase = 10;

    /// <summary>
    /// Validates a Luhn (mod 10) checksum, as used by credit card numbers and IMEIs.
    /// Spaces and dashes are ignored; any other non-digit makes the value invalid.
    /// </summary>
    /// <param name="value">The number to check, for example <c>4111 1111 1111 1111</c>.</param>
    /// <returns><c>true</c> when the value has at least two digits and a valid check digit.</returns>
    public static bool Luhn(string value)
    {
        if (value == null)
        {
            return false;
        }

        value = WithAsciiDigits(value);
        int sum = 0;
        int digits = 0;
        for (int i = value.Length - 1; i >= 0; i--)
        {
            char c = value[i];
            if (c == ' ' || c == '-')
            {
                continue;
            }

            if (!IsAsciiDigit(c))
            {
                return false;
            }

            sum += LuhnDigit(c - '0', doubled: digits % 2 == 1);
            digits++;
        }

        return digits >= LuhnMinimumDigits && sum % LuhnModulus == 0;
    }

    /// <summary>
    /// Validates the ISO 13616 mod-97 check digits of an IBAN. Spaces are ignored and letters are
    /// accepted in either case.
    /// </summary>
    /// <param name="value">The IBAN, for example <c>GB82 WEST 1234 5698 7654 32</c>.</param>
    /// <returns><c>true</c> when the structure is plausible and the remainder mod 97 is 1.</returns>
    public static bool IbanChecksum(string value)
    {
        if (value == null)
        {
            return false;
        }

        string compact = WithAsciiDigits(value).Replace(" ", string.Empty).ToUpperInvariant();
        if (compact.Length < IbanMinimumLength || compact.Length > IbanMaximumLength || !HasIbanPrefix(compact))
        {
            return false;
        }

        string rearranged = compact.Substring(IbanPrefixLength) + compact.Substring(0, IbanPrefixLength);
        return TryMod97(rearranged, out int remainder) && remainder == IbanExpectedRemainder;
    }

    /// <summary>
    /// Validates that the value is exactly <c>yyyy-MM-dd</c> and names a real calendar date,
    /// so <c>2024-02-29</c> is valid and <c>2026-02-30</c> is not.
    /// </summary>
    /// <param name="value">The date text.</param>
    /// <returns><c>true</c> for a real date in ISO 8601 calendar format.</returns>
    public static bool IsoDate(string value)
    {
        if (value == null || value.Length != IsoDateFormat.Length)
        {
            return false;
        }

        return DateTime.TryParseExact(WithAsciiDigits(value), IsoDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    /// <summary>
    /// Validates a dotted-quad IPv4 address whose four octets are each 0-255 (one to three digits;
    /// leading zeros are accepted, as by <see cref="CommonPatterns.IPv4()"/>).
    /// </summary>
    /// <param name="value">The address, for example <c>192.168.0.1</c>.</param>
    /// <returns><c>true</c> when every octet is in range.</returns>
    public static bool IPv4(string value)
    {
        if (value == null)
        {
            return false;
        }

        string[] octets = WithAsciiDigits(value).Split('.');
        if (octets.Length != IPv4OctetCount)
        {
            return false;
        }

        foreach (string octet in octets)
        {
            if (!IsOctet(octet))
            {
                return false;
            }
        }

        return true;
    }

    private static int LuhnDigit(int digit, bool doubled)
    {
        if (!doubled)
        {
            return digit;
        }

        int product = digit * 2;
        return product > LuhnDoubledDigitLimit ? product - LuhnDoubledDigitLimit : product;
    }

    private static bool HasIbanPrefix(string compact) =>
        IsAsciiUpper(compact[0]) && IsAsciiUpper(compact[1]) && IsAsciiDigit(compact[2]) && IsAsciiDigit(compact[3]);

    private static bool TryMod97(string text, out int remainder)
    {
        remainder = 0;
        foreach (char c in text)
        {
            if (IsAsciiDigit(c))
            {
                remainder = (remainder * DecimalBase + (c - '0')) % IbanModulus;
            }
            else if (IsAsciiUpper(c))
            {
                int letterValue = c - 'A' + LetterValueOffset;
                remainder = (remainder * DecimalBase * DecimalBase + letterValue) % IbanModulus;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOctet(string octet)
    {
        if (octet.Length == 0 || octet.Length > IPv4MaximumOctetDigits)
        {
            return false;
        }

        int value = 0;
        foreach (char c in octet)
        {
            if (!IsAsciiDigit(c))
            {
                return false;
            }

            value = value * DecimalBase + (c - '0');
        }

        return value <= IPv4MaximumOctet;
    }

    private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

    /// <summary>Replaces every Unicode decimal digit with the ASCII digit of the same value.</summary>
    private static string WithAsciiDigits(string value)
    {
        char[]? converted = null;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (IsAsciiDigit(c) || !char.IsDigit(c))
            {
                continue;
            }

            converted ??= value.ToCharArray();
            converted[i] = (char)('0' + CharUnicodeInfo.GetDecimalDigitValue(c));
        }

        return converted == null ? value : new string(converted);
    }

    private static bool IsAsciiUpper(char c) => c >= 'A' && c <= 'Z';
}
