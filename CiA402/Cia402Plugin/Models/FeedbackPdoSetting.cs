using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;

namespace CiA402.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public sealed class FeedbackPdoSetting : INotifyPropertyChanged
{
    private int _number = 1;
    private string _layout = "0x6041:0:16,0x6064:0:32";

    public int Number
    {
        get => _number;
        set => Set(ref _number, value);
    }

    public string Layout
    {
        get => _layout;
        set => Set(ref _layout, value);
    }

    // 平台助手读取集合项时保留全部字段；界面与执行仍使用同一设置对象。
    public override string ToString() => System.Text.Json.JsonSerializer.Serialize(this, GetType());

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new(name));
    }
}
