# Typed templates: string interpolation in reverse

A template describes a line of text the way you would write an interpolated string, and turns it
into a typed parser **and** formatter:

```csharp
public enum LogLevel { Debug, Info, Warn, Error }
public record LogLine(DateTime Date, LogLevel Level, string User, string Message);

Template<LogLine> log = Template.Of<LogLine>("{Date:yyyy-MM-dd} [{Level}] {User}: {Message}");

LogLine line = log.Parse("2026-11-02 [WARN] ahmad: disk at 91%");
// LogLine { Date = 2026-11-02, Level = Warn, User = ahmad, Message = disk at 91% }

string text = log.Format(line);
// "2026-11-02 [Warn] ahmad: disk at 91%"
```

`Parse(Format(x))` round-trips: both directions use the same formats and culture.

## API

| Member | What it does |
|---|---|
| `Template.Of<T>(string template, TemplateOptions? options = null)` | Compiles the template. Throws `ArgumentException` for malformed templates, unknown names, unsupported formats or unbound required constructor parameters. |
| `T Parse(string input)` | The input must match the **whole** template. Throws `FormatException` that says where and why it failed. |
| `bool TryParse(string input, out T value)` | Same, without exceptions. |
| `IEnumerable<T> ParseAll(string text)` | Lazily finds every occurrence in a larger text, such as a log file. |
| `string Format(T value)` | Renders a value. Throws `ArgumentException` if a value could not be read back by its placeholder. |
| `Pattern Pattern` | The unanchored pattern with one named group per placeholder, for composition or `Extract<T>`. |

`TemplateOptions` has three settings, all optional:

```csharp
new TemplateOptions
{
    Culture = new CultureInfo("de-DE"),      // numbers, dates, month/day names, AM/PM (default: invariant)
    IgnoreCase = true,                       // literal template text matches in any case
    Timeout = TimeSpan.FromMilliseconds(100) // regex match timeout (default: none)
};
```

## Placeholder syntax

- `{Name}` or `{Name:format}`. Everything else is literal text and is escaped for you.
- `{{` and `}}` are literal braces: `Template.Of<Item>("{{{Id}}}")` parses `"{42}"` and formats `new Item(7)` as `"{7}"`.
- Names bind exactly like `Pattern.Extract<T>`: case-insensitively to the parameters of the public
  constructor with the most parameters, then to settable properties. `[RegexGroup("w")]` renames a
  member, so `record Sized([RegexGroup("w")] int Width)` uses `{w}`.
- An unknown name fails at `Of<T>()` and lists what is available:

  ```text
  The placeholder {Severity} at position 19 does not match any member of LogLine.
  Available: {Date}, {Level}, {Message}, {User}. (Parameter 'template')
  ```

- A required constructor parameter (no default value, not nullable) without a placeholder also fails at `Of<T>()`.
- A placeholder used twice must read the same text twice (`"{Word}-{Word}"` matches `ab-ab`, not `ab-cd`).
- Formats may contain braces, such as `{Sku:re:[A-Z]{3}\d+}`; nesting is counted and `\` escapes the next character.

## How each member type is matched

| Member type | No format | Supported formats |
|---|---|---|
| `string` | `[^\r\n]*?` (lazy); the last placeholder of the template is greedy `[^\r\n]*` | `re:<regex>` |
| `int`, `long`, `short`, `sbyte` | optional culture minus sign, then `\d+` | `D`, `Dn` (exactly *n* digits, for example `D4`), `X`, `Xn` (hex) |
| `uint`, `ulong`, `ushort`, `byte` | `\d+` (no sign) | same as above |
| `decimal` | `-?\d+(?:.\d+)?` with the culture's sign and decimal separator | `G`, `F`, `Fn` (exactly *n* decimals) |
| `double`, `float` | as `decimal`, plus an exponent and the culture's NaN / infinity symbols | `G`, `R`, `F`, `Fn` |
| `bool` | `true` or `false`, any case (formatted in lower case) | none |
| `Guid` | format `D` | `D`, `N`, `B`, `P` |
| enums | the member names, any case, longest first | none |
| `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` | a lazy non-empty run, read by the extraction converter, written with the round-trip format `O` | any .NET custom format, or the standard formats `d D f F g G m o r s t T u y` |
| `TimeSpan` | a lazy non-empty run, read by `TimeSpan.Parse`, written with `c` | custom TimeSpan formats, or `c`, `g`, `G` |
| `char` | one character | none |
| `Nullable<T>` | the inner fragment made optional; empty text reads as `null`, `null` writes nothing | as for `T` |
| anything else (for example `Uri`, `Version`, types registered with `ExtractionConverters.Register`) | a lazy non-empty run, read by the extraction converters | `re:<regex>` |

Any member type accepts `re:<regex>` to replace its fragment; the value is still converted by the type's converter.

```csharp
public record Product(string Sku, int Quantity, decimal Price);

Template<Product> product = Template.Of<Product>(@"{Sku:re:[A-Z]{3}\d+} x{Quantity} @ {Price:F2}");

product.Parse("ABC123 x2 @ 9.50");               // Product { Sku = ABC123, Quantity = 2, Price = 9.50 }
product.Format(new Product("XYZ9", 4, 12m));     // "XYZ9 x4 @ 12.00"
```

### Dates and times

A date format is translated into a regex that matches exactly what `ToString(format, culture)`
produces, and the text is read back with `ParseExact` in the same culture.

Supported custom specifiers: `y`, `yy`, `yyy`, `yyyy` (and more `y`s), `M`, `MM`, `MMM`, `MMMM`
(culture month names, including genitive forms), `d`, `dd`, `ddd`, `dddd` (culture day names), `H`,
`HH`, `h`, `hh`, `m`, `mm`, `s`, `ss`, `f` to `fffffff`, `t`, `tt` (culture AM/PM designators), `z`,
`zz`, `zzz`, `K`, `:` and `/` (culture separators), `'quoted'` and `"quoted"` literals, `\` escapes
and `%`. Other characters are literal. `F` (optional fraction digits) and `g` (era) are rejected with
an `ArgumentException`.

```csharp
public record Invoice(int Number, DateTime Issued);

Template<Invoice> invoice = Template.Of<Invoice>("INV-{Number:D4}/{Issued:dd MMM yyyy}");

invoice.Parse("INV-0042/02 Nov 2026");                         // Invoice { Number = 42, Issued = 2026-11-02 }
invoice.Format(new Invoice(7, new DateTime(2026, 1, 5)));      // "INV-0007/05 Jan 2026"
invoice.Format(new Invoice(12345, DateTime.Today));            // ArgumentException: 12345 has more than 4 digits
```

- `DateTime` is parsed with `DateTimeStyles.RoundtripKind`, so `{When:o}` preserves the kind and every tick.
- `DateTimeOffset` with a format that has no offset is read as **UTC**. Without a format, `{When}` uses
  `DateTimeOffset.Parse`, which reads a value without an offset in the machine's local zone.
  `{When:yyyy-MM-ddTHH:mm:sszzz}` reads `2026-11-02T10:30:00+03:00` with offset +03:00.
- `TimeSpan` custom formats follow .NET rules: literals must be quoted or escaped, so write
  `{Elapsed:hh\:mm\:ss}`; `{Elapsed:hh:mm}` is rejected. Custom TimeSpan formats have no sign, so
  negative durations only round-trip with `c`, `g` or `G`; `Format` throws `ArgumentException` for a
  negative value under a custom format.

## Parsing errors say where and why

`Parse` uses `Pattern.Trace` to report the position, the placeholder being read, and what was expected:

```text
The input does not match the template "{Date:yyyy-MM-dd} [{Level}] {User}: {Message}":
at position 4, while reading placeholder {Date:yyyy-MM-dd}, expected the character '-' (regex -)
but found "/11/02 [WARN] ah...".
```

A matched value that cannot be converted (for example an integer that overflows) also throws
`FormatException`, naming the member. `TryParse` returns `false` for both cases.

## Scanning text

```csharp
string file = "2026-11-01 [INFO] api: started\nnot a log line\n2026-11-02 [ERROR] db: connection lost\n";

foreach (LogLine entry in log.ParseAll(file))
{
    Console.WriteLine($"{entry.Level} {entry.User} {entry.Message}");
}
// Info api started
// Error db connection lost
```

String placeholders never cross a line break, so `ParseAll` works line by line on both `\n` and
`\r\n` text, and the last placeholder stops at the end of its line.

## Cultures and Unicode digits

```csharp
Template<Price> german = Template.Of<Price>("{Amount} EUR", new TemplateOptions { Culture = new CultureInfo("de-DE") });

german.Parse("12,50 EUR");            // Price { Amount = 12.50 }
german.Format(new Price(3.75m));      // "3,75 EUR"
```

`\d` in .NET matches every Unicode decimal digit, and templates convert them: `{Value}` reads
`"٢٠٢٦"` (Arabic-Indic), `"۲۰۲۶"` (Persian) and `"२०२६"` (Devanagari) as `2026`, and
`{When:yyyy-MM-dd}` reads `"٢٠٢٦-١١-٠٢"`. The Arabic decimal separator `٫` is mapped to the
culture's separator when it appears in a matched number (use `re:` to allow it in the fragment).

## Using the pattern directly

`template.Pattern` is an ordinary `Pattern`, not anchored:

```csharp
log.Pattern.ToString();
// (?<Date>\d{4}-\d{2}-\d{2}) \[(?<Level>(?i:Debug|Error|Info|Warn))\] (?<User>[^\r\n]*?): (?<Message>[^\r\n]*)

log.Pattern.Extract<LogLine>("prefix 2026-11-02 [INFO] a: b");   // User = a, Message = b
```

## Limitations

- `Format` checks each value against its own placeholder (a string with a line break, a number wider
  than `D4`, a value that does not match its `re:` regex, or an enum value that is not a single named
  member are rejected). It does **not** detect ambiguity between neighboring placeholders: a user
  name that contains `": "` in `"{User}: {Message}"` formats fine but parses back differently.
- A `null` string formats as an empty string and parses back as `""`.
- Adjacent placeholders with no literal text between them (`"{A}{B}"`) are matched by regex
  backtracking; that works for distinct types (numbers, enums) but two strings next to each other are ambiguous.
- `[Flags]` enum combinations, the date specifiers `F` and `g`, and numeric formats other than the
  ones listed above are not supported.
- A placeholder bound to a constructor parameter with no matching public property can be parsed but
  not formatted (`Format` throws `InvalidOperationException`).
