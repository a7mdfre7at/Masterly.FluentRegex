namespace Masterly.FluentRegex
{
    /// <summary>
    /// Provides pre-built patterns for common validation and matching use cases.
    /// All patterns are anchored (StartOfLine/EndOfLine) for full-string matching.
    /// Each method returns a new Pattern instance that can be further customized.
    /// </summary>
    /// <remarks>
    /// Use these patterns for quick validation without building patterns manually.
    /// Each pattern is built using the fluent API and can be inspected with ToString().
    ///
    /// These patterns are designed for common cases and may need customization
    /// for specific requirements (e.g., international phone numbers, locale-specific dates).
    /// </remarks>
    /// <example>
    /// // Validate an email address
    /// bool isValid = CommonPatterns.Email().IsMatch("user@example.com");
    ///
    /// // Extract all URLs from text (remove anchors first)
    /// var urlPattern = Pattern.With.RegEx(CommonPatterns.Url().ToString().Trim('^', '$'));
    /// var matches = urlPattern.Matches(text);
    /// </example>
    public static class CommonPatterns
    {
        /// <summary>
        /// Matches a basic email address format.
        /// </summary>
        /// <returns>A Pattern matching email addresses.</returns>
        /// <remarks>
        /// Format: local-part@domain.tld
        /// - Local part: letters, digits, dots, underscores, hyphens
        /// - Domain: letters, digits, dots, hyphens
        /// - TLD: at least 2 letters
        ///
        /// Note: This is a simplified pattern. For RFC 5322 compliance,
        /// consider using a more comprehensive pattern or a dedicated library.
        /// </remarks>
        /// <example>
        /// CommonPatterns.Email().IsMatch("user@example.com");      // true
        /// CommonPatterns.Email().IsMatch("user.name@domain.org");  // true
        /// CommonPatterns.Email().IsMatch("invalid");               // false
        /// </example>
        public static Pattern Email() => Pattern.With
            .StartOfLine
            .Set(Pattern.With.Letter.Digit.Literal("._-")).Repeat.OneOrMore
            .Literal("@")
            .Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore
            .Literal(".")
            .Set(Pattern.With.Letter).Repeat.AtLeast(2)
            .EndOfLine;

        /// <summary>
        /// Matches HTTP and HTTPS URLs.
        /// </summary>
        /// <returns>A Pattern matching web URLs.</returns>
        /// <remarks>
        /// Matches URLs starting with http:// or https://
        /// followed by a domain and optional path.
        ///
        /// Does not match FTP, mailto, or other URL schemes.
        /// </remarks>
        /// <example>
        /// CommonPatterns.Url().IsMatch("https://example.com");           // true
        /// CommonPatterns.Url().IsMatch("https://example.com/path/page"); // true
        /// CommonPatterns.Url().IsMatch("ftp://example.com");             // false
        /// </example>
        public static Pattern Url() => Pattern.With
            .StartOfLine
            .Literal("http")
            .Literal("s").Repeat.Optional
            .Literal("://")
            .Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore
            .NonCapturingGroup(
                Pattern.With.Literal(".")
                .Set(Pattern.With.Letter.Digit.Literal("-")).Repeat.OneOrMore
            ).Repeat.OneOrMore
            .NonCapturingGroup(
                Pattern.With.Literal("/")
                .Set(Pattern.With.Word.Literal(".-~:/?#[]@!$&'()*+,;=%")).Repeat.ZeroOrMore
            ).Repeat.Optional
            .EndOfLine;

        /// <summary>
        /// Matches US phone number formats.
        /// </summary>
        /// <returns>A Pattern matching US phone numbers.</returns>
        /// <remarks>
        /// Supported formats:
        /// - (123) 456-7890
        /// - 123-456-7890
        /// - 123.456.7890
        /// - 1234567890
        ///
        /// Does not validate area codes or match international formats.
        /// </remarks>
        /// <example>
        /// CommonPatterns.PhoneNumber().IsMatch("(555) 123-4567"); // true
        /// CommonPatterns.PhoneNumber().IsMatch("555-123-4567");   // true
        /// </example>
        public static Pattern PhoneNumber() => Pattern.With
            .StartOfLine
            .NonCapturingGroup(
                Pattern.With.Literal("(")
                .Digit.Repeat.Times(3)
                .Literal(")")
                .Set(Pattern.With.Whitespace.Literal("-")).Repeat.Optional
            ).Choice(
                Pattern.With.Literal("(").Digit.Repeat.Times(3).Literal(")").Set(Pattern.With.Whitespace.Literal("-")).Repeat.Optional,
                Pattern.With.Digit.Repeat.Times(3).Set(Pattern.With.Literal(".-")).Repeat.Optional
            )
            .Digit.Repeat.Times(3)
            .Set(Pattern.With.Literal(".-")).Repeat.Optional
            .Digit.Repeat.Times(4)
            .EndOfLine;

        /// <summary>
        /// Matches IPv4 addresses (0.0.0.0 to 255.255.255.255).
        /// </summary>
        /// <returns>A Pattern matching valid IPv4 addresses.</returns>
        /// <remarks>
        /// Validates that each octet is between 0 and 255.
        /// Does not match CIDR notation (e.g., 192.168.1.0/24).
        /// </remarks>
        /// <example>
        /// CommonPatterns.IPv4().IsMatch("192.168.1.1");      // true
        /// CommonPatterns.IPv4().IsMatch("255.255.255.255");  // true
        /// CommonPatterns.IPv4().IsMatch("256.1.1.1");        // false (256 > 255)
        /// </example>
        public static Pattern IPv4()
        {
            // Matches 0-255
            var octet = Pattern.With.Choice(
                Pattern.With.Literal("25").Set(Pattern.With.Literal("0-5")),                    // 250-255
                Pattern.With.Literal("2").Set(Pattern.With.Literal("0-4")).Digit,               // 200-249
                Pattern.With.Set(Pattern.With.Literal("01")).Repeat.Optional.Digit.Digit.Repeat.Optional  // 0-199
            );

            return Pattern.With
                .StartOfLine
                .NonCapturingGroup(octet).Literal(".")
                .NonCapturingGroup(octet).Literal(".")
                .NonCapturingGroup(octet).Literal(".")
                .NonCapturingGroup(octet)
                .EndOfLine;
        }

        /// <summary>
        /// Matches IPv6 addresses in full notation (8 groups of 4 hex digits).
        /// </summary>
        /// <returns>A Pattern matching full IPv6 addresses.</returns>
        /// <remarks>
        /// Matches full notation only: xxxx:xxxx:xxxx:xxxx:xxxx:xxxx:xxxx:xxxx
        /// Does not match compressed notation (::) or mixed IPv4/IPv6.
        /// </remarks>
        /// <example>
        /// CommonPatterns.IPv6().IsMatch("2001:0db8:85a3:0000:0000:8a2e:0370:7334"); // true
        /// CommonPatterns.IPv6().IsMatch("::1");  // false (compressed notation)
        /// </example>
        public static Pattern IPv6()
        {
            var hexGroup = Pattern.With.Set(Pattern.With.Digit.Literal("a-fA-F")).Repeat.Times(1, 4);

            return Pattern.With
                .StartOfLine
                .NonCapturingGroup(hexGroup)
                .NonCapturingGroup(Pattern.With.Literal(":").NonCapturingGroup(hexGroup)).Repeat.Times(7)
                .EndOfLine;
        }

        /// <summary>
        /// Matches a GUID/UUID in standard hyphenated format.
        /// </summary>
        /// <returns>A Pattern matching GUIDs.</returns>
        /// <remarks>
        /// Format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx (8-4-4-4-12)
        /// Case-insensitive for hex digits (a-f, A-F).
        /// Does not match GUIDs without hyphens or in braces.
        /// </remarks>
        /// <example>
        /// CommonPatterns.Guid().IsMatch("550e8400-e29b-41d4-a716-446655440000"); // true
        /// CommonPatterns.Guid().IsMatch("550E8400-E29B-41D4-A716-446655440000"); // true
        /// CommonPatterns.Guid().IsMatch("{550e8400-e29b-41d4-a716-446655440000}"); // false
        /// </example>
        public static Pattern Guid()
        {
            var hex = Pattern.With.Digit.Literal("a-fA-F");

            return Pattern.With
                .StartOfLine
                .Set(hex).Repeat.Times(8)
                .Literal("-")
                .Set(hex).Repeat.Times(4)
                .Literal("-")
                .Set(hex).Repeat.Times(4)
                .Literal("-")
                .Set(hex).Repeat.Times(4)
                .Literal("-")
                .Set(hex).Repeat.Times(12)
                .EndOfLine;
        }

        /// <summary>
        /// Matches CSS hex color codes.
        /// </summary>
        /// <returns>A Pattern matching hex color codes.</returns>
        /// <remarks>
        /// Supported formats:
        /// - #RGB (3 digits)
        /// - #RGBA (4 digits)
        /// - #RRGGBB (6 digits)
        /// - #RRGGBBAA (8 digits)
        ///
        /// Case-insensitive for hex digits.
        /// </remarks>
        /// <example>
        /// CommonPatterns.HexColor().IsMatch("#FFF");      // true
        /// CommonPatterns.HexColor().IsMatch("#FF5733");   // true
        /// CommonPatterns.HexColor().IsMatch("#FF573380"); // true (with alpha)
        /// CommonPatterns.HexColor().IsMatch("FFF");       // false (missing #)
        /// </example>
        public static Pattern HexColor()
        {
            var hex = Pattern.With.Digit.Literal("a-fA-F");

            return Pattern.With
                .StartOfLine
                .Literal("#")
                .Choice(
                    Pattern.With.Set(hex).Repeat.Times(8),  // #RRGGBBAA
                    Pattern.With.Set(hex).Repeat.Times(6),  // #RRGGBB
                    Pattern.With.Set(hex).Repeat.Times(4),  // #RGBA
                    Pattern.With.Set(hex).Repeat.Times(3)   // #RGB
                )
                .EndOfLine;
        }

        /// <summary>
        /// Matches dates in ISO 8601 format (YYYY-MM-DD).
        /// </summary>
        /// <returns>A Pattern matching ISO date format.</returns>
        /// <remarks>
        /// Format: YYYY-MM-DD with hyphens.
        /// Performs basic validation on month (01-19) and day (01-39) ranges.
        /// Does not validate month/day combinations (e.g., Feb 30).
        /// </remarks>
        /// <example>
        /// CommonPatterns.DateIso().IsMatch("2024-01-15"); // true
        /// CommonPatterns.DateIso().IsMatch("2024/01/15"); // false (wrong separator)
        /// </example>
        public static Pattern DateIso() => Pattern.With
            .StartOfLine
            .Digit.Repeat.Times(4)  // Year
            .Literal("-")
            .Set(Pattern.With.Literal("0-1")).Digit  // Month
            .Literal("-")
            .Set(Pattern.With.Literal("0-3")).Digit  // Day
            .EndOfLine;

        /// <summary>
        /// Matches dates in common US and ISO formats.
        /// </summary>
        /// <returns>A Pattern matching common date formats.</returns>
        /// <remarks>
        /// Supported formats:
        /// - YYYY-MM-DD, YYYY/MM/DD
        /// - MM/DD/YYYY, MM-DD-YYYY
        /// - DD/MM/YYYY, DD-MM-YYYY
        /// - M/D/YY, M/D/YYYY
        ///
        /// Does not validate actual date values.
        /// </remarks>
        /// <example>
        /// CommonPatterns.Date().IsMatch("2024-01-15");  // true
        /// CommonPatterns.Date().IsMatch("01/15/2024"); // true
        /// CommonPatterns.Date().IsMatch("1/5/24");     // true
        /// </example>
        public static Pattern Date() => Pattern.With
            .StartOfLine
            .Choice(
                // YYYY-MM-DD or YYYY/MM/DD
                Pattern.With.Digit.Repeat.Times(4).Set(Pattern.With.Literal("/-")).Digit.Repeat.Times(1, 2).Set(Pattern.With.Literal("/-")).Digit.Repeat.Times(1, 2),
                // MM/DD/YYYY or DD/MM/YYYY or MM-DD-YYYY or DD-MM-YYYY
                Pattern.With.Digit.Repeat.Times(1, 2).Set(Pattern.With.Literal("/-")).Digit.Repeat.Times(1, 2).Set(Pattern.With.Literal("/-")).Digit.Repeat.Times(2, 4)
            )
            .EndOfLine;

        /// <summary>
        /// Matches time in 24-hour format (HH:MM or HH:MM:SS).
        /// </summary>
        /// <returns>A Pattern matching 24-hour time format.</returns>
        /// <remarks>
        /// Valid hours: 00-23
        /// Valid minutes/seconds: 00-59
        /// Seconds are optional.
        /// </remarks>
        /// <example>
        /// CommonPatterns.Time24().IsMatch("12:30");    // true
        /// CommonPatterns.Time24().IsMatch("23:59:59"); // true
        /// CommonPatterns.Time24().IsMatch("25:00");    // false (invalid hour)
        /// </example>
        public static Pattern Time24() => Pattern.With
            .StartOfLine
            .Choice(
                Pattern.With.Set(Pattern.With.Literal("01")).Digit,  // 00-19
                Pattern.With.Literal("2").Set(Pattern.With.Literal("0-3"))  // 20-23
            )
            .Literal(":")
            .Set(Pattern.With.Literal("0-5")).Digit  // Minutes 00-59
            .NonCapturingGroup(
                Pattern.With.Literal(":")
                .Set(Pattern.With.Literal("0-5")).Digit  // Seconds 00-59
            ).Repeat.Optional
            .EndOfLine;

        /// <summary>
        /// Matches time in 12-hour format with AM/PM indicator.
        /// </summary>
        /// <returns>A Pattern matching 12-hour time format.</returns>
        /// <remarks>
        /// Valid hours: 1-12 (leading zero optional)
        /// Valid minutes/seconds: 00-59
        /// AM/PM indicator (case-insensitive)
        /// Optional space before AM/PM
        /// </remarks>
        /// <example>
        /// CommonPatterns.Time12().IsMatch("12:30 PM");    // true
        /// CommonPatterns.Time12().IsMatch("1:30 am");     // true
        /// CommonPatterns.Time12().IsMatch("12:30:45 PM"); // true
        /// CommonPatterns.Time12().IsMatch("12:30");       // false (missing AM/PM)
        /// </example>
        public static Pattern Time12() => Pattern.With
            .StartOfLine
            .Set(Pattern.With.Literal("0-1")).Repeat.Optional.Digit  // Hours 1-12
            .Literal(":")
            .Set(Pattern.With.Literal("0-5")).Digit  // Minutes 00-59
            .NonCapturingGroup(
                Pattern.With.Literal(":")
                .Set(Pattern.With.Literal("0-5")).Digit  // Seconds 00-59
            ).Repeat.Optional
            .Whitespace.Repeat.Optional
            .Choice(
                Pattern.With.Literal("AM"),
                Pattern.With.Literal("PM"),
                Pattern.With.Literal("am"),
                Pattern.With.Literal("pm")
            )
            .EndOfLine;

        /// <summary>
        /// Matches 16-digit credit card numbers with optional separators.
        /// </summary>
        /// <returns>A Pattern matching credit card numbers.</returns>
        /// <remarks>
        /// Matches exactly 16 digits in groups of 4.
        /// Allows spaces or hyphens between groups.
        /// Does not validate card type or Luhn checksum.
        /// </remarks>
        /// <example>
        /// CommonPatterns.CreditCard().IsMatch("1234567890123456");     // true
        /// CommonPatterns.CreditCard().IsMatch("1234 5678 9012 3456");  // true
        /// CommonPatterns.CreditCard().IsMatch("1234-5678-9012-3456");  // true
        /// </example>
        public static Pattern CreditCard() => Pattern.With
            .StartOfLine
            .Digit.Repeat.Times(4)
            .NonCapturingGroup(
                Pattern.With.Set(Pattern.With.Whitespace.Literal("-")).Repeat.Optional
                .Digit.Repeat.Times(4)
            ).Repeat.Times(3)  // Exactly 3 more groups of 4 digits = 16 total
            .EndOfLine;

        /// <summary>
        /// Matches US Social Security Numbers in standard format.
        /// </summary>
        /// <returns>A Pattern matching SSNs.</returns>
        /// <remarks>
        /// Format: XXX-XX-XXXX (with hyphens required)
        /// Does not validate area number or group number validity.
        /// </remarks>
        /// <example>
        /// CommonPatterns.SocialSecurityNumber().IsMatch("123-45-6789"); // true
        /// CommonPatterns.SocialSecurityNumber().IsMatch("123456789");   // false
        /// </example>
        public static Pattern SocialSecurityNumber() => Pattern.With
            .StartOfLine
            .Digit.Repeat.Times(3)
            .Literal("-")
            .Digit.Repeat.Times(2)
            .Literal("-")
            .Digit.Repeat.Times(4)
            .EndOfLine;

        /// <summary>
        /// Matches US ZIP codes (5-digit or ZIP+4 format).
        /// </summary>
        /// <returns>A Pattern matching ZIP codes.</returns>
        /// <remarks>
        /// Supported formats:
        /// - 12345 (5 digits)
        /// - 12345-6789 (ZIP+4)
        /// </remarks>
        /// <example>
        /// CommonPatterns.ZipCode().IsMatch("12345");      // true
        /// CommonPatterns.ZipCode().IsMatch("12345-6789"); // true
        /// CommonPatterns.ZipCode().IsMatch("1234");       // false
        /// </example>
        public static Pattern ZipCode() => Pattern.With
            .StartOfLine
            .Digit.Repeat.Times(5)
            .NonCapturingGroup(
                Pattern.With.Literal("-")
                .Digit.Repeat.Times(4)
            ).Repeat.Optional
            .EndOfLine;

        /// <summary>
        /// Matches usernames (alphanumeric with underscores, 3-20 characters).
        /// </summary>
        /// <returns>A Pattern matching valid usernames.</returns>
        /// <remarks>
        /// Allowed characters: letters (a-z, A-Z), digits (0-9), underscore (_)
        /// Length: 3-20 characters
        /// Does not allow spaces or special characters.
        /// </remarks>
        /// <example>
        /// CommonPatterns.Username().IsMatch("user123");      // true
        /// CommonPatterns.Username().IsMatch("user_name");    // true
        /// CommonPatterns.Username().IsMatch("ab");           // false (too short)
        /// CommonPatterns.Username().IsMatch("user name");    // false (space)
        /// </example>
        public static Pattern Username() => Pattern.With
            .StartOfLine
            .Set(Pattern.With.Letter.Digit.Literal("_")).Repeat.Times(3, 20)
            .EndOfLine;

        /// <summary>
        /// Matches strong passwords with multiple character type requirements.
        /// </summary>
        /// <returns>A Pattern matching strong passwords.</returns>
        /// <remarks>
        /// Requirements:
        /// - At least 8 characters
        /// - At least one lowercase letter
        /// - At least one uppercase letter
        /// - At least one digit
        /// - At least one special character (!@#$%^&amp;*()_+-=)
        ///
        /// Uses lookahead assertions for validation without consuming characters.
        /// </remarks>
        /// <example>
        /// CommonPatterns.StrongPassword().IsMatch("Password1!");  // true
        /// CommonPatterns.StrongPassword().IsMatch("weakpass");    // false
        /// CommonPatterns.StrongPassword().IsMatch("Short1!");     // false (< 8 chars)
        /// </example>
        public static Pattern StrongPassword() => Pattern.With
            .StartOfLine
            .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.LowercaseLetter))
            .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.UppercaseLetter))
            .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Digit)
            .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.Literal("!@#$%^&*()_+-=")))
            .Anything.Repeat.AtLeast(8)
            .EndOfLine;

        /// <summary>
        /// Matches Windows and Unix file paths.
        /// </summary>
        /// <returns>A Pattern matching file paths.</returns>
        /// <remarks>
        /// Windows: C:\folder\file.ext
        /// Unix: /folder/file.ext
        ///
        /// Allows letters, digits, spaces, dots, hyphens in path components.
        /// </remarks>
        /// <example>
        /// CommonPatterns.FilePath().IsMatch("C:\\Users\\file.txt"); // true
        /// CommonPatterns.FilePath().IsMatch("/home/user/file.txt"); // true
        /// </example>
        public static Pattern FilePath() => Pattern.With
            .StartOfLine
            .Choice(
                // Windows path: C:\folder\file.ext
                Pattern.With.Set(Pattern.With.Letter).Literal(":").Literal("\\")
                    .Set(Pattern.With.Word.Literal(" .-")).Repeat.OneOrMore
                    .NonCapturingGroup(Pattern.With.Literal("\\").Set(Pattern.With.Word.Literal(" .-")).Repeat.OneOrMore).Repeat.ZeroOrMore,
                // Unix path: /folder/file.ext
                Pattern.With.Literal("/")
                    .Set(Pattern.With.Word.Literal(" .-")).Repeat.ZeroOrMore
                    .NonCapturingGroup(Pattern.With.Literal("/").Set(Pattern.With.Word.Literal(" .-")).Repeat.ZeroOrMore).Repeat.ZeroOrMore
            )
            .EndOfLine;

        /// <summary>
        /// Matches HTML/XML tags with optional attributes.
        /// </summary>
        /// <returns>A Pattern matching HTML tags.</returns>
        /// <remarks>
        /// Matches opening, closing, and self-closing tags.
        /// Supports attributes with double or single quoted values.
        /// Does not validate HTML structure or attribute names.
        /// </remarks>
        /// <example>
        /// CommonPatterns.HtmlTag().IsMatch("&lt;div&gt;");                    // true
        /// CommonPatterns.HtmlTag().IsMatch("&lt;/div&gt;");                   // true
        /// CommonPatterns.HtmlTag().IsMatch("&lt;img src=\"test.jpg\" /&gt;"); // true
        /// </example>
        public static Pattern HtmlTag() => Pattern.With
            .Literal("<")
            .Literal("/").Repeat.Optional
            .Set(Pattern.With.Letter.Digit).Repeat.OneOrMore
            .NonCapturingGroup(
                Pattern.With.Whitespace.Repeat.OneOrMore
                .Set(Pattern.With.Word.Literal("-")).Repeat.OneOrMore
                .Literal("=")
                .Choice(
                    Pattern.With.Literal("\"").NegatedSet(Pattern.With.Literal("\"")).Repeat.ZeroOrMore.Literal("\""),
                    Pattern.With.Literal("'").NegatedSet(Pattern.With.Literal("'")).Repeat.ZeroOrMore.Literal("'")
                )
            ).Repeat.ZeroOrMore
            .Whitespace.Repeat.ZeroOrMore
            .Literal("/").Repeat.Optional
            .Literal(">");

        /// <summary>
        /// Matches integer numbers (positive or negative, no decimals).
        /// </summary>
        /// <returns>A Pattern matching integers.</returns>
        /// <example>
        /// CommonPatterns.Integer().IsMatch("123");   // true
        /// CommonPatterns.Integer().IsMatch("-123");  // true
        /// CommonPatterns.Integer().IsMatch("12.34"); // false
        /// </example>
        public static Pattern Integer() => Pattern.With
            .StartOfLine
            .Literal("-").Repeat.Optional
            .Digit.Repeat.OneOrMore
            .EndOfLine;

        /// <summary>
        /// Matches decimal numbers (positive or negative, optional decimal part).
        /// </summary>
        /// <returns>A Pattern matching decimal numbers.</returns>
        /// <example>
        /// CommonPatterns.Decimal().IsMatch("123");     // true
        /// CommonPatterns.Decimal().IsMatch("123.45");  // true
        /// CommonPatterns.Decimal().IsMatch("-123.45"); // true
        /// CommonPatterns.Decimal().IsMatch(".45");     // false (missing integer part)
        /// </example>
        public static Pattern Decimal() => Pattern.With
            .StartOfLine
            .Literal("-").Repeat.Optional
            .Digit.Repeat.OneOrMore
            .NonCapturingGroup(
                Pattern.With.Literal(".")
                .Digit.Repeat.OneOrMore
            ).Repeat.Optional
            .EndOfLine;

        /// <summary>
        /// Matches numbers in scientific notation (e.g., 1.5e10, -2E-5).
        /// </summary>
        /// <returns>A Pattern matching scientific notation.</returns>
        /// <remarks>
        /// Format: [sign]digits[.digits]E|e[sign]digits
        /// Supports both 'e' and 'E' for exponent.
        /// </remarks>
        /// <example>
        /// CommonPatterns.ScientificNotation().IsMatch("1e10");     // true
        /// CommonPatterns.ScientificNotation().IsMatch("1.5E-10");  // true
        /// CommonPatterns.ScientificNotation().IsMatch("-2.5e+10"); // true
        /// CommonPatterns.ScientificNotation().IsMatch("123");      // false (no exponent)
        /// </example>
        public static Pattern ScientificNotation() => Pattern.With
            .StartOfLine
            .Literal("-").Repeat.Optional
            .Digit.Repeat.OneOrMore
            .NonCapturingGroup(Pattern.With.Literal(".").Digit.Repeat.OneOrMore).Repeat.Optional
            .Set(Pattern.With.Literal("eE"))
            .Set(Pattern.With.Literal("+-")).Repeat.Optional
            .Digit.Repeat.OneOrMore
            .EndOfLine;
    }
}
