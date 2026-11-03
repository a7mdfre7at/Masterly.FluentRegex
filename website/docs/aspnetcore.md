---
id: aspnetcore
title: "ASP.NET Core Route Constraints"
description: "Register named fluent patterns once and use them as route constraints, such as /items/{id:pattern(sku)}, in minimal APIs and controllers."
---

The `Masterly.FluentRegex.AspNetCore` package turns fluent patterns into ASP.NET Core route constraints. You register each pattern once under a name, then use the name in any route template: `/items/{id:pattern(sku)}`. A request is routed to the endpoint only when the pattern matches the **whole** route value and every [semantic rule](./semantic-rules.md) passes.

Use it when:

- Several endpoints accept the same kind of identifier (a SKU, an order number, a slug) and you want one definition instead of a regex copied into every template.
- The value needs a check a regex cannot do, such as a Luhn checksum or a real calendar date.
- You want the same pattern for routing, link generation and body validation.

| Type | Namespace | What it is |
|------|-----------|------------|
| `AddFluentPatterns(...)` | `Microsoft.Extensions.DependencyInjection` | Extension method on `IServiceCollection`. Registers the registry and the `pattern` route constraint. |
| `PatternRegistry` | `Masterly.FluentRegex.AspNetCore` | The application's named patterns. A thread-safe singleton. |
| `PatternRouteConstraint` | `Masterly.FluentRegex.AspNetCore` | The `IRouteConstraint` that ASP.NET Core creates for each `pattern(name)` in a template. |

The package targets `net8.0` and `net10.0`, and is marked trimming and Native AOT compatible. It references the `Microsoft.AspNetCore.App` shared framework, so it works in any ASP.NET Core 8 or 10 app.

```shell
dotnet add package Masterly.FluentRegex.AspNetCore
```

The package depends on `Masterly.FluentRegex`, so you get the core library too. See [Installation](./installation.md) for the other packages.

## Registering patterns

Declare your patterns once, register them with `AddFluentPatterns`, then refer to them by name in route templates.

```csharp
using Masterly.FluentRegex;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddFluentPatterns(registry => registry
    .Add("sku", CatalogPatterns.Sku)
    .Add("card", CatalogPatterns.Card));

WebApplication app = builder.Build();

app.MapGet("/items/{id:pattern(sku)}", (string id) => $"item {id}");
app.MapGet("/cards/{number:pattern(card)}", (string number) => "card accepted");

app.Run();

public static class CatalogPatterns
{
    public static readonly Pattern Sku = Pattern.With
        .UppercaseLetter.Repeat.Times(3)
        .Literal("-")
        .Digit.Repeat.Times(4);

    public static readonly ConstrainedPattern Card = Pattern.With
        .Digit.Repeat.Times(16)
        .Must(Validators.Luhn, "Luhn checksum");
}
```

Running this app on Kestrel gives:

| Request | Response | Why |
|---------|----------|-----|
| `GET /items/ABC-1234` | 200, `item ABC-1234` | The whole segment is a SKU. |
| `GET /items/ABC-12345` | 404 | The pattern matches `ABC-1234`, but not the whole segment. |
| `GET /items/XABC-1234` | 404 | Same: an extra character in front. |
| `GET /items/abc-1234` | 404 | The pattern asks for uppercase letters. |
| `GET /cards/4111111111111111` | 200, `card accepted` | 16 digits with a valid check digit. |
| `GET /cards/4111111111111112` | 404 | Well-formed, but the Luhn rule rejects it. |

- `AddFluentPatterns` lives in the `Microsoft.Extensions.DependencyInjection` namespace, like other service registration methods, so it needs no extra `using`. You only need `using Masterly.FluentRegex.AspNetCore;` when your code names `PatternRegistry` or `PatternRouteConstraint`.
- The callback receives the `PatternRegistry`. `Add` returns the same registry, so calls chain.
- `Add` accepts a plain `Pattern` or a `ConstrainedPattern` (a pattern with rules from `Must`). A plain pattern is stored as a `ConstrainedPattern` with no rules.
- A value that fails the constraint is treated as "this route does not apply". If no other endpoint matches, the response is 404. See [Returning 400 instead of 404](#returning-400-instead-of-404) when you want a validation error instead.
- The constraint name is the constant `FluentPatternsServiceCollectionExtensions.ConstraintName`, which is `"pattern"`.

:::tip

Keep patterns in `public static readonly` fields of one class, as `CatalogPatterns` does. The same fields then feed the route constraints, [`[MatchesPattern]`](./semantic-rules.md) on request models, [EF Core queries](./entity-framework-core.md) and your tests.

:::

### What AddFluentPatterns registers

| Registration | Details |
|--------------|---------|
| `PatternRegistry` | One instance, registered as a singleton. Inject it anywhere: endpoints, controllers, services. |
| The `pattern` route constraint | Added to `RouteOptions.ConstraintMap` with `SetParameterPolicy<PatternRouteConstraint>("pattern")`. Built-in constraints such as `int` and `regex` stay registered. |

- `services` or `configure` being `null` throws `ArgumentNullException`.
- An exception thrown inside your callback propagates out of `AddFluentPatterns` unchanged.
- The callback runs immediately, during service registration, not when the app starts.

### Calling AddFluentPatterns more than once

Call it as often as you like, for example once per feature module. Every call adds to the same registry, and the constraint is registered only once.

```csharp
using System;
using Masterly.FluentRegex.AspNetCore;

// builder comes from Program.cs, as in the first sample
builder.Services.AddFluentPatterns(registry => registry.Add("sku", CatalogPatterns.Sku));
builder.Services.AddFluentPatterns(registry => registry.Add("card", CatalogPatterns.Card));

WebApplication app = builder.Build();

PatternRegistry patterns = app.Services.GetRequiredService<PatternRegistry>();
Console.WriteLine(string.Join(", ", patterns.Names)); // prints: card, sku
```

- A name that is already registered, in any call, throws `ArgumentException`: `A pattern named 'SKU' is already registered. (Parameter 'name')`. Names are compared case-insensitively, so `sku` and `SKU` clash.
- `AddFluentPatterns` reuses an existing registry only when it is a non-keyed `PatternRegistry` instance. A keyed `PatternRegistry` registration is ignored and left untouched.

:::warning

Let `AddFluentPatterns` create the registry, or register your own instance **before** it.

- `services.AddSingleton(new PatternRegistry())` **before** `AddFluentPatterns` works: your patterns go into that instance, and the `pattern` constraint is registered as usual.
- `services.AddSingleton<PatternRegistry>()` **after** `AddFluentPatterns` breaks routing: the later registration wins, so the constraint receives a new, empty registry. The first request fails with a `RouteCreationException` whose base exception reads `No fluent pattern named 'sku' is registered (registered: none).`

:::

### Pattern names

| Rule | Example |
|------|---------|
| Names are case-insensitive, in templates and in the registry. | `{id:pattern(SKU)}` uses the pattern registered as `sku`. |
| Spaces around the name in a template are trimmed. | `{id:pattern( sku )}` works. |
| Names are stored exactly as you register them. | `Add(" sku ", ...)` stores `" sku "`, which no template can reach because template names are trimmed. |
| A blank name is rejected. | `Add(" ", ...)` throws `ArgumentException`. A `null` name throws `ArgumentNullException`. |
| A name can be registered once. | A second `Add("SKU", ...)` throws `ArgumentException`. |

Short, lowercase names with letters, digits and dashes (`sku`, `order-number`) read well in templates and need no escaping.

## Whole-value semantics

The constraint checks the route value with `ConstrainedPattern.IsValid`. That method wraps the pattern as `\A(?:pattern)\z`, so the pattern must consume the entire value. You do not add anchors yourself, and a pattern that already starts with `StartOfLine` and ends with `EndOfLine`, as many [common patterns](./common-patterns.md) do, works the same.

This is the main difference from ASP.NET Core's built-in `regex(...)` constraint. The built-in constraint searches for a match anywhere in the value, and it always ignores case. These routes ran side by side in one app (templates shown as C# string literals):

| Template | Request | Response |
|----------|---------|----------|
| `{id:regex([A-Z]{{3}}-\\d{{4}})}` | `/h/xabc-12345` | 200: unanchored and case-insensitive |
| `{id:regex(^[A-Z]{{3}}-\\d{{4}}$)}` | `/g/abc-1234` | 200: anchored, but still case-insensitive |
| `{id:pattern(sku)}` | `/fp/abc-1234` | 404 |
| `{id:pattern(sku)}` | `/fp/XABC-12345` | 404 |

Other details you get for free:

- **No escaping.** The built-in constraint needs `{{` and `}}` for every brace in the regex, and a C# string needs `\\` for every backslash. A pattern name needs neither.
- **One definition.** Change `CatalogPatterns.Sku` and every route that uses `pattern(sku)` follows.
- **Works with the slim builder.** `WebApplication.CreateSlimBuilder()` does not register the `regex` constraint, and a template that uses it fails with `A route parameter uses the regex constraint, which isn't registered.` The `pattern` constraint is registered by `AddFluentPatterns` and works with both builders.

What the constraint sees:

- **Decoded values.** `GET /items/ABC%2D1234` reaches the endpoint with `id` = `ABC-1234`.
- **No trailing slash.** Routing ignores a trailing slash, so `GET /items/ABC-1234/` is routed like `GET /items/ABC-1234`, and the value is `ABC-1234`.
- **Text.** A route value that is not a string, such as an `int` you pass to link generation, is converted with `Convert.ToString(value, CultureInfo.InvariantCulture)` first. A `decimal` of `12.5` becomes `"12.5"` even on a server whose culture uses a comma as the decimal separator.
- **Missing values.** A route value that is missing or `null` is rejected, even when the pattern would accept empty text. So is a value whose `ToString()` returns `null`. An empty string is checked like any other value.

## Adding semantic rules

Register a `ConstrainedPattern` and the constraint also runs its rules. A value is routed only when the pattern matches the whole value and every rule accepts it.

```csharp
using Masterly.FluentRegex;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

// builder and app come from Program.cs, as in the first sample
builder.Services.AddFluentPatterns(registry => registry
    .Add("card", CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum"))
    .Add("date", CommonPatterns.DateIso().Must(Validators.IsoDate, "real calendar date")));

app.MapGet("/cards/{number:pattern(card)}", (string number) => $"card {number}");
app.MapGet("/reports/{day:pattern(date)}", (string day) => $"report for {day}");
```

| Request | Response | Why |
|---------|----------|-----|
| `GET /cards/4111-1111-1111-1111` | 200 | `CreditCard()` allows dashes between groups, and `Validators.Luhn` skips them. |
| `GET /cards/4111111111111112` | 404 | The check digit is wrong. |
| `GET /reports/2024-02-29` | 200 | A real date in a leap year. |
| `GET /reports/2026-02-30` | 404 | The shape is right, but February has no 30th day. |
| `GET /reports/2026-13-01` | 404 | `DateIso()` accepts month `13` (its month is `[0-1]\d`), so the `IsoDate` rule is what rejects it. |
| `GET /reports/2026-1-01` | 404 | The pattern needs a two-digit month. |

- Rules run only after the pattern has matched, in the order you added them, and stop at the first failure.
- Rules can also target a named group with `Must("group", rule)`. See [Semantic Rules and Validation](./semantic-rules.md) for every way to write a rule and the built-in `Validators`.
- The route constraint only answers yes or no. To tell the caller which rule failed, use `Check` as shown in [Validating request bodies](#validating-request-bodies).

## Minimal APIs

The constraint works in every place a minimal API accepts a route template: `MapGet`, `MapPost` and the other `Map*` methods, route groups, optional parameters, default values, catch-all parameters, and next to other constraints.

```csharp
using Masterly.FluentRegex;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

// builder and app come from Program.cs, as in the first sample
builder.Services.AddFluentPatterns(registry => registry
    .Add("sku", CatalogPatterns.Sku)
    .Add("digits", Pattern.With.Digit.Repeat.OneOrMore)
    .Add("doc-path", Pattern.With.Set(Pattern.With.LowercaseLetter.Digit.Literal("-/")).Repeat.OneOrMore));

// after builder.Build():
RouteGroupBuilder products = app.MapGroup("/api/v1/products");
products.MapGet("/{sku:pattern(sku)}", (string sku) => $"product {sku}");

app.MapGet("/search/{sku:pattern(sku)?}", (string? sku) => sku ?? "all products");
app.MapGet("/featured/{sku:pattern(sku)=ABC-0000}", (string sku) => $"featured {sku}");
app.MapGet("/codes/{code:pattern(digits):maxlength(3)}", (string code) => $"code {code}");
app.MapGet("/docs/{**path:pattern(doc-path)}", (string path) => $"doc {path}");
app.MapGet("/orders/{id:pattern(digits)}", (int id) => $"order {id}");
```

The requests gave:

| Request | Response |
|---------|----------|
| `GET /api/v1/products/ABC-1234` | 200, `product ABC-1234` |
| `GET /api/v1/products/abc-1234` | 404 |
| `GET /search` | 200, `all products` |
| `GET /search/ABC-1234` | 200, `ABC-1234` |
| `GET /search/abc` | 404 |
| `GET /featured` | 200, `featured ABC-0000` |
| `GET /featured/XYZ-0001` | 200, `featured XYZ-0001` |
| `GET /codes/123` | 200, `code 123` |
| `GET /codes/1234` | 404 |
| `GET /docs/guides/getting-started` | 200, `doc guides/getting-started` |
| `GET /docs/Guides/intro` | 404 |
| `GET /orders/42` | 200, `order 42` |
| `GET /orders/99999999999` | 400 |

- **Route groups.** A template inside `MapGroup` uses the constraint the same way.
- **Optional parameters** (`{sku:pattern(sku)?}`). A missing segment is allowed and the parameter is `null`. A segment that is present must be valid.
- **Default values** (`{sku:pattern(sku)=ABC-0000}`). The default is checked too. Make sure it matches the pattern: with an invalid default such as `=nope`, a request without the segment returns 404.
- **Several constraints** (`pattern(digits):maxlength(3)`). Every constraint must accept the value.
- **Catch-all parameters** (`{**path:pattern(doc-path)}`). The constraint sees the whole rest of the path, slashes included, so the pattern must allow `/`.
- **Typed parameters.** The constraint checks the text. Binding to the handler's parameter type happens after routing. `99999999999` is all digits, so the route matches, but it does not fit in an `int`, and the minimal API returns 400.

## Controllers

Attribute routes accept the constraint in `[Route]`, `[HttpGet]` and the other `[Http*]` attributes. Register the controllers and the patterns, then map the controllers:

```csharp
// Program.cs: builder and app as in the first sample
builder.Services.AddControllers();
builder.Services.AddFluentPatterns(registry => registry.Add("sku", CatalogPatterns.Sku));

// after builder.Build():
app.MapControllers();
```

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public sealed record ProductDto(string Sku, string Name);

public sealed record CreateProductRequest(string Sku, string Name);

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly PatternRegistry _patterns;

    public ProductsController(PatternRegistry patterns)
    {
        _patterns = patterns;
    }

    [HttpGet("{sku:pattern(sku)}", Name = "GetProduct")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ProductDto> Get(string sku)
    {
        return new ProductDto(sku, "Standing desk");
    }

    [HttpPost]
    [ProducesResponseType<ProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<ProductDto> Create(CreateProductRequest request)
    {
        ConstraintResult result = _patterns.Get("sku").Check(request.Sku);
        if (!result.IsValid)
        {
            ModelState.AddModelError(nameof(request.Sku), result.ToString());
            return ValidationProblem(ModelState);
        }

        ProductDto product = new ProductDto(request.Sku, request.Name);
        return CreatedAtRoute("GetProduct", new { sku = product.Sku }, product);
    }
}
```

| Request | Response |
|---------|----------|
| `GET /api/v1/products/ABC-1234` | 200, `{"sku":"ABC-1234","name":"Standing desk"}` |
| `GET /api/v1/products/abc-1234` | 404 |
| `POST /api/v1/products` with `{"sku":"ABC-1234",...}` | 201, `Location: http://localhost/api/v1/products/ABC-1234` |
| `POST /api/v1/products` with `{"sku":"ABC-12",...}` | 400, `application/problem+json`, error for `Sku`: `Invalid: the value does not match the pattern at position 4: expected \d{4} (a digit, exactly 4 times)` |

- The controller receives the `PatternRegistry` through its constructor, like any other service.
- `CreatedAtRoute` builds the `Location` header through link generation, which applies the constraint too. Validate the value first, as `Create` does. See [Link generation](#link-generation).
- The constraint is the same in conventional routes. `app.MapControllerRoute(name: "shop", pattern: "shop/{sku:pattern(sku)}", defaults: new { controller = "Shop", action = "Show" })` routes `GET /shop/ABC-1234` to `ShopController.Show` and returns 404 for `GET /shop/abc`. Controllers marked `[ApiController]` must use attribute routes.

### Validating request models with [MatchesPattern]

For values in a request body, the core package's `[MatchesPattern]` attribute uses the same pattern fields. With `[ApiController]`, an invalid model is turned into a 400 response before your action runs.

```csharp
using System.ComponentModel.DataAnnotations;
using Masterly.FluentRegex;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public sealed class CreateOrderRequest
{
    [Required]
    [MatchesPattern(typeof(CatalogPatterns), nameof(CatalogPatterns.Sku))]
    public string? Sku { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }
}

[ApiController]
[Route("api/v1/orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public IActionResult Create(CreateOrderRequest request)
    {
        return Accepted();
    }
}
```

- `POST /api/v1/orders` with `{"sku":"ABC-1234","quantity":2}` returns 202.
- With `{"sku":"ABC-12345","quantity":2}` it returns 400 with the error `The field Sku does not match the required pattern.`
- The attribute is a separate feature of the core package (`net8.0` and `net10.0` only). See [Semantic Rules and Validation](./semantic-rules.md) for custom messages and how the member is resolved.

## Link generation

ASP.NET Core checks route constraints when it builds URLs too. `LinkGenerator`, `CreatedAtRoute` and `Url.Link` produce a link only when the value passes the pattern and its rules.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

// app comes from Program.cs, with "sku" registered as in the first sample
app.MapGet("/items/{id:pattern(sku)}", (string id) => $"item {id}").WithName("GetItem");

app.MapGet("/links/{id}", (string id, LinkGenerator links) =>
    links.GetPathByName("GetItem", new { id }) ?? "no link");

app.MapPost("/items/{id}", (string id) => TypedResults.CreatedAtRoute("GetItem", new { id }));
```

| Request | Response |
|---------|----------|
| `GET /links/ABC-1234` | 200, `/items/ABC-1234` |
| `GET /links/not-a-sku` | 200, `no link` |
| `POST /items/ABC-1234` | 201, `Location: http://localhost/items/ABC-1234` |
| `POST /items/not-a-sku` | Fails with `InvalidOperationException: No route matches the supplied values.` |

- `GetPathByName` returns `null` when the value fails the constraint, when it is missing, or when a rule rejects it.
- Values that are not strings are converted with the invariant culture first, so `links.GetPathByName("GetNumber", new { id = 42 })` gives `/numbers/42` for a route `/numbers/{id:pattern(number)}` whose pattern is `Digit.Repeat.OneOrMore`.

:::warning

`CreatedAtRoute` (minimal APIs and controllers) throws `InvalidOperationException` when it cannot build the `Location` link, and the client gets a 500. Validate a value from the request body before you pass it to `CreatedAtRoute`.

:::

## Returning 400 instead of 404

Route constraints exist to pick an endpoint, not to report errors. A value that fails every constraint ends in a 404 with no explanation. When clients should get a 400 with a reason, use one of these approaches.

### A fallback route

Map the same template without the constraint. Routing prefers the constrained route, so valid values still reach it, and everything else reaches the fallback.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

// app comes from Program.cs, with "sku" registered as in the first sample
app.MapGet("/items/{id:pattern(sku)}", (string id) => $"item {id}");
app.MapGet("/items/{id}", (string id) => TypedResults.Problem(
    detail: $"'{id}' is not a SKU. Use three capital letters, a dash and four digits.",
    statusCode: StatusCodes.Status400BadRequest));
```

```text
GET /items/ABC-1234 -> 200 item ABC-1234
GET /items/abc-1234 -> 400 {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Bad Request","status":400,"detail":"'abc-1234' is not a SKU. Use three capital letters, a dash and four digits."}
```

### Validating request bodies

Body values never go through route constraints. Inject the `PatternRegistry` and call `Check`, which says why a value failed. The result's `ToString()` never includes the value itself, so it is safe to return and to log.

```csharp
using System.Collections.Generic;
using Masterly.FluentRegex;
using Masterly.FluentRegex.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

// app comes from Program.cs, with "sku" registered as in the first sample;
// CreateProductRequest is the record from the Controllers section
app.MapPost("/api/v1/orders", (CreateProductRequest request, PatternRegistry patterns) =>
{
    ConstraintResult result = patterns.Get("sku").Check(request.Sku);
    if (!result.IsValid)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(CreateProductRequest.Sku)] = new[] { result.ToString() },
        });
    }

    return Results.Created($"/api/v1/orders/{request.Sku}", request);
});
```

```text
POST {"sku":"ABC-1234","name":"Desk"} -> 201, Location: /api/v1/orders/ABC-1234
POST {"sku":"abc-1234","name":"Desk"} -> 400 application/problem+json
  {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,
   "errors":{"Sku":["Invalid: the value does not match the pattern at position 0: expected [A-Z]{3} (one of [A-Z], exactly 3 times)"]}}
```

- `Check` returns a `ConstraintResult`. `IsValid` is the verdict. When a rule failed, `FailedRule` holds its description and `Trace` is `null`; the text reads `Invalid: the rule 'Luhn checksum' rejected the value`. When the pattern itself failed, `PatternFailed` is `true`, `FailedRule` is `null`, and `Trace` shows where matching stopped.
- Reading the pattern from the registry keeps the body check and the route constraint in sync. You can also use the `CatalogPatterns` fields directly.

## Using the registry

`PatternRegistry` is a plain class. Inject it, or create one yourself in tests and tools.

```csharp
using System;
using Masterly.FluentRegex;
using Masterly.FluentRegex.AspNetCore;

PatternRegistry registry = new PatternRegistry()
    .Add("sku", CatalogPatterns.Sku)
    .Add("card", CatalogPatterns.Card);

Console.WriteLine(string.Join(", ", registry.Names));               // prints: card, sku
Console.WriteLine(registry.Contains("SKU"));                        // prints: True
Console.WriteLine(registry.Get("sku").IsValid("ABC-1234"));         // prints: True
Console.WriteLine(registry.Get("card").Check("4111111111111112"));  // prints: Invalid: the rule 'Luhn checksum' rejected the value

if (registry.TryGet("iban", out ConstrainedPattern? iban))
{
    Console.WriteLine(iban.IsValid("GB82WEST12345698765432"));
}
else
{
    Console.WriteLine("iban is not registered");                     // prints: iban is not registered
}
```

| Member | Returns | Notes |
|--------|---------|-------|
| `Add(string name, Pattern pattern)` | The same registry | Wraps the pattern in a `ConstrainedPattern` with no rules. |
| `Add(string name, ConstrainedPattern pattern)` | The same registry | Keeps the pattern's rules. |
| `Get(string name)` | `ConstrainedPattern` | Throws `InvalidOperationException` when the name is not registered. |
| `TryGet(string name, out ConstrainedPattern? pattern)` | `bool` | `false` and a `null` pattern when the name is not registered. |
| `Contains(string name)` | `bool` | `true` when the name is registered. |
| `Names` | `IReadOnlyList<string>` | The registered names in ordinal order, as registered. `Zip` sorts before `card`. |

- All lookups are case-insensitive.
- `Names` is a snapshot. Names you add later do not appear in a list you already hold.
- The registry is thread-safe. Adding the same name from two threads lets exactly one call win; the other throws.
- `Add` throws `ArgumentNullException` for a `null` name or pattern, and `ArgumentException` for a blank or duplicate name. A failed `Add` leaves the registry unchanged.
- `Get`, `TryGet` and `Contains` throw `ArgumentNullException` for a `null` name.
- `Get` explains a missing name and lists what is registered: `No fluent pattern named 'iban' is registered (registered: card, sku). Register it with services.AddFluentPatterns(registry => registry.Add("iban", pattern)).` With an empty registry the list reads `(registered: none)`.

## How the constraint is created

ASP.NET Core creates the constraint itself when it builds the route table, which happens on the first request:

1. It finds `pattern` in `RouteOptions.ConstraintMap` and creates a `PatternRouteConstraint`.
2. The constructor `PatternRouteConstraint(PatternRegistry registry, string name)` gets the registry from dependency injection and the text between the parentheses as `name`.
3. The constructor trims the name and looks the pattern up with `registry.Get`. The constraint keeps that pattern for the life of the app.

| Member | What it does |
|--------|--------------|
| `PatternRouteConstraint(PatternRegistry registry, string name)` | Resolves the pattern. Throws `ArgumentNullException` for a `null` argument and `InvalidOperationException` for an unknown or blank name. |
| `Name` | The trimmed name as written in the template, for example `SKU` for `pattern( SKU )`. |
| `Pattern` | The `ConstrainedPattern` that values must satisfy. |
| `Match(httpContext, route, routeKey, values, routeDirection)` | `true` when `values[routeKey]` is present, not `null`, and `Pattern.IsValid` accepts its text. The same rule applies to incoming requests and to URL generation. The route key is looked up case-insensitively. Throws `ArgumentNullException` for a `null` `routeKey` or `values`. |

Because the pattern is resolved once, adding a pattern to the registry after the route table is built does not change existing constraints.

## Errors and diagnostics

Mistakes in route templates surface on the first request, not at startup. They break routing for the whole app, so every request fails until you fix them, even requests for other routes.

| Mistake | What happens |
|---------|--------------|
| Unknown name: `{id:pattern(nope)}` | `RouteCreationException: An error occurred while trying to create an instance of 'Masterly.FluentRegex.AspNetCore.PatternRouteConstraint'.` Its base exception is `InvalidOperationException: No fluent pattern named 'nope' is registered (registered: sku). Register it with services.AddFluentPatterns(registry => registry.Add("nope", pattern)).` |
| No name: `{id:pattern}` | `RouteCreationException` for `PatternRouteConstraint`, with base exception `ArgumentNullException: Value cannot be null. (Parameter 'name')`. |
| Blank name: `{id:pattern( )}` | `RouteCreationException` for `PatternRouteConstraint`, with base exception `InvalidOperationException: No fluent pattern named '' is registered (registered: sku). ...`. |
| `AddFluentPatterns` never called | `InvalidOperationException: The constraint reference 'pattern' could not be resolved to a type. Register the constraint type with 'Microsoft.AspNetCore.Routing.RouteOptions.ConstraintMap'.` |
| Same name registered twice | `ArgumentException` during service registration, before the app is built. |
| A regex timeout during matching | The `RegexMatchTimeoutException` is not caught. The request fails and your exception handler turns it into a 500. See [Performance and safety](#performance-and-safety). |

:::tip

Add one test that sends a request to each templated route. It turns these first-request failures into a failing test instead of a broken deployment. See [Testing your routes](#testing-your-routes).

:::

## Testing your routes

Use the in-memory test server from the `Microsoft.AspNetCore.TestHost` package to send real requests through routing. This xUnit v3 test passes:

```csharp
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Masterly.FluentRegex.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class ItemRouteTests
{
    [Theory]
    [InlineData("/items/ABC-1234", HttpStatusCode.OK)]
    [InlineData("/items/ABC-12345", HttpStatusCode.NotFound)]
    [InlineData("/items/abc-1234", HttpStatusCode.NotFound)]
    public async Task GetItem_RoutesOnlyWholeSkus(string path, HttpStatusCode expected)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddFluentPatterns(registry => registry.Add("sku", CatalogPatterns.Sku));
        await using WebApplication app = builder.Build();
        app.MapGet("/items/{id:pattern(sku)}", (string id) => $"item {id}");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public void SkuConstraint_LowercaseValue_IsRejected()
    {
        PatternRegistry registry = new PatternRegistry().Add("sku", CatalogPatterns.Sku);
        PatternRouteConstraint constraint = new PatternRouteConstraint(registry, "sku");
        RouteValueDictionary values = new RouteValueDictionary { ["id"] = "abc-1234" };

        bool accepted = constraint.Match(null, null, "id", values, RouteDirection.IncomingRequest);

        Assert.False(accepted);
    }
}
```

- The first test exercises the full pipeline: registration, route table, constraint and endpoint.
- The second test calls the constraint directly. `httpContext` and `route` may be `null`.
- To test the pattern itself, generate matching and non-matching inputs with [`[MatchingData]` and `[NonMatchingData]`](./xunit.md), or with [test data generation](./test-data-generation.md).

## Performance and safety

- **One regex per pattern.** `ConstrainedPattern` builds its whole-value regex the first time it validates and caches it. Every request after that reuses it.
- **One constraint per route.** ASP.NET Core creates the constraint once, when the route table is built. It does not touch the registry per request.
- **Route values are untrusted input.** Anyone can send any path. A pattern with nested quantifiers can backtrack for a very long time on a crafted value. Keep route patterns simple, check them with [ReDoS safety](./redos-safety.md), and the build-time [analyzers](./compile-time-checks.md) warn about risky constructs.
- **Timeouts.** Patterns have no match timeout by default. Set one with `WithTimeout`, for example `CommonPatterns.Slug().WithTimeout(TimeSpan.FromMilliseconds(100))`. The route constraint uses the pattern's timeout. When it elapses during routing, the `RegexMatchTimeoutException` propagates and the request fails with a 500. That is still better than a thread that is stuck.
- **Linear-time matching.** `UseNonBacktracking()` switches a pattern to .NET's non-backtracking engine, which cannot backtrack catastrophically. It is available on every target this package supports.
- **Logging.** `ConstraintResult.ToString()` never includes the validated value, so you can log it without leaking user input.

This app registers the same risky pattern twice, once with a timeout and once on the non-backtracking engine:

```csharp
using System;
using Masterly.FluentRegex;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

// builder and app come from Program.cs, as in the first sample
Pattern risky = Pattern.FromRegex("(a+)+b");

builder.Services.AddFluentPatterns(registry => registry
    .Add("with-timeout", risky.WithTimeout(TimeSpan.FromMilliseconds(100)))
    .Add("linear", risky.UseNonBacktracking()));

// after builder.Build():
app.MapGet("/timeout/{value:pattern(with-timeout)}", (string value) => value);
app.MapGet("/linear/{value:pattern(linear)}", (string value) => value);
```

Running it on Kestrel, with a path of 40 `a` characters followed by `c`:

| Request | Response |
|---------|----------|
| `GET /timeout/aaaa…aaac` | 500, after the 100 ms timeout elapsed |
| `GET /linear/aaaa…aaac` | 404, quickly |
| `GET /timeout/aab` | 200, `aab` |
| `GET /linear/aab` | 200, `aab` |

:::note

The non-backtracking engine does not support backreferences, lookarounds, atomic groups, conditionals or right-to-left matching. A pattern that uses them is accepted by `AddFluentPatterns`, but it throws `NotSupportedException` when it runs, so every request that reaches the constraint fails. Cover such routes with a test.

:::

## API reference

| Member | Description |
|--------|-------------|
| `IServiceCollection AddFluentPatterns(this IServiceCollection services, Action<PatternRegistry> configure)` | Registers the `PatternRegistry` singleton and the `pattern` route constraint, then runs `configure`. Repeated calls share one registry. Returns `services`. |
| `FluentPatternsServiceCollectionExtensions.ConstraintName` | The constant `"pattern"`. |
| `PatternRegistry.Add(string name, Pattern pattern)` | Registers a pattern. Returns the registry. |
| `PatternRegistry.Add(string name, ConstrainedPattern pattern)` | Registers a pattern with rules. Returns the registry. |
| `PatternRegistry.Get(string name)` | Returns the pattern or throws `InvalidOperationException`. |
| `PatternRegistry.TryGet(string name, out ConstrainedPattern? pattern)` | Returns `true` and the pattern when the name is registered. |
| `PatternRegistry.Contains(string name)` | Returns `true` when the name is registered. |
| `PatternRegistry.Names` | Registered names, ordinal order. |
| `PatternRouteConstraint(PatternRegistry registry, string name)` | Creates the constraint for a registered name. |
| `PatternRouteConstraint.Name` | The trimmed pattern name. |
| `PatternRouteConstraint.Pattern` | The `ConstrainedPattern` route values must satisfy. |
| `PatternRouteConstraint.Match(...)` | `IRouteConstraint.Match`: whole-value match plus every rule. |

For the rest of the library, see the [API Reference](./api-reference.md).

## Common pitfalls

- **Expecting a 400.** A failed constraint means "no route", so the client gets a 404. Add a [fallback route](#a-fallback-route) or validate in the endpoint when the client needs to know what was wrong.
- **Typos in pattern names.** An unknown name breaks every request on the first request after startup, not only the route that uses it. Cover routes with a test.
- **Registering `PatternRegistry` yourself.** Depending on the order, either the `pattern` constraint is never registered or the constraint gets an empty registry. Let `AddFluentPatterns` create the registry.
- **An invalid default value.** `{sku:pattern(sku)=nope}` makes the route unreachable without the segment.
- **Catch-all parameters.** `{**path:pattern(x)}` passes the whole remaining path, with its slashes, to the pattern.
- **Assuming the constraint protects the request body.** It only sees route values. Use `Check` or [`[MatchesPattern]`](#validating-request-models-with-matchespattern) for bodies and query strings.
- **Passing unchecked values to `CreatedAtRoute`.** It throws when the value fails the constraint. Validate first.
- **Case.** Unlike the built-in `regex` constraint, a fluent pattern is case-sensitive unless you build it to ignore case. `abc-1234` does not match an uppercase SKU pattern.
