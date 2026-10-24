using PluginCommon;
using xTestPlatform.Core.Engine;

namespace OpcUa.Helpers;

/// <summary>调用方令牌与 SDK 执行表代次共同约束提交、发布和完成；不持有硬件。</summary>
internal sealed class OpcUaExecutionGuard
{
    private readonly IExecutionContext _context;
    private readonly CancellationToken _caller;
    internal ExecutionEpoch Epoch { get; }
    internal OpcUaExecutionGuard(IExecutionContext context, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested(); _context = context; _caller = caller;
        Epoch = ExecutionEpoch.Capture(context.Resources, "OpcUa"); Check();
    }
    internal void Check()
    {
        _caller.ThrowIfCancellationRequested();
        if (_context.IsAbortRequested) throw new OperationCanceledException("OPC UA 宿主已明确请求中断，禁止迟到请求或结果发布", _caller);
        Epoch.ThrowIfExpired();
    }
    internal void EnsureCurrent(string key, OpcUaConnectionResource owner)
    { Check(); OpcUaHelper.EnsureCurrentConnection(_context, key, owner, _caller); Check(); }
}
