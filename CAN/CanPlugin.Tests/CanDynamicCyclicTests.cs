using System.Reflection;
using CAN.Models;
using CAN.Runtime;
using CAN.Helpers;
using System.Runtime.CompilerServices;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanDynamicCyclicTests
{
    [Fact]
    public async Task Actual_executor_follows_variable_enable_and_period_without_recreating_task()
    {
        var context = DispatchProxy.Create<IExecutionContext, PayloadExpressionContext>(); var state = (PayloadExpressionContext)(object)context;
        var enabled = new StrongBox<bool>(false); var period = new StrongBox<int>(5);
        state.Variables["Locals.Enable"] = enabled; state.Variables["Locals.Period"] = period;
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); state.Adapter = owner;
        state.Plugin = new CanCyclicSendStartPlugin();
        state.Setting = new CanCyclicSendStartSetting { ControlRefreshMs = 5, Messages = [new()
        {
            Enabled = false, EnabledExpression = "Locals.Enable.Value", CycleTimeMsExpression = "Locals.Period.Value",
            Protection = new() { RcEnabled = true, RcInitialValue = 7 }
        }] };
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());
            await Task.Delay(25); Assert.Empty(raw.Sent);
            enabled.Value = true; await Until(() => raw.Sent.Count >= 3);
            enabled.Value = false; await Task.Delay(50); int paused = raw.Sent.Count;
            period.Value = 20; await Task.Delay(50); Assert.Equal(paused, raw.Sent.Count);
            enabled.Value = true; await Until(() => raw.Sent.Count >= paused + 2);
            await owner.StopJobAsync("CyclicTask1", 1000, default);
            Assert.Equal(Enumerable.Range(7, raw.Sent.Count).Select(value => (byte)value), raw.Sent.Select(frame => frame.Data[0]));
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Identity_evaluation_timeout_retains_preparation_until_real_exit_and_then_allows_rerun()
    {
        var context = DispatchProxy.Create<IExecutionContext, PayloadExpressionContext>(); var state = (PayloadExpressionContext)(object)context;
        state.Variables["Locals.DataIds"] = "CAN1";
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.BeforeRead = () => { entered.TrySetResult(); release.Wait(); };
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); state.Adapter = owner;
        var setting = new CanCyclicSendStartSetting { ConnectionName = "Locals.DataIds", ExpressionTimeoutMs = 300, Messages = [] };
        state.Plugin = new CanCyclicSendStartPlugin(); state.Setting = setting;
        try
        {
            var running = state.Plugin.CreateExecutor().ExecuteAsync(context);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(TestStatus.Error, (await running.WaitAsync(TimeSpan.FromSeconds(2))).StepResult.Status);
            var blocked = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, blocked.StepResult.Status); Assert.Contains("旧表达式", blocked.StepResult.Error!.Message);
            release.Set(); state.BeforeRead = null;
            await Task.Delay(50);
            setting.ConnectionName = "\"CAN1\"";
            var retried = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Passed, retried.StepResult.Status); Assert.Empty(raw.Sent);
        }
        finally { release.Set(); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task First_control_fault_is_visible_while_native_write_is_still_blocked()
    {
        using var release = new ManualResetEventSlim(); var raw = new FakeAdapter { WriteBlock = release };
        var owner = new CanConnectionResource(raw); var control = new ControlBox(true, 2);
        var loop = new CanDynamicCyclicLoop(control.Get());
        var job = owner.StartJob("dynamic", token => loop.RunAsync(2, _ => Task.FromResult(control.Get()), (ct, check) =>
        { owner.Write(new() { Id = 0x123, Data = [0] }, ct, check); return Task.CompletedTask; }, () => { }, token));
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            control.Set(true, 0); await Until(() => job.Error != null);
            Assert.Contains("大于 0", job.Error); Assert.False(job.Completion.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => owner.StartJob("dynamic", _ => Task.CompletedTask));
            release.Set(); await owner.StopJobAsync("dynamic", 1000, default);
            Assert.Contains("大于 0", job.Error);
        }
        finally { release.Set(); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task First_control_fault_freezes_other_rows_queued_behind_native_write()
    {
        using var release = new ManualResetEventSlim(); var raw = new FakeAdapter { WriteBlock = release };
        var owner = new CanConnectionResource(raw); var control = new ControlBox(true, 2);
        var queued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken jobToken = default;
        var job = owner.StartJob("two-rows", async token =>
        {
            jobToken = token;
            var first = new CanDynamicCyclicLoop(control.Get()).RunAsync(2, _ => Task.FromResult(control.Get()), (ct, check) =>
            { owner.Write(new() { Id = 0x123, Data = [0] }, ct, check); return Task.CompletedTask; }, () => { }, token);
            await raw.WriteEntered.Task;
            var second = Task.Run(() =>
            {
                queued.TrySetResult();
                owner.Write(new() { Id = 0x456, Data = [0] }, token, token.ThrowIfCancellationRequested,
                    new CanPayloadProcessor(new() { RcEnabled = true, RcInitialValue = 7 }));
            });
            await Task.WhenAll(first, second);
        });
        try
        {
            await queued.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var gate = (CanSubmissionGate)typeof(CanConnectionResource).GetField("_txGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
            await Until(() => gate.QueuedCount == 1);
            control.Set(true, 0); await Until(() => job.Error != null);
            Assert.True(jobToken.IsCancellationRequested); Assert.False(job.Completion.IsCompleted);
            release.Set(); await owner.StopJobAsync("two-rows", 1000, default);
            Assert.DoesNotContain(raw.Sent, frame => frame.Id == 0x456);
            Assert.Contains("大于 0", job.Error);
        }
        finally { release.Set(); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Unfinished_expression_keeps_job_ownership_until_drain_completes()
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        var actual = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tracking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = owner.StartJob("pending-expression", async token =>
        {
            var scope = new CanCyclicEvaluationScope(100);
            scope.Track(actual.Task); tracking.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { await scope.DrainAsync(); }
        });
        try
        {
            await tracking.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.StopJobAsync("pending-expression", 30, default));
            Assert.False(job.Completion.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => owner.StartJob("pending-expression", _ => Task.CompletedTask));
            actual.TrySetResult(); await owner.StopJobAsync("pending-expression", 1000, default);
            var fresh = owner.StartJob("pending-expression", _ => Task.CompletedTask);
            await fresh.Completion; await owner.StopJobAsync("pending-expression", 1000, default);
        }
        finally { actual.TrySetResult(); await owner.CloseAsync(); }
    }
    [Fact]
    public async Task Pause_resume_and_period_change_keep_counter_but_restart_uses_initial()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            for (int restart = 0; restart < 2; restart++)
            {
                int offset = raw.Sent.Count;
                var control = new ControlBox(false, 5);
                var loop = new CanDynamicCyclicLoop(control.Get());
                var processor = new CanPayloadProcessor(new() { RcEnabled = true, RcInitialValue = 7 });
                using var stop = new CancellationTokenSource();
                var running = loop.RunAsync(2, _ => Task.FromResult(control.Get()), (ct, check) =>
                { owner.Write(new() { Id = 0x123, Data = [0] }, ct, check, processor); return Task.CompletedTask; }, () => { }, stop.Token);
                control.Set(true, 5); await Until(() => raw.Sent.Count >= offset + 3);
                control.Set(false, 5); await Until(() => !loop.CurrentControl.Enabled);
                await Task.Delay(20); int paused = raw.Sent.Count; await Task.Delay(30); Assert.Equal(paused, raw.Sent.Count);
                control.Set(false, 20); await Until(() => loop.CurrentControl.PeriodMs == 20);
                Assert.Equal(paused, raw.Sent.Count);
                control.Set(true, 20); await Until(() => raw.Sent.Count >= paused + 2);
                stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
                var sent = raw.Sent.Skip(offset).Select(frame => frame.Data[0]).ToArray();
                Assert.Equal(Enumerable.Range(7, sent.Length).Select(n => (byte)n), sent);
            }
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pausing_during_payload_or_transmit_queue_discards_old_frame_without_counter_consumption(bool blockQueue)
    {
        using var release = new ManualResetEventSlim();
        var raw = new FakeAdapter { WriteBlock = blockQueue ? release : null }; var owner = new CanConnectionResource(raw);
        Task blocker = blockQueue ? Task.Run(() => owner.Write(new() { Id = 0x777, Data = [0] })) : Task.CompletedTask;
        if (blockQueue) await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preparation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var control = new ControlBox(true, 5); var loop = new CanDynamicCyclicLoop(control.Get());
        var processor = new CanPayloadProcessor(new() { RcEnabled = true, RcInitialValue = 7 });
        using var stop = new CancellationTokenSource();
        int calls = 0;
        var running = loop.RunAsync(2, _ => Task.FromResult(control.Get()), async (ct, check) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                entered.TrySetResult();
                if (!blockQueue) await preparation.Task.WaitAsync(ct);
            }
            check(); owner.Write(new() { Id = 0x123, Data = [0] }, ct, check, processor);
        }, () => { }, stop.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            control.Set(false, 5); await Until(() => !loop.CurrentControl.Enabled);
            release.Set(); preparation.TrySetResult(); await blocker; await Task.Delay(25);
            Assert.DoesNotContain(raw.Sent, frame => frame.Id == 0x123);
            control.Set(true, 5); await Until(() => raw.Sent.Any(frame => frame.Id == 0x123));
            Assert.Equal(7, raw.Sent.First(frame => frame.Id == 0x123).Data[0]);
        }
        finally
        {
            release.Set(); preparation.TrySetResult(); stop.Cancel();
            await Record.ExceptionAsync(() => running); await blocker; await owner.CloseAsync();
        }
    }

    [Fact]
    public async Task Invalid_dynamic_period_stops_worker_instead_of_using_old_value()
    {
        var loop = new CanDynamicCyclicLoop(new(false, 10));
        await Assert.ThrowsAsync<ArgumentException>(() => loop.RunAsync(1,
            _ => Task.FromResult(new CanCyclicControl(true, 0)), (_, _) => throw new Exception("must not submit"), () => { }, default));
    }

    private sealed class ControlBox(bool enabled, int period)
    {
        private readonly object _gate = new();
        private CanCyclicControl _value = new(enabled, period);
        internal CanCyclicControl Get() { lock (_gate) return _value; }
        internal void Set(bool enabled, int period) { lock (_gate) _value = new(enabled, period); }
    }

    private static async Task Until(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(5000);
        while (!condition()) await Task.Delay(2, deadline.Token);
    }
    [Fact]
    public async Task False_expression_creates_paused_task_and_never_sends_or_advances_counter()
    {
        var (owner, raw, context) = Setup(new() { EnabledExpression = "false", CycleTimeMs = 5, Protection = new() { RcEnabled = true, RcInitialValue = 7 } });
        try
        {
            var result = await new CanCyclicSendStartPlugin().CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Passed, result.StepResult.Status);
            await Task.Delay(120);
            Assert.Empty(raw.Sent);
            await owner.StopJobAsync("CyclicTask1", 1000, default);
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("\"10\"")]
    public async Task Invalid_period_expression_is_rejected_before_job_creation(string expression)
    {
        var (owner, raw, context) = Setup(new() { CycleTimeMsExpression = expression });
        try
        {
            var result = await new CanCyclicSendStartPlugin().CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Empty(raw.Sent);
        }
        finally { await owner.CloseAsync(); }
    }

    private static (CanConnectionResource Owner, FakeAdapter Raw, IExecutionContext Context) Setup(CyclicMessageItem row)
    {
        var context = DispatchProxy.Create<IExecutionContext, DbcTestContext>(); var state = (DbcTestContext)(object)context;
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); state.Adapter = owner;
        state.Plugin = new CanCyclicSendStartPlugin();
        state.Setting = new CanCyclicSendStartSetting { ControlRefreshMs = 5, Messages = [row] };
        return (owner, raw, context);
    }
}
