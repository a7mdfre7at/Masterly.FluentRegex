# Regex coverage for test suites

Line coverage tells you a line containing a regex ran. It does not tell you whether your tests ever took the
`Ms` branch of `^(?:Mr|Ms)\.\s\w+$`. Regex coverage does.

## One-shot measurement

```csharp
CoverageReport report = Pattern.FromRegex(@"^(?:Mr|Ms)\.\s\w+$")
    .MeasureCoverage(new[] { "Mr. Smith", "Ms. Jones", "Mx. Doe" });

Console.WriteLine(report);
// Coverage for ^(?:Mr|Ms)\.\s\w+$: 100% (2/2), 3 inputs
//   ✓ branch "Mr" (1 hit)
//   ✓ branch "Ms" (1 hit)
```

## Recording across a test run

`TrackCoverage()` returns a `PatternCoverage` recorder. Match through it instead of the pattern, keep it for
the whole run, and print the report at the end. The recorder is thread-safe (all counters use `Interlocked`),
so it can live in a static field shared by tests that run in parallel.

```csharp
PatternCoverage title = Pattern.FromRegex(@"^(?:Mr|Ms|Dr)\.?\s\w{1,3}(?:-\d+)?$").TrackCoverage();

title.IsMatch("Mr. Bob");
title.IsMatch("Mr Al");
title.IsMatch("Dr. Bo-12");
title.IsMatch("Mr. X");
title.IsMatch("nope");

Console.WriteLine(title.Report());
// Coverage for ^(?:Mr|Ms|Dr)\.?\s\w{1,3}(?:-\d+)?$: 83% (5/6), 5 inputs
//   ✓ branch "Mr" (3 hits)
//   ✗ branch "Ms" was never matched
//   ✓ branch "Dr" (1 hit)
//   ✓ optional part "\.?" (3 hits)
//   ✓ upper bound 3 of "\w{1,3}" (1 hit)
//   ✓ optional part "(?:-\d+)?" (1 hit)
```

A sketch of the same idea in xUnit: share the recorder through a static field and assert on the report
in a dedicated test or in a fixture's `Dispose`.

```csharp
public static class Patterns
{
    public static readonly PatternCoverage Title =
        Pattern.FromRegex(@"^(?:Mr|Ms)\.\s\w+$").TrackCoverage();
}

[Theory]
[InlineData("Mr. Smith")]
[InlineData("Ms. Jones")]
public void Title_Accepts(string input) => Patterns.Title.IsMatch(input).ShouldBeTrue();

// later: Patterns.Title.Report().Uncovered.ShouldBeEmpty();
```

`PatternCoverage` members:

| Member | What it does |
|--------|--------------|
| `IsMatch(input)` | Same result as `Pattern.IsMatch`, and records every match in the input. |
| `Match(input)` | Returns the match of the **original** pattern (same groups and values, no marker groups), and records. |
| `Record(input)` | Records every match without returning anything. |
| `Report()` | Returns a snapshot `CoverageReport`; later recordings do not change it. |
| `Pattern` | The tracked pattern. |

```csharp
Match m = Pattern.FromRegex(@"^(?<title>Mr|Ms)\.\s(?<name>\w+)$").TrackCoverage().Match("Ms. Jones");
// m.Groups["title"].Value == "Ms", m.Groups["name"].Value == "Jones", m.Groups.Count == 3
```

## The report

| Member | Meaning |
|--------|---------|
| `Percentage` | Covered items / all items × 100 (for example `83.33...`). A pattern with no items is 100%. |
| `Items` | Every tracked item in pattern order. |
| `Uncovered` | The items no match exercised. |
| `InputsRecorded` | All recorded inputs, whether they matched or not. |
| `ToString()` | The text report shown above (percentage rounded to a whole number). |

Each `CoverageItem` has a `Kind`, a `Description`, the `Start` and `Length` of the construct in the
pattern's regex text, a `HitCount` (the number of successful matches that exercised it) and `IsCovered`.

## What is tracked

| `CoverageItemKind` | Tracked for | Covered when |
|--------------------|-------------|--------------|
| `AlternationBranch` | every branch of `a\|b`, and the yes/no branches of a conditional `(?(name)yes\|no)` | the branch is part of a successful match |
| `OptionalPartTaken` | every quantifier with a minimum of 0: `?`, `*`, `{0,n}` | its body ran at least once in a match |
| `UpperBoundReached` | every bounded range with max > min, such as `{1,3}` or `{0,5}` (but not `?`) | its body ran the maximum number of times in one match |

Exact counts (`{4}`) and `+` have no item: there is no decision to cover.

## How it works

The pattern is parsed and rewritten with empty marker groups: one at the start of each branch, and one at the
start of each relevant quantifier body (`(?:(?<marker>)body){1,3}`). After a match, the number of captures of
each marker says which branches ran and how many times each repetition ran. Only the final successful match
is read, so a branch the engine tried and then backtracked out of does not count:

```csharp
Console.WriteLine(Pattern.FromRegex(@"^(?:ab|a)c$").MeasureCoverage(new[] { "ac" }));
// Coverage for ^(?:ab|a)c$: 50% (1/2), 1 input
//   ✗ branch "ab" was never matched
//   ✓ branch "a" (1 hit)
```

Markers use explicit group numbers far above any group in the pattern, so numbered groups, named groups and
backreferences (including numbered references to named groups, such as `(?<a>x|y)\1`) keep their meaning.
They also capture under `RegexOptions.ExplicitCapture`. The pattern's options and timeout are kept.

## Limits

- **Upper bounds are only tracked outside other quantifiers.** In `^(?:\d{1,3}\.){3}\d{1,3}$` only the last
  `\d{1,3}` gets an upper-bound item: inside a repeated group the captures of all iterations add up, so the
  count per match is not exact.

  ```csharp
  Pattern.FromRegex(@"^(?:\d{1,3}\.){3}\d{1,3}$").MeasureCoverage(new[] { "10.0.0.1", "192.168.100.200" });
  // Coverage for ^(?:\d{1,3}\.){3}\d{1,3}$: 100% (1/1), 2 inputs
  //   ✓ upper bound 3 of "\d{1,3}" (1 hit)
  ```
- **Negative lookarounds and conditional conditions are not instrumented**, because their captures never
  survive a match. Positive lookarounds are instrumented.
- **Hit counts are per match**, not per iteration: a branch taken three times inside one match counts once.
- **Every match in an input counts**, including empty matches of patterns that can match nothing.
- **Coverage is not correctness.** 100% means each decision point was exercised by some match, not that every
  combination was, nor that the inputs were rejected or accepted for the right reasons.
- **Cost.** `IsMatch` and `Record` run only the instrumented pattern; `Match` also runs the original to return an
  unaltered `Match`. Patterns that use `UseNonBacktracking()` are recorded with the backtracking engine (the
  non-backtracking engine keeps only the last capture of a repetition). It finds the same matches, but without
  the linear-time guarantee, so keep a timeout when inputs may be hostile.
