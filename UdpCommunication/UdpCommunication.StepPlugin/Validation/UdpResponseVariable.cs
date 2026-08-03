using xTestPlatform.Core.Engine;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.StepPlugin.Validation;

public static class UdpResponseVariable
{
    private const string StepScope = "Step";

    public static string? NormalizePath(string? configuredPath)
    {
        var path = configuredPath?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var separator = path.IndexOf('.');
        if (separator < 0)
        {
            return $"{StepScope}.{path}";
        }

        var scope = path[..separator].Trim();
        var name = path[(separator + 1)..].Trim();
        return $"{scope}.{name}";
    }

    public static string? Validate(string? configuredPath, IExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = NormalizePath(configuredPath);
        if (path is null)
        {
            return null;
        }

        if (!TrySplitPath(path, out var scopeName, out var variableName))
        {
            return "回复变量必须使用“作用域.变量名”格式";
        }

        if (scopeName == StepScope)
        {
            return null;
        }

        var scope = GetScope(scopeName, context);
        if (scope is null)
        {
            return $"回复变量作用域“{scopeName}”无效";
        }

        if (!context.HasVariable(path))
        {
            return $"回复变量“{path}”未定义";
        }

        var definition = scope.GetVariableDefinition(variableName);
        if (definition is null)
        {
            return $"无法读取回复变量“{path}”的定义";
        }

        if (definition.AccessMode == VariableAccessMode.ReadOnly)
        {
            return $"回复变量“{path}”是只读变量";
        }

        if (definition.DataType is not (
            VariableDataType.String or
            VariableDataType.Dynamic or
            VariableDataType.Object))
        {
            return $"回复变量“{path}”类型必须为 String、Dynamic 或 Object";
        }

        return null;
    }

    private static IVariableScope? GetScope(string scopeName, IExecutionContext context) =>
        scopeName switch
        {
            "ProjectGlobals" => context.ProjectGlobals,
            "FileGlobals" => context.FileGlobals,
            "Locals" => context.Locals,
            "Parameters" => context.Parameters,
            "RunState" => context.RunState,
            _ => null
        };

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
        return !string.IsNullOrWhiteSpace(name);
    }
}
