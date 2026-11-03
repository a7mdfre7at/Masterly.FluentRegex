---
id: playground
title: "Browser Playground"
description: "Try every feature in your browser: twelve live panels, shareable links, and the real library running locally in WebAssembly."
---

The FluentRegex Playground is a web page that runs the real Masterly.FluentRegex library in your browser. Type a .NET regex, pick options, paste some test text, and every panel updates as you type: matches, a plain-English explanation, a railroad diagram, the equivalent fluent C# code, generated test data, a ReDoS check, a match trace, a backtracking profile, branch coverage, an equivalence proof, the optimizer, and translations to JavaScript, JSON Schema and SQL.

Open it at **[a7mdfre7at.github.io/Masterly.FluentRegex/playground/](https://a7mdfre7at.github.io/Masterly.FluentRegex/playground/)**.

Use the playground when you need to:

- try a feature before you add the package to a project,
- debug a regex someone pasted into an issue or a pull request,
- send a colleague a link that opens with your exact pattern, options and test text,
- check a pattern for ReDoS or translate it to another engine without writing any code.

:::tip[Nothing leaves your browser]

The playground is a Blazor WebAssembly app. The library runs inside your browser tab, so your pattern and test text are never sent to a server. See [Privacy](#privacy).

:::

## The workspace

The page has three areas:

| Area | What it holds |
|------|---------------|
| Top bar | **Copy share link** and a link to the GitHub repository. |
| Inputs (left) | The regex box, the option checkboxes, the test text and a live preview of the matches. |
| Panels (right) | Twelve tabs. Each tab shows one view of the pattern. |

On a screen narrower than 900 pixels, the panels move below the inputs. The first visit downloads the .NET runtime and the library, so the page takes a moment to load. After that, everything runs locally.

### The regex box

Type a regex in .NET syntax, exactly as you would pass it to `new Regex(...)` or `Pattern.FromRegex(...)`. You type the regex itself, not a C# string literal, so write `\d`, not `\\d`.

When the regex is invalid, the box gets a red outline and shows the error from .NET. Every panel shows the same error until you fix it. For example, typing `(abc` shows:

```text
The regular expression is invalid: Invalid pattern '(abc' at offset 4. Not enough )'s. (Parameter 'regex')
```

The page opens with a sample email pattern, `^(?<user>[\w.+-]+)@(?<domain>[\w-]+(?:\.[\w-]+)+)$`, with **Multiline** on and three lines of test text. The examples on this page use that sample unless they say otherwise.

### Options

Each checkbox turns one setting on or off. The pattern is rebuilt as soon as you click.

| Checkbox | Effect |
|----------|--------|
| **IgnoreCase** | `RegexOptions.IgnoreCase`: letters match in either case. |
| **Multiline** | `RegexOptions.Multiline`: `^` and `$` match at the start and end of every line. On by default. |
| **Singleline** | `RegexOptions.Singleline`: `.` also matches `\n`. |
| **ExplicitCapture** | `RegexOptions.ExplicitCapture`: only named groups capture. |
| **IgnorePatternWhitespace** | `RegexOptions.IgnorePatternWhitespace`: spaces in the regex are ignored and `#` starts a comment. |
| **CultureInvariant** | `RegexOptions.CultureInvariant`: case-insensitive matching ignores the current culture. |
| **ASCII digits only** | Calls `WithDigits(DigitMode.Ascii)`, so `\d` matches only `0`-`9`. |

By default, .NET's `\d` matches every Unicode decimal digit, such as `٢` and `९`. Hover over the checkbox to see this reminder. With **ASCII digits only** checked, the playground rewrites every `\d` to `[0-9]` and every `\D` to `[^0-9]` before it does anything else, inside sets too. The rewritten text is what the panels see, so Explain, Fluent C# and Export all show `[0-9]`. For example, `\d\D[\d\w]\p{Nd}` becomes `[0-9][^0-9][0-9\w]\p{Nd}`:

- `\w` and `\p{Nd}` are left as written, so they still match Unicode digits.
- With `RegexOptions.ECMAScript`, which has no checkbox but can come from a [share link](#share-links), `\d` already means `0`-`9`, so nothing is rewritten.

See [Unicode](./unicode.md) for more on digit modes.

:::note

Multiline is on by default because the test text usually holds one sample per line. Turn it off when the pattern must match the whole input, and remember that some panels need it off. See [Compare](#compare).

:::

### Test text and the preview

Paste the text to test in the **Test text** box. Below it, the preview shows the same text with every match highlighted and every captured group marked inside it. Hover over a group to see its name.

The preview uses the library's HTML highlighter, which encodes all text, so markup in your test text is shown as text and never runs. See [Highlighting](./highlighting.md).

## The panels

Click a tab to switch panels. Only the selected panel is computed, so heavy panels such as Safety and Compare cost nothing until you open them. Each panel is computed once per edit and then reused.

| Panel | Shows | Library feature |
|-------|-------|-----------------|
| [Matches](#matches) | Every match with its position and groups. | [Matching](./matching.md) |
| [Explain](#explain) | The pattern in plain English. | [Validation and Debugging](./validation-and-debugging.md) |
| [Diagram](#diagram) | A railroad diagram, plus the Mermaid source. | [Diagrams](./diagrams.md) |
| [Fluent C#](#fluent-c) | The fluent code that builds the same regex. | [Regex to Fluent Code](./regex-to-fluent-code.md) |
| [Samples](#samples) | Generated strings the pattern accepts, and near misses it rejects. | [Test Data Generation](./test-data-generation.md) |
| [Safety](#safety) | ReDoS findings, confirmed with a timed attack input. | [ReDoS Safety](./redos-safety.md) |
| [Trace](#trace) | Which construct matched what, or exactly where matching failed. | [Match Tracing](./match-tracing.md) |
| [Profile](#profile) | Backtracking steps per construct, with a heatmap. | [Backtracking Profiler](./backtracking-profiler.md) |
| [Coverage](#coverage) | Which branches and optional parts the test text exercises. | [Regex Coverage](./regex-coverage.md) |
| [Compare](#compare) | A proof that two regexes are equivalent, or the shortest counterexample. | [Pattern Algebra](./pattern-algebra.md) |
| [Optimize](#optimize) | Verified, meaning-preserving rewrites. | [Optimizer](./optimizer.md) |
| [Export](#export) | JavaScript, JSON Schema, SQL and LIKE translations, plus language facts. | [Export](./export.md), [SQL Dialects](./sql-dialects.md), [SQL LIKE](./sql-like.md) |

Every panel handles its own errors. When a feature does not support your pattern, that panel shows the reason and the other panels keep working.

### Matches

The Matches panel runs the regex over the whole test text. It shows how many matches there are, and a card for each one with the matched text, its position and every group that took part in the match.

For the sample, the panel finds two matches. In short, the cards read:

```text
2 match(es)
ahmad@example.com     at 0
  user    ahmad
  domain  example.com
support@masterly.dev  at 31
  user    support
  domain  masterly.dev
```

- Positions count characters from the start of the test text. A line break counts as one character.
- Groups that did not take part in a match are left out. Unnamed groups appear under their number.
- The panel lists at most 500 matches, and the count stops at 500 too. Use a shorter test text if you need to see later matches.

### Explain

The Explain panel describes the pattern line by line in plain English, the same text `pattern.Explain()` returns. Nested lines show what is inside a group.

```text
Pattern: ^(?<user>[\w.+-]+)@(?<domain>[\w-]+(?:\.[\w-]+)+)$

Explanation:
  - Start of line/string
  - Named capturing group 'user'
    - Any character in: [\w.+-]
    - One or more
    - End of group
  - Character '@'
  - Named capturing group 'domain'
    - Any character in: [\w-]
    - One or more
    - Non-capturing group
      - Escaped literal '.'
      - Any character in: [\w-]
      - One or more
      - End of group
    - One or more
    - End of group
  - End of line/string
```

### Diagram

The Diagram panel draws the pattern as a railroad diagram (`ToRailroadSvg()`). Read it from left to right: every path from the start to the end spells out text the pattern matches.

Open **Mermaid source** below the diagram to get the same structure as a Mermaid flowchart (`ToMermaid()`). Paste it into a GitHub issue, a pull request or any Markdown file that renders Mermaid.

### Fluent C#

The Fluent C# panel turns the regex into fluent builder code (`ToFluentCode()`). Copy it into your project to replace a hard-to-read regex string with code that names each part.

For the sample, the panel shows:

```csharp
Pattern.With
    .StartOfLine
    .NamedGroup("user", Pattern.With.Set(Pattern.With.Word.Literal(".+-")).Repeat.OneOrMore)
    .Literal("@")
    .NamedGroup(
        "domain",
        Pattern.With
            .Set(Pattern.With.Word.Literal("-")).Repeat.OneOrMore
            .NonCapturingGroup(
                Pattern.With
                    .Literal(".")
                    .Set(Pattern.With.Word.Literal("-")).Repeat.OneOrMore).Repeat.OneOrMore)
    .EndOfLine
    .WithOptions(RegexOptions.Multiline)
    .WithTimeout(TimeSpan.FromMilliseconds(1000))
```

- The code needs `using System;`, `using System.Text.RegularExpressions;` and `using Masterly.FluentRegex;`.
- The options you checked appear as `.WithOptions(...)`.
- The last line is the playground's own one-second [timeout](#timeouts-and-catastrophic-patterns). Keep it, change it to the timeout your application needs, or remove it.
- The generated code matches exactly what the regex matches, but it may escape characters differently. The code above builds `^(?<user>[\w\.\+\-]+)@(?<domain>[\w\-]+(?:\.[\w\-]+)+)$`.
- With **ASCII digits only** checked, digits appear as `.Range('0', '9')`.

### Samples

The Samples panel shows two lists of ten generated strings:

- **Matching** strings that the pattern accepts (`GenerateMatching`).
- **Not matching** near misses that the pattern rejects (`GenerateNonMatching`). They are small changes to matching strings, so they sit right next to the pattern's boundaries.

The playground always uses the same seed, so the same pattern always shows the same samples. Control characters are shown as `\r`, `\n` and `\t`, so you can see them.

For `^\d{3}-\d{4}$` with **ASCII digits only** checked and **Multiline** off, the panel shows:

| List | Samples |
|------|---------|
| Matching | `174-9629`, `667-0313`, `870-8059`, `364-3161`, `040-2338`, `219-7134`, `308-7024`, `133-3141`, `836-6510`, `029-8160` |
| Not matching | `828-538`, `308-724`, `2016-2526`, `5335-9083`, `17-49629`, `Z029-8160`, `042-251`, `93`, `0`, `400-:0041` |

:::tip

Paste the near misses into the test text to check that none of them match. They make good negative test cases. To generate them in your own tests, see [Test Data Generation](./test-data-generation.md).

:::

### Safety

The Safety panel checks the pattern for catastrophic backtracking (ReDoS) with `AnalyzeSafety(confirm: true)`. The top line shows the overall risk, in green when the pattern is safe and in red when it is not. Below it, each finding shows:

- the kind of problem, its position in the regex and the risky fragment, with its risk level,
- what goes wrong,
- how to fix it,
- **Confirmed: the attack input timed out.** when the playground ran a crafted attack input against the pattern and the match did not finish within 250 ms.

The sample email pattern shows `Risk: None`. For `^(a+)+$`, the panel shows one confirmed finding:

```text
Risk: High
NestedQuantifier at 1: (a+)+ (High)
The repetition 'a+' is nested inside the repetition '+'. The same text can be split between iterations in exponentially many ways, so a failing match backtracks through all of them.
Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. For untrusted input also use WithTimeout(...) or UseNonBacktracking().
Confirmed: the attack input timed out.
```

Each confirmation can take up to 250 ms, so the Safety panel can take a moment on a pattern with several findings.

### Trace

The Trace panel matches the pattern against one line of input and shows which part of the pattern matched which part of the text. When the match fails, it shows how far matching got and exactly where it stopped.

Type the input in the **Input to trace** box. When the page loads, the box holds the first line of the test text. After that it keeps its own value: editing the test text does not change it.

On a match, the panel shows `Matched ahmad@example.com at 0` in green, followed by the full trace:

```text
Pattern: ^(?<user>[\w.+-]+)@(?<domain>[\w-]+(?:\.[\w-]+)+)$
Input:   "ahmad@example.com"
Result:  MATCH at 0, length 17: "ahmad@example.com"

  [0..0)    ^                                ""               start of line
  [0..5)    (?<user>[\w.+-]+)                "ahmad"          group 'user'
    [0..5)    [\w.+-]+                       "ahmad"          one of [\w . + -], one or more times
  [5..6)    @                                "@"              the character '@'
  [6..17)   (?<domain>[\w-]+(?:\.[\w-]+)+)   "example.com"    group 'domain'
    [6..13)   [\w-]+                         "example"        one of [\w -], one or more times
    [13..17)  (?:\.[\w-]+)+                  ".com"           group, one or more times
      [13..17)  (?:\.[\w-]+)                 ".com"           group
        [13..14)  \.                         "."              the character '.'
        [14..17)  [\w-]+                     "com"            one of [\w -], one or more times
  [17..17)  $                                ""               end of line
```

For `not an email`, the panel shows `No match. the character '@' at position 3, found " an email"` in red. The trace below it lists what matched before the failure and points at the position:

```text
Failed at position 3: expected @ (the character '@')
Found:   " an email"

  not an email
     ^
```

### Profile

The Profile panel counts the work a backtracking engine does to match one line of input, and shows which constructs cost the most. Its **Input to profile** box shares its value with **Input to trace**, so switching between the two panels keeps the same input.

The top line shows the total steps and backtracks. For `ahmad@example.com` it shows `43 steps, 4 backtracks` in green. Below it, the full report shows a heatmap under the pattern and a table of hotspots:

```text
Pattern  ^(?<user>[\w.+-]+)@(?<domain>[\w-]+(?:\.[\w-]+)+)$
Heat     ▁        ▆▆▆▆▆▆▆▆ ▁          ██████▂▂▂▂▂▅▅▅▅▅▅▂▂ ▁
Input    17 characters
Result   match at 0, length 17: 43 steps, 4 backtracks
Engine   agrees with the .NET engine

    Share        Steps   Backtracks  Construct
    37.2%           16            1  [\w-]+  (at 29)
    27.9%           12            1  [\w.+-]+  (at 9)
    18.6%            8            1  [\w-]+  (at 40)
     4.7%            2            1  (?:\.[\w-]+)+  (at 35)
     4.7%            2            0  \.  (at 38)

Verdict  cheap: 2.5 steps per input character.
```

The profiler stops after 1,000,000 steps, so it returns quickly even for a catastrophic pattern. When it runs out, the top line turns red. For `^(a+)+$` and 34 `a` characters followed by `!`, the report ends with:

```text
Result   gave up: the step budget of 1,000,000 ran out after 333,310 backtracks
Verdict  catastrophic backtracking, mostly in 'a+'. Make it atomic or possessive, or remove the nested or overlapping repetition.
```

The profiler does not support conditionals, balancing groups, the `\G` anchor or `RightToLeft`. For those, the panel shows a message that names the construct.

### Coverage

The Coverage panel treats each line of the test text as one input and reports which decision points of the pattern those inputs exercised: each branch of an alternation, each optional part and each bounded repetition. Every match in a line counts.

For `^(?:cat|dog)s?$` with **Multiline** on and the test text `cat` and `cats` on two lines, the panel shows:

```text
Coverage for ^(?:cat|dog)s?$: 67% (2/3), 2 inputs
  ✓ branch "cat" (2 hits)
  ✗ branch "dog" was never matched
  ✓ optional part "s?" (1 hit)
```

Add a line such as `dog` and the report goes to 100%. A pattern with nothing to cover, such as the sample email pattern, reports `100% (0/0)` and says there are no alternation branches, optional parts or bounded repetitions to cover.

### Compare

The Compare panel proves whether your pattern and a second regex match exactly the same strings. Type the second regex in the **Compare with (same options)** box. It uses the same options and digit mode as your pattern. When the page loads, the box holds a built-in example, `^[\w.+-]+@[\w-]+\.[\w.-]+$`.

The top line names the relation between the two languages, your pattern on the left and the second regex on the right:

| Relation | Meaning |
|----------|---------|
| `Equivalent` | Both match exactly the same strings. Shown in green. |
| `ProperSubset` | Every string your pattern matches, the second one matches too, but not the other way round. |
| `ProperSuperset` | The other way round: your pattern matches everything the second one matches, and more. |
| `Overlapping` | Each matches some strings the other rejects, and both match some strings. |
| `Disjoint` | No string matches both. |

Every relation except `Equivalent` is shown in red. Below it, the panel explains the result and, when the two differ, gives the shortest string that tells them apart. For `\A(?:ab|a)c\z` and `\Aa+c\z`, with **Multiline** off, the top line reads `Overlapping`, followed by:

```text
Not equivalent (the patterns overlap). Shortest counterexample: "abc" (left accepts it, right rejects it); also "aac" (right accepts it, left rejects it). Both accept "ac".
```

Change the second regex to `\Aab?c\z` and the panel shows `Equivalent`, followed by `Equivalent: both patterns match exactly the same strings.`

When the second regex is invalid, the panel shows the error from .NET, in the same form as the regex box.

:::warning[Turn Multiline off to compare]

The proof works on the regular subset of .NET regex: literals, classes, groups, alternation, quantifiers, and anchors at the very start or end. It does not support `^` with Multiline. With the default settings, the Compare panel shows this error:

```text
Language algebra does not support '^' with RegexOptions.Multiline (use \A instead): '^' at position 0. Only regular constructs (literals, classes, groups, alternation, quantifiers, and anchors at the very start or end) can be compared.
```

Uncheck **Multiline**, or anchor both patterns with `\A` and `\z`. Lookarounds and backreferences cannot be compared either. See [Pattern Algebra](./pattern-algebra.md).

:::

### Optimize

The Optimize panel runs the verified optimizer and shows the optimized regex and the list of rewrites it applied.

- **Proven equivalent** (green) means the optimizer proved that the new regex matches exactly the same strings as yours.
- **Checked on samples (pattern is outside the provable subset)** means the pattern uses something the proof does not support, such as `^` with Multiline. The optimizer then only accepts rewrites that agree with your pattern on generated samples.

For `\A(?:cat|car|cab)\z`, the panel shows **Proven equivalent**, the regex `\Aca[trb]\z` and these rewrites:

```text
Merged alternation 'cat|car|cab' into 'ca[trb]'
Unwrapped redundant group '(?:ca[trb])'
```

The sample email pattern has nothing to rewrite, so the panel shows it unchanged with an empty list. Because the sample uses `^` with Multiline, its label reads **Checked on samples (pattern is outside the provable subset)**.

### Export

The Export panel translates the pattern for other engines. It has five parts, from top to bottom.

**JavaScript** shows a regex literal you can paste into JavaScript or TypeScript, with a warning for each behavior that differs from .NET. .NET's `\w` and `\d` match Unicode, so the translation spells that out. For the sample:

```text
/(?<=^|\n)(?<user>[\p{L}\p{Mn}\p{Nd}\p{Pc}.+\-]+)@(?<domain>[\p{L}\p{Mn}\p{Nd}\p{Pc}\-]+(?:\.[\p{L}\p{Mn}\p{Nd}\p{Pc}\-]+)+)(?=\n|$)/u
```

**JSON Schema / OpenAPI** shows the same pattern without slashes and flags, ready for a `pattern` keyword.

**SQL** shows a WHERE condition with the exact .NET meaning, over a column named `value`, with a warning for each behavior the database cannot match. Pick the database in the **Database** list. The default is PostgreSQL. For `^\d{3}-\d{4}$` with **ASCII digits only** checked and **Multiline** off:

| Database | Condition |
|----------|-----------|
| PostgreSQL | `value ~ '\A[0-9]{3}-[0-9]{4}(?=\n?\Z)'` |
| MySQL | `REGEXP_LIKE(value, '(?-i)\\A[0-9]{3}-[0-9]{4}(?=\\n?\\z)')` |
| MariaDB | `value REGEXP '(?-i)\\A[0-9]{3}-[0-9]{4}(?=\\n?\\z)'` |
| SQL Server 2025 | `REGEXP_LIKE(value, N'\A[0-9]{3}-[0-9]{4}\n?\z')` |
| SQLite (EF Core) | `value REGEXP '^[0-9]{3}-[0-9]{4}$'` |

The translation keeps .NET's rule that `$` also matches before a final newline, which is why most conditions end with an optional `\n`. Use this condition in raw SQL or Dapper. With EF Core, see [Entity Framework Core](./entity-framework-core.md).

**LIKE** compiles the pattern to a `LIKE` condition for the same database, for databases without regex support or as an index-friendly prefilter. The label tells you how close it is:

- **Proven exact**: the `LIKE` condition keeps exactly the rows the regex keeps.
- **Proven superset: keeps every match, may keep more**: run the regex condition afterwards to remove the extra rows.

For the same pattern on PostgreSQL, the panel shows `value LIKE '___-____%'` as a superset, with a warning for each loss. Only SQL Server's `LIKE` can express character sets, so on SQL Server it shows `value LIKE N'[0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]%'`. End the pattern with `\z` instead of `$` to remove the trailing `%`. See [SQL LIKE](./sql-like.md).

:::note

When a pattern is outside the regular subset, such as the sample with Multiline on, the label still reads "Proven superset". The warning under it is more precise: the `LIKE` pattern is a superset by construction, but the algebra could not prove it.

:::

**Language facts** shows what every string the pattern finds a match in has in common: its length range, a required prefix and suffix, and whether it accepts the empty string. For `^\d{3}-\d{4}$` with ASCII digits:

```text
Substring: length 8..9, prefix "", suffix "", accepts empty: false (exact)
```

The `8..9` comes from the optional newline that `$` allows. The word `(exact)` or `(approximate)` tells you whether the facts were proven or estimated. See [Language Facts](./language-facts.md).

## Timeouts and catastrophic patterns

The playground builds every pattern with a one-second match timeout. When a match runs longer than that, it is stopped and the panel shows:

```text
Matching took longer than 1 s and was stopped. Check the Safety panel for catastrophic backtracking.
```

- The timeout applies to everything that runs the regex on your text: Matches, Trace, Coverage and the preview. When the preview times out, it is left empty.
- Other panels keep working. Explain, Diagram and Safety do not run the regex on your text, so they still show results for a catastrophic pattern.
- Profile counts steps in its own interpreter and stops after 1,000,000 steps, as described in [Profile](#profile). It compares its result with the .NET engine only when it finishes within that budget.
- Safety confirmations use their own 250 ms limit per finding.
- The timeout stops the match, but the tab still waits for it. Each timed-out panel can pause the page for about a second.

Try it: enter `^(a+)+$`, turn Multiline off, and paste 34 `a` characters followed by `!` into the test text. Matches stops after a second and the preview stays empty. Safety shows a confirmed high risk. Paste the same text into **Input to trace** too, because that box does not follow the test text: then Trace stops after a second and Profile reports that the step budget ran out.

## Share links

The playground keeps its state in the URL fragment, the part after `#`, and updates it as you type. Click **Copy share link** to copy the full URL to the clipboard. The button reads **Link copied** until you next change the regex, the options or the test text. Anyone who opens the link sees the same pattern, options, test text and panel.

The fragment has these keys:

| Key | Holds | Encoding |
|-----|-------|----------|
| `p` | The regex. | UTF-8, then base64url without `=` padding. |
| `o` | The regex options. | The `RegexOptions` value as an integer. |
| `t` | The test text. | UTF-8, then base64url without `=` padding. |
| `tab` | The selected panel: `matches`, `explain`, `diagram`, `code`, `samples`, `safety`, `trace`, `profile`, `coverage`, `compare`, `optimize` or `export`. | URL-escaped text. |
| `ad` | `1` when **ASCII digits only** is checked. Left out otherwise. | Literal `1`. |

The `o` values for the checkboxes are `IgnoreCase` = 1, `Multiline` = 2, `ExplicitCapture` = 4, `Singleline` = 16, `IgnorePatternWhitespace` = 32 and `CultureInvariant` = 512. Add them together to combine options: `o=3` is IgnoreCase and Multiline.

**What a link does not hold**

The inputs inside panels are not part of the link: the Trace and Profile input, the Compare regex and the selected database. When someone opens your link, the Trace and Profile input starts as the first line of the test text, Compare starts with its built-in example, and Export shows PostgreSQL. Put the input you want to trace on the first line of the test text before you share.

**How links are read**

- A missing key keeps its default. A link with no fragment opens the sample.
- A `p` or `t` value that is not valid base64url is ignored, and the default is kept.
- An `o` value that is not an integer is ignored. An integer is used as it is, so a link can turn on options that have no checkbox. When the combination is invalid, the regex box shows the error from .NET.
- When a key appears twice, the last value wins.
- An unknown `tab` value selects no panel. Click a tab to continue.

The playground updates the address with `history.replaceState`, so editing does not add entries to your browser history. The Back button leaves the playground instead of undoing an edit.

### Building a share link in code

Because the format is simple, you can create links from your own code. For example, link to the playground from a failing test, a code review bot or your own docs:

```csharp
using System;
using System.Text;
using System.Text.RegularExpressions;

public static class PlaygroundLinks
{
    private const string PlaygroundUrl = "https://a7mdfre7at.github.io/Masterly.FluentRegex/playground/";

    public static string Create(string regex, RegexOptions options, string testText) =>
        $"{PlaygroundUrl}#p={Encode(regex)}&o={(int)options}&t={Encode(testText)}&tab=matches";

    // Base64url without padding, the same encoding the playground writes
    private static string Encode(string text) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
```

```csharp
string link = PlaygroundLinks.Create(@"^\d{3}-\d{4}$", RegexOptions.Multiline, "555-1234\n555-12x4");
Console.WriteLine(link);
// prints: https://a7mdfre7at.github.io/Masterly.FluentRegex/playground/#p=XlxkezN9LVxkezR9JA&o=2&t=NTU1LTEyMzQKNTU1LTEyeDQ&tab=matches
```

- This is exactly the fragment the playground itself writes for the same state, so the link round-trips.
- Change `tab=matches` to open another panel, for example `tab=safety`.
- Append `&ad=1` to check **ASCII digits only**.

## Privacy

The playground runs entirely in your browser:

- The library is compiled to WebAssembly and runs inside your tab. Matching, analysis and translation all happen on your machine.
- The playground makes no network calls with your data. The only downloads are the app's own files when the page loads.
- Browsers do not send the URL fragment to the server, so even the request for the page does not include your pattern or test text.
- **Copy share link** writes to your clipboard only.

:::warning[A share link contains your text]

The link itself holds your regex and test text. Base64url is an encoding, not encryption: anyone with the link can read them. Do not put passwords, tokens or personal data in the test text of a link you share.

:::

## Running it locally

The playground lives in `playground/Masterly.FluentRegex.Playground` in the repository. It targets .NET 10, so you need the .NET 10 SDK. From the repository root:

```bash
dotnet run --project playground/Masterly.FluentRegex.Playground
```

The default launch profile serves the app at `http://localhost:5137` and opens your browser. Use `--launch-profile https` to serve it at `https://localhost:7156` as well.

- The app references the library project in `src/Masterly.FluentRegex`, not the NuGet package. Run it after you change the library to see your change in every panel.
- The app is not part of the main solution, so building the solution does not build it. The `test/Masterly.FluentRegex.Playground.Tests` project in the solution compiles the playground's `PlaygroundState.cs` and `PatternWorkbench.cs` directly and tests the share-link handling and every panel's logic.
- The app loads full ICU globalization data, because the library's culture-sensitive parsing needs it. This makes the first download larger.
- **Copy share link** uses the browser's clipboard API, which works only on `https` pages and on `localhost`.

To publish a static copy, run:

```bash
dotnet publish playground/Masterly.FluentRegex.Playground -c Release -o publish
```

The `publish/wwwroot` folder holds plain static files that any web server can host. When you serve them from a sub-path, change `<base href="/" />` in `index.html` to that path, for example `<base href="/Masterly.FluentRegex/playground/" />`. The repository's docs workflow does this when it deploys the playground next to this documentation site.

## Taking your work back to code

Every panel calls a public library method, so anything you see in the playground you can reproduce in code. The playground builds its pattern like this:

```csharp
using System;
using System.Text.RegularExpressions;
using Masterly.FluentRegex;

// The pattern as the playground builds it: regex box, option checkboxes, digit mode and timeout
Pattern pattern = Pattern.FromRegex(@"^(?<user>[\w.+-]+)@(?<domain>[\w-]+(?:\.[\w-]+)+)$", RegexOptions.Multiline)
    .WithDigits(DigitMode.Unicode)            // "ASCII digits only" is unchecked
    .WithTimeout(TimeSpan.FromSeconds(1));    // the playground's matching timeout

string testText = "ahmad@example.com\nnot an email\nsupport@masterly.dev";

foreach (Match match in pattern.ToRegex().Matches(testText))
{
    Console.WriteLine($"{match.Value} at {match.Index}, user = {match.Groups["user"].Value}");
}
// prints: ahmad@example.com at 0, user = ahmad
// prints: support@masterly.dev at 31, user = support
```

Each panel then makes one call on that pattern:

| Panel | Call |
|-------|------|
| Preview | `pattern.Highlight(testText, HighlightFormat.Html)` |
| Matches | `pattern.ToRegex().Matches(testText)` |
| Explain | `pattern.Explain()` |
| Diagram | `pattern.ToRailroadSvg()` and `pattern.ToMermaid()` |
| Fluent C# | `pattern.ToFluentCode()` |
| Samples | `pattern.GenerateMatching(10, 1)` and `pattern.GenerateNonMatching(10, 1)` |
| Safety | `pattern.AnalyzeSafety(confirm: true)` |
| Trace | `pattern.Trace(input)` |
| Profile | `pattern.Profile(input)` |
| Coverage | `pattern.MeasureCoverage(lines)`, one line of the test text per input |
| Compare | `Pattern.Compare(pattern, Pattern.FromRegex(otherRegex, options).WithDigits(digitMode))` |
| Optimize | `pattern.Optimize()` |
| Export | `pattern.ToFlavor(RegexFlavor.JavaScript)`, `ToFlavor(RegexFlavor.JsonSchema)`, `ToSqlCondition("value", flavor)` with the warnings of `ToFlavor(flavor)`, `ToLike(dialect)` and `GetLanguageFacts(MatchScope.Substring)` |

For **LIKE**, MySQL and MariaDB both use `LikeDialect.MySql`, PostgreSQL uses `LikeDialect.PostgreSql`, SQL Server uses `LikeDialect.SqlServer` and SQLite uses `LikeDialect.Sqlite`. The panel shows `like.ToSql("value")`.

Every call and every result type, such as `SafetyReport`, `MatchTrace` and `LikeConversion`, is in the `Masterly.FluentRegex` namespace. For the details of each call, follow the links in [the panel table](#the-panels).

:::tip

The command-line tool runs many of the same analyses from a terminal or a CI job. See [Command-Line Tool](./command-line-tool.md).

:::
