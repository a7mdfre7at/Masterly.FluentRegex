# Masterly.FluentRegex.AspNetCore

ASP.NET Core integration for [Masterly.FluentRegex](https://github.com/a7mdfre7at/Masterly.FluentRegex):
register named fluent patterns once and use them as route constraints.

```csharp
using Masterly.FluentRegex;

public static class Patterns
{
    public static readonly Pattern Sku = Pattern.With
        .UppercaseLetter.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);
}

builder.Services.AddFluentPatterns(registry => registry
    .Add("sku", Patterns.Sku)
    .Add("card", CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum")));

app.MapGet("/items/{id:pattern(sku)}", (string id) => $"item {id}");
// GET /items/ABC-1234   -> 200
// GET /items/ABC-12345  -> 404 (the whole segment must match)
```

- `AddFluentPatterns` registers a `PatternRegistry` singleton and the `pattern` route constraint.
  Calling it again adds to the same registry.
- `PatternRouteConstraint` accepts a value only when the pattern matches the **whole** value and every
  rule of a `ConstrainedPattern` passes.
- An unknown name in a route template, such as `{id:pattern(nope)}`, fails when the route table is
  built (on the first request) with a `RouteCreationException` whose inner
  `InvalidOperationException` names the pattern and lists the registered names.
- Pattern names are case-insensitive. Targets .NET 8 and .NET 9, trimming and AOT compatible.
