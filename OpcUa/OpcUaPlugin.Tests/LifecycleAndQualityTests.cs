using Opc.Ua;
using OpcUa.Helpers;
using OpcUa.Models;
using Xunit;

namespace OpcUa.Tests;

public sealed class LifecycleAndQualityTests
{
    [Fact]
    public async Task SdkCancellationIsNotRelabelledAsInternalDeadline()
    {
        var resource = new OpcUaConnectionResource(() => Task.CompletedTask, 1000);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resource.RunAsync(
            _ => Task.FromCanceled<int>(new CancellationToken(true)), 1000, "SDK cancelled", default));
        await resource.CloseAsync(1000);
    }

    [Fact]
    public async Task ACancelledSdkCloseCanBeRetriedOnSameResource()
    {
        int calls = 0;
        var resource = new OpcUaConnectionResource(() => Interlocked.Increment(ref calls) == 1
            ? Task.FromCanceled(new CancellationToken(true)) : Task.CompletedTask, 1000);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resource.CloseAsync(1000));
        Assert.False(resource.IsClosed);
        await resource.CloseAsync(1000);
        Assert.True(resource.IsClosed); Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(StatusCodes.BadNoCommunication)]
    [InlineData(StatusCodes.UncertainLastUsableValue)]
    public void UnusableQualityCannotSatisfyNotEqual(uint status) =>
        Assert.False(OpcUaHelper.CompareValue(new DataValue { StatusCode = status }, "0", OpcUaCompareMode.NotEqual));

    [Fact]
    public void GoodTypedValueMatchesWithoutLosingItsType() =>
        Assert.True(OpcUaHelper.CompareValue(new DataValue(new Variant(12)), "10", OpcUaCompareMode.GreaterThan));

    [Fact]
    public async Task AcquisitionPreservesStatusAndSourceTime()
    {
        var source = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        int calls = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = new OpcUaDataAcqTask("quality", async ct =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                return new DataValueCollection { new() { Value = 123, StatusCode = StatusCodes.BadNoCommunication, SourceTimestamp = source } };
            ready.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return new();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var record = Assert.Single(task.Read(-1));
            Assert.Equal(StatusCodes.BadNoCommunication, Assert.Single(record.StatusCodes));
            Assert.Equal(source, Assert.Single(record.SourceTimestamps));
            Assert.True(task.ConsecutiveFailures > 0);
            Assert.False(string.IsNullOrWhiteSpace(task.LastError));
        }
        finally { await task.StopAsync(1000); task.Dispose(); }
    }

    [Fact]
    public async Task StopCannotClaimExitWhileReaderIgnoresCancellation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<DataValueCollection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = new OpcUaDataAcqTask("pending", _ => { entered.TrySetResult(); return release.Task; },
            [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<TimeoutException>(() => task.StopAsync(20));
            Assert.False(task.HasExited);
        }
        finally
        {
            release.TrySetResult(new() { new(new Variant(5)) });
            await task.StopAsync(1000); task.Dispose();
        }
        Assert.True(task.HasExited); Assert.Equal(0, task.SampleCount);
    }

    [Fact]
    public async Task FailedExportDoesNotConsumeFifo()
    {
        int calls = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = new OpcUaDataAcqTask("fifo", async ct =>
        {
            if (Interlocked.Increment(ref calls) == 1) return new() { new(new Variant(5)) };
            ready.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return new();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<InvalidOperationException>(() => task.ConsumeAsync<int>(-1,
                _ => throw new InvalidOperationException("export failed"), default));
            Assert.Equal(1, task.SampleCount);
        }
        finally { await task.StopAsync(1000); task.Dispose(); }
    }

    [Fact]
    public async Task ConnectionDeadlineTracksOriginalTaskUntilItActuallyExits()
    {
        int closed = 0;
        var resource = new OpcUaConnectionResource(() => { Interlocked.Increment(ref closed); return Task.CompletedTask; }, 1000);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = resource.RunAsync(_ => { entered.TrySetResult(); return release.Task; }, 100, "fake read", default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<TimeoutException>(() => read);
            await Assert.ThrowsAsync<InvalidOperationException>(() => resource.RunAsync(_ => Task.FromResult(2), 100, "new read", default));
            await Assert.ThrowsAsync<TimeoutException>(() => resource.CloseAsync(20));
            Assert.Equal(0, closed);
        }
        finally { release.TrySetResult(1); await resource.CloseAsync(1000); }
        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task StopDeadlineAlsoCoversWaitingForAnActiveConsumer()
    {
        int calls = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = new OpcUaDataAcqTask("consumer", async ct =>
        {
            if (Interlocked.Increment(ref calls) == 1) return new() { new(new Variant(5)) };
            ready.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return new();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var consume = task.ConsumeAsync(-1, _ => { entered.TrySetResult(); return release.Task; }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var stopping = Task.Run(() => task.StopAsync(20));
        try
        {
            Assert.Same(stopping, await Task.WhenAny(stopping, Task.Delay(300)));
            await Assert.ThrowsAsync<TimeoutException>(() => stopping);
            await Assert.ThrowsAsync<InvalidOperationException>(() => consume.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(task.Completion.IsCompleted);
        }
        finally { release.TrySetResult(1); await Assert.ThrowsAsync<InvalidOperationException>(() => consume); try { await stopping; } catch (TimeoutException) { } task.Dispose(); }
    }
}
