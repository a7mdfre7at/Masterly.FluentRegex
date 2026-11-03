# Optimizer: shorter patterns with identical behavior

`Pattern.Optimize()` rewrites a pattern into a simpler one that behaves **exactly** the same in
.NET: the same leftmost-first matches, the same groups with the same numbers and captures, and the
same options. It never reorders alternation branches, so priority is preserved.

```csharp
OptimizationResult result = Pattern.FromRegex("cat|car|cab").Optimize();

result.Optimized.ToString();  // "ca[trb]"
result.VerifiedEquivalent;    // true
Console.WriteLine(result);
// Optimized 'cat|car|cab' to 'ca[trb]' (proven equivalent):
//   - Merged alternation 'cat|car|cab' into 'ca[trb]'
```

`OptimizationResult` has `Original`, `Optimized`, `AppliedRewrites` (one human-readable line per
rewrite, in order), `VerifiedEquivalent`, `IsChanged` and a descriptive `ToString()`. When nothing
can be improved, `Optimized` is the original instance.

## Rewrites

| Rewrite | Example |
|---|---|
| Single-character branches become a class | `a\|b\|c` → `[abc]` |
| Common prefix of adjacent branches is factored out | `cat\|car\|cab` → `ca[trb]` |
| A later duplicate branch is removed | `foo\|bar\|foo` → `foo\|bar` |
| Quantifiers are normalized | `x{1}y{0,1}z{0,}w{1,}` → `xy?z*w+` |
| Runs of the same atom are merged, when the result is shorter | `\d\d\d-\d\d\d\d` → `\d{3}-\d{4}`, `aa*b` → `a+b` (but `\d\d` and `a+a+` stay) |
| Redundant non-capturing groups are unwrapped | `^(?:(?:a\|b)\|c)$` → `^[abc]$` |

```csharp
Pattern.FromRegex(@"\d\d\d-\d\d\d\d").Optimize().ToString();
// Optimized '\d\d\d-\d\d\d\d' to '\d{3}-\d{4}' (proven equivalent):
//   - Merged repeated atoms '\d\d\d' into '\d{3}'
//   - Merged repeated atoms '\d\d\d\d' into '\d{4}'

Pattern.FromRegex(@"^(?:(?:a|b)|c)$").Optimize().ToString();
// Optimized '^(?:(?:a|b)|c)$' to '^[abc]$' (proven equivalent):
//   - Unwrapped redundant group '(?:a|b)'
//   - Merged alternation 'a|b|c' into '[abc]'
//   - Unwrapped redundant group '(?:[abc])'

CommonPatterns.IPv4().Optimize().Optimized.ToString();
// ^(25[0-5]|2[0-4]\d|[01]?\d\d?)\.(25[0-5]|2[0-4]\d|[01]?\d\d?)\.(25[0-5]|2[0-4]\d|[01]?\d\d?)\.(25[0-5]|2[0-4]\d|[01]?\d\d?)$
```

Capture groups keep their numbers, names and contents:

```csharp
OptimizationResult dates = Pattern.FromRegex(@"(?<year>\d\d\d\d)-(?<month>\d\d)").Optimize();
dates.Optimized.ToString();                                 // (?<year>\d{4})-(?<month>\d\d)
dates.Optimized.Match("2024-05").Groups["year"].Value;      // "2024"
```

## What it will not do

Rewrites that would change behavior are never applied, even when the set of matched strings would
stay the same:

```csharp
Pattern.FromRegex("a|ab").Optimize().ToString();         // No rewrites applied to 'a|ab'.
// "ab?" matches the same strings in full, but on "ab" it finds "ab" where "a|ab" finds "a".

Pattern.FromRegex("(a)|(b)|(a)").Optimize().ToString();  // No rewrites applied to '(a)|(b)|(a)'.
// The third branch looks redundant, but removing it would renumber groups.
```

Branches are never reordered, prefixes are only factored when they consist of single-character
atoms (so no backtracking choice moves), duplicate branches are only removed when they contain no
capturing groups, alternations whose branches change inline options are left alone, and a prefix is
not factored out when that would only make the pattern longer.

## How each rewrite is verified

Each candidate rewrite is applied on its own and kept only if it passes both checks:

1. **Match behavior.** The original and rewritten regexes run on the same inputs: samples from
   `GenerateMatching` and `GenerateNonMatching` of the original plus mutated copies (embedded,
   doubled, truncated, upper-cased, with a trailing newline). For every input, every successive
   match must agree on success, index, length, and the index, length and capture list of every
   group. The group numbers and names must also be identical.
2. **Proof.** When the pattern is in the regular subset supported by
   [language algebra](algebra.md), the rewritten pattern must be proven to match exactly the same
   strings as the original.

`VerifiedEquivalent` is `true` when the original pattern is in the regular subset, so step 2 could
prove every rewrite, even when no rewrite was applied (`Pattern.FromRegex("a|ab").Optimize()` has
`IsChanged == false` and `VerifiedEquivalent == true`). For patterns outside the regular subset
(backreferences, lookbehinds, conditionals and so on) the same order-preserving rewrites are still
applied, but only checked by step 1, and `VerifiedEquivalent` is `false`:

```csharp
Pattern.FromRegex(@"(cat|car)\1").Optimize().ToString();
// Optimized '(cat|car)\1' to '(ca[tr])\1' (checked on samples, not proven):
//   - Merged alternation 'cat|car' into 'ca[tr]'
```

The optimized pattern keeps the original's options and match timeout.
