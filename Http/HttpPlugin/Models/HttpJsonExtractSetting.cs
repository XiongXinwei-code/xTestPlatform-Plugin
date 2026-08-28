using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Http.Models;

/// <summary>
/// 提取映射项，把源文档中某个路径的值写入指定变量
/// </summary>
[MessagePackObject(true)]
public class HttpExtractItem : INotifyPropertyChanged
{
    private string _path = string.Empty;
    private string _targetVariable = string.Empty;
    private string _defaultValue = string.Empty;

    /// <summary>提取路径，JSON 为点号路径，XML 为 XPath</summary>
    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    /// <summary>提取结果写入的变量名</summary>
    [VariablePathField]
    public string TargetVariable
    {
        get => _targetVariable;
        set => SetProperty(ref _targetVariable, value);
    }

    /// <summary>路径未命中时写入的默认值</summary>
    public string DefaultValue
    {
        get => _defaultValue;
        set => SetProperty(ref _defaultValue, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// JSON 提取步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class HttpJsonExtractSetting
{
    /// <summary>待解析的 JSON 文本（通常填写存放响应体的变量名表达式）</summary>
    [ExpressionField]
    public string SourceJson { get; set; } = "HttpResponse";

    /// <summary>提取映射列表</summary>
    public ObservableCollection<HttpExtractItem> Items { get; set; } = [];

    /// <summary>任一路径未命中时是否判定步骤失败</summary>
    public bool FailOnMissingPath { get; set; } = true;
}
