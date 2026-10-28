using System.Diagnostics;
using System.IO;
using Opc.Ua;
using OpcUa.Helpers;
using Xunit;

namespace OpcUa.Tests;

[CollectionDefinition("OPC close diagnostics", DisableParallelization = true)]
public sealed class CloseDiagnosticCollection { }

[Collection("OPC close diagnostics")]
public sealed class CloseDiagnosticBoundaryTests
{
    [Fact]
    public async Task Diagnostic_listener_cannot_fault_actual_callback_exit()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var collector = new OpcUaDataAcqTask("T", async token =>
        {
            using var registration = token.Register(() => throw new IOException("callback already exited"));
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new DataValueCollection();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 10);
        var listener = new ThrowingListener();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Trace.Listeners.Add(listener);
            await collector.StopAsync(2000);
            Assert.NotNull(collector.CancellationError);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            collector.Dispose();
            await collector.CloseCompletion.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    [Fact]
    public async Task Parent_close_retains_exited_child_read_diagnostic()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int released = 0;
        var owner = new OpcUaConnectionResource(() => { released++; return Task.CompletedTask; }, 2000);
        owner.AcquisitionReader = async (_, token) =>
        {
            using var registration = token.Register(() => throw new IOException("child read callback failed"));
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new DataValueCollection();
        };
        var collector = owner.StartAcquisition("T", [new() { NodeId = "ns=2;s=V" }], 1, 0, 10);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await owner.CloseAsync(2000);
        Assert.True(owner.IsClosed);
        Assert.True(collector.CloseCompletion.IsCompletedSuccessfully);
        Assert.Equal(1, released);
        Assert.Contains("child read callback failed", owner.CloseWarning?.ToString() ?? "");
    }

    private sealed class ThrowingListener : TraceListener
    {
        public override void Write(string? message) => throw new IOException("diagnostic listener failed");
        public override void WriteLine(string? message) => Write(message);
    }
}
