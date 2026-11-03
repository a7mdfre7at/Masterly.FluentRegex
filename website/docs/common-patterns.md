---
id: common-patterns
title: "Common Patterns Library"
description: "Ready-made, ReDoS-safe patterns for emails, URLs, IP addresses, dates, card numbers, versions and more, with Unicode or ASCII digits."
---

`CommonPatterns` is a static class of ready-made patterns for formats that show up in almost every application: email addresses, URLs, IP addresses, dates, card numbers, versions, tokens and numbers. Each method returns an ordinary `Pattern`, so you can test it, extract from it, combine it with rules, or chain more settings onto it, exactly like a pattern you built yourself.

Use this page when you need to:

- validate a common format without writing the regex yourself,
- pick how strictly an email address or URL is checked,
- read parts of a value, such as the components of a version or a date,
- make `\d` accept only the ASCII digits 0-9,
- know exactly what each pattern accepts and rejects before you rely on it.

All members on this page live in the `Masterly.FluentRegex` namespace and work on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10.

## Using a common pattern

Call a method and use the pattern it returns. `ToString()` shows the regex, so you can always check what you are getting.

```csharp
using System;
using Masterly.FluentRegex;

Pattern email = CommonPatterns.Email();

Console.WriteLine(email.IsMatch("user@example.com"));   // True
Console.WriteLine(email.IsMatch("invalid"));            // False
Console.WriteLine(email);                               // ^[a-zA-Z\d\._\-]+@[a-zA-Z\d\.\-]+\.[a-zA-Z]{2,}$
```

- Almost every pattern is wrapped in `^` and `$`, so it validates a whole value. The two exceptions are `HtmlTag()` and `MarkdownLink()`, which are written to find values inside text.
- Every call builds a new `Pattern`. Store it in a field or a `static readonly` member when you use it often. The pattern caches its compiled `Regex` after the first match.
- Patterns are immutable. Chaining a setting returns a new pattern and leaves the original unchanged (see [Customizing a common pattern](#customizing-a-common-pattern)).
- Every method also has an overload that takes a `DigitMode`. By default `\d` matches any Unicode decimal digit; pass `DigitMode.Ascii` to accept only 0-9 (see [Unicode or ASCII digits](#unicode-or-ascii-digits)).

:::warning

In .NET, `$` matches at the very end of the input and also just before a final newline. `IsMatch` therefore accepts a value that ends with `\n`:

```csharp
Pattern zip = CommonPatterns.ZipCode();

Console.WriteLine(zip.IsMatch("12345"));     // True
Console.WriteLine(zip.IsMatch("12345\n"));   // True: $ also matches before a final newline
```

Trim input from forms and files first, or validate with `IsValid` on a constrained pattern, which requires the match to cover the whole input (see [Adding checks a regex cannot express](#adding-checks-a-regex-cannot-express)).

:::

### Customizing a common pattern

A common pattern is a normal `Pattern`, so every pattern setting works on it. Use `WithOptions` to change how the regex runs and `WithTimeout` to limit how long one match may take.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern time12 = CommonPatterns.Time12().WithOptions(RegexOptions.IgnoreCase);
Console.WriteLine(time12.IsMatch("1:30 Pm"));   // True

Pattern guid = CommonPatterns.Guid().WithTimeout(TimeSpan.FromMilliseconds(100));
Console.WriteLine(guid.Timeout);                // 00:00:00.1000000

Console.WriteLine(ReferenceEquals(CommonPatterns.Email(), CommonPatterns.Email()));   // False
```

- `Time12()` lists `AM`, `PM`, `am` and `pm`, but not mixed case such as `Pm`. `RegexOptions.IgnoreCase` accepts every spelling.
- The last line shows that each call creates a new instance.
- To build a stricter or looser variant, start from the regex shown by `ToString()` with `Pattern.FromRegex`, or write your own with the fluent API (see [Composing Patterns](./composing-patterns.md)).

## Contact and web

| Method | Matches | Named groups |
|--------|---------|--------------|
| `Email()` | `local@domain.tld`. The local part allows letters, digits, `.`, `_` and `-`. The top-level domain needs at least two letters. | none |
| `Email(PatternStrictness)` | An email address at the chosen strictness (see [Choosing a strictness](#choosing-a-strictness)). | `local`, `domain` (strict only) |
| `Url()` | `http://` or `https://`, at least two dot-separated host labels, and an optional path that starts with `/`. | none |
| `Url(PatternStrictness)` | A URL at the chosen strictness. | `scheme`, `host`, `port`, `path`, `query`, `fragment` (strict only) |
| `PhoneNumberE164()` | An E.164 international number: `+`, a non-zero digit and 1 to 14 more digits, with no spaces. | `number` |
| `PhoneNumber()` | Intended for US phone numbers, but see the warning below. | none (one numbered group) |

```csharp
Pattern email = CommonPatterns.Email();
Console.WriteLine(email.IsMatch("first.last@sub.example.org"));   // True
Console.WriteLine(email.IsMatch("user.name+tag@mail.co"));        // False
Console.WriteLine(email.IsMatch("user@example"));                 // False

Pattern url = CommonPatterns.Url();
Console.WriteLine(url.IsMatch("https://example.com/path/page"));  // True
Console.WriteLine(url.IsMatch("ftp://files.example.com"));        // False
Console.WriteLine(url.IsMatch("https://example.com:8080/api"));   // False
Console.WriteLine(url.IsMatch("https://localhost"));              // False

Pattern e164 = CommonPatterns.PhoneNumberE164();
Console.WriteLine(e164.IsMatch("+14155552671"));                  // True
Console.WriteLine(e164.IsMatch("+1 415 555 2671"));               // False
Console.WriteLine(e164.IsMatch("+0123456"));                      // False
```

- `Email()` does not allow `+` in the local part, so plus-addressing such as `name+tag@` is rejected. Use `Email(PatternStrictness.Strict)` or `Email(PatternStrictness.Loose)` when you need it.
- `Email()` checks the shape only. It accepts consecutive dots such as `user@sub..example.com`.
- `Url()` does not accept a port, a query string without a path (`https://example.com?q=1`), or a single-label host such as `localhost`. `Url(PatternStrictness.Strict)` accepts all three.
- `PhoneNumberE164()` expects the number already normalized: no spaces, dashes or brackets.

:::warning

In version 3.1.0, `PhoneNumber()` does not match the formats its documentation lists. Its regex requires a bracketed area code followed by a second three-digit group, so none of the common US formats match:

```csharp
Pattern phone = CommonPatterns.PhoneNumber();

Console.WriteLine(phone.IsMatch("555-123-4567"));        // False
Console.WriteLine(phone.IsMatch("(555) 123-4567"));      // False
Console.WriteLine(phone.IsMatch("5551234567"));          // False
Console.WriteLine(phone.IsMatch("(555) 123-456-7890"));  // True
```

Use `PhoneNumberE164()` for normalized international numbers, or build the US format yourself:

```csharp
Pattern usPhone = Pattern.With
    .StartOfLine
    .OneOf(
        Pattern.With.Literal("(").Digit.Repeat.Times(3).Literal(")").Whitespace.Repeat.Optional,
        Pattern.With.Digit.Repeat.Times(3).AnyOf(".-").Repeat.Optional)
    .Digit.Repeat.Times(3)
    .AnyOf(".-").Repeat.Optional
    .Digit.Repeat.Times(4)
    .EndOfLine
    .WithAsciiDigits();

Console.WriteLine(usPhone);                             // ^(?:\([0-9]{3}\)\s?|[0-9]{3}[\.\-]?)[0-9]{3}[\.\-]?[0-9]{4}$
Console.WriteLine(usPhone.IsMatch("555-123-4567"));     // True
Console.WriteLine(usPhone.IsMatch("(555) 123-4567"));   // True
Console.WriteLine(usPhone.IsMatch("555.123.4567"));     // True
Console.WriteLine(usPhone.IsMatch("5551234567"));       // True
Console.WriteLine(usPhone.IsMatch("555-123-456"));      // False
```

:::

The regex each method produces:

```text
Email()             ^[a-zA-Z\d\._\-]+@[a-zA-Z\d\.\-]+\.[a-zA-Z]{2,}$
Url()               ^https?://[a-zA-Z\d\-]+(?:\.[a-zA-Z\d\-]+)+(?:/[\w\.\-~:/\?#\[\]@!\$&'\(\)\*\+,;=%]*)?$
PhoneNumberE164()   ^\+(?<number>[1-9]\d{1,14})$
PhoneNumber()       ^(?:\(\d{3}\)[\s\-]?)(\(\d{3}\)[\s\-]?|\d{3}[\.\-]?)\d{3}[\.\-]?\d{4}$
```

## Network and identifiers

| Method | Matches | Named groups |
|--------|---------|--------------|
| `IPv4()` | A dotted-quad address with each octet from 0 to 255. Leading zeros are allowed. | none (four numbered groups, one per octet) |
| `IPv6()` | Full notation only: eight groups of one to four hex digits separated by `:`. | none |
| `MacAddress()` | Six pairs of hex digits, separated consistently by `:` or by `-`. | `sep` |
| `Guid()` | A hyphenated GUID or UUID (8-4-4-4-12 hex digits), in either case. | none |
| `Jwt()` | A JSON Web Token in compact form: three base64url segments separated by dots; the first two start with `eyJ`. | `header`, `payload`, `signature` |
| `Base64()` | Non-empty standard Base64 (RFC 4648) with correct `=` padding. | none |

```csharp
Pattern ipv4 = CommonPatterns.IPv4();
Console.WriteLine(ipv4.IsMatch("192.168.1.1"));         // True
Console.WriteLine(ipv4.IsMatch("256.1.1.1"));           // False
Console.WriteLine(ipv4.IsMatch("192.168.1.0/24"));      // False
Console.WriteLine(ipv4.IsMatch("010.001.000.001"));     // True

Pattern ipv6 = CommonPatterns.IPv6();
Console.WriteLine(ipv6.IsMatch("2001:0db8:85a3:0000:0000:8a2e:0370:7334"));  // True
Console.WriteLine(ipv6.IsMatch("1:2:3:4:5:6:7:8"));                          // True
Console.WriteLine(ipv6.IsMatch("2001:db8::8a2e:370:7334"));                  // False

Pattern mac = CommonPatterns.MacAddress();
Console.WriteLine(mac.IsMatch("00:1A:2B:3C:4D:5E"));    // True
Console.WriteLine(mac.IsMatch("00-1a-2b-3c-4d-5e"));    // True
Console.WriteLine(mac.IsMatch("00:1A-2B:3C:4D:5E"));    // False
Console.WriteLine(mac.IsMatch("001A2B3C4D5E"));         // False

Pattern guid = CommonPatterns.Guid();
Console.WriteLine(guid.IsMatch("550E8400-E29B-41D4-A716-446655440000"));    // True
Console.WriteLine(guid.IsMatch("{550e8400-e29b-41d4-a716-446655440000}"));  // False
Console.WriteLine(guid.IsMatch("550e8400e29b41d4a716446655440000"));        // False

Pattern jwt = CommonPatterns.Jwt();
Console.WriteLine(jwt.IsMatch("eyJhbGciOiJub25lIn0.eyJ4IjoxfQ."));   // True
Console.WriteLine(jwt.IsMatch("abc.def.ghi"));                       // False

Pattern base64 = CommonPatterns.Base64();
Console.WriteLine(base64.IsMatch("SGVsbG8gV29ybGQh"));   // True
Console.WriteLine(base64.IsMatch("TQ=="));               // True
Console.WriteLine(base64.IsMatch("TQ="));                // False
Console.WriteLine(base64.IsMatch(""));                   // False
```

- `IPv4()` rejects CIDR notation and accepts leading zeros such as `010`. Some parsers read a leading zero as octal, so reject leading zeros yourself if that matters.
- `IPv6()` does not accept the compressed `::` form, an embedded IPv4 address or a zone index such as `%eth0`.
- `MacAddress()` uses a backreference to the `sep` group, which is how it rejects mixed separators. The compact form without separators is not accepted.
- `Guid()` does not accept braces or the 32-digit form without hyphens.
- `Jwt()` checks the shape only. It never verifies the signature, and it accepts an empty signature (an unsecured token).
- `Base64()` does not accept whitespace or line breaks, and does not accept the base64url alphabet (`-` and `_`).

The regex each method produces:

```text
IPv4()        ^(?:(25[0-5]|2[0-4]\d|[01]?\d\d?))\.(?:(25[0-5]|2[0-4]\d|[01]?\d\d?))\.(?:(25[0-5]|2[0-4]\d|[01]?\d\d?))\.(?:(25[0-5]|2[0-4]\d|[01]?\d\d?))$
IPv6()        ^(?:[\da-fA-F]{1,4})(?::(?:[\da-fA-F]{1,4})){7}$
MacAddress()  ^[0-9a-fA-F]{2}(?<sep>[:\-])(?:[0-9a-fA-F]{2}\k<sep>){4}[0-9a-fA-F]{2}$
Guid()        ^[\da-fA-F]{8}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{12}$
Jwt()         ^(?<header>eyJ[A-Za-z0-9_\-]*)\.(?<payload>eyJ[A-Za-z0-9_\-]*)\.(?<signature>[A-Za-z0-9_\-]*)$
Base64()      ^(?=.)(?:[A-Za-z0-9\+/]{4})*(?:[A-Za-z0-9\+/]{2}==|[A-Za-z0-9\+/]{3}=)?$
```

## Date and time

| Method | Matches | Named groups |
|--------|---------|--------------|
| `DateIso()` | `YYYY-MM-DD` with hyphens. The month is two digits starting with 0 or 1, the day two digits starting with 0 to 3. | none |
| `Date()` | `YYYY-MM-DD` or `YYYY/MM/DD`, and `MM/DD/YYYY`, `DD/MM/YYYY` or their `-` forms, with one- or two-digit months and days and two- to four-digit years. | none (one numbered group) |
| `DateTimeIso()` | An ISO 8601 date and time such as `2024-05-17T10:30:00.123+02:00`. Seconds, a fraction of up to 7 digits and the offset are optional. | `year`, `month`, `day`, `hour`, `minute`, `second`, `fraction`, `offset` |
| `Time24()` | `HH:MM` or `HH:MM:SS` on a 24-hour clock, hours 00 to 23, with a two-digit hour. | none (one numbered group for the hour) |
| `Time12()` | `H:MM` or `HH:MM`, optional `:SS`, an optional whitespace character, then `AM`, `PM`, `am` or `pm`. | none (one numbered group for the indicator) |

```csharp
Pattern dateIso = CommonPatterns.DateIso();
Console.WriteLine(dateIso.IsMatch("2024-01-15"));   // True
Console.WriteLine(dateIso.IsMatch("2024/01/15"));   // False
Console.WriteLine(dateIso.IsMatch("2024-13-01"));   // True
Console.WriteLine(dateIso.IsMatch("2024-00-39"));   // True

Pattern date = CommonPatterns.Date();
Console.WriteLine(date.IsMatch("2024/01/15"));      // True
Console.WriteLine(date.IsMatch("01/15/2024"));      // True
Console.WriteLine(date.IsMatch("1/5/24"));          // True
Console.WriteLine(date.IsMatch("15.01.2024"));      // False
Console.WriteLine(date.IsMatch("99/99/999"));       // True

Pattern dateTime = CommonPatterns.DateTimeIso();
Console.WriteLine(dateTime.IsMatch("2024-05-17T10:30"));                    // True
Console.WriteLine(dateTime.IsMatch("2024-05-17T23:59:59.1234567+02:00"));   // True
Console.WriteLine(dateTime.IsMatch("2024-12-31 00:00:00-0530"));            // True
Console.WriteLine(dateTime.IsMatch("2024-13-17T10:30"));                    // False
Console.WriteLine(dateTime.IsMatch("2024-05-17"));                          // False
Console.WriteLine(dateTime.IsMatch("2024-02-31T10:30"));                    // True

Pattern time24 = CommonPatterns.Time24();
Console.WriteLine(time24.IsMatch("23:59:59"));   // True
Console.WriteLine(time24.IsMatch("24:00"));      // False
Console.WriteLine(time24.IsMatch("9:30"));       // False

Pattern time12 = CommonPatterns.Time12();
Console.WriteLine(time12.IsMatch("12:30 PM"));   // True
Console.WriteLine(time12.IsMatch("1:30am"));     // True
Console.WriteLine(time12.IsMatch("12:30"));      // False
Console.WriteLine(time12.IsMatch("1:30 Pm"));    // False
Console.WriteLine(time12.IsMatch("13:00 PM"));   // True
Console.WriteLine(time12.IsMatch("00:15 AM"));   // True
```

- `DateIso()` only limits the first digit of the month and day, so `2024-13-01` and `2024-00-39` pass. Add `Validators.IsoDate` to accept only real calendar dates (see [Adding checks a regex cannot express](#adding-checks-a-regex-cannot-express)).
- `Date()` does not check the values at all, and it does not accept dots as separators. A value like `01/02/2024` is ambiguous: the pattern cannot tell January 2 from February 1.
- `DateTimeIso()` checks month (01-12), day (01-31), hour (00-23) and minute ranges, but not the number of days in a month, so `2024-02-31T10:30` passes. It needs a time: a date alone does not match. The separator can be `T`, `t` or a space, and the offset can be `Z`, `z`, `+hh:mm` or `+hhmm`.
- `Time24()` needs a two-digit hour, so `9:30` is rejected.
- `Time12()` checks the hour only loosely: it accepts hours from `0` to `19`, so `13:00 PM` and `00:15 AM` pass. Mixed-case `Pm` is rejected unless you add `RegexOptions.IgnoreCase`.

The regex each method produces:

```text
DateIso()      ^\d{4}-[0-1]\d-[0-3]\d$
Date()         ^(\d{4}[/\-]\d{1,2}[/\-]\d{1,2}|\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4})$
DateTimeIso()  ^(?<year>\d{4})-(?<month>(?:0[1-9]|1[0-2]))-(?<day>(?:0[1-9]|[12]\d|3[01]))[Tt ](?<hour>(?:[01]\d|2[0-3])):(?<minute>[0-5]\d)(?::(?<second>[0-5]\d)(?:\.(?<fraction>\d{1,7}))?)?(?<offset>(?:[Zz]|[\+\-](?:[01]\d|2[0-3]):?[0-5]\d))?$
Time24()       ^([01]\d|2[0-3]):[0-5]\d(?::[0-5]\d)?$
Time12()       ^[0-1]?\d:[0-5]\d(?::[0-5]\d)?\s?(AM|PM|am|pm)$
```

## Financial and identity

| Method | Matches | Named groups |
|--------|---------|--------------|
| `CreditCard()` | Exactly 16 digits in four groups of four. Each gap may be empty, a space, a hyphen or another whitespace character. | none |
| `Iban()` | The shape of an IBAN: two uppercase letters, two check digits, then 11 to 30 uppercase letters or digits, either compact or in space-separated groups of four. | `country`, `check` |
| `SocialSecurityNumber()` | A US Social Security Number in the form `XXX-XX-XXXX`. The hyphens are required. | none |
| `ZipCode()` | A US ZIP code: five digits, or ZIP+4 such as `12345-6789`. | none |

```csharp
Pattern card = CommonPatterns.CreditCard();
Console.WriteLine(card.IsMatch("4111 1111 1111 1111"));   // True
Console.WriteLine(card.IsMatch("4111-1111 1111-1111"));   // True
Console.WriteLine(card.IsMatch("378282246310005"));       // False
Console.WriteLine(card.IsMatch("1234 5678 9012 3456"));   // True

Pattern iban = CommonPatterns.Iban();
Console.WriteLine(iban.IsMatch("DE89370400440532013000"));        // True
Console.WriteLine(iban.IsMatch("GB82 WEST 1234 5698 7654 32"));   // True
Console.WriteLine(iban.IsMatch("de89370400440532013000"));        // False

Pattern ssn = CommonPatterns.SocialSecurityNumber();
Console.WriteLine(ssn.IsMatch("123-45-6789"));   // True
Console.WriteLine(ssn.IsMatch("123456789"));     // False
Console.WriteLine(ssn.IsMatch("000-00-0000"));   // True

Pattern zip = CommonPatterns.ZipCode();
Console.WriteLine(zip.IsMatch("12345"));         // True
Console.WriteLine(zip.IsMatch("12345-6789"));    // True
Console.WriteLine(zip.IsMatch("12345-678"));     // False
```

- `CreditCard()` accepts only 16-digit numbers, so 15-digit American Express numbers are rejected. Each separator is chosen on its own, so mixed spaces and hyphens pass. It does not check the card type or the Luhn checksum; `1234 5678 9012 3456` passes.
- `Iban()` checks the shape only. Lowercase input is rejected. The mod-97 checksum cannot be written as a regex; add `Validators.IbanChecksum` for it.
- `SocialSecurityNumber()` does not check which area, group and serial numbers were ever issued, so `000-00-0000` passes.

The regex each method produces:

```text
CreditCard()            ^\d{4}(?:[\s\-]?\d{4}){3}$
Iban()                  ^(?<country>[A-Z]{2})(?<check>\d{2})(?:[A-Z\d]{11,30}|(?: [A-Z\d]{4}){2,7}(?: [A-Z\d]{1,3})?)$
SocialSecurityNumber()  ^\d{3}-\d{2}-\d{4}$
ZipCode()               ^\d{5}(?:-\d{4})?$
```

## User input and text

| Method | Matches | Named groups |
|--------|---------|--------------|
| `Username()` | 3 to 20 ASCII letters, digits or underscores. With the default Unicode digits, digits from other scripts also count. | none |
| `StrongPassword()` | At least 8 characters with at least one lowercase letter, one uppercase letter, one digit and one of `!@#$%^&*()_+-=`. | none |
| `Slug()` | Lowercase ASCII letters and digits in words separated by single hyphens, such as `hello-world-2024`. | none |
| `HexColor()` | A CSS hex color: `#` followed by 3, 4, 6 or 8 hex digits, in either case. | none (one numbered group) |
| `FilePath()` | An absolute Windows path such as `C:\folder\file.ext` or an absolute Unix path such as `/folder/file.ext`. | none (one numbered group) |
| `SemVer()` | A Semantic Versioning 2.0.0 version, using the official grammar from semver.org. | `major`, `minor`, `patch`, `prerelease`, `buildmetadata` |

```csharp
Pattern username = CommonPatterns.Username();
Console.WriteLine(username.IsMatch("User_Name_123"));   // True
Console.WriteLine(username.IsMatch("ab"));              // False
Console.WriteLine(username.IsMatch("user.name"));       // False

Pattern password = CommonPatterns.StrongPassword();
Console.WriteLine(password.IsMatch("Password1!"));      // True
Console.WriteLine(password.IsMatch("Password1"));       // False
Console.WriteLine(password.IsMatch("Passw0rd?"));       // False
Console.WriteLine(password.IsMatch("Pass word1!"));     // True

Pattern slug = CommonPatterns.Slug();
Console.WriteLine(slug.IsMatch("hello-world-2024"));    // True
Console.WriteLine(slug.IsMatch("Hello-World"));         // False
Console.WriteLine(slug.IsMatch("hello--world"));        // False

Pattern color = CommonPatterns.HexColor();
Console.WriteLine(color.IsMatch("#FFF"));               // True
Console.WriteLine(color.IsMatch("#FF573380"));          // True
Console.WriteLine(color.IsMatch("#FF"));                // False
Console.WriteLine(color.IsMatch("FF5733"));             // False

Pattern path = CommonPatterns.FilePath();
Console.WriteLine(path.IsMatch(@"C:\Users\file.txt"));              // True
Console.WriteLine(path.IsMatch(@"d:\my folder\sub-dir\a.tar.gz"));  // True
Console.WriteLine(path.IsMatch("/var/log/"));                       // True
Console.WriteLine(path.IsMatch("C:/Users/file.txt"));               // False
Console.WriteLine(path.IsMatch("./relative/file.txt"));             // False
Console.WriteLine(path.IsMatch(@"\\server\share\file.txt"));        // False

Pattern semVer = CommonPatterns.SemVer();
Console.WriteLine(semVer.IsMatch("1.0.0-alpha.1+build.5"));   // True
Console.WriteLine(semVer.IsMatch("1.02.0"));                  // False
Console.WriteLine(semVer.IsMatch("v1.0.0"));                  // False
```

- `Username()` uses ASCII letters only and does not stop a name from starting with a digit or an underscore.
- `StrongPassword()` counts only the listed special characters, so `?` does not satisfy that rule. It accepts spaces and any other character, including non-ASCII letters, and it has no upper length limit. A line break inside the value makes it fail, because `.` does not match `\n`; a single trailing line break is accepted, as with every pattern that ends in `$`.
- `FilePath()` accepts word characters (`\w`: letters, digits and `_` from any script), spaces, `.` and `-` in each segment. It rejects relative paths, forward slashes in Windows paths, UNC paths such as `\\server\share` and characters such as `*` or `?`.
- `SemVer()` rejects leading zeros in numeric parts and a `v` prefix. Strip the prefix first when your tags use one.

The regex each method produces:

```text
Username()        ^[a-zA-Z\d_]{3,20}$
StrongPassword()  ^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#\$%\^&\*\(\)_\+\-=]).{8,}$
Slug()            ^[a-z\d]+(?:-[a-z\d]+)*$
HexColor()        ^#([\da-fA-F]{8}|[\da-fA-F]{6}|[\da-fA-F]{4}|[\da-fA-F]{3})$
FilePath()        ^([a-zA-Z]:\\[\w \.\-]+(?:\\[\w \.\-]+)*|/[\w \.\-]*(?:/[\w \.\-]*)*)$
SemVer()          ^(?<major>(?:0|[1-9]\d*))\.(?<minor>(?:0|[1-9]\d*))\.(?<patch>(?:0|[1-9]\d*))(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z\-][0-9a-zA-Z\-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z\-][0-9a-zA-Z\-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z\-]+(?:\.[0-9a-zA-Z\-]+)*))?$
```

## Numbers

| Method | Matches | Named groups |
|--------|---------|--------------|
| `Integer()` | An optional `-` followed by one or more digits. | none |
| `Decimal()` | An `Integer()` value with an optional `.` and one or more fraction digits. | none |
| `ScientificNotation()` | A `Decimal()` value followed by `e` or `E`, an optional `+` or `-`, and one or more exponent digits. | none |

```csharp
Pattern integer = CommonPatterns.Integer();
Console.WriteLine(integer.IsMatch("-123"));      // True
Console.WriteLine(integer.IsMatch("007"));       // True
Console.WriteLine(integer.IsMatch("+789"));      // False
Console.WriteLine(integer.IsMatch("1,000"));     // False

Pattern number = CommonPatterns.Decimal();
Console.WriteLine(number.IsMatch("-123.45"));    // True
Console.WriteLine(number.IsMatch("123"));        // True
Console.WriteLine(number.IsMatch(".45"));        // False
Console.WriteLine(number.IsMatch("12,5"));       // False

Pattern scientific = CommonPatterns.ScientificNotation();
Console.WriteLine(scientific.IsMatch("1.5E-10"));   // True
Console.WriteLine(scientific.IsMatch("-2.5e+10"));  // True
Console.WriteLine(scientific.IsMatch("123"));       // False
Console.WriteLine(scientific.IsMatch(".5e3"));      // False
```

- None of the number patterns accept a leading `+`, thousands separators, or a comma as the decimal separator.
- `Decimal()` needs a digit before the `.`, so `.45` and `12.` are rejected.
- `ScientificNotation()` requires an exponent. Plain numbers do not match; combine it with `Decimal()` when both forms are allowed.
- The patterns check the text only. They do not check that the value fits in an `int` or a `decimal`; parse it to be sure.

The regex each method produces:

```text
Integer()             ^-?\d+$
Decimal()             ^-?\d+(?:\.\d+)?$
ScientificNotation()  ^-?\d+(?:\.\d+)?[eE][\+\-]?\d+$
```

## Markup

| Method | Matches | Named groups |
|--------|---------|--------------|
| `HtmlTag()` | One opening, closing or self-closing HTML or XML tag, with attributes whose values are in double or single quotes. Not anchored. | none (one numbered group for the last attribute value) |
| `MarkdownLink()` | An inline Markdown link such as `[text](https://example.com "Title")`. Images (`![alt](src)`) are skipped. Not anchored. | `text`, `url`, `title` |

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern tag = CommonPatterns.HtmlTag();
Console.WriteLine(tag.IsMatch("<img src=\"test.jpg\" />"));   // True
Console.WriteLine(tag.IsMatch("<a href=unquoted>"));          // False
Console.WriteLine(tag.IsMatch("<input disabled>"));           // False
Console.WriteLine(tag.Count("<p>Hello <b>world</b></p>"));    // 4

foreach (Match m in tag.Matches("<p class='intro'>Hi</p>"))
{
    Console.WriteLine(m.Value);
}
// prints: <p class='intro'>
// prints: </p>
```

- Because `HtmlTag()` is not anchored, `IsMatch` returns `true` for any text that contains a tag. Use `Matches` or `Count` to work with each tag.
- Unquoted attribute values and attributes without a value, such as `disabled`, are not accepted, so `<input disabled>` is not found.
- A regex cannot parse HTML structure. Use `HtmlTag()` to find tags, not to validate a document.
- `MarkdownLink()` is shown with typed extraction in [Reading named groups](#reading-named-groups).

The regex each method produces:

```text
HtmlTag()       </?[a-zA-Z\d]+(?:\s+[\w\-]+=("[^"]*"|'[^']*'))*\s*/?>
MarkdownLink()  (?<!!)\[(?<text>[^\]]*)\]\((?<url>[^\)\s]+)(?:\s+"(?<title>[^"]*)")?\)
```

## Choosing a strictness

`Email` and `Url` have an overload that takes a `PatternStrictness`. Pick the level based on what you do with the value.

| Value | Use it when | `Email(strictness)` | `Url(strictness)` |
|-------|-------------|---------------------|-------------------|
| `Loose` | You only filter obvious garbage, for example in a search box. | `something@something.something`: no spaces, exactly one `@`, at least one dot after it. Any other character, including non-ASCII letters, is allowed. | Any scheme (a letter, then letters, digits, `+`, `.` or `-`), then `://` and one or more non-space characters. |
| `Standard` | The balanced default. | The same pattern as `Email()`. | The same pattern as `Url()`. |
| `Strict` | You validate data you store or send on. | RFC 5322 dot-atom local part of at most 64 characters, RFC 1035 domain labels of at most 63 characters with no leading or trailing hyphen, a letter-only top-level domain, and at most 254 characters overall. | `http` or `https`; a host name, an IPv4 address or `localhost`; an optional port from 1 to 65535; and RFC 3986 path, query and fragment characters. |

```csharp
Pattern loose = CommonPatterns.Email(PatternStrictness.Loose);
Pattern standard = CommonPatterns.Email(PatternStrictness.Standard);
Pattern strict = CommonPatterns.Email(PatternStrictness.Strict);

Console.WriteLine(loose.IsMatch("üñî@exämple.de"));                    // True
Console.WriteLine(standard.IsMatch("üñî@exämple.de"));                 // False

Console.WriteLine(standard.IsMatch("o'brien+tag@mail.example.co.uk"));  // False
Console.WriteLine(strict.IsMatch("o'brien+tag@mail.example.co.uk"));    // True

Console.WriteLine(standard.IsMatch("a..b@example.com"));                // True
Console.WriteLine(strict.IsMatch("a..b@example.com"));                  // False
Console.WriteLine(strict.IsMatch("a@-example.com"));                    // False
Console.WriteLine(strict.IsMatch(new string('a', 65) + "@example.com")); // False

Match match = strict.Match("jane.doe@example.org");
Console.WriteLine(match.Groups["local"].Value);    // jane.doe
Console.WriteLine(match.Groups["domain"].Value);   // example.org
```

- Strict is not always narrower. It accepts characters that `Standard` rejects, such as `+` and `'`, and rejects shapes that `Standard` accepts, such as consecutive dots.
- Only the strict email pattern has named groups (`local` and `domain`).
- No regex can tell whether a mailbox exists. Send a confirmation email when that matters.

```csharp
Pattern looseUrl = CommonPatterns.Url(PatternStrictness.Loose);
Pattern strictUrl = CommonPatterns.Url(PatternStrictness.Strict);

Console.WriteLine(looseUrl.IsMatch("ftp://files.example.com/a"));            // True
Console.WriteLine(looseUrl.IsMatch("custom+app://x"));                       // True
Console.WriteLine(strictUrl.IsMatch("http://localhost:8080/api?q=1#top"));   // True
Console.WriteLine(strictUrl.IsMatch("https://192.168.0.1/x"));               // True
Console.WriteLine(strictUrl.IsMatch("https://example.com:70000"));           // False
Console.WriteLine(strictUrl.IsMatch("https://300.1.1.1/"));                  // False
Console.WriteLine(strictUrl.IsMatch("https://example"));                     // False
Console.WriteLine(strictUrl.IsMatch("ftp://example.com"));                   // False
```

- The strict URL pattern has the named groups `scheme`, `host`, `port`, `path`, `query` and `fragment`, so you can bind it to a record (see [Reading named groups](#reading-named-groups)).
- A host needs at least one dot and a top-level domain of letters, except `localhost` and IPv4 addresses. IPv6 hosts such as `[::1]` are not accepted.
- An undefined `PatternStrictness` value, such as `(PatternStrictness)99`, falls back to `Standard`.

## Reading named groups

Several patterns name their parts. Bind them to a record or class with `Extract<T>`, `TryExtract<T>` and `ExtractAll<T>`: each property is filled from the group of the same name, ignoring case, and converted to the property type (see [Typed Extraction](./typed-extraction.md)).

```csharp
using System;
using Masterly.FluentRegex;

SemanticVersion version = CommonPatterns.SemVer().Extract<SemanticVersion>("3.0.0-rc.2+build.77");
Console.WriteLine(version);
// prints: SemanticVersion { Major = 3, Minor = 0, Patch = 0, Prerelease = rc.2, BuildMetadata = build.77 }

SemanticVersion release = CommonPatterns.SemVer().Extract<SemanticVersion>("1.4.2");
Console.WriteLine(release.Prerelease is null);   // True

Console.WriteLine(CommonPatterns.SemVer().TryExtract("1.02.0", out SemanticVersion? bad));  // False

UrlParts parts = CommonPatterns.Url(PatternStrictness.Strict)
    .Extract<UrlParts>("https://api.example.com:8443/v1/users?active=true");
Console.WriteLine(parts);
// prints: UrlParts { Scheme = https, Host = api.example.com, Port = 8443, Path = /v1/users, Query = active=true }

IsoStamp stamp = CommonPatterns.DateTimeIso().Extract<IsoStamp>("2024-05-17T10:30:00+02:00");
Console.WriteLine(stamp);
// prints: IsoStamp { Year = 2024, Month = 5, Day = 17, Hour = 10, Minute = 30, Offset = +02:00 }

public record SemanticVersion(int Major, int Minor, int Patch, string? Prerelease, string? BuildMetadata);

public record UrlParts(string Scheme, string Host, int? Port, string Path, string? Query);

public record IsoStamp(int Year, int Month, int Day, int Hour, int Minute, string? Offset);
```

- An optional group that did not take part in the match binds as `null`, so make those properties nullable (`string?`, `int?`).
- `BuildMetadata` binds to the `buildmetadata` group because names are compared without case.
- A record may bind only some of the groups. `IsoStamp` leaves out `second` and `fraction`.

`MarkdownLink()` is not anchored, so it is meant for `ExtractAll`:

```csharp
// markdown comes from your application
string markdown = "See [the docs](https://example.com/docs \"Docs\") and ![logo](logo.png) or [home](/).";

foreach (Link link in CommonPatterns.MarkdownLink().ExtractAll<Link>(markdown))
{
    Console.WriteLine(link);
}
// prints: Link { Text = the docs, Url = https://example.com/docs, Title = Docs }
// prints: Link { Text = home, Url = /, Title =  }

public record Link(string Text, string Url, string? Title);
```

- The image `![logo](logo.png)` is skipped by a negative lookbehind for `!`.
- The second link has no title, so `Title` is `null`.

For the other patterns, read the groups from the `Match` directly:

```csharp
Match iban = CommonPatterns.Iban().Match("GB82 WEST 1234 5698 7654 32");
Console.WriteLine(iban.Groups["country"].Value);   // GB
Console.WriteLine(iban.Groups["check"].Value);     // 82

Match phone = CommonPatterns.PhoneNumberE164().Match("+442071838750");
Console.WriteLine(phone.Groups["number"].Value);   // 442071838750

Match mac = CommonPatterns.MacAddress().Match("00-1A-2B-3C-4D-5E");
Console.WriteLine(mac.Groups["sep"].Value);        // -
```

| Method | Named groups |
|--------|--------------|
| `Email(PatternStrictness.Strict)` | `local`, `domain` |
| `Url(PatternStrictness.Strict)` | `scheme`, `host`, `port`, `path`, `query`, `fragment` |
| `PhoneNumberE164()` | `number` (the digits without `+`) |
| `Iban()` | `country`, `check` |
| `SemVer()` | `major`, `minor`, `patch`, `prerelease`, `buildmetadata` |
| `Jwt()` | `header`, `payload`, `signature` |
| `MacAddress()` | `sep` (the separator) |
| `DateTimeIso()` | `year`, `month`, `day`, `hour`, `minute`, `second`, `fraction`, `offset` |
| `MarkdownLink()` | `text`, `url`, `title` |

## Unicode or ASCII digits

In .NET, `\d` matches every Unicode decimal digit, not only 0-9. `\d{4}` also accepts `٢٠٢٦` (Arabic-Indic digits) and `२०२६` (Devanagari digits). That suits free text in any language. Formats defined over ASCII, such as IP addresses, ISO dates or card numbers, usually should not accept them.

The `DigitMode` enum chooses which characters count as a digit:

| Value | `\d` matches | Notes |
|-------|--------------|-------|
| `DigitMode.Unicode` | Any Unicode decimal digit (category `Nd`). | The .NET default and the behavior of every parameterless `CommonPatterns` method. |
| `DigitMode.Ascii` | Only `0` to `9`. `\D` matches everything else. | Use it for formats defined over ASCII. |

### DigitMode overloads

Every `CommonPatterns` method has an overload that takes a `DigitMode` as its last parameter. `Email` and `Url` also have `(PatternStrictness, DigitMode)` overloads.

```csharp
// "٩" is ARABIC-INDIC DIGIT NINE, "٤٢" is 42 in Arabic-Indic digits
Console.WriteLine(CommonPatterns.IPv4().IsMatch("0.0.0.٩"));                   // True
Console.WriteLine(CommonPatterns.IPv4(DigitMode.Unicode).IsMatch("0.0.0.٩"));  // True
Console.WriteLine(CommonPatterns.IPv4(DigitMode.Ascii).IsMatch("0.0.0.٩"));    // False
Console.WriteLine(CommonPatterns.IPv4(DigitMode.Ascii).IsMatch("10.0.0.1"));   // True

Console.WriteLine(CommonPatterns.Integer().IsMatch("٤٢"));                     // True
Console.WriteLine(CommonPatterns.Integer(DigitMode.Ascii).IsMatch("٤٢"));      // False

Console.WriteLine(CommonPatterns.DateIso().IsMatch("٢٠٢٦-01-15"));             // True
Console.WriteLine(CommonPatterns.DateIso().IsMatch("٢٠٢٦-٠١-١٥"));             // False
Console.WriteLine(CommonPatterns.DateIso(DigitMode.Ascii));                    // ^[0-9]{4}-[0-1][0-9]-[0-3][0-9]$

Pattern email = CommonPatterns.Email(PatternStrictness.Standard, DigitMode.Ascii);
Console.WriteLine(email.IsMatch("user٩@example.com"));   // False
Console.WriteLine(email.IsMatch("user9@example.com"));   // True
```

- `X(DigitMode.Unicode)` returns the same regex as `X()`. `X(DigitMode.Ascii)` returns `X().WithAsciiDigits()`.
- The Unicode default can be inconsistent inside one value. `DateIso()` accepts Arabic-Indic digits in the year (`\d`) but not in the month or day (`[0-1]`, `[0-3]`), so a fully Arabic-Indic date is rejected while a mixed one passes. `DigitMode.Ascii` removes the surprise.
- An undefined value, such as `(DigitMode)42`, throws `ArgumentOutOfRangeException`. When the argument is a compile-time constant, the analyzer catches it first and fails the build with error `FRX006` ("Pattern construction throws").

:::tip

Use `DigitMode.Ascii` for anything you store, compare or pass to another system: IP addresses, dates, card numbers, postal codes, versions. Keep the Unicode default for free text typed by people in any language.

:::

### Switching any pattern to ASCII digits

`WithAsciiDigits()` works on every `Pattern`, not only on common patterns. `WithDigits(DigitMode)` does the same with the mode as a parameter.

```csharp
Pattern year = Pattern.FromRegex(@"^\d{4}$");
Pattern asciiYear = year.WithAsciiDigits();

Console.WriteLine(asciiYear);                      // ^[0-9]{4}$
Console.WriteLine(year.IsMatch("٢٠٢٦"));           // True
Console.WriteLine(asciiYear.IsMatch("٢٠٢٦"));      // False
Console.WriteLine(asciiYear.IsMatch("2026"));      // True

Console.WriteLine(Pattern.FromRegex(@"\D+").WithAsciiDigits());               // [^0-9]+
Console.WriteLine(Pattern.FromRegex(@"[\d.]+").WithAsciiDigits());            // [0-9.]+
Console.WriteLine(Pattern.FromRegex(@"(?<=\d)x(?!\d)").WithAsciiDigits());    // (?<=[0-9])x(?![0-9])
Console.WriteLine(Pattern.FromRegex(@"\p{Nd}+\w+").WithAsciiDigits());        // \p{Nd}+\w+

Pattern digits = Pattern.FromRegex(@"\d+");
Console.WriteLine(ReferenceEquals(digits.WithDigits(DigitMode.Unicode), digits));   // True
Console.WriteLine(digits.WithDigits(DigitMode.Ascii));                              // [0-9]+
```

| Member | Returns | Throws |
|--------|---------|--------|
| `WithAsciiDigits()` | A copy in which `\d` and `\D` mean ASCII digits and non-digits. Returns this same instance when nothing needs rewriting. | Nothing. |
| `WithDigits(DigitMode digits)` | This pattern for `Unicode`; `WithAsciiDigits()` for `Ascii`. | `ArgumentOutOfRangeException` for an undefined value. |

What the rewrite changes, and what it keeps:

- `\d` and `\D` are rewritten everywhere: on their own, inside sets, inside negated sets, inside class subtractions and inside lookarounds. Each replacement matches exactly the ASCII meaning. For example, `[\Dx]` becomes a set of every character except 0-9, plus `x`. That set is long, because it lists the Unicode digit ranges that are now non-digits.
- The options and the timeout are kept.
- Explicit categories such as `\p{Nd}` are left as written, because you asked for them.
- `\w` is left as written. It still contains every Unicode digit (see the next section).
- A pattern with no `\d` or `\D`, an empty pattern, and a pattern with `RegexOptions.ECMAScript` (where `\d` is already ASCII) are returned unchanged, as the same instance.

### What ASCII mode does not change

Only `\d` and `\D` change meaning. Classes such as `\w` and negated sets still accept Unicode digits.

```csharp
Pattern url = CommonPatterns.Url(DigitMode.Ascii);
Console.WriteLine(url.IsMatch("https://example.com/page٣"));                       // True
Console.WriteLine(CommonPatterns.FilePath(DigitMode.Ascii).IsMatch("/tmp/٣"));     // True
Console.WriteLine(CommonPatterns.Username().IsMatch("user٣"));                     // True
Console.WriteLine(CommonPatterns.Username(DigitMode.Ascii).IsMatch("user٣"));      // False
```

- The `Url()` path uses `\w`, so it still accepts `٣` after the switch. `FilePath()` uses only `\w` for its segments.
- `Username()` uses `[a-zA-Z\d_]`, so the switch makes it ASCII only.
- These methods produce the same regex in both modes, because they contain no `\d` or `\D`: `Base64`, `FilePath`, `Jwt`, `MacAddress`, `MarkdownLink`, `Email(Loose)`, `Email(Strict)` and `Url(Loose)`.
- To make a pattern fully ASCII, see [Unicode](./unicode.md) for the other Unicode-aware classes.

### At compile time and on the command line

The `DigitMode` overloads work inside a `[GeneratePattern]` member. The source generator evaluates the chain at build time and emits the regex text and a cached `Regex` (see [Compile-time Checks](./compile-time-checks.md)).

```csharp
using System;
using Masterly.FluentRegex;

public static partial class Network
{
    [GeneratePattern]
    public static Pattern Address => CommonPatterns.IPv4(DigitMode.Ascii);
}
```

```csharp
Console.WriteLine(Network.AddressRegex().IsMatch("10.0.0.1"));   // True
Console.WriteLine(Network.AddressRegex().IsMatch("0.0.0.٩"));    // False
```

- The generator adds `AddressRegex()`, which returns a cached `Regex`, and the constant `AddressSource`, which holds the regex text with `[0-9]` instead of `\d`.

The `fluentregex` tool accepts `--ascii-digits` on every command that takes a regex (see [Command-line Tool](./command-line-tool.md)). Without it, generated samples can contain digits from other scripts:

```text
> fluentregex generate "^\d{4}$" --count 3 --seed 1
1749
6296
67߄3

> fluentregex generate "^\d{4}$" --count 3 --seed 1 --ascii-digits
1749
6296
6703
```

## Finding values inside text

Most common patterns are anchored with `^` and `$`, so `Matches` on a sentence finds nothing unless the whole sentence is one value. There are two ways to search text with them.

```csharp
// text comes from your application
string text = "Contact sales@example.com or support@example.org today.";

Pattern findEmail = Pattern.FromRegex(CommonPatterns.Email().ToString().Trim('^', '$'));

foreach (Match m in findEmail.Matches(text))
{
    Console.WriteLine(m.Value);
}
// prints: sales@example.com
// prints: support@example.org

Redactor redactor = Redactor.For(CommonPatterns.Email());
Console.WriteLine(redactor.Mask(text));
// prints: Contact *****@*******.*** or *******@*******.*** today.
```

- Trimming the anchors works for patterns that use `^` and `$` only at the ends. It breaks `Email(PatternStrictness.Strict)`, whose length lookahead contains its own `$`. Without anchors, a match can also start or end in the middle of a longer token.
- `Redactor` does the conversion for you. It turns the outer `^` and `$` into word boundaries, `(?<!\w)` and `(?!\w)`, so a value is found only as a whole token (see [PII Redaction](./pii-redaction.md)).
- `HtmlTag()` and `MarkdownLink()` are not anchored and work with `Matches` and `ExtractAll` as they are.

## Adding checks a regex cannot express

Some rules cannot be written as a regex: a Luhn checksum, an IBAN's mod-97 check digits, or whether February 30 exists. Add them with `Must`, which returns a `ConstrainedPattern`. The `Validators` class has ready-made checks for the common patterns (see [Semantic Rules](./semantic-rules.md)).

```csharp
ConstrainedPattern card = CommonPatterns.CreditCard(DigitMode.Ascii).Must(Validators.Luhn, "Luhn checksum");
Console.WriteLine(card.IsValid("4111 1111 1111 1111"));   // True
Console.WriteLine(card.IsValid("4111 1111 1111 1112"));   // False

ConstrainedPattern iban = CommonPatterns.Iban(DigitMode.Ascii).Must(Validators.IbanChecksum, "IBAN checksum");
Console.WriteLine(iban.IsValid("GB82 WEST 1234 5698 7654 32"));   // True
Console.WriteLine(iban.IsValid("GB83 WEST 1234 5698 7654 32"));   // False

ConstrainedPattern isoDate = CommonPatterns.DateIso(DigitMode.Ascii).Must(Validators.IsoDate, "real calendar date");
Console.WriteLine(isoDate.IsValid("2024-02-29"));     // True
Console.WriteLine(isoDate.IsValid("2026-02-30"));     // False
Console.WriteLine(isoDate.IsValid("2024-13-01"));     // False
Console.WriteLine(isoDate.IsValid("2024-02-29\n"));   // False

ConstraintResult result = isoDate.Check("2026-02-30");
Console.WriteLine(result.IsValid);      // False
Console.WriteLine(result.FailedRule);   // real calendar date
```

| Validator | Pair it with | Checks |
|-----------|--------------|--------|
| `Validators.Luhn` | `CreditCard()` | The Luhn (mod 10) check digit. Spaces and dashes are ignored. |
| `Validators.IbanChecksum` | `Iban()` | The ISO 13616 mod-97 check digits. Spaces are ignored. |
| `Validators.IsoDate` | `DateIso()` | That the value is exactly `yyyy-MM-dd` and a real calendar date, so 2024-02-29 passes and 2026-02-30 fails. |
| `Validators.IPv4` | `IPv4()` | That every octet is 0-255, with one to three digits. |

- `IsValid` requires the match to cover the whole input, so the trailing newline that `IsMatch` lets through is rejected.
- `Check` explains a failure: the description of the first failed rule, or a trace of where the pattern stopped matching.
- Pair the validators with `DigitMode.Ascii`. `Validators.Luhn` rejects any character that is not an ASCII digit, space or dash, so a Unicode-digit card number would match the pattern and then fail the rule anyway.

## Safety and test data

Every common pattern passes the library's ReDoS analyzer (`AnalyzeSafety().IsSafe` is `true`) in both digit modes and at every strictness, and the test data generator can produce matching samples for each one. That makes them a safe starting point for input from users.

```csharp
Pattern ip = CommonPatterns.IPv4(DigitMode.Ascii);

Console.WriteLine(ip.AnalyzeSafety().IsSafe);                              // True
Console.WriteLine(string.Join(", ", ip.GenerateMatching(3, seed: 42)));   // 127.253.251.243, 254.11.253.8, 250.70.207.202
```

- `AnalyzeSafety()` reports catastrophic backtracking risks (see [ReDoS Safety](./redos-safety.md)).
- `GenerateMatching` and `GenerateNonMatching` produce test inputs. Pass a seed to get the same values on every run (see [Test Data Generation](./test-data-generation.md)).
- `Url()` no longer allows dots in its first host label. In 2.x that let a hostile input trigger polynomial backtracking (see [Upgrading from 2.x](./upgrading-from-2x.md)).

## Known limitations

Common patterns are designed for common cases. Check this table before you rely on one for a format with stricter rules.

| Method | Does not |
|--------|----------|
| `Email()` | Accept `+` or other special characters in the local part; reject consecutive dots. |
| `Url()` | Accept ports, `localhost`, other schemes, or a query without a path. |
| `PhoneNumber()` | Match the usual US formats in 3.1.0 (see [Contact and web](#contact-and-web)). |
| `PhoneNumberE164()` | Accept spaces, dashes or brackets. |
| `IPv4()` | Reject leading zeros; accept CIDR suffixes. |
| `IPv6()` | Accept the compressed `::` form, embedded IPv4 or zone indexes. |
| `Guid()` | Accept braces or the form without hyphens. |
| `Jwt()` | Verify the signature or decode the payload. |
| `Base64()` | Accept whitespace, line breaks or the base64url alphabet. |
| `DateIso()` | Check that the month is 01-12 or the day 01-31. |
| `Date()` | Check values, accept `.` as a separator, or resolve day/month order. |
| `DateTimeIso()` | Check the number of days in a month, or accept a date without a time. |
| `Time24()` | Accept a one-digit hour. |
| `Time12()` | Limit the hour to 1-12, or accept mixed-case `Am`/`Pm`. |
| `CreditCard()` | Accept 13-, 15- or 19-digit cards, or check the Luhn digit. |
| `Iban()` | Check the mod-97 digits or the country-specific length; accept lowercase. |
| `SocialSecurityNumber()` | Accept the form without hyphens, or reject numbers never issued. |
| `ZipCode()` | Accept postal codes of other countries. |
| `StrongPassword()` | Count characters outside `!@#$%^&*()_+-=` as special, or limit length. |
| `FilePath()` | Accept relative paths, UNC paths, or `/` in Windows paths. |
| `Integer()`, `Decimal()`, `ScientificNotation()` | Accept a leading `+`, thousands separators or a comma decimal separator. |
| `HtmlTag()` | Accept unquoted or value-less attributes, or validate document structure. |

## Member reference

`CommonPatterns` is a static class with 30 patterns. Each comes in two forms, and `Email` and `Url` have two more:

| Form | Example | Description |
|------|---------|-------------|
| `X()` | `CommonPatterns.IPv4()` | The pattern with Unicode digits. |
| `X(DigitMode digits)` | `CommonPatterns.IPv4(DigitMode.Ascii)` | The same pattern with the chosen meaning of `\d`. Throws `ArgumentOutOfRangeException` for an undefined mode. |
| `Email(PatternStrictness strictness)`, `Url(PatternStrictness strictness)` | `CommonPatterns.Url(PatternStrictness.Strict)` | The pattern at the chosen strictness. An undefined value means `Standard`. |
| `Email(PatternStrictness strictness, DigitMode digits)`, `Url(PatternStrictness strictness, DigitMode digits)` | `CommonPatterns.Email(PatternStrictness.Strict, DigitMode.Ascii)` | Both choices at once. |

The 30 patterns, by section on this page:

- [Contact and web](#contact-and-web): `Email`, `Url`, `PhoneNumber`, `PhoneNumberE164`
- [Network and identifiers](#network-and-identifiers): `IPv4`, `IPv6`, `MacAddress`, `Guid`, `Jwt`, `Base64`
- [Date and time](#date-and-time): `DateIso`, `Date`, `DateTimeIso`, `Time24`, `Time12`
- [Financial and identity](#financial-and-identity): `CreditCard`, `Iban`, `SocialSecurityNumber`, `ZipCode`
- [User input and text](#user-input-and-text): `Username`, `StrongPassword`, `Slug`, `HexColor`, `FilePath`, `SemVer`
- [Numbers](#numbers): `Integer`, `Decimal`, `ScientificNotation`
- [Markup](#markup): `HtmlTag`, `MarkdownLink`

Related types:

| Type | Kind | Members |
|------|------|---------|
| `PatternStrictness` | enum | `Loose`, `Standard`, `Strict` |
| `DigitMode` | enum | `Unicode`, `Ascii` |
| `Pattern.WithAsciiDigits()` | method | Rewrites `\d` and `\D` to ASCII. |
| `Pattern.WithDigits(DigitMode)` | method | Applies a `DigitMode` to any pattern. |
