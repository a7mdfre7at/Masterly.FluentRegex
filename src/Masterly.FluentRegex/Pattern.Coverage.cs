#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Coverage;

namespace Masterly.FluentRegex;

/// <summary>
/// Coverage: find out which branches, optional parts and repetition limits a set of inputs exercises.
/// </summary>
public partial class Pattern
{
    /// <summary>
    /// Creates a recorder that tracks which decision points of this pattern are exercised by the inputs
    /// matched through it. Keep it across a test run (for example in a static field) and inspect
    /// <see cref="PatternCoverage.Report"/> at the end.
    /// </summary>
    /// <returns>A new, thread-safe coverage recorder.</returns>
    /// <remarks>
    /// <para>Three kinds of items are tracked:</para>
    /// <list type="bullet">
    /// <item><description>every branch of an alternation or conditional was taken;</description></item>
    /// <item><description>the body of every quantifier with a minimum of zero ran at least once;</description></item>
    /// <item><description>
    /// every bounded repetition with a range, such as <c>{2,5}</c>, ran its maximum in some match. This is only
    /// tracked for repetitions that are not nested inside another quantifier, where the count per match is exact.
    /// </description></item>
    /// </list>
    /// <para>
    /// The pattern is instrumented with empty marker groups; the original options and timeout are kept.
    /// Negative lookarounds and the conditions of conditionals are not instrumented, because their captures
    /// never survive. Patterns that use <c>UseNonBacktracking()</c> are recorded with the backtracking engine,
    /// which reports every iteration of a repetition. It finds the same matches, but without the linear-time
    /// guarantee, so keep a timeout on patterns that face hostile input.
    /// </para>
    /// <para>
    /// <see cref="PatternCoverage.IsMatch"/> and <see cref="PatternCoverage.Record"/> run only the instrumented
    /// pattern; <see cref="PatternCoverage.Match"/> also runs the original one to return an unaltered match.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">The pattern is not valid regex syntax.</exception>
    /// <example>
    /// static readonly PatternCoverage Title = Pattern.FromRegex(@"^(?:Mr|Ms)\.\s\w+$").TrackCoverage();
    /// // in tests: Title.IsMatch("Mr. Smith").ShouldBeTrue();
    /// // at the end: Console.WriteLine(Title.Report());
    /// </example>
    public PatternCoverage TrackCoverage()
    {
        InstrumentedPattern instrumented = CoverageInstrumenter.Instrument(ParseTree());
        return new PatternCoverage(this, CachedRegex, CompileInstrumented(instrumented.Regex), instrumented.Probes);
    }

    /// <summary>
    /// Matches every input and reports which decision points of this pattern they exercised.
    /// </summary>
    /// <param name="inputs">The inputs to match. Every match in each input counts.</param>
    /// <returns>The coverage report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inputs"/>, or an input, is null.</exception>
    /// <exception cref="InvalidOperationException">The pattern is not valid regex syntax.</exception>
    /// <example>
    /// CoverageReport report = Pattern.FromRegex(@"^(?:Mr|Ms)\.\s\w+$").MeasureCoverage(new[] { "Mr. Smith" });
    /// // report.Uncovered: branch "Ms"
    /// </example>
    public CoverageReport MeasureCoverage(IEnumerable<string> inputs)
    {
        if (inputs == null)
        {
            throw new ArgumentNullException(nameof(inputs));
        }

        PatternCoverage coverage = TrackCoverage();
        foreach (string input in inputs)
        {
            coverage.Record(input ?? throw new ArgumentNullException(nameof(inputs), "The inputs cannot contain null."));
        }

        return coverage.Report();
    }

    private Regex CompileInstrumented(string instrumented)
    {
        RegexOptions options = _options;
#if NET7_0_OR_GREATER
        // The non-backtracking engine keeps only the last capture of a repeated group, which would hide
        // how many times a repetition ran.
        options &= ~RegexOptions.NonBacktracking;
#endif
        try
        {
            return new Regex(instrumented, options, _timeout);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"The pattern {this} could not be instrumented for coverage: {ex.Message}", ex);
        }
    }
}
