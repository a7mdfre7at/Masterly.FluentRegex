---
id: upgrading-from-2x
title: "Upgrading from 2.x"
description: "What changed between 2.x and 3.x: immutable patterns, corrected regex output, new exceptions and build checks, and a step-by-step upgrade checklist."
sidebar_label: "Upgrading from 2.x"
---

Most 2.x code compiles against 3.x without changes. No public member was removed or renamed. What changed is behaviour: patterns are now immutable, several builder members produce corrected regex text, invalid calls throw, and the package now checks your patterns at build time. Read this page before you upgrade, then work through the [upgrade checklist](#upgrade-checklist) at the end.

:::note[Which version you land on]

On NuGet.org, the release after 2.0.0 is 3.1.0. If you install from NuGet, the 3.0 changes and the [3.1 notes](#upgrading-from-30-to-31) both apply to you. 3.1 has no breaking changes to the 3.0 API.

:::

## What changed at a glance

| Change | Who is affected | What to do |
|--------|-----------------|------------|
| [Patterns are immutable](#patterns-are-immutable) | Code that calls a member as a statement and ignores the result, such as `pattern.WithOptions(...);` | Assign the result. |
| [`Literal` inside a `Set` escapes `-`](#literal-inside-a-set-escapes-the-hyphen) | Code that wrote a range as `Set(Literal("a-f"))` | Use `Range('a', 'f')`. |
| [`Letter`, `LowercaseLetter`, `UppercaseLetter` outside a `Set`](#letter-lowercaseletter-and-uppercaseletter-outside-a-set) | Nobody, unless you relied on the broken output | Nothing. |
| [`Repeat.AtMost(n)`](#repeatatmostn) | Nobody, unless you relied on the broken output | Nothing. |
| [A quantifier on a quantifier](#a-quantifier-on-a-quantifier) | Nobody, unless you relied on the broken output | Nothing. |
| [Invalid builder calls throw](#invalid-builder-calls-throw) | Code that built an invalid pattern or passed `null` | Fix the call. |
| [`ToRegex()` uses the pattern's options](#toregex-and-options) | Code that relied on `ToRegex()` ignoring `WithOptions` | Usually nothing. Rebuild every assembly. |
| [`StrongPassword()` and `Url()` were fixed](#common-patterns-that-changed) | Code that calls them | Check the inputs you accept. |
| [Build-time checks](#new-build-time-checks) | Constant patterns that are invalid or risky | Fix the errors and review the warnings. |
| [Target frameworks](#target-frameworks) | Nobody: the .NET Standard 2.1 build is still there | Nothing. |
| [Value equality](#patterns-compare-by-value) (3.1) | Code that uses patterns as dictionary keys or calls `Equals` | Check the code that relied on reference equality. |

## Patterns are immutable

In 2.x, every member changed the pattern it was called on and returned that same instance. In 3.x, every member returns a **new** pattern and leaves the original unchanged. This is the change most likely to affect your code.

```csharp
using System;
using Masterly.FluentRegex;

Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;
Pattern withDash = digits.Literal("-");
Pattern withDot = digits.Literal(".");

Console.WriteLine(digits);    // \d+
Console.WriteLine(withDash);  // \d+-
Console.WriteLine(withDot);   // \d+\.
```

- In 2.x, all three variables pointed at the same object, and all three lines printed `\d+-\.`.
- In 3.x, `digits` stays `\d+`, so you can extend it in as many directions as you like.
- `Pattern.With` and `new Pattern()` still start a new, empty pattern.

### Assign the result of every call

Code that called a member as a statement relied on the old mutation. It still compiles, but the call now has no effect.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern greeting = Pattern.With.Literal("hello");
greeting.WithOptions(RegexOptions.IgnoreCase);     // the new pattern is thrown away
Console.WriteLine(greeting.IsMatch("HELLO"));      // False

greeting = greeting.WithOptions(RegexOptions.IgnoreCase);
Console.WriteLine(greeting.IsMatch("HELLO"));      // True
```

The first `IsMatch` printed `True` in 2.x. In 3.x the options are lost because the result of `WithOptions` was never stored.

The same applies when you build a pattern step by step, for example in a loop or behind an `if`. Assign each step back to the variable:

```csharp
string[] segments = { "api", "v1", "orders" };

Pattern route = Pattern.With.StartOfLine;
foreach (string segment in segments)
{
    route = route.Literal("/").Literal(segment);
}
route = route.EndOfLine;

Console.WriteLine(route);                            // ^/api/v1/orders$
Console.WriteLine(route.IsMatch("/api/v1/orders"));  // True
```

In 2.x, `route.Literal("/").Literal(segment);` without the assignment also worked. In 3.x it builds a pattern and discards it.

:::warning

The C# compiler does not warn about a discarded `Pattern`, so search your code for it. Look for statements that start with a pattern variable and end with a method call, such as `pattern.Literal(...);`, `pattern.WithOptions(...);` or `pattern.Group(...);`. A statement that ends with a property, such as `pattern.Digit;`, never compiled, so you will not find those.

:::

To let the build find them, turn on the IDE0058 rule ("expression value is never used") while you upgrade. Add this to the project file:

```xml
<PropertyGroup>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
</PropertyGroup>
```

And this to `.editorconfig`:

```ini
[*.cs]
csharp_style_unused_value_expression_statement_preference = discard_variable:warning
dotnet_diagnostic.IDE0058.severity = warning
```

The build then reports `warning IDE0058: Expression value is never used` for each `greeting.WithOptions(RegexOptions.IgnoreCase);`. Two limits:

- IDE0058 reports every discarded value, not only patterns, so expect noise from calls such as `StringBuilder.Append`. Turn it off again once the upgrade is done.
- With the .NET 10 SDK, it does not report discarded values in top-level statements (a `Program.cs` without a `Main` method). It reports them only inside methods, so search top-level code by hand.

### What immutability gives you

Immutability fixes several 2.x problems and makes patterns safer to share.

**Building blocks stay intact.** Store a pattern in a `static readonly` field and reuse it in as many other patterns as you like. Nothing you build from it can change it. See [Composing Patterns](./composing-patterns.md).

**Patterns are safe to share across threads.** In 2.x, two threads that extended the same shared pattern changed it under each other. A 3.x pattern never changes after it is created.

**The compiled regex is never stale.** A pattern compiles its `Regex` on first use and caches it. In 2.x, extending a pattern after it had matched kept the old cached regex:

```csharp
Pattern cat = Pattern.With.Literal("cat");
Console.WriteLine(cat.IsMatch("cat"));       // True

Pattern catDog = cat.Literal("dog");
Console.WriteLine(catDog.IsMatch("cat"));    // False
Console.WriteLine(catDog.IsMatch("catdog")); // True
```

In 2.x, `catDog.IsMatch("cat")` returned `True`: the pattern text was `catdog`, but matching still used the cached regex for `cat`.

**Performance notes**

- Extending a pattern is cheap: it adds one element to a shared, persistent list instead of copying the whole pattern.
- Each pattern instance compiles and caches its own regex. A pattern that you build again on every call compiles again on every call, so build it once and keep it in a `static readonly` field.
- `ToRegex()`, `ToRegex(options)` and `IsMatch(input, options)` create a new `Regex` every time. For repeated matching, call `WithOptions` once and use `IsMatch(input)`, which uses the cached regex.

### Patterns compare by value

This change arrived in 3.1. `Pattern` now implements `IEquatable<Pattern>`. Two patterns are equal when they render the same regex text with the same options and timeout, however they were built.

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern first = Pattern.With.Digit.Repeat.OneOrMore;
Pattern second = Pattern.With.RegEx(@"\d+");

Console.WriteLine(first.Equals(second));                                     // True
Console.WriteLine(first == second);                                          // False
Console.WriteLine(first.Equals(first.WithOptions(RegexOptions.IgnoreCase))); // False

HashSet<Pattern> unique = new HashSet<Pattern> { first, second };
Console.WriteLine(unique.Count);                                             // 1
```

- In 2.x, `Equals` compared references, so `first.Equals(second)` was `False`.
- `==` and `!=` are not overloaded. They still compare references.
- A `Dictionary` or `HashSet` keyed by `Pattern` now treats equal patterns as one key. Check code that relied on two separately built patterns being different keys.

## Builder output changes

Several 2.x builder members produced regex text that did not mean what their name said. 3.x fixes them. The fixes change the regex text, so tests that compare `pattern.ToString()` with a stored string may need new expected values. The table shows the output of 2.0.0 and 3.x for the same code.

| Code | 2.x output | 3.x output |
|------|------------|------------|
| `Set(Literal("a-f"))` | `[a-f]` (a range) | `[a\-f]` (three characters) |
| `Set(Literal(".-_"))` | `[\.-_]` (a range) | `[\.\-_]` (three characters) |
| `Letter` | `a-zA-Z` | `[a-zA-Z]` |
| `LowercaseLetter` | `a-z` | `[a-z]` |
| `UppercaseLetter` | `A-Z` | `[A-Z]` |
| `Digit.Repeat.AtMost(3)` | `\d{,3}` | `\d{0,3}` |
| `Digit.Repeat.Times(2).Repeat.Times(3)` | `\d{2}{3}` (invalid) | `(?:\d{2}){3}` |

### Literal inside a Set escapes the hyphen

In 2.x, a hyphen in a `Literal` inside a `Set` created a range. `Set(Literal(".-_"))` was meant as three separator characters, but it matched all 50 characters from `.` to `_`, including digits, uppercase letters, `@` and `/`. In 3.x, every character of a `Literal` is literal, inside a set or outside it. Use `Range` when you want a range.

```csharp
Console.WriteLine(Pattern.With.Set(Pattern.With.Literal("a-f")));   // [a\-f]
Console.WriteLine(Pattern.With.Set(Pattern.With.Range('a', 'f')));  // [a-f]
Console.WriteLine(Pattern.With.Range('a', 'f'));                    // [a-f]

Pattern separators = Pattern.With.Set(Pattern.With.Literal(".-_"));
Console.WriteLine(separators.IsMatch("@"));                         // False
Console.WriteLine(separators.IsMatch("-"));                         // True
```

- `Range(from, to)` is new in 3.0. On its own it renders a bracketed class. Inside a `Set` it renders just the range, so you can combine it with other members.
- `Range` throws `ArgumentException` when `from` is greater than `to`.
- If your 2.x code wrote ranges with `Literal`, replace each one with `Range`. See [Character Classes](./character-classes.md).

### Letter, LowercaseLetter and UppercaseLetter outside a Set

In 2.x, these three rendered a bare range such as `a-zA-Z`, which only worked inside a `Set`. On their own they matched that literal text. In 3.x they render a full class on their own and the bare range inside a `Set`:

```csharp
Console.WriteLine(Pattern.With.Letter);                               // [a-zA-Z]
Console.WriteLine(Pattern.With.LowercaseLetter.Repeat.OneOrMore);     // [a-z]+
Console.WriteLine(Pattern.With.Set(Pattern.With.Letter.Digit));       // [a-zA-Z\d]
Console.WriteLine(Pattern.With.Letter.IsMatch("x"));                  // True
```

`Pattern.With.Letter.IsMatch("x")` returned `False` in 2.x. Code inside a `Set` renders exactly as before.

### Repeat.AtMost(n)

In 2.x, `AtMost(n)` rendered `{,n}`. .NET does not support that shorthand and matches it as literal text. In 3.x it renders `{0,n}`:

```csharp
Pattern upToThree = Pattern.With.StartOfLine.Digit.Repeat.AtMost(3).EndOfLine;

Console.WriteLine(upToThree);                 // ^\d{0,3}$
Console.WriteLine(upToThree.IsMatch("12"));   // True
Console.WriteLine(upToThree.IsMatch("1234")); // False
```

In 2.x, the same pattern rejected `"12"` and accepted the text `"1{,3}"`.

### A quantifier on a quantifier

In 2.x, adding a quantifier to an element that already had one produced an invalid regex such as `\d{2}{3}`. In 3.x, the quantified element is wrapped in a non-capturing group first:

```csharp
Console.WriteLine(Pattern.With.Digit.Repeat.Times(2).Repeat.Times(3));   // (?:\d{2}){3}
Console.WriteLine(Pattern.With.Digit.Repeat.OneOrMore.Repeat.Optional);  // \d+?
Console.WriteLine(Pattern.With.Digit.Repeat.OneOrMore.Lazy);             // \d+?
```

- `Repeat.Optional` directly after a quantifier is the one exception. It still produces the lazy form, as it did in 2.x, so existing code keeps its meaning.
- Prefer the new `.Lazy` for lazy quantifiers. The 2.x README documented `.Lazy`, but 2.x did not have it. 3.0 added `.Lazy` and `.Possessive`. See [Quantifiers](./quantifiers.md).

### What did not change

- `Choice(...)` still creates a numbered capturing group, as in 2.x. Use the new `OneOf(...)` or the `|` operator when you do not want one.
- `Literal` outside a set escapes the same characters as before.
- `Explain()`, `IsValid()`, `TryValidate(out string)`, `GetGroupCount()` and `GetGroupNames()` return the same results.
- `\d` still matches every Unicode decimal digit, as it did in 2.x. See [Unicode digits](#unicode-digits).

```csharp
Pattern pet = Pattern.With.Choice(Pattern.With.Literal("cat"), Pattern.With.Literal("dog"));

Console.WriteLine(pet);                                                         // (cat|dog)
Console.WriteLine(Pattern.With.OneOf("cat", "dog"));                            // (?:cat|dog)
Console.WriteLine(Pattern.With.Literal("cat") | Pattern.With.Literal("dog"));   // (?:cat|dog)
```

## Invalid builder calls throw

2.x built whatever you asked for, even when the result was not a valid regex. You found out when the pattern was first used, or never. 3.x checks the arguments when you build the pattern.

| Call | 2.x | 3.x |
|------|-----|-----|
| `Pattern.With.Repeat.OneOrMore` (nothing to repeat) | `+` | `InvalidOperationException` |
| `Repeat.Times(-1)` | `\d{-1}` | `ArgumentOutOfRangeException` (`exactly`) |
| `Repeat.Times(5, 2)` | `\d{5,2}` | `ArgumentOutOfRangeException` (`max`) |
| `Repeat.Times(-1, 2)` | `\d{-1,2}` | `ArgumentOutOfRangeException` (`min`) |
| `Repeat.AtLeast(-2)` | `\d{-2,}` | `ArgumentOutOfRangeException` (`x`) |
| `Repeat.AtMost(-1)` | `\d{,-1}` | `ArgumentOutOfRangeException` (`x`) |
| `Literal((string)null)` | `NullReferenceException` | `ArgumentNullException` (`literalChars`) |
| `Group(null)` | `()` | `ArgumentNullException` (`pattern`) |
| `Set(null)`, `NegatedSet(null)`, `NamedGroup("x", null)` | `[]`, `[^]`, `(?<x>)` | `ArgumentNullException` (`pattern`) |
| `Choice(Digit, null)` | `(\d\|)` | `ArgumentNullException` (`secondChoice`) |
| `new Repeat(null)` | `NullReferenceException` on first use | `ArgumentNullException` (`parentPattern`) |
| `Backreference(-1)` | `\-1` | `ArgumentOutOfRangeException` (`groupNumber`) |
| `Backreference(0)` | `\0` (the NUL character, not a backreference) | `ArgumentOutOfRangeException` (`groupNumber`) |
| `NamedGroup("my-name", Digit)` | `(?<my-name>\d)` (invalid) | `ArgumentException` (`name`) |
| `NamedGroup("", Digit)` | `(?<>\d)` (invalid) | `ArgumentException` (`name`) |
| `Backreference("a-b")` | `\k<a-b>` (invalid) | `ArgumentException` (`name`) |

`NonCapturingGroup`, `AtomicGroup` and the four lookaround members also throw `ArgumentNullException` for a `null` pattern. `Conditional(groupNumber, ...)` throws `ArgumentOutOfRangeException` for a group number below 1, and `Conditional(groupName, ...)` checks the name like `NamedGroup` does. A group name must be non-empty and contain only letters, digits and underscores.

Members that are new in 3.x validate their arguments too: `.Lazy` and `.Possessive` throw `InvalidOperationException` unless they directly follow a quantifier, `Range` throws `ArgumentException` for a reversed range, `OneOf` throws `ArgumentException` without alternatives, and `WithTimeout` throws `ArgumentOutOfRangeException` for zero, negative or too long timeouts.

When the arguments come from data, catch the exception where you build the pattern:

```csharp
// maximumLength comes from your application's configuration
int maximumLength = -1;

try
{
    Pattern code = Pattern.With.Word.Repeat.AtMost(maximumLength);
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine(ex.Message);   // The maximum cannot be negative. (Parameter 'x')
}
```

When the arguments are constants, you will not get this far: the build fails with FRX006. See [New build-time checks](#new-build-time-checks).

:::note

Some mistakes are still found only when the regex is created. Raw text passed to `RegEx(...)` is not checked when you build the pattern. The group-name check accepts any mix of letters, digits and underscores, so `NamedGroup("1x", Pattern.With.Digit)` builds `(?<1x>\d)` without throwing, even though .NET rejects a name that starts with a digit. Use `IsValid()` or `TryValidate(out string error)` for patterns built from data, and see [Validation and Debugging](./validation-and-debugging.md). For constant patterns, the build reports these as FRX003.

:::

## ToRegex and options

In 2.x, `ToRegex(RegexOptions options = RegexOptions.None)` was a single method with an optional parameter. Calling `ToRegex()` passed `None` and ignored the options you had set with `WithOptions`. 3.x splits it into two overloads:

| Member | Options | Timeout |
|--------|---------|---------|
| `ToRegex()` | The pattern's own `Options` | The pattern's own `Timeout` |
| `ToRegex(options)` | Exactly `options`, instead of the pattern's own | The pattern's own `Timeout` |
| `IsMatch(input, options)` | Exactly `options`, instead of the pattern's own | The pattern's own `Timeout` |

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern hello = Pattern.With.Literal("hello")
    .WithOptions(RegexOptions.IgnoreCase)
    .WithTimeout(TimeSpan.FromSeconds(1));

Regex regex = hello.ToRegex();
Console.WriteLine(regex.Options);                              // IgnoreCase
Console.WriteLine(regex.MatchTimeout);                         // 00:00:01

Regex exact = hello.ToRegex(RegexOptions.None);
Console.WriteLine(exact.IsMatch("HELLO"));                     // False
Console.WriteLine(exact.MatchTimeout);                         // 00:00:01
Console.WriteLine(hello.IsMatch("HELLO", RegexOptions.None));  // False

Regex multiline = hello.ToRegex(hello.Options | RegexOptions.Multiline);
Console.WriteLine(multiline.Options);                          // IgnoreCase, Multiline
```

- To add an option on top of the pattern's own, combine them yourself, as in the last example.
- `Options`, `Timeout` and `IsEmpty` are new read-only properties in 3.0.
- `WithTimeout` and `UseNonBacktracking()` are new in 3.0. `UseNonBacktracking()` needs .NET 8 or later. See [ReDoS Safety](./redos-safety.md).

### Rebuild everything that references the package

An assembly compiled against 2.x still loads and runs with 3.x. Builder changes, such as the `Letter` and `AtMost` fixes, take effect without a rebuild because they live inside the library.

`ToRegex()` is the exception. The old assembly was compiled to call `ToRegex(RegexOptions.None)`, and it keeps doing so until you rebuild it:

```csharp
// In a library compiled against 2.x and not rebuilt
Regex regex = Pattern.With.Literal("hello").WithOptions(RegexOptions.IgnoreCase).ToRegex();
Console.WriteLine(regex.Options);   // None
```

After you rebuild the library against 3.x, the same line prints `IgnoreCase`. Rebuild every project that references Masterly.FluentRegex, including your own shared libraries, and get updated builds of third-party packages that depend on it.

## Common patterns that changed

Two `CommonPatterns` methods match different inputs in 3.x.

**`StrongPassword()`.** In 2.x, its special-character set contained the range `+` to `=`. Every digit, and characters such as `.`, `/`, `:` and `<`, counted as a special character, so a password with no symbol passed.

```csharp
Pattern password = CommonPatterns.StrongPassword();

Console.WriteLine(password.IsMatch("Abcdefg1"));    // False
Console.WriteLine(password.IsMatch("Abcdefg1!"));   // True
```

`"Abcdefg1"` passed in 2.x. In 3.x the special character must be one of `!@#$%^&*()_+-=`.

**`Url()`.** 2.x had three problems: dots in the first host label caused polynomial backtracking, the host could start with a dot or contain `..`, and the path class contained the range `.` to `~`. The range let `<`, `>`, `{` and `}` through, but left out `-`.

```csharp
Pattern url = CommonPatterns.Url();

Console.WriteLine(url.IsMatch("https://docs.example.com/a/b?x=1")); // True
Console.WriteLine(url.IsMatch("https://example.com/my-page"));      // True
Console.WriteLine(url.IsMatch("https://a..b.com"));                 // False
Console.WriteLine(url.IsMatch("https://example.com/a<b>"));         // False
```

| Input | 2.x | 3.x |
|-------|-----|-----|
| `https://example.com/my-page` | `False` | `True` |
| `https://a..b.com` | `True` | `False` |
| `https://.example.com` | `True` | `False` |
| `https://example.com/a<b>` | `True` | `False` |
| `https://example.com/a{b}` | `True` | `False` |

Running `AnalyzeSafety()` on the 2.x regex reports `AdjacentOverlappingQuantifiers` at Medium risk. The 3.x pattern reports no risk.

**Other common patterns.** Every other method that existed in 2.x accepts exactly the same strings in 3.x. Comparing each 2.x regex with its 3.x pattern proves them equivalent. `CreditCard`, `Date`, `Email`, `FilePath`, `HtmlTag`, `PhoneNumber` and `ScientificNotation` do render slightly different text, because a hyphen inside a set is now always escaped (`[\s\-]` instead of `[\s-]`). The hyphen was already literal in those positions. You can run the same proof with [pattern algebra](./pattern-algebra.md):

```csharp
// The regex text CommonPatterns.CreditCard() produced in 2.x
Pattern creditCard2x = Pattern.With.RegEx(@"^\d{4}(?:[\s-]?\d{4}){3}$");

Console.WriteLine(creditCard2x.IsEquivalentTo(CommonPatterns.CreditCard()));  // True
```

The remaining methods, `DateIso`, `Decimal`, `Guid`, `HexColor`, `IPv4`, `IPv6`, `Integer`, `SocialSecurityNumber`, `Time12`, `Time24`, `Username` and `ZipCode`, render exactly the same text as in 2.x. 3.x also adds many new common patterns and `PatternStrictness` levels for `Email` and `Url`. See [Common Patterns](./common-patterns.md).

## New build-time checks

The 3.x package includes Roslyn analyzers, so installing it is enough to turn them on. They evaluate every fluent chain built only from constants, so they also check the patterns you already have. A 2.x pattern that was invalid or risky can fail the build after the upgrade.

| ID | Severity | Reported when |
|----|----------|---------------|
| FRX003 | Error | A constant pattern produces an invalid regex, for example `Pattern.With.RegEx("(abc")`. |
| FRX004 | Warning | A constant pattern has a high ReDoS risk, such as a nested quantifier `(\w+)+`. |
| FRX005 | Info | A constant pattern may backtrack polynomially. |
| FRX006 | Error | Building a constant pattern throws, for example `Pattern.With.Repeat.OneOrMore` or `Repeat.Times(5, 2)`. |
| FRX007 | Info | A constant pattern in a field or property could be backed by a .NET `[GeneratedRegex]` method. A code fix adds it. |

FRX001, FRX002, FRX008 and FRX009 only concern features that are new in 3.0 (`[GeneratePattern]` and `Extract<T>`), so 2.x code does not trigger them.

For example, this line compiled under 2.x:

```csharp
public static readonly Pattern Separator = Pattern.With.Repeat.OneOrMore;
```

After the upgrade it fails the build:

```text
error FRX006: This pattern throws InvalidOperationException when it is built: Repeat must follow an element to repeat, for example Pattern.With.Digit.Repeat.OneOrMore.
```

- Fix FRX003 and FRX006: the pattern was already broken in 2.x, but you found out only at run time, or never.
- FRX004 is a warning. In a project with `TreatWarningsAsErrors`, it fails the build too. Make the inner repetition `.Possessive`, wrap it in `AtomicGroup(...)`, or rewrite it. See [ReDoS Safety](./redos-safety.md).
- Patterns built from runtime values are not evaluated at build time. They are checked when you build them, as described in [Invalid builder calls throw](#invalid-builder-calls-throw).
- The analyzers need the C# compiler from the .NET 8 SDK or later (Roslyn 4.8).

To change a severity while you work through the findings, use `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.FRX004.severity = suggestion
```

See [Compile-Time Checks](./compile-time-checks.md) for every diagnostic and the `[GeneratePattern]` source generator.

## Target frameworks

| Package version | Targets |
|-----------------|---------|
| 2.0.0 | `netstandard2.1` |
| 3.0.0 | `netstandard2.1`, `net8.0`, `net9.0` |
| 3.1.0 | `netstandard2.1`, `net8.0`, `net10.0` |

- Anything that ran 2.x still gets the `netstandard2.1` build, so you do not need to change your target framework.
- The `net8.0` and `net10.0` builds are trimming and Native AOT compatible.
- Some members only exist in the newer builds:

| Member | Needs |
|--------|-------|
| `UseNonBacktracking()` | .NET 8 or later |
| `IsMatch(ReadOnlySpan<char>)`, `Count(ReadOnlySpan<char>)`, `EnumerateMatches(ReadOnlySpan<char>)` | .NET 8 or later |
| `[MatchesPattern]` (DataAnnotations) | .NET 8 or later |
| `EnumerateSplits(ReadOnlySpan<char>)` | .NET 10 or later (.NET 9 in 3.0) |

The companion packages added in 3.1 (Masterly.FluentRegex.AspNetCore, .Xunit, .AI and .EntityFrameworkCore) target `net8.0` and `net10.0` only. See [Installation](./installation.md).

## Upgrading from 3.0 to 3.1

3.1 has no breaking changes to the 3.0 API, and the core package has no new dependencies. Check these points:

**The .NET 9 build is gone.** .NET 9 left support in May 2026, so 3.1 ships `net8.0` and `net10.0` instead of `net9.0`. A .NET 9 app keeps working on the `net8.0` build. `EnumerateSplits(ReadOnlySpan<char>)` is the only member that it loses, because that member now needs .NET 10.

**A new `Validators` class.** 3.1 adds `Masterly.FluentRegex.Validators`, with the semantic checks `Luhn`, `IbanChecksum`, `IsoDate` and `IPv4`. If your code has its own `Validators` type and imports both namespaces, the name becomes ambiguous:

```text
error CS0104: 'Validators' is an ambiguous reference between 'MyCompany.Validation.Validators' and 'Masterly.FluentRegex.Validators'
```

Add a using alias to pick yours:

```csharp
using System;
using Masterly.FluentRegex;
using MyCompany.Validation;                          // your own namespace
using Validators = MyCompany.Validation.Validators;

// Calls your own Validators class, not the library's
Console.WriteLine(Validators.IsPostcode("12345"));   // True
```

The same can happen with any type that 3.x added to the `Masterly.FluentRegex` namespace. If you upgrade straight from 2.x, also look for your own types named, for example, `Template`, `Replacement`, `Redactor`, `RiskLevel`, `MatchScope` or `DigitMode`. A type declared in the same namespace as the code that uses it wins without an error, so the clash appears only when the namespace is imported with `using`.

**Patterns compare by value.** See [Patterns compare by value](#patterns-compare-by-value).

**Extraction reads every Unicode digit.** `Extract<T>` and templates now parse numbers written with any Unicode decimal digits, such as Arabic-Indic "٢٠٢٦", which `\d` already matched. This is a 3.1 fix. See [Typed Extraction](./typed-extraction.md).

**The CLI writes UTF-8.** If a script reads the output of the `fluentregex` tool, it now receives UTF-8. See [Command-Line Tool](./command-line-tool.md).

### Unicode digits

`\d` matches every Unicode decimal digit, not only 0-9. This is .NET's behaviour, and it is the same in 2.x, 3.0 and 3.1. As a result, the parameterless `CommonPatterns` methods accept digits such as "٩". 3.1 adds a way to opt out:

```csharp
Console.WriteLine(CommonPatterns.IPv4().IsMatch("0.0.0.٩"));                     // True
Console.WriteLine(CommonPatterns.IPv4(DigitMode.Ascii).IsMatch("0.0.0.٩"));      // False
Console.WriteLine(CommonPatterns.IPv4().WithAsciiDigits().IsMatch("0.0.0.٩"));   // False
```

Pass `DigitMode.Ascii`, or call `WithAsciiDigits()` on any pattern, when only 0-9 are valid. See [Unicode](./unicode.md).

## Your own extension methods

If you wrote extension methods for `Pattern` under 2.x, check their names against the members 3.x added, such as `Count`, `OneOf`, `Then`, `Trace`, `Highlight`, `Optimize`, `Extract` and `Must`. When an instance method and an extension method have the same name and parameters, C# calls the instance method and reports nothing:

```csharp
using System;
using Masterly.FluentRegex;

Pattern word = Pattern.With.Word.Repeat.OneOrMore;
Console.WriteLine(word.Count("one two three"));   // 3

public static class PatternExtensions
{
    // Written for 2.x, which had no Count method. 3.x never calls it.
    public static int Count(this Pattern pattern, string input) => -1;
}
```

Rename or remove such extensions so that every call site does what you expect.

## Proving your own patterns did not change

The safest way to upgrade is to compare each pattern's regex before and after. Before you upgrade, save `pattern.ToString()` for the patterns you care about, for example from a unit test that writes them to a file. After the upgrade, compare each saved regex with the new pattern:

```csharp
using System;
using Masterly.FluentRegex;

// The regex text this pattern produced under 2.x, saved before the upgrade
string regexIn2x = @"^[\.-_]+$";

Pattern before = Pattern.With.RegEx(regexIn2x);
Pattern after = Pattern.With.StartOfLine.Set(Pattern.With.Literal(".-_")).Repeat.OneOrMore.EndOfLine;

PatternComparison comparison = Pattern.Compare(before, after);
Console.WriteLine(after);                     // ^[\.\-_]+$
Console.WriteLine(comparison.AreEquivalent);  // False
Console.WriteLine(comparison);
// Not equivalent (the patterns overlap). Shortest counterexample: "A" (left accepts it, right rejects it); also "-" (right accepts it, left rejects it). Both accept ".".
```

- A different regex text is not a problem by itself. `AreEquivalent` tells you whether the two patterns accept exactly the same strings.
- When they differ, the comparison gives you the shortest string that only one of them accepts. Here it shows that the 2.x pattern accepted `A` and rejected `-`.
- Comparison works on the regular subset of .NET regex. Lookaheads are supported only at the start of a pattern. Other constructs, such as backreferences, throw `NotSupportedException`. See [Pattern Algebra](./pattern-algebra.md).

## Building from source

If you build the package from source rather than installing it from NuGet: `GeneratePackageOnBuild` is now off. Run `dotnet pack` to create the packages.

## Upgrade checklist

1. **Save a snapshot.** Before you change the package version, record `pattern.ToString()` for your important patterns, and make sure your tests pass on 2.x.
2. **Update the package.** Run `dotnet add package Masterly.FluentRegex` in every project that uses it. You do not need to change your target framework.
3. **Rebuild everything.** Rebuild every project and library that references Masterly.FluentRegex, so that `ToRegex()` calls bind to the new overload.
4. **Fix the build errors.** FRX003 and FRX006 point at patterns that were already broken in 2.x. Fix CS0104 for `Validators` or another new type name with a using alias.
5. **Review the build warnings.** FRX004 marks patterns with a high ReDoS risk, and FRX005 (info) marks polynomial backtracking.
6. **Find discarded results.** Search for statements such as `pattern.WithOptions(...);` and assign the result, or turn on IDE0058 temporarily to find them.
7. **Replace ranges written as literals.** Change `Set(Literal("a-z"))` to `Set(Range('a', 'z'))`.
8. **Check common patterns.** Review the inputs you accept with `StrongPassword()` and `Url()`. Use `DigitMode.Ascii` where only 0-9 are valid.
9. **Check equality.** Review dictionaries, sets and `Equals` calls that use `Pattern` and relied on reference equality.
10. **Check your extension methods** on `Pattern` for names that 3.x now defines.
11. **Compare with the snapshot.** Use `Pattern.Compare` on each saved regex. Update tests that assert regex text only after you confirm the meaning is what you want.
12. **Run your tests.** Then consider the new tools for testing patterns: [Regex Coverage](./regex-coverage.md), [Property Testing](./property-testing.md) and [Test Data Generation](./test-data-generation.md).

For the full list of changes, see the [CHANGELOG](https://github.com/a7mdfre7at/Masterly.FluentRegex/blob/master/CHANGELOG.md). For the new features you can adopt after the upgrade, start with [Core Concepts](./core-concepts.md) and [Best Practices](./best-practices.md).
