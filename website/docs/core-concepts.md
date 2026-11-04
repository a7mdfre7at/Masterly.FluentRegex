---
id: core-concepts
title: "Core Concepts"
description: "The Pattern type: starting a chain with Pattern.With, method chaining, immutability, automatic escaping, options, ToString/ToRegex and value equality."
---

Every regular expression you build with FluentRegex is a `Pattern`. You start one with `Pattern.With`, add pieces by chaining properties and methods, and read the result with `ToString()` or `ToRegex()`. This page explains the rules that every other page relies on: how a chain is built, why a pattern never changes once it exists, how text is escaped for you, which settings a pattern carries, and when two patterns are equal.

Read this page once before the rest of the Building Patterns section, which starts with [Basic Patterns](./basic-patterns.md). The building blocks themselves (anchors, character classes, quantifiers, groups) have their own pages.

All core types live in the `Masterly.FluentRegex` namespace. The .NET types `Regex`, `RegexOptions` and `Match` come from `System.Text.RegularExpressions`. Unless a section says otherwise, everything here works on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10.

## The Pattern type at a glance

`Pattern` is an immutable class. These are the members this page covers:

| Member | Kind | What it does |
|--------|------|--------------|
| `Pattern.With` | Static property | Returns a new, empty pattern. The usual start of a chain. |
| `new Pattern()` | Constructor | Same as `Pattern.With`. Prefer `Pattern.With` for readability. |
| `Pattern.FromRegex(regex, options)` | Static method | Wraps an existing regex string after validating it. |
| `IsEmpty` | Property | `true` when the pattern has no elements. |
| `RegEx(text)` | Method | Appends raw regex text, without escaping. |
| `Literal(text)`, `Literal(params char[])` | Method | Appends text that is matched literally. Special characters are escaped. |
| `Then(pattern)`, `+` | Method, operator | Appends all elements of another pattern. |
| `Repeat` | Property | Starts a quantifier for the last element. Returns a `Repeat`, not a `Pattern`. |
| `Options`, `Timeout` | Properties | The `RegexOptions` and match timeout the pattern runs with. |
| `WithOptions(options)`, `WithTimeout(timeout)`, `UseNonBacktracking()` | Methods | Return a copy with different execution settings. |
| `ToString()` | Method | Returns the regular expression text. |
| `ToRegex()`, `ToRegex(options)` | Methods | Create a `System.Text.RegularExpressions.Regex`. |
| `Equals(...)`, `GetHashCode()` | Methods | Value equality: same regex text, options and timeout. |

There are no implicit conversions. A `string` never turns into a `Pattern` by itself, and a `Pattern` never turns into a `Regex` or a `string` by itself. You always call `Pattern.FromRegex`, `RegEx`, `ToString()` or `ToRegex()`.

## Starting a pattern

### Pattern.With

Every chain starts with `Pattern.With`. It returns a new, empty pattern, and each property or method after it adds one piece:

```csharp
using Masterly.FluentRegex;

Pattern pattern = Pattern.With
    .Digit
    .Literal("@")
    .Word;

string regex = pattern.ToString();       // \d@\w
bool matches = pattern.IsMatch("7@a");   // true
```

- `Pattern.With` is a property, but it is not a shared singleton. Every read creates a new empty pattern.
- `Digit` and `Word` are properties, so they have no parentheses. Members that need an argument, such as `Literal("@")`, are methods.
- You can call `IsMatch`, `Match`, `Replace` and the other matching methods directly on the pattern. See [Matching](./matching.md).

### Empty patterns

A pattern with no elements is valid. It renders as an empty string, and an empty regex matches every input at position 0.

```csharp
Pattern empty = Pattern.With;

bool isEmpty = empty.IsEmpty;                                    // true
string text = empty.ToString();                                  // "" (an empty string)
bool matchesAnything = empty.IsMatch("abc");                     // true
bool sameInstance = ReferenceEquals(Pattern.With, Pattern.With); // false
bool constructorToo = new Pattern().IsEmpty;                     // true
```

- Use `IsEmpty` to check whether a pattern you built conditionally received any elements before you match with it.
- A quantifier needs something to repeat. `Pattern.With.Repeat.OneOrMore` throws `InvalidOperationException`. See [Errors and compile-time checks](#errors-and-compile-time-checks).

### Starting from an existing regex

When you already have a regex string, wrap it with `Pattern.FromRegex`. It validates the text, so a typo fails right away instead of at the first match. The result is a normal pattern that you can extend:

```csharp
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern date = Pattern.FromRegex(@"\d{4}-\d{2}-\d{2}");

Pattern utcDate = Pattern.With.StartOfLine.Then(date).Literal("Z").EndOfLine;

string regex = utcDate.ToString();             // ^\d{4}-\d{2}-\d{2}Z$
bool matches = utcDate.IsMatch("2026-11-03Z"); // true

Pattern caseless = Pattern.FromRegex("^abc$", RegexOptions.IgnoreCase);

RegexOptions options = caseless.Options; // IgnoreCase
bool upper = caseless.IsMatch("ABC");    // true
```

- `FromRegex(regex, options)` stores the options on the pattern. `options` defaults to `RegexOptions.None`. The options are also used for validation, so syntax that depends on an option is checked correctly.
- An invalid regex throws `ArgumentException` whose message starts with `The regular expression is invalid:`. A `null` regex throws `ArgumentNullException`.
- The wrapped regex becomes one raw element, exactly like `RegEx(...)`. A quantifier written right after it is appended to the text, so it repeats only the last atom of the regex (see [Quantifiers apply to the last element](#quantifiers-apply-to-the-last-element)). Wrap it with `Group(...)` or `OneOf(...)` to repeat the whole thing.
- To turn a regex into readable fluent code instead of wrapping it, see [Regex to Fluent Code](./regex-to-fluent-code.md).

## Method chaining

Each member returns a new `Pattern`, so you write the whole expression as one chain. Put one concept on each line and the chain reads top to bottom like the regex it builds:

```csharp
Pattern email = Pattern.With
    .StartOfLine                                      // ^
    .Set(Pattern.With.Letter.Digit.Literal("._-"))    // [a-zA-Z\d\._\-]
    .Repeat.OneOrMore                                 // +
    .Literal("@")                                     // @
    .Set(Pattern.With.Letter.Digit.Literal(".-"))     // [a-zA-Z\d\.\-]
    .Repeat.OneOrMore                                 // +
    .Literal(".")                                     // \.
    .Letter.Repeat.AtLeast(2)                         // [a-zA-Z]{2,}
    .EndOfLine;                                       // $

string regex = email.ToString();                       // ^[a-zA-Z\d\._\-]+@[a-zA-Z\d\.\-]+\.[a-zA-Z]{2,}$
bool valid = email.IsMatch("jane.doe@example.com");    // true
bool noTopLevelDomain = email.IsMatch("jane@example"); // false
```

- Members such as `Set(...)`, `Group(...)` and `OneOf(...)` take another pattern as their content. You build that inner pattern with its own `Pattern.With` chain.
- `Repeat` is the one step in a chain that is not a `Pattern`. It returns a `Repeat` object, and its members (`OneOrMore`, `ZeroOrMore`, `Optional`, `Times(n)`, `Times(min, max)`, `AtLeast(n)`, `AtMost(n)`) return the `Pattern`. See [Quantifiers](./quantifiers.md).
- Settings calls such as `WithOptions(...)` also return a `Pattern`, so the chain can continue after them.

:::note

This is the email sample from the README, with the real output. The README's comment shows the character class as `[a-zA-Z\d._-]`. Since 3.0 every character from `Literal` is escaped inside a set, so the actual output is `[a-zA-Z\d\._\-]`. Both match the same characters.

:::

### Quantifiers apply to the last element

A quantifier, `.Lazy` and `.Possessive` change the **last element** of the pattern, not everything before it. What counts as one element depends on how you added it:

```csharp
Pattern lastLetterOnly = Pattern.With.Literal("ab").Repeat.OneOrMore;                     // ab+
Pattern wholeWordGroup = Pattern.With.Group(Pattern.With.Literal("ab")).Repeat.OneOrMore; // (ab)+
Pattern wholeWord = Pattern.With.OneOf("ab").Repeat.OneOrMore;                            // (?:ab)+
Pattern rawText = Pattern.With.RegEx("ab").Repeat.OneOrMore;                              // ab+
Pattern appended = Pattern.With.Literal("x").Then(Pattern.With.Literal("ab")).Repeat.OneOrMore; // xab+
```

- `Literal("ab")` adds one element per character, so only `b` is repeated.
- `Group(...)` and `OneOf(...)` wrap their content in one element, so the whole word is repeated. `OneOf` uses a non-capturing group.
- `RegEx("ab")` adds one element, but the quantifier is written right after it. The regex engine then repeats only `b`, exactly as it would in a hand-written `ab+`.
- `Then(...)` appends the other pattern's elements one by one, as if you had written them inline. A quantifier after it applies to the other pattern's last element.
- A quantifier directly after another quantifier wraps the first in a non-capturing group: `Pattern.With.Digit.Repeat.Times(2).Repeat.Times(3)` builds `(?:\d{2}){3}`.
- `Repeat.Optional` follows the same rule: `Pattern.With.Digit.Repeat.OneOrMore.Repeat.Optional` builds `(?:\d+)?`, an optional repetition. Write `.Lazy` for a lazy quantifier: `Pattern.With.Digit.Repeat.OneOrMore.Lazy` builds `\d+?`.

## Immutability

A pattern never changes after it is created. Every property and method returns a **new** pattern and leaves the original as it was. This is the most important rule in the library.

### Reusing building blocks

Because a pattern cannot change, you can store one in a variable and use it in as many other patterns as you like:

```csharp
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

Pattern price = Pattern.With.Literal("$").Then(digits);
Pattern year = digits.Literal("AD");

string digitsRegex = digits.ToString(); // \d+  (unchanged)
string priceRegex = price.ToString();   // \$\d+
string yearRegex = year.ToString();     // \d+AD
```

- `digits.Literal("AD")` returned a new pattern. `digits` still renders `\d+`.
- `Then(digits)` copies the elements of `digits` into the new pattern. Later changes to the new pattern cannot affect `digits`.
- For larger compositions (operators, reusable fragments, templates), see [Composing Patterns](./composing-patterns.md).

### Branching from a shared prefix

You can continue the same pattern in several directions. Each branch gets its own copy:

```csharp
Pattern prefix = Pattern.With.StartOfLine.Literal("ID-");

Pattern numericId = prefix.Digit.Repeat.Times(4).EndOfLine;
Pattern letterId = prefix.UppercaseLetter.Repeat.Times(2).EndOfLine;

string prefixRegex = prefix.ToString();     // ^ID-
string numericRegex = numericId.ToString(); // ^ID-\d{4}$
string letterRegex = letterId.ToString();   // ^ID-[A-Z]{2}$
```

### Always use the returned pattern

Because nothing changes in place, a call whose result you ignore has no effect. This is the one mistake immutability makes possible:

```csharp
Pattern pattern = Pattern.With.Literal("abc");

pattern.WithOptions(RegexOptions.IgnoreCase);           // the result is thrown away
bool before = pattern.IsMatch("ABC");                   // false

pattern = pattern.WithOptions(RegexOptions.IgnoreCase); // keep the new pattern
bool after = pattern.IsMatch("ABC");                    // true
```

:::warning

In 2.x, every call changed the pattern and returned the same instance, so a statement such as `p.WithOptions(...);` worked. Since 3.0 it does nothing. Assign the result: `p = p.WithOptions(...)`. The compiler and the analyzer do not warn about an ignored result. See [Upgrading from 2.x](./upgrading-from-2x.md).

:::

### Sharing patterns across threads

An immutable pattern is safe to share. The usual way to use one is a `static readonly` field that is built once and read from any thread:

```csharp
public static class Patterns
{
    public static readonly Pattern Sku = Pattern.With
        .StartOfLine
        .Literal("SKU-")
        .Digit.Repeat.Times(4)
        .EndOfLine;
}

bool valid = Patterns.Sku.IsMatch("SKU-0042"); // true
```

- Many threads can call `IsMatch`, `Match`, `Replace` and the other matching methods on the same pattern at the same time.
- A pattern caches its regex text the first time you call `ToString()`, and its compiled `Regex` the first time you match with it. The caches are filled without a lock. Two threads that race on first use may each build the value once. Both results are identical, so this is harmless.
- Because a pattern never changes, its cached `Regex` can never be out of date. In 2.x, matching and then extending a pattern could keep using the old regex. That bug cannot happen in 3.x.
- To skip building the chain at run time completely, use `[GeneratePattern]`. See [Compile-Time Checks](./compile-time-checks.md).

:::tip

Build patterns once and keep them, for example in `static readonly` fields. A pattern built inside a method that runs often creates a new `Pattern`, renders it and compiles a new `Regex` on every call.

:::

### How it works

Internally a pattern is a persistent, immutable linked list of elements (tokens). Adding an element creates one new node that points at the previous ones, so:

- Extending a pattern takes constant time and shares memory with the original. A long chain does not copy itself at every step.
- `Then(other)` and `+` add one node per element of `other`.
- The options and timeout are stored on each pattern, next to the list.

## Automatic escaping

`Literal(...)` matches text exactly as you wrote it. Every character that means something special in a regex is escaped for you, so you never have to remember which characters need a backslash.

```csharp
Pattern price = Pattern.With.Literal("$100.00");          // \$100\.00
Pattern brackets = Pattern.With.Literal("[test]");         // \[test\]
Pattern quantifiers = Pattern.With.Literal("a+b*c?");      // a\+b\*c\?
Pattern path = Pattern.With.Literal(@"C:\temp\{x}|(y)^");  // C:\\temp\\\{x\}\|\(y\)\^
Pattern controls = Pattern.With.Literal("a\tb\nc\r");      // a\tb\nc\r
Pattern characters = Pattern.With.Literal('$', '.', '-');  // \$\.-

bool exact = price.IsMatch("$100.00");   // true
bool anyChar = price.IsMatch("$100500"); // false: the dot is not a wildcard
```

There are two overloads:

| Overload | Use it for |
|----------|------------|
| `Literal(string literalChars)` | Words, separators and any fixed text. |
| `Literal(params char[] characters)` | One or more single characters, such as `Literal('$', '.')`. |

The parameter names are shown as they appear in the source. They matter only if you pass the argument by name.

Both add **one element per character**, which matters for [quantifiers](#quantifiers-apply-to-the-last-element). A `null` argument throws `ArgumentNullException`. An empty string adds nothing.

Which characters are escaped depends on where the literal ends up:

| Where | Escaped with a backslash | Left as is |
|-------|--------------------------|------------|
| Normal position | `^ $ . \| { } [ ] ( ) * + ? \` | Everything else, including `-`, `/`, `#` and space |
| Inside `Set(...)` or `NegatedSet(...)` | `^ $ . \| { } [ ] ( ) * + ? \ -` | Everything else |

Control characters are written as escapes in both positions:

| Character | Rendered as |
|-----------|-------------|
| Tab, newline, carriage return | `\t`, `\n`, `\r` |
| Form feed, vertical tab, escape (U+001B) | `\f`, `\v`, `\e` |
| Any other control character | `\uXXXX`, for example `\u0001` |

### Literals inside a set

Inside a character class, a `-` between two characters forms a range. `Literal` escapes it, so the characters you pass are always matched as themselves. Use `Range(from, to)` when you want a range:

```csharp
Pattern hexLetters = Pattern.With.Set(Pattern.With.Literal("a-f"));  // [a\-f]
Pattern specials = Pattern.With.Set(Pattern.With.Literal(@"^]-\"));  // [\^\]\-\\]
Pattern range = Pattern.With.Set(Pattern.With.Range('a', 'f'));      // [a-f]

bool dashMatches = hexLetters.IsMatch("-"); // true
bool cMatches = hexLetters.IsMatch("c");    // false: a\-f is three characters, not a range
bool cInRange = range.IsMatch("c");         // true
```

More about sets, ranges and the shorthand classes is on [Character Classes](./character-classes.md).

### Raw regex with RegEx

`RegEx(text)` adds regex text exactly as written, with no escaping. Use it for syntax the fluent API does not cover, or to paste a fragment you already trust. You can mix raw and literal pieces in one chain:

```csharp
Pattern fileName = Pattern.With
    .RegEx("[a-z]+")    // raw regex, added as written
    .Literal(".txt");   // literal text, escaped

string regex = fileName.ToString();           // [a-z]+\.txt
bool matches = fileName.IsMatch("notes.txt"); // true
```

- `RegEx` does not validate its text. An invalid fragment is only detected when the regex is created, at the first match or `ToRegex()` call, which throws `RegexParseException` (an `ArgumentException`). Use `Pattern.FromRegex` when you want validation up front. When the text is a constant, the analyzer already reports it at build time as `FRX003`.
- `RegEx(null)` and `RegEx("")` do not throw. They add an **empty element** that renders nothing. The pattern is then no longer `IsEmpty`, and a quantifier right after it has nothing to repeat: `Pattern.With.RegEx("").Repeat.OneOrMore` renders the invalid regex `+`. This differs from `Literal("")`, which adds no element at all.
- On .NET 8 and .NET 10 the parameter is marked with `[StringSyntax(StringSyntaxAttribute.Regex)]`, so Visual Studio and Rider color the regex inside the string.
- Never pass user input to `RegEx`. Pass it to `Literal`, which escapes it.

### Literals and IgnorePatternWhitespace

With `RegexOptions.IgnorePatternWhitespace`, .NET ignores unescaped white space and treats `#` as the start of a comment. `Literal` escapes both when the pattern has that option, so the literal still matches its own text:

```csharp
Pattern spaced = Pattern.With.Literal("a b").WithOptions(RegexOptions.IgnorePatternWhitespace);

string regex = spaced.ToString();         // a\ b
bool withSpace = spaced.IsMatch("a b");   // true
bool withoutSpace = spaced.IsMatch("ab"); // false

Pattern hashed = Pattern.With.Literal("a#b").WithOptions(RegexOptions.IgnorePatternWhitespace);

bool hashMatches = hashed.IsMatch("a#b"); // true
```

Without the option, `Literal` writes spaces and `#` as they are, because they are ordinary characters in a normal regex.

## Options and timeout

A pattern carries two execution settings next to its regex text. Every matching method and `ToRegex()` uses them.

| Property | Default | Change it with |
|----------|---------|----------------|
| `Options` | `RegexOptions.None` | `WithOptions(options)`, `UseNonBacktracking()` |
| `Timeout` | `Regex.InfiniteMatchTimeout` (no timeout) | `WithTimeout(timeout)` |

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern word = Pattern.With.WordBoundary.Literal("cat").WordBoundary;

Pattern caseless = word.WithOptions(RegexOptions.IgnoreCase);
Pattern replaced = caseless.WithOptions(RegexOptions.Multiline);
Pattern combined = word.WithOptions(RegexOptions.IgnoreCase | RegexOptions.Multiline);
Pattern limited = caseless.WithTimeout(TimeSpan.FromMilliseconds(250));

bool exactCase = word.IsMatch("CAT");              // false
bool anyCase = caseless.IsMatch("CAT");            // true
RegexOptions replacedOptions = replaced.Options;   // Multiline (IgnoreCase is gone)
RegexOptions combinedOptions = combined.Options;   // IgnoreCase, Multiline
TimeSpan timeout = limited.Timeout;                // 00:00:00.2500000
RegexOptions limitedOptions = limited.Options;     // IgnoreCase (kept)
```

- `WithOptions` **replaces** the options. It does not add to them. Combine flags with `|` in one call.
- `WithTimeout` keeps the options, and `WithOptions` keeps the timeout.
- `WithTimeout` accepts a positive value below about 24 days, or `Regex.InfiniteMatchTimeout`. Zero, a negative value or a larger value throws `ArgumentOutOfRangeException`. When the timeout elapses during a match, .NET throws `RegexMatchTimeoutException`.
- `UseNonBacktracking()` adds `RegexOptions.NonBacktracking` to the current options. It exists only on .NET 8 and .NET 10. The non-backtracking engine does not support backreferences, lookarounds, atomic groups (including `.Possessive`), conditionals or `RightToLeft`, and throws `NotSupportedException` when such a pattern runs.
- A timeout or the non-backtracking engine is how you protect a pattern that sees untrusted input. See [ReDoS Safety](./redos-safety.md).

Settings stay on the pattern while you keep chaining, so you can set them anywhere in the chain:

```csharp
Pattern id = Pattern.With.Literal("id")
    .WithOptions(RegexOptions.IgnoreCase)
    .Digit.Repeat.OneOrMore;   // keeps going after WithOptions

RegexOptions options = id.Options; // IgnoreCase
bool matches = id.IsMatch("ID42"); // true
```

### Settings of building blocks are not carried over

Options and the timeout belong to the pattern you run, not to the pieces inside it. When you compose patterns, the settings of the inner patterns are dropped:

```csharp
Pattern code = Pattern.With.Literal("abc").WithOptions(RegexOptions.IgnoreCase);

Pattern appended = Pattern.With.Literal("x").Then(code);
Pattern grouped = Pattern.With.Group(code);
Pattern either = code | Pattern.With.Literal("y");
Pattern codeFirst = code + Pattern.With.Literal("x");

RegexOptions appendedOptions = appended.Options;   // None
bool appendedMatches = appended.IsMatch("xABC");   // false
RegexOptions groupedOptions = grouped.Options;     // None
RegexOptions eitherOptions = either.Options;       // IgnoreCase: the left side's settings
RegexOptions codeFirstOptions = codeFirst.Options; // IgnoreCase: the left side's settings
```

| Composition | Settings of the result |
|-------------|------------------------|
| `outer.Then(inner)`, `outer + inner` | Those of `outer` (the left side) |
| `outer.Group(inner)`, `outer.Set(inner)` and other wrapping members | Those of `outer` |
| `left \| right` | Those of `left`, like `+` |

When only part of a pattern should ignore case, use an inline option scope instead. It is written into the regex text, so it survives composition:

```csharp
Pattern scoped = Pattern.With.Literal("x").IgnoreCase(Pattern.With.Literal("abc"));

string regex = scoped.ToString();               // x(?i:abc)
bool matches = scoped.IsMatch("xABC");          // true
bool outerStillExact = scoped.IsMatch("XABC");  // false
```

`IgnoreCase(...)`, `CaseSensitive(...)` and `InlineOptions(...)` are covered on [Composing Patterns](./composing-patterns.md).

## Getting the regex out

### ToString()

`ToString()` returns the regular expression text. String interpolation and `Console.WriteLine(pattern)` call it for you. It contains only the regex: the options and timeout are not part of the text.

```csharp
Pattern pattern = Pattern.With
    .StartOfLine
    .Digit.Repeat.OneOrMore
    .Literal("-")
    .Word.Repeat.Times(3, 5)
    .EndOfLine
    .WithOptions(RegexOptions.IgnoreCase)
    .WithTimeout(TimeSpan.FromSeconds(2));

string regexText = pattern.ToString();       // ^\d+-\w{3,5}$
string interpolated = $"Pattern: {pattern}"; // Pattern: ^\d+-\w{3,5}$
```

- The text is rendered once and cached on the pattern. Later calls return the same string.
- Use the text when another API needs a plain regex string, such as a `[RegularExpression]` attribute, a config file or a database. To export to other regex dialects, see [Export](./export.md).
- In the debugger, a pattern is shown as its regex text. Expanding it shows the options, timeout, group names, a plain-English explanation and a ReDoS verdict.

### ToRegex()

`ToRegex()` creates a `System.Text.RegularExpressions.Regex` for APIs that need one:

```csharp
// pattern is the pattern from the previous sample
Regex regex = pattern.ToRegex();
RegexOptions options = regex.Options;      // IgnoreCase
TimeSpan timeout = regex.MatchTimeout;     // 00:00:02

Regex multiline = pattern.ToRegex(RegexOptions.Multiline);
RegexOptions explicitOptions = multiline.Options; // Multiline (replaces IgnoreCase)
TimeSpan sameTimeout = multiline.MatchTimeout;    // 00:00:02

bool freshInstance = ReferenceEquals(pattern.ToRegex(), pattern.ToRegex()); // false
```

| Overload | Options used | Timeout used |
|----------|--------------|--------------|
| `ToRegex()` | The pattern's `Options` | The pattern's `Timeout` |
| `ToRegex(options)` | `options`, instead of the pattern's own | The pattern's `Timeout` |

- Each call creates a **new** `Regex`. Keep the result if you need it more than once.
- You often do not need `ToRegex()` at all. `IsMatch`, `Match`, `Matches`, `Replace`, `Split` and `Count` on the pattern use an internal `Regex` that is created on first use and then reused.
- The matching overloads that take a `RegexOptions` argument, such as `IsMatch(input, options)`, create a new `Regex` on every call. Prefer `WithOptions` once, then match.
- An invalid regex (for example from `RegEx(...)`) throws when the `Regex` is created.

## Equality

Two patterns are equal when they render the **same regex text** with the **same options** and the **same timeout**, no matter how they were built:

```csharp
Pattern fluent = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;
Pattern parsed = Pattern.FromRegex(@"^\d+$");

bool equal = fluent.Equals(parsed);                           // true
bool sameHash = fluent.GetHashCode() == parsed.GetHashCode(); // true
bool sameReference = ReferenceEquals(fluent, parsed);         // false

bool otherOptions = fluent.Equals(Pattern.FromRegex(@"^\d+$", RegexOptions.IgnoreCase)); // false
bool otherTimeout = fluent.Equals(parsed.WithTimeout(TimeSpan.FromSeconds(1)));          // false
bool notAString = fluent.Equals(@"^\d+$");                                               // false
```

- `Pattern` implements `IEquatable<Pattern>`. `Equals(object)` and `Equals(Pattern)` behave the same. A `null` or an object of another type is never equal.
- The regex text is compared ordinally (exact characters, case-sensitive).
- `Pattern.With.Literal("a")` and `Pattern.With.RegEx("a")` are equal, because both render `a`.
- Two empty patterns are equal. Because only the text counts, `Pattern.With.RegEx("")` is also equal to an empty pattern, even though its `IsEmpty` is `false`.

:::warning

The `==` and `!=` operators are **not** overloaded. They compare references, so `fluent == parsed` is `false` in the example above. Use `Equals` to compare values.

:::

### Equal text, not equal meaning

Equality compares text. Two patterns that match exactly the same strings but are written differently are not equal. To compare what patterns match, use [Pattern Algebra](./pattern-algebra.md):

```csharp
Pattern left = Pattern.FromRegex("[0-9]");
Pattern right = Pattern.FromRegex("[0123456789]");

bool sameText = left.Equals(right);             // false
bool sameLanguage = left.IsEquivalentTo(right); // true
```

### Patterns as dictionary keys

Hash codes agree with equality, so patterns work as keys in dictionaries and members of hash sets:

```csharp
using System.Collections.Generic;
using Masterly.FluentRegex;

Dictionary<Pattern, string> labels = new Dictionary<Pattern, string>
{
    [Pattern.FromRegex(@"\d+")] = "digits",
};

string label = labels[Pattern.With.Digit.Repeat.OneOrMore]; // digits

HashSet<Pattern> unique = new HashSet<Pattern> { Pattern.With.Digit, Pattern.FromRegex(@"\d"), Pattern.With.Word };
int count = unique.Count; // 2
```

- Value equality is also why patterns work well in Entity Framework Core queries. EF Core caches a compiled query by comparing its constants with `Equals`, so a freshly built but equal pattern reuses the cached query. See [Entity Framework Core](./entity-framework-core.md).
- `Equals` and `GetHashCode` render the pattern on first use. The text is cached, so later comparisons are cheap.

## Errors and compile-time checks

The builder checks its arguments as you build. A mistake throws right away, at the call that caused it, instead of producing a broken regex:

| Exception | Thrown when | Example |
|-----------|-------------|---------|
| `ArgumentNullException` | A required argument is `null`. | `Literal((string)null)`, `Then(null)`, `Set(null)`, `Group(null)`, `null + pattern`, `null \| pattern`, `FromRegex(null)` |
| `InvalidOperationException` | A quantifier has nothing to repeat, or `.Lazy` / `.Possessive` does not follow a quantifier. | `Pattern.With.Repeat.OneOrMore`, `Pattern.With.Digit.Lazy`, `Pattern.With.Digit.Possessive` |
| `ArgumentOutOfRangeException` | A count or a timeout is out of range. | `Repeat.Times(-1)`, `Repeat.Times(5, 2)`, `WithTimeout(TimeSpan.Zero)` |
| `ArgumentException` | `FromRegex` receives an invalid regex, or another argument is invalid. | `Pattern.FromRegex("(unclosed")`, `Pattern.With.OneOf(new string[0])` |
| `RegexParseException` (an `ArgumentException`) | Raw text from `RegEx(...)` is invalid. Thrown when the `Regex` is created, not when the pattern is built. | `Pattern.With.RegEx("(unclosed").IsMatch(input)` |

The messages tell you how to fix the call. For example, `Pattern.With.Digit.Lazy` throws `InvalidOperationException` with the message `Lazy must directly follow a quantifier, for example Pattern.With.Digit.Repeat.OneOrMore.Lazy.`

:::tip

The package includes a Roslyn analyzer that evaluates chains built from constants while you type. A chain that would throw fails the build with error `FRX006`, and a chain that produces an invalid regex fails with error `FRX003`, so most of the mistakes above never reach run time. See [Compile-Time Checks](./compile-time-checks.md).

:::

## Availability by target framework

Everything on this page works on .NET Standard 2.1, .NET 8 and .NET 10, with these exceptions:

| Feature | .NET Standard 2.1 | .NET 8 | .NET 10 |
|---------|-------------------|--------|---------|
| `Pattern.With`, chaining, `Literal`, `RegEx`, `Then`, `FromRegex` | Yes | Yes | Yes |
| `WithOptions`, `WithTimeout`, `ToString()`, `ToRegex()`, equality | Yes | Yes | Yes |
| `UseNonBacktracking()` | No | Yes | Yes |
| Regex syntax coloring for `RegEx(...)` and `FromRegex(...)` arguments | No | Yes | Yes |

## Summary

- Start every pattern with `Pattern.With`, or wrap an existing regex with `Pattern.FromRegex`.
- Every call returns a new pattern. Keep the result, and share building blocks freely.
- Use `Literal` for text and `RegEx` only for regex syntax you trust. Never pass user input to `RegEx`.
- Options and the timeout belong to the outermost pattern. Use inline scopes such as `IgnoreCase(...)` for parts of a pattern.
- `ToString()` gives you the regex text, `ToRegex()` a new `Regex`. The matching methods on the pattern reuse one cached `Regex`.
- `Equals` compares regex text, options and timeout. `==` compares references.

Next, learn the individual building blocks on [Basic Patterns](./basic-patterns.md), or see more ways to combine patterns on [Composing Patterns](./composing-patterns.md).
