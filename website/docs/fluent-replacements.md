---
id: fluent-replacements
title: "Fluent Replacements"
description: "Build replacement text with a validated fluent builder: group references, safe literals, transforms, computed parts and evaluators."
---

A fluent replacement describes the text that goes in place of each match, one part at a time: a group, some literal text, a transformed group, or text computed from the match. It is an alternative to .NET replacement strings such as `"${day}/${month}/$1"`.

Use it whenever the replacement refers to groups. Every group name and number is checked against the pattern before anything is replaced, so a typo throws an exception instead of leaking `${mnth}` into your output. Literal text is escaped for you, and you can transform a group's text without writing a `MatchEvaluator`.

`Pattern` offers three families of `Replace` overloads. This page covers the fluent one in depth and shows when the other two are the better fit.

| Overload | Replacement is | Group references checked | Use it when |
|----------|----------------|--------------------------|-------------|
| `Replace(input, r => ...)`, `Replace(input, r => ..., count)` | A `Replacement` built with the fluent builder | Yes, before replacing | The replacement uses groups, literal `$` signs or per-group transforms. |
| `Replace(input, replacement)`, `Replace(input, replacement, count)` | A .NET replacement string (`$1`, `${name}`, `$$`) | No | You already have a replacement string, or the replacement is plain text. |
| `Replace(input, evaluator)`, `Replace(input, evaluator, count)` | A `MatchEvaluator` (`Match` to `string`) | No | The whole replacement is custom code. See [Evaluator overloads](#evaluator-overloads). |

All of them use the pattern's cached `Regex`, with the pattern's options and timeout. The fluent builder lives in the `Masterly.FluentRegex` namespace and is available on every target framework the core package supports (`netstandard2.1`, `net8.0` and `net10.0`).

## Your first fluent replacement

Pass a lambda that receives an empty `Replacement` and chains the parts you want, in order.

```csharp
using System;
using Masterly.FluentRegex;

Pattern isoDate = Pattern.With
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4)).Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2)).Literal("-")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2));

string european = isoDate.Replace(
    "Due 2024-05-17",
    r => r.Group("day").Literal("/").Group("month").Literal("/").Group("year"));

Console.WriteLine(european); // prints: Due 17/05/2024
```

- Each builder method adds one part and returns the same `Replacement`, so the calls chain.
- Text outside the matches (`Due `) is kept as it is. Only the matched text is replaced.
- The lambda runs once per `Replace` call, not once per match. The finished replacement is then applied to every match.

The rest of this page uses the `isoDate` pattern from this sample.

### Builder methods

| Method | Inserts | .NET equivalent |
|--------|---------|-----------------|
| `Group(string name)` | The text captured by a named group. | `${name}` |
| `Group(string name, Func<string, string> transform)` | The named group's text, passed through `transform`. | None |
| `Group(int number)` | The text captured by a numbered group. `0` is the whole match. | `${number}` |
| `Group(int number, Func<string, string> transform)` | The numbered group's text, passed through `transform`. | None |
| `Literal(string text)` | `text` exactly as written. `$` is escaped for you. | Text with `$` doubled |
| `WholeMatch(Func<string, string> transform = null)` | The entire match, optionally transformed. | `$&` |
| `BeforeMatch()` | All input text before the match. | `` $` `` |
| `AfterMatch()` | All input text after the match. | `$'` |
| `LastGroup()` | The text of the highest-numbered group. | `$+` |
| `EntireInput()` | The whole input string. | `$_` |
| `Computed(Func<Match, string> compute)` | Any text computed from the `Match`. | None |

`Replacement.With` returns a new, empty builder each time you read it (`new Replacement()` does the same). You need it only when you build a replacement outside the `Replace` call (see [Reusing a replacement](#reusing-a-replacement)).

## Why replacement strings are risky

A .NET replacement string that names a missing group does not fail. `Regex.Replace` copies the unknown reference into the output as plain text, and your bug ships.

```csharp
string result = isoDate.Replace("2024-05-17", "${day}/${mnth}/${year}");
Console.WriteLine(result); // prints: 17/${mnth}/2024

string numbered = isoDate.Replace("2024-05-17", "$3/$7/$1");
Console.WriteLine(numbered); // prints: 17/$7/2024
```

The fluent builder makes the same mistake an exception. See the next section.

## Group-name validation

Before it replaces anything, `Replace` checks every `Group(name)` and `Group(number)` part against the pattern. A part that refers to a group the pattern does not have throws `ArgumentException`. The message names the bad group, shows the pattern and lists the groups that do exist.

```csharp
try
{
    isoDate.Replace("2024-05-17", r => r.Group("day").Literal("/").Group("mnth").Literal("/").Group("year"));
}
catch (ArgumentException ex)
{
    Console.WriteLine(ex.Message);
    // prints: Group 'mnth' does not exist in the pattern (?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2}). Available groups: 0, year, month, day.
}

try
{
    isoDate.Replace("no dates in this text", r => r.Group(7));
}
catch (ArgumentException ex)
{
    Console.WriteLine(ex.Message);
    // prints: Group #7 does not exist in the pattern (?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2}). Available groups: 0, year, month, day.
}
```

- Validation runs on every call, before the input is searched. The second call throws even though the input contains no date, so a typo fails on the first test run, not only when real data happens to match.
- Named groups appear as `'name'` in the message. Numbered groups appear as `#number`.
- Group names are case-sensitive, like .NET's own lookup. `r.Group("Year")` throws `Group 'Year' does not exist ...` for this pattern.
- `Group(0)` is always valid, because every pattern has group `0`, the whole match.
- The check is made at run time. The analyzer does not check replacement group names at build time (see [Compile-time checks](./compile-time-checks.md) for what it does check).

:::warning

Validation covers `Group(...)` parts only. Code inside a `Computed` lambda reads groups through `match.Groups[...]`, which .NET does not check: a misspelled name returns an empty, unsuccessful group. Prefer `Group(name, transform)` when you only need one group's text, so the name is validated.

:::

### Referencing groups by number

`Group(int)` refers to groups by their .NET number. Named groups get numbers too, so the `isoDate` groups are `1` (year), `2` (month) and `3` (day).

```csharp
Console.WriteLine(isoDate.Replace("2024-05-17", r => r.Group(3).Literal(".").Group(2).Literal(".").Group(1)));
// prints: 17.05.2024

Console.WriteLine(isoDate.Replace("Due 2024-05-17", r => r.Literal("<").Group(0).Literal(">")));
// prints: Due <2024-05-17>
```

When a pattern mixes named and unnamed groups, .NET numbers the unnamed groups first, from left to right, and the named groups after them. This is easy to get wrong:

```csharp
Pattern keyValue = Pattern.With
    .NamedGroup("key", Pattern.With.Letter.Repeat.OneOrMore)
    .Literal("=")
    .Group(Pattern.With.Digit.Repeat.OneOrMore);

Console.WriteLine(keyValue.ToString()); // prints: (?<key>[a-zA-Z]+)=(\d+)

Console.WriteLine(keyValue.Replace("size=42", r => r.Group(1).Literal(" -> ").Group(2)));
// prints: 42 -> size

Console.WriteLine(keyValue.Replace("size=42", r => r.Group("key").Literal(" -> ").Group(1)));
// prints: size -> 42
```

- Group `1` is the unnamed digits group, even though it comes second in the pattern. The named group `key` is group `2`.
- Name every group you refer to, and refer to it by name. Validation then also protects you when the pattern changes.
- To learn how groups are numbered and named, see [Groups and backreferences](./groups-and-backreferences.md).

## Literal text and dollar signs

In a .NET replacement string, `$` starts a substitution, so a literal dollar sign has to be written as `$$`. `Literal` does that escaping for you: the text you pass is inserted exactly as written.

```csharp
Pattern amount = Pattern.With.NamedGroup("amount", Pattern.With.Digit.Repeat.OneOrMore);

string price = amount.Replace("Total: 42", r => r.Literal("$").Group("amount").Literal(".00"));
Console.WriteLine(price); // prints: Total: $42.00

Console.WriteLine(isoDate.Replace("2024-05-17", r => r.Literal("$1 costs $").Group("year")));
// prints: $1 costs $2024
```

Compare that with the replacement string you would have to write by hand. Here `$1` is read as group 1 and `$$` as one dollar sign:

```csharp
Console.WriteLine(isoDate.Replace("2024-05-17", "$1 costs $${year}"));
// prints: 2024 costs ${year}
```

- `Literal` escapes only `$`. Backslashes and braces have no special meaning in .NET replacement syntax, so `r.Literal(@"C:\{x}\").Group("year")` gives `C:\{x}\2024`.
- `Literal(null)` throws `ArgumentNullException`. `Literal("")` is allowed and adds nothing.
- The value returned by a transform or by `Computed` is inserted as is. It is never read as replacement syntax, so a transform that returns `"2024 $1"` inserts `2024 $1`.

## Transforming captured text

Pass a `Func<string, string>` as the second argument to `Group` to change a group's text before it is inserted. The function receives the captured text and returns the text to insert.

```csharp
using System.Globalization;

string[] months = CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames;

string readable = isoDate.Replace("Released 2024-05-07", r => r
    .Group("month", month => months[int.Parse(month, CultureInfo.InvariantCulture) - 1])
    .Literal(" ")
    .Group("day", day => day.TrimStart('0'))
    .Literal(", ")
    .Group("year"));

Console.WriteLine(readable); // prints: Released May 7, 2024
```

- The group name is still validated. Only the inserted text changes.
- Transformed and untransformed parts mix freely in one replacement.
- `Group(number, transform)` works the same way for numbered groups.
- A `null` transform throws `ArgumentNullException`.

To transform the entire match, use `WholeMatch` with a transform:

```csharp
Pattern word = Pattern.With.Letter.Repeat.OneOrMore;

Console.WriteLine(word.Replace("hello fluent world", r => r.WholeMatch(text => text.ToUpperInvariant())));
// prints: HELLO FLUENT WORLD
```

`WholeMatch()` without an argument inserts the match unchanged. Passing `null` is the same as passing nothing.

### Groups that did not take part in the match

An optional group that did not match captures nothing. Its transform still runs and receives an empty string, so a transform can supply a default.

```csharp
Pattern signed = Pattern.With
    .NamedGroup("sign", Pattern.With.Literal("-")).Repeat.Optional
    .NamedGroup("digits", Pattern.With.Digit.Repeat.OneOrMore);

string explicitSigns = signed.Replace("5 -7 12", r => r
    .Group("sign", sign => sign.Length == 0 ? "+" : sign)
    .Group("digits"));

Console.WriteLine(explicitSigns); // prints: +5 -7 +12
```

Without a transform, a group that did not take part inserts nothing: `r.Literal("[").Group("sign").Literal("]").Group("digits")` turns `5 -7` into `[]5 [-]7`.

## Computing text from the match

`Computed` adds a part whose text comes from a function of the whole `Match`. Use it when a part depends on more than one group, on the match position, or on anything else in the `Match` object.

```csharp
Pattern number = Pattern.With.Digit.Repeat.OneOrMore;

string scaled = number.Replace("1 2 3", r => r
    .Computed(match => (int.Parse(match.Value, CultureInfo.InvariantCulture) * 10).ToString(CultureInfo.InvariantCulture)));
Console.WriteLine(scaled); // prints: 10 20 30

string positions = number.Replace("7 8 9", r => r
    .WholeMatch()
    .Computed(match => $"@{match.Index}"));
Console.WriteLine(positions); // prints: 7@0 8@2 9@4
```

- The function runs once per match.
- `Computed(null)` throws `ArgumentNullException`.
- Exceptions thrown by the function are not caught. They leave `Replace` and no result is returned.
- Group lookups inside the function are not validated. `match.Groups["yaer"].Value` is an empty string, so `r.Computed(match => $"[{match.Groups["yaer"].Value}]")` turns `2024-05-17` into `[]` without any error.

## Special substitutions

Four parts insert text that is not a group. Each one maps to a .NET substitution.

```csharp
Pattern b = Pattern.With.Literal("b");

Console.WriteLine(b.Replace("abc", r => r.Literal("[").BeforeMatch().Literal("|").WholeMatch().Literal("|").AfterMatch().Literal("]")));
// prints: a[a|b|c]c

Console.WriteLine(b.Replace("abc", r => r.Literal("(").EntireInput().Literal(")")));
// prints: a(abc)c
```

| Method | Substitution | Inserts |
|--------|--------------|---------|
| `BeforeMatch()` | `` $` `` | Everything in the input before the start of the match. |
| `AfterMatch()` | `$'` | Everything in the input after the end of the match. |
| `EntireInput()` | `$_` | The whole input string, once per match. |
| `LastGroup()` | `$+` | The highest-numbered group. |

`LastGroup()` means the group with the highest number, not the last group that captured something. When that group did not take part in the match, it inserts nothing:

```csharp
Pattern setting = Pattern.With
    .Group(Pattern.With.Letter.Repeat.OneOrMore)
    .Literal("=")
    .Group(Pattern.With.Digit.Repeat.OneOrMore).Repeat.Optional;

Console.WriteLine(setting.ToString()); // prints: ([a-zA-Z]+)=(\d+)?

Console.WriteLine(setting.Replace("a=1 b=", r => r.Literal("<").LastGroup().Literal(">")));
// prints: <1> <>
```

:::note

`BeforeMatch()`, `AfterMatch()` and `EntireInput()` copy large parts of the input into every replacement. On long inputs with many matches the result grows quickly.

:::

## Limiting the number of replacements

Each fluent overload has a version with a `count` parameter. It replaces at most `count` matches, in the order the regex finds them: from the start of the input for a normal pattern.

```csharp
Pattern number = Pattern.With.Digit.Repeat.OneOrMore;

Console.WriteLine(number.Replace("1 2 3", r => r.Literal("#"), 2));          // prints: # # 3
Console.WriteLine(number.Replace("1 2 3", r => r.Literal("#"), count: -1));  // prints: # # #
Console.WriteLine(number.Replace("1 2 3", r => r.Literal("#"), 0));          // prints: 1 2 3
```

| `count` | Result |
|---------|--------|
| `-1` | Every match is replaced. This is what the overload without `count` uses. |
| `0` | Nothing is replaced. The input comes back unchanged. |
| Positive | At most that many matches are replaced. |
| Less than `-1` | `ArgumentOutOfRangeException`: `Count cannot be less than -1. (Parameter 'count')` |

Group validation runs before `count` is used, so `count: 0` still throws for a misspelled group.

A pattern built with `RegexOptions.RightToLeft` finds its matches from the end of the input, so `count` limits the last matches instead of the first:

```csharp
using System.Text.RegularExpressions;

Pattern fromRight = number.WithOptions(RegexOptions.RightToLeft);

Console.WriteLine(fromRight.Replace("1 2 3", r => r.Literal("#"), 2)); // prints: 1 # #
```

## Evaluator overloads

The fluent builder covers most replacements. When the whole replacement is custom code, the `MatchEvaluator` overloads are simpler. They pass each `Match` to your function and insert what it returns.

```csharp
Pattern number = Pattern.With.Digit.Repeat.OneOrMore;

string doubled = number.Replace("a1b2c3", match => (int.Parse(match.Value, CultureInfo.InvariantCulture) * 2).ToString(CultureInfo.InvariantCulture));
Console.WriteLine(doubled); // prints: a2b4c6

MatchEvaluator mask = match => new string('*', match.Length);
string firstTwo = number.Replace("a12b345c6", mask, 2);
Console.WriteLine(firstTwo); // prints: a**b***c6
```

The same masking written with the builder:

```csharp
string same = number.Replace("a12b345c6", r => r.WholeMatch(text => new string('*', text.Length)), 2);
Console.WriteLine(same); // prints: a**b***c6
```

| Overload | Signature | Notes |
|----------|-----------|-------|
| Evaluator | `string Replace(string input, MatchEvaluator evaluator)` | Replaces every match with the evaluator's result. |
| Evaluator with count | `string Replace(string input, MatchEvaluator evaluator, int count)` | Replaces at most `count` matches. |
| Fluent | `string Replace(string input, Func<Replacement, Replacement> build)` | Validates groups, then replaces every match. |
| Fluent with count | `string Replace(string input, Func<Replacement, Replacement> build, int count)` | Validates groups, then replaces at most `count` matches. |
| String | `string Replace(string input, string replacement)` | Plain .NET replacement string, for example `number.Replace("a1b2", "<$0>")` gives `a<1>b<2>`. |
| String with count | `string Replace(string input, string replacement, int count)` | `number.Replace("a1b2c3", "#", 2)` gives `a#b#c3`. |

- The compiler picks the overload from the lambda. A lambda that uses `Match` members, such as `match.Value`, binds to `MatchEvaluator`. A lambda that calls builder methods, such as `r.Group(...)`, binds to the fluent overload.
- Evaluators and replacement strings are not validated. Only the fluent overloads check group references.
- Pick the evaluator when you would otherwise write a single `Computed(...)` part and nothing else. Pick the builder as soon as the replacement names a group.

## Reusing a replacement

The lambda you pass to `Replace` may return any `Replacement`, not only the one it receives. Build a replacement once with `Replacement.With` and return it from the lambda:

```csharp
Replacement toGerman = Replacement.With.Group("day").Literal(".").Group("month").Literal(".").Group("year");

Console.WriteLine(isoDate.Replace("From 2024-05-17 to 2025-01-02", _ => toGerman));
// prints: From 17.05.2024 to 02.01.2025
```

A static method with the signature `Replacement Method(Replacement r)` works as a method group:

```csharp
static Replacement ToUs(Replacement r) => r.Group("month").Literal("/").Group("day").Literal("/").Group("year");

Console.WriteLine(isoDate.Replace("2024-05-17", ToUs)); // prints: 05/17/2024
```

- A shared replacement is validated against each pattern you use it with, on every call.
- `Replacement` is mutable: every builder method adds a part to the same instance. Finish building a shared replacement before you use it, and never call builder methods on it afterwards. Reading it from several threads at once is safe because `Replace` does not change it.
- Returning `null` from the lambda throws `InvalidOperationException`: `The replacement builder returned null.`

### Getting the .NET replacement string

`ToString()` returns the equivalent .NET replacement string. Use it to log a replacement, or to pass it to an API that accepts only a replacement string.

```csharp
Console.WriteLine(toGerman.ToString()); // prints: ${day}.${month}.${year}
Console.WriteLine(isoDate.Replace("2024-05-17", toGerman.ToString())); // prints: 17.05.2024

Console.WriteLine(Replacement.With.Group("year").Literal("-$").Group(2).WholeMatch().ToString());
// prints: ${year}-$$${2}$&
```

- Group references always use the braced form (`${year}`, `${2}`), and literal `$` signs are doubled.
- Transformed groups and `Computed` parts have no replacement-string form. They show as `{computed}`: `Replacement.With.Group("year", year => year).Literal("!").ToString()` returns `{computed}!`.

:::warning

Do not pass a string containing `{computed}` to a string overload. It is a placeholder for display, not a substitution, so `Replace` inserts the text `{computed}` literally.

:::

## How a fluent replacement runs

Each call to the fluent `Replace` does this:

1. Checks that `input` and `build` are not `null` (`ArgumentNullException`).
2. Calls your lambda with a new, empty `Replacement`. A `null` result throws `InvalidOperationException`.
3. Validates every group reference against the pattern's cached `Regex` (`ArgumentException`).
4. Replaces the matches, in one of two ways:
   - **No transforms and no `Computed` parts:** the parts are joined into one .NET replacement string (the same string `ToString()` returns) and passed to `Regex.Replace`. Matching and substitution then cost the same as the string overload.
   - **At least one transform or `Computed` part:** the replacement runs as a `MatchEvaluator`. For each match, every part is evaluated in order and the results are joined.

Some consequences:

- Building and validating is a small fixed cost per call: one pass over the parts, independent of input size. In a hot loop over many small strings, build the replacement once and return it from the lambda (see [Reusing a replacement](#reusing-a-replacement)).
- The evaluator path calls a function per part per match, so it allocates more than the string path. Keep transforms for the parts that need them.
- The pattern's options and timeout apply. A pattern built with `.WithTimeout(...)` throws `RegexMatchTimeoutException` from `Replace` when matching takes too long. See [ReDoS safety](./redos-safety.md).
- An exception from a transform or a `Computed` part stops the whole call. No partially replaced string is returned.

## Errors

| Exception | Thrown when |
|-----------|-------------|
| `ArgumentNullException` | `input` or `build` is `null`; or `Literal(null)`, `Computed(null)`, or a `null` transform passed to `Group`. |
| `ArgumentException` | `Group(null)` or `Group("")` (`A group name is required.`); or a group name or number that the pattern does not have. |
| `ArgumentOutOfRangeException` | `Group(-1)` or any negative group number; or `count` is less than `-1`. |
| `InvalidOperationException` | The builder lambda returned `null`. |
| `RegexMatchTimeoutException` | Matching took longer than the pattern's timeout. |

Errors from builder methods, such as `Group(-1)`, are thrown while your lambda runs, so they also come out of `Replace`.

:::tip

Cover each fluent replacement with one unit test. Because validation runs before the input is searched, any input catches a misspelled group name, even one with no match.

:::

## Related pages

- [Matching](./matching.md) covers `IsMatch`, `Match`, `Matches`, `Split` and `Count` on the same cached `Regex`.
- [Typed extraction](./typed-extraction.md) binds groups to objects instead of rewriting text.
- [Templates](./templates.md) build patterns and output from a readable template string.
- [PII redaction](./pii-redaction.md) replaces sensitive values with masks.
- [Entity Framework Core](./entity-framework-core.md) translates `Replace` with a replacement string to SQL on the databases that support it. The fluent builder runs in memory only, but `ToString()` gives you a replacement string when the replacement has no transforms or `Computed` parts.
