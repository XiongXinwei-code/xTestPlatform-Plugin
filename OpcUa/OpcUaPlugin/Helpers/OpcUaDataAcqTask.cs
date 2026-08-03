using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Models;

namespace OpcUa.Helpers;

/// <summary>OPC UA 后台数据采集任务，管理定时轮询和数据缓冲</summary>
public sealed class OpcUaDataAcqTask : IDisposable
{
    private readonly Session _session;
    private readonly List<OpcUaDataAcqItem> _items;
    private readonly int _samplingIntervalMs;
    private readonly CancellationTokenSource _cts;
    private readonly Task _loopTask;
    private readonly ConcurrentQueue<DataAcqRecord> _buffer = new();
    private readonly DateTime _startTime;

    public string TaskName { get; }
    public bool IsRunning => !_cts.IsCancellationRequested;
    public int SampleCount => _buffer.Count;

    public OpcUaDataAcqTask(string taskName, Session session, List<OpcUaDataAcqItem> items, int samplingIntervalMs, int maxDurationMs)
    {
        TaskName = taskName;
        _session = session;
        _items = items;
        _samplingIntervalMs = samplingIntervalMs;
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

    /// <summary>停止采集并返回所有缓冲数据</summary>
    public async Task<List<DataAcqRecord>> StopAsync()
    {
        _cts.Cancel();
        try { await _loopTask; } catch (OperationCanceledException) { }

        var results = new List<DataAcqRecord>();
        while (_buffer.TryDequeue(out var record))
            results.Add(record);
        return results;
    }

    /// <summary>导出数据为 CSV 文件</summary>
    public static void ExportToCsv(string filePath, List<OpcUaDataAcqItem> items, List<DataAcqRecord> records)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var sb = new StringBuilder();

        // 表头
        sb.Append("Timestamp");
        foreach (var item in items)
            sb.Append(',').Append(string.IsNullOrWhiteSpace(item.ColumnName) ? item.NodeId : item.ColumnName);
        sb.AppendLine();

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

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(false));
    }

    /// <summary>计算统计值（均值、最大值、最小值）</summary>
    public static DataAcqStatistics CalculateStatistics(int columnIndex, List<DataAcqRecord> records)
    {
        var stats = new DataAcqStatistics();
        var values = new List<double>();

        foreach (var record in records)
        {
            if (record.Values[columnIndex] != null &&
                double.TryParse(Convert.ToString(record.Values[columnIndex], CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            {
                values.Add(v);
            }
        }

        if (values.Count > 0)
        {
            stats.Average = values.Average();
            stats.Max = values.Max();
            stats.Min = values.Min();
            stats.Count = values.Count;
        }

        return stats;
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

/// <summary>采集统计结果</summary>
public class DataAcqStatistics
{
    public double Average { get; set; }
    public double Max { get; set; }
    public double Min { get; set; }
    public int Count { get; set; }
}
