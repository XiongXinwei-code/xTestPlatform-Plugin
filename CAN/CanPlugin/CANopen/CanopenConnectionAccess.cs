using System.Diagnostics;
using System.Runtime.CompilerServices;
using CAN.Adapters;
using CAN.Models;

namespace CAN.CANopen;

/// <summary>共享 CANopen 访问入口；不拥有适配器，不启动后台接收或周期发送。</summary>
public sealed class CanopenConnectionAccess
{
    private static readonly ConditionalWeakTable<ICanAdapter, CanopenConnectionAccess> Connections = new();
    private readonly ICanAdapter _adapter;
    private readonly SemaphoreSlim _receiveGate = new(1, 1);
    private readonly SemaphoreSlim _sdoGate = new(1, 1);
    private readonly Dictionary<uint, Queue<CanopenReceivedFrame>> _pending = new();
    private readonly Dictionary<byte, object> _owners = new();
    private volatile bool _unusable;

    private CanopenConnectionAccess(ICanAdapter adapter) => _adapter = adapter;
    public static CanopenConnectionAccess For(ICanAdapter adapter) => Connections.GetValue(adapter, a => new(a));

    public CanopenNodeAccess ReserveNode(byte nodeId, int timeoutMs)
    {
        if (nodeId is < 1 or > 127 || timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(nodeId), "节点或通信超时无效");
        CheckUsable();
        var owner = new object();
        lock (_owners)
        {
            if (_owners.ContainsKey(nodeId)) throw new InvalidOperationException($"CANopen 节点 {nodeId} 已被轴会话占用");
            _owners.Add(nodeId, owner);
        }
        return new CanopenNodeAccess(this, new CanopenSdoClient(new CanopenBus(_adapter, owner), nodeId, timeoutMs), nodeId, owner, timeoutMs);
    }

    internal async Task<IDisposable> EnterSdoAsync(byte node, object? owner, int timeoutMs, CancellationToken ct)
    {
        if (!await _sdoGate.WaitAsync(timeoutMs, ct).ConfigureAwait(false))
            throw new TimeoutException($"等待 CANopen SDO 事务超时（{timeoutMs} ms）");
        try
        {
            CheckUsable();
            lock (_owners)
                if (_owners.TryGetValue(node, out var current) && !ReferenceEquals(current, owner))
                    throw new InvalidOperationException($"CANopen 节点 {node} 已由轴会话管理，请勿混用原始 SDO 步骤");
            return new ReleaseGate(_sdoGate);
        }
        catch { _sdoGate.Release(); throw; }
    }

    internal void ReleaseNode(byte node, object owner)
    {
        lock (_owners)
            if (_owners.TryGetValue(node, out var current) && ReferenceEquals(current, owner)) _owners.Remove(node);
    }

    internal void InvalidateSdoTransaction() => _unusable = true;

    private void CheckUsable()
    {
        if (_unusable) throw new InvalidOperationException("CANopen 通信存在未完成或超时的底层调用，请停止操作并重新打开 CAN 连接");
        if (!_adapter.IsConnected) throw new InvalidOperationException("CAN 连接已关闭");
    }

    public async Task SendAsync(uint cobId, byte[] data, int timeoutMs, CancellationToken ct)
    {
        CheckUsable();
        ct.ThrowIfCancellationRequested();
        if (cobId > 0x7FF || data.Length > 8 || timeoutMs <= 0) throw new ArgumentException("CANopen 标准帧或通信超时无效");
        var task = Task.Run(() => _adapter.Write(new CanMessage { Id = cobId, Data = data.ToArray(), FrameType = CanFrameType.Standard }), ct);
        await AwaitIo(task, timeoutMs, ct).ConfigureAwait(false);
    }

    private async Task AwaitIo(Task task, int timeoutMs, CancellationToken ct)
    {
        try { await task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false); }
        catch
        {
            if (!task.IsCompleted)
            {
                // 给响应取消的驱动一次有界的完成机会，避免正常取消永久禁用共享连接。
                try { await task.WaitAsync(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false); }
                catch (Exception) { }
                if (!task.IsCompleted)
                {
                    _unusable = true;
                    _ = task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                }
            }
            throw;
        }
    }

    public async Task<CanopenReceivedFrame?> ReceiveAsync(uint cobId, int timeoutMs, CancellationToken ct)
    {
        CheckUsable();
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "接收超时必须大于零");
        var clock = Stopwatch.StartNew();
        if (!await _receiveGate.WaitAsync(timeoutMs, ct).ConfigureAwait(false)) throw new TimeoutException($"等待 CANopen 接收超时（{timeoutMs} ms）");
        try
        {
            CheckUsable();
            if (_pending.TryGetValue(cobId, out var queued) && queued.Count > 0) return queued.Dequeue();
            while (clock.ElapsedMilliseconds < timeoutMs)
            {
                ct.ThrowIfCancellationRequested();
                var remain = Math.Max(1, timeoutMs - (int)clock.ElapsedMilliseconds);
                var read = Task.Run(() => _adapter.Read(Math.Min(remain, 50), ct), ct);
                await AwaitIo(read, remain + 100, ct).ConfigureAwait(false);
                var frame = await read.ConfigureAwait(false);
                if (frame == null || frame.IsTransmitEcho || frame.FrameType != CanFrameType.Standard || frame.IsFd || frame.Id > 0x7FF) continue;
                var received = new CanopenReceivedFrame(frame, Stopwatch.GetTimestamp());
                if (frame.Id == cobId) return received;
                if (!_pending.TryGetValue(frame.Id, out var queue)) _pending[frame.Id] = queue = new();
                if (queue.Count >= 64) queue.Dequeue();
                queue.Enqueue(received);
            }
            return null;
        }
        finally { _receiveGate.Release(); }
    }

    private sealed class ReleaseGate(SemaphoreSlim gate) : IDisposable
    {
        private int _disposed;
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) gate.Release(); }
    }
}

public sealed record CanopenReceivedFrame(CanMessage Frame, long ObservedTimestamp)
{
    public TimeSpan QueueAge => Stopwatch.GetElapsedTime(ObservedTimestamp);
}

/// <summary>独占节点的 SDO 客户端；释放时不关闭共享 CAN 连接。</summary>
public sealed class CanopenNodeAccess : IDisposable
{
    private readonly CanopenConnectionAccess _connection;
    private readonly CanopenSdoClient _client;
    private readonly byte _node;
    private readonly object _owner;
    private int _disposed;
    public int TimeoutMs { get; }
    internal CanopenNodeAccess(CanopenConnectionAccess connection, CanopenSdoClient client, byte node, object owner, int timeoutMs)
        => (_connection, _client, _node, _owner, TimeoutMs) = (connection, client, node, owner, timeoutMs);
    public Task<byte[]> ReadObjectAsync(ushort index, byte subIndex, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        return _client.UploadAsync(index, subIndex, false, ct);
    }
    public Task WriteObjectAsync(ushort index, byte subIndex, byte[] value, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        return _client.DownloadAsync(index, subIndex, value, false, ct);
    }
    public Task SendPdoAsync(uint cobId, byte[] value, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        return _connection.SendAsync(cobId, value, TimeoutMs, ct);
    }
    public Task<CanopenReceivedFrame?> ReceivePdoAsync(uint cobId, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        return _connection.ReceiveAsync(cobId, TimeoutMs, ct);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _connection.ReleaseNode(_node, _owner);
    }
}
