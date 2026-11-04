using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Http.Models;

/// <summary>
/// HTTP 默认请求头项，随客户端创建后附加到每次请求
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public class HttpHeaderItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _value = string.Empty;

    /// <summary>请求头名称，例如 X-Api-Key</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>请求头值（支持表达式）</summary>
    [ExpressionField]
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    // 平台助手读取集合项时保留全部字段；界面与执行仍使用同一设置对象。
    public override string ToString() => System.Text.Json.JsonSerializer.Serialize(this, GetType());

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
