using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>批量写入的单个节点项</summary>
[MessagePackObject(true)]
public class OpcUaBatchWriteItem : INotifyPropertyChanged
{
    private string _nodeId = "";
    private string _writeValue = "";
    private OpcUaDataType _dataType = OpcUaDataType.Auto;

    /// <summary>节点标识</summary>
    [ExpressionField]
    public string NodeId { get => _nodeId; set => SetProperty(ref _nodeId, value); }

    /// <summary>要写入的值（表达式）</summary>
    [ExpressionField]
    public string WriteValue { get => _writeValue; set => SetProperty(ref _writeValue, value); }

    /// <summary>数据类型</summary>
    public OpcUaDataType DataType { get => _dataType; set => SetProperty(ref _dataType, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

/// <summary>OPC UA 批量写入步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaBatchWriteSetting
{
    /// <summary>连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "OpcUa1";

    /// <summary>要写入的节点列表</summary>
    public ObservableCollection<OpcUaBatchWriteItem> Items { get; set; } = new();

    /// <summary>超时时间（毫秒）</summary>
    public int TimeoutMs { get; set; } = 5000;
}
