#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex;

/// <summary>
/// What the debugger shows when a <see cref="Pattern"/> is expanded. Every value is computed on first access
/// and cached, and no property ever throws: a failure is shown as a message instead.
/// </summary>
internal sealed class PatternDebugView
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Pattern _pattern;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string[]? _groupNames;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string[]? _explanation;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string? _safety;

    /// <summary>Creates the view for <paramref name="pattern"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    public PatternDebugView(Pattern pattern)
    {
        _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
    }

    /// <summary>Gets the regular expression text.</summary>
    public string Regex => _pattern.ToString();

    /// <summary>Gets the options the pattern runs with.</summary>
    public RegexOptions Options => _pattern.Options;

    /// <summary>Gets the match timeout.</summary>
    public TimeSpan Timeout => _pattern.Timeout;

    /// <summary>Gets the names of the named groups.</summary>
    public string[] GroupNames => _groupNames ??= Attempt(_pattern.GetGroupNames, Unavailable);

    /// <summary>Gets the lines of <see cref="Pattern.Explain"/>.</summary>
    public string[] Explanation => _explanation ??= Attempt(ExplanationLines, Unavailable);

    /// <summary>Gets the ReDoS verdict of <see cref="Pattern.AnalyzeSafety"/>, without timed confirmation.</summary>
    public string Safety => _safety ??= Attempt(SafetyVerdict, exception => $"Unavailable: {exception.Message}");

    private string[] ExplanationLines()
    {
        List<string> lines = new List<string>();
        using StringReader reader = new StringReader(_pattern.Explain());
        for (string? line = reader.ReadLine(); line != null; line = reader.ReadLine())
        {
            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        return lines.ToArray();
    }

    private string SafetyVerdict()
    {
        SafetyReport report = _pattern.AnalyzeSafety();
        if (report.Issues.Count == 0)
        {
            return $"{report.Risk}: no catastrophic backtracking risks found.";
        }

        string more = report.Issues.Count > 1 ? $" (+{report.Issues.Count - 1} more)" : string.Empty;
        return $"{report.Risk}: {report.Issues[0]}{more}";
    }

    private static string[] Unavailable(Exception exception) => new[] { $"Unavailable: {exception.Message}" };

    /// <summary>
    /// Runs <paramref name="compute"/>, turning any exception into a value. Catching everything is deliberate:
    /// a debugger display that throws hides the very object the developer is trying to inspect.
    /// </summary>
    private static T Attempt<T>(Func<T> compute, Func<Exception, T> onFailure)
    {
        try
        {
            return compute();
        }
        catch (Exception exception)
        {
            return onFailure(exception);
        }
    }
}
