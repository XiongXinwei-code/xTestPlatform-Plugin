using System.Reflection;
using CAN.CANopen;
using CAN.Models;
using CAN.Runtime;
using CAN.UDS;
using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class LifecycleReauditTests
{
    [Theory]
    [InlineData(255, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 128)]
    public async Task BlockDownloadRejectsImpossibleAcknowledgement(int sequence, int nextSize)
    {
        var raw = new FakeAdapter();
        raw.OnWrite = frame => raw.Incoming.Add(new() { Id = 0x581, Data = frame.Data[0] switch
        {
            0xC6 => [0xA0, 0, 0x20, 0, 1, 0, 0, 0],
            0x81 => [0xA2, (byte)sequence, (byte)nextSize, 0, 0, 0, 0, 0],
            _ => [0xA1, 0, 0, 0, 0, 0, 0, 0]
        } });
        var client = new CanopenSdoClient(new CanopenBus(raw), 1, 1000);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.DownloadAsync(0x2000, 0, [1], true, default));
        Assert.Equal(2, raw.Sent.Count); // No end-transfer request after invalid acknowledgement.
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.DownloadAsync(0x2000, 0, [2], true, default));
        Assert.Equal(2, raw.Sent.Count); // The failed transaction remains isolated.
    }

    [Theory]
    [InlineData("raw")]
    [InlineData("did")]
    [InlineData("routine")]
    public async Task NullUdsPayloadIsRejectedAsNullWithoutParsingItsSource(string kind)
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        var raw = new FakeAdapter(); state.Adapter = raw;
        if (kind == "raw") { state.Plugin = new UdsRawRequestPlugin(); state.Setting = new UdsRawRequestSetting { RequestData = "null", WaitResponse = false }; }
        else if (kind == "did") { state.Plugin = new UdsWriteDataByIdPlugin(); state.Setting = new UdsWriteDataByIdSetting { Data = "null" }; }
        else { state.Plugin = new UdsRoutineControlPlugin(); state.Setting = new UdsRoutineControlSetting { OptionRecord = "null" }; }
        var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Empty(raw.Sent);
        Assert.Contains("求值结果为 null", result.StepResult.Error!.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationInsideMissingResourceLookupRemainsAborted(bool write)
    {
        var actual = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var registry = DispatchProxy.Create<IResourceRegistry, LookupCanRegistry>(); var proxy = (LookupCanRegistry)(object)registry; proxy.Inner = actual;
        var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>(); var state = (SdkCanContext)(object)context; state.Registry = registry;
        state.Plugin = write ? new CanWritePlugin() : new CanReadPlugin(); state.Setting = state.Plugin.CreateSerializer().CreateDefault();
        using var caller = new CancellationTokenSource(); proxy.BeforeLookup = caller.Cancel;
        Assert.Equal(TestStatus.Aborted, (await state.Plugin.CreateExecutor().ExecuteAsync(context, caller.Token)).StepResult.Status);
        actual.GetType().GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null);
    }

    [Fact]
    public async Task ConnectionCloseHonorsConfiguredControlledStopBeyondFiveSeconds()
    {
        var raw = new ConfirmedAdapter(); var owner = new CanConnectionResource(raw);
        var firstCycle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task response = Task.CompletedTask;
        raw.OnWrite = frame =>
        {
            if (frame.Id == 0x80) firstCycle.TrySetResult();
            if (frame.Id == 0x601)
            {
                stopSent.TrySetResult();
                response = Task.Run(async () =>
                {
                    await Task.Delay(5500);
                    raw.Incoming.Add(new() { Id = 0x581, Data = [0x60, frame.Data[1], frame.Data[2], frame.Data[3], 0, 0, 0, 0] });
                });
            }
        };
        var service = owner.StartService("one", [new("A", 0x181, 1, 100)]);
        try
        {
            service.StartConfiguredTargets([1], [new(0x201, [1])], new(0x80, []), new(1000, 8000, 1000, true, 20000),
                new([new(1, 0x2000, 0, [1])], 8000)); // Synthetic object; no hardware control word.
            await firstCycle.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var close = owner.CloseAsync(10000);
            await stopSent.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await close;
            Assert.True(owner.IsClosed); Assert.True(service.Snapshot().Cycle.StopCommandsAcknowledged);
        }
        finally
        {
            await response;
            try { await owner.CloseAsync(10000); } catch (InvalidOperationException) when (owner.IsClosed) { }
        }
    }

    [Fact]
    public async Task CapturedOldServiceStopCannotCloseReplacement()
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        try
        {
            var old = owner.StartService("one", [new("A", 0x181, 1, 100)]);
            await owner.StopServiceAsync("one", 1000, default);
            var replacement = owner.StartService("one", [new("A", 0x181, 1, 100)]);
            await owner.StopServiceAsync("one", old, 1000, default);
            Assert.Same(replacement, owner.GetService("one"));
            Assert.False(replacement.IsClosed);
        }
        finally { await owner.CloseAsync(); }
    }
}
