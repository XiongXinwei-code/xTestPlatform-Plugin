using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using OpcUa.Helpers;
using OpcUa.Models;
using OpcUa.Executors;
using Opc.Ua.Client;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace OpcUa.Tests;

public sealed class DebugRerunBoundaryTests
{
    [Fact]
    public async Task ActualConnectCancellationReturnsAbortedWithoutWaitingOutCleanupBudget()
    {
        var plugin = new OpcUaConnectPlugin();
        var setting = new OpcUaConnectSetting { ConnectionName = "\"C\"", EndpointUrl = "\"opc.tcp://unit.test:4840\"", TimeoutMs = 1500 };
        var (context, state) = BoundaryContext.Create(plugin, setting);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCreate = new TaskCompletionSource<Session>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var caller = new CancellationTokenSource();
        var executor = new OpcUaConnectExecutor(_ => { entered.TrySetResult(); return releaseCreate.Task; });
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>(setting.ConnectionName, context);
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>(setting.EndpointUrl, context);
        var execution = executor.ExecuteAsync(context, caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            caller.Cancel();
            Assert.Same(execution, await Task.WhenAny(execution, Task.Delay(250)));
            Assert.Equal(TestStatus.Aborted, (await execution).StepResult.Status);
            Assert.True(state.Store.Values.TryGetValue("OpcUa_C", out var current));
            Assert.False(Assert.IsType<OpcUaConnectionResource>(current).IsClosed);
        }
        finally
        {
            releaseCreate.TrySetException(new InvalidOperationException("fake create released"));
            await execution;
            if (state.Store.Values.TryGetValue("OpcUa_C", out var current))
                await Assert.IsType<OpcUaConnectionResource>(current).CloseAsync(2000);
        }
    }

    [Fact]
    public async Task ActualReadEditorAllowsCsvWithoutWaveformVariable()
    {
        var plugin = new OpcUaDataAcqReadPlugin();
        var setting = new OpcUaDataAcqReadSetting { SaveToFile = true, CsvFilePath = "\"C:/audit/records.csv\"" };
        var (context, _) = BoundaryContext.Create(plugin, setting);
        var errors = await ValidateReadAsync(plugin, setting, context);
        Assert.DoesNotContain(errors, error => error.Message.Contains("结果变量不能为空"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualReadEditorChecksEachOptionalJsonVariable(bool health)
    {
        var plugin = new OpcUaDataAcqReadPlugin();
        var setting = new OpcUaDataAcqReadSetting { SaveToFile = true, CsvFilePath = "\"C:/audit/records.csv\"" };
        if (health) setting.HealthVariable = "Locals.MissingHealth";
        else setting.RecordsJsonVariable = "Locals.MissingRecords";
        var (context, _) = BoundaryContext.Create(plugin, setting);
        var errors = await ValidateReadAsync(plugin, setting, context);
        Assert.Contains(errors, error => error.Message.Contains(health ? "MissingHealth" : "MissingRecords"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualStartCannotPublishAnAcquisitionAfterItsCallerLeaves(bool deadline)
    {
        var plugin = new OpcUaDataAcqStartPlugin();
        var setting = new OpcUaDataAcqStartSetting
        {
            ConnectionName = "\"C\"", TaskName = "\"T\"", TimeoutMs = deadline ? 30 : 1000,
            SamplingIntervalMs = 5, Items = [new() { NodeId = "ns=2;s=V" }]
        };
        var (context, state) = BoundaryContext.Create(plugin, setting);
        var connection = new OpcUaConnectionResource(() => Task.CompletedTask, 1000);
        state.Store.Values["OpcUa_C"] = connection;
        using var caller = new CancellationTokenSource();
        // Warm expression compilation; this test fixes the publication window, not compiler latency.
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>(setting.TaskName, context);
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>(setting.ConnectionName, context);
        state.Store.BeforeTryAdd = () =>
        {
            if (deadline) Thread.Sleep(70);
            else caller.Cancel();
        };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.Equal(deadline ? TestStatus.Error : TestStatus.Aborted, result.StepResult.Status);
            Assert.False(state.Store.Values.ContainsKey("OpcUaDataAcq_T"));
        }
        finally { await connection.CloseAsync(2000); }
    }

    private static Task<IReadOnlyList<StepSettingError>> ValidateReadAsync(
        OpcUaDataAcqReadPlugin plugin, OpcUaDataAcqReadSetting setting, IExecutionContext context) =>
        new OpcUa.UI.OpcUaDataAcqReadEditorPlugin().ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = plugin.CreateSerializer().Serialize(setting), Evaluator = ExpressionEvaluatorFactory.Default,
            ExecutionContext = context, SequenceFile = new SequenceFile(), Block = [], CurrentStep = new Step()
        });
}

public class BoundaryContext : DispatchProxy
{
    public BoundaryStore Store = null!;
    public readonly ConcurrentDictionary<string, object> Variables = new();
    public readonly ConcurrentQueue<(string Path, object? Value)> Published = new();
    public Action? OnLog;
    public Action? OnPublish;
    public Action? BeforeCurrentStep;
    public bool AbortRequested;
    public void UseRegistry(IResourceRegistry registry) => _registry = registry;
    private object _registry = null!;
    private StepExecutionInfo _step = null!;
    public static (IExecutionContext Context, BoundaryContext State) Create(IStepPlugin plugin, object setting, BoundaryStore? sharedStore = null)
    {
        var context = DispatchProxy.Create<IExecutionContext, BoundaryContext>();
        var state = (BoundaryContext)(object)context;
        var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion;
        step.StepSetting.Setting = plugin.CreateSerializer().Serialize(setting);
        state._step = new() { Step = step };
        var type = typeof(IExecutionContext).GetProperty("Resources")?.PropertyType ??
            typeof(IExecutionContext).GetInterfaces().SelectMany(i => i.GetProperties()).First(p => p.Name == "Resources").PropertyType;
        state._registry = sharedStore ?? DispatchProxy.Create(type, typeof(BoundaryStore));
        state.Store = (BoundaryStore)state._registry;
        return (context, state);
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "Log") { OnLog?.Invoke(); return null; }
        if (method.Name == "get_CurrentStep") { BeforeCurrentStep?.Invoke(); return _step; }
        if (method.Name == "SetVariable") { OnPublish?.Invoke(); Published.Enqueue(((string)args![0]!, args[1])); return null; }
        return method.Name switch
        {
            "get_CurrentStep" => _step,
            "get_Resources" => _registry,
            "get_IsAbortRequested" => AbortRequested,
            "HasVariable" => Variables.ContainsKey((string)args![0]!),
            "GetVariable" => Variables.TryGetValue((string)args![0]!, out var value) ? value : null,
            _ => method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null
        };
    }
}

public class BoundaryStore : DispatchProxy
{
    public readonly ConcurrentDictionary<string, object> Values = new();
    public Action? BeforeTryAdd;
    public Action<string, Type>? OnTryGet;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        args ??= [];
        string key = args.Length == 0 ? "" : args[0]?.ToString() ?? "";
        switch (method!.Name)
        {
            case "TryGet":
                OnTryGet?.Invoke(key, method.GetGenericArguments()[0]);
                bool exists = Values.TryGetValue(key, out var value) && method.GetGenericArguments()[0].IsInstanceOfType(value);
                args[1] = exists ? value : null; return exists;
            case "TryAdd": BeforeTryAdd?.Invoke(); return Values.TryAdd(key, args[1]!);
            case "Set": Values[key] = args[1]!; return null;
            case "Remove":
                bool removed = Values.TryRemove(key, out _); return method.ReturnType == typeof(bool) ? removed : null;
            case "Contains": return Values.ContainsKey(key);
            default: throw new NotSupportedException(method.Name);
        }
    }
}
