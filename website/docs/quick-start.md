---
id: quick-start
title: "Quick Start"
description: "Build a first pattern, test it, find and extract matches with named groups, and explain it, all in five minutes."
---

This walkthrough builds one small pattern and uses it for the everyday jobs: testing input, finding matches, reading named groups, binding them to a type, replacing text and explaining the pattern in plain English. Every output shown on this page comes from running the code.

You need the `Masterly.FluentRegex` package. If you have not installed it yet, run this in your project folder (see [Installation](./installation.md) for the other options):

```bash
dotnet add package Masterly.FluentRegex
```

Everything on this page works on every target the package supports (.NET Standard 2.1, .NET 8 and .NET 10), except `UseNonBacktracking()` and the span methods, which need .NET 8 or later. All core types live in the `Masterly.FluentRegex` namespace. The .NET types `Match`, `Regex` and `RegexOptions` come from `System.Text.RegularExpressions`.

## Step 1: Build a pattern

Every pattern starts with `Pattern.With`, which gives you a new, empty pattern. Each property or method adds one piece and returns a new pattern, so you build the whole expression as one chain.

The pattern below matches an item code such as `123-abc`: one or more digits, a dash, then three to five word characters, and nothing else on the line.

```csharp title="Build a pattern"
using System;
using Masterly.FluentRegex;

Pattern pattern = Pattern.With
    .StartOfLine                // ^
    .Digit.Repeat.OneOrMore     // \d+
    .Literal("-")               // -
    .Word.Repeat.Times(3, 5)    // \w{3,5}
    .EndOfLine;                 // $

Console.WriteLine(pattern.ToString()); // prints: ^\d+-\w{3,5}$
```

- `ToString()` returns the regular expression the chain builds. `Console.WriteLine(pattern)` prints the same text.
- A quantifier such as `.Repeat.OneOrMore` or `.Repeat.Times(3, 5)` applies to the piece just before it. The other quantifiers are `ZeroOrMore`, `Optional`, `Times(n)`, `AtLeast(n)` and `AtMost(n)`. See [Quantifiers](./quantifiers.md).
- `Literal(...)` escapes regex special characters for you: `Pattern.With.Literal("$100.00")` builds `\$100\.00`. Use `RegEx(...)` only when you want to add raw regex text: `Pattern.With.RegEx("[a-z]+")` builds `[a-z]+`.
- Patterns are immutable. Adding to a pattern never changes it; you always get a new one. See [Core Concepts](./core-concepts.md).

:::tip

The package ships compile-time analyzers. If a chain builds an invalid regex, for example `Pattern.With.RegEx("(unclosed")`, the build fails with error `FRX003` before you ever run the code. See [Compile-Time Checks](./compile-time-checks.md).

:::

## Step 2: Test it

`IsMatch` answers the simplest question: does the input match? You call it on the pattern itself. There is no need to create a `Regex` first.

```csharp title="Test input"
// pattern is the item-code pattern from Step 1
bool valid = pattern.IsMatch("123-abc");          // true
bool lettersFirst = pattern.IsMatch("abc-123");   // false
bool tooShort = pattern.IsMatch("123-ab");        // false: only 2 word characters
bool tooLong = pattern.IsMatch("123-abcdef");     // false: 6 word characters, then the line must end
bool extraText = pattern.IsMatch("order 123-abc"); // false: ^ requires the code at the start
```

- `IsMatch` looks for a match **anywhere** in the input. The pattern above rejects extra text only because it starts with `StartOfLine` and ends with `EndOfLine`.
- Without the anchors, the same pieces find a match inside a longer string:

```csharp title="Without anchors"
Pattern unanchored = Pattern.With
    .Digit.Repeat.OneOrMore
    .Literal("-")
    .Word.Repeat.Times(3, 5);

Console.WriteLine(unanchored);                              // prints: \d+-\w{3,5}
bool found = unanchored.IsMatch("order 123-abcdef");        // true: it matches "123-abcde"
```

:::warning

To validate a whole value, such as a form field, always anchor the pattern with `StartOfLine` and `EndOfLine`. An unanchored pattern accepts any input that merely contains a match.

:::

## Step 3: Find matches and read named groups

To pull values out of text, wrap the interesting parts in named groups. `NamedGroup(name, pattern)` captures whatever the inner pattern matches under that name.

This version finds item codes inside a sentence. It uses `WordBoundary` instead of the line anchors, so each code must stand on its own as a word.

```csharp title="Find matches"
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern itemCode = Pattern.With
    .WordBoundary
    .NamedGroup("number", Pattern.With.Digit.Repeat.OneOrMore)
    .Literal("-")
    .NamedGroup("code", Pattern.With.Word.Repeat.Times(3, 5))
    .WordBoundary;

Console.WriteLine(itemCode); // prints: \b(?<number>\d+)-(?<code>\w{3,5})\b

string text = "Shipped 123-abc and 456-defgh, but not 789-xy.";

// The first match
Match first = itemCode.Match(text);
Console.WriteLine(first.Success);                  // prints: True
Console.WriteLine(first.Value);                    // prints: 123-abc
Console.WriteLine(first.Index);                    // prints: 8
Console.WriteLine(first.Groups["number"].Value);   // prints: 123
Console.WriteLine(first.Groups["code"].Value);     // prints: abc

// Every match
foreach (Match match in itemCode.Matches(text))
{
    Console.WriteLine($"{match.Groups["number"].Value} / {match.Groups["code"].Value}");
}
// prints:
// 123 / abc
// 456 / defgh
```

- `Match` and `Matches` return the standard .NET `Match` and `MatchCollection`, so everything you know about them still applies.
- `789-xy` is skipped because `xy` has only two word characters.
- `Match(input, startAt)` starts the search at a given index.
- When nothing matches, `Match` does not return `null`. It returns a match whose `Success` is `false` and whose `Value` is an empty string. Asking that failed match for a group also returns an empty string, so check `Success` first.

To see which groups a pattern defines:

```csharp title="Inspect the groups"
string[] names = itemCode.GetGroupNames();  // ["number", "code"]
int groupCount = itemCode.GetGroupCount();  // 2
```

- `GetGroupNames()` returns only named groups. Groups made with `Group(...)` are numbered, not named, so they are not listed.
- `GetGroupCount()` counts every capturing group, named or numbered. It does not count group 0 (the whole match) or groups made with `NonCapturingGroup(...)`.

For more about groups, numbered groups and backreferences, see [Groups and Backreferences](./groups-and-backreferences.md).

### Bind matches to a type

Reading groups one string at a time gets repetitive. `Extract<T>` matches the input and binds the named groups to a record or class for you, converting each value to the member's type.

```csharp title="Typed extraction"
// itemCode is the pattern above
ItemCode single = itemCode.Extract<ItemCode>("Shipped 123-abc today");
Console.WriteLine(single); // prints: ItemCode { Number = 123, Code = abc }

foreach (ItemCode item in itemCode.ExtractAll<ItemCode>("Shipped 123-abc and 456-defgh, but not 789-xy."))
{
    Console.WriteLine(item);
}
// prints:
// ItemCode { Number = 123, Code = abc }
// ItemCode { Number = 456, Code = defgh }

bool extracted = itemCode.TryExtract<ItemCode>("no codes here", out ItemCode? missing);
// extracted is false, missing is null

public sealed record ItemCode(int Number, string Code);
```

- Groups bind to constructor parameters and properties by name, ignoring case. `number` becomes `Number` and is parsed as an `int`.
- Numbers and dates are parsed with the invariant culture. Pass an `IFormatProvider` as the last argument to change that.
- `ExtractAll<T>` is lazy. It finds and binds the next match only when you ask for it.

| Method | When the input does not match | When a value cannot be converted |
|--------|-------------------------------|----------------------------------|
| `Extract<T>(input)` | Throws `FormatException` | Throws `FormatException` |
| `TryExtract<T>(input, out result)` | Returns `false`, `result` is `default` | Returns `false` |
| `ExtractAll<T>(input)` | Yields nothing | Throws `FormatException` while you enumerate |

When `Extract<T>` fails to match, the message tells you where to look next:

```text title="FormatException message"
The input does not match the pattern \b(?<number>\d+)-(?<code>\w{3,5})\b. Use Trace(input) to see where it fails.
```

- A `null` input makes `Extract<T>` and `ExtractAll<T>` throw `ArgumentNullException`. `TryExtract<T>` returns `false`.
- Attributes, converters and binding rules are covered in [Typed Extraction](./typed-extraction.md).

## Step 4: Replace, split and count

The same pattern rewrites text. A replacement string can refer to named groups with `${name}` and to numbered groups with `$1`.

```csharp title="Replace, split and count"
// itemCode and text come from Step 3:
// text is "Shipped 123-abc and 456-defgh, but not 789-xy."

string swapped = itemCode.Replace(text, "${code}#${number}");
// Shipped abc#123 and defgh#456, but not 789-xy.

string upper = itemCode.Replace(text, (Match match) => match.Groups["code"].Value.ToUpperInvariant());
// Shipped ABC and DEFGH, but not 789-xy.

string firstOnly = itemCode.Replace(text, "[code]", 1);
// Shipped [code] and 456-defgh, but not 789-xy.

int count = itemCode.Count(text); // 2

Pattern separator = Pattern.With
    .Whitespace.Repeat.ZeroOrMore
    .Literal(",")
    .Whitespace.Repeat.ZeroOrMore;

string[] parts = separator.Split("red, green ,blue"); // ["red", "green", "blue"]
```

- The `count` argument of `Replace` and `Split` limits how many matches are replaced, or how many parts are returned.
- A function passed to `Replace` is a standard `MatchEvaluator`: it receives each `Match` and returns the replacement text. Writing the parameter type, `(Match match) =>`, is optional; it only makes clear that this is not the other `Replace` overload, which takes a fluent replacement builder. See [Fluent Replacements](./fluent-replacements.md).
- All the matching methods are covered in [Matching](./matching.md), including the allocation-free span methods on .NET 8 and later.

## Step 5: Explain it

`Explain()` describes the pattern in plain English, one construct per line. Use it to check that a chain builds what you meant, or to understand a pattern someone else wrote.

```csharp title="Explain a pattern"
// pattern is the item-code pattern from Step 1
Console.WriteLine(pattern.Explain());
```

```text title="Console output"
Pattern: ^\d+-\w{3,5}$

Explanation:
  - Start of line/string
  - Digit (any Unicode decimal digit)
  - One or more
  - Character '-'
  - Word character (any Unicode letter, digit or _)
  - Between 3 and 5 times
  - End of line/string
```

Groups are indented, so the structure is easy to see. Here is the explanation of `itemCode` from Step 3:

```text title="Console output"
Pattern: \b(?<number>\d+)-(?<code>\w{3,5})\b

Explanation:
  - Word boundary
  - Named capturing group 'number'
    - Digit (any Unicode decimal digit)
    - One or more
    - End of group
  - Character '-'
  - Named capturing group 'code'
    - Word character (any Unicode letter, digit or _)
    - Between 3 and 5 times
    - End of group
  - Word boundary
```

:::note

`Explain()` describes `\d` and `\w` the way .NET matches them: by default `\d` also matches digits from other scripts, such as `١٢٣`, and `\w` also matches letters such as `é`. To limit `\d` and `\w` to ASCII, use `RegexOptions.ECMAScript` (see [Step 6](#step-6-use-it-with-regex-options-and-timeouts)) or an explicit set. See [Unicode](./unicode.md).

:::

### Find out why an input does not match

`Explain()` describes the pattern. `Trace(input)` runs it against one input and shows what each construct consumed, or exactly where matching stopped.

```csharp title="Trace a failing input"
// pattern is the item-code pattern from Step 1
Console.WriteLine(pattern.Trace("123-ab"));
```

```text title="Console output"
Pattern: ^\d+-\w{3,5}$
Input:   "123-ab"
Result:  NO MATCH

Matched before the failure:
  [0..0)    ^                                ""               start of input
  [0..3)    \d+                              "123"            a digit, one or more times
  [3..4)    -                                "-"              the character '-'

Failed at position 4: expected \w{3,5} (a word character, between 3 and 5 times)
Found:   "ab"

  123-ab
      ^
```

`Trace` returns a `MatchTrace` object from the `Masterly.FluentRegex.Tracing` namespace, so you can also read `IsMatch`, `Steps` and `Failure` in code. Printing it gives the report above. See [Match Tracing](./match-tracing.md).

### Check a pattern built at run time

The analyzers catch invalid regex text that is written in your code. When the text arrives at run time, for example from configuration or a user, check it before you use it:

```csharp title="Validate run-time regex text"
// userRegex comes from your application, here "(unclosed"
Pattern custom = Pattern.With.RegEx(userRegex);

bool isValid = custom.IsValid(); // false

if (!custom.TryValidate(out string error))
{
    Console.WriteLine(error); // prints: Invalid pattern '(unclosed' at offset 9. Not enough )'s.
}
```

- `IsValid()` returns `false` instead of throwing. `TryValidate` also gives you the engine's error message; when the text is valid it returns `true` and sets the message to `null`.
- Both methods compile the pattern with its own options. For example, a backreference such as `(a)\1` is invalid after `UseNonBacktracking()` (.NET 8 and later), because the non-backtracking engine does not support backreferences.
- Calling `IsMatch` or `Match` on an invalid pattern throws `ArgumentException`.
- More debugging tools are described in [Validation and Debugging](./validation-and-debugging.md).

## Step 6: Use it with Regex, options and timeouts

Some APIs expect a `System.Text.RegularExpressions.Regex`. `ToRegex()` creates one from the pattern. The `Regex` uses the pattern's options and timeout unless you pass other options.

```csharp title="Convert, set options and add a timeout"
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

// pattern is the item-code pattern from Step 1
Regex regex = pattern.ToRegex();
bool matches = regex.IsMatch("123-abc"); // true

Regex ignoreCase = pattern.ToRegex(RegexOptions.IgnoreCase);
Console.WriteLine(ignoreCase.Options); // prints: IgnoreCase

Pattern hello = Pattern.With.Literal("hello");
Pattern helloAnyCase = hello.WithOptions(RegexOptions.IgnoreCase);

bool exact = hello.IsMatch("HELLO");                                // false
bool anyCase = helloAnyCase.IsMatch("HELLO");                       // true
bool oneOff = hello.IsMatch("HELLO", RegexOptions.IgnoreCase);      // true

Pattern guarded = pattern.WithTimeout(TimeSpan.FromMilliseconds(250));
Console.WriteLine(guarded.Timeout);              // prints: 00:00:00.2500000
Console.WriteLine(guarded.ToRegex().MatchTimeout); // prints: 00:00:00.2500000
```

| Member | What it does |
|--------|--------------|
| `ToRegex()` | Creates a new `Regex` with the pattern's options and timeout. |
| `ToRegex(options)` | Creates a new `Regex` with the given options instead. The pattern's timeout still applies. |
| `WithOptions(options)` | Returns a copy of the pattern that uses these options for every match. The original keeps its options. |
| `IsMatch(input, options)` | Tests once with other options, without changing the pattern. |
| `WithTimeout(timeout)` | Returns a copy that throws `RegexMatchTimeoutException` when a match runs longer than the timeout. |
| `Options`, `Timeout` | The options and timeout the pattern uses. A new pattern has `RegexOptions.None` and no timeout (`Regex.InfiniteMatchTimeout`). |

- The pattern's own matching methods (`IsMatch(input)`, `Match`, `Matches`, `Replace`, `Split`, `Count`, `Extract`) build their `Regex` once, on first use, and reuse it. `ToRegex()` always creates a new instance, so keep the result if you call it in a loop.
- `WithTimeout` rejects zero and negative values with `ArgumentOutOfRangeException`. Pass `Regex.InfiniteMatchTimeout` to remove a timeout.
- `IsMatch(input, options)` builds a new `Regex` on every call. For repeated use, prefer `WithOptions`.

:::tip

Add a timeout to any pattern that runs on input you do not control. It is the runtime defence against catastrophic backtracking. On .NET 8 and later, `UseNonBacktracking()` switches to an engine whose run time grows linearly with the input. See [ReDoS Safety](./redos-safety.md).

:::

## Start from a ready-made pattern

You do not have to build every format yourself. `CommonPatterns` holds tested patterns for common formats. Each method returns a normal `Pattern`, so everything on this page works with it.

```csharp title="Use a common pattern"
using Masterly.FluentRegex;

Pattern email = CommonPatterns.Email();

bool ok = email.IsMatch("ada@example.com");                // true
bool bad = email.IsMatch("not an email");                  // false
bool date = CommonPatterns.DateIso().IsMatch("2026-11-03"); // true
```

See [Common Patterns](./common-patterns.md) for the full list.

## What you used

| Member | Returns | Purpose |
|--------|---------|---------|
| `Pattern.With` | `Pattern` | A new, empty pattern to start a chain. |
| `.StartOfLine`, `.EndOfLine`, `.WordBoundary` | `Pattern` | Anchors: `^`, `$`, `\b`. |
| `.Digit`, `.Word`, `.Whitespace` | `Pattern` | Character classes: `\d`, `\w`, `\s`. |
| `.Literal(text)` | `Pattern` | Text matched as is, with special characters escaped. |
| `.RegEx(text)` | `Pattern` | Raw regex text, not escaped. |
| `.Repeat.OneOrMore`, `.Repeat.Times(min, max)` | `Pattern` | Quantifiers for the piece before them. |
| `.NamedGroup(name, pattern)` | `Pattern` | A named capturing group: `(?<name>...)`. |
| `ToString()` | `string` | The regular expression text. |
| `IsMatch(input)` | `bool` | Whether the pattern matches anywhere in the input. |
| `Match(input)`, `Matches(input)` | `Match`, `MatchCollection` | The first match, or every match. |
| `Extract<T>`, `TryExtract<T>`, `ExtractAll<T>` | `T`, `bool`, `IEnumerable<T>` | Bind named groups to a type. |
| `Replace`, `Split`, `Count` | `string`, `string[]`, `int` | Rewrite, split and count. |
| `GetGroupNames()`, `GetGroupCount()` | `string[]`, `int` | The groups the pattern defines. |
| `Explain()` | `string` | A plain-English description of the pattern. |
| `Trace(input)` | `MatchTrace` | What each construct matched, or where matching failed. |
| `IsValid()`, `TryValidate(out error)` | `bool` | Check that the regex text is valid. |
| `ToRegex()`, `WithOptions`, `WithTimeout` | `Regex`, `Pattern`, `Pattern` | Interop, options and timeouts. |

The complete list is in the [API Reference](./api-reference.md).

## Common pitfalls

- **Forgetting the anchors.** `IsMatch` finds a match anywhere. Without `StartOfLine` and `EndOfLine`, `"order 123-abcdef"` passes a pattern meant for `"123-abc"`. See [Step 2](#step-2-test-it).
- **A trailing newline still passes.** Without `RegexOptions.Multiline`, `$` also matches just before a final `\n`. So `^\d+$` accepts `"123\n"`. To reject the newline, anchor with `StartOfInput` and `EndOfInput` instead. They build `\A\d+\z`, which rejects `"123\n"` and accepts `"123"`. See [Basic Patterns](./basic-patterns.md).
- **Multi-line input with line anchors.** By default `^` and `$` mean the start and end of the whole input, so `^\d+$` does not match `"123\n456"`. With `WithOptions(RegexOptions.Multiline)` it finds two matches, one per line.
- **Assuming ASCII.** `\d` and `\w` match Unicode digits and letters by default. See the note in [Step 5](#step-5-explain-it).
- **Expecting `Match` to return `null`.** A failed match has `Success == false`. Check it before you read groups.
- **Using `RegEx` for plain text.** `RegEx("$100.00")` treats `$` and `.` as regex syntax. Use `Literal` for text you want matched exactly.

## Next steps

- Learn how chains, immutability and escaping work in [Core Concepts](./core-concepts.md).
- Browse every building block, starting with [Basic Patterns](./basic-patterns.md) and [Character Classes](./character-classes.md).
- Combine small patterns into bigger ones with [Composing Patterns](./composing-patterns.md).
- Draw a pattern as a diagram with [Diagrams](./diagrams.md), or highlight matches in text with [Highlighting](./highlighting.md).
- Generate test inputs that match, or nearly match, with [Test Data Generation](./test-data-generation.md).
- Translate an existing regex into fluent code with [Regex to Fluent Code](./regex-to-fluent-code.md).
- See complete, real-world scenarios in [Examples](./examples.md).
