using Xunit;

// Cold expression compilation competes with wall-clock deadlines across collections.
// Limit unrelated fixtures; each concurrency test still controls its own workers.
[assembly: CollectionBehavior(MaxParallelThreads = 4)]

namespace CAN.Tests;

// These tests deliberately block native calls or an ExecutionContext continuation.
// Isolate them from unrelated short deadline tests; concurrency under test stays explicit.
[CollectionDefinition("CAN blocked I/O", DisableParallelization = true)]
public sealed class TimingCollection { }
