# Masterly.FluentRegex

A fluent, readable API for building regular expression patterns in .NET

<img src="https://raw.githubusercontent.com/a7mdfre7at/Masterly.FluentRegex/master/repo_image.png" width="200" height="180">

[![Nuget](https://img.shields.io/nuget/v/Masterly.FluentRegex?style=flat-square)](https://www.nuget.org/packages/Masterly.FluentRegex) ![Nuget](https://img.shields.io/nuget/dt/Masterly.FluentRegex?label=nuget%20downloads&style=flat-square) ![GitHub last commit](https://img.shields.io/github/last-commit/a7mdfre7at/Masterly.FluentRegex?style=flat-square) ![GitHub](https://img.shields.io/github/license/a7mdfre7at/Masterly.FluentRegex?style=flat-square) [![Build](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/build.yml/badge.svg)](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/build.yml) [![CodeQL Analysis](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/codeql-analysis.yml) [![Publish to NuGet](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/publish.yml/badge.svg)](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/publish.yml)

## Give a Star! :star:

If you like or are using this project please give it a star. Thanks!

## Table of Contents

- [Introduction](#introduction)
- [Why FluentRegex?](#why-fluentregex)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
  - [The Pattern Builder](#the-pattern-builder)
  - [Method Chaining](#method-chaining)
  - [Automatic Escaping](#automatic-escaping)
- [Basic Patterns](#basic-patterns)
  - [Anchors](#anchors)
  - [Character Matchers](#character-matchers)
  - [Literal Text](#literal-text)
  - [Raw Regex](#raw-regex)
- [Character Classes](#character-classes)
  - [Character Sets](#character-sets)
  - [Negated Sets](#negated-sets)
  - [Alternation (OR)](#alternation-or)
- [Quantifiers (Repeat)](#quantifiers-repeat)
  - [Basic Quantifiers](#basic-quantifiers)
  - [Bounded Quantifiers](#bounded-quantifiers)
  - [Lazy (Non-Greedy) Matching](#lazy-non-greedy-matching)
- [Groups](#groups)
  - [Capturing Groups](#capturing-groups)
  - [Named Groups](#named-groups)
  - [Non-Capturing Groups](#non-capturing-groups)
  - [Atomic Groups](#atomic-groups)
  - [Extracting Group Values](#extracting-group-values)
- [Assertions](#assertions)
  - [Lookahead](#lookahead)
  - [Lookbehind](#lookbehind)
  - [Practical Assertion Examples](#practical-assertion-examples)
- [Backreferences](#backreferences)
  - [Named Backreferences](#named-backreferences)
  - [Numbered Backreferences](#numbered-backreferences)
- [Conditional Patterns](#conditional-patterns)
- [Matching API](#matching-api)
  - [Testing Matches](#testing-matches)
  - [Finding Matches](#finding-matches)
  - [Replace Operations](#replace-operations)
  - [Split Operations](#split-operations)
  - [Regex Options](#regex-options)
- [Common Patterns Library](#common-patterns-library)
  - [Contact Information](#contact-information)
  - [Network and System](#network-and-system)
  - [Date and Time](#date-and-time)
  - [Financial and Identity](#financial-and-identity)
  - [User Input](#user-input)
  - [Numbers](#numbers)
  - [Markup](#markup)
- [Validation and Debugging](#validation-and-debugging)
  - [Pattern Validation](#pattern-validation)
  - [Group Information](#group-information)
  - [Pattern Explanation](#pattern-explanation)
- [Complete Examples](#complete-examples)
  - [Email Validation](#email-validation)
  - [Phone Number Parser](#phone-number-parser)
  - [URL Parser](#url-parser)
  - [Log File Parser](#log-file-parser)
  - [Data Transformation](#data-transformation)
  - [Password Validation](#password-validation)
  - [HTML Tag Extraction](#html-tag-extraction)
  - [CSV Line Parser](#csv-line-parser)
- [API Reference](#api-reference)
  - [Pattern Class](#pattern-class)
  - [Repeat Class](#repeat-class)
  - [CommonPatterns Class](#commonpatterns-class)
- [Best Practices](#best-practices)
- [Performance Considerations](#performance-considerations)
- [Migration Guide](#migration-guide)
- [Contributing](#contributing)
- [License](#license)

---

## Introduction

> Some people, when confronted with a problem, think "I know, I'll use regular expressions." Now they have two problems. *- Jamie Zawinski*

Regular expressions are incredibly powerful, but they're notorious for being difficult to read, write, and maintain. Consider this email validation regex:

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

Can you understand what this does at a glance? Neither can most developers.

**Masterly.FluentRegex** transforms regex creation into readable, maintainable, self-documenting code using a fluent builder pattern.

---

## Why FluentRegex?

### Traditional Regex vs FluentRegex

**Traditional approach:**
```csharp
var regex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*]).{8,}$");
```

**FluentRegex approach:**
```csharp
Pattern password = Pattern.With
    .StartOfLine
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.LowercaseLetter))
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.UppercaseLetter))
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Digit)
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.Literal("!@#$%^&*")))
    .Anything.Repeat.AtLeast(8)
    .EndOfLine;
```

### Key Benefits

| Feature | Benefit |
|---------|---------|
| **Self-Documenting** | Code reads like English - no regex knowledge needed to understand |
| **IntelliSense Support** | Full IDE autocompletion guides you through available options |
| **Compile-Time Safety** | Catch errors during development, not at runtime |
| **Maintainable** | Easy to modify patterns months later |
| **Testable** | Break complex patterns into smaller, testable pieces |
| **No Escaping Headaches** | Automatic escaping of special characters |

---

## Installation

### Package Manager Console
```
Install-Package Masterly.FluentRegex
```

### .NET CLI
```bash
dotnet add package Masterly.FluentRegex
```

### PackageReference
```xml
<PackageReference Include="Masterly.FluentRegex" Version="2.0.0" />
```

### Supported Frameworks
- .NET Standard 2.1+
- .NET Core 3.0+
- .NET 5.0+
- .NET 6.0+
- .NET 7.0+
- .NET 8.0+

---

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

// Use it directly - no need to convert to Regex first!
bool isMatch = pattern.IsMatch("123-abc");     // true
bool noMatch = pattern.IsMatch("abc-123");     // false

// Find matches
Match match = pattern.Match("123-test");
MatchCollection matches = pattern.Matches("123-abc 456-def");

// Replace content
string result = pattern.Replace("123-abc", "REPLACED");

// Get the underlying regex string
string regexString = pattern.ToString();  // ^\d+-\w{3,5}$

// Or convert to System.Text.RegularExpressions.Regex
Regex regex = pattern.ToRegex(RegexOptions.IgnoreCase);
```

---

## Core Concepts

### The Pattern Builder

Every pattern starts with `Pattern.With`, which creates a new pattern builder instance:

```csharp
Pattern pattern = Pattern.With
    .Digit
    .Literal("@")
    .Word;
// Result: \d@\w
```

### Method Chaining

FluentRegex uses the builder pattern, where each method returns a `Pattern` instance, enabling elegant method chaining:

```csharp
Pattern email = Pattern.With
    .StartOfLine                                              // ^
    .Set(Pattern.With.Letter.Digit.Literal("._-"))           // [a-zA-Z\d._-]
    .Repeat.OneOrMore                                         // +
    .Literal("@")                                             // @
    .Set(Pattern.With.Letter.Digit.Literal(".-"))            // [a-zA-Z\d.-]
    .Repeat.OneOrMore                                         // +
    .Literal(".")                                             // \.
    .Letter.Repeat.AtLeast(2)                                // [a-zA-Z]{2,}
    .EndOfLine;                                               // $
```

### Automatic Escaping

The `Literal()` method automatically escapes regex special characters:

```csharp
Pattern p1 = Pattern.With.Literal("$100.00");
// Result: \$100\.00  ($ and . are escaped)

Pattern p2 = Pattern.With.Literal("[test]");
// Result: \[test\]   (brackets are escaped)

Pattern p3 = Pattern.With.Literal("a+b*c?");
// Result: a\+b\*c\?  (quantifiers are escaped)
```

Use `RegEx()` when you need raw, unescaped regex:

```csharp
Pattern raw = Pattern.With.RegEx("[a-z]+");
// Result: [a-z]+  (no escaping)
```

---

## Basic Patterns

### Anchors

Anchors match positions, not characters:

| Method | Regex | Description |
|--------|-------|-------------|
| `.StartOfLine` | `^` | Start of line or string |
| `.EndOfLine` | `$` | End of line or string |
| `.WordBoundary` | `\b` | Position between word and non-word character |

```csharp
// Match lines that start with "Error"
Pattern errorLine = Pattern.With
    .StartOfLine
    .Literal("Error");
// Result: ^Error

// Match complete words only
Pattern wholeWord = Pattern.With
    .WordBoundary
    .Literal("test")
    .WordBoundary;
// Result: \btest\b

wholeWord.IsMatch("this is a test");     // true
wholeWord.IsMatch("testing");            // false (not a whole word)
```

### Character Matchers

Single character matchers:

| Method | Regex | Description |
|--------|-------|-------------|
| `.Digit` | `\d` | Any digit [0-9] |
| `.NonDigit` | `\D` | Any non-digit |
| `.Word` | `\w` | Word character [a-zA-Z0-9_] |
| `.NonWord` | `\W` | Non-word character |
| `.Whitespace` | `\s` | Whitespace (space, tab, newline, etc.) |
| `.NonWhitespace` | `\S` | Non-whitespace |
| `.Letter` | `a-zA-Z` | Any letter (use in character sets) |
| `.LowercaseLetter` | `a-z` | Lowercase letter |
| `.UppercaseLetter` | `A-Z` | Uppercase letter |
| `.Anything` | `.` | Any character except newline |
| `.Tab` | `\t` | Tab character |
| `.Newline` | `\n` | Newline character |
| `.CarriageReturn` | `\r` | Carriage return |

```csharp
// Match a word followed by digits
Pattern wordDigits = Pattern.With
    .Word.Repeat.OneOrMore
    .Whitespace
    .Digit.Repeat.OneOrMore;
// Result: \w+\s\d+

wordDigits.IsMatch("item 123");   // true
wordDigits.IsMatch("item123");    // false (no whitespace)
```

### Literal Text

Match exact text with automatic escaping:

```csharp
// Single characters
Pattern p1 = Pattern.With.Literal('$');
// Result: \$

// Multiple characters
Pattern p2 = Pattern.With.Literal('a', 'b', 'c');
// Result: abc

// Strings
Pattern p3 = Pattern.With.Literal("Hello, World!");
// Result: Hello, World!

// Special characters are auto-escaped
Pattern p4 = Pattern.With.Literal("(test)");
// Result: \(test\)
```

### Raw Regex

When you need unescaped regex syntax:

```csharp
// Insert raw regex
Pattern raw = Pattern.With
    .StartOfLine
    .RegEx("[a-z]+")
    .RegEx("\\d{3}")
    .EndOfLine;
// Result: ^[a-z]+\d{3}$
```

---

## Character Classes

### Character Sets

Match one character from a set:

```csharp
// Match any vowel
Pattern vowels = Pattern.With.Set(Pattern.With.Literal("aeiouAEIOU"));
// Result: [aeiouAEIOU]

// Match hex digit
Pattern hex = Pattern.With.Set(Pattern.With.Digit.Literal("a-fA-F"));
// Result: [\da-fA-F]

// Match alphanumeric
Pattern alphanumeric = Pattern.With.Set(Pattern.With.Letter.Digit);
// Result: [a-zA-Z\d]

// Combined sets
Pattern identifier = Pattern.With
    .Set(Pattern.With.Letter.Literal("_"))
    .Set(Pattern.With.Letter.Digit.Literal("_")).Repeat.ZeroOrMore;
// Result: [a-zA-Z_][a-zA-Z\d_]*
```

### Negated Sets

Match any character NOT in the set:

```csharp
// Match any non-digit
Pattern nonDigit = Pattern.With.NegatedSet(Pattern.With.Digit);
// Result: [^\d]

// Match any character except vowels
Pattern consonants = Pattern.With.NegatedSet(Pattern.With.Literal("aeiouAEIOU"));
// Result: [^aeiouAEIOU]

// Match non-whitespace, non-punctuation
Pattern simple = Pattern.With.NegatedSet(Pattern.With.Whitespace.Literal(".,!?"));
// Result: [^\s.,!?]
```

### Alternation (OR)

Match one of several alternatives:

```csharp
// Match yes, no, or maybe
Pattern response = Pattern.With.Choice(
    Pattern.With.Literal("yes"),
    Pattern.With.Literal("no"),
    Pattern.With.Literal("maybe")
);
// Result: (yes|no|maybe)

// Match different file extensions
Pattern imageFile = Pattern.With
    .Word.Repeat.OneOrMore
    .Literal(".")
    .Choice(
        Pattern.With.Literal("jpg"),
        Pattern.With.Literal("png"),
        Pattern.With.Literal("gif"),
        Pattern.With.Literal("webp")
    );
// Result: \w+\.(jpg|png|gif|webp)

// Match different date separators
Pattern dateSeparator = Pattern.With.Choice(
    Pattern.With.Literal("-"),
    Pattern.With.Literal("/"),
    Pattern.With.Literal(".")
);
// Result: (-|/|\.)
```

---

## Quantifiers (Repeat)

### Basic Quantifiers

| Method | Regex | Description |
|--------|-------|-------------|
| `.Repeat.OneOrMore` | `+` | One or more times |
| `.Repeat.ZeroOrMore` | `*` | Zero or more times |
| `.Repeat.Optional` | `?` | Zero or one time (optional) |

```csharp
// Required digits
Pattern required = Pattern.With.Digit.Repeat.OneOrMore;
// Result: \d+

// Optional prefix
Pattern optionalPlus = Pattern.With.Literal("+").Repeat.Optional.Digit.Repeat.OneOrMore;
// Result: \+?\d+

// Zero or more spaces
Pattern flexibleSpaces = Pattern.With.Whitespace.Repeat.ZeroOrMore;
// Result: \s*
```

### Bounded Quantifiers

| Method | Regex | Description |
|--------|-------|-------------|
| `.Repeat.Times(n)` | `{n}` | Exactly n times |
| `.Repeat.Times(min, max)` | `{min,max}` | Between min and max times |
| `.Repeat.AtLeast(n)` | `{n,}` | At least n times |
| `.Repeat.AtMost(n)` | `{,n}` | At most n times |

```csharp
// Phone number: exactly 10 digits
Pattern phone = Pattern.With.Digit.Repeat.Times(10);
// Result: \d{10}

// ZIP code: 5 or 9 digits
Pattern zip = Pattern.With
    .Digit.Repeat.Times(5)
    .Group(Pattern.With.Literal("-").Digit.Repeat.Times(4)).Repeat.Optional;
// Result: \d{5}(-\d{4})?

// Password: 8-20 characters
Pattern password = Pattern.With.Word.Repeat.Times(8, 20);
// Result: \w{8,20}

// At least 3 words
Pattern sentence = Pattern.With
    .Word.Repeat.OneOrMore
    .NonCapturingGroup(Pattern.With.Whitespace.Repeat.OneOrMore.Word.Repeat.OneOrMore)
    .Repeat.AtLeast(2);
// Result: \w+(?:\s+\w+){2,}
```

### Lazy (Non-Greedy) Matching

By default, quantifiers are greedy (match as much as possible). Use `.Lazy` for non-greedy matching:

```csharp
// Greedy: matches entire string between first < and last >
Pattern greedy = Pattern.With.Literal("<").Anything.Repeat.OneOrMore.Literal(">");
// Result: <.+>
// Input: "<a>text<b>" matches "<a>text<b>"

// Lazy: matches smallest possible string
Pattern lazy = Pattern.With.Literal("<").Anything.Repeat.OneOrMore.Lazy.Literal(">");
// Result: <.+?>
// Input: "<a>text<b>" matches "<a>" and "<b>" separately

// Lazy zero or more
Pattern lazyZeroOrMore = Pattern.With.Anything.Repeat.ZeroOrMore.Lazy;
// Result: .*?

// Lazy with quantifiers
Pattern lazyBounded = Pattern.With.Digit.Repeat.Times(1, 5).Lazy;
// Result: \d{1,5}?
```

---

## Groups

### Capturing Groups

Capture matched content for extraction or backreference:

```csharp
// Simple capturing group
Pattern grouped = Pattern.With
    .Group(Pattern.With.Digit.Repeat.OneOrMore)
    .Literal("-")
    .Group(Pattern.With.Word.Repeat.OneOrMore);
// Result: (\d+)-(\w+)

Match match = grouped.Match("123-abc");
Console.WriteLine(match.Groups[1].Value);  // "123"
Console.WriteLine(match.Groups[2].Value);  // "abc"
```

### Named Groups

Use meaningful names instead of numbers:

```csharp
// Named capturing groups
Pattern phone = Pattern.With
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .Literal("-")
    .NamedGroup("exchange", Pattern.With.Digit.Repeat.Times(3))
    .Literal("-")
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4));
// Result: (?<area>\d{3})-(?<exchange>\d{3})-(?<number>\d{4})

Match match = phone.Match("555-123-4567");
Console.WriteLine(match.Groups["area"].Value);      // "555"
Console.WriteLine(match.Groups["exchange"].Value);  // "123"
Console.WriteLine(match.Groups["number"].Value);    // "4567"
```

### Non-Capturing Groups

Group patterns without creating a capture:

```csharp
// Non-capturing group for repetition
Pattern words = Pattern.With
    .NonCapturingGroup(Pattern.With.Word.Repeat.OneOrMore.Whitespace).Repeat.OneOrMore
    .Word.Repeat.OneOrMore;
// Result: (?:\w+\s)+\w+

// Doesn't pollute match.Groups
Pattern protocol = Pattern.With
    .NonCapturingGroup(Pattern.With.Choice(
        Pattern.With.Literal("http"),
        Pattern.With.Literal("https")
    ))
    .Literal("://");
// Result: (?:http|https)://
```

### Atomic Groups

Prevent backtracking for better performance:

```csharp
// Atomic group - once matched, won't backtrack
Pattern atomic = Pattern.With
    .AtomicGroup(Pattern.With.Digit.Repeat.OneOrMore)
    .Word;
// Result: (?>\d+)\w

// Useful for avoiding catastrophic backtracking
Pattern optimized = Pattern.With
    .AtomicGroup(Pattern.With.Word.Repeat.OneOrMore);
// Result: (?>\w+)
```

### Extracting Group Values

```csharp
Pattern datePattern = Pattern.With
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
    .Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2))
    .Literal("-")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2));

// Single match
Match match = datePattern.Match("Date: 2024-01-15");
if (match.Success)
{
    Console.WriteLine($"Year: {match.Groups["year"].Value}");
    Console.WriteLine($"Month: {match.Groups["month"].Value}");
    Console.WriteLine($"Day: {match.Groups["day"].Value}");
}

// Multiple matches
MatchCollection matches = datePattern.Matches("Events: 2024-01-15, 2024-02-20, 2024-03-25");
foreach (Match m in matches)
{
    Console.WriteLine($"{m.Groups["year"].Value}/{m.Groups["month"].Value}/{m.Groups["day"].Value}");
}
// Output: 2024/01/15, 2024/02/20, 2024/03/25
```

---

## Assertions

Zero-width assertions match positions without consuming characters.

### Lookahead

| Method | Regex | Description |
|--------|-------|-------------|
| `.PositiveLookahead(pattern)` | `(?=...)` | Must be followed by pattern |
| `.NegativeLookahead(pattern)` | `(?!...)` | Must NOT be followed by pattern |

```csharp
// Match 'q' only if followed by 'u'
Pattern qWithU = Pattern.With
    .Literal("q")
    .PositiveLookahead(Pattern.With.Literal("u"));
// Result: q(?=u)

qWithU.IsMatch("queen");  // true (matches 'q')
qWithU.IsMatch("iraq");   // false

// Match 'q' only if NOT followed by 'u'
Pattern qWithoutU = Pattern.With
    .Literal("q")
    .NegativeLookahead(Pattern.With.Literal("u"));
// Result: q(?!u)

qWithoutU.IsMatch("iraq");   // true
qWithoutU.IsMatch("queen");  // false
```

### Lookbehind

| Method | Regex | Description |
|--------|-------|-------------|
| `.PositiveLookbehind(pattern)` | `(?<=...)` | Must be preceded by pattern |
| `.NegativeLookbehind(pattern)` | `(?<!...)` | Must NOT be preceded by pattern |

```csharp
// Match digits preceded by '$'
Pattern price = Pattern.With
    .PositiveLookbehind(Pattern.With.Literal("$"))
    .Digit.Repeat.OneOrMore;
// Result: (?<=\$)\d+

Match match = price.Match("Price: $100");
Console.WriteLine(match.Value);  // "100" (not "$100")

// Match digits NOT preceded by '$'
Pattern nonPrice = Pattern.With
    .NegativeLookbehind(Pattern.With.Literal("$"))
    .Digit.Repeat.OneOrMore;
// Result: (?<!\$)\d+

nonPrice.IsMatch("$100");     // false
nonPrice.IsMatch("Item 100"); // true
```

### Practical Assertion Examples

```csharp
// Password must contain lowercase, uppercase, digit, and be 8+ chars
Pattern strongPassword = Pattern.With
    .StartOfLine
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.LowercaseLetter))
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.UppercaseLetter))
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Digit)
    .Anything.Repeat.AtLeast(8)
    .EndOfLine;

strongPassword.IsMatch("Password1");    // true
strongPassword.IsMatch("password1");    // false (no uppercase)
strongPassword.IsMatch("PASSWORD1");    // false (no lowercase)
strongPassword.IsMatch("Password");     // false (no digit)
strongPassword.IsMatch("Pass1");        // false (less than 8 chars)

// Match word only if it's not part of a longer word
Pattern isolatedNumber = Pattern.With
    .NegativeLookbehind(Pattern.With.Digit)
    .Digit.Repeat.Times(3)
    .NegativeLookahead(Pattern.With.Digit);

isolatedNumber.IsMatch("123");      // true
isolatedNumber.IsMatch("1234");     // false
isolatedNumber.IsMatch("a123b");    // true (letters don't count)
```

---

## Backreferences

Reference previously captured groups within the same pattern.

### Named Backreferences

```csharp
// Find duplicate words
Pattern duplicateWords = Pattern.With
    .WordBoundary
    .NamedGroup("word", Pattern.With.Word.Repeat.OneOrMore)
    .Whitespace.Repeat.OneOrMore
    .Backreference("word")
    .WordBoundary;
// Result: \b(?<word>\w+)\s+\k<word>\b

duplicateWords.IsMatch("the the");        // true
duplicateWords.IsMatch("hello hello");    // true
duplicateWords.IsMatch("hello world");    // false

// Find matching quotes
Pattern quotedString = Pattern.With
    .NamedGroup("quote", Pattern.With.Set(Pattern.With.Literal("'\"")))
    .NegatedSet(Pattern.With.Literal("'\"")).Repeat.ZeroOrMore
    .Backreference("quote");
// Result: (?<quote>['"])[^'"]*\k<quote>

quotedString.IsMatch("'hello'");     // true
quotedString.IsMatch("\"world\"");   // true
quotedString.IsMatch("'mixed\"");    // false
```

### Numbered Backreferences

```csharp
// Reference groups by number (1-based)
Pattern htmlTag = Pattern.With
    .Literal("<")
    .Group(Pattern.With.Word.Repeat.OneOrMore)
    .Literal(">")
    .Anything.Repeat.ZeroOrMore.Lazy
    .Literal("</")
    .Backreference(1)
    .Literal(">");
// Result: <(\w+)>.*?</\1>

htmlTag.IsMatch("<div>content</div>");  // true
htmlTag.IsMatch("<div>content</span>"); // false (mismatched tags)
```

---

## Conditional Patterns

Match different patterns based on whether a group was captured.

```csharp
// Match optional opening paren with required closing paren only if opening exists
Pattern conditional = Pattern.With
    .Group(Pattern.With.Literal("(")).Repeat.Optional
    .Word.Repeat.OneOrMore
    .Conditional(1, Pattern.With.Literal(")"), Pattern.With.RegEx(""));
// Result: (\()?\\w+(?(1)\)|)

conditional.IsMatch("(test)");  // true
conditional.IsMatch("test");    // true
conditional.IsMatch("(test");   // false (opening without closing)
conditional.IsMatch("test)");   // false (closing without opening)

// Named conditional
Pattern namedConditional = Pattern.With
    .NamedGroup("open", Pattern.With.Literal("[")).Repeat.Optional
    .Digit.Repeat.OneOrMore
    .Conditional("open", Pattern.With.Literal("]"), Pattern.With.RegEx(""));
// Result: (?<open>\[)?\d+(?(open)\]|)
```

---

## Matching API

Execute patterns directly without converting to `Regex`.

### Testing Matches

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

// Basic test
bool hasDigits = digits.IsMatch("abc123def");  // true
bool noDigits = digits.IsMatch("no numbers");  // false

// With options
Pattern caseInsensitive = Pattern.With.Literal("hello");
caseInsensitive.IsMatch("HELLO", RegexOptions.IgnoreCase);  // true
```

### Finding Matches

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

// First match
Match first = digits.Match("abc123def456");
Console.WriteLine(first.Value);  // "123"
Console.WriteLine(first.Index);  // 3

// First match starting at position
Match fromPos = digits.Match("abc123def456", 5);
Console.WriteLine(fromPos.Value);  // "456"

// All matches
MatchCollection all = digits.Matches("a1b22c333d4444");
foreach (Match m in all)
{
    Console.WriteLine($"'{m.Value}' at index {m.Index}");
}
// Output:
// '1' at index 1
// '22' at index 3
// '333' at index 6
// '4444' at index 10
```

### Replace Operations

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

// Simple replacement
string result1 = digits.Replace("a1b2c3", "X");
// Result: "aXbXcX"

// Limited replacement
string result2 = digits.Replace("a1b2c3d4", "X", 2);
// Result: "aXbXc3d4"

// Dynamic replacement with evaluator
string result3 = digits.Replace("a1b2c3", m =>
    (int.Parse(m.Value) * 10).ToString());
// Result: "a10b20c30"

// Using group references in replacement
Pattern nameValue = Pattern.With
    .NamedGroup("name", Pattern.With.Word.Repeat.OneOrMore)
    .Literal("=")
    .NamedGroup("value", Pattern.With.Digit.Repeat.OneOrMore);

string result4 = nameValue.Replace("x=1 y=2 z=3", "${name}: ${value}");
// Result: "x: 1 y: 2 z: 3"

// Replacement pattern reference guide:
// $0 or $& - Entire match
// $1, $2   - Numbered groups
// ${name}  - Named groups
// $`       - Text before match
// $'       - Text after match
// $$       - Literal $
```

### Split Operations

```csharp
// Split on comma
Pattern comma = Pattern.With.Literal(",");
string[] parts = comma.Split("a,b,c,d");
// Result: ["a", "b", "c", "d"]

// Split on whitespace
Pattern whitespace = Pattern.With.Whitespace.Repeat.OneOrMore;
string[] words = whitespace.Split("hello   world\ttest");
// Result: ["hello", "world", "test"]

// Limited splits
Pattern delimiter = Pattern.With.Literal("-");
string[] limited = delimiter.Split("a-b-c-d-e", 3);
// Result: ["a", "b", "c-d-e"]
```

### Regex Options

```csharp
// Set default options
Pattern pattern = Pattern.With
    .Literal("hello")
    .WithOptions(RegexOptions.IgnoreCase);

pattern.IsMatch("HELLO");  // true
pattern.IsMatch("hello");  // true

// Options per operation
Pattern caseSensitive = Pattern.With.Literal("test");
caseSensitive.IsMatch("TEST", RegexOptions.IgnoreCase);  // true

// Combine options
Regex regex = pattern.ToRegex(
    RegexOptions.IgnoreCase |
    RegexOptions.Multiline |
    RegexOptions.Compiled
);

// Available RegexOptions:
// - IgnoreCase: Case-insensitive matching
// - Multiline: ^ and $ match line boundaries
// - Singleline: . matches newline characters
// - Compiled: Compile to IL for better performance
// - IgnorePatternWhitespace: Allow whitespace and comments in pattern
// - RightToLeft: Search from right to left
// - ECMAScript: ECMAScript-compliant behavior
// - CultureInvariant: Ignore cultural differences
```

---

## Common Patterns Library

Pre-built, tested patterns for common validation scenarios.

### Contact Information

```csharp
// Email addresses
CommonPatterns.Email().IsMatch("user@example.com");      // true
CommonPatterns.Email().IsMatch("user.name+tag@mail.co"); // true
CommonPatterns.Email().IsMatch("invalid");               // false

// URLs (HTTP/HTTPS)
CommonPatterns.Url().IsMatch("https://example.com");          // true
CommonPatterns.Url().IsMatch("http://sub.domain.org/path");   // true
CommonPatterns.Url().IsMatch("ftp://files.com");              // false (not http/https)

// Phone numbers (US formats)
CommonPatterns.PhoneNumber().IsMatch("555-123-4567");     // true
CommonPatterns.PhoneNumber().IsMatch("(555) 123-4567");   // true
CommonPatterns.PhoneNumber().IsMatch("5551234567");       // true
CommonPatterns.PhoneNumber().IsMatch("+1 555 123 4567");  // true
```

### Network and System

```csharp
// IPv4 addresses (validates 0-255 range)
CommonPatterns.IPv4().IsMatch("192.168.1.1");    // true
CommonPatterns.IPv4().IsMatch("10.0.0.255");     // true
CommonPatterns.IPv4().IsMatch("256.1.1.1");      // false (256 > 255)
CommonPatterns.IPv4().IsMatch("192.168.1");      // false (incomplete)

// IPv6 addresses (full notation)
CommonPatterns.IPv6().IsMatch("2001:0db8:85a3:0000:0000:8a2e:0370:7334");  // true

// GUIDs/UUIDs
CommonPatterns.Guid().IsMatch("550e8400-e29b-41d4-a716-446655440000");  // true
CommonPatterns.Guid().IsMatch("550E8400-E29B-41D4-A716-446655440000");  // true (case insensitive)

// File paths (Windows and Unix)
CommonPatterns.FilePath().IsMatch("C:\\Users\\file.txt");       // true
CommonPatterns.FilePath().IsMatch("/home/user/file.txt");       // true
CommonPatterns.FilePath().IsMatch("./relative/path/file.txt");  // true
```

### Date and Time

```csharp
// ISO 8601 dates (YYYY-MM-DD)
CommonPatterns.DateIso().IsMatch("2024-01-15");  // true
CommonPatterns.DateIso().IsMatch("2024-13-01");  // false (invalid month)

// Multiple date formats
CommonPatterns.Date().IsMatch("2024-01-15");    // true
CommonPatterns.Date().IsMatch("01/15/2024");    // true
CommonPatterns.Date().IsMatch("15.01.2024");    // true

// 24-hour time
CommonPatterns.Time24().IsMatch("23:59");       // true
CommonPatterns.Time24().IsMatch("23:59:59");    // true
CommonPatterns.Time24().IsMatch("24:00");       // false (invalid hour)

// 12-hour time with AM/PM
CommonPatterns.Time12().IsMatch("12:30 PM");    // true
CommonPatterns.Time12().IsMatch("12:30:45 AM"); // true
CommonPatterns.Time12().IsMatch("13:00 PM");    // false (invalid hour for 12-hour format)
```

### Financial and Identity

```csharp
// Credit card numbers (16 digits, optional separators)
CommonPatterns.CreditCard().IsMatch("1234567890123456");         // true
CommonPatterns.CreditCard().IsMatch("1234-5678-9012-3456");      // true
CommonPatterns.CreditCard().IsMatch("1234 5678 9012 3456");      // true

// Social Security Numbers (US)
CommonPatterns.SocialSecurityNumber().IsMatch("123-45-6789");    // true
CommonPatterns.SocialSecurityNumber().IsMatch("123456789");      // false (requires dashes)

// US ZIP codes
CommonPatterns.ZipCode().IsMatch("12345");       // true
CommonPatterns.ZipCode().IsMatch("12345-6789");  // true (ZIP+4)
```

### User Input

```csharp
// Usernames (3-20 alphanumeric chars with underscores)
CommonPatterns.Username().IsMatch("user_123");      // true
CommonPatterns.Username().IsMatch("ab");            // false (too short)
CommonPatterns.Username().IsMatch("user@name");     // false (invalid char)

// Strong passwords (8+ chars with lowercase, uppercase, digit, special)
CommonPatterns.StrongPassword().IsMatch("Password1!");   // true
CommonPatterns.StrongPassword().IsMatch("Password1");    // false (no special char)
CommonPatterns.StrongPassword().IsMatch("password1!");   // false (no uppercase)

// Hex colors
CommonPatterns.HexColor().IsMatch("#FFF");        // true (short form)
CommonPatterns.HexColor().IsMatch("#FF5733");     // true (6-digit)
CommonPatterns.HexColor().IsMatch("#FF5733AA");   // true (8-digit with alpha)
CommonPatterns.HexColor().IsMatch("FF5733");      // false (missing #)
```

### Numbers

```csharp
// Integers (positive and negative)
CommonPatterns.Integer().IsMatch("123");    // true
CommonPatterns.Integer().IsMatch("-456");   // true
CommonPatterns.Integer().IsMatch("+789");   // true
CommonPatterns.Integer().IsMatch("12.34");  // false (has decimal)

// Decimal numbers
CommonPatterns.Decimal().IsMatch("123.45");   // true
CommonPatterns.Decimal().IsMatch("-123.45");  // true
CommonPatterns.Decimal().IsMatch("123");      // true (integer is valid decimal)
CommonPatterns.Decimal().IsMatch(".45");      // false (requires integer part)

// Scientific notation
CommonPatterns.ScientificNotation().IsMatch("1.5e10");    // true
CommonPatterns.ScientificNotation().IsMatch("-2.5E-10");  // true
CommonPatterns.ScientificNotation().IsMatch("1e5");       // true
```

### Markup

```csharp
// HTML tags
CommonPatterns.HtmlTag().IsMatch("<div>");                        // true
CommonPatterns.HtmlTag().IsMatch("<div class=\"test\">");         // true
CommonPatterns.HtmlTag().IsMatch("</div>");                       // true
CommonPatterns.HtmlTag().IsMatch("<img src=\"image.png\" />");    // true
```

---

## Validation and Debugging

### Pattern Validation

```csharp
Pattern valid = Pattern.With.Digit.Repeat.OneOrMore;
Pattern invalid = Pattern.With.RegEx("[unclosed");

// Quick validation
bool isValid1 = valid.IsValid();    // true
bool isValid2 = invalid.IsValid();  // false

// Validation with error details
if (!invalid.TryValidate(out string errorMessage))
{
    Console.WriteLine($"Invalid pattern: {errorMessage}");
    // Output: Invalid pattern: parsing "[unclosed" - Unterminated [] set.
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
// count: 2 (named group + numbered group, excludes non-capturing)
```

### Pattern Explanation

Generate human-readable explanations:

```csharp
Pattern pattern = Pattern.With
    .StartOfLine
    .NamedGroup("digits", Pattern.With.Digit.Repeat.OneOrMore)
    .Literal("-")
    .Word.Repeat.ZeroOrMore.Lazy
    .EndOfLine;

Console.WriteLine(pattern.Explain());
```

**Output:**
```
Pattern: ^(?<digits>\d+)-\w*?$

Explanation:
  - Start of line/string
  - Named capturing group 'digits'
    - Digit [0-9]
    - One or more
  - End of group
  - Escaped literal '-'
  - Word character [a-zA-Z0-9_]
  - Zero or more (lazy)
  - End of line/string
```

---

## Complete Examples

### Email Validation

```csharp
// Simple email pattern
Pattern email = Pattern.With
    .StartOfLine
    .Set(Pattern.With.Letter.Digit.Literal("._+-")).Repeat.OneOrMore
    .Literal("@")
    .Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore
    .Literal(".")
    .Letter.Repeat.Times(2, 10)
    .EndOfLine;

// Test various emails
email.IsMatch("user@example.com");          // true
email.IsMatch("user.name@domain.org");      // true
email.IsMatch("user+tag@mail.co.uk");       // true
email.IsMatch("invalid@");                  // false
email.IsMatch("@nodomain.com");             // false

// Or use the built-in pattern
CommonPatterns.Email().IsMatch("user@example.com");  // true
```

### Phone Number Parser

```csharp
Pattern phone = Pattern.With
    .StartOfLine
    .Literal("(").Repeat.Optional
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .Literal(")").Repeat.Optional
    .Set(Pattern.With.Whitespace.Literal("-")).Repeat.Optional
    .NamedGroup("exchange", Pattern.With.Digit.Repeat.Times(3))
    .Set(Pattern.With.Whitespace.Literal("-")).Repeat.Optional
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4))
    .EndOfLine;

string[] testNumbers = {
    "(555) 123-4567",
    "555-123-4567",
    "555 123 4567",
    "5551234567"
};

foreach (var num in testNumbers)
{
    Match match = phone.Match(num);
    if (match.Success)
    {
        Console.WriteLine($"Input: {num}");
        Console.WriteLine($"  Area: {match.Groups["area"].Value}");
        Console.WriteLine($"  Exchange: {match.Groups["exchange"].Value}");
        Console.WriteLine($"  Number: {match.Groups["number"].Value}");
    }
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
    .NamedGroup("domain",
        Pattern.With.Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore)
    .NamedGroup("port",
        Pattern.With.Literal(":").Digit.Repeat.Times(1, 5)).Repeat.Optional
    .NamedGroup("path",
        Pattern.With.Literal("/").Set(Pattern.With.Word.Literal("/-._~")).Repeat.ZeroOrMore)
        .Repeat.Optional
    .NamedGroup("query",
        Pattern.With.Literal("?").NegatedSet(Pattern.With.Literal("#")).Repeat.ZeroOrMore)
        .Repeat.Optional
    .NamedGroup("fragment",
        Pattern.With.Literal("#").Anything.Repeat.ZeroOrMore)
        .Repeat.Optional
    .EndOfLine;

Match match = url.Match("https://example.com:8080/path/to/page?query=value#section");

Console.WriteLine($"Protocol: {match.Groups["protocol"].Value}");  // "https"
Console.WriteLine($"Domain: {match.Groups["domain"].Value}");      // "example.com"
Console.WriteLine($"Port: {match.Groups["port"].Value}");          // ":8080"
Console.WriteLine($"Path: {match.Groups["path"].Value}");          // "/path/to/page"
Console.WriteLine($"Query: {match.Groups["query"].Value}");        // "?query=value"
Console.WriteLine($"Fragment: {match.Groups["fragment"].Value}");  // "#section"
```

### Log File Parser

```csharp
Pattern logEntry = Pattern.With
    .StartOfLine
    .Literal("[")
    .NamedGroup("date", Pattern.With
        .Digit.Repeat.Times(4).Literal("-")
        .Digit.Repeat.Times(2).Literal("-")
        .Digit.Repeat.Times(2))
    .Whitespace
    .NamedGroup("time", Pattern.With
        .Digit.Repeat.Times(2).Literal(":")
        .Digit.Repeat.Times(2).Literal(":")
        .Digit.Repeat.Times(2))
    .Literal("]")
    .Whitespace
    .Literal("[")
    .NamedGroup("level", Pattern.With.Word.Repeat.OneOrMore)
    .Literal("]")
    .Whitespace
    .NamedGroup("message", Pattern.With.Anything.Repeat.OneOrMore)
    .EndOfLine;

string[] logs = {
    "[2024-01-15 10:30:45] [INFO] Application started",
    "[2024-01-15 10:30:46] [DEBUG] Loading configuration",
    "[2024-01-15 10:30:47] [ERROR] Database connection failed",
    "[2024-01-15 10:30:48] [WARN] Retry attempt 1 of 3"
};

foreach (var log in logs)
{
    Match match = logEntry.Match(log);
    if (match.Success)
    {
        Console.WriteLine($"[{match.Groups["level"].Value}] {match.Groups["message"].Value}");
    }
}
// Output:
// [INFO] Application started
// [DEBUG] Loading configuration
// [ERROR] Database connection failed
// [WARN] Retry attempt 1 of 3
```

### Data Transformation

```csharp
// Convert dates from MM/DD/YYYY to YYYY-MM-DD
Pattern usDate = Pattern.With
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2))
    .Literal("/")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2))
    .Literal("/")
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4));

string input = "Events: 12/25/2024, 01/01/2025, 07/04/2025";
string result = usDate.Replace(input, "${year}-${month}-${day}");
// Result: "Events: 2024-12-25, 2025-01-01, 2025-07-04"

// Format phone numbers
Pattern rawPhone = Pattern.With
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .NamedGroup("exchange", Pattern.With.Digit.Repeat.Times(3))
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4));

string phones = "Call 5551234567 or 5559876543";
string formatted = rawPhone.Replace(phones, "(${area}) ${exchange}-${number}");
// Result: "Call (555) 123-4567 or (555) 987-6543"

// Mask sensitive data
Pattern ssn = Pattern.With
    .Digit.Repeat.Times(3)
    .Literal("-")
    .NamedGroup("middle", Pattern.With.Digit.Repeat.Times(2))
    .Literal("-")
    .NamedGroup("last", Pattern.With.Digit.Repeat.Times(4));

string data = "SSN: 123-45-6789";
string masked = ssn.Replace(data, "XXX-${middle}-${last}");
// Result: "SSN: XXX-45-6789"
```

### Password Validation

```csharp
// Build a comprehensive password validator
Pattern passwordValidator = Pattern.With
    .StartOfLine
    // At least one lowercase letter
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.LowercaseLetter))
    // At least one uppercase letter
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.UppercaseLetter))
    // At least one digit
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Digit)
    // At least one special character
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.Literal("!@#$%^&*()_+-=")))
    // Minimum 8 characters, maximum 50
    .Anything.Repeat.Times(8, 50)
    .EndOfLine;

// Test passwords
var passwords = new[] {
    ("Password1!", true),
    ("password1!", false),   // No uppercase
    ("PASSWORD1!", false),   // No lowercase
    ("Password!", false),    // No digit
    ("Password1", false),    // No special char
    ("Pass1!", false),       // Too short
    ("ValidPassword123!", true)
};

foreach (var (password, expected) in passwords)
{
    bool isValid = passwordValidator.IsMatch(password);
    Console.WriteLine($"{password}: {(isValid ? "Valid" : "Invalid")} (expected: {expected})");
}
```

### HTML Tag Extraction

```csharp
Pattern htmlTag = Pattern.With
    .Literal("<")
    .NamedGroup("tagName", Pattern.With.Letter.Repeat.OneOrMore)
    .NamedGroup("attributes", Pattern.With.NegatedSet(Pattern.With.Literal(">")).Repeat.ZeroOrMore)
    .Literal(">");

string html = @"<div class=""container""><p id=""intro"">Hello</p><span>World</span></div>";

MatchCollection tags = htmlTag.Matches(html);
foreach (Match tag in tags)
{
    Console.WriteLine($"Tag: {tag.Groups["tagName"].Value}");
    if (!string.IsNullOrWhiteSpace(tag.Groups["attributes"].Value))
    {
        Console.WriteLine($"  Attributes: {tag.Groups["attributes"].Value.Trim()}");
    }
}
// Output:
// Tag: div
//   Attributes: class="container"
// Tag: p
//   Attributes: id="intro"
// Tag: span
```

### CSV Line Parser

```csharp
// Parse CSV with quoted fields (handles commas inside quotes)
Pattern csvField = Pattern.With
    .Choice(
        // Quoted field
        Pattern.With
            .Literal("\"")
            .NamedGroup("quoted", Pattern.With
                .NegatedSet(Pattern.With.Literal("\"")).Repeat.ZeroOrMore)
            .Literal("\""),
        // Unquoted field
        Pattern.With
            .NamedGroup("unquoted", Pattern.With
                .NegatedSet(Pattern.With.Literal(",\"\r\n")).Repeat.ZeroOrMore)
    );

string csvLine = "John,\"Doe, Jr.\",30,\"New York, NY\"";

MatchCollection fields = csvField.Matches(csvLine);
List<string> values = new List<string>();

foreach (Match field in fields)
{
    string value = field.Groups["quoted"].Success
        ? field.Groups["quoted"].Value
        : field.Groups["unquoted"].Value;
    values.Add(value);
}

Console.WriteLine(string.Join(" | ", values));
// Output: John | Doe, Jr. | 30 | New York, NY
```

---

## API Reference

### Pattern Class

#### Static Entry Point

| Property | Description |
|----------|-------------|
| `Pattern.With` | Creates a new pattern builder instance |

#### Anchor Methods

| Property | Regex | Description |
|----------|-------|-------------|
| `.StartOfLine` | `^` | Matches start of line/string |
| `.EndOfLine` | `$` | Matches end of line/string |
| `.WordBoundary` | `\b` | Matches word boundary position |

#### Character Matcher Properties

| Property | Regex | Description |
|----------|-------|-------------|
| `.Digit` | `\d` | Matches digit [0-9] |
| `.NonDigit` | `\D` | Matches non-digit |
| `.Word` | `\w` | Matches word character [a-zA-Z0-9_] |
| `.NonWord` | `\W` | Matches non-word character |
| `.Whitespace` | `\s` | Matches whitespace |
| `.NonWhitespace` | `\S` | Matches non-whitespace |
| `.Letter` | `a-zA-Z` | Matches any letter (for use in sets) |
| `.LowercaseLetter` | `a-z` | Matches lowercase letter |
| `.UppercaseLetter` | `A-Z` | Matches uppercase letter |
| `.Anything` | `.` | Matches any character except newline |
| `.Tab` | `\t` | Matches tab character |
| `.Newline` | `\n` | Matches newline character |
| `.CarriageReturn` | `\r` | Matches carriage return |

#### Literal and Raw Methods

| Method | Description |
|--------|-------------|
| `.Literal(string)` | Matches literal text (auto-escaped) |
| `.Literal(params char[])` | Matches literal characters (auto-escaped) |
| `.RegEx(string)` | Inserts raw regex (no escaping) |

#### Character Class Methods

| Method | Regex | Description |
|--------|-------|-------------|
| `.Set(pattern)` | `[...]` | Matches one character from set |
| `.NegatedSet(pattern)` | `[^...]` | Matches one character not in set |
| `.Choice(p1, p2, ...)` | `(p1\|p2\|...)` | Matches any one of the alternatives |

#### Group Methods

| Method | Regex | Description |
|--------|-------|-------------|
| `.Group(pattern)` | `(...)` | Capturing group |
| `.NamedGroup(name, pattern)` | `(?<name>...)` | Named capturing group |
| `.NonCapturingGroup(pattern)` | `(?:...)` | Non-capturing group |
| `.AtomicGroup(pattern)` | `(?>...)` | Atomic (non-backtracking) group |

#### Assertion Methods

| Method | Regex | Description |
|--------|-------|-------------|
| `.PositiveLookahead(pattern)` | `(?=...)` | Must be followed by pattern |
| `.NegativeLookahead(pattern)` | `(?!...)` | Must NOT be followed by pattern |
| `.PositiveLookbehind(pattern)` | `(?<=...)` | Must be preceded by pattern |
| `.NegativeLookbehind(pattern)` | `(?<!...)` | Must NOT be preceded by pattern |

#### Backreference Methods

| Method | Regex | Description |
|--------|-------|-------------|
| `.Backreference(name)` | `\k<name>` | Reference named group |
| `.Backreference(int)` | `\n` | Reference numbered group |

#### Conditional Method

| Method | Regex | Description |
|--------|-------|-------------|
| `.Conditional(name, truePattern, falsePattern)` | `(?(name)true\|false)` | Conditional on named group |
| `.Conditional(int, truePattern, falsePattern)` | `(?(n)true\|false)` | Conditional on numbered group |

#### Matching Methods

| Method | Description |
|--------|-------------|
| `.IsMatch(input)` | Tests if pattern matches input |
| `.IsMatch(input, options)` | Tests with specified options |
| `.Match(input)` | Finds first match |
| `.Match(input, startAt)` | Finds first match starting at position |
| `.Matches(input)` | Finds all matches |
| `.Replace(input, replacement)` | Replaces all matches |
| `.Replace(input, replacement, count)` | Replaces up to count matches |
| `.Replace(input, evaluator)` | Replaces using function |
| `.Replace(input, evaluator, count)` | Replaces up to count using function |
| `.Split(input)` | Splits at matches |
| `.Split(input, count)` | Splits into up to count parts |

#### Configuration Methods

| Method | Description |
|--------|-------------|
| `.WithOptions(options)` | Sets default RegexOptions |
| `.ToRegex(options)` | Compiles to System.Text.RegularExpressions.Regex |
| `.ToString()` | Returns the regex pattern string |

#### Validation Methods

| Method | Description |
|--------|-------------|
| `.IsValid()` | Returns true if pattern is valid |
| `.TryValidate(out error)` | Validates with error message |
| `.GetGroupNames()` | Returns named group names |
| `.GetGroupCount()` | Returns capturing group count |
| `.Explain()` | Returns human-readable explanation |

### Repeat Class

Access via `.Repeat` property on Pattern:

| Property/Method | Regex | Description |
|-----------------|-------|-------------|
| `.OneOrMore` | `+` | One or more times |
| `.ZeroOrMore` | `*` | Zero or more times |
| `.Optional` | `?` | Zero or one time |
| `.Times(n)` | `{n}` | Exactly n times |
| `.Times(min, max)` | `{min,max}` | Between min and max times |
| `.AtLeast(n)` | `{n,}` | At least n times |
| `.AtMost(n)` | `{,n}` | At most n times |
| `.Lazy` | `?` suffix | Non-greedy matching |

### CommonPatterns Class

All methods return `Pattern` instances:

| Method | Description |
|--------|-------------|
| `Email()` | Email addresses |
| `Url()` | HTTP/HTTPS URLs |
| `PhoneNumber()` | US phone numbers |
| `IPv4()` | IPv4 addresses (validates 0-255) |
| `IPv6()` | IPv6 addresses |
| `Guid()` | GUIDs/UUIDs |
| `HexColor()` | CSS hex colors (#RGB, #RRGGBB, #RRGGBBAA) |
| `DateIso()` | ISO 8601 dates (YYYY-MM-DD) |
| `Date()` | Multiple date formats |
| `Time24()` | 24-hour time |
| `Time12()` | 12-hour time with AM/PM |
| `CreditCard()` | 16-digit credit cards |
| `SocialSecurityNumber()` | US SSN |
| `ZipCode()` | US ZIP codes (5-digit and ZIP+4) |
| `Username()` | Alphanumeric usernames (3-20 chars) |
| `StrongPassword()` | Complex password requirements |
| `FilePath()` | Windows and Unix file paths |
| `HtmlTag()` | HTML/XML tags |
| `Integer()` | Signed integers |
| `Decimal()` | Decimal numbers |
| `ScientificNotation()` | Scientific notation |

---

## Best Practices

### 1. Start Simple, Add Complexity

```csharp
// Start with the core pattern
Pattern email = Pattern.With
    .Word.Repeat.OneOrMore
    .Literal("@")
    .Word.Repeat.OneOrMore;

// Then refine
Pattern emailBetter = Pattern.With
    .Set(Pattern.With.Letter.Digit.Literal("._+-")).Repeat.OneOrMore
    .Literal("@")
    .Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore
    .Literal(".")
    .Letter.Repeat.AtLeast(2);
```

### 2. Use Named Groups for Extraction

```csharp
// Instead of remembering group numbers
Pattern bad = Pattern.With
    .Group(Pattern.With.Digit.Repeat.Times(3))
    .Literal("-")
    .Group(Pattern.With.Digit.Repeat.Times(4));

// Use meaningful names
Pattern good = Pattern.With
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .Literal("-")
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4));
```

### 3. Validate Patterns During Development

```csharp
Pattern pattern = BuildComplexPattern();

// Validate before use
if (!pattern.TryValidate(out string error))
{
    throw new InvalidOperationException($"Invalid pattern: {error}");
}
```

### 4. Use Explain() for Documentation

```csharp
Pattern complexPattern = BuildComplexPattern();

// Generate documentation
string documentation = complexPattern.Explain();
Console.WriteLine(documentation);
```

### 5. Prefer Non-Capturing Groups When Not Extracting

```csharp
// Unnecessarily captures groups
Pattern bad = Pattern.With
    .Group(Pattern.With.Literal("http").Literal("s").Repeat.Optional)
    .Literal("://");

// Better - doesn't create capture
Pattern good = Pattern.With
    .NonCapturingGroup(Pattern.With.Literal("http").Literal("s").Repeat.Optional)
    .Literal("://");
```

### 6. Use CommonPatterns When Available

```csharp
// Don't reinvent the wheel
Pattern email = CommonPatterns.Email();
Pattern ipv4 = CommonPatterns.IPv4();
Pattern guid = CommonPatterns.Guid();
```

---

## Performance Considerations

### 1. Use Compiled Option for Frequently Used Patterns

```csharp
// For patterns used many times
Pattern frequentPattern = Pattern.With
    .Digit.Repeat.OneOrMore
    .WithOptions(RegexOptions.Compiled);

// Or when converting to Regex
Regex compiled = pattern.ToRegex(RegexOptions.Compiled);
```

### 2. Cache Pattern Instances

```csharp
// Bad - creates new pattern each time
public bool ValidateEmail(string email)
{
    return Pattern.With./*...*/.IsMatch(email);
}

// Good - reuse pattern instance
private static readonly Pattern EmailPattern = Pattern.With
    .StartOfLine
    .Set(Pattern.With.Letter.Digit.Literal("._+-")).Repeat.OneOrMore
    .Literal("@")
    .Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore
    .Literal(".")
    .Letter.Repeat.AtLeast(2)
    .EndOfLine;

public bool ValidateEmail(string email)
{
    return EmailPattern.IsMatch(email);
}
```

### 3. Use Atomic Groups to Prevent Backtracking

```csharp
// Can cause catastrophic backtracking
Pattern risky = Pattern.With
    .Word.Repeat.OneOrMore
    .Word.Repeat.OneOrMore;

// Use atomic group to prevent backtracking
Pattern safe = Pattern.With
    .AtomicGroup(Pattern.With.Word.Repeat.OneOrMore)
    .Word.Repeat.OneOrMore;
```

### 4. Prefer Specific Patterns Over Greedy Wildcards

```csharp
// Slow - lots of backtracking
Pattern slow = Pattern.With
    .Anything.Repeat.ZeroOrMore
    .Literal("end");

// Faster - more specific
Pattern fast = Pattern.With
    .NegatedSet(Pattern.With.Literal("e")).Repeat.ZeroOrMore
    .Literal("end");

// Or use lazy quantifier
Pattern lazy = Pattern.With
    .Anything.Repeat.ZeroOrMore.Lazy
    .Literal("end");
```

---

## Migration Guide

### From Traditional Regex

**Before:**
```csharp
var regex = new Regex(@"^\d{3}-\d{3}-\d{4}$");
bool isMatch = regex.IsMatch(input);
```

**After:**
```csharp
Pattern phone = Pattern.With
    .StartOfLine
    .Digit.Repeat.Times(3)
    .Literal("-")
    .Digit.Repeat.Times(3)
    .Literal("-")
    .Digit.Repeat.Times(4)
    .EndOfLine;
bool isMatch = phone.IsMatch(input);
```

### Pattern Equivalents

| Regex | FluentRegex |
|-------|-------------|
| `^` | `.StartOfLine` |
| `$` | `.EndOfLine` |
| `\d` | `.Digit` |
| `\w` | `.Word` |
| `\s` | `.Whitespace` |
| `.` | `.Anything` |
| `\b` | `.WordBoundary` |
| `[abc]` | `.Set(Pattern.With.Literal("abc"))` |
| `[^abc]` | `.NegatedSet(Pattern.With.Literal("abc"))` |
| `(pattern)` | `.Group(pattern)` |
| `(?<name>pattern)` | `.NamedGroup("name", pattern)` |
| `(?:pattern)` | `.NonCapturingGroup(pattern)` |
| `(?=pattern)` | `.PositiveLookahead(pattern)` |
| `(?!pattern)` | `.NegativeLookahead(pattern)` |
| `(?<=pattern)` | `.PositiveLookbehind(pattern)` |
| `(?<!pattern)` | `.NegativeLookbehind(pattern)` |
| `\k<name>` | `.Backreference("name")` |
| `+` | `.Repeat.OneOrMore` |
| `*` | `.Repeat.ZeroOrMore` |
| `?` | `.Repeat.Optional` |
| `{n}` | `.Repeat.Times(n)` |
| `{n,m}` | `.Repeat.Times(n, m)` |
| `{n,}` | `.Repeat.AtLeast(n)` |
| `+?` / `*?` | `.Repeat.OneOrMore.Lazy` / `.Repeat.ZeroOrMore.Lazy` |

---

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Development Setup

```bash
# Clone the repository
git clone https://github.com/a7mdfre7at/Masterly.FluentRegex.git

# Navigate to the project
cd Masterly.FluentRegex

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests
dotnet test
```

---

## License

MIT License - see the [LICENSE](LICENSE) file for details.

**Free Software, Hell Yeah!**
