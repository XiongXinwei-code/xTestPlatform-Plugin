using CiA402.Models;
using CiA402.Core;
using System.Diagnostics;

namespace CiA402.Transport;

/// <summary>将运动与监控的单次传输串行执行；软超时不能终止底层调用，因此未完成前保留占用。</summary>
public sealed class CoordinatedAxisTransport(ICia402AxisTransport inner, int timeoutMs) : ICia402AxisTransport, IAsyncDisposable
{
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private string? _faultReason;
    private string? _pendingPassiveReceive;
    private string _lastFailure = "尚无通信失败";
    private int _disposed;
    private int _innerDisposed;
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool SupportsConcurrentMonitoring => inner.SupportsConcurrentMonitoring;
    public bool FeedbackIsPassiveReceive => inner.FeedbackIsPassiveReceive;
    public Action<Exception>? CleanupError { get; set; }
    public string DiagnosticContext => $"传输隔离={Volatile.Read(ref _faultReason) ?? "无"}; 等待被动接收退出={Volatile.Read(ref _pendingPassiveReceive) ?? "无"}; 最近通信失败={Volatile.Read(ref _lastFailure)}";

    public Task<byte[]> ReadObjectAsync(ushort index, byte subIndex, CancellationToken ct)
        => ExecuteAsync(token => inner.ReadObjectAsync(index, subIndex, token), $"读对象 0x{index:X4}:{subIndex:X2}", ct);
    public Task WriteObjectAsync(ushort index, byte subIndex, byte[] value, CancellationToken ct)
        => ExecuteAsync(async token => { await inner.WriteObjectAsync(index, subIndex, value, token).ConfigureAwait(false); return true; }, $"写对象 0x{index:X4}:{subIndex:X2}，长度={value.Length}", ct);
    public Task WriteTargetAsync(OperationMode mode, long target, ushort controlWord, CancellationToken ct)
        => ExecuteAsync(async token => { await inner.WriteTargetAsync(mode, target, controlWord, token).ConfigureAwait(false); return true; }, $"发送周期目标，模式={mode}，控制字=0x{controlWord:X4}", ct);
    public Task<AxisFeedback> ReadFeedbackAsync(bool position, bool velocity, bool torque, CancellationToken ct)
        => ExecuteAsync(token => inner.ReadFeedbackAsync(position, velocity, torque, token), $"读取反馈，位置={position}，速度={velocity}，转矩={torque}", ct, inner.FeedbackIsPassiveReceive);
    public void RequireCyclicMode(OperationMode mode)
    {
        CheckAvailable();
        inner.RequireCyclicMode(mode);
    }

    private void CheckAvailable()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Volatile.Read(ref _faultReason) is { } reason)
            throw new InvalidOperationException($"轴传输曾有尚未退出的超时或取消调用，已隔离后续访问；请先解除绑定并等待底层释放，按下层错误恢复通信后再绑定，不得另开 SDO 绕过隔离；首次隔离原因：{reason}");
    }

    private async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, string description, CancellationToken ct, bool passiveReceive = false)
    {
        CheckAvailable();
        var clock = Stopwatch.StartNew();
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var entered = false;
        var releaseLater = false;
        try
        {
            deadline.CancelAfter(timeoutMs);
            await _ioGate.WaitAsync(deadline.Token).ConfigureAwait(false);
            entered = true;
            CheckAvailable();
            // 调用入口本身也可能同步阻塞，不能在调用线程上直接取得其 Task。
            var pending = Task.Run(() => { CheckBudget(); CheckAvailable(); return operation(deadline.Token); }, deadline.Token);
            try
            {
                var result = await pending.WaitAsync(deadline.Token).ConfigureAwait(false);
                CheckBudget();
                CheckAvailable();
                return result;
            }
            catch (Exception ex)
            {
                if (!pending.IsCompleted)
                {
                    // 被动接收的取消不污染协议事务，但未实际退出前仍保留 I/O 门，禁止重入。
                    var reason = Describe(true, ex);
                    if (passiveReceive) Volatile.Write(ref _pendingPassiveReceive, reason);
                    else Interlocked.CompareExchange(ref _faultReason, reason, null);
                    releaseLater = true;
                    _ = pending.ContinueWith(task =>
                    {
                        _ = task.Exception;
                        if (passiveReceive) Volatile.Write(ref _pendingPassiveReceive, null);
                        deadline.Dispose();
                        Release();
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                throw;
            }
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            var detail = Describe(releaseLater, ex);
            Volatile.Write(ref _lastFailure, detail);
            var policy = passiveReceive ? "被动反馈接收超时，本次反馈无效；原调用退出后可退避重读" : "轴传输或排队等待超时";
            var error = new TimeoutException($"{policy}（{timeoutMs} ms）；{detail}", ex);
            error.Data[AxisFailureContext.TransportKey] = detail;
            throw error;
        }
        catch (Exception ex)
        {
            var detail = Describe(releaseLater, ex);
            Volatile.Write(ref _lastFailure, detail);
            AxisFailureContext.AttachTransport(ex, detail);
            throw;
        }
        finally
        {
            if (!releaseLater)
            {
                deadline.Dispose();
                if (entered) Release();
            }
        }

        void CheckBudget()
        {
            ct.ThrowIfCancellationRequested();
            AxisOperationBoundary.Check();
            if (clock.ElapsedMilliseconds >= timeoutMs)
                throw new TimeoutException($"单次轴通信超过配置总期限 {timeoutMs} ms，禁止迟到提交或发布结果");
            deadline.Token.ThrowIfCancellationRequested();
        }

        string Describe(bool pending, Exception error)
            => $"{description}; 阶段={(entered ? "底层 I/O" : "等待 I/O 排队")}; 耗时={clock.ElapsedMilliseconds} ms; CommunicationTimeoutMs={timeoutMs}; 调用令牌取消={ct.IsCancellationRequested}; 单次期限取消={deadline.IsCancellationRequested}; "
                + $"等待结束时底层调用尚未退出={pending}; 恢复策略={(passiveReceive ? "被动接收退出后可重试，未退出前禁止重入" : "非被动事务未退出时持续隔离")}; 原因={error.GetType().Name}: {error.Message}"
                + (inner is IAxisTransportDiagnostics diagnostics ? "; 后端上下文=" + diagnostics.DiagnosticContext : "");
    }

    private void Release()
    {
        _ioGate.Release();
        if (Volatile.Read(ref _disposed) != 0) TryDisposeInner();
    }

    private void TryDisposeInner()
    {
        if (!_ioGate.Wait(0)) return;
        try { QueueDisposeInner(); }
        finally { _ioGate.Release(); }
    }

    private void QueueDisposeInner()
    {
        if (Interlocked.Exchange(ref _innerDisposed, 1) != 0) return;
        _ = Task.Run(() =>
        {
            try { inner.Dispose(); _released.TrySetResult(); }
            catch (Exception ex)
            {
                _released.TrySetException(ex);
                _ = _released.Task.Exception;
                try { CleanupError?.Invoke(ex); } catch (Exception) { }
            }
        });
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        TryDisposeInner();
    }

    public ValueTask DisposeAsync() { Dispose(); return new(_released.Task); }
}
