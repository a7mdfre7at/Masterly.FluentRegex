---
id: regex-coverage
title: "Regex Coverage"
description: "Find out which alternation branches, optional parts and repetition limits your test inputs never exercise."
---

Line coverage tells you that a line containing a regex ran. It cannot tell you whether any test ever took the `Ms` branch of `^(?:Mr|Ms)\.\s\w+$`. Regex coverage can. You give a pattern a set of inputs, and it reports every decision point inside the pattern that no match exercised.

Use regex coverage when you need to:

- check that a test suite really exercises every alternative of a pattern,
- find the optional part or the upper limit that no test input reaches,
- fail a build when a pattern's tests leave a branch untested,
- see how well a list of sample inputs, such as generated data or log lines, covers a pattern.

All coverage types live in the `Masterly.FluentRegex` namespace and work on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10. Only `UseNonBacktracking()`, mentioned in [Options, timeouts and engines](#options-timeouts-and-engines), needs .NET 8 or later.

| Member | Returns | Use it when |
|--------|---------|-------------|
| `pattern.MeasureCoverage(inputs)` | `CoverageReport` | You have all the inputs at hand and want one report. |
| `pattern.TrackCoverage()` | `PatternCoverage` | You want to record inputs as they arrive, for example across a whole test run. |

## Measuring coverage in one call

`MeasureCoverage` matches every input against the pattern and returns a `CoverageReport`. Print the report to see each decision point and whether a match exercised it.

```csharp
using System;
using Masterly.FluentRegex;

CoverageReport report = Pattern.FromRegex(@"^(?:Mr|Ms)\.\s\w+$")
    .MeasureCoverage(new[] { "Mr. Smith", "Mr. Jones", "Dr. Who" });

Console.Write(report);
// Coverage for ^(?:Mr|Ms)\.\s\w+$: 50% (1/2), 3 inputs
//   ✓ branch "Mr" (2 hits)
//   ✗ branch "Ms" was never matched

Console.WriteLine(report.Percentage);      // 50
Console.WriteLine(report.InputsRecorded);  // 3
Console.WriteLine(report.Uncovered.Count); // 1
```

- The pattern has two decision points: the branches `Mr` and `Ms`. Two inputs took `Mr`, none took `Ms`, so coverage is 50%.
- `Dr. Who` does not match, so it exercises nothing. It still counts as a recorded input.
- `inputs` is any `IEnumerable<string>`: an array, a list, the rows of a test table or the lines of a file.
- Every match inside an input counts, not just the first. See [Every match counts](#every-match-counts).

Coverage works the same for patterns you build fluently. The report shows the regex the pattern renders:

```csharp
Pattern title = Pattern.With
    .StartOfLine
    .OneOf("Mr", "Ms", "Dr")
    .Literal(".").Repeat.Optional
    .Whitespace
    .Letter.Repeat.Times(2, 20)
    .EndOfLine;

Console.Write(title.MeasureCoverage(new[] { "Mr. Smith", "Dr Jones" }));
// Coverage for ^(?:Mr|Ms|Dr)\.?\s[a-zA-Z]{2,20}$: 60% (3/5), 2 inputs
//   ✓ branch "Mr" (1 hit)
//   ✗ branch "Ms" was never matched
//   ✓ branch "Dr" (1 hit)
//   ✓ optional part "\.?" (1 hit)
//   ✗ upper bound 20 of "[a-zA-Z]{2,20}" was never reached
```

- `OneOf` gives three branches. `.Repeat.Optional` gives an optional part, and `.Repeat.Times(2, 20)` gives an upper bound.
- The report tells you exactly what to add: an input that starts with `Ms`, and a name with 20 letters.

### Method reference

`CoverageReport MeasureCoverage(IEnumerable<string> inputs)`

| | |
|---|---|
| **Returns** | A `CoverageReport` for all the inputs. |
| **`ArgumentNullException`** | `inputs` is `null` (the message is the default one), or one of the inputs is `null` (the message starts with "The inputs cannot contain null."). In both cases `ParamName` is `"inputs"`. |
| **`InvalidOperationException`** | The pattern is not valid regex syntax. See [Errors](#errors). |

An empty list of inputs is valid. The report then has 0 inputs and every item is uncovered.

## Reading the report

A `CoverageReport` is a read-only snapshot. Read its members to make decisions in code, or print it with `ToString()`.

| Member | Type | Meaning |
|--------|------|---------|
| `Percentage` | `double` | Covered items divided by all items, times 100, from 0 to 100. Not rounded, so 1 of 8 is `12.5`. A pattern with no items is 100. |
| `Items` | `IReadOnlyList<CoverageItem>` | Every tracked item, in the order it appears in the pattern. |
| `Uncovered` | `IReadOnlyList<CoverageItem>` | The items that no match exercised, in pattern order. |
| `InputsRecorded` | `int` | How many inputs were recorded, whether they matched or not. |
| `ToString()` | `string` | A summary line and one line per item, as shown above. The percentage is rounded to a whole number, halves away from zero, so 12.5 prints as `13%`. Every line ends with a newline, so use `Console.Write`, not `Console.WriteLine`. |

Each entry is a `CoverageItem`:

| Member | Type | Meaning |
|--------|------|---------|
| `Kind` | `CoverageItemKind` | `AlternationBranch`, `OptionalPartTaken` or `UpperBoundReached`. See [What is tracked](#what-is-tracked). |
| `Description` | `string` | A short label, such as `branch "Mr"` or `upper bound 3 of "d{1,3}"`. Fragments longer than 40 characters are cut and end with `...`. |
| `Start` | `int` | Where the construct starts in the pattern's regex text. |
| `Length` | `int` | The length of the construct in the regex text. An empty branch has length 0. |
| `HitCount` | `int` | How many successful matches exercised the item. |
| `IsCovered` | `bool` | `true` when `HitCount` is greater than 0. |
| `ToString()` | `string` | The item's line of the report, such as `✓ branch "x" (1 hit)`. |

```csharp
CoverageReport report = Pattern.FromRegex(@"^(?:x|y)c?d{1,3}$").MeasureCoverage(new[] { "xd" });

foreach (CoverageItem item in report.Items)
{
    Console.WriteLine($"{item.Kind} at {item.Start}+{item.Length}: {item}");
}
// AlternationBranch at 4+1: ✓ branch "x" (1 hit)
// AlternationBranch at 6+1: ✗ branch "y" was never matched
// OptionalPartTaken at 8+2: ✗ optional part "c?" was never taken
// UpperBoundReached at 10+6: ✗ upper bound 3 of "d{1,3}" was never reached
```

- An uncovered line ends with "was never matched" for a branch, "was never taken" for an optional part and "was never reached" for an upper bound.
- `Start` and `Length` of a quantifier item span the whole quantified construct, including the quantifier: `c?` and `d{1,3}`.
- `CoverageReport` and `CoverageItem` are sealed and their constructors are internal. You only get them from `MeasureCoverage` or `Report()`.

### Pointing at the uncovered part

`Start` and `Length` index into the regex text, the same string that `pattern.ToString()` returns. Use them to underline what is missing, for example in a test failure message:

```csharp
const string regex = @"^(?:Mr|Ms)\.\s\w+$";
CoverageReport report = Pattern.FromRegex(regex).MeasureCoverage(new[] { "Mr. Smith" });

foreach (CoverageItem item in report.Uncovered)
{
    Console.WriteLine(regex);
    Console.WriteLine($"{new string(' ', item.Start)}{new string('^', Math.Max(item.Length, 1))} {item.Description}");
}
// ^(?:Mr|Ms)\.\s\w+$
//        ^^ branch "Ms"
```

## Recording coverage over time

`TrackCoverage()` returns a `PatternCoverage` recorder. Match through the recorder instead of the pattern. It gives the same answers and records every match it sees. Call `Report()` whenever you want the result.

```csharp
PatternCoverage title = Pattern.FromRegex(@"^(?:Mr|Ms|Dr)\.?\s\w{1,3}(?:-\d+)?$").TrackCoverage();

title.IsMatch("Mr. Bob");
title.IsMatch("Mr Al");
title.IsMatch("Dr. Bo-12");
title.IsMatch("Mr. X");
title.IsMatch("nope");

Console.Write(title.Report());
// Coverage for ^(?:Mr|Ms|Dr)\.?\s\w{1,3}(?:-\d+)?$: 83% (5/6), 5 inputs
//   ✓ branch "Mr" (3 hits)
//   ✗ branch "Ms" was never matched
//   ✓ branch "Dr" (1 hit)
//   ✓ optional part "\.?" (3 hits)
//   ✓ upper bound 3 of "\w{1,3}" (1 hit)
//   ✓ optional part "(?:-\d+)?" (1 hit)
```

| Member | What it does |
|--------|--------------|
| `IsMatch(input)` | Returns the same `bool` as `Pattern.IsMatch`, and records every match in the input. |
| `Match(input)` | Returns the first match of the **original** pattern, with the same groups and values as `Pattern.Match`. Records every match in the input. |
| `Record(input)` | Records every match in the input and returns nothing. |
| `Report()` | Returns a `CoverageReport` of everything recorded so far. |
| `Pattern` | The tracked `Pattern`. |

`IsMatch`, `Match` and `Record` throw `ArgumentNullException` when `input` is `null`. Every call counts as one recorded input, whether it matched or not.

### Getting the original match back

The recorder matches with an instrumented copy of the pattern (see [How it works](#how-it-works)). `Match` then runs the original pattern too, so the `Match` you get back has no extra groups:

```csharp
using System.Text.RegularExpressions;

PatternCoverage coverage = Pattern.FromRegex(@"^(?<title>Mr|Ms)\.\s(?<name>\w+)$").TrackCoverage();

Match match = coverage.Match("Ms. Jones");

Console.WriteLine(match.Groups["title"].Value);                    // Ms
Console.WriteLine(match.Groups["name"].Value);                     // Jones
Console.WriteLine(match.Groups.Count);                             // 3
Console.WriteLine(coverage.Report().Uncovered[0].Description);     // branch "Mr"
```

- `Groups.Count` is 3 (the whole match, `title` and `name`): none of the marker groups leak into the result.
- When nothing matches, `Match` returns an unsuccessful `Match` (`Success` is `false`), exactly like `Pattern.Match`.

### Reports are snapshots

`Report()` copies the current counts. Later recordings do not change a report you already took:

```csharp
PatternCoverage coverage = Pattern.FromRegex(@"^(?:Mr|Ms)\.\s\w+$").TrackCoverage();
CoverageReport before = coverage.Report();

coverage.Record("Mr. Smith");
CoverageReport after = coverage.Report();

Console.WriteLine(before.InputsRecorded);     // 0
Console.WriteLine(after.InputsRecorded);      // 1
Console.WriteLine(after.Items[0].HitCount);   // 1
```

There is no reset. To start again, call `TrackCoverage()` for a new recorder.

### Sharing a recorder between threads

The recorder is thread-safe: it updates its counters with `Interlocked`. One recorder can live in a static field and be used by tests that run in parallel.

```csharp
using System.Threading.Tasks;

PatternCoverage title = Pattern.FromRegex(@"^(?:Mr|Ms)\.\s\w+$").TrackCoverage();

Parallel.For(0, 1000, i => title.Record(i % 2 == 0 ? "Mr. Smith" : "Ms. Jones"));

Console.Write(title.Report());
// Coverage for ^(?:Mr|Ms)\.\s\w+$: 100% (2/2), 1000 inputs
//   ✓ branch "Mr" (500 hits)
//   ✓ branch "Ms" (500 hits)
```

:::note

A report taken while other threads are still recording includes some of their inputs, but not necessarily all of them. Take the final report after the work is done.

:::

## What is tracked

Coverage tracks three kinds of decision points:

| `CoverageItemKind` | Tracked for | Covered when |
|--------------------|-------------|--------------|
| `AlternationBranch` | Every branch of an alternation `a\|b`, and the yes and no branches of a conditional `(?(name)yes\|no)` | The branch is part of a successful match. |
| `OptionalPartTaken` | Every quantifier with a minimum of 0: `?`, `*`, `{0,n}`, and their lazy forms such as `*?` | The quantified part ran at least once in a match. |
| `UpperBoundReached` | Every bounded range whose maximum is above its minimum, such as `{1,3}` or `{0,5}`, but not `?` or `{0,1}` | The quantified part ran its maximum number of times in one match. |

A quantifier with a minimum of 0 and a bounded range gets both items:

```csharp
Console.Write(Pattern.FromRegex("^ab{0,2}$").MeasureCoverage(new[] { "ab" }));
// Coverage for ^ab{0,2}$: 50% (1/2), 1 input
//   ✓ optional part "b{0,2}" (1 hit)
//   ✗ upper bound 2 of "b{0,2}" was never reached

Console.Write(Pattern.FromRegex("^colou?r$").MeasureCoverage(new[] { "color" }));
// Coverage for ^colou?r$: 0% (0/1), 1 input
//   ✗ optional part "u?" was never taken

Console.Write(Pattern.FromRegex(@"^\d{1,3}$").MeasureCoverage(new[] { "12" }));
// Coverage for ^\d{1,3}$: 0% (0/1), 1 input
//   ✗ upper bound 3 of "\d{1,3}" was never reached
```

Conditionals get one item per branch. The condition itself is not tracked. A missing or empty branch is labelled `empty`:

```csharp
Console.Write(Pattern.FromRegex(@"^(?<q>"")?\w+(?(q)""|)$").MeasureCoverage(new[] { "\"abc\"" }));
// Coverage for ^(?<q>")?\w+(?(q)"|)$: 67% (2/3), 1 input
//   ✓ optional part "(?<q>")?" (1 hit)
//   ✓ condition-true branch """ (1 hit)
//   ✗ empty condition-false branch was never matched
```

Branches inside a positive lookahead or lookbehind are tracked. Branches inside a negative lookaround are not, because a negative lookaround never keeps what it matched:

```csharp
Console.Write(Pattern.FromRegex(@"^(?=a|b)\w$").MeasureCoverage(new[] { "b" }));
// Coverage for ^(?=a|b)\w$: 50% (1/2), 1 input
//   ✗ branch "a" was never matched
//   ✓ branch "b" (1 hit)

Console.Write(Pattern.FromRegex(@"^(?!foo|bar)\w+$").MeasureCoverage(new[] { "baz" }));
// Coverage for ^(?!foo|bar)\w+$: 100% (0/0), 1 input
//   (no alternation branches, optional parts or bounded repetitions to cover)
```

### Patterns with nothing to cover

Some constructs make no decision, so they get no item:

- an exact count such as `{4}`,
- `+`, and `{n,}` with `n` of 1 or more, which have no upper bound and always run at least once (`{0,}` behaves like `*` and gets an optional-part item),
- `{0}`, which never runs its body,
- anything inside a negative lookaround or a conditional's condition.

A pattern with no items reports 100% and says so:

```csharp
Console.Write(Pattern.FromRegex(@"^\d{4}$").MeasureCoverage(new[] { "2024" }));
// Coverage for ^\d{4}$: 100% (0/0), 1 input
//   (no alternation branches, optional parts or bounded repetitions to cover)
```

## How matches are counted

### Only the final match counts

Coverage reads the final successful match. A branch that the engine tried and then backtracked out of does not count:

```csharp
Console.Write(Pattern.FromRegex(@"^(?:ab|a)c$").MeasureCoverage(new[] { "ac" }));
// Coverage for ^(?:ab|a)c$: 50% (1/2), 1 input
//   ✗ branch "ab" was never matched
//   ✓ branch "a" (1 hit)
```

The engine tries `ab` first, fails on `c`, and then matches with `a`. Only `a` is part of the match.

### Every match counts

Inputs are scanned for every match, not just the first, and a hit is counted per match:

```csharp
Console.Write(Pattern.FromRegex("cat|dog").MeasureCoverage(new[] { "cat and dog", "cat cat cat" }));
// Coverage for cat|dog: 100% (2/2), 2 inputs
//   ✓ branch "cat" (4 hits)
//   ✓ branch "dog" (1 hit)

Console.Write(Pattern.FromRegex("^(?:a|b)+$").MeasureCoverage(new[] { "aaa", "ab" }));
// Coverage for ^(?:a|b)+$: 100% (2/2), 2 inputs
//   ✓ branch "a" (2 hits)
//   ✓ branch "b" (1 hit)
```

- In the first report, `cat` appears four times across two inputs, so it has 4 hits.
- In the second, `aaa` takes branch `a` three times but is one match, so it adds one hit. `HitCount` counts matches, not iterations.
- Empty matches count too. `x*` on `"ab"` finds three empty matches, but none of them takes the optional part, so `x*` stays uncovered.

### Upper bounds inside other quantifiers

An upper bound is tracked only when its quantifier is not inside another quantifier. Inside a repeated group, the iterations of all outer repetitions add up, so the count per match is not exact. The optional and branch items inside are still tracked.

```csharp
Console.Write(Pattern.FromRegex(@"^(?:\d{1,3}\.){3}\d{1,3}$")
    .MeasureCoverage(new[] { "10.0.0.1", "192.168.100.200" }));
// Coverage for ^(?:\d{1,3}\.){3}\d{1,3}$: 100% (1/1), 2 inputs
//   ✓ upper bound 3 of "\d{1,3}" (1 hit)
```

Only the last `\d{1,3}` has an item. The first one sits inside `{3}`.

Any enclosing quantifier counts, even an optional `?`:

```csharp
Console.Write(Pattern.FromRegex(@"^(?:a{1,2}|b)?$").MeasureCoverage(new[] { "aa" }));
// Coverage for ^(?:a{1,2}|b)?$: 67% (2/3), 1 input
//   ✓ optional part "(?:a{1,2}|b)?" (1 hit)
//   ✓ branch "a{1,2}" (1 hit)
//   ✗ branch "b" was never matched
```

`a{1,2}` gets no upper-bound item because it sits inside the `?`. Its branch is still tracked.

## Using coverage in tests

### Assert on a list of inputs

Keep the test inputs in one list. Feed them to the theory that checks the pattern, and to a second test that checks coverage. Because the coverage test calls `MeasureCoverage` itself, it does not depend on the order the tests run in.

```csharp
using System.Collections.Generic;
using System.Linq;
using Masterly.FluentRegex;
using Xunit;

public static class Patterns
{
    public static readonly Pattern Title = Pattern.FromRegex(@"^(?:Mr|Ms|Dr)\.?\s\w+$");
}

public class TitleTests
{
    private static readonly string[] ValidTitles = { "Mr. Smith", "Ms Jones", "Dr Watson" };

    public static IEnumerable<object[]> ValidTitleRows => ValidTitles.Select(title => new object[] { title });

    [Theory]
    [MemberData(nameof(ValidTitleRows))]
    public void Title_ValidInput_Matches(string input)
    {
        Assert.True(Patterns.Title.IsMatch(input));
    }

    [Fact]
    public void Title_ValidTitles_CoverEveryDecisionPoint()
    {
        CoverageReport report = Patterns.Title.MeasureCoverage(ValidTitles);

        Assert.True(report.Uncovered.Count == 0, report.ToString());
    }
}
```

- Passing `report.ToString()` as the assertion message puts the whole report in the test output.
- Without `"Dr Watson"`, the coverage test fails with this message:

```text
Coverage for ^(?:Mr|Ms|Dr)\.?\s\w+$: 75% (3/4), 2 inputs
  ✓ branch "Mr" (1 hit)
  ✓ branch "Ms" (1 hit)
  ✗ branch "Dr" was never matched
  ✓ optional part "\.?" (1 hit)
```

### Record through a shared fixture

When a pattern is exercised by many tests, record through one `PatternCoverage` and check the report after the last test. In xUnit, a class fixture is created before the first test of the class and disposed after the last one:

```csharp
using System;
using Masterly.FluentRegex;
using Xunit;

public sealed class OrderIdCoverageFixture : IDisposable
{
    public PatternCoverage OrderId { get; } =
        Pattern.FromRegex(@"^(?:ORD|INV)-\d{4,6}$").TrackCoverage();

    public void Dispose()
    {
        CoverageReport report = OrderId.Report();
        if (report.Uncovered.Count > 0)
        {
            throw new InvalidOperationException(report.ToString());
        }
    }
}

public class OrderIdTests : IClassFixture<OrderIdCoverageFixture>
{
    private readonly PatternCoverage _orderId;

    public OrderIdTests(OrderIdCoverageFixture fixture)
    {
        _orderId = fixture.OrderId;
    }

    [Theory]
    [InlineData("ORD-1234")]
    [InlineData("ORD-123456")]
    [InlineData("INV-98765")]
    public void OrderId_ValidInput_Matches(string input)
    {
        Assert.True(_orderId.IsMatch(input));
    }

    [Fact]
    public void OrderId_MissingPrefix_DoesNotMatch()
    {
        Assert.False(_orderId.IsMatch("1234"));
    }
}
```

- `"1234"` does not match, so it exercises nothing. It still counts as a recorded input.
- When an item stays uncovered, xUnit 2 reports a `[Test Class Cleanup Failure (OrderIdTests)]` holding the report text, and marks every test of the class as failed. Without `"INV-98765"` the message reads:

```text
[Test Class Cleanup Failure (OrderIdTests)]: System.InvalidOperationException : Coverage for ^(?:ORD|INV)-\d{4,6}$: 67% (2/3), 3 inputs
  ✓ branch "ORD" (2 hits)
  ✗ branch "INV" was never matched
  ✓ upper bound 6 of "\d{4,6}" (1 hit)
```

:::tip

Prefer the first approach when you can. A test that measures its own list of inputs is independent of other tests. A shared recorder only sees the tests that ran, so a filtered run (for example `dotnet test --filter`) can report uncovered items that the full suite does cover.

:::

### Set a minimum instead of 100%

Some items, such as a 20-letter name, are not worth a dedicated input. Check `Percentage` against a minimum, and list what is missing:

```csharp
using System.Linq;

Pattern orderId = Pattern.FromRegex(@"^(?:ORD|INV)-\d{4,6}(?:-[A-Z])?$");
string[] testInputs = { "ORD-1234", "ORD-98765" };

CoverageReport report = orderId.MeasureCoverage(testInputs);
string missing = string.Join(", ", report.Uncovered.Select(item => item.Description));

Console.WriteLine(report.Percentage >= 75); // False
Console.WriteLine(missing);
// branch "INV", upper bound 6 of "\d{4,6}", optional part "(?:-[A-Z])?"
```

### Check generated test data

[Test Data Generation](./test-data-generation.md) produces matching strings for a pattern. Measure them to see whether a generated set exercises every decision point before you rely on it:

```csharp
Pattern postcode = Pattern.FromRegex(@"^[A-Z]{1,2}\d{1,2} ?\d[A-Z]{2}$");
string[] samples = postcode.GenerateMatching(count: 50, seed: 7);

Console.Write(postcode.MeasureCoverage(samples));
// Coverage for ^[A-Z]{1,2}\d{1,2} ?\d[A-Z]{2}$: 100% (3/3), 50 inputs
//   ✓ upper bound 2 of "[A-Z]{1,2}" (29 hits)
//   ✓ upper bound 2 of "\d{1,2}" (25 hits)
//   ✓ optional part " ?" (27 hits)
```

## Options, timeouts and engines

The recorder keeps the pattern's `RegexOptions` and timeout, so it accepts and rejects exactly the same inputs:

```csharp
using System.Text.RegularExpressions;

PatternCoverage answer = Pattern.FromRegex("^(?:yes|no)$", RegexOptions.IgnoreCase).TrackCoverage();

Console.WriteLine(answer.IsMatch("YES"));                    // True
Console.WriteLine(answer.Report().Uncovered[0].Description); // branch "no"
```

- `RegexOptions.ExplicitCapture` and `RegexOptions.IgnorePatternWhitespace` are supported. Branches are still tracked under `ExplicitCapture`.
- Numbered groups, named groups and backreferences keep their meaning. This includes numbered references to named groups, such as `(?<a>x|y)\1`, and explicitly numbered groups such as `(?<5>a|z)`.
- A timeout set with `WithTimeout` applies to the recorder. A slow input throws `RegexMatchTimeoutException`, as it would with the pattern itself. That input is still counted in `InputsRecorded`, because the count goes up before matching starts.
- A pattern built with `WithAsciiDigits()` is measured as rewritten, so the report shows `[0-9]` where you wrote `\d`.

:::warning

Patterns that use `UseNonBacktracking()` (.NET 8 and later) are recorded with the backtracking engine, because the non-backtracking engine keeps only the last capture of a repetition. The recorder finds the same matches, but without the linear-time guarantee. Keep a timeout on the pattern when coverage inputs may be hostile, and see [ReDoS Safety](./redos-safety.md).

:::

```csharp
PatternCoverage digits = Pattern.FromRegex(@"^\d{1,3}$").UseNonBacktracking().TrackCoverage();
digits.Record("123");

Console.Write(digits.Report());
// Coverage for ^\d{1,3}$: 100% (1/1), 1 input
//   ✓ upper bound 3 of "\d{1,3}" (1 hit)
```

### Cost

- `TrackCoverage()` parses the pattern and compiles one extra `Regex`. `MeasureCoverage` does this on every call, so reuse a recorder if you measure the same pattern many times.
- `IsMatch` and `Record` run only the instrumented pattern. They walk every match in the input, so they can do more work than `Pattern.IsMatch`, which stops at the first match.
- `Match` runs the instrumented pattern for recording, and then the original pattern to return an unaltered match.

Coverage is a testing tool. Do not leave a recorder in a production hot path.

## How it works

The pattern is parsed and rewritten with empty marker groups. One marker goes at the start of each branch. Each tracked quantifier gets a marker at the start of its body, wrapped as `(?:(?<marker>)body){1,3}`. After a match, the number of captures of each marker shows which branches ran and how many times each repetition ran:

- a branch or optional part is exercised when its marker captured at least once,
- an upper bound is reached when its marker captured as many times as the maximum.

Markers use explicit group numbers from 1,000,000 upwards, or above the highest group number in the pattern if that is larger. That is why the original groups and backreferences are not renumbered.

## Errors

| Exception | When |
|-----------|------|
| `ArgumentNullException` | `MeasureCoverage(null)`, an input in the list is `null`, or `IsMatch`, `Match` or `Record` gets `null`. |
| `InvalidOperationException` | `TrackCoverage()` or `MeasureCoverage` runs on a pattern that is not valid regex syntax. The message starts with "The pattern is not valid regex syntax:". In the unlikely case that the rewritten pattern does not compile, the message starts with "The pattern ... could not be instrumented for coverage:". |
| `RegexMatchTimeoutException` | Matching an input takes longer than the pattern's timeout. |

`Pattern.FromRegex` already checks the syntax and throws `ArgumentException` for an invalid regex, so you only meet the `InvalidOperationException` with a pattern that skipped that check, such as one built with `Pattern.With.RegEx(...)`. When the regex is a string literal in your code, the analyzer described in [Compile-Time Checks](./compile-time-checks.md) already reports it as a build error. A regex that arrives at run time is the case to handle:

```csharp
// regexText comes from your application, for example "(" (a group that never closes)
try
{
    Pattern.With.RegEx(regexText).TrackCoverage();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message);
    // The pattern is not valid regex syntax: Not enough )'s (at position 0)
}
```

## From the command line

The `fluentregex` tool measures coverage of the lines of a file, or of standard input when you pass `-`. See [Command-Line Tool](./command-line-tool.md).

```bash
fluentregex coverage '^(?:Mr|Ms)\.\s\w+$' titles.txt
```

```text
Coverage for ^(?:Mr|Ms)\.\s\w+$: 50% (1/2), 3 inputs
  ✓ branch "Mr" (2 hits)
  ✗ branch "Ms" was never matched
```

- Each line of the file is one input. Both `\n` and `\r\n` line endings work. Trailing line breaks at the end of the file are ignored, but an empty line in the middle is an input. An empty file counts as one empty input.
- `--options IgnoreCase,Multiline` sets `RegexOptions`, and `--ascii-digits` makes `\d` match only `0-9` (the report then shows `[0-9]` instead of `\d`).
- The exit code is 0 when every item is covered, 1 when at least one item is uncovered and 2 for a usage or input error, such as an invalid regex or a missing file. That makes the command usable as a CI check.

## Limits

- **Coverage is not correctness.** 100% means each decision point was exercised by some match. It does not mean every combination was tested, or that inputs were accepted or rejected for the right reasons. Pair coverage with tests for inputs that must not match.
- **Only successful matches count.** An input that does not match covers nothing, so coverage cannot tell you whether your rejection tests are good.
- **Hit counts are per match**, not per iteration.
- **Upper bounds are not tracked inside other quantifiers**, and nothing is tracked inside negative lookarounds or conditional conditions.

## Related pages

- [Test Data Generation](./test-data-generation.md) creates matching and near-miss inputs.
- [Property Testing](./property-testing.md) checks a pattern against many generated inputs.
- [xUnit](./xunit.md) adds pattern assertions to your tests.
- [Match Tracing](./match-tracing.md) shows which part of a pattern matched what, for a single input.
- [Backtracking Profiler](./backtracking-profiler.md) counts the work the engine does for an input.
