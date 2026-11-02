#nullable enable

using System;
using System.Collections.Generic;
using Masterly.FluentRegex.Learning;

namespace Masterly.FluentRegex;

/// <summary>
/// Learning: infer a pattern from examples of what it must and must not match.
/// </summary>
public partial class Pattern
{
    /// <summary>
    /// Infers an anchored pattern (<c>^...$</c>) that fully matches every example in
    /// <paramref name="match"/> and rejects every example in <paramref name="reject"/>.
    /// </summary>
    /// <param name="match">Examples the pattern must fully match. At least one is required.</param>
    /// <param name="reject">Optional examples the pattern must not match.</param>
    /// <returns>
    /// A pattern built with the fluent API, so <see cref="ToFluentCode"/> shows readable code that can be
    /// pasted into a project and refined.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Each example is cut into runs of digits, letters and whitespace, with every other character as its own
    /// token. When the examples line up, each position becomes literal text if all examples agree, otherwise
    /// the narrowest class that covers them (<c>\d</c>, <c>[A-Z]</c>, <c>[a-z]</c>, <c>[a-zA-Z]</c>, <c>\w</c>,
    /// <c>\s</c>, or a set of the observed punctuation) with an exact count or a <c>{min,max}</c> range.
    /// When a reject example is still accepted, positions are specialized step by step (literal characters,
    /// narrower sets, and finally the exact observed texts). Examples that do not line up are grouped by shape
    /// and combined in a non-capturing alternation; an example that cannot be generalized is kept literally.
    /// </para>
    /// <para>
    /// The result generalizes only as far as the examples show: it is a starting point, not a specification.
    /// Classes are ASCII based; other letters are kept as literal characters. <c>$</c> also matches before a
    /// final newline, so <c>\z</c> is used instead when a reject example ends with a newline.
    /// The output is deterministic: duplicates are ignored and examples are processed in ordinal order.
    /// Every candidate is verified with the real regex engine, which suits tens or hundreds of examples
    /// rather than whole data sets.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="match"/>, or an example, is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="match"/> is empty, or a reject example cannot be separated from the match examples
    /// (it is also listed as a match example).
    /// </exception>
    /// <example>
    /// Pattern invoice = Pattern.Learn(
    ///     match: new[] { "INV-2024-0001", "INV-2025-0420" },
    ///     reject: new[] { "INV-24-1", "inv-2024-0001" });
    /// // ^INV-\d{4}-\d{4}$
    /// </example>
    public static Pattern Learn(IEnumerable<string> match, IEnumerable<string>? reject = null) =>
        PatternLearner.Learn(match, reject);

    /// <summary>
    /// Infers an anchored pattern (<c>^...$</c>) that fully matches every given example.
    /// See <see cref="Learn(IEnumerable{string}, IEnumerable{string})"/> for how examples are generalized.
    /// </summary>
    /// <param name="match">Examples the pattern must fully match. At least one is required.</param>
    /// <returns>The learned pattern.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="match"/>, or an example, is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="match"/> is empty.</exception>
    public static Pattern Learn(params string[] match) =>
        PatternLearner.Learn(match ?? throw new ArgumentNullException(nameof(match)), null);
}
