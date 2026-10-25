using System.Collections.Concurrent;
using System.Reflection;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace PluginRegression.Tests;

public class TestContext : DispatchProxy
{
    public ConcurrentDictionary<string, object?> Variables { get; } = new();
    // Declaration and publication are separate so lifecycle probes can assert zero writes.
    public Dictionary<string, object?> DeclaredVariables { get; } = new();
    public ConcurrentDictionary<string, object> Items { get; } = new();
    public List<string> Logs { get; } = new();
    public StepExecutionInfo Current { get; set; } = null!;
    private object? _registry;

    public static (IExecutionContext Context, TestContext State) Create(IStepPlugin plugin, object setting)
    {
        var ctx = Create<IExecutionContext, TestContext>();
        var state = (TestContext)(object)ctx;
        state.SetStep(plugin, setting);
        return (ctx, state);
    }

    public void SetStep(IStepPlugin plugin, object setting)
    {
        var serializer = plugin.CreateSerializer();
        var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting);
        Current = new StepExecutionInfo { Step = step };
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        args ??= [];
        switch (method!.Name)
        {
            case "get_CurrentStep": return Current;
            case "get_IsAbortRequested": return false;
            case "HasVariable": return Variables.ContainsKey((string)args[0]!) || DeclaredVariables.ContainsKey((string)args[0]!);
            case "GetVariable": return Variables.TryGetValue((string)args[0]!, out var known) ? known : DeclaredVariables.GetValueOrDefault((string)args[0]!);
            case "SetVariable": Variables[(string)args[0]!] = args[1]; return null;
            case "Log": lock (Logs) Logs.Add(string.Join(" ", args.Select(a => a?.ToString()))); return null;
            case "get_Resources":
                if (_registry == null)
                {
                    _registry = Create(method.ReturnType, typeof(ResourceProxy));
                    ((ResourceProxy)_registry).Items = Items;
                }
                return _registry;
        }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}

public class ResourceProxy : DispatchProxy
{
    public ConcurrentDictionary<string, object> Items { get; set; } = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        args ??= [];
        var key = args.Length > 0 ? args[0] as string ?? "" : "";
        switch (method!.Name)
        {
            case "Contains": return Items.ContainsKey(key);
            case "TryGet":
                bool exists = Items.TryGetValue(key, out var value) && method.GetGenericArguments()[0].IsInstanceOfType(value);
                args[1] = exists ? value : null;
                return exists;
            case "TryAdd": return Items.TryAdd(key, args[1]!);
            case "Set":
                if (Items.TryGetValue(key, out var previous)) (previous as IDisposable)?.Dispose();
                Items[key] = args[1]!;
                return null;
            case "Remove":
                if (!Items.TryRemove(key, out var old)) return false;
                if (args.Length < 2 || args[1] is true) (old as IDisposable)?.Dispose();
                return true;
        }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
