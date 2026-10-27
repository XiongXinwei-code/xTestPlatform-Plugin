using System.IO;
using Opc.Ua;
using OpcUa.Executors;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Models;
using Xunit;

namespace OpcUa.Tests;

public sealed class CloseOutcomeTests
{
    [Fact]
    public async Task Remote_close_failure_does_not_skip_local_dispose()
    {
        int disposed = 0;
        var shutdown = new OpcUaSessionShutdown((_, _) => Task.FromException<StatusCode>(new IOException("remote session unavailable")),
            () => Interlocked.Increment(ref disposed));

        await shutdown.CloseAsync();

        Assert.False(shutdown.RemoteCloseCallSucceeded);
        Assert.Equal(1, disposed);
    }

    [Fact]
    public async Task Failed_dispose_cannot_be_retried_as_a_fake_successful_noop()
    {
        int disposed = 0;
        var shutdown = new OpcUaSessionShutdown((_, _) => Task.FromResult<StatusCode>(StatusCodes.Good), () =>
        {
            if (Interlocked.Increment(ref disposed) == 1) throw new IOException("SDK release failed before all resources exited");
        });
        await Assert.ThrowsAnyAsync<Exception>(shutdown.CloseAsync);
        await Assert.ThrowsAnyAsync<Exception>(shutdown.CloseAsync);
        Assert.Equal(1, disposed);
    }

    [Fact]
    public async Task Actual_sdk_shape_uses_remote_close_without_implicit_dispose()
    {
        bool? closeChannelArgument = null;
        int disposes = 0;
        bool markedDisposed = false;
        void Dispose()
        {
            Interlocked.Increment(ref disposes);
            if (markedDisposed) return;
            markedDisposed = true;
            throw new IOException("first Dispose marked disposed but did not release all SDK resources");
        }
        var shutdown = new OpcUaSessionShutdown((closeChannel, _) =>
        {
            closeChannelArgument = closeChannel;
            if (closeChannel) Dispose(); // Models the real SDK's default overload.
            return Task.FromResult<StatusCode>(StatusCodes.Good);
        }, Dispose);

        await Assert.ThrowsAnyAsync<Exception>(shutdown.CloseAsync);
        await Assert.ThrowsAnyAsync<Exception>(shutdown.CloseAsync);
        Assert.False(closeChannelArgument);
        Assert.Equal(1, disposes);
    }

    [Fact]
    public async Task Lifetime_callback_failure_does_not_skip_connection_release()
    {
        int released = 0;
        var owner = new OpcUaConnectionResource(() => { Interlocked.Increment(ref released); return Task.CompletedTask; }, 2000);
        using var registration = owner.LifetimeToken.Register(() => throw new IOException("lifetime callback returned failure"));

        await owner.CloseAsync(2000);
        await owner.CloseAsync(2000);

        Assert.True(owner.IsClosed);
        Assert.Equal(1, released);
    }

    [Fact]
    public async Task Acquisition_stop_uses_actual_exit_and_keeps_callback_failure_as_diagnostic()
    {
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = new OpcUaDataAcqTask("T", async token =>
        {
            using var registration = token.Register(() => throw new IOException("read callback returned failure"));
            reading.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new DataValueCollection();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
        try
        {
            await reading.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var (context, _) = BoundaryContext.Create(new OpcUaDataAcqStopPlugin(),
                new OpcUaDataAcqStopSetting { TaskName = "\"T\"", TimeoutMs = 10000 });
            context.Resources.Set("OpcUaDataAcq_T", task);

            var result = await new OpcUaDataAcqStopExecutor().ExecuteAsync(context);

            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.NotNull(task.CancellationError);
            Assert.False(context.Resources.TryGet<OpcUaDataAcqTask>("OpcUaDataAcq_T", out _));
        }
        finally { task.Dispose(); await task.CloseCompletion.WaitAsync(TimeSpan.FromSeconds(3)); }
    }
}
