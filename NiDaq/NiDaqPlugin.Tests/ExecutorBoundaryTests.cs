using System.Collections.Concurrent;
using AuditTestSupport;
using NiDaq.Executors;
using NiDaq.Helpers;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class ExecutorBoundaryTests
{
    [Fact]
    public async Task ActualConfigCancellationDuringSuccessLogFreezesOnlyItsCreatedOwner()
    {
        var setting = new NiDaqSyncConfigSetting { TaskName = "\"T\"", AiChannels = [new() { PhysicalChannel = "FakeLog/ai0" }], EncoderChannels = [new() { CounterChannel = "FakeLog/ctr0" }] };
        var (context, state) = ContextProxy.Create(new NiDaqSyncConfigPlugin(), setting); using var caller = new CancellationTokenSource(); state.OnLog = caller.Cancel;
        var owner = new DaqTaskResource(() => { }, () => { });
        var executor = new NiDaqSyncConfigExecutor((_, _) => { }, _ => owner);
        try
        {
            var result = await executor.ExecuteAsync(context, caller.Token); Assert.Equal(TestStatus.Aborted, result.StepResult.Status); Assert.True(owner.IsClosing);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Fact]
    public async Task ActualNullTaskNameDoesNotBecomeSourceTextAndConfigureADevice()
    {
        var setting = new NiDaqSyncConfigSetting { TaskName = "null", AiChannels = [new() { PhysicalChannel = "Dev1/ai0" }],
            EncoderChannels = [new() { CounterChannel = "Dev1/ctr0" }] };
        var (context, _) = ContextProxy.Create(new NiDaqSyncConfigPlugin(), setting); int configurations = 0;
        var owners = new List<DaqTaskResource>();
        var executor = new NiDaqSyncConfigExecutor((_, _) => configurations++, channels =>
        { var owner = new DaqTaskResource(() => { }, () => { }, channels); owners.Add(owner); return owner; });
        try
        {
            var result = await executor.ExecuteAsync(context); Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(0, configurations);
            Assert.Equal("0", await ExpressionEvaluatorFactory.Default.EvalStringAsync("0", context));
            Assert.Equal("", await ExpressionEvaluatorFactory.Default.EvalStringAsync("\"\"", context));
        }
        finally { foreach (var owner in owners) await owner.CloseAsync(2000, default); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualSyncConfigFirstRerunWaitsForSameTableCancelledOwner(bool alreadyReleased)
    {
        string device = "Fake" + Guid.NewGuid().ToString("N");
        var setting = new NiDaqSyncConfigSetting { TaskName = "\"T\"", TimeoutMs = 2000,
            AiChannels = [new() { PhysicalChannel = device + "/ai0" }], EncoderChannels = [new() { CounterChannel = device + "/ctr0" }] };
        var (context, _) = ContextProxy.Create(new NiDaqSyncConfigPlugin(), setting);
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>(setting.TaskName, context);
        using var configureExit = new ManualResetEventSlim(); using var releaseExit = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owners = new List<DaqTaskResource>(); int configurationCount = 0;
        var executor = new NiDaqSyncConfigExecutor((_, _) =>
        {
            if (Interlocked.Increment(ref configurationCount) == 1) { entered.TrySetResult(); configureExit.Wait(); }
        }, physical =>
        {
            bool first = owners.Count == 0;
            var resource = new DaqTaskResource(() => { }, () => { if (first) { releasing.TrySetResult(); releaseExit.Wait(); } }, physical);
            owners.Add(resource); return resource;
        });
        using var caller = new CancellationTokenSource();
        var first = executor.ExecuteAsync(context, caller.Token);
        Task<ExecutionResult>? next = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(4)); caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await first.WaitAsync(TimeSpan.FromSeconds(2))).StepResult.Status);
            configureExit.Set(); await releasing.Task.WaitAsync(TimeSpan.FromSeconds(2));
            if (alreadyReleased) { releaseExit.Set(); await owners[0].CloseAsync(2000, default); }
            next = executor.ExecuteAsync(context);
            if (!alreadyReleased)
            {
                await Task.WhenAny(next, Task.Delay(50)); Assert.False(next.IsCompleted); Assert.Equal(1, configurationCount); releaseExit.Set();
            }
            Assert.Equal(TestStatus.Completed, (await next.WaitAsync(TimeSpan.FromSeconds(3))).StepResult.Status);
            Assert.Equal(2, configurationCount); Assert.Same(owners[^1], NiDaqTaskRegistry.Get(context, "T"));
        }
        finally
        {
            configureExit.Set(); releaseExit.Set(); await first;
            if (next != null) await next; foreach (var owner in owners) await owner.CloseAsync(2000, default);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualWaveformReadDoesNotPublishUnderAReplacementOwner(bool sync)
    {
        var old = new DaqTaskResource(() => { }, () => { }); var fresh = new DaqTaskResource(() => { }, () => { });
        await old.ConfigureAsync(() => { }, default); await fresh.ConfigureAsync(() => { }, default);
        IStepPlugin plugin = sync ? new NiDaqSyncReadPlugin() : new NiDaqAiReadPlugin();
        object setting = sync ? new NiDaqSyncReadSetting { TaskName = "\"T\"", ResultVariable = "Locals.w", SaveToFile = false, ExportFormat = DaqExportFormat.Variable }
            : new NiDaqAiReadSetting { TaskName = "\"T\"", ResultVariable = "Locals.w", SaveToFile = false, ExportFormat = DaqExportFormat.Variable };
        var (context, state) = ContextProxy.Create(plugin, setting); var store = (StoreProxy)state.Store!; store.Values["NiDaq.Task:T"] = old;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); using var exit = new ManualResetEventSlim();
        DaqReadResult Read(DaqTaskResource _, int __, int ___, bool ____) { entered.TrySetResult(); exit.Wait(); return new(new double[,] { { 2 } }, ["AI"], 1000); }
        IStepExecutor executor = sync ? new NiDaqSyncReadExecutor(Read) : new NiDaqAiReadExecutor(Read);
        using var guard = new CancellationTokenSource(15000); var continuation = new HeldDaqExecutorContext();
        var execution = continuation.Start(() => executor.ExecuteAsync(context));
        try
        {
            await continuation.PumpUntilAsync(entered.Task, execution, guard.Token); exit.Set(); await continuation.WaitForContinuationAsync(guard.Token);
            store.Values["NiDaq.Task:T"] = fresh; await continuation.DrainUntilAsync(execution, guard.Token);
            Assert.Equal(TestStatus.Error, (await execution).StepResult.Status); Assert.Empty(state.Published); Assert.False(fresh.IsClosed);
        }
        finally { exit.Set(); await continuation.DrainUntilAsync(execution, guard.Token); await old.CloseAsync(2000, default); await fresh.CloseAsync(2000, default); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualReadCancelledInsideFinalLogCannotReturnCompleted(bool sync)
    {
        var owner = new DaqTaskResource(() => { }, () => { }); await owner.ConfigureAsync(() => { }, default);
        IStepPlugin plugin = sync ? new NiDaqSyncReadPlugin() : new NiDaqAiReadPlugin();
        object setting = sync ? new NiDaqSyncReadSetting { TaskName = "\"T\"", ResultVariable = "Locals.w", SaveToFile = false, ExportFormat = DaqExportFormat.Variable }
            : new NiDaqAiReadSetting { TaskName = "\"T\"", ResultVariable = "Locals.w", SaveToFile = false, ExportFormat = DaqExportFormat.Variable };
        var (context, state) = ContextProxy.Create(plugin, setting); ((StoreProxy)state.Store!).Values["NiDaq.Task:T"] = owner;
        using var caller = new CancellationTokenSource(); state.OnLog = caller.Cancel;
        DaqReadResult Read(DaqTaskResource _, int __, int ___, bool ____) => new(new double[,] { { 2 } }, ["AI"], 1000);
        IStepExecutor executor = sync ? new NiDaqSyncReadExecutor(Read) : new NiDaqAiReadExecutor(Read);
        try { Assert.Equal(TestStatus.Aborted, (await executor.ExecuteAsync(context, caller.Token)).StepResult.Status); }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Fact]
    public async Task EveryActualExecutorHonorsAlreadyCancelledCallerBeforeResourceLookup()
    {
        using var caller = new CancellationTokenSource(); caller.Cancel();
        IStepPlugin[] plugins = [new NiDaqAiConfigPlugin(), new NiDaqAiReadPlugin(), new NiDaqAoConfigPlugin(), new NiDaqAoWriteSinglePlugin(),
            new NiDaqEncoderConfigPlugin(), new NiDaqEncoderReadPlugin(), new NiDaqSyncConfigPlugin(), new NiDaqSyncReadPlugin(),
            new NiDaqDiReadPlugin(), new NiDaqDoWritePlugin(), new NiDaqTaskStartPlugin(), new NiDaqTaskStopPlugin()];
        var failures = new List<string>();
        foreach (var plugin in plugins)
        {
            var (context, _) = ContextProxy.Create(plugin, plugin.CreateSerializer().CreateDefault());
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token).WaitAsync(TimeSpan.FromSeconds(10));
            if (result.StepResult.Status != TestStatus.Aborted) failures.Add(plugin.StepTypeId + ": " + result.StepResult.Status);
        }
        Assert.Empty(failures);
    }
}

internal sealed class HeldDaqExecutorContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posts = new();
    public override void Post(SendOrPostCallback callback, object? state) => _posts.Enqueue((callback, state));
    internal Task<ExecutionResult> Start(Func<Task<ExecutionResult>> execute)
    { var previous = Current; SetSynchronizationContext(this); try { return execute(); } finally { SetSynchronizationContext(previous); } }
    private bool RunOne()
    {
        if (!_posts.TryDequeue(out var post)) return false; var previous = Current; SetSynchronizationContext(this);
        try { post.Callback(post.State); } finally { SetSynchronizationContext(previous); } return true;
    }
    internal async Task PumpUntilAsync(Task target, Task<ExecutionResult> execution, CancellationToken guard)
    { while (!target.IsCompleted) { guard.ThrowIfCancellationRequested(); if (execution.IsCompleted) throw new InvalidOperationException((await execution).StepResult.Error?.Detail); if (!RunOne()) await Task.Delay(2, guard); } }
    internal async Task WaitForContinuationAsync(CancellationToken guard) { while (_posts.IsEmpty) await Task.Delay(2, guard); }
    internal async Task DrainUntilAsync(Task execution, CancellationToken guard) { while (!execution.IsCompleted) { guard.ThrowIfCancellationRequested(); if (!RunOne()) await Task.Delay(2, guard); } }
}
