# Language algebra: prove how patterns relate

Two regular expressions that look different can match exactly the same strings, and two that look
alike can differ in ways no unit test thought to check. Language algebra answers these questions
with a proof instead of a guess, and hands you the shortest string that shows the difference.

```csharp
using Masterly.FluentRegex;

PatternComparison result = Pattern.Compare(Pattern.FromRegex(@"\d{3}"), Pattern.FromRegex(@"[0-9]{3}"));

Console.WriteLine(result.Relation);         // ProperSuperset
Console.WriteLine(result.OnlyLeftExample);  // "00٠"  (U+0660 ARABIC-INDIC DIGIT ZERO)
Console.WriteLine(result);
// Not equivalent (left is a proper superset of right). Shortest counterexample: "00٠"
// (left accepts it, right rejects it). Both accept "000".
```

`\d` matches every Unicode decimal digit, not just `0-9`. The algebra finds that on its own.

## What "language" means here

The language of a pattern is the set of strings it matches **in full**, as if it were wrapped in
`\A(?:...)\z`, with the pattern's own `RegexOptions` (IgnoreCase, Singleline, CultureInvariant,
ECMAScript, IgnorePatternWhitespace, inline options such as `(?i)`). Every character class is
computed by the real .NET engine over all 65,536 UTF-16 code units, so Unicode categories, class
subtraction and case-insensitivity rules are exact.

An explicit `$` or `\Z` at the end keeps its .NET meaning: it also accepts one trailing `\n`.
That is a classic validation bug, and the algebra reports it:

```csharp
Pattern.Compare(Pattern.FromRegex(@"^\d+$"), Pattern.FromRegex(@"\A\d+\z")).ToString();
// Not equivalent (left is a proper superset of right). Shortest counterexample: "0\n"
// (left accepts it, right rejects it). Both accept "0".
```

## Comparing patterns

`Pattern.Compare(left, right)` returns a `PatternComparison`:

| Member | Meaning |
|---|---|
| `Relation` | `Equivalent`, `ProperSubset`, `ProperSuperset`, `Overlapping` or `Disjoint` |
| `OnlyLeftExample` | Shortest string the left pattern accepts and the right rejects, or `null` |
| `OnlyRightExample` | Shortest string the right pattern accepts and the left rejects, or `null` |
| `CommonExample` | Shortest string both accept, or `null` |
| `ToString()` | A one-line explanation with the shortest counterexample |

Examples are the shortest possible and prefer readable characters (lowercase, uppercase, digits,
then other printable ASCII). Every example is re-checked with the real .NET engine before it is
returned; if the engine ever disagreed with the automaton, you would get an
`InvalidOperationException` instead of a wrong answer.

```csharp
PatternComparison email = Pattern.Compare(
    CommonPatterns.Email(PatternStrictness.Strict),
    CommonPatterns.Email(PatternStrictness.Loose));

email.Relation;          // ProperSubset: every strict address is also a loose one
email.OnlyRightExample;  // "a@a.a"  (loose accepts a one-letter TLD, strict does not)
email.CommonExample;     // "a@a.aa"

Pattern.Compare(CommonPatterns.Email(), CommonPatterns.Email(PatternStrictness.Loose)).ToString();
// Not equivalent (the patterns overlap). Shortest counterexample: "a@a.a" (right accepts it,
// left rejects it); also "a@..aa" (left accepts it, right rejects it). Both accept "a@a.aa".
```

Shortcuts for the common questions:

```csharp
Pattern.FromRegex("a|ab").IsEquivalentTo(Pattern.FromRegex("ab?"));    // true
Pattern.FromRegex("[0-9]{3}").IsSubsetOf(Pattern.FromRegex(@"\d{3}"));  // true
Pattern.FromRegex("[ab]+").Overlaps(Pattern.FromRegex("[bc]+"));        // true
Pattern.FromRegex(@"^(?!a)a").MatchesNothing();                         // true
Pattern.FromRegex(@"[a-z]{2,}\d").ShortestMatch();                      // "aa0"
```

## Lookaheads at the start

Lookaheads at the very start of a pattern (after an optional `^` or `\A`) constrain the whole
input, so they are supported. This covers the usual password-policy style:

```csharp
Pattern.Compare(
    Pattern.FromRegex(@"^(?=.*\d)(?=.*[a-z]).{8,}$"),
    Pattern.FromRegex(@"^.{8,}$")).ToString();
// Not equivalent (left is a proper subset of right). Shortest counterexample: "aaaaaaaa"
// (right accepts it, left rejects it). Both accept "aaaaaaa0".
```

## Combining patterns

`Intersect`, `Except` and `Union` build a new pattern from the combined language. The result keeps
the left pattern's options and timeout, is anchored so that `IsMatch` agrees with membership, and
is proven equivalent to the combined automaton before it is returned.

```csharp
Pattern.FromRegex("[a-z]+").Intersect(Pattern.FromRegex(@"\w{3}"));      // ^[a-z]{3}\z
Pattern.FromRegex(@"^\d+$").Intersect(Pattern.FromRegex(@"^\d{3}$"));     // ^\d{3}$
Pattern.FromRegex(@"\w+").Except(Pattern.FromRegex("[a-z]+"));            // ^[a-z]*[\w-[a-z]]\w*\z
Pattern.FromRegex(@"\d{3}").Except(Pattern.FromRegex("[0-9]{3}"));
// ^(?:[\d-[0-9]]\d{2}|[0-9](?:[0-9][\d-[0-9]]|[\d-[0-9]]\d))\z

Pattern.FromRegex("[a-z]+").Union(Pattern.FromRegex("[0-9]+"));          // [a-z]+|[0-9]+
Pattern.FromRegex("abc").Union(Pattern.FromRegex("XYZ", RegexOptions.IgnoreCase));
// ^(?:abc|[Xx][Yy][Zz])\z   (options differ, so the case-insensitivity is spelled out)
```

When both patterns share options and can be joined directly, `Union` returns a plain alternation;
otherwise it is synthesized like the other two. Results synthesized from complex automata (for
example ones that involve lookaheads) are correct but can be long.

## What is not supported

Anything that is not a regular language, or that changes the language in ways an automaton cannot
model, throws `NotSupportedException` naming the construct and its position:

- backreferences, conditionals, balancing groups, atomic groups
- lookbehinds, and lookaheads anywhere but the very start
- `\b`, `\B`, `\G`
- `^`, `\A` anywhere but the very start; `$`, `\Z`, `\z` anywhere but the very end
- `^` and `$` under `RegexOptions.Multiline` (use `\A` and `\z`), and `RegexOptions.RightToLeft`

```csharp
Pattern.FromRegex(@"(\w)\1").IsEquivalentTo(Pattern.FromRegex("aa"));
// NotSupportedException: Language algebra does not support backreferences: '\1' at position 4. ...
```

Analysis is bounded so it can never hang. Repeat bounds above 1,000 (such as `a{2,5000}`), patterns
needing more than 200,000 NFA states, and automata that grow beyond 50,000 states throw
`InvalidOperationException` with a clear message:

```csharp
Pattern.FromRegex(@"[ab]*a[ab]{18}").ShortestMatch();
// InvalidOperationException: The automaton grew beyond 50000 states; the pattern is too complex
// for language algebra.
```

## How it works

1. The pattern is parsed into the library's syntax tree, and each literal or class becomes an exact
   65,536-bit character set computed by the .NET engine.
2. All sets involved are partitioned into *minterms*: the coarsest groups of characters no
   transition can tell apart. This keeps the alphabet small but exact.
3. A Thompson NFA is built, anchors are validated by position, determinized by subset construction
   and minimized (Moore's algorithm). Leading lookaheads become extra automata that are intersected
   (or subtracted, for negative lookaheads).
4. A breadth-first search over the product automaton finds the shortest witness for each region,
   trying the most readable character of each minterm first.
5. For `Intersect`, `Except` and `Union`, the product automaton is minimized and turned back into a
   regex by state elimination, then simplified (class merging, prefix and suffix factoring,
   repeat folding) and proven equal to the automaton again.
