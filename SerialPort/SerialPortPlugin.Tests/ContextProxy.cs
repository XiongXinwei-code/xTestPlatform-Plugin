using System.Collections.Concurrent;
using System.Reflection;
using xTestPlatform.Core.Engine;

namespace AuditTestSupport;
public class ContextProxy : DispatchProxy
{
    public object? Store;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_Resources" ? Store : null;
    public static IExecutionContext Create()
    {
        var context = Create<IExecutionContext, ContextProxy>();
        var property = typeof(IExecutionContext).GetProperty("Resources") ?? typeof(IExecutionContext).GetInterfaces().SelectMany(t => t.GetProperties()).First(p => p.Name == "Resources");
        ((ContextProxy)(object)context).Store = Create(property.PropertyType, typeof(StoreProxy));
        return context;
    }
}
public class StoreProxy : DispatchProxy
{
    private readonly ConcurrentDictionary<string, object> _values = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method == null || args == null) return null;
        string key = args[0]?.ToString() ?? "";
        if (method.Name == "TryAdd") return _values.TryAdd(key, args[1]!);
        if (method.Name == "TryGet")
        {
            bool found = _values.TryGetValue(key, out var value) && method.GetGenericArguments()[0].IsInstanceOfType(value);
            args[1] = found ? value : null; return found;
        }
        if (method.Name == "Remove")
        {
            bool found = _values.TryRemove(key, out var value); if (value is IDisposable resource) resource.Dispose();
            return method.ReturnType == typeof(bool) ? found : null;
        }
        throw new NotSupportedException(method.Name);
    }
}
