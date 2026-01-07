# What's New

### Added

#### Named Groups and Backreferences
- `NamedGroup(name, pattern)` - Create named capturing groups `(?<name>...)`
- `NonCapturingGroup(pattern)` - Create non-capturing groups `(?:...)`
- `AtomicGroup(pattern)` - Create atomic (non-backtracking) groups `(?>...)`
- `Backreference(name)` - Reference named groups with `\k<name>`
- `Backreference(number)` - Reference numbered groups with `\1`, `\2`, etc.
- `Conditional(groupName, truePattern, falsePattern)` - Conditional matching based on named group capture
- `Conditional(groupNumber, truePattern, falsePattern)` - Conditional matching based on numbered group capture

#### Zero-Width Assertions (Lookahead/Lookbehind)
- `PositiveLookahead(pattern)` - Assert pattern follows current position `(?=...)`
- `NegativeLookahead(pattern)` - Assert pattern does NOT follow current position `(?!...)`
- `PositiveLookbehind(pattern)` - Assert pattern precedes current position `(?<=...)`
- `NegativeLookbehind(pattern)` - Assert pattern does NOT precede current position `(?<!...)`

#### Full Matching API
- `ToRegex(options)` - Compile pattern to `System.Text.RegularExpressions.Regex`
- `WithOptions(options)` - Set default `RegexOptions` for all operations
- `IsMatch(input)` - Test if pattern matches input string
- `IsMatch(input, options)` - Test with specific options
- `Match(input)` - Find first match in input string
- `Match(input, startAt)` - Find first match starting at position
- `Matches(input)` - Find all non-overlapping matches
- `Replace(input, replacement)` - Replace all matches with string
- `Replace(input, replacement, count)` - Replace up to count matches
- `Replace(input, evaluator)` - Replace with custom `MatchEvaluator` function
- `Replace(input, evaluator, count)` - Replace up to count matches with evaluator
- `Split(input)` - Split string at pattern matches
- `Split(input, count)` - Split into maximum count parts

#### Common Patterns Library (`CommonPatterns` class)
Pre-built, tested patterns for common validation scenarios:
- `Email()` - Email address validation
- `Url()` - HTTP/HTTPS URL validation
- `PhoneNumber()` - US phone number formats (with/without parentheses, dashes, dots)
- `IPv4()` - IPv4 addresses with octet validation (0-255)
- `IPv6()` - IPv6 addresses in full notation
- `Guid()` - GUID/UUID in standard hyphenated format
- `HexColor()` - CSS hex colors (#RGB, #RGBA, #RRGGBB, #RRGGBBAA)
- `DateIso()` - ISO 8601 dates (YYYY-MM-DD)
- `Date()` - Common date formats (MM/DD/YYYY, DD-MM-YYYY, etc.)
- `Time24()` - 24-hour time format (HH:MM or HH:MM:SS)
- `Time12()` - 12-hour time format with AM/PM
- `CreditCard()` - 16-digit credit card numbers with optional separators
- `SocialSecurityNumber()` - US SSN format (XXX-XX-XXXX)
- `ZipCode()` - US ZIP codes (5-digit and ZIP+4)
- `Username()` - Alphanumeric usernames (3-20 characters)
- `StrongPassword()` - Password with complexity requirements
- `FilePath()` - Windows and Unix file paths
- `HtmlTag()` - HTML/XML tags with attributes
- `Integer()` - Signed integer numbers
- `Decimal()` - Decimal numbers
- `ScientificNotation()` - Scientific notation (1.5e10)

#### Validation and Debugging
- `IsValid()` - Validate pattern is syntactically correct
- `TryValidate(out errorMessage)` - Validate with detailed error information
- `GetGroupNames()` - Get array of named capturing group names
- `GetGroupCount()` - Get count of all capturing groups
- `Explain()` - Generate human-readable explanation of pattern

### Changed
- `Pattern` class is now a partial class for better code organization
- Test project renamed from `Masterly.FluentRegex.UnitTests` to `Masterly.FluentRegex.Tests`
- Test namespace updated to `Masterly.FluentRegex.Tests`

### Documentation
- Comprehensive README.md with full API documentation
- Table of contents for easy navigation
- Detailed examples for all features
- Quick reference tables for Pattern and CommonPatterns classes
- Real-world use case examples (email, phone, URL parsing, log parsing, data transformation)
- XML documentation comments on all public methods