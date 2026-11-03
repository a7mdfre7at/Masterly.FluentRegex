using Xunit;

// The container fixtures start several database servers; running test classes in parallel starves them.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
