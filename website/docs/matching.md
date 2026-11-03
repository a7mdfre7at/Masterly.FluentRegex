---
id: matching
title: "Matching, Replacing and Splitting"
description: "Test, find, count, replace and split text with a pattern, set options and timeouts, and use the allocation-free span APIs."
---

Every `Pattern` can run itself. You do not need to call `ToRegex()` first: a pattern tests, finds, counts, replaces and splits text directly. These methods wrap `System.Text.RegularExpressions.Regex`, so they return the standard `Match`, `MatchCollection` and `string[]` types and behave exactly like the matching `Regex` methods.

Use the methods on this page whenever you have a pattern and a string. To bind matches to typed objects, see [Typed Extraction](./typed-extraction.md). To read input that does not fit in a string, such as a large file, see [Streaming](./streaming.md).

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

bool hasDigits = digits.IsMatch("abc123def");                       // true
Match first = digits.Match("abc123def456");                         // first.Value is "123"
int count = digits.Count("a1b22c333");                              // 3
string masked = digits.Replace("PIN 1234", "****");                 // "PIN ****"
string[] letters = Pattern.With.Literal(",").Split("a,b,c");        // ["a", "b", "c"]
```

The pattern builds its `Regex` on first use and caches it. Because a pattern is immutable, the cached `Regex` can never go stale. See [Performance and thread safety](#performance-and-thread-safety).

| Method | Returns | Use it when |
|--------|---------|-------------|
| `IsMatch(input)` | `bool` | You only need a yes/no answer |
| `Match(input)`, `Match(input, startAt)` | `Match` | You want the first match, its position or its groups |
| `Matches(input)` | `MatchCollection` | You want every match |
| `Count(input)` | `int` | You only need the number of matches |
| `Replace(input, ...)` | `string` | You want to rewrite the matches |
| `Split(input)`, `Split(input, count)` | `string[]` | You want the text between the matches |
| `EnumerateMatches(span)`, `EnumerateSplits(span)` | Enumerators of positions | You want to avoid allocations on modern .NET (see [Span APIs on modern .NET](#span-apis-on-modern-net)) |

## Testing for a match

`IsMatch` returns `true` when the pattern matches anywhere in the input.

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

Console.WriteLine(digits.IsMatch("abc123def"));   // True
Console.WriteLine(digits.IsMatch("no numbers"));  // False
```

### Matching the whole string

"Anywhere" is the important word. `\d+` is found inside `abc123`, so `IsMatch` returns `true`. To accept only input that is digits from start to end, anchor the pattern at both ends with `StartOfInput` and `EndOfInput`.

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;
Pattern onlyDigits = Pattern.With.StartOfInput.Digit.Repeat.OneOrMore.EndOfInput;

Console.WriteLine(digits.IsMatch("abc123"));      // True
Console.WriteLine(onlyDigits.IsMatch("abc123"));  // False
Console.WriteLine(onlyDigits.IsMatch("123"));     // True
Console.WriteLine(onlyDigits);                    // \A\d+\z
```

Prefer `StartOfInput` / `EndOfInput` (`\A` / `\z`) over `StartOfLine` / `EndOfLine` (`^` / `$`) for validation. `$` also matches before a final newline, so a value with a trailing `\n` slips through:

```csharp
// onlyDigits is from the previous example
Pattern lineDigits = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;

Console.WriteLine(lineDigits.IsMatch("123\n"));  // True
Console.WriteLine(onlyDigits.IsMatch("123\n"));  // False
```

:::note

Some features need to know which of these two meanings you have in mind. They take a `MatchScope`: `MatchScope.Substring` means "what `IsMatch` accepts" and `MatchScope.FullString` means "the whole string matches", as if the pattern were wrapped in `\A(?:...)\z` (an explicit `$` in the pattern still accepts a final newline). Language facts default to `FullString` (see [Language Facts](./language-facts.md)) and LIKE translation defaults to `Substring` (see [SQL LIKE](./sql-like.md)).

:::

### Testing with other options

`IsMatch(input, options)` runs the pattern once with different `RegexOptions`.

```csharp
Pattern hello = Pattern.With.Literal("hello");
Pattern ignoreCase = hello.WithOptions(RegexOptions.IgnoreCase);

Console.WriteLine(hello.IsMatch("HELLO", RegexOptions.IgnoreCase));                         // True
Console.WriteLine(ignoreCase.IsMatch("HELLO", RegexOptions.Multiline));                     // False
Console.WriteLine(ignoreCase.IsMatch("HELLO", ignoreCase.Options | RegexOptions.Multiline)); // True
```

- The options **replace** the pattern's own options. They are not added to them. In the second line `IgnoreCase` is lost, so the test fails. Combine them yourself with `pattern.Options | ...`, as in the third line.
- The pattern's timeout still applies.
- This overload builds a new `Regex` on every call and does not use the cache. Calling it in a loop is much slower than calling `IsMatch(input)` on a pattern built with `WithOptions`.

:::tip

If you always test with the same options, build the pattern once with `WithOptions` (see [Options](#options)) and store it in a `static readonly` field. Keep `IsMatch(input, options)` for one-off checks.

:::

## Finding the first match

`Match` returns the first match. Check `Success` before you read the value: when nothing matches, you get a failed match, never `null`.

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

Match first = digits.Match("abc123def456");
Console.WriteLine(first.Success);  // True
Console.WriteLine(first.Value);    // 123
Console.WriteLine(first.Index);    // 3
Console.WriteLine(first.Length);   // 3

Match second = first.NextMatch();
Console.WriteLine(second.Value);   // 456

Match none = digits.Match("no numbers");
Console.WriteLine(none.Success);                 // False
Console.WriteLine(none.Value == string.Empty);   // True
```

- `Index` is the position of the first matched character. `Length` is the number of matched characters.
- `NextMatch()` continues the search after the current match. A loop over `Match` and `NextMatch()` gives the same matches as `Matches`.
- A failed match has `Success` false, an empty `Value`, and `Index` and `Length` of zero.

### Starting at a position

`Match(input, startAt)` starts the search at a character position.

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

Console.WriteLine(digits.Match("abc123def456", 5).Value);  // 3
Console.WriteLine(digits.Match("abc123def456", 6).Value);  // 456
```

The search starts at `startAt`, but the engine still sees the whole input. That has three effects:

```csharp
Pattern atStart = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore;
Pattern contiguous = Pattern.With.RegEx(@"\G").Digit.Repeat.OneOrMore;
Pattern afterLetter = Pattern.With.PositiveLookbehind(Pattern.With.Letter).Digit.Repeat.OneOrMore;

Console.WriteLine(atStart.Match("ab12", 2).Success);     // False
Console.WriteLine(contiguous.Match("ab12", 2).Success);  // True
Console.WriteLine(afterLetter.Match("ab12", 2).Value);   // 12
```

- `^` (`StartOfLine`) and `\A` (`StartOfInput`) still mean the start of the input, not `startAt`. They do not match at position 2.
- `\G` means "where this search started". Use it to require a match exactly at `startAt`.
- Lookbehinds can look at the characters before `startAt`. Here the lookbehind sees the `b` at position 1.
- `startAt` must be between `0` and `input.Length`. Any other value throws `ArgumentOutOfRangeException`.

### Reading groups

A match carries its groups. Read a named group with `Groups["name"]`.

```csharp
Pattern assignment = Pattern.With
    .NamedGroup("name", Pattern.With.Word.Repeat.OneOrMore)
    .Literal("=")
    .NamedGroup("value", Pattern.With.Digit.Repeat.OneOrMore);

Match match = assignment.Match("timeout=30");

Console.WriteLine(match.Groups["name"].Value);       // timeout
Console.WriteLine(match.Groups["value"].Value);      // 30
Console.WriteLine(match.Groups["missing"].Success);  // False
```

- A group name that does not exist does not throw. You get a group with `Success` false and an empty `Value`.
- Numbered groups work too: `match.Groups[1]`. Group 0 is the whole match.
- To turn groups into a typed object instead of reading strings, see [Typed Extraction](./typed-extraction.md) and [Templates](./templates.md). For group syntax, see [Groups and Backreferences](./groups-and-backreferences.md).

## Finding all matches

`Matches` returns every match, from left to right.

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

MatchCollection all = digits.Matches("a1b22c333d4444");

foreach (Match match in all)
{
    Console.WriteLine($"'{match.Value}' at index {match.Index}");
}
// prints:
// '1' at index 1
// '22' at index 3
// '333' at index 6
// '4444' at index 10

Console.WriteLine(all.Count);  // 4
```

- Matches never overlap. The next search starts where the previous match ended, so `\d{2}` finds `12` and `34` in `12345`, not `23` or `45`.
- When nothing matches, the collection is empty. It is never `null`.
- `MatchCollection` is filled lazily as you iterate. Reading `Count` scans the whole input. If you only need the number, call [`Count`](#counting-matches) instead.

A pattern that can match nothing, such as `\d*`, also finds empty matches between characters:

```csharp
Pattern optionalDigits = Pattern.With.Digit.Repeat.ZeroOrMore;

foreach (Match match in optionalDigits.Matches("a1"))
{
    Console.WriteLine($"[{match.Value}] at {match.Index}");
}
// prints:
// [] at 0
// [1] at 1
// [] at 2
```

:::warning

Empty matches are a common source of surprising counts and replacements. `\d*` counts 3 matches in `a1`, and `Replace("a1", "-")` returns `-a--`. Use `OneOrMore` instead of `ZeroOrMore` when an empty match makes no sense.

:::

## Counting matches

`Count` returns the number of matches without building a `MatchCollection`.

```csharp
Pattern word = Pattern.With.Word.Repeat.OneOrMore;

Console.WriteLine(word.Count("the quick brown fox"));  // 4
Console.WriteLine(word.Count(string.Empty));           // 0
```

- `Count(string)` works on every target framework. On .NET 8 and later it calls `Regex.Count`, which does not allocate `Match` objects. On `netstandard2.1` it walks the matches with `Match` and `NextMatch()`, which gives the same number but allocates.
- On .NET 8 and later there is also `Count(ReadOnlySpan<char>)` (see [Span APIs on modern .NET](#span-apis-on-modern-net)).
- A `null` input throws `ArgumentNullException` on every target.

## Replacing text

`Replace` rewrites every match. It returns a new string, and returns the input unchanged when nothing matches.

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

Console.WriteLine(digits.Replace("a1b2c3", "X"));     // aXbXcX
Console.WriteLine(digits.Replace("no digits", "X"));  // no digits
```

There are four overloads, plus one for fluent replacements:

| Overload | Replaces |
|----------|----------|
| `Replace(input, replacement)` | Every match, with a replacement string |
| `Replace(input, replacement, count)` | The first `count` matches, with a replacement string |
| `Replace(input, evaluator)` | Every match, with the string a function returns |
| `Replace(input, evaluator, count)` | The first `count` matches, with the string a function returns |
| `Replace(input, build)`, `Replace(input, build, count)` | Every match (or the first `count`), with a replacement built fluently (see [Fluent Replacements](./fluent-replacements.md)) |

### Replacement strings

A replacement string can refer to the match and its groups with `$` substitutions.

```csharp
Pattern assignment = Pattern.With
    .NamedGroup("name", Pattern.With.Word.Repeat.OneOrMore)
    .Literal("=")
    .NamedGroup("value", Pattern.With.Digit.Repeat.OneOrMore);

Console.WriteLine(assignment.Replace("x=1 y=2 z=3", "${name}: ${value}"));  // x: 1 y: 2 z: 3
Console.WriteLine(assignment.Replace("x=1 y=2", "$2=$1"));                 // 1=x 2=y
Console.WriteLine(assignment.Replace("x=1 y=2", "[$0]"));                  // [x=1] [y=2]
```

These are the substitutions .NET supports. The last column shows the result of `assignment.Replace("a x=1 b", "<...>")` with the substitution inside the angle brackets:

| Substitution | Inserts | `a x=1 b` becomes |
|--------------|---------|-------------------|
| `$0` or `$&` | The whole match | `a <x=1> b` |
| `$1`, `$2`, ... | A numbered group | `$2` gives `a <1> b` |
| `${name}` | A named group | `${name}` gives `a <x> b` |
| `` $` `` | The text before the match | `a <a > b` |
| `$'` | The text after the match | `a < b> b` |
| `$+` | The last group | `a <1> b` |
| `$_` | The entire input | `a <a x=1 b> b` |
| `$$` | A literal `$` | `a <$> b` |

- A substitution that names a group that does not exist, such as `${unknown}` or `$9`, is not an error. It stays in the output as literal text.
- To put a literal `$` right before a group, double it: `$$` then `${amount}`.

```csharp
Pattern amount = Pattern.With.NamedGroup("amount", Pattern.With.Digit.Repeat.OneOrMore);

Console.WriteLine(amount.Replace("Total: 42", "$$${amount}.00"));  // Total: $42.00
Console.WriteLine(amount.Replace("Total: 42", "$${amount}"));      // Total: ${amount}
```

:::warning

Never pass user input as a replacement string without escaping it. A user-supplied `$0` or `${name}` is interpreted, not copied. To insert untrusted text literally, double every `$` (`text.Replace("$", "$$")`) or use an evaluator, whose return value is never interpreted.

:::

### Limiting the number of replacements

Pass `count` to replace only the first matches.

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

Console.WriteLine(digits.Replace("a1b2c3d4", "X", 2));   // aXbXc3d4
Console.WriteLine(digits.Replace("a1b2c3d4", "X", 0));   // a1b2c3d4
Console.WriteLine(digits.Replace("a1b2c3d4", "X", -1));  // aXbXcXdX
```

| `count` | Effect |
|---------|--------|
| A positive number | Replaces at most that many matches, from the left |
| `0` | Replaces nothing |
| `-1` | Replaces every match, like the overload without `count` |
| Below `-1` | Throws `ArgumentOutOfRangeException` |

### Replacing with a function

Pass a `MatchEvaluator` (any `Func<Match, string>`-shaped lambda) when the new text depends on the match.

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

string tenTimes = digits.Replace("a1b2c3", match => (int.Parse(match.Value) * 10).ToString());
Console.WriteLine(tenTimes);  // a10b20c30

string firstTwo = digits.Replace("a1b2c3", match => $"<{match.Value}>", 2);
Console.WriteLine(firstTwo);  // a<1>b<2>c3
```

- The function runs once per match, in order, and receives the full `Match`, including its groups.
- Its return value is inserted literally. Returning `"$0"` inserts the two characters `$0`, not the match.
- A `null` evaluator throws `ArgumentNullException`.

### Replacing with a fluent replacement

Instead of a `$` string, you can build the replacement with the same fluent style as the pattern. Group names and numbers are checked against the pattern before anything is replaced: a group the pattern does not have throws `ArgumentException`, instead of being copied as literal text.

```csharp
Pattern date = Pattern.With
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
    .Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2))
    .Literal("-")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2));

string european = date.Replace("Due 2026-11-03", replacement => replacement
    .Group("day").Literal("/").Group("month").Literal("/").Group("year"));

Console.WriteLine(european);  // Due 03/11/2026

string firstYearOnly = date.Replace("2026-11-03 2027-01-02", replacement => replacement.Group("year"), 1);
Console.WriteLine(firstYearOnly);  // 2026 2027-01-02
```

- `Replace(input, build, count)` takes the same `count` values as the string overloads.
- The groups are checked once, before the first replacement. `Group("nope")` throws `ArgumentException` with a message that lists the available groups: `Group 'nope' does not exist in the pattern ... Available groups: 0, year, month, day.`
- A `null` builder throws `ArgumentNullException`. A builder that returns `null` throws `InvalidOperationException` ("The replacement builder returned null.").
- The fluent overloads use the same cached `Regex` as the other methods.

The full `Replacement` API is described in [Fluent Replacements](./fluent-replacements.md).

## Splitting text

`Split` cuts the input at every match and returns the pieces between them.

```csharp
Pattern comma = Pattern.With.Literal(",");
Pattern whitespace = Pattern.With.Whitespace.Repeat.OneOrMore;

string[] letters = comma.Split("a,b,c,d");
string[] words = whitespace.Split("hello   world\ttest");

Console.WriteLine(string.Join(" | ", letters));  // a | b | c | d
Console.WriteLine(string.Join(" | ", words));    // hello | world | test
```

### Empty pieces

`Split` keeps empty pieces. A delimiter at the start or end, or two delimiters in a row, give an empty string.

```csharp
using System.Linq;

Pattern comma = Pattern.With.Literal(",");

string[] parts = comma.Split(",a,,b,");
Console.WriteLine(parts.Length);             // 5
Console.WriteLine(string.Join("|", parts));  // |a||b|

string[] nonEmpty = parts.Where(part => part.Length > 0).ToArray();
Console.WriteLine(string.Join("|", nonEmpty));  // a|b
```

- When nothing matches, you get a one-element array that holds the whole input.
- Splitting an empty string gives a one-element array that holds an empty string.
- There is no `RemoveEmptyEntries` option. Filter the array as shown, or match the pieces instead of the delimiters with `Matches`.

### Captured delimiters

When the pattern has capturing groups, the captured text is added to the result, between the pieces. Use this to keep the delimiters.

```csharp
Pattern operators = Pattern.With.Group(Pattern.With.AnyOf("+-"));
Pattern quietOperators = Pattern.With.AnyOf("+-");

Console.WriteLine(string.Join(" | ", operators.Split("1+2-3")));       // 1 | + | 2 | - | 3
Console.WriteLine(string.Join(" | ", quietOperators.Split("1+2-3")));  // 1 | 2 | 3
```

If you group the delimiter only to apply a quantifier, use `NonCapturingGroup` (or `RegexOptions.ExplicitCapture`) so the delimiters stay out of the result.

### Limiting the number of pieces

Pass `count` to get at most that many pieces. The last piece holds the rest of the input.

```csharp
Pattern dash = Pattern.With.Literal("-");

Console.WriteLine(string.Join(" | ", dash.Split("a-b-c-d-e", 3)));  // a | b | c-d-e
```

| `count` | Effect |
|---------|--------|
| A number above 1 | At most that many pieces |
| `1` | One piece: the whole input |
| `0` | No limit, like the overload without `count` |
| A negative number | Throws `ArgumentOutOfRangeException` |

## Options

`WithOptions` returns a copy of the pattern that runs with the given `RegexOptions`. The `Options` property tells you which options a pattern has.

```csharp
Pattern hello = Pattern.With.Literal("hello");
Pattern ignoreCase = hello.WithOptions(RegexOptions.IgnoreCase);

Console.WriteLine(hello.Options);                // None
Console.WriteLine(ignoreCase.Options);           // IgnoreCase
Console.WriteLine(hello.IsMatch("HELLO"));       // False
Console.WriteLine(ignoreCase.IsMatch("HELLO"));  // True
Console.WriteLine(ignoreCase);                   // hello
```

- `WithOptions` never changes the original pattern. `hello` still matches case-sensitively.
- Options are not part of the regex text. `ToString()` returns `hello` for both patterns. They are applied when the pattern runs, by every method on this page, by `ToRegex()` and by the code the source generator emits.
- Options take part in equality: `hello.Equals(ignoreCase)` is `false`.

### Replacing or adding options

`WithOptions` replaces all options, like `IsMatch(input, options)`. To add an option, combine it with the current ones.

```csharp
// ignoreCase is from the previous example
Pattern multilineOnly = ignoreCase.WithOptions(RegexOptions.Multiline);
Pattern both = ignoreCase.WithOptions(ignoreCase.Options | RegexOptions.Multiline);

Console.WriteLine(multilineOnly.Options);  // Multiline
Console.WriteLine(both.Options);           // IgnoreCase, Multiline
```

### How options travel through composition

Options belong to the chain you call `WithOptions` on. They carry forward as you extend that chain, but they do not come along when the pattern is nested inside another one.

```csharp
Pattern code = Pattern.With.Literal("abc").WithOptions(RegexOptions.IgnoreCase);

Pattern extended = code.Literal("-x");
Pattern nested = Pattern.With.Literal("id:").Group(code);
Pattern inline = Pattern.With.Literal("id:").IgnoreCase(Pattern.With.Literal("abc"));

Console.WriteLine(extended.Options);          // IgnoreCase
Console.WriteLine(nested.Options);            // None
Console.WriteLine(nested.IsMatch("id:ABC"));  // False
Console.WriteLine(inline);                    // id:(?i:abc)
Console.WriteLine(inline.IsMatch("id:ABC"));  // True
Console.WriteLine(inline.IsMatch("ID:ABC"));  // False
```

| How you combine | Options of the result |
|-----------------|-----------------------|
| Extending a chain: `code.Literal(...)`, `code.Then(other)`, `code + other` | The options of `code`, the left side |
| Nesting: `Group(code)`, `OneOf(code, ...)`, `other.Then(code)`, `other + code` | The options of the outer chain; those of `code` are dropped |
| `code \| other` | `None`: the alternation starts a new chain |

:::tip

When only part of a pattern should ignore case, do not rely on `WithOptions` of a nested pattern. Use the inline scopes `IgnoreCase(pattern)`, `CaseSensitive(pattern)` and `InlineOptions(enable, pattern, disable)`. They are rendered into the regex text, so they survive any composition (see [Composing Patterns](./composing-patterns.md)).

:::

### Which options to use

| Option | Effect |
|--------|--------|
| `IgnoreCase` | Case-insensitive matching |
| `Multiline` | `^` and `$` match at every line break, not only at the ends of the input |
| `Singleline` | `.` (`Anything`) also matches `\n` |
| `ExplicitCapture` | Only named groups capture; plain `(...)` groups do not |
| `IgnorePatternWhitespace` | Unescaped spaces in the regex are ignored and `#` starts a comment |
| `RightToLeft` | The search runs from the end of the input towards the start |
| `CultureInvariant` | Case-insensitive comparisons ignore the current culture |
| `ECMAScript` | ECMAScript behavior. Can be combined only with `IgnoreCase`, `Multiline`, `CultureInvariant` and `Compiled` |
| `Compiled` | Compiles the regex to IL. Slower first use, faster matching. Worth it because the pattern caches its `Regex` |
| `NonBacktracking` | The linear-time engine on .NET 8 and later. Prefer `UseNonBacktracking()` (see [The non-backtracking engine](#the-non-backtracking-engine)) |

`Multiline` and `Singleline` change what anchors and `Anything` mean:

```csharp
Pattern line = Pattern.With.StartOfLine.Word.Repeat.OneOrMore.EndOfLine;
string text = "alpha\nbeta\ngamma";

Console.WriteLine(line.Matches(text).Count);                                      // 0
Console.WriteLine(line.WithOptions(RegexOptions.Multiline).Matches(text).Count);  // 3

Pattern tag = Pattern.With.Literal("<").Anything.Repeat.ZeroOrMore.Lazy.Literal(">");

Console.WriteLine(tag.IsMatch("<a\nb>"));                                       // False
Console.WriteLine(tag.WithOptions(RegexOptions.Singleline).IsMatch("<a\nb>"));  // True
```

`RightToLeft` finds the last match first:

```csharp
Pattern lastNumber = Pattern.With.Digit.Repeat.OneOrMore.WithOptions(RegexOptions.RightToLeft);

Console.WriteLine(lastNumber.Match("a1b22c333").Value);              // 333
Console.WriteLine(lastNumber.Match("a1b22c333").NextMatch().Value);  // 22
```

:::note

Under `IgnorePatternWhitespace`, literals escape their spaces and `#`, so `Pattern.With.Literal("a b").Literal("#")` with that option renders `a\ b\#` and still matches `a b#`. Text added with `RegEx(...)` is not changed.

:::

An invalid combination, such as `ECMAScript` with `Singleline`, is not reported by `WithOptions`. The `Regex` constructor throws `ArgumentOutOfRangeException` when the pattern first runs. When the pattern and its options are constants, the analyzer reports the problem at compile time as error FRX003 (see [Compile-Time Checks](./compile-time-checks.md)).

## Getting a Regex

Use `ToRegex()` when an API needs a `System.Text.RegularExpressions.Regex`, for example a validation attribute or a library you pass the regex to.

```csharp
Pattern word = Pattern.With.Word.Repeat.OneOrMore
    .WithOptions(RegexOptions.IgnoreCase)
    .WithTimeout(TimeSpan.FromSeconds(1));

Regex regex = word.ToRegex();
Console.WriteLine(regex);               // \w+
Console.WriteLine(regex.Options);       // IgnoreCase
Console.WriteLine(regex.MatchTimeout);  // 00:00:01

Regex compiled = word.ToRegex(RegexOptions.Compiled);
Console.WriteLine(compiled.Options);       // Compiled
Console.WriteLine(compiled.MatchTimeout);  // 00:00:01
```

| Method | Options | Timeout |
|--------|---------|---------|
| `ToRegex()` | The pattern's options | The pattern's timeout |
| `ToRegex(options)` | The given options, which replace the pattern's | The pattern's timeout |

- Each call creates a **new** `Regex`. The pattern's own matching methods use a private cached instance, and `ToRegex()` does not return it. If you call `ToRegex()` yourself, keep the result instead of calling it again.
- An invalid regex or an invalid options combination throws here, immediately.
- `ToString()` returns only the regex text, without options or timeout.
- To have the regex text computed at build time, use the source generator (see [Compile-Time Checks](./compile-time-checks.md)). The method it emits returns a cached `Regex` with the pattern's options plus `Compiled`, and the pattern's timeout.

## Timeouts

A pattern with nested quantifiers can take exponential time on some inputs. On untrusted input, give the pattern a time budget with `WithTimeout`. When the budget runs out, the engine stops and throws `RegexMatchTimeoutException` instead of hanging.

```csharp
// The analyzer reports FRX004 (ReDoS risk) for this nested quantifier
Pattern nested = Pattern.With.Group(Pattern.With.Letter.Repeat.OneOrMore).Repeat.OneOrMore.EndOfLine;
Pattern guarded = nested.WithTimeout(TimeSpan.FromMilliseconds(100));

string attack = new string('a', 40) + "!";

try
{
    guarded.IsMatch(attack);
}
catch (RegexMatchTimeoutException exception)
{
    Console.WriteLine($"Timed out after {exception.MatchTimeout.TotalMilliseconds} ms");  // Timed out after 100 ms
}
```

- The timeout travels with the pattern into every matching method on this page, into `ToRegex()` and `ToRegex(options)`, and into the code the source generator emits. It survives `WithOptions` and extending the chain.
- `Timeout` returns the pattern's timeout. A new pattern has `Regex.InfiniteMatchTimeout`, which means no timeout.
- The exception's `Pattern` and `Input` properties hold the regex text and the input. Do not log `Input` when it can contain personal data.

`WithTimeout` validates its argument:

| Value | Result |
|-------|--------|
| A positive time up to about 24 days | Accepted |
| `Regex.InfiniteMatchTimeout` | Accepted. Removes a timeout set earlier |
| Zero, a negative time, or more than about 24 days | Throws `ArgumentOutOfRangeException` |

:::warning

A pattern always passes its own timeout to `Regex`, even when it is infinite. The process-wide default set with `AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", ...)` therefore does **not** apply to patterns. Call `WithTimeout` on every pattern that runs on untrusted input.

:::

A timeout limits the damage; it does not fix the pattern. To find and fix the risky part, see [ReDoS Safety](./redos-safety.md).

## The non-backtracking engine

On .NET 8 and later, `UseNonBacktracking()` returns a copy of the pattern that runs on the linear-time engine. Its matching time grows linearly with the input length, so it is immune to catastrophic backtracking.

```csharp
// nested is from the "Timeouts" example
Pattern linear = nested.UseNonBacktracking();
string longAttack = new string('a', 50_000) + "!";

Console.WriteLine(linear.Options);              // NonBacktracking
Console.WriteLine(linear.IsMatch(longAttack));  // False
```

- `UseNonBacktracking()` adds `RegexOptions.NonBacktracking` to the existing options. It does not replace them.
- Captures, named groups and replacement substitutions still work.
- A later `WithOptions(...)` replaces all options and drops `NonBacktracking`. Call `UseNonBacktracking()` last, or include the flag yourself.
- The member is not available on `netstandard2.1`.

The engine does not support every construct. The pattern is built without error; the exception comes when the pattern first runs (or when you call `ToRegex()`):

```csharp
Pattern doubled = Pattern.With.NamedGroup("char", Pattern.With.Letter).Backreference("char");
Pattern linearDoubled = doubled.UseNonBacktracking();  // no error yet

try
{
    linearDoubled.IsMatch("aa");
}
catch (NotSupportedException exception)
{
    Console.WriteLine(exception.Message);
}
// prints: RegexOptions.NonBacktracking is not supported in conjunction with expressions containing: 'backreference (\ number)'.
```

| Construct | Exception at first use |
|-----------|------------------------|
| Backreferences (`Backreference`) | `NotSupportedException` |
| Lookaheads and lookbehinds | `NotSupportedException` |
| Atomic groups, including `Possessive` quantifiers | `NotSupportedException` |
| Conditionals (`Conditional`) | `NotSupportedException` |
| `RegexOptions.RightToLeft` | `ArgumentOutOfRangeException` |

:::tip

For untrusted input on .NET 8 and later, `UseNonBacktracking()` is the strongest guarantee when the pattern avoids these constructs. Otherwise use `WithTimeout`. The two can be combined.

:::

## Span APIs on modern .NET

On .NET 8 and later, patterns expose the allocation-free `ReadOnlySpan<char>` APIs of `Regex`. Use them on hot paths, or to search part of a string without calling `Substring`.

```csharp
Pattern number = Pattern.With.Digit.Repeat.OneOrMore;
ReadOnlySpan<char> text = "order 17, order 204".AsSpan();

Console.WriteLine(number.IsMatch(text));  // True
Console.WriteLine(number.Count(text));    // 2

foreach (ValueMatch match in number.EnumerateMatches(text))
{
    Console.WriteLine($"{text.Slice(match.Index, match.Length).ToString()} at {match.Index}");
}
// prints:
// 17 at 6
// 204 at 16
```

| Member | Returns | Available on |
|--------|---------|--------------|
| `IsMatch(ReadOnlySpan<char>)` | `bool` | net8.0, net10.0 |
| `Count(ReadOnlySpan<char>)` | `int` | net8.0, net10.0 |
| `EnumerateMatches(ReadOnlySpan<char>)` | `Regex.ValueMatchEnumerator` of `ValueMatch` | net8.0, net10.0 |
| `EnumerateSplits(ReadOnlySpan<char>)` | `Regex.ValueSplitEnumerator` of `Range` | net10.0 |

- A `ValueMatch` has only `Index` and `Length`. It has no `Value` and no groups. Slice the input to get the text, and use `Match` when you need groups.
- Positions are relative to the span you pass, not to the string it came from.
- The enumerators are `ref struct`s. Use them in a `foreach`; you cannot store them in a field or use them across an `await`.

Searching part of a string:

```csharp
// number is from the previous example
string record = "id=17;qty=204;note=none";
ReadOnlySpan<char> quantity = record.AsSpan(6, 7);  // "qty=204"

Console.WriteLine(number.IsMatch(quantity));  // True

foreach (ValueMatch match in number.EnumerateMatches(quantity))
{
    Console.WriteLine(match.Index);  // 4
}
```

### Splitting without allocations

On .NET 10, `EnumerateSplits` gives the pieces as `Range` values instead of strings.

```csharp
Pattern comma = Pattern.With.Literal(",");
string csv = "a,b,,c";

foreach (Range part in comma.EnumerateSplits(csv))
{
    Console.WriteLine($"{part} -> \"{csv[part]}\"");
}
// prints:
// 0..1 -> "a"
// 2..3 -> "b"
// 4..4 -> ""
// 5..6 -> "c"
```

- Empty pieces are included, as with `Split`.
- Unlike `Split`, captured groups are **not** added to the result. Only the pieces between the matches are returned.

:::note

The package ships builds for `netstandard2.1`, `net8.0` and `net10.0`, and your project gets the closest one. A .NET 9 project uses the `net8.0` build, so `EnumerateSplits` is not available there, even though .NET 9's `Regex` has it. Projects on .NET Core 3.x to .NET 7 use the `netstandard2.1` build, which has none of the span APIs and no `UseNonBacktracking()`.

:::

## Errors and edge cases

| Situation | What happens |
|-----------|--------------|
| `input` is `null` | `ArgumentNullException` from every method (the parameter name is `input`) |
| `replacement` or `evaluator` is `null` | `ArgumentNullException` |
| `startAt` is below 0 or above `input.Length` | `ArgumentOutOfRangeException` |
| `Replace` `count` below `-1`, or `Split` `count` below 0 | `ArgumentOutOfRangeException` |
| The regex text is invalid | An `ArgumentException` when the pattern first runs (or on `ToRegex()`). On .NET 7 and later it is the derived `RegexParseException`. Catch `ArgumentException` to handle every target |
| The options are an invalid combination | `ArgumentOutOfRangeException` when the pattern first runs |
| The timeout elapses | `RegexMatchTimeoutException` |
| An empty pattern (`Pattern.With`, where `IsEmpty` is `true`) | Matches the empty string everywhere: `IsMatch` is `true` and `Count("ab")` is 3 |

An invalid regex usually comes from raw text added with `RegEx(...)`. Building the pattern does not parse it; the first match does:

```csharp
// regexText comes from your configuration, for example "(abc"
Pattern custom = Pattern.With.RegEx(regexText);

try
{
    custom.IsMatch("abc");
}
catch (ArgumentException exception)
{
    Console.WriteLine(exception.Message);  // Invalid pattern '(abc' at offset 4. Not enough )'s.
}
```

When the regex text is a constant in your code, the analyzer reports it at compile time as error FRX003, so the program never runs with it. To check text you load at run time before you use it, see [Validation and Debugging](./validation-and-debugging.md).

## Performance and thread safety

- **The `Regex` is cached per pattern instance.** The first call to a matching method builds it; later calls reuse it. Store patterns you use often in `static readonly` fields, so the cache is built once.
- **Equal patterns do not share a cache.** Two patterns built the same way are equal (`Equals` compares the regex text, the options and the timeout), but each instance builds its own `Regex`. Note that `==` is not overloaded: it still compares references, so `Pattern.With.Digit == Pattern.With.Digit` is `false` while `Equals` returns `true`.
- **Some calls skip the cache.** `IsMatch(input, options)`, `ToRegex()` and `ToRegex(options)` build a new `Regex` every time.
- **Patterns are safe to share between threads.** A pattern is immutable, and `Regex` matching methods are thread-safe. If two threads trigger the first use at the same moment, both may build a `Regex` and one is kept; the result is the same.
- **Use `Compiled` for hot patterns.** Because the cache keeps the compiled `Regex`, the extra start-up cost is paid once per pattern instance.
- **Use the span APIs on hot paths** on .NET 8 and later: `Count` and `EnumerateMatches` do not allocate `Match` objects.
- **Prefer `Count` over `Matches(...).Count`** when you only need the number.

```csharp
public static class OrderPatterns
{
    public static readonly Pattern OrderNumber = Pattern.With
        .StartOfInput
        .Literal("ORD-")
        .Digit.Repeat.Times(6)
        .EndOfInput
        .WithOptions(RegexOptions.Compiled)
        .WithTimeout(TimeSpan.FromMilliseconds(100));
}
```

Every call then reuses the one compiled `Regex`:

```csharp
Console.WriteLine(OrderPatterns.OrderNumber.IsMatch("ORD-004211"));  // True
Console.WriteLine(OrderPatterns.OrderNumber.IsMatch("ORD-42"));      // False
```

## Framework availability

| Member | netstandard2.1 | net8.0 | net10.0 |
|--------|:--------------:|:------:|:-------:|
| `IsMatch(string)`, `IsMatch(string, RegexOptions)` | Yes | Yes | Yes |
| `Match(string)`, `Match(string, int)`, `Matches(string)` | Yes | Yes | Yes |
| `Count(string)` | Yes | Yes | Yes |
| `Replace` (all overloads), `Split` (both overloads) | Yes | Yes | Yes |
| `ToRegex()`, `ToRegex(RegexOptions)` | Yes | Yes | Yes |
| `WithOptions`, `WithTimeout`, `Options`, `Timeout` | Yes | Yes | Yes |
| `UseNonBacktracking()` | No | Yes | Yes |
| `IsMatch(ReadOnlySpan<char>)`, `Count(ReadOnlySpan<char>)`, `EnumerateMatches` | No | Yes | Yes |
| `EnumerateSplits` | No | No | Yes |

To run these methods over a `TextReader` or a stream, see [Streaming](./streaming.md). To replace matches with masks, see [PII Redaction](./pii-redaction.md). To see step by step why an input matches or fails, see [Match Tracing](./match-tracing.md), and to measure how much a match backtracks, see [Backtracking Profiler](./backtracking-profiler.md).
