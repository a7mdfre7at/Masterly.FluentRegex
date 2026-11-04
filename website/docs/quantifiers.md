---
id: quantifiers
title: "Quantifiers"
description: "Repeat an element a fixed, bounded or open number of times, and choose greedy, lazy or possessive matching."
---

A quantifier says how many times an element may repeat: one or more digits, an optional sign, exactly four hex characters, at least two words. In Masterly.FluentRegex you add one by writing `.Repeat` after the element and then picking a count. Follow the count with `.Lazy` or `.Possessive` when you need to change how the engine backtracks.

Use this page when you need to:

- repeat a character, class, group or alternation,
- limit a repetition to an exact count or a range,
- choose between greedy, lazy and possessive matching,
- stop a repetition from causing catastrophic backtracking.

All members on this page live in the `Masterly.FluentRegex` namespace and work on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10. The only exception is `UseNonBacktracking()`, mentioned in [Quantifiers and catastrophic backtracking](#quantifiers-and-catastrophic-backtracking), which needs .NET 8 or later.

| Member | Regex | Matches the element |
|--------|-------|---------------------|
| `.Repeat.OneOrMore` | `+` | One or more times. |
| `.Repeat.ZeroOrMore` | `*` | Zero or more times. |
| `.Repeat.Optional` | `?` | Zero times or once. |
| `.Repeat.Times(n)` | `{n}` | Exactly `n` times. |
| `.Repeat.Times(min, max)` | `{min,max}` | Between `min` and `max` times, both included. |
| `.Repeat.AtLeast(n)` | `{n,}` | `n` or more times. |
| `.Repeat.AtMost(n)` | `{0,n}` | Up to `n` times, including zero. |
| `.Lazy` | adds `?` | As few times as possible (non-greedy). |
| `.Possessive` | `(?>...)` | As many times as possible, and never gives any back. |

`Repeat` is a property of `Pattern` that returns a small `Repeat` builder. Every member of that builder returns a new `Pattern`, so you keep chaining after it. `Lazy` and `Possessive` are properties of `Pattern` itself.

The `Repeat` class also has a public constructor, `new Repeat(pattern)`, which does the same as `pattern.Repeat`. It throws `ArgumentNullException` when `pattern` is `null`. You rarely need it; the property reads better in a chain.

## Repeating an element

### One or more, zero or more, optional

These three cover most everyday repetitions. Write `.Repeat` right after the element you want to repeat.

```csharp
using System;
using Masterly.FluentRegex;

Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;
Pattern signed = Pattern.With.Literal("+").Repeat.Optional.Digit.Repeat.OneOrMore;
Pattern spaces = Pattern.With.Whitespace.Repeat.ZeroOrMore;

Console.WriteLine(digits);                              // \d+
Console.WriteLine(signed);                              // \+?\d+
Console.WriteLine(spaces);                              // \s*

Console.WriteLine(signed.Match("call +42 now").Value);  // +42
Console.WriteLine(signed.Match("call 42 now").Value);   // 42
```

- `OneOrMore` requires at least one occurrence. `ZeroOrMore` and `Optional` also accept none.
- The `+` in `Literal("+")` is escaped for you (`\+`). Only the `Repeat` call adds a quantifier.
- Every call returns a new pattern. `digits` is never changed by later calls, so you can reuse it as a building block (see [Core Concepts](./core-concepts.md)).

Anchor the pattern when the whole input must follow the count. Without anchors, a pattern that can match zero characters succeeds on any input.

```csharp
Pattern oneOrMore = Pattern.With.StartOfLine.Literal("a").Repeat.OneOrMore.EndOfLine;
Pattern zeroOrMore = Pattern.With.StartOfLine.Literal("a").Repeat.ZeroOrMore.EndOfLine;
Pattern optional = Pattern.With.StartOfLine.Literal("a").Repeat.Optional.EndOfLine;

Console.WriteLine(oneOrMore.IsMatch(""));    // False
Console.WriteLine(zeroOrMore.IsMatch(""));   // True
Console.WriteLine(optional.IsMatch("aa"));   // False
```

### Exact counts and ranges

Use the bounded quantifiers when the count is known, such as a ten-digit phone number or a five-digit ZIP code.

```csharp
Pattern phone = Pattern.With.Digit.Repeat.Times(10);
Console.WriteLine(phone);                          // \d{10}

Pattern zip = Pattern.With.StartOfLine
    .Digit.Repeat.Times(5)
    .Group(Pattern.With.Literal("-").Digit.Repeat.Times(4)).Repeat.Optional
    .EndOfLine;
Console.WriteLine(zip);                            // ^\d{5}(-\d{4})?$
Console.WriteLine(zip.IsMatch("12345"));           // True
Console.WriteLine(zip.IsMatch("12345-6789"));      // True
Console.WriteLine(zip.IsMatch("12345-67"));        // False

Pattern password = Pattern.With.StartOfLine.Word.Repeat.Times(8, 20).EndOfLine;
Console.WriteLine(password);                       // ^\w{8,20}$
Console.WriteLine(password.IsMatch("abc123"));     // False
Console.WriteLine(password.IsMatch("abc12345"));   // True

Pattern threeWords = Pattern.With
    .Word.Repeat.OneOrMore
    .NonCapturingGroup(Pattern.With.Whitespace.Repeat.OneOrMore.Word.Repeat.OneOrMore)
    .Repeat.AtLeast(2);
Console.WriteLine(threeWords);                     // \w+(?:\s+\w+){2,}
Console.WriteLine(threeWords.IsMatch("hello world"));            // False
Console.WriteLine(threeWords.Match("say hello world").Value);    // say hello world

Pattern upToThree = Pattern.With.Literal("a").Repeat.AtMost(3);
Console.WriteLine(upToThree);                      // a{0,3}
Console.WriteLine(upToThree.Match("aaaaa").Value); // aaa
```

- `Times(n)` and `Times(min, max)` are overloads of one method. Both bounds of `Times(min, max)` are inclusive.
- `AtLeast(2)` on the group means "two or more extra words", so `threeWords` needs three words in total.
- `AtMost(3)` stops after three characters even when more follow. It does not fail on longer input. Add anchors if longer input must be rejected.
- Counts are written with the invariant culture, so the output is the same on every machine.

| Method | Parameters | Throws `ArgumentOutOfRangeException` when |
|--------|------------|-------------------------------------------|
| `Times(int exactly)` | `exactly`: the exact count. | `exactly` is negative. |
| `Times(int min, int max)` | `min`, `max`: inclusive bounds. | `min` is negative, or `max` is less than `min`. |
| `AtLeast(int x)` | `x`: the minimum count. | `x` is negative. |
| `AtMost(int x)` | `x`: the maximum count. | `x` is negative. |

### Why AtMost writes `{0,n}`

Some regex flavors accept `{,n}` as "at most n". .NET does not. It reads `{,n}` as literal text, so `a{,3}` matches the five characters `a{,3}`, not up to three `a` characters. `AtMost` always writes the lower bound for you.

```csharp
using System.Text.RegularExpressions;

Pattern upToThree = Pattern.With.Literal("a").Repeat.AtMost(3);
Match match = upToThree.Match("bbb");
Console.WriteLine(match.Success);                          // True
Console.WriteLine(match.Value.Length);                     // 0

Console.WriteLine(new Regex("^a{,3}$").IsMatch("aaa"));    // False
Console.WriteLine(new Regex("a{,3}").IsMatch("a{,3}"));    // True
```

:::note

Because `AtMost` allows zero, `upToThree` succeeds with an empty match on input that has no `a` at all. The same is true for `ZeroOrMore`, `Optional` and `Times(0, n)`. Check `match.Value.Length`, or anchor the pattern, when an empty match is not acceptable.

:::

### Zero and equal bounds

A count of zero is allowed. `Times(0)` matches the element zero times, which is the same as leaving it out. `Times(n, n)` is the same as `Times(n)`, written in the longer form.

```csharp
Pattern never = Pattern.With.StartOfLine.Literal("a").Repeat.Times(0).EndOfLine;
Console.WriteLine(never);                // ^a{0}$
Console.WriteLine(never.IsMatch(""));    // True
Console.WriteLine(never.IsMatch("a"));   // False
Console.WriteLine(Pattern.With.Digit.Repeat.Times(3, 3));   // \d{3,3}
```

The builder writes exactly what you ask for. To turn `{1}`, `{0,1}`, `{0,}` and `{1,}` into their short forms, run the [optimizer](./optimizer.md) (see [Reading and simplifying quantifiers](#reading-and-simplifying-quantifiers)).

## What a quantifier repeats

A quantifier always applies to the **last element** of the pattern. What counts as one element depends on how you added it.

```csharp
Pattern lastLetterOnly = Pattern.With.Literal("ha").Repeat.OneOrMore;
Console.WriteLine(lastLetterOnly);                         // ha+
Console.WriteLine(lastLetterOnly.Match("hahaha").Value);   // ha

Pattern wholeWord = Pattern.With.NonCapturingGroup(Pattern.With.Literal("ha")).Repeat.OneOrMore;
Console.WriteLine(wholeWord);                              // (?:ha)+
Console.WriteLine(wholeWord.Match("hahaha").Value);        // hahaha

Console.WriteLine((Pattern.With.Literal("cat") | Pattern.With.Literal("dog")).Repeat.OneOrMore);  // (?:cat|dog)+
Console.WriteLine(Pattern.With.OneOf("cat", "dog").Repeat.Times(2));                             // (?:cat|dog){2}
Console.WriteLine(Pattern.With.Set(Pattern.With.Range('a', 'f').Digit).Repeat.OneOrMore);        // [a-f\d]+
Console.WriteLine(Pattern.With.RegEx("ha").Repeat.OneOrMore);                                    // ha+
Console.WriteLine(Pattern.With.Literal("x").Then(Pattern.With.Literal("ab")).Repeat.OneOrMore);  // xab+
```

| You added | One element is | Example |
|-----------|----------------|---------|
| `Literal("text")` | The last character only. | `Literal("ha").Repeat.OneOrMore` is `ha+` |
| `Digit`, `Word`, `Letter`, `Anything` and other shorthands | The whole shorthand. | `\d+`, `[a-zA-Z]+`, `.+` |
| `Set(...)`, `NegatedSet(...)`, `Range(...)`, `AnyOf(...)` | The whole character class. | `[a-f\d]+` |
| `Group`, `NonCapturingGroup`, `NamedGroup`, `AtomicGroup` | The whole group. | `(?:ha)+` |
| `OneOf(...)`, `Choice(...)`, the `\|` operator | The whole alternation. | `(?:cat\|dog)+`, or `(cat\|dog)+` for `Choice`, which makes a capturing group |
| `RegEx("raw")` | The raw text, with the quantifier written after it. Regex rules then apply it to the last atom only. | `RegEx("ha").Repeat.OneOrMore` is `ha+` |
| `Then(other)` or `left + right` | The last element of `other` or `right`, as if written inline. | `xab+` |

:::tip

To repeat a word or any sequence, wrap it first: `NonCapturingGroup(Pattern.With.Literal("ha")).Repeat.OneOrMore`. `OneOf` and the `|` operator already produce a group, so their quantifier repeats the whole alternation.

:::

### Repeating a capturing group

When you repeat a capturing group, the group's `Value` holds only the **last** repetition. The earlier ones are in `Captures`. To capture all of the repeated text as one value, put the quantifier inside the group.

```csharp
using System.Linq;
using System.Text.RegularExpressions;

Pattern eachDigit = Pattern.With.Group(Pattern.With.Digit).Repeat.Times(3);
Match match = eachDigit.Match("123");

Console.WriteLine(eachDigit);                     // (\d){3}
Console.WriteLine(match.Groups[1].Value);         // 3
Console.WriteLine(string.Join(",", match.Groups[1].Captures.Select(capture => capture.Value)));  // 1,2,3

Pattern allDigits = Pattern.With.Group(Pattern.With.Digit.Repeat.Times(3));
Console.WriteLine(allDigits);                             // (\d{3})
Console.WriteLine(allDigits.Match("123").Groups[1].Value); // 123
```

More on groups and captures: [Groups and Backreferences](./groups-and-backreferences.md).

### Repeating something that is already repeated

.NET rejects a quantifier written directly after another one: `new Regex(@"\d{2}{3}")` throws `Nested quantifier '{'`. When the last element already has a quantifier, `Repeat` wraps it in a non-capturing group first, so the result is always valid.

```csharp
Pattern pairs = Pattern.With.Digit.Repeat.Times(2).Repeat.Times(3);
Console.WriteLine(pairs);              // (?:\d{2}){3}
Console.WriteLine(pairs.IsValid());    // True
```

`Repeat.Optional` follows the same rule, so it makes a repeated element optional as a whole. A lazy quantifier is written with `.Lazy`:

```csharp
Pattern optional = Pattern.With.Digit.Repeat.OneOrMore.Repeat.Optional;
Console.WriteLine(optional);                        // (?:\d+)?
Console.WriteLine(optional.Match("12345").Value);   // 12345

Pattern version = Pattern.With.StartOfLine.Literal("v").Digit.Repeat.Times(2).Repeat.Optional.EndOfLine;
Console.WriteLine(version);                  // ^v(?:\d{2})?$
Console.WriteLine(version.IsMatch("v"));     // True
Console.WriteLine(version.IsMatch("v12"));   // True

Pattern lazy = Pattern.With.Digit.Repeat.OneOrMore.Lazy;
Console.WriteLine(lazy);                     // \d+?
Console.WriteLine(lazy.Match("12345").Value);   // 1
```

`.Lazy` and `Repeat.Optional` can be combined in either order, and each keeps its meaning:

```csharp
Pattern lazyThenOptional = Pattern.With.Digit.Repeat.AtLeast(2).Lazy.Repeat.Optional;
Console.WriteLine(lazyThenOptional);                       // (?:\d{2,}?)?
Console.WriteLine(lazyThenOptional.Match("12345").Value);  // 12

Pattern optionalThenLazy = Pattern.With.Digit.Repeat.AtLeast(2).Repeat.Optional.Lazy;
Console.WriteLine(optionalThenLazy);                       // (?:\d{2,})??
```

- `lazyThenOptional` is an optional group around a lazy repetition: when it matches, it takes as few digits as it can, two here.
- `optionalThenLazy` is a lazy optional group: it prefers to match nothing, so `Match("12345")` returns an empty match at position 0.

## Greedy, lazy and possessive

Every quantifier has a mode. The mode decides how many repetitions the engine tries first, and whether it may give some back later to let the rest of the pattern match.

| Mode | How you write it | Tries first | Gives repetitions back? | `<` + `.` repeated + `>` on `<a>text<b>` |
|------|------------------|-------------|-------------------------|------------------------------------------|
| Greedy | The default | As many as possible | Yes, one at a time | One match: `<a>text<b>` |
| Lazy | `.Lazy` | As few as possible | Takes more, one at a time | Two matches: `<a>` and `<b>` |
| Possessive | `.Possessive` | As many as possible | Never | No match |

Here are all three on the same input:

```csharp
using System.Linq;

// html comes from your application
string html = "<a>text<b>";

Pattern greedy = Pattern.With.Literal("<").Anything.Repeat.OneOrMore.Literal(">");
Pattern lazy = Pattern.With.Literal("<").Anything.Repeat.OneOrMore.Lazy.Literal(">");
Pattern possessive = Pattern.With.Literal("<").Anything.Repeat.OneOrMore.Possessive.Literal(">");

Console.WriteLine(greedy);       // <.+>
Console.WriteLine(lazy);         // <.+?>
Console.WriteLine(possessive);   // <(?>.+)>

Console.WriteLine(string.Join(" | ", greedy.Matches(html).Select(m => m.Value)));   // <a>text<b>
Console.WriteLine(string.Join(" | ", lazy.Matches(html).Select(m => m.Value)));     // <a> | <b>
Console.WriteLine(possessive.IsMatch(html));                                        // False
```

- **Greedy.** `.+` first takes `a>text<b>`, everything to the end. The final `>` then has nothing left to match, so the engine gives back one character at a time until `>` fits. The result runs from the first `<` to the last `>`.
- **Lazy.** `.+?` first takes one character, `a`. The engine then tries `>`, which fits. The match stops at the first `>`, and the next search finds `<b>`.
- **Possessive.** `(?>.+)` takes everything to the end and never gives any of it back. The final `>` can never match, so there is no match at all.

### Greedy (the default)

Every quantifier is greedy unless you say otherwise. Greedy is right for most patterns, especially anchored ones where the whole input must match. Its cost is backtracking: when the rest of the pattern fails, the engine gives characters back one by one and tries again.

### Lazy

`.Lazy` makes the quantifier match as few times as possible. It works on every quantifier and writes a `?` after it.

```csharp
Console.WriteLine(Pattern.With.Anything.Repeat.ZeroOrMore.Lazy);    // .*?
Console.WriteLine(Pattern.With.Digit.Repeat.Optional.Lazy);         // \d??
Console.WriteLine(Pattern.With.Digit.Repeat.AtLeast(2).Lazy);       // \d{2,}?

Pattern fewDigits = Pattern.With.Digit.Repeat.Times(1, 5).Lazy;
Console.WriteLine(fewDigits);                        // \d{1,5}?
Console.WriteLine(fewDigits.Match("12345").Value);   // 1

Pattern anchored = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.Lazy.EndOfLine;
Console.WriteLine(anchored.Match("12345").Value);    // 12345

Console.WriteLine($"'{Pattern.With.Anything.Repeat.ZeroOrMore.Lazy.Match("abc").Value}'");  // ''
```

- A lazy quantifier with nothing after it takes its minimum: `\d{1,5}?` matches one digit, and `.*?` matches the empty string.
- Lazy changes which match is found, not whether one exists. When an anchor or literal follows, the engine keeps taking more until that part fits, so `^\d+?$` still matches all of `12345`.
- `Times(n).Lazy` is valid (`\d{3}?`) but changes nothing, because an exact count has only one choice.

:::tip

Lazy quantifiers are handy for "up to the next delimiter" matches such as `<.+?>`. A negated set is often clearer and faster: `<[^>]+>` says "anything except `>`" directly and does not need to backtrack. See [Character Classes](./character-classes.md).

:::

### Possessive

`.Possessive` makes the quantifier match as many times as possible and then lock in the result. The engine never backtracks into it. .NET has no possessive syntax (`new Regex(@"\d++")` throws `Nested quantifier '+'`), so the library writes the equivalent atomic group: `(?>\d+)`.

```csharp
Pattern greedy = Pattern.With.Digit.Repeat.OneOrMore.Digit;
Pattern possessive = Pattern.With.Digit.Repeat.OneOrMore.Possessive.Digit;

Console.WriteLine(greedy.Match("12345").Value);   // 12345
Console.WriteLine(possessive);                    // (?>\d+)\d
Console.WriteLine(possessive.IsMatch("12345"));   // False

Console.WriteLine(Pattern.With.Literal("ab").Repeat.OneOrMore.Possessive);     // a(?>b+)
Console.WriteLine(Pattern.With.RegEx(@"x\d").Repeat.ZeroOrMore.Possessive);    // x(?>\d*)
Console.WriteLine(Pattern.With.NonCapturingGroup(Pattern.With.Literal("ab")).Repeat.OneOrMore.Possessive);  // (?>(?:ab)+)
Console.WriteLine(Pattern.With.Digit.Repeat.Times(2, 4).Possessive);           // (?>\d{2,4})
```

- In `greedy`, `\d+` first takes all five digits, then gives one back so the last `\d` can match. In `possessive`, `(?>\d+)` keeps all five, so the last `\d` has nothing left and the match fails.
- Only the quantified element moves into the atomic group. For `Literal("ab")` that is the last character (`a(?>b+)`). For raw text from `RegEx("x\d")`, the library parses the text and wraps only its last atom (`x(?>\d*)`).
- `Possessive` is the same as wrapping the repetition in `AtomicGroup(...)`. Use `AtomicGroup` when the atomic part is more than one quantified element (see [Groups and Backreferences](./groups-and-backreferences.md)).

Use possessive when the repetition can never usefully give characters back. A typical case is a repetition followed by something it cannot match:

```csharp
using System.Linq;

// tag comes from your application
string tag = "<a>text<b>";

Pattern safeTag = Pattern.With.Literal("<")
    .NegatedSet(Pattern.With.Literal(">")).Repeat.OneOrMore.Possessive
    .Literal(">");

Console.WriteLine(safeTag);   // <(?>[^>]+)>
Console.WriteLine(string.Join(" | ", safeTag.Matches(tag).Select(m => m.Value)));   // <a> | <b>
```

`[^>]+` can never take a `>`, so giving characters back could never help the final `>` match. Making it possessive keeps the same matches and lets a failing attempt end at once.

### Combining modes

`Lazy` and `Possessive` change the mode of the quantifier they follow. When you call both, the last call wins. Calling `Repeat` again starts a new quantifier on the wrapped result.

```csharp
Console.WriteLine(Pattern.With.Digit.Repeat.OneOrMore.Lazy.Possessive);   // (?>\d+)
Console.WriteLine(Pattern.With.Digit.Repeat.OneOrMore.Possessive.Lazy);   // \d+?
Console.WriteLine(Pattern.With.Digit.Repeat.OneOrMore.Possessive.Repeat.OneOrMore);  // (?:(?>\d+))+
```

:::note

`Lazy` and `Possessive` must come **directly** after a quantifier. `Pattern.With.Digit.Lazy`, or `.Repeat.OneOrMore.Literal("x").Lazy`, throws `InvalidOperationException`. See [Errors](#errors).

:::

## Quantifiers and catastrophic backtracking

A repetition inside another repetition, such as `(\d+)+`, can split the same text between the inner and outer loop in exponentially many ways. When the match fails, a backtracking engine tries all of them. On a few dozen characters this can take seconds or longer. This is called catastrophic backtracking, and attackers use it for ReDoS (regular expression denial of service).

`Possessive` is the simplest fix: the inner loop keeps what it took, so there is only one way to split the text.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;
using Masterly.FluentRegex.Analysis;

// input comes from your application
string input = new string('1', 28) + "x";

Pattern risky = Pattern.With.StartOfLine
    .Group(Pattern.With.Digit.Repeat.OneOrMore).Repeat.OneOrMore
    .EndOfLine;
SafetyReport riskyReport = risky.AnalyzeSafety();
Console.WriteLine(risky);                      // ^(\d+)+$
Console.WriteLine(riskyReport.Risk);           // High
Console.WriteLine(riskyReport.Issues[0]);      // High NestedQuantifier at 1 '(\d+)+': The repetition '\d+' is nested inside ...

try
{
    risky.WithTimeout(TimeSpan.FromSeconds(1)).IsMatch(input);
}
catch (RegexMatchTimeoutException)
{
    Console.WriteLine("timed out");            // timed out
}

Pattern safe = Pattern.With.StartOfLine
    .Group(Pattern.With.Digit.Repeat.OneOrMore.Possessive).Repeat.OneOrMore
    .EndOfLine;
Console.WriteLine(safe);                        // ^((?>\d+))+$
Console.WriteLine(safe.AnalyzeSafety().Risk);   // None
Console.WriteLine(safe.IsMatch(input));         // False
Console.WriteLine(safe.IsMatch("12345"));       // True
```

- `risky` ran for the whole one-second timeout on 29 characters before it gave up. `safe` returned `False` straight away.
- The library also warns at build time. Compiling the line that builds `risky` reports warning `FRX004`: *ReDoS risk in '(\d+)+' ... Fix: Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers.* See [Compile-Time Checks](./compile-time-checks.md).
- Two adjacent repetitions that can match the same characters, such as `\d+\d+`, are a milder (polynomial) risk. `AnalyzeSafety` reports them as a `Medium` `AdjacentOverlappingQuantifiers` issue. Merge them into one repetition, or make their character sets disjoint.

Other ways to keep repetitions safe:

| Option | What it does | Notes |
|--------|--------------|-------|
| `.Possessive` or `AtomicGroup(...)` | Removes the backtracking choices inside the repetition. | Can change what matches when backtracking was needed. Test the pattern after the change. |
| A narrower class | Makes adjacent repetitions unable to match the same characters, such as `[^>]+` instead of `.+`. | Usually also faster. |
| `WithTimeout(TimeSpan)` | Throws `RegexMatchTimeoutException` when a match runs too long. | A safety net for untrusted input, not a fix. |
| `UseNonBacktracking()` | Runs on the .NET non-backtracking engine, which takes time linear in the input length. | .NET 8 and later only. Not available in the .NET Standard 2.1 build. Does not support atomic groups, so it cannot run possessive patterns (see below). |

The full story, including attack inputs and confirmation runs, is in [ReDoS Safety](./redos-safety.md). To see where a pattern spends its steps, use the [Backtracking Profiler](./backtracking-profiler.md).

## Errors

Quantifier mistakes throw while the pattern is being built, not later when it runs.

| Call | Exception | Message |
|------|-----------|---------|
| `Pattern.With.Repeat.OneOrMore` (nothing to repeat) | `InvalidOperationException` | `Repeat must follow an element to repeat, for example Pattern.With.Digit.Repeat.OneOrMore.` |
| `Pattern.With.Digit.Lazy` | `InvalidOperationException` | `Lazy must directly follow a quantifier, for example Pattern.With.Digit.Repeat.OneOrMore.Lazy.` |
| `Pattern.With.Digit.Possessive` | `InvalidOperationException` | `Possessive must directly follow a quantifier, for example Pattern.With.Digit.Repeat.OneOrMore.Possessive.` |
| `Repeat.Times(-1)` | `ArgumentOutOfRangeException` | `The count cannot be negative. (Parameter 'exactly')` |
| `Repeat.Times(-1, 2)` | `ArgumentOutOfRangeException` | `The minimum cannot be negative. (Parameter 'min')` |
| `Repeat.Times(5, 2)` | `ArgumentOutOfRangeException` | `The maximum cannot be less than the minimum. (Parameter 'max')` |
| `Repeat.AtLeast(-1)` | `ArgumentOutOfRangeException` | `The minimum cannot be negative. (Parameter 'x')` |
| `Repeat.AtMost(-1)` | `ArgumentOutOfRangeException` | `The maximum cannot be negative. (Parameter 'x')` |

When the count comes from a variable, the check happens at run time:

```csharp
// count comes from your application
int count = int.Parse("-1");

try
{
    Pattern.With.Digit.Repeat.Times(count);
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine(ex.Message);   // The count cannot be negative. (Parameter 'exactly')
}
```

:::tip

When the arguments are constants, you do not have to wait for a run. The analyzers that ship in the package evaluate the pattern while you compile and report error `FRX006`, for example *This pattern throws ArgumentOutOfRangeException when it is built: The count cannot be negative. (Parameter 'exactly')*. See [Compile-Time Checks](./compile-time-checks.md).

:::

## Quantifiers on other engines

Greedy and lazy quantifiers exist in almost every regex engine. Possessive ones do not, because they become atomic groups, and many engines have no atomic groups.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern lazy = Pattern.With.Literal("<").Anything.Repeat.OneOrMore.Lazy.Literal(">");
Pattern possessive = Pattern.With.Literal("<").Anything.Repeat.OneOrMore.Possessive.Literal(">");

Console.WriteLine(lazy.ToFlavor(RegexFlavor.JavaScript).Pattern);   // <[^\n]+?>
try
{
    possessive.ToFlavor(RegexFlavor.JavaScript);
}
catch (NotSupportedException ex)
{
    Console.WriteLine(ex.Message);
    // prints:
    // The pattern cannot be converted to JavaScript without changing its meaning:
    //   - atomic group '(?>...)' at position 1
}

Console.WriteLine(lazy.UseNonBacktracking().Match("<a>text<b>").Value);   // <a>
try
{
    possessive.UseNonBacktracking().IsMatch("<a>text<b>");
}
catch (NotSupportedException ex)
{
    Console.WriteLine(ex.Message);
    // prints: RegexOptions.NonBacktracking is not supported in conjunction with expressions containing: 'atomic subexpressions (?> pattern)'.
}
```

- In the JavaScript output, `.` became `[^\n]` because .NET's `.` and JavaScript's `.` do not stop at the same characters. The lazy `?` is kept.
- `ToFlavor` refuses to drop the atomic group, because that would change what the pattern matches. If you export a pattern, prefer a narrower class (such as `[^>]+`) over `Possessive`. See [Export](./export.md) and [SQL Dialects](./sql-dialects.md).

This is what `ToFlavor` does with the possessive pattern `<(?>.+)>` for each `RegexFlavor`:

| Flavor | Result |
|--------|--------|
| `JavaScript`, `JsonSchema` | Throws `NotSupportedException` (atomic group). |
| `PostgreSql`, `SqlServer` | Throws `NotSupportedException` (atomic group). |
| `Sqlite` | Throws `NotSupportedException`: the EF Core `regexp()` function runs with `RegexOptions.NonBacktracking`, which rejects atomic groups. |
| `MySql`, `MariaDb` | Works: `(?-i)<(?>[^\n]+)>`. Both engines (ICU and PCRE2) support atomic groups. |
- `UseNonBacktracking()` (.NET 8 and later) does not need `Possessive`: it never backtracks, so it is already safe from catastrophic backtracking.

## Reading and simplifying quantifiers

`Explain()` describes each quantifier in plain English. It names the mode for lazy quantifiers, and shows a possessive one as an atomic group.

```csharp
Console.WriteLine(Pattern.With.Digit.Repeat.Times(2, 4).Explain());
// prints:
// Pattern: \d{2,4}
//
// Explanation:
//   - Digit (any Unicode decimal digit)
//   - Between 2 and 4 times

Console.WriteLine(Pattern.With.Anything.Repeat.ZeroOrMore.Lazy.Explain());
// prints:
// Pattern: .*?
//
// Explanation:
//   - Any character (except newline)
//   - Zero or more (lazy)
```

`Optimize()` rewrites long quantifier forms into their short ones and reports whether it could prove that the result matches the same strings:

```csharp
OptimizationResult result = Pattern.FromRegex("x{1}y{0,1}z{0,}w{1,}").Optimize();
Console.WriteLine(result.Optimized);             // xy?z*w+
Console.WriteLine(result.VerifiedEquivalent);    // True
Console.WriteLine(result.AppliedRewrites[1]);    // Simplified quantifier 'y{0,1}' to 'y?'
```

`VerifiedEquivalent` is `true` when the optimizer proved equivalence with language algebra. For patterns outside the regular subset (for example with backreferences or lookarounds) it is `false`, and the rewrites were checked on sample inputs only.

`ToFluentCode()` goes the other way. It turns a regex into fluent C#, choosing the matching `Repeat` member for each quantifier:

```csharp
Console.WriteLine(Pattern.FromRegex(@"\d{0,3}\w{2,}?").ToFluentCode());
// prints:
// Pattern.With
//     .Digit.Repeat.AtMost(3)
//     .Word.Repeat.AtLeast(2).Lazy
```

| Regex quantifier | Fluent code |
|------------------|-------------|
| `+` | `Repeat.OneOrMore` |
| `*` | `Repeat.ZeroOrMore` |
| `?` | `Repeat.Optional` |
| `{n}` | `Repeat.Times(n)` |
| `{n,}` | `Repeat.AtLeast(n)` |
| `{0,n}` | `Repeat.AtMost(n)` |
| `{min,max}` | `Repeat.Times(min, max)` |
| A trailing `?` (lazy) | `.Lazy` |
| `(?>\d+)` (atomic group) | `AtomicGroup(Pattern.With.Digit.Repeat.OneOrMore)`, not `.Possessive` |

More tools: [Validation and Debugging](./validation-and-debugging.md), [Optimizer](./optimizer.md) and [Regex to Fluent Code](./regex-to-fluent-code.md).

## Tips and pitfalls

- **Repeat sequences through a group.** `Literal("ab").Repeat.OneOrMore` is `ab+`. Write `NonCapturingGroup(Pattern.With.Literal("ab")).Repeat.OneOrMore` for `(?:ab)+`.
- **Write `.Lazy` for a lazy quantifier.** `Repeat.Optional` after another quantifier makes the repetition optional, `(?:\d+)?`, not lazy.
- **Anchor exact counts.** `Times(5)` without anchors also finds five digits inside a longer number.
- **Watch for empty matches.** `ZeroOrMore`, `Optional`, `AtMost` and `Times(0, n)` can all match nothing.
- **Prefer a narrow class to a lazy dot.** `[^>]+` is clearer and faster than `.+?` before `>`.
- **Never nest open-ended repetitions without protection.** Make the inner one `Possessive`, or restructure it. Run `AnalyzeSafety()` on patterns that handle untrusted input.
- **Use `Possessive` only where giving back cannot help.** It can turn a match into no match, as `(?>\d+)\d` shows.
- **Avoid `Possessive` in patterns you export.** It becomes an atomic group, which JavaScript, JSON Schema, PostgreSQL, SQL Server and SQLite cannot express. Only the MySQL and MariaDB flavors keep it.

For a summary of every regex quantifier, see the [Regex Cheat Sheet](./regex-cheat-sheet.md).
