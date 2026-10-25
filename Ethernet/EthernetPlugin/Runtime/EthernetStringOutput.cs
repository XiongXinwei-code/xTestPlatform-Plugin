using xTestPlatform.Core.Engine;

namespace Ethernet.Runtime;

/// <summary>One declaration/path/type contract for runtime preflight and the actual editor.</summary>
internal static class EthernetStringOutput
{
    internal static bool IsCompletePath(string path)
    {
        var parts = path.Split('.', 2);
        return parts.Length == 2 && parts[0].Length > 0 && !string.IsNullOrWhiteSpace(parts[1]) &&
            (char.IsLetter(parts[0][0]) || parts[0][0] == '_') &&
            parts[0].All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    internal static (string Code, string Message)? GetError(IExecutionContext context, string path, Action? ensureActive = null)
    {
        ensureActive?.Invoke();
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (!IsCompletePath(path))
            return ("ETH_VAR03", $"结果变量 {path} 必须使用 Scope.Name 完整变量路径，作用域由当前上下文解析，不能使用裸名称、非法作用域标识或空成员");
        bool exists = context.HasVariable(path);
        ensureActive?.Invoke();
        if (!exists) return ("ETH_VAR01", $"结果变量 {path} 不存在，请先声明字符串变量或留空不写入");
        var value = context.GetVariable(path);
        ensureActive?.Invoke();
        return value is not null && value is not string
            ? ("ETH_VAR02", $"结果变量 {path} 类型不匹配，期望 string，实际 {value.GetType().Name}")
            : null;
    }

    internal static void Validate(IExecutionContext context, string path, Action ensureActive)
    {
        if (GetError(context, path, ensureActive) is { } error) throw new InvalidOperationException(error.Message);
        ensureActive();
    }
}
