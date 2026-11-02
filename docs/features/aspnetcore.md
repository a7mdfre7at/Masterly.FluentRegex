# ASP.NET Core integration

New in 3.1: the `Masterly.FluentRegex.AspNetCore` package (.NET 8 and .NET 9) turns fluent patterns
into route constraints. The behavior below is covered by tests that run a real in-memory app.

```shell
dotnet add package Masterly.FluentRegex.AspNetCore
```

```csharp
using Masterly.FluentRegex;

public static class Patterns
{
    public static readonly Pattern Sku = Pattern.With
        .UppercaseLetter.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddFluentPatterns(registry => registry
    .Add("sku", Patterns.Sku)
    .Add("card", Pattern.With.Digit.Repeat.Times(16).Must(Validators.Luhn, "Luhn checksum")));

WebApplication app = builder.Build();
app.MapGet("/items/{id:pattern(sku)}", (string id) => $"item {id}");
app.MapGet("/cards/{number:pattern(card)}", (string number) => "card");
app.Run();
```

| Request | Result |
|---|---|
| `GET /items/ABC-1234` | 200, `item ABC-1234` |
| `GET /items/ABC-12345` | 404: the whole segment must match |
| `GET /items/abc-1234` | 404 |
| `GET /cards/4111111111111111` | 200 |
| `GET /cards/4111111111111112` | 404: well-formed, but the Luhn rule fails |

## How it works

- `AddFluentPatterns` registers a `PatternRegistry` singleton and the `pattern` route constraint
  (`RouteOptions.SetParameterPolicy<PatternRouteConstraint>("pattern")`). Calling it again adds more
  patterns to the same registry.
- ASP.NET Core activates `PatternRouteConstraint(PatternRegistry registry, string name)` itself: the
  registry comes from dependency injection and `name` is the text inside `pattern(...)`.
- The constraint accepts a value only when the pattern matches the **whole** value and every rule of a
  `ConstrainedPattern` passes (`ConstrainedPattern.IsValid`). Non-string route values are formatted with
  the invariant culture first.
- It also applies to link generation: `LinkGenerator.GetPathByName("item", new { id = "not-a-sku" })`
  returns null.
- Pattern names are case-insensitive: `{id:pattern(SKU)}` works too.

## Errors

- An unknown name, such as `{id:pattern(nope)}`, fails when the route table is built (on the first
  request) with a `RouteCreationException` whose inner `InvalidOperationException` reads:
  `No fluent pattern named 'nope' is registered (registered: sku). Register it with services.AddFluentPatterns(registry => registry.Add("nope", pattern)).`
- `PatternRegistry.Add` throws `ArgumentException` for a blank or duplicate name.

## API

| Member | Purpose |
|---|---|
| `IServiceCollection.AddFluentPatterns(Action<PatternRegistry>)` | registers the registry and constraint |
| `PatternRegistry.Add(name, Pattern)` / `Add(name, ConstrainedPattern)` | registers a named pattern (chainable) |
| `PatternRegistry.Get(name)`, `TryGet`, `Contains`, `Names` | lookups |
| `PatternRouteConstraint` | the `IRouteConstraint`; `Name` and `Pattern` expose what it checks |
