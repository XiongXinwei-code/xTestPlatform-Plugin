using Xunit;

namespace CAN.Tests;

// These tests deliberately block native calls or an ExecutionContext continuation.
// Isolate them from unrelated short deadline tests; concurrency under test stays explicit.
[CollectionDefinition("CAN blocked I/O", DisableParallelization = true)]
public sealed class TimingCollection { }
