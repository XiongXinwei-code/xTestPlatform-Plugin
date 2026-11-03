using System.Reflection;
using CAN.Dbc;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcBackgroundLifecycleTests
{
    private static (IExecutionContext Context, DbcOutputTestContext State) Context(CanConnectionResource owner, string task = "DbcRx1")
    {
        var context = DispatchProxy.Create<IExecutionContext, DbcOutputTestContext>();
        var state = (DbcOutputTestContext)(object)context;
        state.Plugin = new DbcReadBackgroundStartPlugin(); state.Adapter = owner;
        state.Setting = new DbcReadBackgroundSetting { Document = new() { Text = DbcCycleTimingTests.Text },
            TaskName = $"\"{task}\"", PublishIntervalMs = 5, MaxAgeMs = 1000, UseMessageList = true,
            Messages = [new() { MessageId = 291, SignalOutputs = [new() { Name = "Value", TargetVariable = "Locals.First" }] },
                new() { MessageId = 292, SignalOutputs = [new() { Name = "Value", TargetVariable = "Locals.Second" }] }] };
        state.Variables["Locals.First"] = -1; state.Variables["Locals.Second"] = -1;
        return (context, state);
    }
    private static void Passed(ExecutionResult result) =>
        Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());

    [Fact]
    public async Task Successful_start_detaches_step_token_matches_multiple_IDs_and_stop_joins_publications()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var (context, state) = Context(owner); using var caller = new CancellationTokenSource();
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.AfterWrite = path => { if (path == "Locals.First") first.TrySetResult(); if (path == "Locals.Second") second.TrySetResult(); };
        try
        {
            Passed(await state.Plugin.CreateExecutor().ExecuteAsync(context, caller.Token));
            caller.Cancel(); // A completed start step's token is not the listener's lifetime.
            raw.Incoming.Add(new() { Id = 999, Data = [99, 99] });
            raw.Incoming.Add(new() { Id = 291, FrameType = CanFrameType.Extended, Data = [88, 88] });
            raw.Incoming.Add(new() { Id = 291, IsFd = true, Data = [77, 77] });
            raw.Incoming.Add(new() { Id = 291, Data = [12, 0] });
            raw.Incoming.Add(new() { Id = 292, Data = [34, 0] });
            await Task.WhenAll(first.Task, second.Task).WaitAsync(TimeSpan.FromSeconds(5));
            await owner.StopBackgroundReadAsync("DbcRx1", 2000, default);
            Assert.Equal(12, state.Variables["Locals.First"]); Assert.Equal(34, state.Variables["Locals.Second"]);
            int writes = state.Writes.Count;
            raw.Incoming.Add(new() { Id = 291, Data = [55, 0] }); await Task.Delay(40);
            Assert.Equal(writes, state.Writes.Count); Assert.Equal(12, state.Variables["Locals.First"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Stop_during_setter_preserves_output_ownership_until_actual_setter_returns()
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        var (context, state) = Context(owner);
        using var setterEntered = new ManualResetEventSlim(); using var releaseSetter = new ManualResetEventSlim();
        state.AfterWrite = path => { if (path == "Locals.First") { setterEntered.Set(); releaseSetter.Wait(); } };
        var router = (CanReceiveRouter)typeof(CanConnectionResource).GetField("_router", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
        try
        {
            Passed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            router.Publish(new() { Id = 291, Data = [12, 0] });
            Assert.True(await Task.Run(() => setterEntered.Wait(5000)));
            await Assert.ThrowsAnyAsync<Exception>(() => owner.StopBackgroundReadAsync("DbcRx1", 20, default));
            var guard = new CanExecutionGuard(context, default);
            Assert.Throws<InvalidOperationException>(() => CanBackgroundOutputLease.Acquire(context, guard, ["Locals.First"]));
            releaseSetter.Set(); await owner.StopBackgroundReadAsync("DbcRx1", 2000, default);
            using var restored = CanBackgroundOutputLease.Acquire(context, guard, ["Locals.First"]);
            Assert.Equal(-1, state.Variables["Locals.Second"]);
        }
        finally { releaseSetter.Set(); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Restart_gets_new_subscription_and_does_not_republish_old_frames()
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        var (context, state) = Context(owner);
        var router = (CanReceiveRouter)typeof(CanConnectionResource).GetField("_router", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.AfterWrite = path => { if (path == "Locals.First") observed.TrySetResult(); };
        try
        {
            Passed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            router.Publish(new() { Id = 291, Data = [12, 0] }); await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.StopBackgroundReadAsync("DbcRx1", 2000, default);
            state.Variables["Locals.First"] = -1;
            int oldWrites = state.Writes.Count;
            Passed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            await Task.Delay(35); Assert.Equal(-1, state.Variables["Locals.First"]); Assert.Equal(oldWrites, state.Writes.Count);
            await owner.StopBackgroundReadAsync("DbcRx1", 2000, default);
        }
        finally { await owner.CloseAsync(); }
    }
}
