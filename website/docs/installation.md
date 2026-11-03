---
id: installation
title: "Installation"
description: "Install the core package and only the companion packages you need. Targets, dependencies, what each framework gets, and how to check the install."
---

Install the core package first. It holds the whole fluent API and also ships the source generator, the analyzers and the code fix, so compile-time checks need no extra install. Then add only the companion packages you need. All six packages ship together with the same version number.

| Package | What it is for | Target frameworks | Extra dependencies |
|---------|----------------|-------------------|--------------------|
| `Masterly.FluentRegex` | The core: the fluent builder, matching, typed extraction, templates, streaming, redaction, proofs, the optimizer, ReDoS analysis, tracing, diagrams, test data and export. Also the source generator, analyzers and code fix. | `netstandard2.1`, `net8.0`, `net10.0` | None |
| `Masterly.FluentRegex.AspNetCore` | Named patterns as route constraints, such as `/items/{id:pattern(sku)}` | `net8.0`, `net10.0` | The ASP.NET Core shared framework (`Microsoft.AspNetCore.App`) |
| `Masterly.FluentRegex.EntityFrameworkCore` | Patterns inside LINQ queries, translated to the database's regex dialect, and `CHECK` constraints | `net8.0`, `net10.0` | `Microsoft.EntityFrameworkCore.Relational` 8.0.33 on .NET 8, 10.0.14 on .NET 10 |
| `Masterly.FluentRegex.Xunit` | `[MatchingData]` and `[NonMatchingData]` theory data for xUnit v3 | `net8.0`, `net10.0` | `xunit.v3.extensibility.core` 3.2.1 |
| `Masterly.FluentRegex.AI` | Drafts patterns from plain language with Claude and verifies every draft locally | `net8.0`, `net10.0` | None. It calls the API with `HttpClient`. |
| `Masterly.FluentRegex.Tool` | The `fluentregex` command-line tool | `net8.0`, runs on .NET 8 or later | None to install. The tool bundles what it needs. |

Every companion package also depends on the core package, at the same version. The versions shown are the minimum versions each package references.

## Install the core package

Use the .NET CLI:

```bash
dotnet add package Masterly.FluentRegex
```

Or the Visual Studio Package Manager Console:

```powershell
Install-Package Masterly.FluentRegex
```

Or add the reference to your project file by hand:

```xml
<ItemGroup>
  <PackageReference Include="Masterly.FluentRegex" Version="3.1.0" />
</ItemGroup>
```

All public types live in the `Masterly.FluentRegex` namespace. The companion packages add their own namespaces, listed in [Install companion packages](#install-companion-packages).

### What the core package contains

The core package has no NuGet dependencies. It contains three builds of the library and the compiler extensions:

| Path in the package | What it is |
|---------------------|------------|
| `lib/net10.0/Masterly.FluentRegex.dll` | The .NET 10 build. Everything, including `EnumerateSplits`. |
| `lib/net8.0/Masterly.FluentRegex.dll` | The .NET 8 build. Used by .NET 8 and .NET 9 apps. |
| `lib/netstandard2.1/Masterly.FluentRegex.dll` | The .NET Standard 2.1 build. Used by .NET Core 3.x, .NET 5 to 7, Mono and Unity. |
| `analyzers/dotnet/cs/Masterly.FluentRegex.Analyzers.dll` | The `[GeneratePattern]` source generator and the FRX analyzers. |
| `analyzers/dotnet/cs/Masterly.FluentRegex.CodeFixes.dll` | The IDE code fix that adds a .NET `[GeneratedRegex]` method (FRX007). |

NuGet picks the build that matches your project. See [What each framework gets](#what-each-framework-gets) for the differences.

## Check the install

Paste this into a console app's `Program.cs` and run it. It checks the three things you get from the core package: the runtime library, the build NuGet picked, and the source generator.

```csharp
using System;
using System.Reflection;
using System.Runtime.Versioning;
using Masterly.FluentRegex;

Pattern pattern = Pattern.With
    .StartOfLine
    .Digit.Repeat.OneOrMore
    .Literal("-")
    .Word.Repeat.Times(3, 5)
    .EndOfLine;

Console.WriteLine(pattern.ToString());         // ^\d+-\w{3,5}$
Console.WriteLine(pattern.IsMatch("123-abc")); // True
Console.WriteLine(pattern.IsMatch("123-ab"));  // False

Assembly library = typeof(Pattern).Assembly;
string? build = library.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
Console.WriteLine($"Masterly.FluentRegex {library.GetName().Version} ({build})");
// prints on .NET 10: Masterly.FluentRegex 3.1.0.0 (.NETCoreApp,Version=v10.0)
// prints on .NET 8:  Masterly.FluentRegex 3.1.0.0 (.NETCoreApp,Version=v8.0)

Console.WriteLine(Validators.IsoDateSource);                     // ^\d{4}-\d{2}$
Console.WriteLine(Validators.IsoDateRegex().IsMatch("2026-11")); // True

public static partial class Validators
{
    [GeneratePattern]
    public static Pattern IsoDate => Pattern.With
        .StartOfLine.Digit.Repeat.Times(4).Literal("-").Digit.Repeat.Times(2).EndOfLine;
}
```

- The first block proves the runtime library loads and matches.
- The `TargetFrameworkAttribute` line shows which build NuGet picked. A .NET 9 app also prints `v8.0`, because .NET 9 uses the .NET 8 build.
- `IsoDateSource` and `IsoDateRegex()` do not exist in your code. The source generator writes them at build time. If they compile, compile-time checks are working. See [Compile-time checks](#compile-time-checks).

## Install companion packages

Add only the packages you need:

```bash
dotnet add package Masterly.FluentRegex.AspNetCore
dotnet add package Masterly.FluentRegex.EntityFrameworkCore
dotnet add package Masterly.FluentRegex.Xunit
dotnet add package Masterly.FluentRegex.AI
```

| Package | Namespace | Entry point |
|---------|-----------|-------------|
| `Masterly.FluentRegex.AspNetCore` | `Masterly.FluentRegex.AspNetCore` (`AddFluentPatterns` is in `Microsoft.Extensions.DependencyInjection`) | `services.AddFluentPatterns(...)` |
| `Masterly.FluentRegex.EntityFrameworkCore` | `Masterly.FluentRegex.EntityFrameworkCore` | `optionsBuilder.UseFluentRegex()` |
| `Masterly.FluentRegex.Xunit` | `Masterly.FluentRegex.Xunit` | `[MatchingData]`, `[NonMatchingData]` |
| `Masterly.FluentRegex.AI` | `Masterly.FluentRegex.AI` | `new PatternAssistant(httpClient, options)` |

A companion package brings the core package with it, and the source generator and analyzers still run. Reference `Masterly.FluentRegex` directly anyway when your own code uses it, so the version you build against is the one in your project file.

:::note

The companion packages target .NET 8 and .NET 10 only. A project that targets `netstandard2.1`, or .NET 5 to 7, can use the core package but not the companion packages.

:::

The samples in this section share this pattern:

```csharp
using Masterly.FluentRegex;

public static class Patterns
{
    public static readonly Pattern Sku = Pattern.With
        .StartOfLine.UppercaseLetter.Repeat.Times(3).Literal("-").AsciiDigit.Repeat.Times(4).EndOfLine;
}
```

### ASP.NET Core

`Masterly.FluentRegex.AspNetCore` carries a framework reference to `Microsoft.AspNetCore.App`. In a web project (`Microsoft.NET.Sdk.Web`) that is already there. In any other project, installing the package adds the ASP.NET Core shared framework to the app.

To check the install, register a pattern and read it back from the container:

```csharp
using System;
using Masterly.FluentRegex.AspNetCore;
using Microsoft.Extensions.DependencyInjection;

ServiceCollection services = new ServiceCollection();
services.AddFluentPatterns(registry => registry.Add("sku", Patterns.Sku));

using ServiceProvider provider = services.BuildServiceProvider();
PatternRegistry registry = provider.GetRequiredService<PatternRegistry>();

Console.WriteLine(string.Join(", ", registry.Names)); // sku
```

- In a web app, call `builder.Services.AddFluentPatterns(...)` instead and use the name in a route: `app.MapGet("/items/{id:pattern(sku)}", ...)`.
- `AddFluentPatterns` registers a `PatternRegistry` singleton and the `pattern` route constraint. Calling it again adds to the same registry.
- Full guide: [ASP.NET Core](./aspnetcore.md).

### Entity Framework Core

`Masterly.FluentRegex.EntityFrameworkCore` references only `Microsoft.EntityFrameworkCore.Relational`. It does not reference a database provider, so install your provider as usual. Each target uses the EF Core release that ships with its framework:

| Your app targets | EF Core the package is built against |
|------------------|--------------------------------------|
| .NET 10 | EF Core 10 (`Microsoft.EntityFrameworkCore.Relational` 10.0.14 or later) |
| .NET 8 or .NET 9 | EF Core 8 (`Microsoft.EntityFrameworkCore.Relational` 8.0.33 or later) |

The dialect is detected from the provider you register:

| Provider | Dialect |
|----------|---------|
| Npgsql | PostgreSQL |
| Pomelo (`Pomelo.EntityFrameworkCore.MySql`) | MySQL, or MariaDB when the server version says MariaDB |
| Oracle's `MySql.EntityFrameworkCore` | MySQL |
| SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`) | SQL Server 2025 regex functions when the compatibility level is 170 or later (Azure SQL included), otherwise a `LIKE` translation |
| `Microsoft.EntityFrameworkCore.Sqlite` | SQLite |

To check the install, turn the integration on with `UseFluentRegex()` and print the SQL of a query. This needs no running database:

```csharp
using System;
using System.Linq;
using Masterly.FluentRegex.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

DbContextOptions<ShopContext> options = new DbContextOptionsBuilder<ShopContext>()
    .UseSqlite("Data Source=:memory:")
    .UseFluentRegex()
    .Options;

using ShopContext db = new ShopContext(options);
IQueryable<Product> query = db.Products.Where(p => Patterns.Sku.IsMatch(p.Sku!)); // ! is fine: SQL handles NULL

Console.WriteLine(query.ToQueryString());
// SELECT "p"."Id", "p"."Sku"
// FROM "Products" AS "p"
// WHERE "p"."Sku" IS NOT NULL AND "p"."Sku" REGEXP '^[A-Z]{3}-[0-9]{4}$' AND length("p"."Sku") <= 9

public sealed class Product
{
    public int Id { get; set; }
    public string? Sku { get; set; }
}

public sealed class ShopContext : DbContext
{
    public ShopContext(DbContextOptions<ShopContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
}
```

- This sample uses the `Microsoft.EntityFrameworkCore.Sqlite` provider. With Npgsql, Pomelo or SQL Server, chain `.UseFluentRegex()` after `UseNpgsql`, `UseMySql` or `UseSqlServer` the same way.
- Without `UseFluentRegex()` the same query throws `InvalidOperationException`: "Translation of method 'Masterly.FluentRegex.Pattern.IsMatch' failed".
- Full guide: [Entity Framework Core](./entity-framework-core.md).

### xUnit

`Masterly.FluentRegex.Xunit` works with xUnit v3 only. Add it to a test project that already uses `xunit.v3`:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <OutputType>Exe</OutputType>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
  <PackageReference Include="xunit.v3" Version="3.2.1" />
  <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  <PackageReference Include="Masterly.FluentRegex.Xunit" Version="3.1.0" />
</ItemGroup>
```

To check the install, feed a theory with generated values:

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.Xunit;
using Xunit;

public static class ZipPatterns
{
    public static Pattern ZipCode => Pattern.FromRegex(@"^[0-9]{5}$");
}

public class ZipCodeTests
{
    [Theory]
    [MatchingData(typeof(ZipPatterns), nameof(ZipPatterns.ZipCode), Count = 3, Seed = 42)]
    public void ZipCode_WhenValueIsGenerated_Matches(string value)
    {
        Assert.Matches(@"^[0-9]{5}$", value);
    }
}
```

`dotnet test` reports three passed tests, one for each generated value.

- `Count` sets how many values are generated. It defaults to 25. A pattern with fewer distinct values produces fewer rows.
- `Seed` defaults to 0. The values are deterministic, so the same seed gives the same rows on every run.
- The package depends on `xunit.v3.extensibility.core` 3.2.1. It does not work with xUnit v2 (`xunit` 2.x).
- Full guide: [xUnit theory data](./xunit.md).

### AI drafting

`Masterly.FluentRegex.AI` has no SDK dependency. It calls the Claude Messages API with the `HttpClient` you pass in. You need an Anthropic API key to draft patterns, but not to check the install. The constructor validates the options without calling the API:

```csharp
using System;
using System.Net.Http;
using Masterly.FluentRegex.AI;

string apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY.");

PatternAssistantOptions assistantOptions = new PatternAssistantOptions { ApiKey = apiKey };

using HttpClient httpClient = new HttpClient(); // in an app, take it from IHttpClientFactory
PatternAssistant assistant = new PatternAssistant(httpClient, assistantOptions);

Console.WriteLine(assistantOptions);
// Model = claude-opus-5-5, MaxAttempts = 3, BaseAddress = https://api.anthropic.com/, Timeout = 00:05:00, MaxTokens = 16000, Effort = medium, ApiKey = <redacted>
```

- The constructor throws `ArgumentNullException` when `httpClient` or the options are null.
- It throws `ArgumentException` when the API key, model or effort is empty or blank, when the API key contains whitespace or control characters, or when `BaseAddress` is not an absolute URI.
- It throws `ArgumentOutOfRangeException` when `MaxAttempts` or `MaxTokens` is less than 1, or when `Timeout` is not positive (`Timeout.InfiniteTimeSpan` is allowed).
- `ToString()` never shows the API key.
- Full guide: [AI drafting](./ai-drafting.md).

## Install the command-line tool

The tool is a .NET tool package named `Masterly.FluentRegex.Tool`. The command it installs is `fluentregex`.

Install it for your user account:

```bash
dotnet tool install --global Masterly.FluentRegex.Tool
fluentregex --version
# 3.1.0
```

Or pin it to a repository with a tool manifest, which is the better choice for CI:

```bash
dotnet new tool-manifest
dotnet tool install Masterly.FluentRegex.Tool
dotnet tool run fluentregex --version
```

Update or remove it with the usual commands:

```bash
dotnet tool update --global Masterly.FluentRegex.Tool
dotnet tool uninstall --global Masterly.FluentRegex.Tool
```

- The tool targets .NET 8 and rolls forward to newer major versions, so any machine with the .NET 8, 9 or 10 runtime can run it.
- It bundles the core library and the Roslyn C# parser (used by `scan` and `convert --file`). Your projects do not need to reference anything.
- `fluentregex --version` prints the version of the bundled core library.
- Running `fluentregex` with no arguments prints the help and exits with code 2.
- Full guide: [Command-line tool](./command-line-tool.md).

A quick check that the tool works:

```bash
fluentregex explain '^\d{3}$'
```

```text
Pattern: ^\d{3}$

Explanation:
  - Start of line/string
  - Digit (any Unicode decimal digit)
  - Exactly 3 times
  - End of line/string
```

## What each framework gets

NuGet picks the build of the core package that matches your project's target framework:

| Your project targets | Build you get | What you get |
|----------------------|---------------|--------------|
| .NET 10 | `net10.0` | Everything, including `EnumerateSplits`. Trimming and Native AOT compatible. |
| .NET 9 | `net8.0` | Everything except `EnumerateSplits`. Trimming and Native AOT compatible. |
| .NET 8 | `net8.0` | Everything except `EnumerateSplits`. Trimming and Native AOT compatible. |
| .NET 5, 6 and 7 | `netstandard2.1` | The .NET Standard 2.1 feature set below, even though the runtime supports more. |
| .NET Core 3.x, Mono, Unity | `netstandard2.1` | The full builder, matching on strings, analysis, generation, tracing and diagrams. No span APIs. |

There is no `net9.0` build. Version 3.1.0 dropped it, and a .NET 9 app runs the .NET 8 build without changes. Only `EnumerateSplits` needs .NET 10.

:::warning

.NET Framework (4.x) is not supported. It does not implement .NET Standard 2.1.

:::

### Feature availability

| Feature | `netstandard2.1` | `net8.0` | `net10.0` |
|---------|:----------------:|:--------:|:---------:|
| Fluent builder, `FromRegex`, operators, common patterns | Yes | Yes | Yes |
| String matching: `IsMatch`, `Match`, `Matches`, `Replace`, `Split`, `Count(string)` | Yes | Yes | Yes |
| Extraction, templates, streaming, redaction, highlighting | Yes | Yes | Yes |
| Proofs, language facts, optimizer, ReDoS analysis, profiler, coverage | Yes | Yes | Yes |
| Tracing, diagrams, test data, property testing, export, SQL dialects | Yes | Yes | Yes |
| `[GeneratePattern]` source generator and FRX analyzers | Yes | Yes | Yes |
| Span overloads: `IsMatch(ReadOnlySpan<char>)`, `Count(ReadOnlySpan<char>)`, `EnumerateMatches` | No | Yes | Yes |
| `EnumerateSplits(ReadOnlySpan<char>)` | No | No | Yes |
| `UseNonBacktracking()` | No | Yes | Yes |
| `[MatchesPattern]` DataAnnotations attribute | No | Yes | Yes |
| `DateOnly` and `TimeOnly` in `Extract<T>` and `Template<T>` | No | Yes | Yes |
| Regex syntax coloring in the IDE for `RegEx(...)` and `FromRegex(...)` arguments | No | Yes | Yes |
| Marked trimming and Native AOT compatible (`IsAotCompatible`) | No | Yes | Yes |

- `Count(string)` works on every build. On `netstandard2.1` it walks the matches one by one; on .NET 8 and 10 it calls `Regex.Count`. The result is the same.
- `DateOnly` and `TimeOnly` do not exist in .NET Standard 2.1, so on that build extraction and templates support `DateTime`, `DateTimeOffset` and `TimeSpan` only.
- `[MatchesPattern]` needs `System.ComponentModel.DataAnnotations`, which is not part of .NET Standard 2.1.

### Span APIs

On .NET 8 and later you can match a `ReadOnlySpan<char>` without allocating strings:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

Pattern number = Pattern.With.Digit.Repeat.OneOrMore;
ReadOnlySpan<char> text = "order 12, items 3 and 45";

Console.WriteLine(number.IsMatch(text)); // True
Console.WriteLine(number.Count(text));   // 3

foreach (ValueMatch match in number.EnumerateMatches(text))
{
    Console.WriteLine(text.Slice(match.Index, match.Length).ToString());
}
// 12
// 3
// 45
```

`EnumerateSplits` needs .NET 10. When a library targets both .NET 8 and .NET 10, guard it with a preprocessor symbol:

```csharp
#if NET10_0_OR_GREATER
Pattern separator = Pattern.With.Literal(", ");
ReadOnlySpan<char> csv = "red, green, blue";

foreach (Range part in separator.EnumerateSplits(csv))
{
    Console.WriteLine(csv[part].ToString());
}
// red
// green
// blue
#endif
```

On a .NET 8 or .NET 9 target, `EnumerateSplits` does not compile: `error CS1061: 'Pattern' does not contain a definition for 'EnumerateSplits'`. Use `Split(string)` there.

On a `netstandard2.1` target (and on .NET 5 to 7), the span calls do not compile either:

| Call | Compiler error on `netstandard2.1` |
|------|-----------------------------------|
| `number.IsMatch(span)` | CS1503: cannot convert from `ReadOnlySpan<char>` to `string` |
| `number.EnumerateMatches(span)` | CS1061: `Pattern` does not contain a definition for `EnumerateMatches` |
| `number.UseNonBacktracking()` | CS1061: `Pattern` does not contain a definition for `UseNonBacktracking` |
| `[MatchesPattern(...)]` | CS0246: the type or namespace name `MatchesPattern` could not be found |

For details on the span APIs, see [Matching](./matching.md).

## Trimming and Native AOT

The .NET 8 and .NET 10 builds of the core package are marked trimming and Native AOT compatible. Reflection-based features such as `Extract<T>`, `Template.Of<T>` and `[MatchesPattern]` carry trimming annotations, so the trimmer keeps the constructors and properties they bind to.

Publish as usual:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

This program publishes with `dotnet publish -r win-x64` without a single trimming or AOT warning, and the native executable binds the record correctly:

```csharp
using System;
using Masterly.FluentRegex;

Pattern line = Pattern.FromRegex(@"^(?<Name>\w+)=(?<Value>\d+)$");
Setting setting = line.Extract<Setting>("retries=3");

Console.WriteLine($"{setting.Name} {setting.Value}"); // retries 3

public sealed record Setting(string Name, int Value);
```

| Package | Trimming and Native AOT |
|---------|-------------------------|
| `Masterly.FluentRegex` (`net8.0`, `net10.0`) | Marked compatible |
| `Masterly.FluentRegex.AspNetCore` | Marked compatible |
| `Masterly.FluentRegex.AI` | Marked compatible. JSON uses source-generated `System.Text.Json`. |
| `Masterly.FluentRegex.EntityFrameworkCore` | Not marked. Follow EF Core's own AOT guidance. |
| `Masterly.FluentRegex.Xunit` | Not marked. |

:::tip

Native AOT on Windows needs the Visual Studio C++ build tools ("Desktop development with C++"). If `dotnet publish` fails at the link step, the library is not the cause.

:::

## Compile-time checks

The source generator, the analyzers and the code fix ship inside the core package under `analyzers/dotnet/cs`. They run inside the C# compiler, so they work for every target framework of your project, `netstandard2.1` included. There is nothing to install or turn on.

They are built against Roslyn 4.8, so they need the C# compiler from the .NET 8 SDK (Visual Studio 2022 17.8) or later.

To see the analyzers at work, build code that contains a risky regex:

```csharp
using System;
using Masterly.FluentRegex;

Pattern risky = Pattern.FromRegex(@"^(\w+\s?)*$");
Console.WriteLine(risky); // ^(\w+\s?)*$
```

The build succeeds and reports a warning:

```text
warning FRX004: ReDoS risk in '(\w+\s?)*': The repetition '\w+' is nested inside the repetition '*'. The same text can be split between iterations in exponentially many ways, so a failing match backtracks through all of them. Fix: Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. For untrusted input also use WithTimeout(...) or UseNonBacktracking(). (https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/compile-time-checks)
```

| ID | Severity | Reported when |
|----|----------|---------------|
| FRX001 | Warning | A `[GeneratePattern]` chain cannot be evaluated at compile time. |
| FRX002 | Error | The type that holds a `[GeneratePattern]` member is not `partial`. |
| FRX003 | Error | A pattern produces an invalid regular expression. |
| FRX004 | Warning | A pattern is vulnerable to catastrophic (exponential) backtracking. |
| FRX005 | Info | A pattern may backtrack polynomially. |
| FRX006 | Error | A builder call throws when the pattern is built. |
| FRX007 | Info | A constant pattern can be backed by a .NET `[GeneratedRegex]` method. The code fix adds it. |
| FRX008 | Warning | An `Extract<T>` target member has no matching group in the pattern. |
| FRX009 | Error | A `[GeneratePattern]` member is not `static`. |

- The generated `...Regex()` method gets the same accessibility as the member you mark. A `private` member produces a `private` method, which code outside the type cannot call (CS0122).
- Full guide: [Compile-time checks](./compile-time-checks.md).

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| CS1061: `Pattern` does not contain a definition for `EnumerateSplits` | The project targets .NET 8 or .NET 9. | Target .NET 10, or use `Split(string)`. |
| CS1061 for `EnumerateMatches` or `UseNonBacktracking`, or CS1503 when passing a span to `IsMatch` | The project targets `netstandard2.1` or .NET 5 to 7, so it gets the `netstandard2.1` build. | Target .NET 8 or later. |
| CS0246: `MatchesPattern` could not be found | Same as above: `[MatchesPattern]` is not in the `netstandard2.1` build. | Target .NET 8 or later. |
| `...Source` constant or `...Regex()` method not found | The source generator did not run. | Make the containing type `partial` (FRX002) and the member `static` (FRX009). Check that the compiler is Roslyn 4.8 or later. |
| CS0122: the generated `...Regex()` method is inaccessible | The marked member is `private`. | Make the member as visible as the callers need. |
| `InvalidOperationException`: Translation of method `Masterly.FluentRegex.Pattern.IsMatch` failed | `UseFluentRegex()` is missing on the `DbContextOptionsBuilder`. | Chain `.UseFluentRegex()` after your provider call. |
| NU1202: Package `Masterly.FluentRegex.AspNetCore` is not compatible with `netstandard2.1` (or `net7.0`, and so on) | A companion package was added to a project that targets `netstandard2.1` or .NET 5 to 7. | Companion packages need .NET 8 or later. |

## Next steps

- [Quick Start](./quick-start.md) builds, tests and explains your first pattern.
- [Core Concepts](./core-concepts.md) explains immutability, `Pattern.With` and how a chain becomes a regex.
- [Upgrading from 2.x](./upgrading-from-2x.md) lists the breaking changes if you are moving from version 2.
