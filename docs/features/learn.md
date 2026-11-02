# Learn a pattern from examples

`Pattern.Learn` infers an anchored pattern from strings it must match and, optionally, strings it must
reject. The result is an ordinary `Pattern` built with the fluent API, so `ToFluentCode()` gives you
readable code to paste into your project and refine.

```csharp
Pattern invoice = Pattern.Learn(
    match:  new[] { "INV-2024-0001", "INV-2025-0420" },
    reject: new[] { "INV-24-1", "inv-2024-0001" });

Console.WriteLine(invoice);                 // ^INV-\d{4}-\d{4}$
Console.WriteLine(invoice.ToFluentCode());
// Pattern.With
//     .StartOfLine
//     .Literal("INV-")
//     .Digit.Repeat.Times(4)
//     .Literal("-")
//     .Digit.Repeat.Times(4)
//     .EndOfLine

invoice.IsMatch("INV-2030-9999");           // true
invoice.IsMatch("INV-2030-99999");          // false
```

When you only have examples to match, use the `params` overload:

```csharp
Pattern.Learn("a1", "b2");                  // ^[a-z]\d$
Pattern.Learn("cat", "dog", "bird");        // ^[a-z]{3,4}$
Pattern.Learn("v1.2.3", "v10.20.30");       // ^v\d{1,2}\.\d{1,2}\.\d{1,2}$
Pattern.Learn("2024-01-02", "2025/11/30");  // ^\d{4}[\-/]\d{2}[\-/]\d{2}$
```

## How it works

1. **Tokenize.** Each example is cut into runs of digits, runs of letters and runs of whitespace. Every
   other character (punctuation, symbols) is its own token.
2. **Align.** When all examples have the same shape, each position becomes:
   - the literal text, when every example has the same text there;
   - otherwise the narrowest class that covers all examples: `\d`, `[A-Z]`, `[a-z]`, `[a-zA-Z]`, `\w`,
     `\s`, or a set of the punctuation seen there, such as `[\-/]`;
   - with an exact count when all runs have the same length, otherwise `{min,max}`.

   If the fine-grained shapes differ, runs of word characters are tried as single tokens
   (`"AB12"` and `"A1B2"` give `^\w{4}$`).
3. **Separate.** Every reject example is tested with the real regex engine. While one is still accepted,
   one position at a time is specialized: first literal characters where the examples agree, then sets of
   the observed characters, and finally the exact observed texts. The position that rejects the most
   remaining examples is chosen first. Specializations that turn out to be unnecessary are undone.

   ```csharp
   Pattern.Learn(new[] { "2024", "2025" }, new[] { "2026" });
   // ^202[45]$

   Pattern.Learn(new[] { "INV-2024-0001", "INV-2025-0420" }, new[] { "INV-2024-9999" });
   // ^INV-\d{4}-0\d{3}$

   Pattern.Learn(new[] { "cat", "dog", "bird" }, new[] { "cow" });
   // ^(?:bird|cat|dog)$
   ```
4. **Group.** Examples with different shapes are grouped by shape, each group is learned separately, and
   the groups are combined in a non-capturing alternation. A group that cannot be generalized without
   accepting a reject example falls back to its exact examples.

   ```csharp
   Pattern.Learn("abc", "12-34");                              // ^(?:12-34|abc)$
   Pattern.Learn(new[] { "A-1", "B-2" }, new[] { "A-2" });     // ^(?:A-1|B-2)$
   ```

The result is always anchored, matches every match example in full, and rejects every reject example.
The final pattern is verified once more before it is returned.

## Errors

| Situation | Exception |
|-----------|-----------|
| `match` is `null`, or any example is `null` | `ArgumentNullException` |
| `match` is empty | `ArgumentException` (`ParamName` is `match`) |
| a reject example is also a match example | `ArgumentException` naming the example (`ParamName` is `reject`) |

Because a pattern can always fall back to the exact match examples, the only examples that cannot be
separated are those listed on both sides.

## Limits

- **It generalizes only as far as the examples show.** `Pattern.Learn("cat", "dog")` gives `^[a-z]{3}$`,
  which also accepts `"xyz"`. Add reject examples to steer it, and review the result: it is a starting point,
  not a specification.
- **Text shared by all examples stays literal.** `Pattern.Learn("2024-01-02", "2024-03-04")` keeps `2024`
  literally. Give examples that vary where your data varies.
- **Classes are ASCII.** Letters outside `A-Z`/`a-z` are kept as literal characters, so
  `Pattern.Learn("José", "Jesús")` gives `^(?:Jesús|José)$`.
- **`$` accepts a trailing newline.** `^abc$` matches `"abc\n"`. When a reject example ends with a newline,
  `\z` is used instead: `Pattern.Learn(new[] { "abc" }, new[] { "abc\n" })` gives `^abc\z`.
- **Positions are learned independently.** A reject that combines parts of different match examples (such as
  `A-2` for `A-1` and `B-2`) forces the exact examples.
- **Deterministic.** Duplicates are ignored and examples are processed in ordinal order, so the same set of
  examples always gives the same pattern, whatever their order.
- **Sized for examples, not data sets.** Each candidate is compiled and tested with the regex engine, which
  is fast for tens or hundreds of examples.
