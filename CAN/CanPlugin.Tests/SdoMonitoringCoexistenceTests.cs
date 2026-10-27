using System.Reflection;
using CAN.Adapters.NiXnet;
using CAN.CANopen.Cyclic;
using CAN.Helpers;
using CAN.Executors;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class SdoMonitoringCoexistenceTests
{
    private static void Close(NiXnetAdapter adapter) { try { adapter.Close(); } catch (Exception) when (adapter.HandlesReleased) { } }
    private static IResourceRegistry Registry() => (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
        .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;

    [Fact]
    public async Task Missing_prior_Sdo_echo_isolates_before_a_queued_foreign_write_and_keeps_the_original_fault()
    {
        var io = new Driver(); var adapter = new NiXnetAdapter(io, 1, 2); var owner = new CanConnectionResource(adapter);
        var service = owner.StartService("old", [new("feedback", 0x181, 1, 1000)]);
        var tick = (SemaphoreSlim)typeof(CanopenCyclicService).GetField("_tickGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        await tick.WaitAsync();
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10000, 5000, 1000));
            io.AllowReads = false;
            owner.Write(new() { Id = 0x601, Data = [0x40, 0x41, 0x60, 0, 0, 0, 0, 0] });
            // Hold the real worker at its tick gate and drive exactly one batch;
            // the connection/adapter locks and late foreign writer are real.
            var sending = Task.Run(() => owner.SendBatch(service, [new(0x80, [0])], true, default, _ => { }, CanConfirmationBudget.Start(300, default)));
            await Task.Delay(100);
            var foreign = Task.Run(() => owner.Write(new() { Id = 0x602, Data = [1] }));
            await Assert.ThrowsAsync<TimeoutException>(() => sending);
            await Assert.ThrowsAsync<InvalidOperationException>(() => foreign);
            Assert.Equal(1, io.Inner.SingleCalls); Assert.Equal(0, io.Inner.Calls);
            Assert.NotNull(owner.IsolationReason);
            Assert.Contains("TimeoutException", service.Snapshot().Cycle.Error);
            Assert.False(adapter.ConfirmationAvailable);
        }
        finally { io.AllowReads = true; tick.Release(); try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task A_queued_foreign_write_cannot_overtake_a_partially_completed_legacy_batch()
    {
        using var release = new ManualResetEventSlim();
        var raw = new ConfirmedAdapter { WriteBlock = release, OnWrite = frame =>
            { if (frame.Id == 0x80) throw new InvalidOperationException("fake Sync native failure after RPDO"); } };
        var owner = new CanConnectionResource(raw);
        var service = owner.StartService("partial", [new("feedback", 0x181, 1, 1000)]);
        try
        {
            service.StartTargets([1], [new(0x201, [1])], new(0x80, [0]), new(10000, 5000, 1000, true, 20000));
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var foreign = Task.Run(() => owner.Write(new() { Id = 0x601, Data = [1] }));
            var stopped = service.StopTargetsAsync(5000);
            release.Set();
            await Assert.ThrowsAnyAsync<Exception>(() => stopped);
            await Assert.ThrowsAsync<InvalidOperationException>(() => foreign);
            Assert.Equal(new uint[] { 0x201 }, raw.Sent.Select(frame => frame.Id));
            Assert.True(service.Snapshot().Cycle.PartialCycle);
            Assert.NotNull(service.Snapshot().Cycle.Error); Assert.NotNull(owner.IsolationReason);
        }
        finally { release.Set(); try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task Actual_IO_CanClose_needs_no_Sdo_after_isolation_and_allows_first_reopen()
    {
        var io = new Driver(); var raw = new NiXnetAdapter(io, 1, 2);
        string channel = "CAN" + Random.Shared.Next(10000, int.MaxValue);
        string physical = CanChannelIdentity.Normalize(CanAdapterType.NI, channel);
        var owner = new CanConnectionResource(raw, physical); var registry = Registry();
        registry.Set(CanHelper.GetAdapterKey("CAN1"), owner);
        try
        {
            owner.Isolate("fake retained old communication failure");
            Assert.Throws<InvalidOperationException>(() => owner.Write(new() { Id = 0x601, Data = [0x40, 0x41, 0x60] }));
            var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>(); var state = (SdkCanContext)(object)context;
            state.Registry = registry; state.Plugin = new CanClosePlugin(); state.Setting = new CanCloseSetting { TimeoutMs = 5000 };
            var result = await new CanCloseExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.True(owner.IsClosed); Assert.Equal(2, io.Inner.Cleared);
            Assert.Equal(0, io.Inner.SingleCalls); Assert.Equal(0, io.Inner.Calls);
            Assert.False(registry.Contains(CanHelper.GetAdapterKey("CAN1")));
            var next = new CanConnectionResource(new FakeAdapter(), physical);
            await next.CloseAsync(); Assert.True(next.IsClosed);
        }
        finally { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task An_old_Sdo_echo_can_arrive_within_the_batch_budget_without_rejecting_the_new_batch()
    {
        var io = new Driver { AllowReads = false }; var adapter = new NiXnetAdapter(io, 1, 2);
        Task? sending = null;
        try
        {
            adapter.VerifyConfirmationAvailable();
            adapter.Write(new() { Id = 0x601, Data = [0x40, 0x41, 0x60, 0, 0, 0, 0, 0] });
            sending = Task.Run(() => ((ICanBudgetedConfirmedBatchTransmitter)adapter).WriteBatchConfirmed(
                [new() { Id = 0x80, Data = [0] }], CanConfirmationBudget.Start(5000, default), default, () => { }, _ => { }, _ => { }));
            await Task.Delay(100);
            Assert.False(sending.IsCompleted); Assert.Equal(0, io.Inner.Calls);
            io.AllowReads = true; await sending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new uint[] { 0x601, 0x80 }, io.Inner.Ids);
        }
        finally { io.AllowReads = true; if (sending != null) try { await sending; } catch (Exception) { } Close(adapter); }
    }

    [Fact]
    public async Task ServiceStart_false_uses_host_submission_without_a_confirmed_transmitter()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); var registry = Registry();
        registry.Set(CanHelper.GetAdapterKey("CAN1"), owner);
        var plugin = new ServiceStartPlugin(); var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>();
        var state = (SdkCanContext)(object)context;
        state.Registry = registry; state.Plugin = plugin;
        state.Setting = new CyclicSetting { EnableMonitoringSync = true, RequireOnBusConfirmation = false,
            ServiceName = "\"test\"", CycleTimeoutMs = 1000, MaxLatenessMs = 1000,
            FeedbackRoutes = [new() { Name = "feedback", CobId = "0x181", Dlc = 1 }] };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var ready = new CancellationTokenSource(5000);
            while (owner.GetService("test").Snapshot().Cycle.Timing?.Batch == null) await Task.Delay(2, ready.Token);
            var snapshot = owner.GetService("test").Snapshot();
            Assert.Equal("UnconfirmedSubmission", snapshot.Cycle.Timing!.Batch!.Strategy);
            Assert.Equal(0, snapshot.Cycle.ConfirmedFrames);
            Assert.Null(owner.IsolationReason);
        }
        finally { try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public async Task A_late_but_fully_confirmed_monitoring_batch_faults_service_without_poisoning_connection()
    {
        var io = new Driver(); var owner = new CanConnectionResource(new NiXnetAdapter(io, 1, 2));
        var service = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
        using var release = new ManualResetEventSlim(); int blocked = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        io.QueryAfterWrite = () => { if (Interlocked.Exchange(ref blocked, 1) == 0) { entered.TrySetResult(); if (!release.Wait(10000)) throw new TimeoutException("fake query blocked"); } };
        try
        {
            service.StartMonitoring(new(0x80, [0]), new(10, 1000, 1000));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var guard = new CancellationTokenSource(5000);
            while (service.Snapshot().Cycle.Timing?.DeadlineObservedWithPendingSendTask != true) await Task.Delay(2, guard.Token);
            Assert.Null(owner.IsolationReason); Assert.False(service.Snapshot().Cycle.HostExited);
            release.Set();
            while (!service.Snapshot().Cycle.HostExited) await Task.Delay(2, guard.Token);
            var snapshot = service.Snapshot();
            Assert.NotNull(snapshot.Cycle.Error); Assert.Equal(snapshot.Cycle.SubmittedFrames, snapshot.Cycle.ConfirmedFrames);
            Assert.Null(owner.IsolationReason);
            try { await owner.StopServiceAsync("test", service, 5000, default); } catch (Exception) when (service.IsClosed) { }
            Assert.True(service.IsClosed);
            var next = owner.StartService("test", [new("feedback", 0x181, 1, 1000)]);
            await next.CloseAsync();
        }
        finally { release.Set(); try { await owner.CloseAsync(); } catch (Exception) when (owner.IsClosed) { } }
    }

    [Fact]
    public void Error_summary_contains_the_actual_exit_and_confirmation_mode_fields()
    {
        var cycle = new CycleStatus("Faulted", 1, 0, 0, 1, 0, 0, 1, "MonitoringSYNC", "failure", true, null)
        { Timing = new("MonitoringSYNC", 10, 20, 100) { SendTaskExited = true,
            Batch = new(Guid.NewGuid(), "NiXnetOrderedRawBatch", 1, "ConfirmationWait") { NativeExited = true } } };
        var snapshot = new CommunicationSnapshot(new(Guid.NewGuid(), Guid.NewGuid(), true, [], 0, 0, 0, new(null, null, null, null), null), cycle);
        Assert.Contains("NativeExited=True", snapshot.ErrorSummary);
        Assert.Contains("SendTaskExited=True", snapshot.ErrorSummary);
        Assert.Contains("HostExited=True", snapshot.ErrorSummary);
    }

    private sealed class Driver : NiXnetIo
    {
        internal readonly NiBatchEvidenceTests.Driver Inner = new();
        internal volatile bool AllowReads = true;
        internal Action? QueryAfterWrite;
        public override int Read(uint session, byte[] bytes, out uint count)
        { if (AllowReads) return Inner.Read(session, bytes, out count); count = 0; return 0; }
        public override int Write(uint session, byte[] bytes) => Inner.Write(session, bytes);
        public override int WriteBatch(uint session, byte[] bytes, double timeout) => Inner.WriteBatch(session, bytes, timeout);
        public override int ReadTime(uint session, out ulong time, out int fault) => Inner.ReadTime(session, out time, out fault);
        public override int TransmitComplete(uint session) { if (Inner.Calls > 0) QueryAfterWrite?.Invoke(); return Inner.TransmitComplete(session); }
        public override int Stop(uint session) => Inner.Stop(session);
        public override int Clear(uint session) => Inner.Clear(session);
        public override void Check(int status) => Inner.Check(status);
    }
}
