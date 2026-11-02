# fluentregex

The command-line companion for [Masterly.FluentRegex](https://www.nuget.org/packages/Masterly.FluentRegex).

```bash
dotnet tool install --global Masterly.FluentRegex.Tool
```

## Commands

| Command | What it does |
|---------|--------------|
| `fluentregex convert <regex>` | Prints fluent C# code that builds the regex |
| `fluentregex convert --file <path>` | Converts every regex literal in a C# file or folder |
| `fluentregex analyze <regex> [--confirm]` | Reports catastrophic backtracking (ReDoS) risks, optionally proving them with a timed attack |
| `fluentregex scan <path> [--fail-on high\|medium\|none]` | Scans C# code for risky regex literals and fails the build when it finds them |
| `fluentregex explain <regex>` | Explains the regex in plain English |
| `fluentregex generate <regex> [--count N] [--non-matching]` | Generates matching strings or near-miss strings for tests |
| `fluentregex trace <regex> <input>` | Shows which part of the regex matched which text, or where it failed |
| `fluentregex diagram <regex> [--format svg\|mermaid] [--output file]` | Draws a railroad diagram or a Mermaid flowchart |

Every command accepts `--options IgnoreCase,Multiline` to set regex options.

## Use in CI

```yaml
- run: dotnet tool install --global Masterly.FluentRegex.Tool
- run: fluentregex scan src --fail-on high
```

`scan` finds regexes in `new Regex(...)`, `Regex.IsMatch/Match/Matches/Replace/Split(...)`,
`[GeneratedRegex(...)]` and `[RegularExpression(...)]`. It exits with code 1 when a finding
reaches the `--fail-on` level.
