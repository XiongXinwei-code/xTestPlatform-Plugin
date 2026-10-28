using System.Reflection;
using CAN.CANopen.Cyclic;
using CAN.Runtime;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class StartCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelDuringConfirmationAdmissionMustNotStartOutputs(bool monitoring)
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>();
        var state = (CloseContext)(object)context;
        var raw = new PreflightDriver(); var owner = new CanConnectionResource(raw);
        state.Adapter = owner;
        CyclicPluginBase plugin = monitoring ? new ServiceStartPlugin() : new CycleStartPlugin();
        state.Plugin = plugin;
        state.Setting = new CyclicSetting
        {
            ConnectionName = "\"CAN1\"", ServiceName = "\"one\"", EnableMonitoringSync = monitoring,
            CycleTimeoutMs = 3000, MaxLatenessMs = 1000, RequireOnBusConfirmation = true,
            FeedbackRoutes = [new() { Name = "feedback", CobId = "0x181", Dlc = 1 }],
            Targets = [new() { CobId = "0x201", Data = "\"01\"", Dlc = 1 }]
        };
        if (!monitoring) owner.StartService("one", [new("feedback", 0x181, 1, 100)]);
        using var stop = new CancellationTokenSource();
        var starting = Task.Run(() => new CyclicExecutor(plugin).ExecuteAsync(context, stop.Token));
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stop.Cancel(); raw.Release.Set();
            var result = await starting.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            Assert.Empty(raw.Sent);
            if (!monitoring) Assert.False(owner.GetService("one").Snapshot().Cycle.MotionActive);
        }
        finally
        {
            raw.Release.Set(); await starting;
            try { await owner.CloseAsync(); } catch (InvalidOperationException) when (owner.IsClosed) { }
        }
    }

    private sealed class PreflightDriver : FakeAdapter, ICanConfirmedTransmitter
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        public void VerifyConfirmationAvailable() { Entered.TrySetResult(); Release.Wait(); }
        public void WriteConfirmed(CanMessage frame, CancellationToken ct) => Write(frame);
    }
}
