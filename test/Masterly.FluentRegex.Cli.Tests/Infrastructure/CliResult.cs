namespace Masterly.FluentRegex.Cli.Tests.Infrastructure;

/// <summary>The exit code and the captured standard output and error of one CLI run.</summary>
/// <param name="ExitCode">The value returned by <c>Cli.Run</c>.</param>
/// <param name="Output">Everything written to standard output.</param>
/// <param name="Error">Everything written to standard error.</param>
public sealed record CliResult(int ExitCode, string Output, string Error)
{
    /// <summary>The standard output split into lines, without the trailing empty line.</summary>
    public string[] OutputLines =>
        Output.Length == 0 ? Array.Empty<string>() : Output.TrimEnd('\r', '\n').Split(Environment.NewLine);
}
