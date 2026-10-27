using System.Diagnostics;
using System.Reflection;
using CAN.Adapters.NiXnet;
using CAN.CANopen.Cyclic;
using CAN.Runtime;
using MessagePack;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class SubmitOnlyCycleTests
{
    [Fact]
    public void Legacy_cycle_settings_explicitly_migrate_to_submit_only_without_erasing_other_values()
    {
        var plugin = new CycleStartPlugin();
        var legacy = new CyclicSetting { RequireOnBusConfirmation = true, PeriodMs = 17, SyncData = "\"00\"" };
        var serializer = plugin.CreateSerializer();
        var migrated = (CyclicSetting)serializer.Deserialize(MessagePackSerializer.Serialize(legacy), 1);
        Assert.Equal(2, serializer.SettingVersion); Assert.False(migrated.RequireOnBusConfirmation);
        Assert.Equal(17, migrated.PeriodMs); Assert.Equal("\"00\"", migrated.SyncData);
        var explicitDiagnostic = (CyclicSetting)serializer.Deserialize(serializer.Serialize(legacy), 2);
        Assert.True(explicitDiagnostic.RequireOnBusConfirmation);
        Assert.False(((CyclicSetting)serializer.CreateDefault()).RequireOnBusConfirmation);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ni_cycle_submits_one_ordered_buffer_without_echo_or_session_wait(bool echoCapability)
    {
        var io = new NiBatchEvidenceTests.Driver { EchoCount = 0, SessionDone = false };
        var adapter = new NiXnetAdapter(io, 1, 2);
        typeof(NiXnetAdapter).GetField("_echoTxEnabled", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(adapter, echoCapability);
        var owner = new CanConnectionResource(adapter);
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(10000, 5000, 1000, false, 60000));
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 1);
            Assert.Equal(1, io.Calls); Assert.Equal(0, io.SingleCalls);
            Assert.Equal(new uint[] { 0x201, 0x80 }, io.Ids);
            var batch = service.Snapshot().Cycle.Timing!.Batch!;
            Assert.Equal("NiXnetOrderedSubmissionOnly", batch.Strategy);
            Assert.Equal(0, batch.SessionCompletionQueries); Assert.Equal(0, batch.ConfirmedFrames);
            Assert.True(batch.NativeExited); Assert.Equal(2, batch.KnownAcceptedFrames);
            Assert.Equal(0, service.Snapshot().Cycle.ConfirmedFrames);
            Assert.Equal(service.Snapshot().Cycle.Timing!.AdapterBatchMs, service.Snapshot().Cycle.Timing!.HostPathMs);
            await service.StopTargetsAsync(); await service.CloseAsync();
            Assert.True(service.IsClosed); Assert.Equal(0, service.Snapshot().Cycle.ConfirmedFrames);
            await owner.CloseAsync(); Assert.True(owner.IsClosed);
        }
        finally { await Close(owner); }
    }

    [Fact]
    public async Task Submit_only_monitor_motion_and_Sdo_stop_close_and_restart_without_inventing_confirmation()
    {
        var raw = new FakeAdapter();
        raw.OnWrite = frame =>
        {
            if (frame.Id == 0x601)
                raw.Incoming.Add(new() { Id = 0x581, Data = [0x60, frame.Data[1], frame.Data[2], frame.Data[3], 0, 0, 0, 0] });
        };
        var owner = new CanConnectionResource(raw);
        try
        {
            var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
            service.StartMonitoring(new(0x80, [0]), new(10000, 5000, 1000) { RequireOnBusConfirmation = false });
            await Until(() => service.Snapshot().Cycle.CompletedCycles == 1);
            for (int run = 0; run < 2; run++)
            {
                long previous = service.Snapshot().Cycle.CompletedCycles;
                service.StartConfiguredTargets([1], [new(0x201, [(byte)run])], new(0x80, [0]), new(10000, 5000, 1000, false, 60000),
                    new([new(1, 0x6040, 0, [0])], 2000));
                await Until(() => service.Snapshot().Cycle.CompletedCycles > previous);
                await service.StopTargetsAsync();
                Assert.True(service.Snapshot().Cycle.StopCommandsAcknowledged);
                Assert.False(service.Snapshot().Cycle.MotorStoppedVerified);
            }
            await owner.StopServiceAsync("test", 5000, default); Assert.True(service.IsClosed);
            Assert.Equal(0, service.Snapshot().Cycle.ConfirmedFrames);
            var next = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
            await next.CloseAsync();
            await owner.CloseAsync(); Assert.True(owner.IsClosed);
        }
        finally { await Close(owner); }
    }

    private static async Task Until(Func<bool> check)
    { using var guard = new CancellationTokenSource(5000); while (!check()) await Task.Delay(2, guard.Token); }
    private static async Task Close(CanConnectionResource owner)
    { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }

    [Fact]
    public async Task Submit_only_wait_timeout_retains_real_native_ownership_until_the_call_exits()
    {
        var io = new NiBatchEvidenceTests.Driver { BlockNative = true, EchoCount = 0 };
        var owner = new CanConnectionResource(new NiXnetAdapter(io, 1, 2));
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(10000, 5000, 1000, false, 60000));
            await io.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => service.StopTargetsAsync(30));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
            Assert.False(owner.IsClosed); Assert.Equal(0, io.Cleared);
            Assert.False(service.Snapshot().Cycle.HostExited); Assert.Equal(0, service.Snapshot().Cycle.ConfirmedFrames);
        }
        finally { io.Release.Set(); await Close(owner); }
        Assert.True(owner.IsClosed); Assert.Equal(2, io.Cleared);
    }

    [Fact]
    public async Task The_same_submission_budget_is_rechecked_after_a_slow_final_diagnostic_callback()
    {
        var io = new NiBatchEvidenceTests.Driver { EchoCount = 0 };
        var adapter = new NiXnetAdapter(io, 1, 2);
        try
        {
            Assert.Throws<TimeoutException>(() => ((ICanBudgetedBatchSubmitter)adapter).WriteBatchSubmitted(
                [new() { Id = 0x80, Data = [0] }], CanConfirmationBudget.Start(100, default), default, () => { }, _ => { },
                evidence => { if (evidence.Phase == "Completed") Thread.Sleep(130); }));
            Assert.Equal(1, io.Calls); Assert.Equal(0, io.SingleCalls);
        }
        finally { adapter.Close(); }
    }

    [Fact]
    public void A_completed_diagnostic_mode_cannot_make_later_submit_only_traffic_register_strict_Sdo_tickets()
    {
        var io = new NiBatchEvidenceTests.Driver { EchoCount = 0 };
        var adapter = new NiXnetAdapter(io, 1, 2);
        try
        {
            adapter.VerifyConfirmationAvailable();
            ((ICanBudgetedBatchSubmitter)adapter).WriteBatchSubmitted([new() { Id = 0x80, Data = [0] }],
                CanConfirmationBudget.Start(5000, default), default, () => { }, _ => { }, _ => { });
            adapter.Write(new() { Id = 0x601, Data = [0x40, 0x41, 0x60, 0, 0, 0, 0, 0] });
            Assert.False((bool)typeof(NiXnetAdapter).GetField("_strictEnabled", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!);
            var ledger = (NiTransmitLedger)typeof(NiXnetAdapter).GetField("_transmits", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
            Assert.Equal(0, ledger.PendingCount); Assert.False(io.QueriedAfterWrite.Task.IsCompleted);
        }
        finally { try { adapter.Close(); } catch (Exception) when (adapter.HandlesReleased) { } }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pending_or_unknown_strict_transmits_cannot_be_erased_by_entering_submit_only(bool unknown)
    {
        var io = new NiBatchEvidenceTests.Driver { EchoCount = 0 }; var adapter = new NiXnetAdapter(io, 1, 2);
        try
        {
            adapter.VerifyConfirmationAvailable();
            if (unknown) io.Status = -1;
            void Write() => adapter.Write(new() { Id = 0x601, Data = [1] });
            if (unknown) Assert.Throws<InvalidOperationException>(Write); else Write();
            Assert.Throws<InvalidOperationException>(() => ((ICanBudgetedBatchSubmitter)adapter).WriteBatchSubmitted(
                [new() { Id = 0x80, Data = [0] }], CanConfirmationBudget.Start(5000, default), default, () => { }, _ => { }, _ => { }));
            Assert.Equal(0, io.Calls);
            Assert.True((bool)typeof(NiXnetAdapter).GetField("_strictEnabled", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!);
            Assert.Equal(1, ((NiTransmitLedger)typeof(NiXnetAdapter).GetField("_transmits", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!).PendingCount);
        }
        finally { try { adapter.Close(); } catch (Exception) when (adapter.HandlesReleased) { } }
    }

    [Fact]
    public void Reentering_diagnostic_mode_must_drain_untracked_submissions_before_enabling_strict_writes()
    {
        var io = new NiBatchEvidenceTests.Driver { EchoCount = 0, SessionDone = false }; var adapter = new NiXnetAdapter(io, 1, 2);
        try
        {
            ((ICanBudgetedBatchSubmitter)adapter).WriteBatchSubmitted([new() { Id = 0x80, Data = [0] }],
                CanConfirmationBudget.Start(5000, default), default, () => { }, _ => { }, _ => { });
            Assert.Throws<TimeoutException>(() => ((ICanBudgetedConfirmationProbe)adapter).VerifyConfirmationAvailable(CanConfirmationBudget.Start(200, default), default));
            Assert.True(io.QueriedAfterWrite.Task.IsCompleted); Assert.Equal(1, io.Calls); Assert.Equal(0, io.SingleCalls);
            Assert.False((bool)typeof(NiXnetAdapter).GetField("_strictEnabled", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!);
        }
        finally { io.SessionDone = true; try { adapter.Close(); } catch (Exception) when (adapter.HandlesReleased) { } }
    }
}
