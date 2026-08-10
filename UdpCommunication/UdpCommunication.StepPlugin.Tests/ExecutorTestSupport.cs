using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UdpCommunication;
using UdpCommunication.Executors;
using UdpCommunication.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace UdpCommunication.Tests;

/// <summary>
/// Test context: uses real StepExecutionInfo but intercepts IExecutionContext calls via DispatchProxy.
/// </summary>
internal static class TestExecutionContextFactory
{
    public static IExecutionContext Create(Step step) =>
        CreateWithProxy(new UdpSendSetting(), step).Context;

    public static IExecutionContext Create(UdpSendSetting setting, Step step) =>
        CreateWithProxy(setting, step).Context;

    public static (IExecutionContext Context, TestExecutionContextProxy Proxy) CreateWithProxy(Step step) =>
        CreateWithProxy(new UdpSendSetting(), step);

    public static (IExecutionContext Context, TestExecutionContextProxy Proxy) CreateWithProxy(object setting, Step step) =>
        CreateWithProxy(setting, step, null);

    public static (IExecutionContext Context, TestExecutionContextProxy Proxy) CreateWithProxy(
        object setting, Step step, Dictionary<string, object>? sharedRuntimeData)
    {
        // Test factory serializes setting to StepSetting.Setting by default (simulating runtime loading).
        // For tests that need to test "empty config uses defaults" path, set Setting to null before calling.
        if (step.StepSetting.Setting is null || step.StepSetting.Setting.Length == 0)
        {
            var serializer = setting switch
            {
                UdpSendAndReceiveSetting => new UdpSendAndReceivePlugin().CreateSerializer(),
                UdpSendSetting => new UdpSendPlugin().CreateSerializer(),
                UdpReceiveSetting => new UdpReceivePlugin().CreateSerializer(),
                UdpOpenSetting => new UdpOpenPlugin().CreateSerializer(),
                UdpCloseSetting => new UdpClosePlugin().CreateSerializer(),
                _ => new UdpSendPlugin().CreateSerializer()
            };
            var data = serializer.Serialize(setting);
            step.StepSetting.Setting = data;
            step.StepSetting.SettingVersion = serializer.SettingVersion;
        }

        var proxy = new TestExecutionContextProxy(setting, sharedRuntimeData);
        var context = proxy.CreateContext(step);
        return (context, proxy);
    }

    /// <summary>
    /// Use MockExpressionEvaluator within a using block to avoid Roslyn compilation hanging in test process.
    /// </summary>
    public static IDisposable Use(IExpressionEvaluator evaluator) => TestableEvaluator.Use(evaluator);
}

public class TestExecutionContextProxy
{
    public List<string> Logs { get; } = [];
    public Dictionary<string, object?> WrittenVariables { get; } = new(StringComparer.Ordinal);
    /// <summary>共享 RuntimeData 字典，由 StepExecutionInfo 引用以模拟运行框架。
    /// 测试多个步骤间的 transport 共享时，应让所有 Proxy 复用同一个 RuntimeData 实例。</summary>
    public Dictionary<string, object> RuntimeData { get; }

    public TestVariableScope ProjectGlobals { get; } = new();
    public TestVariableScope FileGlobals { get; } = new();
    public TestVariableScope Locals { get; } = new();
    public TestVariableScope Parameters { get; } = new();
    public TestVariableScope RunState { get; } = new();

    private readonly object _setting;

    public TestExecutionContextProxy(object setting, Dictionary<string, object>? sharedRuntimeData = null)
    {
        _setting = setting;
        RuntimeData = sharedRuntimeData ?? new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public IExecutionContext CreateContext(Step step)
    {
        var context = DispatchProxy.Create<IExecutionContext, TestExecutionContextDispatchProxy>();
        var dispatch = (TestExecutionContextDispatchProxy)(object)context;
        dispatch.Initialize(this, step);
        return context;
    }

    public bool HasVariable(string path)
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

    public object? SetVariable(string path, object? value)
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

public class TestExecutionContextDispatchProxy : DispatchProxy
{
    private TestExecutionContextProxy? _owner;
    private Step? _step;
    private object? _currentStep;

    public void Initialize(TestExecutionContextProxy owner, Step step)
    {
        _owner = owner;
        _step = step;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (_owner is null) throw new InvalidOperationException("Test context not initialized");
        return targetMethod?.Name switch
        {
            "get_CurrentStep" => _currentStep ??= CreateStepExecutionInfo(_owner, _step!),
            "get_LogAction" => new Action<string>(_owner.Logs.Add),
            "get_ProjectGlobals" => _owner.ProjectGlobals,
            "get_FileGlobals" => _owner.FileGlobals,
            "get_Locals" => _owner.Locals,
            "get_Parameters" => _owner.Parameters,
            "get_RunState" => _owner.RunState,
            "HasVariable" => _owner.HasVariable((string)args![0]!),
            "SetVariable" => _owner.SetVariable((string)args![0]!, args[1]),
            _ => null
        };
    }

    private static object CreateStepExecutionInfo(TestExecutionContextProxy owner, Step step)
    {
        var type = typeof(IExecutionContext).GetProperty(nameof(IExecutionContext.CurrentStep))!.PropertyType;
        var info = Activator.CreateInstance(type) ?? throw new InvalidOperationException("Cannot create step execution info");
        type.GetProperty("Step")!.SetValue(info, step);
        // 共享 RuntimeData 字典，让测试代码可以读到 executors 写入的 transport。
        type.GetProperty("RuntimeData")!.SetValue(info, owner.RuntimeData);
        return info;
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

/// <summary>
/// Mock IExpressionEvaluator for test environment.
/// - Quoted strings (e.g. "\"127.0.0.1\"") returns the unquoted content.
/// - Simple variable references (e.g. "Port") resolved via IExecutionContext.
/// - Other expressions returned as-is.
/// </summary>
internal sealed class MockExpressionEvaluator : IExpressionEvaluator
{
    private static readonly Regex QuotedLiteralRegex = new(@"^""(.*)""$", RegexOptions.Compiled);

    public object? Evaluate(string expression, IExecutionContext context) =>
        ParseExpression(expression, context);

    public T? Evaluate<T>(string expression, IExecutionContext context)
    {
        var result = ParseExpression(expression, context);
        if (result is T typed) return typed;
        return (T?)Convert.ChangeType(result, typeof(T));
    }

    public Task<object?> EvaluateAsync(string expression, IExecutionContext context) =>
        Task.FromResult<object?>(ParseExpression(expression, context));

    public Task<T?> EvaluateAsync<T>(string expression, IExecutionContext context)
    {
        var result = ParseExpression(expression, context);
        if (result is T typed) return Task.FromResult<T?>(typed);
        return Task.FromResult((T?)Convert.ChangeType(result, typeof(T)));
    }

    public Task<ExpressionEvalResult> TryEvaluateAsync(string expression, IExecutionContext context)
    {
        try
        {
            var result = ParseExpression(expression, context);
            return Task.FromResult(ExpressionEvalResult.Ok(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ExpressionEvalResult.Fail(ex, EvalFailureKind.Other));
        }
    }

    public bool ValidateExpression(string expression, IExecutionContext context, out string errorMessage)
    {
        errorMessage = string.Empty;
        return true;
    }

    public IEnumerable<string> GetReferencedVariables(string expression) =>
        [];

    public void ClearCache() { }

    private static string ParseExpression(string expression, IExecutionContext context)
    {
        // Remove escaped quotes from quoted string literals.
        var match = QuotedLiteralRegex.Match(expression.Trim());
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // Simple variable references (e.g. "Port", "Step.Address").
        if (context != null && expression.Length > 0 && char.IsLetter(expression[0]))
        {
            if (context.HasVariable(expression))
            {
                return expression; // Variable exists, return reference path.
            }
        }

        // Pure literals (numbers, identifiers).
        return expression;
    }
}
