using CiA402.Transport;
using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Engine;
using CiA402.Executors;
using Xunit;

namespace CiA402.Tests;

public sealed class Round2MonotonicBudgetTests
{
    [Fact]
    public async Task ExpiredControlScopeDoesNotBlockIndependentSessionCleanup()
    {
        var plugin = new ReadStatePlugin(); var (context, state) = TestContext.Create(plugin, new ReadStateSetting { TimeoutMs = 1000 });
        await SettingValidation.EvaluateAsync(new ReadStateSetting(), context, default);
        var inner = new ProbeAxisTransport();
        int reads = 0;
        inner.ReadHandler = async (index, ct) =>
        {
            if (Interlocked.Increment(ref reads) == 1) await Task.Delay(1100);
            return await inner.Drive.ReadObjectAsync(index, 0, ct);
        };
        var config = new BindAxisSetting { CommunicationTimeoutMs = 5000 };
        using var session = new AxisSession(new AxisController(inner, config), config, default, (_, _) => { });
        state.Resources[AxisSession.Key("Axis1")] = session;
        var result = await new TwoIoExecutor(true).ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        await session.CloseAsync();
        Assert.Equal(1, reads);
        Assert.Equal(2, inner.Drive.Writes.Count); // Stop and disable cleanup reached the drive.
        Assert.All(inner.Drive.Writes, write => Assert.Equal((ushort)0x6040, write.Index));
        Assert.Equal(1, inner.DisposeCount);
    }

    [Fact]
    public async Task ExpiredStepCannotSubmitSecondIoWhileTimerNotificationIsDelayed()
    {
        var plugin = new ReadStatePlugin(); var (context, state) = TestContext.Create(plugin, new ReadStateSetting { TimeoutMs = 1000 });
        await SettingValidation.EvaluateAsync(new ReadStateSetting(), context, default);
        int writes = 0;
        var inner = new ProbeAxisTransport
        {
            ReadHandler = async (_, _) => { await Task.Delay(1100); return new byte[] { 0x40, 0 }; },
            WriteHandler = (_, _, _) => { Interlocked.Increment(ref writes); return Task.CompletedTask; }
        };
        var config = new BindAxisSetting { CommunicationTimeoutMs = 5000 };
        using var session = new AxisSession(new AxisController(inner, config), config, default, (_, _) => { });
        var transport = session.Controller.Transport;
        state.Resources[AxisSession.Key("Axis1")] = session;
        try
        {
            var result = await new TwoIoExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(0, writes);
            // The expired ordinary action's guard must not become a permanent session token.
            await transport.WriteObjectAsync(0x2000, 0, [1], default); Assert.Equal(1, writes);
        }
        finally { await session.CloseAsync(); }
        Assert.Equal(1, inner.DisposeCount);
    }

    private sealed class TwoIoExecutor(bool control = false) : AxisActionExecutor<ReadStateSetting>
    {
        protected override bool IsControl => control;
        protected override Task<Action?> RunAsync(ReadStateSetting setting, EvaluatedSettings values, IExecutionContext context, CancellationToken ct, CancellationToken caller, Action check)
        {
            // Deterministically delay only the soft timer notification. The configured
            // Stopwatch deadline still elapses; production must not rely on callbacks.
            var source = (CancellationTokenSource)typeof(CancellationToken).GetField("_source", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(ct)!;
            source.CancelAfter(Timeout.Infinite);
            return base.RunAsync(setting, values, context, ct, caller, check);
        }
        protected override async Task OperateAsync(ReadStateSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
        {
            await session.Controller.Transport.ReadObjectAsync(0x6041, 0, ct);
            await session.Controller.Transport.WriteObjectAsync(0x2000, 0, [1], ct);
        }
    }

    [Fact]
    public async Task SuccessfulOperationCannotOutliveConfiguredStepBudgetDuringFinalLog()
    {
        var plugin = new ReadStatePlugin(); var (context, state) = TestContext.Create(plugin, new ReadStateSetting { TimeoutMs = 1000 });
        var config = new BindAxisSetting(); var probe = new ProbeAxisTransport();
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        state.Resources[AxisSession.Key("Axis1")] = session;
        await SettingValidation.EvaluateAsync(new ReadStateSetting(), context, default);
        bool logged = false;
        state.OnLog = args => { if (args.Any(a => Equals(a, "CiA402 步骤执行完成"))) { logged = true; Thread.Sleep(1100); } };
        try { var result = await plugin.CreateExecutor().ExecuteAsync(context); Assert.True(logged); Assert.Equal(TestStatus.Error, result.StepResult.Status); }
        finally { await session.CloseAsync(); }
    }

    [Fact]
    public async Task CompletedTransportCannotPublishAfterItsMonotonicDeadline()
    {
        using var resume = new ManualResetEventSlim(); using var returnNative = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int held = 0; bool completed = false;
        var flow = new AsyncLocal<bool>(change =>
        {
            if (change.ThreadContextChanged && change.CurrentValue && Volatile.Read(ref completed) && Interlocked.CompareExchange(ref held, 1, 0) == 0)
            { paused.TrySetResult(); resume.Wait(5000); }
        });
        var inner = new ReturnedTransport(() =>
        {
            entered.TrySetResult(); if (!returnNative.Wait(5000)) throw new TimeoutException("test native barrier");
            Volatile.Write(ref completed, true); flow.Value = false; return Task.FromResult(new byte[] { 1 });
        });
        await using var transport = new CoordinatedAxisTransport(inner, 1000);
        Task<byte[]> pending;
        flow.Value = true;
        try { pending = transport.ReadObjectAsync(0x6041, 0, default); }
        finally { flow.Value = false; }
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); returnNative.Set();
            await paused.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Delay(1100); resume.Set();
            await Assert.ThrowsAsync<TimeoutException>(() => pending);
        }
        finally { Interlocked.Exchange(ref held, 1); returnNative.Set(); resume.Set(); try { await pending; } catch { } }
    }

    private sealed class ReturnedTransport(Func<Task<byte[]>> read) : ICia402AxisTransport
    {
        public Task<byte[]> ReadObjectAsync(ushort index, byte subIndex, CancellationToken ct) => read();
        public Task WriteObjectAsync(ushort index, byte subIndex, byte[] value, CancellationToken ct) => Task.CompletedTask;
        public Task WriteTargetAsync(OperationMode mode, long target, ushort controlWord, CancellationToken ct) => Task.CompletedTask;
        public Task<AxisFeedback> ReadFeedbackAsync(bool position, bool velocity, bool torque, CancellationToken ct) => Task.FromResult(new AxisFeedback(0, null, null, null));
        public void RequireCyclicMode(OperationMode mode) { }
        public void Dispose() { }
    }
}
