using PluginCommon;
using xTestPlatform.Core.Engine;

namespace NiDaq.Helpers;

/// <summary>一次步骤保留同一个执行代次；清表与调用方取消是不同的失效来源。</summary>
internal sealed class DaqExecutionGuard
{
    private readonly IExecutionContext _context;
    private readonly CancellationToken _caller;
    internal ExecutionEpoch Epoch { get; }
    internal DaqExecutionGuard(IExecutionContext context, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested(); _context = context; _caller = caller;
        Epoch = ExecutionEpoch.Capture(context.Resources, "NiDaq"); Check();
    }
    internal void Check()
    {
        _caller.ThrowIfCancellationRequested();
        if (_context.IsAbortRequested) throw new OperationCanceledException("DAQ 宿主已明确请求中断，禁止迟到设备提交或结果发布", _caller);
        Epoch.ThrowIfExpired();
    }
    internal void EnsureCurrent(string name, DaqTaskResource owner)
    { Check(); NiDaqTaskRegistry.EnsureCurrent(_context, name, owner, _caller); Check(); }
}
