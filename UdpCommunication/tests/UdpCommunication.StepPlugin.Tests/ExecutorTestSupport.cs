using System.Reflection;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.StepPlugin.Tests;

internal sealed class TestStepSettingSerializer(object defaultSetting, object deserializedSetting) : IStepSettingSerializer
{
    public bool CreateDefaultCalled { get; private set; }
    public int SettingVersion => 1;
    public byte[] Serialize(object setting) => [1];
    public object Deserialize(byte[] data, int dataVersion) => deserializedSetting;
    public object CreateDefault()
    {
        CreateDefaultCalled = true;
        return defaultSetting;
    }
}

internal static class TestExecutionContextFactory
{
    public static IExecutionContext Create(Step step)
    {
        return CreateWithProxy(step).Context;
    }

    public static (IExecutionContext Context, TestExecutionContextProxy Proxy) CreateWithProxy(Step step)
    {
        var context = DispatchProxy.Create<IExecutionContext, TestExecutionContextProxy>();
        var proxy = (TestExecutionContextProxy)(object)context;
        proxy.CurrentStep = CreateStepExecutionInfo(step);
        return (context, proxy);
    }

    private static object CreateStepExecutionInfo(Step step)
    {
        var type = typeof(IExecutionContext).GetProperty(nameof(IExecutionContext.CurrentStep))!.PropertyType;
        var currentStep = Activator.CreateInstance(type) ?? throw new InvalidOperationException("无法创建当前步骤上下文");
        type.GetProperty("Step")!.SetValue(currentStep, step);
        return currentStep;
    }
}

public class TestExecutionContextProxy : DispatchProxy
{
    public object? CurrentStep { get; set; }
    public List<string> Logs { get; } = [];
    public Dictionary<string, object?> WrittenVariables { get; } = new(StringComparer.Ordinal);
    public TestVariableScope ProjectGlobals { get; } = new();
    public TestVariableScope FileGlobals { get; } = new();
    public TestVariableScope Locals { get; } = new();
    public TestVariableScope Parameters { get; } = new();
    public TestVariableScope RunState { get; } = new();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        return targetMethod?.Name switch
        {
            "get_CurrentStep" => CurrentStep,
            "get_LogAction" => new Action<string>(Logs.Add),
            "get_ProjectGlobals" => ProjectGlobals,
            "get_FileGlobals" => FileGlobals,
            "get_Locals" => Locals,
            "get_Parameters" => Parameters,
            "get_RunState" => RunState,
            "HasVariable" => HasVariable((string)args![0]!),
            "SetVariable" => SetVariable((string)args![0]!, args[1]),
            _ => throw new NotSupportedException($"测试上下文未实现 {targetMethod?.Name}")
        };
    }

    private bool HasVariable(string path)
    {
        if (!TrySplitPath(path, out var scope, out var name))
        {
            return false;
        }

        return scope switch
        {
            "Step" => WrittenVariables.ContainsKey(path),
            "ProjectGlobals" => ProjectGlobals.Contains(name),
            "FileGlobals" => FileGlobals.Contains(name),
            "Locals" => Locals.Contains(name),
            "Parameters" => Parameters.Contains(name),
            "RunState" => RunState.Contains(name),
            _ => false
        };
    }

    private object? SetVariable(string path, object? value)
    {
        WrittenVariables[path] = value;
        return null;
    }

    private static bool TrySplitPath(string path, out string scope, out string name)
    {
        var separator = path.IndexOf('.');
        if (separator <= 0 || separator == path.Length - 1)
        {
            scope = string.Empty;
            name = string.Empty;
            return false;
        }

        scope = path[..separator];
        name = path[(separator + 1)..];
        return true;
    }
}

public sealed class TestVariableScope : IVariableScope
{
    private readonly Dictionary<string, Variables> _definitions = new(StringComparer.Ordinal);

    public void Add(Variables definition) => _definitions[definition.Name] = definition;
    public IEnumerable<string> GetAllNames() => _definitions.Keys;
    public bool Contains(string name) => _definitions.ContainsKey(name);
    public void Clear() => _definitions.Clear();
    public Variables? GetVariableDefinition(string name) =>
        _definitions.GetValueOrDefault(name.Split('.')[0]);
    public IEnumerable<Variables> GetAllDefinitions() => _definitions.Values;
}
