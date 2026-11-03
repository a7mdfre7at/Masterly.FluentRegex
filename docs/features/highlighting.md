# Match highlighting

See at a glance where a pattern matches and what each group captured, in a terminal, a web page or a
test assertion.

```csharp
public string Highlight(string text, HighlightFormat format = HighlightFormat.Ansi);
public enum HighlightFormat { Ansi, Html, Brackets }
```

## Brackets: for tests and logs

```csharp
Pattern phone = Pattern.With.Digit.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);
Console.WriteLine(phone.Highlight("Call 555-1234 or 555-9876 now", HighlightFormat.Brackets));
```

```text
Call [555-1234] or [555-9876] now
```

Groups are labelled with their name, or their number when unnamed:

```csharp
Pattern date = Pattern.With
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4)).Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2)).Literal("-")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2));

Console.WriteLine(date.Highlight("Released 2026-11-02, patched 2026-11-09.", HighlightFormat.Brackets));
```

```text
Released [[year:2026]-[month:11]-[day:02]], patched [[year:2026]-[month:11]-[day:09]].
```

This makes compact, readable assertions:

```csharp
Pattern.FromRegex(@"(?<sign>[+-])?(?<digits>\d+)")
    .Highlight("-4 and 7", HighlightFormat.Brackets)
    .ShouldBe("[[sign:-][digits:4]] and [[digits:7]]");
```

Brackets already in the text are not escaped, so the output is for reading and asserting, not parsing.

## Html: for web pages

```csharp
Console.WriteLine(date.Highlight("Due <b>2026-11-02</b>", HighlightFormat.Html));
```

```html
Due &lt;b&gt;<mark class="frx-match"><span class="frx-group frx-group-year" title="year">2026</span>-<span class="frx-group frx-group-month" title="month">11</span>-<span class="frx-group frx-group-day" title="day">02</span></mark>&lt;/b&gt;
```

All text and group names are HTML-encoded, so untrusted input is safe to render. Style the output
with CSS, for example:

```css
.frx-match { background: #fff3b0; }
.frx-group { border-bottom: 2px solid #3b82f6; }
.frx-group-year { border-color: #ef4444; }
```

## Ansi: for terminals

```csharp
Console.WriteLine(date.Highlight("Due 2026-11-02"));
```

Matches get a blue background and each group a bright foreground color; nested groups take the
innermost group's color. A one-line legend follows. The raw output (with `ESC` written as `\e`) is:

```text
Due \e[44;93m2026\e[44;97m-\e[44;92m11\e[44;97m-\e[44;96m02\e[0m
Legend: \e[44;97m match \e[0m \e[44;93m year \e[0m \e[44;92m month \e[0m \e[44;96m day \e[0m
```

In `Ansi` output, escape characters in the input are shown as the visible symbol `␛` (U+241B), so
highlighted text cannot inject terminal control sequences. `Brackets` and `Html` output copy U+001B
unchanged. Unnamed groups appear in the legend as `group 1`, `group 2`.

## Details

- **Nested groups** nest: `(?<word>(\w+))` on `hi` renders `[[word:[1:hi]]]`.
- **Adjacent and repeated groups**: every capture of a repeated group is marked, so `(\d)+` on `v12`
  renders `v[[1:1][1:2]]`.
- **Groups that did not participate** are not marked: `(x)?(y)` on `y` renders `[[2:y]]`.
- **Lookaround captures** are clipped to their match. Text a lookahead captured beyond the match end
  is not marked inside the match, and a lookbehind capture before the match is dropped.
- **Empty matches** follow `Regex.Matches` exactly and render as empty markers: `a*` on `bab` renders
  `[]b[a][]b[]`. In HTML an empty match is an empty `<mark class="frx-match"></mark>`. With ANSI colors
  empty matches and empty groups are invisible, because there is no character to color.
- **RightToLeft** patterns are rendered in text order.
- `text` must not be null (`ArgumentNullException`); an undefined `HighlightFormat` value throws
  `ArgumentOutOfRangeException`.
