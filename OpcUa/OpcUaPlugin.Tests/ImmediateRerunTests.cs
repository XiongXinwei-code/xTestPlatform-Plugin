using OpcUa.Helpers;
using Xunit;

namespace OpcUa.Tests;

public class ImmediateRerunTests
{
    [Fact]
    public async Task SameLogicalConnectionWaitsForActualPreviousCloseBeforeCreatingSession()
    {
        string key = "OPC:opc.tcp://unit.test:4840/:" + Guid.NewGuid();
        var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new OpcUaConnectionResource(() => { entered.TrySetResult(); return closing.Task; }, 1000, retirementKey: key);
        old.Dispose();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var next = new OpcUaConnectionResource(2000, retirementKey: key);
        int created = 0;
        Task opening = next.InitializeAsync(_ => { Interlocked.Increment(ref created); throw new MarkerException(); }, 2000, default);
        try
        {
            await Task.Delay(100);
            Assert.False(opening.IsCompleted);
            Assert.Equal(0, created);
            closing.TrySetResult();
            await Assert.ThrowsAsync<MarkerException>(() => opening);
            Assert.Equal(1, created);
        }
        finally { closing.TrySetResult(); await old.CloseAsync(2000); await next.CloseAsync(2000); }
    }

    [Fact]
    public async Task DifferentLogicalConnectionsOnSameEndpointDoNotCloseOrWaitForEachOther()
    {
        string endpoint = "OPC:opc.tcp://unit.test:4840/:" + Guid.NewGuid();
        var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int closeA = 0, closeB = 0;
        var a = new OpcUaConnectionResource(() => { Interlocked.Increment(ref closeA); return closing.Task; }, 1000, retirementKey: endpoint + ":A");
        var b = new OpcUaConnectionResource(() => { Interlocked.Increment(ref closeB); return Task.CompletedTask; }, 1000, retirementKey: endpoint + ":B");
        try
        {
            a.Dispose();
            await Assert.ThrowsAsync<MarkerException>(() => b.InitializeAsync(_ => throw new MarkerException(), 1000, default));
            Assert.Equal(0, closeB);
        }
        finally { closing.TrySetResult(); await a.CloseAsync(2000); await b.CloseAsync(2000); }
        Assert.Equal(1, closeA);
        Assert.Equal(1, closeB);
    }

    private sealed class MarkerException : Exception;
}
