---
id: xunit
title: "xUnit Theory Data"
description: "[MatchingData] and [NonMatchingData] feed xUnit v3 theories with reproducible strings a pattern matches, or near misses it rejects."
---

The `Masterly.FluentRegex.Xunit` package turns a pattern into rows for an xUnit v3 theory. `[MatchingData]` supplies strings the pattern matches. `[NonMatchingData]` supplies strings it rejects, most of them near misses one small edit away from a valid sample. Use them to test code that must agree with a pattern, such as a hand-written validator, a parser or an input normalizer, without typing the examples yourself.

| Attribute | Each row is | Generated with |
|-----------|-------------|----------------|
| `[MatchingData]` | A string the pattern matches. | `Pattern.GenerateMatching(count, seed)` |
| `[NonMatchingData]` | A string the pattern does not match. | `Pattern.GenerateNonMatching(count, seed)` |

Both attributes live in the `Masterly.FluentRegex.Xunit` namespace. They are thin wrappers over the generators described in [Test Data Generation](./test-data-generation.md), so everything that page says about how samples are built applies here too.

:::tip[What you need]

- The package targets `net8.0` and `net10.0`, so your test project must target .NET 8 or later. There is no `netstandard2.1` build.
- It works with **xUnit v3** only. It depends on `xunit.v3.extensibility.core` 3.2.1, so use `xunit.v3` 3.2.1 or later. It does not work with xUnit v2 (`xunit` 2.x).
- It brings in the core `Masterly.FluentRegex` package of the same version.

:::

## Installing

Add the package to your test project:

```bash
dotnet add package Masterly.FluentRegex.Xunit
```

A minimal xUnit v3 test project looks like this. xUnit v3 test projects are executables, so `OutputType` is `Exe`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Masterly.FluentRegex.Xunit" Version="3.1.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <PackageReference Include="xunit.v3" Version="3.2.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
</Project>
```

Every sample on this page was run with this project on .NET 10 using `dotnet test`. The passing samples also pass on .NET 8.

## Feeding a theory

Put the pattern in a public static member, then point the attribute at it with the type and the member name. The examples on this page test this small validator. It stands for your own code:

```csharp
using System.Linq;

namespace MyApp;

public static class PostalCodeValidator
{
    public static bool IsValid(string value) => value.Length == 5 && value.All(char.IsAsciiDigit);

    public static string Normalize(string value) => value.Trim();
}
```

The pattern describes what a valid postal code looks like:

```csharp
using Masterly.FluentRegex;

namespace MyApp.Tests;

public static class TestPatterns
{
    public static readonly Pattern PostalCode = Pattern.FromRegex(@"^[0-9]{5}$");
}
```

The tests check the validator from both sides:

```csharp
using Masterly.FluentRegex.Xunit;
using Xunit;

namespace MyApp.Tests;

public class PostalCodeValidatorTests
{
    [Theory]
    [MatchingData(typeof(TestPatterns), nameof(TestPatterns.PostalCode), Count = 10, Seed = 42)]
    public void IsValid_WhenInputMatchesPattern_ReturnsTrue(string value)
    {
        Assert.True(PostalCodeValidator.IsValid(value));
    }

    [Theory]
    [NonMatchingData(typeof(TestPatterns), nameof(TestPatterns.PostalCode), Count = 10)]
    public void IsValid_WhenInputIsANearMiss_ReturnsFalse(string value)
    {
        Assert.False(PostalCodeValidator.IsValid(value));
    }
}
```

`dotnet test` runs 20 tests, one per row, and all of them pass. `dotnet test --list-tests` shows each row with its sample in the name:

```text
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "15257")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "23205")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "17579")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "50508")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "30717")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "60546")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "02747")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "50000")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "93017")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "49783")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputIsANearMiss_ReturnsFalse(value: "#74876")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputIsANearMiss_ReturnsFalse(value: "04014:")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputIsANearMiss_ReturnsFalse(value: "4717!9")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputIsANearMiss_ReturnsFalse(value: "7876")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputIsANearMiss_ReturnsFalse(value: "191\t6")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputIsANearMiss_ReturnsFalse(value: "4409")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputIsANearMiss_ReturnsFalse(value: "040-4")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputIsANearMiss_ReturnsFalse(value: "926220")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputIsANearMiss_ReturnsFalse(value: "3,795")
MyApp.Tests.PostalCodeValidatorTests.IsValid_WhenInputIsANearMiss_ReturnsFalse(value: "25399é")
```

- Each row passes one `string` to the theory. The test explorer and the console show that string in the row's name, with control characters escaped (`"191\t6"` holds a tab).
- The near misses are realistic mistakes: a character added (`#74876`, `926220`, `25399é`), removed (`7876`), or replaced (`040-4`, `3,795`). The generator also duplicates, swaps and truncates characters. When it runs out of near misses, it adds the empty string, a single space, and random strings made from the pattern's own characters.
- Use `nameof` for the member name. A rename then updates the attribute, and a typo is a compile error instead of a failed test.
- Each row is a separate test case, so a failing sample shows up on its own and you can run it alone.

## Where the pattern comes from

The first two constructor arguments say where to read the pattern: a type, and the name of a member on that type. The member can be any of these:

| Member kind | Example | Notes |
|-------------|---------|-------|
| Static field | `public static readonly Pattern OrderNumber = ...;` | Built once, when the type is first used. |
| Static property | `public static Pattern Sku => ...;` | Needs a getter. A write-only property is not found. |
| Static method | `public static Pattern CouponCode() => ...;` | Must take no parameters. |
| Inherited static member | A static member declared on a base class | Found through the type you pass. |

```csharp
using Masterly.FluentRegex;

namespace MyApp.Tests;

public class SharedPatterns
{
    public static Pattern CurrencyCode => Pattern.FromRegex("^[A-Z]{3}$");
}

public class OrderPatterns : SharedPatterns
{
    // A static field
    public static readonly Pattern OrderNumber = Pattern.FromRegex("^ORD-[0-9]{6}$");

    // A static property
    public static Pattern Sku => Pattern.With
        .StartOfLine
        .UppercaseLetter.Repeat.Times(3)
        .Literal("-")
        .AsciiDigit.Repeat.Times(4)
        .EndOfLine;

    // A parameterless static method
    public static Pattern CouponCode() => Pattern.FromRegex("^[A-Z]{4}[0-9]{2}$");

    public static Pattern YesOrNo => Pattern.FromRegex("^(yes|no)$");
}
```

```csharp
using Masterly.FluentRegex.Xunit;
using Xunit;

namespace MyApp.Tests;

public class PatternSourceTests
{
    [Theory, MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.OrderNumber), Count = 3)]
    public void OrderNumber_FromStaticField_HasPrefix(string orderNumber) => Assert.StartsWith("ORD-", orderNumber);

    [Theory, MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.Sku), Count = 3)]
    public void Sku_FromStaticProperty_IsEightCharacters(string sku) => Assert.Equal(8, sku.Length);

    [Theory, MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.CouponCode), Count = 3)]
    public void CouponCode_FromStaticMethod_IsSixCharacters(string couponCode) => Assert.Equal(6, couponCode.Length);

    [Theory, MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.CurrencyCode), Count = 3)]
    public void CurrencyCode_FromBaseType_IsThreeLetters(string currencyCode) => Assert.Equal(3, currencyCode.Length);
}
```

The rows:

```text
OrderNumber_FromStaticField_HasPrefix(orderNumber: "ORD-855424")
OrderNumber_FromStaticField_HasPrefix(orderNumber: "ORD-409380")
OrderNumber_FromStaticField_HasPrefix(orderNumber: "ORD-560497")
Sku_FromStaticProperty_IsEightCharacters(sku: "VOO-4244")
Sku_FromStaticProperty_IsEightCharacters(sku: "AZI-8056")
Sku_FromStaticProperty_IsEightCharacters(sku: "CLZ-7397")
CouponCode_FromStaticMethod_IsSixCharacters(couponCode: "VOOL24")
CouponCode_FromStaticMethod_IsSixCharacters(couponCode: "MAZI80")
CouponCode_FromStaticMethod_IsSixCharacters(couponCode: "NRCL97")
CurrencyCode_FromBaseType_IsThreeLetters(currencyCode: "VOO")
CurrencyCode_FromBaseType_IsThreeLetters(currencyCode: "LHM")
CurrencyCode_FromBaseType_IsThreeLetters(currencyCode: "MAZ")
```

**How the member is found**

- Only **public static** members count. Instance members, and private or internal members, are not found.
- The lookup tries a property first, then a field, then a parameterless method, all with the name you gave.
- A method that takes parameters is not found, even when it returns a `Pattern`. An overloaded method works when one overload takes no parameters.
- The member's declared type must be `Pattern` (or a type derived from it). `Pattern?` is fine as long as the value is not `null`.
- The type you pass can be any class, including a static class or the test class itself.

`Pattern` is immutable, so a `static readonly` field is safe to share between tests. Any of the ways to build a pattern work: `Pattern.FromRegex`, the fluent builder, or a ready-made pattern such as `CommonPatterns.IPv4()` (see [Common Patterns](./common-patterns.md)).

:::note

The patterns above with the same shape produce similar letters (`VOO`, `VOO-4244`, `VOOL24`). Every attribute starts from seed 0 unless you set `Seed`, so similar patterns walk the same random sequence. Set a different `Seed` per theory when you want more variety across your suite.

:::

## Count and Seed

Both attributes have two settable properties of their own. Set them as named arguments.

| Property | Default | Meaning |
|----------|---------|---------|
| `Count` | `25` | How many rows to ask for. Must be at least 1. The theory gets fewer rows when the pattern has fewer distinct strings. |
| `Seed` | `0` | The generator seed. The same pattern, `Count` and `Seed` always give the same rows. |

Two read-only properties report what the attribute was built with: `PatternSource` (the type) and `MemberName`.

### Reproducible rows

Generation is always seeded. Without a `Seed`, the attribute uses 0, not a random value. This matters for three reasons:

- **Every run is the same.** A row that fails on your machine fails in CI with the same sample.
- **Discovery works.** The attributes report `SupportsDiscoveryEnumeration() == true`, so xUnit lists every row as its own test case before the run. Test explorers show the rows, and you can run one row on its own.
- **No flaky tests.** A test only changes when you change the pattern, `Count` or `Seed`.

To explore other samples, change the seed. Keep the new seed if it finds a bug, so the failing row stays in the suite:

```csharp
using Masterly.FluentRegex.Xunit;
using Xunit;

namespace MyApp.Tests;

public class SeedTests
{
    [Theory, MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.OrderNumber), Count = 3, Seed = 1)]
    public void OrderNumber_WithSeedOne_IsNotEmpty(string orderNumber) => Assert.NotEmpty(orderNumber);

    [Theory, MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.OrderNumber), Count = 3, Seed = -1)]
    public void OrderNumber_WithSeedMinusOne_IsNotEmpty(string orderNumber) => Assert.NotEmpty(orderNumber);
}
```

```text
OrderNumber_WithSeedOne_IsNotEmpty(orderNumber: "ORD-174962")
OrderNumber_WithSeedOne_IsNotEmpty(orderNumber: "ORD-966703")
OrderNumber_WithSeedOne_IsNotEmpty(orderNumber: "ORD-138708")
OrderNumber_WithSeedMinusOne_IsNotEmpty(orderNumber: "ORD-174962")
OrderNumber_WithSeedMinusOne_IsNotEmpty(orderNumber: "ORD-966703")
OrderNumber_WithSeedMinusOne_IsNotEmpty(orderNumber: "ORD-138708")
```

:::warning

A negative seed gives the same rows as the matching positive seed: `Seed = -1` and `Seed = 1` above produce identical rows. When you try several seeds, count upward from 0.

:::

### Changing Count

- On `[MatchingData]`, a smaller `Count` gives the first rows of a larger one with the same seed. Raising `Count` keeps the rows you have and adds more.
- On `[NonMatchingData]`, changing `Count` can change every row. The generator first makes `Count * 2` valid samples to mutate (at least 5, at most 40), so a different `Count` can shift the random sequence. From `Count = 20` up, that number stays at 40, and a smaller `Count` again gives the first rows of a larger one.

### Previewing the rows

The attributes call `GenerateMatching(Count, Seed)` and `GenerateNonMatching(Count, Seed)` on the pattern, so you can preview the exact rows in a console app or a REPL:

```csharp
using System;
using Masterly.FluentRegex;

Pattern postalCode = Pattern.FromRegex(@"^[0-9]{5}$");

// The same rows as [MatchingData(..., Count = 10, Seed = 42)]
string[] rows = postalCode.GenerateMatching(10, 42);
Console.WriteLine(string.Join(", ", rows));
// prints: 15257, 23205, 17579, 50508, 30717, 60546, 02747, 50000, 93017, 49783

// The same rows as [NonMatchingData(..., Count = 3)], which uses the default seed 0
string[] nearMisses = postalCode.GenerateNonMatching(3, 0);
Console.WriteLine(string.Join(", ", nearMisses));
// prints: a49739, 8554, 4973
```

The first line matches the rows of the first theory in [Feeding a theory](#feeding-a-theory). The second line shows the effect of `Count` on near misses: with `Count = 10` the same pattern starts with `#74876`, `04014:` and `4717!9`.

### Other xUnit settings on the attribute

Both attributes derive from xUnit's `DataAttribute`, so the compiler also accepts its settings, such as `Skip`, `SkipWhen`, `SkipUnless`, `Explicit`, `Label`, `TestDisplayName`, `Timeout` and `Traits`. These attributes **ignore** them. Each row is built as a plain one-value row, so none of these settings reach the test cases.

:::warning

`[MatchingData(typeof(TestPatterns), nameof(TestPatterns.PostalCode), Skip = "not ready")]` compiles, but every row still runs. `Explicit = true`, `Timeout`, `Traits` and `TestDisplayName` are ignored in the same way.

:::

Put these settings on the theory instead. They then apply to every row:

```csharp
using Masterly.FluentRegex.Xunit;
using Xunit;

namespace MyApp.Tests;

public class TheorySettingsTests
{
    [Theory(Skip = "Validator rewrite in progress")]
    [MatchingData(typeof(TestPatterns), nameof(TestPatterns.PostalCode), Count = 2)]
    public void IsValid_WhileRewriting_IsSkipped(string value) => Assert.True(PostalCodeValidator.IsValid(value));

    [Theory]
    [Trait("Category", "Generated")]
    [MatchingData(typeof(TestPatterns), nameof(TestPatterns.PostalCode), Count = 2)]
    public void IsValid_WithTrait_ReturnsTrue(string value) => Assert.True(PostalCodeValidator.IsValid(value));
}
```

- The skipped theory is reported once, as `IsValid_WhileRewriting_IsSkipped [SKIP]`, without its rows.
- `dotnet test --filter "Category=Generated"` runs the two rows of the second theory.

## When the pattern has fewer strings than Count

`Count` is a maximum. A pattern with only a few distinct strings yields only those, and the theory runs once per string:

```csharp
using Masterly.FluentRegex.Xunit;
using Xunit;

namespace MyApp.Tests;

public class AnswerTests
{
    [Theory, MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.YesOrNo), Count = 10)]
    public void Answer_FromPattern_IsYesOrNo(string answer)
    {
        Assert.Contains(answer, new string[] { "yes", "no" });
    }
}
```

```text
Answer_FromPattern_IsYesOrNo(answer: "no")
Answer_FromPattern_IsYesOrNo(answer: "yes")
```

- The rows within one attribute are always distinct.
- Very narrow patterns can also give fewer rows, because the generator samples a bounded number of candidates. See [Test Data Generation](./test-data-generation.md) for the details.
- When a pattern gives **no** rows at all, the theory fails instead of passing with zero rows. See [Errors](#errors).

## Combining data sources

Both attributes allow multiple instances on one theory, and they combine with other xUnit data attributes such as `[InlineData]`. The theory runs once for each row of each attribute.

This validator accepts two reference formats. It stands for your own code:

```csharp
using System.Text.RegularExpressions;

namespace MyApp;

public static class OrderReference
{
    public static bool IsKnownFormat(string reference) =>
        Regex.IsMatch(reference, "^(ORD-[0-9]{6}|[A-Z]{4}[0-9]{2})$");
}
```

```csharp
using Masterly.FluentRegex.Xunit;
using Xunit;

namespace MyApp.Tests;

public class OrderReferenceTests
{
    [Theory]
    [MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.OrderNumber), Count = 3)]
    [MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.CouponCode), Count = 3)]
    [InlineData("ORD-000000")]
    public void IsKnownFormat_ForEveryKnownShape_ReturnsTrue(string reference)
    {
        Assert.True(OrderReference.IsKnownFormat(reference));
    }

    [Theory]
    [NonMatchingData(typeof(OrderPatterns), nameof(OrderPatterns.OrderNumber), Count = 5, Seed = 7)]
    public void IsKnownFormat_ForNearMiss_ReturnsFalse(string reference)
    {
        Assert.False(OrderReference.IsKnownFormat(reference));
    }
}
```

```text
IsKnownFormat_ForEveryKnownShape_ReturnsTrue(reference: "ORD-855424")
IsKnownFormat_ForEveryKnownShape_ReturnsTrue(reference: "ORD-409380")
IsKnownFormat_ForEveryKnownShape_ReturnsTrue(reference: "ORD-560497")
IsKnownFormat_ForEveryKnownShape_ReturnsTrue(reference: "VOOL24")
IsKnownFormat_ForEveryKnownShape_ReturnsTrue(reference: "MAZI80")
IsKnownFormat_ForEveryKnownShape_ReturnsTrue(reference: "NRCL97")
IsKnownFormat_ForEveryKnownShape_ReturnsTrue(reference: "ORD-000000")
IsKnownFormat_ForNearMiss_ReturnsFalse(reference: "ORD-01")
IsKnownFormat_ForNearMiss_ReturnsFalse(reference: "#ORD-812628")
IsKnownFormat_ForNearMiss_ReturnsFalse(reference: "ORD-011a86")
IsKnownFormat_ForNearMiss_ReturnsFalse(reference: "ORRD-189813")
IsKnownFormat_ForNearMiss_ReturnsFalse(reference: "ORD-007210,")
```

- Use `[InlineData]` next to generated rows for the edge cases you always want covered, such as all zeros.
- Near misses of one format can be valid in another format. When the code accepts several formats, check the near misses against all of them, or build one pattern with a choice of every format.

### Duplicate rows are skipped

Rows from different attributes are not compared with each other. When two attributes produce the same string, xUnit v3 runs that row once and skips the copy, because both rows get the same test case ID. Stacking two seeds of a pattern with only two strings shows this:

```csharp
using Masterly.FluentRegex.Xunit;
using Xunit;

namespace MyApp.Tests;

public class DuplicateRowTests
{
    [Theory]
    [MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.YesOrNo), Seed = 1)]
    [MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.YesOrNo), Seed = 2)]
    public void Answer_FromTwoSeeds_IsYesOrNo(string answer)
    {
        Assert.Contains(answer, new string[] { "yes", "no" });
    }
}
```

The theory lists only two rows, `"yes"` and `"no"`, and `dotnet test` prints a message like this one for each copy (the timestamp, the assembly name and the ID depend on your project):

```text
[xUnit.net 00:00:00.40] XunitDocs.Tests: Skipping test case with duplicate ID '5371ff7bcae630e097a33c907ea0d4a5cd5126beb04c00c75be62369babd7349' ('MyApp.Tests.DuplicateRowTests.Answer_FromTwoSeeds_IsYesOrNo(answer: "yes")' and 'MyApp.Tests.DuplicateRowTests.Answer_FromTwoSeeds_IsYesOrNo(answer: "yes")')
```

The message is informational. The run still passes.

## Theory parameters

Each row holds exactly one value, a `string`, passed to the theory's first parameter.

| Theory signature | Works? |
|------------------|--------|
| `(string value)` | Yes. |
| `(object value)` or another type a `string` can be assigned to | Yes. The value is still a `string`. |
| `(string value, int expectedLength = 8)` | Yes. The extra parameter takes its default value. |
| `(string value, int length)` | No. Each row fails: "The test method expected 2 parameter values, but 1 parameter value was provided." |
| `(int value)` or `()` | No. The theory fails before any row runs. See [Errors](#errors). |

```csharp
using Masterly.FluentRegex.Xunit;
using Xunit;

namespace MyApp.Tests;

public class ParameterTests
{
    [Theory, MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.Sku), Count = 2)]
    public void Sku_AsObjectParameter_IsString(object sku)
    {
        Assert.IsType<string>(sku);
    }

    [Theory, MatchingData(typeof(OrderPatterns), nameof(OrderPatterns.Sku), Count = 2)]
    public void Sku_WithOptionalParameter_HasExpectedLength(string sku, int expectedLength = 8)
    {
        Assert.Equal(expectedLength, sku.Length);
    }
}
```

```text
Sku_AsObjectParameter_IsString(sku: "VOO-4244")
Sku_AsObjectParameter_IsString(sku: "AZI-8056")
Sku_WithOptionalParameter_HasExpectedLength(sku: "VOO-4244", expectedLength: 8)
Sku_WithOptionalParameter_HasExpectedLength(sku: "AZI-8056", expectedLength: 8)
```

## Errors

A mistake in the attribute never turns into a theory that silently runs zero rows. Each one fails the theory with a message that names the attribute, the type and the member. The failing theory appears as one test without arguments, for example `MyApp.Tests.BrokenTests.B1_Missing`.

These are the real messages, from theories in a `MyApp.Tests.BrokenTests` class that read their patterns from a `MyApp.Tests.BadPatterns` class:

| Mistake | Message (`InvalidOperationException`) |
|---------|---------------------------------------|
| No member with that name | `[MatchingData(BadPatterns.PostalCode)]: type 'MyApp.Tests.BadPatterns' has no public static property, field or parameterless method named 'PostalCode'.` |
| The method takes parameters | `[MatchingData(BadPatterns.OfLength)]: type 'MyApp.Tests.BadPatterns' has no public static property, field or parameterless method named 'OfLength'.` |
| The member is not a `Pattern` | `[MatchingData(BadPatterns.NotAPattern)]: member 'MyApp.Tests.BadPatterns.NotAPattern' is of type 'System.String', not 'Masterly.FluentRegex.Pattern'.` |
| The member returns `null` | `[MatchingData(BadPatterns.NotReady)]: member 'MyApp.Tests.BadPatterns.NotReady' returned null.` |
| `Count` is below 1 | `[MatchingData(TestPatterns.PostalCode)] has Count = 0; it must be at least 1.` |
| The first parameter cannot take a `string` | `[MatchingData(TestPatterns.PostalCode)] supplies one string per row, but theory 'B6_IntParameter' does not take a string as its first parameter.` |
| The pattern matches every string | `[NonMatchingData(BadPatterns.Anything)] produced no non-matching samples for pattern '.*', so theory 'B7_NoNonMatching' has no data.` |
| The pattern matches nothing | `[MatchingData(BadPatterns.Nothing)] produced no matching samples for pattern '(?!)', so theory 'B8_NoMatching' has no data.` |

- The checks run in this order: `Count`, then the theory's first parameter, then the member lookup, then generation. A theory with several mistakes reports the first one.
- The short form `BadPatterns.PostalCode` uses the type's simple name. The longer form uses its full name with the namespace.
- The checks run when xUnit asks for the data, at test discovery, not when the code compiles.

:::warning

The constructor rejects a `null` type, and a `null`, empty or whitespace member name (`ArgumentNullException` or `ArgumentException`). xUnit v3 does not show that exception. The theory fails with only "No data found for MyApp.Tests.CtorTests.C1_Blank". When you see "No data found" on a theory that uses these attributes, check the constructor arguments first. Using `typeof` and `nameof` avoids the problem.

:::

## What generated data catches

Generated rows test what the pattern **says**, not what you think it says. That is where they find bugs. A common case is `\d`: in .NET it matches any Unicode decimal digit, such as Arabic-Indic `٩` or Devanagari `४`, not only `0` to `9`.

```csharp
using Masterly.FluentRegex;

namespace MyApp.Tests;

public static class DigitPatterns
{
    // \d: any Unicode decimal digit
    public static Pattern FiveDigits => Pattern.With.StartOfLine.Digit.Repeat.Times(5).EndOfLine;

    // [0-9]: ASCII digits only
    public static Pattern FiveAsciiDigits => Pattern.With.StartOfLine.AsciiDigit.Repeat.Times(5).EndOfLine;

    // the same pattern, with \d switched to ASCII
    public static Pattern FiveDigitsAscii => FiveDigits.WithDigits(DigitMode.Ascii);
}
```

`FiveDigits` is `^\d{5}$`. The other two are both `^[0-9]{5}$`. Feeding the ASCII-only validator from `FiveDigits`:

```csharp
using Masterly.FluentRegex.Xunit;
using Xunit;

namespace MyApp.Tests;

public class TrapTests
{
    [Theory, MatchingData(typeof(DigitPatterns), nameof(DigitPatterns.FiveDigits), Count = 30, Seed = 42)]
    public void IsValid_WhenInputMatchesPattern_ReturnsTrue(string value)
    {
        Assert.True(PostalCodeValidator.IsValid(value));
    }
}
```

8 of the 30 rows fail. These are the failing rows:

```text
TrapTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "1٩212")
TrapTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "2꤂562")
TrapTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "446෧3")
TrapTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "09᱙82")
TrapTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "4978൩")
TrapTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "5꘩11᮶")
TrapTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "४3708")
TrapTests.IsValid_WhenInputMatchesPattern_ReturnsTrue(value: "9၂956")
```

Each one reports the same assertion failure:

```text
Assert.True() Failure
Expected: True
Actual:   False
```

The test is right to fail: the pattern and the validator disagree. Decide which one is wrong:

- If only `0` to `9` are valid, fix the pattern. Use `.AsciiDigit` in the fluent builder, or call `.WithDigits(DigitMode.Ascii)` on an existing pattern. With `FiveAsciiDigits` or `FiveDigitsAscii`, all 30 rows pass.
- If any digit is valid, fix the validator: use `char.IsDigit` instead of `char.IsAsciiDigit`.

Many ready-made patterns in `CommonPatterns` use `\d` too, so their samples include non-ASCII digits. Those patterns have an overload that takes a `DigitMode`, such as `CommonPatterns.DateIso(DigitMode.Ascii)`. See [Unicode](./unicode.md).

## Anchor your patterns

A sample counts as non-matching only when the pattern finds **no match anywhere** in it. Without `^` and `$`, many useful near misses contain a valid run, so they are dropped. Compare this unanchored pattern with the anchored `TestPatterns.PostalCode` in [Feeding a theory](#feeding-a-theory):

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.Xunit;
using Xunit;

namespace MyApp.Tests;

public static class LoosePatterns
{
    public static Pattern PostalCodeAnywhere => Pattern.FromRegex("[0-9]{5}");
}

public class AnchorTests
{
    [Theory, NonMatchingData(typeof(LoosePatterns), nameof(LoosePatterns.PostalCodeAnywhere), Count = 10)]
    public void PostalCodeAnywhere_NearMiss_ContainsNoFiveDigitRun(string value) => Assert.DoesNotMatch("[0-9]{5}", value);
}
```

```text
PostalCodeAnywhere_NearMiss_ContainsNoFiveDigitRun(value: "4717!9")
PostalCodeAnywhere_NearMiss_ContainsNoFiveDigitRun(value: "7876")
PostalCodeAnywhere_NearMiss_ContainsNoFiveDigitRun(value: "191\t6")
PostalCodeAnywhere_NearMiss_ContainsNoFiveDigitRun(value: "4409")
PostalCodeAnywhere_NearMiss_ContainsNoFiveDigitRun(value: "040-4")
PostalCodeAnywhere_NearMiss_ContainsNoFiveDigitRun(value: "3,795")
PostalCodeAnywhere_NearMiss_ContainsNoFiveDigitRun(value: "4 9739")
PostalCodeAnywhere_NearMiss_ContainsNoFiveDigitRun(value: "699@90")
PostalCodeAnywhere_NearMiss_ContainsNoFiveDigitRun(value: "4717Z")
PostalCodeAnywhere_NearMiss_ContainsNoFiveDigitRun(value: "")
```

The anchored pattern produced `#74876`, `04014:` and `926220`. The unanchored one cannot, because each of them contains five digits in a row. The last row is the empty string, which holds no five-digit run either. Those are exactly the inputs a validator most often gets wrong, so anchor the patterns you use for validation tests.

## Writing your own data attribute

Both attributes derive from the abstract `PatternDataAttribute`, which you can subclass to plug in your own generator. The base class does the member lookup, the checks and the error messages. You supply the samples.

This attribute wraps each matching sample in whitespace, to test code that trims its input:

```csharp
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Masterly.FluentRegex;
using Masterly.FluentRegex.Xunit;

namespace MyApp.Tests;

/// <summary>Supplies matching samples wrapped in whitespace, for code that trims its input.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class PaddedMatchingDataAttribute : PatternDataAttribute
{
    public PaddedMatchingDataAttribute(
        [DynamicallyAccessedMembers(PatternMembers)] Type patternSource,
        string memberName)
        : base(patternSource, memberName)
    {
    }

    protected override string SampleKind => "padded matching";

    protected override string[] Generate(Pattern pattern, int count, int seed) =>
        pattern.GenerateMatching(count, seed)
            .Select(sample => $"  {sample}\t")
            .ToArray();
}
```

```csharp
using Xunit;

namespace MyApp.Tests;

public class PostalCodeNormalizeTests
{
    [Theory, PaddedMatchingData(typeof(TestPatterns), nameof(TestPatterns.PostalCode), Count = 3)]
    public void Normalize_WhenInputIsPadded_ReturnsValidPostalCode(string value)
    {
        Assert.True(PostalCodeValidator.IsValid(PostalCodeValidator.Normalize(value)));
    }
}
```

```text
Normalize_WhenInputIsPadded_ReturnsValidPostalCode(value: "  85542\t")
Normalize_WhenInputIsPadded_ReturnsValidPostalCode(value: "  44093\t")
Normalize_WhenInputIsPadded_ReturnsValidPostalCode(value: "  80560\t")
```

**`PatternDataAttribute` members**

| Member | Kind | Purpose |
|--------|------|---------|
| `PatternDataAttribute(patternSource, memberName)` | protected constructor | Stores the type and member name. Throws `ArgumentNullException` for a `null` argument and `ArgumentException` for an empty or whitespace name. |
| `PatternSource` | `Type`, read-only | The type that declares the pattern member. |
| `MemberName` | `string`, read-only | The name of the pattern member. |
| `Count` | `int`, default 25 | Passed to `Generate` as `count`. |
| `Seed` | `int`, default 0 | Passed to `Generate` as `seed`. |
| `SampleKind` | abstract, protected | The word used in the "produced no ... samples" error, such as `"matching"`. |
| `Generate(pattern, count, seed)` | abstract, protected | Returns the rows. Return an empty array, never `null`, when there are none. |
| `PatternMembers` | protected `const` | The `DynamicallyAccessedMemberTypes` flags for the constructor's `Type` parameter. |
| `GetData(testMethod, disposalTracker)` | override | Runs the checks, resolves the pattern, calls `Generate` and wraps each sample in a one-argument row. You do not call it yourself. |
| `SupportsDiscoveryEnumeration()` | override | Returns `true`. |

- Error messages use your class name without the `Attribute` suffix, for example `[PaddedMatchingData(TestPatterns.Nope)]: type 'MyApp.Tests.TestPatterns' has no public static property, field or parameterless method named 'Nope'.`
- Keep `Generate` deterministic for a given pattern, count and seed. xUnit can ask for the data more than once, for example to list the tests and then to run them, and expects the same rows each time. Do not use an unseeded `Random`, the clock or other shared state.
- `GetData` builds plain one-value rows, so a subclass ignores the inherited `DataAttribute` settings too. See [Other xUnit settings on the attribute](#other-xunit-settings-on-the-attribute).
- Add `[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]` if you want to stack your attribute. The built-in attributes are also `sealed` and `Inherited = true`.
- Forward `[DynamicallyAccessedMembers(PatternMembers)]` on the constructor's `Type` parameter, as shown. It tells the trimmer to keep the public members the lookup needs.

## Pairing with property checks

Theory data gives you a fixed, readable list of rows in the test explorer. For a randomized check that also shrinks a failure to the smallest counter-example, use `PatternProperty` from the core package. See [Property Testing](./property-testing.md). The two work well together: use theory data for the examples you want to see by name, and a property check to search further.

## Checklist

- Anchor validation patterns with `^` and `$` (`.StartOfLine` and `.EndOfLine`).
- Use `typeof` and `nameof` in the attribute, never string literals.
- Test from both sides: `[MatchingData]` for input that must be accepted, `[NonMatchingData]` for input that must be rejected.
- Leave `Seed` fixed in committed tests. Try other seeds locally, from 0 upward, to explore.
- Add `[InlineData]` rows for edge cases you always want covered.
- Put `Skip`, `Explicit`, `Timeout` and traits on `[Theory]` or `[Trait]`, not on `[MatchingData]` or `[NonMatchingData]`, which ignore them.
- When a generated row fails, decide whether the pattern or the code is wrong. Do not change the seed to make it go away.
