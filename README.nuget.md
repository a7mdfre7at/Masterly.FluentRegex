# Masterly.FluentRegex

A fluent, readable API for building regular expression patterns in .NET.

## Quick Start

```csharp
using Masterly.FluentRegex;

// Build a pattern fluently
Pattern pattern = Pattern.With
    .StartOfLine
    .Digit.Repeat.OneOrMore
    .Literal("-")
    .Word.Repeat.Times(3, 5)
    .EndOfLine;

// Use it directly
bool isMatch = pattern.IsMatch("123-abc");
string result = pattern.Replace("123-abc", "X");

// Or convert to Regex
Regex regex = pattern.ToRegex(RegexOptions.IgnoreCase);
```

## Features

- **Fluent API** - Build regex patterns with readable, chainable methods
- **Named Groups** - `NamedGroup("name", pattern)` for easy capture extraction
- **Lookahead/Lookbehind** - Zero-width assertions for advanced matching
- **Full Matching API** - `IsMatch`, `Match`, `Matches`, `Replace`, `Split`
- **Common Patterns** - Pre-built patterns for Email, URL, Phone, IP, GUID, and more
- **Validation & Debugging** - `IsValid()`, `Explain()` for pattern analysis

## Common Patterns

```csharp
CommonPatterns.Email().IsMatch("user@example.com");     // true
CommonPatterns.Url().IsMatch("https://example.com");    // true
CommonPatterns.IPv4().IsMatch("192.168.1.1");           // true
CommonPatterns.Guid().IsMatch("550e8400-e29b-...");     // true
CommonPatterns.StrongPassword().IsMatch("Password1!"); // true
```

## Documentation

For full documentation, examples, and API reference, visit the [GitHub repository](https://github.com/a7mdfre7at/Masterly.FluentRegex#readme).

## License

MIT
