---
id: semantic-rules
title: "Semantic Rules and Validation"
description: "Attach checks a regex cannot express, such as checksums and real dates, explain why a value failed, and validate models with [MatchesPattern]."
---

A regular expression checks the shape of a value. It cannot check that a card number has a valid check digit, that `2026-02-30` is a real date, or that an IPv4 octet is at most 255. A semantic rule is a .NET predicate that runs on each match. `Must(...)` attaches one or more rules to a pattern and returns a `ConstrainedPattern`. A match only counts when the pattern matches and every rule accepts it.

Use semantic rules when:

- The format is easy to describe with a pattern, but correctness needs arithmetic or a lookup: Luhn, IBAN mod-97, ISBN, calendar dates, number ranges, allow-lists.
- You validate whole values (form fields, route values, model properties) and want to know why a value failed.
- You search text and want to skip values that look right but are not real, for example when you [redact PII](./pii-redaction.md).

All types on this page live in the `Masterly.FluentRegex` namespace.

| Type | What it is | Target frameworks |
|------|------------|-------------------|
| `Pattern.Must(...)` | Two methods on every pattern that return a `ConstrainedPattern`. | netstandard2.1, net8.0, net10.0 |
| `ConstrainedPattern` | A pattern plus an ordered list of rules. Immutable and thread-safe. | netstandard2.1, net8.0, net10.0 |
| `ConstraintResult` | The verdict of `ConstrainedPattern.Check(input)`, with the reason for a failure. | netstandard2.1, net8.0, net10.0 |
| `Validators` | Ready-made rules: `Luhn`, `IbanChecksum`, `IsoDate`, `IPv4`. | netstandard2.1, net8.0, net10.0 |
| `MatchesPatternAttribute` | A DataAnnotations attribute, `[MatchesPattern]`, for model validation. | net8.0, net10.0 only |

## Adding a rule to a pattern

Call `Must` on any pattern and pass a predicate over the matched text. The second argument is a description. It is reported when the rule rejects a value.

```csharp
using Masterly.FluentRegex;

ConstrainedPattern card = CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");

bool shapeOnly = CommonPatterns.CreditCard().IsMatch("4111 1111 1111 1112"); // true: the shape is right
bool valid = card.IsMatch("4111 1111 1111 1111");                           // true
bool invalid = card.IsMatch("4111 1111 1111 1112");                         // false: wrong check digit
```

- `Must(Func<string, bool> rule, string? description = null)` receives the whole matched text and returns `true` to accept it.
- `Must` never changes the pattern it is called on. It returns a new `ConstrainedPattern`, so you can keep the plain pattern and the constrained one side by side.
- A `null` rule throws `ArgumentNullException` (parameter `rule`).
- The rule runs only after the pattern has matched. It never sees text that does not have the right shape, so it can index into the value without extra checks.

### Writing your own rule

A rule is any `Func<string, bool>`: a lambda, a local function or a static method. A method group keeps the pattern readable when the check is longer than one line.

```csharp
using Masterly.FluentRegex;

public static class BookRules
{
    public static bool Isbn13Checksum(string value)
    {
        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            int digit = value[i] - '0';
            sum += i % 2 == 0 ? digit : digit * 3;
        }

        int checkDigit = (10 - sum % 10) % 10;
        return value[12] - '0' == checkDigit;
    }
}

ConstrainedPattern isbn = Pattern.With.StartOfLine.Digit.Repeat.Times(13).EndOfLine
    .Must(BookRules.Isbn13Checksum, "ISBN-13 check digit");

bool good = isbn.IsMatch("9780306406157");                   // true
bool bad = isbn.IsMatch("9780306406158");                    // false
string reason = isbn.Check("9780306406158").ToString();      // Invalid: the rule 'ISBN-13 check digit' rejected the value

ConstrainedPattern evenNumber = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine
    .Must(value => (value[value.Length - 1] - '0') % 2 == 0, "even number");

bool even = evenNumber.IsMatch("1234");                      // true
bool odd = evenNumber.IsMatch("1235");                       // false
```

- `Isbn13Checksum` can read `value[12]` without a length check, because the pattern already guarantees exactly 13 digits.
- The pattern guarantees digits, not ASCII digits. `\d` also matches digits from other scripts, such as `٩٧٨...`, and `value[i] - '0'` is meaningless for them. When a rule does digit arithmetic, restrict the pattern to `0`-`9` with `WithAsciiDigits()` or reject other characters in the rule. See [Common pitfalls](#common-pitfalls).
- Keep rules pure and fast. They run once for every candidate match, and a search can produce many candidates.
- Let the pattern do the structural work and the rule do only what a regex cannot. That keeps the rule short and lets tools such as the [optimizer](./optimizer.md) and the [ReDoS analyzer](./redos-safety.md) see the real shape of the value.

:::warning

A rule that throws stops the whole call. `ConstrainedPattern` does not catch exceptions from your predicate. For example, `value => int.Parse(value) > 0` throws `OverflowException` for `99999999999`. Prefer `int.TryParse(value, out int number) && number > 0`, which simply rejects the value. See [Common pitfalls](#common-pitfalls).

:::

## Rules on a named group

Often only one part of a match needs the extra check. `Must(groupName, rule, description)` passes the value of a named group instead of the whole match.

```csharp
using Masterly.FluentRegex;

Pattern isoDateText = Pattern.With.Digit.Repeat.Times(4).Literal("-").Digit.Repeat.Times(2).Literal("-").Digit.Repeat.Times(2);

ConstrainedPattern booking = Pattern.With
    .StartOfLine
    .Literal("BOOK-")
    .NamedGroup("date", isoDateText)
    .EndOfLine
    .Must("date", Validators.IsoDate, "real calendar date");

string leapDay = booking.Check("BOOK-2024-02-29").ToString(); // Valid
string noSuchDay = booking.Check("BOOK-2026-02-30").ToString(); // Invalid: the rule 'real calendar date' rejected the value
```

Group rules and whole-match rules can be mixed on one pattern. `CommonPatterns.Iban()` exposes the groups `country` and `check`, so you can validate the checksum and restrict the country in one place:

```csharp
using System.Collections.Generic;
using Masterly.FluentRegex;

HashSet<string> supportedCountries = new HashSet<string> { "DE", "GB", "NL" };

ConstrainedPattern iban = CommonPatterns.Iban()
    .Must(Validators.IbanChecksum, "IBAN checksum")
    .Must("country", country => supportedCountries.Contains(country), "supported country");

string ok = iban.Check("GB82 WEST 1234 5698 7654 32").ToString();       // Valid
string checksum = iban.Check("GB82 WEST 1234 5698 7654 33").ToString(); // Invalid: the rule 'IBAN checksum' rejected the value
string country = iban.Check("FR1420041010050500013M02606").ToString();  // Invalid: the rule 'supported country' rejected the value
```

- The group name is checked when you call `Must`. An unknown name throws `ArgumentException` with the message `The pattern has no group named 'year'. (Parameter 'groupName')`. A `null` name throws `ArgumentNullException`.
- Group numbers work as names too, because .NET treats `"1"`, `"2"`, ... as the names of numbered groups. `Pattern.FromRegex(@"^(\d+)-(\d+)$").Must("2", end => end.Length == 2)` checks the second group. Named groups are clearer; see [Groups and backreferences](./groups-and-backreferences.md).
- When a group captures more than once (a group inside a repetition), the rule sees the last capture, which is the value .NET reports for `Group.Value`.

### Optional groups stay optional

A rule on a group that did not take part in the match is skipped. The value is not rejected because an optional part is missing.

```csharp
using Masterly.FluentRegex;

ConstrainedPattern phone = Pattern.With
    .StartOfLine
    .Digit.Repeat.Times(3)
    .NamedGroup("ext", Pattern.With.Literal("x").Digit.Repeat.OneOrMore).Repeat.Optional
    .EndOfLine
    .Must("ext", ext => ext.Length <= 4, "extension of at most 3 digits");

string noExtension = phone.Check("555").ToString();          // Valid: the rule is skipped
string shortExtension = phone.Check("555x12").ToString();    // Valid
string longExtension = phone.Check("555x12345").ToString();  // Invalid: the rule 'extension of at most 3 digits' rejected the value
```

If the part is required, make it required in the pattern, not in the rule.

## Chaining rules

`Must` can be called again on a `ConstrainedPattern`. Every call returns a new instance with one more rule. Rules run in the order you added them, and evaluation stops at the first rule that fails.

```csharp
using System.Collections.Generic;
using Masterly.FluentRegex;

Pattern code = Pattern.With.StartOfLine.NamedGroup("number", Pattern.With.Digit.Repeat.Times(1, 6)).EndOfLine;

ConstrainedPattern rules = code
    .Must(value => value != "000000", "not all zeros")
    .Must("number", number => int.Parse(number) % 7 == 0)
    .Must(value => value.Length >= 2);

IReadOnlyList<string> descriptions = rules.RuleDescriptions;
// not all zeros | rule 2 on group 'number' | rule 3

string zeros = rules.Check("000000").ToString(); // Invalid: the rule 'not all zeros' rejected the value
string fifteen = rules.Check("15").ToString();   // Invalid: the rule 'rule 2 on group 'number'' rejected the value
string seven = rules.Check("7").ToString();      // Invalid: the rule 'rule 3' rejected the value
string fourteen = rules.Check("14").ToString();  // Valid
```

- A rule without a description, or with an empty or white-space one, gets a default name from its position: `rule 3` for a whole-match rule and `rule 2 on group 'number'` for a group rule. Numbers count from 1 across all rules of the pattern.
- `RuleDescriptions` lists the descriptions in evaluation order.
- Put cheap rules first. A later rule does not run once an earlier one has failed, and none of them runs when the pattern itself does not match.
- Give every rule a description you would be happy to show a user. The description is what `Check` reports and what `[MatchesPattern]` puts in its error message.

`ConstrainedPattern` members:

| Member | What it does |
|--------|--------------|
| `new ConstrainedPattern(pattern)` | Creates a constrained pattern without rules. A `null` pattern throws `ArgumentNullException`. |
| `Pattern` | The underlying pattern. It is the same instance you called `Must` on. |
| `RuleDescriptions` | The rule descriptions, in evaluation order. |
| `Must(rule, description)` | Returns a copy with one more whole-match rule. |
| `Must(groupName, rule, description)` | Returns a copy with one more rule on a named group. |
| `IsMatch(input)` | `true` when some match in the input passes every rule. Searches the input. |
| `Match(input)` | The first match that passes every rule, or `null`. |
| `Matches(input)` | Every match that passes every rule, found lazily, left to right. |
| `IsValid(input)` | `true` when the **whole** input matches and passes every rule. No diagnostics. |
| `Check(input)` | Validates the whole input and returns a `ConstraintResult` that explains a failure. |

A constrained pattern without rules is still useful. It gives a plain pattern whole-value validation with diagnostics:

```csharp
using Masterly.FluentRegex;

ConstrainedPattern zip = new ConstrainedPattern(CommonPatterns.ZipCode());

int ruleCount = zip.RuleDescriptions.Count;    // 0
bool plusFour = zip.IsValid("12345-6789");     // true
string tooShort = zip.Check("1234").ToString();
// Invalid: the value does not match the pattern at position 0: expected \d{5} (a digit, exactly 5 times)
```

:::tip

`ConstrainedPattern` is immutable and safe to share between threads. Build it once and store it in a `static readonly` field, like a `Regex`. The whole-input regex used by `IsValid` and `Check` is built on first use and then reused.

:::

## Validating a whole value or searching text

A constrained pattern answers two different questions. Pick the member that matches the question.

| Question | Members | Semantics |
|----------|---------|-----------|
| Is this whole value valid? | `IsValid(input)`, `Check(input)` | The pattern must consume the entire input, like `RegularExpressionAttribute`. |
| Does this text contain a valid value? | `IsMatch(input)`, `Match(input)`, `Matches(input)` | The pattern may match anywhere in the input. |

```csharp
using Masterly.FluentRegex;

ConstrainedPattern threeDigits = Pattern.With.Digit.Repeat.Times(3).Must(value => value[0] != '0', "no leading zero");

bool found = threeDigits.IsMatch("x123y");             // true: "123" is somewhere in the text
bool whole = threeDigits.IsValid("x123y");             // false: the whole input is not three digits
bool exact = threeDigits.IsValid("123");               // true
string why = threeDigits.Check("x123y").ToString();
// Invalid: the value does not match the pattern at position 0: expected \d{3} (a digit, exactly 3 times)
string rule = threeDigits.Check("012").ToString();     // Invalid: the rule 'no leading zero' rejected the value
```

- `IsValid` and `Check` wrap the pattern as `\A(?:pattern)\z`. You do not need to add anchors to the pattern, and anchors that are already there do no harm.
- The wrapper finds a full-length alternative even when a shorter one comes first: a pattern built with `OneOf(Literal("a"), Literal("ab"))` accepts `"ab"` in `IsValid`.
- The whole-input regex uses the pattern's options and timeout. A trailing `# comment` in `RegexOptions.IgnorePatternWhitespace` mode is handled, so it cannot swallow the closing wrapper.
- `IsValid` is the cheap form of `Check`. It returns a `bool` and builds no diagnostics.

### Searching with rules

`Match` and `Matches` return the regular `System.Text.RegularExpressions.Match` objects of the accepted candidates, so groups, `Index` and `Length` work as usual.

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

ConstrainedPattern cardNumber = Pattern.With.Digit.Repeat.Times(16).Must(Validators.Luhn, "Luhn checksum");

string text = "ids 4111111111111112 4111111111111111";

foreach (Match match in cardNumber.Matches(text))
{
    Console.WriteLine($"{match.Value} at {match.Index}");
}
// prints: 4111111111111111 at 21

Match? first = cardNumber.Match("card: 4111111111111111.");  // first.Value is "4111111111111111"
Match? none = cardNumber.Match("card: 4111111111111112.");   // null
```

How the search moves through the input:

- When a candidate fails a rule, the search resumes one character after the start of that candidate. A valid value that overlaps a rejected one is still found.
- Accepted matches never overlap. After an accepted match, the search continues at its end.
- Only the first match the regex finds at a position is tested. If the rules reject it, a shorter or different match at the same position is not tried.
- `Matches` is lazy. It searches only as far as you enumerate.
- For a pattern with `RegexOptions.RightToLeft`, the regular matches are found first and then filtered by the rules.
- `Matches(null)`, `IsMatch(null)`, `Match(null)`, `IsValid(null)` and `Check(null)` throw `ArgumentNullException`.

```csharp
using System.Linq;
using Masterly.FluentRegex;

ConstrainedPattern notStartingWithOne = Pattern.With.Digit.Repeat.Times(3).Must(value => value[0] != '1');
string resumed = string.Join(", ", notStartingWithOne.Matches("1234").Select(match => match.Value));
// 234: "123" was rejected, the search resumed at index 1

ConstrainedPattern triples = Pattern.With.Digit.Repeat.Times(3).Must(value => true);
string noOverlap = string.Join(", ", triples.Matches("1234567").Select(match => match.Value));
// 123, 456

ConstrainedPattern twoDigits = Pattern.With.Digit.Repeat.OneOrMore.Must(value => value.Length == 2);
string firstOnly = string.Join(", ", twoDigits.Matches("1234").Select(match => $"{match.Value} at {match.Index}"));
// 34 at 2: "1234" and "234" were rejected; the shorter "12" at index 0 was never tried
```

:::note

Most `CommonPatterns` are anchored with `^...$` because they are built for validation. A constrained pattern built from one only finds a value that is the whole input: `CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum").IsMatch("card 4111 1111 1111 1111 on file")` returns `false`. To find values inside text, build an unanchored pattern as above, or use a [`Redactor`](./pii-redaction.md), which turns those anchors into token boundaries, `(?<!\w)...(?!\w)`, for you.

:::

## Reading the result

`Check(input)` returns a `ConstraintResult`. It tells you whether the value is valid and, when it is not, which side rejected it: the pattern (the shape is wrong) or a rule (the shape is right but the value is not).

```csharp
using Masterly.FluentRegex;

ConstrainedPattern card = CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");

ConstraintResult ok = card.Check("4111 1111 1111 1111");
ConstraintResult badChecksum = card.Check("4111 1111 1111 1112");
ConstraintResult badShape = card.Check("4111 1111 11x1 1111");

bool okIsValid = ok.IsValid;                          // true
string okText = ok.ToString();                        // Valid

bool ruleFailed = badChecksum.PatternFailed;          // false
string? failedRule = badChecksum.FailedRule;          // Luhn checksum
MatchTrace? noTrace = badChecksum.Trace;              // null

bool shapeFailed = badShape.PatternFailed;            // true
string? noRule = badShape.FailedRule;                 // null
int position = badShape.Trace!.Failure.Position;      // 4
string construct = badShape.Trace.Failure.Construct;  // (?:[\s\-]?\d{4}){3}
string expected = badShape.Trace.Failure.Description; // group, exactly 3 times
string summary = badShape.ToString();
// Invalid: the value does not match the pattern at position 4: expected (?:[\s\-]?\d{4}){3} (group, exactly 3 times)
```

| Member | Type | Meaning |
|--------|------|---------|
| `IsValid` | `bool` | The whole input matched the pattern and every rule accepted it. |
| `PatternFailed` | `bool` | The pattern itself rejected the input. Same as `Trace != null`. |
| `FailedRule` | `string?` | The description of the first rule that rejected the input. `null` when the input is valid or the pattern failed. |
| `Trace` | `MatchTrace?` | The trace of the whole-input pattern when the pattern failed, otherwise `null`. `Trace.Failure` holds `Position`, `Construct`, `Description`, `MatchedSoFar`, `Found` and `MatchedSteps`. |
| `ToString()` | `string` | A one-line summary: `Valid`, `Invalid: the rule '...' rejected the value`, or `Invalid: the value does not match the pattern at position N: expected ... (...)`. In the rare case where the tracer cannot locate the failure, `Trace.Failure` is `null` and the summary is just `Invalid: the value does not match the pattern`. |

At most one of `FailedRule` and `Trace` is set. A pattern failure is reported before any rule runs, so a value with the wrong shape never reaches your rules. A `switch` on the result covers all three cases:

```csharp
// card is the constrained pattern from the previous example; input comes from your application
ConstraintResult result = card.Check(input);

string message = result switch
{
    { IsValid: true } => "OK",
    { PatternFailed: true } => $"Wrong format near position {result.Trace?.Failure?.Position}",
    _ => $"Failed check: {result.FailedRule}",
};
// "4111 1111 1111 1111" gives: OK
// "4111 1111 1111 1112" gives: Failed check: Luhn checksum
// "4111 1111 11x1 1111" gives: Wrong format near position 4
```

- `Trace.Failure.Position` is where the construct that could not match began, not always the exact bad character. In the card example the `x` is at index 12, but the failing construct, the three remaining digit blocks, begins at index 4.
- The trace describes the whole-input wrapper, so `Trace.Pattern` is `\A(?:^\d{4}(?:[\s\-]?\d{4}){3}$)\z`, not the bare pattern.
- For the full step-by-step report of a failed match, see [Match tracing](./match-tracing.md).

:::warning

`ConstraintResult.ToString()` never includes the validated input, so it is safe to log. The `Trace` object is different: `Trace.Input`, `Trace.Failure.Found` and `Trace.ToString()` contain the input text (for example `Found` is `" 1111 11x1 1111"` above). Do not log the trace for sensitive values such as card numbers.

:::

## Built-in validators

`Validators` holds ready-made rules for common checksums and ranges. Each one is a `static bool Method(string value)`, so you pass it as a method group: `Must(Validators.Luhn, "Luhn checksum")`.

| Validator | Accepts | Ignores | Rejects |
|-----------|---------|---------|---------|
| `Validators.Luhn` | A Luhn (mod 10) check digit, as used by card numbers and IMEIs. At least two digits. | Spaces and `-` | Any other character, such as `.` or a non-ASCII digit |
| `Validators.IbanChecksum` | ISO 13616 mod-97 check digits. Letters in either case. 5 to 34 characters, starting with two letters and two digits. | Spaces | Any other separator, such as `-` |
| `Validators.IsoDate` | Exactly `yyyy-MM-dd` naming a real date in the Gregorian calendar, from `0001-01-01`. | Nothing | One-digit months or days, times, surrounding white space, year `0000` |
| `Validators.IPv4` | Four dot-separated octets, each one to three ASCII digits with a value of 0-255. Leading zeros are accepted, as by `CommonPatterns.IPv4()`. | Nothing | Missing or extra octets, empty octets, surrounding white space |

```csharp
using Masterly.FluentRegex;

bool mixedSeparators = Validators.Luhn("4111-1111 1111-1111");             // true
bool imei = Validators.Luhn("79927398713");                                 // true
bool dots = Validators.Luhn("4111.1111.1111.1111");                         // false
bool oneDigit = Validators.Luhn("0");                                       // false: needs at least two digits
bool twoDigits = Validators.Luhn("00");                                     // true

bool lowercase = Validators.IbanChecksum("gb82west12345698765432");         // true
bool dashes = Validators.IbanChecksum("GB82-WEST-1234-5698-7654-32");       // false

bool leapDay = Validators.IsoDate("2024-02-29");                            // true
bool notLeap = Validators.IsoDate("2023-02-29");                            // false
bool shortMonth = Validators.IsoDate("2026-1-01");                          // false
bool yearZero = Validators.IsoDate("0000-01-01");                           // false

bool leadingZeros = Validators.IPv4("010.001.1.1");                         // true
bool outOfRange = Validators.IPv4("256.1.1.1");                             // false
bool padded = Validators.IPv4(" 1.1.1.1");                                  // false

bool nullValue = Validators.Luhn(null!);                                    // false
```

- Every validator returns `false` for `null` instead of throwing.
- The validators use invariant, ASCII-only parsing. They never depend on the current culture.
- `IsoDate` checks the calendar, not the time zone or a range. Add your own rule for "not in the future" or "after 1900".
- `IbanChecksum` checks the check digits only. It does not check the length that each country requires. Pair it with a pattern such as `CommonPatterns.Iban()` for the structure.

### Guarding a loose pattern

The validators are most useful next to a pattern that is deliberately loose. `CommonPatterns.DateIso()` is `^\d{4}-[0-1]\d-[0-3]\d$`, which accepts `2026-02-30`. A rule closes the gap:

```csharp
using Masterly.FluentRegex;

bool shapeOnly = CommonPatterns.DateIso().IsMatch("2026-02-30");            // true

ConstrainedPattern date = CommonPatterns.DateIso().Must(Validators.IsoDate, "real calendar date");
string calendar = date.Check("2026-02-30").ToString();
// Invalid: the rule 'real calendar date' rejected the value

Pattern dottedQuad = Pattern.FromRegex(@"^\d{1,3}(?:\.\d{1,3}){3}$");
ConstrainedPattern ipAddress = dottedQuad.Must(Validators.IPv4, "octets 0-255");
string ok = ipAddress.Check("192.168.1.1").ToString();                      // Valid
string tooBig = ipAddress.Check("999.168.1.1").ToString();
// Invalid: the rule 'octets 0-255' rejected the value
```

A loose pattern plus a rule is often easier to read and cheaper to run than a pattern that encodes every range. Compare `\d{1,3}` with the alternation needed for "0 to 255".

:::note

If your own code has a type named `Validators` and imports `Masterly.FluentRegex`, the name becomes ambiguous. Qualify it (`Masterly.FluentRegex.Validators.Luhn`) or add an alias such as `using RegexValidators = Masterly.FluentRegex.Validators;`.

:::

## Validating models with [MatchesPattern]

`[MatchesPattern]` is a DataAnnotations `ValidationAttribute`. It validates a string property, field or parameter against a `Pattern` or `ConstrainedPattern` that a public static member provides. Anything that runs DataAnnotations validation picks it up: `Validator.TryValidateObject`, ASP.NET Core MVC model binding, Blazor's `DataAnnotationsValidator`, and others.

:::note

The attribute ships in the `net8.0` and `net10.0` builds of the package only. `System.ComponentModel.DataAnnotations` is not part of `netstandard2.1`. `Must`, `ConstrainedPattern` and `Validators` work on every target.

:::

Declare the patterns once, as public static properties, fields or parameterless methods, and point the attribute at them with `typeof` and `nameof`:

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Masterly.FluentRegex;

public static class CheckoutPatterns
{
    public static readonly Pattern Coupon = Pattern.With.UppercaseLetter.Repeat.Times(2).Digit.Repeat.Times(3);

    public static Pattern Sku => Pattern.With.UppercaseLetter.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);

    public static ConstrainedPattern Card => CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum");

    public static Pattern OrderNumber() => Pattern.With.Literal("ORD-").Digit.Repeat.Times(6);
}

public sealed class CheckoutRequest
{
    [MatchesPattern(typeof(CheckoutPatterns), nameof(CheckoutPatterns.Sku))]
    public string? Sku { get; set; }

    [Required]
    [MatchesPattern(typeof(CheckoutPatterns), nameof(CheckoutPatterns.Card))]
    public string? Card { get; set; }

    [Display(Name = "Order number")]
    [MatchesPattern(typeof(CheckoutPatterns), nameof(CheckoutPatterns.OrderNumber))]
    public string? OrderNumber { get; set; }

    [MatchesPattern(typeof(CheckoutPatterns), nameof(CheckoutPatterns.Coupon), ErrorMessage = "{0} is not a valid coupon code.")]
    public string? Coupon { get; set; }
}

CheckoutRequest request = new CheckoutRequest
{
    Sku = "ABC-12345",
    Card = "4111 1111 1111 1112",
    OrderNumber = "ORD-12",
    Coupon = "ab123",
};

List<ValidationResult> results = new List<ValidationResult>();
bool isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
// false

foreach (ValidationResult result in results)
{
    Console.WriteLine($"{string.Join(", ", result.MemberNames)}: {result.ErrorMessage}");
}
// prints:
// Sku: The field Sku does not match the required pattern.
// Card: The field Card is invalid: Luhn checksum.
// OrderNumber: The field Order number does not match the required pattern.
// Coupon: Coupon is not a valid coupon code.
```

What the attribute does:

- **The whole value must match**, like `RegularExpressionAttribute`. `ABC-12345` fails even though it contains a valid SKU. You do not need `^` and `$` in the pattern.
- **`null` and empty strings are valid.** Combine with `[Required]` to reject them. An empty `Card` above is reported only by `[Required]`: `The Card field is required.`
- **Rule failures name the rule.** When a `ConstrainedPattern` rule rejects the value, the message is `The field {name} is invalid: {rule description}.` When the pattern itself fails, the message is `The field {name} does not match the required pattern.`
- **A custom message always wins.** When you set `ErrorMessage` (or `ErrorMessageResourceType` and `ErrorMessageResourceName`), it is used for both pattern and rule failures. `{0}` is the display name.
- **Display names are respected.** `[Display(Name = "Order number")]` changes `{name}` in the message.
- **The member name is reported.** Each `ValidationResult` lists the property in `MemberNames`, so frameworks attach the error to the right field.
- **Non-string values fail.** On a property of another type, such as `object` holding `42`, the message is `The field {name} must be a string to be matched against a pattern.`

:::note

`Validator.TryValidateObject` without `validateAllProperties: true` checks only `[Required]`. That is standard DataAnnotations behavior, not specific to `[MatchesPattern]`. ASP.NET Core MVC validates all attributes.

:::

### Using the attribute directly

The attribute is a normal `ValidationAttribute`, so the inherited members work too. This is handy in unit tests or when you validate a single value.

```csharp
// request is the CheckoutRequest from the previous example
MatchesPatternAttribute sku = new MatchesPatternAttribute(typeof(CheckoutPatterns), nameof(CheckoutPatterns.Sku));

bool good = sku.IsValid("ABC-1234");   // true
bool bad = sku.IsValid("ABC-12");      // false
bool empty = sku.IsValid(null);        // true: null is left to [Required]

ValidationResult? single = sku.GetValidationResult(
    "ABC-12",
    new ValidationContext(request) { DisplayName = "Stock code", MemberName = "Sku" });
// single.ErrorMessage is "The field Stock code does not match the required pattern."

sku.Validate("ABC-12", "Sku");
// throws ValidationException: The field Sku does not match the required pattern.

ValidationContext cardContext = new ValidationContext(request) { MemberName = nameof(CheckoutRequest.Card) };
List<ValidationResult> cardResults = new List<ValidationResult>();
bool cardIsValid = Validator.TryValidateProperty("4111 1111 1111 1112", cardContext, cardResults);
// false; cardResults[0].ErrorMessage is "The field Card is invalid: Luhn checksum."

MatchesPatternAttribute card = new MatchesPatternAttribute(typeof(CheckoutPatterns), nameof(CheckoutPatterns.Card));
card.Validate("4111 1111 1111 1112", "Card");
// throws ValidationException: The field Card does not match the required pattern.
```

- The members that take a `ValidationContext` (`GetValidationResult`, `Validate(value, context)`, `Validator.TryValidateProperty` and `Validator.TryValidateObject`) report the failed rule by name.
- The members without a context come from the `ValidationAttribute` base class. `IsValid(value)` returns only a `bool`. `Validate(value, name)` throws with the general message, `The field {name} does not match the required pattern.`, even when a rule failed, as the last line above shows.
- `GetValidationResult` copies `MemberName` from the context into `MemberNames`. When the context has no `MemberName`, the result has no member names, and the message uses the context's default display name, which is the name of the validated object's type: `The field CheckoutRequest is invalid: Luhn checksum.`

| Member | What it is |
|--------|------------|
| `MatchesPatternAttribute(Type patternType, string memberName)` | Creates the attribute. `null` arguments throw `ArgumentNullException`. An empty or white-space `memberName` throws `ArgumentException`. |
| `PatternType` | The type that declares the pattern. |
| `MemberName` | The name of the public static property, field or parameterless method that provides it. |
| `ErrorMessage`, `ErrorMessageResourceType`, `ErrorMessageResourceName` | Inherited from `ValidationAttribute`. Replace the default messages. |

### How the pattern member is resolved

- The attribute looks for a public static **property** first, then a public static **field**, then a public static **parameterless method**, all with the given name on `PatternType`.
- The member must return a `Pattern` or a `ConstrainedPattern`. A plain `Pattern` is treated as a `ConstrainedPattern` without rules.
- The member is read once, the first time a non-empty string is validated, and the result is cached per type and member name for the life of the process. A property that builds a new pattern on every call is therefore fine, but a member whose value changes later is not picked up. A failed lookup is not cached: it throws again on every validation.
- A null or empty value is accepted before the member is read, so a misconfigured attribute only fails once it sees a real value.
- The attribute can be applied once per property, field or parameter (`AllowMultiple = false`).
- `PatternType` is annotated with `[DynamicallyAccessedMembers]` for public properties, fields and methods, so the trimmer keeps the members the attribute reads under trimming and Native AOT.

A misconfigured attribute throws `InvalidOperationException` on first validation, naming the type and member. The messages below come from a class `BadPatterns` with a missing member `Postcode`, a `string` property `Coupons`, a `Pattern?` property `Missing` that returns `null`, and a property `Broken` that throws:

| Problem | Message |
|---------|---------|
| No such member | `The type 'BadPatterns' has no public static property, field or parameterless method named 'Postcode'.` |
| Wrong return type | `'BadPatterns.Coupons' returns 'System.String', but a Pattern or ConstrainedPattern is required.` |
| Member returned `null` | `'BadPatterns.Missing' returned null instead of a Pattern.` |
| Member threw | `'BadPatterns.Broken' threw while providing the pattern.` The original exception is the `InnerException`. |

The type name in the message is the full name, including its namespace.

:::tip

Write a unit test that validates each annotated model once with a non-empty value. Configuration errors then fail the build instead of the first real request.

:::

## Where else rules apply

A `ConstrainedPattern` is accepted by several other parts of the library. The rules always run in .NET.

| Feature | How rules are used | More |
|---------|--------------------|------|
| `Redactor.Add(ConstrainedPattern, style)` | Only hits that pass every rule are redacted. | [PII redaction](./pii-redaction.md) |
| ASP.NET Core `PatternRegistry.Add(name, ConstrainedPattern)` and the `{id:pattern(name)}` route constraint | A route value must be a whole match (`IsValid`) and pass every rule. | [ASP.NET Core](./aspnetcore.md) |
| EF Core `MatchingAsync` and `ToMatchingListAsync` with a `ConstrainedPattern` | SQL applies a prefilter based on the underlying pattern; each loaded row is then checked in .NET with `ConstrainedPattern.IsMatch`, which searches the column value rather than requiring a whole match. | [Entity Framework Core](./entity-framework-core.md) |
| EF Core `AddCheckConstraintsFromAttributes()` | `[MatchesPattern]` becomes a database CHECK constraint from `ConstrainedPattern.Pattern` only. The rules are not part of the constraint. | [Entity Framework Core](./entity-framework-core.md) |

```csharp
using Masterly.FluentRegex;

Redactor redactor = Redactor.For().Add(CommonPatterns.CreditCard().Must(Validators.Luhn, "Luhn checksum"), RedactionStyle.KeepLast(4));

string masked = redactor.Mask("card 4111 1111 1111 1111, not 4111 1111 1111 1112");
// card **** **** **** 1111, not 4111 1111 1111 1112
```

:::warning

Rules cannot be translated to SQL. Calling `constrained.IsMatch(...)` inside an EF Core LINQ query throws `InvalidOperationException` that tells you to query with `constrained.Pattern` or to use `MatchingAsync(selector, constrained)`. A database CHECK constraint generated from `[MatchesPattern]` enforces only the shape; keep the attribute so the rules still run in your application.

:::

## Common pitfalls

**A rule that throws.** Exceptions from your predicate are not caught. Make rules total: return `false` instead of throwing.

```csharp
using Masterly.FluentRegex;

ConstrainedPattern positive = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine
    .Must(value => int.Parse(value) > 0, "positive");
// positive.Check("99999999999") throws OverflowException

ConstrainedPattern safePositive = Pattern.With.StartOfLine.Digit.Repeat.OneOrMore.EndOfLine
    .Must(value => int.TryParse(value, out int number) && number > 0, "positive 32-bit number");

string tooBig = safePositive.Check("99999999999").ToString(); // Invalid: the rule 'positive 32-bit number' rejected the value
string fine = safePositive.Check("42").ToString();            // Valid
```

**Using `IsMatch` to validate a form field.** `IsMatch` searches, so `"x123y"` passes a three-digit pattern. Use `IsValid` or `Check` for whole values. See [Validating a whole value or searching text](#validating-a-whole-value-or-searching-text).

**Searching with an anchored common pattern.** A `CommonPatterns` pattern finds a value only when it is the whole input. Build an unanchored pattern for text, or use a `Redactor`.

**Expecting a shorter alternative to be tried.** After a rule rejects a candidate, the search moves to the next position. It does not try a shorter match at the same position. Make the pattern itself precise, for example with `Times(16)` instead of `OneOrMore`.

**Non-ASCII digits.** `\d` in .NET matches digits from every script, but the built-in validators accept ASCII `0`-`9` only. `Validators.Luhn("٤١١١١١١١١١١١١١١١")` returns `false`. This is usually what you want for card and bank numbers. Your own rules that compute with `c - '0'` must guard against such digits too. To restrict the pattern as well, call `WithAsciiDigits()` on it or use the `DigitMode.Ascii` overloads of `CommonPatterns`, such as `CommonPatterns.CreditCard(DigitMode.Ascii)` (see [Unicode](./unicode.md)).

**Logging the trace.** `ConstraintResult.ToString()` is safe to log; `ConstraintResult.Trace` contains the input.

**Untrusted input and timeouts.** `IsValid` and `Check` use the pattern's timeout. Set one with `WithTimeout(...)` before calling `Must`, and handle `RegexMatchTimeoutException`. The timeout does not cover the time your rules take.

```csharp
using System;
using Masterly.FluentRegex;

ConstrainedPattern email = CommonPatterns.Email()
    .WithTimeout(TimeSpan.FromMilliseconds(100))
    .Must(value => value.Length <= 254, "at most 254 characters");

bool ok = email.IsValid("bob@example.com"); // true
TimeSpan timeout = email.Pattern.Timeout;   // 00:00:00.1000000
```

See [ReDoS safety](./redos-safety.md) for patterns that are risky on untrusted input, and [Validation and debugging](./validation-and-debugging.md) for checking a pattern before you ship it.
