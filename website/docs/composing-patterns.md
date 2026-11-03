---
id: composing-patterns
title: "Composing Patterns"
description: "Reuse patterns as building blocks: join them with Then and +, offer alternatives with OneOf and |, scope options inline, wrap existing regexes and compare patterns by value."
---

A `Pattern` is immutable, so any pattern you build can be stored once and reused inside other patterns. Composing is how you keep large expressions readable: you name the small parts (a year, a separator, a region code) and join them into the larger formats your application validates.

This page covers every way to put patterns together:

| Member | Renders | Use it when |
|--------|---------|-------------|
| `a.Then(b)` | `ab` | You want to append a stored pattern in the middle of a fluent chain. |
| `a + b` | `ab` | You join stored patterns into a sequence. Same result as `Then`. |
| `a \| b` | `(?:a\|b)` | You want either pattern, without a numbered group. Chains flatten into one group. |
| `.OneOf(patterns)` / `.OneOf(words)` | `(?:a\|b)` | Same as `\|`, as a fluent method, or from a list of literal words. |
| `.Choice(a, b, ...)` | `(a\|b)` | You want either pattern **and** need the choice as a numbered capture. |
| `.IgnoreCase(p)`, `.CaseSensitive(p)`, `.InlineOptions(on, p, off)` | `(?i:...)` | Only part of the pattern needs different options. |
| `Pattern.FromRegex(regex, options)` | the regex as-is | You already have a regex string and want to use it as a building block. |
| `.RegEx(regex)` | the regex as-is | You want to add raw regex text in the middle of a fluent chain. |
| `Equals`, `GetHashCode` | | You compare patterns or use them as dictionary keys. |

All members on this page live in the `Masterly.FluentRegex` namespace and are available on every target framework of the package: `netstandard2.1`, `net8.0` and `net10.0`. Groups such as `Group`, `NamedGroup` and `NonCapturingGroup` are covered in [Groups and Backreferences](./groups-and-backreferences.md).

## Patterns are immutable building blocks

Every member returns a new pattern and leaves the original unchanged. You can extend the same pattern in two directions and both results stay independent.

```csharp
using Masterly.FluentRegex;

Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;

Pattern price = Pattern.With.Literal("$").Then(digits);
Pattern year = digits.Literal("AD");

string digitsRegex = digits.ToString(); // \d+
string priceRegex = price.ToString();   // \$\d+
string yearRegex = year.ToString();     // \d+AD
```

- `digits` is still `\d+` after both patterns were built from it.
- Patterns are safe to share between threads, so a `static readonly` field is the natural home for a building block.
- Internally a pattern is a persistent list of elements. Adding one element is O(1) and shares the elements of the original, so building many variants of one block is cheap.

Because nothing is changed in place, a call whose result you ignore has no effect. Assign the result instead:

```csharp
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern pattern = Pattern.With.Literal("hello");

pattern.WithOptions(RegexOptions.IgnoreCase);           // result ignored: pattern is unchanged
bool ignored = pattern.IsMatch("HELLO");                // false

pattern = pattern.WithOptions(RegexOptions.IgnoreCase); // assign the new pattern
bool assigned = pattern.IsMatch("HELLO");               // true
```

:::note

In 2.x every call changed and returned the same instance, so `p.WithOptions(...);` as a statement worked. Since 3.0 you must assign the result. See [Upgrading from 2.x](./upgrading-from-2x.md).

:::

### A library of building blocks

Group the blocks of one domain in a static class. Small blocks get names, and the formats you validate are joined from them.

```csharp
using Masterly.FluentRegex;

public static class OrderPatterns
{
    public static readonly Pattern Dash = Pattern.With.Literal("-");
    public static readonly Pattern Year = Pattern.With.Digit.Repeat.Times(4);
    public static readonly Pattern Sequence = Pattern.With.Digit.Repeat.Times(6);
    public static readonly Pattern Region = Pattern.With.OneOf("EU", "US", "APAC");

    public static readonly Pattern OrderNumber =
        Pattern.With.Literal("ORD") + Dash + Region + Dash + Year + Dash + Sequence;

    public static readonly Pattern InvoiceNumber =
        Pattern.With.Literal("INV") + Dash + Year + Dash + Sequence;

    public static readonly Pattern AnyReference =
        Pattern.With.StartOfInput.Then(OrderNumber | InvoiceNumber).EndOfInput;
}
```

Use the composed patterns like any other pattern:

```csharp
string order = OrderPatterns.OrderNumber.ToString();             // ORD-(?:EU|US|APAC)-\d{4}-\d{6}
string reference = OrderPatterns.AnyReference.ToString();        // \A(?:ORD-(?:EU|US|APAC)-\d{4}-\d{6}|INV-\d{4}-\d{6})\z

bool isOrder = OrderPatterns.OrderNumber.IsMatch("ORD-EU-2026-000042");     // true
bool isReference = OrderPatterns.AnyReference.IsMatch("INV-2026-000042");   // true
bool badRegion = OrderPatterns.AnyReference.IsMatch("ORD-XX-2026-000042");  // false
```

- Small blocks stay unanchored so they can be joined anywhere. Add anchors only on the final pattern you validate with, as `AnyReference` does.
- A pattern caches its regex string and its compiled `Regex` the first time you use it. Storing the composed pattern in a field means it is built and compiled once, not on every call.
- To build the chain at compile time instead of at startup, see [Compile-time checks](./compile-time-checks.md). The `[GeneratePattern]` source generator accepts the `+` and `|` operators and other static patterns.

:::warning

Static fields are initialized in the order they are written. A field that uses a block declared **below** it sees `null`, and the first use of the class throws a `TypeInitializationException`. Its inner exception is an `ArgumentNullException` that names the parameter that received `null`: `pattern` for `Then`, `left` or `right` for the `+` and `|` operators (for example `Value cannot be null. (Parameter 'right')`). Declare the small blocks first, as above.

:::

## Joining patterns in sequence

`Then` and the `+` operator append one pattern after another. They produce the same result, so pick the one that reads better at the call site.

```csharp
using Masterly.FluentRegex;

Pattern area = Pattern.With.Digit.Repeat.Times(3);
Pattern dash = Pattern.With.Literal("-");
Pattern line = Pattern.With.Digit.Repeat.Times(4);

Pattern withThen = area.Then(dash).Then(area).Then(dash).Then(line);
Pattern withPlus = area + dash + area + dash + line;

string thenRegex = withThen.ToString();  // \d{3}-\d{3}-\d{4}
string plusRegex = withPlus.ToString();  // \d{3}-\d{3}-\d{4}
bool same = withThen.Equals(withPlus);   // true
```

`Then` is most useful in the middle of a fluent chain, where an operator would need extra parentheses:

```csharp
// withPlus is the pattern from the previous sample
Pattern phone = Pattern.With.StartOfLine.Then(withPlus).EndOfLine;

string regex = phone.ToString();                       // ^\d{3}-\d{3}-\d{4}$
bool exact = phone.IsMatch("555-010-4477");            // true
bool embedded = phone.IsMatch("call 555-010-4477");    // false
```

| Member | Signature | Returns | Throws |
|--------|-----------|---------|--------|
| `Then` | `Pattern Then(Pattern pattern)` | A new pattern: this pattern's elements, then all elements of `pattern`. | `ArgumentNullException` when `pattern` is `null`. |
| `+` | `static Pattern operator +(Pattern left, Pattern right)` | The same as `left.Then(right)`. | `ArgumentNullException` when either side is `null` (parameter `left` or `right`). |

- Joining with an empty pattern is allowed and adds nothing: `Pattern.With + Pattern.With.Literal("a")` renders `a`.
- `Then` copies references to the elements of the right pattern, so its cost grows with the length of the right pattern, not with the length of the text it renders. Nothing is re-rendered until you call `ToString()` or match.
- `"Pattern: " + pattern` still produces a `string`. `Pattern` has no implicit conversion to `string`, so the C# string concatenation rule applies and calls `ToString()`.

### Quantifiers after a join apply to the last element

`Then` and `+` append the right pattern's elements one by one, exactly as if you had written them inline. A quantifier that follows repeats only the **last** element, not the whole joined block.

```csharp
using Masterly.FluentRegex;

Pattern ab = Pattern.With.Literal("a") + Pattern.With.Literal("b");

Pattern lastOnly = ab.Repeat.OneOrMore;
Pattern whole = Pattern.With.NonCapturingGroup(ab).Repeat.OneOrMore;

string lastOnlyRegex = lastOnly.ToString();        // ab+
string wholeRegex = whole.ToString();              // (?:ab)+
string lastOnlyMatch = lastOnly.Match("abab").Value; // ab
string wholeMatch = whole.Match("abab").Value;     // abab
```

To repeat, make optional or quantify a whole block, wrap it in `NonCapturingGroup` (or `Group` / `NamedGroup` when you also want to capture it) first.

### Options and timeout come from the left pattern

A pattern carries its own `Options` and `Timeout`. When you join two patterns, the result keeps the settings of the **left** pattern. The settings of the right pattern are dropped.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern code = Pattern.With.Literal("abc").WithOptions(RegexOptions.IgnoreCase);
Pattern prefix = Pattern.With.Literal("id-");

RegexOptions leftCode = (code + prefix).Options;    // IgnoreCase
RegexOptions leftPrefix = (prefix + code).Options;  // None
bool lost = (prefix + code).IsMatch("id-ABC");      // false: IgnoreCase was dropped

Pattern limited = Pattern.With.Literal("a").WithTimeout(TimeSpan.FromSeconds(1));
TimeSpan kept = (limited + prefix).Timeout;         // 00:00:01
TimeSpan dropped = (prefix + limited).Timeout;      // -00:00:00.0010000 (Regex.InfiniteMatchTimeout)
```

You have two ways to get the behavior you want:

```csharp
// code and prefix are from the previous sample
Pattern whole = (prefix + code).WithOptions(RegexOptions.IgnoreCase);
bool wholeMatch = whole.IsMatch("ID-ABC");          // true: the whole pattern ignores case

Pattern scoped = prefix.IgnoreCase(Pattern.With.Literal("abc"));
string scopedRegex = scoped.ToString();             // id-(?i:abc)
bool scopedMatch = scoped.IsMatch("id-ABC");        // true: only "abc" ignores case
```

- Set options and timeouts on the **final** pattern, after composing, with `WithOptions`, `WithTimeout` or `UseNonBacktracking` (.NET 8 and .NET 10 targets only).
- For a one-off check you can also pass options at match time: `(prefix + code).IsMatch("id-ABC", RegexOptions.IgnoreCase)` returns `true`. This overload builds a new `Regex` on every call instead of using the pattern's cached one, so prefer `WithOptions` in hot paths.
- When a building block must always ignore case, put the option **inside** the block with an inline scope. A scope is part of the regex text, so it survives any join. See [Scoping options to part of a pattern](#scoping-options-to-part-of-a-pattern).

## Offering alternatives

### OneOf with patterns

`OneOf` matches any one of the given patterns inside a non-capturing group. It does not add a numbered group, so the numbers of your other groups stay the same.

```csharp
using Masterly.FluentRegex;

Pattern id = Pattern.With.OneOf(
    Pattern.With.Digit.Repeat.Times(3),
    Pattern.With.Letter.Repeat.Times(2));

string regex = id.ToString(); // (?:\d{3}|[a-zA-Z]{2})
```

### OneOf with words

The `string` overload takes literal words. Special characters are escaped for you.

```csharp
using Masterly.FluentRegex;

Pattern image = Pattern.With.Word.Repeat.OneOrMore.Literal(".").OneOf("jpg", "png", "gif");

string imageRegex = image.ToString();          // \w+\.(?:jpg|png|gif)
bool isImage = image.IsMatch("photo.png");     // true

string escaped = Pattern.With.OneOf("a.b", "c+").ToString(); // (?:a\.b|c\+)
string single = Pattern.With.OneOf("only").ToString();       // (?:only)
```

| Overload | Throws |
|----------|--------|
| `Pattern OneOf(params Pattern[] alternatives)` | `ArgumentException` when the array is `null` or empty; `ArgumentNullException` when an item is `null`. |
| `Pattern OneOf(params string[] words)` | `ArgumentException` when the array is `null` or empty; `ArgumentNullException` when a word is `null`. |

- One alternative is allowed. It still renders a group: `(?:only)`.
- When the arguments are constants, the analyzer reports these exceptions at build time as FRX006. The same applies to a `null` passed to `Then`, `+`, `|` or a scope, and to an unsupported flag in `InlineOptions`.
- An empty string is a valid alternative. It matches the empty string, so the whole group becomes optional: `Pattern.With.Literal("x").OneOf("a", "")` renders `x(?:a|)` and matches `"x"`. Prefer `.Repeat.Optional` to say this directly (see [below](#do-not-start-an-alternation-with-an-empty-pattern)).

### The | operator

`a | b` builds the same non-capturing group as `OneOf`. Chains of `|` flatten into one group, even when you add parentheses.

```csharp
using Masterly.FluentRegex;

Pattern cat = Pattern.With.Literal("cat");
Pattern dog = Pattern.With.Literal("dog");
Pattern bird = Pattern.With.Literal("bird");

Pattern animal = cat | dog | bird;
string animalRegex = animal.ToString();   // (?:cat|dog|bird)

Pattern grouped = (cat | dog) | (bird | Pattern.With.Literal("fish"));
string groupedRegex = grouped.ToString(); // (?:cat|dog|bird|fish)

Pattern plural = (cat | dog) + Pattern.With.Literal("s");
string pluralRegex = plural.ToString();   // (?:cat|dog)s
```

- The signature is `static Pattern operator |(Pattern left, Pattern right)`. A `null` on either side throws `ArgumentNullException` with parameter `left` or `right`.
- Only groups made by `|` flatten. A group from `OneOf` is kept as it is: `Pattern.With.OneOf(cat, dog) | bird` renders `(?:(?:cat|dog)|bird)`. Both match the same strings.
- A pattern that has anything after the alternation is a single branch: `Pattern.With.Literal("x").Then(cat | dog) | bird` renders `(?:x(?:cat|dog)|bird)`.
- Because the alternation is always wrapped in a group, joining the result with `+` is safe. `(cat | dog) + Pattern.With.Literal("s")` cannot turn into `cat|dogs`.
- For the same reason the result is a single element that a quantifier can follow directly: `(cat | dog).Repeat.OneOrMore` renders `(?:cat|dog)+` and matches all of `"catdog"`.

Like `+`, the `|` operator takes the options and timeout of the left side. When both sides share their settings, the result keeps them:

```csharp
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern yes = Pattern.With.Literal("yes").WithOptions(RegexOptions.IgnoreCase);
Pattern no = Pattern.With.Literal("no").WithOptions(RegexOptions.IgnoreCase);

Pattern answer = yes | no;
RegexOptions options = answer.Options;   // IgnoreCase
bool upper = answer.IsMatch("YES");      // true
```

The right side's options and timeout are ignored. When the sides differ, set the settings on the result, or use an inline scope inside each branch.

### Choosing between Choice, OneOf and |

`Choice` also offers alternatives, but it creates a **numbered capturing group**, which shifts the numbers of every group after it.

```csharp
using Masterly.FluentRegex;

Pattern capturing = Pattern.With.Choice(Pattern.With.Literal("cat"), Pattern.With.Literal("dog"))
    .Group(Pattern.With.Digit);
Pattern nonCapturing = Pattern.With.OneOf("cat", "dog").Group(Pattern.With.Digit);

string capturingRegex = capturing.ToString();        // (cat|dog)(\d)
string nonCapturingRegex = nonCapturing.ToString();  // (?:cat|dog)(\d)

string firstCapturing = capturing.Match("dog7").Groups[1].Value;       // dog
string firstNonCapturing = nonCapturing.Match("dog7").Groups[1].Value; // 7

int capturingCount = capturing.GetGroupCount();       // 2
int nonCapturingCount = nonCapturing.GetGroupCount(); // 1
```

| Member | Group | Minimum alternatives | Flattens with `\|` |
|--------|-------|----------------------|--------------------|
| `Choice(first, second, params additional)` | Numbered, capturing: `(a\|b)` | 2 | No |
| `OneOf(params Pattern[])`, `OneOf(params string[])` | Non-capturing: `(?:a\|b)` | 1 | No |
| `a \| b` | Non-capturing: `(?:a\|b)` | 2 | Yes |

Prefer `OneOf` or `|`. Use `Choice` only when you need to read which alternative matched, and consider a `NamedGroup` around a `OneOf` instead so the code does not depend on group numbers.

### Do not start an alternation with an empty pattern

`Pattern.With` is an empty pattern. Used as a branch, it becomes an empty alternative that always matches.

```csharp
using Masterly.FluentRegex;

Pattern sign = Pattern.With | Pattern.With.Literal("-");
string signRegex = sign.ToString();     // (?:|-)
bool matchesEmpty = sign.IsMatch("");   // true

Pattern optionalSign = Pattern.With.Literal("-").Repeat.Optional;
string optionalRegex = optionalSign.ToString(); // -?
```

When a part is optional, say so with `.Repeat.Optional`. It is shorter and states the intent.

### The | operator versus Union

`Union` from [Pattern Algebra](./pattern-algebra.md) also combines two patterns, but in a different way. `|` joins the regex text. `Union` builds a new regex from the automata of both patterns and returns it as a raw pattern.

```csharp
using Masterly.FluentRegex;

string unionRegex = Pattern.FromRegex("cat").Union(Pattern.FromRegex("dog")).ToString(); // cat|dog
string operatorRegex = (Pattern.FromRegex("cat") | Pattern.FromRegex("dog")).ToString(); // (?:cat|dog)
```

- `Union` keeps the left pattern's options and timeout. `|` resets them.
- The `Union` result is not wrapped in a group. Before you join it with other patterns, wrap it in `NonCapturingGroup` (see [Using FromRegex safely](#using-fromregex-safely)).

## Scoping options to part of a pattern

Options set with `WithOptions` apply to the whole pattern. An inline scope applies options to one part only, and it is written into the regex text, so it works wherever the block is reused.

| Member | Renders | Effect |
|--------|---------|--------|
| `IgnoreCase(Pattern pattern)` | `(?i:...)` | The inner pattern ignores case, whatever the outer options are. |
| `CaseSensitive(Pattern pattern)` | `(?-i:...)` | The inner pattern is case-sensitive, even when the whole pattern uses `RegexOptions.IgnoreCase`. |
| `InlineOptions(RegexOptions enable, Pattern pattern, RegexOptions disable = RegexOptions.None)` | `(?imnsx-imnsx:...)` | Turns any supported options on and off for the inner pattern. |

### Ignore case for one part

```csharp
using Masterly.FluentRegex;

Pattern id = Pattern.With.Literal("id-").IgnoreCase(Pattern.With.Literal("abc"));

string regex = id.ToString();      // id-(?i:abc)
bool upperCode = id.IsMatch("id-ABC"); // true
bool upperPrefix = id.IsMatch("ID-abc"); // false: the prefix is still case-sensitive
```

### Keep one part case-sensitive

```csharp
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern sku = Pattern.With.Literal("sku-")
    .CaseSensitive(Pattern.With.Literal("X"))
    .WithOptions(RegexOptions.IgnoreCase);

string regex = sku.ToString();        // sku-(?-i:X)
bool upperPrefix = sku.IsMatch("SKU-X"); // true: the prefix ignores case
bool lowerX = sku.IsMatch("sku-x");     // false: X must be upper case
```

### Any inline option

`InlineOptions` turns options on with `enable` and off with `disable`. Here `.` must cross line breaks inside a paragraph tag, but nowhere else.

```csharp
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern paragraph = Pattern.With.Literal("<p>")
    .InlineOptions(RegexOptions.Singleline, Pattern.With.Anything.Repeat.ZeroOrMore.Lazy)
    .Literal("</p>");

string regex = paragraph.ToString(); // <p>(?s:.*?)</p>
string match = paragraph.Match("<p>line one\nline two</p>").Value; // "<p>line one\nline two</p>": the whole input, line break included

Pattern mixed = Pattern.With.InlineOptions(
    RegexOptions.IgnoreCase | RegexOptions.Singleline,
    Pattern.With.Anything,
    RegexOptions.Multiline);
string mixedRegex = mixed.ToString(); // (?is-m:.)
```

Only five options can be written inline:

| `RegexOptions` value | Flag | Effect inside the scope |
|----------------------|------|-------------------------|
| `IgnoreCase` | `i` | Letters match in either case. |
| `Multiline` | `m` | `^` and `$` match at every line, not only at the ends of the input. |
| `ExplicitCapture` | `n` | Plain `(...)` groups do not capture; only named groups do. |
| `Singleline` | `s` | `.` also matches `\n`. |
| `IgnorePatternWhitespace` | `x` | Unescaped whitespace and `#` comments in the inner regex are ignored. |

- Any other flag, such as `RegexOptions.Compiled`, throws `ArgumentException` with the message `Only IgnoreCase, Multiline, ExplicitCapture, Singleline and IgnorePatternWhitespace can be applied inline.` The parameter name is `enable` or `disable`, whichever held the bad flag.
- `InlineOptions(RegexOptions.None, pattern)` is allowed and renders a plain non-capturing group: `(?:.)`.
- `ExplicitCapture` changes group counts. `Pattern.With.InlineOptions(RegexOptions.ExplicitCapture, Pattern.With.Group(Pattern.With.Digit)).GetGroupCount()` returns `0`.
- Inline scopes are part of the regex text, so they are kept by `ToString()`, by `Equals`, and by every export that supports them. See [Export](./export.md) for target platforms that do not.

### A wrapped pattern's own options are ignored

When you pass a pattern to `Group`, `NonCapturingGroup`, `NamedGroup`, `OneOf`, a lookaround or a scope, only its regex text is used. Its `Options` and `Timeout` are not. This is the same rule as for joining: settings are not part of the text.

```csharp
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern code = Pattern.With.Literal("abc").WithOptions(RegexOptions.IgnoreCase);

Pattern wrapped = Pattern.With.Literal("id-").Group(code);
string wrappedRegex = wrapped.ToString();          // id-(abc)
RegexOptions wrappedOptions = wrapped.Options;     // None
bool wrappedMatch = wrapped.IsMatch("id-ABC");     // false

Pattern scoped = Pattern.With.Literal("id-").Group(Pattern.With.IgnoreCase(Pattern.With.Literal("abc")));
string scopedRegex = scoped.ToString();            // id-((?i:abc))
bool scopedMatch = scoped.IsMatch("id-ABC");       // true
```

:::tip

If a building block has an option that is part of its meaning (a product code that is always case-insensitive), write it as an inline scope inside the block. Keep `WithOptions` for settings of the final pattern.

:::

## Wrapping an existing regex with FromRegex

`Pattern.FromRegex` turns a regex string into a pattern. Use it to reuse a regex you already have, from a configuration file, a legacy class or a colleague, as a building block next to fluent code.

```csharp
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern legacy = Pattern.FromRegex(@"[A-Z]{2}\d{6}");
Pattern tagged = Pattern.With.StartOfLine.Literal("REF:").Then(legacy).EndOfLine;

string regex = tagged.ToString();                  // ^REF:[A-Z]{2}\d{6}$
bool isTagged = tagged.IsMatch("REF:AB123456");    // true

Pattern hello = Pattern.FromRegex("hello", RegexOptions.IgnoreCase);
RegexOptions options = hello.Options;              // IgnoreCase
bool upper = hello.IsMatch("HELLO");               // true
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `string regex` | (required) | The .NET regular expression. It is used exactly as written. |
| `RegexOptions options` | `RegexOptions.None` | The options the expression is used with. They become the pattern's `Options`, as if you had called `WithOptions`. |

- The regex is validated by constructing a `Regex` with the given options. An invalid regex throws `ArgumentException` with parameter name `regex` and a message such as `The regular expression is invalid: Invalid pattern '(abc' at offset 4. Not enough )'s.` The original parse error is the `InnerException`.
- A `null` regex throws `ArgumentNullException`.
- When the regex is a constant, the analyzer reports an invalid one at build time as FRX006 (see [Compile-time checks](./compile-time-checks.md)).
- On the `net8.0` and `net10.0` builds the `regex` parameter carries `[StringSyntax(StringSyntaxAttribute.Regex, nameof(options))]`, so Visual Studio and Rider colour and check the string as a regex.
- The result can be traced, analyzed, drawn or turned into fluent code. `legacy.ToFluentCode()` returns `Pattern.With.UppercaseLetter.Repeat.Times(2).Digit.Repeat.Times(6)` (split over lines). See [Regex to Fluent Code](./regex-to-fluent-code.md).

### Raw text in the middle of a chain with RegEx

`FromRegex` starts a new pattern. To add raw regex text in the middle of a fluent chain, use the instance method `RegEx(string regex)`. It appends the text exactly as written.

```csharp
using Masterly.FluentRegex;

Pattern tag = Pattern.With.Literal("id:").RegEx(@"[A-Z]{2}\d{6}");
string regex = tag.ToString();          // id:[A-Z]{2}\d{6}
bool isTag = tag.IsMatch("id:AB123456"); // true
```

- `RegEx` does **not** validate the text and does not take options. An invalid fragment is only detected when the pattern is first matched, which throws an `ArgumentException` (a `RegexParseException` on modern .NET). For a constant, the analyzer reports it at build time as FRX003.
- The same safety rules as for `FromRegex` apply: the text is not grouped for you. See the next section.
- `Pattern.FromRegex(regex, options)` is exactly `Pattern.With.RegEx(regex).WithOptions(options)`, after the validation step.

### Using FromRegex safely

A pattern from `FromRegex` is raw regex text. The fluent API does not parse it into elements, so it does not add parentheses for you when you join it with other patterns. Two cases need care.

**A top-level alternation leaks into its neighbours.** Joined as-is, `cat|dog` followed by `s` becomes `cat|dogs`, which means "`cat` or `dogs`".

```csharp
using Masterly.FluentRegex;

Pattern animal = Pattern.FromRegex("cat|dog");

Pattern plural = animal + Pattern.With.Literal("s");
string pluralRegex = plural.ToString();   // cat|dogs
bool dog = plural.IsMatch("dog");         // false
bool cat = plural.IsMatch("cat");         // true: no "s" required

Pattern safe = Pattern.With.NonCapturingGroup(animal).Literal("s");
string safeRegex = safe.ToString();       // (?:cat|dog)s
bool dogs = safe.IsMatch("dogs");         // true
bool catWithoutS = safe.IsMatch("cat");   // false
```

As a branch of `|` or `OneOf`, a raw alternation is safe, because the branches are already inside a group: `animal | Pattern.With.Literal("bird")` renders `(?:cat|dog|bird)`.

**A quantifier repeats only the last character.** `Repeat` applies to the last element of the pattern, and for raw text that is its last atom.

```csharp
using Masterly.FluentRegex;

Pattern raw = Pattern.FromRegex("abc");

string repeated = raw.Repeat.OneOrMore.ToString();                                 // abc+
string possessive = raw.Repeat.OneOrMore.Possessive.ToString();                    // ab(?>c+)
string wholeBlock = Pattern.With.NonCapturingGroup(raw).Repeat.OneOrMore.ToString(); // (?:abc)+
```

:::warning

Wrap a `FromRegex` (or `RegEx`) block in `NonCapturingGroup` before you join it with other patterns or quantify it. The same applies to the result of `Union`, `Intersect` and `Except`, which are built with `FromRegex`. Also remember that the `options` you pass to `FromRegex` are dropped when the block is not the left-most pattern of a join; use an inline scope such as `IgnoreCase(Pattern.FromRegex("abc"))` instead.

:::

### Reusing the ready-made patterns

The patterns in [Common Patterns](./common-patterns.md) are anchored with `^` and `$`, because they are meant for validating a whole string. Joined directly, the anchors end up in the middle and the result can never match.

```csharp
using Masterly.FluentRegex;

Pattern broken = CommonPatterns.DateIso() + Pattern.With.Literal("T") + CommonPatterns.Time24();
string brokenRegex = broken.ToString();                // ^\d{4}-[0-1]\d-[0-3]\d$T^([01]\d|2[0-3]):[0-5]\d(?::[0-5]\d)?$
bool brokenMatch = broken.IsMatch("2026-01-31T13:45"); // false

Pattern date = Pattern.FromRegex(CommonPatterns.DateIso().ToString().Trim('^', '$'));
Pattern time = Pattern.FromRegex(CommonPatterns.Time24().ToString().Trim('^', '$'));
Pattern stamp = Pattern.With.StartOfLine
    .NonCapturingGroup(date).Literal("T").NonCapturingGroup(time)
    .EndOfLine;

string stampRegex = stamp.ToString();                  // ^(?:\d{4}-[0-1]\d-[0-3]\d)T(?:([01]\d|2[0-3]):[0-5]\d(?::[0-5]\d)?)$
bool stampMatch = stamp.IsMatch("2026-01-31T13:45");   // true
```

Strip the anchors, wrap each part in a group, and anchor the final pattern once.

## Group numbers in composed patterns

Numbered groups are counted from the left of the **final** pattern. A building block that refers to its own group by number breaks as soon as another capturing group is placed in front of it.

```csharp
using Masterly.FluentRegex;

Pattern doubled = Pattern.With.Group(Pattern.With.Word.Repeat.OneOrMore).Whitespace.Backreference(1);
bool repeatedWord = doubled.IsMatch("the the"); // true

Pattern prefixed = Pattern.With.Group(Pattern.With.Digit).Whitespace.Then(doubled);
string regex = prefixed.ToString();             // (\d)\s(\w+)\s\1
bool sameWord = prefixed.IsMatch("7 the the");  // false: \1 now refers to the digit
bool sameDigit = prefixed.IsMatch("7 the 7");   // true
```

Use named groups in building blocks. A name keeps pointing at the same group wherever the block ends up.

```csharp
using Masterly.FluentRegex;

Pattern doubledNamed = Pattern.With
    .NamedGroup("word", Pattern.With.Word.Repeat.OneOrMore)
    .Whitespace
    .Backreference("word");

Pattern prefixedNamed = Pattern.With.Group(Pattern.With.Digit).Whitespace.Then(doubledNamed);
string regex = prefixedNamed.ToString();            // (\d)\s(?<word>\w+)\s\k<word>
bool sameWord = prefixedNamed.IsMatch("7 the the"); // true
```

- Reusing a block with a named group twice creates two groups with the same name. .NET accepts this, and the group holds the **last** capture: `(doubledNamed + Pattern.With.Literal(";") + doubledNamed).Match("a a;b b").Groups["word"].Value` is `b`.
- `Choice` adds a numbered group; `OneOf` and `|` do not. Prefer them inside reusable blocks so they do not shift the numbers of the patterns that use them.
- To bind named groups to a typed object, see [Typed Extraction](./typed-extraction.md).

## Comparing patterns

Patterns have **value equality**. Two patterns are equal when they render the same regex text and have the same `Options` and `Timeout`, however they were built.

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern fluent = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine;
Pattern parsed = Pattern.FromRegex(@"^\d+$");

bool equal = fluent.Equals(parsed);                          // true
bool sameHash = fluent.GetHashCode() == parsed.GetHashCode(); // true
bool sameReference = fluent == parsed;                       // false: == compares references

bool otherOptions = parsed.Equals(parsed.WithOptions(RegexOptions.IgnoreCase));      // false
bool otherTimeout = parsed.Equals(parsed.WithTimeout(TimeSpan.FromSeconds(1)));      // false

Dictionary<Pattern, string> names = new Dictionary<Pattern, string>
{
    [Pattern.FromRegex(@"\d+")] = "digits"
};
string name = names[Pattern.With.Digit.Repeat.OneOrMore];    // digits
```

| Member | Behavior |
|--------|----------|
| `bool Equals(Pattern? other)` | `true` when `other` has the same type, options, timeout and regex text (ordinal comparison). `false` for `null`. |
| `bool Equals(object? obj)` | The same, for any object. A `string` is never equal to a pattern, even with the same text. |
| `int GetHashCode()` | Built from the regex text, options and timeout, so it is consistent with `Equals`. Patterns can key a `Dictionary` or `HashSet`. |
| `==`, `!=` | Not overloaded. They compare references, as for any class. |

- Two empty patterns are equal: `Pattern.With.Equals(new Pattern())` is `true`.
- Equality compares the **text**, not the meaning. `Pattern.With.Digit.Digit` (`\d\d`) and `Pattern.With.Digit.Repeat.Times(2)` (`\d{2}`) are not `Equals`, but `IsEquivalentTo` proves they match the same strings and returns `true`. See [Pattern Algebra](./pattern-algebra.md).
- `Equals` and `GetHashCode` render the pattern the first time. The text is cached on the instance, so later calls are cheap.
- Value equality is what lets Entity Framework Core reuse a compiled query that embeds a pattern built fresh on every request. See [Entity Framework Core](./entity-framework-core.md).

## Match scopes: what a composed pattern describes

A pattern on its own can mean two different sets of strings. `IsMatch` accepts any input that **contains** a match, unless the pattern is anchored. Language-level tools usually talk about the strings the pattern matches **entirely**. The `MatchScope` enum lets you say which one you mean.

| Value | Describes |
|-------|-----------|
| `MatchScope.FullString` | The strings the pattern matches from start to end, as if it were wrapped in `\A(?:...)\z`. An explicit `$` keeps accepting a final newline. |
| `MatchScope.Substring` | The strings for which `IsMatch` returns `true`: those that contain a match anywhere, unless the pattern is anchored at one or both ends. |

Two members take a scope, with different defaults:

| Member | Default scope | Page |
|--------|---------------|------|
| `GetLanguageFacts(MatchScope scope = MatchScope.FullString)` | `FullString` | [Language Facts](./language-facts.md) |
| `ToLike(LikeDialect dialect, MatchScope scope = MatchScope.Substring, LikeCollation collation = LikeCollation.Binary)` | `Substring` | [SQL LIKE](./sql-like.md) |

The scope matters most for building blocks, which are usually unanchored:

```csharp
using Masterly.FluentRegex;

Pattern invoice = Pattern.With.Literal("INV-").Range('0', '9').Repeat.Times(4);

LanguageFacts whole = invoice.GetLanguageFacts();
// FullString: length 8..8, prefix "INV-", suffix "", accepts empty: false (exact)

LanguageFacts anywhere = invoice.GetLanguageFacts(MatchScope.Substring);
// Substring: length 8..unbounded, prefix "", suffix "", accepts empty: false (exact)

LanguageFacts anchored = Pattern.With.StartOfInput.Then(invoice).EndOfInput
    .GetLanguageFacts(MatchScope.Substring);
// Substring: length 8..8, prefix "INV-", suffix "", accepts empty: false (exact)

string contains = invoice.ToLike(LikeDialect.SqlServer).Expression;                         // %INV-[0-9][0-9][0-9][0-9]%
string exact = invoice.ToLike(LikeDialect.SqlServer, MatchScope.FullString).Expression;     // INV-[0-9][0-9][0-9][0-9]
```

- The comments under `GetLanguageFacts` show `LanguageFacts.ToString()`.
- An unanchored block in `Substring` scope can be surrounded by any text, so its maximum length is unbounded (`MaxLength` is `null`) and it has no required prefix.
- Anchoring the final pattern with `StartOfInput` and `EndOfInput` makes both scopes describe the same strings.
- Any value outside the enum throws `ArgumentOutOfRangeException`.

:::note

Inline options scopes (`IgnoreCase`, `InlineOptions`) and match scopes are unrelated. An inline scope changes how part of the regex matches. A match scope only tells an analysis or translation which set of strings to describe; it never changes the pattern.

:::

## Best practices

- **Name the parts.** A field called `Region` or `Sequence` explains a regex better than any comment.
- **Keep blocks unanchored; anchor once.** Put `StartOfLine`/`EndOfLine` or `StartOfInput`/`EndOfInput` only on the final pattern.
- **Group before you quantify.** `Then` and `+` do not group, so wrap a block in `NonCapturingGroup` before `.Repeat`.
- **Prefer `|` and `OneOf` over `Choice`.** They do not add numbered groups.
- **Use named groups inside blocks.** Numbered backreferences break when a block is moved.
- **Set options and timeouts last.** Joins keep only the left pattern's settings, and `|` keeps none. Use inline scopes for options that belong to a block.
- **Wrap raw regex.** Put every `FromRegex`, `Union`, `Intersect` or `Except` result in a `NonCapturingGroup` before joining it.
- **Store composed patterns in `static readonly` fields**, declared after the blocks they use, so they are built and compiled once.
- **Add a timeout or the non-backtracking engine** to the final pattern when it sees untrusted input. Composing small safe blocks can still produce a risky whole; check it with [ReDoS Safety](./redos-safety.md).

## Related pages

- [Groups and Backreferences](./groups-and-backreferences.md): `Group`, `NamedGroup`, `NonCapturingGroup`, `AtomicGroup`, `Conditional`.
- [Quantifiers](./quantifiers.md): `Repeat`, `Lazy` and `Possessive`.
- [Pattern Algebra](./pattern-algebra.md): prove that two compositions match the same strings.
- [Validation and Debugging](./validation-and-debugging.md): `Explain`, `GetGroupNames` and `GetGroupCount` for a composed pattern.
- [Best Practices](./best-practices.md).
