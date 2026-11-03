#nullable enable

namespace Masterly.FluentRegex;

public static partial class CommonPatterns
{
    /// <summary>Same as <see cref="Base64()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Base64(DigitMode digits) => Base64().WithDigits(digits);

    /// <summary>Same as <see cref="CreditCard()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern CreditCard(DigitMode digits) => CreditCard().WithDigits(digits);

    /// <summary>Same as <see cref="Date()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Date(DigitMode digits) => Date().WithDigits(digits);

    /// <summary>Same as <see cref="DateIso()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern DateIso(DigitMode digits) => DateIso().WithDigits(digits);

    /// <summary>Same as <see cref="DateTimeIso()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern DateTimeIso(DigitMode digits) => DateTimeIso().WithDigits(digits);

    /// <summary>Same as <see cref="Decimal()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Decimal(DigitMode digits) => Decimal().WithDigits(digits);

    /// <summary>Same as <see cref="Email()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Email(DigitMode digits) => Email().WithDigits(digits);

    /// <summary>Same as <see cref="FilePath()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern FilePath(DigitMode digits) => FilePath().WithDigits(digits);

    /// <summary>Same as <see cref="Guid()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Guid(DigitMode digits) => Guid().WithDigits(digits);

    /// <summary>Same as <see cref="HexColor()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern HexColor(DigitMode digits) => HexColor().WithDigits(digits);

    /// <summary>Same as <see cref="HtmlTag()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern HtmlTag(DigitMode digits) => HtmlTag().WithDigits(digits);

    /// <summary>Same as <see cref="IPv4()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern IPv4(DigitMode digits) => IPv4().WithDigits(digits);

    /// <summary>Same as <see cref="IPv6()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern IPv6(DigitMode digits) => IPv6().WithDigits(digits);

    /// <summary>Same as <see cref="Iban()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Iban(DigitMode digits) => Iban().WithDigits(digits);

    /// <summary>Same as <see cref="Integer()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Integer(DigitMode digits) => Integer().WithDigits(digits);

    /// <summary>Same as <see cref="Jwt()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Jwt(DigitMode digits) => Jwt().WithDigits(digits);

    /// <summary>Same as <see cref="MacAddress()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern MacAddress(DigitMode digits) => MacAddress().WithDigits(digits);

    /// <summary>Same as <see cref="MarkdownLink()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern MarkdownLink(DigitMode digits) => MarkdownLink().WithDigits(digits);

    /// <summary>Same as <see cref="PhoneNumber()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern PhoneNumber(DigitMode digits) => PhoneNumber().WithDigits(digits);

    /// <summary>Same as <see cref="PhoneNumberE164()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern PhoneNumberE164(DigitMode digits) => PhoneNumberE164().WithDigits(digits);

    /// <summary>Same as <see cref="ScientificNotation()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern ScientificNotation(DigitMode digits) => ScientificNotation().WithDigits(digits);

    /// <summary>Same as <see cref="SemVer()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern SemVer(DigitMode digits) => SemVer().WithDigits(digits);

    /// <summary>Same as <see cref="Slug()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Slug(DigitMode digits) => Slug().WithDigits(digits);

    /// <summary>Same as <see cref="SocialSecurityNumber()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern SocialSecurityNumber(DigitMode digits) => SocialSecurityNumber().WithDigits(digits);

    /// <summary>Same as <see cref="StrongPassword()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern StrongPassword(DigitMode digits) => StrongPassword().WithDigits(digits);

    /// <summary>Same as <see cref="Time12()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Time12(DigitMode digits) => Time12().WithDigits(digits);

    /// <summary>Same as <see cref="Time24()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Time24(DigitMode digits) => Time24().WithDigits(digits);

    /// <summary>Same as <see cref="Url()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Url(DigitMode digits) => Url().WithDigits(digits);

    /// <summary>Same as <see cref="Username()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Username(DigitMode digits) => Username().WithDigits(digits);

    /// <summary>Same as <see cref="ZipCode()"/>, with the given meaning of a digit.</summary>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern ZipCode(DigitMode digits) => ZipCode().WithDigits(digits);

    /// <summary>Same as <see cref="Email(PatternStrictness)"/>, with the given meaning of a digit.</summary>
    /// <param name="strictness">How strictly the format is validated.</param>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Email(PatternStrictness strictness, DigitMode digits) => Email(strictness).WithDigits(digits);

    /// <summary>Same as <see cref="Url(PatternStrictness)"/>, with the given meaning of a digit.</summary>
    /// <param name="strictness">How strictly the format is validated.</param>
    /// <param name="digits">Which characters count as a digit. <see cref="DigitMode.Ascii"/> accepts only 0-9.</param>
    /// <returns>The pattern.</returns>
    public static Pattern Url(PatternStrictness strictness, DigitMode digits) => Url(strictness).WithDigits(digits);
}
