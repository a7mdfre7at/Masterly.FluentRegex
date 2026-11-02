# Masterly.FluentRegex.Xunit

xUnit v3 theory data from [Masterly.FluentRegex](https://github.com/a7mdfre7at/Masterly.FluentRegex) patterns.

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.Xunit;

public static class Patterns
{
    public static Pattern ZipCode => Pattern.FromRegex(@"^[0-9]{5}$");
}

public class ZipCodeValidatorTests
{
    [Theory, MatchingData(typeof(Patterns), nameof(Patterns.ZipCode), Count = 50, Seed = 42)]
    public void Accepts_valid_zip(string value) => ZipCodeValidator.IsValid(value).ShouldBeTrue();

    [Theory, NonMatchingData(typeof(Patterns), nameof(Patterns.ZipCode), Count = 50)]
    public void Rejects_invalid_zip(string value) => ZipCodeValidator.IsValid(value).ShouldBeFalse();
}
```

- `[MatchingData]` supplies strings the pattern matches; `[NonMatchingData]` supplies near misses it rejects.
- The pattern comes from a public static property, field or parameterless method returning `Pattern`.
  A wrong name or type fails with a message naming the type and member.
- Rows are deterministic (default `Seed = 0`, default `Count = 25`), so runs are reproducible and each
  row's display name shows the sample, for example `Accepts_valid_zip(value: "90210")`.
- Multiple attributes can be stacked on one theory.

Requires xUnit v3 (`xunit.v3` 3.2.1 or later).
