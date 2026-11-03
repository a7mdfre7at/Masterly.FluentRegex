---
id: match-tracing
title: "Match Tracing"
description: "See which construct consumed which characters, or exactly where and why a pattern stopped matching."
---

`Trace(input)` runs a pattern against one string and explains the result. For a match, it lists every construct of the pattern with the exact span and text it consumed. For a failure, it names the construct that could not match, the position where matching stopped, and what the input held there.

Use it when a pattern does not do what you expect: a value you think is valid is rejected, a group captures too much, or an alternative you did not plan for wins. It is a debugging tool. To read a pattern in plain English without running it, see [Validation and Debugging](./validation-and-debugging.md). To mark every match in a larger text, see [Highlighting](./highlighting.md). To find out why a match is slow, see [Backtracking Profiler](./backtracking-profiler.md).

| Type or member | What it is | Available on |
|----------------|------------|--------------|
| `Pattern.Trace(string input)` | Matches the pattern and returns a `MatchTrace`. | netstandard2.1, net8.0, net10.0 |
| `MatchTrace` | The whole result: the verdict, the match, the steps or the failure. Its `ToString()` is a readable report. | netstandard2.1, net8.0, net10.0 |
| `TraceStep` | One construct and the text it consumed, with its child steps. | netstandard2.1, net8.0, net10.0 |
| `TraceFailure` | Where and why the pattern stopped matching. | netstandard2.1, net8.0, net10.0 |

All four types live in the `Masterly.FluentRegex` namespace. The same report is also available from the [command-line tool](#from-the-command-line) and the [Playground](./playground.md).

## Tracing a match

Call `Trace` on any pattern and print the result. This pattern expects an ISO date with two-digit months, and the input has a one-digit month:

```csharp
using System;
using Masterly.FluentRegex;

Pattern date = Pattern.With
    .StartOfLine
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
    .Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2))
    .Literal("-")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2))
    .EndOfLine;

MatchTrace trace = date.Trace("2024-5-17");

Console.WriteLine(trace.IsMatch); // False
Console.WriteLine(trace);
```

The second line prints:

```text
Pattern: ^(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})$
Input:   "2024-5-17"
Result:  NO MATCH

Matched before the failure:
  [0..0)    ^                                ""               start of input
  [0..4)    (?<year>\d{4})                   "2024"           group 'year'
    [0..4)    \d{4}                          "2024"           a digit, exactly 4 times
  [4..5)    -                                "-"              the character '-'

Failed at position 5: expected \d{2} (a digit, exactly 2 times)
Found:   "5-17"

  2024-5-17
       ^
```

- The report says what matched (`^`, the year, the first `-`), which construct failed (`\d{2}` inside the `month` group), where (position 5), and what was there instead (`"5-17"`).
- The caret under the input points at the failure position.
- `Trace` never throws because the input does not match. A failed match is a normal result with `IsMatch == false`.

## Reading a successful trace

For a match, the report lists every construct in pattern order, with the span it consumed:

```csharp
// date is the pattern from "Tracing a match"
Console.WriteLine(date.Trace("2024-05-17"));
```

```text
Pattern: ^(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})$
Input:   "2024-05-17"
Result:  MATCH at 0, length 10: "2024-05-17"

  [0..0)    ^                                ""               start of input
  [0..4)    (?<year>\d{4})                   "2024"           group 'year'
    [0..4)    \d{4}                          "2024"           a digit, exactly 4 times
  [4..5)    -                                "-"              the character '-'
  [5..7)    (?<month>\d{2})                  "05"             group 'month'
    [5..7)    \d{2}                          "05"             a digit, exactly 2 times
  [7..8)    -                                "-"              the character '-'
  [8..10)   (?<day>\d{2})                    "17"             group 'day'
    [8..10)   \d{2}                          "17"             a digit, exactly 2 times
  [10..10)  $                                ""               end of input
```

Each line has four columns:

| Column | Meaning |
|--------|---------|
| `[5..7)` | The span: the start index, inclusive, and the end index, exclusive. `[5..7)` covers positions 5 and 6. |
| `(?<month>\d{2})` | The construct, as regex text. |
| `"05"` | The exact text the construct consumed. |
| `group 'month'` | A plain-language description of the construct. |

- Child steps are indented by two spaces under their parent. A group has one child per part of its body.
- Anchors and other zero-width constructs, such as `^`, `$` and `\b`, show an empty span like `[0..0)` and the value `""`.
- Consecutive literal characters are traced as one piece of text. `.Literal(": ")` appears as one step, `the text ": "`, not as two.
- The spans come from the real .NET engine. `Trace` wraps every construct in a hidden named group and runs the instrumented regex, so the positions are what the engine actually did, not a simulation.

### Repeated groups

When a group is repeated, its step has one child per iteration. This makes it easy to see how the engine split the text between iterations:

```csharp
Pattern phone = Pattern.With
    .NonCapturingGroup(Pattern.With.Digit.Repeat.Times(3).Literal("-")).Repeat.OneOrMore
    .Digit.Repeat.Times(4);

Console.WriteLine(phone.Trace("555-123-4567"));
```

```text
Pattern: (?:\d{3}-)+\d{4}
Input:   "555-123-4567"
Result:  MATCH at 0, length 12: "555-123-4567"

  [0..8)    (?:\d{3}-)+                      "555-123-"       group, one or more times
    [0..4)    (?:\d{3}-)                     "555-"           group
      [0..3)    \d{3}                        "555"            a digit, exactly 3 times
      [3..4)    -                            "-"              the character '-'
    [4..8)    (?:\d{3}-)                     "123-"           group
      [4..7)    \d{3}                        "123"            a digit, exactly 3 times
      [7..8)    -                            "-"              the character '-'
  [8..12)   \d{4}                            "4567"           a digit, exactly 4 times
```

- The quantified group `(?:\d{3}-)+` is one step. Its children are the two iterations, `"555-"` and `"123-"`.
- Each iteration has its own children, so you see the parts of every iteration.
- A quantifier on a single construct, such as `\d{4}` or `.+`, is one step. Only repeated groups get per-iteration children.

### Alternatives

For an alternation, only the branch that matched appears in the trace:

```csharp
Pattern pets = Pattern.With.OneOf("cat", "dog").Literal("s");

Console.WriteLine(pets.Trace("dogs"));
```

```text
Pattern: (?:cat|dog)s
Input:   "dogs"
Result:  MATCH at 0, length 4: "dogs"

  [0..3)    (?:cat|dog)                      "dog"            group
    [0..3)    dog                            "dog"            the text "dog"
      [0..3)    dog                          "dog"            the text "dog"
  [3..4)    s                                "s"              the character 's'
```

- The `cat` branch did not match, so it has no step.
- A branch of more than one construct is a step of its own, and the literal text inside it is a child step. That is why a branch made only of text, like `dog`, shows the same text on two levels.

### Lookarounds, backreferences and comments

Lookarounds are zero-width steps. `Trace` does not look inside them:

```csharp
Pattern password = Pattern.FromRegex(@"^(?=.*\d)\w{8,}$");

Console.WriteLine(password.Trace("password1"));
```

```text
Pattern: ^(?=.*\d)\w{8,}$
Input:   "password1"
Result:  MATCH at 0, length 9: "password1"

  [0..0)    ^                                ""               start of input
  [0..0)    (?=.*\d)                         ""               followed by
  [0..9)    \w{8,}                           "password1"      a word character, at least 8 times
  [9..9)    $                                ""               end of input
```

Backreferences show the text they repeated:

```csharp
Pattern repeated = Pattern.With.Group(Pattern.With.Word.Repeat.OneOrMore).Whitespace.Backreference(1);

Console.WriteLine(repeated.Trace("hello hello"));
```

```text
Pattern: (\w+)\s\1
Input:   "hello hello"
Result:  MATCH at 0, length 11: "hello hello"

  [0..5)    (\w+)                            "hello"          capture group #1
    [0..5)    \w+                            "hello"          a word character, one or more times
  [5..6)    \s                               " "              a whitespace character
  [6..11)   \1                               "hello"          the same text captured by group #1
```

- Numbered backreferences such as `\1` and named ones such as `\k<word>` both work.
- An inline comment `(?#...)` is reported as a zero-length step described as `a comment`.
- An empty alternative gets no step of its own. When the empty branch of `(?:a|)` is taken, the group is a step with the value `""` and no children.

### Where the match starts

`Trace` finds the same match as `Match(input)`: the first one, from the left. Without anchors, that match does not have to start at position 0:

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

Console.WriteLine(digits.Trace("order 42 shipped"));
```

```text
Pattern: \d+
Input:   "order 42 shipped"
Result:  MATCH at 6, length 2: "42"

  [6..8)    \d+                              "42"             a digit, one or more times
```

Lazy quantifiers are traced as the engine ran them. In `<.+?>` over `"<b>bold</b>"`, the step for `.+?` is `[1..2)` with the value `"b"`, and the match is `"<b>"`.

## Working with the trace as data

The report is built from plain objects. Use them when you want to assert on a trace in a test, or build your own message.

### MatchTrace

| Member | Type | Meaning |
|--------|------|---------|
| `Pattern` | `string` | The regex string that was traced. Same as `pattern.ToString()`. |
| `Input` | `string` | The traced input. |
| `IsMatch` | `bool` | `true` when the pattern matched. |
| `Index` | `int` | The start of the match, or `-1` when there is no match. |
| `Length` | `int` | The length of the match, or `0` when there is no match. |
| `Value` | `string` | The matched text, or `null` when there is no match. |
| `Steps` | `IReadOnlyList<TraceStep>` | The top-level steps of a match. Empty when there is no match. |
| `Failure` | `TraceFailure` | Where and why matching stopped. `null` when the pattern matched. |
| `ToString()` | `string` | The readable report shown on this page. |

### TraceStep

| Member | Type | Meaning |
|--------|------|---------|
| `Construct` | `string` | The regex text of the construct, such as `(?<year>\d{4})`. |
| `Description` | `string` | A plain-language description, such as `group 'year'`. |
| `Index` | `int` | Where in the input the construct started matching. |
| `Length` | `int` | How many characters it consumed. `0` for anchors and lookarounds. |
| `Value` | `string` | The consumed text. |
| `Children` | `IReadOnlyList<TraceStep>` | The steps of the construct's parts. A repeated group has one child per iteration. Empty for a construct with no parts. |
| `ToString()` | `string` | A one-line summary: `Construct => "Value" at Index`. |

```csharp
// date is the pattern from "Tracing a match"
MatchTrace trace = date.Trace("2024-05-17");

Console.WriteLine(trace.IsMatch);         // True
Console.WriteLine(trace.Index);           // 0
Console.WriteLine(trace.Length);          // 10
Console.WriteLine(trace.Value);           // 2024-05-17
Console.WriteLine(trace.Steps.Count);     // 7
Console.WriteLine(trace.Failure == null); // True

TraceStep year = trace.Steps[1];

Console.WriteLine(year.Description);      // group 'year'
Console.WriteLine(year.Index);            // 0
Console.WriteLine(year.Length);           // 4
Console.WriteLine(year.Children.Count);   // 1
Console.WriteLine(year);                  // (?<year>\d{4}) => "2024" at 0
```

- `Steps` holds the top-level constructs only. The seven steps here are `^`, the year group, `-`, the month group, `-`, the day group and `$`.
- Steps are sorted by position. Constructs that start at the same position keep their order in the pattern.

### Walking the steps

`Children` makes the trace a tree. A small recursive method visits every step:

```csharp
using System;
using System.Collections.Generic;
using Masterly.FluentRegex;

Pattern log = Pattern.With
    .StartOfLine
    .NamedGroup("time", Pattern.With.Digit.Repeat.Times(2).Literal(":").Digit.Repeat.Times(2))
    .Literal(" ")
    .NamedGroup("level", Pattern.With.OneOf("INFO", "WARN", "ERROR"))
    .Literal(": ")
    .NamedGroup("message", Pattern.With.Anything.Repeat.OneOrMore)
    .EndOfLine;

PrintSteps(log.Trace("10:30 WARN: disk at 91%").Steps, 0);

static void PrintSteps(IReadOnlyList<TraceStep> steps, int depth)
{
    foreach (TraceStep step in steps)
    {
        Console.WriteLine($"{new string(' ', depth * 2)}{step.Description}: \"{step.Value}\"");
        PrintSteps(step.Children, depth + 1);
    }
}
```

```text
start of input: ""
group 'time': "10:30"
  a digit, exactly 2 times: "10"
  the character ':': ":"
  a digit, exactly 2 times: "30"
the character '␣': " "
group 'level': "WARN"
  group: "WARN"
    the text "WARN": "WARN"
      the text "WARN": "WARN"
the text ": ": ": "
group 'message': "disk at 91%"
  any character except newline, one or more times: "disk at 91%"
end of input: ""
```

- Descriptions show a space as `␣`, so a space literal is visible.
- To read a group's value in production code, use `Match` and its `Groups`, or [typed extraction](./typed-extraction.md). The trace is for understanding a match, not for extracting data.

## Diagnosing a failed match

When the pattern does not match, `Failure` explains why.

| Member | Type | Meaning |
|--------|------|---------|
| `Position` | `int` | The input position where the pattern could not continue. |
| `Construct` | `string` | The regex text of the construct that failed, such as `\d{2}`. |
| `Description` | `string` | A plain-language description of what was expected, such as `a digit, exactly 2 times`. |
| `MatchedSoFar` | `string` | The text the constructs before the failure matched. |
| `Found` | `string` | The input from `Position` on, in double quotes. When more than 16 characters remain, it is the first 16 followed by `...`, inside the quotes. At the end of the input it is the text `the end of the input`, without quotes. |
| `MatchedSteps` | `IReadOnlyList<TraceStep>` | The trace of the top-level constructs that matched before the failure. When the failure is inside a group that had already consumed text, the steps stop before that group (see [Common failure shapes](#common-failure-shapes)). |

```csharp
// date is the pattern from "Tracing a match"
MatchTrace trace = date.Trace("2024-5-17");
TraceFailure? failure = trace.Failure;

Console.WriteLine(trace.Index);                 // -1
Console.WriteLine(trace.Value == null);         // True
Console.WriteLine(trace.Steps.Count);           // 0
Console.WriteLine(failure!.Position);           // 5
Console.WriteLine(failure.Construct);           // \d{2}
Console.WriteLine(failure.Description);         // a digit, exactly 2 times
Console.WriteLine(failure.MatchedSoFar);        // 2024-
Console.WriteLine(failure.Found);               // "5-17"
Console.WriteLine(failure.MatchedSteps.Count);  // 3
```

- On a failure, `Steps` is empty. The steps that did match are in `Failure.MatchedSteps`.
- `Found` includes the double quotes. Do not add your own.
- `Failure` is `null` when the pattern matched. In rare cases it can also be `null` on a failure, when no single construct can be blamed. Always check it before you use it. (The `!` above is safe because this input is known to fail.)

### How the failure position is found

`Trace` does not guess. It matches ever-longer prefixes of the pattern against the input with the real engine:

1. It finds the longest prefix of the top-level constructs that still matches. The next construct is the one that failed.
2. If that construct is a plain group (capturing, non-capturing, named or atomic, but not a lookaround or a balancing group), it repeats the search inside the group, to point at the exact part that failed. If the group's body is an alternation, every branch is searched. This descent goes one level deep only: a group nested inside that group is blamed as a whole.
3. If the pattern is an alternation at the top level, it does this for every branch and blames the branch that got furthest. When two branches stop at the same position, the deeper construct wins.

Because the search is done on single constructs, literal text is checked character by character. The failure points at the exact character, not at the whole literal:

```csharp
// log is the pattern from "Walking the steps"
TraceFailure? failure = log.Trace("10:30 WARN disk").Failure;

Console.WriteLine(failure!.Position);      // 10
Console.WriteLine(failure.Construct);      // :
Console.WriteLine(failure.MatchedSoFar);   // 10:30 WARN
Console.WriteLine(failure.Found);          // " disk"
```

### Common failure shapes

**Input too short.** The failure is at the end of the input:

```csharp
// date is the pattern from "Tracing a match"
Console.WriteLine(date.Trace("2024-05"));
```

```text
Pattern: ^(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})$
Input:   "2024-05"
Result:  NO MATCH

Matched before the failure:
  [0..0)    ^                                ""               start of input
  [0..4)    (?<year>\d{4})                   "2024"           group 'year'
    [0..4)    \d{4}                          "2024"           a digit, exactly 4 times
  [4..5)    -                                "-"              the character '-'
  [5..7)    (?<month>\d{2})                  "05"             group 'month'
    [5..7)    \d{2}                          "05"             a digit, exactly 2 times

Failed at position 7: expected - (the character '-')
Found:   the end of the input

  2024-05
         ^
```

**Extra text at the end.** Everything matched, but the end anchor did not. `date.Trace("2024-05-17x")` reports `Failed at position 10: expected $ (end of input)` and `Found:   "x"`.

**None of the alternatives.** In the log pattern, `OneOf` puts the alternatives in a `(?:...)` group inside the `level` group. That inner group is two levels deep, so the search blames it as a whole:

```csharp
// log is the pattern from "Walking the steps"
Console.WriteLine(log.Trace("10:30 DEBUG: cache miss"));
```

```text
Pattern: ^(?<time>\d{2}:\d{2}) (?<level>(?:INFO|WARN|ERROR)): (?<message>.+)$
Input:   "10:30 DEBUG: cache miss"
Result:  NO MATCH

Matched before the failure:
  [0..0)    ^                                ""               start of input
  [0..5)    (?<time>\d{2}:\d{2})             "10:30"          group 'time'
    [0..2)    \d{2}                          "10"             a digit, exactly 2 times
    [2..3)    :                              ":"              the character ':'
    [3..5)    \d{2}                          "30"             a digit, exactly 2 times
  [5..6)                                     " "              the character '␣'

Failed at position 6: expected (?:INFO|WARN|ERROR) (group)
Found:   "DEBUG: cache mis..."

  10:30 DEBUG: cache miss
        ^
```

When the alternatives are one level deep, every branch is searched, and the first branch that got furthest is blamed. If no branch can start, that is the first character of the first branch. For `^(?<level>INFO|WARN|ERROR):` and `"DEBUG:"`, the report says `Failed at position 0: expected I (the character 'I')`. For the input `"WARM:"`, it says `Failed at position 3: expected N (the character 'N')`.

**A failure inside a group.** The report points at the part inside the group:

```csharp
// log is the pattern from "Walking the steps"
MatchTrace trace = log.Trace("10:3 WARN: x");

Console.WriteLine(trace.Failure!.MatchedSoFar);        // 10:
Console.WriteLine(trace.Failure.MatchedSteps.Count);   // 1
Console.WriteLine(trace);
```

```text
Pattern: ^(?<time>\d{2}:\d{2}) (?<level>(?:INFO|WARN|ERROR)): (?<message>.+)$
Input:   "10:3 WARN: x"
Result:  NO MATCH

Matched before the failure:
  [0..0)    ^                                ""               start of input

Failed at position 3: expected \d{2} (a digit, exactly 2 times)
Found:   "3 WARN: x"

  10:3 WARN: x
     ^
```

:::note

When the failure is inside a group that had already consumed some text, `MatchedSteps` lists the constructs before that group, here only `^`. The group's own partial match is not a step, but `MatchedSoFar` holds all the text that matched, here `"10:"`.

:::

**A top-level alternation.** The branch that got furthest is blamed. For `abc|x` and the input `"abd"`, the report says `Failed at position 2: expected c (the character 'c')`, with `ab` as the only matched step.

### Limits of the failure diagnosis

The diagnosis looks at the first match of each prefix. That is usually what you want, but keep these cases in mind:

- **Lazy quantifiers.** A lazy prefix matches as little as possible, so the failure position can be earlier than you expect. For `^\w+?=` and `"port:8080"`, the prefix `^\w+?` matches only `"p"`, and the report says `Failed at position 1: expected =`. The real problem, the `:`, is at position 4.
- **Unanchored patterns.** The prefix match is the first one from the left, not the one that got furthest. For `SKU-\d{4}` and `"SKU-1 and SKU-123"`, the report points at position 4, after the first `SKU-`. Anchor the pattern when you trace a whole value.
- **Repeated groups and lookarounds** are blamed as a whole. The search does not look inside them. For `^(?=.*\d)\w{8,}$` and `"password"`, the report says `expected (?=.*\d) (followed by)` at position 0.
- **Nested groups** are blamed as a whole below the first level. For `^(?<a>x(?<b>yz))` and `"xyq"`, the report says `Failed at position 1: expected (?<b>yz) (group 'b')`, not the `z` at position 2.

## Formatting the report

`MatchTrace.ToString()` builds the report shown on this page. It is the same text that the command-line tool and the Playground print.

| Part | Format |
|------|--------|
| Header | `Pattern:`, `Input:` (in quotes) and `Result:`. A match shows `MATCH at <index>, length <length>: "<value>"`. A failure shows `NO MATCH`. |
| Steps | One line per step: span, construct, quoted value, description. Each nesting level adds two spaces of indentation. |
| Long constructs | A construct longer than 32 characters is cut to its first 29 characters and `...`. |
| Failure | The matched steps under `Matched before the failure:` (left out when there are none), then `Failed at position <n>: expected <construct> (<description>)` and `Found:`. When `Failure` is `null`, the report ends after `Result:  NO MATCH`. |
| Caret line | The input again, with a `^` under the failure position. |

Control characters are escaped in the `Input:` line, the caret line and the quoted step values, so a tab is shown as `\t`, a newline as `\n` and other control characters as `\uXXXX`. The caret stays aligned with the escaped text:

```csharp
Pattern word = Pattern.FromRegex(@"^\w+$");

Console.WriteLine(word.Trace("tab\there"));
```

```text
Pattern: ^\w+$
Input:   "tab\there"
Result:  NO MATCH

Matched before the failure:
  [0..0)    ^                                ""               start of input
  [0..3)    \w+                              "tab"            a word character, one or more times

Failed at position 3: expected $ (end of input)
Found:   "\there"

  tab\there
     ^
```

:::note

The report escapes control characters on every line, the `Found:` line included. The `Failure.Found` and `Failure.MatchedSoFar` properties hold the raw input text, so escape them yourself before you show them in HTML, logs or a single-line message.

:::

### Writing your own message

The report is meant for developers. For an end user, build a shorter message from `Failure`:

```csharp
static string Explain(Pattern pattern, string input)
{
    MatchTrace trace = pattern.Trace(input);
    if (trace.IsMatch)
    {
        return "OK";
    }

    TraceFailure? failure = trace.Failure;
    return failure == null
        ? "The value does not match."
        : $"At character {failure.Position + 1}: expected {failure.Description}, found {failure.Found}.";
}

Console.WriteLine(Explain(CommonPatterns.DateIso(), "2024-13-5"));   // At character 9: expected one of [0-3], found "5".
Console.WriteLine(Explain(CommonPatterns.DateIso(), "2024-12-05"));  // OK
```

- `Position` is zero-based. Add one when you show it to a person.
- The description is written for people who know a little about patterns. Map it to your own wording when the audience is not technical.

:::warning

The trace contains the input. Do not write a trace, or `MatchedSoFar` and `Found`, to logs when the input can hold personal data such as an email address or a card number. See [PII Redaction](./pii-redaction.md).

:::

## Options, timeouts and errors

`Trace` uses the pattern's own options and timeout:

```csharp
using System.Text.RegularExpressions;

Pattern hex = Pattern.FromRegex("^0x[0-9a-f]+$", RegexOptions.IgnoreCase);

Console.WriteLine(hex.Trace("0XFF").IsMatch); // True
```

- `IgnoreCase`, `Multiline`, `Singleline`, `IgnorePatternWhitespace` and the other options apply to the matching. The descriptions of anchors and `.` follow `Multiline` and `Singleline`: with `Multiline`, `^` is described as `start of line`, and without it as `start of input`; with `Singleline`, `.` is `any character` instead of `any character except newline`. `IgnoreCase` does not change the descriptions, so `[a-z]+` is still `one of [a-z], one or more times`.
- `RegexOptions.Compiled` is removed for tracing. Every trace builds new, short-lived regexes, so compiling them would only cost time.
- With `RegexOptions.RightToLeft`, the result is correct (`IsMatch`, `Index`, `Length`, `Value`), but `Steps` is empty.
- With `UseNonBacktracking()` (net8.0 and net10.0 only), the .NET engine keeps only the last capture of each group, so a repeated group shows only its last iteration. For `(?:\d{3}-)+\d{4}` and `"555-123-4567"`, the `(?:\d{3}-)+` step spans `"555-123-"` but has a single child, `"123-"`.
- When the instrumented regex cannot be built for an unusual pattern, `Trace` still reports the match, with an empty `Steps` list.

### Timeouts

The pattern's timeout (see `WithTimeout`) applies to every regex that `Trace` runs.

- If the main match times out, `Trace` throws `RegexMatchTimeoutException`, exactly as `Match` would.
- While diagnosing a failure, a prefix probe that times out is skipped, and a shorter prefix is blamed instead.
- The instrumented regex that builds `Steps` and `Failure.MatchedSteps` is not protected the same way. If it runs past the timeout, `Trace` throws `RegexMatchTimeoutException` too.

### Exceptions

| Exception | When |
|-----------|------|
| `ArgumentNullException` | `input` is `null`. `ParamName` is `"input"`. |
| `InvalidOperationException` | The pattern text cannot be parsed, for example raw text added with `RegEx("(abc")`. The message starts with `The pattern is not valid regex syntax:`. A constant invalid pattern is already reported at compile time as FRX003. See [Compile-Time Checks](./compile-time-checks.md). |
| `ArgumentException` | The pattern text parses, but the .NET engine rejects it, for example raw text `(a)\2` built at run time, which refers to an undefined group. On .NET 8 and later this is a `RegexParseException`, with a message such as `Invalid pattern '(a)\2' at offset 5. Reference to undefined group number 2.` |
| `RegexMatchTimeoutException` | A match ran longer than the pattern's timeout. See [Timeouts](#timeouts). |

## Where tracing is used for you

Several features call `Trace` and put its result into their own errors.

### Semantic rules

`ConstrainedPattern.Check(input)` returns a `ConstraintResult`. When the pattern itself does not match, its `Trace` property holds the `MatchTrace` of the whole-input match:

```csharp
ConstrainedPattern isoDate = CommonPatterns.DateIso().Must(Validators.IsoDate, "real calendar date");

ConstraintResult shape = isoDate.Check("2024-1-05");

Console.WriteLine(shape.PatternFailed);               // True
Console.WriteLine(shape.Trace!.Failure!.Position);    // 6
Console.WriteLine(shape);                             // Invalid: the value does not match the pattern at position 6: expected \d (a digit)

ConstraintResult rule = isoDate.Check("2024-02-30");

Console.WriteLine(rule.Trace == null);                // True
Console.WriteLine(rule);                              // Invalid: the rule 'real calendar date' rejected the value
```

- `Trace` is set only when the pattern failed. When a rule rejects the value, `Trace` is `null` and `FailedRule` names the rule.
- See [Semantic Rules](./semantic-rules.md).

### Templates

`Template<T>.Parse` throws a `FormatException` whose message comes from the failure trace:

```csharp
Template<Setting> setting = Template.Of<Setting>("{Key}={Value}");

setting.Parse("port=80x");
// throws FormatException: The input does not match the template "{Key}={Value}": at position 7,
// while reading the end of the template (the input has extra text after it), expected end of input
// (regex \z) but found "x".

public record Setting(string Key, int Value);
```

See [Templates](./templates.md).

### Typed extraction

`Extract<T>` throws a `FormatException` when the input does not match. The message points you to `Trace`:

```csharp
// Setting is the record from "Templates"
Pattern pair = Pattern.FromRegex(@"^(?<Key>\w+)=(?<Value>\d+)$");

try
{
    pair.Extract<Setting>("port:8080");
}
catch (FormatException error)
{
    // error.Message: The input does not match the pattern ^(?<Key>\w+)=(?<Value>\d+)$.
    //                Use Trace(input) to see where it fails.
    TraceFailure? failure = pair.Trace("port:8080").Failure;

    Console.WriteLine(failure!.Position);   // 4
    Console.WriteLine(failure.Construct);   // =
    Console.WriteLine(failure.Found);       // ":8080"
}
```

See [Typed Extraction](./typed-extraction.md).

### From the command line

The `fluentregex trace` command prints the same report. Pass regex options with `--options`, separated by commas:

```bash
fluentregex trace "^\d{4}-\d{2}-\d{2}$" "2024-5-17"
fluentregex trace "^[a-z]+$" "HELLO" --options IgnoreCase
```

The first command prints:

```text
Pattern: ^\d{4}-\d{2}-\d{2}$
Input:   "2024-5-17"
Result:  NO MATCH

Matched before the failure:
  [0..0)    ^                                ""               start of input
  [0..4)    \d{4}                            "2024"           a digit, exactly 4 times
  [4..5)    -                                "-"              the character '-'

Failed at position 5: expected \d{2} (a digit, exactly 2 times)
Found:   "5-17"

  2024-5-17
       ^
```

- In a terminal, the report is green for a match and yellow for a failure. When the output is redirected to a file or a pipe, it is plain text.
- The exit code is `0` for a match and for a failure. `trace` explains a result; it does not test it, so do not use its exit code as a pass or fail signal in a script.
- The exit code is `2` for a usage error, such as a missing `<input>` argument, an unknown option name or an invalid regex. The message goes to standard error, for example `error: Missing <input> argument. Run 'fluentregex --help'.`
- The global `--ascii-digits` flag works here too. It makes `\d` match only `0-9` before the trace runs.
- See [Command-Line Tool](./command-line-tool.md).

The [Playground](./playground.md) has a Trace tab that shows the same report as you type, under a one-line verdict built from `Value` and `Index`, or from `Failure.Description`, `Failure.Position` and `Failure.Found`.

## Performance and thread safety

- `Trace` is a diagnostic tool, not a matching method. It builds a new instrumented regex on every call. A failure builds one more regex for each prefix it tries. Do not call it on every request in a hot path. Call `IsMatch` or `Match` first, and trace only the inputs you need to explain.
- Nothing is cached between calls, and `Pattern` is immutable, so `Trace` is safe to call from several threads at once.
- A `MatchTrace` holds the input and every consumed value. Do not keep traces of large inputs around longer than you need them.

:::tip

In a unit test, assert on `Failure.Position` and `Failure.Construct` instead of the whole report. The report's column widths are for reading, and the structured members say exactly what you mean:

```csharp
using Xunit;

// date is the pattern from "Tracing a match"
MatchTrace trace = date.Trace("2024-5-17");

Assert.False(trace.IsMatch);
Assert.Equal(5, trace.Failure!.Position);
Assert.Equal(@"\d{2}", trace.Failure.Construct);
```

:::
