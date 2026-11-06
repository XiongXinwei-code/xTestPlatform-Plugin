using System.IO;
using System.Reflection;
using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.Models;
using LabVIEWCallPlugin.UI;
using PluginCommon;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

[CollectionDefinition("VI execution", DisableParallelization = true)]
public sealed class ExecutionCollection { }

[Collection("VI execution")]
public sealed class StepExecutionTests
{
    [Theory]
    [InlineData(".lvlibp")]
    [InlineData(".dll")]
    [InlineData(".exe")]
    public async Task IdeViStageRejectsOtherArtifactsBeforeFilesystemOrBridgeAccess(string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), "unsupported" + extension);
        var context = BridgeTestContext.For(path, 5000);
        var state = (BridgeTestContext)(object)context;
        int fileCalls = 0;
        int nativeCalls = 0;
        var executor = ExecutionScopedExecutor.Create("LabVIEWCall", () => new LabVIEWCallExecutor(
            (_, _, _, _, _) => { nativeCalls++; return new(); },
            _ => { fileCalls++; return true; }));
        try
        {
            var result = await executor.ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains(".vi", result.StepResult.Error!.Message);
            Assert.Equal(0, fileCalls);
            Assert.Equal(0, nativeCalls);
            Assert.Empty(state.Writes);
        }
        finally { state.Registry.Retire(); }
    }

    [Theory]
    [InlineData("bridge")]
    [InlineData("metadata")]
    [InlineData("timeout")]
    [InlineData("unexpected-cancellation")]
    public async Task ViFailureReturnsErrorAndNeverPublishesAResult(string failure)
    {
        string path = Path.Combine(Path.GetTempPath(), "failed-call.vi");
        var context = BridgeTestContext.For(path, 5000);
        var state = (BridgeTestContext)(object)context;
        Exception expected = failure switch
        {
            "bridge" => new InvalidOperationException("辅助进程加载失败 0xC0000005"),
            "metadata" => new InvalidDataException("输入端子名称重复"),
            "timeout" => new TimeoutException("VI 服务调用超过等待期限"),
            _ => new OperationCanceledException("桥内部取消，调用方仍有效")
        };
        var executor = ExecutionScopedExecutor.Create("LabVIEWCall", () =>
            new LabVIEWCallExecutor((_, _, _, _, _) => throw expected, _ => true));
        try
        {
            var result = await executor.ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.NotNull(result.StepResult.Error);
            Assert.Contains(expected.Message, result.StepResult.Error.Message);
            Assert.Contains(path, result.StepResult.Error.Message);
            Assert.Empty(state.Writes);
        }
        finally { state.Registry.Retire(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StalledFileCheckKeepsExecutionResponsiveAndNeverSubmitsLate(bool cancel)
    {
        var called = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probeExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var caller = new CancellationTokenSource();
        int invoked = 0;
        var path = Path.Combine(Path.GetTempPath(), "simulated-network-path.vi");
        var context = BridgeTestContext.For(path, cancel ? 0 : 300);
        var step = ExecutionScopedExecutor.Create("LabVIEWCall", () => new LabVIEWCallExecutor(
            (_, _, _, _, _) => { Interlocked.Increment(ref invoked); return new(); },
            candidate =>
            {
                if (!string.Equals(candidate, path, StringComparison.Ordinal)) return true;
                called.TrySetResult(Environment.CurrentManagedThreadId);
                try { release.Wait(TimeSpan.FromSeconds(6)); return true; }
                finally { probeExited.TrySetResult(); }
            }));
        try
        {
            int callingThread = Environment.CurrentManagedThreadId;
            var waiting = step.ExecuteAsync(context, caller.Token);
            Assert.NotEqual(callingThread, await called.Task.WaitAsync(TimeSpan.FromSeconds(3)));
            if (cancel) caller.Cancel();
            Assert.Equal(cancel ? TestStatus.Aborted : TestStatus.Error,
                (await waiting.WaitAsync(TimeSpan.FromSeconds(3))).StepResult.Status);
            Assert.Equal(0, invoked);
            var fresh = BridgeTestContext.For(Path.Combine(Path.GetTempPath(), "available.vi"), 5000);
            try { Assert.Equal(TestStatus.Completed, (await step.ExecuteAsync(fresh)).StepResult.Status); }
            finally { ((BridgeTestContext)(object)fresh).Registry.Retire(); }
        }
        finally
        {
            release.Set();
            await probeExited.Task.WaitAsync(TimeSpan.FromSeconds(3));
            ((BridgeTestContext)(object)context).Registry.Retire();
        }
        Assert.Equal(1, invoked);
    }

    [Fact]
    public async Task RetryOfSameSlowPathSharesOnlyTheFilesystemProbe()
    {
        using var cancel = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int lookups = 0;
        var files = new ViFileProbe(path =>
        {
            if (path == "available.vi") return true;
            Interlocked.Increment(ref lookups);
            started.TrySetResult();
            release.Wait(TimeSpan.FromSeconds(6));
            return true;
        });
        var old = files.ExistsAsync("slow.vi", cancel.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var retry = files.ExistsAsync("slow.vi", default);
        try
        {
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => old);
            Assert.True(await files.ExistsAsync("available.vi", default).WaitAsync(TimeSpan.FromSeconds(3)));
        }
        finally { release.Set(); await retry.WaitAsync(TimeSpan.FromSeconds(3)); }
        Assert.Equal(1, lookups);
    }

    [Fact]
    public async Task SuspendedBindingRetainsBridgeUntilItsRealContinuationExits()
    {
        using var stop = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int nativeCalls = 0;
        try
        {
            var old = ViCallGate.RunStepAsync("异步输入", stop.Token, async token =>
            {
                entered.TrySetResult();
                await finish.Task;
                token.ThrowIfCancellationRequested();
                return Interlocked.Increment(ref nativeCalls);
            });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            stop.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => old);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ViCallGate.RunAsync("立即重跑", 1000, default, _ => Interlocked.Increment(ref nativeCalls)));
        }
        finally { finish.TrySetResult(); await ViCallGate.SharedIdle.WaitAsync(TimeSpan.FromSeconds(3)); }
        Assert.Equal(0, nativeCalls);
        Assert.Equal(1, await ViCallGate.RunAsync("再次运行", 1000, default, _ => Interlocked.Increment(ref nativeCalls)));
    }

    [Fact]
    public void SdkSerializerRetainsTerminalConfigurationAndRejectsMisspelledFields()
    {
        IStepPlugin plugin = new LabVIEWCallStepPlugin();
        var serializer = plugin.CreateSerializer();
        var setting = (LabVIEWCallSetting)serializer.CreateDefault();
        setting.InputParameters = """[[["a"],{"Name":"a","Type":"I32","Value":"12"}]]""";
        setting.OutputParameters = """[[["sum"],{"Name":"sum","Type":"I32","Variable":"Locals.Sum"}]]""";
        setting.TimeoutMs = 1234;
        var restored = (LabVIEWCallSetting)serializer.Deserialize(serializer.Serialize(setting), serializer.SettingVersion);
        Assert.Equal(setting.InputParameters, restored.InputParameters);
        Assert.Equal(setting.OutputParameters, restored.OutputParameters);
        Assert.Equal(1234, restored.TimeoutMs);
        Assert.Throws<InvalidDataException>(() => restored.InputParameters = setting.InputParameters.Replace("Value", "Vaule"));
        Assert.Equal(setting.InputParameters, restored.InputParameters);
    }

    [Fact]
    public void ToolboxExposesOneCallViWithExactlyOneMatchingEditor()
    {
        var executableType = Assert.Single(typeof(LabVIEWCallStepPlugin).Assembly.GetTypes(),
            type => !type.IsAbstract && typeof(IStepPlugin).IsAssignableFrom(type));
        var editorType = Assert.Single(typeof(LabVIEWCallEditorPlugin).Assembly.GetTypes(),
            type => !type.IsAbstract && typeof(IStepEditorPlugin).IsAssignableFrom(type));
        var executable = Assert.IsType<LabVIEWCallStepPlugin>(Activator.CreateInstance(executableType));
        var editor = Assert.IsType<LabVIEWCallEditorPlugin>(Activator.CreateInstance(editorType));
        Assert.Equal("Adapter.LabVIEWCall", executable.StepTypeId);
        Assert.Equal(executable.StepTypeId, editor.StepTypeId);
        Assert.Equal("LabVIEW_Execution_Call VI", executable.DisplayName);
    }

    [Fact]
    public async Task RemovedRegistryEntryPreventsPublishingEvenBeforeDisposeNotification()
    {
        string path = Path.ChangeExtension(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), ".vi");
        File.WriteAllText(path, "Fake bridge fixture");
        var context = BridgeTestContext.For(path, 5000);
        var state = (BridgeTestContext)(object)context;
        var entry = ExecutionScopedExecutor.Create("LabVIEWCall", () => new LabVIEWCallExecutor((_, _, _, _, _) =>
        {
            // The registry can remove the entry before its disposal callback runs.
            state.Registry.RemoveWithoutDisposing();
            return new() { ["Locals.Result"] = 1 };
        }));
        try
        {
            Assert.Equal(TestStatus.Error, (await entry.ExecuteAsync(context)).StepResult.Status);
            Assert.Empty(state.Writes);
        }
        finally { state.Registry.Retire(); File.Delete(path); }
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("timeout")]
    [InlineData("retire")]
    [InlineData("publish")]
    public async Task RealStepRejectsConflictingRerunAndPublishesOnlyCurrentResults(string stop)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.vi");
        File.WriteAllText(path, "Test fixture for path validation. A fake bridge handles every invocation.");
        using var finish = new ManualResetEventSlim();
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inSetter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var entry = ExecutionScopedExecutor.Create("LabVIEWCall", () => new LabVIEWCallExecutor((_, _, _, _, _) =>
        {
            int call = Interlocked.Increment(ref calls);
            if (call == 1) { entered.TrySetResult(); if (stop != "publish") finish.Wait(TimeSpan.FromSeconds(8)); }
            return new() { ["Locals.Result"] = call };
        }));
        var oldContext = BridgeTestContext.For(path, stop == "timeout" ? 250 : 5000);
        var newContext = BridgeTestContext.For(path, 5000);
        if (stop == "publish") ((BridgeTestContext)(object)oldContext).OnWrite = () =>
        {
            inSetter.TrySetResult();
            finish.Wait(TimeSpan.FromSeconds(8));
        };
        try
        {
            var active = entry.ExecuteAsync(oldContext, caller.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            if (stop == "publish") await inSetter.Task.WaitAsync(TimeSpan.FromSeconds(3));
            if (stop is "cancel" or "publish") caller.Cancel();
            if (stop == "retire") ((BridgeTestContext)(object)oldContext).Registry.Retire();
            Assert.Equal(stop is "cancel" or "publish" ? TestStatus.Aborted : TestStatus.Error,
                (await active.WaitAsync(TimeSpan.FromSeconds(3))).StepResult.Status);
            Assert.Equal(TestStatus.Error, (await entry.ExecuteAsync(newContext, default)).StepResult.Status);
            Assert.Equal(1, calls);
            finish.Set();
            await ViCallGate.SharedIdle.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(TestStatus.Completed, (await entry.ExecuteAsync(newContext, default)).StepResult.Status);
            if (stop == "publish") Assert.Equal(1, Assert.Single(((BridgeTestContext)(object)oldContext).Writes));
            else Assert.Empty(((BridgeTestContext)(object)oldContext).Writes);
            Assert.Equal(2, Assert.Single(((BridgeTestContext)(object)newContext).Writes));
        }
        finally
        {
            finish.Set();
            ((BridgeTestContext)(object)oldContext).Registry.Retire();
            ((BridgeTestContext)(object)newContext).Registry.Retire();
            File.Delete(path);
        }
    }
}

public class BridgeTestContext : DispatchProxy
{
    private StepExecutionInfo _step = null!;
    private IResourceRegistry _registry = null!;
    internal BridgeTestRegistry Registry => (BridgeTestRegistry)(object)_registry;
    internal List<object?> Writes { get; } = [];
    internal List<(LogLevel Level, string Message, string? Detail)> Logs { get; } = [];
    internal Action? OnWrite;
    internal static IExecutionContext For(string path, int timeout)
    {
        var context = Create<IExecutionContext, BridgeTestContext>();
        var state = (BridgeTestContext)(object)context;
        state._registry = Create<IResourceRegistry, BridgeTestRegistry>();
        var plugin = (IStepPlugin)new LabVIEWCallStepPlugin();
        var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.Setting = plugin.CreateSerializer().Serialize(new LabVIEWCallSetting { ViFilePath = path, TimeoutMs = timeout });
        state._step = new StepExecutionInfo { Step = step };
        return context;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        switch (method!.Name)
        {
            case "get_CurrentStep": return _step;
            case "get_Resources": return _registry;
            case "SetVariable": OnWrite?.Invoke(); Writes.Add(args![1]); break;
            case "HasVariable": return true;
            case "Log": Logs.Add(((LogLevel)args![0]!, (string)args[1]!, args.Length > 2 ? args[2] as string : null)); break;
        }
        return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
    }
}

public class BridgeTestRegistry : DispatchProxy
{
    private readonly Dictionary<string, object?> _owners = new();
    internal void RemoveWithoutDisposing() { lock (_owners) _owners.Clear(); }
    internal void Retire()
    {
        object?[] items;
        lock (_owners) { items = _owners.Values.ToArray(); _owners.Clear(); }
        foreach (var item in items) (item as IDisposable)?.Dispose();
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        lock (_owners)
        {
            if (method!.Name == "TryAdd") return _owners.TryAdd((string)args![0]!, args[1]);
            if (method.Name == "TryGet") return _owners.TryGetValue((string)args![0]!, out args[1]);
        }
        throw new NotSupportedException(method!.Name);
    }
}
