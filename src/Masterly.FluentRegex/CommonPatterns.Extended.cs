using System;

namespace Masterly.FluentRegex
{
    /// <summary>How strictly a common pattern validates its input.</summary>
    public enum PatternStrictness
    {
        /// <summary>Accepts anything with the right overall shape. Good for quick filtering.</summary>
        Loose,

        /// <summary>The balanced default used by the parameterless overloads.</summary>
        Standard,

        /// <summary>Follows the relevant standard closely. Good for validating stored data.</summary>
        Strict
    }

    public static partial class CommonPatterns
    {
        // Character sets shared by several patterns.
        private static Pattern AlphaNumeric => Pattern.With.Range('a', 'z').Range('A', 'Z').Range('0', '9');
        private static Pattern Base64UrlChars => Pattern.With.Range('A', 'Z').Range('a', 'z').Range('0', '9').Literal("_-");

        /// <summary>
        /// Matches an email address with the chosen strictness.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><see cref="PatternStrictness.Loose"/>: something@something.something, no spaces.</item>
        /// <item><see cref="PatternStrictness.Standard"/>: same as <see cref="Email()"/>.</item>
        /// <item><see cref="PatternStrictness.Strict"/>: RFC 5322 dot-atom local part (at most 64 characters),
        /// RFC 1035 domain labels (at most 63 characters, no leading or trailing hyphen),
        /// a letter-only top-level domain, and at most 254 characters overall.</item>
        /// </list>
        /// </remarks>
        public static Pattern Email(PatternStrictness strictness)
        {
            switch (strictness)
            {
                case PatternStrictness.Loose:
                    return Pattern.With
                        .StartOfLine
                        .NoneOf(" \t\r\n@").Repeat.OneOrMore
                        .Literal("@")
                        .NoneOf(" \t\r\n@.").Repeat.OneOrMore
                        .NonCapturingGroup(Pattern.With.Literal(".").NoneOf(" \t\r\n@.").Repeat.OneOrMore).Repeat.OneOrMore
                        .EndOfLine;

                case PatternStrictness.Strict:
                {
                    Pattern atom = Pattern.With.Set(AlphaNumeric.Literal("!#$%&'*+/=?^_`{|}~-")).Repeat.OneOrMore;
                    Pattern label = Pattern.With
                        .Set(AlphaNumeric)
                        .NonCapturingGroup(Pattern.With.Set(AlphaNumeric.Literal("-")).Repeat.Times(0, 61).Set(AlphaNumeric)).Repeat.Optional;

                    return Pattern.With
                        .StartOfLine
                        .PositiveLookahead(Pattern.With.Anything.Repeat.Times(1, 254).EndOfLine)
                        .PositiveLookahead(Pattern.With.NoneOf("@").Repeat.Times(1, 64).Literal("@"))
                        .NamedGroup("local", Pattern.With.Then(atom).NonCapturingGroup(Pattern.With.Literal(".").Then(atom)).Repeat.ZeroOrMore)
                        .Literal("@")
                        .NamedGroup("domain", Pattern.With
                            .NonCapturingGroup(Pattern.With.Then(label).Literal(".")).Repeat.OneOrMore
                            .Letter.Repeat.Times(2, 63))
                        .EndOfLine;
                }

                default:
                    return Email();
            }
        }

        /// <summary>
        /// Matches a URL with the chosen strictness.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><see cref="PatternStrictness.Loose"/>: any scheme followed by "://" and non-space characters.</item>
        /// <item><see cref="PatternStrictness.Standard"/>: same as <see cref="Url()"/>.</item>
        /// <item><see cref="PatternStrictness.Strict"/>: http or https; a valid host name, IPv4 address or
        /// localhost; an optional port from 1 to 65535; and RFC 3986 path, query and fragment characters.
        /// Named groups: scheme, host, port, path, query, fragment.</item>
        /// </list>
        /// </remarks>
        public static Pattern Url(PatternStrictness strictness)
        {
            switch (strictness)
            {
                case PatternStrictness.Loose:
                    return Pattern.With
                        .StartOfLine
                        .Letter.Set(AlphaNumeric.Literal("+.-")).Repeat.ZeroOrMore
                        .Literal("://")
                        .NonWhitespace.Repeat.OneOrMore
                        .EndOfLine;

                case PatternStrictness.Strict:
                {
                    Pattern label = Pattern.With
                        .Set(AlphaNumeric)
                        .NonCapturingGroup(Pattern.With.Set(AlphaNumeric.Literal("-")).Repeat.Times(0, 61).Set(AlphaNumeric)).Repeat.Optional;
                    Pattern hostName = Pattern.With
                        .NonCapturingGroup(Pattern.With.Then(label).Literal(".")).Repeat.OneOrMore
                        .Letter.Repeat.Times(2, 63);
                    Pattern ipv4 = Ipv4Body();
                    Pattern port = Pattern.With.OneOf(
                        Pattern.With.Literal("6553").Range('0', '5'),
                        Pattern.With.Literal("655").Range('0', '2').Digit,
                        Pattern.With.Literal("65").Range('0', '4').Digit.Repeat.Times(2),
                        Pattern.With.Literal("6").Range('0', '4').Digit.Repeat.Times(3),
                        Pattern.With.Range('1', '5').Digit.Repeat.Times(4),
                        Pattern.With.Range('1', '9').Digit.Repeat.Times(0, 3));
                    Pattern pathChars = Pattern.With.Set(Pattern.With.Word.Literal("-.~%!$&'()*+,;=:@"));
                    Pattern queryChars = Pattern.With.Set(Pattern.With.Word.Literal("-.~%!$&'()*+,;=:@/?"));

                    return Pattern.With
                        .StartOfLine
                        .NamedGroup("scheme", Pattern.With.Literal("http").Literal("s").Repeat.Optional)
                        .Literal("://")
                        .NamedGroup("host", Pattern.With.OneOf(Pattern.With.Literal("localhost"), ipv4, hostName))
                        .NonCapturingGroup(Pattern.With.Literal(":").NamedGroup("port", port)).Repeat.Optional
                        .NamedGroup("path", Pattern.With.NonCapturingGroup(Pattern.With.Literal("/").Then(pathChars).Repeat.ZeroOrMore).Repeat.ZeroOrMore)
                        .NonCapturingGroup(Pattern.With.Literal("?").NamedGroup("query", Pattern.With.Then(queryChars).Repeat.ZeroOrMore)).Repeat.Optional
                        .NonCapturingGroup(Pattern.With.Literal("#").NamedGroup("fragment", Pattern.With.Then(queryChars).Repeat.ZeroOrMore)).Repeat.Optional
                        .EndOfLine;
                }

                default:
                    return Url();
            }
        }

        /// <summary>
        /// Matches an international phone number in E.164 format: a plus sign, a non-zero digit,
        /// then at most 14 more digits.
        /// </summary>
        /// <example>
        /// CommonPatterns.PhoneNumberE164().IsMatch("+14155552671"); // true
        /// CommonPatterns.PhoneNumberE164().IsMatch("0044 20 7946 0958"); // false
        /// </example>
        public static Pattern PhoneNumberE164() => Pattern.With
            .StartOfLine
            .Literal("+")
            .NamedGroup("number", Pattern.With.Range('1', '9').Digit.Repeat.Times(1, 14))
            .EndOfLine;

        /// <summary>
        /// Matches the shape of an IBAN: a two-letter country code, two check digits and
        /// 11 to 30 alphanumeric characters, either compact or in groups of four separated by spaces.
        /// </summary>
        /// <remarks>
        /// Named groups: country, check. The mod-97 checksum cannot be expressed as a regex;
        /// validate it separately when correctness matters.
        /// </remarks>
        public static Pattern Iban()
        {
            Pattern alnum = Pattern.With.Set(Pattern.With.UppercaseLetter.Digit);
            return Pattern.With
                .StartOfLine
                .NamedGroup("country", Pattern.With.UppercaseLetter.Repeat.Times(2))
                .NamedGroup("check", Pattern.With.Digit.Repeat.Times(2))
                .OneOf(
                    Pattern.With.Then(alnum).Repeat.Times(11, 30),
                    Pattern.With
                        .NonCapturingGroup(Pattern.With.Literal(" ").Then(alnum).Repeat.Times(4)).Repeat.Times(2, 7)
                        .NonCapturingGroup(Pattern.With.Literal(" ").Then(alnum).Repeat.Times(1, 3)).Repeat.Optional)
                .EndOfLine;
        }

        /// <summary>
        /// Matches a Semantic Versioning 2.0.0 version, using the official grammar from semver.org.
        /// </summary>
        /// <remarks>
        /// Named groups: major, minor, patch, prerelease, buildmetadata. Combine with
        /// <see cref="Pattern.Extract{T}(string, IFormatProvider)"/> to parse versions into records.
        /// </remarks>
        /// <example>
        /// CommonPatterns.SemVer().IsMatch("1.0.0-alpha.1+build.5"); // true
        /// CommonPatterns.SemVer().IsMatch("1.02.0"); // false (leading zero)
        /// </example>
        public static Pattern SemVer()
        {
            Pattern number = Pattern.With.OneOf(Pattern.With.Literal("0"), Pattern.With.Range('1', '9').Digit.Repeat.ZeroOrMore);
            Pattern identifierChars = Pattern.With.Set(Pattern.With.Range('0', '9').Range('a', 'z').Range('A', 'Z').Literal("-"));
            Pattern prereleaseIdentifier = Pattern.With.OneOf(
                Pattern.With.Literal("0"),
                Pattern.With.Range('1', '9').Digit.Repeat.ZeroOrMore,
                Pattern.With.Digit.Repeat.ZeroOrMore.Set(Pattern.With.Letter.Literal("-")).Then(identifierChars).Repeat.ZeroOrMore);

            return Pattern.With
                .StartOfLine
                .NamedGroup("major", number).Literal(".")
                .NamedGroup("minor", number).Literal(".")
                .NamedGroup("patch", number)
                .NonCapturingGroup(Pattern.With.Literal("-").NamedGroup("prerelease",
                    Pattern.With.Then(prereleaseIdentifier).NonCapturingGroup(Pattern.With.Literal(".").Then(prereleaseIdentifier)).Repeat.ZeroOrMore)).Repeat.Optional
                .NonCapturingGroup(Pattern.With.Literal("+").NamedGroup("buildmetadata",
                    Pattern.With.Then(identifierChars).Repeat.OneOrMore.NonCapturingGroup(Pattern.With.Literal(".").Then(identifierChars).Repeat.OneOrMore).Repeat.ZeroOrMore)).Repeat.Optional
                .EndOfLine;
        }

        /// <summary>
        /// Matches a JSON Web Token in compact serialization: three base64url segments separated by dots.
        /// The header and payload must start with "eyJ", the encoding of a JSON object's opening brace.
        /// </summary>
        /// <remarks>Named groups: header, payload, signature. The signature is not verified.</remarks>
        public static Pattern Jwt() => Pattern.With
            .StartOfLine
            .NamedGroup("header", Pattern.With.Literal("eyJ").Set(Base64UrlChars).Repeat.ZeroOrMore)
            .Literal(".")
            .NamedGroup("payload", Pattern.With.Literal("eyJ").Set(Base64UrlChars).Repeat.ZeroOrMore)
            .Literal(".")
            .NamedGroup("signature", Pattern.With.Set(Base64UrlChars).Repeat.ZeroOrMore)
            .EndOfLine;

        /// <summary>
        /// Matches a MAC address as six pairs of hex digits separated consistently by ':' or '-'.
        /// </summary>
        /// <example>
        /// CommonPatterns.MacAddress().IsMatch("00:1A:2B:3C:4D:5E"); // true
        /// CommonPatterns.MacAddress().IsMatch("00:1A-2B:3C:4D:5E"); // false (mixed separators)
        /// </example>
        public static Pattern MacAddress() => Pattern.With
            .StartOfLine
            .HexDigit.Repeat.Times(2)
            .NamedGroup("sep", Pattern.With.AnyOf(":-"))
            .NonCapturingGroup(Pattern.With.HexDigit.Repeat.Times(2).Backreference("sep")).Repeat.Times(4)
            .HexDigit.Repeat.Times(2)
            .EndOfLine;

        /// <summary>
        /// Matches non-empty standard Base64 (RFC 4648) with correct padding.
        /// </summary>
        public static Pattern Base64()
        {
            Pattern chars = Pattern.With.Set(Pattern.With.Range('A', 'Z').Range('a', 'z').Range('0', '9').Literal("+/"));
            return Pattern.With
                .StartOfLine
                .PositiveLookahead(Pattern.With.Anything)
                .NonCapturingGroup(Pattern.With.Then(chars).Repeat.Times(4)).Repeat.ZeroOrMore
                .OneOf(
                    Pattern.With.Then(chars).Repeat.Times(2).Literal("=="),
                    Pattern.With.Then(chars).Repeat.Times(3).Literal("=")).Repeat.Optional
                .EndOfLine;
        }

        /// <summary>
        /// Matches a URL slug: lowercase letters and digits in words separated by single hyphens.
        /// </summary>
        /// <example>
        /// CommonPatterns.Slug().IsMatch("hello-world-2024"); // true
        /// CommonPatterns.Slug().IsMatch("Hello--World");    // false
        /// </example>
        public static Pattern Slug() => Pattern.With
            .StartOfLine
            .Set(Pattern.With.LowercaseLetter.Digit).Repeat.OneOrMore
            .NonCapturingGroup(Pattern.With.Literal("-").Set(Pattern.With.LowercaseLetter.Digit).Repeat.OneOrMore).Repeat.ZeroOrMore
            .EndOfLine;

        /// <summary>
        /// Matches an ISO 8601 date and time, such as 2024-05-17T10:30:00.123+02:00.
        /// </summary>
        /// <remarks>
        /// Seconds, fractional seconds (up to 7 digits) and the offset are optional. The offset is
        /// "Z" or ±hh:mm. Named groups: year, month, day, hour, minute, second, fraction, offset.
        /// Month and day ranges are checked; day-of-month validity per month is not.
        /// </remarks>
        public static Pattern DateTimeIso()
        {
            Pattern hour = Pattern.With.OneOf(Pattern.With.AnyOf("01").Digit, Pattern.With.Literal("2").Range('0', '3'));
            Pattern minute = Pattern.With.Range('0', '5').Digit;

            return Pattern.With
                .StartOfLine
                .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
                .Literal("-")
                .NamedGroup("month", Pattern.With.OneOf(Pattern.With.Literal("0").Range('1', '9'), Pattern.With.Literal("1").Range('0', '2')))
                .Literal("-")
                .NamedGroup("day", Pattern.With.OneOf(Pattern.With.Literal("0").Range('1', '9'), Pattern.With.AnyOf("12").Digit, Pattern.With.Literal("3").AnyOf("01")))
                .AnyOf("Tt ")
                .NamedGroup("hour", hour)
                .Literal(":")
                .NamedGroup("minute", minute)
                .NonCapturingGroup(Pattern.With
                    .Literal(":")
                    .NamedGroup("second", Pattern.With.Range('0', '5').Digit)
                    .NonCapturingGroup(Pattern.With.Literal(".").NamedGroup("fraction", Pattern.With.Digit.Repeat.Times(1, 7))).Repeat.Optional).Repeat.Optional
                .NamedGroup("offset", Pattern.With.OneOf(
                    Pattern.With.AnyOf("Zz"),
                    Pattern.With.AnyOf("+-").Then(hour).Literal(":").Repeat.Optional.Then(minute))).Repeat.Optional
                .EndOfLine;
        }

        /// <summary>
        /// Finds Markdown inline links such as [text](https://example.com "Title").
        /// Not anchored, so use it with Matches or ExtractAll.
        /// </summary>
        /// <remarks>Named groups: text, url, title.</remarks>
        public static Pattern MarkdownLink() => Pattern.With
            .NegativeLookbehind(Pattern.With.Literal("!"))
            .Literal("[")
            .NamedGroup("text", Pattern.With.NoneOf("]").Repeat.ZeroOrMore)
            .Literal("](")
            .NamedGroup("url", Pattern.With.NegatedSet(Pattern.With.Literal(")").Whitespace).Repeat.OneOrMore)
            .NonCapturingGroup(Pattern.With
                .Whitespace.Repeat.OneOrMore
                .Literal("\"")
                .NamedGroup("title", Pattern.With.NoneOf("\"").Repeat.ZeroOrMore)
                .Literal("\"")).Repeat.Optional
            .Literal(")");

        private static Pattern Ipv4Body()
        {
            Pattern octet = Pattern.With.OneOf(
                Pattern.With.Literal("25").Range('0', '5'),
                Pattern.With.Literal("2").Range('0', '4').Digit,
                Pattern.With.Literal("1").Digit.Repeat.Times(2),
                Pattern.With.Range('1', '9').Digit,
                Pattern.With.Digit);
            return Pattern.With
                .Then(octet).Literal(".")
                .Then(octet).Literal(".")
                .Then(octet).Literal(".")
                .Then(octet);
        }
    }
}
