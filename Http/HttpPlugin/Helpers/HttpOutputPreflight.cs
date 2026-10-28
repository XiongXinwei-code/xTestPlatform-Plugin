using xTestPlatform.Core.Engine;

namespace Http.Helpers;

/// <summary>输出路径是声明的变量位置，不是表达式或隐式创建变量的指令。</summary>
internal static class HttpOutputPreflight
{
    internal static bool IsScopedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var parts = path.Split('.', 2);
        return parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]) && parts[0].Length > 0 &&
            (char.IsLetter(parts[0][0]) || parts[0][0] == '_') && parts[0].All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    internal static void Validate(IExecutionContext context, string path, Type expected, Action ensureActive, bool optional = true)
    {
        ensureActive();
        if (optional && string.IsNullOrWhiteSpace(path)) return;
        if (!IsScopedPath(path)) throw new ArgumentException($"HTTP 输出必须为带作用域的完整变量路径：{path}");
        bool declared = context.HasVariable(path);
        ensureActive();
        if (!declared) throw new ArgumentException($"HTTP 输出变量 {path} 未声明或路径不存在");
        object? value = context.GetVariable(path);
        ensureActive();
        if (value != null && value.GetType() != expected)
            throw new ArgumentException($"HTTP 输出变量 {path} 类型不匹配，需要 {expected.Name}，实际 {value.GetType().Name}");
    }
}
