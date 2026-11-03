---
id: regex-to-fluent-code
title: "Regex to Fluent Code"
description: "Wrap an existing regular expression with FromRegex and turn it into readable, editable fluent C# with ToFluentCode or the convert command."
---

Every codebase has regexes nobody dares to touch. `Pattern.FromRegex` wraps an existing regular expression in a `Pattern`, and `ToFluentCode()` turns any pattern into C# source that rebuilds it with the fluent API. Paste the result into your code, and the regex becomes something you can read, review and change one step at a time.

Use this page when you need to:

- migrate hand-written regexes, one by one or a whole folder at a time, to fluent code,
- understand an unfamiliar regex by reading it as named steps,
- use an existing regex with the rest of the library (matching, tracing, diagrams, safety analysis) without rewriting it first,
- check that the converted code still matches exactly what the original regex matched.

The two members live on `Pattern` in the `Masterly.FluentRegex` namespace:

| Member | What it does |
|--------|--------------|
| `Pattern.FromRegex(string regex, RegexOptions options = RegexOptions.None)` | Validates a regex and wraps it in a `Pattern`. |
| `pattern.ToFluentCode(int indentSize = 4)` | Returns C# source code, as a `string`, that rebuilds the pattern with the fluent API. |

Both work on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10. On .NET 7 and later, the `regex` parameter of `FromRegex` is marked with `[StringSyntax(StringSyntaxAttribute.Regex)]`, so Visual Studio and Rider color and check the regex text as you type.

## Converting a regex

Wrap the regex with `FromRegex`, then call `ToFluentCode()` and print or copy the result.

```csharp
using System;
using Masterly.FluentRegex;

string code = Pattern.FromRegex(@"^(?<user>[\w.]+)@(?<host>[\w-]+\.\w{2,})$").ToFluentCode();

Console.WriteLine(code);
```

This prints:

```csharp
Pattern.With
    .StartOfLine
    .NamedGroup("user", Pattern.With.Set(Pattern.With.Word.Literal(".")).Repeat.OneOrMore)
    .Literal("@")
    .NamedGroup(
        "host",
        Pattern.With
            .Set(Pattern.With.Word.Literal("-")).Repeat.OneOrMore
            .Literal(".")
            .Word.Repeat.AtLeast(2))
    .EndOfLine
```

- The result is a C# expression of type `Pattern`, not a statement. It has no trailing `;`, so you can assign it, return it or pass it as an argument.
- Every construct becomes the fluent member that renders it: `^` becomes `StartOfLine`, `[\w.]` becomes a `Set`, `{2,}` becomes `Repeat.AtLeast(2)`. The full mapping is in [How each construct is translated](#how-each-construct-is-translated).
- Constructs that have no fluent member are kept as raw regex text inside `RegEx(...)`, so the code is always complete. See [Constructs kept as raw regex](#constructs-kept-as-raw-regex).
- The playground does the same conversion live in its **Fluent C#** tab. See [Playground](./playground.md).

## Wrapping an existing regex with FromRegex

`FromRegex` checks that the text is a valid .NET regex, then returns a `Pattern` that renders exactly that text. You do not have to convert anything to use the result: it works with every `Pattern` feature, such as matching, [tracing](./match-tracing.md), [diagrams](./diagrams.md), [ReDoS analysis](./redos-safety.md) and the [optimizer](./optimizer.md).

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern sku = Pattern.FromRegex(@"^[A-Z]{3}-\d{4}$", RegexOptions.IgnoreCase);

Console.WriteLine(sku);                     // ^[A-Z]{3}-\d{4}$
Console.WriteLine(sku.Options);             // IgnoreCase
Console.WriteLine(sku.IsMatch("abc-1234")); // True
```

| Parameter | Default | Meaning |
|-----------|---------|---------|
| `regex` | Required | The .NET regular expression. It is stored exactly as written. |
| `options` | `RegexOptions.None` | The options the regex is used with. They become the pattern's `Options` and are part of the validation. |

- The text is kept as written, not normalized. `Pattern.FromRegex(@"a{,3}").ToString()` returns `a{,3}`.
- The pattern has no timeout. Its `Timeout` is `Regex.InfiniteMatchTimeout` until you call `WithTimeout(...)`.
- `FromRegex` is the same as `Pattern.With.RegEx(regex).WithOptions(options)`, plus the validation. `RegEx(...)` alone does not validate anything.

### When the regex is invalid

`FromRegex` builds a `Regex` once to validate the text and the options together. It reports a problem with these exceptions:

| Input | Exception | Details |
|-------|-----------|---------|
| `regex` is `null` | `ArgumentNullException` | `ParamName` is `"regex"`. |
| The text is not valid .NET regex syntax | `ArgumentException` | `ParamName` is `"regex"`. The message starts with `The regular expression is invalid: `. `InnerException` is the original exception from .NET (a `RegexParseException` on .NET 7 and later). |
| The options cannot be used together, such as `ECMAScript` with `Singleline` | `ArgumentException` | Same as above. `InnerException` is the `ArgumentOutOfRangeException` from .NET, with `ParamName` `"options"`. |

```csharp
// regexText comes from your application, for example a settings file
try
{
    Pattern pattern = Pattern.FromRegex(regexText);
}
catch (ArgumentException ex)
{
    Console.WriteLine(ex.ParamName);                      // regex
    Console.WriteLine(ex.Message);
    // prints: The regular expression is invalid: Invalid pattern '(abc' at offset 4. Not enough )'s. (Parameter 'regex')
    Console.WriteLine(ex.InnerException?.GetType().Name); // RegexParseException
}
```

- The output above is for `regexText` set to `"(abc"`.
- Everything after `The regular expression is invalid: ` comes from .NET, so the wording can change between .NET versions. Check the exception type, not the message.

:::tip

When the regex is a string literal in your code, you do not have to wait for the exception. The analyzers that ship with the package report an invalid `FromRegex(...)` literal as a build error (`FRX006`). See [Compile-time checks](./compile-time-checks.md).

:::

### Adding to a wrapped regex

A pattern from `FromRegex` is a normal pattern, so you can chain more steps onto it. The new steps are appended to the regex text as it is. That is a problem when the regex has a top-level `|`:

```csharp
Pattern animals = Pattern.FromRegex("cat|dog");

Console.WriteLine(animals.Literal("s"));                                 // cat|dogs
Console.WriteLine(Pattern.With.NonCapturingGroup(animals).Literal("s")); // (?:cat|dog)s
```

- `cat|dogs` matches `cat` or `dogs`, which is probably not what you meant. Wrap the wrapped regex in `NonCapturingGroup(...)` first, as in the second line.
- Converting to fluent code first avoids the problem. `ToFluentCode()` turns a top-level `|` into `OneOf(...)`, which renders its own group.

Options belong to the outermost pattern only. When you nest a `FromRegex` pattern inside another pattern, its options are dropped:

```csharp
Pattern word = Pattern.FromRegex("abc", RegexOptions.IgnoreCase);
Pattern nested = Pattern.With.NonCapturingGroup(word).Literal("!");

Console.WriteLine(nested);         // (?:abc)!
Console.WriteLine(nested.Options); // None

Pattern scoped = Pattern.With.IgnoreCase(Pattern.FromRegex("abc")).Literal("!");

Console.WriteLine(scoped);                 // (?i:abc)!
Console.WriteLine(scoped.IsMatch("ABC!")); // True
```

- To keep case-insensitive matching for one part, use `IgnoreCase(...)` or `InlineOptions(...)`. They write the option into the regex text, so it survives nesting.
- See [Composing patterns](./composing-patterns.md) for more on combining patterns.

## Generating code with ToFluentCode

`ToFluentCode()` works on any pattern, not only on patterns from `FromRegex`. It reads the regex text the pattern renders, so the code shows the regex, not the way the pattern was built:

```csharp
Pattern phone = Pattern.With.StartOfLine.Digit.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4).EndOfLine;

Console.WriteLine(phone.ToFluentCode());
// prints:
// Pattern.With
//     .StartOfLine
//     .Digit.Repeat.Times(3)
//     .Literal("-")
//     .Digit.Repeat.Times(4)
//     .EndOfLine

Console.WriteLine(Pattern.FromRegex(@"\d+").ToFluentCode()); // Pattern.With.Digit.Repeat.OneOrMore

Console.WriteLine(Pattern.FromRegex(@"^\d{4}$").WithAsciiDigits().ToFluentCode());
// prints:
// Pattern.With
//     .StartOfLine
//     .Range('0', '9').Repeat.Times(4)
//     .EndOfLine
```

- A pattern built from a [common pattern](./common-patterns.md), the [optimizer](./optimizer.md) or [learning from examples](./learning-from-examples.md) can be converted the same way.
- Because the code comes from the rendered regex, a member that renders as another construct comes back as that construct. `Possessive` renders an atomic group, so it converts to `AtomicGroup(...)`:

```csharp
Pattern possessive = Pattern.With.Digit.Repeat.OneOrMore.Possessive;

Console.WriteLine(possessive);                // (?>\d+)
Console.WriteLine(possessive.ToFluentCode()); // Pattern.With.AtomicGroup(Pattern.With.Digit.Repeat.OneOrMore)
```

- `WithAsciiDigits()` rewrites `\d` to `[0-9]`, so the code uses `Range('0', '9')`. See [Unicode](./unicode.md) for why `\d` matches more than 0-9 in .NET.

### Options and timeout

When the pattern has options or a timeout, the code ends with `.WithOptions(...)` and `.WithTimeout(...)`:

```csharp
Pattern pattern = Pattern.FromRegex("abc", RegexOptions.IgnoreCase | RegexOptions.Multiline)
    .WithTimeout(TimeSpan.FromMilliseconds(250));

Console.WriteLine(pattern.ToFluentCode());
// prints:
// Pattern.With
//     .Literal("abc")
//     .WithOptions(RegexOptions.IgnoreCase | RegexOptions.Multiline)
//     .WithTimeout(TimeSpan.FromMilliseconds(250))
```

- Every `RegexOptions` flag is written by name, joined with `|`, in this order: `IgnoreCase`, `Multiline`, `ExplicitCapture`, `Compiled`, `Singleline`, `IgnorePatternWhitespace`, `RightToLeft`, `ECMAScript`, `CultureInvariant`, `NonBacktracking`.
- `.WithOptions(...)` is left out when the options are `None`. `.WithTimeout(...)` is left out when there is no timeout.
- The timeout is written in milliseconds with up to three decimals, for example `TimeSpan.FromMilliseconds(1500)`.
- The code for `RegexOptions.NonBacktracking` compiles only on .NET 7 and later, because older targets do not have that enum member.

### Layout and indentation

The generated code is formatted to be pasted as it is:

- A short pattern stays on one line: `Pattern.With.Digit.Repeat.OneOrMore`. That happens when the top-level chain has at most three members and the line is at most 90 characters. Every property, method call, `Repeat` step, `Lazy` and `WithOptions` counts as one member.
- A longer pattern gets one line per member. A quantifier stays on the line of the element it repeats: `.Digit.Repeat.Times(3)`.
- A nested pattern, such as the body of a group, stays on one line when it fits in 90 characters, counting its indentation. Otherwise it is split the same way, and the arguments of the call go on separate lines.
- Line breaks are always `\n`, whatever the operating system.

`indentSize` sets the number of spaces per indentation level. The default is 4.

```csharp
Pattern email = Pattern.FromRegex(@"^(?<user>[\w.]+)@(?<host>[\w-]+\.\w{2,})$");

Console.WriteLine(email.ToFluentCode(indentSize: 2));
```

This prints:

```csharp
Pattern.With
  .StartOfLine
  .NamedGroup("user", Pattern.With.Set(Pattern.With.Word.Literal(".")).Repeat.OneOrMore)
  .Literal("@")
  .NamedGroup(
    "host",
    Pattern.With
      .Set(Pattern.With.Word.Literal("-")).Repeat.OneOrMore
      .Literal(".")
      .Word.Repeat.AtLeast(2))
  .EndOfLine
```

- `indentSize: 0` is allowed. The code keeps its line breaks but has no indentation.
- A negative `indentSize` throws `ArgumentOutOfRangeException` with `ParamName` `"indentSize"`.

### Compiling the generated code

The code uses only public API, but it needs these `using` directives in the file where you paste it:

| The code contains | You need |
|-------------------|----------|
| `Pattern`, `UnicodeGroup` | `using Masterly.FluentRegex;` |
| `RegexOptions` (in `WithOptions` or `InlineOptions`) | `using System.Text.RegularExpressions;` |
| `TimeSpan` (in `WithTimeout`) | `using System;` |

Unicode categories are written with their full name, `System.Globalization.UnicodeCategory.UppercaseLetter`, so they need no extra `using`.

### When ToFluentCode throws

`ToFluentCode()` parses the regex text of the pattern. A pattern from `FromRegex` was already validated, so it always converts. A pattern that contains unvalidated text from `RegEx(...)` can fail:

```csharp
// rawText comes from your application and was never validated
Pattern unvalidated = Pattern.With.RegEx(rawText);

try
{
    unvalidated.ToFluentCode();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message);
    // prints: The pattern is not valid regex syntax: Not enough )'s (at position 0)
}
```

- The output above is for `rawText` set to `"(abc"`. The position in the message is where the parser found the problem.
- When the raw text is a string literal, such as `RegEx("(abc")`, the analyzers report it at build time (`FRX003`), so this mostly happens with text that arrives at run time.
- To validate text from outside your code, wrap it with `FromRegex` instead of `RegEx`. See [Validation and debugging](./validation-and-debugging.md).

## How each construct is translated

The converter picks the most readable fluent member that renders the same regex. Every row below is real output of `Pattern.FromRegex(regex).ToFluentCode()`. To keep the tables short, most rows leave out the leading `Pattern.With.`, every row is shown on one line (the real output of a longer row is split as described in [Layout and indentation](#layout-and-indentation)), and `...` stands for the part of the code that comes before.

### Characters and literals

| Regex | Generated code |
|-------|----------------|
| `abc` | `Pattern.With.Literal("abc")` |
| `\$\d` | `Pattern.With.Literal("$").Digit` |
| `.` | `Pattern.With.Anything` |
| `\t`, `\n`, `\r` (alone) | `Pattern.With.Tab`, `Pattern.With.Newline`, `Pattern.With.CarriageReturn` |
| `\r\n` | `Pattern.With.Literal("\r\n")` |
| `\x41B` | `Pattern.With.Literal("AB")` |
| `\e` | `Pattern.With.Literal("\u001B")` |
| `"hi"` | `Pattern.With.Literal("\"hi\"")` |
| `a{,3}` | `Pattern.With.Literal("a{,3}")` |

- Neighboring characters are merged into one `Literal(...)`. Escapes are removed, because `Literal` escapes regex characters for you.
- Strings are written as C# literals. Quotes, backslashes and control characters are escaped, for example `\"`, `\\`, `\0` and `\u0001`. Other characters, such as `é`, are written as they are. A character outside the Basic Multilingual Plane, such as an emoji, is written as its two surrogate escapes, for example `😀`.
- `a{,3}` is not a quantifier in .NET. It matches the text `a{,3}`, so it becomes a literal.

### Character classes

| Regex | Generated code |
|-------|----------------|
| `\d`, `\D` | `Digit`, `NonDigit` |
| `\w`, `\W` | `Word`, `NonWord` |
| `\s`, `\S` | `Whitespace`, `NonWhitespace` |
| `[a-zA-Z]` | `Letter` |
| `[a-z]`, `[A-Z]` | `LowercaseLetter`, `UppercaseLetter` |
| `[0-9a-fA-F]` | `HexDigit` |
| `[a-f]`, `[0-9]` | `Range('a', 'f')`, `Range('0', '9')` |
| `[aeiou]` | `AnyOf("aeiou")` |
| `[^aeiou]` | `NoneOf("aeiou")` |
| `[ab0-9]` | `Set(Pattern.With.Literal("ab").Range('0', '9'))` |
| `[\w.-]` | `Set(Pattern.With.Word.Literal(".-"))` |
| `[^a-z]` | `NegatedSet(Pattern.With.LowercaseLetter)` |
| `[\s\S]` | `Set(Pattern.With.Whitespace.NonWhitespace)` |

- `Letter` and `HexDigit` are used only when the ranges are written in exactly that order. `[A-Za-z]` becomes `Set(Pattern.With.UppercaseLetter.LowercaseLetter)`, and `[a-fA-F0-9]` becomes `Set(Pattern.With.Range('a', 'f').Range('A', 'F').Range('0', '9'))`. Both match the same characters.
- A class of single characters only becomes `AnyOf(...)` or `NoneOf(...)`. A class that mixes ranges, shorthands or categories becomes `Set(...)` or `NegatedSet(...)`, built member by member.
- See [Character classes](./character-classes.md) for these members.

### Unicode categories and blocks

| Regex | Generated code |
|-------|----------------|
| `\p{L}`, `\P{L}` | `Unicode(UnicodeGroup.Letter)`, `NotUnicode(UnicodeGroup.Letter)` |
| `\p{Lu}` | `Unicode(System.Globalization.UnicodeCategory.UppercaseLetter)` |
| `\P{Nd}` | `NotUnicode(System.Globalization.UnicodeCategory.DecimalDigitNumber)` |
| `\p{IsGreek}` | `UnicodeBlock("Greek")` |
| `\P{IsGreek}` | `RegEx(@"\P{IsGreek}")` |

- The one-letter groups `L`, `M`, `N`, `P`, `S`, `Z` and `C` use the `UnicodeGroup` enum. Two-letter categories use `UnicodeCategory`.
- There is no fluent member for a negated block, so `\P{IsGreek}` is kept as raw regex, also inside a set.
- See [Unicode](./unicode.md) for these members.

### Anchors

| Regex | Generated code |
|-------|----------------|
| `^`, `$` | `StartOfLine`, `EndOfLine` |
| `\A`, `\z` | `StartOfInput`, `EndOfInput` |
| `\b`, `\B` | `WordBoundary`, `NonWordBoundary` |
| `\Z`, `\G` | `RegEx(@"\Z")`, `RegEx(@"\G")` |

See [Assertions](./assertions.md) for anchors and boundaries.

### Quantifiers

| Regex | Generated code |
|-------|----------------|
| `\d+`, `\d{1,}` | `Digit.Repeat.OneOrMore` |
| `\d*` | `Digit.Repeat.ZeroOrMore` |
| `\d?`, `\d{0,1}` | `Digit.Repeat.Optional` |
| `\d{3}` | `Digit.Repeat.Times(3)` |
| `\d{2,}` | `Digit.Repeat.AtLeast(2)` |
| `\d{2,5}` | `Digit.Repeat.Times(2, 5)` |
| `\d{0,3}` | `Digit.Repeat.AtMost(3)` |
| `\d+?`, `\d{2,5}?` | `Digit.Repeat.OneOrMore.Lazy`, `Digit.Repeat.Times(2, 5).Lazy` |

A quantifier after a literal character is written after the literal run it ends. See [A quantifier after a literal repeats one character](#a-quantifier-after-a-literal-repeats-one-character). See [Quantifiers](./quantifiers.md) for the members.

### Groups and alternation

| Regex | Generated code |
|-------|----------------|
| `(\d)` | `Group(Pattern.With.Digit)` |
| `(a\|b)` | `Choice(Pattern.With.Literal("a"), Pattern.With.Literal("b"))` |
| `(?:\d)` | `NonCapturingGroup(Pattern.With.Digit)` |
| `(?:a\|b)`, `a\|b` | `OneOf(Pattern.With.Literal("a"), Pattern.With.Literal("b"))` |
| `(?<y>\d)`, `(?'y'\d)` | `NamedGroup("y", Pattern.With.Digit)` |
| `(?>\d+)` | `AtomicGroup(Pattern.With.Digit.Repeat.OneOrMore)` |
| `(?=\d)`, `(?!\d)` | `PositiveLookahead(...)`, `NegativeLookahead(...)` |
| `(?<=\d)`, `(?<!\d)` | `PositiveLookbehind(...)`, `NegativeLookbehind(...)` |
| `(?:)` | `NonCapturingGroup(Pattern.With)` |

- A capturing group around an alternation becomes `Choice(...)`. A non-capturing group around an alternation, or an alternation with no group, becomes `OneOf(...)`.
- An empty branch or an empty group body is written as `Pattern.With`.
- With `RegexOptions.ExplicitCapture`, a plain `(...)` does not capture, so it becomes `NonCapturingGroup(...)`.
- See [Groups and backreferences](./groups-and-backreferences.md) and [Assertions](./assertions.md).

### Backreferences and conditionals

| Regex | Generated code |
|-------|----------------|
| `(a)\1` | `Group(Pattern.With.Literal("a")).Backreference(1)` |
| `(?<w>a)\k<w>` | `NamedGroup("w", Pattern.With.Literal("a")).Backreference("w")` |
| `(a)?(?(1)b\|c)` | `...Conditional(1, Pattern.With.Literal("b"), Pattern.With.Literal("c"))` |
| `(?<x>a)?(?(x)b)` | `...Conditional("x", Pattern.With.Literal("b"), Pattern.With)` |
| `(?(?=a)ab\|c)` | `RegEx(@"(?(?=a)ab\|c)")` |

- A conditional that tests a group, by number or by name, becomes `Conditional(...)`. When there is no "no" branch, the third argument is the empty `Pattern.With`.
- A conditional that tests an expression has no fluent member, so the whole conditional is kept as raw regex.

### Inline options and comments

| Regex | Generated code |
|-------|----------------|
| `(?i:ab)` | `IgnoreCase(Pattern.With.Literal("ab"))` |
| `(?-i:ab)` | `CaseSensitive(Pattern.With.Literal("ab"))` |
| `(?m:^a)` | `InlineOptions(RegexOptions.Multiline, Pattern.With.StartOfLine.Literal("a"))` |
| `(?i-m:a)` | `InlineOptions(RegexOptions.IgnoreCase, Pattern.With.Literal("a"), RegexOptions.Multiline)` |
| `(?-im:a)` | `InlineOptions(RegexOptions.None, Pattern.With.Literal("a"), RegexOptions.IgnoreCase \| RegexOptions.Multiline)` |
| `(?i)ab` | `RegEx(@"(?i)").Literal("ab")` |
| `a(?#note)b` | `Literal("a").RegEx(@"(?#note)").Literal("b")` |

- A scoped option group, `(?flags:...)`, becomes a member that wraps its body. Turning only `i` on or off uses `IgnoreCase` or `CaseSensitive`. Any other combination uses `InlineOptions(enable, body, disable)`. The `disable` argument is left out when nothing is turned off, and `enable` is `RegexOptions.None` when options are only turned off.
- An option switch without a body, such as `(?i)`, applies to the rest of the enclosing group. It is kept as raw regex so it keeps that reach.
- With `RegexOptions.IgnorePatternWhitespace`, whitespace and `#` comments in the regex are dropped from the code. The option itself is kept in `.WithOptions(...)`. Escaped spaces and `#` stay literal (see [IgnorePatternWhitespace and escaped spaces](#ignorepatternwhitespace-and-escaped-spaces)).

## Constructs kept as raw regex

Some constructs have no fluent member. The converter writes them as `RegEx(...)` with their original text, so the generated code still renders the same regex.

| Construct | Example | Generated code |
|-----------|---------|----------------|
| Balancing group | `(?<o>a)(?<c-o>b)` | `NamedGroup("o", Pattern.With.Literal("a")).RegEx(@"(?<c-o>b)")` |
| Class subtraction | `[a-z-[aeiou]]` | `RegEx(@"[a-z-[aeiou]]")` |
| Negated Unicode block | `\P{IsGreek}` | `RegEx(@"\P{IsGreek}")` |
| End of input or before a final newline | `\Z` | `RegEx(@"\Z")` |
| Contiguous match | `\G` | `RegEx(@"\G")` |
| Expression conditional | `(?(?=a)ab\|c)` | `RegEx(@"(?(?=a)ab\|c)")` |
| Inline option switch | `(?i)` | `RegEx(@"(?i)")` |
| Inline comment | `(?#note)` | `RegEx(@"(?#note)")` |

- Only the construct itself is kept raw. The rest of the pattern is still converted, as the balancing group row shows.
- The raw text is written as a verbatim string, `@"..."`. When it contains a control character, a normal escaped string is used instead.
- Search the generated code for `RegEx(` to find the parts you may want to rewrite by hand.

## Round-tripping

The generated code builds a pattern that matches exactly the same strings as the original. The regex text can differ, because the fluent members escape some characters the original did not.

```csharp
Pattern original = Pattern.FromRegex(@"^(?<user>[\w.]+)@(?<host>[\w-]+\.\w{2,})$");

// Pasted from original.ToFluentCode()
Pattern rebuilt = Pattern.With
    .StartOfLine
    .NamedGroup("user", Pattern.With.Set(Pattern.With.Word.Literal(".")).Repeat.OneOrMore)
    .Literal("@")
    .NamedGroup(
        "host",
        Pattern.With
            .Set(Pattern.With.Word.Literal("-")).Repeat.OneOrMore
            .Literal(".")
            .Word.Repeat.AtLeast(2))
    .EndOfLine;

Console.WriteLine(original);                                          // ^(?<user>[\w.]+)@(?<host>[\w-]+\.\w{2,})$
Console.WriteLine(rebuilt);                                           // ^(?<user>[\w\.]+)@(?<host>[\w\-]+\.\w{2,})$
Console.WriteLine(rebuilt.IsEquivalentTo(original));                  // True
Console.WriteLine(rebuilt.ToFluentCode() == original.ToFluentCode()); // True
```

- `[\w.]` became `[\w\.]`. The extra escapes do not change what the class matches.
- `IsEquivalentTo` proves that both patterns accept exactly the same strings. It works for regular patterns; see [Pattern algebra](./pattern-algebra.md) for what it supports.
- Converting the rebuilt pattern again gives the same code. The conversion is a fixpoint, so you can run it again later without the code drifting.
- Groups keep their kind, name and number, so `Match(...).Groups["host"]` still works on the rebuilt pattern.

The library's own test suite checks this for a corpus of about 60 real-world and edge-case regexes. For each one it compiles the generated code with Roslyn, runs it on 25 matching and 25 non-matching strings, compares the answers with the original regex, and checks that converting again gives the same code.

## Pitfalls

### A quantifier after a literal repeats one character

In a regex, a quantifier repeats only the character just before it: `colou?r` makes only the `u` optional. The converter keeps that meaning, so the code can look misleading:

```csharp
Pattern generated = Pattern.With.Literal("colou").Repeat.Optional.Literal("r");

Console.WriteLine(generated);                  // colou?r
Console.WriteLine(generated.IsMatch("color")); // True

Console.WriteLine(Pattern.With.Literal("ab").Repeat.OneOrMore);                         // ab+
Console.WriteLine(Pattern.With.NonCapturingGroup(Pattern.With.Literal("ab")).Repeat.OneOrMore); // (?:ab)+
```

- `Literal("colou").Repeat.Optional` does not make `colou` optional. It makes the last character, `u`, optional. That is exactly what the original regex did.
- When you edit the code and want to repeat a whole word, wrap it in `NonCapturingGroup(...)` first.

### IgnorePatternWhitespace and escaped spaces

With `RegexOptions.IgnorePatternWhitespace`, an escaped space (`\ `) or an escaped `#` (`\#`) becomes part of a `Literal(...)`. A pattern with that option escapes the space and `#` again when it renders, so the rebuilt pattern means the same as the original:

```csharp
Pattern original = Pattern.FromRegex(@"^order\ \#\d+$", RegexOptions.IgnorePatternWhitespace);

// Pasted from original.ToFluentCode()
Pattern rebuilt = Pattern.With
    .StartOfLine
    .Literal("order #")
    .Digit.Repeat.OneOrMore
    .EndOfLine
    .WithOptions(RegexOptions.IgnorePatternWhitespace);

Console.WriteLine(rebuilt);                       // ^order\ \#\d+$
Console.WriteLine(original.IsMatch("orderly"));   // False
Console.WriteLine(rebuilt.IsMatch("orderly"));    // False
Console.WriteLine(rebuilt.IsMatch("order #42"));  // True
```

- Regexes that only use whitespace for layout, such as `\d+  # digits` on several lines, lose that layout. The option is still kept, because it does not hurt the fluent parts.
- Keep the option when the code contains a `RegEx(...)` part. Raw parts keep their original text, including layout whitespace and comments, so they still need `IgnorePatternWhitespace`. For example, `(?<c-o>b c)` in x-mode is kept as `RegEx(@"(?<c-o>b c)")`.

### Other things to check

- **`(?i)` and other option switches** stay as `RegEx(@"(?i)")`. They change the meaning of everything after them in the same group. Consider replacing them with `IgnoreCase(...)` around the part that needs it, or with `.WithOptions(RegexOptions.IgnoreCase)`.
- **`[RegularExpression]` patterns** from data annotations are tested against the whole value, even without anchors. The converted pattern is not. Add `StartOfInput` and `EndOfInput` when you use it for full-value validation:

```csharp
using System.ComponentModel.DataAnnotations;

RegularExpressionAttribute attribute = new RegularExpressionAttribute(@"\d+");
Pattern converted = Pattern.With.Digit.Repeat.OneOrMore;
Pattern anchored = Pattern.With.StartOfInput.Digit.Repeat.OneOrMore.EndOfInput;

Console.WriteLine(attribute.IsValid("12a")); // False
Console.WriteLine(converted.IsMatch("12a")); // True
Console.WriteLine(anchored.IsMatch("12a"));  // False
```

- **`\d` matches every Unicode digit** in .NET, and so does the generated `Digit`. Call `WithAsciiDigits()` before converting when you want `Range('0', '9')` instead.
- **Review the result.** The code is equivalent to the regex, including its bugs. A good next step is [ReDoS safety](./redos-safety.md) and the [optimizer](./optimizer.md).

## From the command line

The `fluentregex` tool converts regexes without writing any code. Install it once:

```bash
dotnet tool install --global Masterly.FluentRegex.Tool
```

### Converting one regex

`fluentregex convert <regex>` prints the same code as `ToFluentCode()`:

```bash
fluentregex convert '\d{3}-\d{4}'
```

```csharp
Pattern.With
    .Digit.Repeat.Times(3)
    .Literal("-")
    .Digit.Repeat.Times(4)
```

| Option | What it does |
|--------|--------------|
| `--options <list>` | Comma-separated `RegexOptions` names, not case-sensitive, for example `--options IgnoreCase,Multiline`. The code then ends with `.WithOptions(...)`. |
| `--ascii-digits` | Applies `WithAsciiDigits()` before converting, so `\d` becomes `Range('0', '9')`. |

```bash
fluentregex convert 'abc' --options IgnoreCase,Multiline
# Pattern.With.Literal("abc").WithOptions(RegexOptions.IgnoreCase | RegexOptions.Multiline)
```

Errors are printed to standard error and the tool exits with code 2:

```bash
fluentregex convert '(abc'
# error: The regular expression is invalid: Invalid pattern '(abc' at offset 4. Not enough )'s. (Parameter 'regex')

fluentregex convert 'abc' --options Foo
# error: Unknown regex option 'Foo'.
```

### Converting a file or a folder

`fluentregex convert --file <path>` finds the regex literals in one C# file, or in every `.cs` file under a folder, and converts each one. For a file like this:

```csharp
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

public static partial class Validators
{
    private static readonly Regex Sku = new Regex(@"^[A-Z]{3}-\d{4}$", RegexOptions.IgnoreCase);

    public static bool IsZip(string input) => Regex.IsMatch(input, @"^\d{5}(?:-\d{4})?$");

    public static bool Broken(string input) => Regex.IsMatch(input, "(unclosed");
}

public sealed class Customer
{
    [RegularExpression(@"^\+?\d{7,15}$")]
    public string Phone { get; set; } = "";
}
```

`fluentregex convert --file src` prints:

```text
// src\Validators.cs(6,51) new Regex: ^[A-Z]{3}-\d{4}$
Pattern.With
    .StartOfLine
    .UppercaseLetter.Repeat.Times(3)
    .Literal("-")
    .Digit.Repeat.Times(4)
    .EndOfLine
    .WithOptions(RegexOptions.IgnoreCase);

// src\Validators.cs(8,68) Regex.IsMatch: ^\d{5}(?:-\d{4})?$
Pattern.With
    .StartOfLine
    .Digit.Repeat.Times(5)
    .NonCapturingGroup(Pattern.With.Literal("-").Digit.Repeat.Times(4)).Repeat.Optional
    .EndOfLine;

// src\Validators.cs(10,69) Regex.IsMatch: (unclosed
// skipped: The regular expression is invalid: Invalid pattern '(unclosed' at offset 9. Not enough )'s. (Parameter 'regex')

// src\Validators.cs(15,24) [RegularExpression]: ^\+?\d{7,15}$
Pattern.With
    .StartOfLine
    .Literal("+").Repeat.Optional
    .Digit.Repeat.Times(7, 15)
    .EndOfLine;

3 regex literal(s) converted. 1 skipped as invalid.
```

- Each result starts with a comment that gives the file, the line and column of the string literal, where it was found, and the original regex. The code ends with `;`, ready to paste.
- The file path is relative to the current folder and uses the separator of your operating system. The output above is from Windows. A file outside the current folder is shown with the path as it was found, based on the path you passed.
- The tool finds string literals in `new Regex(...)`, in the static `Regex` methods (`IsMatch`, `Match`, `Matches`, `Replace`, `Split`, `Count`, `EnumerateMatches`, `EnumerateSplits`), in `[GeneratedRegex(...)]` and in `[RegularExpression(...)]`. Verbatim (`@"..."`) and raw (`"""..."""`) string literals are found. It reads options written as `RegexOptions.X | RegexOptions.Y` in the same call or attribute.
- A regex in a constant, a variable or an interpolated string is not found. The tool reads the source text and does not resolve symbols. For the same reason, the type must be written as `Regex` or `System.Text.RegularExpressions.Regex`: a target-typed `new(@"...")` assigned to a `Regex` field is not found.
- An invalid regex is reported with `// skipped:` and does not stop the run.
- In a folder, files under `bin`, `obj`, `.git` and `node_modules` are skipped. Files are processed in path order.
- The code goes to standard output. The final count goes to standard error, so `fluentregex convert --file src > converted.cs` writes only code. Skipped literals are counted separately.
- `--options` and `--ascii-digits` are not used with `--file`. Each literal keeps the options found next to it in the source.
- The command exits with 0, even when some literals were skipped. A path that does not exist exits with 2.

The tool runs on .NET 8 or later. See [Command-line tool](./command-line-tool.md) for every command, including `scan`, which finds ReDoS risks in the same kinds of literals.

## Related pages

- [Core concepts](./core-concepts.md) explains how a `Pattern` is built and rendered.
- [Pattern algebra](./pattern-algebra.md) proves two patterns equivalent, which is how you check a hand-edited conversion.
- [Optimizer](./optimizer.md) simplifies a regex before or after you convert it.
- [Learning from examples](./learning-from-examples.md) creates a pattern from sample strings. Its result converts to fluent code the same way.
- [Compile-time checks](./compile-time-checks.md) catches invalid regex literals during the build.
