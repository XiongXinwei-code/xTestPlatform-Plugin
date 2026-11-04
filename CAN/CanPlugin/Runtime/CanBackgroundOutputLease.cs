using xTestPlatform.Core.Engine;

namespace CAN.Runtime;

/// <summary>监听输出所有权。停止等待超时不释放路径，真实 setter 退出后才能再次申请。</summary>
internal sealed class CanBackgroundOutputLease : IDisposable
{
    private static readonly object RegistryGate = new();
    // 保守地跨执行代次保留路径；SDK 没有按代次原子提交变量的接口。
    private static readonly Dictionary<string, CanBackgroundOutputLease> Owners = new(StringComparer.OrdinalIgnoreCase);
    private readonly IExecutionContext _context;
    private readonly CanExecutionGuard _guard;
    private readonly Dictionary<string, IVariableScope?> _scopes;
    private readonly HashSet<string> _paths;
    private readonly object _gate = new();
    private bool _disposed;
    private int _publishing;

    private CanBackgroundOutputLease(IExecutionContext context, CanExecutionGuard guard, string[] paths)
    {
        _context = context; _guard = guard; _paths = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _scopes = paths.Select(ScopeName).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(name => name, Scope, StringComparer.OrdinalIgnoreCase);
    }

    internal static CanBackgroundOutputLease Acquire(IExecutionContext context, CanExecutionGuard guard, IEnumerable<string> targets)
    {
        guard.Check();
        var paths = targets.ToArray();
        if (paths.Length == 0 || paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length ||
            paths.Any(path => string.IsNullOrWhiteSpace(path) || path != path.Trim() || path.IndexOf('.') <= 0 || !context.HasVariable(path)))
            throw new ArgumentException("监听输出必须为已声明且不重复的完整变量路径，至少配置一个输出");
        var lease = new CanBackgroundOutputLease(context, guard, paths);
        lock (RegistryGate)
        {
            var conflict = paths.FirstOrDefault(Owners.ContainsKey);
            if (conflict != null) throw new InvalidOperationException($"变量 {conflict} 已被监听任务占用或旧发布尚未退出；请先停止原任务并确认退出");
            foreach (string path in paths) Owners.Add(path, lease);
        }
        return lease;
    }

    private static string ScopeName(string path) => path[..path.IndexOf('.')];
    private IVariableScope? Scope(string name) => name.ToLowerInvariant() switch
    {
        "locals" => _context.Locals, "fileglobals" => _context.FileGlobals,
        "projectglobals" => _context.ProjectGlobals, "parameters" => _context.Parameters,
        "runstate" => _context.RunState,
        _ => throw new ArgumentException($"监听输出不支持作用域 {name}；请使用已声明的 Locals/FileGlobals/ProjectGlobals/Parameters/RunState 变量")
    };

    internal void Check(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _guard.CheckLifetime();
        lock (_gate) if (_disposed) throw new InvalidOperationException("监听输出已停止，禁止发布旧结果");
        foreach (var (name, expected) in _scopes)
            if (!ReferenceEquals(Scope(name), expected)) throw new InvalidOperationException("监听变量作用域已更换，禁止写入新运行");
    }

    internal void Publish(IReadOnlyDictionary<string, object?> values, CancellationToken ct)
    {
        Check(ct);
        if (values.Keys.Any(path => !_paths.Contains(path))) throw new InvalidOperationException("监听未申请该变量的输出所有权");
        lock (_gate)
        {
            if (_disposed) throw new InvalidOperationException("监听已停止");
            _publishing++;
        }
        try
        {
            foreach (var (path, value) in values)
            {
                Check(ct);
                _context.SetVariable(path, value);
                Check(ct);
            }
        }
        finally
        {
            lock (_gate) { _publishing--; if (_disposed && _publishing == 0) ReleasePaths(); }
        }
    }

    public void Dispose()
    {
        lock (_gate) { _disposed = true; if (_publishing == 0) ReleasePaths(); }
    }
    private void ReleasePaths()
    {
        lock (RegistryGate)
            foreach (string path in _paths)
                if (Owners.TryGetValue(path, out var owner) && ReferenceEquals(owner, this)) Owners.Remove(path);
    }
}
