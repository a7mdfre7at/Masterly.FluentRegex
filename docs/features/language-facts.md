# Language facts: what every accepted string has in common

`GetLanguageFacts` reads, from a pattern's minimal automaton, the facts that hold for every string it
accepts: the shortest and longest accepted lengths, the text every accepted string starts and ends with,
and whether the empty string (or nothing at all) is accepted. They are sound by construction, so a query
planner, an index chooser or a LIKE translation can rely on them without rejecting a string the pattern
accepts.

```csharp
LanguageFacts facts = Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z").GetLanguageFacts();

facts.MinLength;       // 13
facts.MaxLength;       // 13
facts.RequiredPrefix;  // "INV-"
facts.RequiredSuffix;  // ""   (digits can vary)
facts.AcceptsEmpty;    // false
facts.IsEmptyLanguage; // false
facts.IsExact;         // true
facts.ToString();      // FullString: length 13..13, prefix "INV-", suffix "", accepts empty: false (exact)
```

## Scopes

| `MatchScope` | The strings described |
|---|---|
| `FullString` (default) | Strings the pattern matches entirely, as in language algebra (`\A(?:p)\z`, where an explicit `$` still accepts a final newline). |
| `Substring` | Strings for which `IsMatch` returns true: the language of `[\s\S]*?(?:p)[\s\S]*`, with the padding dropped on each side the pattern anchors (`^`/`\A` at the start, `$`/`\Z`/`\z` at the end; `RegexOptions.Multiline` makes `^` and `$` non-anchoring). |

```csharp
Pattern.FromRegex(@"^abc").GetLanguageFacts();                      // FullString: length 3..3, prefix "abc", suffix "abc"
Pattern.FromRegex(@"^abc").GetLanguageFacts(MatchScope.Substring);  // Substring: length 3..unbounded, prefix "abc", suffix ""
Pattern.FromRegex(@"xyz\z").GetLanguageFacts(MatchScope.Substring); // Substring: length 3..unbounded, prefix "", suffix "xyz"
Pattern.FromRegex(@"xyz$").GetLanguageFacts(MatchScope.Substring);  // Substring: length 3..unbounded, prefix "", suffix ""
```

The last line is not a bug: `xyz$` also accepts `"xyz\n"`, so no suffix can be required. Use `\z` when
you mean the very end.

More examples, all exact:

```csharp
Pattern.FromRegex(@"^ab?ba\z").GetLanguageFacts();              // length 3..4, prefix "ab", suffix "ba"
Pattern.FromRegex(@"^(?:ab|ac)\z").GetLanguageFacts();          // length 2..2, prefix "a", suffix ""
Pattern.FromRegex(@"^a\d*b\z").GetLanguageFacts();              // length 2..unbounded, prefix "a", suffix "b"
Pattern.FromRegex(@"^ab-12\z", RegexOptions.IgnoreCase).GetLanguageFacts();
                                                                // length 5..5, prefix "", suffix "-12"  (only caseless characters can be required)
Pattern.FromRegex(@"^(?=.*\d).{8,}$").GetLanguageFacts();      // length 8..unbounded (leading lookaheads are modeled)
Pattern.FromRegex(@"[^\s\S]").GetLanguageFacts();               // FullString: matches nothing (exact)
CommonPatterns.Email(PatternStrictness.Strict).GetLanguageFacts(); // length 6..255, prefix "", suffix ""
CommonPatterns.Url().GetLanguageFacts();                        // length 10..unbounded, prefix "http", suffix ""
```

## How the facts are computed

1. The scoped pattern is compiled to a minimal DFA over exact character sets, exactly as for
   `Pattern.Compare` (see [algebra.md](algebra.md)).
2. `MinLength` is a breadth-first search to the nearest accepting state; `MaxLength` is the longest path
   to an accepting state, or `null` when the automaton has a cycle (every state of a minimal DFA is live,
   so any cycle makes the language unbounded).
3. `RequiredPrefix` follows the forced path from the start state: while the state is not accepting and
   exactly one single-character transition leads onward, that character begins every accepted string.
4. `RequiredSuffix` is the same walk backwards from the set of accepting states, simulated lazily as sets
   of states, so the reversed automaton is never determinized in full.

## Outside the regular subset

When the algebra cannot model the pattern (backreferences, lookarounds elsewhere than the start,
conditionals, `\b`, multiline anchors, `RightToLeft`, or an automaton beyond the size limits),
`GetLanguageFacts` never throws. It returns a sound syntactic approximation with `IsExact == false`:

- `RequiredPrefix` is the run of literal characters at the start of the pattern (zero-width constructs
  such as lookarounds and `\b` are skipped because they consume nothing; a quantifier contributes its
  mandatory copies; an alternation contributes what all branches share); `RequiredSuffix` is the same
  from the end. They are only reported when the scope or an anchor pins that end.
- `MinLength` is a lower bound summed over the tree; `MaxLength` is `null`.
- `AcceptsEmpty` is decided by the engine itself on the empty string.

```csharp
Pattern.FromRegex(@"^abc(\w)\1def\z").GetLanguageFacts();   // length 7..unbounded, prefix "abc", suffix "def" (approximate)
Pattern.FromRegex(@"^ab\bc(?<=c)d$").GetLanguageFacts();    // length 4..unbounded, prefix "abcd", suffix "" (approximate)
Pattern.FromRegex(@"(?m)^abc$").GetLanguageFacts(MatchScope.Substring); // length 3..unbounded, prefix "", suffix "" (approximate)
```

Approximate facts are still safe to act on: the real prefix and suffix can only be longer, the real
minimum length only higher.

## Tested properties

The test suite generates matching samples for every `CommonPatterns` member and a set of hand-picked
patterns and checks that each sample respects `MinLength`, `MaxLength`, `RequiredPrefix` and
`RequiredSuffix` in `FullString` scope, and that the samples embedded in random junk still respect the
`Substring` facts.
