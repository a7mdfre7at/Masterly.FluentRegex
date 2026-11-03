---
id: groups-and-backreferences
title: "Groups and Backreferences"
description: "Capturing, named, non-capturing and atomic groups, reading group values, backreferences and conditional patterns."
---

A group wraps part of a pattern in parentheses. You use groups for three jobs: to **capture** a piece of the match so you can read it later, to **treat several elements as one** so a quantifier repeats all of them, and to **control backtracking**. Once a group has captured text, a **backreference** can require the same text again later in the pattern, and a **conditional** can choose what comes next based on whether the group matched.

Pick a group method based on what you need:

| Method | Regex | Captures? | Use it when |
|--------|-------|-----------|-------------|
| `Group(pattern)` | `(...)` | Yes, by number | You want the text and a number is enough. |
| `NamedGroup(name, pattern)` | `(?<name>...)` | Yes, by name and number | You want the text and a readable name. This is the best default. |
| `NonCapturingGroup(pattern)` | `(?:...)` | No | You only need to group, for example to repeat a sequence. |
| `AtomicGroup(pattern)` | `(?>...)` | No | Once the group has matched, the engine must never backtrack into it. |
| `Backreference(name)`, `Backreference(number)` | `\k<name>`, `\1` | No | The same text must appear again. |
| `Conditional(group, yes, no)` | `(?(group)yes\|no)` | No | What follows depends on whether a group matched. |

All of these methods live on `Pattern` in the `Masterly.FluentRegex` namespace and work on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10. Like every builder method, each one returns a new pattern and leaves the original unchanged. The `Match`, `Group` and `RegexOptions` types on this page come from `System.Text.RegularExpressions`.

## Capturing groups

`Group(pattern)` wraps a pattern in a numbered capturing group. After a match, read the captured text from `Match.Groups` by number.

```csharp title="Capture by number"
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern itemCode = Pattern.With
    .Group(Pattern.With.Digit.Repeat.OneOrMore)
    .Literal("-")
    .Group(Pattern.With.Word.Repeat.OneOrMore);

Console.WriteLine(itemCode); // prints: (\d+)-(\w+)

Match match = itemCode.Match("Order 123-abc");

Console.WriteLine(match.Groups[0].Value); // prints: 123-abc
Console.WriteLine(match.Groups[1].Value); // prints: 123
Console.WriteLine(match.Groups[2].Value); // prints: abc
Console.WriteLine(match.Groups.Count);    // prints: 3
```

- Group `0` is always the whole match. Your groups start at `1`.
- `Groups.Count` includes group `0`, so a pattern with two groups reports `3`.
- The argument is a full pattern, so a group can hold anything: a single element, a long chain or other groups.
- Passing `null` throws `ArgumentNullException`. This is true for every method on this page that takes a pattern.

## Named groups

`NamedGroup(name, pattern)` creates a capturing group you can read by name. Names make the code that reads the match easy to follow, and they survive changes to the pattern that would shift group numbers.

```csharp title="Capture by name"
Pattern phone = Pattern.With
    .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
    .Literal("-")
    .NamedGroup("exchange", Pattern.With.Digit.Repeat.Times(3))
    .Literal("-")
    .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4));

Console.WriteLine(phone); // prints: (?<area>\d{3})-(?<exchange>\d{3})-(?<number>\d{4})

Match match = phone.Match("Call 555-123-4567 today");

Console.WriteLine(match.Groups["area"].Value);      // prints: 555
Console.WriteLine(match.Groups["exchange"].Value);  // prints: 123
Console.WriteLine(match.Groups["number"].Value);    // prints: 4567
Console.WriteLine(match.Groups[1].Value);           // prints: 555
Console.WriteLine(match.Groups["missing"].Success); // prints: False
```

- A named group also gets a number, so `Groups[1]` and `Groups["area"]` are the same group here. See [How groups are numbered](#how-groups-are-numbered) for the rule.
- Asking for a name the pattern does not define does not throw. You get an unsuccessful, empty group. Check `Success` when the name comes from outside your code.
- Named groups are what [Typed Extraction](./typed-extraction.md) binds to properties and constructor parameters.

### Group name rules

The library checks every group name when you build the pattern, so a bad name fails early with a clear message instead of an obscure regex parse error later.

| Name | Result |
|------|--------|
| Letters, digits and underscores, starting with a letter or `_` (`year`, `first_name`, `año`) | Valid. Unicode letters are allowed. |
| `null` or `""` | `ArgumentException`: "A group name cannot be null or empty." |
| Any other character (`first-name`, `a b`, `x.y`) | `ArgumentException`: "Group name '...' may only contain letters, digits and underscores." |
| Starts with a digit but has letters (`1st`) | Passes the name check, but the regex is invalid. `IsValid()` returns `false`. |
| Only digits (`5`) | Creates a group with that explicit **number**, not a name. `GetGroupNames()` does not list it. |

The same rules apply to the names you pass to `Backreference(name)` and `Conditional(name, ...)`.

```csharp title="A name from configuration"
// groupName comes from your configuration
string groupName = "first-name";

try
{
    Pattern.With.NamedGroup(groupName, Pattern.With.Word.Repeat.OneOrMore);
}
catch (ArgumentException exception)
{
    Console.WriteLine(exception.Message);
    // prints: Group name 'first-name' may only contain letters, digits and underscores. (Parameter 'name')
}

Pattern year = Pattern.With.NamedGroup("año", Pattern.With.Digit.Repeat.Times(4));
Console.WriteLine(year.Match("2024").Groups["año"].Value); // prints: 2024
```

:::tip

When the name is a constant, you do not have to wait for the exception. The package's analyzer evaluates the chain at build time and reports error `FRX006` ("This pattern throws ArgumentException when it is built") on the line that builds it. See [Compile-Time Checks](./compile-time-checks.md).

:::

## How groups are numbered

.NET numbers capturing groups by the position of their opening parenthesis, from left to right. Nested groups count too.

```csharp title="Nested groups"
Pattern version = Pattern.With
    .Literal("v")
    .Group(Pattern.With
        .Group(Pattern.With.Digit.Repeat.OneOrMore)
        .Literal(".")
        .Group(Pattern.With.Digit.Repeat.OneOrMore));

Console.WriteLine(version); // prints: v((\d+)\.(\d+))

Match match = version.Match("v1.25");

Console.WriteLine(match.Groups[1].Value); // prints: 1.25
Console.WriteLine(match.Groups[2].Value); // prints: 1
Console.WriteLine(match.Groups[3].Value); // prints: 25
```

There is one twist. .NET numbers all **unnamed** groups first, then the **named** groups. A named group therefore gets a higher number than an unnamed group that comes after it in the text.

```csharp title="Named groups are numbered last"
Pattern price = Pattern.With
    .NamedGroup("currency", Pattern.With.UppercaseLetter.Repeat.Times(3))
    .Literal(" ")
    .Group(Pattern.With.Digit.Repeat.OneOrMore);

Console.WriteLine(price); // prints: (?<currency>[A-Z]{3}) (\d+)

Match match = price.Match("USD 250");

Console.WriteLine(match.Groups[1].Value);          // prints: 250
Console.WriteLine(match.Groups[2].Value);          // prints: USD
Console.WriteLine(match.Groups["currency"].Value); // prints: USD
```

- When you mix named and unnamed groups, read named groups by name. Never guess their number.
- JavaScript and other engines number groups strictly left to right. When you export a pattern, the exporter warns you if the numbers change. See [Export](./export.md).

### Choice captures, OneOf does not

`Choice(...)` wraps its alternatives in a **capturing** group, so it adds a group number. `OneOf(...)` and the `|` operator use a non-capturing group and add none.

```csharp title="Choice versus OneOf"
Pattern capturing = Pattern.With
    .Choice(Pattern.With.Literal("https"), Pattern.With.Literal("http"))
    .Literal("://");

Pattern nonCapturing = Pattern.With
    .OneOf("https", "http")
    .Literal("://");

Console.WriteLine(capturing);                    // prints: (https|http)://
Console.WriteLine(capturing.GetGroupCount());    // prints: 1
Console.WriteLine(nonCapturing);                 // prints: (?:https|http)://
Console.WriteLine(nonCapturing.GetGroupCount()); // prints: 0
```

Use `Choice` when you want to read which alternative matched. Use `OneOf` everywhere else, so you do not shift the numbers of the groups that follow. See [Composing Patterns](./composing-patterns.md) for `OneOf` and the operators.

## Reading group values

### One match or many

`Match` returns the first match. `Matches` returns all of them. Each `Match` carries its own groups.

```csharp title="Groups in every match"
Pattern date = Pattern.With
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
    .Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2))
    .Literal("-")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2));

foreach (Match match in date.Matches("Events: 2024-01-15, 2024-02-20, 2024-03-25"))
{
    Console.WriteLine($"{match.Groups["day"].Value}/{match.Groups["month"].Value}/{match.Groups["year"].Value}");
}
// prints:
// 15/01/2024
// 20/02/2024
// 25/03/2024

Match second = date.Match("2024-01-15 and 2025-12-31", 10);
Console.WriteLine(second.Value); // prints: 2025-12-31
```

- `Match(input, startAt)` starts the search at a given index.
- `Match` never returns `null`. When nothing matches, `Success` is `false` and every group is empty.
- For other ways to run a pattern, see [Matching](./matching.md).

### Optional groups that did not match

A group can take part in a match or not. A group made optional with `.Repeat.Optional` that did not match has `Success == false` and an empty `Value`. It does not throw and its value is not `null`.

```csharp title="Check Success for optional groups"
Pattern amount = Pattern.With
    .NamedGroup("sign", Pattern.With.Literal("-")).Repeat.Optional
    .NamedGroup("digits", Pattern.With.Digit.Repeat.OneOrMore);

Match match = amount.Match("42");

Console.WriteLine(match.Groups["sign"].Success);      // prints: False
Console.WriteLine($"[{match.Groups["sign"].Value}]"); // prints: []
Console.WriteLine(match.Groups["digits"].Value);      // prints: 42
```

An empty `Value` cannot tell you the difference between "did not match" and "matched an empty string". Use `Success` when that difference matters.

### Repeated groups keep every capture

When a quantifier repeats a capturing group, `Value` holds only the **last** repetition. Every repetition is kept in `Captures`.

```csharp title="All captures of a repeated group"
using System.Linq;

Pattern octets = Pattern.With
    .Group(Pattern.With.Digit.Repeat.OneOrMore.Literal(".")).Repeat.OneOrMore;

Console.WriteLine(octets); // prints: (\d+\.)+

Match match = octets.Match("10.20.30.");

Console.WriteLine(match.Groups[1].Value);          // prints: 30.
Console.WriteLine(match.Groups[1].Captures.Count); // prints: 3
Console.WriteLine(string.Join(" ", match.Groups[1].Captures.Select(capture => capture.Value)));
// prints: 10. 20. 30.
```

`Captures` is a .NET feature that most other regex engines do not have. Do not rely on it in a pattern you plan to [export](./export.md).

### Listing the groups of a pattern

Two methods describe the groups a pattern defines, without matching anything.

| Method | Returns |
|--------|---------|
| `GetGroupNames()` | `string[]` of the **named** groups. Numbered groups, including the implicit group `0`, are left out. |
| `GetGroupCount()` | `int`: the number of capturing groups, named and numbered. Group `0`, non-capturing, atomic and lookaround groups are not counted. |

```csharp title="Inspect the groups"
Pattern pattern = Pattern.With
    .Group(Pattern.With.Digit)
    .NonCapturingGroup(Pattern.With.Whitespace)
    .NamedGroup("first", Pattern.With.Word)
    .NamedGroup("second", Pattern.With.Word);

Console.WriteLine(pattern.GetGroupCount());                    // prints: 3
Console.WriteLine(string.Join(", ", pattern.GetGroupNames())); // prints: first, second
```

- Both methods build a new `Regex` on each call, so cache the result if you call them in a loop.
- Both throw `RegexParseException` (an `ArgumentException`) when the pattern is not a valid regex. Check with `IsValid()` first if the pattern is built from outside input.

`Explain()` describes each group in plain English:

```csharp title="Explain a pattern with groups"
Pattern pattern = Pattern.With
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
    .Literal("-")
    .Group(Pattern.With.Digit.Repeat.Times(2));

Console.WriteLine(pattern.Explain());
// prints:
// Pattern: (?<year>\d{4})-(\d{2})
//
// Explanation:
//   - Named capturing group 'year'
//     - Digit (any Unicode decimal digit)
//     - Exactly 4 times
//     - End of group
//   - Character '-'
//   - Capturing group
//     - Digit (any Unicode decimal digit)
//     - Exactly 2 times
//     - End of group
```

See [Validation and Debugging](./validation-and-debugging.md) for more.

### Using groups in replacements

`Replace` accepts .NET substitutions: `${name}` for a named group and `$1`, `$2` for numbered groups. For anything more than reordering, pass a `MatchEvaluator` and read the groups yourself.

```csharp title="Reorder with substitutions"
Pattern date = Pattern.With
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
    .Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2))
    .Literal("-")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2));

Console.WriteLine(date.Replace("Due 2024-01-15.", "${day}/${month}/${year}"));
// prints: Due 15/01/2024.

Console.WriteLine(date.Replace("Due 2024-01-15.", match => $"{match.Groups["month"].Value}.{match.Groups["day"].Value}"));
// prints: Due 01.15.

Pattern twoWords = Pattern.With
    .Group(Pattern.With.Word.Repeat.OneOrMore)
    .Literal(" ")
    .Group(Pattern.With.Word.Repeat.OneOrMore);

Console.WriteLine(twoWords.Replace("hello world", "$2 $1")); // prints: world hello
```

:::tip

Prefer `${name}` over `$name`. The braces make the end of the name explicit, so text such as `${year}0` cannot be misread. For a typed, fluent alternative to substitution strings, see [Fluent Replacements](./fluent-replacements.md).

:::

### Binding groups to a type

Instead of reading groups one by one, `Extract<T>` binds named groups to the constructor parameters or properties of a type, converting the text as it goes.

```csharp title="Bind named groups to a record"
Pattern semver = Pattern.With
    .NamedGroup("major", Pattern.With.Digit.Repeat.OneOrMore)
    .Literal(".")
    .NamedGroup("minor", Pattern.With.Digit.Repeat.OneOrMore);

ReleaseVersion version = semver.Extract<ReleaseVersion>("v3.12");

Console.WriteLine(version); // prints: ReleaseVersion { Major = 3, Minor = 12 }

public record ReleaseVersion(int Major, int Minor);
```

Group names match member names without regard to case. [Typed Extraction](./typed-extraction.md) covers `Extract`, `TryExtract`, `ExtractAll`, `[RegexGroup]` and conversions.

## Non-capturing groups

`NonCapturingGroup(pattern)` groups elements without capturing them. Use it whenever you need parentheses only for structure. It keeps `Match.Groups` small and leaves the numbers of your real groups alone.

The most common reason is to repeat a sequence. A quantifier applies to the **last element** before it, so a sequence must be grouped to be repeated as a whole.

```csharp title="Repeat a sequence"
Pattern words = Pattern.With
    .NonCapturingGroup(Pattern.With.Word.Repeat.OneOrMore.Whitespace).Repeat.OneOrMore
    .Word.Repeat.OneOrMore;

Console.WriteLine(words);                 // prints: (?:\w+\s)+\w+
Console.WriteLine(words.GetGroupCount()); // prints: 0

Pattern withoutGroup = Pattern.With.Literal("ab").Repeat.OneOrMore;
Pattern withGroup = Pattern.With.NonCapturingGroup(Pattern.With.Literal("ab")).Repeat.OneOrMore;

Console.WriteLine(withoutGroup);              // prints: ab+
Console.WriteLine(withGroup);                 // prints: (?:ab)+
Console.WriteLine(withGroup.IsMatch("abab")); // prints: True
```

- `Literal("ab").Repeat.OneOrMore` builds `ab+`: only the `b` repeats. Wrap the literal to repeat all of it. See [Quantifiers](./quantifiers.md).
- A non-capturing group adds no runtime cost worth measuring. The [optimizer](./optimizer.md) removes the ones that change nothing.

:::warning

A non-capturing group does not hide the groups **inside** it. `NonCapturingGroup(Choice(...))` still captures, because `Choice` creates its own capturing group:

```csharp
Pattern protocol = Pattern.With
    .NonCapturingGroup(Pattern.With.Choice(Pattern.With.Literal("http"), Pattern.With.Literal("https")))
    .Literal("://");

Console.WriteLine(protocol);                 // prints: (?:(http|https))://
Console.WriteLine(protocol.GetGroupCount()); // prints: 1
```

Use `Pattern.With.OneOf("https", "http")` instead. It builds `(?:https|http)` directly.

:::

### Turning capture off for a whole pattern

`RegexOptions.ExplicitCapture` makes every **unnamed** group non-capturing. Named groups still capture. Apply it to the whole pattern with `WithOptions`, or to one part with `InlineOptions`.

```csharp title="ExplicitCapture"
Pattern pattern = Pattern.With
    .Group(Pattern.With.Digit.Repeat.OneOrMore)
    .Literal("-")
    .NamedGroup("code", Pattern.With.Word.Repeat.OneOrMore);

Pattern explicitOnly = pattern.WithOptions(RegexOptions.ExplicitCapture);

Console.WriteLine(pattern.GetGroupCount());                          // prints: 2
Console.WriteLine(explicitOnly.GetGroupCount());                     // prints: 1
Console.WriteLine(explicitOnly.Match("12-ab").Groups["code"].Value); // prints: ab

Pattern scoped = Pattern.With
    .InlineOptions(RegexOptions.ExplicitCapture, Pattern.With.Group(Pattern.With.Digit.Repeat.OneOrMore))
    .Group(Pattern.With.Word.Repeat.OneOrMore);

Console.WriteLine(scoped);                 // prints: (?n:(\d+))(\w+)
Console.WriteLine(scoped.GetGroupCount()); // prints: 1
```

`WithOptions` replaces the pattern's options. To keep options you already set, combine them: `pattern.WithOptions(pattern.Options | RegexOptions.ExplicitCapture)`.

## Atomic groups

`AtomicGroup(pattern)` matches its content once and then locks it in. If the rest of the pattern fails, the engine does not come back to try a shorter or different match inside the group. This changes what the pattern matches, so use it on purpose.

```csharp title="Atomic versus backtracking"
Pattern atomic = Pattern.With
    .StartOfLine
    .AtomicGroup(Pattern.With.Digit.Repeat.OneOrMore)
    .Digit
    .EndOfLine;

Pattern backtracking = Pattern.With
    .StartOfLine
    .Group(Pattern.With.Digit.Repeat.OneOrMore)
    .Digit
    .EndOfLine;

Console.WriteLine(atomic);                      // prints: ^(?>\d+)\d$
Console.WriteLine(atomic.IsMatch("123"));       // prints: False
Console.WriteLine(backtracking.IsMatch("123")); // prints: True
```

- In the atomic pattern, `\d+` takes all three digits. The final `\d` has nothing left, and the engine may not give a digit back, so the match fails.
- In the backtracking pattern, `\d+` gives back the `3`, and the final `\d` matches it.
- An atomic group does not capture. To capture too, put a group inside: `AtomicGroup(Pattern.With.NamedGroup("n", ...))`.

Atomic groups are the main tool against catastrophic backtracking. When nested or overlapping quantifiers can split the same input in many ways, an atomic group removes all but one of those ways. See [ReDoS Safety](./redos-safety.md) to find the patterns that need it, and [Backtracking Profiler](./backtracking-profiler.md) to measure the effect.

### Possessive quantifiers

`.Possessive` after a quantifier is shorthand for an atomic group around the quantified element. .NET has no possessive syntax, so the library emits the equivalent atomic group.

```csharp title="Possessive builds an atomic group"
Pattern possessive = Pattern.With.Digit.Repeat.OneOrMore.Possessive;
Pattern repeatedGroup = Pattern.With.NonCapturingGroup(Pattern.With.Literal("ab")).Repeat.OneOrMore.Possessive;

Console.WriteLine(possessive);    // prints: (?>\d+)
Console.WriteLine(repeatedGroup); // prints: (?>(?:ab)+)
```

`.Possessive` must come directly after a quantifier, or it throws `InvalidOperationException`. See [Quantifiers](./quantifiers.md).

## Backreferences

A backreference matches **the same text** that a group captured earlier in the same match. It does not repeat the group's pattern; it repeats its result.

### Named backreferences

`Backreference(name)` builds `\k<name>`. This is the clearest form and does not break when groups are added or moved.

```csharp title="Find doubled words"
Pattern duplicateWords = Pattern.With
    .WordBoundary
    .NamedGroup("word", Pattern.With.Word.Repeat.OneOrMore)
    .Whitespace.Repeat.OneOrMore
    .Backreference("word")
    .WordBoundary;

Console.WriteLine(duplicateWords); // prints: \b(?<word>\w+)\s+\k<word>\b

Console.WriteLine(duplicateWords.IsMatch("the the"));     // prints: True
Console.WriteLine(duplicateWords.IsMatch("hello world")); // prints: False
Console.WriteLine(duplicateWords.Match("This is is a test").Groups["word"].Value); // prints: is
Console.WriteLine(duplicateWords.Replace("This is is a test test", "${word}"));     // prints: This is a test
```

- The word boundaries matter. Without the final `\b`, the pattern would match `the the` inside `the theme`.
- Replacing each match with `${word}` keeps one copy of every doubled word.

A backreference is the usual way to match a closing delimiter that must equal the opening one:

```csharp title="Matching quotes"
Pattern quoted = Pattern.With
    .NamedGroup("quote", Pattern.With.Set(Pattern.With.Literal("'\"")))
    .NegatedSet(Pattern.With.Literal("'\"")).Repeat.ZeroOrMore
    .Backreference("quote");

Console.WriteLine(quoted); // prints: (?<quote>['"])[^'"]*\k<quote>

Console.WriteLine(quoted.IsMatch("'hello'"));   // prints: True
Console.WriteLine(quoted.IsMatch("\"world\"")); // prints: True
Console.WriteLine(quoted.IsMatch("'mixed\""));  // prints: False
```

### Numbered backreferences

`Backreference(number)` builds `\1`, `\2` and so on. Numbers start at `1`; `0` or a negative number throws `ArgumentOutOfRangeException`.

```csharp title="Matching tag names"
Pattern htmlTag = Pattern.With
    .Literal("<")
    .Group(Pattern.With.Word.Repeat.OneOrMore)
    .Literal(">")
    .Anything.Repeat.ZeroOrMore.Lazy
    .Literal("</")
    .Backreference(1)
    .Literal(">");

Console.WriteLine(htmlTag); // prints: <(\w+)>.*?</\1>

Console.WriteLine(htmlTag.IsMatch("<div>content</div>"));  // prints: True
Console.WriteLine(htmlTag.IsMatch("<div>content</span>")); // prints: False
```

The number follows the [numbering rules](#how-groups-are-numbered) above, including "named groups are numbered last". When the pattern has named groups, a named backreference is safer.

:::note

A regex is not an HTML parser. This pattern is fine for simple, flat markup, but it cannot handle nested tags of the same name or attributes. Use an HTML parser for real documents.

:::

### Case-insensitive backreferences

A backreference follows the pattern's case option. With `RegexOptions.IgnoreCase`, `The` and `the` count as the same text.

```csharp title="Backreference and IgnoreCase"
Console.WriteLine(duplicateWords.IsMatch("The the")); // prints: False
Console.WriteLine(duplicateWords.WithOptions(RegexOptions.IgnoreCase).IsMatch("The the")); // prints: True
```

`duplicateWords` is the doubled-word pattern from [Named backreferences](#named-backreferences).

### Backreferences to groups that did not match

In .NET, a backreference to a group that did not take part in the match **fails**. It does not match an empty string. The `ECMAScript` option switches to the JavaScript rule, where it matches empty.

```csharp title="A backreference to an optional group"
Pattern pattern = Pattern.With
    .StartOfLine
    .Group(Pattern.With.Literal("a")).Repeat.Optional
    .Literal("b")
    .Backreference(1)
    .EndOfLine;

Console.WriteLine(pattern);                // prints: ^(a)?b\1$
Console.WriteLine(pattern.IsMatch("aba")); // prints: True
Console.WriteLine(pattern.IsMatch("b"));   // prints: False
Console.WriteLine(pattern.WithOptions(RegexOptions.ECMAScript).IsMatch("b")); // prints: True
```

### Backreferences to groups that do not exist

`Backreference` checks the name format, but not whether the group exists. That check happens when the regex is parsed. A reference to an undefined name or number makes the pattern invalid:

```csharp title="Validate references built at run time"
// referenceName comes from your application
string referenceName = "missing";
Pattern pattern = Pattern.With.Digit.Backreference(referenceName);

Console.WriteLine(pattern.IsValid()); // prints: False

if (!pattern.TryValidate(out string error))
{
    Console.WriteLine(error);
    // prints: Invalid pattern '\d\k<missing>' at offset 13. Reference to undefined group name 'missing'.
}
```

- Calling `IsMatch`, `Match` or any other matching method on such a pattern throws `RegexParseException`, which derives from `ArgumentException`.
- When the chain is built from constants, the analyzer reports error `FRX003` ("The pattern produces an invalid regular expression") at build time, so the mistake never reaches run time. See [Compile-Time Checks](./compile-time-checks.md).
- A backreference written **before** its group, such as `Pattern.With.Backreference(1).Group(Pattern.With.Literal("a"))`, is a valid regex, but it cannot match: when the engine reaches `\1`, group 1 has not captured anything yet.

## Conditional patterns

`Conditional(group, truePattern, falsePattern)` builds `(?(group)yes|no)`. At that point in the match, the engine checks whether the group captured something. If it did, `truePattern` must match next; if it did not, `falsePattern` must match.

The classic use is a balanced optional delimiter: allow `(42)` and `42`, but not `(42` or `42)`.

```csharp title="Conditional by group number"
Pattern parenthesized = Pattern.With
    .StartOfLine
    .Group(Pattern.With.Literal("(")).Repeat.Optional
    .Digit.Repeat.OneOrMore
    .Conditional(1, Pattern.With.Literal(")"), Pattern.With)
    .EndOfLine;

Console.WriteLine(parenthesized); // prints: ^(\()?\d+(?(1)\)|)$

Console.WriteLine(parenthesized.IsMatch("(42)")); // prints: True
Console.WriteLine(parenthesized.IsMatch("42"));   // prints: True
Console.WriteLine(parenthesized.IsMatch("(42"));  // prints: False
Console.WriteLine(parenthesized.IsMatch("42)"));  // prints: False
```

The same works with a named group:

```csharp title="Conditional by group name"
Pattern bracketed = Pattern.With
    .StartOfLine
    .NamedGroup("open", Pattern.With.Literal("[")).Repeat.Optional
    .Digit.Repeat.OneOrMore
    .Conditional("open", Pattern.With.Literal("]"), Pattern.With)
    .EndOfLine;

Console.WriteLine(bracketed); // prints: ^(?<open>\[)?\d+(?(open)\]|)$

Console.WriteLine(bracketed.IsMatch("[7]")); // prints: True
Console.WriteLine(bracketed.IsMatch("7"));   // prints: True
Console.WriteLine(bracketed.IsMatch("[7"));  // prints: False
```

Both branches can be real patterns. Here a leading `#` means "a number follows", and its absence means "a name follows":

```csharp title="Two real branches"
Pattern label = Pattern.With
    .StartOfLine
    .NamedGroup("unit", Pattern.With.Literal("#")).Repeat.Optional
    .Conditional("unit", Pattern.With.Digit.Repeat.OneOrMore, Pattern.With.Letter.Repeat.OneOrMore)
    .EndOfLine;

Console.WriteLine(label); // prints: ^(?<unit>#)?(?(unit)\d+|[a-zA-Z]+)$

Console.WriteLine(label.IsMatch("#42"));    // prints: True
Console.WriteLine(label.IsMatch("lobby"));  // prints: True
Console.WriteLine(label.IsMatch("#lobby")); // prints: False
Console.WriteLine(label.IsMatch("42"));     // prints: False
```

### Parameters

| Parameter | Rules |
|-----------|-------|
| `groupNumber` (`int`) | Must be `1` or more, or `ArgumentOutOfRangeException` is thrown. A number with no matching group makes the pattern invalid. |
| `groupName` (`string`) | Checked like a group name: `ArgumentException` for `null`, empty or invalid characters. |
| `truePattern` | Required. `null` throws `ArgumentNullException`. |
| `falsePattern` | Optional in practice. `Pattern.With`, `Pattern.With.RegEx("")` and `null` all produce an empty "no" branch. |

```csharp title="Three ways to write an empty branch"
Pattern withEmptyPattern = Pattern.With.Group(Pattern.With.Literal("<")).Repeat.Optional.Digit
    .Conditional(1, Pattern.With.Literal(">"), Pattern.With);
Pattern withEmptyRegex = Pattern.With.Group(Pattern.With.Literal("<")).Repeat.Optional.Digit
    .Conditional(1, Pattern.With.Literal(">"), Pattern.With.RegEx(""));
Pattern withNull = Pattern.With.Group(Pattern.With.Literal("<")).Repeat.Optional.Digit
    .Conditional(1, Pattern.With.Literal(">"), null);

Console.WriteLine(withEmptyPattern); // prints: (<)?\d(?(1)>|)
Console.WriteLine(withEmptyRegex);   // prints: (<)?\d(?(1)>|)
Console.WriteLine(withNull);         // prints: (<)?\d(?(1)>|)
```

`Pattern.With` reads best and is what the library's own tests use.

### Alternation inside a branch

A conditional has room for exactly one `|`, the one between its two branches. The branches are written into the regex as they are, so a raw fragment with its own top-level `|`, such as `Pattern.With.RegEx("a|b")`, produces `(\d)(?(1)a|b|c)`. .NET rejects that regex with "Too many | in (?()|)". For a constant chain the analyzer reports it as `FRX003` at build time.

To offer several alternatives in one branch, use `OneOf` or the `|` operator. Both wrap the alternatives in a non-capturing group:

```csharp title="Several alternatives in one branch"
Pattern grouped = Pattern.With
    .Group(Pattern.With.Digit)
    .Conditional(1, Pattern.With.OneOf("a", "b"), Pattern.With.Literal("c"));

Console.WriteLine(grouped);           // prints: (\d)(?(1)(?:a|b)|c)
Console.WriteLine(grouped.IsValid()); // prints: True
```

### Anchor conditional patterns

A conditional only constrains the text the match covers. Without anchors, the engine is free to start the match after the opening delimiter, and the "no" branch then accepts the rest:

```csharp title="Unanchored conditionals match more than you expect"
Pattern unanchored = Pattern.With
    .Group(Pattern.With.Literal("(")).Repeat.Optional
    .Word.Repeat.OneOrMore
    .Conditional(1, Pattern.With.Literal(")"), Pattern.With);

Console.WriteLine(unanchored.IsMatch("(test"));     // prints: True
Console.WriteLine(unanchored.Match("(test").Value); // prints: test
```

To validate a whole value, add `StartOfLine` and `EndOfLine` (or the string anchors) as in the examples above. See [Assertions](./assertions.md).

:::warning

In .NET, `(?(name)...)` only tests a group when a group with that name **exists**. If it does not, .NET silently treats the name as a lookahead for that literal text. The pattern is still valid, so neither `IsValid()` nor the analyzer reports anything:

```csharp
Pattern guarded = Pattern.With
    .Conditional("abc", Pattern.With.Literal("abc!"), Pattern.With.Literal("x"));

Console.WriteLine(guarded);                 // prints: (?(abc)abc!|x)
Console.WriteLine(guarded.IsValid());       // prints: True
Console.WriteLine(guarded.IsMatch("abc!")); // prints: True
Console.WriteLine(guarded.IsMatch("x"));    // prints: True
Console.WriteLine(guarded.IsMatch("abc"));  // prints: False
```

Check the spelling of the group name. A numbered conditional does not have this problem: `Conditional(3, ...)` with no group 3 is reported as invalid.

:::

## Groups in composed patterns

Patterns are values, and you often build big ones from small ones with `+` or `Then`. Group **names** travel with the pieces. Group **numbers** do not: they are decided by the final, combined pattern.

### Numbered backreferences shift

A piece that uses `Backreference(1)` assumes it holds group 1. Put another group in front of it and `\1` now points at the wrong group.

```csharp title="A numbered reference breaks when composed"
Pattern doubledCharacter = Pattern.With
    .Group(Pattern.With.Word)
    .Backreference(1);

Pattern prefix = Pattern.With
    .Group(Pattern.With.Digit)
    .Literal(":");

Pattern combined = prefix + doubledCharacter;

Console.WriteLine(combined);                 // prints: (\d):(\w)\1
Console.WriteLine(combined.IsMatch("1:aa")); // prints: False
Console.WriteLine(combined.IsMatch("1:a1")); // prints: True
```

The same pieces with a named group behave correctly:

```csharp title="A named reference survives composition"
Pattern doubledCharacter = Pattern.With
    .NamedGroup("character", Pattern.With.Word)
    .Backreference("character");

Pattern prefix = Pattern.With
    .Group(Pattern.With.Digit)
    .Literal(":");

Pattern combined = prefix + doubledCharacter;

Console.WriteLine(combined);                 // prints: (\d):(?<character>\w)\k<character>
Console.WriteLine(combined.IsMatch("1:aa")); // prints: True
Console.WriteLine(combined.IsMatch("1:a1")); // prints: False
```

Use named groups, named backreferences and named conditionals in any pattern you plan to reuse.

### Reusing a piece repeats its group name

Using the same named piece twice gives two groups with one name. .NET allows this. The name then refers to one group that captures twice: `Value` holds the last capture, and `Captures` holds both.

```csharp title="One name, two captures"
Pattern year = Pattern.With.NamedGroup("year", Pattern.With.Digit.Repeat.Times(4));
Pattern yearRange = year.Literal("-").Then(year);

Console.WriteLine(yearRange); // prints: (?<year>\d{4})-(?<year>\d{4})

Match match = yearRange.Match("2019-2024");

Console.WriteLine(match.Groups["year"].Value);             // prints: 2024
Console.WriteLine(match.Groups["year"].Captures.Count);    // prints: 2
Console.WriteLine(match.Groups["year"].Captures[0].Value); // prints: 2019
Console.WriteLine(yearRange.GetGroupCount());              // prints: 1
```

If you need both values separately, give each copy its own name, for example with a small factory method that takes the name as a parameter.

## Errors and validation

| Call | Throws | When |
|------|--------|------|
| `Group`, `NamedGroup`, `NonCapturingGroup`, `AtomicGroup` | `ArgumentNullException` | The pattern argument is `null`. |
| `NamedGroup`, `Backreference(string)`, `Conditional(string, ...)` | `ArgumentException` | The name is `null`, empty, or has a character other than a letter, digit or underscore. |
| `Backreference(int)`, `Conditional(int, ...)` | `ArgumentOutOfRangeException` | The number is less than `1`. |
| `Conditional(...)` | `ArgumentNullException` | `truePattern` is `null`. |
| `Choice(...)`, `OneOf(...)` | `ArgumentNullException` / `ArgumentException` | An alternative is `null` / `OneOf` gets no alternatives. |
| `.Possessive` | `InvalidOperationException` | The last element is not quantified. |
| Any matching method | `RegexParseException` (an `ArgumentException`) | The final regex is invalid, for example a reference to an undefined group, a name such as `1st`, or a raw `\|` inside a conditional branch. |

- `RegexParseException` is public from .NET 7 on. Catch `ArgumentException` if your code also runs on older runtimes through the .NET Standard 2.1 build.
- The builder methods throw immediately, while you build the pattern. The regex itself is only parsed when you first match, call `ToRegex()`, `GetGroupNames()` or `GetGroupCount()`, or validate it.
- Use `IsValid()` or `TryValidate(out string errorMessage)` to check a pattern without catching exceptions. See [Validation and Debugging](./validation-and-debugging.md).
- For chains made of constants, the analyzer moves both kinds of error to the build: `FRX006` for a builder call that throws and `FRX003` for an invalid regex. See [Compile-Time Checks](./compile-time-checks.md).

## The non-backtracking engine

`UseNonBacktracking()` runs a pattern on .NET's linear-time engine, which is immune to ReDoS. It is available on .NET 8 and .NET 10 only, not on .NET Standard 2.1.

That engine supports capturing, named and non-capturing groups. It does **not** support backreferences, atomic groups (including `.Possessive`) or conditionals. Building such a pattern works; running it throws `NotSupportedException`.

```csharp title="Groups on the non-backtracking engine"
Pattern doubled = Pattern.With
    .Group(Pattern.With.Word)
    .Backreference(1);

try
{
    doubled.UseNonBacktracking().IsMatch("aa");
}
catch (NotSupportedException exception)
{
    Console.WriteLine(exception.Message);
    // prints: RegexOptions.NonBacktracking is not supported in conjunction with expressions containing: 'backreference (\ number)'.
}

Pattern groupsOnly = Pattern.With
    .NamedGroup("code", Pattern.With.Word.Repeat.OneOrMore)
    .NonCapturingGroup(Pattern.With.Literal("-"));

Console.WriteLine(groupsOnly.UseNonBacktracking().Match("ab-").Groups["code"].Value); // prints: ab
```

The same three constructs are outside the "regular" subset that [Pattern Algebra](./pattern-algebra.md) can reason about. Comparing a pattern that contains a backreference, an atomic group or a conditional, for example with `IsEquivalentTo`, throws `NotSupportedException` with a message such as "Language algebra does not support backreferences: '\1' at position 4." Capturing, named and non-capturing groups are fully supported there.

## Best practices

- **Name the groups you read.** `Groups["year"]` survives edits; `Groups[3]` does not.
- **Do not capture what you do not read.** Use `NonCapturingGroup` or `OneOf` for structure, and `Choice` only when you want to know which branch matched.
- **Prefer named backreferences and conditionals** in any pattern you compose or reuse.
- **Check `Success`** before trusting the value of an optional group.
- **Anchor validation patterns**, especially ones with conditionals.
- **Reach for atomic groups or `.Possessive`** when a ReDoS scan flags a pattern, and confirm the fix with the [Backtracking Profiler](./backtracking-profiler.md).

## API summary

| Member | Builds | Notes |
|--------|--------|-------|
| `Group(Pattern pattern)` | `(...)` | Numbered capturing group. |
| `NamedGroup(string name, Pattern pattern)` | `(?<name>...)` | Named capturing group. Name: letters, digits, `_`. |
| `NonCapturingGroup(Pattern pattern)` | `(?:...)` | Groups without capturing. |
| `AtomicGroup(Pattern pattern)` | `(?>...)` | Locks in its match; no backtracking into it. |
| `Choice(Pattern firstChoice, Pattern secondChoice, params Pattern[] additionalChoices)` | `(a\|b\|...)` | Alternation in a **capturing** group. |
| `OneOf(params Pattern[] alternatives)`, `OneOf(params string[] words)` | `(?:a\|b\|...)` | Alternation without capturing. Strings are escaped. |
| `.Possessive` | `(?>x+)` | After a quantifier; emits an atomic group. |
| `Backreference(string name)` | `\k<name>` | Same text as the named group. |
| `Backreference(int groupNumber)` | `\1`, `\2`, ... | Same text as the numbered group; `groupNumber >= 1`. |
| `Conditional(string groupName, Pattern truePattern, Pattern falsePattern)` | `(?(name)yes\|no)` | Branch on whether the named group matched. |
| `Conditional(int groupNumber, Pattern truePattern, Pattern falsePattern)` | `(?(1)yes\|no)` | Branch on whether the numbered group matched. |
| `GetGroupNames()` | `string[]` | Names of named groups only. |
| `GetGroupCount()` | `int` | Capturing groups, excluding group `0`. |

For the complete public API, see the [API Reference](./api-reference.md). For a compact syntax table, see the [Regex Cheat Sheet](./regex-cheat-sheet.md).
