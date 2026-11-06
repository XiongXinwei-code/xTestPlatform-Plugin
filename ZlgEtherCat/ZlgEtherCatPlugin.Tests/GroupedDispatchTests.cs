using System.Reflection;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;
using Xunit;

namespace ZlgEtherCat.Tests;

public sealed class GroupedDispatchTests
{
    [Fact]
    public async Task All109ActionsDispatchExactlyOnceThroughTheRealExecutorBoundary()
    {
        foreach (var (group, operations) in GroupedCases.Expected)
        foreach (var operation in operations)
        {
            var plugin = GroupedCases.Plugin(group);
            var setting = GroupedCases.Setting(group, operation);
            GroupedCases.UseValidRequestSample(setting);
            setting.ResultVariable = "Locals.Sample";
            setting.TimeoutMs = 10000;
            var (context, state) = GroupedExecutionContext.Create(plugin, setting);
            var type = typeof(RecordingExecutor<>).MakeGenericType(setting.GetType());
            var executor = (IRecordingExecutor)Activator.CreateInstance(type, plugin)!;
            var result = await executor.ExecuteAsync(context);
            Assert.True(result.StepResult.Status == TestStatus.Completed, $"{group}/{operation}: {result.StepResult.Error}");
            Assert.Equal(1, executor.Calls);
            Assert.Equal(operation, executor.Operation);
            Assert.NotNull(executor.Request);
            Assert.InRange(executor.Request!.TimeoutMs, 1, setting.TimeoutMs);
            Assert.Single(state.Writes);
            Assert.Equal("Locals.Sample", state.Writes[0].Path);
            Assert.Equal("recorded", state.Writes[0].Value);
        }
    }

    [Fact]
    public async Task ChangingTheStepWhileDispatchWaitsCannotChangeItsCapturedOperationOrArguments()
    {
        var plugin = new PdoWritePlugin();
        var (context, state) = GroupedExecutionContext.Create(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo, Offset = 0x1000 });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new RecordingExecutor<PdoWriteSetting>(plugin)
        {
            Handler = async (_, _, _, ct) => { entered.SetResult(); await release.Task.WaitAsync(ct); return "read"; }
        };
        var run = executor.ExecuteAsync(context);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        state.SetStep(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdoMasked, Offset = 0x2000, Mask = "new byte[] {1}" });
        release.SetResult();
        Assert.Equal(TestStatus.Completed, (await run).StepResult.Status);
        Assert.Equal(1, executor.Calls);
        Assert.Equal("WritePdo", executor.Operation);
        Assert.Equal(0x1000, executor.Request!.Get<int>("Offset"));
        Assert.False(executor.Request.Arguments.ContainsKey("Mask"));
    }

    [Fact]
    public async Task InvalidSelectedParametersNeverDispatchAndInactiveWriteParametersDoNotExecute()
    {
        var plugin = new PdoWritePlugin();
        var (context, state) = GroupedExecutionContext.Create(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo, Mask = "bad expression !!!" });
        var executor = new RecordingExecutor<PdoWriteSetting>(plugin);
        Assert.Equal(TestStatus.Completed, (await executor.ExecuteAsync(context)).StepResult.Status);
        Assert.Equal("WritePdo", executor.Operation);
        Assert.Equal(1, executor.Calls);
        state.SetStep(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdoMasked, Mask = "bad expression !!!" });
        var rejected = await executor.ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, rejected.StepResult.Status);
        Assert.NotNull(rejected.StepResult.Error);
        Assert.Equal(1, executor.Calls);
        state.Current!.Step.StepSetting.Setting = [0xc1];
        Assert.Equal(TestStatus.Error, (await executor.ExecuteAsync(context)).StepResult.Status);
        Assert.Equal(1, executor.Calls);
    }

    [Fact]
    public async Task InternalDeadlineIsErrorWhileOnlyCallerCancellationIsAborted()
    {
        var plugin = new BeginExitPlugin();
        var (context, state) = GroupedExecutionContext.Create(plugin, new BeginExitSetting { Operation = BeginExitOperation.BeginExit, TimeoutMs = 250 });
        var executor = new RecordingExecutor<BeginExitSetting>(plugin)
        {
            Handler = async (_, _, _, ct) => { await Task.Delay(Timeout.Infinite, ct); return null; }
        };
        var timedOut = await executor.ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, timedOut.StepResult.Status);
        Assert.NotNull(timedOut.StepResult.Error);
        Assert.Equal(1, executor.Calls);
        state.SetStep(plugin, new BeginExitSetting { Operation = BeginExitOperation.BeginExit, TimeoutMs = 5000 });
        using var caller = new CancellationTokenSource();
        executor.Handler = (_, _, _, _) => { caller.Cancel(); return Task.FromCanceled<object?>(caller.Token); };
        Assert.Equal(TestStatus.Aborted, (await executor.ExecuteAsync(context, caller.Token)).StepResult.Status);
        Assert.Equal(2, executor.Calls);
    }

    [Fact]
    public async Task RetiredExecutionCannotPublishACompletedDispatchResult()
    {
        var plugin = new ClosePlugin();
        var (context, state) = GroupedExecutionContext.Create(plugin, new CloseSetting { Operation = CloseOperation.Close, ResultVariable = "Locals.Sample" });
        var executor = new RecordingExecutor<CloseSetting>(plugin)
        {
            Handler = (_, _, _, _) => { state.ReplaceRegistry(); return Task.FromResult<object?>("late"); }
        };
        Assert.Equal(TestStatus.Error, (await executor.ExecuteAsync(context)).StepResult.Status);
        Assert.Equal(1, executor.Calls);
        Assert.Empty(state.Writes);
    }

    private interface IRecordingExecutor : IStepExecutor
    {
        int Calls { get; }
        string? Operation { get; }
        ZlgRequest? Request { get; }
    }

    private sealed class RecordingExecutor<T>(IStepPlugin plugin) : ZlgExecutorBase<T>(plugin), IRecordingExecutor where T : ZlgGroupedSetting
    {
        public int Calls { get; private set; }
        public string? Operation { get; private set; }
        public ZlgRequest? Request { get; private set; }
        public Func<string, ZlgRequest, IExecutionContext, CancellationToken, Task<object?>>? Handler { get; set; }
        protected override Task<object?> DispatchAsync(string operation, ZlgRequest request, IExecutionContext context, CancellationToken ct)
        {
            Calls++;
            Operation = operation;
            Request = request;
            return Handler?.Invoke(operation, request, context, ct) ?? Task.FromResult<object?>("recorded");
        }
    }
}

public class GroupedExecutionContext : DispatchProxy
{
    private IResourceRegistry _registry = Create<IResourceRegistry, ChannelRegistryProxy>();
    internal StepExecutionInfo? Current { get; private set; }
    internal List<(string Path, object? Value)> Writes { get; } = [];

    internal static (IExecutionContext Context, GroupedExecutionContext State) Create(IStepPlugin plugin, ZlgSetting setting)
    {
        var context = Create<IExecutionContext, GroupedExecutionContext>();
        var state = (GroupedExecutionContext)(object)context;
        state.SetStep(plugin, setting);
        return (context, state);
    }

    internal void SetStep(IStepPlugin plugin, ZlgSetting setting) => Current = new StepExecutionInfo { Step = GroupedCases.Step(plugin, setting) };
    internal void ReplaceRegistry() => _registry = Create<IResourceRegistry, ChannelRegistryProxy>();

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        switch (method!.Name)
        {
            case "get_Resources": return _registry;
            case "get_CurrentStep": return Current;
            case "HasVariable": return args?[0]?.ToString() == "Locals.Sample";
            case "SetVariable": Writes.Add(((string)args![0]!, args[1])); return null;
        }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
