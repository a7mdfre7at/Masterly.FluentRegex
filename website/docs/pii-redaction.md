---
id: pii-redaction
title: "PII Redaction"
description: "Find e-mail addresses, card numbers, IBANs and other sensitive values in free text and mask, label or partly hide them, with a report that is safe to log."
---

A `Redactor` finds sensitive values in free text and rewrites them. You give it the patterns of the values to hide, such as e-mail addresses, card numbers or national IDs, and a style for each one: mask it, replace it with a label, or keep a few characters visible. Use it before text leaves your control: log messages, support tickets, analytics exports, error reports and prompts sent to an external service.

The redactor works with the patterns you already have. Patterns written to validate a whole value, such as the ones in [Common Patterns](./common-patterns.md), find the same values inside a sentence, with a few exceptions listed in [Anchors that are kept](#anchors-that-are-kept). Rules added with `Must` (see [Semantic Rules](./semantic-rules.md)) filter the hits, so a card pattern with a Luhn rule only redacts real card numbers.

| Type | What it is |
|------|------------|
| `Redactor` | An immutable list of patterns, each with a style. `Mask` returns the redacted text; `Redact` also reports where each value was. |
| `RedactionStyle` | How a value is rewritten: `Mask`, `KeepLast(n)`, `KeepFirst(n)`, `Label(text)`, `Email` or `Custom(...)`. |
| `RedactionReport` | The redacted text and one `RedactedSpan` per redacted value. It never contains the original values. |
| `RedactedSpan` | Where one value was, how long it was, where its replacement is and which pattern and style produced it. |

All four types live in the `Masterly.FluentRegex` namespace and work on every target the package supports: .NET Standard 2.1, .NET 8 and .NET 10. Every output on this page comes from running the code.

## Redact text in one call

Build a redactor from the patterns of the values you want to hide, then call `Mask`:

```csharp title="Redact a message"
using System;
using Masterly.FluentRegex;

Redactor redactor = Redactor.For(CommonPatterns.Email())
    .Add(CommonPatterns.CreditCard().Must(Validators.Luhn), RedactionStyle.KeepLast(4))
    .Add(CommonPatterns.SocialSecurityNumber(), RedactionStyle.Label("[SSN]"));

string message = "Mail bob@example.com, card 4111 1111 1111 1111 (not 4111 1111 1111 1112), ssn 123-45-6789.";

Console.WriteLine(redactor.Mask(message));
// prints: Mail ***@*******.***, card **** **** **** 1111 (not 4111 1111 1111 1112), ssn [SSN].
```

- `Redactor.For(...)` registers patterns with the default style, `RedactionStyle.Mask`: every letter and digit becomes `*`, and punctuation stays.
- `Add(pattern, style)` registers one more pattern with its own style. Here the card keeps its last four digits and the SSN is replaced by a label.
- `CommonPatterns.Email()` is written as `^...$` to validate a whole value. The redactor still finds the address inside the sentence. See [How patterns are found in text](#how-patterns-are-found-in-text).
- The second card number has the right shape but a wrong check digit, so the Luhn rule rejects it and it is left alone.

## Build a redactor

A redactor is a list of rules. Each rule is a pattern plus a style. You start with `Redactor.For` and add rules with `Add`.

| Member | What it does |
|--------|--------------|
| `Redactor.For(params Pattern[] patterns)` | Creates a redactor that masks every value found by the patterns with `RedactionStyle.Mask`. `Redactor.For()` with no arguments creates an empty redactor. |
| `Add(Pattern pattern, RedactionStyle? style = null)` | Returns a new redactor that also redacts the values found by `pattern`. A `null` style means `RedactionStyle.Mask`. |
| `Add(ConstrainedPattern pattern, RedactionStyle? style = null)` | Returns a new redactor that also redacts the values found by `pattern` that pass all of its rules. |
| `Mask(string text)` | Returns the redacted text. Same as `Redact(text).Text`. |
| `Redact(string text)` | Returns a `RedactionReport` with the redacted text and the redacted spans. |
| `PatternCount` | The number of registered patterns. |

### Start with `Redactor.For`

`For` takes any number of patterns and masks all of them:

```csharp title="Mask several kinds of values"
Redactor masker = Redactor.For(
    CommonPatterns.Email(),
    CommonPatterns.SocialSecurityNumber(),
    CommonPatterns.PhoneNumberE164());

int count = masker.PatternCount;                                     // 3
string masked = masker.Mask("bob@example.com, 123-45-6789, +14155552671");
// ***@*******.***, ***-**-****, +***********

Redactor empty = Redactor.For();
int none = empty.PatternCount;                                       // 0
string unchanged = empty.Mask("bob@example.com");                    // bob@example.com
```

- An empty redactor returns the text unchanged. It is a convenient starting point when every rule needs its own style: `Redactor.For().Add(...).Add(...)`.
- `For` accepts only `Pattern`. To register a `ConstrainedPattern`, use `Add`.

### Add a pattern with a style

`Add` registers one more pattern. Pass a style as the second argument, or leave it out to mask the value:

```csharp title="Add patterns with and without a style"
Redactor redactor = Redactor.For()
    .Add(CommonPatterns.Email(), RedactionStyle.Label("[EMAIL]"))
    .Add(CommonPatterns.SocialSecurityNumber());

string masked = redactor.Mask("bob@example.com, 123-45-6789");   // [EMAIL], ***-**-****
```

The order of the `Add` calls matters only as the last tie-breaker when two patterns find the same value. See [When matches overlap](#when-matches-overlap).

### Filter hits with rules

A regular expression checks the shape of a value. It cannot check a card's check digit or an IBAN's checksum. Add a `ConstrainedPattern` (a pattern with `Must` rules) and the redactor redacts only the hits that pass every rule:

```csharp title="Redact only values that pass their rules"
ConstrainedPattern card = Pattern.With.Digit.Repeat.Times(16).Must(Validators.Luhn, "Luhn checksum");
Redactor redactor = Redactor.For().Add(card, RedactionStyle.Label("[CARD]"));

string cards = redactor.Mask("ids 4111111111111112 4111111111111111");
// ids 4111111111111112 [CARD]

ConstrainedPattern iban = CommonPatterns.Iban().Must(Validators.IbanChecksum, "IBAN checksum");
Redactor ibans = Redactor.For().Add(iban, RedactionStyle.Label("[IBAN]"));

string payments = ibans.Mask("pay GB82WEST12345698765432, not GB00WEST12345698765432");
// pay [IBAN], not GB00WEST12345698765432

ConstrainedPattern evenPair = Pattern.FromRegex(@"^(?<n>\d{2})$")
    .Must("n", value => "02468".Contains(value[1]), "even");
Redactor evens = Redactor.For().Add(evenPair, RedactionStyle.Label("[N]"));

string pairs = evens.Mask("12 13 14");   // [N] 13 [N]
```

- Rules on a named group work too, as the last example shows. The anchors of `^(?<n>\d{2})$` become token boundaries, so the pattern finds each two-digit token in the sentence.
- When a candidate fails a rule, the search resumes one character after the candidate's start, so a valid value that overlaps a rejected one is still found. [Semantic Rules](./semantic-rules.md) covers rules and the built-in `Validators` (`Luhn`, `IbanChecksum`, `IsoDate`, `IPv4`).

:::tip

Pair a shape pattern with a checksum rule whenever one exists. It keeps the redactor from hiding order numbers, timestamps and other harmless digit runs that happen to have the right length.

:::

### Share one redactor

A redactor is immutable. `Add` never changes the redactor you call it on; it returns a new one:

```csharp title="Add returns a new redactor"
Redactor original = Redactor.For(CommonPatterns.Email());
Redactor extended = original.Add(CommonPatterns.SocialSecurityNumber());

int before = original.PatternCount;                    // 1
int after = extended.PatternCount;                     // 2
string fromOriginal = original.Mask("123-45-6789");    // 123-45-6789
string fromExtended = extended.Mask("123-45-6789");    // ***-**-****
```

Because it is immutable, a redactor is also thread-safe. Build it once and share it, for example as a singleton in dependency injection. The DI types in the second sample come from the `Microsoft.Extensions.DependencyInjection` package.

```csharp title="Register one redactor for the whole application"
using Masterly.FluentRegex;

public static class PiiRedactors
{
    public static Redactor Default { get; } = Redactor.For(CommonPatterns.Email())
        .Add(CommonPatterns.CreditCard().Must(Validators.Luhn), RedactionStyle.KeepLast(4))
        .Add(CommonPatterns.SocialSecurityNumber(), RedactionStyle.Label("[SSN]"));
}

public sealed class TicketSanitizer
{
    private readonly Redactor _redactor;

    public TicketSanitizer(Redactor redactor)
    {
        _redactor = redactor;
    }

    public string Sanitize(string body) => _redactor.Mask(body);
}
```

```csharp title="Resolve and use it"
using Microsoft.Extensions.DependencyInjection;

ServiceCollection services = new ServiceCollection();
services.AddSingleton(PiiRedactors.Default);
services.AddSingleton<TicketSanitizer>();

using ServiceProvider provider = services.BuildServiceProvider();
TicketSanitizer sanitizer = provider.GetRequiredService<TicketSanitizer>();

string safe = sanitizer.Sanitize("Customer bob@example.com paid with 5555 5555 5555 4444.");
// Customer ***@*******.*** paid with **** **** **** 4444.
```

- `Add` converts each pattern into its search form once. The `Regex` behind it is created on the first `Mask` or `Redact` call and cached; every later call reuses it. Building a new redactor per call throws that work away.
- When a pattern needs no rewriting (it has no wrapping anchors), the redactor uses your `Pattern` instance as it is and shares that pattern's cached `Regex`.
- Concurrent `Mask` calls on one instance are safe and return the same result.

## Choose a style

A style decides what replaces a value. Pick one per pattern:

| Style | Example | Output |
|-------|---------|--------|
| `RedactionStyle.Mask` (default) | `123-45-6789` | `***-**-****` |
| `RedactionStyle.KeepLast(4)` | `4111-1111-1111-1111` | `****-****-****-1111` |
| `RedactionStyle.KeepFirst(4)` | `GB82 WEST 1234 5698 7654 32` | `GB82 **** **** **** **** **` |
| `RedactionStyle.Label("[EMAIL]")` | `alice@example.com` | `[EMAIL]` |
| `RedactionStyle.Email` | `alice@example.com` | `a***@example.com` |
| `RedactionStyle.Custom(match => ...)` | `bob@example.com` | Whatever your function returns, such as `<email:example.com>` |

Styles are immutable and thread-safe. `Mask` and `Email` are shared static instances; `KeepLast`, `KeepFirst`, `Label` and `Custom` are factory methods that return a new instance on every call. `RedactionStyle` has no public or protected constructor, so you cannot derive your own style from it: use `Custom` for anything the built-in styles do not cover.

### Mask every letter and digit

`RedactionStyle.Mask` replaces every letter and digit with `*` and keeps everything else: spaces, dashes, dots, `@`, `+` and other punctuation.

```csharp title="Mask"
Redactor ssn = Redactor.For(CommonPatterns.SocialSecurityNumber());
string maskedSsn = ssn.Mask("SSN 123-45-6789.");          // SSN ***-**-****.

Redactor code = Redactor.For(Pattern.With.Literal("AB-12 cd"));
string maskedCode = code.Mask("id AB-12 cd!");             // id **-** **!
```

- Letters are masked as well as digits.
- The output keeps the length and the layout of the value. That keeps logs readable, but it also shows the shape and length of what was hidden. Use `Label` when even the length must not leak.

### Keep the first or last characters

`KeepLast(count)` masks every letter and digit except the last `count` of them. `KeepFirst(count)` keeps the first `count` instead. Separators are never counted and are always kept.

```csharp title="KeepLast and KeepFirst"
Redactor cards = Redactor.For()
    .Add(CommonPatterns.CreditCard().Must(Validators.Luhn), RedactionStyle.KeepLast(4));
string card = cards.Mask("card 4111-1111-1111-1111");                // card ****-****-****-1111

Redactor phones = Redactor.For().Add(CommonPatterns.PhoneNumberE164(), RedactionStyle.KeepLast(2));
string phone = phones.Mask("call +447911123456");                    // call +**********56

Redactor ibans = Redactor.For().Add(CommonPatterns.Iban(), RedactionStyle.KeepFirst(4));
string iban = ibans.Mask("IBAN GB82 WEST 1234 5698 7654 32");        // IBAN GB82 **** **** **** **** **

Redactor ssn = Redactor.For().Add(CommonPatterns.SocialSecurityNumber(), RedactionStyle.KeepFirst(3));
string area = ssn.Mask("ssn 123-45-6789");                           // ssn 123-**-****

Redactor pins = Redactor.For().Add(Pattern.With.Digit.Repeat.Times(4), RedactionStyle.KeepLast(4));
string pin = pins.Mask("PIN 1234");                                  // PIN ****
```

- A value with no more letters and digits than `count` is masked completely, as the PIN shows. A redaction never reveals a whole value.
- `KeepLast(0)` and `KeepFirst(0)` mask like `Mask`, but the report names the style `KeepLast(0)` or `KeepFirst(0)`.
- A negative `count` throws `ArgumentOutOfRangeException` when you create the style.

### Replace with a label

`Label(text)` replaces the whole value with fixed text:

```csharp title="Label"
Redactor redactor = Redactor.For().Add(CommonPatterns.Email(), RedactionStyle.Label("[EMAIL]"));

string masked = redactor.Mask("from alice@example.com to bob@example.org");
// from [EMAIL] to [EMAIL]
```

- The label hides the length and the shape of the value. It is the safest style.
- The label is used as-is. It is not a substitution pattern, so `$1` or `${name}` in it are not expanded. Use `Custom` to build the replacement from the match.
- A `null` label throws `ArgumentNullException`; an empty label throws `ArgumentException`.

### Keep the e-mail domain

`RedactionStyle.Email` keeps the first character of the address and everything from the last `@` onwards. The rest of the local part becomes `***`, whatever its length:

```csharp title="Email"
Redactor redactor = Redactor.For().Add(CommonPatterns.Email(), RedactionStyle.Email);
string addresses = redactor.Mask("from alice@example.com and b@x.io");
// from a***@example.com and b***@x.io

Redactor tokens = Redactor.For().Add(Pattern.With.Literal("secret42"), RedactionStyle.Email);
string token = tokens.Mask("token secret42");                        // token ********

Redactor handles = Redactor.For().Add(Pattern.With.Literal("@").Word.Repeat.OneOrMore, RedactionStyle.Email);
string handle = handles.Mask("ping @alice now");                     // ping @***** now
```

- `alice` and `b` both become `***`, so the output does not reveal how long the local part was.
- A value without an `@`, or one that starts with `@`, is masked like `Mask`.
- Use it when support staff need the domain to route or group tickets, but not the person.

### Compute the replacement

`Custom(replacement)` calls your function with the .NET `Match` and uses the string it returns. Use it to keep a named group, to show the length, or to keep a parameter name while hiding its value:

```csharp title="Custom"
Redactor domains = Redactor.For()
    .Add(CommonPatterns.Email(), RedactionStyle.Custom(match => $"<email:{match.Value.Split('@')[1]}>"));
string domain = domains.Mask("mail bob@example.com");   // mail <email:example.com>

Pattern passwordParameter = Pattern.With
    .Literal("password=")
    .NamedGroup("value", Pattern.With.NegatedSet(Pattern.With.Literal("&")).Repeat.OneOrMore);

Redactor queries = Redactor.For()
    .Add(passwordParameter, RedactionStyle.Custom(match => $"password=[{match.Groups["value"].Length} chars]"));

string query = queries.Mask("POST /login?user=ann&password=s3cr3t!&remember=1");
// POST /login?user=ann&password=[7 chars]&remember=1
```

- `match.Groups` holds the groups of your pattern, including named groups. The redactor's search form of the pattern keeps every group.
- The function must not return `null`. If it does, `Mask` and `Redact` throw `InvalidOperationException` ("The custom redaction style returned null.").
- Passing a `null` function to `Custom` throws `ArgumentNullException`.
- The function runs once per redacted value, possibly from many threads at once when you share the redactor. Keep it pure.

:::warning

A custom style decides what is printed, so it can leak. Do not return the match itself, a group that holds the secret, or a reversible encoding of it. Hashing a value is not redaction either when the input space is small, such as phone numbers or SSNs: the hash can be reversed by trying every value.

:::

### Unicode values

Masking works per Unicode code point and uses `char.IsLetterOrDigit`, so accented and non-Latin letters are masked like ASCII letters, and a character outside the Basic Multilingual Plane becomes a single `*`:

```csharp title="Unicode letters"
Redactor names = Redactor.For().Add(Pattern.With.Literal("Zoë-Ångström"), RedactionStyle.KeepFirst(1));
string name = names.Mask("name Zoë-Ångström");               // name Z**-********

string astral = char.ConvertFromUtf32(0x20000);              // one CJK letter, two UTF-16 chars
Redactor astralRedactor = Redactor.For(Pattern.With.Literal($"a{astral}b"));
string masked = astralRedactor.Mask($"x a{astral}b");       // x ***

string decomposedName = "Zoé";                         // "Zoé" written as e + combining acute accent
string marks = Redactor.For(Pattern.With.Literal(decomposedName)).Mask(decomposedName);
int markedLength = marks.Length;                             // 4
bool accentKept = marks == "***́";                      // true
```

- The `Email` style also keeps a whole first code point, never half of a surrogate pair.
- Combining marks (U+0300 to U+036F and similar) are not letters, so they are kept like punctuation. A decomposed `é` becomes `*` followed by the accent. Normalize the text to form C first (`text.Normalize()`) if that matters to you.

### Style names

Every style has a `Name`, which is what the report shows. `ToString()` returns the same text.

| Member | `Mask` | `Email` | `KeepLast(4)` | `KeepFirst(2)` | `Label("[X]")` | `Custom(...)` |
|--------|--------|---------|---------------|----------------|----------------|---------------|
| `Name` / `ToString()` | `Mask` | `Email` | `KeepLast(4)` | `KeepFirst(2)` | `Label` | `Custom` |
| `LabelText` | `null` | `null` | `null` | `null` | `[X]` | `null` |

## Read the report

`Mask` returns only the text. `Redact` returns a `RedactionReport` that also says where every value was, so you can count redactions, raise metrics, highlight the redacted parts in a UI or prove in an audit log that something was removed.

```csharp title="Redact with a report"
Redactor support = Redactor.For()
    .Add(CommonPatterns.Email(), RedactionStyle.Email)
    .Add(CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum"), RedactionStyle.KeepLast(4))
    .Add(CommonPatterns.Iban().Must(Validators.IbanChecksum, "IBAN checksum"), RedactionStyle.KeepFirst(4))
    .Add(CommonPatterns.SocialSecurityNumber(), RedactionStyle.Label("[SSN]"))
    .Add(CommonPatterns.PhoneNumberE164(), RedactionStyle.KeepLast(2));

string ticket = "From alice@example.com: refund to GB82 WEST 1234 5698 7654 32, card 5555-5555-5555-4444, call +447911123456.";

RedactionReport report = support.Redact(ticket);

Console.WriteLine(report.Text);
// prints: From a***@example.com: refund to GB82 **** **** **** **** **, card ****-****-****-4444, call +**********56.
Console.WriteLine(report.HasRedactions);   // prints: True
Console.WriteLine(report);
// prints: Redacted 4 span(s): [5..22) Email, [34..61) KeepFirst(4), [68..87) KeepLast(4), [94..107) KeepLast(2)

foreach (RedactedSpan span in report.Spans)
{
    string replacement = report.Text.Substring(span.RedactedStart, span.RedactedLength);
    Console.WriteLine($"{span.Start,3} {span.Length,2} -> {span.RedactedStart,3} {span.RedactedLength,2}  pattern {span.PatternIndex}  {span.Style,-12} {replacement}");
}
// prints:
//   5 17 ->   5 16  pattern 0  Email        a***@example.com
//  34 27 ->  33 27  pattern 2  KeepFirst(4) GB82 **** **** **** **** **
//  68 19 ->  67 19  pattern 1  KeepLast(4)  ****-****-****-4444
//  94 13 ->  93 13  pattern 4  KeepLast(2)  +**********56
```

- `Start` and `Length` describe the value in the **original** text. `RedactedStart` and `RedactedLength` describe its replacement in `report.Text`.
- The two differ as soon as a replacement has a different length. Here `alice@example.com` (17 characters) became `a***@example.com` (16), so every later span starts one character earlier in the redacted text.
- `Spans` is ordered by position, not by pattern. `PatternIndex` is the zero-based order in which the pattern was registered: 2 is the IBAN rule, the third `Add`.
- The SSN pattern found nothing, so it has no span.

### Report members

| `RedactionReport` member | Meaning |
|--------------------------|---------|
| `Text` | The redacted text. |
| `Spans` | One `RedactedSpan` per redacted value, ordered by position. Empty when nothing was redacted. |
| `HasRedactions` | `true` when at least one value was redacted. |
| `ToString()` | A summary such as `Redacted 2 span(s): [4..15) Mask, [22..37) Label [EMAIL]`, without any original value. |

| `RedactedSpan` member | Meaning |
|-----------------------|---------|
| `Start` | Start of the value in the original text. |
| `Length` | Length of the value in the original text. |
| `RedactedStart` | Start of the replacement in `RedactionReport.Text`. |
| `RedactedLength` | Length of the replacement in `RedactionReport.Text`. |
| `Style` | The `Name` of the style that produced the replacement, such as `Mask` or `KeepLast(4)`. |
| `Label` | The label text for a `Label` style; otherwise `null`. |
| `PatternIndex` | The zero-based registration index of the pattern that found the value. |
| `ToString()` | The half-open range in the **original** text (`Start` to `Start + Length`), the style name and, for a label, the label text, such as `[5..20) Mask` or `[22..37) Label [EMAIL]`. |

Neither type has a public constructor. You get them from `Redact`.

### When nothing is found

```csharp title="Labels in the report and an empty report"
Redactor redactor = Redactor.For(CommonPatterns.SocialSecurityNumber())
    .Add(CommonPatterns.Email(), RedactionStyle.Label("[EMAIL]"));

RedactionReport report = redactor.Redact("ssn 123-45-6789, mail bob@example.com");
string text = report.Text;           // ssn ***-**-****, mail [EMAIL]
string summary = report.ToString();  // Redacted 2 span(s): [4..15) Mask, [22..37) Label [EMAIL]
string? label = report.Spans[1].Label; // [EMAIL]

RedactionReport clean = redactor.Redact("nothing to see");
string same = clean.Text;              // nothing to see
bool any = clean.HasRedactions;        // false
string cleanSummary = clean.ToString(); // Redacted 0 span(s)
```

:::note

The report is safe to log. It keeps positions, lengths, style names and labels, never the original values. Two things still come from you: the label text you chose (it appears in `ToString()`), and the replacement text in `Text`, which for `KeepLast`, `KeepFirst`, `Email` and `Custom` deliberately shows part of the value. Positions and lengths can also hint at what was there, for example a 19-character span in a payment log.

:::

## How patterns are found in text

`CommonPatterns` and most validation patterns are anchored: `^\d{3}-\d{2}-\d{4}$` only matches when the whole input is an SSN. Searching a sentence with that pattern as written would find nothing. The redactor therefore rewrites the pattern before it searches.

### Wrapping anchors become token boundaries

When an anchor wraps the whole pattern, the redactor replaces it with a word-boundary lookaround:

| Anchor in your pattern | Becomes | Meaning |
|------------------------|---------|---------|
| Leading `^` (without `RegexOptions.Multiline`) or `\A` | `(?<!\w)` | The value must not follow a word character. |
| Trailing `$` (without `RegexOptions.Multiline`), `\Z` or `\z` | `(?!\w)` | The value must not be followed by a word character. |

So a validation pattern finds whole tokens, never pieces of longer words:

```csharp title="Validation patterns find whole tokens"
Redactor ssn = Redactor.For(CommonPatterns.SocialSecurityNumber());
string ids = ssn.Mask("ids 123-45-6789 and 987-65-4321, x123-45-6789, ssn_123-45-6789, 123-45-67890");
// ids ***-**-**** and ***-**-****, x123-45-6789, ssn_123-45-6789, 123-45-67890

Redactor email = Redactor.For(CommonPatterns.Email());
string html = email.Mask("<a href=\"mailto:bob@example.com\">bob@example.com</a>");
// <a href="mailto:***@*******.***">***@*******.***</a>
```

- `x123-45-6789` and `123-45-67890` are left alone: a letter or a digit touches the value.
- `ssn_123-45-6789` is left alone too, because `_` is a word character. Keep this in mind for identifiers such as `user_bob@example.com` or `card_4111...`.
- Punctuation such as `:`, `"`, `>` or a space is not a word character, so `mailto:bob@example.com` is found.
- Only the anchors are rewritten. The rest of the pattern, its options (such as `IgnoreCase`), its groups and its timeout are kept.

### Unanchored patterns match anywhere

A pattern without anchors is searched as written, so it can match inside a longer token. Anchor it, or add word boundaries, when the value must stand on its own:

```csharp title="Anchored versus unanchored"
Redactor loose = Redactor.For(Pattern.With.Digit.Repeat.Times(4));
string partial = loose.Mask("PIN 1234, order 123456");          // PIN ****, order ****56

Redactor whole = Redactor.For(Pattern.With.StartOfLine.Digit.Repeat.Times(4).EndOfLine);
string tokens = whole.Mask("PIN 1234, order 123456");           // PIN ****, order 123456

Redactor bounded = Redactor.For(Pattern.FromRegex(@"\b\d{4}\b"));
string bounds = bounded.Mask("PIN 1234, order 123456");         // PIN ****, order 123456
```

The unanchored pattern half-masked the order number, which is worse than both options: the order number is damaged and the log no longer shows a clean value.

### Anchors that are kept

Only anchors that wrap the **entire** pattern are rewritten. Everything else keeps its normal meaning:

```csharp title="Anchors the redactor does not rewrite"
using System.Text.RegularExpressions;

Redactor multiline = Redactor.For(Pattern.FromRegex("^secret$", RegexOptions.Multiline));
string lines = multiline.Mask("a\nsecret\nb secret");          // "a\n******\nb secret"

Redactor alternation = Redactor.For(Pattern.FromRegex("^cat$|^dog$"));
string pets = alternation.Mask("my cat and dog");                // my cat and dog

Redactor grouped = Redactor.For(Pattern.FromRegex("^(?:cat|dog)$"));
string groupedPets = grouped.Mask("my cat and dog");             // my *** and ***

Redactor rightToLeft = Redactor.For(Pattern.FromRegex(@"^\d{3}$", RegexOptions.RightToLeft));
string inText = rightToLeft.Mask("id 123");                      // id 123
string alone = rightToLeft.Mask("123");                          // ***

Redactor leadingOnly = Redactor.For(Pattern.FromRegex(@"^\d{3}"));
string leading = leadingOnly.Mask("a 123 4567");                 // a *** ***7
```

- **Multiline anchors** (`^` and `$` with `RegexOptions.Multiline`) keep their line meaning. The second `secret` is not alone on its line, so it stays.
- **A top-level alternation** such as `^cat$|^dog$` is not wrapped by one pair of anchors, so nothing is rewritten and it only matches a whole input. Group the alternatives, `^(?:cat|dog)$`, and it works in text.
- **Right-to-left patterns** are never rewritten.
- **One anchor alone** is still rewritten. `^\d{3}` became `(?<!\w)\d{3}`, which found `123` and the start of `4567`.
- **Anchors inside the pattern**, for example in a lookahead, are kept. `CommonPatterns.Email(PatternStrictness.Strict)` starts with `(?=.{1,254}$)`, which requires the end of the input within 254 characters. In a longer text the address is not found, so prefer `CommonPatterns.Email()` for redaction.

:::warning

`CommonPatterns.PhoneNumber()` does not match ordinary US numbers in version 3.1.0, even as a whole value: `CommonPatterns.PhoneNumber().IsMatch("555-123-4567")` and `IsMatch("(555) 123-4567")` both return `false`, so a redactor built from it redacts nothing. Use `CommonPatterns.PhoneNumberE164()` for `+` numbers, or write a phone pattern for your own formats and test it before you rely on it.

:::

## When matches overlap

Each pattern searches the whole text on its own. When hits of different patterns overlap, the redactor keeps one and drops the others, using these rules in order:

1. The hit that **starts earliest** wins.
2. For hits with the same start, the **longest** hit wins.
3. For hits with the same start and length, the pattern **registered first** wins.

```csharp title="Overlap rules"
RedactionReport earliest = Redactor.For(Pattern.With.Literal("bcd"), Pattern.With.Literal("abc")).Redact("abcd");
string first = earliest.Text;                        // ***d
int firstIndex = earliest.Spans[0].PatternIndex;     // 1: "abc" starts earlier

RedactionReport longest = Redactor.For(Pattern.With.Literal("ab"), Pattern.With.Literal("abc")).Redact("abcx");
string longer = longest.Text;                        // ***x
int longerIndex = longest.Spans[0].PatternIndex;     // 1: same start, "abc" is longer

Redactor tie = Redactor.For()
    .Add(Pattern.With.Literal("ab"), RedactionStyle.Label("[A]"))
    .Add(Pattern.With.Literal("ab"), RedactionStyle.Label("[B]"));
string tied = tie.Mask("ab");                        // [A]: registered first
```

In real text, the longest-hit rule lets a specific pattern beat a generic one, whatever the registration order:

```csharp title="A specific pattern beats a generic one"
Redactor redactor = Redactor.For()
    .Add(Pattern.With.Digit.Repeat.AtLeast(6), RedactionStyle.Label("[NUMBER]"))
    .Add(CommonPatterns.Email(), RedactionStyle.Label("[EMAIL]"));

string users = redactor.Mask("user 12345678@example.com called from 4155552671");
// user [EMAIL] called from [NUMBER]

Redactor accounts = Redactor.For()
    .Add(Pattern.With.Digit.Repeat.Times(8), RedactionStyle.Label("[NUMBER]"))
    .Add(Pattern.With.Literal("acct-").Digit.Repeat.Times(8), RedactionStyle.Label("[ACCOUNT]"));

string ledger = accounts.Mask("acct-12345678 and 87654321");
// [ACCOUNT] and [NUMBER]
```

- The address `12345678@example.com` and the number `12345678` start at the same position. The address is longer, so it wins and is labelled as a whole.
- Matches of one pattern never overlap each other. A pattern keeps all of its own hits except the ones another pattern wins.
- Zero-length matches are ignored. `Pattern.With.Digit.Repeat.ZeroOrMore` on `a1b` redacts only the `1`: `a*b`, `Redacted 1 span(s): [1..2) Mask`.

Rules run before overlaps are resolved. A candidate that fails a pattern's rules never competes, so another pattern can still redact the same text. Here the second number fails the Luhn rule of the card pattern, and the generic number pattern labels it instead:

```csharp title="A rejected candidate does not block other patterns"
Redactor mixed = Redactor.For()
    .Add(Pattern.With.Digit.Repeat.Times(16).Must(Validators.Luhn), RedactionStyle.Label("[CARD]"))
    .Add(Pattern.With.Digit.Repeat.AtLeast(10), RedactionStyle.Label("[NUM]"));

string numbers = mixed.Mask("4111111111111111 4111111111111112");   // [CARD] [NUM]
```

:::warning

A dropped hit is dropped completely. It is not redacted for the part that the winning hit does not cover. In the first example, `bcd` lost to `abc`, and the `d` stayed visible. When a generic pattern can start inside or before a sensitive value, make sure the sensitive pattern covers the whole value from an earlier or equal start, or check `report.Text` in a test.

:::

## Performance and safety

### Cost

- `Redact` runs every pattern over the whole text, then sorts and merges the hits. The cost grows with the length of the text and the number of patterns. Register only the patterns you need.
- `Mask` and `Redact` take a `string`, so the whole text must be in memory. For a large log file, redact it line by line (a value that spans a line break is then not found). For matching over a `TextReader`, see [Streaming](./streaming.md).
- Rules in a `ConstrainedPattern` run once per candidate. Keep them cheap.

### Timeouts and ReDoS

The search form keeps the pattern's timeout. A pattern that runs too long throws `RegexMatchTimeoutException` from `Mask` or `Redact`:

```csharp title="Timeouts are kept"
Pattern risky = Pattern.FromRegex(@"(a|aa)+c").WithTimeout(TimeSpan.FromMilliseconds(50));
Redactor redactor = Redactor.For(risky);

try
{
    redactor.Mask(new string('a', 60));
}
catch (RegexMatchTimeoutException)
{
    Console.WriteLine("timed out");   // prints: timed out
}
```

Redaction usually runs on text you do not control, so give hand-written patterns a timeout and check them for catastrophic backtracking. See [ReDoS Safety](./redos-safety.md).

### The non-backtracking engine

On .NET 8 and .NET 10, `UseNonBacktracking()` gives linear-time matching, but that engine does not support lookarounds. The `(?<!\w)` and `(?!\w)` boundaries that replace wrapping anchors are lookarounds, so `Add` (and `For`) throws `NotSupportedException` for an anchored non-backtracking pattern. An unanchored one works:

```csharp title="Non-backtracking patterns must be unanchored"
try
{
    Redactor anchored = Redactor.For(CommonPatterns.Email().UseNonBacktracking());
}
catch (NotSupportedException)
{
    Console.WriteLine("not supported");   // prints: not supported
}

Pattern ssnInText = Pattern.With
    .Digit.Repeat.Times(3).Literal("-").Digit.Repeat.Times(2).Literal("-").Digit.Repeat.Times(4)
    .UseNonBacktracking();
string masked = Redactor.For(ssnInText).Mask("ssn 123-45-6789");   // ssn ***-**-****
```

For an anchored pattern the exception is thrown when the redactor is built, not on the first call to `Mask`, so you find out at startup. An unanchored pattern is used as it is and not checked in `Add`: if it contains a construct the non-backtracking engine rejects (a lookaround or backreference of your own), `NotSupportedException` comes from the first `Mask` or `Redact` call instead. `UseNonBacktracking()` does not exist on .NET Standard 2.1.

## Errors and edge cases

| Call | Throws |
|------|--------|
| `Redactor.For(null)` | `ArgumentNullException` (`patterns`) |
| `Redactor.For(pattern, null)` | `ArgumentNullException` (`patterns`): "The patterns must not contain null." |
| `Add(null)` (either overload) | `ArgumentNullException` (`pattern`) |
| `Mask(null)`, `Redact(null)` | `ArgumentNullException` (`text`) |
| `RedactionStyle.KeepLast(-1)`, `KeepFirst(-1)` | `ArgumentOutOfRangeException` (`count`): "The count must not be negative." |
| `RedactionStyle.Label(null)` | `ArgumentNullException` (`label`) |
| `RedactionStyle.Label("")` | `ArgumentException` (`label`): "The label must not be empty." |
| `RedactionStyle.Custom(null)` | `ArgumentNullException` (`replacement`) |
| A `Custom` function returns `null` | `InvalidOperationException` from `Mask` or `Redact` |
| A pattern exceeds its timeout | `RegexMatchTimeoutException` from `Mask` or `Redact` |
| `Add` with an anchored non-backtracking pattern | `NotSupportedException` |
| An unanchored non-backtracking pattern with a lookaround or backreference | `NotSupportedException` from the first `Mask` or `Redact` |

Other edge cases:

- Empty text returns empty text and an empty report.
- A redactor with no patterns returns the text unchanged.
- A `Custom` function that throws lets its exception propagate from `Mask` or `Redact`.

## Best practices

- **Redact at the boundary.** Run the redactor where text leaves your system (the logger, the ticket exporter, the outbound API call), not deep inside business code.
- **Prefer `Label` for the most sensitive values.** It hides length and shape. Use `KeepLast` only where people need the tail to identify a value, such as the last four card digits.
- **Add checksum rules** with `Must` so that only real card numbers and IBANs are redacted.
- **Test the redactor with real samples** of your text, including values next to `_`, inside URLs and at line ends. A regex-based redactor only finds what its patterns describe; it is a safety net, not a guarantee that no personal data remains.
- **Build once, share everywhere.** A redactor is immutable and thread-safe.

## Related pages

- [Semantic Rules](./semantic-rules.md): `Must`, `ConstrainedPattern` and the built-in validators.
- [Common Patterns](./common-patterns.md): ready-made patterns for e-mail, cards, IBANs, phone numbers and more.
- [Fluent Replacements](./fluent-replacements.md): general find-and-replace when you need more than redaction.
- [ReDoS Safety](./redos-safety.md): timeouts and backtracking analysis for patterns that run on untrusted input.
