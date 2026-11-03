namespace Masterly.FluentRegex.Cli.Tests.Infrastructure;

/// <summary>
/// Every CLI test redirects the process-wide <see cref="Console"/> streams and some change the current
/// directory, so they all share one collection that never runs in parallel.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CliCollection
{
    /// <summary>The collection name used by every CLI test class.</summary>
    public const string Name = "Cli";
}
