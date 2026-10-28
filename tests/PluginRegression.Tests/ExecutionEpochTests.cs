extern alias EthernetCore;
using EthernetCore::PluginCommon;
using Xunit;

namespace PluginRegression.Tests;

public sealed class ExecutionEpochTests
{
    [Fact]
    public void Steps_in_one_sdk_execution_share_the_same_marker_without_ending_each_others_lifetimes()
    {
        var resources = SdkResourceLifecycleTests.NewRegistry();
        try
        {
            var first = ExecutionEpoch.Capture(resources, "EpochTest");
            var second = ExecutionEpoch.Capture(resources, "EpochTest");
            Assert.Same(first, second);
            first.ThrowIfExpired();
            second.ThrowIfExpired();
        }
        finally { SdkResourceLifecycleTests.Cleanup(resources, "CleanupAll"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sdk_cleanup_retires_the_captured_epoch_and_the_first_new_execution_has_a_distinct_usable_marker(bool newDebugContext)
    {
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var previous = ExecutionEpoch.Capture(resources, "EpochTest");
        SdkResourceLifecycleTests.Cleanup(resources, newDebugContext ? "CleanupAll" : "CleanupExecution");
        Assert.Throws<InvalidOperationException>(previous.ThrowIfExpired);
        var nextResources = newDebugContext ? SdkResourceLifecycleTests.NewRegistry() : resources;
        var current = ExecutionEpoch.Capture(nextResources, "EpochTest");
        try
        {
            Assert.NotSame(previous, current);
            previous.Dispose();
            Assert.Throws<InvalidOperationException>(previous.ThrowIfExpired);
            current.ThrowIfExpired();
        }
        finally { SdkResourceLifecycleTests.Cleanup(nextResources, "CleanupAll"); }
    }

    [Fact]
    public void Removing_the_sdk_entry_invalidates_admission_before_the_dispose_callback_runs()
    {
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var previous = ExecutionEpoch.Capture(resources, "EpochTest");
        Assert.True(resources.Remove("PluginCommon.ExecutionEpoch:EpochTest", dispose: false));
        Assert.Throws<InvalidOperationException>(previous.ThrowIfExpired);
        var current = ExecutionEpoch.Capture(resources, "EpochTest");
        try { current.ThrowIfExpired(); }
        finally { SdkResourceLifecycleTests.Cleanup(resources, "CleanupAll"); }
    }
}
