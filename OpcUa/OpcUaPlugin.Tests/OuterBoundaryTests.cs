using System.Collections.Concurrent;
using Opc.Ua;
using OpcUa.Executors;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace OpcUa.Tests;

public sealed class OuterBoundaryTests
{
    [Theory]
    [InlineData("Connect")]
    [InlineData("Disconnect")]
    [InlineData("AcqStart")]
    [InlineData("AcqStop")]
    public async Task ActualLifecycleLogCancellationDoesNotReportCompleted(string kind)
    {
        using var caller = new CancellationTokenSource();
        var items = new List<OpcUaDataAcqItem> { new() { NodeId = "ns=2;s=V" } };
        var owner = new OpcUaConnectionResource(() => Task.CompletedTask, 2000)
        { AcquisitionReader = (_, _) => Task.FromResult(new DataValueCollection { new DataValue(1) { StatusCode = StatusCodes.Good } }) };
        IStepPlugin plugin; object setting;
        switch (kind)
        {
            case "Connect": plugin = new OpcUaConnectPlugin(); setting = new OpcUaConnectSetting { ConnectionName = "\"C\"", EndpointUrl = "\"opc.tcp://unit.test:4840\"" }; break;
            case "Disconnect": plugin = new OpcUaDisconnectPlugin(); setting = new OpcUaDisconnectSetting { ConnectionName = "\"C\"" }; break;
            case "AcqStart": plugin = new OpcUaDataAcqStartPlugin(); setting = new OpcUaDataAcqStartSetting { ConnectionName = "\"C\"", TaskName = "\"T\"", Items = [.. items] }; break;
            default: plugin = new OpcUaDataAcqStopPlugin(); setting = new OpcUaDataAcqStopSetting { TaskName = "\"T\"" }; break;
        }
        var (context, state) = BoundaryContext.Create(plugin, setting);
        if (kind != "Connect") state.Store.Values["OpcUa_C"] = owner;
        if (kind == "AcqStop") { state.Store.Values["OpcUaDataAcq_T"] = owner.StartAcquisition("T", items, 10, 0, 100); state.Store.Values["OpcUaDataAcq_T_items"] = items; }
        state.OnLog = caller.Cancel;
        IStepExecutor executor = kind == "Connect" ? new OpcUaConnectExecutor(_ => Task.FromResult<Opc.Ua.Client.Session>(null!)) : plugin.CreateExecutor();
        try
        {
            Assert.Equal(TestStatus.Aborted, (await executor.ExecuteAsync(context, caller.Token)).StepResult.Status);
            if (kind == "AcqStart") Assert.True(Assert.IsType<OpcUaDataAcqTask>(state.Store.Values["OpcUaDataAcq_T"]).IsStopRequested);
        }
        finally
        {
            state.OnLog = null; await owner.CloseAsync(2000);
            if (state.Store.Values.TryGetValue("OpcUa_C", out var created) && created is OpcUaConnectionResource connection && !ReferenceEquals(connection, owner)) await connection.CloseAsync(2000);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualAcquisitionStopNeverTargetsAStartedLaterGeneration(bool hadOldTask)
    {
        var startPlugin = new OpcUaDataAcqStartPlugin();
        var startSetting = new OpcUaDataAcqStartSetting { TaskName = "\"T\"", ConnectionName = "\"C\"", SamplingIntervalMs = 10,
            Items = [new() { NodeId = "ns=2;s=V" }] };
        var (startContext, state) = BoundaryContext.Create(startPlugin, startSetting);
        var connection = new OpcUaConnectionResource(() => Task.CompletedTask, 2000)
        { AcquisitionReader = (_, _) => Task.FromResult(new DataValueCollection { new DataValue(1) { StatusCode = StatusCodes.Good } }) };
        state.Store.Values["OpcUa_C"] = connection;
        var (stopContext, _) = BoundaryContext.Create(new OpcUaDataAcqStopPlugin(), new OpcUaDataAcqStopSetting { TaskName = "\"T\"" }, state.Store);
        const string key = "OpcUaDataAcq_T";
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>(startSetting.TaskName, startContext);
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>(startSetting.ConnectionName, startContext);
        if (hadOldTask) Assert.Equal(TestStatus.Completed, (await startPlugin.CreateExecutor().ExecuteAsync(startContext)).StepResult.Status);
        OpcUaDataAcqTask? fresh = null;
        var stopExecutor = new OpcUaDataAcqStopExecutor(() =>
        {
            var started = startPlugin.CreateExecutor().ExecuteAsync(startContext).GetAwaiter().GetResult();
            Assert.Equal(TestStatus.Completed, started.StepResult.Status);
            fresh = Assert.IsType<OpcUaDataAcqTask>(state.Store.Values[key]);
        });
        try
        {
            var stopped = await stopExecutor.ExecuteAsync(stopContext).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(TestStatus.Error, stopped.StepResult.Status); // 原目标为空或已被新 Start 替换，绝不误报关闭新任务。
            Assert.NotNull(fresh); Assert.False(fresh.IsStopRequested); Assert.Same(fresh, state.Store.Values[key]);
        }
        finally { await connection.CloseAsync(2000); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ActualReadDoesNotPublishFromAnExpiredCallerOrReplacedOwner(bool batch, bool cancel)
    {
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var value = new TaskCompletionSource<DataValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new OpcUaConnectionResource(() => Task.CompletedTask, 2000);
        var fresh = new OpcUaConnectionResource(() => Task.CompletedTask, 2000);
        IStepPlugin plugin = batch ? new OpcUaBatchReadPlugin() : new OpcUaReadPlugin();
        object setting = batch ? new OpcUaBatchReadSetting { ConnectionName = "\"C\"", Items = [new() { NodeId = "ns=2;s=V", ResultVariable = "Locals.v" }] }
            : new OpcUaReadSetting { ConnectionName = "\"C\"", NodeId = "ns=2;s=V", ResultVariable = "Locals.v" };
        var (context, state) = BoundaryContext.Create(plugin, setting);
        state.Store.Values["OpcUa_C"] = old;
        IStepExecutor executor = batch ? new OpcUaBatchReadExecutor(async (_, _) =>
        { entered.TrySetResult(); return new ReadResponse { Results = [await value.Task] }; })
            : new OpcUaReadExecutor((_, _) => { entered.TrySetResult(); return value.Task; });
        var continuation = new HeldExecutorContext();
        var execution = continuation.Start(() => executor.ExecuteAsync(context, caller.Token));
        try
        {
            await continuation.PumpUntilAsync(entered.Task, execution, guard.Token);
            value.SetResult(new DataValue(new Variant(77)) { StatusCode = StatusCodes.Good });
            await continuation.WaitForContinuationAsync(guard.Token);
            if (cancel) caller.Cancel(); else state.Store.Values["OpcUa_C"] = fresh;
            await continuation.DrainUntilAsync(execution, guard.Token);
            Assert.Equal(cancel ? TestStatus.Aborted : TestStatus.Error, (await execution).StepResult.Status);
            Assert.Empty(state.Published);
            Assert.False(fresh.IsClosed);
        }
        finally
        {
            value.TrySetResult(new DataValue(77)); await continuation.DrainUntilAsync(execution, guard.Token);
            await old.CloseAsync(2000); await fresh.CloseAsync(2000);
        }
    }

    [Fact]
    public async Task EveryActualExecutorHonorsAlreadyCancelledCallerBeforeResourceLookup()
    {
        using var caller = new CancellationTokenSource(); caller.Cancel();
        IStepPlugin[] plugins = [new OpcUaConnectPlugin(), new OpcUaDisconnectPlugin(), new OpcUaReadPlugin(), new OpcUaWritePlugin(), new OpcUaBatchReadPlugin(),
            new OpcUaBatchWritePlugin(), new OpcUaSubscribePlugin(), new OpcUaDataAcqStartPlugin(), new OpcUaDataAcqReadPlugin(), new OpcUaDataAcqStopPlugin()];
        var failures = new List<string>();
        foreach (var plugin in plugins)
        {
            var (context, _) = BoundaryContext.Create(plugin, plugin.CreateSerializer().CreateDefault());
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token).WaitAsync(TimeSpan.FromSeconds(10));
            if (result.StepResult.Status != TestStatus.Aborted) failures.Add(plugin.StepTypeId + ": " + result.StepResult.Status);
        }
        Assert.Empty(failures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualWriteCancellationInsideSuccessLogCannotReturnCompleted(bool batch)
    {
        using var caller = new CancellationTokenSource();
        IStepPlugin plugin = batch ? new OpcUaBatchWritePlugin() : new OpcUaWritePlugin();
        object setting = batch ? new OpcUaBatchWriteSetting { ConnectionName = "\"C\"", Items = [new() { NodeId = "ns=2;s=V", WriteValue = "\"1\"", DataType = OpcUaDataType.String }] }
            : new OpcUaWriteSetting { ConnectionName = "\"C\"", NodeId = "ns=2;s=V", WriteValue = "\"1\"", DataType = OpcUaDataType.String };
        var (context, state) = BoundaryContext.Create(plugin, setting);
        var owner = new OpcUaConnectionResource(() => Task.CompletedTask, 2000); state.Store.Values["OpcUa_C"] = owner;
        state.OnLog = caller.Cancel;
        Task<DataValue> Read(NodeId _, CancellationToken __) => Task.FromResult(new DataValue(1));
        Task<WriteResponse> Write(WriteValueCollection _, CancellationToken __) => Task.FromResult(new WriteResponse { Results = [StatusCodes.Good] });
        IStepExecutor executor = batch ? new OpcUaBatchWriteExecutor(Read, Write) : new OpcUaWriteExecutor(Read, Write);
        try { Assert.Equal(TestStatus.Aborted, (await executor.ExecuteAsync(context, caller.Token)).StepResult.Status); }
        finally { await owner.CloseAsync(2000); }
    }

    [Fact]
    public async Task ActualStringWriteDoesNotSendNullSourceText()
    {
        var (context, state) = BoundaryContext.Create(new OpcUaWritePlugin(), new OpcUaWriteSetting
        { ConnectionName = "\"C\"", NodeId = "ns=2;s=V", WriteValue = "null", DataType = OpcUaDataType.String });
        var owner = new OpcUaConnectionResource(() => Task.CompletedTask, 2000); state.Store.Values["OpcUa_C"] = owner;
        object? written = new object();
        var executor = new OpcUaWriteExecutor((_, _) => Task.FromResult(new DataValue(1)), (values, _) =>
        { written = values[0].Value.Value; return Task.FromResult(new WriteResponse { Results = [StatusCodes.Good] }); });
        try
        {
            var result = await executor.ExecuteAsync(context);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status); Assert.Equal("", written);
        }
        finally { await owner.CloseAsync(2000); }
    }
}

internal sealed class HeldExecutorContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posts = new();
    public override void Post(SendOrPostCallback callback, object? state) => _posts.Enqueue((callback, state));
    internal Task<ExecutionResult> Start(Func<Task<ExecutionResult>> execute)
    { var previous = Current; SetSynchronizationContext(this); try { return execute(); } finally { SetSynchronizationContext(previous); } }
    private bool RunOne()
    {
        if (!_posts.TryDequeue(out var post)) return false;
        var previous = Current; SetSynchronizationContext(this); try { post.Callback(post.State); } finally { SetSynchronizationContext(previous); } return true;
    }
    internal async Task PumpUntilAsync(Task target, Task<ExecutionResult> execution, CancellationToken guard)
    { while (!target.IsCompleted) { guard.ThrowIfCancellationRequested(); if (execution.IsCompleted) throw new InvalidOperationException((await execution).StepResult.Error?.Detail); if (!RunOne()) await Task.Delay(2, guard); } }
    internal async Task WaitForContinuationAsync(CancellationToken guard) { while (_posts.IsEmpty) await Task.Delay(2, guard); }
    internal async Task DrainUntilAsync(Task execution, CancellationToken guard) { while (!execution.IsCompleted) { guard.ThrowIfCancellationRequested(); if (!RunOne()) await Task.Delay(2, guard); } }
}
