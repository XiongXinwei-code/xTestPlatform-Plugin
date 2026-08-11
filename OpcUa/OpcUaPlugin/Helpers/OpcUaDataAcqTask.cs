using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Models;

namespace OpcUa.Helpers;

/// <summary>OPC UA 后台数据采集任务，管理定时轮询和有界 FIFO 数据缓冲（仿硬件采集卡模式）</summary>
public sealed class OpcUaDataAcqTask : IDisposable
{
    private readonly Session _session;
    private readonly List<OpcUaDataAcqItem> _items;
    private readonly int _samplingIntervalMs;
    private readonly int _bufferSize;
    private readonly CancellationTokenSource _cts;
    private readonly Task _loopTask;
    private readonly ConcurrentQueue<DataAcqRecord> _buffer = new();
    private readonly DateTime _startTime;

    public string TaskName { get; }
    public bool IsRunning => !_cts.IsCancellationRequested;
    public int SampleCount => _buffer.Count;
    public int SamplingIntervalMs => _samplingIntervalMs;
    public IReadOnlyList<OpcUaDataAcqItem> Items => _items;

    /// <summary>缓冲区是否已溢出（溢出后采集停止，与硬件 FIFO 溢出行为一致）</summary>
    public bool HasOverflowed { get; private set; }

    public OpcUaDataAcqTask(string taskName, Session session, List<OpcUaDataAcqItem> items, int samplingIntervalMs, int maxDurationMs, int bufferSize)
    {
        TaskName = taskName;
        _session = session;
        _items = items;
        _samplingIntervalMs = samplingIntervalMs;
        _bufferSize = bufferSize > 0 ? bufferSize : 10000;
        _cts = new CancellationTokenSource();
        _startTime = DateTime.Now;

        if (maxDurationMs > 0)
            _cts.CancelAfter(maxDurationMs);

        _loopTask = Task.Run(() => CollectLoop(_cts.Token));
    }

    private async Task CollectLoop(CancellationToken ct)
    {
        // 构建读取节点列表
        var nodesToRead = new ReadValueIdCollection();
        foreach (var item in _items)
        {
            nodesToRead.Add(new ReadValueId
            {
                NodeId = NodeId.Parse(item.NodeId),
                AttributeId = Attributes.Value
            });
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var response = await _session.ReadAsync(null, 0, TimestampsToReturn.Both, nodesToRead, ct);

                var record = new DataAcqRecord
                {
                    Timestamp = DateTime.Now,
                    Values = new object?[_items.Count]
                };

                for (int i = 0; i < _items.Count; i++)
                {
                    record.Values[i] = response.Results[i].Value;
                }

                // 缓冲满则溢出：停止采集，由 Read 步骤报错（仿硬件 FIFO 溢出）
                if (_buffer.Count >= _bufferSize)
                {
                    HasOverflowed = true;
                    break;
                }

                _buffer.Enqueue(record);

                await Task.Delay(_samplingIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // 采集出错时跳过本次，继续下一次
                await Task.Delay(_samplingIntervalMs, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>从 FIFO 缓冲中取出（消费）数据，maxCount 为 -1 时取出当前全部可用数据</summary>
    public List<DataAcqRecord> Read(int maxCount)
    {
        var results = new List<DataAcqRecord>();
        while ((maxCount < 0 || results.Count < maxCount) && _buffer.TryDequeue(out var record))
            results.Add(record);
        return results;
    }

    /// <summary>停止采集并返回缓冲中未被消费的残留数据</summary>
    public async Task<List<DataAcqRecord>> StopAsync()
    {
        _cts.Cancel();
        try { await _loopTask; } catch (OperationCanceledException) { }

        var results = new List<DataAcqRecord>();
        while (_buffer.TryDequeue(out var record))
            results.Add(record);
        return results;
    }

    /// <summary>追加写入 CSV 文件，文件不存在时先写入表头</summary>
    public static void AppendCsv(string filePath, IReadOnlyList<OpcUaDataAcqItem> items, List<DataAcqRecord> records)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var sb = new StringBuilder();

        // 表头（仅首次写入）
        if (!File.Exists(filePath))
        {
            sb.Append("Timestamp");
            foreach (var item in items)
                sb.Append(',').Append(string.IsNullOrWhiteSpace(item.ColumnName) ? item.NodeId : item.ColumnName);
            sb.AppendLine();
        }

        // 数据行
        foreach (var record in records)
        {
            sb.Append(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            for (int i = 0; i < items.Count; i++)
            {
                sb.Append(',');
                if (record.Values[i] != null)
                    sb.Append(Convert.ToString(record.Values[i], CultureInfo.InvariantCulture));
            }
            sb.AppendLine();
        }

        File.AppendAllText(filePath, sb.ToString(), new UTF8Encoding(false));
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

/// <summary>单条采集记录</summary>
public class DataAcqRecord
{
    public DateTime Timestamp { get; set; }
    public object?[] Values { get; set; } = Array.Empty<object?>();
}
