---
id: learning-from-examples
title: "Learning Patterns from Examples"
description: "Infer a readable, anchored pattern from strings it must match and strings it must reject with Pattern.Learn."
---

`Pattern.Learn` writes a first draft of a pattern for you. You give it strings the pattern must match and, optionally, strings it must reject. It returns an ordinary `Pattern`, built with the fluent API, that matches every match example in full and rejects every reject example.

Use it when you have sample data but no regex yet: invoice numbers, SKUs, version strings, log prefixes, codes from a legacy system. Turn the result into fluent C# with `ToFluentCode()`, paste it into your project, and refine it there.

There are two overloads. Both live on `Pattern` in the `Masterly.FluentRegex` namespace and work on every target framework of the package (`netstandard2.1`, `net8.0` and `net10.0`).

| Overload | Use it when |
|----------|-------------|
| `Pattern.Learn(params string[] match)` | You only have examples to match. |
| `Pattern.Learn(IEnumerable<string> match, IEnumerable<string>? reject = null)` | You also have examples the pattern must not match, or your examples are in a list or set. |

There are no other options. The learner's behavior depends only on the two sets of examples, so the way to steer it is to choose better examples.

:::tip

The result is a starting point, not a specification. It generalizes only as far as your examples show. Read it, test it against data you did not learn from, and edit it.

:::

## Learning from examples

Pass the strings the pattern must match. Each one must be matched in full.

```csharp
using System;
using Masterly.FluentRegex;

Pattern version = Pattern.Learn("v1.2.3", "v10.20.30");

Console.WriteLine(version);         // prints: ^v\d{1,2}\.\d{1,2}\.\d{1,2}$

version.IsMatch("v4.0.12");         // true
version.IsMatch("v4.0");            // false
version.IsMatch("v123.0.0");        // false
```

- Text that every example shares stays literal: the `v` and the dots.
- Text that differs becomes the narrowest class that covers all examples, here `\d`.
- The count is exact when every example has the same length at that position, otherwise a `{min,max}` range taken from the examples. Here `1` and `10` give `\d{1,2}`.
- The result is always anchored with `^` and `$`. See [Anchors and trailing newlines](#anchors-and-trailing-newlines).

A few more results, to show the shape of the output:

```csharp
Pattern.Learn("a1", "b2");                 // ^[a-z]\d$
Pattern.Learn("cat", "dog", "bird");       // ^[a-z]{3,4}$
Pattern.Learn("2024-01-02", "2025/11/30"); // ^\d{4}[\-/]\d{2}[\-/]\d{2}$
Pattern.Learn("hello");                    // ^hello$
Pattern.Learn("cat", "cat", "dog");        // ^[a-z]{3}$
Pattern.Learn("x-1", "abc", "y-22");       // ^(?:abc|[a-z]-\d{1,2})$
```

- One example gives that exact text. There is nothing to generalize from.
- Duplicates are ignored. `"cat", "cat", "dog"` learns from two examples.
- Examples with different shapes become alternatives in a non-capturing group. See [When the examples have different shapes](#when-the-examples-have-different-shapes).
- The result does not depend on the order of the examples. `Pattern.Learn("y-22", "x-1", "abc")` gives the same pattern as the last line.

The `IEnumerable<string>` overload takes any sequence, such as a list loaded from a file or a database:

```csharp
using System;
using System.Collections.Generic;
using Masterly.FluentRegex;

// both lists come from your application, for example a test fixture
List<string> skus = new List<string> { "SKU-001", "SKU-042", "SKU-317" };
HashSet<string> notSkus = new HashSet<string> { "SKU-1", "sku-001" };

Pattern sku = Pattern.Learn(skus, notSkus);

Console.WriteLine(sku); // prints: ^SKU-\d{3}$
```

- Each sequence is read once. Its items are copied, duplicates removed and sorted in ordinal order before learning starts.
- Passing `null` for `reject`, or leaving it out, means there are no reject examples.

## Steering with reject examples

Match examples alone only tell the learner what is allowed. Reject examples tell it what is not. Use them to keep the pattern from accepting values you know are wrong.

```csharp
Pattern invoice = Pattern.Learn(
    match: new[] { "INV-2024-0001", "INV-2025-0420" },
    reject: new[] { "INV-24-1", "inv-2024-0001" });

Console.WriteLine(invoice);          // prints: ^INV-\d{4}-\d{4}$

invoice.IsMatch("INV-2030-9999");    // true
invoice.IsMatch("INV-2030-99999");   // false
invoice.IsMatch("XINV-2030-9999");   // false
```

- The learner starts with the most general pattern the match examples allow. When that pattern already rejects every reject example, it is returned unchanged. That is the case here: `^INV-\d{4}-\d{4}$` is what you get without the rejects too.
- When a reject example is still accepted, the learner makes one position more specific at a time until every reject example is rejected. See [How reject examples narrow the pattern](#how-reject-examples-narrow-the-pattern).
- Every candidate is checked with the real .NET regex engine, not with an approximation.

Reject examples that the general pattern would accept change the result:

```csharp
Pattern.Learn(new[] { "2024", "2025" }, new[] { "2026" });
// ^202[45]$

Pattern.Learn(new[] { "10:30 AM", "9:05 PM" });
// ^\d{1,2}:\d{2} [A-Z]{2}$
Pattern.Learn(new[] { "10:30 AM", "9:05 PM" }, new[] { "10:30 XM" });
// ^\d{1,2}:\d{2} [AP]M$

Pattern.Learn(new[] { "INV-2024-0001", "INV-2025-0420" }, new[] { "INV-2024-9999" });
// ^INV-\d{4}-0\d{3}$

Pattern.Learn(new[] { "cat", "dog", "bird" }, new[] { "cow" });
// ^(?:bird|cat|dog)$
```

- Only the position that needs it is narrowed. In the time example, the hours and minutes stay `\d`. Only the `AM`/`PM` position becomes `[AP]M`.
- A narrowed position keeps literal characters where all examples agree. In the invoice example, both counters start with `0`, so the last group becomes `0\d{3}`.
- When nothing narrower works, a position lists the exact texts it saw, as in `(?:bird|cat|dog)`.

:::tip

Good reject examples are near misses: the wrong case, one digit too few, a neighboring year, a separator you do not accept. A reject that is wildly different from the match examples, such as `"hello"` for an invoice number, is usually rejected already and changes nothing.

:::

## Turning the result into code

The learned value is a normal `Pattern`. Everything else in the library works on it: matching, [optimizing](./optimizer.md), [explaining](./validation-and-debugging.md), [comparing](./pattern-algebra.md) and [exporting](./export.md). The most useful next step is usually `ToFluentCode()`, which prints C# that rebuilds the pattern:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern order = Pattern.Learn(
    match: new[] { "ORD-1042-A", "ORD-2210-B", "ORD-9001-Z" },
    reject: new[] { "ord-1042-a" });

Console.WriteLine(order);                // prints: ^ORD-\d{4}-[A-Z]$
Console.WriteLine(order.ToFluentCode());
// prints:
// Pattern.With
//     .StartOfLine
//     .Literal("ORD-")
//     .Digit.Repeat.Times(4)
//     .Literal("-")
//     .UppercaseLetter
//     .EndOfLine

Regex regex = order.ToRegex();
regex.IsMatch("ORD-5555-Q");             // true
```

- `ToFluentCode(indentSize)` takes the number of spaces per indentation level. The default is 4. `ToFluentCode(2)` gives the same code indented by two spaces.
- `ToRegex()` returns a `System.Text.RegularExpressions.Regex` for code that expects one.
- Learned patterns use only constructs that have a fluent equivalent: `Literal`, `Digit`, `UppercaseLetter`, `LowercaseLetter`, `Letter`, `Word`, `Whitespace`, `AnyOf`, `OneOf`, `Repeat.Times`, `StartOfLine`, `EndOfLine` and `EndOfInput`. The generated code is always plain fluent calls. For alternations, see the sample in [When the examples have different shapes](#when-the-examples-have-different-shapes).
- To go the other way, from an existing regex string to fluent code, use `Pattern.FromRegex(...).ToFluentCode()`. See [Regex to Fluent Code](./regex-to-fluent-code.md).

### Checking the result against data you did not learn from

The learned pattern fits its examples by construction, so testing it against the same examples proves nothing. Keep some samples back and test against those:

```csharp
// order is the pattern learned above; held-out samples come from your data, not from the training lists
string[] heldOut = { "ORD-5555-Q", "ORD-123-A", "ORD-1042-AB" };
foreach (string sample in heldOut)
{
    Console.WriteLine($"{sample}: {order.IsMatch(sample)}");
}
// prints:
// ORD-5555-Q: True
// ORD-123-A: False
// ORD-1042-AB: False
```

To check that the learned pattern says exactly what you meant, compare it with a pattern you write by hand. [Pattern algebra](./pattern-algebra.md) proves equivalence over every possible input:

```csharp
Pattern expected = Pattern.FromRegex(@"^ORD-[0-9]{4}-[A-Z]$");

order.IsEquivalentTo(expected);                    // false: \d also matches non-ASCII digits
order.WithAsciiDigits().IsEquivalentTo(expected);  // true
```

To lock the learned pattern into your test suite, use [property testing](./property-testing.md) or [regex coverage](./regex-coverage.md).

## How examples are generalized

The learner works in four steps: it cuts each example into tokens, lines the examples up token by token, picks a pattern for each position, and then narrows positions until the reject examples are rejected. This section covers the first three steps. The next section covers the narrowing.

### Cutting examples into tokens

Each example is cut into runs of similar characters. The learner tries two granularities:

| Granularity | Runs | Example: `"AB12-x"` |
|-------------|------|---------------------|
| Fine (tried first) | A run of ASCII digits, a run of ASCII letters, a run of whitespace. Every other character is its own token. | `AB`, `12`, `-`, `x` |
| Coarse (tried second) | A run of word characters (`A-Z`, `a-z`, `0-9`, `_`), a run of whitespace. Every other character is its own token. | `AB12`, `-`, `x` |

- Letters and digits are ASCII only. A letter such as `é` counts as punctuation: it is a token of its own.
- At fine granularity, `_` counts as punctuation. At coarse granularity, it is part of a word run.
- Characters are UTF-16 code units. An emoji outside the Basic Multilingual Plane is two tokens, one per surrogate. When the examples agree on the emoji it stays literal (`Learn("x😀", "y😀")` gives `^[a-z]😀$`). When they differ, the set holds single surrogates, so `ToString()` prints unpaired code units.

### Lining the examples up

Examples line up when their tokens have the same shape: the same number of tokens, and at each position the same kind of token (a word run, a whitespace run, or a single punctuation character). A word run lines up with any other word run, so `"a1"` and `"1a"` line up: both are two word runs at fine granularity.

When all examples line up, each position becomes one element of the pattern:

| What the examples have at the position | Element | Example |
|----------------------------------------|---------|---------|
| The same text in every example | That text, literally | `Learn("2024-01-02", "2024-03-04")` gives `^2024-\d{2}-\d{2}$` |
| Different punctuation characters | A set of the characters seen | `Learn("a.b", "a,b", "a;b")` gives `^a[,\.;]b$` |
| Different runs of letters, digits or whitespace | The narrowest class that covers them, with a count | `Learn("12", "345")` gives `^\d{2,3}$` |

The classes are tried from the most specific to the most general. The first one that contains every character seen at the position wins:

| Class | Fluent element | Regex | Chosen when the position holds | Example |
|-------|----------------|-------|--------------------------------|---------|
| Digit | `Digit` | `\d` | Only ASCII digits | `Learn("12", "345")` gives `^\d{2,3}$` |
| Uppercase letter | `UppercaseLetter` | `[A-Z]` | Only `A-Z` | `Learn("HELLO", "WORLD")` gives `^[A-Z]{5}$` |
| Lowercase letter | `LowercaseLetter` | `[a-z]` | Only `a-z` | `Learn("hello", "world")` gives `^[a-z]{5}$` |
| Letter | `Letter` | `[a-zA-Z]` | ASCII letters of both cases | `Learn("Hello", "World")` gives `^[a-zA-Z]{5}$` |
| Word character | `Word` | `\w` | ASCII letters, digits and `_` mixed | `Learn("a1", "1a")` gives `^\w{2}$` |
| Whitespace | `Whitespace` | `\s` | Only whitespace | `Learn("hello world", "goodbye  moon")` gives `^[a-z]{5,7}\s{1,2}[a-z]{4,5}$` |

- The count comes from the shortest and the longest text at the position: an exact count such as `{4}` when they are equal, otherwise `{min,max}`. A count of 1 is not written.
- Adjacent elements are merged where possible, so the pattern stays short. Two adjacent literals become one literal, and two adjacent copies of the same class, or of the same set, add their counts. `Learn("a..b", "a,,b")` gives `^a[,\.]{2}b$`.
- Punctuation positions never become a class. Three examples with `.`, `,` and `;` give the set `[,\.;]`, not "any punctuation".

### When the examples have different shapes

When the fine tokens do not line up, the learner tries the coarse tokens. Treating letters and digits as one run often lines the examples up:

```csharp
Pattern.Learn("AB12", "A1B2"); // ^\w{4}$
Pattern.Learn("a1b", "2cd");   // ^\w{3}$
```

When neither granularity lines all examples up, the learner groups the examples by shape (coarse shape first, then fine shape), learns each group on its own with the same steps, and joins the groups in a non-capturing alternation:

```csharp
Pattern.Learn("abc", "12-34");             // ^(?:12-34|abc)$
Pattern.Learn("x-1", "abc", "y-22");       // ^(?:abc|[a-z]-\d{1,2})$
Pattern.Learn("ab", "ab-cd", "ab-cd-ef");  // ^(?:ab|ab-cd|ab-cd-ef)$

Console.WriteLine(Pattern.Learn("x-1", "abc", "y-22").ToFluentCode());
// prints:
// Pattern.With
//     .StartOfLine
//     .OneOf(
//         Pattern.With.Literal("abc"),
//         Pattern.With.LowercaseLetter.Literal("-").Digit.Repeat.Times(1, 2))
//     .EndOfLine
```

- A group with one example stays that exact text. That is why `"abc"` and `"12-34"` are both literal: each is alone in its group.
- `"x-1"` and `"y-22"` have the same shape, so they form a group and generalize to `[a-z]-\d{1,2}`.
- The learner does not discover repetition. `"ab"`, `"ab-cd"` and `"ab-cd-ef"` have three different shapes, so you get three alternatives, not `ab(?:-[a-z]{2})*`. Write lists of variable length by hand. See [Quantifiers](./quantifiers.md).

## How reject examples narrow the pattern

Every position has a ladder of options, from the most general to the most specific. The learner starts every position on the first rung. While a reject example is still accepted, it moves one position one rung up.

### The specialization ladder

For a position made of letter, digit or whitespace runs, the rungs are:

| Rung | What it matches | `"SKU-001"`, `"SKU-042"`, `"SKU-317"` at the digits | Available when |
|------|-----------------|-------------------------------------------------------|----------------|
| 1. Class with a count | The covering class | `\d{3}` | Always |
| 2. Class per character | A literal where all examples agree on that character, the class elsewhere | `\d{3}` (same as rung 1, so it is skipped) | All texts at the position have the same length |
| 3. Set per character | A literal where all examples agree, a set of the characters seen elsewhere | `[03][014][127]` | All texts at the position have the same length |
| 4. Exact texts | A choice of the texts seen | `(?:001\|042\|317)` | Always |

- A rung that would render the same as a lower rung is left out.
- Positions where every example has the same text, and punctuation positions, have one rung only. They never move.
- When the texts at a position have different lengths, only rungs 1 and 4 exist. `Learn(new[] { "12", "345" }, new[] { "99" })` gives `^(?:12|345)$`.

The same reject can need different rungs depending on the match examples:

```csharp
Pattern.Learn(new[] { "SKU-001", "SKU-042", "SKU-317" });
// ^SKU-\d{3}$
Pattern.Learn(new[] { "SKU-001", "SKU-042", "SKU-317" }, new[] { "SKU-000" });
// ^SKU-[03][014][127]$
Pattern.Learn(new[] { "SKU-001", "SKU-042", "SKU-317", "SKU-580", "SKU-999" }, new[] { "SKU-000" });
// ^SKU-(?:001|042|317|580|999)$
```

- With three examples, the sets on rung 3 already exclude `000`, so the learner stops there.
- With five examples, every digit column contains a `0`, so the sets still accept `000`. Only the exact texts on rung 4 reject it.

:::warning

A single reject can make the pattern much narrower than you meant. `^SKU-[03][014][127]$` rejects `SKU-999`, and the five-example result accepts nothing but those five SKUs. The learner cannot express "any three digits except `000`". When you see sets or exact choices in the result, decide whether you really want them, or write the exclusion yourself with a negative lookahead. See [Assertions](./assertions.md).

:::

### Which position moves first

At each step, the learner tries moving each position one rung up and keeps the move that rejects the most of the reject examples that are still accepted. Then it re-checks and repeats.

```csharp
Pattern.Learn(new[] { "a1", "b2" }, new[] { "c3" });
// ^[ab]\d$

Pattern.Learn(new[] { "ab12", "cd34" }, new[] { "ab14" });
// ^[a-z]{2}(?:12|34)$
```

- **Ties go to the leftmost position.** For `"c3"`, narrowing the letter to `[ab]` or the digit to `[12]` would both work. The letter comes first, so it moves.
- **The search always makes progress.** When no single move rejects anything, the leftmost position that can still move moves anyway. The search ends at the latest when every position lists its exact texts.
- **Unneeded moves are undone.** When every reject example is rejected, the learner walks the positions from left to right and moves each one back down as far as it can while every reject example stays rejected. In the second sample, the letters were narrowed only to make progress. Once the digits list their exact texts, the letters go back to `[a-z]{2}`.

### When positions cannot separate the examples

Positions are narrowed one at a time, and each position only knows what it saw. A reject that combines parts of different match examples cannot be rejected position by position:

```csharp
Pattern.Learn(new[] { "A-1", "B-2" }, new[] { "A-2" });
// ^(?:A-1|B-2)$

Pattern.Learn(new[] { "GB", "US", "DE" }, new[] { "FR" });
// ^[DGU][BES]$
```

- In the first sample, the first position has seen `A` and the last has seen `2`. Even with exact texts at both, `A-2` is accepted. The learner then gives up on generalizing this group and falls back to the exact match examples.
- In the second sample, `FR` is rejected, but the result also accepts `GE`, `UB` and `DS`, which are not country codes from the examples. Positions are learned independently, so combinations you never listed get through. Add those combinations as reject examples, or list the allowed codes yourself with `OneOf`.

## Anchors and trailing newlines

The learned pattern always starts with `^` (`StartOfLine`). It usually ends with `$` (`EndOfLine`). In .NET, `$` also matches before a final `\n`, so a learned pattern accepts a trailing newline:

```csharp
Pattern code = Pattern.Learn("abc");
code.IsMatch("abc\n");                                       // true

Pattern strict = Pattern.Learn(new[] { "abc" }, new[] { "abc\n" });
Console.WriteLine(strict);                                   // prints: ^abc\z
strict.IsMatch("abc\n");                                     // false
```

- When any reject example ends with `\n`, the learner ends the pattern with `\z` (`EndOfInput`) instead of `$`, so the trailing newline is rejected. Otherwise it keeps the more familiar `$`.
- An empty reject example does not trigger `\z`. `Learn(new[] { "a" }, new[] { string.Empty })` gives `^a$`.
- An empty match example is allowed. `Learn(string.Empty)` gives `^$`.

:::note

Because of the anchors, a learned pattern matches whole inputs only. `Pattern.Learn("ORD-1042-A").IsMatch("Order ORD-1042-A shipped")` is `false`. To find the value inside longer text, copy the output of `ToFluentCode()` and remove `.StartOfLine` and `.EndOfLine`, or use word boundaries instead. See [Assertions](./assertions.md).

:::

## When learning fails

A pattern can always fall back to listing the exact match examples, so learning succeeds for any input except a few invalid ones:

| Situation | Exception | `ParamName` | Message |
|-----------|-----------|-------------|---------|
| `match` is `null` | `ArgumentNullException` | `match` | `Value cannot be null.` |
| An example in `match` is `null` | `ArgumentNullException` | `match` | `The examples cannot contain null.` |
| An example in `reject` is `null` | `ArgumentNullException` | `reject` | `The examples cannot contain null.` |
| `match` is empty, including `Pattern.Learn()` with no arguments | `ArgumentException` | `match` | `At least one example to match is required.` |
| A reject example is also a match example | `ArgumentException` | `reject` | `The example "def" is listed both as a match and as a reject, so no pattern can separate it.` |

```csharp
try
{
    Pattern.Learn(new[] { "abc", "def" }, new[] { "def" });
}
catch (ArgumentException exception)
{
    Console.WriteLine(exception.ParamName); // prints: reject
    Console.WriteLine(exception.Message);
    // prints: The example "def" is listed both as a match and as a reject, so no pattern can separate it. (Parameter 'reject')
}
```

- The Message column leaves out the suffix that .NET adds. The full `Message` ends with `(Parameter 'match')` or `(Parameter 'reject')`, as the sample shows.
- The contradiction check is case-sensitive and ordinal. `"abc"` as a match and `"ABC"` as a reject is not a contradiction.
- Only the first contradicting example is named, in ordinal order.
- Before returning, the learner checks the final pattern once more against every example. If the pattern missed a match example it would throw `InvalidOperationException`, and if it accepted a reject example it would throw `ArgumentException` with `ParamName` `reject`. These checks guard against a bug in the learner. You should never see them, but they mean a wrong pattern is never returned silently.

## Using the command-line tool

The [command-line tool](./command-line-tool.md) runs the same learner. It prints the regex, a blank line, and the fluent code:

```bash
fluentregex learn INV-2024-0001 INV-2025-0420 --not INV-24-1 inv-2024-0001
```

```text
^INV-\d{4}-\d{4}$

Pattern.With
    .StartOfLine
    .Literal("INV-")
    .Digit.Repeat.Times(4)
    .Literal("-")
    .Digit.Repeat.Times(4)
    .EndOfLine
```

- Every argument after `--not` is a reject example.
- `--from-file <path>` reads one example per line and adds the lines to the current list: to the match examples before `--not`, to the reject examples after it. Empty lines are skipped, so a file cannot supply an empty example. Lines that hold only spaces are kept as examples.
- Errors, such as no match examples, a missing file or an example listed on both sides, are printed to standard error with an `error:` prefix, and the exit code is 2.

```bash
fluentregex learn --from-file skus.txt --not --from-file bad-skus.txt
```

## Limits and pitfalls

- **It generalizes only as far as the examples show.** `Pattern.Learn("cat", "dog")` gives `^[a-z]{3}$`, which also accepts `"xyz"`. Add reject examples to steer it, and review the result.
- **Text shared by all examples stays literal.** `Pattern.Learn("2024-01-02", "2024-03-04")` keeps `2024`, so it rejects `2025-01-02`. Give examples that vary wherever your real data varies.
- **Classes are ASCII.** Letters outside `A-Z` and `a-z` are treated like punctuation: kept literally when all examples agree, or put in a set when they differ. `Pattern.Learn("José", "Rosé")` gives `^[a-zA-Z]{3}é$`, `Pattern.Learn("café", "cafè")` gives `^caf[èé]$`, and `Pattern.Learn("José", "Jesús")` gives `^(?:Jesús|José)$`. For patterns over other scripts, see [Unicode](./unicode.md).
- **`\d` and `\w` still match Unicode in .NET.** The learner decides with ASCII rules, but the `\d` it writes accepts every Unicode decimal digit, and `\w` accepts every Unicode letter. Call `WithAsciiDigits()` when the format is defined over `0-9`:

  ```csharp
  Pattern year = Pattern.Learn("2024", "1999"); // ^\d{4}$
  year.IsMatch("٢٠٢٦");                         // true

  Pattern asciiYear = year.WithAsciiDigits();   // ^[0-9]{4}$
  asciiYear.IsMatch("٢٠٢٦");                    // false
  ```

- **Positions are learned independently.** A reject that combines parts of different match examples forces exact texts, and combinations you never saw can get through. See [When positions cannot separate the examples](#when-positions-cannot-separate-the-examples).
- **No repetition, no optional parts.** The learner never writes `*`, `+` or `?`. Examples with optional parts become separate alternatives. Groups can still nest: when an alternative lists exact texts at one position, you get a choice inside the alternation. `Learn(new[] { "ab-1", "cd-22", "zzz" }, new[] { "ad-1" })` gives `^(?:(?:ab|cd)-\d{1,2}|zzz)$`.
- **Case matters.** The learner never uses `RegexOptions.IgnoreCase`. `"ABC"` and `"abc"` are different examples, and a lowercase reject is a useful way to rule out the wrong case.
- **Deterministic.** Duplicates are ignored and examples are processed in ordinal order, so the same set of examples always gives the same pattern, whatever their order.
- **Sized for examples, not data sets.** Every candidate pattern is compiled and run against the reject examples, and every narrowing step tries each position. That is fast for tens or hundreds of examples. Do not feed it a whole table.
- **Bounded output.** A learned pattern uses only exact counts and `{min,max}` ranges taken from the examples. It never has `*`, `+` or a quantifier around a group, so the [ReDoS analyzer](./redos-safety.md) reports `Risk: None` for the samples on this page. Run `AnalyzeSafety()` on your own result like on any other pattern.

## Member reference

| Member | Returns | Notes |
|--------|---------|-------|
| `Pattern.Learn(params string[] match)` | `Pattern` | Learns from match examples only. Throws `ArgumentNullException` for a `null` array or example, `ArgumentException` for an empty array. |
| `Pattern.Learn(IEnumerable<string> match, IEnumerable<string>? reject = null)` | `Pattern` | Learns from match and reject examples. `reject` may be `null`. Also throws `ArgumentException` (`ParamName` `reject`) when an example is on both sides. |
| `pattern.ToFluentCode(int indentSize = 4)` | `string` | C# that rebuilds the learned pattern with the fluent API. |
| `pattern.ToRegex()` | `Regex` | A .NET `Regex` for the learned pattern. |
| `pattern.WithAsciiDigits()` | `Pattern` | Rewrites `\d` as `[0-9]`. |
| `pattern.IsEquivalentTo(other)` | `bool` | Proves the learned pattern matches exactly the same strings as another one. See [Pattern Algebra](./pattern-algebra.md). |
