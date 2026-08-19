using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>数据采集节点项</summary>
[MessagePackObject(true)]
public class OpcUaDataAcqItem : INotifyPropertyChanged
{
    private string _nodeId = "";
    private string _columnName = "";

    /// <summary>节点标识</summary>
    public string NodeId { get => _nodeId; set => SetProperty(ref _nodeId, value); }

    /// <summary>列名
    public string ColumnName { get => _columnName; set => SetProperty(ref _columnName, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

/// <summary>数据导出格式</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum DataAcqExportFormat
{
    Csv = 0,
    Variable = 1,
    Both = 2
}

/// <summary>OPC UA 数据采集启动步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaDataAcqStartSetting
{
    /// <summary>采集任务名称，用于在 Stop 步骤中引用</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"DataAcq1\"";

    /// <summary>OPC UA 连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"OpcUa1\"";

    /// <summary>要采集的节点列表</summary>
    public ObservableCollection<OpcUaDataAcqItem> Items { get; set; } = new();

    /// <summary>采样间隔（毫秒）</summary>
    public int SamplingIntervalMs { get; set; } = 100;

    /// <summary>最大采集时长（毫秒），0 表示无限制（需手动 Stop）</summary>
    public int MaxDurationMs { get; set; } = 0;

    /// <summary>FIFO 缓冲区容量（条数），缓冲满时采集溢出停止，需及时执行 DataAcq_Read 消费数据</summary>
    public int BufferSize { get; set; } = 10000;
}
