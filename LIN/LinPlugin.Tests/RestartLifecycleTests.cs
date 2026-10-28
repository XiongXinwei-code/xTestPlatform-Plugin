using LIN.Adapters;
using LIN.Models;
using LIN.Helpers;
using LIN.Runtime;
using PluginCommon;
using Xunit;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Tests;

public sealed class RestartLifecycleTests
{
    [Fact]
    public async Task OpenReplacesAnAlreadyClosedOwnerInTheSameRegistry()
    {
        var context = System.Reflection.DispatchProxy.Create<IExecutionContext, LinContext>();
        var state = (LinContext)(object)context;
        var old = new LinConnectionResource(new Driver()); old.Open(new() { Channel = Channel() }); await old.CloseAsync();
        state.Items[LinHelper.GetAdapterKey("LIN1")] = old;
        state.Plugin = new LinOpenPlugin(); state.Setting = new LinOpenSetting { ConnectionName = "\"LIN1\"", Channel = $"\"{Channel()}\"" };
        try
        {
            var result = await new LIN.Executors.LinOpenExecutor(_ => new Driver()).ExecuteAsync(context);
            Assert.Equal(xTestPlatform.Core.Models.TestStatus.Completed, result.StepResult.Status);
            Assert.NotSame(old, state.Items[LinHelper.GetAdapterKey("LIN1")]);
        }
        finally { foreach (var owner in state.Items.Values.OfType<LinConnectionResource>()) await owner.CloseAsync(); }
    }

    [Fact]
    public async Task CancelledOpenReturnsAbortedWithoutWaitingForUncooperativeDriverCleanup()
    {
        var context = System.Reflection.DispatchProxy.Create<IExecutionContext, LinContext>();
        var state = (LinContext)(object)context;
        string channel = Channel();
        state.Plugin = new LinOpenPlugin();
        state.Setting = new LinOpenSetting { Channel = $"\"{channel}\"", ConnectionName = "\"opening\"", TimeoutMs = 500 };
        await WarmExpressions(context, $"\"{channel}\"", "\"opening\"");
        var raw = new Driver { BlockOpen = true };
        using var stop = new CancellationTokenSource();
        var opening = new LIN.Executors.LinOpenExecutor(_ => raw).ExecuteAsync(context, stop.Token);
        try
        {
            await raw.OpenEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            stop.Cancel();
            var result = await opening.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(xTestPlatform.Core.Models.TestStatus.Aborted, result.StepResult.Status);
            var retained = Assert.Single(state.Items.Values.OfType<LinConnectionResource>());
            Assert.False(retained.IsClosed); Assert.Equal(0, raw.CloseCount);
        }
        finally
        {
            raw.Release.Set(); await opening;
            foreach (var retained in state.Items.Values.OfType<LinConnectionResource>()) await retained.CloseAsync();
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StopWaitDistinguishesCallerCancellationFromDeadlineAndRetainsNativeOwner(bool cyclic, bool callerCancels)
    {
        var context = System.Reflection.DispatchProxy.Create<IExecutionContext, LinContext>();
        var state = (LinContext)(object)context;
        int timeout = callerCancels ? 3000 : 40;
        state.Plugin = cyclic ? new LinCyclicSendStopPlugin() : new LinClosePlugin();
        state.Setting = cyclic
            ? new LinCyclicSendStopSetting { TaskName = "\"cyclic\"", TimeoutMs = timeout }
            : new LinCloseSetting { ConnectionName = "\"LIN1\"", TimeoutMs = timeout };
        await WarmExpressions(context, "\"cyclic\"", "\"LIN1\"");
        var raw = new Driver(); var owner = new LinConnectionResource(raw); owner.Open(new() { Channel = Channel() });
        var job = owner.StartJob([ct => Task.Run(() => owner.Write(new() { Data = [1] }, ct))]);
        state.Items[LinHelper.GetAdapterKey("LIN1")] = owner;
        state.Items[LIN.Executors.LinCyclicSendStartExecutor.GetTaskKey("cyclic")] = job;
        using var stop = new CancellationTokenSource();
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            using var registry = await LinRegistryGuard.Enter(context.Resources, default);
            var waiting = state.Plugin.CreateExecutor().ExecuteAsync(context, stop.Token);
            if (callerCancels) { await Task.Delay(20); stop.Cancel(); }
            var result = await waiting.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(callerCancels ? xTestPlatform.Core.Models.TestStatus.Aborted : xTestPlatform.Core.Models.TestStatus.Error, result.StepResult.Status);
            Assert.False(job.Completion.IsCompleted); Assert.False(owner.IsClosed); Assert.Equal(0, raw.CloseCount);
            Assert.Contains(owner, state.Items.Values); Assert.Contains(job, state.Items.Values);
        }
        finally { raw.Release.Set(); await owner.CloseAsync(); }
    }

    private static string Channel() => "LIN_TEST_" + Guid.NewGuid().ToString("N");
    private static string Key(string channel) => "LIN:NI:" + channel.Trim().ToUpperInvariant();

    [Fact]
    public async Task OpenStepWaitsForPreviousPhysicalOwnerBeforeNewDriverOpen()
    {
        string channel = Channel(); var oldRaw = new Driver(); var old = new LinConnectionResource(oldRaw);
        var context = System.Reflection.DispatchProxy.Create<xTestPlatform.Core.Engine.IExecutionContext, LinContext>();
        var state = (LinContext)(object)context; var next = new Driver();
        state.Plugin = new LinOpenPlugin(); state.Setting = new LinOpenSetting { Channel = $"\"{channel.ToLowerInvariant()}\"", ConnectionName = "\"new-run\"", TimeoutMs = 2000 };
        await WarmExpressions(context, ((LinOpenSetting)state.Setting).Channel, "\"new-run\"");
        old.Open(new() { Channel = channel }); var write = Task.Run(() => old.Write(new() { Data = [1] }));
        try
        {
            await oldRaw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)); old.Dispose();
            var opening = new LIN.Executors.LinOpenExecutor(_ => next).ExecuteAsync(context);
            await Task.Delay(30); Assert.False(opening.IsCompleted); Assert.False(next.IsConnected);
            oldRaw.Release.Set(); await AssertRetiredWrite(write);
            var result = await opening; Assert.Equal(xTestPlatform.Core.Models.TestStatus.Completed, result.StepResult.Status);
            Assert.True(old.IsClosed); Assert.True(next.IsConnected);
        }
        finally
        {
            oldRaw.Release.Set(); await AssertRetiredWrite(write); await old.CloseAsync();
            foreach (var item in state.Items.Values.OfType<LinConnectionResource>()) await item.CloseAsync();
        }
    }

    [Fact]
    public async Task OpenStepHonorsConfiguredDeadlineWithoutOpeningNewDriver()
    {
        string channel = Channel(); var oldRaw = new Driver(); var old = new LinConnectionResource(oldRaw);
        var context = System.Reflection.DispatchProxy.Create<xTestPlatform.Core.Engine.IExecutionContext, LinContext>();
        var state = (LinContext)(object)context; var next = new Driver();
        state.Plugin = new LinOpenPlugin(); state.Setting = new LinOpenSetting { Channel = $"\"{channel}\"", ConnectionName = "\"new-run\"", TimeoutMs = 40 };
        await WarmExpressions(context, ((LinOpenSetting)state.Setting).Channel, "\"new-run\"");
        old.Open(new() { Channel = channel }); var write = Task.Run(() => old.Write(new() { Data = [1] }));
        try
        {
            await oldRaw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)); old.Dispose();
            var result = await new LIN.Executors.LinOpenExecutor(_ => next).ExecuteAsync(context);
            Assert.Equal(xTestPlatform.Core.Models.TestStatus.Error, result.StepResult.Status);
            Assert.False(next.IsConnected); Assert.False(old.IsClosed); Assert.Equal(0, oldRaw.CloseCount);
        }
        finally { oldRaw.Release.Set(); await AssertRetiredWrite(write); await old.CloseAsync(); }
    }

    [Fact]
    public async Task CloseStepHonorsConfiguredDeadlineAndKeepsUnreleasedResource()
    {
        var raw = new Driver(); var owner = new LinConnectionResource(raw); owner.Open(new() { Channel = Channel() });
        var context = System.Reflection.DispatchProxy.Create<xTestPlatform.Core.Engine.IExecutionContext, LinContext>();
        var state = (LinContext)(object)context;
        state.Plugin = new LinClosePlugin(); state.Setting = new LinCloseSetting { ConnectionName = "\"LIN1\"", TimeoutMs = 40 };
        await WarmExpressions(context, "\"LIN1\"");
        var write = Task.Run(() => owner.Write(new() { Data = [1] }));
        state.Items[LIN.Helpers.LinHelper.GetAdapterKey("LIN1")] = owner;
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var result = await new LIN.Executors.LinCloseExecutor().ExecuteAsync(context);
            Assert.Equal(xTestPlatform.Core.Models.TestStatus.Error, result.StepResult.Status);
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.CloseCount); Assert.Contains(owner, state.Items.Values);
        }
        finally { raw.Release.Set(); await AssertRetiredWrite(write); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task CloseTimeoutWaitingForRegistryStillFreezesCapturedOwner()
    {
        var context = System.Reflection.DispatchProxy.Create<xTestPlatform.Core.Engine.IExecutionContext, LinContext>();
        var state = (LinContext)(object)context;
        state.Plugin = new LinClosePlugin(); state.Setting = new LinCloseSetting { ConnectionName = "\"LIN1\"", TimeoutMs = 40 };
        await WarmExpressions(context, "\"LIN1\"");
        var raw = new Driver(); var owner = new LinConnectionResource(raw); owner.Open(new() { Channel = Channel() });
        state.Items[LIN.Helpers.LinHelper.GetAdapterKey("LIN1")] = owner;
        using var registry = await LinRegistryGuard.Enter(context.Resources, default);
        try
        {
            var result = await new LIN.Executors.LinCloseExecutor().ExecuteAsync(context);
            Assert.Equal(xTestPlatform.Core.Models.TestStatus.Error, result.StepResult.Status);
            Assert.False(owner.IsConnected, "关闭请求即使未取得登记门，也必须冻结捕获的旧资源");
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task ConfiguredWriteDeadlineRetainsNativeOwnershipUntilExit()
    {
        var raw = new Driver(); var owner = new LinConnectionResource(raw); owner.Open(new() { Channel = Channel() });
        var write = Task.Run(() => owner.Write(new() { Data = [1] }, default, 750));
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var error = await Assert.ThrowsAsync<TimeoutException>(() => write);
            Assert.Contains("750", error.Message);
            Assert.Equal(0, raw.CloseCount);
            var closing = owner.CloseAsync(2000); await Task.Delay(20); Assert.False(closing.IsCompleted);
            raw.Release.Set(); await closing; Assert.True(owner.IsClosed);
        }
        finally { raw.Release.Set(); try { await write; } catch (TimeoutException) { } await owner.CloseAsync(); }
    }

    [Fact]
    public async Task DisposeCancelsItsOwnPendingReadAndAllowsNextRun()
    {
        string channel = Channel(); var raw = new Driver(); var owner = new LinConnectionResource(raw);
        owner.Open(new() { Channel = channel });
        var read = Task.Run(() => owner.Read(10000));
        try
        {
            await raw.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            owner.Dispose();
            await RetiringResources.WaitForPreviousAsync(Key(channel), 2000);
            Assert.True(read.IsCompleted || owner.IsClosed, "旧资源收尾必须等待其接收实际退出");
            Assert.True(raw.ReadCancelled); Assert.True(owner.IsClosed); Assert.Equal(1, raw.CloseCount);
            var retired = await Assert.ThrowsAsync<InvalidOperationException>(() => read);
            Assert.Contains("退出", retired.Message); // Owner retirement is not cancellation of the original caller.
        }
        finally { raw.Release.Set(); try { await read; } catch (InvalidOperationException) { } await owner.CloseAsync(); }
    }

    [Fact]
    public async Task ImmediateRestartWaitsForUncancellableNativeWrite()
    {
        string channel = Channel(); var raw = new Driver(); var owner = new LinConnectionResource(raw);
        owner.Open(new() { Channel = channel });
        var write = Task.Run(() => owner.Write(new() { Data = [1] }));
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)); owner.Dispose();
            var restart = RetiringResources.WaitForPreviousAsync(Key(channel), 2000);
            await Task.Delay(30); Assert.False(restart.IsCompleted); Assert.Equal(0, raw.CloseCount);
            raw.Release.Set(); await AssertRetiredWrite(write); await restart;
            Assert.True(owner.IsClosed); Assert.Equal(1, raw.CloseCount);
        }
        finally { raw.Release.Set(); await AssertRetiredWrite(write); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task FailedCloseRemainsReachableForNextRunRetry()
    {
        string channel = Channel(); var raw = new Driver { FailClose = true }; var owner = new LinConnectionResource(raw);
        owner.Open(new() { Channel = channel });
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CloseAsync());
        raw.FailClose = false;
        await RetiringResources.WaitForPreviousAsync(Key(channel), 2000);
        Assert.True(owner.IsClosed); Assert.Equal(2, raw.CloseCount);
    }

    [Fact]
    public async Task RestartTimeoutDoesNotReleasePendingNativeWrite()
    {
        string channel = Channel(); var raw = new Driver(); var owner = new LinConnectionResource(raw);
        owner.Open(new() { Channel = channel }); var write = Task.Run(() => owner.Write(new() { Data = [1] }));
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)); owner.Dispose();
            await Assert.ThrowsAsync<TimeoutException>(() => RetiringResources.WaitForPreviousAsync(Key(channel), 30));
            Assert.Equal(0, raw.CloseCount); Assert.False(owner.IsClosed);
        }
        finally { raw.Release.Set(); await AssertRetiredWrite(write); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task WaitingDoesNotCloseAnotherActiveConnection()
    {
        string channel = Channel(); var raw = new Driver(); var owner = new LinConnectionResource(raw);
        owner.Open(new() { Channel = channel });
        await RetiringResources.WaitForPreviousAsync(Key(channel), 1000);
        Assert.Equal(0, raw.CloseCount); Assert.True(owner.IsConnected);
        await owner.CloseAsync();
    }

    private static async Task AssertRetiredWrite(Task write)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => write);
        Assert.Contains("退出", error.Message);
    }

    private static async Task WarmExpressions(xTestPlatform.Core.Engine.IExecutionContext context, params string[] expressions)
    {
        // Compile expression infrastructure before entering a deliberately blocked native call.
        foreach (string expression in expressions)
            await ExpressionEvaluatorFactory.Default.EvalStringAsync(expression, context);
    }

    private sealed class Driver : ILinAdapter
    {
        public readonly ManualResetEventSlim Release = new();
        public readonly TaskCompletionSource ReadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource WriteEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource OpenEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool BlockOpen;
        public bool IsConnected { get; private set; }
        public bool ReadCancelled;
        public bool FailClose;
        public int CloseCount;
        public void Open(LinAdapterConfig config) { OpenEntered.TrySetResult(); if (BlockOpen) Release.Wait(); IsConnected = true; }
        public void Close() { CloseCount++; if (FailClose) throw new InvalidOperationException("clear failed"); IsConnected = false; }
        public void Write(LinFrame frame) { WriteEntered.TrySetResult(); Release.Wait(); }
        public LinFrame? Read(int timeoutMs, CancellationToken ct = default)
        {
            ReadEntered.TrySetResult();
            try { Release.Wait(ct); return null; }
            catch (OperationCanceledException) { ReadCancelled = true; throw; }
        }
        public LinFrame? Read(byte id, int timeoutMs, CancellationToken ct = default) => Read(timeoutMs, ct);
        public void Wakeup(bool remote = true) { }
        public void Sleep(bool remote = true) { }
        public void Dispose() => Close();
    }
}
