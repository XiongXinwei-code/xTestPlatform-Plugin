using CiA402.Models;

namespace CiA402.Transport;

/// <summary>将运动与监控的单次传输串行执行；软超时不能终止底层调用，因此未完成前保留占用。</summary>
public sealed class CoordinatedAxisTransport(ICia402AxisTransport inner, int timeoutMs) : ICia402AxisTransport
{
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private volatile bool _faulted;
    private int _disposed;
    private int _innerDisposed;
    public bool SupportsConcurrentMonitoring => inner.SupportsConcurrentMonitoring;
    public Action<Exception>? CleanupError { get; set; }

    public Task<byte[]> ReadObjectAsync(ushort index, byte subIndex, CancellationToken ct)
        => ExecuteAsync(token => inner.ReadObjectAsync(index, subIndex, token), ct);
    public Task WriteObjectAsync(ushort index, byte subIndex, byte[] value, CancellationToken ct)
        => ExecuteAsync(async token => { await inner.WriteObjectAsync(index, subIndex, value, token).ConfigureAwait(false); return true; }, ct);
    public Task WriteTargetAsync(OperationMode mode, long target, ushort controlWord, CancellationToken ct)
        => ExecuteAsync(async token => { await inner.WriteTargetAsync(mode, target, controlWord, token).ConfigureAwait(false); return true; }, ct);
    public Task<AxisFeedback> ReadFeedbackAsync(bool position, bool velocity, bool torque, CancellationToken ct)
        => ExecuteAsync(token => inner.ReadFeedbackAsync(position, velocity, torque, token), ct);
    public void RequireCyclicMode(OperationMode mode)
    {
        CheckAvailable();
        inner.RequireCyclicMode(mode);
    }

    private void CheckAvailable()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_faulted) throw new InvalidOperationException("轴传输存在未完成的超时或取消调用，不能继续访问；请解除绑定并恢复通信后重新绑定");
    }

    private async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        CheckAvailable();
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
            var pending = Task.Run(() => operation(deadline.Token), deadline.Token);
            try { return await pending.WaitAsync(deadline.Token).ConfigureAwait(false); }
            catch
            {
                if (!pending.IsCompleted)
                {
                    _faulted = true;
                    releaseLater = true;
                    _ = pending.ContinueWith(task =>
                    {
                        _ = task.Exception;
                        deadline.Dispose();
                        Release();
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                throw;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException($"轴传输或排队等待超过 {timeoutMs} ms");
        }
        finally
        {
            if (!releaseLater)
            {
                deadline.Dispose();
                if (entered) Release();
            }
        }
    }

    private void Release()
    {
        try
        {
            if (Volatile.Read(ref _disposed) != 0) DisposeInner();
        }
        finally { _ioGate.Release(); }
    }

    private void DisposeInner()
    {
        if (Interlocked.Exchange(ref _innerDisposed, 1) != 0) return;
        try { inner.Dispose(); }
        catch (Exception ex)
        {
            try { CleanupError?.Invoke(ex); } catch (Exception) { }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (_ioGate.Wait(0)) Release();
    }
}
