# FluentRegex Playground

A browser playground that runs the real Masterly.FluentRegex library in WebAssembly. Type a .NET regex,
pick options, paste test text, and every panel updates as you type:

| Panel | Shows |
|-------|-------|
| Matches | Every match with its groups; the test text is highlighted with matches and groups |
| Explain | The pattern in plain English |
| Diagram | Railroad diagram (SVG) and Mermaid source |
| Fluent C# | The fluent code that builds the same regex |
| Samples | Generated strings the pattern accepts and near misses it rejects |
| Safety | ReDoS findings, confirmed with a timed attack input |
| Trace | Which construct matched what, or exactly where matching failed |
| Profile | Backtracking steps per construct, with a heatmap |
| Coverage | Which branches and repetition limits the lines of the test text exercise |
| Compare | Proof of equivalence with another regex, or the shortest counterexample |
| Optimize | Verified, semantics-preserving rewrites |
| Export | JavaScript and JSON Schema translations with any warnings |

The pattern, options, test text and selected panel are stored in the URL fragment, so **Copy share link**
reproduces exactly what you see. Nothing is sent to a server.

## Run locally

```bash
dotnet run --project playground/Masterly.FluentRegex.Playground
```

## Deploy

`.github/workflows/playground.yml` publishes the app to GitHub Pages on every push to `master` that touches
the library or the playground. Enable Pages with source **GitHub Actions** in the repository settings first.

Matching runs with a one-second timeout, so a catastrophic pattern stops instead of freezing the tab.
