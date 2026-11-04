---
id: templates
title: "Typed Templates"
description: "String interpolation in reverse: one template parses text into a typed object and formats the object back, with formats for numbers, dates and times."
---

A typed template describes a line of text the way you would write an interpolated string, such as `"{Date:yyyy-MM-dd} [{Level}] {User}: {Message}"`. `Template.Of<T>` compiles it into a parser **and** a formatter for your type `T`. Use a template when you read and write the same text format, such as a log line, a file name, an invoice number or a fixed-width record, and want typed values instead of strings.

| You want to | Use |
|-------------|-----|
| Read and write one line format with typed values | A template (this page) |
| Bind the named groups of a pattern you already built to an object | [Typed Extraction](./typed-extraction.md) (`Pattern.Extract<T>`) |
| Rewrite matches inside a larger text | [Fluent Replacements](./fluent-replacements.md) |

Templates live in the core package, in the `Masterly.FluentRegex` namespace, and work on every target framework (`netstandard2.1`, `net8.0` and `net10.0`). Only `DateOnly` and `TimeOnly` members need `net8.0` or later.

## A first template

Declare a type with one member per value:

```csharp
using System;

public enum LogLevel { Debug, Info, Warn, Error }

public record LogLine(DateTime Date, LogLevel Level, string User, string Message);
```

Then write the template with one `{Name}` placeholder per member. Everything else in the template is literal text.

```csharp
using System;
using Masterly.FluentRegex;

Template<LogLine> log = Template.Of<LogLine>("{Date:yyyy-MM-dd} [{Level}] {User}: {Message}");

LogLine line = log.Parse("2026-11-02 [WARN] ahmad: disk at 91%");
// line.Date    = 2026-11-02 (DateTime)
// line.Level   = LogLevel.Warn
// line.User    = "ahmad"
// line.Message = "disk at 91%"

string text = log.Format(line);            // "2026-11-02 [Warn] ahmad: disk at 91%"
bool roundTrips = log.Parse(text) == line; // true
```

- `{Date:yyyy-MM-dd}` has a format after the colon. It controls both how the date is matched and how it is written.
- `{Level}` binds to an enum. It matches the member names in any case (`WARN`), and `Format` writes the declared name (`Warn`).
- `Parse(Format(x))` round-trips because both directions use the same formats and the same culture.
- Compile a template once and reuse it. `Template<T>` is immutable and thread-safe, so a `static readonly` field is a good home for it.

## The template API

`Template.Of<T>` compiles the template. Everything else is a member of the returned `Template<T>`.

| Member | What it does |
|--------|--------------|
| `Template.Of<T>(string template, TemplateOptions? options = null)` | Compiles the template for `T`. Throws `ArgumentNullException` for a `null` template and `ArgumentException` for any problem in it (see [Errors when compiling a template](#errors-when-compiling-a-template)). `null` options use the defaults. |
| `T Parse(string input)` | Parses text that must match the **whole** template. Throws `FormatException` that says where and why it failed. |
| `bool TryParse(string input, out T value)` | Like `Parse`, but returns `false` instead of throwing a `FormatException`. `value` is `default` on failure. |
| `IEnumerable<T> ParseAll(string text)` | Finds every occurrence of the template inside a larger text, such as a whole log file. Lazy. |
| `string Format(T value)` | Writes the value with the template. Throws `ArgumentException` when a member value would not parse back. |
| `Pattern Pattern` | The compiled, unanchored [pattern](./core-concepts.md) with one named group per placeholder. |
| `string ToString()` | Returns the template text you passed to `Of`. |

```csharp
// log is the template from "A first template"
bool parsed = log.TryParse("2026-11-02 [WARN] ahmad: disk at 91%", out LogLine? entry); // true
bool failed = log.TryParse("2026/11/02 [WARN] ahmad: disk", out LogLine? none);        // false, none is null

string source = log.ToString(); // "{Date:yyyy-MM-dd} [{Level}] {User}: {Message}"
```

- `Parse` and `TryParse` are anchored: the input must match from its first to its last character. Extra text before or after the template is a failure. To find the template inside a larger text, use [`ParseAll`](#scanning-a-larger-text) or [`Pattern`](#using-the-templates-pattern).
- `TryParse` returns `false` for a `null` input, for text that does not match, and for a value that matches but cannot be converted, such as an `int` that overflows. Some other failures still throw. See the warning in [Timeout](#timeout) and [Limitations and pitfalls](#limitations-and-pitfalls).
- `Parse` throws `ArgumentNullException` for a `null` input, `ParseAll` for a `null` text and `Format` for a `null` value.
- There is no `ToPattern` method. The compiled regex is the `Pattern` property (see [Using the template's pattern](#using-the-templates-pattern)).

## Placeholder syntax

A placeholder is `{Name}` or `{Name:format}`. All other characters are literal text, and the library escapes them for you: `[`, `]`, `.` and the other regex metacharacters match themselves.

| Syntax | Meaning |
|--------|---------|
| `{Name}` | A member of `T`, matched by its type's default rule. |
| `{Name:format}` | A member with a format. The format depends on the member type: `D4` for integers, `F2` for decimals, `yyyy-MM-dd` for dates, and so on. |
| `{Name:re:regex}` | A member matched by your own regex. Works for every member type. See [Custom regex with re:](#custom-regex-with-re). |
| `{{` and `}}` | A literal `{` and `}`. |

```csharp
public record Item(int Id);
```

```csharp
using Masterly.FluentRegex;

Template<Item> braces = Template.Of<Item>("{{{Id}}}");

int id = braces.Parse("{42}").Id;           // 42
string text = braces.Format(new Item(7));   // "{7}"
```

- A placeholder name starts with a letter or `_` and contains only letters, digits and `_`. Spaces are not trimmed: `{ Id }` throws.
- Everything after the **first** colon is the format, so a format may contain more colons, as in `{Start:HH:mm}`.
- A format may contain braces, such as `{Sku:re:[A-Z]{3}\d+}`. The parser counts nested braces, and `\` escapes the next character, so `{Text:re:[^\}]+}` is one placeholder.

### Names and binding

Placeholder names bind to `T` exactly like [Typed Extraction](./typed-extraction.md):

1. The parameters of the public constructor with the **most** parameters.
2. Then the public settable properties (`set` or `init`) that are not already bound through the constructor.

Names are case-insensitive, so `{id}` binds the member `Id`. `[RegexGroup("name")]` on a parameter or property renames the placeholder.

```csharp
using Masterly.FluentRegex;

public record Sized([RegexGroup("w")] int Width);

public class Endpoint
{
    public string Host { get; set; } = "";

    public int Port { get; set; }
}
```

```csharp
using Masterly.FluentRegex;

Template<Sized> sized = Template.Of<Sized>("w={w}");
int width = sized.Parse("w=640").Width; // 640

Template<Endpoint> endpoint = Template.Of<Endpoint>("{Host}:{Port}");
Endpoint parsed = endpoint.Parse("db.local:5432");                          // Host = "db.local", Port = 5432
string text = endpoint.Format(new Endpoint { Host = "cache", Port = 6379 }); // "cache:6379"
```

- After a rename, only the new name works: `Template.Of<Sized>("w={Width}")` throws, and the message lists `{w}` as the available name.
- An unknown name fails at `Template.Of<T>`, not at parse time. The message lists every name you can use.
- Every constructor parameter that has no default value and is not nullable must have a placeholder. Leaving one out throws `ArgumentException` at `Template.Of<T>`.
- A constructor parameter with a default value, a nullable parameter and a settable property may be left out. The parameter gets its default value and the property keeps its initial value.

```csharp
public record Order(int Id, string Note = "none");
```

```csharp
using Masterly.FluentRegex;

Template<Order> order = Template.Of<Order>("order {Id}");
Order parsed = order.Parse("order 9"); // Order { Id = 9, Note = none }
```

### Repeated placeholders

A placeholder may appear more than once. The second occurrence must read the **same text** as the first, because it compiles to a backreference.

```csharp
public record Pair(string Word);
```

```csharp
using Masterly.FluentRegex;

Template<Pair> pair = Template.Of<Pair>("{Word}-{Word}");

string word = pair.Parse("ab-ab").Word;           // "ab"
bool different = pair.TryParse("ab-cd", out _);   // false
string text = pair.Format(new Pair("xy"));        // "xy-xy"
string regex = pair.Pattern.ToString();           // (?<Word>[^\r\n]*?)-\k<Word>
```

A repeated placeholder must use the same format every time. `"{Number:D4}/{Number:D2}"` throws `ArgumentException`.

## How each member type is matched

Each placeholder becomes a regex fragment inside one named group. The member type, and the format if you give one, decide the fragment. The same choice decides how the text is converted and how a value is written.

| Member type | Without a format | Formats |
|-------------|------------------|---------|
| `string` | Any text on one line, lazy: `[^\r\n]*?`. The **last** placeholder of the template is greedy: `[^\r\n]*`. | `re:` only |
| `int`, `long`, `short`, `sbyte` | Optional minus sign of the culture, then `\d+` | `D`, `Dn`, `X`, `Xn` |
| `uint`, `ulong`, `ushort`, `byte` | `\d+` (no sign) | `D`, `Dn`, `X`, `Xn` |
| `decimal` | Optional minus sign, digits, optional decimal separator and digits of the culture | `G`, `F`, `Fn` |
| `double`, `float` | As `decimal`, plus an exponent and the culture's `NaN` and infinity symbols. Written with `R`. | `G`, `R`, `F`, `Fn` |
| `bool` | `true` or `false` in any case. Written in lower case. | `re:` only |
| `Guid` | Format `D` (hyphenated) | `D`, `N`, `B`, `P` |
| `char` | Exactly one character that is not a line break | `re:` only |
| Enums | The member names in any case, longest first | `re:` only |
| `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` | Any non-empty text on one line, read by the [extraction converters](./typed-extraction.md). Written with `O`. | .NET custom formats and the standard formats `d D f F g G m M o O r R s t T u y Y` |
| `TimeSpan` | Any non-empty text on one line, read by `TimeSpan.Parse`. Written with `c`. | Custom TimeSpan formats and `c`, `t`, `T`, `g`, `G` |
| `Nullable<T>` | The fragment of `T`, made optional | The formats of `T` |
| Anything else (`Uri`, `Version`, your own types) | Any non-empty text on one line, read by the extraction converters | `re:` only |

Every type also accepts `{Name:re:regex}`. A format that the type does not support fails at `Template.Of<T>` with a message that lists the supported formats.

### Strings

A string placeholder matches any text on one line. It is lazy, so it stops at the first place where the rest of the template can match. The last placeholder of the template is greedy and takes the rest of the line.

```csharp
// log is the template from "A first template"
LogLine line = log.Parse("2026-11-02 [WARN] ahmad: a: b: c");
// line.User = "ahmad", line.Message = "a: b: c"
```

- `{User}` stops at the first `": "`, and `{Message}` takes everything after it, including more `": "`.
- String placeholders never match a line break. `Format` rejects a string that contains one (see [Errors when formatting](#errors-when-formatting)).
- A string placeholder can match an empty string, as in `"a {Text} b"` with the input `"a  b"`.
- A `null` string is written as an empty string and parses back as `""`, not `null`.

### Integers

Integers match digits, and signed types also accept the culture's minus sign. Formats work as in `int.ToString(format)`:

| Format | Matches | Writes |
|--------|---------|--------|
| (none) or `D` | One or more digits | `42`, `-12` |
| `Dn`, such as `D4` | Exactly *n* digits | `0042` |
| `X` | One or more hex digits, any case | `FF` (`x` writes `ff`) |
| `Xn`, such as `X6` | Exactly *n* hex digits, any case | `FF8800` |

```csharp
public record Invoice(int Number);

public record Delta(int Change, uint Count);

public record Color(int Rgb);
```

```csharp
using Masterly.FluentRegex;

Template<Invoice> invoice = Template.Of<Invoice>("INV-{Number:D4}");
int number = invoice.Parse("INV-0042").Number;          // 42
bool tooShort = invoice.TryParse("INV-42", out _);      // false: D4 needs exactly 4 digits
string text = invoice.Format(new Invoice(7));           // "INV-0007"

Template<Delta> delta = Template.Of<Delta>("{Change}/{Count}");
Delta parsed = delta.Parse("-5/3");                     // Delta { Change = -5, Count = 3 }
bool unsignedMinus = delta.TryParse("5/-3", out _);     // false: uint has no sign

Template<Color> color = Template.Of<Color>("#{Rgb:X6}");
int rgb = color.Parse("#ff8800").Rgb;                   // 16746496
string hex = color.Format(new Color(0xFF8800));         // "#FF8800"
```

- The format letter is case-insensitive for matching. For writing it follows .NET: `X6` writes upper-case and `x6` lower-case hex digits.
- The width after the letter can be 0 to 99. `D100`, `Dx` and formats such as `N2` throw `ArgumentException` at `Template.Of<T>`.
- `Format` rejects a value that does not fit the width, such as `12345` for `D4`, because a `D4` placeholder reads exactly four digits.
- Hex placeholders have no sign. A negative value is written in two's complement (`-1` as `FFFFFFFF` with `X`) and reads back as the same negative value.
- A matched number that overflows the member type, such as `99999999999` for an `int`, fails at parse time with a `FormatException` that names the member.

### Decimal, double and float

Floating-point members match an optional minus sign, digits and an optional fraction, using the culture's decimal separator. `double` and `float` also match an exponent (`1.5e-3`) and the culture's `NaN`, `Infinity` and `-Infinity` symbols.

| Format | Matches | Writes |
|--------|---------|--------|
| (none) or `G` | A plain number (with exponent and specials for `double` and `float`) | `decimal`: as stored, such as `1.10`. `double` and `float`: the round-trip `R` text when there is no format. |
| `R` | Same as `G`. `double` and `float` only. | The shortest text that reads back as the same value |
| `F` | Exactly the culture's default number of decimals (2 in the invariant culture) | `3.00` |
| `Fn`, such as `F2` | Exactly *n* decimals. `F0` means no decimal separator. | `9.50` |

```csharp
public record Price(decimal Amount);

public record Measure(decimal Price, double Ratio, float Weight);
```

```csharp
using Masterly.FluentRegex;

Template<Price> price = Template.Of<Price>("{Amount:F2} USD");
decimal amount = price.Parse("9.50 USD").Amount;        // 9.50
bool oneDecimal = price.TryParse("9.5 USD", out _);     // false: F2 needs exactly 2 decimals
string rounded = price.Format(new Price(3.456m));       // "3.46 USD"

Template<Measure> measure = Template.Of<Measure>("{Price};{Ratio};{Weight}");
Measure parsed = measure.Parse("-12.5;1.5e-3;-Infinity"); // Price = -12.5, Ratio = 0.0015, Weight = float.NegativeInfinity
bool grouped = measure.TryParse("1,000.5;1;1", out _);     // false: no group separators
```

- Group separators (`1,000.5`) and a leading `+` are never matched. Use `re:` if your text has them.
- `Format` with `Fn` rounds the value, so `3.456m` becomes `3.46`. The text parses back, but as the rounded value.
- `R` on a `decimal`, and formats such as `N2` or `C`, throw `ArgumentException` at `Template.Of<T>`.

### Booleans, GUIDs, characters and enums

```csharp
using System;

public enum Mode { In, Inside, Out }

public record Flag(bool Enabled);

public record Reference(Guid Id);

public record ModeLine(Mode Mode, string Rest);

public record Initial(char Letter, string Rest);
```

```csharp
using System;
using Masterly.FluentRegex;

Template<Flag> flag = Template.Of<Flag>("enabled={Enabled}");
bool enabled = flag.Parse("enabled=TRUE").Enabled;     // true
bool word = flag.TryParse("enabled=yes", out _);       // false: only true or false
string flagText = flag.Format(new Flag(true));         // "enabled=true"

Template<Reference> reference = Template.Of<Reference>("id={Id:B}");
Guid id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
string idText = reference.Format(new Reference(id));   // "id={0f8fad5b-d9cb-469f-a165-70867728950e}"

Template<ModeLine> mode = Template.Of<ModeLine>("{Mode}{Rest}");
ModeLine modeLine = mode.Parse("Inside!");             // ModeLine { Mode = Inside, Rest = ! }

Template<Initial> initial = Template.Of<Initial>("{Letter}{Rest}");
Initial letter = initial.Parse("Xylophone");           // Initial { Letter = X, Rest = ylophone }
```

| Type | Notes |
|------|-------|
| `bool` | Matches `true` and `false` in any case. Other words that typed extraction accepts, such as `yes` or `1`, do not match. Written as `true` or `false`. |
| `Guid` | `D` (default) `0f8fad5b-d9cb-...`, `N` 32 hex digits, `B` in braces, `P` in parentheses. The format letter is case-insensitive. Hex digits match in any case. Other formats throw. |
| `char` | Exactly one character that is not a line break. |
| Enums | The member names, longest first, so `Inside` wins over `In`. `Format` writes the declared name, and throws `ArgumentException` for a value that is not a single named member, such as `(LogLevel)42` or a `[Flags]` combination like `Read, Write`. |

### Nullable members

A `Nullable<T>` member uses the fragment of `T`, made optional. Empty text reads as `null`, and `null` writes nothing.

```csharp
public record Reading(int? Value, string Unit);
```

```csharp
using Masterly.FluentRegex;

Template<Reading> reading = Template.Of<Reading>("temp={Value}{Unit}");

Reading warm = reading.Parse("temp=21C");                 // Value = 21, Unit = "C"
Reading missing = reading.Parse("temp=C");                // Value = null, Unit = "C"
string text = reading.Format(new Reading(null, "C"));     // "temp=C"
string regex = reading.Pattern.ToString();                // temp=(?<Value>(?:(?:-)?\d+)?)(?<Unit>[^\r\n]*)
```

- Formats work as for `T`: `{Value:D4}` on an `int?` matches four digits or nothing.
- A nullable constructor parameter is never required, so you may leave it out of the template.

### Other types and registered converters

Types without a dedicated rule, such as `Uri` and `Version`, match any non-empty text on one line and are read by the typed-extraction converters. They are written with `ToString()`, or `IFormattable.ToString(null, culture)` when the type implements it.

```csharp
using System;

public record Link(Uri Url, Version Release);

public record Barcode(string Digits)
{
    public override string ToString() => Digits;
}

public record Packaged(Barcode Code);
```

```csharp
using Masterly.FluentRegex;

Template<Link> link = Template.Of<Link>("{Url} v{Release}");
Link parsed = link.Parse("https://example.com/docs v3.1.0"); // Url = https://example.com/docs, Release = 3.1.0
string text = link.Format(parsed);                           // "https://example.com/docs v3.1.0"

ExtractionConverters.Register<Barcode>(code => new Barcode(code.ToUpperInvariant()));

Template<Packaged> packaged = Template.Of<Packaged>("item {Code}");
string digits = packaged.Parse("item abc12").Code.Digits;            // "ABC12"
string item = packaged.Format(new Packaged(new Barcode("XY9")));     // "item XY9"
```

- A converter registered with `ExtractionConverters.Register<T>` wins over the built-in rules, even for built-in types such as `int`. The placeholder then matches any non-empty text on one line and takes no format other than `re:`.
- The fragment is chosen when the template compiles, so register converters **before** calling `Template.Of<T>`. A template compiled earlier keeps its built-in fragment and formats.
- The `Register<T>(Func<string, IFormatProvider, T>)` overload receives the template's `Culture`.
- `Template.Of<T>` does not check that a converter exists. A type with no converter compiles, and `Parse` then throws `FormatException`: `No converter for type 'Barcode'. Register one with ExtractionConverters.Register.`
- Converters are global, so register them once at startup. See [Typed Extraction](./typed-extraction.md).

### Custom regex with re:

`{Name:re:regex}` replaces the fragment of any member type with your own regex. The matched text is still converted, and the value still written, by the member type's default rule.

```csharp
public record Product(string Sku, int Quantity, decimal Price);
```

```csharp
using Masterly.FluentRegex;

Template<Product> product = Template.Of<Product>(@"{Sku:re:[A-Z]{3}\d+} x{Quantity} @ {Price:F2}");

Product parsed = product.Parse("ABC123 x2 @ 9.50");           // Product { Sku = ABC123, Quantity = 2, Price = 9.50 }
string text = product.Format(new Product("XYZ9", 4, 12m));    // "XYZ9 x4 @ 12.00"
string regex = product.Pattern.ToString();
// (?<Sku>(?:[A-Z]{3}\d+)) x(?<Quantity>(?:-)?\d+) @ (?<Price>(?:-)?\d+\.\d{2})
```

- The regex is checked at `Template.Of<T>`. An empty or invalid regex throws `ArgumentException` that quotes the .NET parser error.
- `Format` checks the written value against your regex. `new Product("abc", 1, 1m)` throws because `abc` does not match `[A-Z]{3}\d+`.
- `re:` changes what is matched, not how the text is converted or written. `{Enabled:re:yes|no}` on a `bool` matches `yes`, but `bool.Parse` cannot convert it, so `Parse` throws and `TryParse` returns `false`. Use a registered converter for such types.
- A date or time with `re:` has no format, so `Format` writes the round-trip `O` text. `{When:re:\d{4}-\d{2}-\d{2}}` parses `2026-11-02`, but `Format` throws because `2026-11-02T00:00:00.0000000` does not match the regex. Use a date format such as `{When:yyyy-MM-dd}` instead.
- Your regex should not match line breaks if you use `ParseAll` on multi-line text.

**Named groups inside `re:` bind more members.** A named group in your regex binds the member with the same name, using the default conversion. Those members are filled when parsing, but `Format` writes only the outer placeholder.

```csharp
public record Coded(string Whole, string Prefix = "", int Number = 0);
```

```csharp
using System;
using Masterly.FluentRegex;

Template<Coded> coded = Template.Of<Coded>(@"{Whole:re:(?<Prefix>[A-Z]+)-(?<Number>\d+)};");

foreach (Coded code in coded.ParseAll("AB-42; CD-7;"))
{
    Console.WriteLine(code);
}
// prints:
// Coded { Whole = AB-42, Prefix = AB, Number = 42 }
// Coded { Whole = CD-7, Prefix = CD, Number = 7 }
```

## Dates and times

With a format, a date placeholder is translated into a regex that matches exactly what `ToString(format, culture)` produces. The text is read back with `ParseExact` in the same culture, so `Parse(Format(x))` round-trips.

```csharp
using System;

public record DatedInvoice(int Number, DateTime Issued);
```

```csharp
using System;
using Masterly.FluentRegex;

Template<DatedInvoice> invoice = Template.Of<DatedInvoice>("INV-{Number:D4}/{Issued:dd MMM yyyy}");

DatedInvoice parsed = invoice.Parse("INV-0042/02 Nov 2026");                // Number = 42, Issued = 2026-11-02
string text = invoice.Format(new DatedInvoice(7, new DateTime(2026, 1, 5))); // "INV-0007/05 Jan 2026"
string regex = invoice.Pattern.ToString();
// INV-(?<Number>(?:-)?\d{4})/(?<Issued>\d{2} (?i:Apr|Aug|Dec|Feb|Jan|Jul|Jun|Mar|May|Nov|Oct|Sep) \d{4})
```

- Month and day names come from the culture and match in any case, so `02 nov 2026` parses too.
- `DateTime` values are parsed with `DateTimeStyles.RoundtripKind`. A format without `K` or `z` gives `DateTimeKind.Unspecified`.

### Custom date and time formats

| Specifier | Matches |
|-----------|---------|
| `y`, `yy` | 1 to 2 digits, exactly 2 digits |
| `yyy`, `yyyy` (and more `y`) | 3 to 4 digits, exactly 4 digits (or as many as there are `y`s) |
| `M`, `MM` | 1 to 2 digits, exactly 2 digits |
| `MMM`, `MMMM` | Abbreviated and full month names of the culture, including genitive forms |
| `d`, `dd` | 1 to 2 digits, exactly 2 digits |
| `ddd`, `dddd` | Abbreviated and full day names of the culture |
| `H`, `HH`, `h`, `hh`, `m`, `mm`, `s`, `ss` | 1 to 2 digits, exactly 2 digits |
| `f` to `fffffff` | Exactly that many fraction digits (at most 7) |
| `t`, `tt` | The first letter of, or the full, AM/PM designator of the culture. Optional when the culture has an empty designator. |
| `z`, `zz`, `zzz` | Offset as `+3`, `+03`, `+03:00` |
| `K` | `Z`, an offset such as `+03:00`, or nothing |
| `:` and `/` | The culture's time and date separators |
| `'text'`, `"text"`, `\c` | Literal text |
| `%` | Ignored (it marks a one-letter custom format such as `%d`) |
| Any other character | Itself, as a literal |

```csharp
using System;

public record Moment(DateTime When);
```

```csharp
using System;
using Masterly.FluentRegex;

Template<Moment> clock = Template.Of<Moment>("{When:yyyy-MM-dd hh:mm:ss.fff tt}");
Moment afternoon = clock.Parse("2026-11-02 03:04:05.123 PM"); // When = 2026-11-02 15:04:05.123

Template<Moment> names = Template.Of<Moment>("{When:dddd, MMMM d, yyyy}");
string longDate = names.Format(new Moment(new DateTime(2026, 11, 2))); // "Monday, November 2, 2026"

Template<Moment> quoted = Template.Of<Moment>(@"{When:yyyy'-W'MM\-dd 'at' H\h}");
string week = quoted.Format(new Moment(new DateTime(2026, 11, 2, 9, 0, 0))); // "2026-W11-02 at 9h"
```

:::warning

`F` to `FFFFFFF` (optional fraction digits) and `g` (era) are not supported, nor are more than 7 `f`s or an unterminated quote. They throw `ArgumentException` at `Template.Of<T>`, and the message lists the supported specifiers.

:::

### Standard date and time formats

A one-letter format is a .NET standard format. It is expanded to the culture's pattern first, then translated like a custom format. These are the results in the invariant culture for 2026-11-02 14:30:15:

| Format | Writes | Notes |
|--------|--------|-------|
| `d` | `11/02/2026` | Short date |
| `D` | `Monday, 02 November 2026` | Long date |
| `f` | `Monday, 02 November 2026 14:30` | Long date, short time |
| `F` | `Monday, 02 November 2026 14:30:15` | Full date and time |
| `g` | `11/02/2026 14:30` | Short date, short time |
| `G` | `11/02/2026 14:30:15` | Short date, long time |
| `m` or `M` | `November 02` | Month and day |
| `o` or `O` | `2026-11-02T14:30:15.0000000` | Round trip. Always invariant. |
| `r` or `R` | `Mon, 02 Nov 2026 14:30:15 GMT` | RFC 1123. Always invariant. |
| `s` | `2026-11-02T14:30:15` | Sortable. Always invariant. |
| `t` | `14:30` | Short time |
| `T` | `14:30:15` | Long time |
| `u` | `2026-11-02 14:30:15Z` | Universal sortable. Always invariant. |
| `y` or `Y` | `2026 November` | Year and month |

- Other standard formats, such as `U`, throw `ArgumentException`.
- A format that leaves out part of the value loses that part when parsing. `t` and `T` read back with today's date, and `y` reads back as the first day of the month.
- `{When:o}` keeps every tick and the `DateTimeKind`: a UTC value writes as `2026-11-02T10:30:00.1234567Z` and parses back as the same UTC value.

### Dates without a format

A date or time placeholder without a format matches any non-empty text on one line (lazy, unless it is the last placeholder). The typed-extraction converter reads it with `DateTime.Parse` in the template culture, so many layouts are accepted. `Format` writes the round-trip format `O`.

```csharp
using System;
using Masterly.FluentRegex;

// Moment is the record from "Custom date and time formats"
Template<Moment> loose = Template.Of<Moment>("at {When}");

string text = loose.Format(new Moment(new DateTime(2026, 11, 2, 14, 30, 0))); // "at 2026-11-02T14:30:00.0000000"
Moment parsed = loose.Parse("at 2026-11-02 14:30");                          // When = 2026-11-02 14:30:00
```

:::tip

Give date placeholders a format whenever you can. A format makes the fragment precise, so the placeholder cannot swallow neighboring text, and it makes parsing independent of what `DateTime.Parse` happens to accept.

:::

### DateTimeOffset and time zones

```csharp
using System;

public record Stamp(DateTimeOffset When);
```

```csharp
using Masterly.FluentRegex;

Template<Stamp> withOffset = Template.Of<Stamp>("{When:yyyy-MM-ddTHH:mm:sszzz}");
Stamp local = withOffset.Parse("2026-11-02T10:30:00+03:00"); // When = 2026-11-02 10:30:00 +03:00
string text = withOffset.Format(local);                      // "2026-11-02T10:30:00+03:00"

Template<Stamp> withoutOffset = Template.Of<Stamp>("{When:yyyy-MM-dd HH:mm}");
Stamp utc = withoutOffset.Parse("2026-11-02 10:30");         // When = 2026-11-02 10:30:00 +00:00
```

- With a format, a `DateTimeOffset` without an offset in the text is read as **UTC** (`DateTimeStyles.AssumeUniversal`). Parsing never depends on the machine's time zone.
- `zzz` keeps the offset. `z` writes the short form, such as `+3`.

:::warning

The UTC rule applies only to placeholders **with** a format. A `DateTimeOffset` placeholder without a format is read by `DateTimeOffset.Parse`, which uses the machine's local offset when the text has none. Give `DateTimeOffset` placeholders a format, or make sure the text always carries an offset.

:::

### DateOnly and TimeOnly

`DateOnly` and `TimeOnly` members need `net8.0` or later. They take the same custom and standard formats as `DateTime`.

```csharp
using System;

public record Slot(DateOnly Day, TimeOnly Start);
```

```csharp
using System;
using Masterly.FluentRegex;

Template<Slot> slot = Template.Of<Slot>("{Day:yyyy-MM-dd} {Start:HH:mm}");

Slot parsed = slot.Parse("2026-11-02 09:15");                                         // Day = 2026-11-02, Start = 09:15
string text = slot.Format(new Slot(new DateOnly(2026, 12, 24), new TimeOnly(18, 0))); // "2026-12-24 18:00"
```

- `o` writes `2026-11-02` for a `DateOnly` and `09:15:00.0000000` for a `TimeOnly`. Without a format they are written the same way.
- `r` writes only the date part for a `DateOnly` (`Mon, 02 Nov 2026`) and only the time part for a `TimeOnly`.

### TimeSpan

`TimeSpan` members take the .NET TimeSpan formats: custom formats built from `d`, `h`, `m`, `s` and `f`, and the standard formats `c` (also written `t` or `T`), `g` and `G`.

```csharp
using System;

public record Duration(TimeSpan Elapsed);
```

```csharp
using System;
using Masterly.FluentRegex;

Template<Duration> took = Template.Of<Duration>(@"took {Elapsed:hh\:mm\:ss}");
TimeSpan elapsed = took.Parse("took 01:02:03").Elapsed;                      // 01:02:03
string text = took.Format(new Duration(new TimeSpan(0, 4, 5, 6)));           // "took 04:05:06"

Template<Duration> days = Template.Of<Duration>(@"{Elapsed:d'd 'hh'h'}");
string daysText = days.Format(new Duration(new TimeSpan(3, 4, 0, 0)));      // "3d 04h"
```

| Specifier | Matches |
|-----------|---------|
| `d` | One or more digits |
| `dd` to `dddddddd` | At least that many digits |
| `h`, `m`, `s` | 1 to 2 digits |
| `hh`, `mm`, `ss` | Exactly 2 digits |
| `f` to `fffffff` | Exactly that many fraction digits |
| `'text'`, `"text"`, `\c` | Literal text |
| `c`, `t`, `T` | `[-][d.]hh:mm:ss[.fffffff]`, such as `-1.02:03:04.5000000` |
| `g` | Short general format of the culture, such as `-1:2:03:04.5` |
| `G` | Long general format of the culture, such as `-1:02:03:04.5000000` |

:::warning

TimeSpan formats follow the .NET rules, which are stricter than date formats: every literal must be quoted or escaped. Write `{Elapsed:hh\:mm}`, not `{Elapsed:hh:mm}`. An unescaped `:` throws `ArgumentException` at `Template.Of<T>`.

:::

- Custom TimeSpan formats have no sign, so `Format` throws `ArgumentException` for a negative value under one, such as `TimeSpan.FromMinutes(-90)` with `hh\:mm`, instead of writing it as positive. Use `c`, `g` or `G` when values can be negative: `c` writes `-01:30:00`.
- A custom format without `d` drops whole days. `Format` writes `TimeSpan.FromHours(25)` as `01:00` with `hh\:mm`, without an error. Add a `d` specifier, or use `c`, when durations can reach a day.
- Without a format, `Format` writes the constant format `c` and `Parse` uses `TimeSpan.Parse`, which also accepts short forms such as `1:02:03`.

## Options

`TemplateOptions` is an optional second argument to `Template.Of<T>`. All settings are init-only.

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Culture` | `IFormatProvider` | `CultureInfo.InvariantCulture` | Decimal separator, minus sign, `NaN` and infinity symbols, date and time separators, month and day names, AM/PM designators and the standard date patterns. Used for both parsing and formatting. |
| `IgnoreCase` | `bool` | `false` | Literal template text and `re:` regexes match in any case. Enum names, booleans, month and day names and hex digits always match in any case. |
| `Timeout` | `TimeSpan?` | `null` (no timeout) | The regex match timeout. A match that runs longer throws `RegexMatchTimeoutException`. |

### Culture

The culture changes both what a placeholder matches and how a value is written.

```csharp
using System.Globalization;
using Masterly.FluentRegex;

// Price is the record from "Decimal, double and float"
TemplateOptions german = new TemplateOptions { Culture = new CultureInfo("de-DE") };
Template<Price> price = Template.Of<Price>("{Amount} EUR", german);

decimal amount = price.Parse("12,50 EUR").Amount;      // 12.50
string text = price.Format(new Price(3.75m));          // "3,75 EUR"
bool invariantText = price.TryParse("12.50 EUR", out _); // false: de-DE uses a comma
```

- `/` and `:` in a date format use the culture's separators. With `de-DE`, `{When:dd/MM/yyyy}` writes `02.11.2026`.
- Month and day names come from the culture. With `fr-FR`, `{When:d MMMM yyyy}` writes and reads `14 février 2026`.
- Templates never use the current thread culture. The default is the invariant culture, and a `null` culture also falls back to the invariant culture.

### Unicode digits

`\d` in .NET matches every Unicode decimal digit, and templates convert them. `{Id}` on an `int` reads `"٢٠٢٦"` (Arabic-Indic), `"۲۰۲۶"` (Persian) and `"२०२६"` (Devanagari) as `2026`, and `{When:yyyy-MM-dd}` reads `"٢٠٢٦-١١-٠٢"` as 2026-11-02.

```csharp
using Masterly.FluentRegex;

// Item and Price are the records from earlier sections
Template<Item> item = Template.Of<Item>("{Id}");
int arabicIndic = item.Parse("٢٠٢٦").Id;  // 2026

// The number fragment accepts the Arabic decimal separator next to the culture's own
Template<Price> price = Template.Of<Price>("{Amount}");
decimal amount = price.Parse("١٢٫٥").Amount; // 12.5
```

- The Arabic decimal separator `٫` is accepted by the default number fragment, next to the culture's own separator, and is converted to the culture's separator before the number is parsed.
- `Format` writes numbers with the .NET formatting of the culture, so a round trip turns Unicode digits into ASCII digits.

### Case sensitivity

Literal template text is case-sensitive by default. Set `IgnoreCase` to accept it in any case.

```csharp
using Masterly.FluentRegex;

// Item is the record from "Placeholder syntax"
Template<Item> strict = Template.Of<Item>("id={Id}");
bool upper = strict.TryParse("ID=5", out _);   // false

Template<Item> relaxed = Template.Of<Item>("id={Id}", new TemplateOptions { IgnoreCase = true });
int id = relaxed.Parse("ID=5").Id;             // 5
string text = relaxed.Format(new Item(5));     // "id=5": Format writes the template text as written
```

`IgnoreCase` also applies to `re:` regexes: with it, `{Text:re:[a-z]+}` matches `ABC`, and `Format` accepts `XYZ`. Without it, `Format` rejects `XYZ`.

### Timeout

A timeout is the runtime guard against catastrophic backtracking, for example when a `re:` regex is risky or the input comes from users. See [ReDoS Safety](./redos-safety.md).

```csharp
public record Note(string Text);
```

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

TemplateOptions guarded = new TemplateOptions { Timeout = TimeSpan.FromMilliseconds(50) };
Template<Note> risky = Template.Of<Note>("{Text:re:(a+)+b}", guarded);

TimeSpan timeout = risky.Pattern.Timeout; // 00:00:00.0500000

try
{
    risky.Parse(new string('a', 40) + "!");
}
catch (RegexMatchTimeoutException)
{
    // the match took longer than 50 ms
}
```

- The timeout applies to `Parse`, `TryParse`, `ParseAll` and the `Pattern` property.
- `TimeSpan.Zero`, a negative value or a value over about 24 days throws `ArgumentOutOfRangeException` at `Template.Of<T>`. The one negative value that is accepted is `Regex.InfiniteMatchTimeout`, which means no timeout.
- Without a timeout, `Pattern.Timeout` is `Regex.InfiniteMatchTimeout`.

:::warning

`TryParse` turns only `FormatException` into `false`. A `RegexMatchTimeoutException` is **not** turned into `false`: it propagates from `TryParse` too. Catch it where you handle untrusted input.

:::

## When something goes wrong

Templates fail early and explain themselves. Problems in the template fail when you compile it, problems in the input fail when you parse, and values that would not read back fail when you format.

### Errors when compiling a template

Every problem in the template throws `ArgumentException` from `Template.Of<T>`, with `ParamName` set to `"template"`. A `null` template throws `ArgumentNullException`. Positions are zero-based indexes of the opening brace.

| Problem | Example | Message (shortened) |
|---------|---------|---------------------|
| Empty template | `""` | `The template is empty.` |
| Unclosed placeholder | `"{Id"` | `The placeholder starting at position 0 is not closed. Write '{{' for a literal brace.` |
| Stray closing brace | `"Id}"` | `Unexpected '}' at position 2. Write '}}' for a literal brace.` |
| Invalid name | `"{1d}"`, `"{}"` | `Invalid placeholder name '1d' at position 0. Use a member name such as {Date} or {Date:yyyy-MM-dd}.` |
| Empty format | `"{Id:}"` | `The placeholder at position 0 has an empty format. Remove the ':' or add a format.` |
| Unknown name | `"{Date:yyyy-MM-dd} [{Severity}] {User}: {Message}"` for `LogLine` | `The placeholder {Severity} at position 19 does not match any member of LogLine. Available: {Date}, {Level}, {Message}, {User}.` |
| Required parameter missing | `"{Date} {User}"` for `LogLine` | `The template has no placeholder for the required constructor parameter(s) {Level} of LogLine. Add the placeholder, or give the parameter a default value.` |
| Repeated with another format | `"{Number:D4}/{Number:D2}"` | `The placeholder {Number:D2} at position 12 repeats {Number:D4} with a different format.` |
| Unsupported format | `"{Id:N2}"` on an `int` | `The format 'N2' is not supported for Int32 placeholders. Use D, Dn, X or Xn (for example D4 or X8).` |
| Format on a type without formats | `"{Level:G}"` on an enum | `LogLevel placeholders do not take a format ('G'). Use {Name:re:...} to give an explicit regex.` |
| Empty `re:` | `"{Word:re:}"` for `Pair` | `The 're:' format needs a regex, for example {Sku:re:[A-Z]{3}\d+}.` |
| Invalid `re:` | `"{Word:re:[a-z}"` for `Pair` | `The placeholder regex '[a-z' is invalid: Invalid pattern '[a-z' at offset 4. Unterminated [] set.` |
| Unsupported date or TimeSpan format | `"{When:U}"`, `"{Elapsed:hh:mm}"` | The specifier, and the full list of supported specifiers |
| Type with nothing to bind | `"{Anything}"` for `object` | `... Available: (none: the type has no bindable constructor parameters or settable properties).` |

Because these errors come from the template text, a unit test that only calls `Template.Of<T>` catches all of them.

### Errors when parsing

When the input does not match, `Parse` throws `FormatException`. The message names the position, the placeholder or literal text being read, and what was expected. It is built with [match tracing](./match-tracing.md).

```csharp
using System;

// log is the template from "A first template"
try
{
    log.Parse("2026/11/02 [WARN] ahmad: disk at 91%");
}
catch (FormatException ex)
{
    Console.WriteLine(ex.Message);
}
// prints:
// The input does not match the template "{Date:yyyy-MM-dd} [{Level}] {User}: {Message}": at position 4, while reading placeholder {Date:yyyy-MM-dd}, expected the character '-' (regex -) but found "/11/02 [WARN] ah...".
```

Other examples of the same message:

| Input problem | Message (after the template text) |
|---------------|-----------------------------------|
| Unknown enum name, `[FATAL]` | `at position 12, while reading placeholder {Level}, expected group with options i (regex (?i:Debug\|Error\|Info\|Warn)) but found "FATAL] ahmad: di...".` |
| Wrong literal text, `id=[12]` for `"id=<{Id}>"` | `at position 3, while reading the text "id=<", expected the character '<' (regex <) but found "[12]".` |
| Input too short, `id=12` for `"id={Id};"` | `at position 5, while reading the text ";", expected the character ';' (regex ;) but found the end of the input.` |
| Extra text, `id=12;x` for `"id={Id};"` | `at position 6, while reading the end of the template (the input has extra text after it), expected end of input (regex \z) but found "x".` |

When the text matches but a value cannot be converted, `Parse` also throws `FormatException`, and the message names the member:

```text
Group 'Id' value "99999999999" cannot be converted to Int32: Value was either too large or too small for an Int32.
Group 'Date' value "2026-13-02" cannot be converted to DateTime: The DateTime represented by the string '2026-13-02' is not supported in calendar 'System.Globalization.GregorianCalendar'.
```

`TryParse` returns `false` in all of these cases.

:::note

The parse error message quotes up to 16 characters of the input. If your input can hold secrets or personal data, do not log the message as is.

:::

### Errors when formatting

`Format` checks every value against its own placeholder before writing it, so it never produces text that its placeholder cannot read back.

| Problem | Exception | Example message |
|---------|-----------|-----------------|
| A value does not fit its fragment | `ArgumentException` (`ParamName` is `"value"`) | `The value "12345" cannot be written as {Number:D4}: it does not match (?:-)?\d{4}, so it would not parse back.` |
| A string with a line break | `ArgumentException` | `The value "a` (line break) `b" cannot be written as {Word}: it does not match [^\r\n]*?, so it would not parse back.` |
| A value that fails its `re:` regex | `ArgumentException` | `The value "abc" cannot be written as {Sku:re:[A-Z]{3}\d+}: it does not match (?:[A-Z]{3}\d+), so it would not parse back.` |
| An enum value that is not a single named member | `ArgumentException` | `The LogLevel value '42' is not a single named member, so it cannot be written as one of: Debug, Info, Warn, Error.` |
| A constructor parameter with no public property | `InvalidOperationException` | `Opaque has no readable public property for {secret}, so Format cannot read its value.` |

- A placeholder bound to a constructor parameter that has no matching public property can be parsed but not formatted.
- `Format` checks each value on its own. It does **not** detect values that make neighboring placeholders ambiguous. See [Limitations and pitfalls](#limitations-and-pitfalls).

## Scanning a larger text

`ParseAll` finds every occurrence of the template in a larger text and returns one value per occurrence. Lines that do not match are skipped.

```csharp
using System;

// log is the template from "A first template"
string file = "2026-11-01 [INFO] api: started\nnot a log line\r\n2026-11-02 [ERROR] db: connection lost\n";

foreach (LogLine entry in log.ParseAll(file))
{
    Console.WriteLine($"{entry.Level} {entry.User} [{entry.Message}]");
}
// prints:
// Info api [started]
// Error db [connection lost]
```

- `ParseAll` is not anchored. It finds the template anywhere, also in the middle of a line.
- String placeholders never cross a line break, so it works line by line on both `\n` and `\r\n` text, and the last placeholder stops at the end of its line.
- Evaluation is lazy. A conversion error, such as an overflowing number, throws `FormatException` when that occurrence is enumerated, not when you call `ParseAll`.
- To read a file or a stream of any size without loading it into one string, see [Streaming](./streaming.md).

## Using the template's pattern

`template.Pattern` is an ordinary, unanchored `Pattern` with one named group per placeholder. Use it with every other feature of the library: test it, compose it into a larger pattern, or bind matches with `Extract<T>`.

```csharp
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

// log is the template from "A first template"
Pattern pattern = log.Pattern;

string regex = pattern.ToString();
// (?<Date>\d{4}-\d{2}-\d{2}) \[(?<Level>(?i:Debug|Error|Info|Warn))\] (?<User>[^\r\n]*?): (?<Message>[^\r\n]*)

bool found = pattern.IsMatch("prefix 2026-11-02 [INFO] a: b");            // true
LogLine? extracted = pattern.Extract<LogLine>("prefix 2026-11-02 [INFO] a: b"); // User = "a", Message = "b"

Pattern wholeLine = Pattern.With.StartOfLine.Then(log.Pattern).EndOfLine;
bool lineOnly = wholeLine.IsMatch("prefix 2026-11-02 [INFO] a: b");       // false

Regex compiled = log.Pattern.ToRegex();
string[] groups = compiled.GetGroupNames();                               // 0, Date, Level, User, Message
```

- Group names are the member names, or the `[RegexGroup]` names.
- `Pattern.Extract<T>` binds with the **default** conversions of [Typed Extraction](./typed-extraction.md), not with the template's formats. For a format such as `dd MMM yyyy` or `X6`, use `Parse`, `TryParse` or `ParseAll` on the template instead. For example, `Extract<Color>("#ff8800")` on the `{Rgb:X6}` pattern throws `FormatException`.
- The pattern carries the template's `IgnoreCase` option and timeout.
- Because the pattern is ordinary, you can [explain](./validation-and-debugging.md) it, [draw](./diagrams.md) it, [check it for ReDoS](./redos-safety.md) or [export](./export.md) it.

## Limitations and pitfalls

- **Ambiguous neighbors.** `Format` checks each value against its own placeholder only. A user name that contains `": "` in `"{User}: {Message}"` formats fine but parses back differently: `User = "a: b"`, `Message = "c"` writes `2026-11-02 [Info] a: b: c`, which parses back as `User = "a"`, `Message = "b: c"`. Put a literal between strings that cannot appear inside them, or constrain the string with `re:`.
- **Adjacent placeholders.** `"{A}{B}"` with no literal text between them is matched by regex backtracking. That works for distinct types, such as an enum followed by a string. Two strings next to each other are ambiguous: `"hello"` parses as `A = ""`, `B = "hello"`.
- **Lossy formats.** Formats that drop part of a value lose it on the way back: `F2` rounds, `t` drops the date, `y` drops the day, a custom TimeSpan format drops the sign, and one without `d` drops whole days.
- **`null` strings.** A `null` string writes as an empty string and parses back as `""`.
- **Structs without a constructor.** A `struct` that has only settable properties and no explicit constructor works: `Parse` starts from `default` and sets the properties. Only a struct with neither a public constructor nor a settable property throws `InvalidOperationException` (`Value type '...' needs a public constructor or settable properties.`).
- **Exceptions that `TryParse` lets through.** `TryParse` catches only `FormatException`. A constructor that throws (for example, a record that validates its arguments) surfaces as `TargetInvocationException`, and a struct with nothing to bind as `InvalidOperationException`, from both `Parse` and `TryParse`.
- **Not supported.** `[Flags]` enum combinations, the date specifiers `F` and `g`, the standard date format `U`, and numeric formats other than those listed in [How each member type is matched](#how-each-member-type-is-matched).
- **Literal-only templates.** A template with no placeholders, such as `"hello"`, is valid. `Parse` returns a new `T` whose members keep their defaults (constructor default values and property initializers), and `Format` writes the literal text.
- **Reuse.** `Template.Of<T>` translates formats and builds regexes. Compile each template once and keep it in a field instead of calling `Of` on every request.
