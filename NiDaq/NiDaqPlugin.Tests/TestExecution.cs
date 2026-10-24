using Xunit;

// Native-exit fixtures intentionally block worker threads and assert configured deadlines.
// Keep unrelated fixtures serial; each lifecycle test still schedules its own controlled races.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
