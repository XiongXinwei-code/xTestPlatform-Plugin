using CAN.Adapters;
using PluginCommon;
using xTestPlatform.Core.Engine;

namespace CAN.Runtime;

/// <summary>步骤代次、原调用方与资源身份围栏，不改变共享协议路由或后台任务令牌。</summary>
internal sealed class CanExecutionGuard
{
    private static readonly AsyncLocal<Action?> Submission = new();
    internal static void CheckSubmission() => Submission.Value?.Invoke();
    private readonly IExecutionContext _context;
    private readonly CancellationToken _caller;
    private readonly ExecutionEpoch _epoch;
    private readonly Dictionary<string, ICanAdapter> _adapters = new();
    internal CanExecutionGuard(IExecutionContext context, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        _context = context; _caller = caller;
        _epoch = ExecutionEpoch.Capture(context.Resources, "CAN");
    }
    internal void Watch(string key, ICanAdapter adapter)
    {
        Check();
        if (_adapters.TryGetValue(key, out var previous) && !ReferenceEquals(previous, adapter))
            throw new InvalidOperationException("同一步骤的 CAN 连接代次已变化");
        _adapters[key] = adapter; Check();
    }
    internal void Check() { _caller.ThrowIfCancellationRequested(); CheckLifetime(); }
    internal void CheckLifetime()
    {
        _epoch.ThrowIfExpired();
        foreach (var (key, expected) in _adapters)
            if (!_context.Resources.TryGet<ICanAdapter>(key, out var current) || !ReferenceEquals(current, expected))
                throw new InvalidOperationException("CAN 连接已被移除或替换，旧步骤不能继续提交或发布结果");
    }
    internal void SetVariable(string name, object? value) { Check(); _context.SetVariable(name, value); Check(); }
    internal void Log(string message) { Check(); _context.Log(message); Check(); }
    internal void Log(LogLevel level, string message, string? detail = null) { Check(); _context.Log(level, message, detail); Check(); }
    internal async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        Check(); var previous = Submission.Value; Submission.Value = Check;
        try { var result = await operation().ConfigureAwait(false); Check(); return result; }
        finally { Submission.Value = previous; }
    }
    internal async Task RunAsync(Func<Task> operation)
    {
        Check(); var previous = Submission.Value; Submission.Value = Check;
        try { await operation().ConfigureAwait(false); Check(); }
        finally { Submission.Value = previous; }
    }
}
