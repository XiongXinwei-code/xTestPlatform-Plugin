using System.Globalization;

namespace ZlgEtherCat.Contracts;

/// <summary>已完成表达式求值的单步请求；操作由具体 Executor 固定，不接受用户函数名。</summary>
public sealed record ZlgRequest(string ConnectionName, int TimeoutMs, string ResultVariable,
    IReadOnlyDictionary<string, object?> Arguments)
{
    public T Get<T>(string name)
    {
        if (!Arguments.TryGetValue(name, out var value) || value is null)
            throw new ArgumentException($"缺少参数：{name}");
        if (value is T result) return result;
        if (typeof(T).IsEnum && value is IConvertible)
        {
            var converted = (T)Enum.ToObject(typeof(T), value);
            if (!Enum.IsDefined(typeof(T), converted)) throw new ArgumentException($"参数 {name} 的枚举值未定义");
            return converted;
        }
        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    public T GetOrDefault<T>(string name, T fallback = default!) =>
        Arguments.TryGetValue(name, out var value) && value is not null ? Get<T>(name) : fallback;
}
