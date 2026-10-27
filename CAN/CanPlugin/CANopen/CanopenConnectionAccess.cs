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
    private readonly Dictionary<byte, HashSet<object>> _senderOwners = [];
    private readonly AsyncLocal<SdoLease?> _submissionScope = new();
    private SdoLease? _activeSdo;
    private volatile bool _unusable;

    private CanopenConnectionAccess(ICanAdapter adapter) => _adapter = adapter;
    public static CanopenConnectionAccess For(ICanAdapter adapter) => Connections.GetValue(adapter, a => new(a));

    internal IDisposable EnterControlledMode(Action<CanMessage> preserveQueued, int timeoutMs, CancellationToken ct)
    {
        var budget = CanConfirmationBudget.Start(timeoutMs, ct) with { Operation = "接收模式转换" };
        if (!_receiveGate.Wait(budget.Remaining(ct), ct)) throw new TimeoutException("等待旧CANopen接收退出超过配置期限");
        bool sdoEntered = false;
        try
        {
            if (!_sdoGate.Wait(budget.Remaining(ct), ct)) throw new TimeoutException("等待现有SDO发送结束超过共享接收转换期限");
            sdoEntered = true;
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
        catch { if (sdoEntered) _sdoGate.Release(); _receiveGate.Release(); throw; }
    }

    private sealed class ReleasePair(SemaphoreSlim receive, SemaphoreSlim sdo) : IDisposable
    {
        public void Dispose() { sdo.Release(); receive.Release(); }
    }

    public CanopenNodeAccess ReserveNode(byte nodeId, int timeoutMs)
        => ReserveNodeCore(nodeId, timeoutMs, false, default);
    public CanopenNodeAccess ReserveNode(byte nodeId, int timeoutMs, CancellationToken ct)
        => ReserveNodeCore(nodeId, timeoutMs, false, ct);
    internal CanopenNodeAccess ReserveNodeForSender(byte nodeId, int timeoutMs)
        => ReserveNodeCore(nodeId, timeoutMs, true, default);
    private CanopenNodeAccess ReserveNodeCore(byte nodeId, int timeoutMs, bool allowExternalReads, CancellationToken ct)
    {
        if (nodeId is < 1 or > 127 || timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(nodeId), "节点或通信超时无效");
        ct.ThrowIfCancellationRequested();
        if (_adapter is CanConnectionResource resource) resource.EnsureSharedReceive(timeoutMs, ct);
        CheckUsable();
        var owner = new object();
        var inbox = (_adapter as CanConnectionResource)?.CreateNodeInbox();
        try
        {
            lock (_owners)
            {
                if (!allowExternalReads)
                {
                    if (_owners.ContainsKey(nodeId)) throw new InvalidOperationException($"CANopen 节点 {nodeId} 已存在轴管理会话");
                    _owners.Add(nodeId, owner);
                }
                else
                {
                    if (!_senderOwners.TryGetValue(nodeId, out var senders)) _senderOwners[nodeId] = senders = [];
                    senders.Add(owner);
                }
            }
            return new CanopenNodeAccess(this, new CanopenSdoClient(new CanopenBus(_adapter, owner), nodeId, timeoutMs), nodeId, owner, timeoutMs, inbox);
        }
        catch { inbox?.Dispose(); throw; }
    }

    internal async Task<SdoLease> EnterSdoAsync(byte node, object? owner, int timeoutMs, CancellationToken ct, bool readOnly = false)
    {
        long started = Stopwatch.GetTimestamp();
        if (_adapter is CanConnectionResource { Controlled: false } pending) await Task.Run(() => pending.EnsureSharedReceive(timeoutMs, ct), CancellationToken.None).ConfigureAwait(false);
        int left = timeoutMs - (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        if (left <= 0) throw new TimeoutException("等待共享CAN接收转换超过SDO配置期限");
        var stop = (_adapter as CanConnectionResource)?.ForegroundWaitSignal;
        bool entered = stop == null ? await _sdoGate.WaitAsync(left, ct).ConfigureAwait(false)
            : await stop.EnterAsync(_sdoGate, left, ct, "CANopen SDO 事务排队").ConfigureAwait(false);
        if (!entered)
            throw new TimeoutException($"等待 CANopen SDO 事务超时（{timeoutMs} ms）");
        try
        {
            CheckUsable();
            lock (_owners)
                if (owner != null && !(_owners.TryGetValue(node, out var current) && ReferenceEquals(current, owner)) &&
                    !(_senderOwners.TryGetValue(node, out var senders) && senders.Contains(owner)))
                    throw new ObjectDisposedException(nameof(CanopenNodeAccess), "节点管理租约已释放，不能执行迟到操作");
            if (_adapter is CanConnectionResource resource) resource.BeginSdoTransaction(node);
            var lease = new SdoLease(this); lock (_owners) _activeSdo = lease; return lease;
        }
        catch { _sdoGate.Release(); throw; }
    }

    internal sealed class SdoLease(CanopenConnectionAccess access) : IDisposable
    {
        private int _disposed;
        internal IDisposable Activate()
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            var previous = access._submissionScope.Value; access._submissionScope.Value = this;
            return new SubmissionScope(access, previous);
        }
        internal bool IsActive => Volatile.Read(ref _disposed) == 0;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            lock (access._owners) if (ReferenceEquals(access._activeSdo, this)) access._activeSdo = null;
            access._sdoGate.Release();
        }
    }
    private sealed class SubmissionScope(CanopenConnectionAccess access, SdoLease? previous) : IDisposable
    { public void Dispose() => access._submissionScope.Value = previous; }

    /// <summary>只阻止原始请求插入正在执行的协议事务；不会把分开的CAN写/读推断成完整SDO。</summary>
    internal IDisposable? EnterSupplementalSubmission(bool hasStandardSdoRequest, CanConfirmationBudget? budget,
        CancellationToken ct, Action admission)
    {
        if (!hasStandardSdoRequest) return null;
        if (_submissionScope.Value is { } token)
        {
            lock (_owners) if (token.IsActive && ReferenceEquals(_activeSdo, token)) return null;
            throw new InvalidOperationException("原SDO事务身份已退出，禁止迟到提交");
        }
        admission(); CheckUsable();
        while (!_sdoGate.Wait(budget is { } limit ? Math.Min(50, limit.Remaining(ct)) : 50, ct))
        { admission(); CheckUsable(); }
        try { admission(); CheckUsable(); if (budget is { } remaining) _ = remaining.Remaining(ct); return new ReleaseGate(_sdoGate); }
        catch { _sdoGate.Release(); throw; }
    }

    internal void ReleaseNode(byte node, object owner)
    {
        lock (_owners)
        {
            if (_owners.TryGetValue(node, out var current) && ReferenceEquals(current, owner))
                _owners.Remove(node);
            if (_senderOwners.TryGetValue(node, out var senders) && senders.Remove(owner) && senders.Count == 0)
                _senderOwners.Remove(node);
        }
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
    internal async Task<CanopenReceivedFrame?> ReceiveNodeAsync(CanFrameInbox inbox, uint cobId, int timeoutMs, CancellationToken ct)
    {
        CheckUsable();
        var frame = await ((CanConnectionResource)_adapter).ReadNodeInboxAsync(inbox, cobId, timeoutMs, ct).ConfigureAwait(false);
        CheckUsable(); return frame == null ? null : CanopenReceivedFrame.From(frame);
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
        long started = Stopwatch.GetTimestamp();
        CheckUsable();
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "接收超时必须大于零");
        if (_adapter is CanConnectionResource resource)
        {
            if (!resource.Controlled) await Task.Run(() => resource.EnsureSharedReceive(timeoutMs, ct), CancellationToken.None).ConfigureAwait(false);
            int remaining = timeoutMs - (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (remaining <= 0) throw new TimeoutException("共享接收转换已耗尽CANopen接收配置期限");
            var routed = await resource.ReadCanopenAsync(cobId, remaining, ct).ConfigureAwait(false);
            if (routed != null && Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException("CANopen接收结果超过包含模式转换的配置总期限");
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

/// <summary>节点管理租约及独立反馈副本；普通补充操作共享事务门，释放不关闭父CAN。</summary>
public sealed class CanopenNodeAccess : IDisposable
{
    private readonly CanopenConnectionAccess _connection;
    private readonly CanopenSdoClient _client;
    private readonly byte _node;
    private readonly object _owner;
    private readonly CanFrameInbox? _inbox;
    private int _disposed;
    public int TimeoutMs { get; }
    internal CanopenNodeAccess(CanopenConnectionAccess connection, CanopenSdoClient client, byte node, object owner, int timeoutMs, CanFrameInbox? inbox = null)
        => (_connection, _client, _node, _owner, TimeoutMs, _inbox) = (connection, client, node, owner, timeoutMs, inbox);
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
        using var scope = transaction.Activate();
        await _connection.SendAsync(cobId, value, TimeoutMs, ct).ConfigureAwait(false);
    }
    public async Task<CanopenReceivedFrame?> ReceivePdoAsync(uint cobId, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        if (_inbox != null)
        {
            return await _connection.ReceiveNodeAsync(_inbox, cobId, TimeoutMs, ct).ConfigureAwait(false);
        }
        return await _connection.ReceiveAsync(cobId, TimeoutMs, ct).ConfigureAwait(false);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) { _inbox?.Dispose(); _connection.ReleaseNode(_node, _owner); }
    }
}
