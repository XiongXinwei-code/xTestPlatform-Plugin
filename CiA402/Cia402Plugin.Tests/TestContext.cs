using System.Reflection;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.Tests;

public class TestContext : DispatchProxy
{
    public Dictionary<string, object?> Variables { get; } = new();
    public Dictionary<string, object> Resources { get; } = new();
    public StepExecutionInfo? Current { get; set; }
    private object? _registry;
    private readonly Dictionary<Type, object> _scopes = new();

    public static (IExecutionContext Context, TestContext State) Create(IStepPlugin plugin, object setting)
    {
        var context = DispatchProxy.Create<IExecutionContext, TestContext>();
        var state = (TestContext)(object)context;
        var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = plugin.CreateSerializer().Serialize(setting);
        state.Current = new StepExecutionInfo { Step = step };
        return (context, state);
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        args ??= [];
        switch (method!.Name)
        {
            case "get_CurrentStep": return Current;
            case "set_CurrentStep": Current = (StepExecutionInfo?)args[0]; return null;
            case "get_IsAbortRequested": return false;
            case "Log": return null;
            case "HasVariable": return Variables.ContainsKey((string)args[0]!);
            case "GetVariable": return Variables.GetValueOrDefault((string)args[0]!);
            case "SetVariable": Variables[(string)args[0]!] = args[1]; return null;
            case "get_Resources":
                if (_registry == null)
                {
                    _registry = Create(method.ReturnType, typeof(RegistryProxy));
                    ((RegistryProxy)_registry).Items = Resources;
                }
                return _registry;
        }
        if (method.Name is "get_Locals" or "get_FileGlobals" or "get_ProjectGlobals" or "get_Parameters" or "get_RunState")
        {
            if (!_scopes.TryGetValue(method.ReturnType, out var scope))
            {
                scope = Create(method.ReturnType, typeof(ScopeProxy));
                ((ScopeProxy)scope).Owner = this;
                _scopes.Add(method.ReturnType, scope);
            }
            return scope;
        }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}

public class RegistryProxy : DispatchProxy
{
    public Dictionary<string, object> Items { get; set; } = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        args ??= [];
        var key = args.Length > 0 ? args[0] as string ?? "" : "";
        switch (method!.Name)
        {
            case "Contains": return Items.ContainsKey(key);
            case "TryGet":
                var exists = Items.TryGetValue(key, out var value) && method.GetGenericArguments()[0].IsInstanceOfType(value);
                args[1] = exists ? value : null; return exists;
            case "TryAdd": return Items.TryAdd(key, args[1]!);
            case "Set": Items[key] = args[1]!; return null;
            case "Remove":
                if (!Items.Remove(key, out var old)) return false;
                if (args.Length < 2 || args[1] is true) (old as IDisposable)?.Dispose();
                return true;
        }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}

public class ScopeProxy : DispatchProxy
{
    public TestContext Owner { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "GetVariableDefinition" && Owner.Variables.Keys.Any(k => k.Split('.')[1] == (string)args![0]!))
            return Activator.CreateInstance(method.ReturnType);
        if (method.Name == "Contains") return Owner.Variables.Keys.Any(k => k.Split('.')[1] == (string)args![0]!);
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
