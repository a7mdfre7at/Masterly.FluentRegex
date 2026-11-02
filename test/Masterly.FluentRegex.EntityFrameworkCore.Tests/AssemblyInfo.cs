using Xunit;

// The adapter's test hook (CoreDialectAdapter.FactsOverride) is process-wide, so test classes must not interleave.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
