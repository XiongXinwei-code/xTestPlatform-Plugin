using NiDaq.Models;

namespace NiDaq.Helpers;

/// <summary>仅由所属 DAQ owner 的串行 I/O/清理 worker 调用，不拥有第二个 SDK 句柄。</summary>
internal sealed class AnalogOutputSession(AoOutputPlan plan, Action<double[]> write, Action start, Action stop, Action prepareWrite)
{
    private bool _nativeMayBeStarted, _ready, _finalAttempted, _finalApplied;
    private Exception? _unconfirmedDisposition;
    public int CleanupTimeoutMs => plan.CleanupTimeoutMs;
    public void Start(Action ensureActive)
    {
        if (_nativeMayBeStarted) throw new InvalidOperationException("AO 任务已启动或之前启动结果未知，请先 TaskStop");
        ensureActive();
        _nativeMayBeStarted = true; // SDK Start 抛错亦可能存在副作用，不能误认为未启动。
        start();
        ensureActive();
        prepareWrite();
        ensureActive();
        write(plan.InitialValues);
        ensureActive();
        _ready = true;
    }
    public void Write(double[] values, Action ensureActive)
    {
        if (!_ready) throw new InvalidOperationException("AO 尚未成功 TaskStart，不能自动启动输出");
        var copy = plan.ValidateValues(values);
        ensureActive();
        prepareWrite();
        ensureActive();
        write(copy);
        ensureActive();
    }
    public void Close(Action ensureActive)
    {
        _ready = false;
        if (_unconfirmedDisposition != null)
            throw new InvalidOperationException("AO 终值写入曾失败或结果未知，禁止自动重发/释放，请核实设备输出及原始错误", _unconfirmedDisposition);
        if (!_nativeMayBeStarted) return;
        Exception? finalError = null;
        if (plan.StopPolicy == AoStopPolicy.WriteFinalValues && !_finalApplied)
        {
            if (_finalAttempted) throw new InvalidOperationException("AO 终值写入结果未知，禁止自动重发；保留任务隔离，请核实设备输出及原始错误");
            ensureActive();
            // Stream.Timeout / writer 构造属于准备，不是电压提交。准备失败可显式重试。
            prepareWrite();
            ensureActive();
            _finalAttempted = true;
            try { write(plan.FinalValues); _finalApplied = true; }
            catch (Exception ex) { finalError = ex; _unconfirmedDisposition = ex; }
        }
        // 指定终值失败不能跳过仍可提交的 Stop；但 Stop 返回也不能伪称终值已经施加。
        try { ensureActive(); stop(); _nativeMayBeStarted = false; }
        catch (Exception stopError) when (finalError != null) { throw new AggregateException("AO 终值与停止均未确认，保留隔离", finalError, stopError); }
        if (finalError != null) throw new InvalidOperationException("AO Stop 已返回，但指定终值未确认，保留隔离并禁止盲重发", finalError);
    }
}
