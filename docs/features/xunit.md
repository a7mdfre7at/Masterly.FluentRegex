# xUnit theory data: `[MatchingData]` and `[NonMatchingData]`

The `Masterly.FluentRegex.Xunit` package feeds xUnit v3 theories with strings generated from a pattern.

```shell
dotnet add package Masterly.FluentRegex.Xunit
```

```csharp
using System.Text.RegularExpressions;
using Masterly.FluentRegex;
using Masterly.FluentRegex.Xunit;
using Xunit;

public static class Patterns
{
    public static readonly Pattern FiveDigits = Pattern.FromRegex(@"^\d{5}$");
    public static Pattern HexColor => CommonPatterns.HexColor();
    public static Pattern IsoDate() => CommonPatterns.DateIso();
}

public class ValidatorTests
{
    [Theory, MatchingData(typeof(Patterns), nameof(Patterns.FiveDigits), Count = 30, Seed = 42)]
    public void Accepts_five_digits(string value)
    {
        // \d matches any Unicode decimal digit, and the generator deliberately produces some.
        Assert.Equal(5, value.Length);
        Assert.All(value, c => Assert.True(char.IsDigit(c)));
    }

    [Theory, NonMatchingData(typeof(Patterns), nameof(Patterns.FiveDigits), Count = 30)]
    public void Rejects_everything_else(string value)
    {
        Assert.False(Regex.IsMatch(value, @"^\d{5}$"));
    }

    [Theory]
    [MatchingData(typeof(Patterns), nameof(Patterns.HexColor), Count = 5)]
    [MatchingData(typeof(Patterns), nameof(Patterns.IsoDate), Count = 5)]
    public void Stacked_attributes_combine_rows(string value) => value.ShouldNotBeNullOrEmpty();
}
```

The first two theories mirror this repository's integration tests. The first one's original version asserted ASCII
digits (`c >= '0' && c <= '9'`) and failed on rows such as `Accepts_five_digits(value: "1٩212")`: exactly
the kind of assumption generated data is meant to catch.

## Behaviour

- **Where the pattern comes from.** A public static property, field or parameterless method returning
  `Pattern`, on the given type or a base type. A missing member, a member of another type, a method with
  parameters, or a `null` value fails with a message naming the type and member, for example
  `[MatchingData(TestPatterns.DoesNotExist)]: type 'MyTests.TestPatterns' has no public static property, field or parameterless method named 'DoesNotExist'.`
- **Reproducible.** `Seed` defaults to 0 and `Count` to 25, so every run and every discovery produces the
  same rows. Change `Seed` to explore other samples.
- **Readable rows.** Each row is one `string` argument, so the test explorer shows the sample in the row
  name, for example `Accepts_five_digits(value: "4978൩")`.
- **Discovery.** `SupportsDiscoveryEnumeration()` returns `true`; rows are listed individually.
- **Fewer rows than `Count`** are returned when the pattern has fewer distinct strings (`^x$` yields one row).
  If a pattern yields none (for example `[NonMatchingData]` on `.*`), the theory fails with an explanation
  instead of silently running no rows.
- The theory's first parameter must accept a `string`.

Both attributes derive from `PatternDataAttribute`, which you can subclass to plug in your own generator.

Pair the attributes with `PatternProperty` from the core package when you want shrinking: see
[property-testing.md](property-testing.md).
