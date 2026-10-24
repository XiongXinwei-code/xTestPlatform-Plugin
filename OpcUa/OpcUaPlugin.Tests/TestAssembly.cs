using Xunit;

// Syncfusion/WPF theme caches are process-global and cannot be initialized by
// several independent STA dispatchers concurrently in a test host.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
