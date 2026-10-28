using System.Reflection;
using CAN.Adapters;
using CAN.CANopen.Cyclic;
using CAN.Executors;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class LifecycleGuardTests
{
    private static Task<IDisposable> Enter(object registry, string name, CancellationToken ct = default)
        => (Task<IDisposable>)typeof(CanConnectionResource).Assembly.GetType("CAN.Runtime.CanRegistryGuard")!
            .GetMethod("EnterAsync")!.Invoke(null, [registry, name, ct, 5000])!;

    [Fact]
    public async Task ConnectionNamesDifferingOnlyInCaseShareLifecycleGate()
    {
        var registry = new object();
        using var first = await Enter(registry, "CAN1");
        using var stop = new CancellationTokenSource(30);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Enter(registry, "can1", stop.Token));
    }

    [Fact]
    public async Task QueuedOldCloseCannotCloseReplacementConnection()
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>();
        var state = (CloseContext)(object)context;
        var old = new FakeAdapter(); var replacement = new FakeAdapter();
        state.Adapter = old;
        using (await Enter(context.Resources, "CAN1"))
        {
            var closing = new CanCloseExecutor().ExecuteAsync(context);
            await state.LookedUp.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(closing.IsCompleted);
            // 模拟持有生命周期锁的 Open 已将旧代次替换为新代次。
            state.Adapter = replacement;
            state.Closing = closing;
        }
        var result = await state.Closing!.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        Assert.Same(replacement, state.Adapter);
        Assert.Equal(0, replacement.CloseCount);
        Assert.Equal(0, state.RemoveCount);
    }

    [Fact]
    public async Task CycleStopReportsReceiveFaultEvenWhenNoCycleWriteFailed()
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>();
        var state = (CloseContext)(object)context;
        var plugin = new CycleStopPlugin();
        state.Plugin = plugin;
        state.Setting = new CyclicSetting { ConnectionName = "\"CAN1\"", ServiceName = "\"one\"" };
        var connection = new CanConnectionResource(new FakeAdapter { ReadFailure = new InvalidOperationException("receive failed") });
        state.Adapter = connection;
        var service = connection.StartService("one", [new("A", 0x181, 2, 100)]);
        using var deadline = new CancellationTokenSource(3000);
        while (service.Snapshot().Receive.BackgroundError == null) await Task.Delay(5, deadline.Token);
        Assert.Null(service.Snapshot().Cycle.Error);
        var result = await new CyclicExecutor(plugin).ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.CloseAsync());
        Assert.True(connection.IsClosed);
    }
}

public class CloseContext : DispatchProxy
{
    public Dictionary<string, object?> Variables { get; } = new();
    public int ResourceWrites { get; set; }
    public ICanAdapter? Adapter { get; set; }
    public int RemoveCount { get; set; }
    public Task<ExecutionResult>? Closing { get; set; }
    public IStepPlugin Plugin { get; set; } = new CanClosePlugin();
    public object Setting { get; set; } = new CanCloseSetting { ConnectionName = "\"CAN1\"" };
    public byte[]? SettingBytes { get; set; }
    public Action<string>? OnLog { get; set; }
    public List<string> Logs { get; } = [];
    public TaskCompletionSource LookedUp { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private object? _registry;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        switch (method!.Name)
        {
            case "Log":
                var message = string.Join(" ", args ?? []); Logs.Add(message); OnLog?.Invoke(message); return null;
            case "SetVariable": Variables[(string)args![0]!] = args[1]; return null;
            case "get_CurrentStep":
                var plugin = Plugin; var step = new Step();
                step.StepSetting.StepType = plugin.StepTypeId;
                step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion;
                step.StepSetting.Setting = SettingBytes ?? plugin.CreateSerializer().Serialize(Setting);
                return new StepExecutionInfo { Step = step };
            case "get_Resources":
                if (_registry == null)
                {
                    _registry = Create(method.ReturnType, typeof(CloseRegistry));
                    ((CloseRegistry)_registry).Owner = this;
                }
                return _registry;
        }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
public class CloseRegistry : DispatchProxy
{
    private readonly Dictionary<string, object> _other = new();
    public CloseContext Owner { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "TryGet")
        {
            object? value = Owner.Adapter != null && method.GetGenericArguments()[0].IsInstanceOfType(Owner.Adapter) ? Owner.Adapter : _other.GetValueOrDefault((string)args![0]!);
            bool found = value != null && method.GetGenericArguments()[0].IsInstanceOfType(value);
            args![1] = found ? value : null;
            if (method.GetGenericArguments()[0] == typeof(ICanAdapter)) Owner.LookedUp.TrySetResult();
            return found;
        }
        if (method.Name == "Remove")
        {
            if (_other.Remove((string)args![0]!, out var old)) { (old as IDisposable)?.Dispose(); return true; }
            Owner.RemoveCount++; var adapter = Owner.Adapter; Owner.Adapter = null; adapter?.Dispose(); return adapter != null;
        }
        if (method.Name == "Set") { _other[(string)args![0]!] = args[1]!; Owner.ResourceWrites++; return null; }
        if (method.Name == "TryAdd")
        {
            if (args![1] is not ICanAdapter) return _other.TryAdd((string)args[0]!, args[1]!);
            if (Owner.Adapter != null) return false;
            Owner.Adapter = (ICanAdapter)args![1]!; Owner.ResourceWrites++; return true;
        }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
