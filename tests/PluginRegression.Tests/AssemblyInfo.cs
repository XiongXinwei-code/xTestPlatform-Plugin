using Xunit;

// These tests deliberately park thread-pool workers and cancellation callbacks at
// ownership boundaries. Keep test classes isolated; each concurrency test still
// creates its own real parallel tasks and verifies the specified interleaving.
// Do not loosen product deadlines or wall-time assertions to hide fixture starvation.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
