using System.Diagnostics;
using Masterly.FluentRegex.Cli.Tests.Infrastructure;

namespace Masterly.FluentRegex.Cli.Tests;

/// <summary>Runs the real <c>fluentregex</c> executable to verify the process exit codes end to end.</summary>
[Collection(CliCollection.Name)]
public sealed class EntryPointTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);

    [Theory]
    [InlineData(0, "--version")]
    [InlineData(0, "explain", "a")]
    [InlineData(1, "analyze", "(a+)+$")]
    [InlineData(2, "convert")]
    [InlineData(2, "bogus")]
    [InlineData(2)]
    public void Main_Arguments_ExitsWithTheCodeReturnedByRun(int expectedExitCode, params string[] args)
    {
        ProcessResult result = RunTool(args);

        result.ExitCode.ShouldBe(expectedExitCode);
    }

    [Fact]
    public void Main_Version_PrintsTheLibraryVersion()
    {
        string expected = typeof(Pattern).Assembly.GetName().Version!.ToString(3); // The library assembly always carries a version.

        ProcessResult result = RunTool("--version");

        result.Output.Trim().ShouldBe(expected);
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void Main_UnknownCommand_WritesTheErrorToStandardError()
    {
        ProcessResult result = RunTool("bogus");

        result.Output.ShouldBeEmpty();
        result.Error.Trim().ShouldBe("error: Unknown command 'bogus'. Run 'fluentregex --help'.");
    }

    private static ProcessResult RunTool(params string[] args)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo(DotNetHost())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // The tool beside the tests is bound to this test target's library builds, so it must run on the
        // test's runtime rather than the net8.0 runtime its own runtimeconfig asks for.
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--runtimeconfig");
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Masterly.FluentRegex.Cli.Tests.runtimeconfig.json"));
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "fluentregex.dll"));
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(startInfo)!; // Start only returns null when reusing a shell process.
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(ProcessTimeout);
        exited.ShouldBeTrue("fluentregex did not exit in time.");
        return new ProcessResult(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    /// <summary>The dotnet host that runs the tests, falling back to the one on the PATH.</summary>
    private static string DotNetHost() => Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
