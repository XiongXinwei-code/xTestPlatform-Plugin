using CAN.Adapters;
using CAN.CANopen;
using CAN.Models;
using CAN.Runtime;
using Xunit;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class OwnerRetirementMechanismTests
{
    [Fact]
    public async Task Startup_cancel_before_success_keeps_reviewed_owner_stop_command_available()
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var context = System.Reflection.DispatchProxy.Create<IExecutionContext, SdkCanContext>();
        ((SdkCanContext)(object)context).Registry = registry;
        var raw = new StopDriver(); var owner = new CanConnectionResource(raw);
        var service = owner.StartService("startup", [new("feedback", 0x181, 8, 100)]);
        var published = Signal(); var releasePublisher = Signal();
        var wrapper = PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CallbackExecutor(async _ =>
        {
            service.StartConfiguredTargets([1], [new(0x201, [1])], new(0x80, [0]),
                new(200, 1000, 2000, true, 60000), new([new(1, 0x2000, 0, [0])], 1000));
            published.TrySetResult();
            await releasePublisher.Task; // Start is staged, not yet returned successfully.
            return new() { StepResult = new() { Status = TestStatus.Completed } };
        }));
        using var caller = new CancellationTokenSource();
        var execution = wrapper.ExecuteAsync(context, caller.Token);
        try
        {
            await published.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await raw.CycleSent.Task.WaitAsync(TimeSpan.FromSeconds(3));
            using var completeCycle = new CancellationTokenSource(2000);
            while (service.Snapshot().Cycle.CompletedCycles == 0) await Task.Delay(1, completeCycle.Token);
            caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await execution.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            await raw.StopSent.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await service.StopTargetsAsync(2000);
            Assert.True(service.Snapshot().Cycle.StopCommandsAcknowledged);
            Assert.Null(owner.IsolationReason);
        }
        finally
        {
            releasePublisher.TrySetResult(); await execution;
            await owner.CloseAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scoped_abort_freezes_only_an_admitted_raw_write_before_returning(bool queued)
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var context = System.Reflection.DispatchProxy.Create<IExecutionContext, SdkCanContext>();
        ((SdkCanContext)(object)context).Registry = registry;
        var raw = new BlockingDriver("Write"); var owner = new CanConnectionResource(raw);
        Task? first = null;
        if (queued) { first = owner.WriteAsync(new() { Id = 1 }, 60000); await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
        // The executor deliberately does not forward the step token; the scope must
        // still freeze its admitted transaction before publishing Aborted.
        var wrapper = PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CallbackExecutor(async _ =>
        {
            await owner.WriteAsync(new() { Id = 2 }, 60000, default);
            return new() { StepResult = new() { Status = TestStatus.Completed } };
        }));
        using var caller = new CancellationTokenSource();
        var execution = wrapper.ExecuteAsync(context, caller.Token);
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await execution.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            if (queued) Assert.Null(owner.IsolationReason);
            else Assert.NotNull(owner.IsolationReason);
            Assert.Equal(0, raw.Closes);
        }
        finally
        {
            raw.Release.TrySetResult();
            if (first != null) await first;
            owner.Dispose(); await owner.CloseAsync();
            await execution;
        }
        Assert.Equal(1, raw.Writes);
    }

    [Theory]
    [InlineData("Open")]
    [InlineData("Read")]
    [InlineData("Write")]
    [InlineData("Confirmation")]
    public async Task Close_retires_front_wait_without_releasing_real_native_call(string operation)
    {
        var raw = new BlockingDriver(operation);
        string physical = Guid.NewGuid().ToString();
        var owner = new CanConnectionResource(raw, physical);
        Task waiting = operation switch
        {
            "Open" => owner.OpenAsync(new(), 60000),
            "Read" => owner.ReadAsync(null, Timeout.Infinite),
            "Write" => owner.WriteAsync(new() { Id = 0x123, Data = [1] }, 60000),
            _ => Task.Run(() => owner.VerifyConfirmationAvailable(60000))
        };
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            owner.Dispose();
            await AssertRetired(waiting);
            Assert.Equal(0, raw.Closes);
            Assert.False(owner.IsClosed);
            Assert.Throws<InvalidOperationException>(() => new CanConnectionResource(new BlockingDriver("None"), physical));
        }
        finally
        {
            raw.Release.TrySetResult();
            try { await waiting; } catch (Exception) { }
            await owner.CloseAsync();
        }
        Assert.True(owner.IsClosed);
        var next = new CanConnectionResource(new BlockingDriver("None"), physical);
        await next.CloseAsync();
    }

    [Fact]
    public async Task Closing_controlled_receive_retires_routed_waiter_before_blocked_pump_exits()
    {
        var raw = new BlockingDriver("Read");
        var owner = new CanConnectionResource(raw);
        owner.StartService("monitor", [new("feedback", 0x181, 8, 100)]);
        await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var waiting = owner.ReadCanopenAsync(0x181, 60000, default);
        try
        {
            owner.Dispose();
            await AssertRetired(waiting);
            Assert.Equal(0, raw.Closes);
        }
        finally
        {
            raw.Release.TrySetResult();
            try { await waiting; } catch (Exception) { }
            await owner.CloseAsync();
        }
    }

    [Fact]
    public async Task Closing_retires_active_and_queued_protocol_waits_without_admitting_next_transaction()
    {
        var raw = new BlockingDriver("Read");
        var owner = new CanConnectionResource(raw);
        var entered = Signal();
        var releaseTransaction = Signal();
        int queuedSubmissions = 0;
        // Keep the first protocol operation alive independently of its raw observer.
        var first = CanProtocolTransaction.RunAsync(owner, 60000, async _ =>
        {
            entered.TrySetResult();
            var native = owner.ReadAsync(null, 60000);
            try { await native; }
            finally { await releaseTransaction.Task; }
            return 1;
        }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var queued = CanProtocolTransaction.RunAsync(owner, 60000, _ =>
        {
            Interlocked.Increment(ref queuedSubmissions);
            return Task.FromResult(2);
        }, default);
        try
        {
            owner.Dispose();
            await AssertRetired(first);
            await AssertRetired(queued);
            Assert.Equal(0, queuedSubmissions);
            Assert.Equal(0, raw.Closes);
        }
        finally
        {
            releaseTransaction.TrySetResult(); raw.Release.TrySetResult();
            try { await first; } catch (Exception) { }
            try { await queued; } catch (Exception) { }
            await owner.CloseAsync();
        }
    }

    [Fact]
    public async Task Closing_retires_shared_CANopen_raw_receive_before_native_call_exits()
    {
        var raw = new BlockingDriver("Read"); var owner = new CanConnectionResource(raw);
        var waiting = CanopenConnectionAccess.For(owner).ReceiveAsync(0x581, 60000, default);
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            owner.Dispose();
            await AssertRetired(waiting);
            Assert.Equal(0, raw.Closes);
        }
        finally
        {
            raw.Release.TrySetResult();
            try { await waiting; } catch (Exception) { }
            await owner.CloseAsync();
        }
    }

    [Fact]
    public async Task Closing_retires_SDO_queue_without_releasing_another_transactions_gate()
    {
        var owner = new CanConnectionResource(new BlockingDriver("None"));
        var access = CanopenConnectionAccess.For(owner);
        using var active = await access.EnterSdoAsync(1, null, 60000, default);
        var waiting = access.EnterSdoAsync(2, null, 60000, default);
        try { owner.Dispose(); await AssertRetired(waiting); }
        finally { active.Dispose(); await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Caller_cancel_is_fast_and_real_receive_owns_cancel_callbacks_until_they_exit(bool failBoth)
    {
        var raw = new CallbackDriver { FailBoth = failBoth }; var owner = new CanConnectionResource(raw);
        using var caller = new CancellationTokenSource();
        var reading = owner.ReadAsync(null, 60000, caller.Token);
        Task? cancel = null;
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            cancel = Task.Run(caller.Cancel);
            await raw.CallbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Same(cancel, await Task.WhenAny(cancel, Task.Delay(400)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(1)));
            owner.Dispose(); raw.ReturnRead.TrySetResult();
            await Task.Delay(50);
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.Closes);
        }
        finally
        {
            raw.ReturnRead.TrySetResult(); raw.ExitCallback.TrySetResult();
            if (cancel != null) await cancel;
            try { await reading; } catch (Exception) { }
            if (!failBoth) await owner.CloseAsync();
            else
            {
                var error = await Assert.ThrowsAsync<AggregateException>(() => owner.CloseAsync());
                Assert.Contains("native failure", error.ToString()); Assert.Contains("callback failure", error.ToString());
                Assert.True(owner.IsClosed);
            }
        }
    }

    [Fact]
    public async Task Repeated_dispose_of_completed_failed_close_never_runs_user_log_inline()
    {
        var raw = new BlockingDriver("None");
        var enteredLog = Signal(); var exitLog = Signal();
        var owner = new CanConnectionResource(raw, reportCloseError: _ =>
        {
            enteredLog.TrySetResult(); exitLog.Task.GetAwaiter().GetResult();
        });
        owner.StartJob("failed", _ => Task.FromException(new InvalidOperationException("historical worker failure")));
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CloseAsync());
            Assert.True(owner.IsClosed);
            var disposing = Task.Run(owner.Dispose);
            await enteredLog.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Same(disposing, await Task.WhenAny(disposing, Task.Delay(400)));
        }
        finally { exitLog.TrySetResult(); owner.Dispose(); }
    }

    [Fact]
    public async Task Original_caller_cancel_remains_cancellation_when_owner_also_closes()
    {
        var raw = new BlockingDriver("Read");
        var owner = new CanConnectionResource(raw);
        using var caller = new CancellationTokenSource();
        var waiting = owner.ReadAsync(null, 60000, caller.Token);
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            caller.Cancel(); owner.Dispose();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.Equal(0, raw.Closes);
        }
        finally
        {
            raw.Release.TrySetResult();
            try { await waiting; } catch (Exception) { }
            await owner.CloseAsync();
        }
    }

    private static async Task AssertRetired(Task waiting)
    {
        Assert.Same(waiting, await Task.WhenAny(waiting, Task.Delay(400)));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => waiting);
        Assert.Contains("退出", error.Message);
    }
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class CallbackExecutor(Func<CancellationToken, Task<ExecutionResult>> run) : IStepExecutor
    {
        public Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default) => run(cancellationToken);
    }
    private sealed class StopDriver : ICanAdapter, ICanConfirmedTransmitter
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<CanMessage> _incoming = new();
        internal readonly TaskCompletionSource CycleSent = Signal(), StopSent = Signal();
        public bool IsConnected { get; private set; } = true;
        public void Open(CanAdapterConfig config) { }
        public void Close() => IsConnected = false;
        public void Dispose() => IsConnected = false;
        public void WriteConfirmed(CanMessage frame, CancellationToken token) { token.ThrowIfCancellationRequested(); Write(frame); }
        public void Write(CanMessage frame)
        {
            if (frame.Id == 0x80) CycleSent.TrySetResult();
            if (frame.Id != 0x601) return;
            _incoming.Enqueue(new() { Id = 0x581, Data = [0x60, frame.Data[1], frame.Data[2], frame.Data[3], 0, 0, 0, 0] });
            StopSent.TrySetResult();
        }
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < timeoutMs)
            {
                ct.ThrowIfCancellationRequested();
                if (_incoming.TryDequeue(out var frame)) return frame;
                Thread.Sleep(1);
            }
            return null;
        }
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => Read(timeoutMs, ct);
    }

    private sealed class CallbackDriver : ICanAdapter
    {
        internal readonly TaskCompletionSource Entered = Signal(), CallbackEntered = Signal(), ReturnRead = Signal(), ExitCallback = Signal();
        internal int Closes;
        internal bool FailBoth;
        public bool IsConnected { get; private set; } = true;
        public void Open(CanAdapterConfig config) { }
        public void Close() { Closes++; IsConnected = false; }
        public void Dispose() => IsConnected = false;
        public void Write(CanMessage message) { }
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default)
        {
            _ = ct.Register(() =>
            {
                CallbackEntered.TrySetResult(); ExitCallback.Task.GetAwaiter().GetResult();
                if (FailBoth) throw new InvalidOperationException("CAN callback failure");
            });
            Entered.TrySetResult(); ReturnRead.Task.GetAwaiter().GetResult();
            if (FailBoth) throw new InvalidOperationException("CAN native failure");
            return null;
        }
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => Read(timeoutMs, ct);
    }

    private sealed class BlockingDriver(string blocked) : ICanAdapter, ICanConfirmedTransmitter
    {
        internal readonly TaskCompletionSource Entered = Signal(), Release = Signal();
        internal int Closes, Writes;
        public bool IsConnected { get; private set; } = true;
        public bool ConfirmationAvailable => true;
        public void VerifyConfirmationAvailable() => Block("Confirmation");
        public void Open(CanAdapterConfig config) => Block("Open");
        public void Close() { Interlocked.Increment(ref Closes); IsConnected = false; }
        public void Dispose() => IsConnected = false;
        public void Write(CanMessage message) { Interlocked.Increment(ref Writes); Block("Write"); }
        public void WriteConfirmed(CanMessage message, CancellationToken ct) => Block("Write");
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) { Block("Read"); return null; }
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => Read(timeoutMs, ct);
        private void Block(string operation)
        {
            if (blocked != operation) return;
            Entered.TrySetResult(); Release.Task.GetAwaiter().GetResult();
        }
    }
}
