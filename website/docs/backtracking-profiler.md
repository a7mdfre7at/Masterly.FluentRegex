---
id: backtracking-profiler
title: "Backtracking Profiler"
description: "Count the steps and backtracks a pattern costs on a real input, see which construct does the work, and guard that cost in tests."
---

The backtracking profiler runs a pattern on one input and tells you how much work it did and where that work went. It counts every step and every backtrack, charges each one to the construct that caused it, and prints the result as a text heatmap over the pattern.

Use it when you want to know what a pattern actually costs. [ReDoS Safety](./redos-safety.md) reads a pattern and tells you it *might* backtrack catastrophically. The profiler runs it and shows you that it *does*, on which input, and which part of the pattern is to blame. It is also the quickest way to compare two versions of a pattern before you pick one.

```csharp
using System;
using Masterly.FluentRegex;

ProfileReport report = Pattern.FromRegex(@"(a+)+$").Profile("aaaaaaaaaaaaaaaaaaaaaaaa!");

Console.WriteLine(report.BudgetExhausted);      // True
Console.WriteLine(report.Steps);                // 1000000
Console.WriteLine(report.Hotspots[0].Fragment); // a+
Console.WriteLine(report);                      // prints the heatmap shown below
```

```text
Pattern  (a+)+$
Heat     ▃██▃▃▃
Input    25 characters
Result   gave up: the step budget of 1,000,000 ran out after 333,317 backtracks
Engine   not checked, because the step budget ran out

    Share        Steps   Backtracks  Construct
    66.7%      666,682      166,662  a+  (at 1)
    16.7%      166,663      166,655  (a+)+  (at 0)
    16.7%      166,655            0  $  (at 5)

Verdict  catastrophic backtracking, mostly in 'a+'. Make it atomic or possessive, or remove the nested or overlapping repetition.
```

- The pattern `(a+)+$` can split a run of `a`s between the inner and outer loop in exponentially many ways. The `!` at the end makes every split fail, so the matcher tries them all.
- The profiler stops after 1,000,000 steps by default, so even an exponential pattern returns in milliseconds. `BudgetExhausted` tells you it gave up.
- `a+` did two thirds of the work. That is the construct to fix.

The profiler lives on `Pattern` in the `Masterly.FluentRegex` namespace. It needs no extra package and works on every target of the library: `netstandard2.1`, `net8.0` and `net10.0`.

| You want to | Use |
|-------------|-----|
| Know whether a pattern *can* backtrack catastrophically, without an input | `AnalyzeSafety()` (see [ReDoS Safety](./redos-safety.md)) |
| Know how much work a pattern *does* on a given input, and where | `Profile(input)` (this page) |
| Watch the matcher step through one input, decision by decision | [Match Tracing](./match-tracing.md) |
| Be warned while you type the pattern | The analyzer (see [Compile-Time Checks](./compile-time-checks.md)) |

## Profiling a pattern

Call `Profile` on any pattern: one built with the fluent API, one parsed with `Pattern.FromRegex`, or one from the [pattern library](./common-patterns.md).

```csharp
public ProfileReport Profile(string input, ProfileOptions? options = null);
```

| Parameter | Meaning |
|-----------|---------|
| `input` | The text to match. Pick the input you are worried about, such as the longest or most hostile one your application can receive. |
| `options` | Limits for the run. `null` (the default) means a step budget of 1,000,000. See [Setting the step budget](#setting-the-step-budget). |

`Profile` returns a `ProfileReport`. It never returns `null`.

| Exception | Thrown when |
|-----------|-------------|
| `ArgumentNullException` | `input` is `null`. |
| `NotSupportedException` | The pattern uses a construct the profiler does not emulate. The message names the construct and its position. See [Unsupported constructs](#unsupported-constructs). |
| `InvalidOperationException` | The pattern is not valid regex syntax, for example raw text added with `RegEx(...)`. |

```csharp
using System;
using Masterly.FluentRegex;

// broken comes from your application, for example a pattern read from configuration
string broken = "(abc";

try
{
    Pattern.With.RegEx(broken).Profile("abc");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message); // The pattern is not valid regex syntax: Not enough )'s (at position 0)
}
```

- `Pattern.FromRegex` already rejects invalid syntax with an `ArgumentException`, so a pattern built that way never reaches this `InvalidOperationException`.
- The pattern's options are part of the run. `RegexOptions.IgnoreCase`, `Multiline`, `Singleline`, `ExplicitCapture`, `IgnorePatternWhitespace` and `ECMAScript` change what the interpreter matches, just as they change the real engine.
- Each call parses the pattern, builds its own interpreter and returns a new, read-only report. Nothing is cached between calls except the pattern's own `Regex`, which the [engine cross-check](#the-engine-cross-check) uses.
- The cross-check runs that `Regex` with the pattern's own timeout. If the pattern was built with `WithTimeout(...)` and the real engine takes longer than that on your input, `Profile` lets the `RegexMatchTimeoutException` through.

## Reading the report

`ProfileReport.ToString()` renders the report as text. Every line starts with a fixed-width label:

| Line | What it shows |
|------|---------------|
| `Pattern` | The regex that was profiled. Control characters, such as a tab, are shown as `·` here and in the table. |
| `Heat` | One shade per pattern character: how much work the construct under that character did. See [The heat row](#the-heat-row). |
| `Input` | The length of the input. |
| `Result` | What the interpreter found (`match at 6, length 5`, or `no match`) and the total steps and backtracks. When the budget ran out: `gave up: the step budget of ... ran out after ... backtracks`. |
| `Engine` | Whether the real .NET engine found the same result: `agrees with the .NET engine`, `DISAGREES: the .NET engine found ...`, or `not checked, because the step budget ran out`. |
| Table | The five most expensive constructs, with their share of all steps, their steps and their backtracks. |
| `Verdict` | A one-line summary and, for an expensive pattern, what to do about it. |

Here is a healthy pattern for comparison:

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.With.Digit.Repeat.OneOrMore.Profile("order 12345 shipped"));
```

```text
Pattern  \d+
Heat     ███
Input    19 characters
Result   match at 6, length 5: 24 steps, 1 backtrack
Engine   agrees with the .NET engine

    Share        Steps   Backtracks  Construct
   100.0%           24            1  \d+  (at 0)

Verdict  cheap: 1.3 steps per input character.
```

- `\d+` is tried at positions 0 to 5 and fails each time before it matches `12345` at position 6. Those failed starts are part of the 24 steps.
- The one backtrack is the loop giving up after the last digit, when it tries a sixth character and finds a space.

### The heat row

The `Heat` row puts one shade under each character of the pattern. A character takes the steps of the innermost construct that covers it, and the shade is scaled against the hottest construct in the report:

| Shade | Meaning |
|-------|---------|
| `█` | The hottest construct in this report, or one within about 7% of it. |
| `▇` `▆` `▅` `▄` `▃` `▂` `▁` | Less work, in seven steps down from the hottest. Any construct that did work gets at least `▁`. |
| (blank) | No work charged to this character, such as the parentheses of a capturing group. Blanks at the end of the row are trimmed. |

Shades are relative. A row full of `█` means the work is spread evenly, not that the pattern is slow. Read the `Result` and `Verdict` lines for the absolute cost.

### The hotspot table

The table lists at most five constructs, the most expensive first. Each row shows:

- **Share**: the construct's fraction of all steps.
- **Steps** and **Backtracks**: the work charged to it.
- **Construct**: its regex text and its zero-based position in the pattern, such as `a+  (at 1)`.

When two constructs have the same steps, the one with more backtracks comes first, then the one that starts earlier. The table stops at five rows. `report.Hotspots` always holds every construct that did any work.

### The verdict

The verdict is chosen in this order:

| Condition | Verdict |
|-----------|---------|
| The budget ran out | `catastrophic backtracking, mostly in '<hottest>'. Make it atomic or possessive, or remove the nested or overlapping repetition.` |
| The interpreter and the engine disagree | `the interpreter and the .NET engine disagree, so these counts may not describe the real engine.` |
| 10 steps per input character or fewer | `cheap: <rate>.` |
| More than 10, up to 100 | `moderate: <rate>, mostly in '<hottest>'.` |
| More than 100 | `heavy backtracking: <rate>, mostly in '<hottest>'. Consider an atomic group or a narrower class.` |

The rate is total steps divided by the input length. An empty input counts as one character.

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.FromRegex(@"\w+x").Profile(new string('a', 20)));
```

```text
Pattern  \w+x
Heat     ███▄
Input    20 characters
Result   no match: 672 steps, 210 backtracks
Engine   agrees with the .NET engine

    Share        Steps   Backtracks  Construct
    68.8%          462          210  \w+  (at 0)
    31.3%          210            0  x  (at 3)

Verdict  moderate: 33.6 steps per input character, mostly in '\w+'.
```

`\w+` grabs every remaining letter from each start position, then gives them back one at a time looking for an `x`. The cost grows with the square of the input length. Four loops in a row make it far worse:

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.FromRegex(@"\w*\w*\w*\w*x").Profile(new string('a', 14)));
```

```text
Pattern  \w*\w*\w*\w*x
Heat     ▁▁▁▁▁▁▃▃▃███▅
Input    14 characters
Result   no match: 42,604 steps, 15,488 backtracks
Engine   agrees with the .NET engine

    Share        Steps   Backtracks  Construct
    54.6%       23,256       11,628  \w*  (at 9)
    27.3%       11,628            0  x  (at 12)
    14.4%        6,120        3,060  \w*  (at 6)
     3.2%        1,360          680  \w*  (at 3)
     0.6%          240          120  \w*  (at 0)

Verdict  heavy backtracking: 3043.1 steps per input character, mostly in '\w*'. Consider an atomic group or a narrower class.
```

The heat row shows the work piling up in the last loop, which is re-run for every split the earlier loops try.

## Steps per construct

A **step** is one attempt by the matcher to apply a construct at a position. A **backtrack** is one return to a saved choice point to try something else. Each one is charged to a construct:

| Construct | A step is counted | A backtrack is charged | Reported as |
|-----------|-------------------|------------------------|-------------|
| Literal, character class, `.` | Each time it is tested against one character. | Never; it has no choices. | The character or class, such as `x` or `[^>]`. |
| Anchor (`^`, `$`, `\A`, `\z`, `\Z`, `\b`, `\B`) | Each time it is tested at a position. | Never. | The anchor, such as `$`. |
| Backreference (`\1`, `\k<name>`) | Each time it is compared at a position. | Never. | The reference, such as `\1`. |
| Quantifier (`*`, `+`, `?`, `{n,m}`, lazy forms) | Each time the loop decides whether to run its body again. | Each time the matcher returns to try one more or one fewer iteration. | The whole quantified construct, such as `(a+)+` or `\d{2}`. |
| Alternation (`a\|b\|c`) | Each time the matcher starts a branch that still has another branch after it. The last branch is not counted. | Each time the matcher comes back to try the next branch. | The alternatives, such as `x\|xy\|xyz`. |
| Atomic group, lookahead, lookbehind | Each time the group is entered. | Never on the group itself. | The group, such as `(?>...)` or `(?!un)`. |
| Capturing and non-capturing groups | Not counted. Their contents are. | Not charged. | Not listed. |

A single character, class, anchor or backreference that is directly quantified is reported **together with its quantifier**. The tests of `a` inside `a+` are charged to `a+`, so `a+` is one hotspot rather than two. A quantified group, such as `(\w+\s?)*`, is a hotspot of its own, and its contents keep their own hotspots.

Every step charged to a construct is part of the total, so the steps of all hotspots add up to `report.Steps` and their shares add up to 1.

### Alternation

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.FromRegex(@"(?:x|xy|xyz)+z").Profile("xyzxyz"));
```

```text
Pattern  (?:x|xy|xyz)+z
Heat     ▅▅▅▅█▅▂█▃██▅▅▃
Input    6 characters
Result   match at 0, length 3: 20 steps, 7 backtracks
Engine   agrees with the .NET engine

    Share        Steps   Backtracks  Construct
    30.0%            6            5  x|xy|xyz  (at 3)
    15.0%            3            2  (?:x|xy|xyz)+  (at 0)
    15.0%            3            0  x  (at 3)
    15.0%            3            0  x  (at 5)
    10.0%            2            0  x  (at 8)

Verdict  cheap: 3.3 steps per input character.
```

- The alternation `x|xy|xyz` collects most of the backtracks: each time a branch fails, the matcher comes back to try the next one.
- The three `x` rows are three different literals, at positions 3, 5 and 8 of the pattern. The position tells them apart.

### Lookarounds and backreferences

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.FromRegex(@"(?<=\$)\d+(?:\.\d{2})?").Profile("Total: $42.50"));
```

```text
Pattern  (?<=\$)\d+(?:\.\d{2})?
Heat     ███████▆▆▆▃▃▃▂▂▅▅▅▅▅▃▃
Input    13 characters
Result   match at 8, length 5: 32 steps, 1 backtrack
Engine   agrees with the .NET engine

    Share        Steps   Backtracks  Construct
    28.1%            9            0  (?<=\$)  (at 0)
    28.1%            9            0  \$  (at 4)
    18.8%            6            1  \d+  (at 7)
    15.6%            5            0  \d{2}  (at 15)
     6.3%            2            0  (?:\.\d{2})?  (at 10)

Verdict  cheap: 2.5 steps per input character.
```

- The lookbehind is entered once per start position, and its body `\$` is tested once each time. Lookbehinds run right to left from the current position, as in .NET.
- The fixed repetition `\d{2}` is one hotspot, like any other quantified character.

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.FromRegex(@"(\w+)\s\1").Profile("hello hello world"));
```

```text
Pattern  (\w+)\s\1
Heat      ███ ▂▂▂▂
Input    17 characters
Result   match at 0, length 11: 14 steps, 1 backtrack
Engine   agrees with the .NET engine

    Share        Steps   Backtracks  Construct
    85.7%           12            1  \w+  (at 1)
     7.1%            1            0  \s  (at 5)
     7.1%            1            0  \1  (at 7)

Verdict  cheap: 0.8 steps per input character.
```

The capturing group's parentheses are blank in the heat row: the group itself costs nothing. The backreference `\1` is one step, however long the captured text is.

## Finding a catastrophic pattern

Profile the pattern on the input that worries you most. A catastrophic pattern usually looks harmless on ordinary input and explodes on a near-miss: a long run of matching characters followed by one that breaks the match.

```csharp
using System;
using Masterly.FluentRegex;

Pattern suspect = Pattern.FromRegex(@"(a|aa)+$");

ProfileReport typical = suspect.Profile("aaaa");
ProfileReport hostile = suspect.Profile(new string('a', 30) + "!");

Console.WriteLine($"{typical.Steps} steps, exhausted: {typical.BudgetExhausted}"); // 17 steps, exhausted: False
Console.WriteLine($"{hostile.Steps} steps, exhausted: {hostile.BudgetExhausted}"); // 1000000 steps, exhausted: True
```

To see how the cost grows, profile inputs of increasing length with an unlimited budget. Keep the inputs short: an exponential pattern doubles its cost with every extra character.

```csharp
using System;
using Masterly.FluentRegex;

Pattern nested = Pattern.FromRegex(@"(a+)+$");
ProfileOptions unlimited = new ProfileOptions { StepBudget = long.MaxValue };

foreach (int length in new[] { 8, 10, 12, 14, 16 })
{
    ProfileReport report = nested.Profile(new string('a', length) + "!", unlimited);
    Console.WriteLine($"{length} characters: {report.Steps:N0} steps, {report.Backtracks:N0} backtracks");
}

// prints:
// 8 characters: 3,042 steps, 1,004 backtracks
// 10 characters: 12,252 steps, 4,072 backtracks
// 12 characters: 49,110 steps, 16,356 backtracks
// 14 characters: 196,560 steps, 65,504 backtracks
// 16 characters: 786,378 steps, 262,108 backtracks
```

Every two extra characters multiply the work by four. At that rate, 40 characters would need roughly 13 trillion steps (an extrapolation, not a run).

:::warning

An unlimited budget removes the only thing that stops an exponential pattern. Use `long.MaxValue` only with inputs you have sized yourself, as above. Never profile untrusted input without a budget.

:::

## Comparing a fix

The profiler is most useful side by side: profile the risky pattern and the fixed one on the same input.

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.FromRegex(@"^(\w+\s?)*$").Profile("an innocent looking sentence!"));
```

```text
Pattern  ^(\w+\s?)*$
Heat     ▁▃███▄▄▄▃▃▃
Input    29 characters
Result   gave up: the step budget of 1,000,000 ran out after 374,547 backtracks
Engine   not checked, because the step budget ran out

    Share        Steps   Backtracks  Construct
    50.0%      499,771      124,690  \w+  (at 2)
    25.0%      249,875      124,687  \s?  (at 5)
    12.5%      125,183      125,170  (\w+\s?)*  (at 1)
    12.5%      125,170            0  $  (at 10)
     0.0%            1            0  ^  (at 0)

Verdict  catastrophic backtracking, mostly in '\w+'. Make it atomic or possessive, or remove the nested or overlapping repetition.
```

Because `\s?` is optional, a word such as `innocent` can be split between iterations in many ways (`innocent`, `inno` + `cent`, `i` + `nnocent`, ...). Making each word atomic removes that choice:

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(Pattern.FromRegex(@"^(?>\w+\s?)*$").Profile("an innocent looking sentence!"));
```

```text
Pattern  ^(?>\w+\s?)*$
Heat     ▅▂▂▂███▂▂▂▂▂▂
Input    29 characters
Result   no match: 116 steps, 10 backtracks
Engine   agrees with the .NET engine

    Share        Steps   Backtracks  Construct
    51.7%           60            4  \w+  (at 4)
    25.9%           30            0  ^  (at 0)
     9.5%           11            1  \s?  (at 7)
     4.3%            5            5  (?>\w+\s?)*  (at 1)
     4.3%            5            0  (?>\w+\s?)  (at 1)

Verdict  cheap: 4 steps per input character.
```

From more than a million steps to 116, with the same result. The fluent API gives you the same fix with `AtomicGroup(...)`:

```csharp
using System;
using Masterly.FluentRegex;

string sentence = "an innocent looking sentence!";

Pattern risky = Pattern.With.StartOfLine
    .Group(Pattern.With.Word.Repeat.OneOrMore.Whitespace.Repeat.Optional).Repeat.ZeroOrMore
    .EndOfLine;
Pattern safe = Pattern.With.StartOfLine
    .AtomicGroup(Pattern.With.Word.Repeat.OneOrMore.Whitespace.Repeat.Optional).Repeat.ZeroOrMore
    .EndOfLine;

ProfileReport before = risky.Profile(sentence);
ProfileReport after = safe.Profile(sentence);

Console.WriteLine(safe.ToString());                                                // ^(?>\w+\s?)*$
Console.WriteLine($"before: {before.Steps:N0} steps, {before.BudgetExhausted}"); // before: 1,000,000 steps, True
Console.WriteLine($"after: {after.Steps:N0} steps, {after.BudgetExhausted}");    // after: 116 steps, False
```

- `.Possessive` on a quantifier produces an atomic group too: `Pattern.With.Word.Repeat.OneOrMore.Possessive` is `(?>\w+)`.
- A cheap verdict on one input does not prove a pattern safe on every input. Confirm a fix with [ReDoS Safety](./redos-safety.md), and keep a profiling test for the worst input you know (see [Guarding a pattern in a test](#guarding-a-pattern-in-a-test)).

The same comparison works for a pattern that is merely slow. On the input `<b>bold</b> text`, `<.+>` takes 40 steps, the lazy `<.+?>` takes 5 and `<[^>]+>` takes 6:

```csharp
using System;
using Masterly.FluentRegex;

string html = "<b>bold</b> text";

long greedy = Pattern.FromRegex(@"<.+>").Profile(html).Steps;     // 40
long lazy = Pattern.FromRegex(@"<.+?>").Profile(html).Steps;      // 5
long negated = Pattern.FromRegex(@"<[^>]+>").Profile(html).Steps; // 6
```

The greedy and lazy versions also match different text (`<b>bold</b>` against `<b>`), so check the result as well as the cost.

## Using the report in code

Everything in the text report is also available as properties, so a test or a tool can act on it.

| Member | Type | Meaning |
|--------|------|---------|
| `IsMatch` | `bool` | The interpreter found a match. `false` when the budget ran out. |
| `MatchIndex` | `int` | Where the match starts, or `-1` when there is none. |
| `MatchLength` | `int` | The length of the match, or `0` when there is none. |
| `Steps` | `long` | Total steps. Equal to the budget when the budget ran out. |
| `Backtracks` | `long` | Total backtracks. |
| `BudgetExhausted` | `bool` | The matcher hit `ProfileOptions.StepBudget` before it finished: catastrophic backtracking on this input, or a budget that is too small. |
| `AgreesWithEngine` | `bool` | The real .NET engine found the same result (match or not, index and length). `false` when it disagrees, and also when the budget ran out, because no comparison is made then. |
| `Hotspots` | `IReadOnlyList<ProfileHotspot>` | Every construct that did any work, the most expensive first. Empty when nothing was counted, for example for the empty pattern `(?:)`. |
| `ToString()` | `string` | The text report described in [Reading the report](#reading-the-report). |

```csharp
using System;
using Masterly.FluentRegex;

ProfileReport report = Pattern.With.Digit.Repeat.OneOrMore.Profile("order 12345 shipped");

Console.WriteLine(report.IsMatch);          // True
Console.WriteLine(report.MatchIndex);       // 6
Console.WriteLine(report.MatchLength);      // 5
Console.WriteLine(report.Steps);            // 24
Console.WriteLine(report.Backtracks);       // 1
Console.WriteLine(report.BudgetExhausted);  // False
Console.WriteLine(report.AgreesWithEngine); // True
Console.WriteLine(report.Hotspots.Count);   // 1
```

### Hotspots

Each `ProfileHotspot` describes one construct and the work charged to it.

| Member | Type | Meaning |
|--------|------|---------|
| `Fragment` | `string` | The regex text of the construct, such as `a+` or `(?=\d)`. |
| `Start` | `int` | Its zero-based position in the regex string. |
| `Length` | `int` | Its length in the regex string. |
| `Steps` | `long` | Steps charged to it. |
| `Backtracks` | `long` | Backtracks charged to it. |
| `Share` | `double` | Its fraction of all steps, from 0 to 1. |
| `ToString()` | `string` | A one-line summary, such as `'.+' at 1: 80.0% of steps, 32 steps, 7 backtracks`. |

```csharp
using System;
using Masterly.FluentRegex;

ProfileReport report = Pattern.FromRegex(@"<.+>").Profile("<b>bold</b> text");

foreach (ProfileHotspot hotspot in report.Hotspots)
{
    Console.WriteLine(hotspot);
}

// prints:
// '.+' at 1: 80.0% of steps, 32 steps, 7 backtracks
// '>' at 3: 17.5% of steps, 7 steps, 0 backtracks
// '<' at 0: 2.5% of steps, 1 step, 0 backtracks

ProfileHotspot hottest = report.Hotspots[0];
Console.WriteLine($"{hottest.Fragment} {hottest.Start} {hottest.Length} {hottest.Share}"); // .+ 1 2 0.8
```

- `Start` and `Length` point into the regex string, so `pattern.ToString().Substring(hottest.Start, hottest.Length)` is the fragment. Use them to underline the construct in your own tooling.
- Hotspots that did no work at all are left out. A branch the matcher never reached does not appear.
- The text table shows the top five. `Hotspots` keeps them all: `\w*\w*\w*\w*\w*x` on `aaaaaa` has six hotspots, and only five appear in the text.
- Percentages and counts in `ToString()` use the invariant culture (`80.0%`, `1,000`), whatever the current culture is.

## Setting the step budget

`ProfileOptions` has one setting: the number of steps the interpreter may take before it gives up.

| Property | Type | Default | Rules |
|----------|------|---------|-------|
| `StepBudget` | `long` | `1,000,000` | Must be positive. Zero or a negative value throws `ArgumentOutOfRangeException` when you set it. |

```csharp
using System;
using Masterly.FluentRegex;

Console.WriteLine(new ProfileOptions().StepBudget); // 1000000

try
{
    ProfileOptions invalid = new ProfileOptions { StepBudget = 0 };
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine(ex.Message); // prints: StepBudget must be positive. (Parameter 'StepBudget')
                                   //         Actual value was 0.
}
```

When the budget runs out, the report says so and stops there:

```csharp
using System;
using Masterly.FluentRegex;

ProfileOptions tiny = new ProfileOptions { StepBudget = 5 };
ProfileReport report = Pattern.With.Digit.Repeat.OneOrMore.Profile("abcdef123", tiny);

Console.WriteLine(report.BudgetExhausted);  // True
Console.WriteLine(report.Steps);            // 5
Console.WriteLine(report.IsMatch);          // False
Console.WriteLine(report.MatchIndex);       // -1
Console.WriteLine(report.AgreesWithEngine); // False
```

- `IsMatch` is `false`, `MatchIndex` is `-1` and `MatchLength` is `0`: the interpreter never finished, so there is no result to report.
- The real engine is not run, so `AgreesWithEngine` is `false` and the `Engine` line reads `not checked, because the step budget ran out`.
- The verdict reads `catastrophic backtracking`, even here, where `\d+` is perfectly healthy and the budget was just too small. Choose a budget that a healthy pattern can finish on your input. The verdict calls a pattern moderate up to 100 steps per input character, so a budget of at least 100 times the input length lets every cheap or moderate pattern finish.

:::tip

A budget is a cut-off, not a measurement. Two patterns that both exhaust a 1,000,000-step budget may differ by orders of magnitude. To compare them, profile shorter inputs that both finish, or raise the budget.

:::

## Guarding a pattern in a test

A unit test can pin a pattern's cost on its worst known input. Pick a budget well above what the pattern needs today, so the test fails only when someone reintroduces a backtracking problem.

```csharp
using System.Linq;
using Masterly.FluentRegex;
using Xunit;

public class SentencePatternTests
{
    private static readonly Pattern Sentence = Pattern.With.StartOfLine
        .AtomicGroup(Pattern.With.Word.Repeat.OneOrMore.Whitespace.Repeat.Optional).Repeat.ZeroOrMore
        .EndOfLine;

    [Fact]
    public void Sentence_OnWorstCaseInput_StaysWithinStepBudget()
    {
        string worstCase = string.Concat(Enumerable.Repeat("word ", 1000)) + "!";
        ProfileOptions guard = new ProfileOptions { StepBudget = 50_000 };

        ProfileReport report = Sentence.Profile(worstCase, guard);

        Assert.False(report.BudgetExhausted, report.ToString());
        Assert.True(report.AgreesWithEngine, report.ToString());
    }
}
```

- The atomic version needs 21,007 steps on this 5,001-character input, well inside the 50,000-step guard. The non-atomic `^(\w+\s?)*$` exhausts the same budget.
- Passing `report.ToString()` as the assertion message puts the whole heatmap in the test output when the test fails.
- Checking `AgreesWithEngine` makes sure the counts describe a pattern the profiler understood.
- For more ways to test patterns, see [xUnit Integration](./xunit.md), [Property Testing](./property-testing.md) and [Test Data Generation](./test-data-generation.md).

## Running the profiler from the command line

The `fluentregex` tool runs the same profiler without writing any code. See [Command-Line Tool](./command-line-tool.md) to install it.

```text
fluentregex profile <regex> <input> [--options <list>] [--ascii-digits]
```

```text
> fluentregex profile "hello" "Say HELLO" --options IgnoreCase
Pattern  hello
Heat     █▂▂▂▂
Input    9 characters
Result   match at 4, length 5: 9 steps, 0 backtracks
Engine   agrees with the .NET engine

    Share        Steps   Backtracks  Construct
    55.6%            5            0  h  (at 0)
    11.1%            1            0  e  (at 1)
    11.1%            1            0  l  (at 2)
    11.1%            1            0  l  (at 3)
    11.1%            1            0  o  (at 4)

Verdict  cheap: 1 steps per input character.
```

| Option | Meaning |
|--------|---------|
| `--options <list>` | Comma-separated `RegexOptions` names, such as `IgnoreCase,Multiline`. |
| `--ascii-digits` | Rewrites `\d` as `[0-9]` before profiling, so `\d+` is reported as `[0-9]+`. `fluentregex --help` lists this flag under the general options, not on the `profile` usage line, but `profile` accepts it. |

| Exit code | Meaning |
|-----------|---------|
| `0` | The profiler finished within the default 1,000,000-step budget. On a terminal the report is printed in green. |
| `1` | The budget ran out: catastrophic backtracking on this input. On a terminal the report is printed in red. |
| `2` | Usage or input error: a missing argument, invalid regex syntax or an unsupported construct. The message goes to standard error, for example `error: The backtracking profiler does not emulate RegexOptions.RightToLeft.` |

The command always uses the default budget. When the output is redirected, the report is written without color. The exit code makes it easy to fail a build script on a catastrophic pattern.

:::note

The `Exit codes` line of `fluentregex --help` mentions `1` only for `analyze` and `scan`. The `profile` command also returns `1` when the step budget runs out.

:::

## What the profiler emulates

The profiler does not time the real engine. It runs the pattern in its own step-counting backtracking interpreter, which follows .NET's matching order:

- leftmost match first: it tries every start position in turn, from 0 to the end of the input;
- alternatives from left to right;
- greedy loops try one more iteration before exiting, lazy loops exit before iterating;
- capturing and named groups, numbered and named backreferences;
- atomic groups, lookaheads and lookbehinds, positive and negative. Lookbehinds run right to left, like .NET, and captures inside them follow .NET's right-to-left order;
- every anchor except `\G`, including `^` and `$` under `Multiline`, and `.` with or without `Singleline`;
- case-insensitive matching, inline options such as `(?i:...)`, Unicode categories and class subtraction.

The interpreter keeps its choice points on an explicit list instead of the call stack, so a long input cannot overflow the stack.

### The engine cross-check

Every report that finishes within its budget is checked against the real .NET engine on the same input. When both find the same result, `AgreesWithEngine` is `true` and the `Engine` line reads `agrees with the .NET engine`. If they ever disagree, the report says so: the `Engine` line shows what the engine found, and the verdict warns that the counts may not describe the real engine. The library's own tests cross-check the interpreter on every construct it supports and on generated samples of the [common patterns](./common-patterns.md).

A pattern with `RegexOptions.NonBacktracking` (for example from `UseNonBacktracking()`, which exists only in the `net8.0` and `net10.0` builds) is profiled as a backtracking pattern and checked against the backtracking engine. The report then shows the cost the pattern would have without the non-backtracking engine:

```csharp
using System;
using Masterly.FluentRegex;

ProfileReport report = Pattern.FromRegex("a+").UseNonBacktracking().Profile("baa");

Console.WriteLine($"{report.MatchIndex} {report.MatchLength} {report.AgreesWithEngine}"); // 1 2 True
```

### Unsupported constructs

These constructs throw `NotSupportedException` before any matching starts. The message names the construct and, for conditionals, balancing groups and `\G`, its zero-based position:

| Construct | Message |
|-----------|---------|
| Conditional on a group, `(?(x)a\|b)` | `The backtracking profiler does not emulate the conditional '(?(x)a\|b)' at position 0.` |
| Conditional on an expression, `(?(\d)a\|b)` | `The backtracking profiler does not emulate the conditional '(?(\d)a\|b)' at position 0.` |
| Balancing group, `(?<o>a)(?<-o>b)` | `The backtracking profiler does not emulate the balancing group '(?<-o>' at position 7.` |
| The `\G` anchor, `\Ga` | `The backtracking profiler does not emulate the \G anchor at position 0.` |
| `RegexOptions.RightToLeft` | `The backtracking profiler does not emulate RegexOptions.RightToLeft.` |
| A backreference to a group that does not exist, such as `\5` or `\k<nope>`, in raw text added with `RegEx(...)` | `The backreference '\5' refers to an unknown group.` |

The last row is not a missing feature: .NET rejects such a pattern too. `Pattern.FromRegex` throws an `ArgumentException` for it before you can call `Profile`.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

try
{
    Pattern.FromRegex("a", RegexOptions.RightToLeft).Profile("a");
}
catch (NotSupportedException ex)
{
    Console.WriteLine(ex.Message); // The backtracking profiler does not emulate RegexOptions.RightToLeft.
}
```

To profile such a pattern, profile the parts you can. For example, profile each branch of a conditional as its own pattern.

## What the numbers mean

The interpreter runs **without** .NET's optimizations. The real engine may make `\d+` atomic when nothing after it could match a digit, jump straight to a literal prefix, or compile the pattern to IL. The profiler does none of that, so its counts show the cost a pattern *invites* from a classic backtracking engine. They are a model, not a measurement of the optimized .NET engine.

That is what you want for safety work. A pattern that is cheap only because of an engine optimization is fragile: a small edit, another option or another regex engine can remove the optimization. A pattern that is cheap in the profiler is cheap by construction.

- Use the counts to **compare** patterns and inputs, not as a time estimate.
- Steps grow with the input. Compare steps per input character, or profile the same input.
- An atomic fix removes exponential growth, but trying every start position still costs something: `(?>a+)+$` takes 152 steps on 8 `a`s and a `!`, 424 on 16 and 1,352 on 32. That is polynomial growth, which is fine for most inputs.

## Common pitfalls

- **Profiling only the happy path.** Matching input is usually cheap. Catastrophic backtracking shows up on near-misses: a long valid prefix followed by one bad character. Profile those.
- **Reading a small budget as a verdict.** `BudgetExhausted` means the budget ran out, nothing more. On a small budget even `\d+` gets a `catastrophic backtracking` verdict. See [Setting the step budget](#setting-the-step-budget).
- **Expecting `AgreesWithEngine` to be `true` after the budget runs out.** No comparison is made then, so it is `false`. Check `BudgetExhausted` first.
- **Reading the shades as absolute.** The heat row is scaled to the hottest construct in that report. Compare `Steps` across reports, not shades.
- **Looking for a capturing group in the hotspots.** Plain groups cost nothing themselves. Their contents, and the quantifier on the group, carry the work.
- **Profiling with an unlimited budget on untrusted input.** `StepBudget = long.MaxValue` lets an exponential pattern run for as long as it takes.
- **Ignoring the analyzer.** Literal patterns such as `(a+)+$` already raise the `FRX004` ReDoS warning at compile time. The profiler confirms and measures what the analyzer flags. See [Compile-Time Checks](./compile-time-checks.md).

:::note

The profiler runs in-process and stops at its step budget, but it is a diagnostic tool. To protect production code from a slow pattern, use `WithTimeout(...)` or `UseNonBacktracking()` as described in [ReDoS Safety](./redos-safety.md) and [Best Practices](./best-practices.md).

:::
