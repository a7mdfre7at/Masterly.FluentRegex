# Masterly.FluentRegex

A fluent, readable API for building regular expression patterns in .NET

<img src="https://raw.githubusercontent.com/a7mdfre7at/Masterly.FluentRegex/master/repo_image.png" width="200" height="180">

[![Nuget](https://img.shields.io/nuget/v/Masterly.FluentRegex?style=flat-square)](https://www.nuget.org/packages/Masterly.FluentRegex) ![Nuget](https://img.shields.io/nuget/dt/Masterly.FluentRegex?label=nuget%20downloads&style=flat-square) ![GitHub last commit](https://img.shields.io/github/last-commit/a7mdfre7at/Masterly.FluentRegex?style=flat-square) ![GitHub](https://img.shields.io/github/license/a7mdfre7at/Masterly.FluentRegex?style=flat-square) [![Build](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/build.yml/badge.svg)](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/build.yml) [![CodeQL Analysis](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/codeql-analysis.yml) [![Publish to NuGet](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/publish.yml/badge.svg)](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/publish.yml)

## Give a Star! :star:

If you like or are using this project please give it a star. Thanks!

## Table of Contents

- [Introduction](#introduction)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Core Features](#core-features)
  - [Basic Patterns](#basic-patterns)
  - [Character Classes](#character-classes)
  - [Quantifiers (Repeat)](#quantifiers-repeat)
  - [Groups](#groups)
  - [Lookahead and Lookbehind](#lookahead-and-lookbehind)
  - [Backreferences](#backreferences)
- [Matching API](#matching-api)
- [Common Patterns Library](#common-patterns-library)
- [Validation and Debugging](#validation-and-debugging)
- [Complete Examples](#complete-examples)
- [API Reference](#api-reference)
- [License](#license)

## Introduction

> Some people, when confronted with a problem, think "I know, I'll use regular expressions." Now they have two problems. *- Jamie Zawinski*

Regular expressions are powerful but notoriously difficult to read and maintain. Consider this email validation regex:

```csharp
(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*
  |  "(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21\x23-\x5b\x5d-\x7f]
      |  \\[\x01-\x09\x0b\x0c\x0e-\x7f])*")
@ (?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?
  |  \[(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}
       (?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?|[a-z0-9-]*[a-z0-9]:
          (?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21-\x5a\x53-\x7f]
          |  \\[\x01-\x09\x0b\x0c\x0e-\x7f])+)
     \])
```

**Masterly.FluentRegex** transforms regex creation into readable, maintainable code using a fluent builder pattern.

## Installation

### Package Manager Console
```
Install-Package Masterly.FluentRegex
```

### .NET CLI
```
dotnet add package Masterly.FluentRegex
```

### PackageReference
```xml
<PackageReference Include="Masterly.FluentRegex" Version="x.x.x" />
```

## Quick Start

```csharp
using Masterly.FluentRegex;

// Build a pattern
Pattern pattern = Pattern.With
    .StartOfLine
    .Digit.Repeat.OneOrMore
    .Literal("-")
    .Word.Repeat.Times(3, 5)
    .EndOfLine;

// Use it directly
bool isMatch = pattern.IsMatch("123-abc");  // true
string result = pattern.Replace("123-abc", "X");  // "X"

// Or convert to Regex
Regex regex = pattern.ToRegex(RegexOptions.IgnoreCase);
```

## Core Features

### Basic Patterns

| Method | Regex | Description |
|--------|-------|-------------|
| `.StartOfLine` | `^` | Start of line/string anchor |
| `.EndOfLine` | `$` | End of line/string anchor |
| `.Anything` | `.` | Any character except newline |
| `.Literal("text")` | `text` | Literal text (auto-escaped) |
| `.Digit` | `\d` | Digit [0-9] |
| `.NonDigit` | `\D` | Non-digit |
| `.Word` | `\w` | Word character [a-zA-Z0-9_] |
| `.NonWord` | `\W` | Non-word character |
| `.Whitespace` | `\s` | Whitespace character |
| `.NonWhitespace` | `\S` | Non-whitespace |
| `.WordBoundary` | `\b` | Word boundary |
| `.Letter` | `a-zA-Z` | Any letter (use inside Set) |
| `.LowercaseLetter` | `a-z` | Lowercase letter |
| `.UppercaseLetter` | `A-Z` | Uppercase letter |
| `.Tab` | `\t` | Tab character |
| `.Newline` | `\n` | Newline character |
| `.CarriageReturn` | `\r` | Carriage return |

**Example:**
```csharp
// Match a word followed by digits
Pattern pattern = Pattern.With
    .Word.Repeat.OneOrMore
    .Whitespace
    .Digit.Repeat.OneOrMore;
// Produces: \w+\s\d+
```

### Character Classes

| Method | Regex | Description |
|--------|-------|-------------|
| `.Set(pattern)` | `[...]` | Character class (match any) |
| `.NegatedSet(pattern)` | `[^...]` | Negated class (match none) |
| `.Choice(p1, p2, ...)` | `(p1\|p2\|...)` | Alternation (OR) |

**Example:**
```csharp
// Match hex characters
Pattern hex = Pattern.With.Set(Pattern.With.Digit.Literal("a-fA-F"));
// Produces: [\da-fA-F]

// Match vowels only
Pattern vowels = Pattern.With.Set(Pattern.With.Literal("aeiouAEIOU"));
// Produces: [aeiouAEIOU]

// Match anything except digits
Pattern nonDigit = Pattern.With.NegatedSet(Pattern.With.Digit);
// Produces: [^\d]

// Match different formats
Pattern format = Pattern.With.Choice(
    Pattern.With.Literal("yes"),
    Pattern.With.Literal("no"),
    Pattern.With.Literal("maybe")
);
// Produces: (yes|no|maybe)
```

### Quantifiers (Repeat)

| Method | Regex | Description |
|--------|-------|-------------|
| `.Repeat.OneOrMore` | `+` | One or more |
| `.Repeat.ZeroOrMore` | `*` | Zero or more |
| `.Repeat.Optional` | `?` | Zero or one |
| `.Repeat.Times(n)` | `{n}` | Exactly n times |
| `.Repeat.Times(min, max)` | `{min,max}` | Between min and max |
| `.Repeat.AtLeast(n)` | `{n,}` | At least n times |
| `.Repeat.AtMost(n)` | `{,n}` | At most n times |
| `.Repeat.Lazy` | `?` suffix | Lazy (non-greedy) matching |

**Example:**
```csharp
// Phone number: 3 digits, dash, 3 digits, dash, 4 digits
Pattern phone = Pattern.With
    .Digit.Repeat.Times(3)
    .Literal("-")
    .Digit.Repeat.Times(3)
    .Literal("-")
    .Digit.Repeat.Times(4);
// Produces: \d{3}-\d{3}-\d{4}

// Optional area code
Pattern optionalArea = Pattern.With
    .Group(Pattern.With.Digit.Repeat.Times(3)).Repeat.Optional
    .Digit.Repeat.Times(7);
// Produces: (\d{3})?\d{7}

// Lazy matching
Pattern lazy = Pattern.With.Anything.Repeat.ZeroOrMore.Lazy;
// Produces: .*?
```

### Groups

| Method | Regex | Description |
|--------|-------|-------------|
| `.Group(pattern)` | `(...)` | Capturing group |
| `.NamedGroup("name", pattern)` | `(?<name>...)` | Named capturing group |
| `.NonCapturingGroup(pattern)` | `(?:...)` | Non-capturing group |
| `.AtomicGroup(pattern)` | `(?>...)` | Atomic (non-backtracking) group |

**Example:**
```csharp
// Named groups for phone number parts
Pattern phone = Pattern.With
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .Literal("-")
    .NamedGroup("exchange", Pattern.With.Digit.Repeat.Times(3))
    .Literal("-")
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4));
// Produces: (?<area>\d{3})-(?<exchange>\d{3})-(?<number>\d{4})

// Extract named groups from match
Match match = phone.Match("555-123-4567");
string area = match.Groups["area"].Value;      // "555"
string exchange = match.Groups["exchange"].Value; // "123"
string number = match.Groups["number"].Value;    // "4567"

// Non-capturing group for repeated patterns
Pattern repeatedWord = Pattern.With
    .NonCapturingGroup(Pattern.With.Word.Repeat.OneOrMore.Whitespace).Repeat.OneOrMore;
// Produces: (?:\w+\s)+
```

### Lookahead and Lookbehind

Zero-width assertions that match positions without consuming characters.

| Method | Regex | Description |
|--------|-------|-------------|
| `.PositiveLookahead(pattern)` | `(?=...)` | Must be followed by pattern |
| `.NegativeLookahead(pattern)` | `(?!...)` | Must NOT be followed by pattern |
| `.PositiveLookbehind(pattern)` | `(?<=...)` | Must be preceded by pattern |
| `.NegativeLookbehind(pattern)` | `(?<!...)` | Must NOT be preceded by pattern |

**Example:**
```csharp
// Match 'q' only if followed by 'u'
Pattern qFollowedByU = Pattern.With
    .Literal("q")
    .PositiveLookahead(Pattern.With.Literal("u"));
// Produces: q(?=u)
// Matches "q" in "queen", but not in "iraq"

// Match digits preceded by '$' (extract price without $)
Pattern price = Pattern.With
    .PositiveLookbehind(Pattern.With.Literal("$"))
    .Digit.Repeat.OneOrMore;
// Produces: (?<=\$)\d+
// In "$100", matches "100" (not "$100")

// Password validation using multiple lookaheads
Pattern strongPassword = Pattern.With
    .StartOfLine
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.LowercaseLetter))
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.UppercaseLetter))
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Digit)
    .Anything.Repeat.AtLeast(8)
    .EndOfLine;
// Requires: lowercase, uppercase, digit, 8+ chars
```

### Backreferences

Reference previously captured groups within the same pattern.

| Method | Regex | Description |
|--------|-------|-------------|
| `.Backreference("name")` | `\k<name>` | Reference named group |
| `.Backreference(n)` | `\n` | Reference numbered group |

**Example:**
```csharp
// Find duplicate words
Pattern duplicateWords = Pattern.With
    .WordBoundary
    .NamedGroup("word", Pattern.With.Word.Repeat.OneOrMore)
    .Whitespace.Repeat.OneOrMore
    .Backreference("word")
    .WordBoundary;
// Produces: \b(?<word>\w+)\s+\k<word>\b
// Matches "the the", "hello hello", etc.

duplicateWords.IsMatch("the the");     // true
duplicateWords.IsMatch("hello world"); // false
```

### Conditional Patterns

Match different patterns based on whether a group was captured.

```csharp
// Match optional opening paren, content, then closing paren only if opening exists
Pattern conditional = Pattern.With
    .Group(Pattern.With.Literal("(")).Repeat.Optional
    .Word.Repeat.OneOrMore
    .Conditional(1, Pattern.With.Literal(")"), Pattern.With.RegEx(""));
// Produces: (\()?\w+(?(1)\)|)
```

## Matching API

Execute patterns directly without converting to `Regex` first.

### Basic Matching

```csharp
Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;

// Test if pattern matches
bool isMatch = pattern.IsMatch("abc123def");  // true

// Find first match
Match match = pattern.Match("abc123def456");
// match.Value == "123", match.Index == 3

// Find all matches
MatchCollection matches = pattern.Matches("abc123def456ghi789");
// matches[0].Value == "123"
// matches[1].Value == "456"
// matches[2].Value == "789"
```

### Replace Operations

```csharp
Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;

// Simple replacement
string result = pattern.Replace("a1b2c3", "X");
// Result: "aXbXcX"

// Limited replacement
string result = pattern.Replace("a1b2c3d4", "X", 2);
// Result: "aXbXc3d4"

// Dynamic replacement with evaluator
string result = pattern.Replace("a1b2c3", m => (int.Parse(m.Value) * 2).ToString());
// Result: "a2b4c6"

// Using group references
Pattern namedPattern = Pattern.With
    .NamedGroup("num", Pattern.With.Digit.Repeat.OneOrMore);
string result = namedPattern.Replace("Value: 123", "[${num}]");
// Result: "Value: [123]"
```

### Split Operations

```csharp
// Split on comma
Pattern comma = Pattern.With.Literal(",");
string[] parts = comma.Split("a,b,c");
// parts: ["a", "b", "c"]

// Split on whitespace
Pattern whitespace = Pattern.With.Whitespace.Repeat.OneOrMore;
string[] words = whitespace.Split("hello   world\ttest");
// words: ["hello", "world", "test"]

// Limited splits
string[] parts = comma.Split("a,b,c,d,e", 3);
// parts: ["a", "b", "c,d,e"]
```

### Regex Options

```csharp
// Set default options for pattern
Pattern pattern = Pattern.With
    .Literal("hello")
    .WithOptions(RegexOptions.IgnoreCase);

pattern.IsMatch("HELLO");  // true
pattern.IsMatch("hello");  // true

// Or specify options per operation
pattern.IsMatch("HELLO", RegexOptions.IgnoreCase);

// Get compiled Regex with options
Regex regex = pattern.ToRegex(RegexOptions.IgnoreCase | RegexOptions.Multiline);
```

## Common Patterns Library

Pre-built patterns for common validation scenarios.

```csharp
using Masterly.FluentRegex;

// Email validation
CommonPatterns.Email().IsMatch("user@example.com");  // true

// URL validation (HTTP/HTTPS)
CommonPatterns.Url().IsMatch("https://example.com/path");  // true

// Phone numbers (US formats)
CommonPatterns.PhoneNumber().IsMatch("(555) 123-4567");  // true
CommonPatterns.PhoneNumber().IsMatch("555-123-4567");    // true

// IP addresses
CommonPatterns.IPv4().IsMatch("192.168.1.1");      // true
CommonPatterns.IPv4().IsMatch("256.1.1.1");        // false (invalid)
CommonPatterns.IPv6().IsMatch("2001:0db8:85a3:0000:0000:8a2e:0370:7334");  // true

// GUID/UUID
CommonPatterns.Guid().IsMatch("550e8400-e29b-41d4-a716-446655440000");  // true

// Hex colors
CommonPatterns.HexColor().IsMatch("#FF5733");   // true
CommonPatterns.HexColor().IsMatch("#FFF");      // true (short form)

// Dates
CommonPatterns.DateIso().IsMatch("2024-01-15");   // true (YYYY-MM-DD)
CommonPatterns.Date().IsMatch("01/15/2024");     // true (various formats)

// Times
CommonPatterns.Time24().IsMatch("23:59:59");     // true
CommonPatterns.Time12().IsMatch("12:30 PM");     // true

// Credit cards (16 digits)
CommonPatterns.CreditCard().IsMatch("1234-5678-9012-3456");  // true

// Social Security Numbers
CommonPatterns.SocialSecurityNumber().IsMatch("123-45-6789");  // true

// ZIP codes
CommonPatterns.ZipCode().IsMatch("12345");       // true
CommonPatterns.ZipCode().IsMatch("12345-6789"); // true (ZIP+4)

// Usernames (alphanumeric, 3-20 chars)
CommonPatterns.Username().IsMatch("user_123");   // true

// Strong passwords
CommonPatterns.StrongPassword().IsMatch("Password1!");  // true
// Requires: lowercase, uppercase, digit, special char, 8+ chars

// File paths
CommonPatterns.FilePath().IsMatch("C:\\Users\\file.txt");  // true
CommonPatterns.FilePath().IsMatch("/home/user/file.txt"); // true

// HTML tags
CommonPatterns.HtmlTag().IsMatch("<div class=\"test\">");  // true

// Numbers
CommonPatterns.Integer().IsMatch("-123");           // true
CommonPatterns.Decimal().IsMatch("123.45");         // true
CommonPatterns.ScientificNotation().IsMatch("1.5e10");  // true
```

## Validation and Debugging

### Pattern Validation

```csharp
Pattern pattern = Pattern.With.Digit.Repeat.OneOrMore;

// Quick validation check
bool isValid = pattern.IsValid();  // true

// Validation with error details
Pattern invalid = Pattern.With.RegEx("[unclosed");
if (!invalid.TryValidate(out string errorMessage))
{
    Console.WriteLine($"Invalid pattern: {errorMessage}");
}
```

### Group Information

```csharp
Pattern pattern = Pattern.With
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .Group(Pattern.With.Digit.Repeat.Times(4))
    .NonCapturingGroup(Pattern.With.Whitespace);

// Get named group names
string[] names = pattern.GetGroupNames();
// names: ["area"]

// Get total capturing group count
int count = pattern.GetGroupCount();
// count: 2 (named + numbered, excludes non-capturing)
```

### Pattern Explanation

Generate human-readable explanations of patterns:

```csharp
Pattern pattern = Pattern.With
    .StartOfLine
    .NamedGroup("digits", Pattern.With.Digit.Repeat.OneOrMore)
    .Literal("-")
    .Word.Repeat.ZeroOrMore
    .EndOfLine;

Console.WriteLine(pattern.Explain());
```

**Output:**
```
Pattern: ^(?<digits>\d+)-\w*$

Explanation:
  - Start of line/string
  - Named capturing group 'digits'
    - Digit [0-9]
    - One or more
  - End of group
  - Escaped literal '-'
  - Word character [a-zA-Z0-9_]
  - Zero or more
  - End of line/string
```

## Complete Examples

### Email Validation

```csharp
// Simple email pattern
Pattern email = Pattern.With
    .StartOfLine
    .Set(Pattern.With.Letter.Digit.Literal("._-")).Repeat.OneOrMore
    .Literal("@")
    .Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore
    .Literal(".")
    .Set(Pattern.With.Letter).Repeat.AtLeast(2)
    .EndOfLine;

email.IsMatch("user@example.com");      // true
email.IsMatch("user.name@domain.org");  // true
email.IsMatch("invalid");               // false

// Or use the built-in pattern
CommonPatterns.Email().IsMatch("user@example.com");
```

### Phone Number with Named Groups

```csharp
Pattern phone = Pattern.With
    .StartOfLine
    .Literal("(").Repeat.Optional
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .Literal(")").Repeat.Optional
    .Set(Pattern.With.Whitespace.Literal("-")).Repeat.Optional
    .NamedGroup("exchange", Pattern.With.Digit.Repeat.Times(3))
    .Set(Pattern.With.Literal("-")).Repeat.Optional
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4))
    .EndOfLine;

Match match = phone.Match("(555) 123-4567");
if (match.Success)
{
    Console.WriteLine($"Area: {match.Groups["area"].Value}");
    Console.WriteLine($"Exchange: {match.Groups["exchange"].Value}");
    Console.WriteLine($"Number: {match.Groups["number"].Value}");
}
```

### URL Parser

```csharp
Pattern url = Pattern.With
    .StartOfLine
    .NamedGroup("protocol", Pattern.With.Choice(
        Pattern.With.Literal("https"),
        Pattern.With.Literal("http")
    ))
    .Literal("://")
    .NamedGroup("domain", Pattern.With.Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore)
    .NamedGroup("path", Pattern.With.Literal("/").Set(Pattern.With.Word.Literal("/-")).Repeat.ZeroOrMore).Repeat.Optional
    .EndOfLine;

Match match = url.Match("https://example.com/path/to/page");
Console.WriteLine($"Protocol: {match.Groups["protocol"].Value}");  // "https"
Console.WriteLine($"Domain: {match.Groups["domain"].Value}");      // "example.com"
Console.WriteLine($"Path: {match.Groups["path"].Value}");          // "/path/to/page"
```

### Log Parser

```csharp
Pattern logEntry = Pattern.With
    .StartOfLine
    .Literal("[")
    .NamedGroup("timestamp", Pattern.With.Digit.Repeat.Times(4).Literal("-").Digit.Repeat.Times(2).Literal("-").Digit.Repeat.Times(2))
    .Whitespace
    .NamedGroup("time", Pattern.With.Digit.Repeat.Times(2).Literal(":").Digit.Repeat.Times(2).Literal(":").Digit.Repeat.Times(2))
    .Literal("]")
    .Whitespace
    .Literal("[")
    .NamedGroup("level", Pattern.With.Word.Repeat.OneOrMore)
    .Literal("]")
    .Whitespace
    .NamedGroup("message", Pattern.With.Anything.Repeat.OneOrMore)
    .EndOfLine;

string log = "[2024-01-15 10:30:45] [ERROR] Database connection failed";
Match match = logEntry.Match(log);

Console.WriteLine($"Date: {match.Groups["timestamp"].Value}");  // "2024-01-15"
Console.WriteLine($"Time: {match.Groups["time"].Value}");       // "10:30:45"
Console.WriteLine($"Level: {match.Groups["level"].Value}");     // "ERROR"
Console.WriteLine($"Message: {match.Groups["message"].Value}"); // "Database connection failed"
```

### Data Transformation

```csharp
// Convert dates from MM/DD/YYYY to YYYY-MM-DD
Pattern datePattern = Pattern.With
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2))
    .Literal("/")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2))
    .Literal("/")
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4));

string input = "Born on 12/25/1990 and graduated on 05/15/2012";
string result = datePattern.Replace(input, "${year}-${month}-${day}");
// Result: "Born on 1990-12-25 and graduated on 2012-05-15"
```

## API Reference

### Pattern Class

| Property/Method | Description |
|----------------|-------------|
| `Pattern.With` | Static entry point for building patterns |
| `.ToString()` | Get the regex string |
| `.ToRegex(options)` | Compile to `System.Text.RegularExpressions.Regex` |
| `.WithOptions(options)` | Set default `RegexOptions` |
| `.IsMatch(input)` | Test if pattern matches |
| `.Match(input)` | Find first match |
| `.Matches(input)` | Find all matches |
| `.Replace(input, replacement)` | Replace matches |
| `.Split(input)` | Split string at matches |
| `.IsValid()` | Validate pattern syntax |
| `.TryValidate(out error)` | Validate with error message |
| `.GetGroupNames()` | Get named group names |
| `.GetGroupCount()` | Get capturing group count |
| `.Explain()` | Get human-readable explanation |

### CommonPatterns Class

| Method | Description |
|--------|-------------|
| `Email()` | Email address validation |
| `Url()` | HTTP/HTTPS URL validation |
| `PhoneNumber()` | US phone number formats |
| `IPv4()` | IPv4 address (0.0.0.0 - 255.255.255.255) |
| `IPv6()` | IPv6 address (full notation) |
| `Guid()` | GUID/UUID format |
| `HexColor()` | CSS hex color (#RGB, #RRGGBB, etc.) |
| `DateIso()` | ISO 8601 date (YYYY-MM-DD) |
| `Date()` | Common date formats |
| `Time24()` | 24-hour time (HH:MM:SS) |
| `Time12()` | 12-hour time with AM/PM |
| `CreditCard()` | 16-digit credit card |
| `SocialSecurityNumber()` | US SSN (XXX-XX-XXXX) |
| `ZipCode()` | US ZIP code (5 or 9 digit) |
| `Username()` | Alphanumeric username (3-20 chars) |
| `StrongPassword()` | Password with complexity requirements |
| `FilePath()` | Windows or Unix file paths |
| `HtmlTag()` | HTML/XML tags with attributes |
| `Integer()` | Signed integers |
| `Decimal()` | Decimal numbers |
| `ScientificNotation()` | Scientific notation (1.5e10) |

## License

MIT

**Free Software, Hell Yeah!**
