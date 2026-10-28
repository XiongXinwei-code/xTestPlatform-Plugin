using LIN.Adapters;
using PluginCommon;
using xTestPlatform.Core.Engine;

namespace LIN.Runtime;

/// <summary>步骤准入和结果发布围栏；不会把短步骤令牌绑定到跨步骤资源。</summary>
internal sealed class LinExecutionGuard
{
    private readonly IExecutionContext _context;
    private readonly CancellationToken _caller;
    private readonly ExecutionEpoch _epoch;
    private string? _key;
    private ILinAdapter? _adapter;
    internal LinExecutionGuard(IExecutionContext context, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        _context = context; _caller = caller;
        _epoch = ExecutionEpoch.Capture(context.Resources, "LIN");
    }
    internal void Watch(string key, ILinAdapter adapter) { _key = key; _adapter = adapter; Check(); }
    internal void Check()
    {
        _caller.ThrowIfCancellationRequested(); CheckLifetime();
    }
    internal void CheckLifetime()
    {
        _epoch.ThrowIfExpired();
        if (_key != null && (!_context.Resources.TryGet<ILinAdapter>(_key, out var current) || !ReferenceEquals(current, _adapter)))
            throw new InvalidOperationException("LIN 连接已被移除或替换，旧步骤不能继续提交或发布结果");
    }
    internal void SetVariable(string name, object? value) { Check(); _context.SetVariable(name, value); Check(); }
    internal void Log(string message) { Check(); _context.Log(message); Check(); }
    internal void Log(LogLevel level, string message) { Check(); _context.Log(level, message); Check(); }
}
