using System.Diagnostics;
using System.Reflection;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class IndependentSyncConfigurationTests
{
    [Theory]
    [InlineData(0x80, 0x81, false)]
    [InlineData(0x80, 0x80, false)]
    [InlineData(0x80, 0x80, true)]
    public async Task Monitoring_and_command_Sync_keep_their_own_immutable_bytes(int backgroundId, int commandId, bool emptyCommand)
    {
        var (owner, service, raw) = await Start((uint)backgroundId);
        try
        {
            byte[] command = emptyCommand ? [] : [0x55];
            Motion(service, new((uint)commandId, command));
            if (!emptyCommand) command[0] = 0x99;
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 2);
            await Due(service, backgroundOnly: true);
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 3);
            var frames = raw.Sent.ToArray();
            Assert.Equal(new uint[] { (uint)backgroundId, 0x201, (uint)commandId, (uint)backgroundId }, frames.Select(f => f.Id));
            Assert.Equal(new byte[] { 0 }, frames[0].Data);
            Assert.Equal(emptyCommand ? [] : new byte[] { 0x55 }, frames[2].Data);
            Assert.Equal(new byte[] { 0 }, frames[3].Data);
            await owner.WriteAsync(new() { Id = (uint)backgroundId, Data = [0] }, 1000);
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task Rpdo_cannot_reuse_the_background_Sync_id_even_with_a_different_command_Sync()
    {
        var (owner, service, raw) = await Start(0x201);
        try
        {
            int before = raw.Sent.Count;
            Assert.Throws<ArgumentException>(() => Motion(service, new(0x80, [0])));
            Assert.Equal(before, raw.Sent.Count);
            Assert.Equal("Monitoring", service.Snapshot().Cycle.State);
        }
        finally { await Close(owner); }
    }

    [Theory]
    [InlineData(0x80, true)]
    [InlineData(0x700, false)]
    public async Task Simultaneous_due_compares_batch_head_ids_without_sorting_the_control_batch(int backgroundId, bool backgroundFirst)
    {
        var (owner, service, raw) = await Start((uint)backgroundId);
        try
        {
            Motion(service, new(0x81, [0x55]));
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 2);
            int before = raw.Sent.Count;
            await Due(service, backgroundOnly: false);
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 4);
            Assert.Equal(backgroundFirst ? new uint[] { (uint)backgroundId, 0x201, 0x81 }
                : new uint[] { 0x201, 0x81, (uint)backgroundId }, raw.Sent.Skip(before).Select(f => f.Id));
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task Restart_with_another_command_Sync_keeps_its_configuration_and_allows_manual_old_Sync()
    {
        var (owner, service, _) = await Start(0x80);
        try
        {
            Motion(service, new(0x81, [0x55]));
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 2);
            await service.StopTargetsAsync();
            Motion(service, new(0x82, [0x66]));
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 3);
            await owner.WriteAsync(new() { Id = 0x81, Data = [0] }, 1000);
            Assert.Equal(0x82u, service.Snapshot().Cycle.CommandSyncCobId);
        }
        finally { await Close(owner); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Mixed_confirmation_is_not_silently_accepted_without_a_stop_evidence_contract(bool backgroundConfirmed, bool motionConfirmed)
    {
        var (owner, service, _) = await Start(0x80, backgroundConfirmed);
        try
        {
            if (backgroundConfirmed == motionConfirmed)
            {
                Motion(service, new(0x81, [0x55]), motionConfirmed);
                await Until(() => service.Snapshot().Cycle.CompletedCycles == 2);
            }
            else Assert.Throws<ArgumentException>(() => Motion(service, new(0x81, [0x55]), motionConfirmed));
        }
        finally { await Close(owner); }
    }

    private static void Motion(CanopenCyclicService service, OrderedFrame sync, bool confirmed = true) => service.StartConfiguredTargets([1],
        [new(0x201, [1])], sync, new(10000, 5000, 1000, confirmed, 60000), new([new(1, 0x6040, 0, [0])], 2000));

    [Fact]
    public async Task Batch_purpose_and_layout_are_validated_even_when_both_Sync_ids_are_identical()
    {
        var (owner, service, _) = await Start(0x80);
        try
        {
            Motion(service, new(0x80, [0x55]));
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 2);
            service.ValidateSendBatch([new(0x80, [0])], CanBatchPurpose.Monitoring);
            Assert.Throws<ArgumentException>(() => service.ValidateSendBatch([new(0x80, [0x55])], CanBatchPurpose.Monitoring));
            Assert.Throws<ArgumentException>(() => service.ValidateSendBatch([new(0x80, [0])], CanBatchPurpose.Target));
            Assert.Throws<ArgumentException>(() => service.ValidateSendBatch([new(0x301, [1]), new(0x80, [0x55])], CanBatchPurpose.Target));
            Assert.Throws<ArgumentException>(() => service.ValidateSendBatch([new(0x80, [0])], (CanBatchPurpose)999));
            service.PublishTargets([new(0x201, [2])]);
            // An already captured old batch remains legal after a new value
            // is published; only the immutable PDO layout is checked here.
            service.ValidateSendBatch([new(0x201, [1]), new(0x80, [0x55])], CanBatchPurpose.Target);
            var cycle = service.Snapshot().Cycle;
            Assert.Equal((uint)0x80, cycle.MonitoringSyncCobId); Assert.Equal("00", cycle.MonitoringSyncDataHex);
            Assert.Equal((uint)0x80, cycle.CommandSyncCobId); Assert.Equal("55", cycle.CommandSyncDataHex);
            Assert.Equal("LowerBatchHeadCanId", cycle.SchedulingTieBreak);
        }
        finally { await Close(owner); }
    }

    private static async Task<(CanConnectionResource, CanopenCyclicService, ConfirmedAdapter)> Start(uint backgroundId, bool confirmed = true)
    {
        var raw = new ConfirmedAdapter();
        raw.OnWrite = frame =>
        {
            if (frame.Id == 0x601)
                raw.Incoming.Add(new() { Id = 0x581, Data = [0x60, frame.Data[1], frame.Data[2], frame.Data[3], 0, 0, 0, 0] });
        };
        var owner = new CanConnectionResource(raw);
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        byte[] bytes = [0];
        service.StartMonitoring(new(backgroundId, bytes), new(10000, 5000, 1000) { RequireOnBusConfirmation = confirmed });
        bytes[0] = 0x99;
        await Until(() => service.Snapshot().Cycle.CompletedCycles == 1);
        return (owner, service, raw);
    }

    private static async Task Due(CanopenCyclicService service, bool backgroundOnly)
    {
        var gate = (SemaphoreSlim)typeof(CanopenCyclicService).GetField("_tickGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        await gate.WaitAsync();
        try
        {
            long now = Stopwatch.GetTimestamp();
            foreach (string field in backgroundOnly ? new[] { "_nextSync" } : new[] { "_nextTarget", "_nextSync" })
                typeof(CanopenCyclicService).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, now);
            typeof(CanopenCyclicService).GetMethod("WakeSchedule", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, null);
        }
        finally { gate.Release(); }
    }
    private static async Task Until(Func<bool> check)
    { using var guard = new CancellationTokenSource(3000); while (!check()) await Task.Delay(2, guard.Token); }
    private static async Task Close(CanConnectionResource owner)
    { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
}
