# Semantic rules, PII redaction and DataAnnotations

New in 3.1. Every output below was produced by running the code against the library.

## Semantic rules

A regular expression can check the *shape* of a value, but not that a card number has a valid
checksum or that `2026-02-30` is not a real date. `Must` attaches such rules to a pattern and returns
an immutable `ConstrainedPattern`:

```csharp
ConstrainedPattern card = CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");

card.IsMatch("4111 1111 1111 1111");               // true
card.IsMatch("4111 1111 1111 1112");               // false: well-formed, wrong checksum
card.Check("4111 1111 1111 1112").ToString();      // Invalid: the rule 'Luhn checksum' rejected the value
card.Check("4111 1111 11x1 1111").ToString();
// Invalid: the value does not match the pattern at position 4: expected (?:[\s\-]?\d{4}){3} (group, exactly 3 times)
```

`Check` validates the **whole** input (like `RegularExpressionAttribute`) and tells you why it failed:

| Member | Meaning |
|---|---|
| `IsValid` | the whole input matched and every rule passed |
| `FailedRule` | description of the first rule that rejected the value |
| `Trace` | a `MatchTrace` (see `Pattern.Trace`) when the pattern itself failed; `Trace.Failure.Position` is where the failing construct starts |
| `ToString()` | one-line summary that never contains the input, so it is safe to log |

`IsValid(input)` is the cheap whole-input check without diagnostics.

### Rules on a named group

```csharp
ConstrainedPattern adult = Pattern.With.Literal("age=")
    .NamedGroup("age", Pattern.With.Digit.Repeat.Times(1, 3))
    .Must("age", age => int.Parse(age) >= 18, "18 or older");

adult.IsMatch("age=21");               // true
adult.Check("age=17").ToString();      // Invalid: the rule '18 or older' rejected the value
```

An unknown group name throws `ArgumentException`. A rule on a group that did not participate in the
match (an optional group) is skipped. Rules without a description are reported as `rule 1`,
`rule 2 on group 'age'`, and so on. `Must` can be chained; each call returns a new instance.

### Searching with rules

`IsMatch`, `Match` and `Matches` search the input. When a candidate fails a rule, the search resumes at
the candidate's index + 1, so a valid value that overlaps a rejected one is still found:

```csharp
Pattern.With.Digit.Repeat.Times(3).Must(v => v[0] != '1').Matches("1234");   // "234"

Pattern.With.Digit.Repeat.Times(16).Must(Validators.Luhn)
    .Matches("ids 4111111111111112 4111111111111111");                      // "4111111111111111"
```

Only the first match at a given position is considered: a shorter alternative at the same index that
would satisfy the rules is not explored.

### Built-in validators

| Validator | Accepts |
|---|---|
| `Validators.Luhn` | a Luhn (mod 10) check digit; spaces and dashes are ignored; at least two digits |
| `Validators.IbanChecksum` | ISO 13616 mod-97 check digits; spaces ignored, letters in either case |
| `Validators.IsoDate` | exactly `yyyy-MM-dd` naming a real calendar date (`2024-02-29` yes, `2026-02-30` no) |
| `Validators.IPv4` | four dotted octets, each 0-255 (leading zeros accepted, as by `CommonPatterns.IPv4`) |

All return `false` for null instead of throwing.

```csharp
CommonPatterns.DateIso().Must(Validators.IsoDate, "real calendar date").Check("2026-02-30").FailedRule;
// "real calendar date"
```

> If your own code has a type named `Validators` and imports `Masterly.FluentRegex`, the name becomes
> ambiguous; qualify it or add `using Validators = My.Namespace.Validators;`.

## PII redaction

`Redactor` finds sensitive values in free text and rewrites them, for logs, support tickets and exports.

```csharp
Redactor redactor = Redactor.For(CommonPatterns.Email())
    .Add(CommonPatterns.CreditCard().Must(Validators.Luhn), RedactionStyle.KeepLast(4))
    .Add(CommonPatterns.SocialSecurityNumber(), RedactionStyle.Label("[SSN]"));

redactor.Mask("Mail bob@example.com, card 4111 1111 1111 1111 (not 4111 1111 1111 1112), ssn 123-45-6789.");
// Mail ***@*******.***, card **** **** **** 1111 (not 4111 1111 1111 1112), ssn [SSN].
```

- **Validation patterns work in text.** Anchors that wrap the whole pattern (`^...$`, `\A...\z`) are
  treated as token boundaries, `(?<!\w)...(?!\w)`, so `CommonPatterns` find values inside sentences but
  not inside longer words (`x123-45-6789` is left alone). Multiline anchors are kept. A top-level
  alternation such as `^cat$|^dog$` and `RightToLeft` patterns are not rewritten, and anchors inside a
  lookahead stay, so `CommonPatterns.Email(PatternStrictness.Strict)` can miss an address in long text.
- **Rules filter hits.** Only the Luhn-valid card above was redacted.
- **Overlaps** between patterns: the earliest start wins, then the longest hit, then the pattern
  registered first. The part of a losing hit that reaches past the winner is masked too.
- **Immutable and thread-safe.** `Add` returns a new `Redactor`; share one instance across threads.

### Styles

| Style | Example |
|---|---|
| `RedactionStyle.Mask` (default) | `123-45-6789` -> `***-**-****` (letters and digits masked, punctuation kept) |
| `RedactionStyle.KeepLast(4)` | `4111 1111 1111 1111` -> `**** **** **** 1111` |
| `RedactionStyle.KeepFirst(4)` | `GB82 WEST 1234 5698 7654 32` -> `GB82 **** **** **** **** **` |
| `RedactionStyle.Label("[EMAIL]")` | value -> `[EMAIL]` |
| `RedactionStyle.Email` | `alice@example.com` -> `a***@example.com` (length of the local part hidden) |
| `RedactionStyle.Custom(m => ...)` | `bob@example.com` -> `<email:example.com>` with `m => $"<email:{m.Value.Split('@')[1]}>"` |

`KeepFirst`/`KeepLast` mask a value completely when it has no more letters and digits than the count,
so a redaction never reveals a whole value. Masking works per code point.

### Reports

`Redact` returns the text plus where each value was, without the values themselves:

```csharp
RedactionReport report = redactor.Redact(text);
report.Text;        // the redacted text
report.Spans;       // Start, Length (in the original), RedactedStart, RedactedLength, Style, Label, PatternIndex
report.ToString();  // Redacted 3 span(s): [5..20) Mask, [27..46) KeepLast(4), [78..89) Label [SSN]
```

## DataAnnotations

`[MatchesPattern]` validates a property against a `Pattern` or `ConstrainedPattern` exposed by a public
static property, field or parameterless method (available on .NET 8 and .NET 10; netstandard2.1 has no
DataAnnotations):

```csharp
public static class Patterns
{
    public static Pattern Sku => Pattern.With.UppercaseLetter.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);

    public static ConstrainedPattern Card => CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");
}

public sealed class Order
{
    [MatchesPattern(typeof(Patterns), nameof(Patterns.Sku))]
    public string? Sku { get; set; }

    [Required]
    [MatchesPattern(typeof(Patterns), nameof(Patterns.Card))]
    public string? Card { get; set; }
}

// new Order { Sku = "ABC-12345", Card = "4111 1111 1111 1112" } produces:
// The field Sku does not match the required pattern.
// The field Card is invalid: Luhn checksum.
```

- The **whole** value must match; null and empty strings are valid (combine with `[Required]`).
- A non-string value is reported as `The field X must be a string to be matched against a pattern.`
- A failed rule uses its description; a custom `ErrorMessage` always wins.
- A missing member or a member of the wrong type throws `InvalidOperationException` naming the type
  and member on first validation. The resolved pattern is cached.
- Trimming and Native AOT: the `Type` parameter is annotated with `[DynamicallyAccessedMembers]`.
