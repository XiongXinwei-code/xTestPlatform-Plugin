using System.Diagnostics;

namespace Ethernet.Runtime;

/// <summary>一次通信的原始取消令牌和单调总预算；DNS/连接/发送/完整响应不能重置期限。</summary>
internal sealed class EthernetOperationBudget : IDisposable
{
    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private readonly CancellationToken _caller;
    private readonly CancellationTokenSource _deadline;
    private readonly string _operation;
    private readonly Action? _ensureExecution;
    public int TimeoutMs { get; }
    public string Phase { get; set; } = "准备通信";
    public bool SubmissionStarted { get; private set; }
    public CancellationToken Token => _deadline.Token;
    public int RemainingMs
    {
        get { Check(); return Math.Max(1, TimeoutMs - (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds)); }
    }
    public bool IsExpired => Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds >= TimeoutMs ||
        (_deadline.IsCancellationRequested && !_caller.IsCancellationRequested);
    public EthernetOperationBudget(int timeoutMs, string operation, CancellationToken caller, Action? ensureExecution = null)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "通信总超时必须大于 0");
        caller.ThrowIfCancellationRequested();
        TimeoutMs = timeoutMs; _operation = operation; _caller = caller;
        _ensureExecution = ensureExecution;
        _deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
        _deadline.CancelAfter(timeoutMs);
    }
    public void Check()
    {
        _caller.ThrowIfCancellationRequested();
        _ensureExecution?.Invoke();
        if (IsExpired) throw CreateTimeout();
    }
    public void BeforeSubmission() { Check(); SubmissionStarted = true; Phase = "提交发送"; }
    public TimeoutException CreateTimeout(Exception? inner = null) => new(
        $"{_operation}总超时 {TimeoutMs} ms，阶段={Phase}；" +
        (SubmissionStarted ? "已尝试提交，报文是否送达或被远端执行未知，不自动重发" : "本次尚未提交发送"), inner);
    public void Dispose() => _deadline.Dispose();
}
