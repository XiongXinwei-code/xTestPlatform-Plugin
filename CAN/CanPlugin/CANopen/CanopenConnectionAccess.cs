using System.Diagnostics;
using System.Runtime.CompilerServices;
using CAN.Adapters;
using CAN.Models;
using CAN.Runtime;

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

    internal IDisposable EnterControlledMode(Action<CanMessage> preserveQueued)
    {
        if (!_receiveGate.Wait(0)) throw new InvalidOperationException("旧 CANopen 接收仍在执行，不能切换接收模式");
        if (!_sdoGate.Wait(0)) { _receiveGate.Release(); throw new InvalidOperationException("SDO 事务仍在执行，不能切换接收模式"); }
        try
        {
            CheckUsable();
            foreach (var queue in _pending.Values)
                while (queue.TryDequeue(out var frame))
                {
                    frame.Frame.IngressMonotonicTicks ??= frame.ObservedTimestamp;
                    preserveQueued(frame.Frame);
                }
            _pending.Clear();
            return new ReleasePair(_receiveGate, _sdoGate);
        }
        catch { _sdoGate.Release(); _receiveGate.Release(); throw; }
    }

    private sealed class ReleasePair(SemaphoreSlim receive, SemaphoreSlim sdo) : IDisposable
    {
        public void Dispose() { sdo.Release(); receive.Release(); }
    }

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
        var stop = (_adapter as CanConnectionResource)?.ForegroundWaitSignal;
        bool entered = stop == null ? await _sdoGate.WaitAsync(timeoutMs, ct).ConfigureAwait(false)
            : await stop.EnterAsync(_sdoGate, timeoutMs, ct, "CANopen SDO 事务排队").ConfigureAwait(false);
        if (!entered)
            throw new TimeoutException($"等待 CANopen SDO 事务超时（{timeoutMs} ms）");
        try
        {
            CheckUsable();
            lock (_owners)
                if (_owners.TryGetValue(node, out var current) && !ReferenceEquals(current, owner))
                    throw new InvalidOperationException($"CANopen 节点 {node} 已由轴会话管理，请勿混用原始 SDO 步骤");
            if (_adapter is CanConnectionResource resource) resource.BeginSdoTransaction(node);
            return new ReleaseGate(_sdoGate);
        }
        catch { _sdoGate.Release(); throw; }
    }

    internal void ReleaseNode(byte node, object owner)
    {
        lock (_owners)
            if (_owners.TryGetValue(node, out var current) && ReferenceEquals(current, owner)) _owners.Remove(node);
    }

    internal void InvalidateSdoTransaction()
    {
        _unusable = true;
        if (_adapter is CanConnectionResource resource) resource.Isolate("CANopen SDO 事务未完成或超时");
    }

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
        // Use the connection's tracked submission boundary: a worker queued past the
        // caller deadline must not later send a stale control frame.
        await CanRawIo.WriteAsync(_adapter,
            new CanMessage { Id = cobId, Data = data.ToArray(), FrameType = CanFrameType.Standard },
            timeoutMs, ct).ConfigureAwait(false);
    }

    private async Task AwaitIo(Task task, int timeoutMs, CancellationToken ct)
    {
        try
        {
            var stop = (_adapter as CanConnectionResource)?.ForegroundWaitSignal;
            if (stop == null) await task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
            else await stop.WaitAsync(task, TimeSpan.FromMilliseconds(timeoutMs), ct, "CANopen 原始接收").ConfigureAwait(false);
        }
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
                    if (_adapter is CanConnectionResource resource) resource.Isolate("CANopen 原生 I/O 尚未退出");
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
        if (_adapter is CanConnectionResource { Controlled: true } resource)
        {
            var routed = await resource.ReadCanopenAsync(cobId, timeoutMs, ct).ConfigureAwait(false);
            return routed == null ? null : CanopenReceivedFrame.From(routed);
        }
        var clock = Stopwatch.StartNew();
        var stop = (_adapter as CanConnectionResource)?.ForegroundWaitSignal;
        bool entered = stop == null ? await _receiveGate.WaitAsync(timeoutMs, ct).ConfigureAwait(false)
            : await stop.EnterAsync(_receiveGate, timeoutMs, ct, "CANopen 接收排队").ConfigureAwait(false);
        if (!entered) throw new TimeoutException($"等待 CANopen 接收超时（{timeoutMs} ms）");
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
                var received = CanopenReceivedFrame.From(frame);
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
    public static CanopenReceivedFrame From(CanMessage frame) => new(frame,
        frame.CaptureMonotonicLowerBoundTicks ?? frame.IngressMonotonicTicks ?? 0);
    // Host ingress is only a queueing event, not evidence of when the device captured
    // a frame. In particular it must not make an old native-queue frame fresh again.
    public bool HasKnownObservationTime => Frame.CaptureMonotonicLowerBoundTicks is > 0 &&
        ObservedTimestamp == Frame.CaptureMonotonicLowerBoundTicks && ObservedTimestamp <= Stopwatch.GetTimestamp();
    public TimeSpan QueueAge => HasKnownObservationTime ? Stopwatch.GetElapsedTime(ObservedTimestamp) : TimeSpan.MaxValue;
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
    public async Task SendPdoAsync(uint cobId, byte[] value, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        using var transaction = await _connection.EnterSdoAsync(_node, _owner, TimeoutMs, ct).ConfigureAwait(false);
        await _connection.SendAsync(cobId, value, TimeoutMs, ct).ConfigureAwait(false);
    }
    public async Task<CanopenReceivedFrame?> ReceivePdoAsync(uint cobId, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        return await _connection.ReceiveAsync(cobId, TimeoutMs, ct).ConfigureAwait(false);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _connection.ReleaseNode(_node, _owner);
    }
}
