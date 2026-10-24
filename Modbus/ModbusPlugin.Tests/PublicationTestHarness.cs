using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.ContractTests;

// Tests hold the real executor continuation after its owned I/O has exited.
// No driver or device is opened.
internal sealed class HeldContinuationContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posts = new();
    public override void Post(SendOrPostCallback callback, object? state) => _posts.Enqueue((callback, state));
    public Task<ExecutionResult> Start(Func<Task<ExecutionResult>> execute)
    {
        var previous = Current; SetSynchronizationContext(this);
        try { return execute(); } finally { SetSynchronizationContext(previous); }
    }
    public bool RunOne()
    {
        if (!_posts.TryDequeue(out var post)) return false;
        var previous = Current; SetSynchronizationContext(this);
        try { post.Callback(post.State); } finally { SetSynchronizationContext(previous); }
        return true;
    }
    public async Task PumpUntilAsync(Task target, Task<ExecutionResult> execution, CancellationToken guard)
    {
        while (!target.IsCompleted)
        {
            guard.ThrowIfCancellationRequested();
            if (execution.IsCompleted) throw new InvalidOperationException("Executor exited before the controlled I/O: " + (await execution).StepResult.Error?.Detail);
            if (!RunOne()) await Task.Delay(2, guard);
        }
    }
    public async Task WaitForContinuationAsync(CancellationToken guard)
    { while (_posts.IsEmpty) await Task.Delay(2, guard); }
    public async Task DrainUntilAsync(Task execution, CancellationToken guard)
    { while (!execution.IsCompleted) { guard.ThrowIfCancellationRequested(); if (!RunOne()) await Task.Delay(2, guard); } }
}

public class PublicationContextProxy : DispatchProxy
{
    public readonly ConcurrentQueue<(string Path, object? Value)> Published = new();
    internal Action? OnLog;
    public PublicationStoreProxy Store = null!;
    internal IResourceRegistry? ResourcesOverride;
    private object _registry = null!;
    private StepExecutionInfo _current = null!;
    [ModuleInitializer]
    internal static void LoadExpressionImports() => Assembly.Load("System.Text.Json");
    public static (IExecutionContext Context, PublicationContextProxy State) Create(IStepPlugin plugin, object setting, PublicationStoreProxy? shared = null)
    {
        var context = DispatchProxy.Create<IExecutionContext, PublicationContextProxy>();
        var state = (PublicationContextProxy)(object)context;
        var step = new Step(); var serializer = plugin.CreateSerializer();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting);
        state._current = new StepExecutionInfo { Step = step };
        var type = typeof(IExecutionContext).GetProperty("Resources")?.PropertyType ??
            typeof(IExecutionContext).GetInterfaces().SelectMany(i => i.GetProperties()).First(p => p.Name == "Resources").PropertyType;
        state._registry = shared ?? DispatchProxy.Create(type, typeof(PublicationStoreProxy));
        state.Store = (PublicationStoreProxy)state._registry;
        return (context, state);
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "get_CurrentStep") return _current;
        if (method.Name == "get_Resources") return ResourcesOverride ?? _registry;
        if (method.Name == "get_IsAbortRequested") return false;
        if (method.Name == "Log") { OnLog?.Invoke(); return null; }
        if (method.Name == "HasVariable") return args?[0] is "Locals.rx";
        if (method.Name == "SetVariable") { Published.Enqueue(((string)args![0]!, args[1])); return null; }
        return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
    }
}

public class PublicationStoreProxy : DispatchProxy
{
    public readonly ConcurrentDictionary<string, object> Values = new();
    internal Action<string, Type>? OnTryGet;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        args ??= []; string key = args.Length > 0 ? args[0]?.ToString() ?? "" : "";
        switch (method!.Name)
        {
            case "TryGet":
                OnTryGet?.Invoke(key, method.GetGenericArguments()[0]);
                bool found = Values.TryGetValue(key, out var value) && method.GetGenericArguments()[0].IsInstanceOfType(value);
                args[1] = found ? value : null; return found;
            case "TryAdd": return Values.TryAdd(key, args[1]!);
            case "Set": Values[key] = args[1]!; return null;
            case "Remove":
                bool removed = Values.TryRemove(key, out var old);
                if (removed && (args.Length < 2 || args[1] is true)) (old as IDisposable)?.Dispose();
                return removed;
            case "Contains": return Values.ContainsKey(key);
        }
        throw new NotSupportedException(method.Name);
    }
}
