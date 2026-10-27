using System.Reflection;
using System.Buffers.Binary;
using System.Diagnostics;
using CAN.Adapters;
using CAN.Adapters.NiXnet;
using CAN.Adapters.Zlg;
using CAN.Models;
using CAN.Runtime;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class ConfirmationAdmissionFollowupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_batch_and_owner_commands_use_their_original_shared_budget(bool controlledStop)
    {
        using var guard = new CancellationTokenSource(10000);
        var raw = new RecordingAdapter();
        var owner = new CanConnectionResource(raw);
        raw.OnWrite = frame =>
        {
            if (frame.Id == 0x601) raw.Incoming.Add(new() { Id = 0x581, Data = [0x60, frame.Data[1], frame.Data[2], frame.Data[3], 0, 0, 0, 0] });
        };
        var service = owner.StartService("budget", [new("one", 0x181, 1, 100)]);
        try
        {
            service.StartConfiguredTargets([1], [new(0x201, [1]), new(0x301, [2])], new(0x80, []),
                new(10000, 3000, 1000, true, 20000), controlledStop ? new([new(1, 0x2000, 0, [1])], 8000) : null);
            await raw.First.Task.WaitAsync(guard.Token);
            await Task.Delay(200, guard.Token); raw.Release.Set();
            while (service.Snapshot().Cycle.CompletedCycles == 0) await Task.Delay(1, guard.Token);
            await service.StopTargetsAsync(8000, guard.Token);
            Assert.Equal(0, raw.PublicCalls);
            var cycle = raw.Budgets.Where(x => x.Id != 0x601).ToArray();
            Assert.Equal(3, cycle.Length);
            Assert.All(cycle, x => { Assert.Equal(3000, x.Budget.TimeoutMs); Assert.Equal(cycle[0].Budget.Started, x.Budget.Started); });
            Assert.True(cycle[0].Remaining - cycle[1].Remaining >= 100, "等待首帧确认的耗时必须从后续帧剩余总预算中扣除");
            var stop = raw.Budgets.Where(x => x.Id == 0x601).ToArray();
            if (controlledStop) { Assert.Single(stop); Assert.Equal(8000, stop[0].Budget.TimeoutMs); }
            else Assert.Empty(stop);
            owner.Write(new() { Id = 0x701, Data = [0] }); // AsyncLocal 所有者预算不能泄露到普通发送。
            Assert.Equal(3 + (controlledStop ? 1 : 0), raw.Budgets.Count);
        }
        finally { raw.Release.Set(); await owner.CloseAsync(8000, guard.Token); }
    }

    [Theory]
    [InlineData("NI")]
    [InlineData("ZLG")]
    public async Task A_short_configured_wait_keeps_the_submitted_ticket_until_its_late_echo(string vendor)
    {
        using var guard = new CancellationTokenSource(10000);
        var fixture = Create(vendor);
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => Task.Run(() => ((ICanBudgetedConfirmedTransmitter)fixture.Adapter)
                .WriteConfirmed(new() { Id = 0x201, Data = [1] }, CanConfirmationBudget.Start(100, default), default)).WaitAsync(guard.Token));
            Assert.True(fixture.Written.IsCompleted); Assert.False(((ICanTransmitQuiescence)fixture.Adapter).ConfirmTransmitQuiescent());
            Assert.Throws<InvalidOperationException>(() => fixture.Adapter.Write(new() { Id = 0x201, Data = [2] }));
            fixture.Emit();
            while (!((ICanTransmitQuiescence)fixture.Adapter).ConfirmTransmitQuiescent()) await Task.Delay(1, guard.Token);
        }
        finally { if (fixture.Written.IsCompleted) fixture.Emit(); fixture.Adapter.Close(); }
    }

    [Theory]
    [InlineData("NI")]
    [InlineData("ZLG")]
    public async Task Original_cancellation_is_checked_even_while_linked_callback_delivery_is_blocked(string vendor)
    {
        using var guard = new CancellationTokenSource(10000);
        using var original = new CancellationTokenSource();
        using var gateEntered = new ManualResetEventSlim();
        using var gateRelease = new ManualResetEventSlim();
        using var callbackRelease = new ManualResetEventSlim();
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = Create(vendor);
        var gate = fixture.Adapter.GetType().GetField("_transmitGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Adapter)!;
        var holding = Task.Factory.StartNew(() => { lock (gate) { gateEntered.Set(); gateRelease.Wait(); } },
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task callbacks = Task.CompletedTask;
        CancellationTokenRegistration registration = default;
        try
        {
            Assert.True(gateEntered.Wait(5000));
            registration = original.Token.Register(() => { callbackEntered.TrySetResult(); callbackRelease.Wait(); });
            var budget = CanConfirmationBudget.Start(3000, original.Token);
            var write = Task.Run(() =>
            {
                started.TrySetResult();
                ((ICanBudgetedConfirmedTransmitter)fixture.Adapter).WriteConfirmed(new() { Id = 0x201, Data = [1] }, budget, default);
            });
            await started.Task.WaitAsync(guard.Token);
            callbacks = original.CancelAsync(); await callbackEntered.Task.WaitAsync(guard.Token);
            Assert.True(original.IsCancellationRequested); Assert.False(callbacks.IsCompleted);
            gateRelease.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write.WaitAsync(guard.Token));
            Assert.False(fixture.Written.IsCompleted);
        }
        finally
        {
            gateRelease.Set(); callbackRelease.Set(); await holding; await callbacks; registration.Unregister();
            if (fixture.Written.IsCompleted) fixture.Emit(); fixture.Adapter.Close();
        }
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("deadline")]
    public async Task Ni_checks_the_original_budget_after_a_native_confirmation_query_returns(string transition)
    {
        using var guard = new CancellationTokenSource(15000);
        using var original = new CancellationTokenSource();
        CanConfirmationBudget budget = default;
        var driver = new FinalResultNiDriver(() =>
        {
            if (transition == "cancel") original.Cancel();
            else
                while (Stopwatch.GetElapsedTime(budget.Started).TotalMilliseconds <= budget.TimeoutMs + 10)
                    Thread.Sleep(1); // Cross the monotonic deadline inside the native query, not a timer race.
        });
        var adapter = new NiXnetAdapter(driver, 1, 2);
        budget = CanConfirmationBudget.Start(transition == "deadline" ? 1000 : 5000, original.Token);
        var write = Task.Run(() => ((ICanBudgetedConfirmedTransmitter)adapter)
            .WriteConfirmed(new() { Id = 0x201, Data = [1] }, budget, default));
        try
        {
            if (transition == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write.WaitAsync(guard.Token));
            else await Assert.ThrowsAsync<TimeoutException>(() => write.WaitAsync(guard.Token));
            Assert.True(driver.QueryReturned, "必须实际从已有匹配回显的 native 确认查询返回");
            Assert.True(adapter.ConfirmTransmitQuiescent()); // The ticket really was confirmed; do not undo that fact.
            Assert.Throws<InvalidOperationException>(() => adapter.Write(new() { Id = 0x201, Data = [2] }));
        }
        finally { try { await write; } catch { } adapter.Close(); }
    }

    [Fact]
    public async Task Zlg_checks_the_original_budget_after_a_registered_confirmation_wait_succeeds()
    {
        using var guard = new CancellationTokenSource(15000);
        using var original = new CancellationTokenSource();
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var driver = new CAN.ContractTests.ZlgRuntimeTests.Driver { AutoEcho = false };
        driver.OnClassicTransmit = _ => written.TrySetResult();
        var adapter = new ZlgAdapter(driver, 41, false);
        var budget = CanConfirmationBudget.Start(5000, original.Token);
        var write = Task.Run(() => ((ICanBudgetedConfirmedTransmitter)adapter)
            .WriteConfirmed(new() { Id = 0x201, Data = [1] }, budget, default));
        try
        {
            await written.Task.WaitAsync(guard.Token);
            Task? confirmation;
            var continuation = typeof(Task).GetField("m_continuationObject", BindingFlags.Instance | BindingFlags.NonPublic)!;
            do
            {
                confirmation = PendingConfirmation(adapter);
                if (confirmation == null || continuation.GetValue(confirmation) == null) await Task.Delay(1, guard.Token);
            } while (confirmation == null || continuation.GetValue(confirmation) == null);
            Assert.False(confirmation.IsCompleted);
            // The real WaitAsync continuation is registered: its pre-wait Remaining
            // check has passed. Its token intentionally differs from the original
            // budget token, modelling delayed linked-callback delivery.
            original.Cancel(); driver.EmitLast();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write.WaitAsync(guard.Token));
            Assert.True(adapter.ConfirmTransmitQuiescent());
            Assert.Throws<InvalidOperationException>(() => adapter.Write(new() { Id = 0x201, Data = [2] }));
        }
        finally
        {
            if (written.Task.IsCompleted) driver.EmitLast();
            try { await write; } catch { }
            adapter.Close();
        }
    }

    private static Task? PendingConfirmation(ZlgAdapter adapter)
    {
        var type = typeof(ZlgAdapter);
        var gate = type.GetField("_runtimeGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
        lock (gate)
        {
            var pending = (System.Collections.IEnumerable)type.GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
            var ticket = pending.Cast<object>().FirstOrDefault();
            return ticket == null ? null : ((TaskCompletionSource)ticket.GetType().GetField("Completion")!.GetValue(ticket)!).Task;
        }
    }

    private sealed class FinalResultNiDriver(Action beforeConfirmationReturn) : NiXnetIo
    {
        private byte[]? _echo;
        private bool _echoReturned;
        private readonly ManualResetEventSlim _echoConsumed = new();
        private int _written, _query;
        internal bool QueryReturned;
        private static ulong Now => 130000000000000000UL + (ulong)(Stopwatch.GetTimestamp() * (10_000_000m / Stopwatch.Frequency));
        public override int Read(uint session, byte[] buffer, out uint count)
        {
            var echo = Interlocked.Exchange(ref _echo, null);
            if (echo != null) { echo.CopyTo(buffer, 0); count = (uint)echo.Length; _echoReturned = true; }
            else { count = 0; if (_echoReturned) _echoConsumed.Set(); } // Next pump read proves Observe processed the prior echo.
            return 0;
        }
        public override int Write(uint session, byte[] buffer)
        {
            var echo = buffer.ToArray(); echo[13] = 0x80; BinaryPrimitives.WriteUInt64LittleEndian(echo, Now);
            Interlocked.Exchange(ref _echo, echo); Volatile.Write(ref _written, 1); return 0;
        }
        public override int WriteBatch(uint session, byte[] buffer, double timeoutSeconds)
        {
            var echo = buffer.ToArray(); int offset = 0;
            foreach (var frame in NiOrderedBatchTests.BatchDriver.Split(echo))
            {
                echo[offset + 13] = 0x80;
                BinaryPrimitives.WriteUInt64LittleEndian(echo.AsSpan(offset), Now);
                offset += frame.Length;
            }
            Interlocked.Exchange(ref _echo, echo); Volatile.Write(ref _written, 1); return 0;
        }
        public override int TransmitComplete(uint session)
        {
            if (Volatile.Read(ref _written) != 0 && Interlocked.CompareExchange(ref _query, 1, 0) == 0)
            {
                if (!_echoConsumed.Wait(5000)) throw new TimeoutException("test echo observation barrier");
                beforeConfirmationReturn(); QueryReturned = true;
            }
            return 0;
        }
        public override int ReadTime(uint session, out ulong time, out int fault) { time = Now; fault = 0; return 0; }
        public override int Stop(uint session) => 0;
        public override int State(uint session, out uint state, out int fault) { state = 0; fault = 0; return 0; }
        public override int Clear(uint session) => 0;
        public override void Check(int status) { if (status < 0) throw new InvalidOperationException($"fake NI {status}"); }
    }

    private static (ICanAdapter Adapter, Task Written, Action Emit) Create(string vendor)
    {
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (vendor == "NI")
        {
            var driver = new ConfirmationBudgetFollowupTests.BudgetNiDriver(0x201, written);
            return (new NiXnetAdapter(driver, 1, 2), written.Task, driver.EmitDelayed);
        }
        var zlg = new CAN.ContractTests.ZlgRuntimeTests.Driver { AutoEcho = false };
        zlg.OnClassicTransmit = _ => written.TrySetResult();
        return (new ZlgAdapter(zlg, 41, false), written.Task, zlg.EmitLast);
    }

    private sealed class RecordingAdapter : FakeAdapter, ICanConfirmedTransmitter, ICanBudgetedConfirmedTransmitter
    {
        internal readonly List<(uint Id, CanConfirmationBudget Budget, int Remaining)> Budgets = [];
        internal readonly TaskCompletionSource First = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim Release = new();
        internal int PublicCalls;
        public void WriteConfirmed(CanMessage message, CancellationToken ct)
        { Interlocked.Increment(ref PublicCalls); throw new InvalidOperationException("内部预算能力不得退化到公开默认期限"); }
        public void WriteConfirmed(CanMessage message, CanConfirmationBudget budget, CancellationToken ct)
        {
            Budgets.Add((message.Id, budget, budget.Remaining(ct)));
            if (Budgets.Count == 1) { First.TrySetResult(); Release.Wait(ct); }
            Write(message);
        }
    }
}
