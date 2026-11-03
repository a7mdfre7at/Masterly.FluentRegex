---
id: test-data-generation
title: "Test Data Generation"
description: "Generate strings a pattern matches and near misses it rejects, reproducibly, for tests and fuzzing."
---

Every pattern can produce its own test data. `GenerateMatching` returns strings the pattern matches, and `GenerateNonMatching` returns strings it rejects. Most rejected strings are near misses: a valid sample with one small edit, which is exactly the kind of input that exposes off-by-one mistakes. Use them to test a hand-written validator, to fuzz a parser, or to seed a property test without writing the examples yourself.

| Method | Returns | Use it for |
|--------|---------|------------|
| `GenerateMatching(count, seed)` | Distinct strings that the pattern matches. | Positive test cases: input your code must accept. |
| `GenerateNonMatching(count, seed)` | Distinct strings that the pattern does not match. | Negative test cases: input your code must reject. |

Both methods live on `Pattern` in the `Masterly.FluentRegex` namespace and work on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10.

:::tip[What needs which package]

- [`GenerateMatching` and `GenerateNonMatching`](#generating-matching-strings) are part of the core package.
- The [`[MatchingData]` and `[NonMatchingData]` theory attributes](#xunit-theory-data) need `Masterly.FluentRegex.Xunit`, which targets .NET 8 and .NET 10 and works with xUnit v3.
- The [`generate` command](#from-the-command-line) is part of the command-line tool.

:::

## Generating matching strings

Call `GenerateMatching` with the number of samples you want. Pass a seed when you want the same samples on every run.

```csharp
using System;
using System.Linq;
using Masterly.FluentRegex;

Pattern ipv4 = CommonPatterns.IPv4();

string[] valid = ipv4.GenerateMatching(5, seed: 42);
Console.WriteLine(string.Join("  ", valid));
// prints: 127.253.251.243  254.11.253.8  250.70.207.202  250.235.240.250  6.225.247.255

Console.WriteLine(valid.All(ipv4.IsMatch)); // True
```

- Every returned string has been checked with the real .NET regex engine, using the pattern's own options (minus `RegexOptions.Compiled`). A string that the engine rejects is never returned.
- The samples are distinct. You never get the same string twice in one call.
- `count` defaults to 10. The method returns *up to* `count` strings. See [How many samples you get](#how-many-samples-you-get).
- The return type is `string[]`. An empty array is a valid answer, never `null`.

## Generating near misses

`GenerateNonMatching` returns strings that the pattern rejects. Most of them are one small edit away from a valid sample.

```csharp
string[] invalid = ipv4.GenerateNonMatching(5, seed: 42);
Console.WriteLine(string.Join("  ", invalid));
// prints: 25411.253.8  255.05Z5.229.241  x255.055.229.241  #6.225.247.255  250

Console.WriteLine(invalid.Any(ipv4.IsMatch)); // False
```

Compare these with the valid samples above. `25411.253.8` is `254.11.253.8` with a dot removed. `#6.225.247.255` is `6.225.247.255` with a character in front. `250` is a valid sample cut short. A validator that only checks "digits and dots" accepts several of them, so these strings catch real bugs.

- Every returned string has been checked with the real .NET engine and does not match.
- The strings are distinct, and none of them is one of the valid samples the method started from.
- The same `count` and `seed` rules apply as for `GenerateMatching`.

## Parameters and return values

Both methods have the same signature:

```csharp
public string[] GenerateMatching(int count = 10, int? seed = null)
public string[] GenerateNonMatching(int count = 10, int? seed = null)
```

| Parameter | Default | Meaning |
|-----------|---------|---------|
| `count` | `10` | The most samples to return. `0` returns an empty array. A negative value throws `ArgumentOutOfRangeException` with `ParamName` `"count"`. |
| `seed` | `null` | Makes the output reproducible. `null` seeds from `Environment.TickCount`, so you usually get different samples on each call (see the warning in [Reproducible output with seeds](#reproducible-output-with-seeds)). |

| Exception | When |
|-----------|------|
| `ArgumentOutOfRangeException` | `count` is negative. |
| `InvalidOperationException` | The pattern is not valid regex syntax, for example a raw `RegEx(...)` fragment with an unclosed group. The message starts with "The pattern is not valid regex syntax:". |
| `InvalidOperationException` | The generator reaches a character class whose only members are lone surrogates, such as `\p{Cs}`. The message is "The character class has no character that can be generated (for example, it contains only surrogate code units).". See [Errors](#errors). |

## Reproducible output with seeds

Pass a `seed` to get the same samples every time. This keeps test failures reproducible: a sample that failed yesterday is still in the list today.

```csharp
using System;
using System.Linq;
using Masterly.FluentRegex;

Pattern sku = Pattern.FromRegex(@"^[A-Z]{3}-\d{4}$").WithDigits(DigitMode.Ascii);

string[] first = sku.GenerateMatching(3, seed: 7);
string[] second = sku.GenerateMatching(3, seed: 7);

Console.WriteLine(string.Join("  ", first)); // prints: WBR-9891  KIA-5400  SHE-0196
Console.WriteLine(first.SequenceEqual(second)); // True
```

**Changing the count**

- For `GenerateMatching`, a larger count with the same seed starts with the same samples. `sku.GenerateMatching(5, seed: 7)` returns `WBR-9891  KIA-5400  SHE-0196  BYO-1898  EIA-1128`.
- For `GenerateNonMatching`, a count below 20 gives a different list. The method first builds a pool of valid samples whose size depends on `count` (see [Non-matching samples](#non-matching-samples)), so the near misses change too. From a count of 20 up, the pool size stays at 40, and a larger count usually starts with the same strings: `sku.GenerateNonMatching(30, seed: 7)` starts with the 20 strings of `sku.GenerateNonMatching(20, seed: 7)`.

```csharp
string[] fewer = sku.GenerateNonMatching(3, seed: 7);
string[] more = sku.GenerateNonMatching(5, seed: 7);

Console.WriteLine(string.Join("  ", fewer)); // prints: KIA-/400  BYO1898  KIA-540
Console.WriteLine(more.Take(3).SequenceEqual(fewer)); // False
```

When you need both a short list and a long list that agree, generate the long one and take a prefix of it.

:::warning

Without a seed, the generator seeds from `Environment.TickCount`, which changes only every few milliseconds. Two calls made one right after the other can return exactly the same samples:

```csharp
string[] a = sku.GenerateMatching(3);
string[] b = sku.GenerateMatching(3);
Console.WriteLine(a.SequenceEqual(b)); // often True: both calls usually see the same tick
```

When you want different samples, pass different seeds. In tests, always pass a seed.

:::

The same seed gives the same samples for the same pattern and library version. Every sample on this page was checked on both .NET 8 and .NET 10, and the output was identical. A future version of the library may change how samples are produced, so do not hard-code generated strings as expected values in your tests. Assert on a property of each sample instead, as shown in [Using generated data in tests](#using-generated-data-in-tests).

## How many samples you get

You ask for *up to* `count` samples. You get fewer in these cases:

| Situation | Example | Result |
|-----------|---------|--------|
| The pattern matches fewer distinct strings than you asked for. | `^(?:yes\|no)$`, count 10 | `yes`, `no` |
| The pattern has an open-ended quantifier with nothing around it. | `^a+$`, count 10 | 5 samples: lengths 1 to 5 |
| Lookaheads reject most of the candidates. | `^(?=.*q)(?=.*z)[a-z]{3}$`, count 10 | 5 samples |
| The pattern can never match. | `[^\s\S]` with `GenerateMatching` | Empty array |
| The pattern matches every string, so nothing is left to reject. | `.*` with `GenerateNonMatching` | Empty array |
| Every match is longer than the [sample length limit](#limits). | `^a{2001}$` | Empty array |

```csharp
using System;
using Masterly.FluentRegex;

Pattern yesNo = Pattern.With.StartOfLine.OneOf("yes", "no").EndOfLine;
Console.WriteLine(string.Join("  ", yesNo.GenerateMatching(10, seed: 1))); // prints: yes  no

Console.WriteLine(Pattern.FromRegex("^a+$").GenerateMatching(10, seed: 1).Length); // 5

Pattern needsQandZ = Pattern.FromRegex(@"^(?=.*q)(?=.*z)[a-z]{3}$");
Console.WriteLine(string.Join("  ", needsQandZ.GenerateMatching(10, seed: 1)));
// prints: rqz  qzg  qzc  zqv  qez

Pattern impossible = Pattern.With.NegatedSet(Pattern.With.Whitespace.NonWhitespace);
Console.WriteLine(impossible.GenerateMatching(3, seed: 1).Length); // 0

Pattern everything = Pattern.With.Anything.Repeat.ZeroOrMore;
Console.WriteLine(everything.GenerateNonMatching(5, seed: 1).Length); // 0
```

- The generator samples at random and stops after a fixed number of attempts. It does not enumerate the language. For `GenerateMatching` the budget is `count × 60 + 200` candidates.
- A narrow pattern, such as the `q`-and-`z` example, still has many valid strings. The generator just rarely hits them by chance. Ask for more samples, or rewrite the pattern so the constraint is part of the structure instead of a lookahead.
- An empty result is not an error. If your test needs at least one sample, assert on the length.

:::tip

To check whether a pattern can match at all, or to get its shortest match, use the proven answers in [Language Facts](./language-facts.md) and [Pattern Algebra](./pattern-algebra.md) instead of sampling.

:::

## What "matching" means: anchors

The generator verifies samples with `IsMatch`, which looks for a match *anywhere* in the input. So the meaning of "matching" and "non-matching" depends on whether the pattern is anchored.

```csharp
Pattern threeDigits = Pattern.With.Digit.Repeat.Times(3).WithDigits(DigitMode.Ascii);

Console.WriteLine(string.Join("  ", threeDigits.GenerateNonMatching(6, seed: 1).Select(s => $"\"{s}\"")));
// prints: "9 2"  "17"  "0!3"  "1 8"  "04"  "02é"

Pattern exactlyThree = Pattern.With.StartOfLine.Digit.Repeat.Times(3).EndOfLine.WithDigits(DigitMode.Ascii);

Console.WriteLine(string.Join("  ", exactlyThree.GenerateNonMatching(6, seed: 1).Select(s => $"\"{s}\"")));
// prints: "9962"  "382x"  "9 2"  "7008"  "023 "  "17"
```

- Without anchors, a non-matching sample must not contain three digits in a row *anywhere*. `9962` would match, because it contains `996`. So the near misses are all shorter or broken up.
- With anchors, `9962` and `382x` are rejected because the whole string must be exactly three digits. These are usually the near misses you want for a validator.
- Matching samples of an unanchored pattern contain only the match itself. The generator does not add text around it.

When you generate data for a validator that checks a whole value, anchor the pattern with `StartOfLine` and `EndOfLine` (or use a [common pattern](./common-patterns.md), which is already anchored).

## How samples are built

You do not need these details to use the methods. They help you predict what the samples look like.

### Matching samples

The generator walks the pattern's syntax tree and builds a candidate string, then asks the .NET engine whether the candidate matches. Only verified, unseen candidates are kept.

| Construct | How the generator handles it |
|-----------|------------------------------|
| Literal | Appended as is. With `IgnoreCase`, its case is flipped one time in four. |
| Character class, `\d`, `\w`, `.` | One member is picked. Printable ASCII is preferred: it is picked about 92% of the time when the class has any. Lone surrogates are never picked. |
| Alternation | One branch is picked at random. |
| Quantifier | A repetition count is picked, favoring the boundaries. See [Quantifier counts favor the boundaries](#quantifier-counts-favor-the-boundaries). |
| Group | Its body is generated. A capturing group also remembers its text for backreferences. A balancing group `(?<open-close>...)` forgets the capture of `close`. |
| Backreference | The remembered text is copied. When the group has not captured yet, nothing is added. |
| Conditional `(?(name)yes\|no)` | Takes the "yes" branch when the group has captured, the "no" branch otherwise. Expression conditionals pick a branch at random. |
| Lookahead, lookbehind | Generate nothing. The verification step enforces them. |
| Anchors, inline options, comments | Generate nothing. |

Because every candidate is verified, constructs the generator cannot satisfy directly, such as lookarounds, still produce correct samples. They just cost more attempts.

### Quantifier counts favor the boundaries

Off-by-one bugs live at the edges of a range, so the generator picks the minimum and the maximum more often than the counts in between.

| Quantifier | Counts the generator picks |
|------------|----------------------------|
| `{n}` | Always `n`. |
| `{min,max}` with `max - min` up to 6 | A forced `min` one time in ten, a forced `max` one time in ten, and an even pick from `min` to `max` the rest of the time. For `{2,5}`, lengths 2 and 5 each come up about 30% of the time, and 3 and 4 about 20% each. |
| `{min,max}` with `max - min` above 6 | The same rule, but over `min` to `min + 6` only. |
| `*`, `+` and `{n,}`, greedy or lazy | The same rule, over the minimum to the minimum plus 4. |

```csharp
Console.WriteLine(string.Join("  ", Pattern.FromRegex("^a{0,3}$").GenerateMatching(10, seed: 1).Select(s => $"\"{s}\"")));
// prints: ""  "aaa"  "a"  "aa"

Console.WriteLine(string.Join(", ", Pattern.FromRegex("^a{1,100}$").GenerateMatching(20, seed: 1).Select(s => s.Length).OrderBy(n => n)));
// prints: 1, 2, 3, 4, 5, 6, 7
```

:::note

For a wide range such as `{1,100}`, the generator never reaches the upper bound. Samples stay short and readable, but the maximum length is not tested. Add that edge case by hand, for example `new string('a', 100)` and `new string('a', 101)`.

:::

### Non-matching samples

`GenerateNonMatching` works in three steps and stops as soon as it has `count` strings:

1. **Near misses.** It generates a pool of valid samples (`count × 2`, at least 5 and at most 40), then applies one random edit to a random sample from the pool. It keeps the result when the engine rejects it.
2. **Edge cases.** It tries the empty string and a single space.
3. **Random strings.** It builds strings of 0 to 11 characters from the pattern's alphabet.

The edits used for near misses are:

| Edit | Illustration on `KIA-5400` |
|------|-----------------------|
| Insert a character | `KIA-5x400` |
| Delete a character | `KIA5400` |
| Replace a character | `KIA-/400` |
| Duplicate a character | `KIA--5400` |
| Swap two neighbors | `KAI-5400` |
| Truncate | `KIA-540` |
| Append a character | `KIA-5400;` |
| Prepend a character | `#KIA-5400` |

The characters used for edits and random strings come from the pattern's own literals plus a fixed set of troublemakers: `a`, `Z`, `0`, `9`, space, tab, `-`, `_`, `.`, `@`, `/`, `:`, `;`, `,`, `!`, `#`, `?`, `x`, `Q` and `é`.

```csharp
Console.WriteLine(string.Join("  ", sku.GenerateNonMatching(10, seed: 5)));
// prints: HCC-!9117  OLA-4524a  KFB-x989  CUC-971  OLA-4.24  CHC-5366_  HQY-91  BUD-649x  WHP--6653  CHC-5366Q
```

- Near misses stay close to the shape of a real value. A wrong length, a stray symbol and a missing separator all show up in one short list.
- When the pattern matches only a few strings, the pool is small and the list moves on to edge cases and random strings sooner. For the `yes`/`no` pattern, `GenerateNonMatching(10, seed: 1)` returns `"_no"  ""  "nno"  "yees"  "eys"  "on"  "ye0"  "yyes"  "eyes"  "yes,"`.
- When the pattern can never match, the pool is empty and there is nothing to edit. Every string then comes from the edge cases and the random strings: for `[^\s\S]`, `GenerateNonMatching(5, seed: 1)` returns `""  " "  "!9"  "@0/x!?\t,."  "@@-?QQx #/Z"`, where `\t` stands for a tab character.
- Non-matching samples can contain whitespace, including tabs. Quote them when you print them.

## Constructs and options that are honored

Because every sample is verified by the real engine, backreferences, lookarounds, options and conditionals all work.

```csharp
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

// A backreference: the second half repeats the first
Pattern doubled = Pattern.With.StartOfLine
    .NamedGroup("word", Pattern.With.Letter.Repeat.Times(2, 5))
    .Literal("-")
    .Backreference("word")
    .EndOfLine;
Console.WriteLine(string.Join("  ", doubled.GenerateMatching(4, seed: 5)));
// prints: gwx-gwx  TD-TD  EN-EN  om-om

// Lookaheads: at least one lower-case letter, upper-case letter, digit and symbol
Console.WriteLine(string.Join("  ", CommonPatterns.StrongPassword(DigitMode.Ascii).GenerateMatching(3, seed: 11).Select(s => $"\"{s}\"")));
// prints: "7e훼$gHEI-"  "셄'ptqo!LP@.2"  " +PCO⫛t|1wFx"

// IgnoreCase: case variants of the literal
Pattern keyword = Pattern.With.StartOfLine.Literal("abc").EndOfLine.WithOptions(RegexOptions.IgnoreCase);
Console.WriteLine(string.Join("  ", keyword.GenerateMatching(10, seed: 2)));
// prints: abC  aBc  AbC  Abc  aBC  abc  ABc  ABC

// A conditional: a closing > only when the opening < was captured
Pattern tag = Pattern.FromRegex(@"^(<)?[a-z]+(?(1)>)$");
Console.WriteLine(string.Join("  ", tag.GenerateMatching(6, seed: 2)));
// prints: cufta  tcix  <y>  w  <h>  mmq
```

- The password samples contain Korean and other non-ASCII characters because `StrongPassword` uses `.`, which accepts any character except a newline. The generator picks a non-ASCII member about 8% of the time, so you also test how your code handles them.
- The `IgnoreCase` pattern has only 8 distinct matches, so 8 samples come back for a count of 10.
- Generation ignores `RegexOptions.Compiled`, which does not change what matches.

## Samples are only as strict as the pattern

The generator produces what the pattern accepts, not what you meant it to accept. Check two things before you trust the samples.

### Unicode digits

In .NET, `\d` matches any Unicode decimal digit, not only `0` to `9`. The parameterless [common patterns](./common-patterns.md) keep that default, so some samples contain digits such as `৪` or `꤂`:

```csharp
Console.WriteLine(string.Join("  ", CommonPatterns.IPv4().GenerateMatching(5, seed: 7)));
// prints: 233.꤂.4.53  250.231.250.165  088.00.254.254  23৪.250.1.22  251.4.207.222

Console.WriteLine(string.Join("  ", CommonPatterns.IPv4(DigitMode.Ascii).GenerateMatching(5, seed: 7)));
// prints: 233.8.4.53  250.231.250.165  088.00.254.254  231.250.1.22  251.4.207.222
```

A validator that uses `byte.TryParse` rejects the first and fourth address, and the test fails. That is a real difference between the regex and the validator, not a bug in the generator. Decide which behavior you want:

- When your input should contain only `0` to `9`, use the `DigitMode.Ascii` overload of the common pattern, or call `.WithDigits(DigitMode.Ascii)` on your own pattern. Fix the pattern you use in production, not only the one in the test.
- When you really accept every Unicode digit, keep the default and make the validator accept them too.

See [Unicode](./unicode.md) for more on digit modes.

### Rules the pattern does not encode

`CommonPatterns.DateIso()` checks the *shape* of a date, not whether the month and day exist:

```csharp
Console.WriteLine(string.Join("  ", CommonPatterns.DateIso(DigitMode.Ascii).GenerateMatching(5, seed: 3)));
// prints: 6119-08-09  8450-05-12  0846-16-15  6007-11-28  2616-06-35
```

Month 16 and day 35 match the pattern's regex, `^[0-9]{4}-[0-1][0-9]-[0-3][0-9]$`. If your code parses the value as a real date, filter the samples first, or tighten the pattern so it encodes the rule.

## Limits

| Limit | Value | Effect |
|-------|-------|--------|
| Sample length | 2,000 characters | A candidate that has grown past 2,000 characters is abandoned, and another attempt is made. `^a{2000}$` returns its single match. `^a{2001}$` returns nothing. `^(?:a{3000}\|b)$` returns only `b`. The check runs before each part of the pattern, so the last part can still push a sample slightly over the limit: unanchored `a{2001}` returns its 2,001-character match. |
| Matching attempts | `count × 60 + 200` | `GenerateMatching` stops after this many candidates, even if it has fewer than `count` samples. |
| Non-matching attempts | `count × 80 + 200` per step | Applies separately to the near-miss step and the random-string step. |
| Open-ended repetition | Minimum plus 4 | `*`, `+` and `{n,}` never produce more than 4 extra repetitions. |
| Bounded repetition | Minimum plus 6 | Wide ranges never reach their maximum. |
| Verification time | 1 second per candidate | A candidate the engine cannot decide within one second is skipped, not returned. The pattern's own `WithTimeout` value is not used for verification. |

:::note

A pattern with catastrophic backtracking can make generation slow: each candidate can take up to a second to verify before it is skipped. Such patterns also return fewer samples. Check them with [ReDoS Safety](./redos-safety.md) first.

:::

## Errors

```csharp
Console.WriteLine(Pattern.With.Digit.GenerateMatching(0).Length); // 0

Pattern.With.Digit.GenerateMatching(-1);
// throws ArgumentOutOfRangeException: Specified argument was out of the range of valid values. (Parameter 'count')

Pattern.FromRegex(@"^\p{Cs}$").GenerateMatching(3, seed: 1);
// throws InvalidOperationException: The character class has no character that can be generated (for example, it contains only surrogate code units).

// userRegex comes from your application and holds "(abc"
Pattern.With.RegEx(userRegex).GenerateMatching(1, seed: 1);
// throws InvalidOperationException: The pattern is not valid regex syntax: Not enough )'s (at position 0)
```

- **Lone surrogates.** The generator never picks a lone surrogate (`U+D800` to `U+DFFF`), because it would make a broken string. A class that contains *only* surrogates, such as `\p{Cs}`, has nothing left to pick, so the call throws. A class that contains surrogates *and* other characters works normally.
- **Invalid syntax.** `Pattern.FromRegex` validates its input when you create the pattern, so this error only reaches generation through raw fragments such as `RegEx(...)`. A constant invalid fragment is also reported at build time by the [compile-time checks](./compile-time-checks.md).
- **Impossible or too-narrow patterns** do not throw. They return fewer samples, or none.

## Using generated data in tests

Generated data turns one hand-written example into dozens of test cases. Assert on a property that must hold for every sample, never on the exact strings.

The examples below test this validator:

```csharp
using System.Globalization;
using System.Linq;

public static class Ipv4Validator
{
    public static bool IsValid(string text)
    {
        string[] parts = text.Split('.');
        return parts.Length == 4 && parts.All(IsOctet);
    }

    private static bool IsOctet(string part) =>
        part.Length is >= 1 and <= 3
        && byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out byte _);
}
```

### Plain assertions

Generate the samples inside the test and check every one. This works with any test framework.

```csharp
using Masterly.FluentRegex;
using Xunit;

public static class Patterns
{
    public static Pattern IPv4 => CommonPatterns.IPv4(DigitMode.Ascii);
}

public class Ipv4ValidatorTests
{
    [Fact]
    public void IsValid_GeneratedAddresses_ReturnsTrue()
    {
        string[] addresses = Patterns.IPv4.GenerateMatching(100, seed: 1);

        Assert.All(addresses, address => Assert.True(Ipv4Validator.IsValid(address), address));
    }

    [Fact]
    public void IsValid_NearMisses_ReturnsFalse()
    {
        string[] nearMisses = Patterns.IPv4.GenerateNonMatching(100, seed: 1);

        Assert.All(nearMisses, nearMiss => Assert.False(Ipv4Validator.IsValid(nearMiss), nearMiss));
    }
}
```

- Pass the sample as the assertion message. When a sample fails, the report shows which string it was.
- Keep the seed fixed. A failure then reproduces on every run and on every machine.
- The pattern uses `DigitMode.Ascii` because the validator accepts only `0` to `9`. With the default `CommonPatterns.IPv4()`, 19 of the 100 samples for seed 1 contain non-ASCII digits and the first test fails. See [Unicode digits](#unicode-digits).

### xUnit theory data

With the `Masterly.FluentRegex.Xunit` package, each sample becomes its own theory row, with the sample in the test name.

```bash
dotnet add package Masterly.FluentRegex.Xunit
```

```csharp
using Masterly.FluentRegex.Xunit;
using Xunit;

public class Ipv4ValidatorTheories
{
    [Theory]
    [MatchingData(typeof(Patterns), nameof(Patterns.IPv4), Count = 50, Seed = 42)]
    public void IsValid_MatchingData_ReturnsTrue(string address)
    {
        Assert.True(Ipv4Validator.IsValid(address));
    }

    [Theory]
    [NonMatchingData(typeof(Patterns), nameof(Patterns.IPv4), Count = 50, Seed = 42)]
    public void IsValid_NonMatchingData_ReturnsFalse(string nearMiss)
    {
        Assert.False(Ipv4Validator.IsValid(nearMiss));
    }
}
```

- The attributes call `GenerateMatching` and `GenerateNonMatching` for you. `Count` defaults to 25 and `Seed` defaults to 0, so the rows are the same on every run.
- An attribute that produces no samples fails the theory with an `InvalidOperationException` instead of passing with zero rows. A `Count` below 1 fails the same way.
- See [xUnit Integration](./xunit.md) for every option and error message.

### Property checks

To check a rule against generated samples and get a report with the first counter-example, use `PatternProperty.ForMatching` and `PatternProperty.ForNonMatching`. They are built on the same generator. See [Property Testing](./property-testing.md).

## From the command line

The [command-line tool](./command-line-tool.md) prints samples one per line, so you can paste them into a test or a data file.

```bash
fluentregex generate '^\d{3}-[A-Z]{2}$' --count 4 --seed 1 --ascii-digits
# 174-YQ
# 296-PS
# 031-HX
# 708-BN

fluentregex generate '^\d{3}-[A-Z]{2}$' --count 4 --seed 1 --ascii-digits --non-matching
# 708/-BN
# 708-_BN
# a023-HW
# 296-PSS
```

| Option | Default | Meaning |
|--------|---------|---------|
| `--count N` | `10` | The most samples to print. A negative value is an error, and the tool exits with code 2. |
| `--seed N` | none | Makes the output reproducible. Without it, each run prints different samples. |
| `--non-matching` | off | Prints non-matching strings, mostly near misses, instead of matching samples. |
| `--options <list>` | none | Comma-separated `RegexOptions` names, such as `IgnoreCase,Multiline`. |
| `--ascii-digits` | off | Treats `\d` as `0` to `9` only. It is a global option, so the `generate` line of `--help` does not list it. |

The command prints the same strings as `Pattern.FromRegex(regex, options).GenerateMatching(count, seed)`, or `GenerateNonMatching` with `--non-matching`. With `--ascii-digits`, the pattern is converted with `WithAsciiDigits()` first.

## Performance notes

- Each call parses the pattern and builds a fresh verifier regex before it generates anything. Ask for all the samples you need in one call instead of calling the method in a loop with a count of 1.
- Generate once per test class, for example in a `static readonly` field or with the theory attributes, rather than inside every test.
- Every candidate is verified with the engine, and rejected candidates still cost an attempt. Narrow lookaheads and patterns that match rarely by chance need more attempts per sample. If a call is slow or returns too few samples, simplify the pattern you generate from.
- Other features reuse this generator: the [Optimizer](./optimizer.md) uses matching and non-matching samples to check that an optimized pattern behaves like the original, [AI drafting](./ai-drafting.md) uses matching samples to show what a drafted pattern accepts, and [ReDoS Safety](./redos-safety.md) uses it internally to build sample inputs for its attack strings.

## Checklist

- Pass a `seed` in every test.
- Anchor patterns you use for whole-value validation.
- Use `DigitMode.Ascii` when your input is ASCII-only.
- Assert on properties of each sample, never on the exact generated strings.
- Treat an empty result as "nothing found by sampling", not as proof that no match exists.
- Add the maximum of a wide range, and other edges the generator does not reach, as hand-written cases.
