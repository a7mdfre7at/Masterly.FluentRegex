---
id: command-line-tool
title: "Command-Line Tool"
description: "The fluentregex .NET tool: scan a codebase for ReDoS, prove, optimize, explain, trace, profile, generate and export regexes from a terminal or CI."
---

`fluentregex` is a .NET tool that brings the library's analysis to your terminal. You give it a regex as plain text, and it scans C# code for risky regexes, proves two patterns equivalent, simplifies a pattern, explains and traces it, measures how it backtracks, generates test data, and translates it for JavaScript, TypeScript and SQL.

Use it when you want an answer about a regex without writing a program: while you review a pull request, in a CI step that blocks ReDoS-prone code, or in a script that keeps a generated TypeScript or SQL file in sync with a .NET pattern. It works on any regex and any C# codebase, whether or not the code uses FluentRegex.

```bash
fluentregex compare '\d+' '[0-9]+'
```

```text
Not equivalent (left is a proper superset of right). Shortest counterexample: "٠" (left accepts it, right rejects it). Both accept "0".
```

- The tool proved that the two patterns differ and found the shortest string that shows it: `٠` is the Arabic-Indic digit zero, which .NET's `\d` matches.
- The command exited with code 1, so a script or CI step can act on the answer.

:::tip[What each command calls]

Every command is a thin wrapper over a public library API, and prints the same text that API returns (for example the report's `ToString()`, `Explain()` or `ToFluentCode()`), so the library pages explain each result in depth. The tables below link to them.

:::

## Commands at a glance

| Command | What it does | Library page |
|---------|--------------|--------------|
| [`scan`](#scanning-a-codebase-scan) | Finds regex literals in C# files and reports ReDoS risks | [ReDoS Safety](./redos-safety.md) |
| [`analyze`](#analyzing-one-regex-analyze) | Reports the ReDoS risks of one regex | [ReDoS Safety](./redos-safety.md) |
| [`compare`](#proving-two-regexes-equivalent-compare) | Proves two regexes equivalent or prints the shortest counterexample | [Pattern Algebra](./pattern-algebra.md) |
| [`optimize`](#simplifying-a-regex-optimize) | Simplifies a regex with rewrites that keep its meaning | [Optimizer](./optimizer.md) |
| [`facts`](#reading-proven-facts-facts) | Prints the proven length bounds, prefix and suffix of every match | [Language Facts](./language-facts.md) |
| [`explain`](#explaining-a-regex-explain) | Explains a regex in plain English | [Validation and Debugging](./validation-and-debugging.md) |
| [`trace`](#tracing-a-match-trace) | Shows which part matched what, or where the match failed | [Match Tracing](./match-tracing.md) |
| [`diagram`](#drawing-a-diagram-diagram) | Draws a railroad diagram (SVG) or a Mermaid flowchart | [Diagrams](./diagrams.md) |
| [`highlight`](#highlighting-matches-in-a-file-highlight) | Marks matches and groups inside a text file | [Highlighting](./highlighting.md) |
| [`profile`](#profiling-backtracking-profile) | Counts backtracking steps on one input and shows the hotspots | [Backtracking Profiler](./backtracking-profiler.md) |
| [`coverage`](#measuring-coverage-coverage) | Reports which branches and limits the lines of a file exercise | [Regex Coverage](./regex-coverage.md) |
| [`generate`](#generating-test-data-generate) | Generates matching or near-miss strings | [Test Data Generation](./test-data-generation.md) |
| [`learn`](#learning-a-regex-from-examples-learn) | Infers a regex from examples | [Learning from Examples](./learning-from-examples.md) |
| [`convert`](#converting-to-fluent-code-convert) | Prints the fluent C# that builds a regex, or converts every regex in C# files | [Regex to Fluent Code](./regex-to-fluent-code.md) |
| [`export`](#exporting-to-other-engines-export) | Translates to JavaScript, JSON Schema, TypeScript, five SQL regex dialects or SQL `LIKE` | [Export](./export.md), [SQL Dialects](./sql-dialects.md), [SQL LIKE](./sql-like.md) |

Run `fluentregex --help` to print the same list with every option.

## Installing the tool

The tool ships as the NuGet package `Masterly.FluentRegex.Tool`. Install it once per machine as a global tool:

```bash
dotnet tool install --global Masterly.FluentRegex.Tool
fluentregex --version
```

```text
3.1.0
```

To pin the version per repository instead, install it as a local tool. The version is recorded in `.config/dotnet-tools.json`, which you commit, and everyone restores it with `dotnet tool restore`:

```bash
dotnet new tool-manifest            # once per repository
dotnet tool install Masterly.FluentRegex.Tool
dotnet tool run fluentregex --version
```

| Task | Command |
|------|---------|
| Update a global install | `dotnet tool update --global Masterly.FluentRegex.Tool` |
| Remove a global install | `dotnet tool uninstall --global Masterly.FluentRegex.Tool` |
| Restore local tools after a clone | `dotnet tool restore` |
| Print the library version the tool runs | `fluentregex --version` |

- The tool targets `net8.0` and rolls forward to newer major versions, so it runs on the .NET 8 runtime or any later one.
- `--version` prints the version of the FluentRegex library inside the tool, in three parts.
- The command name is `fluentregex` for both global and local installs. A local tool also runs as `dotnet fluentregex`.

## How arguments work

Every command follows the same shape: the command name, then positional values, then options in any order.

```bash
fluentregex <command> <regex> [more values] [--option value] [--flag]
```

- **Positional values** are the regex, a second regex, an input string or a file path, in the order the command expects.
- **Options** take the next argument as their value, as in `--count 5`. The value is taken as is, even when it starts with a dash, so `--count -1` passes `-1`.
- **Flags** take no value. There are four: `--confirm`, `--non-matching`, `--ascii-digits` and `--case-insensitive`.
- Options and positional values can be mixed in any order. When an option is repeated, the last value wins.

### Options every regex command accepts

All commands that take a regex, except `learn`, accept these two:

| Option | Effect |
|--------|--------|
| `--options <list>` | Comma-separated `RegexOptions` names, such as `IgnoreCase,Multiline`. Names are case-insensitive and spaces around commas are ignored. |
| `--ascii-digits` | Makes `\d` match only `0`-`9`. By default .NET's `\d` matches every Unicode decimal digit. |

```bash
fluentregex compare '\d+' '[0-9]+' --ascii-digits
```

```text
Equivalent: both patterns match exactly the same strings.
```

For `compare`, both options apply to both regexes.

### Quoting the regex

Pass the regex exactly as you would write it in a C# verbatim string, and quote it for your shell:

- In bash, zsh and PowerShell, use single quotes: `'^\d{3}-\d{4}$'`. Inside double quotes, bash would expand `$` and PowerShell would expand `$` and backticks.
- In `cmd.exe`, use double quotes.

:::warning[Things the parser does not catch]

- A value that starts with `--` followed by more characters is read as an option name. A regex such as `--x` swallows the next argument as its value, so `compare '--x' 'a'` fails with `Missing <regexA> argument.`, and when it comes last it fails with `Option '--x' needs a value.` Write it as `\-\-x` or `(?:--x)`. A bare `--` is fine.
- Unknown options are ignored. A typo such as `--cont 5` is silently skipped, and the command uses its default.
- Option names are case-insensitive (`--Count 5` works), but flag names are not. `--Confirm` is read as an option that needs a value and fails.
- Command names are case-sensitive. `Scan` is an unknown command.
- Help exists only for the whole tool: `--help`, `-h` or `help` must be the first argument. `fluentregex scan src --help` fails with `Option '--help' needs a value.`

:::

## Exit codes

Every command uses the same three exit codes, so scripts and CI steps can branch on the result.

| Code | Meaning |
|------|---------|
| `0` | Success, and nothing to report. |
| `1` | The command ran and found something. See the table below. |
| `2` | A usage or input error: a missing argument, an unknown command, option value or regex option, an invalid regex, a file that does not exist, or a construct the command cannot handle. The message goes to standard error and starts with `error:`. |

These are the commands that can return `1`:

| Command | Returns 1 when |
|---------|----------------|
| `scan` | A finding reaches the `--fail-on` level (`high` by default). |
| `analyze` | The overall risk is `High`. A `Medium` risk is printed but returns 0. |
| `compare` | The regexes are not equivalent. |
| `profile` | The step budget ran out (catastrophic backtracking). |
| `coverage` | At least one branch, optional part or upper bound was never exercised. |

All other commands return `0` on success. `trace` returns `0` even when the regex does not match, because the trace itself is the result.

```bash
fluentregex
echo $?        # 2: no command prints the help and counts as a usage error
fluentregex --help
echo $?        # 0 (so do -h and help)
fluentregex bogus
```

```text
error: Unknown command 'bogus'. Run 'fluentregex --help'.
```

A few failures are not turned into code 2. A `--count` or `--seed` value too large for a 32-bit integer, or `diagram --output` into a folder that does not exist, ends with the .NET runtime's `Unhandled exception.` message and its crash exit code (for example `-532462766` on Windows). In CI, treat any code other than `0` and `1` as a broken step.

:::note

The built-in help text says exit code 1 means "findings at or above --fail-on (analyze/scan)". That line is incomplete: `analyze` has no `--fail-on` option, and `compare`, `profile` and `coverage` also return 1, as listed above.

:::

## Output encoding

The tool always writes UTF-8. It sets the console output encoding to UTF-8 before it runs any command, because heat bars, counterexamples, explanations and highlighted text often contain non-ASCII characters. The bytes are UTF-8 without a byte order mark, with the platform's line endings (CRLF on Windows).

What you see depends on the program that reads those bytes:

| Where the output goes | Result |
|-----------------------|--------|
| Straight to a terminal | Correct. The tool switches the console to UTF-8 itself. |
| `>` in bash or `cmd.exe` | A UTF-8 file without a BOM, whatever the active code page is. |
| Captured or redirected by PowerShell 7 | Correct only when `[Console]::OutputEncoding` is UTF-8. PowerShell decodes the bytes with that encoding. |
| `>` in Windows PowerShell 5.1 | A UTF-16 file. The text is garbled unless `[Console]::OutputEncoding` is UTF-8. |

For example, `profile` prints its heat bar with block characters. With PowerShell's console encoding left on an OEM code page such as 437, the captured line is garbled:

```powershell
[Console]::OutputEncoding = [System.Text.Encoding]::GetEncoding(437)
$report = fluentregex profile '(a+)+$' 'aaaaaaaaaaaaaaaa!'
$report[1]
```

```text
Heat     ΓûâΓûêΓûêΓûâΓûâΓûâ
```

Set the encoding to UTF-8 first, and the same line reads correctly:

```powershell
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$report = fluentregex profile '(a+)+$' 'aaaaaaaaaaaaaaaa!'
$report[1]
```

```text
Heat     ▃██▃▃▃
```

- The garbling happens in PowerShell, not in the tool. The tool sent the same UTF-8 bytes both times.
- Put the `[Console]::OutputEncoding` line in your PowerShell profile to fix it for every session.
- Programs that read a redirected file, such as an editor or a CI log viewer, must open it as UTF-8. Most do by default.

## Finding ReDoS risks

### Scanning a codebase: scan

`scan` reads every C# file under a folder (or a single file), finds the regexes written as string literals, and runs the [ReDoS analysis](./redos-safety.md) on each one. It is the command to put in CI.

```bash
fluentregex scan <path> [--fail-on high|medium|none] [--confirm]
```

Take this file, `src/Validation/Legacy.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Demo.Validation;

public static partial class Legacy
{
    private static readonly Regex Email = new Regex(@"^([a-zA-Z0-9]+[._-]?)*@example\.com$", RegexOptions.IgnoreCase);

    public static bool IsSentence(string text) => Regex.IsMatch(text, @"^(\w+\s?)*$");

    public static string MaskTrailingDigits(string text) => Regex.Replace(text, @"\d+\d+$", "#", RegexOptions.Multiline);

    [GeneratedRegex(@"^[A-Z]{3}-\d{4}$")]
    public static partial Regex OrderNumber();

    public static bool IsEmail(string text) => Email.IsMatch(text);
}

public sealed class SignUpRequest
{
    [RegularExpression(@"^\+?[0-9]{7,15}$")]
    public string Phone { get; set; } = string.Empty;
}
```

```bash
fluentregex scan src
```

```text
src\Validation\Legacy.cs(8,53): warning FRX-NestedQuantifier: new Regex "^([a-zA-Z0-9]+[._-]?)*@example\.com$"
    The repetition '[a-zA-Z0-9]+' is nested inside the repetition '*'. The same text can be split between iterations in exponentially many ways, so a failing match backtracks through all of them.
    Fix: Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. For untrusted input also use WithTimeout(...) or UseNonBacktracking().
src\Validation\Legacy.cs(10,71): warning FRX-NestedQuantifier: Regex.IsMatch "^(\w+\s?)*$"
    The repetition '\w+' is nested inside the repetition '*'. The same text can be split between iterations in exponentially many ways, so a failing match backtracks through all of them.
    Fix: Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. For untrusted input also use WithTimeout(...) or UseNonBacktracking().
src\Validation\Legacy.cs(12,81): info FRX-AdjacentOverlappingQuantifiers: Regex.Replace "\d+\d+$"
    The repetitions '\d+' and '\d+' can match the same characters, so a failing match tries every way of dividing them (polynomial time).
    Fix: Merge the repetitions into one, make the first one possessive (.Possessive), or make their character sets disjoint.

Scanned 5 regex literal(s): 2 high, 1 medium risk finding(s).
```

The command exits with 1, because two findings are `High`.

- Each finding starts with `file(line,column)`, the position of the string literal, in the same shape as compiler output. The path is relative to the current directory and uses the platform's separator, so on Linux and macOS it reads `src/Validation/Legacy.cs`. A file outside the current directory is printed with the path as you gave it.
- `High` risks print as `warning`, `Medium` risks as `info`. `Low` risks and safe patterns print nothing. The rule name is `FRX-` followed by the issue kind.
- Under each finding, the tool prints the explanation and the suggested fix.
- The options in the source code are used: the `Regex.Replace` call above was analyzed with `RegexOptions.Multiline`.
- The summary line counts every literal it found, including the two safe ones.

**Where scan looks**

| Construct | Reported as |
|-----------|-------------|
| `new Regex("...")`, `new Regex("...", options)` | `new Regex` |
| `Regex.IsMatch`, `Match`, `Matches`, `Replace`, `Split`, `Count`, `EnumerateMatches`, `EnumerateSplits` with a literal pattern | `Regex.<Method>` |
| `[GeneratedRegex("...")]`, `[GeneratedRegex("...", options)]` | `[GeneratedRegex]` |
| `[RegularExpression("...")]` (data annotations) | `[RegularExpression]` |

- Regular, verbatim (`@"..."`) and raw (`"""..."""`) string literals are read. Fully qualified names such as `System.Text.RegularExpressions.Regex` and the `...Attribute` spelling of the attributes are recognised.
- Options are read when they are written as `RegexOptions.A | RegexOptions.B`. Options passed through a variable or constant are treated as `None`.
- The scan is syntactic. It skips patterns held in variables, constants or interpolated strings, target-typed `new("...")`, and any other API. It also skips FluentRegex patterns, which the [compile-time checks](./compile-time-checks.md) cover instead.
- In a folder, it reads `*.cs` files recursively, in alphabetical order, and skips any path that goes through a `bin`, `obj`, `.git` or `node_modules` folder. A path that names a single file is read as C# whatever its extension.
- A literal that is not a valid regex is reported as `error: invalid regex: ...` and counts as a `High` finding.

**Options**

| Option | Default | Effect |
|--------|---------|--------|
| `--fail-on high` | Yes | Exit with 1 when any `High` finding exists. |
| `--fail-on medium` | | Exit with 1 when any `High` or `Medium` finding exists. |
| `--fail-on none` | | Always exit with 0. Use it to report without failing the build. |
| `--confirm` | Off | Run each finding's attack input under a timeout, and mark the ones that really hang with `[CONFIRMED by timeout]`. |

`--fail-on` is case-insensitive. Any other value, such as `low`, is a usage error.

```bash
fluentregex scan src --confirm --fail-on medium
```

```text
src\Validation\Legacy.cs(8,53): warning FRX-NestedQuantifier: new Regex "^([a-zA-Z0-9]+[._-]?)*@example\.com$" [CONFIRMED by timeout]
src\Validation\Legacy.cs(10,71): warning FRX-NestedQuantifier: Regex.IsMatch "^(\w+\s?)*$" [CONFIRMED by timeout]
src\Validation\Legacy.cs(12,81): info FRX-AdjacentOverlappingQuantifiers: Regex.Replace "\d+\d+$"

Scanned 5 regex literal(s): 2 high, 1 medium risk finding(s).
```

(The explanation and fix lines under each finding are left out here.)

:::note

`--confirm` runs each attack for up to 250 ms. It turns a static warning into proof, but on a large codebase with many findings it adds up to a quarter of a second per finding.

:::

### Analyzing one regex: analyze

`analyze` runs the same analysis on one regex and prints the full report, including a crafted attack input.

```bash
fluentregex analyze <regex> [--confirm] [--options <list>] [--ascii-digits]
```

```bash
fluentregex analyze '^(\w+\s?)*$' --confirm
```

```text
Pattern: ^(\w+\s?)*$
Risk:    High

  [High] NestedQuantifier at position 1: (\w+\s?)*
    The repetition '\w+' is nested inside the repetition '*'. The same text can be split between iterations in exponentially many ways, so a failing match backtracks through all of them.
    Fix: Make the inner repetition possessive (.Possessive), wrap it in AtomicGroup(...), or remove one of the quantifiers. For untrusted input also use WithTimeout(...) or UseNonBacktracking().
    Attack input (33 chars): 00000000000000000000000000000000!
    CONFIRMED: matching did not finish within 244 ms.
```

- The exit code is 1 when the overall risk is `High`, and 0 for `Medium`, `Low` and `None`.
- Without `--confirm`, the `CONFIRMED` line is missing. The time it prints is measured, so it differs slightly from run to run.
- With `--confirm`, a finding whose attack finishes in time prints `Not confirmed: the attack input finished quickly.` instead. That is what `analyze '\d+\d+$' --confirm` prints for its `Medium` finding.
- Long attack inputs are cut off in the report, as in `Attack input (30001 chars): 0000...`.

A safe regex, and one with a `Low` risk:

```bash
fluentregex analyze '^\d{3}-\d{4}$'
```

```text
Pattern: ^\d{3}-\d{4}$
Risk:    None
No catastrophic backtracking risks found.
```

```bash
fluentregex analyze '^(\w|\d)+$'
```

```text
Pattern: ^(\w|\d)+$
Risk:    Low

  [Low] OverlappingAlternation at position 2: \w|\d
    The alternatives '\w' and '\d' overlap. .NET merges single-character alternatives into one set, so this is redundant rather than dangerous.
    Fix: Combine the alternatives into a single character set.
```

:::note

The analysis reads the pattern, not the engine you run it on. `--options NonBacktracking` does not lower the risk it reports. If you already run a pattern with `RegexOptions.NonBacktracking`, a finding is not exploitable, but the report still shows it.

:::

## Proving and simplifying

### Proving two regexes equivalent: compare

`compare` proves whether two regexes match exactly the same strings. When they do not, it prints the shortest string that tells them apart. Use it to check a refactoring before you merge it.

```bash
fluentregex compare <regexA> <regexB> [--options <list>] [--ascii-digits]
```

```bash
fluentregex compare '^(?:Mr|Mrs|Ms)\.? [A-Z][a-z]+$' '^M(?:rs?|s)\.? [A-Z][a-z]+$'
```

```text
Equivalent: both patterns match exactly the same strings.
```

A rewrite that went too far:

```bash
fluentregex compare '^(?:Mr|Mrs|Ms)\.? [A-Z][a-z]+$' '^M[rs]s?\.? [A-Z][a-z]+$'
```

```text
Not equivalent (left is a proper subset of right). Shortest counterexample: "Mss Aa" (right accepts it, left rejects it). Both accept "Mr Aa".
```

| Relation | Command | Output | Exit |
|----------|---------|--------|------|
| Equivalent | `compare '\d+' '[0-9]+' --ascii-digits` | `Equivalent: both patterns match exactly the same strings.` | 0 |
| Left is a proper subset | `compare 'ab' '^ab$'` | `Not equivalent (left is a proper subset of right). Shortest counterexample: "ab\n" (right accepts it, left rejects it). Both accept "ab".` | 1 |
| Left is a proper superset | `compare '\d+' '[0-9]+'` | `Not equivalent (left is a proper superset of right). Shortest counterexample: "٠" (left accepts it, right rejects it). Both accept "0".` | 1 |
| They overlap | `compare '^a{2,3}$' '^a{3,5}$'` | `Not equivalent (the patterns overlap). Shortest counterexample: "aa" (left accepts it, right rejects it); also "aaaa" (right accepts it, left rejects it). Both accept "aaa".` | 1 |
| Disjoint | `compare 'a' 'b'` | `Not equivalent (no string matches both). Shortest counterexample: "a" (left accepts it, right rejects it); also "b" (right accepts it, left rejects it).` | 1 |

- The comparison is about whole-string matches: the strings each regex matches from start to end. So `ab` and `^ab$` differ: the second also accepts `"ab\n"`, because `$` allows a final newline.
- Every example string is checked against the real .NET engine before it is printed.
- Only regular constructs can be compared: literals, classes, groups, alternation, quantifiers, and anchors at the very start or end (a lookahead at the very start is also accepted). Backreferences, lookbehinds, other lookarounds, atomic groups, conditionals, `\b`, `\B`, `\G`, multiline anchors and `RightToLeft` fail with exit code 2:

```bash
fluentregex compare '^(a)\1$' '^aa$'
```

```text
error: Language algebra does not support backreferences: '\1' at position 4. Only regular constructs (literals, classes, groups, alternation, quantifiers, and anchors at the very start or end) can be compared.
```

See [Pattern Algebra](./pattern-algebra.md) for the full set of supported constructs.

### Simplifying a regex: optimize

`optimize` applies rewrites that make a regex shorter or simpler without changing what it matches, and lists each rewrite.

```bash
fluentregex optimize <regex> [--options <list>] [--ascii-digits]
```

```bash
fluentregex optimize '^(?:a|b|c)(?:x)+(?:dog|dot)$'
```

```text
Optimized '^(?:a|b|c)(?:x)+(?:dog|dot)$' to '^[abc]x+do[gt]$' (proven equivalent):
  - Merged alternation 'a|b|c' into '[abc]'
  - Unwrapped redundant group '(?:[abc])'
  - Unwrapped redundant group '(?:x)'
  - Merged alternation 'dog|dot' into 'do[gt]'
  - Unwrapped redundant group '(?:do[gt])'
```

- `proven equivalent` means the result was proven to match the same strings with [pattern algebra](./pattern-algebra.md).
- For a regex outside the regular subset, such as one with a backreference or `\b`, the rewrites are still applied, and the result says `checked on samples, not proven`:

```bash
fluentregex optimize '\bcat(?:s|z)\b'
```

```text
Optimized '\bcat(?:s|z)\b' to '\bcat[sz]\b' (checked on samples, not proven):
  - Merged alternation 's|z' into '[sz]'
  - Unwrapped redundant group '(?:[sz])'
```

- When nothing can be improved, it prints `No rewrites applied to '<regex>'.`
- `optimize` always exits with 0. It does not fix ReDoS risks: `^(\w+\s?)*$` gets no rewrites. Use [`analyze`](#analyzing-one-regex-analyze) for that.

### Reading proven facts: facts

`facts` prints what every string a regex accepts has in common: the shortest and longest length, the text every match starts and ends with, and whether the empty string is accepted. Databases and indexes can rely on these facts, as described in [Language Facts](./language-facts.md).

```bash
fluentregex facts <regex> [--scope full|substring] [--options <list>] [--ascii-digits]
```

```bash
fluentregex facts '^INV-\d{4}-\d{2,3}\z'
```

```text
FullString: length 11..12, prefix "INV-", suffix "", accepts empty: false (exact)
```

| `--scope` | Facts about | Default |
|-----------|-------------|---------|
| `full` (also `fullstring`, `full-string`) | The strings the regex matches as a whole | Yes |
| `substring` (also `search`) | The strings `IsMatch` accepts, where the match can sit anywhere | |

```bash
fluentregex facts '^INV-\d{4}\.pdf\z' --scope substring
fluentregex facts 'INV-\d+' --scope substring
```

```text
Substring: length 12..12, prefix "INV-", suffix ".pdf", accepts empty: false (exact)
Substring: length 5..unbounded, prefix "", suffix "", accepts empty: false (exact)
```

- `(exact)` means the facts come from the regex's automaton. For a regex outside the regular subset, they are read from its syntax and marked `(approximate)`. Approximate facts are still safe to rely on: the prefix and suffix may be shorter than the truth, the minimum lower and the maximum `unbounded`.
- A regex ending in `$` or `\Z` has no required suffix, because both accept a trailing newline. End it with `\z` to require one. For the same reason `^(?:cat|dog)s?$` reports `length 3..5`, not `3..4`.
- A regex that can never match prints `FullString: matches nothing (exact)`, for example for `^a[^\s\S]b$`.
- `facts` never fails on a valid regex and always exits with 0.

## Understanding a regex

### Explaining a regex: explain

`explain` describes a regex step by step in plain English.

```bash
fluentregex explain '^(?<year>\d{4})-(?<month>\d{2})$'
```

```text
Pattern: ^(?<year>\d{4})-(?<month>\d{2})$

Explanation:
  - Start of line/string
  - Named capturing group 'year'
    - Digit (any Unicode decimal digit)
    - Exactly 4 times
    - End of group
  - Character '-'
  - Named capturing group 'month'
    - Digit (any Unicode decimal digit)
    - Exactly 2 times
    - End of group
  - End of line/string
```

The explanation describes `\d` as `Digit (any Unicode decimal digit)`, which is how .NET matches it. With `--ascii-digits`, the tool rewrites `\d` to `[0-9]` first, so the line becomes `Any character in: [0-9]`. Use [`compare`](#proving-two-regexes-equivalent-compare) when the exact meaning matters.

### Tracing a match: trace

`trace` runs a regex on one input and shows which part of the regex matched which text. When the match fails, it shows how far it got and what it expected instead.

```bash
fluentregex trace <regex> <input> [--options <list>] [--ascii-digits]
```

```bash
fluentregex trace '^(?<user>\w+)@(?<host>\w+)\.com$' 'jane@example.org'
```

```text
Pattern: ^(?<user>\w+)@(?<host>\w+)\.com$
Input:   "jane@example.org"
Result:  NO MATCH

Matched before the failure:
  [0..0)    ^                                ""               start of input
  [0..4)    (?<user>\w+)                     "jane"           group 'user'
    [0..4)    \w+                            "jane"           a word character, one or more times
  [4..5)    @                                "@"              the character '@'
  [5..12)   (?<host>\w+)                     "example"        group 'host'
    [5..12)   \w+                            "example"        a word character, one or more times
  [12..13)  \.                               "."              the character '.'

Failed at position 13: expected c (the character 'c')
Found:   "org"

  jane@example.org
               ^
```

- Each row shows the input range as `[start..end)`, the part of the regex, the text it matched and a description. Nested rows belong to the group above them.
- On a successful match, the `Result` line reads `MATCH at 0, length 16: "jane@example.com"` and every part is listed.
- `trace` exits with 0 whether or not the regex matches. See [Match Tracing](./match-tracing.md) for how failures are located.

### Drawing a diagram: diagram

`diagram` draws a regex as a railroad diagram in SVG, or as a Mermaid flowchart you can paste into Markdown.

```bash
fluentregex diagram <regex> [--format svg|mermaid] [--output <file>] [--options <list>] [--ascii-digits]
```

| Option | Default | Effect |
|--------|---------|--------|
| `--format svg` | Yes | A self-contained SVG document with its own styles. |
| `--format mermaid` | | A Mermaid `flowchart LR`. |
| `--output <file>` | Standard output | Writes the diagram to the file, and prints `Wrote <file>` to standard error. |

```bash
fluentregex diagram '^[a-zA-Z\d\._\-]+@[a-zA-Z\d\.\-]+\.[a-zA-Z]{2,}$' --output email.svg
```

This command writes exactly this file:

![Railroad diagram of an email pattern](/img/railroad-email.svg)

```bash
fluentregex diagram '^(?<major>\d+)\.(?<minor>\d+)(-beta)?$' --format mermaid
```

```text
flowchart LR
    start([start])
    n0(["start of input"])
    start --> n0
    subgraph g1["group 'major'"]
        direction LR
        n2["digit · 1+ times"]
        n0 --> n2
    end
    n3["#quot;.#quot;"]
    n2 --> n3
    subgraph g4["group 'minor'"]
        direction LR
        n5["digit · 1+ times"]
        n3 --> n5
    end
    subgraph g6["group #35;1 · optional"]
        direction LR
        n7["#quot;-beta#quot;"]
        n5 --> n7
    end
    n8(["end of input"])
    n7 --> n8
    finish([match])
    n8 --> finish
    classDef literal fill:#e8f1fd,stroke:#3b6fc4,color:#10233f
    classDef charclass fill:#fdf1e3,stroke:#c4802b,color:#3f2508
    classDef anchor fill:#eeeeee,stroke:#888888,color:#333333
    class n3,n7 literal
    class n2,n5 charclass
    class n0,n8 anchor
```

`--output` overwrites an existing file without asking. Any `--format` other than `svg` or `mermaid` is a usage error.

### Highlighting matches in a file: highlight

`highlight` reads a text file and marks every match, and every group inside it. Pass `-` instead of a path to read standard input.

```bash
fluentregex highlight <regex> <file|-> [--format ansi|html|brackets] [--options <list>] [--ascii-digits]
```

With this `orders.log`:

```text
Order ORD-1001 shipped on 2024-03-15.
Order ORD-2002 is pending.
```

```bash
fluentregex highlight '(?<id>ORD-\d+)' orders.log --format brackets
```

```text
Order [[id:ORD-1001]] shipped on 2024-03-15.
Order [[id:ORD-2002]] is pending.
```

```bash
echo 'call 555-1234 or 555-9876' | fluentregex highlight '(\d{3})-(\d{4})' - --format brackets
```

```text
call [[1:555]-[2:1234]] or [[1:555]-[2:9876]]
```

| `--format` | Output |
|------------|--------|
| `ansi` | Colours for a terminal, followed by a legend line that names each group's colour. |
| `html` | `<mark class="frx-match">` around each match and `<span class="frx-group frx-group-<name>" title="<name>">` around each group. |
| `brackets` | Plain text: `[...]` around each match and `[name:...]` around each group. Safe for logs and test snapshots. |

- When you omit `--format`, the tool picks `ansi` in a terminal and `brackets` when the output is redirected to a file or a pipe.
- Groups are labelled by name, or by number when they have no name.

```bash
fluentregex highlight '(?<id>ORD-\d+)' orders.log --format html
```

```text
Order <mark class="frx-match"><span class="frx-group frx-group-id" title="id">ORD-1001</span></mark> shipped on 2024-03-15.
Order <mark class="frx-match"><span class="frx-group frx-group-id" title="id">ORD-2002</span></mark> is pending.
```

## Measuring a regex

### Profiling backtracking: profile

`profile` runs a regex on one input, counts every step and backtrack, and charges each one to the part of the regex that caused it. Use it to see what a risky regex really costs, and to compare a fix with the original.

```bash
fluentregex profile <regex> <input> [--options <list>] [--ascii-digits]
```

```bash
fluentregex profile '^(\w+\s?)*$' "$(printf 'a%.0s' {1..30})!"
```

```text
Pattern  ^(\w+\s?)*$
Heat     ▁▃███▄▄▄▃▃▃
Input    31 characters
Result   gave up: the step budget of 1,000,000 ran out after 374,978 backtracks
Engine   not checked, because the step budget ran out

    Share        Steps   Backtracks  Construct
    50.0%      500,026      124,995  \w+  (at 2)
    25.0%      249,990      124,995  \s?  (at 5)
    12.5%      124,996      124,988  (\w+\s?)*  (at 1)
    12.5%      124,987            0  $  (at 10)
     0.0%            1            0  ^  (at 0)

Verdict  catastrophic backtracking, mostly in '\w+'. Make it atomic or possessive, or remove the nested or overlapping repetition.
```

The exit code is 1, because the budget ran out. The same input against the fixed, atomic version:

```bash
fluentregex profile '^(?>\w+\s?)*$' "$(printf 'a%.0s' {1..30})!"
```

```text
Pattern  ^(?>\w+\s?)*$
Heat     ▅▁▁▁███▁▁▁▁▁▁
Input    31 characters
Result   no match: 104 steps, 4 backtracks
Engine   agrees with the .NET engine

    Share        Steps   Backtracks  Construct
    61.5%           64            1  \w+  (at 4)
    30.8%           32            0  ^  (at 0)
     1.9%            2            2  (?>\w+\s?)*  (at 1)
     1.9%            2            1  \s?  (at 7)
     1.9%            2            0  (?>\w+\s?)  (at 1)

Verdict  cheap: 3.4 steps per input character.
```

- The `Heat` line has one bar per character of the regex. Taller bars did more work.
- `Engine` confirms that the profiler's result agrees with the real .NET engine. It is skipped when the budget ran out.
- The verdict is `cheap`, `moderate`, `heavy backtracking` or `catastrophic backtracking`, and names the construct that did most of the work. In the rare case where the profiler and the .NET engine disagree, the verdict says so.
- The exit code is 1 only when the 1,000,000-step budget runs out. Heavy but finite backtracking exits with 0: `\d+\d+$` on 20 digits followed by `x` reports `heavy backtracking: 232.1 steps per input character` and exits with 0, while the same regex on 200 digits followed by `x` runs out of budget and exits with 1.
- The input is one command-line argument. Use your shell to build long inputs, as above with bash's `printf`.
- `RegexOptions.RightToLeft` is not supported and fails with exit code 2.

### Measuring coverage: coverage

`coverage` treats each line of a file as one test input and reports which alternation branches, optional parts and upper repetition limits those inputs exercise. Use it to check that a set of test cases really covers a regex.

```bash
fluentregex coverage <regex> <file|-> [--options <list>] [--ascii-digits]
```

With `animals.txt` holding the lines `cat`, `cats` and `bird`:

```bash
fluentregex coverage '^(?:cat|dog)s?$' animals.txt
```

```text
Coverage for ^(?:cat|dog)s?$: 67% (2/3), 3 inputs
  ✓ branch "cat" (2 hits)
  ✗ branch "dog" was never matched
  ✓ optional part "s?" (1 hit)
```

The exit code is 1 because one branch was never matched. Add a `dog` line and the report reaches `100% (3/3)` with exit code 0.

- Pass `-` instead of a path to read the inputs from standard input.
- Each line is one input, with `\n` or `\r\n` line endings. Trailing line breaks at the end of the file are ignored, but a blank line in the middle counts as one empty input.
- Upper limits of bounded repetitions are items too: `^\d{2,4}$` has one item, `upper bound 4 of "\d{2,4}"`. Lower limits are not.
- A regex with nothing to cover prints `(no alternation branches, optional parts or bounded repetitions to cover)`, reports `100% (0/0)` and exits with 0.
- An empty file counts as one empty input, so every item stays uncovered and the exit code is 1.

## Generating code and data

### Generating test data: generate

`generate` prints strings that match a regex, or near misses that do not. Every string is checked against the real .NET engine.

```bash
fluentregex generate <regex> [--count N] [--seed N] [--non-matching] [--options <list>] [--ascii-digits]
```

```bash
fluentregex generate '^[A-Z]{3}-\d{4}$' --count 5 --seed 42 --ascii-digits
```

```text
DNG-5723
GAP-1757
XNB-5083
AUC-7605
KQA-2747
```

```bash
fluentregex generate '^[A-Z]{3}-\d{4}$' --count 5 --seed 42 --non-matching --ascii-digits
```

```text
_RHZ-0920
XNB-
B#U-4978
éDNG-5723
#IEL-7159
```

| Option | Default | Effect |
|--------|---------|--------|
| `--count N` | `10` | How many strings to print. `0` prints nothing. A negative number is a usage error. |
| `--seed N` | Random | The same seed prints the same strings. Without one, every run differs. |
| `--non-matching` | Off | Print strings the regex rejects. Most are near misses: valid strings with one character inserted, removed, replaced, duplicated or swapped. |

- The strings are distinct, so fewer than `--count` are printed when the regex has fewer matches. `^[a-c]{2}$` prints 9 strings, not 10.
- A regex that can never match, or whose constraints are too narrow to hit by sampling, prints fewer strings or none. The exit code is still 0.
- `--count` and `--seed` must be whole numbers. `--count x` fails with `The input string 'x' was not in a correct format.`
- Without `--ascii-digits`, `\d` can produce other Unicode digits:

```bash
fluentregex generate '^\d{4}$' --count 8 --seed 3
```

```text
6119
3809
8450
༨542
0846
5645
6007
7178
```

The fourth string starts with `༨`, the Tibetan digit eight.

### Learning a regex from examples: learn

`learn` infers a regex from strings it should match, and optionally strings it must reject. It prints the regex, a blank line, and the fluent C# that builds it.

```bash
fluentregex learn <example>... [--not <example>...] [--from-file <path>]
```

```bash
fluentregex learn INV-2024-001 INV-2023-117 --not INV-24-1 INV-2024-0001
```

```text
^INV-\d{4}-\d{3}$

Pattern.With
    .StartOfLine
    .Literal("INV-")
    .Digit.Repeat.Times(4)
    .Literal("-")
    .Digit.Repeat.Times(3)
    .EndOfLine
```

- Every argument before `--not` is a match. Every argument after it is a reject.
- `--from-file <path>` adds one example per line from a file, and skips blank lines. It adds to whichever list is active, so `--not --from-file rejects.txt` reads rejects. You can mix files and arguments.
- At least one match is required. A string listed as both a match and a reject is a usage error.

```bash
fluentregex learn --from-file orders.txt --not --from-file bad.txt
```

```text
^ORD-\d{4}$

Pattern.With
    .StartOfLine
    .Literal("ORD-")
    .Digit.Repeat.Times(4)
    .EndOfLine
```

:::warning[learn has its own argument parsing]

`learn` does not accept `--options` or `--ascii-digits`. Every argument other than `--not` and `--from-file` is an example, so `fluentregex learn abc abd --ascii-digits` learns `^(?:--ascii-digits|[a-z]{3})$`.

The shared parser still checks the arguments first, so an argument that starts with `--` cannot be last: `fluentregex learn abc --not` fails with `Option '--not' needs a value.`, and an example such as `--x` must be followed by another argument.

:::

See [Learning from Examples](./learning-from-examples.md) for how the inference generalises.

### Converting to fluent code: convert

`convert` prints the fluent C# that builds a regex, so you can move a string regex into the fluent API.

```bash
fluentregex convert <regex> [--options <list>] [--ascii-digits]
fluentregex convert --file <path>
```

```bash
fluentregex convert '^(?<year>\d{4})-(?<month>\d{2})$'
```

```text
Pattern.With
    .StartOfLine
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
    .Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2))
    .EndOfLine
```

The output is a C# expression you can paste as is:

```csharp
using System;
using Masterly.FluentRegex;

// Pasted from: fluentregex convert '^(?<year>\d{4})-(?<month>\d{2})$'
Pattern yearMonth = Pattern.With
    .StartOfLine
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4))
    .Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2))
    .EndOfLine;

Console.WriteLine(yearMonth);                     // ^(?<year>\d{4})-(?<month>\d{2})$
Console.WriteLine(yearMonth.IsMatch("2024-03"));  // True
Console.WriteLine(yearMonth.IsMatch("2024-3"));   // False
```

With `--options`, the output ends with `.WithOptions(...)`, which needs `using System.Text.RegularExpressions;`:

```bash
fluentregex convert '^[a-z]+$' --options IgnoreCase
```

```text
Pattern.With
    .StartOfLine
    .LowercaseLetter.Repeat.OneOrMore
    .EndOfLine
    .WithOptions(RegexOptions.IgnoreCase)
```

**Converting a whole codebase**

`convert --file` takes a file or a folder and converts every regex literal that [`scan`](#scanning-a-codebase-scan) would find, with the options written next to it. Each one is printed as a comment with its location, followed by the fluent code and a semicolon:

```bash
fluentregex convert --file src/Validation/Legacy.cs
```

```text
// src\Validation\Legacy.cs(8,53) new Regex: ^([a-zA-Z0-9]+[._-]?)*@example\.com$
Pattern.With
    .StartOfLine
    .Group(
        Pattern.With
            .Set(Pattern.With.LowercaseLetter.UppercaseLetter.Range('0', '9')).Repeat.OneOrMore
            .AnyOf("._-").Repeat.Optional).Repeat.ZeroOrMore
    .Literal("@example.com")
    .EndOfLine
    .WithOptions(RegexOptions.IgnoreCase);

// src\Validation\Legacy.cs(10,71) Regex.IsMatch: ^(\w+\s?)*$
Pattern.With
    .StartOfLine
    .Group(Pattern.With.Word.Repeat.OneOrMore.Whitespace.Repeat.Optional).Repeat.ZeroOrMore
    .EndOfLine;

// src\Validation\Legacy.cs(12,81) Regex.Replace: \d+\d+$
Pattern.With
    .Digit.Repeat.OneOrMore
    .Digit.Repeat.OneOrMore
    .EndOfLine
    .WithOptions(RegexOptions.Multiline);

// src\Validation\Legacy.cs(14,21) [GeneratedRegex]: ^[A-Z]{3}-\d{4}$
Pattern.With
    .StartOfLine
    .UppercaseLetter.Repeat.Times(3)
    .Literal("-")
    .Digit.Repeat.Times(4)
    .EndOfLine;

// src\Validation\Legacy.cs(22,24) [RegularExpression]: ^\+?[0-9]{7,15}$
Pattern.With
    .StartOfLine
    .Literal("+").Repeat.Optional
    .Range('0', '9').Repeat.Times(7, 15)
    .EndOfLine;

5 regex literal(s) converted.
```

- The options found in the source come along: the `new Regex` call keeps `RegexOptions.IgnoreCase` and the `Regex.Replace` call keeps `RegexOptions.Multiline`.
- The final count goes to standard error, so `convert --file src > converted.cs` captures only the code.
- A literal that is not a valid regex is printed as `// skipped: <reason>`, and the command goes on.
- `--file` ignores `--options` and `--ascii-digits`; each literal uses the options found in the source.

:::tip

Pasting converted code brings the [compile-time checks](./compile-time-checks.md) with it. When the email regex from `Legacy.cs` is pasted as fluent code, the build reports `warning FRX004: ReDoS risk in '([a-zA-Z0-9]+[\._\-]?)*'` on that line.

:::

## Exporting to other engines: export

`export` translates a .NET regex for another engine, keeping its .NET meaning. When the meaning cannot be kept, it fails with exit code 2 and lists every obstacle, rather than printing a regex that behaves differently.

```bash
fluentregex export <regex> [--to <target>] [--column <sql>] [--name <identifier>] [--options <list>] [--ascii-digits]
fluentregex export <regex> --to like [--dialect <dialect>] [--scope substring|full] [--case-insensitive] [--column <sql>] [--options <list>] [--ascii-digits]
```

Any other `--to` value, such as `java`, is a usage error that lists the valid targets.

| `--to` | Also accepted | Prints |
|--------|---------------|--------|
| `js` (default) | `javascript` | A JavaScript regex literal with flags |
| `json-schema` | `jsonschema` | The bare pattern for a JSON Schema `pattern` keyword |
| `typescript` | | A TypeScript module with one exported constant |
| `postgresql` | `postgres`, `pg` | A quoted PostgreSQL string literal, or a `~` condition with `--column` |
| `mysql` | | A quoted MySQL literal, or a `REGEXP_LIKE` condition with `--column` |
| `mariadb` | | A quoted MariaDB literal, or a `REGEXP` condition with `--column` |
| `sqlserver` | `mssql` | A quoted SQL Server literal, or a `REGEXP_LIKE` condition with `--column` |
| `sqlite` | | A quoted literal for SQLite through EF Core, or a `REGEXP` condition with `--column` |
| `like` | | A SQL `LIKE` pattern, with a proof of whether it is exact |

### JavaScript, JSON Schema and TypeScript

```bash
fluentregex export '^INV-[0-9]{4}\z' --to js
fluentregex export '^INV-[0-9]{4}\z' --to json-schema
```

```text
/^INV-[0-9]{4}$/u
^INV-[0-9]{4}$
```

```bash
fluentregex export '^INV-[0-9]{4}\z' --to typescript --name invoiceNumber
```

```text
// Generated by Masterly.FluentRegex. Do not edit by hand.

/**
 * .NET: ^INV-[0-9]{4}\z
 */
export const invoiceNumber = /^INV-[0-9]{4}$/u;
```

- `--name` sets the constant name for `--to typescript` and defaults to `pattern`. It must be a valid TypeScript identifier: `my-pattern` is a usage error.
- .NET's `\d` becomes `\p{Nd}` to keep its Unicode meaning. Add `--ascii-digits` to get `[0-9]`.
- `--column` has no effect on `js`, `json-schema` and `typescript`.

**Warnings**

When the translation is faithful but a detail differs, the tool prints the result and then one `warning:` line per difference. The exit code is still 0:

```bash
fluentregex export '^(?<area>[0-9]{3})-([0-9]{4})$' --to js
```

```text
/^(?<area>[0-9]{3})-([0-9]{4})(?=\n?$)/u
warning: Capture group numbers differ: .NET numbers unnamed groups before named ones, JavaScript numbers all groups left to right. Refer to groups by name.
```

- `$` became `(?=\n?$)`, because .NET's `$` also matches before a final newline.
- For `--to typescript`, warnings are written into the doc comment as `Warning:` lines instead.
- Warning lines go to standard output, after the result. When a script captures the output, take only the first line, or check for lines that start with `warning:`.

A construct with no equivalent fails:

```bash
fluentregex export '(?>a+)b' --to js
```

```text
error: The pattern cannot be converted to JavaScript without changing its meaning:
  - atomic group '(?>...)' at position 0
```

See [Export](./export.md) for every translated construct and warning.

### SQL regex dialects

Each SQL target prints a string literal in that database's syntax. With `--column`, it prints a full `WHERE` condition instead. The column text is inserted as is, so quote it the way your database expects.

```bash
fluentregex export '^INV-[0-9]{4}\z' --to postgresql --column '"Number"'
```

| `--to` | Without `--column` | With `--column '"Number"'` |
|--------|--------------------|----------------------------|
| `postgresql` | `'\AINV-[0-9]{4}\Z'` | `"Number" ~ '\AINV-[0-9]{4}\Z'` |
| `mysql` | `'(?-i)\\AINV-[0-9]{4}\\z'` | `REGEXP_LIKE("Number", '(?-i)\\AINV-[0-9]{4}\\z')` |
| `mariadb` | `'(?-i)\\AINV-[0-9]{4}\\z'` | `"Number" REGEXP '(?-i)\\AINV-[0-9]{4}\\z'` |
| `sqlserver` | `N'\AINV-[0-9]{4}\z'` | `REGEXP_LIKE("Number", N'\AINV-[0-9]{4}\z')` |
| `sqlite` | `'^INV-[0-9]{4}\z'` | `"Number" REGEXP '^INV-[0-9]{4}\z'` |

- The MySQL and MariaDB patterns start with `(?-i)`, so a case-insensitive column collation cannot change the meaning.
- The `sqlite` target is for SQLite through EF Core, whose `regexp()` function runs the .NET engine in your application. The pattern stays a .NET pattern, with its options written inline.
- `\d` without `--ascii-digits` keeps its Unicode meaning. MySQL, MariaDB and SQL Server get `\p{Nd}`, SQLite keeps `\d`, and PostgreSQL, which lacks `\p{Nd}`, gets a long class of digit ranges (`[0-9٠-٩...]`). Add `--ascii-digits` when ASCII digits are what you mean.
- Unsupported constructs fail with exit code 2, for example `export '(a)\1' --to sqlserver` reports `backreference at position 3`.

See [SQL Dialects](./sql-dialects.md) for each engine's rules, and [Entity Framework Core](./entity-framework-core.md) to run these conditions from LINQ.

### SQL LIKE

`--to like` compiles a regex to a SQL `LIKE` pattern. A `LIKE` can use an ordinary index, unlike most regex functions. The tool then proves whether the `LIKE` accepts exactly the same strings as the regex, or a superset.

```bash
fluentregex export '^INV-[0-9]{4}\z' --to like --dialect sqlserver --column '[Number]'
```

```text
[Number] LIKE N'INV-[0-9][0-9][0-9][0-9]'
exact: LIKE accepts exactly the strings the pattern accepts (proven)
```

```bash
fluentregex export '^INV-[0-9]{4}\z' --to like --dialect postgresql --column number
```

```text
number LIKE 'INV-____'
superset: LIKE accepts every string the pattern accepts and possibly more; keep the regex check
warning: A character class became '_' (any character); only SQL Server LIKE can express sets.
```

- `exact` means the `LIKE` can replace the regex.
- `superset` means the `LIKE` is a fast pre-filter: it never drops a row the regex accepts, but you must still apply the regex to the rows it returns. The `warning:` lines usually say why it is not exact. A superset caused only by `--case-insensitive` prints no warning.

| Option | Default | Effect |
|--------|---------|--------|
| `--dialect` | `standard` | `standard` (also `ansi`), `sqlserver` (`mssql`), `postgresql` (`postgres`, `pg`), `mysql` (`mariadb`) or `sqlite`. Only SQL Server `LIKE` can express character sets such as `[0-9]`. |
| `--scope` | `substring` | `substring` (also `search`) mirrors `IsMatch`, where the match can sit anywhere. `full` (also `fullstring`, `full-string`) treats the regex as a whole-string match. |
| `--case-insensitive` | Off | Says the column uses a case-insensitive collation. |
| `--column <sql>` | | Prints `<column> LIKE '...'` instead of the bare pattern. |

```bash
fluentregex export 'error' --to like --column message
fluentregex export 'error' --to like --scope full --column message
```

```text
message LIKE '%error%'
exact: LIKE accepts exactly the strings the pattern accepts (proven)
message LIKE 'error'
exact: LIKE accepts exactly the strings the pattern accepts (proven)
```

Things that make a `LIKE` a superset:

- A final `$` or `\Z`, which also accepts a trailing newline: `^abc$` becomes `name LIKE 'abc%'`. End the regex with `\z` for an exact result.
- Alternations and variable repeats, which become `%`. `^(?:cat|dog)s?$` becomes `name LIKE '%'`.
- A case-sensitive regex against `--case-insensitive`: `^abc\z` becomes `name LIKE 'abc'`, which is a superset. Add `--options IgnoreCase` to the regex and the result is exact.
- Constructs outside the regular subset, such as backreferences. They become `%` (`(a)\1` becomes `name LIKE '%a%'`), and the result is a superset by construction, without a proof.

Special characters are escaped for the dialect, for example `export '^50%_off\z' --to like --dialect sqlite --column name` prints `name LIKE '50\%\_off' ESCAPE '\'`, proven exact. See [SQL LIKE](./sql-like.md) for the complete rules.

## Using the tool in CI

The exit codes make every command usable as a CI gate. The most common gate is `scan`, which stops ReDoS-prone regexes from being merged.

### GitHub Actions

```yaml
name: regex-checks

on:
  pull_request:
  push:
    branches: [master]

jobs:
  regex:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Install fluentregex
        run: dotnet tool install --global Masterly.FluentRegex.Tool

      # Fails the job when a high-risk regex is found
      - name: Scan for ReDoS
        run: fluentregex scan src --fail-on high

      # Fails the job when the test inputs stop covering every branch
      - name: Check regex test coverage
        run: fluentregex coverage '^(?:cat|dog)s?$' test/data/animals.txt

      # Fails the job when the generated TypeScript constant is out of date
      - name: Check exported pattern
        run: |
          fluentregex export '^INV-[0-9]{4}\z' --to typescript --name invoiceNumber > web/src/patterns.ts
          git diff --exit-code web/src/patterns.ts
```

- Any runner with the .NET 8 SDK or later can install the tool. `actions/setup-dotnet` is only needed when the runner image does not have one.
- To use a version pinned in a tool manifest, replace the install step with `dotnet tool restore` and call `dotnet tool run fluentregex ...`.
- To report findings without failing the build, use `--fail-on none`.
- To make the gate stricter, use `--fail-on medium`, and add `--confirm` to mark the findings that really hang.

:::tip[Adopting scan in an existing codebase]

Start with `--fail-on none` to see what the tool finds, fix or suppress the high-risk regexes, then switch to `--fail-on high`. Findings are printed in the same `file(line,column): warning ...` shape as compiler output, so they are easy to search in a build log.

:::

### Other CI systems

Every CI system treats a non-zero exit code as a failure, so the same two commands work anywhere:

```bash
dotnet tool install --global Masterly.FluentRegex.Tool
fluentregex scan src --fail-on high
```

When a step must tell a finding apart from a broken command, check for exit code 1 (findings) versus 2 (usage or input error).

## Troubleshooting

| Message | Cause and fix |
|---------|---------------|
| `error: Missing <regex> argument.` | The command needs a positional value. The name in angle brackets says which one, such as `<input>` for `trace` or `<regexB>` for `compare`. |
| `error: Option '--x' needs a value.` | An option was last on the line without a value, a flag name had the wrong case, or the regex starts with `--`. See [Quoting the regex](#quoting-the-regex). |
| `error: Unknown regex option 'Foo'.` | `--options` takes `RegexOptions` member names, such as `IgnoreCase`. |
| `error: The regular expression is invalid: ...` | The regex does not parse in .NET. The message names the offset. |
| `error: Language algebra does not support ...` | `compare` only handles regular constructs. Remove the backreference, lookaround or atomic group, or test the difference with `generate` and `trace`. |
| `error: The pattern cannot be converted to ... without changing its meaning:` | `export` found a construct the target cannot express. The list below the message names each one. |
| `error: No such file or directory: <path>` | `scan` or `convert --file` was given a path that does not exist. |
| `error: Could not find file '<path>'.` | `highlight`, `coverage` or `learn --from-file` was given a file that does not exist. The message shows the full path. |
| `error: Option '--help' needs a value.` | Help was asked for after a command name. Run `fluentregex --help` on its own. |
| `Unhandled exception. System.OverflowException: ...` | A `--count` or `--seed` value does not fit in a 32-bit integer. The exit code is the runtime's crash code, not 2. |
| `Unhandled exception. System.IO.DirectoryNotFoundException: ...` | `diagram --output` points into a folder that does not exist. Create the folder first. |
| Garbled characters such as `ΓûâΓûê` in place of `▃█` | The tool writes UTF-8, but the reader decoded it with another code page. In PowerShell, run `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8` first. Open redirected files as UTF-8. See [Output encoding](#output-encoding). |
