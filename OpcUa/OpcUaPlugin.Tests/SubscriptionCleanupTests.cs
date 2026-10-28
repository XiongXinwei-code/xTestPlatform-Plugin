using OpcUa.Helpers;
using OpcUa.Executors;
using OpcUa.Models;
using Opc.Ua;
using System.Reflection;
using Xunit;

namespace OpcUa.Tests;

public sealed class SubscriptionCleanupTests
{
    [Fact]
    public async Task SubscribeBeforeSessionInitializationDoesNotLeakAnActivity()
    {
        var resource = new OpcUaConnectionResource(1000);
        var method = typeof(OpcUaSubscribeExecutor).GetMethod("WaitForValueAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var waiting = (Task<DataValue>)method.Invoke(null,
            [resource, NodeId.Parse("ns=2;s=V"), "1", new OpcUaSubscribeSetting(), CancellationToken.None])!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => waiting);
        await resource.CloseAsync(100);
        Assert.True(resource.IsClosed);
    }

    [Fact]
    public async Task CleanupRetainsActivityUntilOriginalCreateAndDeleteExit()
    {
        var create = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deleting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = new Lease(); int removed = 0;
        var cleanup = OpcUaSubscriptionCleanup.Start(create.Task,
            _ => { deleting.TrySetResult(); return delete.Task; }, () => removed++, lease, _ => { }, 1000);
        await Assert.ThrowsAsync<TimeoutException>(() => cleanup.WaitAsync(TimeSpan.FromMilliseconds(20)));
        Assert.Equal(0, lease.Disposals); Assert.Equal(0, removed);
        create.TrySetResult(); await deleting.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0, lease.Disposals);
        delete.TrySetResult(); await cleanup.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, lease.Disposals); Assert.Equal(1, removed);
    }

    [Fact]
    public async Task FailedCreateStillRemovesLocalRegistrationAndPreservesCleanupFailure()
    {
        var lease = new Lease(); int removed = 0;
        var cleanup = OpcUaSubscriptionCleanup.Start(Task.FromException(new InvalidOperationException("create failed")),
            _ => throw new InvalidOperationException("delete failed"), () => removed++, lease, _ => { }, 1000);
        var error = await Assert.ThrowsAsync<AggregateException>(() => cleanup);
        Assert.Contains(error.InnerExceptions, e => e.Message == "create failed");
        Assert.Contains(error.InnerExceptions, e => e.Message == "delete failed");
        Assert.Equal(1, lease.Disposals); Assert.Equal(1, removed);
    }

    private sealed class Lease : IDisposable
    {
        public int Disposals;
        public void Dispose() => Interlocked.Increment(ref Disposals);
    }
}
