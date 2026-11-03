---
id: typed-extraction
title: "Typed Extraction"
description: "Bind named groups to records, classes and structs with Extract<T>, TryExtract<T> and ExtractAll<T>, with type conversion and custom converters."
---

Typed extraction turns a match into an object. You name the groups in your pattern, declare a record or class with members of the same names, and call `Extract<T>`. The library matches the input, converts each group's text to the member's type, and builds the object for you.

Use it when you parse structured text, such as log lines, file names, codes or command output, and want typed values instead of `match.Groups["name"].Value` strings.

The core package has three methods, all on `Pattern`:

| Method | Returns | Use it when |
|--------|---------|-------------|
| `Extract<T>(input, provider)` | `T`. Throws when the input does not match or a value cannot be converted. | The input should always match. A failure is a bug or bad data that must stop the operation. |
| `TryExtract<T>(input, out result, provider)` | `bool`. `result` holds the object when it returns `true`. | The input may or may not match, such as one line of a file with mixed content. |
| `ExtractAll<T>(input, provider)` | `IEnumerable<T>`, one object per match. Lazy. | The input holds many matches, such as a whole log or document. |

All three live in the `Masterly.FluentRegex` namespace, together with `RegexGroupAttribute` and `ExtractionConverters`. They work on every target framework of the package. `provider` is optional everywhere and defaults to the invariant culture.

## Extracting a match into an object

Declare the type you want. A positional record is the shortest way: its constructor parameters become the targets.

```csharp
using System;

public enum Level { Debug, Info, Warn, Error }

public record LogEntry(DateTime Timestamp, Level Level, string Message);
```

Then give the pattern one named group per member and call `Extract<T>`:

```csharp
using System;
using Masterly.FluentRegex;

Pattern logLine = Pattern.With
    .StartOfLine
    .NamedGroup("timestamp", Pattern.With.NonWhitespace.Repeat.OneOrMore)
    .Whitespace.Repeat.OneOrMore
    .NamedGroup("level", Pattern.With.UppercaseLetter.Repeat.OneOrMore)
    .Whitespace.Repeat.OneOrMore
    .NamedGroup("message", Pattern.With.Anything.Repeat.OneOrMore)
    .EndOfLine;

LogEntry entry = logLine.Extract<LogEntry>("2024-05-17T10:30:00 ERROR Disk full");

Console.WriteLine(entry.Timestamp == new DateTime(2024, 5, 17, 10, 30, 0)); // prints: True
Console.WriteLine(entry.Level);                                            // prints: Error
Console.WriteLine(entry.Message);                                          // prints: Disk full
```

- Group names match member names case-insensitively, so the group `timestamp` fills the parameter `Timestamp`.
- Each value is converted to the member's type: `"2024-05-17T10:30:00"` becomes a `DateTime` and `"ERROR"` becomes `Level.Error`. See [Supported types](#supported-types).
- Groups that no member asks for are ignored. Unnamed groups are ignored too, unless you ask for one by number (see [Renaming with RegexGroup](#renaming-with-regexgroup)).
- To build named groups, see [Groups and Backreferences](./groups-and-backreferences.md). A pattern parsed with `Pattern.FromRegex(@"(?<name>...)")` works the same way, and several samples below use it to stay short.

## How groups bind to members

The binder looks at your type once and makes a plan. It follows these rules, in this order:

| Step | Rule |
|------|------|
| 1. Pick a constructor | The public constructor with the most parameters. A record's primary constructor wins over its copy constructor, which is not public. When two constructors have the same number of parameters, the first one that reflection returns wins, which is usually the first one declared. |
| 2. Fill the constructor parameters | Each parameter reads the group with the same name (case-insensitive), or the name from `[RegexGroup]`. |
| 3. Fill the properties | After construction, each public settable property that is not already a constructor parameter reads its group. `init` setters count as settable. Structs get their properties too (see [Structs](#structs)). |
| 4. Skip the rest | Read-only properties, properties with a private setter, indexers and members with no matching group are left alone. |

### Constructor parameters

Constructor parameters may have default values. When the group for a parameter does not take part in the match, the parameter gets its default:

```csharp
public record Size(int Width, int Height, string Unit = "px");
```

```csharp
Pattern size = Pattern.With
    .NamedGroup("width", Pattern.With.Digit.Repeat.OneOrMore)
    .Literal("x")
    .NamedGroup("height", Pattern.With.Digit.Repeat.OneOrMore)
    .NamedGroup("unit", Pattern.With.Letter.Repeat.OneOrMore).Repeat.Optional;

Size screen = size.Extract<Size>("640x480");
Size paper = size.Extract<Size>("10x20cm");

Console.WriteLine(screen); // prints: Size { Width = 640, Height = 480, Unit = px }
Console.WriteLine(paper);  // prints: Size { Width = 10, Height = 20, Unit = cm }
```

Later samples on this page reuse this `size` pattern and the `Size` record.

### Settable and init-only properties

Classes with a parameterless constructor and settable properties work too. So do records that mix constructor parameters with extra `init` or `set` properties.

```csharp
public class Product
{
    public string Sku { get; set; } = "";

    public decimal Price { get; set; }

    public int? Stock { get; set; }
}

public record Order(string Id)
{
    public int Quantity { get; init; }

    public string? Note { get; set; }
}
```

```csharp
Pattern productLine = Pattern.With
    .NamedGroup("sku", Pattern.With.Word.Repeat.OneOrMore).Literal(";")
    .NamedGroup("price", Pattern.With.Digit.Repeat.OneOrMore.Literal(".").Digit.Repeat.Times(2)).Literal(";")
    .NamedGroup("stock", Pattern.With.Digit.Repeat.ZeroOrMore);

Product product = productLine.Extract<Product>("AB12;19.99;");

Console.WriteLine(product.Sku);           // prints: AB12
Console.WriteLine(product.Price);         // prints: 19.99
Console.WriteLine(product.Stock is null); // prints: True (the empty capture binds null to int?)

Pattern orderLine = Pattern.FromRegex(@"(?<id>[A-Z]+-\d+) x(?<quantity>\d+)(?: (?<note>.+))?");

Order order = orderLine.Extract<Order>("ORD-7 x3 gift wrap");

Console.WriteLine(order); // prints: Order { Id = ORD-7, Quantity = 3, Note = gift wrap }
```

- A property keeps its initial value (here `""` for `Sku`) when its group does not take part in the match. The binder only sets properties whose group matched.
- A property is never set twice. If a constructor parameter already has the same name, the property is skipped.

### Renaming with RegexGroup

When a group name and a member name differ, put `[RegexGroup("name")]` on the constructor parameter or the property:

```csharp
using Masterly.FluentRegex;

public record Resolution([RegexGroup("w")] int Width, [RegexGroup("h")] int Height);

public class Ticket
{
    [RegexGroup("ref")]
    public string Reference { get; set; } = "";
}
```

```csharp
Pattern resolution = Pattern.With
    .NamedGroup("w", Pattern.With.Digit.Repeat.OneOrMore)
    .Literal("x")
    .NamedGroup("h", Pattern.With.Digit.Repeat.OneOrMore);

Console.WriteLine(resolution.Extract<Resolution>("1920x1080")); // prints: Resolution { Width = 1920, Height = 1080 }

Pattern ticketLine = Pattern.With.Literal("#").NamedGroup("ref", Pattern.With.Digit.Repeat.OneOrMore);

Console.WriteLine(ticketLine.Extract<Ticket>("Fixed in #4711").Reference); // prints: 4711
```

The attribute also reaches numbered groups, because .NET names them `"0"`, `"1"`, `"2"` and so on. Group `"0"` is the whole match:

```csharp
public record FullName([RegexGroup("2")] string Last, [RegexGroup("1")] string First, [RegexGroup("0")] string Text);
```

```csharp
Pattern name = Pattern.With
    .Group(Pattern.With.Letter.Repeat.OneOrMore)
    .Whitespace
    .Group(Pattern.With.Letter.Repeat.OneOrMore);

FullName ada = name.Extract<FullName>("Ada Lovelace");

Console.WriteLine(ada); // prints: FullName { Last = Lovelace, First = Ada, Text = Ada Lovelace }
```

- `[RegexGroup]` can be placed on a constructor parameter, or on the property that a record generates for it with `[property: RegexGroup("w")]`. The parameter's attribute wins when both are present.
- The name is also matched case-insensitively.
- `new RegexGroupAttribute(null)` throws `ArgumentNullException`.

### Types with several constructors

The binder always uses the public constructor with the most parameters. It does not fall back to a shorter one when a group is missing. Give the extra parameters a group that can be skipped instead:

```csharp
public sealed class NumberRange
{
    public NumberRange(int low, int high)
    {
        Low = low;
        High = high;
    }

    public NumberRange(int low)
        : this(low, low)
    {
    }

    public int Low { get; }

    public int High { get; }
}
```

```csharp
Pattern range = Pattern.FromRegex(@"(?<low>\d+)(?:-(?<high>\d+))?");

NumberRange span = range.Extract<NumberRange>("1-5");
NumberRange single = range.Extract<NumberRange>("7");

Console.WriteLine($"{span.Low}..{span.High}");     // prints: 1..5
Console.WriteLine($"{single.Low}..{single.High}"); // prints: 7..0
```

The second call used the two-parameter constructor too. `high` did not take part in the match, so it got `default(int)`, which is `0`, not the value the one-parameter constructor would have chosen.

### Structs

Structs bind the same way as classes: through the constructor with the most parameters, then through settable properties. A struct with no declared constructor starts as `default` and gets only its settable properties.

```csharp
public readonly record struct GridCell(int Row, int Column);

public struct PlainPoint
{
    public int X { get; set; }

    public int Y { get; set; }
}

public struct Coordinate
{
    public Coordinate(int x)
    {
        X = x;
    }

    public int X { get; }

    public int Y { get; set; }
}
```

```csharp
Pattern cell = Pattern.FromRegex(@"(?<row>\d+):(?<column>\d+)");
Console.WriteLine(cell.Extract<GridCell>("3:4")); // prints: GridCell { Row = 3, Column = 4 }

Pattern pair = Pattern.FromRegex(@"(?<x>\d+),(?<y>\d+)");

PlainPoint point = pair.Extract<PlainPoint>("1,2");
Console.WriteLine($"{point.X},{point.Y}"); // prints: 1,2

Coordinate coordinate = pair.Extract<Coordinate>("1,2");
Console.WriteLine($"{coordinate.X},{coordinate.Y}"); // prints: 1,2 (X from the constructor, Y from the property)
```

A struct that has neither a public constructor nor a settable property has nothing to bind, so extracting it throws `InvalidOperationException` with the message `Value type '...' needs a public constructor or settable properties.`

## Optional groups and missing values

A member can end up without a value in three ways. The pattern may have no group for it, the group may not take part in the match, or the group may match empty text.

| Situation | Constructor parameter | Settable property |
|-----------|-----------------------|-------------------|
| The pattern has no group for it, and it has a default value | The default value | Keeps its initial value |
| The pattern has no group for it, and its type is a reference type or `Nullable<T>` | `null` | Keeps its initial value |
| The pattern has no group for it, and its type is a non-nullable value type | `InvalidOperationException` when you extract | Keeps its initial value |
| The group exists but did not take part in the match | The default value, or `default(T)` | Keeps its initial value |
| The group matched empty text | `""` for `string`, `null` for `Nullable<T>`, a conversion error for other types | The same as for parameters |

```csharp
public record Pair(int First, int Second);

public record Contact(string Name, string? Email, int? Age);
```

```csharp
Pattern pair = Pattern.FromRegex(@"(?<first>\d+)(?:-(?<second>\d+))?");

Console.WriteLine(pair.Extract<Pair>("5")); // prints: Pair { First = 5, Second = 0 }

Pattern contact = Pattern.FromRegex(@"(?<name>\w+)(?: <(?<email>[^>]*)>)?");

Contact noEmail = contact.Extract<Contact>("ada");
Contact emptyEmail = contact.Extract<Contact>("ada <>");

Console.WriteLine(noEmail.Email is null);  // prints: True (the group did not take part)
Console.WriteLine(emptyEmail.Email == ""); // prints: True (the group matched empty text)
Console.WriteLine(noEmail.Age is null);    // prints: True (the pattern has no 'age' group)
```

:::warning

Only non-nullable value types, such as `int` or `DateTime`, are required. A `string` parameter is a reference type, so the binder treats it as optional even without `?`. If you misspell its group name, the parameter is silently `null`. The [compile-time check](#compile-time-checks), which is on by default, warns about this at build time when it can see the pattern.

:::

## Supported types

Each group's text is converted to the member's type. These types work without any setup, as do their `Nullable<T>` forms:

| Type | How the text is read |
|------|----------------------|
| `string`, `object` | As is. An `object` member receives the `string`. |
| `int`, `long`, `short`, `byte`, `sbyte`, `uint`, `ulong`, `ushort` | `NumberStyles.Integer`: optional sign and surrounding white space, no thousands separators. |
| `decimal` | `NumberStyles.Number`: sign, decimal point and thousands separators, no exponent. |
| `double`, `float` | `NumberStyles.Float` plus thousands separators, so exponents such as `1.5e3` work. |
| `bool` | `true`, `yes`, `y`, `on`, `1` and `false`, `no`, `n`, `off`, `0`. Case-insensitive, trimmed. |
| `char` | Exactly one character. |
| Any `enum` | `Enum.Parse` with `ignoreCase: true` after trimming. Member names, comma-separated `[Flags]` names and numbers all work. |
| `DateTime` | `DateTime.Parse` with `DateTimeStyles.RoundtripKind`. |
| `DateTimeOffset` | `DateTimeOffset.Parse`. The offset in the text is kept. |
| `TimeSpan` | `TimeSpan.Parse`, such as `01:30:00`. |
| `DateOnly`, `TimeOnly` | `DateOnly.Parse`, `TimeOnly.Parse`. Only on the `net8.0` and `net10.0` builds. |
| `Guid` | `Guid.Parse`. |
| `Uri` | `new Uri(text, UriKind.RelativeOrAbsolute)`, so relative paths work. |
| `Version` | `Version.Parse`, such as `3.1.0`. |
| Anything else | Needs a converter. See [Custom types with ExtractionConverters](#custom-types-with-extractionconverters). |

```csharp
[Flags]
public enum Permissions { None = 0, Read = 1, Write = 2, Delete = 4 }

public record Job(Guid Id, bool Enabled, Permissions Access, TimeSpan Every, Version Since, Uri Docs, DateOnly Start);
```

```csharp
Pattern jobLine = Pattern.FromRegex(
    @"(?<id>\S+) enabled=(?<enabled>\w+) access=(?<access>[\w, ]+) every=(?<every>\S+) since=(?<since>\S+) docs=(?<docs>\S+) start=(?<start>\S+)");

Job job = jobLine.Extract<Job>(
    "3f2504e0-4f89-11d3-9a0c-0305e82c3301 enabled=yes access=read, write every=01:30:00 since=3.1.0 docs=guide/jobs.md start=2026-11-03");

Console.WriteLine(job.Id);                 // prints: 3f2504e0-4f89-11d3-9a0c-0305e82c3301
Console.WriteLine(job.Enabled);            // prints: True
Console.WriteLine(job.Access);             // prints: Read, Write
Console.WriteLine(job.Every.TotalMinutes); // prints: 90
Console.WriteLine(job.Since.Minor);        // prints: 1
Console.WriteLine(job.Docs.IsAbsoluteUri); // prints: False
Console.WriteLine(job.Start.DayOfWeek);    // prints: Tuesday
```

### Conversion details

The next samples show the edge cases. They use a one-value record and a pattern that captures the whole input:

```csharp
public record Box<T>(T Value);
```

```csharp
Pattern anyValue = Pattern.FromRegex(@"^(?<value>.*)$");

Console.WriteLine(anyValue.Extract<Box<Level>>(" warn ").Value); // prints: Warn
Console.WriteLine(anyValue.Extract<Box<Level>>("3").Value);      // prints: Error
Console.WriteLine(anyValue.Extract<Box<Level>>("42").Value);     // prints: 42 (not a defined member, still accepted)

Console.WriteLine(anyValue.Extract<Box<decimal>>("1,234.50").Value); // prints: 1234.50
Console.WriteLine(anyValue.Extract<Box<double>>("1.5e3").Value);     // prints: 1500
Console.WriteLine(anyValue.TryExtract("1,000", out Box<int>? _));    // prints: False (no thousands separators for integers)
Console.WriteLine(anyValue.TryExtract("1e3", out Box<decimal>? _));  // prints: False (no exponent for decimal)

Console.WriteLine(anyValue.Extract<Box<int>>("٢٠٢٦").Value);     // prints: 2026
Console.WriteLine(anyValue.Extract<Box<decimal>>("٣٫١٤").Value); // prints: 3.14

Console.WriteLine(anyValue.Extract<Box<DateTime>>("2024-05-17T10:30:00Z").Value.Kind); // prints: Utc
Console.WriteLine(anyValue.Extract<Box<DateTime>>("2024-05-17T10:30:00").Value.Kind);  // prints: Unspecified
```

- **Enums accept numbers that are not defined members.** `"42"` became `(Level)42`. If that matters, restrict the group to the member names in the pattern, or check `Enum.IsDefined` after extraction.
- **Unicode digits are converted.** `\d` matches digits from every script, such as Arabic-Indic `٢٠٢٦`. Before integer, `decimal`, `double` and `float` parsing they are mapped to ASCII. The Arabic decimal separator `٫` and thousands separator `٬` become the culture's separators. See [Unicode](./unicode.md).
- **Dates keep their kind.** A `Z` or an offset in the text gives a `DateTime` with `Kind` set accordingly. Without one, `Kind` is `Unspecified`. Use `DateTimeOffset` when the offset matters.
- **Value ranges are checked.** `"300"` for a `byte` fails with a conversion error, which wraps the `OverflowException`.

:::tip

Let the pattern do the validation and the converter do the typing. A group such as `Pattern.With.Digit.Repeat.Times(4)` cannot hold text that fails to parse as `int`, so conversion errors become impossible instead of runtime surprises.

:::

## Culture-sensitive values

Numbers and dates are parsed with the invariant culture unless you pass an `IFormatProvider`. The invariant culture reads `.` as the decimal point and `,` as a thousands separator.

```csharp
using System;

public record Price(decimal Amount, string Currency);

public record Reading(DateTime Taken, double Value);
```

```csharp
using System.Globalization;

Pattern pricePattern = Pattern.With
    .NamedGroup("amount", Pattern.With.Digit.Repeat.OneOrMore.Literal(",").Digit.Repeat.Times(2))
    .Whitespace
    .NamedGroup("currency", Pattern.With.UppercaseLetter.Repeat.Times(3));

Price german = pricePattern.Extract<Price>("12,50 EUR", new CultureInfo("de-DE"));
Price invariant = pricePattern.Extract<Price>("12,50 EUR");

Console.WriteLine(german.Amount);    // prints: 12.50
Console.WriteLine(invariant.Amount); // prints: 1250 (the comma was read as a thousands separator)

Pattern readingLine = Pattern.FromRegex(@"(?<taken>\S+) (?<value>\S+)");

Reading reading = readingLine.Extract<Reading>("17/05/2024 3,5", new CultureInfo("fr-FR"));

Console.WriteLine(reading.Taken.Month);  // prints: 5
Console.WriteLine(reading.Value == 3.5); // prints: True
```

:::warning

Parsing with the wrong culture often succeeds with a wrong value instead of failing. In the sample, `"12,50"` became `1250` under the invariant culture. Always pass the culture the text was written in. The library never falls back to `CultureInfo.CurrentCulture`, so the culture of the machine does not change the result.

:::

`string`, `char`, `bool`, enums, `Guid`, `Uri` and `Version` ignore the provider. Every other built-in type, and every converter registered with the provider overload, uses it.

## Custom types with ExtractionConverters

For a type the library does not know, register a converter once, at application startup. Every later extraction in the process uses it, for constructor parameters and for properties.

```csharp
using System.Globalization;
using Masterly.FluentRegex;

public readonly record struct Money(decimal Amount, string Currency);

public record Invoice(string Number, Money Total);
```

```csharp
ExtractionConverters.Register<Money>(text =>
{
    string[] parts = text.Split(' ');
    return new Money(decimal.Parse(parts[0], CultureInfo.InvariantCulture), parts[1]);
});

Pattern invoiceLine = Pattern.FromRegex(@"(?<number>INV-\d+) total=(?<total>\d+(?:\.\d+)? [A-Z]{3})");

Invoice invoice = invoiceLine.Extract<Invoice>("INV-42 total=19.99 USD");

Console.WriteLine(invoice.Total); // prints: Money { Amount = 19.99, Currency = USD }

Console.WriteLine(ExtractionConverters.Unregister<Money>()); // prints: True
Console.WriteLine(ExtractionConverters.Unregister<Money>()); // prints: False
```

| Member | What it does |
|--------|--------------|
| `Register<T>(Func<string, T> converter)` | Registers a culture-independent converter for `T`. Replaces any converter already registered for `T`. |
| `Register<T>(Func<string, IFormatProvider, T> converter)` | The same, but the converter also receives the provider you passed to `Extract`, or the invariant culture. |
| `Unregister<T>()` | Removes the converter for `T`. Returns `true` when one was registered. |

Use the provider overload when the text is culture-sensitive:

```csharp
ExtractionConverters.Register<Money>((text, provider) =>
    new Money(decimal.Parse(text, NumberStyles.Number, provider), "EUR"));

Money? amount = anyValue.Extract<Box<Money?>>("1.234,56", new CultureInfo("de-DE")).Value;
Money? missing = anyValue.Extract<Box<Money?>>("").Value;

Console.WriteLine(amount);          // prints: Money { Amount = 1234.56, Currency = EUR }
Console.WriteLine(missing is null); // prints: True (empty text is null for Money?; the converter is not called)
```

A registered converter is checked before the built-in conversions, so you can also replace the built-in rule for a type. This one reads dates in a fixed format:

```csharp
ExtractionConverters.Register<DateTime>(text =>
    DateTime.ParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture));

Pattern readingLine = Pattern.FromRegex(@"(?<taken>\S+) (?<value>\S+)");

Reading reading = readingLine.Extract<Reading>("17.05.2024 3.5");

Console.WriteLine(reading.Taken.Day); // prints: 17
```

- **The lookup order** is: a converter for the exact member type, then (for `Nullable<T>`) an empty-text check that returns `null`, then a converter for `T`, then the built-in conversions.
- **A converter for `T` also serves `T?`.** You do not need to register both.
- **Converters are global.** They are stored in one thread-safe, process-wide table. A registration affects every pattern and every [template](./templates.md) in the process, including other libraries that use typed extraction for the same type.
- **Errors in a converter.** A `FormatException`, `OverflowException` or `ArgumentException` thrown by your converter is wrapped in a `FormatException` that names the group. Any other exception propagates unchanged, even through `TryExtract`.
- `Register` throws `ArgumentNullException` for a `null` converter.

:::note

Because converters are global, tests that register one should remove it in a `finally` block or a fixture's dispose. Otherwise a test that runs in parallel may see it. To format a group differently for one pattern only, use a [template](./templates.md) with a per-placeholder format instead.

:::

## Extracting every match

`ExtractAll<T>` returns one object for each match in the input, from left to right:

```csharp
using System.Collections.Generic;

foreach (Size icon in size.ExtractAll<Size>("icons: 16x16, 32x32px and 64x64em"))
{
    Console.WriteLine(icon);
}
// prints:
// Size { Width = 16, Height = 16, Unit = px }
// Size { Width = 32, Height = 32, Unit = px }
// Size { Width = 64, Height = 64, Unit = em }

List<Size> none = new List<Size>(size.ExtractAll<Size>("no sizes here"));
Console.WriteLine(none.Count); // prints: 0
```

The sequence is lazy. Matching and binding happen while you enumerate, so a conversion error surfaces at the match that causes it, after the earlier objects were already returned:

```csharp
public record Entry(int Value);
```

```csharp
Pattern number = Pattern.FromRegex(@"(?<value>\d+)");

IEnumerable<Entry> entries = number.ExtractAll<Entry>("1 2 99999999999 4"); // nothing runs yet

try
{
    foreach (Entry entry in entries)
    {
        Console.WriteLine(entry.Value); // prints: 1, then 2
    }
}
catch (FormatException ex)
{
    Console.WriteLine(ex.Message);
    // prints: Group 'value' value "99999999999" cannot be converted to Int32: Value was either too large or too small for an Int32.
}
```

- A `null` input throws `ArgumentNullException` immediately, when you call `ExtractAll`, not when you start enumerating.
- Every enumeration runs the matching again. Call `.ToList()` once if you need the results more than once.
- There is no "try" form of `ExtractAll`. When some matches may hold bad values, tighten the groups in the pattern, or use `Matches` and `TryExtract` on each match's text.
- `ExtractAll` needs the whole input as a `string`. For a large file, read it line by line and call `TryExtract` on each line, as shown in the next section, or see [Streaming](./streaming.md).

### Many lines in one string

A pattern that uses `StartOfLine` and `EndOfLine` (`^` and `$`) matches only at the start and end of the whole input by default. Over a text with several lines it finds nothing. Turn on `RegexOptions.Multiline` so that `^` and `$` match at every line:

```csharp
using System.Text.RegularExpressions;

// logLine is the pattern from the first sample
string log = "2024-05-17T10:30:00 ERROR Disk full\n2024-05-17T10:31:00 INFO Recovered\n";

List<LogEntry> nothing = new List<LogEntry>(logLine.ExtractAll<LogEntry>(log));
Console.WriteLine(nothing.Count); // prints: 0

Pattern logLines = logLine.WithOptions(RegexOptions.Multiline);

foreach (LogEntry line in logLines.ExtractAll<LogEntry>(log))
{
    Console.WriteLine($"{line.Timestamp:HH:mm} {line.Level} {line.Message}");
}
// prints:
// 10:30 Error Disk full
// 10:31 Info Recovered
```

:::warning

With Windows line endings (`\r\n`), `$` matches before the `\n`, so a group such as `Anything.Repeat.OneOrMore` at the end of the line keeps the trailing `\r` in its text. Exclude `\r` from the last group, or normalize the line endings first.

:::

## Extracting without exceptions

`TryExtract<T>` returns `false` instead of throwing when the input does not match or a value cannot be converted:

```csharp
if (size.TryExtract("5x6", out Size? found))
{
    Console.WriteLine(found); // prints: Size { Width = 5, Height = 6, Unit = px }
}

Console.WriteLine(size.TryExtract("nope", out Size? notFound));        // prints: False
Console.WriteLine(notFound is null);                                  // prints: True
Console.WriteLine(anyValue.TryExtract("300", out Box<byte>? tooBig)); // prints: False (300 overflows a byte)
```

It fits line-by-line parsing of mixed input:

```csharp
using System;
using System.IO;
using Masterly.FluentRegex;

// logLine is the pattern from the first sample; app.log comes from your application
foreach (string line in File.ReadLines("app.log"))
{
    if (logLine.TryExtract(line, out LogEntry entry))
    {
        Console.WriteLine($"{entry.Level}: {entry.Message}");
    }
}
```

What `TryExtract` handles, and what it does not:

| Situation | Result |
|-----------|--------|
| The input does not match | `false`, `result` is `default(T)` |
| The input is `null` | `false`, `result` is `default(T)` |
| A value cannot be converted (format, overflow, no converter) | `false`, `result` is `default(T)` |
| The input matches, but a required parameter has no group, or `T` has no usable constructor | Throws `InvalidOperationException`. This is a programming error, not bad input. |
| A custom converter throws something other than `FormatException`, `OverflowException` or `ArgumentException` | The exception propagates. |
| The pattern's [timeout](./redos-safety.md) elapses | Throws `RegexMatchTimeoutException`. |

:::note

The `out` parameter is not annotated with `[NotNullWhen(true)]`. If you declare it as nullable (`out LogEntry? entry`), the compiler warns when you use it inside the `if`. Declare it as the non-nullable type, as in the line-by-line sample, or use `!`.

:::

## When extraction fails

`Extract<T>` and `ExtractAll<T>` report every problem with an exception whose message says what to fix:

| Exception | Thrown when | Message example |
|-----------|-------------|-----------------|
| `ArgumentNullException` | The input is `null`. | `Value cannot be null. (Parameter 'input')` |
| `FormatException` | The input does not match. | `The input does not match the pattern ... Use Trace(input) to see where it fails.` |
| `FormatException` | A group's text cannot be converted. The original exception is the `InnerException`. | `Group 'width' value "abc" cannot be converted to Int32: ...` |
| `InvalidOperationException` | A non-nullable value-type constructor parameter without a default has no group in the pattern. | `Cannot extract 'Size': the pattern has no group for Height (group 'Height'). Available groups: 0, width. ...` |
| `InvalidOperationException` | `T` is a class without a public constructor. | `Type 'NoPublicConstructor' has no public constructor.` |
| `InvalidOperationException` | `T` is a struct with neither a public constructor nor a settable property. | `Value type 'Opaque' needs a public constructor or settable properties.` |
| `RegexMatchTimeoutException` | The pattern has a timeout and matching takes longer. | The standard .NET message. |

```csharp
try
{
    size.Extract<Size>("nope");
}
catch (FormatException ex)
{
    Console.WriteLine(ex.Message);
    // prints: The input does not match the pattern (?<width>\d+)x(?<height>\d+)(?<unit>[a-zA-Z]+)?. Use Trace(input) to see where it fails.
}

try
{
    Pattern.FromRegex(@"(?<width>\w+)x(?<height>\d+)").Extract<Size>("abcx5");
}
catch (FormatException ex)
{
    Console.WriteLine(ex.Message);
    // prints: Group 'width' value "abc" cannot be converted to Int32: The input string 'abc' was not in a correct format.
}

try
{
    Pattern.FromRegex(@"(?<width>\d+)").Extract<Size>("5");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message);
    // prints: Cannot extract 'Size': the pattern has no group for Height (group 'Height'). Available groups: 0, width. Add NamedGroup(...) to the pattern, give the parameter a default value, or use [RegexGroup("name")].
}
```

Other conversion messages you may see:

| Input and target | Message after `cannot be converted to ...:` |
|------------------|---------------------------------------------|
| `"maybe"` to `bool` | `'maybe' is not a boolean.` |
| `"ab"` to `char` | `Expected exactly one character.` |
| `"300"` to `byte` | `Value was either too large or too small for an unsigned byte.` |
| `"x"` to a type without a converter, such as `StringBuilder` | `No converter for type 'System.Text.StringBuilder'. Register one with ExtractionConverters.Register.` |

- The "does not match" message includes the pattern. When the input is sensitive, do not log the message as is. The input itself is not included.
- To find out why an input does not match, call `pattern.Trace(input)`. See [Match Tracing](./match-tracing.md).
- The type check happens at the first successful match for a pattern and type, not when you build the pattern. An input that does not match reports "does not match" first. A missing group throws on every matching call until you fix it.

## Which part of the input is matched

`Extract<T>` and `TryExtract<T>` use the first match anywhere in the input, like `Regex.Match`. A pattern that is not anchored extracts from the middle of a longer text:

```csharp
Console.WriteLine(size.Extract<Size>("Icon: 32x32px, retina 64x64px")); // prints: Size { Width = 32, Height = 32, Unit = px }

Pattern exactSize = Pattern.With.StartOfInput.Then(size).EndOfInput;

Console.WriteLine(exactSize.TryExtract("Icon: 32x32px", out Size? _)); // prints: False
Console.WriteLine(exactSize.TryExtract("32x32px", out Size? _));       // prints: True
```

:::tip

When you validate a whole value, such as a form field or a file name, anchor the pattern with `StartOfInput` and `EndOfInput`. Otherwise `"garbage 32x32 garbage"` extracts happily. See [Assertions](./assertions.md).

:::

Ready-made patterns from the [pattern library](./common-patterns.md) work as well when they use named groups. `CommonPatterns.SemVer()` has the groups `major`, `minor`, `patch`, `prerelease` and `buildmetadata`:

```csharp
public record SemVer(int Major, int Minor, int Patch, string? Prerelease, string? BuildMetadata);
```

```csharp
SemVer beta = CommonPatterns.SemVer().Extract<SemVer>("2.1.0-beta.3+build.7");
SemVer release = CommonPatterns.SemVer().Extract<SemVer>("1.0.0");

Console.WriteLine(beta);                       // prints: SemVer { Major = 2, Minor = 1, Patch = 0, Prerelease = beta.3, BuildMetadata = build.7 }
Console.WriteLine(release.Prerelease is null); // prints: True
```

## Compile-time checks

The analyzer that ships with the package checks `Extract<T>`, `TryExtract<T>` and `ExtractAll<T>` calls while you build. It reports **FRX008** (a warning) for each constructor parameter of `T` that has no group in the pattern:

```csharp
using System;
using Masterly.FluentRegex;

public static class LogPatterns
{
    public static readonly Pattern ShortLine = Pattern.With
        .NamedGroup("timestamp", Pattern.With.NonWhitespace.Repeat.OneOrMore)
        .Whitespace
        .NamedGroup("level", Pattern.With.Word.Repeat.OneOrMore);
}
```

```csharp
LogEntry entry = LogPatterns.ShortLine.Extract<LogEntry>("2024-05-17 INFO");
```

```text
warning FRX008: 'LogEntry.Message' has no matching group 'Message' in the pattern, so it will never be set. Available groups: 0, timestamp, level. (https://github.com/a7mdfre7at/Masterly.FluentRegex#compile-time-checks)
```

- The analyzer applies the same rules as the binder: the constructor with the most parameters, case-insensitive names and `[RegexGroup]`.
- It is stricter than the runtime. It warns for every parameter without a default value, including `string` and nullable ones that the runtime would silently set to `null`. That is exactly the typo the runtime cannot catch.
- It checks constructor parameters only, not settable properties.
- It can only check patterns it can evaluate while compiling: a fluent chain written inline, or a `static` field or property with a single-expression initializer, as in the sample. A pattern stored in a local variable or passed as a parameter is not checked.
- To make the warning fail the build, add `dotnet_diagnostic.FRX008.severity = error` to your `.editorconfig`.

See [Compile-Time Checks](./compile-time-checks.md) for the other analyzer rules.

## Performance and thread safety

- **One plan per type and pattern.** The first extraction for a combination of `T` and regex (its text and options) reads the type with reflection and caches a binding plan. Later calls reuse it, also from other `Pattern` instances with the same regex. The cache is never trimmed, so an application that builds many distinct patterns at runtime (for example from user input) keeps one plan per pattern and type for the life of the process.
- **Reflection per object.** Each object is created with `ConstructorInfo.Invoke`, and properties are set with `PropertyInfo.SetValue`, so values are boxed. This is fast enough for parsing files and requests. In a hot loop over millions of matches, measure before you choose it over reading `Match.Groups` by hand.
- **The regex is cached** on the `Pattern`, so `Extract` does not recompile the pattern on each call.
- **Thread safety.** `Extract<T>`, `TryExtract<T>` and `ExtractAll<T>` can be called from many threads on the same `Pattern`. `ExtractionConverters` is thread-safe too, but because it is global, register converters at startup rather than while extractions run.
- **Untrusted input.** Extraction runs the regex like any other match. Use `WithTimeout(...)` on patterns that see untrusted input. A timeout surfaces as `RegexMatchTimeoutException`, even from `TryExtract`. See [ReDoS Safety](./redos-safety.md).

## Trimming and Native AOT

On `net8.0` and `net10.0` the package is marked AOT-compatible. The type parameter of `Extract<T>`, `TryExtract<T>` and `ExtractAll<T>` carries `[DynamicallyAccessedMembers(PublicConstructors | PublicProperties)]`. When you call them with a concrete type, the trimmer keeps that type's public constructors and properties, and extraction works in trimmed and Native AOT apps with no extra setup.

When you wrap extraction in your own generic method, forward the annotation. Without it, the trim analyzer reports IL2091:

```csharp
using System.Diagnostics.CodeAnalysis;
using Masterly.FluentRegex;

public static class Parser
{
    // warning IL2091: 'T' generic argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicConstructors', ...
    public static T ParseUnannotated<T>(Pattern pattern, string input) => pattern.Extract<T>(input);

    // no warning
    public static T ParseAnnotated<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        Pattern pattern, string input) => pattern.Extract<T>(input);
}
```

- `[RegexGroup]` attributes are read with reflection. They stay available because the trimmer keeps attributes on the members it keeps.
- Custom converters are plain delegates and need nothing special.
- On `netstandard2.1` the annotations have no effect, because trimming does not apply there.

## Target frameworks

Typed extraction is available on every build of the package. One difference matters:

| Build | Used by | `DateOnly` and `TimeOnly` |
|-------|---------|---------------------------|
| `net8.0`, `net10.0` | Apps on .NET 8 or later | Built in |
| `netstandard2.1` | Apps on .NET Core 3.x, .NET 5, 6 and 7, Mono, Unity | Not built in. Register a converter with `ExtractionConverters.Register<DateOnly>(...)` if your runtime has the type. |

## API summary

| Member | Description |
|--------|-------------|
| `T Pattern.Extract<T>(string input, IFormatProvider provider = null)` | Matches the input and binds the first match to a new `T`. Throws `ArgumentNullException`, `FormatException` or `InvalidOperationException`. |
| `bool Pattern.TryExtract<T>(string input, out T result, IFormatProvider provider = null)` | Like `Extract`, but returns `false` for a `null` input, no match or a conversion error. |
| `IEnumerable<T> Pattern.ExtractAll<T>(string input, IFormatProvider provider = null)` | Lazily binds every match. Throws `ArgumentNullException` eagerly. |
| `RegexGroupAttribute(string name)` | Binds a constructor parameter or property to the named or numbered group `name`. |
| `RegexGroupAttribute.Name` | The group name. |
| `ExtractionConverters.Register<T>(Func<string, T>)` | Registers or replaces a converter for `T`. |
| `ExtractionConverters.Register<T>(Func<string, IFormatProvider, T>)` | Registers or replaces a culture-aware converter for `T`. |
| `ExtractionConverters.Unregister<T>()` | Removes the converter for `T`. Returns `true` when one was removed. |

Related pages:

- [Templates](./templates.md) parse and format one line format with per-placeholder formats, using the same binding rules.
- [Matching](./matching.md) covers `Match`, `Matches` and the other untyped operations.
- [Fluent Replacements](./fluent-replacements.md) rewrite matches by group name instead of extracting them.
