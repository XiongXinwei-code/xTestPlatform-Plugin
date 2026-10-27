using System.Diagnostics;
using System.Reflection;
using CAN.Runtime;
using CAN.CANopen;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class MultipleSyncSourceTests
{
    [Fact]
    public async Task Three_sources_can_share_a_Sync_id_and_stop_independently_without_stopping_the_receiver()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            var sources = new List<CanopenCyclicService>();
            for (byte number = 1; number <= 3; number++)
            {
                var service = owner.StartService("sync" + number, [new("feedback", 0x181, 1, 1000)]);
                sources.Add(service);
                service.StartMonitoring(new(0x80, [number]), new(10000, 5000, 1000) { RequireOnBusConfirmation = false });
            }
            await Until(() => sources.All(service => service.Snapshot().Cycle.CompletedCycles == 1));
            await owner.StopServiceAsync("sync1", 5000, default);
            await owner.WriteAsync(new() { Id = 0x80, Data = [9] }, 1000);
            raw.Incoming.Add(new() { Id = 0x181, Data = [7] });
            await Until(() => sources[1].Snapshot().Receive.Frames[0].Sequence > 0 && sources[2].Snapshot().Receive.Frames[0].Sequence > 0);
            await Due(sources[1]); await Due(sources[2]);
            await Until(() => sources[1].Snapshot().Cycle.CompletedCycles == 2 && sources[2].Snapshot().Cycle.CompletedCycles == 2);
            Assert.Equal(1, raw.Sent.Count(frame => frame.Id == 0x80 && frame.Data[0] == 1));
            Assert.Equal(2, raw.Sent.Count(frame => frame.Id == 0x80 && frame.Data[0] == 2));
            await owner.StopServiceAsync("sync2", 5000, default); await owner.StopServiceAsync("sync3", 5000, default);
            await owner.WriteAsync(new() { Id = 0x80, Data = [9] }, 1000);
            await owner.CloseAsync(); Assert.True(owner.IsClosed);
        }
        finally { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task A_pure_Sync_source_does_not_require_a_feedback_route_or_a_reader()
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        try
        {
            var service = owner.StartService("pure", []);
            service.StartMonitoring(new(0x80, [0]), new(10000, 5000, 1000) { RequireOnBusConfirmation = false });
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 1);
            Assert.Empty(service.Snapshot().Receive.Frames);
            await owner.StopServiceAsync("pure", 5000, default);
            await owner.CloseAsync(); Assert.True(owner.IsClosed);
        }
        finally { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    private static async Task Due(CanopenCyclicService service)
    {
        var gate = (SemaphoreSlim)typeof(CanopenCyclicService).GetField("_tickGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        await gate.WaitAsync();
        try
        {
            typeof(CanopenCyclicService).GetField("_nextSync", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, Stopwatch.GetTimestamp());
            typeof(CanopenCyclicService).GetMethod("WakeSchedule", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, null);
        }
        finally { gate.Release(); }
    }
    private static async Task Until(Func<bool> check)
    { using var guard = new CancellationTokenSource(5000); while (!check()) await Task.Delay(2, guard.Token); }

    [Fact]
    public async Task Independent_Sdo_reads_can_observe_a_cyclic_sender_without_taking_its_control_ownership()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        raw.OnWrite = frame =>
        {
            if (frame.Id == 0x601)
                raw.Incoming.Add(new() { Id = 0x581, Data = frame.Data[0] == 0x40
                    ? [0x4B, frame.Data[1], frame.Data[2], frame.Data[3], 0x27, 0, 0, 0]
                    : [0x60, frame.Data[1], frame.Data[2], frame.Data[3], 0, 0, 0, 0] });
        };
        try
        {
            var service = owner.StartService("motion", []);
            service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(10000, 5000, 1000, false, 60000));
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 1);
            var reader = new CanopenSdoClient(new CanopenBus(owner), 1, 1000);
            Assert.Equal(new byte[] { 0x27, 0 }, await reader.UploadAsync(0x6041, 0, false, default));
            Assert.True(service.Snapshot().Cycle.MotionActive);
            await reader.DownloadAsync(0x6040, 0, [0], false, default);
            await service.StopTargetsAsync(); await owner.CloseAsync();
        }
        finally { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }
}
