---
id: examples
title: "Real-World Examples"
description: "Complete, runnable scenarios: validating e-mails, phones and passwords, parsing URLs, logs and CSV, transforming text, and end-to-end recipes with templates, streaming, redaction, EF Core and untrusted regexes."
---

This page collects complete scenarios you can copy into a project. Each one states the problem, builds the pattern, runs it and shows the real output. The first sections cover everyday jobs with the core fluent API. The later sections are end-to-end recipes that combine several features of the library, such as a template with streaming, or a pattern with a database query.

| Scenario | Features used |
|----------|---------------|
| [E-mail addresses](#e-mail-addresses), [passwords](#passwords), [phone numbers](#phone-numbers) | Sets, lookaheads, named groups, `CommonPatterns` |
| [URLs](#urls), [HTML tags](#html-tags), [CSV lines](#csv-lines), [log lines](#log-lines) | Named groups, optional groups, `Matches` |
| [Log lines into typed records](#log-lines-into-typed-records) | `ExtractAll<T>`, `Extract<T>` |
| [Transforming text](#transforming-text) | `Replace` with replacement strings and fluent replacements |
| [Parsing a large log file](#parsing-a-large-log-file-with-a-template-and-streaming) | `Template<T>`, streaming `Matches(reader)` and `MatchesAsync(stream)` |
| [Validating API input](#validating-api-input-with-rules-and-matchespattern) | `Must` rules, `[MatchesPattern]`, the `pattern` route constraint |
| [Redacting PII in logs](#redacting-pii-in-logs) | `Redactor`, `RedactionStyle`, `RedactionReport` |
| [Searching a database](#searching-a-database-with-ef-core) | EF Core translation, `ToMatchingListAsync`, `HasPattern` |
| [Guarding a user-supplied regex](#guarding-a-user-supplied-regex) | `FromRegex`, `AnalyzeSafety`, `WithTimeout`, `UseNonBacktracking` |
| [A tour of the showcase sample](#a-tour-of-the-showcase-sample) | Operators, `ToFluentCode`, test data, `[GeneratePattern]`, span APIs |

Every output on this page comes from running the code on .NET 10 with version 3.1 of the packages. The samples are written as top-level statements. Put type declarations, such as records and static classes, at the end of `Program.cs` or in their own files.

## Validating input

Validation patterns describe a whole value, so they start with `StartOfLine` (`^`) and end with `EndOfLine` (`$`). Without the anchors, `IsMatch` returns `true` when the value only *contains* a match.

### E-mail addresses

**Problem:** accept e-mail addresses typed into a form, including `+` tags such as `user+tag@mail.co.uk`.

```csharp
using System;
using Masterly.FluentRegex;

Pattern email = Pattern.With
    .StartOfLine
    .Set(Pattern.With.Letter.Digit.Literal("._+-")).Repeat.OneOrMore
    .Literal("@")
    .Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore
    .Literal(".")
    .Letter.Repeat.Times(2, 10)
    .EndOfLine;

Console.WriteLine(email); // prints: ^[a-zA-Z\d\._\+\-]+@[a-zA-Z\d\.\-]+\.[a-zA-Z]{2,10}$

bool simple = email.IsMatch("user@example.com");        // true
bool dotted = email.IsMatch("user.name@domain.org");    // true
bool tagged = email.IsMatch("user+tag@mail.co.uk");     // true
bool noDomain = email.IsMatch("invalid@");              // false
bool noUser = email.IsMatch("@nodomain.com");           // false
bool doubleDots = email.IsMatch("a..b@x..com");         // true: the sets allow any run of dots
```

- `Set(...)` builds one character class from several pieces: `Letter`, `Digit` and the literal characters `._+-`. Characters with a special meaning in a class are escaped for you.
- `Repeat.Times(2, 10)` limits the top-level domain to 2 to 10 letters.
- A shape check like this is good enough for a sign-up form. It does not reject every invalid address, as the last line shows.

`CommonPatterns` has two ready-made e-mail patterns. Pick one by how strict you need to be:

| Input | `CommonPatterns.Email()` | `CommonPatterns.Email(PatternStrictness.Strict)` |
|-------|--------------------------|--------------------------------------------------|
| `user@example.com` | `true` | `true` |
| `user+tag@mail.co.uk` | `false` | `true` |
| `a..b@x.com` | `true` | `false` |
| `user@localhost` | `false` | `false` |

```csharp
using System.Text.RegularExpressions;

Pattern strict = CommonPatterns.Email(PatternStrictness.Strict);

bool plusTag = strict.IsMatch("user+tag@mail.co.uk");   // true
bool doubleDot = strict.IsMatch("a..b@x.com");          // false

Match parts = strict.Match("user+tag@mail.co.uk");
string local = parts.Groups["local"].Value;             // user+tag
string domain = parts.Groups["domain"].Value;           // mail.co.uk
```

- The default `Email()` does not accept `+` in the local part. Use the strict pattern, or your own, when users rely on `+` tags.
- The strict pattern follows the RFC 5321 length limits: 64 characters for the local part and 254 in total. See [Common Patterns](./common-patterns.md) for every built-in pattern.

:::warning

In .NET, `$` also matches before a final `\n`. `email.IsMatch("user@example.com\n")` returns `true`. When a trailing newline must be rejected, anchor with `StartOfInput` and `EndOfInput` (`\A...\z`), or validate with a [constrained pattern's](./semantic-rules.md) `IsValid`, which wraps the pattern in `\A(?:...)\z` for you.

:::

```csharp
Pattern endOfLine = Pattern.With.StartOfLine.Digit.Repeat.Times(3).EndOfLine;
Pattern endOfInput = Pattern.With.StartOfInput.Digit.Repeat.Times(3).EndOfInput;

bool lenient = endOfLine.IsMatch("123\n");    // true
bool exact = endOfInput.IsMatch("123\n");     // false
Console.WriteLine(endOfInput);                // prints: \A\d{3}\z
```

### Passwords

**Problem:** require at least one lowercase letter, one uppercase letter, one digit and one special character, with a length of 8 to 50 characters.

Each requirement is a lookahead at the start of the value. A lookahead checks a condition without consuming text, so all four run from the same position, and the final `.{8,50}` checks the length.

```csharp
Pattern password = Pattern.With
    .StartOfLine
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.LowercaseLetter))
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.UppercaseLetter))
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Digit)
    .PositiveLookahead(Pattern.With.Anything.Repeat.ZeroOrMore.Set(Pattern.With.Literal("!@#$%^&*()_+-=")))
    .Anything.Repeat.Times(8, 50)
    .EndOfLine;

Console.WriteLine(password);
// prints: ^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#\$%\^&\*\(\)_\+\-=]).{8,50}$

string[] candidates = { "Password1!", "password1!", "PASSWORD1!", "Password!", "Password1", "Pass1!", "ValidPassword123!" };

foreach (string candidate in candidates)
{
    Console.WriteLine($"{candidate,-18} {(password.IsMatch(candidate) ? "valid" : "invalid")}");
}
// prints:
// Password1!         valid
// password1!         invalid
// PASSWORD1!         invalid
// Password!          invalid
// Password1          invalid
// Pass1!             invalid
// ValidPassword123!  valid
```

- The four rejected inputs in the middle each miss one rule: no uppercase, no lowercase, no digit, no special character. `Pass1!` is too short.
- `CommonPatterns.StrongPassword()` is the same pattern without the upper limit: `^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#\$%\^&\*\(\)_\+\-=]).{8,}$`. It gives the same answers for these seven inputs.
- `password.AnalyzeSafety().Risk` is `None`: the lookaheads cannot cause catastrophic backtracking. See [ReDoS Safety](./redos-safety.md).
- To tell the user *which* rule failed, use one pattern per rule, or a [constrained pattern](./semantic-rules.md) whose rule descriptions end up in the error message.

### Phone numbers

**Problem:** read US phone numbers written as `(555) 123-4567`, `555-123-4567`, `555 123 4567` or `5551234567`, and pull out the three parts.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern separator = Pattern.With.Set(Pattern.With.Whitespace.Literal("-")).Repeat.Optional;

Pattern phone = Pattern.With
    .StartOfLine
    .Literal("(").Repeat.Optional
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .Literal(")").Repeat.Optional
    .Then(separator)
    .NamedGroup("exchange", Pattern.With.Digit.Repeat.Times(3))
    .Then(separator)
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4))
    .EndOfLine;

Console.WriteLine(phone);
// prints: ^\(?(?<area>\d{3})\)?[\s\-]?(?<exchange>\d{3})[\s\-]?(?<number>\d{4})$

string[] numbers = { "(555) 123-4567", "555-123-4567", "555 123 4567", "5551234567", "(555 123-4567", "555-1234" };

foreach (string number in numbers)
{
    Match match = phone.Match(number);
    Console.WriteLine(match.Success
        ? $"{number,-15} -> {match.Groups["area"].Value} / {match.Groups["exchange"].Value} / {match.Groups["number"].Value}"
        : $"{number,-15} -> no match");
}
// prints:
// (555) 123-4567  -> 555 / 123 / 4567
// 555-123-4567    -> 555 / 123 / 4567
// 555 123 4567    -> 555 / 123 / 4567
// 5551234567      -> 555 / 123 / 4567
// (555 123-4567   -> 555 / 123 / 4567
// 555-1234        -> no match
```

- The separator is declared once and reused with `Then`. Patterns are immutable, so sharing a piece is always safe.
- `NamedGroup` captures each part. Read it with `match.Groups["area"].Value`.
- The two parentheses are optional **independently**, so the unbalanced `(555 123-4567` is accepted.

To accept the parentheses only as a pair, offer the two forms of the area code as a `Choice`. .NET allows the same group name in both branches:

```csharp
// separator is from the previous sample
Pattern threeDigits = Pattern.With.Digit.Repeat.Times(3);

Pattern strictPhone = Pattern.With
    .StartOfLine
    .Choice(
        Pattern.With.Literal("(").NamedGroup("area", threeDigits).Literal(")"),
        Pattern.With.NamedGroup("area", threeDigits))
    .Then(separator)
    .NamedGroup("exchange", threeDigits)
    .Then(separator)
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4))
    .EndOfLine;

Console.WriteLine(strictPhone);
// prints: ^(\((?<area>\d{3})\)|(?<area>\d{3}))[\s\-]?(?<exchange>\d{3})[\s\-]?(?<number>\d{4})$

bool paired = strictPhone.IsMatch("(555) 123-4567");   // true
bool bare = strictPhone.IsMatch("5551234567");         // true
bool openOnly = strictPhone.IsMatch("(555 123-4567");  // false
bool closeOnly = strictPhone.IsMatch("555) 123-4567"); // false
```

`Choice` wraps its branches in a numbered capturing group, `(...|...)`. To build an alternation without a capture, use the `|` operator, as shown in [A tour of the showcase sample](#a-tour-of-the-showcase-sample). For international numbers, `CommonPatterns.PhoneNumberE164()` matches the E.164 form such as `+14155552671`.

## Parsing structured text

These scenarios split a value or a document into named parts. Optional parts are optional groups: check `Group.Success` to tell "absent" from "empty".

### URLs

**Problem:** split a URL into protocol, domain, port, path, query string and fragment.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern url = Pattern.With
    .StartOfLine
    .NamedGroup("protocol", Pattern.With.Choice(
        Pattern.With.Literal("https"),
        Pattern.With.Literal("http")))
    .Literal("://")
    .NamedGroup("domain", Pattern.With.Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore)
    .NamedGroup("port", Pattern.With.Literal(":").Digit.Repeat.Times(1, 5)).Repeat.Optional
    .NamedGroup("path", Pattern.With.Literal("/").Set(Pattern.With.Word.Literal("/-._~")).Repeat.ZeroOrMore).Repeat.Optional
    .NamedGroup("query", Pattern.With.Literal("?").NegatedSet(Pattern.With.Literal("#")).Repeat.ZeroOrMore).Repeat.Optional
    .NamedGroup("fragment", Pattern.With.Literal("#").Anything.Repeat.ZeroOrMore).Repeat.Optional
    .EndOfLine;

Console.WriteLine(url);
// prints: ^(?<protocol>(https|http))://(?<domain>[a-zA-Z\d\.\-]+)(?<port>:\d{1,5})?(?<path>/[\w/\-\._~]*)?(?<query>\?[^#]*)?(?<fragment>#.*)?$

Match match = url.Match("https://example.com:8080/path/to/page?query=value#section");

Console.WriteLine(match.Groups["protocol"].Value);  // prints: https
Console.WriteLine(match.Groups["domain"].Value);    // prints: example.com
Console.WriteLine(match.Groups["port"].Value);      // prints: :8080
Console.WriteLine(match.Groups["path"].Value);      // prints: /path/to/page
Console.WriteLine(match.Groups["query"].Value);     // prints: ?query=value
Console.WriteLine(match.Groups["fragment"].Value);  // prints: #section
```

- `.Repeat.Optional` after `NamedGroup(...)` makes the whole group optional, so the generated regex is `(?<port>...)?`.
- `NegatedSet(Pattern.With.Literal("#"))` is `[^#]`: the query runs up to the fragment.
- Put `https` before `http` in the `Choice`. Alternatives are tried in order; here the `://` that follows would still force the right one, but the habit avoids surprises in patterns where nothing follows.

When an optional part is missing, its group does not take part in the match:

```csharp
// url is from the previous sample
Match plain = url.Match("http://localhost");

bool matched = plain.Success;                      // true
bool hasPort = plain.Groups["port"].Success;       // false
string path = plain.Groups["path"].Value;          // "" (empty)
bool ftp = url.IsMatch("ftp://example.com");       // false
```

`CommonPatterns.Url()` is a validator, not a parser. It has no named groups, requires a dot in the host and does not accept a port: `CommonPatterns.Url().IsMatch("https://example.com:8080/path/to/page?query=value#section")` returns `false`. For real-world URL handling, validate the shape with a pattern and parse the parts with `System.Uri`.

### HTML tags

**Problem:** list the opening tags of a snippet of trusted HTML, with their attributes.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern htmlTag = Pattern.With
    .Literal("<")
    .NamedGroup("tagName", Pattern.With.Letter.Repeat.OneOrMore)
    .NamedGroup("attributes", Pattern.With.NegatedSet(Pattern.With.Literal(">")).Repeat.ZeroOrMore)
    .Literal(">");

Console.WriteLine(htmlTag); // prints: <(?<tagName>[a-zA-Z]+)(?<attributes>[^>]*)>

string html = "<div class=\"container\"><p id=\"intro\">Hello</p><span>World</span></div>";

foreach (Match tag in htmlTag.Matches(html))
{
    string attributes = tag.Groups["attributes"].Value.Trim();
    Console.WriteLine(attributes.Length == 0
        ? tag.Groups["tagName"].Value
        : $"{tag.Groups["tagName"].Value} ({attributes})");
}
// prints:
// div (class="container")
// p (id="intro")
// span
```

- The pattern is not anchored, so `Matches` finds every tag in the text.
- Closing tags such as `</p>` are skipped because `tagName` must start with a letter right after `<`.
- `CommonPatterns.HtmlTag()` validates a single tag, including closing and self-closing ones: `</?[a-zA-Z\d]+(?:\s+[\w\-]+=("[^"]*"|'[^']*'))*\s*/?>`.

:::warning

A regular expression is not an HTML parser. It cannot follow nesting, comments, scripts or a `>` inside an attribute value. Use it for quick extraction from markup you control; for untrusted or arbitrary HTML, use a real HTML parser.

:::

### CSV lines

**Problem:** split a CSV line into fields, where a quoted field may contain commas: `John,"Doe, Jr.",30,"New York, NY"`.

A field is either a quoted string or a run of characters without commas and quotes. The obvious pattern for that has a trap:

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern naiveField = Pattern.With.Choice(
    Pattern.With
        .Literal("\"")
        .NamedGroup("quoted", Pattern.With.NegatedSet(Pattern.With.Literal("\"")).Repeat.ZeroOrMore)
        .Literal("\""),
    Pattern.With
        .NamedGroup("unquoted", Pattern.With.NegatedSet(Pattern.With.Literal(",\"\r\n")).Repeat.ZeroOrMore));

string csvLine = "John,\"Doe, Jr.\",30,\"New York, NY\"";

List<string> values = new List<string>();
foreach (Match field in naiveField.Matches(csvLine))
{
    values.Add(field.Groups["quoted"].Success ? field.Groups["quoted"].Value : field.Groups["unquoted"].Value);
}

Console.WriteLine(values.Count);                 // prints: 8
Console.WriteLine(string.Join(" | ", values));   // prints: John |  | Doe, Jr. |  | 30 |  | New York, NY | 
```

The unquoted branch can match an empty string, so the engine also reports an empty match at every comma and at the end of the line. Eight fields come back instead of four.

The fix is to start a field only at the start of the line or right after a comma. A lookbehind expresses exactly that:

```csharp
Pattern fieldStart = Pattern.With.PositiveLookbehind(
    Pattern.With.Choice(Pattern.With.StartOfLine, Pattern.With.Literal(",")));

Pattern csvField = Pattern.With
    .Then(fieldStart)
    .Choice(
        Pattern.With
            .Literal("\"")
            .NamedGroup("quoted", Pattern.With.NegatedSet(Pattern.With.Literal("\"")).Repeat.ZeroOrMore)
            .Literal("\""),
        Pattern.With
            .NamedGroup("unquoted", Pattern.With.NegatedSet(Pattern.With.Literal(",\"\r\n")).Repeat.ZeroOrMore));

Console.WriteLine(csvField);
// prints: (?<=(^|,))("(?<quoted>[^"]*)"|(?<unquoted>[^,"\r\n]*))

foreach (string line in new[] { "John,\"Doe, Jr.\",30,\"New York, NY\"", "a,,b,", ",x" })
{
    List<string> fields = new List<string>();
    foreach (Match field in csvField.Matches(line))
    {
        fields.Add(field.Groups["quoted"].Success ? field.Groups["quoted"].Value : field.Groups["unquoted"].Value);
    }

    Console.WriteLine($"{fields.Count}: {string.Join(" | ", fields.ConvertAll(value => $"[{value}]"))}");
}
// prints:
// 4: [John] | [Doe, Jr.] | [30] | [New York, NY]
// 4: [a] | [] | [b] | []
// 2: [] | [x]
```

- Empty fields are now real fields: `a,,b,` has four, the second and the last empty.
- `field.Groups["quoted"].Success` tells a quoted empty field (`""`) from an unquoted one.
- This pattern does not handle a doubled quote inside a quoted field (`"say ""hi"""`), and it reads one line at a time. For full RFC 4180 CSV, use a CSV library.

### Log lines

**Problem:** read lines such as `[2024-01-15 10:30:45] [INFO] Application started` and get the date, time, level and message.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

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

string[] lines =
{
    "[2024-01-15 10:30:45] [INFO] Application started",
    "[2024-01-15 10:30:46] [DEBUG] Loading configuration",
    "[2024-01-15 10:30:47] [ERROR] Database connection failed",
    "[2024-01-15 10:30:48] [WARN] Retry attempt 1 of 3",
    "garbage line",
};

foreach (string line in lines)
{
    Match match = logEntry.Match(line);
    if (match.Success)
    {
        Console.WriteLine($"{match.Groups["time"].Value} [{match.Groups["level"].Value}] {match.Groups["message"].Value}");
    }
}
// prints:
// 10:30:45 [INFO] Application started
// 10:30:46 [DEBUG] Loading configuration
// 10:30:47 [ERROR] Database connection failed
// 10:30:48 [WARN] Retry attempt 1 of 3
```

The pattern works line by line. On a whole file in one string, it needs `RegexOptions.Multiline`, so that `^` and `$` match at every line:

```csharp
// logEntry and lines are from the previous sample
string file = string.Join("\n", lines);

int withoutMultiline = logEntry.Matches(file).Count;                                  // 0
int withMultiline = logEntry.WithOptions(RegexOptions.Multiline).Matches(file).Count; // 4
```

:::warning

With `Multiline`, `$` matches before `\n` only. On a file with Windows line endings (`\r\n`), `.+` also takes the `\r`, so every message ends with a carriage return: `"Application started\r"`. Trim the value, or use a [template](./templates.md), whose text placeholders never cross `\r` or `\n`. The [template recipe](#parsing-a-large-log-file-with-a-template-and-streaming) below does that.

:::

### Log lines into typed records

**Problem:** turn log lines straight into objects with a `DateTime`, an enum and a string, instead of reading strings out of groups.

`ExtractAll<T>` binds the named groups of each match to the constructor parameters or properties of `T`, by name and case-insensitively, and converts each value to the member's type.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern logLine = Pattern.With
    .StartOfLine
    .NamedGroup("timestamp", Pattern.With.NonWhitespace.Repeat.OneOrMore)
    .Whitespace.Repeat.OneOrMore
    .NamedGroup("level", Pattern.With.UppercaseLetter.Repeat.OneOrMore)
    .Whitespace.Repeat.OneOrMore
    .NamedGroup("message", Pattern.With.Anything.Repeat.OneOrMore)
    .EndOfLine
    .WithOptions(RegexOptions.Multiline);

string log = """
    2024-05-17T10:30:00 INFO  Service started
    2024-05-17T10:31:12 WARN  Disk at 91%
    2024-05-17T10:32:40 ERROR Payment gateway timeout
    """;

foreach (LogEntry entry in logLine.ExtractAll<LogEntry>(log))
{
    Console.WriteLine($"{entry.Timestamp:HH:mm:ss} [{entry.Level,-5}] {entry.Message}");
}
// prints:
// 10:30:00 [Info ] Service started
// 10:31:12 [Warn ] Disk at 91%
// 10:32:40 [Error] Payment gateway timeout

public enum Level { Debug, Info, Warn, Error }

public record LogEntry(DateTime Timestamp, Level Level, string Message);
```

- The group `timestamp` binds to the `Timestamp` parameter, `level` to `Level`, and `message` to `Message`.
- `INFO` becomes `Level.Info`: enum conversion ignores case.
- The same works for a single value. `CommonPatterns.SemVer()` has the groups `major`, `minor`, `patch`, `prerelease` and `buildmetadata`:

```csharp
SemanticVersion version = CommonPatterns.SemVer().Extract<SemanticVersion>("3.0.0-rc.2+build.77");
Console.WriteLine(version);
// prints: SemanticVersion { Major = 3, Minor = 0, Patch = 0, Prerelease = rc.2, BuildMetadata = build.77 }

SemanticVersion release = CommonPatterns.SemVer().Extract<SemanticVersion>("3.1.0");
// release.Prerelease and release.BuildMetadata are null: their groups did not take part

bool parsed = CommonPatterns.SemVer().TryExtract<SemanticVersion>("v3", out SemanticVersion? none); // false; none is null

public record SemanticVersion(int Major, int Minor, int Patch, string? Prerelease, string? BuildMetadata);
```

When something does not fit, the error says what and where:

| Call | Result |
|------|--------|
| `CommonPatterns.SemVer().Extract<SemanticVersion>("v3")` | `FormatException`: `The input does not match the pattern ^(?<major>...)...$. Use Trace(input) to see where it fails.` |
| `logLine.ExtractAll<LogEntry>("2024-05-17T10:30:00 TRACE x")`, when enumerated | `FormatException`: `Group 'level' value "TRACE" cannot be converted to Level: Requested value 'TRACE' was not found.` |

`ExtractAll` is lazy, so a conversion error is thrown while you enumerate, at the line that fails. [Typed Extraction](./typed-extraction.md) covers the supported types, `[RegexGroup]` and culture handling.

## Transforming text

### Reformatting with replacement strings

**Problem:** rewrite every US date `MM/DD/YYYY` in a text as ISO `YYYY-MM-DD`, and format runs of ten digits as phone numbers.

```csharp
using System;
using Masterly.FluentRegex;

Pattern usDate = Pattern.With
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2))
    .Literal("/")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2))
    .Literal("/")
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4));

string events = usDate.Replace("Events: 12/25/2024, 01/01/2025, 07/04/2025", "${year}-${month}-${day}");
// Events: 2024-12-25, 2025-01-01, 2025-07-04

Pattern rawPhone = Pattern.With
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .NamedGroup("exchange", Pattern.With.Digit.Repeat.Times(3))
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4));

string formatted = rawPhone.Replace("Call 5551234567 or 5559876543", "(${area}) ${exchange}-${number}");
// Call (555) 123-4567 or (555) 987-6543
```

- `${name}` in the replacement string inserts the value of a named group. This is the standard .NET replacement syntax.
- The pattern has no anchors, so every occurrence in the text is replaced.

An unanchored pattern also matches *inside* longer runs. A 15-digit order number contains ten digits, so it gets "formatted" too. Add `WordBoundary` on both sides to match whole tokens only:

```csharp
// rawPhone is from the previous sample
string damaged = rawPhone.Replace("Order 123456789012345", "(${area}) ${exchange}-${number}");
// Order (123) 456-789012345

Pattern bounded = Pattern.With.WordBoundary.Then(rawPhone).WordBoundary;
string safe = bounded.Replace("Order 123456789012345, call 5551234567", "(${area}) ${exchange}-${number}");
// Order 123456789012345, call (555) 123-4567
```

### Masking part of a value

**Problem:** hide the first digits of a social security number and keep the rest.

```csharp
Pattern ssn = Pattern.With
    .Digit.Repeat.Times(3)
    .Literal("-")
    .NamedGroup("middle", Pattern.With.Digit.Repeat.Times(2))
    .Literal("-")
    .NamedGroup("last", Pattern.With.Digit.Repeat.Times(4));

string partly = ssn.Replace("SSN: 123-45-6789", "XXX-${middle}-${last}");   // SSN: XXX-45-6789
string lastOnly = ssn.Replace("SSN: 123-45-6789", "***-**-${last}");        // SSN: ***-**-6789
```

Only the parts you want to keep need a group. For more than one kind of sensitive value, and for a report of what was hidden, use a `Redactor` instead (see [Redacting PII in logs](#redacting-pii-in-logs)).

### Computing the replacement with a fluent replacement

**Problem:** rewrite ISO dates as `May 17, 2024`. That needs code, not just a replacement string: the month number becomes a name and the leading zero of the day goes away.

```csharp
using System;
using System.Globalization;
using Masterly.FluentRegex;

Pattern isoDate = Pattern.With
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4)).Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2)).Literal("-")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2));

string[] months = CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames;

string readable = isoDate.Replace("Released 2024-05-17, patched 2024-06-02.", r => r
    .Group("month", month => months[int.Parse(month, CultureInfo.InvariantCulture) - 1])
    .Literal(" ")
    .Group("day", day => day.TrimStart('0'))
    .Literal(", ")
    .Group("year"));

Console.WriteLine(readable); // prints: Released May 17, 2024, patched Jun 2, 2024.
```

- `Group(name, transform)` inserts the group's value after passing it through your function. `Group(name)` inserts it unchanged and `Literal(text)` inserts fixed text.
- The group names are checked against the pattern **before** anything is replaced. A typo fails loudly:

```csharp
// isoDate is from the previous sample
try
{
    isoDate.Replace("2024-05-17", r => r.Group("yaer"));
}
catch (ArgumentException exception)
{
    Console.WriteLine(exception.Message);
    // prints: Group 'yaer' does not exist in the pattern (?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2}). Available groups: 0, year, month, day.
}

string silent = isoDate.Replace("x 2024-05-17 y", "${yaer}");   // x ${yaer} y: a replacement string keeps the typo as text
```

A plain replacement string cannot catch the typo: .NET writes an unknown `${...}` out literally. [Fluent Replacements](./fluent-replacements.md) lists every replacement part, including `WholeMatch`, `BeforeMatch`, `AfterMatch` and `Computed`.

## Parsing a large log file with a template and streaming

**Problem:** a service writes a log file that can grow to gigabytes. You need typed log entries, for example to count lines per level or to alert on errors, without loading the file into memory. Some lines, such as stack traces, are not log entries and must be skipped.

Two features fit together here:

- A [template](./templates.md) describes the line format the way you would write an interpolated string, and parses a line into a typed record.
- [Streaming](./streaming.md) finds matches in a `TextReader` or a `Stream` while holding only a bounded window of the text.

A template's `Pattern` property is an ordinary unanchored pattern, so you stream with it and parse each match with the template.

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Masterly.FluentRegex;

Console.WriteLine(AppLog.Line.Pattern);
// prints: (?<Time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) \[(?<Level>(?i:Debug|Error|Info|Warn))\] (?<Service>[^\r\n]*?): (?<Message>[^\r\n]*)

Dictionary<Severity, int> perLevel = new Dictionary<Severity, int>();

using (StreamReader reader = File.OpenText("app.log"))
{
    foreach (StreamMatch match in AppLog.Line.Pattern.Matches(reader))
    {
        AppLogLine line = AppLog.Line.Parse(match.Value);
        perLevel[line.Level] = perLevel.TryGetValue(line.Level, out int count) ? count + 1 : 1;
    }
}

foreach (KeyValuePair<Severity, int> entry in perLevel.OrderBy(entry => entry.Key))
{
    Console.WriteLine($"{entry.Key}: {entry.Value}");
}

public enum Severity { Debug, Info, Warn, Error }

public record AppLogLine(DateTime Time, Severity Level, string Service, string Message);

public static class AppLog
{
    public static readonly Template<AppLogLine> Line =
        Template.Of<AppLogLine>("{Time:yyyy-MM-dd HH:mm:ss} [{Level}] {Service}: {Message}");
}
```

For an `app.log` that contains

```text
2026-11-02 08:00:01 [INFO] api: started on port 8080
2026-11-02 08:00:07 [WARN] db: slow query took 2300 ms
   at Db.Query() in Db.cs:line 42
2026-11-02 08:01:15 [ERROR] payments: gateway timeout
2026-11-02 08:01:16 [ERROR] payments: gateway timeout
2026-11-02 08:02:00 [INFO] api: health check ok
```

this prints

```text
Info: 2
Warn: 1
Error: 2
```

- The template is compiled once into a `static readonly` field. `Template<T>` is immutable and thread-safe.
- `{Time:yyyy-MM-dd HH:mm:ss}` matches exactly that date format and parses it into `DateTime`. `{Level}` matches the names of the `Severity` enum in any case.
- The stack-trace line does not match the template's pattern, so the scan skips it. No `try`/`catch` is needed.
- Text placeholders such as `{Service}` and `{Message}` never cross a line break, so each match stays inside one line. That keeps every match far below the streaming limit, `StreamMatchOptions.MaxMatchLength` (4,096 characters by default). A longer log line that is not at the very end of the input throws `InvalidOperationException` instead of being returned possibly cut off. Pass `new StreamMatchOptions { MaxMatchLength = 8192 }` as the second argument of `Matches` if your lines can be that long.
- The memory used depends on the buffer size, not on the size of the file.

### Asynchronously, from a Stream

In a background service or a web request, read the bytes asynchronously with `MatchesAsync`. It decodes the stream and leaves it open, so it also works on streams you do not own, such as an upload's request body.

```csharp
using System.Text;
using System.Threading;

// AppLog and AppLogLine are from the previous sample; cancellationToken comes from your application
await using (FileStream stream = File.OpenRead("app.log"))
{
    await foreach (StreamMatch match in AppLog.Line.Pattern.MatchesAsync(stream, Encoding.UTF8, cancellationToken: cancellationToken))
    {
        AppLogLine line = AppLog.Line.Parse(match.Value);
        if (line.Level == Severity.Error)
        {
            Console.WriteLine($"{match.Index}: {line.Time:HH:mm:ss} {line.Service} - {line.Message}");
        }
    }
}
// prints:
// 142: 08:01:15 payments - gateway timeout
// 196: 08:01:16 payments - gateway timeout
```

- `match.Index` is the character position in the whole file, as a `long`. Use it to jump back to the entry later.
- For values you only read as strings, skip `Parse` and use `match.Groups`. The keys are the template's member names:

```csharp
using (StreamReader reader = File.OpenText("app.log"))
{
    List<string> services = AppLog.Line.Pattern.Matches(reader)
        .Select(match => match.Groups["Service"])
        .Distinct()
        .ToList();

    Console.WriteLine(string.Join(", ", services)); // prints: api, db, payments
}
```

For a file that fits in memory, the template alone is enough: `AppLog.Line.ParseAll(File.ReadAllText("app.log"))` lazily yields the same five entries as an `IEnumerable<AppLogLine>`. `AppLog.Line.Format(entry)` writes an entry back in the same format, with the declared enum name: `2026-11-02 08:00:01 [Info] api: started on port 8080`.

## Validating API input with rules and [MatchesPattern]

**Problem:** an order API takes a SKU in the URL and an order body with a card number and an optional coupon. The SKU must have the form `ABC-1234`, the card number must have a valid Luhn check digit, and invalid requests must get a `400` with one message per field.

Three pieces work together:

- **Patterns** declared once, as public static members.
- **A `Must` rule** for what a regex cannot check: the card's Luhn checksum (see [Semantic Rules](./semantic-rules.md)).
- **`[MatchesPattern]`** to validate the body with DataAnnotations, and the **`pattern` route constraint** from the `Masterly.FluentRegex.AspNetCore` package to validate the URL (see [ASP.NET Core](./aspnetcore.md)).

```csharp
using System.ComponentModel.DataAnnotations;
using Masterly.FluentRegex;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidation();
builder.Services.AddFluentPatterns(registry => registry.Add("sku", OrderPatterns.Sku));

WebApplication app = builder.Build();

app.MapGet("/products/{sku:pattern(sku)}", (string sku) => Results.Ok(new { sku }));

app.MapPost("/orders", (CreateOrderRequest request) => Results.Created($"/orders/{request.Sku}", request));

app.Run();

public static class OrderPatterns
{
    public static Pattern Sku => Pattern.With
        .UppercaseLetter.Repeat.Times(3)
        .Literal("-")
        .AsciiDigit.Repeat.Times(4);

    public static ConstrainedPattern Card => CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");

    public static Pattern Coupon => Pattern.With.UppercaseLetter.Repeat.Times(4).AsciiDigit.Repeat.Times(2);
}

public sealed class CreateOrderRequest
{
    [Required]
    [MatchesPattern(typeof(OrderPatterns), nameof(OrderPatterns.Sku))]
    public string? Sku { get; set; }

    [Required]
    [Display(Name = "Card number")]
    [MatchesPattern(typeof(OrderPatterns), nameof(OrderPatterns.Card))]
    public string? Card { get; set; }

    [MatchesPattern(typeof(OrderPatterns), nameof(OrderPatterns.Coupon), ErrorMessage = "{0} must be four capital letters and two digits.")]
    public string? Coupon { get; set; }
}
```

The project is an ASP.NET Core web project (`Microsoft.NET.Sdk.Web`) targeting .NET 10 with the `Masterly.FluentRegex` and `Masterly.FluentRegex.AspNetCore` packages. These are the real responses:

| Request | Status | Body or reason |
|---------|--------|----------------|
| `GET /products/KBD-1001` | 200 | `{"sku":"KBD-1001"}` |
| `GET /products/kbd-1001` | 404 | The route constraint rejects lowercase letters, so no endpoint matches. |
| `GET /products/KBD-10011` | 404 | The whole segment must match; a valid prefix is not enough. |
| `POST /orders` with `{"sku":"KBD-1001","card":"4111 1111 1111 1111"}` | 201 | `Location: /orders/KBD-1001` |
| `POST /orders` with `{"sku":"KBD-1001","card":"4111 1111 1111 1112","coupon":"save10"}` | 400 | See below |
| `POST /orders` with `{"sku":"KBD-10011"}` | 400 | See below |

```json
{"title":"One or more validation errors occurred.","errors":{"Card":["The field Card number is invalid: Luhn checksum."],"Coupon":["Coupon must be four capital letters and two digits."]}}
```

```json
{"title":"One or more validation errors occurred.","errors":{"Sku":["The field Sku does not match the required pattern."],"Card":["The Card number field is required."]}}
```

- **One source of truth.** The route and the body validate the SKU with the same `OrderPatterns.Sku`. The patterns have no `^` and `$`: both the attribute and the route constraint require the **whole** value to match.
- **Rule failures name the rule.** `4111 1111 1111 1112` has the right shape but a wrong check digit, so the message is `The field Card number is invalid: Luhn checksum.`, using the `[Display]` name.
- **Optional stays optional.** `[MatchesPattern]` treats `null` and empty strings as valid. `Coupon` may be left out; `Card` is mandatory because it also has `[Required]`.
- **`AddValidation()`** is the built-in validation for minimal APIs in .NET 10. It runs every DataAnnotations attribute on the bound `CreateOrderRequest`, including `[MatchesPattern]`, and returns the `400` for you. MVC controllers with `[ApiController]` do the same on every supported .NET version.
- `[MatchesPattern]` ships in the `net8.0` and `net10.0` builds of the core package. The route constraint needs .NET 8 or later.

### On .NET 8 minimal APIs

.NET 8 minimal APIs do not validate DataAnnotations by themselves, and `AddValidation()` does not exist there. Remove the `AddValidation()` call, run the validator in the endpoint and turn the results into a validation problem. This `MapPost` replaces the one in the previous sample; mapping `POST /orders` twice makes every request fail with an ambiguous-match error.

```csharp
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

// app is the WebApplication from the previous sample, built without AddValidation()
app.MapPost("/orders", (CreateOrderRequest request) =>
{
    Dictionary<string, string[]> errors = RequestValidation.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    return Results.Created($"/orders/{request.Sku}", request);
});

public static class RequestValidation
{
    public static Dictionary<string, string[]> Validate(object request)
    {
        List<ValidationResult> results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        return results
            .SelectMany(result => result.MemberNames.Select(member => (member, message: result.ErrorMessage ?? "Invalid value.")))
            .GroupBy(error => error.member, error => error.message)
            .ToDictionary(group => group.Key, group => group.ToArray());
    }
}
```

The invalid card and coupon from the table above then produce the response below. `Results.ValidationProblem` adds the `type` and `status` members. (We ran this code path on .NET 10 with `AddValidation()` removed.)

```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Card":["The field Card number is invalid: Luhn checksum."],"Coupon":["Coupon must be four capital letters and two digits."]}}
```

:::note

Pass `validateAllProperties: true`. Without it, `Validator.TryValidateObject` checks only `[Required]` and skips `[MatchesPattern]`. That is standard DataAnnotations behavior.

:::

## Redacting PII in logs

**Problem:** support logs contain customer e-mail addresses, card numbers, phone numbers and social security numbers. Before the logs are shipped to a log service, every sensitive value must be hidden, but enough must stay visible to help support: the e-mail domain and the last digits of the card.

```csharp
using System;
using System.IO;
using Masterly.FluentRegex;

using (StreamWriter output = File.CreateText("support.redacted.log"))
{
    foreach (string line in File.ReadLines("support.log"))
    {
        output.WriteLine(LogRedaction.Redactor.Mask(line));
    }
}

public static class LogRedaction
{
    public static readonly Redactor Redactor = Redactor.For()
        .Add(CommonPatterns.Email(), RedactionStyle.Email)
        .Add(CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum"), RedactionStyle.KeepLast(4))
        .Add(CommonPatterns.SocialSecurityNumber(), RedactionStyle.Label("[SSN]"))
        .Add(CommonPatterns.PhoneNumberE164(), RedactionStyle.KeepLast(2));
}
```

For a `support.log` that contains

```text
10:00:01 ticket 4411 opened by jane.doe@example.com
10:00:09 card on file 4111 1111 1111 1111, order 4111 1111 1111 1112
10:01:30 callback +14155552671, ssn 123-45-6789
10:02:00 ticket 4411 closed
```

`support.redacted.log` contains

```text
10:00:01 ticket 4411 opened by j***@example.com
10:00:09 card on file **** **** **** 1111, order 4111 1111 1111 1112
10:01:30 callback +*********71, ssn [SSN]
10:02:00 ticket 4411 closed
```

- `File.ReadLines` reads one line at a time, so the file size does not matter.
- The built-in patterns are anchored (`^...$`) for validation. The redactor turns those anchors into token boundaries, so it finds the values inside a sentence.
- `RedactionStyle.Email` keeps the first character and the domain. `KeepLast(4)` keeps the last four digits and the separators.
- The Luhn rule filters the card hits. The second number, `4111 1111 1111 1112`, has a card's shape but a wrong check digit, so it is an order number, not a card, and stays visible. The ticket number `4411` is untouched too.
- A `Redactor` is immutable and thread-safe. Build it once, as here, and share it.

To know what was hidden without logging the values themselves, call `Redact` instead of `Mask`. The report holds positions and styles only:

```csharp
RedactionReport report = LogRedaction.Redactor.Redact("callback +14155552671, ssn 123-45-6789");

Console.WriteLine(report.Text);           // prints: callback +*********71, ssn [SSN]
Console.WriteLine(report.HasRedactions);  // prints: True
Console.WriteLine(report);                // prints: Redacted 2 span(s): [9..21) KeepLast(2), [27..38) Label [SSN]
```

`report.Spans` lists one `RedactedSpan` per value, with its `Start`, `Length`, `Style`, `Label` and the index of the pattern that found it. [PII Redaction](./pii-redaction.md) covers every style, overlapping matches and the non-backtracking engine.

:::tip

Redact as close to the source as possible, for example in the logging pipeline or the exception handler, rather than in the log service. Text that was never written cannot leak.

:::

## Searching a database with EF Core

**Problem:** a product catalog lives in a database. You want to find products by pattern, keep invalid SKUs out of the table, and select products whose barcode has a valid checksum, a rule no SQL dialect can express.

The `Masterly.FluentRegex.EntityFrameworkCore` package translates patterns into the database's own regex dialect. This scenario runs on SQLite, so it needs no server. The same code runs on PostgreSQL, MySQL, MariaDB and SQL Server 2025 (and Azure SQL). Older SQL Server versions have no regex functions: there a pattern is translated only when it can be expressed as an exact `LIKE`, and otherwise the query throws with guidance. See [Entity Framework Core](./entity-framework-core.md) and [SQL Dialects](./sql-dialects.md) for what each one supports.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Masterly.FluentRegex;
using Masterly.FluentRegex.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

public static class CatalogPatterns
{
    public static readonly Pattern Sku = Pattern.With
        .StartOfLine
        .UppercaseLetter.Repeat.Times(3)
        .Literal("-")
        .AsciiDigit.Repeat.Times(4)
        .EndOfLine;

    public static readonly Pattern ModelNumber = Pattern.With
        .WordBoundary
        .UppercaseLetter.Repeat.Times(2)
        .AsciiDigit.Repeat.Times(3, 4)
        .WordBoundary;

    public static readonly ConstrainedPattern Ean13 = Pattern.With
        .StartOfLine
        .AsciiDigit.Repeat.Times(13)
        .EndOfLine
        .Must(Ean13Checksum, "EAN-13 checksum");

    private static bool Ean13Checksum(string value)
    {
        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            int digit = value[i] - '0';
            sum += i % 2 == 0 ? digit : digit * 3;
        }

        return (10 - sum % 10) % 10 == value[12] - '0';
    }
}

public sealed class Product
{
    public int Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Barcode { get; set; }
}

public sealed class CatalogContext : DbContext
{
    private readonly SqliteConnection _connection;

    public CatalogContext(SqliteConnection connection)
    {
        _connection = connection;
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) => options
        .UseSqlite(_connection)
        .UseFluentRegex();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().Property(product => product.Sku).HasPattern(CatalogPatterns.Sku);
    }
}
```

- `UseFluentRegex()` registers the translation. The SQL dialect is detected from the provider.
- `HasPattern(CatalogPatterns.Sku)` adds a database CHECK constraint generated from the same pattern.
- `AsciiDigit` is `[0-9]`. Prefer it over `Digit` (`\d`) in patterns that run in a database. In .NET, `\d` also matches the digits of other scripts, and the translation keeps that meaning: on PostgreSQL, `^\d{3}$` becomes a class of 37 Unicode digit ranges, while `^[0-9]{3}$` stays `[0-9]{3}`.

The project references `Masterly.FluentRegex.EntityFrameworkCore` and `Microsoft.EntityFrameworkCore.Sqlite`. Create the database and add four products:

```csharp
// Product, CatalogContext and CatalogPatterns are from the previous sample
using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

using (CatalogContext setup = new CatalogContext(connection))
{
    setup.Database.EnsureCreated();
    setup.Products.AddRange(
        new Product { Sku = "KBD-1001", Name = "Keyboard KB750", Barcode = "4006381333931" },
        new Product { Sku = "MSE-2040", Name = "Mouse MX3300", Barcode = "4006381333932" },
        new Product { Sku = "MON-3300", Name = "Monitor 27 inch", Barcode = null },
        new Product { Sku = "CBL-0007", Name = "USB cable", Barcode = "5901234123457" });
    setup.SaveChanges();
}

using CatalogContext db = new CatalogContext(connection);
```

### Filter in SQL

Call `IsMatch` inside a LINQ query. It is translated, so the database does the filtering:

```csharp
IQueryable<Product> withModel = db.Products.Where(product => CatalogPatterns.ModelNumber.IsMatch(product.Name));

Console.WriteLine(withModel.ToQueryString());
// prints:
// SELECT "p"."Id", "p"."Barcode", "p"."Name", "p"."Sku"
// FROM "Products" AS "p"
// WHERE "p"."Name" REGEXP '\b[A-Z]{2}[0-9]{3,4}\b'

List<Product> models = await withModel.ToListAsync();
Console.WriteLine(string.Join(", ", models.Select(product => product.Name))); // prints: Keyboard KB750, Mouse MX3300
```

When the library can prove facts about the pattern, it adds index-friendly hints. The SKU pattern can never match more than nine characters, so a length bound is added next to the regex:

```csharp
IQueryable<Product> skuQuery = db.Products.Where(product => CatalogPatterns.Sku.IsMatch(product.Sku));

Console.WriteLine(skuQuery.ToQueryString());
// prints:
// SELECT "p"."Id", "p"."Barcode", "p"."Name", "p"."Sku"
// FROM "Products" AS "p"
// WHERE "p"."Sku" REGEXP '^[A-Z]{3}-[0-9]{4}$' AND length("p"."Sku") <= 9
```

`!pattern.IsMatch(...)` works too. `db.Products.Where(product => !CatalogPatterns.ModelNumber.IsMatch(product.Name))` returns `Monitor 27 inch` and `USB cable`. `EF.Functions.Matches(product.Name, pattern)` is the same filter written as a database function.

### Rules that SQL cannot run

The barcode check needs the EAN-13 checksum, which is .NET code. `ToMatchingListAsync` splits the work: SQL applies a filter that is proven to keep every possible match, and .NET applies the exact pattern and its rules to the rows that come back.

```csharp
List<Product> validBarcodes = await db.Products.ToMatchingListAsync(product => product.Barcode, CatalogPatterns.Ean13);

Console.WriteLine(string.Join(", ", validBarcodes.Select(product => $"{product.Sku} {product.Barcode}")));
// prints: KBD-1001 4006381333931, CBL-0007 5901234123457
```

- `4006381333932` has thirteen digits but a wrong check digit, so the mouse is not returned. The monitor's `NULL` barcode never matches.
- `MatchingAsync` is the streaming form. It returns an `IAsyncEnumerable<Product>` for `await foreach`.
- Calling `CatalogPatterns.Ean13.IsMatch(...)` inside `Where` throws `InvalidOperationException`: `A ConstrainedPattern cannot be translated to SQL because its Must rules run only in .NET. Query with the underlying pattern (constrained.Pattern), or use MatchingAsync(selector, constrained), which applies a proven SQL prefilter and then checks every rule in .NET.`

### The CHECK constraint

`HasPattern` makes the database itself refuse bad SKUs, even when they are written by another application:

```csharp
db.Products.Add(new Product { Sku = "kbd-1", Name = "Broken" });

try
{
    await db.SaveChangesAsync();
}
catch (DbUpdateException exception)
{
    Console.WriteLine(exception.InnerException?.Message);
    // prints: SQLite Error 19: 'CHECK constraint failed: CK_Products_Sku_Pattern'.
}
```

The constraint has the same meaning as `Pattern.IsMatch` and as `[MatchesPattern]` on a model, so the database and your validators never disagree.

:::note

Regex **replacement** in SQL depends on the dialect. On SQLite, `EF.Functions.Replace(product.Name, CatalogPatterns.ModelNumber, "[model]")` throws `NotSupportedException`: `Regular expression replacement cannot be translated on Sqlite. Replace is translated on PostgreSQL, MySQL, MariaDB and SQL Server 2025; load the value and use Pattern.Replace in .NET instead.` Load the names and call `CatalogPatterns.ModelNumber.Replace(name, "[model]")`, which gives `Keyboard [model], Mouse [model]`.

:::

## Guarding a user-supplied regex

**Problem:** a search box lets users type their own regular expression. A user, by mistake or on purpose, can enter a pattern that never finishes on some input (ReDoS) and blocks a server thread. The pattern must be checked before it runs, and every run must be bounded.

Four layers make this safe:

1. **Parse** the text with `Pattern.FromRegex`. Invalid syntax throws `ArgumentException`.
2. **Analyze** it with `AnalyzeSafety()`. Reject patterns with exponential backtracking.
3. **Bound** every run with `WithTimeout`. A run that takes too long throws `RegexMatchTimeoutException`.
4. **Limit** the size of the pattern and the number of results.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

public sealed record SearchOutcome(bool Accepted, string Detail, IReadOnlyList<string> Matches);

public static class UserRegexSearch
{
    private const int MaxRegexLength = 200;
    private const int MaxResults = 100;
    private static readonly TimeSpan MatchBudget = TimeSpan.FromMilliseconds(100);

    public static SearchOutcome Search(string userRegex, string text)
    {
        if (userRegex.Length > MaxRegexLength)
        {
            return Reject($"The pattern is longer than {MaxRegexLength} characters.");
        }

        Pattern pattern;
        try
        {
            pattern = Pattern.FromRegex(userRegex);
        }
        catch (ArgumentException exception)
        {
            return Reject(exception.Message);
        }

        SafetyReport report = pattern.AnalyzeSafety();
        if (report.Risk == RiskLevel.High)
        {
            return Reject($"The pattern can backtrack exponentially: {report.Issues[0].Fragment}");
        }

        Pattern guarded = pattern.WithTimeout(MatchBudget);
        try
        {
            List<string> matches = guarded.Matches(text)
                .Take(MaxResults)
                .Select(match => match.Value)
                .ToList();

            return new SearchOutcome(true, $"{matches.Count} match(es), risk {report.Risk}", matches);
        }
        catch (RegexMatchTimeoutException)
        {
            return Reject($"The search took longer than {MatchBudget.TotalMilliseconds} ms.");
        }
    }

    private static SearchOutcome Reject(string reason) => new SearchOutcome(false, reason, Array.Empty<string>());
}
```

Five searches, as a user would send them:

```csharp
string orders = "Orders KBD-1001 and MSE-2040 shipped; MON-3300 is late.";
string digits = new string('1', 20_000);

SearchOutcome valid = UserRegexSearch.Search(@"[A-Z]{3}-\d{4}", orders);
// accepted: 3 match(es), risk None: KBD-1001, MSE-2040, MON-3300

SearchOutcome broken = UserRegexSearch.Search(@"[A-Z]{3}-(\d{4}", orders);
// rejected: The regular expression is invalid: Invalid pattern '[A-Z]{3}-(\d{4}' at offset 15. Not enough )'s. (Parameter 'regex')

SearchOutcome exponential = UserRegexSearch.Search(@"^(\w+\s?)*$", orders);
// rejected: The pattern can backtrack exponentially: (\w+\s?)*

SearchOutcome ranges = UserRegexSearch.Search(@"(\d+)\s*-\s*(\d+)", "Read pages 10-20 and 30 - 45.");
// accepted: 2 match(es), risk None: 10-20, 30 - 45

SearchOutcome slow = UserRegexSearch.Search(@"(\d+)\s*-\s*(\d+)", digits);
// rejected: The search took longer than 100 ms.
```

- `FromRegex` reports the syntax error with its position. The message is safe to show to the user who typed the pattern.
- `^(\w+\s?)*$` is the classic catastrophic pattern. The analyzer finds the nested quantifier without running anything, so it is rejected before it can hurt.
- The last search shows why the timeout is still needed. The analyzer rates `(\d+)\s*-\s*(\d+)` as `None`, and it is fine on normal text. But on 20,000 digits without a `-`, the engine retries `\d+` from every start position, and the work grows with the square of the input length. In our run it hit the 100 ms budget. Static analysis finds exponential and polynomial backtracking in the pattern's structure; it does not bound the total cost of a search over a large input. The timeout does.
- The policy here rejects only `High` risk. `Medium` risk (polynomial backtracking) is allowed because the timeout bounds it. Reject `Medium` too (`!report.IsSafe`) if you prefer to be strict.
- `MatchBudget` applies to each matching call, and `Take(MaxResults)` stops the search early. Both keep one request from monopolizing a thread.

### With the non-backtracking engine

On .NET 8 and later, `UseNonBacktracking()` switches to the .NET engine whose time is linear in the input length, so no pattern can backtrack catastrophically. It does not support backreferences, lookarounds, atomic groups or conditionals; such a pattern throws `NotSupportedException` when it first runs.

```csharp
// SearchOutcome is from the previous sample
public static class LinearUserRegexSearch
{
    private static readonly TimeSpan MatchBudget = TimeSpan.FromMilliseconds(100);

    public static SearchOutcome Search(string userRegex, string text)
    {
        Pattern pattern;
        try
        {
            pattern = Pattern.FromRegex(userRegex).UseNonBacktracking().WithTimeout(MatchBudget);
        }
        catch (ArgumentException exception)
        {
            return new SearchOutcome(false, exception.Message, Array.Empty<string>());
        }

        try
        {
            List<string> matches = pattern.Matches(text).Take(100).Select(match => match.Value).ToList();
            return new SearchOutcome(true, $"{matches.Count} match(es)", matches);
        }
        catch (NotSupportedException)
        {
            return new SearchOutcome(false, "Backreferences, lookarounds and atomic groups are not allowed.", Array.Empty<string>());
        }
        catch (RegexMatchTimeoutException)
        {
            return new SearchOutcome(false, $"The search took longer than {MatchBudget.TotalMilliseconds} ms.", Array.Empty<string>());
        }
    }
}
```

```csharp
// digits is from the previous sample
SearchOutcome linear = LinearUserRegexSearch.Search(@"(\d+)\s*-\s*(\d+)", digits);
// accepted: 0 match(es); in our run the whole call took about 25 ms instead of timing out

SearchOutcome noBlowUp = LinearUserRegexSearch.Search(@"^(\w+\s?)*$", "00000000000000000000000000000000!");
// accepted: 0 match(es)

SearchOutcome backreference = LinearUserRegexSearch.Search(@"(\w)\1", "aabb");
// rejected: Backreferences, lookarounds and atomic groups are not allowed.

SearchOutcome lookahead = LinearUserRegexSearch.Search(@"\d+(?=px)", "12px");
// rejected: Backreferences, lookarounds and atomic groups are not allowed.
```

Choose by what your users need. The non-backtracking engine accepts more patterns safely, including the catastrophic one, but supports fewer constructs. The backtracking engine with analysis and a timeout supports every construct but rejects some patterns. In both cases, keep the timeout. See [ReDoS Safety](./redos-safety.md) and [Matching](./matching.md) for the details.

## A tour of the showcase sample

The repository has a runnable tour of the library in `samples/Masterly.FluentRegex.Showcase`. Run it from the repository root:

```bash
dotnet run --project samples/Masterly.FluentRegex.Showcase
```

It prints ten numbered sections. Sections 3 (typed extraction), 4 (fluent replacements) and 6 (ReDoS analysis) are covered by the scenarios above. The other sections are summarized here, with their real output.

### Building blocks and operators

Patterns are immutable values. `+` concatenates two patterns and `|` builds an alternation without a capture group:

```csharp
using System;
using Masterly.FluentRegex;

Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;
Pattern price = Pattern.With.Literal("$") + digits + Pattern.With.Literal(".").Digit.Repeat.Times(2);
Pattern animal = Pattern.With.Literal("cat") | Pattern.With.Literal("dog") | Pattern.With.Literal("bird");

Console.WriteLine(digits);                       // prints: \d+
Console.WriteLine(price);                        // prints: \$\d+\.\d{2}
Console.WriteLine(animal);                       // prints: (?:cat|dog|bird)
bool hasPrice = price.IsMatch("Total: $42.50");  // true
```

`digits` is still `\d+` after it was used inside `price`. See [Composing Patterns](./composing-patterns.md).

### From a legacy regex to fluent code

`ToFluentCode()` turns an existing regex into the fluent code that rebuilds it, which is a quick way to make an old pattern readable and reviewable:

```csharp
using System.Text.RegularExpressions;

const string legacy = @"^(?<user>[\w.+-]+)@(?<domain>(?:[a-z0-9-]+\.)+[a-z]{2,})$";

Console.WriteLine(Pattern.FromRegex(legacy, RegexOptions.IgnoreCase).ToFluentCode());
```

```text
Pattern.With
    .StartOfLine
    .NamedGroup("user", Pattern.With.Set(Pattern.With.Word.Literal(".+-")).Repeat.OneOrMore)
    .Literal("@")
    .NamedGroup(
        "domain",
        Pattern.With
            .NonCapturingGroup(
                Pattern.With
                    .Set(Pattern.With.LowercaseLetter.Range('0', '9').Literal("-")).Repeat.OneOrMore
                    .Literal(".")).Repeat.OneOrMore
            .LowercaseLetter.Repeat.AtLeast(2))
    .EndOfLine
    .WithOptions(RegexOptions.IgnoreCase)
```

See [Regex to Fluent Code](./regex-to-fluent-code.md).

### Test data

`GenerateMatching` and `GenerateNonMatching` produce inputs for unit tests. With a seed, the output is the same on every run:

```csharp
string samples = string.Join("  ", CommonPatterns.IPv4().GenerateMatching(5, seed: 42));
// 127.253.251.243  254.11.253.8  250.70.207.202  250.235.240.250  6.225.247.255

string nearMisses = string.Join("  ", CommonPatterns.IPv4().GenerateNonMatching(5, seed: 42));
// 25411.253.8  255.05Z5.229.241  x255.055.229.241  #6.225.247.255  250
```

The non-matching values are near misses: valid addresses with one small change, which are the inputs most likely to expose a bug. See [Test Data Generation](./test-data-generation.md).

### Explaining a failed match

`Trace` shows how far a pattern got and why it stopped:

```csharp
Console.WriteLine(CommonPatterns.Time24().Trace("25:00"));
```

```text
Pattern: ^([01]\d|2[0-3]):[0-5]\d(?::[0-5]\d)?$
Input:   "25:00"
Result:  NO MATCH

Failed at position 1: expected [0-3] (one of [0-3])
Found:   "5:00"

  25:00
   ^
```

The showcase traces `CommonPatterns.DateTimeIso()` with `2024-05-17T25:00`, which prints the full tree of what matched before the failure. See [Match Tracing](./match-tracing.md). Section 8 of the showcase also writes railroad diagrams and a Mermaid flowchart; see [Diagrams](./diagrams.md).

### Compile-time generated regexes

Mark a static `Pattern` property of a `partial` class with `[GeneratePattern]`. The source generator in the package evaluates the pattern while you build, and generates a constant with the regex text and a method that returns a cached `Regex`:

```csharp
public static partial class ProductCodes
{
    private static Pattern Prefix => Pattern.With.UppercaseLetter.Repeat.Times(3);

    /// <summary>A product code such as ABC-1234.</summary>
    [GeneratePattern]
    public static Pattern ProductCode => Pattern.With.StartOfLine.Then(Prefix).Literal("-").Digit.Repeat.Times(4).EndOfLine;
}
```

```csharp
Console.WriteLine(ProductCodes.ProductCodeSource);                   // prints: ^[A-Z]{3}-\d{4}$
bool valid = ProductCodes.ProductCodeRegex().IsMatch("ABC-1234");    // true
bool invalid = ProductCodes.ProductCodeRegex().IsMatch("abc-12");    // false
```

The generated names are the property name plus `Source` and `Regex`. A pattern the generator cannot evaluate, or a class that is not `partial`, is reported as a build diagnostic instead of failing at run time. See [Compile-Time Checks](./compile-time-checks.md).

### Allocation-free span APIs

On .NET 8 and later, `EnumerateMatches` and `Count` work on a `ReadOnlySpan<char>` without allocating `Match` objects:

```csharp
using System.Text.RegularExpressions;

// digits is the pattern from "Building blocks and operators"
ReadOnlySpan<char> text = "order 17, order 204, order 3".AsSpan();

foreach (ValueMatch match in digits.EnumerateMatches(text))
{
    Console.Write($"{text.Slice(match.Index, match.Length).ToString()} ");
}
// prints: 17 204 3

int count = digits.Count(text); // 3
```

See [Matching](./matching.md).

## Related pages

- [Common Patterns](./common-patterns.md): every ready-made pattern used on this page.
- [Typed Extraction](./typed-extraction.md) and [Typed Templates](./templates.md): binding text to objects.
- [Streaming Large Input](./streaming.md): the limits and options of streaming.
- [Semantic Rules](./semantic-rules.md): `Must`, the built-in `Validators` and `[MatchesPattern]`.
- [PII Redaction](./pii-redaction.md): every redaction style and the report.
- [Entity Framework Core](./entity-framework-core.md): every translation and dialect.
- [ReDoS Safety](./redos-safety.md): the analyzer, confirmation runs and fixes.
- [Best Practices](./best-practices.md): habits that keep patterns correct and fast.
