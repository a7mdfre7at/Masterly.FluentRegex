using System.Text.RegularExpressions;
using Masterly.FluentRegex.Analysis;
using Masterly.FluentRegex.Tracing;

namespace Masterly.FluentRegex.Playground;

/// <summary>A captured group of one match, as shown in the matches table.</summary>
/// <param name="Name">The group name or number.</param>
/// <param name="Index">The position of the capture in the test text.</param>
/// <param name="Value">The captured text.</param>
public sealed record GroupView(string Name, int Index, string Value);

/// <summary>One match, as shown in the matches table.</summary>
/// <param name="Index">The position of the match in the test text.</param>
/// <param name="Value">The matched text.</param>
/// <param name="Groups">The groups that participated in the match.</param>
public sealed record MatchView(int Index, string Value, IReadOnlyList<GroupView> Groups);

/// <summary>A regex condition for one database, as shown in the export panel.</summary>
/// <param name="Condition">The WHERE condition over the sample column.</param>
/// <param name="Warnings">Every behavior that differs from .NET; empty when the translation is exact.</param>
public sealed record SqlView(string Condition, IReadOnlyList<string> Warnings);

/// <summary>The outcome of computing a panel: a value, or the error that prevented it.</summary>
/// <typeparam name="T">The panel's value type.</typeparam>
/// <param name="Value">The computed value, when successful.</param>
/// <param name="Error">The error message, when unsuccessful.</param>
public sealed record PanelResult<T>(T? Value, string? Error)
{
    /// <summary>Gets a value indicating whether the panel was computed.</summary>
    public bool Succeeded => Error == null;
}

/// <summary>
/// Builds a <see cref="Pattern"/> from the playground state and computes each panel on demand.
/// Every panel catches its own failure, so one unsupported feature never breaks the others.
/// </summary>
public sealed class PatternWorkbench
{
    /// <summary>The column name used in SQL previews.</summary>
    public const string SampleColumn = "value";

    private const int MaxMatchesShown = 500;
    private const int SampleCount = 10;
    private const int SampleSeed = 1;
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private readonly Dictionary<string, object> _cache = new Dictionary<string, object>();

    /// <summary>Creates a workbench for the given state.</summary>
    /// <param name="state">The playground state.</param>
    public PatternWorkbench(PlaygroundState state)
    {
        State = state;
        try
        {
            Pattern = Masterly.FluentRegex.Pattern.FromRegex(state.Regex, state.Options)
                .WithDigits(Digits)
                .WithTimeout(MatchTimeout);
        }
        catch (ArgumentException ex)
        {
            BuildError = ex.Message;
        }
    }

    /// <summary>Gets the state the workbench was built from.</summary>
    public PlaygroundState State { get; }

    /// <summary>Gets the pattern, or null when the regex is invalid.</summary>
    public Pattern? Pattern { get; }

    /// <summary>Gets the reason the regex could not be built, if any.</summary>
    public string? BuildError { get; }

    /// <summary>Gets the matches in the test text.</summary>
    public PanelResult<IReadOnlyList<MatchView>> Matches => Panel(nameof(Matches), FindMatches);

    /// <summary>Gets the test text as HTML with matches and groups marked. The highlighter encodes all text.</summary>
    public PanelResult<string> HighlightedHtml => Panel(nameof(HighlightedHtml), p => p.Highlight(State.TestText, HighlightFormat.Html));

    /// <summary>Gets the result of the verified optimizer.</summary>
    public PanelResult<OptimizationResult> Optimization => Panel(nameof(Optimization), p => p.Optimize());

    /// <summary>Gets the JavaScript translation.</summary>
    public PanelResult<FlavorConversion> JavaScript => Panel(nameof(JavaScript), p => p.ToFlavor(RegexFlavor.JavaScript));

    /// <summary>Gets the JSON Schema (OpenAPI) translation.</summary>
    public PanelResult<FlavorConversion> JsonSchema => Panel(nameof(JsonSchema), p => p.ToFlavor(RegexFlavor.JsonSchema));

    /// <summary>Gets the regex WHERE condition for a database, keeping the exact .NET meaning.</summary>
    /// <param name="flavor">The database's regex flavor.</param>
    /// <returns>The condition and its warnings, or the error that prevented the translation.</returns>
    public PanelResult<SqlView> Sql(RegexFlavor flavor) =>
        Panel($"{nameof(Sql)}:{flavor}", p => new SqlView(p.ToSqlCondition(SampleColumn, flavor), p.ToFlavor(flavor).Warnings));

    /// <summary>Gets the LIKE compilation for the database that owns the given regex flavor.</summary>
    /// <param name="flavor">The database's regex flavor.</param>
    /// <returns>The LIKE pattern with its proof, or the error that prevented it.</returns>
    public PanelResult<LikeConversion> Like(RegexFlavor flavor) =>
        Panel($"{nameof(Like)}:{flavor}", p => p.ToLike(LikeDialectOf(flavor)));

    /// <summary>Gets what every string the pattern finds a match in has in common.</summary>
    public PanelResult<LanguageFacts> Facts => Panel(nameof(Facts), p => p.GetLanguageFacts(MatchScope.Substring));

    /// <summary>Gets which branches and repetition limits the lines of the test text exercise.</summary>
    public PanelResult<CoverageReport> Coverage => Panel(nameof(Coverage), p => p.MeasureCoverage(TestLines()));

    /// <summary>Gets the plain-English explanation.</summary>
    public PanelResult<string> Explanation => Panel(nameof(Explanation), p => p.Explain());

    /// <summary>Gets the equivalent fluent C# code.</summary>
    public PanelResult<string> FluentCode => Panel(nameof(FluentCode), p => p.ToFluentCode());

    /// <summary>Gets the railroad diagram as SVG markup. The renderer escapes all pattern text.</summary>
    public PanelResult<string> RailroadSvg => Panel(nameof(RailroadSvg), p => p.ToRailroadSvg());

    /// <summary>Gets the Mermaid flowchart source.</summary>
    public PanelResult<string> Mermaid => Panel(nameof(Mermaid), p => p.ToMermaid());

    /// <summary>Gets generated strings the pattern matches.</summary>
    public PanelResult<string[]> MatchingSamples => Panel(nameof(MatchingSamples), p => p.GenerateMatching(SampleCount, SampleSeed));

    /// <summary>Gets generated strings the pattern rejects.</summary>
    public PanelResult<string[]> NonMatchingSamples => Panel(nameof(NonMatchingSamples), p => p.GenerateNonMatching(SampleCount, SampleSeed));

    /// <summary>Gets the ReDoS safety report.</summary>
    public PanelResult<SafetyReport> Safety => Panel(nameof(Safety), p => p.AnalyzeSafety(confirm: true));

    /// <summary>Traces the pattern against one line of input.</summary>
    /// <param name="input">The line to trace.</param>
    /// <returns>The trace, or the error that prevented it.</returns>
    public PanelResult<MatchTrace> Trace(string input) => Panel($"{nameof(Trace)}:{input}", p => p.Trace(input));

    /// <summary>Profiles the backtracking work the pattern does on one line of input.</summary>
    /// <param name="input">The line to profile.</param>
    /// <returns>The profile, or the error that prevented it.</returns>
    public PanelResult<ProfileReport> Profile(string input) => Panel($"{nameof(Profile)}:{input}", p => p.Profile(input));

    /// <summary>Compares the pattern's language with another regex using the same options.</summary>
    /// <param name="otherRegex">The regex to compare against.</param>
    /// <returns>The comparison, or the error that prevented it.</returns>
    public PanelResult<PatternComparison> Compare(string otherRegex) =>
        Panel($"{nameof(Compare)}:{otherRegex}", p => Masterly.FluentRegex.Pattern.Compare(p, Masterly.FluentRegex.Pattern.FromRegex(otherRegex, State.Options).WithDigits(Digits)));

    private DigitMode Digits => State.AsciiDigits ? DigitMode.Ascii : DigitMode.Unicode;

    private static LikeDialect LikeDialectOf(RegexFlavor flavor) => flavor switch
    {
        RegexFlavor.PostgreSql => LikeDialect.PostgreSql,
        RegexFlavor.MySql or RegexFlavor.MariaDb => LikeDialect.MySql,
        RegexFlavor.SqlServer => LikeDialect.SqlServer,
        RegexFlavor.Sqlite => LikeDialect.Sqlite,
        _ => LikeDialect.Standard
    };

    private string[] TestLines() => State.TestText.Replace("\r\n", "\n").Split('\n');

    private PanelResult<T> Panel<T>(string key, Func<Pattern, T> compute)
    {
        if (_cache.TryGetValue(key, out object? cached))
        {
            return (PanelResult<T>)cached;
        }

        PanelResult<T> result = Compute(compute);
        _cache[key] = result;
        return result;
    }

    private PanelResult<T> Compute<T>(Func<Pattern, T> compute)
    {
        if (Pattern == null)
        {
            return new PanelResult<T>(default, BuildError);
        }

        try
        {
            return new PanelResult<T>(compute(Pattern), null);
        }
        catch (RegexMatchTimeoutException)
        {
            return new PanelResult<T>(default, $"Matching took longer than {MatchTimeout.TotalSeconds:0} s and was stopped. Check the Safety panel for catastrophic backtracking.");
        }
        catch (Exception ex) when (ex is NotSupportedException || ex is InvalidOperationException || ex is ArgumentException)
        {
            return new PanelResult<T>(default, ex.Message);
        }
    }

    private IReadOnlyList<MatchView> FindMatches(Pattern pattern)
    {
        Regex regex = pattern.ToRegex();
        string[] groupNames = regex.GetGroupNames();
        List<MatchView> views = new List<MatchView>();

        foreach (Match match in regex.Matches(State.TestText).Take(MaxMatchesShown))
        {
            List<GroupView> groups = groupNames
                .Skip(1)
                .Where(name => match.Groups[name].Success)
                .Select(name => new GroupView(name, match.Groups[name].Index, match.Groups[name].Value))
                .ToList();
            views.Add(new MatchView(match.Index, match.Value, groups));
        }

        return views;
    }
}
