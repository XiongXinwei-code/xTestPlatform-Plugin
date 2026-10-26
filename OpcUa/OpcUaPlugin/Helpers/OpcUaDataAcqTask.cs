using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Models;
using PluginCommon;

namespace OpcUa.Helpers;

/// <summary>带质量、真实时间及健康诊断的轮询 FIFO；取消请求不等于底层读取退出。</summary>
public sealed class OpcUaDataAcqTask : IDisposable
{
    private readonly Func<CancellationToken, Task<DataValueCollection>> _read;
    private readonly List<OpcUaDataAcqItem> _items;
    private readonly int _samplingIntervalMs, _bufferSize;
    private readonly CancellationTokenSource _cts;
    private readonly CancellationToken _token;
    private readonly Task _loopTask;
    private readonly CancellationTokenRegistration _lifetimeRegistration;
    private readonly Timer? _durationTimer;
    private readonly ConcurrentQueue<DataAcqRecord> _buffer = new();
    private readonly SemaphoreSlim _consumer = new(1, 1);
    private readonly OpcUaConsumptionQueue _consumption;
    private readonly object _lifecycle = new();
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _cancelCallbacks = Task.CompletedTask;
    private Task<List<DataAcqRecord>>? _stopDrainTask;
    private int _disposeRequested;
    private int _loopStarted;
    private long _sequence, _lastSuccessUtcTicks, _lastFailureUtcTicks;
    private int _consecutiveFailures, _stopRequested;
    private string? _lastError;
    private Exception? _cancellationError;
    private volatile bool _overflowed;
    public string TaskName { get; }
    public bool IsRunning => !_token.IsCancellationRequested && !_loopTask.IsCompleted;
    public bool HasExited => Completion.IsCompleted;
    public bool IsStopRequested => Volatile.Read(ref _stopRequested) != 0;
    public Task Completion { get { lock (_lifecycle) return Task.WhenAll(_loopTask, _cancelCallbacks, _consumption.Completion, _stopDrainTask ?? Task.CompletedTask); } }
    /// <summary>只有显式冻结并释放后才完成；自然采集循环结束不代表缓冲消费资源已关闭。</summary>
    public Task CloseCompletion => _closed.Task;
    public int PendingConsumers => _consumption.PendingCount;
    public string? LastConsumptionError => _consumption.LastError;
    public long LastExportCommitSequence => _consumption.LastExportCommitSequence;
    internal Func<string, IReadOnlyList<OpcUaDataAcqItem>, IReadOnlyList<DataAcqRecord>, Task> CsvExporter { get; set; }
        = (path, items, records) => { AppendCsv(path, items, records.ToList()); return Task.CompletedTask; };
    public int SampleCount => _buffer.Count;
    public int SamplingIntervalMs => _samplingIntervalMs;
    public IReadOnlyList<OpcUaDataAcqItem> Items => _items;
    public bool HasOverflowed => _overflowed;
    public int ConsecutiveFailures => Volatile.Read(ref _consecutiveFailures);
    public string? LastError => Volatile.Read(ref _lastError);
    public Exception? CancellationError => Volatile.Read(ref _cancellationError);
    public DateTime? LastSuccessUtc => ReadUtc(ref _lastSuccessUtcTicks);
    public DateTime? LastFailureUtc => ReadUtc(ref _lastFailureUtcTicks);
    public string? BackgroundError => _loopTask.Exception?.GetBaseException().ToString();

    public OpcUaDataAcqTask(string taskName, Session session, List<OpcUaDataAcqItem> items, int samplingIntervalMs, int maxDurationMs, int bufferSize)
        : this(taskName, MakeReader(session, items), items, samplingIntervalMs, maxDurationMs, bufferSize) { }

    public OpcUaDataAcqTask(string taskName, Func<CancellationToken, Task<DataValueCollection>> read,
        List<OpcUaDataAcqItem> items, int samplingIntervalMs, int maxDurationMs, int bufferSize, CancellationToken lifetime = default)
    {
        if (samplingIntervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(samplingIntervalMs));
        if (maxDurationMs < 0) throw new ArgumentOutOfRangeException(nameof(maxDurationMs), "最大采集时长必须为 0 或正数");
        if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize), "FIFO 缓冲区容量必须大于 0");
        if (items.Count == 0) throw new ArgumentException("采集节点列表为空", nameof(items));
        foreach (var item in items) OpcUaHelper.ParseNodeId(item.NodeId);
        TaskName = taskName; _read = read;
        _items = items.Select(i => new OpcUaDataAcqItem { NodeId = i.NodeId, ColumnName = i.ColumnName }).ToList();
        _samplingIntervalMs = samplingIntervalMs; _bufferSize = bufferSize;
        _consumption = new(_buffer, _consumer);
        // 外部令牌和时长到期只请求异步取消，不能在宿主停止线程同步执行 SDK 的取消回调。
        _cts = new CancellationTokenSource(); _token = _cts.Token;
        // 此时快停止只接触完整的缓冲/CTS；不读取尚未赋值的采集 Task。
        // 退休步骤先冻结新 owner，再允许构造函数提交后台读取。
        ExecutionScopedExecutor.Own(RequestStop);
        _loopTask = Task.Run(CollectLoop, CancellationToken.None);
        Volatile.Write(ref _loopStarted, 1);
        _ = _loopTask.ContinueWith(t => { if (t.IsFaulted) _ = t.Exception; },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        _lifetimeRegistration = lifetime.UnsafeRegister(static state => ((OpcUaDataAcqTask)state!).RequestStop(), this);
        if (maxDurationMs > 0)
            _durationTimer = new Timer(static state => ((OpcUaDataAcqTask)state!).RequestReadCancellation(), this, maxDurationMs, Timeout.Infinite);
    }

    private static Func<CancellationToken, Task<DataValueCollection>> MakeReader(Session session, List<OpcUaDataAcqItem> items)
    {
        var nodes = new ReadValueIdCollection(items.Select(i => new ReadValueId { NodeId = NodeId.Parse(i.NodeId), AttributeId = Attributes.Value }));
        return async token => (await session.ReadAsync(null, 0, TimestampsToReturn.Both, nodes, token).ConfigureAwait(false)).Results;
    }

    private async Task CollectLoop()
    {
        while (!_token.IsCancellationRequested)
        {
            try
            {
                var values = await _read(_token).ConfigureAwait(false);
                _token.ThrowIfCancellationRequested();
                if (values == null || values.Count != _items.Count)
                    throw new InvalidOperationException($"OPC UA 采集响应数量不匹配：期望={_items.Count}，实际={values?.Count ?? 0}");
                var record = new DataAcqRecord
                {
                    Timestamp = DateTime.UtcNow, ReceiveMonotonicTicks = Stopwatch.GetTimestamp(),
                    Sequence = Interlocked.Increment(ref _sequence),
                    Values = values.Select(v => v.Value is Array array ? array.Clone() : v.Value).ToArray(),
                    StatusCodes = values.Select(v => v.StatusCode.Code).ToArray(),
                    SourceTimestamps = values.Select(v => v.SourceTimestamp).ToArray(),
                    ServerTimestamps = values.Select(v => v.ServerTimestamp).ToArray()
                };
                var unusable = values.Select((v, i) => (v, i)).Where(x => !StatusCode.IsGood(x.v.StatusCode)).ToArray();
                if (unusable.Length > 0)
                    RecordFailure(new InvalidOperationException("采集节点质量不是 Good：" + string.Join(", ", unusable.Select(x => $"{_items[x.i].NodeId}={x.v.StatusCode}"))));
                else { Interlocked.Exchange(ref _lastSuccessUtcTicks, DateTime.UtcNow.Ticks); Interlocked.Exchange(ref _consecutiveFailures, 0); }
                if (_buffer.Count >= _bufferSize) { _overflowed = true; break; }
                _buffer.Enqueue(record);
            }
            catch (OperationCanceledException) when (_token.IsCancellationRequested) { break; }
            catch (Exception ex) { RecordFailure(ex); }
            try { await Task.Delay(_samplingIntervalMs, _token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_token.IsCancellationRequested) { break; }
        }
    }

    private void RecordFailure(Exception ex)
    {
        Interlocked.Increment(ref _consecutiveFailures);
        Interlocked.Exchange(ref _lastFailureUtcTicks, DateTime.UtcNow.Ticks);
        Volatile.Write(ref _lastError, ex.ToString());
    }

    private static DateTime? ReadUtc(ref long ticks)
    { var value = Interlocked.Read(ref ticks); return value == 0 ? null : new DateTime(value, DateTimeKind.Utc); }

    /// <summary>转换或导出失败时不消费 FIFO，只有回调成功后才提交消费。</summary>
    public Task<T> ConsumeAsync<T>(int maxCount, Func<IReadOnlyList<DataAcqRecord>, Task<T>> consume, CancellationToken ct)
        => _consumption.RunAsync(maxCount, (records, _) => consume(records), Timeout.Infinite, ct);

    internal Task<T> ConsumeWithBudgetAsync<T>(int maxCount,
        Func<IReadOnlyList<DataAcqRecord>, OpcUaConsumptionQueue.Consumption, Task<T>> prepare,
        int timeoutMs, CancellationToken ct, Action<T>? publish = null)
        => _consumption.RunAsync(maxCount, prepare, timeoutMs, ct, publish);

    public List<DataAcqRecord> Read(int maxCount)
    {
        _consumer.Wait();
        try
        {
            var result = new List<DataAcqRecord>();
            while ((maxCount < 0 || result.Count < maxCount) && _buffer.TryDequeue(out var record)) result.Add(record);
            return result;
        }
        finally { _consumer.Release(); }
    }

    public void RequestStop()
    {
        lock (_lifecycle)
        {
            if (Interlocked.Exchange(ref _stopRequested, 1) != 0) return;
            _consumption.Freeze();
            RequestReadCancellation();
        }
    }

    private void RequestReadCancellation()
    {
        lock (_lifecycle)
        {
            if ((Volatile.Read(ref _loopStarted) != 0 && _loopTask.IsCompleted) || _cts.IsCancellationRequested) return;
            _cancelCallbacks = ObserveCancellationExitAsync(_cts.CancelAsync());
        }
    }

    private async Task ObserveCancellationExitAsync(Task callbacks)
    {
        try { await callbacks.ConfigureAwait(false); }
        catch (Exception ex)
        {
            Volatile.Write(ref _cancellationError, ex);
            RecordFailure(ex);
            Trace.TraceError($"OPC UA 采集 {TaskName} 的取消回调已退出但失败：{ex}");
        }
    }

    public async Task<List<DataAcqRecord>> StopAsync(int timeoutMs = 5000, CancellationToken ct = default)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        var watch = Stopwatch.StartNew();
        RequestStop();
        Task<List<DataAcqRecord>> actual;
        lock (_lifecycle)
        {
            if (_stopDrainTask == null)
            {
                var pending = Task.WhenAll(_loopTask, _cancelCallbacks, _consumption.Completion);
                _stopDrainTask = Task.Run(async () =>
                {
                    await pending.ConfigureAwait(false);
                    await _consumer.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        var records = new List<DataAcqRecord>();
                        while (_buffer.TryDequeue(out var record)) records.Add(record);
                        return records;
                    }
                    finally { _consumer.Release(); }
                }, CancellationToken.None);
                _ = _stopDrainTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
            actual = _stopDrainTask;
        }
        try
        {
            var records = await actual.WaitAsync(Remaining(), ct).ConfigureAwait(false);
            if (watch.Elapsed.TotalMilliseconds >= timeoutMs) throw new TimeoutException();
            return records;
        }
        catch (TimeoutException ex)
        { throw new TimeoutException($"OPC UA 采集 {TaskName} 在 {timeoutMs} ms 内停止未确认，读取、消费或取消回调尚未退出；保留任务引用与缓冲", ex); }

        TimeSpan Remaining() => TimeSpan.FromMilliseconds(Math.Max(0, timeoutMs - watch.Elapsed.TotalMilliseconds));
    }

    public static void AppendCsv(string filePath, IReadOnlyList<OpcUaDataAcqItem> items, List<DataAcqRecord> records)
    {
        string Escape(string text) => text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        var header = new List<string> { "Timestamp", "Sequence", "ReceiveMonotonicTicks" };
        foreach (var item in items)
        {
            var name = string.IsNullOrWhiteSpace(item.ColumnName) ? item.NodeId : item.ColumnName;
            header.AddRange([name, name + ".StatusCode", name + ".SourceTimestampUtc", name + ".ServerTimestampUtc"]);
        }
        var headerLine = string.Join(',', header.Select(Escape));
        var body = new StringBuilder();
        foreach (var record in records)
        {
            if (record.Values.Length != items.Count) throw new InvalidOperationException("CSV 记录列数与采集配置不一致");
            var row = new List<string> { record.Timestamp.ToString("O"), record.Sequence.ToString(CultureInfo.InvariantCulture), record.ReceiveMonotonicTicks.ToString(CultureInfo.InvariantCulture) };
            for (int i = 0; i < items.Count; i++)
            {
                row.Add(Convert.ToString(record.Values[i], CultureInfo.InvariantCulture) ?? "");
                row.Add(i < record.StatusCodes.Length ? $"0x{record.StatusCodes[i]:X8}" : "Unknown");
                row.Add(i < record.SourceTimestamps.Length && record.SourceTimestamps[i] != DateTime.MinValue ? record.SourceTimestamps[i].ToString("O") : "");
                row.Add(i < record.ServerTimestamps.Length && record.ServerTimestamps[i] != DateTime.MinValue ? record.ServerTimestamps[i].ToString("O") : "");
            }
            body.AppendLine(string.Join(',', row.Select(Escape)));
        }
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (dir != null) Directory.CreateDirectory(dir);
        using var stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        if (stream.Length > 0)
        {
            var bom = new byte[3];
            int prefixCount = stream.Read(bom, 0, bom.Length);
            stream.Position = prefixCount == 3 && bom.AsSpan().SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
            var expected = Encoding.UTF8.GetBytes(headerLine);
            var actual = new byte[expected.Length];
            bool matches = stream.Length - stream.Position >= expected.Length;
            if (matches) { stream.ReadExactly(actual); matches = actual.AsSpan().SequenceEqual(expected); }
            int newline = matches ? stream.ReadByte() : -1;
            matches = matches && (newline == '\n' || (newline == '\r' && stream.ReadByte() == '\n'));
            if (!matches)
                throw new InvalidOperationException("CSV 表头与当前质量/时间列不一致，请使用新文件，旧文件不会被改写");
        }
        bool empty = stream.Length == 0;
        stream.Seek(0, SeekOrigin.End);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        if (empty) writer.WriteLine(headerLine);
        writer.Write(body);
    }

    public void Dispose()
    {
        RequestStop();
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0) return;
        var cleanup = Task.Run(async () =>
        {
            try { await Completion.ConfigureAwait(false); }
            catch (Exception ex) { Trace.TraceError($"OPC UA 采集实际收尾失败：{ex}"); }
            finally
            {
                if (_durationTimer != null) await _durationTimer.DisposeAsync().ConfigureAwait(false);
                await _lifetimeRegistration.DisposeAsync().ConfigureAwait(false);
                _cts.Dispose();
            }
        });
        _ = cleanup.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _closed.TrySetException(t.Exception!.InnerExceptions);
                Trace.TraceError($"OPC UA 采集 {TaskName} 的释放失败：{t.Exception}");
            }
            else if (t.IsCanceled) _closed.TrySetCanceled();
            else _closed.TrySetResult();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}

/// <summary>Timestamp 是主机接收 UTC，不代替节点源时间；轮询不代表等间隔硬件采样。</summary>
public sealed class DataAcqRecord
{
    public DateTime Timestamp { get; set; }
    public long Sequence { get; set; }
    public long ReceiveMonotonicTicks { get; set; }
    public string TimestampDomain { get; set; } = "HostReceiveUtc/Stopwatch; OPC-UA.SourceUtc";
    public object?[] Values { get; set; } = [];
    public uint[] StatusCodes { get; set; } = [];
    public DateTime[] SourceTimestamps { get; set; } = [];
    public DateTime[] ServerTimestamps { get; set; } = [];
}
