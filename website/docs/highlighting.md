---
id: highlighting
title: "Highlighting Matches"
description: "Mark every match and group capture in a text, as ANSI colors for terminals, HTML for web pages or brackets for tests and logs."
---

`Highlight(text)` shows you where a pattern matches in a text and what each group captured. It runs the pattern over the whole text and returns a copy of the text with every match and every group capture marked. Use it to see a pattern at work while you write it, to show matches on a web page, or to write short, readable test assertions.

There is one method and one enum, and both ship in the core `Masterly.FluentRegex` package for every target framework (`netstandard2.1`, `net8.0` and `net10.0`):

```csharp
public string Highlight(string text, HighlightFormat format = HighlightFormat.Ansi);

public enum HighlightFormat { Ansi, Html, Brackets }
```

Pick a format based on where the result is shown:

| Format | Matches look like | Groups look like | Use it for |
|--------|-------------------|------------------|------------|
| `HighlightFormat.Brackets` | `[555-1234]` | `[year:2026]` | Test assertions, logs, plain-text reports. |
| `HighlightFormat.Html` | `<mark class="frx-match">` | `<span class="frx-group frx-group-year" title="year">` | Web pages, HTML reports, Blazor and Razor views. |
| `HighlightFormat.Ansi` (default) | Blue background | A bright foreground color per group, plus a legend line | Terminals and console tools. |

## Highlighting a match

Call `Highlight` on any pattern, built fluently or with `Pattern.FromRegex`. Pass the format as the second argument.

```csharp
using System;
using Masterly.FluentRegex;

Pattern phone = Pattern.With.Digit.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);

string brackets = phone.Highlight("Call 555-1234 or 555-9876 now", HighlightFormat.Brackets);
// Call [555-1234] or [555-9876] now

string html = phone.Highlight("Call 555-1234 or 555-9876 now", HighlightFormat.Html);
// Call <mark class="frx-match">555-1234</mark> or <mark class="frx-match">555-9876</mark> now

string ansi = phone.Highlight("Call 555-1234 or 555-9876 now"); // HighlightFormat.Ansi is the default
Console.WriteLine(ansi);
```

The ANSI result is two lines. Written with the escape character shown as `\e`, it is:

```text
Call \e[44;97m555-1234\e[0m or \e[44;97m555-9876\e[0m now
Legend: \e[44;97m match \e[0m
```

- `Highlight` marks **every** match, in text order, the same matches `Matches(text)` returns. See [Matching](./matching.md).
- Text outside the matches is copied unchanged (Brackets), HTML-encoded with control characters made visible (Html), or copied with escape characters made visible (Ansi).
- When nothing matches, Brackets returns the text unchanged, Html returns it HTML-encoded, and Ansi returns it followed by the legend line.
- The pattern's options and match timeout apply, because `Highlight` runs the same cached `Regex` as `IsMatch` and `Matches`.

## Reading the markers

The Brackets format is the easiest one to read, so the examples in this section use it. The Html and Ansi formats mark exactly the same spans; only the markup differs.

### Named and numbered groups

Each match is wrapped in `[...]`. Each group capture inside it is wrapped in `[label:...]`, where the label is the group name, or its number when the group is unnamed.

```csharp
Pattern date = Pattern.With
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4)).Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2)).Literal("-")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2));

date.Highlight("Released 2026-11-02, patched 2026-11-09.", HighlightFormat.Brackets);
// Released [[year:2026]-[month:11]-[day:02]], patched [[year:2026]-[month:11]-[day:09]].

date.Highlight("No release yet.", HighlightFormat.Brackets);
// No release yet.

Pattern version = Pattern.With
    .Group(Pattern.With.Digit.Repeat.OneOrMore).Literal(".")
    .Group(Pattern.With.Digit.Repeat.OneOrMore);

version.Highlight("Upgrade from 2.0 to 3.1", HighlightFormat.Brackets);
// Upgrade from [[1:2].[2:0]] to [[1:3].[2:1]]
```

- Literal text between groups, such as the `-` in a date, sits inside the match marker but outside every group marker.
- Non-capturing groups, atomic groups and lookarounds are not marked themselves. Only capturing groups are.
- The `date` pattern above is reused in the rest of this page.

:::note

.NET numbers unnamed groups first and named groups after them, so in `(?<key>\w+)=(\w+)` the unnamed group is group 1 and `key` is group 2. The labels are still right (`[[key:a]=[1:1]]` for `a=1`), but the legend order and the ANSI colors follow the group numbers, not the order in the pattern. See [Groups and backreferences](./groups-and-backreferences.md).

:::

### Nested groups

A group inside another group is marked inside it.

```csharp
Pattern.FromRegex(@"(?<word>(\w+))").Highlight("hi", HighlightFormat.Brackets);
// [[word:[1:hi]]]

Pattern.FromRegex(@"(?<outer>a(?<inner>b)c)").Highlight("xabcx", HighlightFormat.Brackets);
// x[[outer:a[inner:b]c]]x
```

When two groups capture exactly the same text, as `word` and group 1 do above, the pattern's structure decides which marker is outside: the group that encloses the other in the pattern encloses it in the output.

### Repeated groups

Every capture of a repeated group is marked, not only the last one that `Groups["name"].Value` would return.

```csharp
Pattern.FromRegex(@"(\d)+").Highlight("v12", HighlightFormat.Brackets);
// v[[1:1][1:2]]

Pattern.FromRegex(@"(?<x>a)(?<x>b)").Highlight("ab", HighlightFormat.Brackets);
// [[x:a][x:b]]
```

This makes highlighting a quick way to check what a quantified group really captured, which is often not what you expect.

### Optional groups and alternation

A group that did not take part in a match is not marked. An empty capture is marked with an empty marker.

```csharp
Pattern.FromRegex(@"(x)?(y)").Highlight("y", HighlightFormat.Brackets);
// [[2:y]]

Pattern.FromRegex(@"(?<cat>cat)|(?<dog>dog)").Highlight("cat dog", HighlightFormat.Brackets);
// [[cat:cat]] [[dog:dog]]

Pattern.FromRegex(@"(a)()(b)").Highlight("ab", HighlightFormat.Brackets);
// [[1:a][2:][3:b]]
```

- `(x)?` skipped its group, so there is no `[1:...]` marker.
- In an alternation, only the branch that matched shows its group.
- `()` matched the empty string, so it shows as `[2:]`.

### Lookaround captures

A group inside a lookahead or lookbehind can capture text outside the match. Captures are clipped to their match, so a marker never leaves the match marker that contains it.

```csharp
Pattern.FromRegex(@"(?=(\w+))\w").Highlight("ab", HighlightFormat.Brackets);
// [[1:a]][[1:b]]

Pattern.FromRegex(@"\w+(?=(!))").Highlight("hi!", HighlightFormat.Brackets);
// [hi]!

Pattern.FromRegex(@"(?<=(a))b").Highlight("ab", HighlightFormat.Brackets);
// a[b]
```

- In the first example, the lookahead captured `ab` and then `b`, but each match is one character long, so each capture is cut to that character.
- A capture that lies entirely outside its match, such as the `!` after `hi` or the `a` before `b`, is dropped.
- So the markers show what the match consumed, not everything the groups saw. To see what a lookaround looked at, use [Match tracing](./match-tracing.md).

### Empty matches

Patterns that can match the empty string produce empty matches, exactly as `Regex.Matches` does. They render as empty markers.

```csharp
Pattern.FromRegex("a*").Highlight("bab", HighlightFormat.Brackets);
// []b[a][]b[]

Pattern.FromRegex(@"\b").Highlight("hi you", HighlightFormat.Brackets);
// []hi[] []you[]

Pattern.FromRegex("a+").Highlight("bab", HighlightFormat.Brackets);
// b[a]b
```

:::tip

A row of `[]` markers is a sign that the pattern can match nothing at all. That is usually a bug: `a*` where you meant `a+`. [Language facts](./language-facts.md) can prove the minimum match length up front.

:::

### Pattern options

`Highlight` uses the pattern's own options, so case-insensitive, multiline and right-to-left patterns highlight what they match.

```csharp
using System.Text.RegularExpressions;

Pattern.FromRegex("error", RegexOptions.IgnoreCase).Highlight("Error, ERROR, error", HighlightFormat.Brackets);
// [Error], [ERROR], [error]

Pattern.FromRegex(@"^\w+", RegexOptions.Multiline).Highlight("alpha one\nbeta two", HighlightFormat.Brackets);
// two lines:
// [alpha] one
// [beta] two

Pattern.FromRegex(@"\d+", RegexOptions.RightToLeft).Highlight("1 22 333", HighlightFormat.Brackets);
// [1] [22] [333]

Pattern.FromRegex(@"\d+").Highlight("12 ١٢", HighlightFormat.Brackets);
// [12] [١٢]

Pattern.FromRegex(@"\d+").WithAsciiDigits().Highlight("12 ١٢", HighlightFormat.Brackets);
// [12] ١٢
```

- Line breaks in the text are kept as they are.
- A `RightToLeft` pattern finds its matches from the end, but the output is always in text order.
- .NET's `\d` matches every Unicode digit, including Arabic-Indic digits. Highlighting makes that easy to spot. `WithAsciiDigits()` limits it to `0-9`. See [Unicode](./unicode.md).

### Brackets in the text

The Brackets format does not escape brackets that are already in the text.

```csharp
Pattern.FromRegex(@"\[\w+\]").Highlight("see [docs] now", HighlightFormat.Brackets);
// see [[docs]] now
```

The output is meant for people and for exact-string assertions. Do not parse it to get the matches back; use `Matches(text)` or [Typed extraction](./typed-extraction.md) for that.

## Using highlights in test assertions

A Brackets string shows the match positions and every group capture in one line. Comparing it with an expected string tests all of them in a single assertion, and a failing assertion shows exactly where the pattern went wrong.

```csharp
using Masterly.FluentRegex;
using Xunit;

public static class Patterns
{
    public static Pattern IsoDate { get; } = Pattern.With
        .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4)).Literal("-")
        .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2)).Literal("-")
        .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2));
}

public class IsoDatePatternTests
{
    [Theory]
    [InlineData("Due 2026-11-02.", "Due [[year:2026]-[month:11]-[day:02]].")]
    [InlineData("From 2026-01-31 to 2026-02-01", "From [[year:2026]-[month:01]-[day:31]] to [[year:2026]-[month:02]-[day:01]]")]
    [InlineData("Version 2026.11.02", "Version 2026.11.02")]
    public void IsoDate_Highlight_MarksEveryDateAndItsParts(string input, string expected)
    {
        string highlighted = Patterns.IsoDate.Highlight(input, HighlightFormat.Brackets);

        Assert.Equal(expected, highlighted);
    }
}
```

- One row checks that a match was found, where it starts and ends, and what every group captured.
- A row whose expected value equals its input, like the last one, asserts that nothing matches.
- The assertion is on a plain string, so it works with any test framework and any assertion library, for example Shouldly's `highlighted.ShouldBe(expected)`.

When the pattern misses something, the failure message points at it. This test expects `2026-11-3` to be found, but the pattern needs a two-digit day:

```csharp
[Fact]
public void IsoDate_Highlight_SingleDigitDay_IsNotFound()
{
    string highlighted = Patterns.IsoDate.Highlight("Due 2026-11-02 or 2026-11-3", HighlightFormat.Brackets);

    Assert.Equal("Due [[year:2026]-[month:11]-[day:02]] or [[year:2026]-[month:11]-[day:3]]", highlighted);
}
```

```text
Assert.Equal() Failure: Strings differ
                                       ↓ (pos 41)
Expected: ···"-[month:11]-[day:02]] or [[year:2026]-[month:11]-["···
Actual:   ···"-[month:11]-[day:02]] or 2026-11-3"
                                       ↑ (pos 41)
```

**With generated data.** Combine highlighting with the `Masterly.FluentRegex.Xunit` package to check the shape of every generated sample. For a pattern without groups, a sample the pattern fully matches highlights as one marker around the whole string:

```csharp
using Masterly.FluentRegex;
using Masterly.FluentRegex.Xunit;
using Xunit;

public static class PhonePatterns
{
    public static Pattern Phone { get; } = Pattern.With.Digit.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);
}

public class PhonePatternTests
{
    [Theory]
    [MatchingData(typeof(PhonePatterns), nameof(PhonePatterns.Phone), Count = 20, Seed = 7)]
    public void Phone_Highlight_GeneratedNumber_IsOneWholeMatch(string phoneNumber)
    {
        string highlighted = PhonePatterns.Phone.Highlight(phoneNumber, HighlightFormat.Brackets);

        Assert.Equal($"[{phoneNumber}]", highlighted);
    }
}
```

- `MatchingData` needs `Masterly.FluentRegex.Xunit`, which targets `net8.0` and `net10.0` and xUnit v3. See [xUnit integration](./xunit.md) and [Test data generation](./test-data-generation.md).
- `Highlight` itself is in the core package, so the first two examples need only the core package and your test framework.

:::tip

Keep expected strings short: one or two matches per row. A long row is hard to read when it fails. For checks across many inputs, prefer [Property testing](./property-testing.md) or [Regex coverage](./regex-coverage.md).

:::

## HTML for web pages

The Html format wraps each match in a `<mark>` element and each group capture in a `<span>`. Style them with CSS.

```csharp
date.Highlight("Due <b>2026-11-02</b>", HighlightFormat.Html);
// Due &lt;b&gt;<mark class="frx-match"><span class="frx-group frx-group-year" title="year">2026</span>-<span class="frx-group frx-group-month" title="month">11</span>-<span class="frx-group frx-group-day" title="day">02</span></mark>&lt;/b&gt;
```

| Element | Markup | Notes |
|---------|--------|-------|
| Match | `<mark class="frx-match">...</mark>` | An empty match is an empty `<mark class="frx-match"></mark>`. |
| Group | `<span class="frx-group frx-group-{label}" title="{label}">...</span>` | `{label}` is the group name, or the number for unnamed groups (`frx-group-1`). The `title` shows the label as a tooltip. |
| Text | HTML-encoded | `<`, `>`, `&`, `"` and `'` become entities, so the text cannot add markup. |

Unnamed groups, repeated groups and empty matches follow the same rules as in [Reading the markers](#reading-the-markers):

```csharp
Pattern.FromRegex(@"(\d)+").Highlight("v12", HighlightFormat.Html);
// v<mark class="frx-match"><span class="frx-group frx-group-1" title="1">1</span><span class="frx-group frx-group-1" title="1">2</span></mark>

Pattern.FromRegex("^").Highlight("ab", HighlightFormat.Html);
// <mark class="frx-match"></mark>ab
```

### Rendering untrusted text

All text, inside and outside the matches, is encoded with `WebUtility.HtmlEncode`. Group names are encoded too. You can render the output of user-supplied text without an extra encoding step.

```csharp
Pattern tag = Pattern.FromRegex(@"(?<tag><\w+>)");

string html = tag.Highlight("<script>alert('x' & 1)</script>", HighlightFormat.Html);
// <mark class="frx-match"><span class="frx-group frx-group-tag" title="tag">&lt;script&gt;</span></mark>alert(&#39;x&#39; &amp; 1)&lt;/script&gt;
```

- Because the result is already encoded, insert it as raw HTML. In Razor use `@Html.Raw(html)`; in Blazor use `@((MarkupString)html)`. Encoding it again would show the tags as text.
- `WebUtility.HtmlEncode` also writes characters from U+00A0 to U+00FF and characters outside the Basic Multilingual Plane as numeric entities: `café` becomes `caf&#233;` and an emoji becomes `&#128512;`. Browsers display them normally.
- Control characters other than tab, line feed and carriage return are shown as their Unicode control pictures in HTML, for example the escape character as `␛` (U+241B).

### A complete page

The output is a fragment, not a page. Put it in an element that keeps line breaks, because HTML collapses them by default.

```csharp
using System.IO;
using Masterly.FluentRegex;

// logText comes from your application
string logText = "2026-11-02 started\n2026-11-03 <stopped>";
string body = date.Highlight(logText, HighlightFormat.Html);

string page = $$"""
    <!DOCTYPE html>
    <html>
    <head>
    <style>
      .highlight { white-space: pre-wrap; font-family: monospace; }
      .frx-match { background: #fff3b0; }
      .frx-group { border-bottom: 2px solid #3b82f6; }
      .frx-group-year { border-color: #ef4444; }
    </style>
    </head>
    <body><div class="highlight">{{body}}</div></body>
    </html>
    """;

File.WriteAllText("dates.html", page);
```

- `white-space: pre-wrap` keeps the line break between the two log lines. Without it, they run together on one line.
- `.frx-group-year` styles only the `year` group. Add one rule per group name to give each group its own color.
- The `<stopped>` in the log text arrives as `&lt;stopped&gt;`, so it shows as text.
- The [Playground](./playground.md) renders its live preview from this same Html output.

## ANSI for terminals

The Ansi format, the default, colors the matches for a terminal. Matches get a blue background. Each group gets its own bright foreground color, and a one-line legend that names the colors follows the text.

```csharp
Console.WriteLine(date.Highlight("Due 2026-11-02"));
```

The raw output, with the escape character written as `\e`, is:

```text
Due \e[44;93m2026\e[44;97m-\e[44;92m11\e[44;97m-\e[44;96m02\e[0m
Legend: \e[44;97m match \e[0m \e[44;93m year \e[0m \e[44;92m month \e[0m \e[44;96m day \e[0m
```

Colors are assigned to the capturing groups in group-number order, and repeat after six groups. The color depends on the group's position in that order, not on the number itself, so explicitly numbered groups such as `(?<5>a)(?<9>b)` still get the first and second colors:

| Element | Code | Color |
|---------|------|-------|
| Match text outside any group | `44;97` | Bright white on blue |
| 1st, 7th, 13th, ... group | `44;93` | Bright yellow on blue |
| 2nd, 8th, ... group | `44;92` | Bright green on blue |
| 3rd, 9th, ... group | `44;96` | Bright cyan on blue |
| 4th, 10th, ... group | `44;95` | Bright magenta on blue |
| 5th, 11th, ... group | `44;91` | Bright red on blue |
| 6th, 12th, ... group | `44;33` | Yellow on blue |

- **Nested groups** take the innermost group's color. `(?<outer>a(?<inner>b)c)` on `xabc` gives `x\e[44;93ma\e[44;92mb\e[44;93mc\e[0m`: `a` and `c` in the `outer` color, `b` in the `inner` color.
- **The legend** lists `match` and then every capturing group of the pattern in number order, whether or not it matched. Groups without a name are listed by number: `group 1`, `group 2`, and `group 5` for `(?<5>a)`.
- **The legend is always there**, even when nothing matched. It is separated from the text by `Environment.NewLine`.
- **Empty matches and empty captures are invisible**, because there is no character to color. `x*` on `ab` returns `ab` followed by the legend.
- **Codes are minimal.** A code is written only where the style changes, and a reset (`\e[0m`) is written where colored text gives way to plain text.
- **Adjacent matches run together.** Two matches with no text between them have the same style, so no code separates them. `a` on `aab` gives `\e[44;97maa\e[0mb`, one colored block for two matches. Brackets shows them as `[a][a]b`.
- **Repeated colors.** With more than six groups, the 7th group has the same color as the 1st. Use the Brackets format when you need to tell many groups or adjacent matches apart.

### Escape characters in the input

Text from a log file or a user can contain terminal control sequences. The Ansi format replaces every escape character (U+001B) in the input with the visible symbol `␛` (U+241B), so the input cannot change colors, move the cursor or clear the screen.

```csharp
Pattern phone = Pattern.With.Digit.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);

string ansi = phone.Highlight("\u001b[31m555-1234");
bool injected = ansi.Contains("\u001b[31m"); // false
```

The output starts with `␛[31m` as plain text, followed by the colored match.

:::warning

The Html format also shows control characters as visible symbols. Only the Brackets format copies them unchanged, because it is meant for test assertions. Do not print Brackets output of untrusted text to a terminal if control sequences are a concern.

:::

### Falling back when output is redirected

Color codes are noise in a file or a CI log. Choose the format based on where the output goes:

```csharp
// text comes from your application
string text = "Released 2026-11-02, patched 2026-11-09.";
HighlightFormat format = Console.IsOutputRedirected ? HighlightFormat.Brackets : HighlightFormat.Ansi;

Console.WriteLine(date.Highlight(text, format));
// when redirected, prints: Released [[year:2026]-[month:11]-[day:02]], patched [[year:2026]-[month:11]-[day:09]].
```

The [command-line tool](./command-line-tool.md) makes the same choice when you do not pass `--format`.

## Highlighting from the command line

The `fluentregex` tool highlights a file, or standard input when the file is `-`:

```text
fluentregex highlight <regex> <file|-> [--format ansi|html|brackets] [--options <list>] [--ascii-digits]
```

```text
> fluentregex highlight "(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})" notes.txt --format brackets
Released [[year:2026]-[month:11]-[day:02]], patched [[year:2026]-[month:11]-[day:09]].
No date on this line.
```

| Option | What it does |
|--------|--------------|
| `--format` | `ansi`, `html` or `brackets`, case-insensitive. Without it, the tool uses `ansi` on a terminal and `brackets` when the output is redirected. |
| `--options` | Comma-separated `RegexOptions` names, such as `IgnoreCase,Multiline`, case-insensitive. An unknown name exits with code 2. |
| `--ascii-digits` | Makes `\d` match only `0-9`. |

- The whole file is highlighted as one text, so a pattern with `^` and `$` needs `--options Multiline` to work per line.
- The result is written with a final line break. When the file already ends with one, the output ends with an empty line.
- An unknown format exits with code 2 and prints `error: Unknown --format 'bold'. Use ansi, html or brackets.`. An invalid regex or a missing file also exits with code 2.

## Errors, limits and performance

| Situation | Result |
|-----------|--------|
| `text` is `null` | `ArgumentNullException` (parameter `text`). |
| `format` is not a defined `HighlightFormat` value, such as `(HighlightFormat)42` | `ArgumentOutOfRangeException` (parameter `format`), message `Unknown highlight format.` |
| The pattern has a match timeout and a match takes longer | `RegexMatchTimeoutException`, as with every other matching method. |
| The text is empty | The empty string (Brackets, Html), or a line break followed by the legend (Ansi). A pattern that can match the empty string returns `[]` (Brackets) or `<mark class="frx-match"></mark>` (Html). |

`Highlight` runs the pattern over untrusted text like any other matching call, so a pattern with catastrophic backtracking can hang it. Set a timeout and handle the exception:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

// untrustedText comes from your application
string untrustedText = new string('a', 40) + "!";
Pattern risky = Pattern.FromRegex(@"(\w+\s?)+$").WithTimeout(TimeSpan.FromMilliseconds(100));

try
{
    Console.WriteLine(risky.Highlight(untrustedText, HighlightFormat.Brackets));
}
catch (RegexMatchTimeoutException)
{
    Console.WriteLine("Highlighting timed out."); // prints: Highlighting timed out.
}
```

The compile-time analyzer flags this pattern as a ReDoS risk. See [ReDoS safety](./redos-safety.md) for how to fix it.

**Performance notes**

- Each call finds every match, collects every capture of every group, and builds the output in memory. The cost grows with the text length and with the number of captures.
- Each call also re-parses the pattern to decide how to nest groups that capture the same text. For short patterns this is negligible.
- The whole text and the whole output are held in memory. For a large log, highlight only the lines you need:

```csharp
using System;
using System.IO;
using Masterly.FluentRegex;

// logPath comes from your application
foreach (string line in File.ReadLines(logPath))
{
    if (date.IsMatch(line))
    {
        Console.WriteLine(date.Highlight(line, HighlightFormat.Brackets));
    }
}
// for a log holding "2026-11-02 started", "no date" and "2026-11-03 stopped", prints:
// [[year:2026]-[month:11]-[day:02]] started
// [[year:2026]-[month:11]-[day:03]] stopped
```

- To find matches in a large stream without loading it into memory, see [Streaming](./streaming.md).
- `Highlight` is a debugging and display tool. To process matches in code, use `Matches`, `Replace` or [Typed extraction](./typed-extraction.md) instead of parsing highlighted output.

## Related tools

Highlighting shows **what** matched. Other tools answer different questions:

| Question | Tool |
|----------|------|
| Why did the pattern match here, or fail there? | [Match tracing](./match-tracing.md) |
| What does this pattern mean in plain English? | [Validation and debugging](./validation-and-debugging.md) |
| What does the pattern look like as a picture? | [Diagrams](./diagrams.md) |
| Why is this pattern slow on this input? | [Backtracking profiler](./backtracking-profiler.md) |
| Which branches of the pattern did my tests exercise? | [Regex coverage](./regex-coverage.md) |
| Can I try this in the browser? | [Playground](./playground.md) |
