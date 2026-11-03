---
id: property-testing
title: "Property Testing"
description: "Check that a property holds for every string a pattern generates, and shrink a failure to the smallest input that still breaks it."
---

A property test checks a rule that must hold for a whole family of inputs, not for one hand-picked example. `PatternProperty` uses a pattern as that family: it generates strings the pattern matches (or near misses it rejects), runs your property on each one, and stops at the first failure. It then shrinks the failing input to the smallest, simplest string that still fails, so you debug `"௮"` instead of `"554౧4"`.

Use it when code downstream of a pattern makes assumptions about the text: a parser that must accept everything the pattern accepts, a hand-written validator that must agree with the pattern, or a formatter that must round-trip.

```csharp
using System;
using Masterly.FluentRegex;

Pattern number = Pattern.FromRegex(@"^\d{1,5}$");

PropertyResult result = PatternProperty.ForMatching(number, s => int.TryParse(s, out _));

Console.WriteLine(result.Passed);                 // False
Console.WriteLine(result.OriginalCounterexample); // 554౧4  (the first generated sample that failed)
Console.WriteLine(result.Counterexample);         // ௮      (after shrinking)
```

- `\d` matches any Unicode decimal digit, and the sample generator deliberately produces some. `int.TryParse` only accepts the ASCII digits `0` to `9`, so the property fails.
- `"554౧4"` contains a Telugu digit one. Shrinking reduced it to a single Tamil digit eight (`௮`): the shortest string that the pattern still matches and the property still rejects.
- The fix is to say what you mean: `[0-9]`, `.WithDigits(DigitMode.Ascii)` or `RegexOptions.ECMAScript`. See [Unicode](./unicode.md).

:::tip[What you need]

`PatternProperty`, `PropertyOptions`, `PropertyResult` and `PropertyFailedException` are part of the core `Masterly.FluentRegex` package and live in the `Masterly.FluentRegex` namespace. They work on every target the package supports (.NET Standard 2.1, .NET 8 and .NET 10), have no dependencies and work with any test framework.

:::

## Checking a property

There are two entry points. Both take the same arguments and return a `PropertyResult`.

| Method | Inputs it generates | Use it to check |
|--------|---------------------|-----------------|
| `PatternProperty.ForMatching(pattern, property, options)` | Strings the pattern matches, from `pattern.GenerateMatching`. | That your code accepts or handles everything the pattern lets through. |
| `PatternProperty.ForNonMatching(pattern, property, options)` | Strings the pattern does not match, from `pattern.GenerateNonMatching`. Most are near misses one edit away from a valid sample. | That your code rejects everything the pattern rejects. |

| Parameter | Type | Meaning |
|-----------|------|---------|
| `pattern` | `Pattern` | The pattern that generates the inputs. Required. |
| `property` | `Func<string, bool>` | The rule to check. Return `true` when it holds. Return `false` or throw to signal a failure. Required. |
| `options` | `PropertyOptions?` | Sample count, seed and shrink budget. Optional; `null` uses the defaults (100 samples, seed 0, 1000 shrink steps). See [Options](#options). |

### Strings the pattern matches

`ForMatching` answers "does my code handle every string this pattern accepts?". Here the fix from the opening sample passes for 500 samples:

```csharp
Pattern ascii = Pattern.FromRegex("^[0-9]{5}$");

PropertyResult result = PatternProperty.ForMatching(
    ascii,
    s => int.TryParse(s, out _),
    new PropertyOptions { Samples = 500, Seed = 42 });

Console.WriteLine(result); // Property passed for 500 matching sample(s) (seed 42).
```

Generated inputs come from the same engine as [Test Data Generation](./test-data-generation.md). Every sample is checked against the real .NET regex engine before it is used, so lookarounds, backreferences and options are respected.

### Strings the pattern rejects

`ForNonMatching` answers "does my code reject every string this pattern rejects?". It is the natural way to test a hand-written validator against the pattern it is supposed to implement. Write the property as "the validator says no":

```csharp
using System.Linq;

Pattern zip = Pattern.FromRegex("^[0-9]{5}$");

// A validator that trims its input first, so it is more lenient than the pattern
static bool IsZip(string value) => value.Trim().Length == 5 && value.Trim().All(char.IsAsciiDigit);

PropertyResult result = PatternProperty.ForNonMatching(zip, s => !IsZip(s));

Console.WriteLine(result.Passed);                         // False
Console.WriteLine($"\"{result.OriginalCounterexample}\""); // " 53716"
Console.WriteLine($"\"{result.Counterexample}\"");         // " 00000"
```

- The near miss `" 53716"` is a valid ZIP code with a space prepended. The pattern rejects it; the validator trims the space and accepts it.
- Shrinking kept the leading space, because that is what causes the failure, and simplified the digits to `0`.
- `char.IsAsciiDigit` needs .NET 7 or later. On older targets, compare against `'0'` and `'9'` instead.

Non-matching samples are built in three stages: near misses (a matching sample with one character inserted, removed, replaced, duplicated, swapped, truncated, appended or prepended), then the empty string and a single space, then short random strings. The extra characters come from the pattern's own literals plus a small fixed set (`a`, `Z`, `0`, `9`, space, tab, `-_.@/:;,!#?xQ` and `é`).

:::note

Because the extra characters come from that small alphabet, `ForNonMatching` is good at finding off-by-one lengths, stray separators and whitespace, and weak at exotic Unicode. If your validator must reject, say, Arabic-Indic digits, add an example-based test for them as well.

:::

### Checking a validator from both sides

Run both entry points to show that a validator and a pattern agree on what is valid. Build the pattern fluently or from a regex; it makes no difference:

```csharp
Pattern sku = Pattern.With
    .StartOfLine
    .Set(Pattern.With.Range('A', 'Z')).Repeat.Times(3)
    .Literal("-")
    .Set(Pattern.With.Range('0', '9')).Repeat.Times(4)
    .EndOfLine;

static bool IsSku(string value) =>
    value.Length == 8
    && value.Take(3).All(c => c >= 'A' && c <= 'Z')
    && value[3] == '-'
    && value.Skip(4).All(c => c >= '0' && c <= '9');

Console.WriteLine(PatternProperty.ForMatching(sku, IsSku));
// prints: Property passed for 100 matching sample(s) (seed 0).

Console.WriteLine(PatternProperty.ForNonMatching(sku, s => !IsSku(s)));
// prints: Property passed for 100 non-matching sample(s) (seed 0).
```

`sku` builds `^[A-Z]{3}-[0-9]{4}$`. With seed 0 the matching samples start with `"VOO-4244"`, `"AZI-8056"` and `"CLZ-7397"`. The non-matching ones are near misses such as `"ABG5799"`, `"PG -0149"`, `"ZMD-00052"` and `"EVG-1584\t"`, plus the empty string.

### Exceptions are failures

A property that throws fails, just like one that returns `false`. The exception is caught and kept on the result:

```csharp
PropertyResult result = PatternProperty.ForMatching(
    Pattern.FromRegex("^[a-z]{1,10}$"),
    s => s.Contains('q') ? throw new InvalidOperationException("no q") : true);

Console.WriteLine(result.OriginalCounterexample); // vwqxcev
Console.WriteLine(result.Counterexample);         // q
Console.WriteLine(result.Exception?.Message);     // no q
```

- Every exception type is caught, including `NullReferenceException` and assertion exceptions from your test framework. That lets you write the property with assertions inside it.
- `result.Exception` is the exception thrown for the shrunk `Counterexample`, not for the original. When the shrunk input fails by returning `false`, it is `null`. See [The failure can change while shrinking](#the-failure-can-change-while-shrinking).

### When there is nothing to test

If the pattern cannot produce a single sample of the requested kind, both methods throw `InvalidOperationException` instead of reporting a pass that tested nothing:

```csharp
try
{
    PatternProperty.ForNonMatching(Pattern.FromRegex(".*"), s => true);
}
catch (InvalidOperationException exception)
{
    Console.WriteLine(exception.Message);
    // prints: The pattern ".*" produced no non-matching samples, so the property cannot be tested.
}
```

`.*` matches every string, so it has no near misses. A pattern that matches nothing, such as `(?!)`, fails the same way for `ForMatching`: `The pattern "(?!)" produced no matching samples, so the property cannot be tested.`

### What throws

| Exception | When |
|-----------|------|
| `ArgumentNullException` | `pattern` or `property` is `null`. `ParamName` is `"pattern"` or `"property"`. |
| `InvalidOperationException` | The pattern produced no samples of the requested kind. Also thrown, by the generator, in the rare case that the library's own regex parser cannot read the pattern (message `The pattern is not valid regex syntax: ...`). |
| `ArgumentOutOfRangeException` | Thrown by the `PropertyOptions` setters: `Samples` below 1 or `MaxShrinkSteps` below 0. |
| `PropertyFailedException` | Only from `result.ThrowIfFailed()`, when the property failed. `ForMatching` and `ForNonMatching` never throw because the property failed; they return a failed result. |

## Reading the result

A `PropertyResult` describes one run. It is a sealed class with no public constructor: you only get one from `ForMatching` or `ForNonMatching`. A passed result has `Passed == true` and no counterexample.

| Member | Type | Meaning |
|--------|------|---------|
| `Passed` | `bool` | `true` when the property held for every sample. |
| `Counterexample` | `string?` | The smallest failing input found by shrinking. `null` when the property passed. |
| `OriginalCounterexample` | `string?` | The first generated input that failed, before shrinking. `null` when the property passed. |
| `SamplesTested` | `int` | How many generated samples were checked, including the failing one. On a pass, the number of samples generated (can be fewer than `Samples`). |
| `ShrinkSteps` | `int` | How many candidate strings were evaluated while shrinking. `0` when the property passed. |
| `Exception` | `Exception?` | The exception the property threw for `Counterexample`. `null` when it returned `false` or passed. |
| `Seed` | `int` | The seed used for generation. Re-run with it to reproduce. |
| `ThrowIfFailed()` | `void` | Throws `PropertyFailedException` when the property failed. Does nothing when it passed. |
| `ToString()` | `string` | A one-line summary on a pass, or the full failure report on a failure. |

On a failure, `ToString()` returns the same report that `ThrowIfFailed()` puts in the exception message:

```csharp
using System;
using System.Globalization;
using Masterly.FluentRegex;

Pattern isoDate = CommonPatterns.DateIso(DigitMode.Ascii);   // ^[0-9]{4}-[0-1][0-9]-[0-3][0-9]$

PropertyResult result = PatternProperty.ForMatching(
    isoDate,
    s => DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));

Console.WriteLine(result);
```

```text
Property failed for a matching sample of pattern "^[0-9]{4}-[0-1][0-9]-[0-3][0-9]$".
  Counterexample:          "0000-00-00"
  Original counterexample: "9380-16-04"
  Samples tested:          2
  Shrink steps:            60
  Seed:                    0
Reproduce with PatternProperty.ForMatching(pattern, property, new PropertyOptions { Seed = 0, Samples = 100 }), or call the property directly with "0000-00-00".
```

The shrunk counterexample tells you the pattern only checks the shape of a date: `DateIso` accepts month 16, month 00 and year 0000, none of which `DateTime` accepts.

How to read the report:

- The pattern and every string are printed as quoted C# literals. Backslashes are doubled, and `"`, tab (`\t`), newline (`\n`), carriage return (`\r`) and NUL (`\0`) are escaped. Other control characters appear as `\uXXXX`. A counterexample that contains a tab shows as `"a\tb"`, so invisible characters cannot hide.
- An `Exception:` line, with the exception's short type name and message, appears between `Shrink steps:` and `Seed:` only when the property threw for the shrunk counterexample. For the sample in [Exceptions are failures](#exceptions-are-failures), the line shows `InvalidOperationException: no q` after the `Exception:` label.
- The last line tells you how to reproduce the run: the method (`ForMatching` or `ForNonMatching`), the seed and the requested sample count. You can also call your property directly with the counterexample.
- On a pass, `ToString()` is one line: `Property passed for 100 non-matching sample(s) (seed 0).`

## Using it in a test

`PatternProperty` does not depend on any test framework. Call `ThrowIfFailed()` at the end of the test: a failed property throws `PropertyFailedException`, and every test runner reports an unexpected exception as a failed test, with the full report as the message.

```csharp
using Masterly.FluentRegex;
using Xunit;

public class NumberPropertyTests
{
    [Fact]
    public void Number_WhenGenerated_AlwaysParses()
    {
        PatternProperty.ForMatching(Pattern.FromRegex(@"^\d{1,5}$"), s => int.TryParse(s, out _))
            .ThrowIfFailed();
    }
}
```

`dotnet test` reports:

```text
  Failed NumberPropertyTests.Number_WhenGenerated_AlwaysParses [28 ms]
  Error Message:
   Masterly.FluentRegex.PropertyFailedException : Property failed for a matching sample of pattern "^\\d{1,5}$".
  Counterexample:          "௮"
  Original counterexample: "554౧4"
  Samples tested:          1
  Shrink steps:            19
  Seed:                    0
Reproduce with PatternProperty.ForMatching(pattern, property, new PropertyOptions { Seed = 0, Samples = 100 }), or call the property directly with "௮".
```

If you prefer your framework's own assertion, assert on `Passed` and pass `ToString()` as the message:

```csharp
// NUnit
using System.Linq;
using Masterly.FluentRegex;
using NUnit.Framework;

public class ZipCodeTests
{
    private static readonly Pattern ZipCode = Pattern.FromRegex("^[0-9]{5}$");

    [Test]
    public void ZipValidator_ForEveryMatchingZipCode_AcceptsIt()
    {
        PropertyResult result = PatternProperty.ForMatching(ZipCode, s => s.Length == 5 && s.All(char.IsAsciiDigit));

        Assert.That(result.Passed, Is.True, result.ToString());
    }
}
```

```csharp
// MSTest
using System.Linq;
using Masterly.FluentRegex;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ZipCodeTests
{
    private static readonly Pattern ZipCode = Pattern.FromRegex("^[0-9]{5}$");

    [TestMethod]
    public void ZipValidator_ForEveryMatchingZipCode_AcceptsIt()
    {
        PropertyResult result = PatternProperty.ForMatching(ZipCode, s => s.Length == 5 && s.All(char.IsAsciiDigit));

        Assert.IsTrue(result.Passed, result.ToString());
    }
}
```

### The exception

`PropertyFailedException` is a sealed `Exception`.

| Member | Meaning |
|--------|---------|
| `Message` | The failure report shown above. |
| `Result` | The failed `PropertyResult`, with the counterexample and seed. `null` only when you construct the exception yourself. |
| `InnerException` | The exception the property threw for the counterexample, the same object as `Result.Exception`. `null` when the property returned `false`. |

The public constructors (`()`, `(message)` and `(message, innerException)`) exist for completeness. The parameterless one uses the message `"The property failed."`. None of them sets `Result`.

### Trying more seeds

The default seed is `0`, so a property test with no options checks the same samples on every run. That keeps CI stable, but it also means a run never explores new inputs. To cover more ground, raise `Samples`, or run the same property for several seeds:

```csharp
using Masterly.FluentRegex;
using Xunit;

public class ZipCodePropertyTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ZipValidator_ForSeveralSeeds_AcceptsEveryMatchingZipCode(int seed)
    {
        PropertyOptions options = new PropertyOptions { Seed = seed, Samples = 200 };

        PatternProperty.ForMatching(Patterns.ZipCode, ZipValidator.IsValid, options)
            .ThrowIfFailed();
    }
}
```

`Patterns.ZipCode` and `ZipValidator.IsValid` stand for your own pattern and validator. When a seed fails, the report names it, so you can pin that seed in a regression test.

:::tip

`PatternProperty` and the xUnit data attributes complement each other. `[MatchingData]` and `[NonMatchingData]` from `Masterly.FluentRegex.Xunit` show each generated sample as its own row in the test explorer, but do not shrink. `PatternProperty` runs everything in one test and shrinks the failure. See [xUnit Integration](./xunit.md).

:::

## Options

`PropertyOptions` is a plain settings class. Create one with an object initializer and pass it as the third argument.

| Option | Default | Meaning |
|--------|---------|---------|
| `Samples` | `100` | How many generated strings to check. Must be at least 1; a smaller value throws `ArgumentOutOfRangeException` from the setter. |
| `Seed` | `0` | The generation seed. The same seed always gives the same samples, the same counterexample and the same shrink steps. Any `int` is allowed. |
| `MaxShrinkSteps` | `1000` | The maximum number of candidate strings evaluated while shrinking. `0` disables shrinking. A negative value throws `ArgumentOutOfRangeException`. |

**Samples**

`Samples` is an upper bound. When the pattern has fewer distinct strings, fewer are tested and the summary says so:

```csharp
PropertyResult result = PatternProperty.ForMatching(Pattern.FromRegex("^(yes|no)$"), s => s.Length <= 3);

Console.WriteLine(result); // Property passed for 2 matching sample(s) (seed 0).
```

**Seed**

`PatternProperty` always passes a seed to the generator, so runs are deterministic even when you pass no options. This is different from calling `GenerateMatching` yourself, which picks a random seed when you omit one. Changing the seed changes the samples, and can change which counterexample is found first:

| Property | Seed | Original counterexample | Counterexample | Samples tested |
|----------|------|-------------------------|----------------|----------------|
| `s => !s.Contains('z')` on `^[a-z0-9]{1,20}$` | 0 | `"z"` | `"z"` | 2 |
| | 1 | `"xn8z"` | `"z"` | 2 |
| | 7 | `"zgf0kri"` | `"z"` | 9 |

**MaxShrinkSteps**

Lower the budget when the property is slow, or set it to `0` to see the raw generated failure:

```csharp
Pattern word = Pattern.FromRegex("^[a-z0-9]{1,20}$");

PropertyResult full = PatternProperty.ForMatching(word, s => s.Length < 5);
PropertyResult raw = PatternProperty.ForMatching(word, s => s.Length < 5, new PropertyOptions { MaxShrinkSteps = 0 });

Console.WriteLine($"{full.Counterexample} after {full.ShrinkSteps} steps"); // aaaaa after 74 steps
Console.WriteLine($"{raw.Counterexample} after {raw.ShrinkSteps} steps");   // kkf9gg after 0 steps
```

When the budget runs out mid-shrink, you get the best counterexample found so far. With `MaxShrinkSteps = 10` the same run still reports `"kkf9gg" after 10 steps`: the first ten candidates were shorter generated samples (`"1"`, `"2"`, `"3"` and so on), and all of them passed the property, so none could replace the original.

## How shrinking works

Shrinking is greedy delta debugging. It is deterministic: the order of candidates depends only on the failing input.

1. **Shorter generated samples.** Samples that were generated but not yet tested, and are shorter than the failing one, are tried first, shortest first.
2. **Remove chunks.** Chunks are removed from the string, starting with half its length and halving down to single characters. A one-character string is also tried as the empty string.
3. **Simplify characters.** Each character is replaced by a simpler one: first the simplest member of its class (`A` for `A`-`Z`, `a` for `a`-`z` and for every other letter including non-Latin ones, `0` for any Unicode digit, space for any whitespace), then `a`, `0`, space and `A`. At each position the first replacement that is accepted wins. A replacement is only tried when it is strictly simpler. The order from simplest is `a`, `0`, space, `A`, the other lowercase letters, the other digits, the other uppercase letters, and then every other character.
4. Steps 2 and 3 repeat until a full pass changes nothing or `MaxShrinkSteps` is reached.

A candidate is kept only when two things are true:

- It is still on the same side of the pattern: still matching for `ForMatching`, still non-matching for `ForNonMatching`. Membership uses `Regex.IsMatch` with the pattern's options, exactly like the generator.
- It still fails the property, by returning `false` or by throwing.

Every candidate evaluated counts towards `ShrinkSteps`, including those rejected for leaving the pattern. A candidate is never evaluated twice.

The "same side" rule is why shrunk counterexamples are always real inputs you could receive. It also explains results that look odd at first:

```csharp
using System.Net;

PropertyResult result = PatternProperty.ForMatching(CommonPatterns.IPv4(), ip => IPAddress.TryParse(ip, out _));

Console.WriteLine(result.OriginalCounterexample); // 092.22١.19.11
Console.WriteLine(result.Counterexample);         // 0.0.0.٩
```

Every octet but one became `0`. The Arabic-Indic nine stays, because replacing it with `0` would make the property pass.

Options matter too. With `RegexOptions.IgnoreCase`, an uppercase letter still matches, so it can survive shrinking when it is the cause:

```csharp
using System.Text.RegularExpressions;

Pattern hex = Pattern.FromRegex("^[0-9a-f]{6}$", RegexOptions.IgnoreCase);

PropertyResult result = PatternProperty.ForMatching(hex, s => s == s.ToLowerInvariant());

Console.WriteLine(result.OriginalCounterexample); // bCC96A
Console.WriteLine(result.Counterexample);         // aaAaaa
```

### The failure can change while shrinking

Shrinking accepts any failing candidate. It does not check that the candidate fails in the same way as the original. If your property can fail for two different reasons, the shrunk counterexample may show the other one:

```csharp
PropertyResult result = PatternProperty.ForMatching(
    Pattern.FromRegex("^[a-z]{1,10}$"),
    s => s.Length > 5 ? throw new ArgumentException("too long") : !s.Contains('x'));

Console.WriteLine(result.OriginalCounterexample); // oolhmm   (threw ArgumentException)
Console.WriteLine(result.Counterexample);         // x        (returned false)
Console.WriteLine(result.Exception is null);      // True
```

When this happens, test one rule per property, or check `OriginalCounterexample` and re-run with `MaxShrinkSteps = 0` to see the first failure on its own.

## Finding real bugs

Property tests are most useful against the [Common Patterns](./common-patterns.md) and other patterns that you did not write yourself. Each result below is the real output of one line of code:

| Pattern | Property | Counterexample | What it means |
|---------|----------|----------------|---------------|
| `CommonPatterns.DateIso(DigitMode.Ascii)` | `DateTime.TryParseExact(s, "yyyy-MM-dd", ...)` | `"0000-00-00"` | The pattern checks the shape of a date, not whether it exists. |
| `CommonPatterns.Slug()` | `Slugify(s) == s` | `"໕"` | The parameterless `Slug()` uses `\d`, which accepts Lao and other Unicode digits. `Slug(DigitMode.Ascii)` passes. |
| `CommonPatterns.IPv4(DigitMode.Ascii)` | `IPAddress.TryParse(s, out _)` | `"09.0.0.0"` | The pattern allows leading zeros. .NET reads `09` as an invalid octal number and rejects the address. |

`Slugify` in the second row is `Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-')`, a typical slug function that should leave a valid slug unchanged.

## Performance and safety

- **Cost.** A run calls your property at most `Samples` times, plus at most `MaxShrinkSteps` times while shrinking. With the defaults, that is at most 1,100 calls. Keep properties free of I/O, or lower both numbers.
- **Stops at the first failure.** Samples after the first failing one are not tested (only the shorter ones are tried as shrink candidates).
- **Timeouts.** Generation and the membership checks during shrinking use a one-second regex timeout per check. A generated candidate or shrink candidate whose membership cannot be decided in time is dropped (a timed-out shrink candidate still counts as a step), so a pattern with catastrophic backtracking cannot stall a single check indefinitely. Each timeout still costs up to a second, and your property itself has no timeout. See [ReDoS Safety](./redos-safety.md).
- **No compiled regex.** `RegexOptions.Compiled` is removed for the membership check. Every other option, including `IgnoreCase`, `ECMAScript` and `CultureInvariant`, is kept.
- **Thread safety.** `PatternProperty` is stateless. Separate runs can execute in parallel tests.

## Common pitfalls

- **Unicode digits.** `\d` and the parameterless `CommonPatterns` methods accept every Unicode decimal digit. Most .NET parsers do not. If the first counterexample you see is a non-Latin digit, switch to `DigitMode.Ascii` or `[0-9]`.
- **A pass is not a proof.** 100 samples is a sample, not every string. A pass means no counterexample was found with this seed and sample count.
- **Forgetting `ThrowIfFailed()`.** `ForMatching` and `ForNonMatching` return a result; they do not throw on failure. A test that ignores the result always passes.
- **Inverting the property.** For `ForNonMatching`, the property is usually "my code rejects it" (`s => !IsValid(s)`), not `IsValid`.
- **Unanchored patterns.** Without `^` and `$`, a pattern matches any string that merely contains a match. Its non-matching samples are then only strings with no match at all: for `\d+` they are strings without a digit, such as `","`, `"?"` and the empty string, not the near misses you want. Anchor patterns that describe a whole value.

## See also

- [Test Data Generation](./test-data-generation.md) for the generator behind the samples.
- [xUnit Integration](./xunit.md) for generated theory rows.
- [Regex Coverage](./regex-coverage.md) for checking that your example tests exercise every branch of a pattern.
- [Unicode](./unicode.md) for `DigitMode` and the Unicode behavior of `\d` and `\w`.
