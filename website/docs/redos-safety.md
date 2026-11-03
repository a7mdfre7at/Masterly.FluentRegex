---
id: redos-safety
title: "ReDoS Safety Analysis"
description: "Find catastrophic backtracking before it reaches production, prove it with a timed attack, and fix it with possessive quantifiers, atomic groups, timeouts or the non-backtracking engine."
---

A regex that backtracks catastrophically can freeze a server with an input of about 30 characters. This is called ReDoS (regular expression denial of service). `AnalyzeSafety()` reads the structure of a pattern and finds the three constructs behind almost every real ReDoS. For each one it explains the problem, suggests a fix, and builds an attack input that triggers it. With `confirm: true` it also runs that input under a short timeout, so a finding becomes proof rather than a guess.

Use this page when you need to:

- check a pattern that will see untrusted input, such as form fields, headers, uploaded files or API payloads,
- understand why a pattern is slow and how to fix it,
- block risky patterns in a build, a unit test or a CI pipeline.

## Analyzing a pattern

Call `AnalyzeSafety()` on any pattern, fluent or parsed. It returns a `SafetyReport`:

```csharp
using System;
using Masterly.FluentRegex;

Pattern email = Pattern.FromRegex(@"^([a-zA-Z0-9]+[._-]?)*@example\.com$");

SafetyReport report = email.AnalyzeSafety();

Console.WriteLine(report.Risk);    // High
Console.WriteLine(report.IsSafe);  // False
Console.WriteLine(report);
```

The last line prints a readable report:

```
Pattern: ^([a-zA-Z0-9]+[._-]?)*@example\.com$
Risk:    High

  [High] NestedQuantifier at position 1: ([a-zA-Z0-9]+[._-]?)*
    The repetition '[a-zA-Z0-9]+' is nested inside the repetition '*'. The same text can be split between iterations in exponentially many ways, so a failing match backtracks through all of them.
    Fix: Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. For untrusted input also use WithTimeout(...) or UseNonBacktracking().
    Attack input (33 chars): 00000000000000000000000000000000!
```

- The analysis is static: it reads the pattern's syntax tree and never runs the regex. It is cheap. In our runs, 1,000 analyses of this pattern took 11 to 15 ms in total.
- `report.Risk` is the highest risk among all findings. `report.IsSafe` is `true` when that risk is `Low` or `None`.
- Each finding gives the position and text of the risky construct, a plain-language explanation, a fix and an attack input.
- `AnalyzeSafety()` is available on every target framework: `netstandard2.1`, `net8.0` and `net10.0`.

The signature is:

```csharp
public SafetyReport AnalyzeSafety(bool confirm = false, TimeSpan? confirmationBudget = null)
```

| Parameter | Default | Meaning |
|-----------|---------|---------|
| `confirm` | `false` | Run each finding's attack input to prove the slowdown (see [Confirming a finding with a timed attack](#confirming-a-finding-with-a-timed-attack)). |
| `confirmationBudget` | 250 ms | The timeout for each confirmation run. Zero or a negative value throws `ArgumentOutOfRangeException`. |

## What the analyzer looks for

Every finding has a `Kind`, one of three `SafetyIssueKind` values:

| Kind | Example | Usual risk | Why it is slow |
|------|---------|------------|----------------|
| `NestedQuantifier` | `(a+)+`, `(\w+\s?)*`, `(x+x+)+` | High: exponential | The same text can be split between the inner and outer repetition in exponentially many ways. |
| `OverlappingAlternation` | `(a\|aa)+`, `(ab\|a\|b)*` | High: exponential | One alternative can match the same text as a combination of the others, so every repetition multiplies the choices. |
| `AdjacentOverlappingQuantifiers` | `\d+\d+`, `\s*.*\s*` | Medium: polynomial | Two neighbouring repetitions compete for the same characters, and a failing match tries every way to divide them. |

All three only cost time when the match **fails**. The engine then backtracks through every way of splitting the text before it gives up. That is why every attack input ends with a character the pattern cannot accept.

### Nested quantifiers

A repetition inside another repetition is the classic ReDoS. The risk depends on how far each one can repeat:

```csharp
RiskLevel unboundedInUnbounded = Pattern.FromRegex(@"^(\w+\s?)*$").AnalyzeSafety().Risk;        // High
RiskLevel competingRepeats = Pattern.FromRegex(@"^(x+x+)+y$").AnalyzeSafety().Risk;             // High
RiskLevel smallInnerBound = Pattern.FromRegex(@"^(a{1,3})+$").AnalyzeSafety().Risk;             // Medium
RiskLevel largeInnerBound = Pattern.FromRegex(@"^(a{1,10})+$").AnalyzeSafety().Risk;            // High
RiskLevel boundedOuter = Pattern.FromRegex(@"^(a+){1,10}$").AnalyzeSafety().Risk;               // Medium
RiskLevel largeBoundedOuter = Pattern.FromRegex(@"^(a+){1,1000}$").AnalyzeSafety().Risk;        // Medium
RiskLevel shortOuter = Pattern.FromRegex(@"^(a+){2,5}$").AnalyzeSafety().Risk;                  // None
RiskLevel fixedCount = Pattern.FromRegex(@"^(?:[A-Za-z0-9+/]{4})*$").AnalyzeSafety().Risk;      // None
RiskLevel separated = Pattern.FromRegex(@"^(\d+\.)+\d+$").AnalyzeSafety().Risk;                 // None
```

- The outer repetition must be large: unbounded (`*`, `+`, `{n,}`) or with a maximum of 10 or more. `(a+){2,5}` repeats at most five times, so it is not reported.
- The result is **High** only when the outer repetition is unbounded and the inner one is unbounded or has a maximum of 10 or more, such as `(a{1,10})+`. Everything else is **Medium**: an inner repetition with a smaller maximum, such as `(a{1,3})+`, or an outer repetition with any upper bound, even a large one such as `(a+){1,1000}`.
- The inner repetition must be able to match the whole iteration on its own, because everything around it can match empty. In `(\w+\s?)*` the `\s?` is optional, so `\w+` alone can fill an iteration. In `(\d+\.)+` every iteration must end with a dot, so there is only one way to split the text. It is safe.
- Fixed counts such as `{4}` match text in exactly one way, so `(?:[A-Za-z0-9+/]{4})*` (Base64 blocks) is safe.
- Two unbounded repetitions that compete inside an unbounded repetition, such as `(x+x+)+`, are also reported as a High `NestedQuantifier`. Its fix text differs: "Merge the inner repetitions into one, or wrap the body in AtomicGroup(...)."

### Overlapping alternation

Alternatives inside a repetition are dangerous when one of them can be built from the others. The analyzer searches for such a text and names it:

```csharp
SafetyReport ambiguous = Pattern.FromRegex(@"^(a|aa)+$").AnalyzeSafety();
Console.WriteLine(ambiguous.Issues[0].Description);
// prints: The text 'aa' can be matched by the alternative 'aa' or by a combination of the other alternatives
//         inside the repetition '+', so the number of ways to match grows exponentially.

RiskLevel threeWay = Pattern.FromRegex(@"^(ab|a|b)*c$").AnalyzeSafety().Risk;          // High
RiskLevel singleCharacters = Pattern.FromRegex(@"^(\w|\d)+$").AnalyzeSafety().Risk;    // Low
RiskLevel distinctWords = Pattern.FromRegex(@"^(?:cat|dog)+$").AnalyzeSafety().Risk;   // None
RiskLevel sharedPrefix = Pattern.FromRegex(@"^(ab|ac)+$").AnalyzeSafety().Risk;        // None
```

- The finding's `Fragment` is the alternation itself, without the parentheses: `a|aa` at position 2.
- `(ab|a|b)*` is High: `ab` can be matched whole, or as `a` then `b`.
- `(ab|ac)+` overlaps on the first character, but no alternative can be built from the others, so there is only one way to match. It is safe.
- Overlapping **single-character** alternatives such as `(\w|\d)+` are reported as **Low**. .NET merges them into one character set, so they are redundant rather than dangerous. A Low finding has no attack input.
- Alternations are only checked inside an **unbounded** repetition. `^(a|aa){1,20}$` reports `None`, even though a large bound such as `{1,20}` still multiplies the ways to match.

The [Optimizer](./optimizer.md) removes the redundancy for you:

```csharp
OptimizationResult merged = Pattern.FromRegex(@"^(\w|\d)+$").Optimize();
Console.WriteLine(merged.Optimized);                        // ^([\w\d])+$
Console.WriteLine(merged.Optimized.AnalyzeSafety().Risk);   // None
```

### Adjacent overlapping repetitions

Two unbounded repetitions side by side compete when their characters overlap. The cost grows polynomially with the input length, so it takes a long input, not a short one, to hurt:

```csharp
RiskLevel digits = Pattern.FromRegex(@"^\d+\d+$").AnalyzeSafety().Risk;                 // Medium
RiskLevel anything = Pattern.FromRegex(@"a.*.*b").AnalyzeSafety().Risk;                 // Medium
RiskLevel optionalBetween = Pattern.FromRegex(@"^\d+(?:a|)\d+$").AnalyzeSafety().Risk;  // Medium
RiskLevel requiredBetween = Pattern.FromRegex(@"^\d+(?:a|b)\d+$").AnalyzeSafety().Risk; // None
RiskLevel disjointSets = Pattern.FromRegex(@"^[a-z]+\d+$").AnalyzeSafety().Risk;        // None
RiskLevel groupSecond = Pattern.FromRegex(@"^\d+(?:\da)+$").AnalyzeSafety().Risk;       // Medium
RiskLevel groupFirst = Pattern.FromRegex(@"^(?:\da)+\d+$").AnalyzeSafety().Risk;        // None
```

- Both repetitions must be unbounded. The first one must repeat a single character (a literal, a class or `.`). The second one can repeat anything whose first character overlaps, so `\d+(?:\da)+` is reported but `(?:\da)+\d+` is not.
- Anything between them that can match empty, such as `(?:a|)`, a lookahead or an optional group, does not separate them. A required character, such as `(?:a|b)`, does.
- Repetitions with disjoint sets, such as `[a-z]+\d+`, never compete.

### Where the analyzer looks

- Inside lookarounds. `^(?=(a+)+b)` is reported as High at `(a+)+`.
- In every part of the pattern. One pattern can produce several findings.
- **Not** inside atomic groups and possessive quantifiers. The engine never backtracks into them, so they cannot cause ReDoS. This is how the fixes below work.

## Risk levels

`RiskLevel` orders the findings from harmless to dangerous:

| `RiskLevel` | Meaning | `IsSafe` |
|-------------|---------|----------|
| `None` | No risky constructs were found. | `true` |
| `Low` | A minor inefficiency that cannot be exploited, such as `(\w\|\d)+`. | `true` |
| `Medium` | Polynomial backtracking. A long hostile input can be slow. | `false` |
| `High` | Exponential backtracking. A short hostile input can hang the process. | `false` |

The values are ordered, so you can compare them: `report.Risk >= RiskLevel.Medium`.

## Reading the report

The report lists every finding, most severe first and then by position:

```csharp
SafetyReport report = Pattern.FromRegex(@"^(a+)+-(b+)+\d+\d+$").AnalyzeSafety();

foreach (SafetyIssue issue in report.Issues)
{
    Console.WriteLine($"{issue.Risk} {issue.Kind} at {issue.Position}: {issue.Fragment} ({issue.AttackInput?.Length} chars)");
}
// prints:
// High NestedQuantifier at 1: (a+)+ (33 chars)
// High NestedQuantifier at 7: (b+)+ (38 chars)
// Medium AdjacentOverlappingQuantifiers at 12: \d+\d+ (30017 chars)

Console.WriteLine(report.Issues[1].AttackInput);   // aaaa-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb!
Console.WriteLine(report.Issues[0]);
// prints: High NestedQuantifier at 1 '(a+)+': The repetition 'a+' is nested inside the repetition '+'.
//         The same text can be split between iterations in exponentially many ways,
//         so a failing match backtracks through all of them.
```

The comment wraps the output for reading; `ToString()` returns it as one line.

**`SafetyReport`**

| Member | What it gives you |
|--------|-------------------|
| `Pattern` | The analyzed regex text. |
| `Risk` | The highest `RiskLevel` among the issues, or `None` when there are none. |
| `IsSafe` | `true` when `Risk` is `Low` or `None`. |
| `Issues` | Every finding, ordered by risk (highest first) and then by position. Empty when nothing was found. |
| `ToString()` | The readable report shown above. For a safe pattern it ends with `No catastrophic backtracking risks found.` |

**`SafetyIssue`**

| Member | What it gives you |
|--------|-------------------|
| `Kind` | `NestedQuantifier`, `OverlappingAlternation` or `AdjacentOverlappingQuantifiers`. |
| `Risk` | The severity of this finding. |
| `Position` | The zero-based index of the construct in the regex text. |
| `Fragment` | The regex text of the construct, such as `(a+)+`. |
| `Description` | A plain-language explanation. |
| `Suggestion` | A suggested fix (read [Check that a fix keeps the meaning](#check-that-a-fix-keeps-the-meaning) before you apply it). |
| `AttackInput` | An input built to trigger the backtracking, or `null` when none could be built (always `null` for Low findings). |
| `Confirmed` | `true` when the attack input timed out, `false` when it finished in time, `null` when confirmation was not requested or there was no attack input. |
| `ConfirmationTime` | How long the confirmation run took, or `null` when it did not run. |
| `ToString()` | One line: risk, kind, position, fragment and description. |

### How attack inputs are built

- An attack input starts with a sample that matches the part of the pattern **before** the risky construct, so the engine reaches it. In `^(a+)+-(b+)+...` the second attack starts with `aaaa-`, and in `^id:(\d+)+$` the attack starts with `id:`. When no prefix can be generated, for example because the pattern starts with a backreference, the attack starts directly with the repeated text.
- It then repeats a character (or, for an alternation, the ambiguous text) that the risky construct accepts: 32 times for exponential risks, 30 times the ambiguous text for alternations, and 30,000 times for polynomial risks.
- It ends with a character the construct cannot accept, so the match fails. This is the first of `!`, `#`, `\u0000`, `\n`, `X`, `0` and space that the construct does not accept. For example `^(.+)+$` ends its attack with `\n`, because `.` accepts all the others. When the construct accepts every one of them, as `([^x]+)+` does, the attack still ends with `!`, and it may then match instead of failing. Alternation attacks always end with `!`.
- In `ToString()`, attack inputs longer than 60 characters are cut to 57 characters plus `...`, and control characters are shown as `\uXXXX`. The `AttackInput` property always holds the full string.

## Confirming a finding with a timed attack

Static analysis tells you a pattern **can** backtrack badly. Pass `confirm: true` to prove it. Each attack input is run against the real .NET regex engine with a short timeout. If the engine does not finish in time, the finding is `Confirmed`:

```csharp
Pattern words = Pattern.FromRegex(@"^(\w+\s?)*$");

SafetyReport report = words.AnalyzeSafety(confirm: true);
SafetyIssue issue = report.Issues[0];

Console.WriteLine(issue.Confirmed);                    // True
Console.WriteLine(issue.ConfirmationTime.HasValue);    // True
Console.WriteLine(report);
```

The report gains one line per finding:

```
Pattern: ^(\w+\s?)*$
Risk:    High

  [High] NestedQuantifier at position 1: (\w+\s?)*
    The repetition '\w+' is nested inside the repetition '*'. The same text can be split between iterations in exponentially many ways, so a failing match backtracks through all of them.
    Fix: Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. For untrusted input also use WithTimeout(...) or UseNonBacktracking().
    Attack input (33 chars): 00000000000000000000000000000000!
    CONFIRMED: matching did not finish within 241 ms.
```

- The time in the `CONFIRMED` line is the measured `ConfirmationTime`, so it is close to the budget but varies from run to run.
- A finding that finished in time shows `Not confirmed: the attack input finished quickly.`
- The confirmation runs with the pattern's own options (except `RegexOptions.Compiled`), so `IgnoreCase` is respected. A `NonBacktracking` pattern has no findings to confirm. The pattern's own `WithTimeout(...)` is ignored; the budget is used instead.

### Choosing the budget

The default budget is 250 ms per finding. Every confirmed finding costs the full budget, so a pattern with two confirmed findings takes about half a second. Lower the budget to make the check faster:

```csharp
// words is from the previous example
SafetyReport quick = words.AnalyzeSafety(confirm: true, confirmationBudget: TimeSpan.FromMilliseconds(50));
Console.WriteLine(quick.Issues[0].Confirmed);  // True
```

The budget is validated even when `confirm` is `false`:

```csharp
Pattern pattern = Pattern.FromRegex(@"^(a+)+$");

try
{
    pattern.AnalyzeSafety(confirmationBudget: TimeSpan.Zero);
}
catch (ArgumentOutOfRangeException exception)
{
    Console.WriteLine(exception.ParamName);   // confirmationBudget
}
```

:::tip

Confirmation runs the regex on purpose until it times out, so it costs real CPU time. Use it in tests, tools and CI, not on every request in production. The static analysis alone is cheap enough to run anywhere.

:::

### When a finding is not confirmed

`Confirmed == false` does **not** mean the pattern is safe. It means this particular attack input finished in time:

```csharp
SafetyIssue unanchored = Pattern.FromRegex(@"(a+)+").AnalyzeSafety(confirm: true).Issues[0];
Console.WriteLine($"{unanchored.Risk} {unanchored.Confirmed}");    // High False

SafetyIssue optimized = Pattern.FromRegex(@"^\d+\d+$").AnalyzeSafety(confirm: true).Issues[0];
Console.WriteLine($"{optimized.Risk} {optimized.Confirmed}");      // Medium False

SafetyIssue low = Pattern.FromRegex(@"^(\w|\d)+$").AnalyzeSafety(confirm: true).Issues[0];
Console.WriteLine($"{low.Risk} {low.AttackInput == null} {low.Confirmed.HasValue}");  // Low True False
```

- **Unanchored patterns** can match part of the attack input and stop early. `(a+)+` finds `aaa...` at the start and succeeds at once. The same construct in `^(a+)+$`, or followed by text that must match, is confirmed. Real inputs that make the match fail can still be slow.
- **The .NET engine optimizes some constructs.** It handles `^\d+\d+$` without the polynomial blow-up, so the attack finishes quickly. The finding is still a real inefficiency in other engines (see [Export](./export.md)) and in similar patterns the engine does not optimize, such as `^\s*.*\s*=$`, which is confirmed.
- **Low findings** have no attack input, so they are never run and `Confirmed` stays `null`.
- **The non-backtracking engine** finishes every attack in linear time, so nothing is confirmed (see [Use the non-backtracking engine](#use-the-non-backtracking-engine)).

## Fixing a risky pattern

There are two kinds of fix. A **structural** fix changes the pattern so it cannot backtrack badly; the analyzer then reports `None`. A **runtime** guard keeps the pattern and limits the damage. For input you do not control, use both.

| Fix | How | Analyzer result | Availability | Trade-off |
|-----|-----|-----------------|--------------|-----------|
| Possessive quantifier | `.Possessive` after the inner repetition | `None` | All targets | Can change what matches (see below). |
| Atomic group | `AtomicGroup(...)` around the repeated body | `None` | All targets | Can change what matches. |
| Rewrite | Merge repetitions, make alternatives exclusive | `None` | All targets | Needs thought; prove it with [Pattern Algebra](./pattern-algebra.md). |
| Timeout | `WithTimeout(...)` | Unchanged | All targets | Matching throws `RegexMatchTimeoutException`. |
| Non-backtracking engine | `UseNonBacktracking()` | Unchanged | `net8.0`, `net10.0` | No backreferences, lookarounds, atomic groups or conditionals. |

### Make the inner repetition possessive or atomic

A possessive quantifier and an atomic group both tell the engine never to give characters back. The engine then has nothing to try again, and the exponential search disappears:

```csharp
Pattern risky = Pattern.With.StartOfLine
    .Group(Pattern.With.Word.Repeat.OneOrMore.Whitespace.Repeat.Optional).Repeat.ZeroOrMore
    .EndOfLine;

Pattern possessive = Pattern.With.StartOfLine
    .Group(Pattern.With.Word.Repeat.OneOrMore.Possessive.Whitespace.Repeat.Optional).Repeat.ZeroOrMore
    .EndOfLine;

Pattern atomic = Pattern.With.StartOfLine
    .AtomicGroup(Pattern.With.Word.Repeat.OneOrMore.Whitespace.Repeat.Optional).Repeat.ZeroOrMore
    .EndOfLine;

Console.WriteLine(risky);                          // ^(\w+\s?)*$
Console.WriteLine(possessive);                     // ^((?>\w+)\s?)*$
Console.WriteLine(atomic);                         // ^(?>\w+\s?)*$
Console.WriteLine(risky.AnalyzeSafety().Risk);     // High
Console.WriteLine(possessive.AnalyzeSafety().Risk); // None
Console.WriteLine(atomic.AnalyzeSafety().Risk);    // None

Console.WriteLine(possessive.IsMatch("hello world"));                        // True
Console.WriteLine(possessive.IsMatch("hello  world"));                       // False
Console.WriteLine(possessive.IsMatch("00000000000000000000000000000000!"));  // False, at once
```

- .NET has no possessive syntax, so `.Possessive` renders an atomic group around the repetition: `\w+` becomes `(?>\w+)`.
- The attack input that hangs `risky` is rejected by the fixed pattern immediately.
- `.Possessive` must directly follow a quantifier, otherwise it throws `InvalidOperationException`. See [Quantifiers](./quantifiers.md) and [Groups and Backreferences](./groups-and-backreferences.md).

### Check that a fix keeps the meaning

A possessive quantifier is safe here because `\w+` is always followed by a space or the end. It is not always that simple. For adjacent repetitions that accept the same characters, a possessive first repetition breaks the pattern, which is why the suggestion for them is to merge the repetitions:

```csharp
Pattern original = Pattern.FromRegex(@"^\d+\d+$");
Pattern possessiveFirst = Pattern.With.StartOfLine
    .Digit.Repeat.OneOrMore.Possessive
    .Digit.Repeat.OneOrMore
    .EndOfLine;

Console.WriteLine(possessiveFirst);                  // ^(?>\d+)\d+$
Console.WriteLine(original.IsMatch("12345"));        // True
Console.WriteLine(possessiveFirst.IsMatch("12345")); // False
Console.WriteLine(possessiveFirst.AnalyzeSafety().Risk); // None
```

The possessive `\d+` takes every digit and never gives one back, so the second `\d+` has nothing left to match. The analyzer still reports `None`, because the broken pattern cannot backtrack. Here, merge the repetitions instead.

:::warning

After applying a possessive quantifier or an atomic group, run your tests against inputs that must match. The analyzer checks safety, not meaning.

:::

### Rewrite the pattern

Often the safest fix is a simpler pattern that matches the same strings. When the rewrite uses only regular constructs, [Pattern Algebra](./pattern-algebra.md) can prove it is equivalent:

```csharp
Pattern pair = Pattern.FromRegex(@"^\d+\d+$");
Pattern merged = Pattern.With.StartOfLine.Digit.Repeat.AtLeast(2).EndOfLine;

Console.WriteLine(merged);                         // ^\d{2,}$
Console.WriteLine(merged.AnalyzeSafety().Risk);    // None
Console.WriteLine(pair.IsEquivalentTo(merged));    // True

Pattern repeated = Pattern.FromRegex(@"^(a|aa)+$");
Pattern simpler = Pattern.With.StartOfLine.Literal("a").Repeat.OneOrMore.EndOfLine;

Console.WriteLine(simpler.AnalyzeSafety().Risk);      // None
Console.WriteLine(repeated.IsEquivalentTo(simpler));  // True
```

Useful rewrites:

- **Merge neighbouring repetitions.** `\d+\d+` is `\d{2,}`.
- **Make alternatives mutually exclusive.** `(a|aa)+` is `a+`.
- **Separate repetitions with a required character.** `(\d+\.)+\d+` is safe because each iteration must end with a dot.
- **Use a negated set instead of `.*`.** "Everything up to the next quote" is `[^"]*"`, which cannot run past the quote (see [Character Classes](./character-classes.md)).

:::note

Pattern algebra does not support atomic groups or possessive quantifiers. `IsEquivalentTo` throws `NotSupportedException` when either side contains one. Use tests to check those fixes.

:::

The [Optimizer](./optimizer.md) does not remove ReDoS risks for you, apart from merging single-character alternatives. In our checks, `Optimize()` left `^(a+)+$`, `^(\w+\s?)*$`, `^(a|aa)+$` and `^\d+\d+$` unchanged.

### Add a timeout

A timeout does not make the pattern faster. It stops a slow match and throws `RegexMatchTimeoutException`, so one hostile request cannot hold a thread for minutes:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

// input comes from your application, for example a form field
string input = "00000000000000000000000000000000!";
Pattern guarded = Pattern.FromRegex(@"^(\w+\s?)*$")
    .WithTimeout(TimeSpan.FromMilliseconds(100));

try
{
    bool valid = guarded.IsMatch(input);
    Console.WriteLine(valid);
}
catch (RegexMatchTimeoutException exception)
{
    Console.WriteLine($"Rejected: no result within {exception.MatchTimeout.TotalMilliseconds} ms");
    // prints: Rejected: no result within 100 ms
}
```

- Every matching method of the pattern uses the timeout: `IsMatch`, `Match`, `Matches`, `Replace`, `Split` and the methods built on them.
- `WithTimeout` accepts a positive value below about 24 days, or `Regex.InfiniteMatchTimeout`. See [Core Concepts](./core-concepts.md).
- Treat the exception as "invalid input" at your system boundary. Do not retry the same input.
- A timeout does not change the structure, so `AnalyzeSafety()` and the compile-time warning still report the pattern.

### Use the non-backtracking engine

On .NET 8 and later, `UseNonBacktracking()` switches the pattern to the .NET non-backtracking engine. It guarantees matching time linear in the input length, so ReDoS is impossible:

```csharp
Pattern linear = Pattern.FromRegex(@"^(\w+\s?)*$").UseNonBacktracking();

Console.WriteLine(linear.IsMatch("00000000000000000000000000000000!"));  // False

SafetyReport report = linear.AnalyzeSafety(confirm: true);
Console.WriteLine(report.Risk);                 // None
Console.WriteLine(report.Issues.Count);         // 0
```

- `UseNonBacktracking()` exists only on the `net8.0` and `net10.0` targets, not on `netstandard2.1`.
- `AnalyzeSafety()` reports no issues (`Risk` is `None`) for a pattern whose `Options` include `RegexOptions.NonBacktracking`, because that engine cannot backtrack.
- The engine does not support backreferences, lookarounds, atomic groups (so no `.Possessive`), conditionals or `RightToLeft`. The pattern builds, but throws when it first runs:

```csharp
Pattern possessive = Pattern.FromRegex(@"^((?>\w+)\s?)*$").UseNonBacktracking();

try
{
    possessive.IsMatch("hello world");
}
catch (NotSupportedException exception)
{
    Console.WriteLine(exception.Message);
    // prints: RegexOptions.NonBacktracking is not supported in conjunction with expressions containing:
    //         'atomic subexpressions (?> pattern)'.
}
```

Choose one approach per pattern: a structural fix (possessive or atomic) **or** the non-backtracking engine, plus a timeout when the input is untrusted.

## Catching risks at compile time

The `Masterly.FluentRegex` package includes a Roslyn analyzer. It evaluates every pattern whose value is known at compile time, fluent chains and `Pattern.FromRegex` with a constant string alike, and runs `AnalyzeSafety()` on it while you build:

```csharp
using System;
using Masterly.FluentRegex;

// FRX004: a nested quantifier in a fluent chain
Pattern words = Pattern.With.StartOfLine
    .Group(Pattern.With.Word.Repeat.OneOrMore.Whitespace.Repeat.Optional).Repeat.ZeroOrMore
    .EndOfLine;

// FRX005: adjacent overlapping repetitions in a constant regex
Pattern pair = Pattern.FromRegex(@"^\d+\d+$");

// FRX004: a timeout does not change the structure, so it is still reported
Pattern timed = Pattern.FromRegex(@"^(\w+\s?)*$").WithTimeout(TimeSpan.FromMilliseconds(100));

// Not reported: the fixed pattern is safe
Pattern fixedWords = Pattern.With.StartOfLine
    .Group(Pattern.With.Word.Repeat.OneOrMore.Possessive.Whitespace.Repeat.Optional).Repeat.ZeroOrMore
    .EndOfLine;

// Not reported: the regex is only known at run time
string regexFromConfig = Environment.GetEnvironmentVariable("INPUT_REGEX") ?? "^.*$";
Pattern runtime = Pattern.FromRegex(regexFromConfig);
```

`dotnet build` prints (shortened):

```
Program.cs(5,17): warning FRX004: ReDoS risk in '(\w+\s?)*': The repetition '\w+' is nested inside the repetition '*'. ... Fix: Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. For untrusted input also use WithTimeout(...) or UseNonBacktracking().
Program.cs(13,17): warning FRX004: ReDoS risk in '(\w+\s?)*': The repetition '\w+' is nested inside the repetition '*'. ...
```

| Diagnostic | Reported for | Default severity |
|------------|--------------|------------------|
| `FRX004` | A High finding. | Warning |
| `FRX005` | A Medium finding. | Info (shown in the IDE, not in `dotnet build` output) |

Low findings are not reported. Change the severity in `.editorconfig` like any analyzer diagnostic. This file fails the build on High findings and shows Medium findings as warnings:

```ini
[*.cs]
dotnet_diagnostic.FRX004.severity = error
dotnet_diagnostic.FRX005.severity = warning
```

With it, the same build fails (messages shortened):

```
Program.cs(10,16): warning FRX005: Polynomial backtracking in '\d+\d+'
Program.cs(13,17): error FRX004: ReDoS risk in '(\w+\s?)*'
Program.cs(5,17): error FRX004: ReDoS risk in '(\w+\s?)*'
```

- Patterns built from runtime values are not checked at compile time. Check them with `AnalyzeSafety()` in a test.
- A chain that calls `UseNonBacktracking()` is not checked either, because the analyzer cannot evaluate that call. That is harmless, since the engine makes ReDoS impossible, and `AnalyzeSafety()` at run time reports `None` for such a pattern (see [Use the non-backtracking engine](#use-the-non-backtracking-engine)).
- The analyzer also reports invalid regexes and other problems. See [Compile-Time Checks](./compile-time-checks.md).

## Scanning a code base in CI

The `fluentregex` command-line tool (package `Masterly.FluentRegex.Tool`) checks regexes that are not written with the fluent API. Install it as described in [Command-Line Tool](./command-line-tool.md).

### Scanning C# files

`fluentregex scan` finds regex literals in every `.cs` file under a path (skipping `bin`, `obj`, `.git` and `node_modules`) and analyzes each one. Given this file:

```csharp
using System.Text.RegularExpressions;

public static partial class InputRules
{
    private static readonly Regex Words = new Regex(@"^(\w+\s?)*$");

    public static bool IsNumberPair(string input) => Regex.IsMatch(input, @"^\d+\d+$");

    public static bool IsSku(string input) => Regex.IsMatch(input, @"^[A-Z]{3}-\d{4}$", RegexOptions.IgnoreCase);

    [GeneratedRegex(@"^(a|aa)+$")]
    private static partial Regex Repeated();
}
```

`fluentregex scan src --confirm` prints:

```
src\Validation\InputRules.cs(5,53): warning FRX-NestedQuantifier: new Regex "^(\w+\s?)*$" [CONFIRMED by timeout]
    The repetition '\w+' is nested inside the repetition '*'. The same text can be split between iterations in exponentially many ways, so a failing match backtracks through all of them.
    Fix: Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. For untrusted input also use WithTimeout(...) or UseNonBacktracking().
src\Validation\InputRules.cs(7,75): info FRX-AdjacentOverlappingQuantifiers: Regex.IsMatch "^\d+\d+$"
    The repetitions '\d+' and '\d+' can match the same characters, so a failing match tries every way of dividing them (polynomial time).
    Fix: Merge the repetitions into one, for example \d+\d+ into \d{2,}, or make their character sets disjoint.
src\Validation\InputRules.cs(11,21): warning FRX-OverlappingAlternation: [GeneratedRegex] "^(a|aa)+$" [CONFIRMED by timeout]
    The text 'aa' can be matched by the alternative 'aa' or by a combination of the other alternatives inside the repetition '+', so the number of ways to match grows exponentially.
    Fix: Make the alternatives mutually exclusive, wrap the alternation in AtomicGroup(...), or use UseNonBacktracking().

Scanned 4 regex literal(s): 2 high, 1 medium risk finding(s).
```

- It finds string literals in `new Regex(...)`, the static `Regex` methods (`IsMatch`, `Match`, `Matches`, `Replace`, `Split`, `Count`, `EnumerateMatches`, `EnumerateSplits`), `[GeneratedRegex]` and `[RegularExpression]`. `RegexOptions` written in the call are applied.
- Only a string literal written directly in the call or attribute is found. A regex held in a constant, a variable or an interpolated string is skipped, and so is a static call that does not start with `Regex.`.
- The path can be a folder or a single `.cs` file.
- Each line uses the `file(line,column): severity` format that CI systems turn into annotations. High findings are `warning`, Medium findings are `info`, and Low findings are not printed.
- An invalid regex is printed as an `error` and counted as a High finding.
- Without `--confirm` the output is the same, minus the `[CONFIRMED by timeout]` tags.

| Option | Effect |
|--------|--------|
| `--fail-on high` (default) | Exit with code 1 when there is a High finding. |
| `--fail-on medium` | Exit with code 1 when there is a High or Medium finding. |
| `--fail-on none` | Always exit with code 0; report only. |
| `--confirm` | Run each attack input with a 250 ms timeout and tag confirmed findings. |

Exit codes: `0` success, `1` findings at or above `--fail-on`, `2` a usage error (such as `--fail-on low`) or a missing path. CI systems fail a step on any non-zero exit code, so the command alone is enough:

```
fluentregex scan src --fail-on high
```

### Analyzing one regex

`fluentregex analyze` prints the same report as `SafetyReport.ToString()` for a single regex:

```
fluentregex analyze "^(\w+\s?)*$" --confirm
```

```
Pattern: ^(\w+\s?)*$
Risk:    High

  [High] NestedQuantifier at position 1: (\w+\s?)*
    The repetition '\w+' is nested inside the repetition '*'. The same text can be split between iterations in exponentially many ways, so a failing match backtracks through all of them.
    Fix: Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. For untrusted input also use WithTimeout(...) or UseNonBacktracking().
    Attack input (33 chars): 00000000000000000000000000000000!
    CONFIRMED: matching did not finish within 246 ms.
```

- Pass `--options IgnoreCase,Multiline` to analyze with options.
- `analyze` exits with code 1 for a High risk and 0 otherwise. It has no `--fail-on` option, so a Medium risk exits with 0. An invalid regex prints an `error:` line and exits with 2.
- The time in the `CONFIRMED` line varies from run to run, as in the library.

## Guarding patterns in unit tests

The compile-time analyzer only sees constant patterns. A unit test can check every pattern your application uses, including ones built from configuration:

```csharp
using System;
using Masterly.FluentRegex;
using Xunit;

public static class AppPatterns
{
    public static readonly Pattern Sku = Pattern.FromRegex(@"^[A-Z]{3}-\d{4}$");

    public static readonly Pattern Words = Pattern.With.StartOfLine
        .Group(Pattern.With.Word.Repeat.OneOrMore.Possessive.Whitespace.Repeat.Optional).Repeat.ZeroOrMore
        .EndOfLine;

    public static readonly Pattern Email = CommonPatterns.Email();
}

public class PatternSafetyTests
{
    public static TheoryData<string, Pattern> Patterns => new TheoryData<string, Pattern>
    {
        { nameof(AppPatterns.Sku), AppPatterns.Sku },
        { nameof(AppPatterns.Words), AppPatterns.Words },
        { nameof(AppPatterns.Email), AppPatterns.Email }
    };

    [Theory]
    [MemberData(nameof(Patterns))]
    public void Pattern_AnalyzeSafety_HasNoMediumOrHighRisk(string name, Pattern pattern)
    {
        SafetyReport report = pattern.AnalyzeSafety();

        Assert.True(report.IsSafe, $"{name} is not ReDoS-safe:{Environment.NewLine}{report}");
    }

    [Fact]
    public void NestedQuantifier_AnalyzeSafetyWithConfirm_IsConfirmedByTimeout()
    {
        Pattern vulnerable = Pattern.FromRegex(@"^(\w+\s?)*$");

        SafetyReport report = vulnerable.AnalyzeSafety(confirm: true);

        SafetyIssue issue = Assert.Single(report.Issues);
        Assert.Equal(SafetyIssueKind.NestedQuantifier, issue.Kind);
        Assert.True(issue.Confirmed);
    }
}
```

- Put the report in the failure message. `report.ToString()` names the construct and the fix, so the failing test explains itself.
- Use `IsSafe` (Low or None) as the bar. Use `report.Risk < RiskLevel.High` if you accept polynomial risks on short, bounded inputs.
- Every parameterless pattern in `CommonPatterns` passes this check, and so do `Email(...)` and `Url(...)` at every `PatternStrictness`.
- The deliberately vulnerable pattern in the second test is a constant, so the build also shows `FRX004` for that line. If your test project treats warnings as errors, suppress it there with `#pragma warning disable FRX004`.
- For more regex assertions, see [xUnit Assertions](./xunit.md).

## Other places the verdict appears

- **The debugger.** Expanding a `Pattern` in the debugger shows a `Safety` line with the risk and the first finding, for example `High: High NestedQuantifier at 1 '(a+)+': ...`. It never runs a confirmation.
- **AI drafting.** The `Masterly.FluentRegex.AI` package runs `AnalyzeSafety()` on every drafted pattern. A draft with **any** finding, even a Low one, fails verification. See [AI Drafting](./ai-drafting.md).
- **The backtracking profiler.** `AnalyzeSafety()` says a pattern **might** be slow. `Profile(input)` counts the actual backtracking steps for a given input and shows where they happen. See [Backtracking Profiler](./backtracking-profiler.md).

## Limits of the analysis

- **It is a pattern-based check.** It covers the three constructs behind almost every real ReDoS, but it is not a proof that a pattern is fast. A `None` result means none of those constructs was found.
- **It judges structure, not the engine.** It reports the same findings with or without `RegexOptions.NonBacktracking`.
- **Confirmation proves, it does not disprove.** `Confirmed == true` is proof. `Confirmed == false` only says one generated input finished in time.
- **Bounded repetitions get less scrutiny.** Overlapping alternatives and competing repetitions are only checked inside an unbounded repetition, so `(a|aa){1,20}` reports `None`.
- **One finding per construct.** Each risky repetition or alternation is reported once, with the first problem found in it.
- **Backreferences limit the alternation check.** Alternatives that refer to outer groups cannot be tested on their own, so they are not reported as ambiguous.
- **Medium is still slow on long input.** Polynomial backtracking grows with the square (or a higher power) of the length. Limit the input length at your system boundary as well.

## Tips and pitfalls

- **Analyze every pattern that sees untrusted input.** Rely on `FRX004` for constant patterns and add a test for patterns built at run time.
- **Prefer a structural fix.** A possessive quantifier, an atomic group or a rewrite removes the risk. A timeout only limits it.
- **Add a timeout anyway** for untrusted input. It is cheap, and it covers what the analyzer cannot see.
- **Do not trust "Not confirmed".** Check the static `Risk`, not `Confirmed`.
- **Test the meaning of a fix.** `.Possessive` in the wrong place makes a pattern match nothing, as `^(?>\d+)\d+$` shows.
- **Do not mix `.Possessive` with `UseNonBacktracking()`.** The non-backtracking engine rejects atomic groups at run time.
- **Run `--confirm` in CI, not at run time.** Each confirmed finding costs the full budget.
- **Composing safe parts can produce an unsafe whole.** `Word.Repeat.OneOrMore` is safe on its own and dangerous inside a repeated group. Analyze the final pattern (see [Composing Patterns](./composing-patterns.md)).

More guidance: [Best Practices](./best-practices.md).
