using System.Net;
using System.Net.Sockets;

namespace Ethernet.Runtime;

/// <summary>命名 UDP 所有者；先终止 socket，再确认真实调用和取消回调退出。</summary>
internal sealed class UdpConnectionResource : IDisposable, IAsyncDisposable
{
    private readonly UdpClient _socket;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1), _receiveGate = new(1, 1);
    private readonly object _sync = new();
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _closed, _active;
    private Task? _closeTask;
    internal string? CloseDiagnostic { get; private set; }
    internal UdpConnectionResource(IPEndPoint local, bool broadcast)
    {
        _socket = new UdpClient(local.AddressFamily);
        try { _socket.EnableBroadcast = broadcast; _socket.Client.Bind(local); }
        catch { _socket.Dispose(); throw; }
    }
    internal IPEndPoint LocalEndpoint => (IPEndPoint)_socket.Client.LocalEndPoint!;
    internal void CheckOpen() { if (Volatile.Read(ref _closed) != 0) throw new InvalidOperationException("UDP 连接已关闭或正在关闭"); }
    internal async Task SendAsync(IEthernetNetworkIo network, string host, int port, byte[] bytes,
        EthernetOperationBudget budget)
    {
        var actual = RunAsync(_sendGate, async token =>
        {
            var endpoint = await EthernetNetworkIo.ResolveEndpointAsync(network, host, port,
                _socket.Client.AddressFamily, budget, token).ConfigureAwait(false);
            CheckOpen(); token.ThrowIfCancellationRequested(); budget.BeforeSubmission();
            await network.SendUdpAsync(_socket, endpoint, bytes, token).ConfigureAwait(false);
            return true;
        }, budget.Token, budget.Check);
        try { await actual.WaitAsync(budget.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!actual.IsCompleted)
        {
            // 停止等待不等于底层调用退出。阻止旧调用继续提交，并保留真实 drain 给 Close/Open。
            Observe(RequestClose()); Observe(actual); throw;
        }
    }
    internal Task<UdpReceiveResult> ReceiveAsync(CancellationToken token, Action check) =>
        RunAsync(_receiveGate, ct => _socket.ReceiveAsync(ct).AsTask(), token, check);
    private async Task<T> RunAsync<T>(SemaphoreSlim gate, Func<CancellationToken, Task<T>> operation,
        CancellationToken caller, Action check)
    {
        lock (_sync) { CheckOpen(); _active++; }
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, _lifetime.Token);
            bool acquired = false;
            try
            {
                await gate.WaitAsync(linked.Token).ConfigureAwait(false); acquired = true;
                linked.Token.ThrowIfCancellationRequested(); check(); CheckOpen();
                var result = await operation(linked.Token).ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested(); check(); CheckOpen(); return result;
            }
            catch (OperationCanceledException ex) when (Volatile.Read(ref _closed) != 0 && !caller.IsCancellationRequested)
            { throw new InvalidOperationException("UDP 连接已关闭或替换，本代次收发已取消；不是接收超时", ex); }
            finally { if (acquired) gate.Release(); }
        }
        finally
        {
            lock (_sync) { _active--; if (_closed != 0 && _active == 0) _drained.TrySetResult(); }
        }
    }
    private Task RequestClose()
    {
        lock (_sync)
        {
            _closed = 1;
            if (_active == 0) _drained.TrySetResult();
            // 只在旧收尾确实结束且失败时重试资源释放，不能并发启动两个关闭任务。
            if (_closeTask == null || _closeTask.IsFaulted) _closeTask = Task.Run(CloseCoreAsync);
            return _closeTask;
        }
    }
    private async Task CloseCoreAsync()
    {
        Task callbacks = _lifetime.CancelAsync();
        Exception? releaseError = null;
        try { _socket.Dispose(); } // 先解除 Receive 的阻塞；不等待取消回调后才释放 socket。
        catch (Exception ex) { releaseError = ex; }
        Exception? callbackError = null;
        try { await callbacks.ConfigureAwait(false); }
        catch (Exception ex) { callbackError = ex; }
        await _drained.Task.ConfigureAwait(false);
        if (releaseError != null) throw new InvalidOperationException("UDP socket 释放失败，保留所有权以供重试", releaseError);
        if (callbackError != null)
        {
            // 回调已退出的历史错误不冒充仍在占用；真实 socket 与调用均已释放。
            CloseDiagnostic = $"UDP 取消回调已退出但报告错误；socket 已释放且全部调用已退出：{callbackError}";
            var diagnostic = CloseDiagnostic;
            Observe(Task.Run(() => System.Diagnostics.Trace.TraceError(diagnostic)));
        }
        _lifetime.Dispose(); _sendGate.Dispose(); _receiveGate.Dispose();
    }
    private static void Observe(Task task) => _ = task.ContinueWith(t => { _ = t.Exception; },
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    public ValueTask DisposeAsync() => new(RequestClose());
    /// <summary>同步资源回收仅请求退出；连接池使用 DisposeAsync 确认收尾。</summary>
    public void Dispose() => Observe(RequestClose());
}
