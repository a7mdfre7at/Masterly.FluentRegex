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
| `fluentregex compare <regexA> <regexB>` | Proves two regexes equivalent or prints the shortest counterexample; exits with 1 when they differ |
| `fluentregex optimize <regex>` | Simplifies the regex with rewrites that are verified not to change what it matches |
| `fluentregex learn <example>... [--not <example>...]` | Infers a regex from examples (or `--from-file <path>`, one per line) |
| `fluentregex profile <regex> <input>` | Counts backtracking steps and shows the hotspots; exits with 1 when the budget runs out |
| `fluentregex highlight <regex> <file\|->` | Shows matches and groups in a file (`--format ansi\|html\|brackets`) |
| `fluentregex coverage <regex> <file\|->` | Reports which branches and limits the lines of a file exercise |
| `fluentregex export <regex> [--to js\|json-schema\|typescript]` | Translates the regex for JavaScript, JSON Schema or a TypeScript constant |
| `fluentregex export <regex> --to postgresql\|mysql\|mariadb\|sqlserver\|sqlite [--column <sql>]` | Translates the regex for a database's own engine with the exact .NET meaning; `--column` prints a `WHERE` condition |
| `fluentregex export <regex> --to like [--dialect sqlserver\|postgresql\|mysql\|sqlite\|standard]` | Compiles the regex to SQL `LIKE` and reports whether it is proven exact or a superset |
| `fluentregex facts <regex> [--scope full\|substring]` | Prints the proven length bounds and the prefix and suffix every match must have |

Every command that takes a regex accepts `--options IgnoreCase,Multiline` to set regex options and
`--ascii-digits` to make `\d` match only 0-9.

```bash
fluentregex compare '\d+' '[0-9]+'
# Not equivalent (left is a proper superset of right). Shortest counterexample: "٠"

fluentregex export '^INV-[0-9]{4}\z' --to like --dialect sqlserver --column '[Number]'
# [Number] LIKE 'INV-[0-9][0-9][0-9][0-9]'
# exact: LIKE accepts exactly the strings the pattern accepts (proven)
```

## Use in CI

```yaml
- run: dotnet tool install --global Masterly.FluentRegex.Tool
- run: fluentregex scan src --fail-on high
```

`scan` finds regexes in `new Regex(...)`, `Regex.IsMatch/Match/Matches/Replace/Split(...)`,
`[GeneratedRegex(...)]` and `[RegularExpression(...)]`. It exits with code 1 when a finding
reaches the `--fail-on` level.
