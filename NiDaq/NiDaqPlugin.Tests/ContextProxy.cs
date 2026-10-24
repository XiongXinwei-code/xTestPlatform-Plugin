using System.Collections.Concurrent;
using System.Reflection;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace AuditTestSupport;
public class ContextProxy : DispatchProxy
{
    public object? Store;
    public StepExecutionInfo? CurrentStep;
    public Action? OnLog;
    public Action? BeforeCurrentStep;
    public bool AbortRequested;
    public readonly ConcurrentQueue<(string Path, object? Value)> Published = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name == "get_Resources") return Store;
        if (method?.Name == "get_CurrentStep") { BeforeCurrentStep?.Invoke(); return CurrentStep; }
        if (method?.Name == "set_CurrentStep") CurrentStep = (StepExecutionInfo?)args![0];
        if (method?.Name == "get_IsAbortRequested") return AbortRequested;
        if (method?.Name == "Log") { OnLog?.Invoke(); return null; }
        if (method?.Name == "SetVariable") { Published.Enqueue(((string)args![0]!, args[1])); return null; }
        return null;
    }
    public static IExecutionContext Create()
    {
        var context = Create<IExecutionContext, ContextProxy>();
        var property = typeof(IExecutionContext).GetProperty("Resources") ?? typeof(IExecutionContext).GetInterfaces().SelectMany(t => t.GetProperties()).First(p => p.Name == "Resources");
        ((ContextProxy)(object)context).Store = Create(property.PropertyType, typeof(StoreProxy));
        return context;
    }
    public static (IExecutionContext Context, ContextProxy State) Create(IStepPlugin plugin, object setting)
    {
        var context = Create(); var state = (ContextProxy)(object)context; state.SetStep(plugin, setting); return (context, state);
    }
    public void SetStep(IStepPlugin plugin, object setting)
    {
        var step = new Step(); var serializer = plugin.CreateSerializer(); step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion; step.StepSetting.Setting = serializer.Serialize(setting); CurrentStep = new() { Step = step };
    }
}
public class StoreProxy : DispatchProxy
{
    public readonly ConcurrentDictionary<string, object> Values = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method == null || args == null) return null;
        string key = args[0]?.ToString() ?? "";
        if (method.Name == "TryAdd") return Values.TryAdd(key, args[1]!);
        if (method.Name == "TryGet")
        {
            bool found = Values.TryGetValue(key, out var value) && method.GetGenericArguments()[0].IsInstanceOfType(value);
            args[1] = found ? value : null; return found;
        }
        if (method.Name == "Remove")
        {
            bool found = Values.TryRemove(key, out var value); if (value is IDisposable resource && (args.Length < 2 || args[1] is true)) resource.Dispose();
            return method.ReturnType == typeof(bool) ? found : null;
        }
        throw new NotSupportedException(method.Name);
    }
}
