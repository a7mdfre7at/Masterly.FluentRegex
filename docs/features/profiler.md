# Backtracking profiler

`AnalyzeSafety()` tells you a pattern *might* backtrack catastrophically. `Profile(input)` shows you
how much work it actually does on a given input, and which construct does it.

```csharp
public ProfileReport Profile(string input, ProfileOptions? options = null);
```

The profiler runs the pattern in a step-counting backtracking interpreter that follows .NET's matching
order: leftmost match first, alternatives left to right, greedy loops before lazy ones, captures,
backreferences, atomic groups, lookaheads, lookbehinds (right to left, like .NET), anchors with
`Multiline`/`Singleline`, and case-insensitivity. A step is counted every time the matcher tries a
construct at a position; a backtrack every time it goes back to try another alternative.

## Finding a catastrophic pattern

```csharp
ProfileReport report = Pattern.FromRegex(@"(a+)+$").Profile("aaaaaaaaaaaaaaaaaaaaaaaa!");
Console.WriteLine(report);
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

The `Heat` row shades every character of the pattern by the work of the innermost construct that
covers it. The interpreter stops after `ProfileOptions.StepBudget` steps (1,000,000 by default), so
even an exponential pattern returns in milliseconds, with `BudgetExhausted == true`.

## Comparing a fix

```csharp
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

Making the repeated word atomic removes the ambiguity:

```csharp
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

## A healthy pattern

```csharp
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

## Using the report in code

| Member | Meaning |
|---|---|
| `IsMatch`, `MatchIndex`, `MatchLength` | What the interpreter found (`-1`/`0` when there is no match). |
| `Steps`, `Backtracks` | Total work. |
| `BudgetExhausted` | The step budget ran out: catastrophic backtracking on this input. |
| `AgreesWithEngine` | The real .NET engine found the same result. `false` when it disagrees, and when the budget ran out (no comparison is made then). |
| `Hotspots` | Constructs ordered by steps, each with `Fragment`, `Start`, `Length`, `Steps`, `Backtracks` and `Share` (0 to 1). |

```csharp
ProfileReport report = Pattern.FromRegex(@"<.+>").Profile("<b>bold</b> text");
foreach (ProfileHotspot hotspot in report.Hotspots)
{
    Console.WriteLine(hotspot);
}
```

```text
'.+' at 1: 80.0% of steps, 32 steps, 7 backtracks
'>' at 3: 17.5% of steps, 7 steps, 0 backtracks
'<' at 0: 2.5% of steps, 1 step, 0 backtracks
```

A directly quantified character, class, anchor or backreference is reported together with its
quantifier, so `a+` is one hotspot. On the same input, `<.+?>` takes 5 steps and `<[^>]+>` 6, against 40.

A unit test can guard a pattern's cost on a worst-case input:

```csharp
string worstCase = string.Concat(Enumerable.Repeat("word ", 1000)) + "!";
ProfileOptions guard = new ProfileOptions { StepBudget = 50_000 };

ProfileReport safe = Pattern.FromRegex(@"^(?>\w+\s?)*$").Profile(worstCase, guard);
// safe.BudgetExhausted == false, safe.Steps == 21007, safe.AgreesWithEngine == true

ProfileReport risky = Pattern.FromRegex(@"^(\w+\s?)*$").Profile(worstCase, guard);
// risky.BudgetExhausted == true
```

## What the numbers mean

The interpreter runs without .NET's optimizations (such as making `\d+` atomic when nothing can follow
it, or jumping to a literal prefix), so the counts show the cost a pattern *invites* from a classic
backtracking engine. They are a model, not a measurement of the optimized .NET engine. Every report
that finishes within budget is cross-checked against the real engine. If they ever disagree, the
report says so in `AgreesWithEngine` and in its `Engine` line rather than hiding it.

## Limitations

These constructs throw `NotSupportedException` naming the construct and its position:

- conditionals, `(?(name)yes|no)` and `(?(expression)yes|no)`
- balancing groups, `(?<open-close>...)`
- the `\G` anchor
- `RegexOptions.RightToLeft`

Inside lookbehinds, which .NET matches right to left, captures follow .NET's right-to-left order.
A pattern with `RegexOptions.NonBacktracking` is profiled and cross-checked as a backtracking pattern.
