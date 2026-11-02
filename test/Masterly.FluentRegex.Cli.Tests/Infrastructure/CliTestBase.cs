using System.Text;

namespace Masterly.FluentRegex.Cli.Tests.Infrastructure;

/// <summary>
/// Runs the CLI in process with redirected console streams and a private temporary directory,
/// restoring the console, the output encoding and the current directory afterwards.
/// </summary>
[Collection(CliCollection.Name)]
public abstract class CliTestBase : IDisposable
{
    /// <summary>Exit code for success.</summary>
    protected const int Ok = 0;

    /// <summary>Exit code for findings (analyze, scan, compare, profile, coverage).</summary>
    protected const int Findings = 1;

    /// <summary>Exit code for usage or input errors.</summary>
    protected const int UsageError = 2;

    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly TextReader _originalIn;
    private readonly Encoding _originalOutputEncoding;
    private readonly string _originalDirectory;

    /// <summary>Captures the console state and creates a unique temporary directory.</summary>
    protected CliTestBase()
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;
        _originalIn = Console.In;
        _originalOutputEncoding = Console.OutputEncoding;
        _originalDirectory = Environment.CurrentDirectory;
        TempDirectory = Path.Combine(Path.GetTempPath(), "frx-cli-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDirectory);
        Console.SetIn(new StringReader(string.Empty));
    }

    /// <summary>A directory private to the current test, deleted on dispose.</summary>
    protected string TempDirectory { get; }

    /// <summary>Runs the CLI with the given arguments and captures what it prints.</summary>
    protected static CliResult Run(params string[] args)
    {
        StringWriter output = new StringWriter();
        StringWriter error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);
        int exitCode = Cli.Run(args);
        Console.Out.Flush();
        Console.Error.Flush();
        return new CliResult(exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Makes the given text the CLI's standard input.</summary>
    protected static void SetStandardInput(string text) => Console.SetIn(new StringReader(text));

    /// <summary>Writes a file below <see cref="TempDirectory"/> and returns its full path.</summary>
    protected string WriteFile(string relativePath, string content)
    {
        string path = Path.Combine(TempDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); // A file path below TempDirectory always has a parent.
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Makes <see cref="TempDirectory"/> the current directory until the test ends.</summary>
    protected void UseTempDirectoryAsCurrentDirectory() => Environment.CurrentDirectory = TempDirectory;

    /// <inheritdoc />
    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDirectory;
        Console.OutputEncoding = _originalOutputEncoding;
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        Console.SetIn(_originalIn);
        Directory.Delete(TempDirectory, recursive: true);
        GC.SuppressFinalize(this);
    }
}
