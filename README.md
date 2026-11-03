<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/images/brand/banner-dark.png">
    <img alt="Masterly.FluentRegex: readable, provable regular expressions for .NET" src="docs/images/brand/banner-light.png" width="100%">
  </picture>
</p>

# Masterly.FluentRegex

A fluent, immutable API for building .NET regular expressions, plus the tools to understand, test, secure and ship them.

<p align="center">
  <a href="https://a7mdfre7at.github.io/Masterly.FluentRegex/"><strong>📖 Read the documentation</strong></a>
  &nbsp;·&nbsp;
  <a href="https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/quick-start">Quick Start</a>
  &nbsp;·&nbsp;
  <a href="https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/examples">Examples</a>
  &nbsp;·&nbsp;
  <a href="https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/api-reference">API Reference</a>
  &nbsp;·&nbsp;
  <a href="https://a7mdfre7at.github.io/Masterly.FluentRegex/playground/">Playground</a>
</p>

[![Docs](https://img.shields.io/badge/docs-a7mdfre7at.github.io-2EE6A6?style=flat-square)](https://a7mdfre7at.github.io/Masterly.FluentRegex/) [![Nuget](https://img.shields.io/nuget/v/Masterly.FluentRegex?style=flat-square)](https://www.nuget.org/packages/Masterly.FluentRegex) ![Nuget](https://img.shields.io/nuget/dt/Masterly.FluentRegex?label=nuget%20downloads&style=flat-square) ![GitHub last commit](https://img.shields.io/github/last-commit/a7mdfre7at/Masterly.FluentRegex?style=flat-square) ![GitHub](https://img.shields.io/github/license/a7mdfre7at/Masterly.FluentRegex?style=flat-square) [![Build](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/build.yml/badge.svg)](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/build.yml) [![CodeQL Analysis](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/codeql-analysis.yml) [![Publish to NuGet](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/publish.yml/badge.svg)](https://github.com/a7mdfre7at/Masterly.FluentRegex/actions/workflows/publish.yml)

> **All documentation lives on the [documentation site](https://a7mdfre7at.github.io/Masterly.FluentRegex/).**
> It covers every feature with run-verified examples, the full API reference and the upgrade guide.
> This README is only a short introduction.

## Install

```bash
dotnet add package Masterly.FluentRegex
```

The package includes the source generator and analyzers. It targets .NET 10, .NET 8 and .NET Standard 2.1.
See [Installation](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/installation) for the companion packages
and the command-line tool.

## In 30 seconds

```csharp
using Masterly.FluentRegex;

Pattern pattern = Pattern.With
    .StartOfLine
    .Digit.Repeat.OneOrMore
    .Literal("-")
    .Word.Repeat.Times(3, 5)
    .EndOfLine;

bool isMatch = pattern.IsMatch("123-abc");   // true
bool noMatch = pattern.IsMatch("abc-123");   // false
string regex = pattern.ToString();           // ^\d+-\w{3,5}$
```

Continue with the [Quick Start](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/quick-start), or try it
in the browser in the [Playground](https://a7mdfre7at.github.io/Masterly.FluentRegex/playground/).

## What it can do

| | |
|---|---|
| **Build** | A readable fluent builder with automatic escaping, [groups](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/groups-and-backreferences), [lookaround](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/assertions), [composition](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/composing-patterns), [semantic rules](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/semantic-rules) such as Luhn checks, and a [library of common patterns](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/common-patterns) |
| **Match** | [Matching, replacing and splitting](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/matching), [typed extraction](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/typed-extraction), [typed templates](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/templates), [streaming](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/streaming), [PII redaction](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/pii-redaction) and [Unicode scripts](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/unicode) |
| **Prove** | [Equivalence proofs with counterexamples](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/pattern-algebra), [language facts](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/language-facts), a [verified optimizer](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/optimizer) and [ReDoS analysis](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/redos-safety) |
| **Test** | [Explain and debug](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/validation-and-debugging), [match tracing](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/match-tracing), [regex coverage](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/regex-coverage), [test data](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/test-data-generation), [property testing](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/property-testing), a [backtracking profiler](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/backtracking-profiler) and [railroad diagrams](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/diagrams) |
| **Ship** | [Regex to fluent code](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/regex-to-fluent-code), [learning from examples](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/learning-from-examples), [AI drafting](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/ai-drafting), [JavaScript and JSON Schema export](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/export), [SQL regex dialects](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/sql-dialects) and [SQL LIKE](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/sql-like) |
| **Integrate** | [EF Core](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/entity-framework-core), [ASP.NET Core](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/aspnetcore), [xUnit](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/xunit), [compile-time checks](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/compile-time-checks) and the [command-line tool](https://a7mdfre7at.github.io/Masterly.FluentRegex/docs/command-line-tool) |

## Packages

| Package | Adds |
|---------|------|
| [`Masterly.FluentRegex`](https://www.nuget.org/packages/Masterly.FluentRegex) | The library, with the source generator and analyzers |
| [`Masterly.FluentRegex.EntityFrameworkCore`](https://www.nuget.org/packages/Masterly.FluentRegex.EntityFrameworkCore) | Patterns in LINQ queries, run as the database's own regex |
| [`Masterly.FluentRegex.AspNetCore`](https://www.nuget.org/packages/Masterly.FluentRegex.AspNetCore) | Route constraints such as `{id:pattern(sku)}` |
| [`Masterly.FluentRegex.Xunit`](https://www.nuget.org/packages/Masterly.FluentRegex.Xunit) | `[MatchingData]` and `[NonMatchingData]` theory data |
| [`Masterly.FluentRegex.AI`](https://www.nuget.org/packages/Masterly.FluentRegex.AI) | Drafts patterns with Claude and verifies them locally |
| [`Masterly.FluentRegex.Tool`](https://www.nuget.org/packages/Masterly.FluentRegex.Tool) | The `fluentregex` command-line tool |

## Contributing

Contributions are welcome. For major changes, please open an issue first to discuss what you would like to change.

```bash
git clone https://github.com/a7mdfre7at/Masterly.FluentRegex.git
cd Masterly.FluentRegex
dotnet test                                                # .NET 8 and .NET 10
dotnet run --project samples/Masterly.FluentRegex.Showcase  # a tour of every feature
```

The documentation site is in [`website/`](website). Run it locally with `npm ci` and `npm start` in that folder.

## License

MIT. See [LICENSE](LICENSE.txt).
