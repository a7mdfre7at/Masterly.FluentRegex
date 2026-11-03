using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

/// <summary>A fact that is skipped when the Docker daemon cannot be reached.</summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!DockerProbe.IsAvailable)
        {
            Skip = DockerProbe.SkipReason;
        }
    }
}

internal static class DockerProbe
{
    private const int TimeoutMilliseconds = 20_000;

    private static readonly Lazy<string?> Failure = new Lazy<string?>(Probe);

    public static bool IsAvailable => Failure.Value == null;

    public static string SkipReason => Failure.Value ?? string.Empty;

    private static string? Probe()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return "Docker is not installed.";
            }

            string error = process.StandardError.ReadToEnd();
            process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(TimeoutMilliseconds))
            {
                process.Kill();
                return "Docker did not answer within 20 seconds.";
            }

            return process.ExitCode == 0 ? null : $"Docker is not reachable: {error.Trim()}";
        }
        catch (Exception ex)
        {
            return $"Docker is not reachable: {ex.Message}";
        }
    }
}
