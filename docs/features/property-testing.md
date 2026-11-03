# Property-based testing with shrinking

`PatternProperty` checks that a property holds for every string a pattern generates. When it does not,
it shrinks the failing input to the smallest, simplest string that still fails, so you debug `"௮"`
rather than `"554౧4"`. It lives in the core package (`Masterly.FluentRegex`) and has no dependencies.

```csharp
using Masterly.FluentRegex;

Pattern number = Pattern.FromRegex(@"^\d{1,5}$");

PropertyResult result = PatternProperty.ForMatching(number, s => int.TryParse(s, out _));

result.Passed;                  // false
result.OriginalCounterexample;  // "554౧4"  (the first generated sample that failed)
result.Counterexample;          // "௮"      (after shrinking)
```

`\d` matches any Unicode decimal digit, and the generator deliberately produces some. `int.TryParse`
only accepts ASCII digits, so the property fails. The fix is `[0-9]` (or `RegexOptions.ECMAScript`):

```csharp
Pattern ascii = Pattern.FromRegex("^[0-9]{5}$");

PatternProperty.ForMatching(ascii, s => int.TryParse(s, out _), new PropertyOptions { Samples = 500, Seed = 42 })
    .ToString();   // "Property passed for 500 matching sample(s) (seed 42)."
```

## In a test

Call `ThrowIfFailed()` inside any test framework:

```csharp
[Fact]
public void Every_generated_number_parses()
{
    PatternProperty.ForMatching(Pattern.FromRegex(@"^\d{1,5}$"), s => int.TryParse(s, out _))
        .ThrowIfFailed();
}
```

The `PropertyFailedException` message shows the escaped, quoted counterexample, the original, the seed
and how to reproduce:

```text
Property failed for a matching sample of pattern "^\\d{1,5}$".
  Counterexample:          "௮"
  Original counterexample: "554౧4"
  Samples tested:          1
  Shrink steps:            19
  Seed:                    0
Reproduce with PatternProperty.ForMatching(pattern, property, new PropertyOptions { Seed = 0, Samples = 100 }), or call the property directly with "௮".
```

`exception.Result` gives you the full `PropertyResult`. When the property threw, the thrown exception is
the `InnerException` and appears in the message.

## Matching and non-matching inputs

| Method | Inputs |
|---|---|
| `PatternProperty.ForMatching(pattern, property, options)` | Strings the pattern matches (`GenerateMatching`). |
| `PatternProperty.ForNonMatching(pattern, property, options)` | Strings it does not match, mostly near misses one edit from a valid sample (`GenerateNonMatching`). |

Use `ForNonMatching` to check that a hand-written validator rejects everything the pattern rejects:

```csharp
Pattern zip = Pattern.FromRegex("^[0-9]{5}$");

PatternProperty.ForNonMatching(zip, s => !(s.Length == 5 && s.All(char.IsAsciiDigit)))
    .ThrowIfFailed();   // passes: 100 non-matching samples, all rejected
```

Membership follows `Regex.IsMatch` with the pattern's options, exactly like the sample generator. If a
pattern cannot produce any samples of the requested kind (for example `ForNonMatching` on `.*`, which
matches everything), an `InvalidOperationException` is thrown instead of reporting a vacuous pass.

## Exceptions are failures

A property that throws fails, and the exception is kept:

```csharp
PropertyResult result = PatternProperty.ForMatching(
    Pattern.FromRegex("^[a-z]{1,10}$"),
    s => s.Contains('q') ? throw new InvalidOperationException("no q") : true);

result.Counterexample;      // "q"
result.Exception?.Message;  // "no q"
```

## Options

| Option | Default | Meaning |
|---|---|---|
| `Samples` | 100 | Generated strings to check (at least 1). Fewer are used when the pattern has fewer distinct strings. |
| `Seed` | 0 | Generation seed. Same seed, same samples, same shrunk counterexample. |
| `MaxShrinkSteps` | 1000 | Maximum candidate strings evaluated while shrinking. 0 disables shrinking. |

## How shrinking works

Greedy delta debugging, deterministic for a given input:

1. Shorter samples that were generated but not yet tested are tried first, shortest first.
2. Chunks are removed, from half the string down to single characters.
3. Characters are replaced with simpler ones: first the first member of the character's class, then
   `a`, `0`, space and `A`. A replacement is kept only when it is simpler, in this order: `a`, `0`,
   space, `A`, other lowercase letters, other digits, other uppercase letters, everything else.
4. Steps 2 and 3 repeat until nothing changes or `MaxShrinkSteps` is reached.

A candidate is kept only when it is still on the same side of the pattern (still matching for
`ForMatching`, still non-matching for `ForNonMatching`) and still fails the property. That is why the
IPv4 property below shrinks to `"0.0.0.٩"`: every octet but one becomes `0`, and the Arabic-Indic nine
stays because replacing it with `0` would make the property pass.

```csharp
PatternProperty.ForMatching(CommonPatterns.IPv4(), ip => IPAddress.TryParse(ip, out _)).Counterexample;
// "0.0.0.٩"
```

`ShrinkSteps` reports how many candidates were evaluated.
