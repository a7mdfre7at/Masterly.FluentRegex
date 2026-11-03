---
id: validation-and-debugging
title: "Validation, Explain and Debugging"
description: "Check that a pattern compiles, read the error, inspect its groups, explain it in plain English and see it in the debugger."
---

A fluent chain always produces regex text, but that text is not always valid. Raw regex added with `RegEx(...)`, text loaded from a settings file and patterns typed by users can all fail to compile. This page covers the members that help you find and understand those problems before they reach production:

| Member | Returns | Use it when |
|--------|---------|-------------|
| `IsValid()` | `bool` | You only need a yes/no answer. |
| `TryValidate(out string errorMessage)` | `bool`, plus the error message | You want to show or log why the pattern is invalid. |
| `GetGroupNames()` | `string[]` | You need the names of the named groups, for example to check them before binding. |
| `GetGroupCount()` | `int` | You need the number of capturing groups. |
| `Explain()` | `string` | You want a plain-English, line-by-line description of the pattern. |
| Debugger display | Shown in the IDE | You are stepping through code and want to see what a `Pattern` variable holds. |

All of these members live on `Pattern` in the `Masterly.FluentRegex` namespace. They are available on every target framework of the package: `netstandard2.1`, `net8.0` and `net10.0`.

:::tip

You do not need these runtime checks for patterns written in your code. The package ships Roslyn analyzers that build constant fluent chains at compile time. An invalid one fails the build with `FRX003`, for example: `The pattern produces an invalid regular expression '[unclosed': Invalid pattern '[unclosed' at offset 9. Unterminated [] set.` Use the members on this page for regex text that only exists at run time. See [Compile-time checks](./compile-time-checks.md).

:::

## Checking that a pattern is valid

### IsValid

`IsValid()` tries to compile the pattern and tells you whether it worked.

```csharp
using System;
using Masterly.FluentRegex;

// regexText comes from your application; in this example it is "[unclosed"
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;
Pattern configured = Pattern.With.RegEx(regexText);

Console.WriteLine(digits.IsValid());     // True
Console.WriteLine(configured.IsValid()); // False
```

- `IsValid()` never throws for a bad pattern. It catches the `ArgumentException` that `Regex` throws and returns `false`.
- An empty pattern (`Pattern.With`) is valid. It compiles to the empty regex, which matches everywhere.
- Each call compiles a new `Regex`. It does not use or fill the cached regex that `IsMatch` and the other matching members use. Validate once, for example when you load the configuration, not on every request.

### TryValidate and the error message

`TryValidate` does the same check and also gives you the error message from the .NET regex parser.

```csharp
using System;
using Masterly.FluentRegex;

// regexText comes from your application; in this example it is @"(?<year>\d{4}"
Pattern pattern = Pattern.With.RegEx(regexText);

if (!pattern.TryValidate(out string? error))
{
    Console.WriteLine($"Invalid pattern: {error}");
}
// prints: Invalid pattern: Invalid pattern '(?<year>\d{4}' at offset 13. Not enough )'s.
```

- `error` is `null` when the method returns `true`.
- The message comes from the .NET runtime, not from this library. Its wording depends on the runtime your application runs on.
- The offset in the message counts characters from the start of the whole regex text, not from the start of the part you added with `RegEx(...)`.

These are the messages .NET 10 gives for common mistakes:

| Regex text | Message |
|------------|---------|
| `[unclosed` | `Invalid pattern '[unclosed' at offset 9. Unterminated [] set.` |
| `(unclosed` | `Invalid pattern '(unclosed' at offset 9. Not enough )'s.` |
| `a)` | `Invalid pattern 'a)' at offset 2. Too many )'s.` |
| `a{2,1}` | `Invalid pattern 'a{2,1}' at offset 6. Illegal {x,y} with x > y.` |
| `x**` | `Invalid pattern 'x**' at offset 3. Nested quantifier '*'.` |
| `\` | `Invalid pattern '\' at offset 1. Illegal \ at end of pattern.` |
| `[z-a]` | `Invalid pattern '[z-a]' at offset 4. [x-y] range in reverse order.` |
| `(?<1a>x)` | `Invalid pattern '(?<1a>x)' at offset 4. Invalid group name: Group names must begin with a word character.` |
| `\k<nope>` | `Invalid pattern '\k<nope>' at offset 8. Reference to undefined group name 'nope'.` |
| `\p{Nope}` | `Invalid pattern '\p{Nope}' at offset 8. Unknown property 'Nope'.` |

:::warning

The message repeats the regex text. If the text came from a user, treat the message as user input too: encode it before you put it in HTML, and think twice before you write it to a shared log.

:::

### Validating and wrapping in one step

When the regex text arrives as a string, `Pattern.FromRegex(regex, options)` validates it and wraps it in a `Pattern` in one call. Unlike `RegEx(...)`, it fails immediately instead of returning a pattern that fails later.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

// regexText comes from your application; in this example it is "a{2,1}"
try
{
    Pattern pattern = Pattern.FromRegex(regexText, RegexOptions.IgnoreCase);
}
catch (ArgumentException exception)
{
    Console.WriteLine(exception.Message);
}
// prints: The regular expression is invalid: Invalid pattern 'a{2,1}' at offset 6. Illegal {x,y} with x > y. (Parameter 'regex')
```

- `FromRegex` validates with the options you pass, so it catches options that do not combine. `RegexOptions.ECMAScript | RegexOptions.Singleline` throws `ArgumentException` with the message `The regular expression is invalid: Specified argument was out of the range of valid values. (Parameter 'options') (Parameter 'regex')`.
- A `null` regex throws `ArgumentNullException`.
- It does not catch `NotSupportedException`. `RegexOptions.NonBacktracking` with a backreference such as `(a)\1` throws `NotSupportedException: RegexOptions.NonBacktracking is not supported in conjunction with expressions containing: 'backreference (\ number)'.` Catch both exception types when the options come from configuration.
- See [Regex to fluent code](./regex-to-fluent-code.md) for what you can do with the wrapped pattern.

### Finding the exact position of the error

`TryValidate` gives you only the message. On .NET 7 and later, the exception that `Regex` throws is the public `RegexParseException`, whose `Error` and `Offset` properties tell you what went wrong and where. Call `ToRegex()` and catch it when you want to point at the bad character, for example in an editor.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

// regexText comes from your application; in this example it is @"(?<year>\d{4}"
Pattern pattern = Pattern.With.RegEx(regexText);

try
{
    Regex regex = pattern.ToRegex();
}
catch (RegexParseException exception)
{
    Console.WriteLine($"{exception.Error} at offset {exception.Offset}");
}
// prints: InsufficientClosingParentheses at offset 13
```

- `RegexParseException` derives from `ArgumentException`, so a `catch (ArgumentException)` block catches it too.
- `RegexParseException` is a public type of the .NET 7+ runtime. Code that targets `netstandard2.1` (or .NET 6 and earlier) cannot reference it. Catch `ArgumentException` there.

### What validation does not check

`IsValid()` and `TryValidate()` compile the regex text with **default options**. They ignore the options you set with `WithOptions(...)` or `UseNonBacktracking()`. That gives wrong answers in both directions:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

// regexText comes from your application; in this example it is "a # [ comment"
Pattern commented = Pattern.With.RegEx(regexText).WithOptions(RegexOptions.IgnorePatternWhitespace);

Console.WriteLine(commented.IsValid());     // False
Console.WriteLine(commented.IsMatch("a"));  // True

Pattern backreference = Pattern.With.Group(Pattern.With.Word).Backreference(1).UseNonBacktracking();

Console.WriteLine(backreference.IsValid()); // True
backreference.IsMatch("aa");                // throws NotSupportedException
```

- With `IgnorePatternWhitespace`, `# [ comment` is a comment. Without it, `[` opens a character set that is never closed. `IsValid()` reports a valid pattern as invalid.
- The non-backtracking engine does not support backreferences, lookarounds, atomic groups or conditionals. `IsValid()` reports the pattern as valid, and the first match throws. `UseNonBacktracking()` exists only on `net8.0` and `net10.0`.
- Options that cannot be combined, such as `RegexOptions.ECMAScript | RegexOptions.Singleline`, also pass `IsValid()` and then throw `ArgumentOutOfRangeException` on first use.

To validate with the pattern's own options and timeout, compile it with `ToRegex()`:

```csharp
using System;
using Masterly.FluentRegex;

// commented and backreference are the patterns from the previous sample
Console.WriteLine(PatternChecks.TryCompile(commented, out string? first));      // True
Console.WriteLine(PatternChecks.TryCompile(backreference, out string? second)); // False
Console.WriteLine(second);
// prints: RegexOptions.NonBacktracking is not supported in conjunction with expressions containing: 'backreference (\ number)'.

public static class PatternChecks
{
    /// <summary>Compiles the pattern with its own options and timeout, and reports why it fails.</summary>
    public static bool TryCompile(Pattern pattern, out string? error)
    {
        try
        {
            _ = pattern.ToRegex();
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException)
        {
            error = exception.Message;
            return false;
        }
    }
}
```

- `ToRegex()` returns a new `Regex` built with the pattern's options and timeout. It does not fill the cache that `IsMatch` uses.
- `ToRegex(RegexOptions options)` compiles with the options you pass instead of the pattern's own options. The pattern's timeout still applies. Use it to check text against the options it will run with, for example `Pattern.With.RegEx(regexText).ToRegex(RegexOptions.IgnorePatternWhitespace)` compiles `a # [ comment` without an error.
- Catch `NotSupportedException` as well as `ArgumentException`. The non-backtracking engine reports unsupported constructs with `NotSupportedException`.

:::note

Validation checks syntax only. A valid pattern can still be slow or match the wrong text. Run [`AnalyzeSafety()`](./redos-safety.md) on patterns that come from outside your code, and set a match timeout with `WithTimeout(...)`.

:::

### What an invalid pattern does

Building an invalid pattern never throws. At run time, `RegEx(regexText)` with the text `[unclosed` returns a pattern, and `ToString()` returns its text. (Written as a constant, the same call is rejected at build time by `FRX003`.) The error shows up when a member needs to compile or parse it:

| Member | With an invalid pattern |
|--------|------------------------|
| `ToString()` | Returns the text. Never throws. |
| `Explain()` | Explains the text character by character. Never throws. See [Explaining a pattern](#explaining-a-pattern). |
| `IsValid()`, `TryValidate(...)` | Return `false`. |
| `IsMatch`, `Match`, `Matches`, `Replace`, `Split`, `ToRegex()` | Throw `ArgumentException` (a `RegexParseException` on .NET 7 and later) with the parser's message. The failed regex is not cached, so every call throws again. |
| `GetGroupNames()`, `GetGroupCount()` | Throw the same `ArgumentException`. |
| `ToFluentCode()`, `Trace(...)`, `AnalyzeSafety()`, `ToMermaid()` and the other analysis members | Throw `InvalidOperationException`, for example `The pattern is not valid regex syntax: Unterminated [] set (at position 0)`. |
| `Pattern.FromRegex(...)` | Throws `ArgumentException` when you create the pattern. |

The analysis members use the library's own regex parser. Its message is shorter, and its position points at the start of the construct that is broken (here the `[` at position 0), not at the place where the .NET parser gave up.

## Inspecting groups

### GetGroupNames

`GetGroupNames()` returns the names of the named groups, in the order they appear in the pattern.

```csharp
using System;
using Masterly.FluentRegex;

Pattern phone = Pattern.With
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .Literal("-")
    .Group(Pattern.With.Digit.Repeat.Times(4))
    .NonCapturingGroup(Pattern.With.Literal(" ext").Digit.Repeat.OneOrMore).Repeat.Optional;

string[] names = phone.GetGroupNames();
int count = phone.GetGroupCount();

Console.WriteLine(phone);                    // (?<area>\d{3})-(\d{4})(?: ext\d+)?
Console.WriteLine(string.Join(", ", names)); // area
Console.WriteLine(count);                    // 2
```

- Numbered groups created with `Group(...)` have no name and are not listed.
- A group with a numeric name, such as `(?<5>a)` added with `RegEx(...)`, is treated as a numbered group and is not listed either.
- A name used twice, as in `(?<d>a)|(?<d>b)`, is listed once.
- A pattern without named groups returns an empty array, never `null`.

### GetGroupCount

`GetGroupCount()` returns the number of capturing groups. It does not count group 0, which is always the whole match.

- Named groups and numbered groups count. In the sample above, `area` and `(\d{4})` give 2.
- Non-capturing groups, lookaheads, lookbehinds and atomic groups do not count.
- A numbered group that shares its number with another group counts once. `(?<d>a)|(?<d>b)` has 1 group.

Two cases often surprise people:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern choice = Pattern.With.Choice(Pattern.With.Literal("cat"), Pattern.With.Literal("dog"));
Pattern alternation = Pattern.With.Literal("cat") | Pattern.With.Literal("dog");

Console.WriteLine(choice);                      // (cat|dog)
Console.WriteLine(choice.GetGroupCount());      // 1
Console.WriteLine(alternation);                 // (?:cat|dog)
Console.WriteLine(alternation.GetGroupCount()); // 0

Pattern date = Pattern.With
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
    .Group(Pattern.With.Literal("-"))
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2));

Console.WriteLine(string.Join(", ", date.GetGroupNames()));                        // year, month
Console.WriteLine(date.GetGroupCount());                                           // 3
Console.WriteLine(date.WithOptions(RegexOptions.ExplicitCapture).GetGroupCount()); // 2
```

- `Choice(...)` wraps the alternatives in a **capturing** group, so it adds a group. The `|` operator builds a non-capturing group instead. See [Composing patterns](./composing-patterns.md).
- Unlike `IsValid()`, the group members compile the pattern with its own options. With `RegexOptions.ExplicitCapture`, unnamed groups do not capture, so they are not counted.

:::warning

`GetGroupNames()` and `GetGroupCount()` throw `ArgumentException` when the pattern is invalid. Because they use the pattern's own options, they also throw `NotSupportedException` for a `UseNonBacktracking()` pattern with a backreference, even though `IsValid()` returned `true` for it. Check the pattern first when the regex text comes from outside your code, preferably with `ToRegex()` as shown in [What validation does not check](#what-validation-does-not-check).

:::

### Checking groups before you bind them

Typed extraction fills properties from groups with the same name. When the pattern comes from configuration, check that the groups you need exist:

```csharp
using System;
using System.Linq;
using Masterly.FluentRegex;

// pattern comes from your application; in this example it is ^(?<user>[\w.]+)@(?<domain>[\w.-]+)$
string[] required = { "user", "host" };
string[] missing = required.Except(pattern.GetGroupNames()).ToArray();

if (missing.Length > 0)
{
    Console.WriteLine($"The pattern has no group named: {string.Join(", ", missing)}");
}
// prints: The pattern has no group named: host
```

Group names are case-sensitive, so `Host` and `host` are different groups. When the pattern is written in your code and you bind it to a type with `Extract<T>`, `TryExtract<T>` or `ExtractAll<T>`, the `FRX008` analyzer warns at compile time about a target member that has no matching group. See [Typed extraction](./typed-extraction.md).

## Explaining a pattern

`Explain()` returns a plain-English description of the pattern, one line per construct. Use it in logs, in code review, or to understand a regex someone else wrote.

```csharp
using System;
using Masterly.FluentRegex;

Pattern pattern = Pattern.With
    .StartOfLine
    .NamedGroup("digits", Pattern.With.Digit.Repeat.OneOrMore)
    .Literal("-")
    .Word.Repeat.ZeroOrMore.Lazy
    .EndOfLine;

Console.WriteLine(pattern.Explain());
```

Output:

```text
Pattern: ^(?<digits>\d+)-\w*?$

Explanation:
  - Start of line/string
  - Named capturing group 'digits'
    - Digit (any Unicode decimal digit)
    - One or more
    - End of group
  - Character '-'
  - Word character (any Unicode letter, digit or _)
  - Zero or more (lazy)
  - End of line/string
```

- The first line is the regex text. A blank line and `Explanation:` follow.
- Each construct gets its own line. A quantifier gets its own line right after the thing it repeats.
- The contents of a group are indented two more spaces. `End of group` is printed at the indentation of the group's contents, and the lines after it move back out.
- Every line ends with `Environment.NewLine`, including the last one.
- An empty pattern returns only the header: `Pattern: `, a blank line and `Explanation:`.
- `Explain()` builds a new string on every call. It parses the pattern once with `Regex` to learn its groups, and still works when the pattern is invalid.

Groups nest as deeply as the pattern does:

```csharp
using System;
using Masterly.FluentRegex;

Pattern quoted = Pattern.With
    .NamedGroup("quote", Pattern.With.AnyOf("'\""))
    .Anything.Repeat.ZeroOrMore.Lazy
    .Backreference("quote")
    .NegativeLookahead(Pattern.With.Word);

Console.WriteLine(quoted.Explain());
```

Output:

```text
Pattern: (?<quote>['"]).*?\k<quote>(?!\w)

Explanation:
  - Named capturing group 'quote'
    - Any character in: ['"]
    - End of group
  - Any character (except newline)
  - Zero or more (lazy)
  - Backreference to named group 'quote'
  - Negative lookahead (must NOT follow)
    - Word character (any Unicode letter, digit or _)
    - End of group
```

### What each construct is called

| Regex | Explanation line |
|-------|------------------|
| `^`, `$` | `Start of line/string`, `End of line/string` |
| `.` | `Any character (except newline)` |
| `*`, `*?` | `Zero or more`, `Zero or more (lazy)` |
| `+`, `+?` | `One or more`, `One or more (lazy)` |
| `?`, `??` | `Optional (zero or one)`, `Optional (lazy)` |
| `{3}` | `Exactly 3 times` |
| `{2,}` | `At least 2 times` |
| `{2,5}` | `Between 2 and 5 times` |
| `{...}?` | The same text followed by ` (lazy)`, for example `Between 2 and 5 times (lazy)` |
| Any other `{`, such as in `{,3}` | `Character '{'`. .NET reads it as a literal brace, not a quantifier. |
| `\d`, `\D` | `Digit (any Unicode decimal digit)`, `Non-digit`. With `RegexOptions.ECMAScript`: `Digit [0-9]`, `Non-digit [^0-9]`. |
| `\w`, `\W` | `Word character (any Unicode letter, digit or _)`, `Non-word character`. With `RegexOptions.ECMAScript`: `Word character [a-zA-Z0-9_]`. |
| `\s`, `\S` | `Whitespace`, `Non-whitespace` |
| `\b`, `\B` | `Word boundary`, `Non-word boundary` |
| `\A`, `\z`, `\Z`, `\G` | `Start of string`, `End of string`, `End of string (or before a final newline)`, `Where the previous match ended` |
| `\t`, `\n`, `\r`, `\f`, `\v`, `\a`, `\e` | `Tab`, `Newline`, `Carriage return`, `Form feed`, `Vertical tab`, `Bell character`, `Escape character (U+001B)` |
| `\x41`, `é`, `\101`, `\cM` | `Character 'A' (U+0041)`, `Character 'é' (U+00E9)`, `Character 'A' (U+0041)`, `Control character Ctrl+M`. Invisible characters show only the code point, such as `Character U+000A`. |
| `\p{Lu}`, `\P{Lu}` | `Any character in Unicode category or block 'Lu'`, `Any character NOT in Unicode category or block 'Lu'` |
| `\` followed by digits | `Backreference to group 12` when that group exists. A single digit is always a backreference. Otherwise the digits are an octal escape, as in .NET: `\12` with fewer than 12 groups is `Character U+000A`. `\0` is always octal. |
| `\k<name>`, `\k'name'` | `Backreference to named group 'name'` |
| Any other escape, such as `\.` | `Escaped literal '.'` |
| `[a-z]`, `[^a-z]` | `Any character in: [a-z]`, `Any character NOT in: [a-z]`. Escaped `]`, a `]` right after `[`, and subtractions such as `[a-z-[aeiou]]` stay inside the set. |
| `(` | `Capturing group` |
| `(?<name>`, `(?'name'` | `Named capturing group 'name'` |
| `(?<close-open>` | `Balancing group 'close' (captures the text since the last capture of 'open' and removes it)` |
| `(?:` | `Non-capturing group` |
| `(?=`, `(?!` | `Positive lookahead (must follow)`, `Negative lookahead (must NOT follow)` |
| `(?<=`, `(?<!` | `Positive lookbehind (must precede)`, `Negative lookbehind (must NOT precede)` |
| `(?>` | `Atomic group (no backtracking)` |
| `(?i:`, `(?i)` | `Group with inline options (on: ignore case)`, `Inline options for the rest of the group (on: ignore case)`. Turned-off options are listed after `off:`. |
| `(?#note)` | `Comment: note` |
| `(?(1)`, `(?(name)` | `Conditional: if group 1 has matched, use the first branch`. The `\|` that starts the other branch is shown as `Otherwise`. |
| `(?(?=...)` | `Conditional: if the lookaround below matches, use the first branch`, followed by the lookaround itself |
| `)` | `End of group` |
| `\|` | `OR (alternation)` |
| A letter or digit (any script, such as `é`) | `Literal 'a'` |
| Any other character, including `_` and space | `Character '-'` |

When a construct is cut off, `Explain()` falls back to a generic line: a `\` at the end gives `Escape character`, `\k` without a name gives `Backreference`, a `[` without a closing `]` gives `Character class start`, `(?` at the end gives `Group start`, and an unfinished `(?<` gives `Lookbehind or named group`. The scan then continues with the next character.

### Group-aware explanations

`Explain()` knows which groups the pattern has, so it can tell a backreference from an octal escape and a group condition from an expression condition. Here is a conditional that requires a closing parenthesis only when an opening one matched:

```csharp
using System;
using Masterly.FluentRegex;

Pattern areaCode = Pattern.FromRegex(@"(\()?\d{3}(?(1)\))");

Console.WriteLine(areaCode.Explain());
```

Output:

```text
Pattern: (\()?\d{3}(?(1)\))

Explanation:
  - Capturing group
    - Escaped literal '('
    - End of group
  - Optional (zero or one)
  - Digit (any Unicode decimal digit)
  - Exactly 3 times
  - Conditional: if group 1 has matched, use the first branch
    - Escaped literal ')'
    - End of group
```

### Anchors, Unicode categories and inline options

```csharp
using System;
using System.Globalization;
using Masterly.FluentRegex;

Pattern pattern = Pattern.With
    .StartOfInput
    .Unicode(UnicodeCategory.UppercaseLetter)
    .IgnoreCase(Pattern.With.Literal("ab"))
    .Digit.Repeat.Times(2);

Console.WriteLine(pattern.Explain());
```

Output:

```text
Pattern: \A\p{Lu}(?i:ab)\d{2}

Explanation:
  - Start of string
  - Any character in Unicode category or block 'Lu'
  - Group with inline options (on: ignore case)
    - Literal 'a'
    - Literal 'b'
    - End of group
  - Digit (any Unicode decimal digit)
  - Exactly 2 times
```

### Limits of Explain

`Explain()` reads the regex text from left to right. Keep these limits in mind:

- **`\d` matches more than 0-9.** .NET's `\d` matches every Unicode decimal digit, such as the Arabic-Indic `٣`, and the line says so. Use `WithAsciiDigits()` or `RegexOptions.ECMAScript` when you mean 0-9 only. See [Character classes](./character-classes.md).
- **`^` and `$` do not show the mode.** They are always `Start of line/string` and `End of line/string`. With `RegexOptions.Multiline` they match at every line; without it, only at the start and end of the input.
- **Whitespace mode is not applied.** With `RegexOptions.IgnorePatternWhitespace`, spaces and `#` comments in the pattern are still listed as characters.
- **It never validates.** `Explain()` never throws, even for an invalid pattern. `(unclosed` is explained as `Capturing group` with eight `Literal` lines and no `End of group`. Do not use it to check a pattern. Use [`TryValidate`](#tryvalidate-and-the-error-message).

:::tip

For a description built from a real parse, use `Trace(input)`. It runs the pattern against an input and describes each construct with the library's parser, for example `a digit, exactly 3 times`, and shows which part of the input each construct matched or where the match failed. See [Match tracing](./match-tracing.md).

:::

### Explaining from the command line

The [command-line tool](./command-line-tool.md) has the same feature. It wraps the regex with `Pattern.FromRegex`, so it validates the regex first.

```bash
fluentregex explain '^(?<year>\d{4})-(?<month>\d{2})$'
```

```text
Pattern: ^(?<year>\d{4})-(?<month>\d{2})$

Explanation:
  - Start of line/string
  - Named capturing group 'year'
    - Digit (any Unicode decimal digit)
    - Exactly 4 times
    - End of group
  - Character '-'
  - Named capturing group 'month'
    - Digit (any Unicode decimal digit)
    - Exactly 2 times
    - End of group
  - End of line/string
```

- `--options IgnoreCase,Multiline` passes `RegexOptions`. Names are not case-sensitive. An unknown name prints `error: Unknown regex option 'Bogus'.` and exits with code 2.
- An invalid regex prints the `FromRegex` message and exits with code 2, for example `error: The regular expression is invalid: Invalid pattern '[unclosed' at offset 9. Unterminated [] set. (Parameter 'regex')`.
- `--ascii-digits` applies `WithAsciiDigits()` before explaining, so `\d` is shown as `Any character in: [0-9]`.

## Seeing a pattern in the debugger

`Pattern` is marked with `[DebuggerDisplay]` and `[DebuggerTypeProxy]`. When you hover over a `Pattern` variable or add it to a watch window, the debugger shows the regex text instead of the type name. When you expand it, you see a summary instead of the private fields.

Take this pattern:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern phone = Pattern.With
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .Literal("-")
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4))
    .WithOptions(RegexOptions.CultureInvariant)
    .WithTimeout(TimeSpan.FromSeconds(2));
```

The collapsed value is `(?<area>\d{3})-(?<number>\d{4})`, without quotes. Expanded, it shows:

| Row | Shows | For `phone` |
|-----|-------|-------------|
| `Regex` | The regex text, the same as `ToString()`. | `(?<area>\d{3})-(?<number>\d{4})` |
| `Options` | The options set with `WithOptions(...)`. | `CultureInvariant` |
| `Timeout` | The match timeout. No timeout shows as `-00:00:00.0010000` (`Regex.InfiniteMatchTimeout`). | `00:00:02` |
| `GroupNames` | The result of `GetGroupNames()`. | `area`, `number` |
| `Explanation` | The non-empty lines of `Explain()`, as an array. | `Pattern: (?<area>\d{3})-(?<number>\d{4})`, `Explanation:`, `  - Named capturing group 'area'`, ... |
| `Safety` | The ReDoS verdict of `AnalyzeSafety()`. | `None: no catastrophic backtracking risks found.` |

- Each row is computed the first time you expand it, then cached. Expanding a pattern never changes it.
- No row ever throws. A row that fails shows `Unavailable: ` and the error. For `(unclosed`, `GroupNames` shows `Unavailable: Invalid pattern '(unclosed' at offset 9. Not enough )'s.` and `Safety` shows `Unavailable: The pattern is not valid regex syntax: Not enough )'s (at position 0)`.
- `Safety` shows the risk level and the most severe issue, with `(+N more)` when there are others. For `(a+)+$` it starts with `High: High NestedQuantifier at 0 '(a+)+': The repetition 'a+' is nested inside the repetition '+'.` It runs the static analysis only, without the timed confirmation. See [ReDoS safety](./redos-safety.md).
- The view works in any debugger that honors these attributes, such as Visual Studio, JetBrains Rider and VS Code with the C# extension. You cannot create the view in code. Call the members listed above instead.

:::note

Expanding `Safety` runs the ReDoS analysis inside the debugger. For a very large pattern it can take a moment. The collapsed value only calls `ToString()`, which is cached.

:::

## Choosing the right tool

`Explain()` describes the pattern alone. Other members answer different questions:

| Question | Use | Page |
|----------|-----|------|
| Does this pattern compile? | `IsValid()`, `TryValidate(...)`, `ToRegex()` | This page |
| What does each part of the pattern mean? | `Explain()` | This page |
| Why does this input match or not match? | `Trace(input)` | [Match tracing](./match-tracing.md) |
| Where are the matches and groups in a text? | `Highlight(...)` | [Highlighting](./highlighting.md) |
| What does the pattern look like as a diagram? | `ToRailroadSvg()`, `ToMermaid()` | [Diagrams](./diagrams.md) |
| Can a hostile input make it hang? | `AnalyzeSafety()` | [ReDoS safety](./redos-safety.md) |
| Where does matching spend its time? | `Profile(input)` | [Backtracking profiler](./backtracking-profiler.md) |
| How do I write this regex with the fluent API? | `ToFluentCode()` | [Regex to fluent code](./regex-to-fluent-code.md) |
| Which branches do my tests exercise? | Regex coverage | [Regex coverage](./regex-coverage.md) |

## Common pitfalls

- **Validating a constant.** A sample like `Pattern.With.RegEx("[unclosed").IsValid()` does not build: the analyzer reports `FRX003` first. Runtime validation is for text that is not known at compile time.
- **Trusting `IsValid()` with options.** It ignores `WithOptions(...)` and `UseNonBacktracking()`. Use `ToRegex()` in a `try` block, as in [What validation does not check](#what-validation-does-not-check).
- **Validating on every call.** `IsValid()`, `TryValidate()`, `GetGroupNames()` and `GetGroupCount()` each compile a new `Regex`. Validate once and keep the result.
- **Using `Explain()` as a validator.** It never throws and happily explains broken text.
- **Expecting `Choice` to be non-capturing.** It adds a numbered group, which changes `GetGroupCount()` and the numbers of the groups after it. Use the `|` operator when you do not need the capture.
- **Parsing the error message.** Its wording belongs to the .NET runtime and can change between versions. Show it to people, but branch on `RegexParseException.Error` in code.
