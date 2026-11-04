using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

public sealed class CanReceiveMappingRow(CanReceiveMapping item, Action changed) : INotifyPropertyChanged
{
    public CanReceiveMapping Item { get; } = item;
    public bool Enabled { get => Item.Enabled; set { if (Item.Enabled != value) { Item.Enabled = value; Changed(); } } }
    public string CanId { get => Item.CanId; set { if (Item.CanId != value) { Item.CanId = value; Changed(); } } }
    public int FrameType { get => (int)Item.FrameType; set { if ((int)Item.FrameType != value) { Item.FrameType = (CanFrameType)value; Changed(); } } }
    public int FrameFormat { get => (int)Item.FrameFormat; set { if ((int)Item.FrameFormat != value) { Item.FrameFormat = (CanReceiveFrameFormat)value; Changed(); } } }
    public string DataVariable { get => Item.DataVariable; set { if (Item.DataVariable != value) { Item.DataVariable = value; Changed(); } } }
    private void Changed([CallerMemberName] string? name = null) { PropertyChanged?.Invoke(this, new(name)); changed(); }
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>背景任务编辑器共用保存机制，步骤描述符仍各自独立。旧页面行不能写回新步骤。</summary>
public sealed class CanBackgroundViewModel(IStepPlugin plugin) : INotifyPropertyChanged
{
    private readonly IStepSettingSerializer _serializer = plugin.CreateSerializer();
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public Action<string, Action>? ExecuteCommand { get => _save.ExecuteCommand; set => _save.ExecuteCommand = value; }
    private Step? _step;
    private Step? _lastAttachedStep;
    private object? _setting;
    private long _generation;
    public bool IsStart => _setting is CanReadBackgroundSetting;
    public bool IsStop => _setting is CanReadBackgroundStopSetting;
    public bool IsStatus => _setting is CanTaskStatusSetting;
    public bool HasResult => IsStart || IsStatus;
    public string ResultLabel => IsStatus ? "输出映射变量：" : "JSON 结果变量：";
    public string ResultHelp => IsStatus
        ? "固定输出完整 JSON 字符串。填写已声明的 String 变量完整路径，不加引号，例如 Locals.CanTasksJson。SendTasks 为发送任务，ReceiveTasks 为监听任务；完整字段、示例及 ID 对应方法见步骤说明。"
        : "已声明的字符串变量完整路径，不加引号，例如 Locals.CanLatest；总线监听配置了逐 ID 映射时可留空。";
    public string Title => IsStatus ? "CAN 后台任务状态" : IsStop ? plugin is CAN.Dbc.DbcReadBackgroundStopPlugin ? "停止 DBC 总线监听" : "停止 CAN 总线监听" : "CAN 总线监听";
    public string? IconPath => plugin.IconPath;
    public string Help => IsStatus
        ? "查询当前连接的周期发送和总线监听任务。任务名称为空字符串表达式时查询全部；包含非活动任务可保留故障及停止诊断。每次覆盖 JSON 字符串，不输出 LIST。按任务类别、TaskName 和报文 ID/帧类型关联，不依赖数组位置；成功查询不代表后台健康。"
        : IsStop ? "填写启动步骤相同的连接与任务名称。停止会等待总线监听退出；成功后该任务不再更新变量。此步骤不会关闭 CAN 连接，也不会停止其他任务。"
        : "启动后立即返回，总线监听持续获取每个 ID 的最新数据，并覆盖已声明的字符串变量（十六进制字节）。发布间隔用于合并最新值，不是总线采样周期；不保存历史报文。可只输出 JSON 或逐 ID 映射；使用相同连接/任务名称的总线监听停止步骤收尾。";
    public ObservableCollection<CanReceiveMappingRow> Mappings { get; } = [];
    public CanReceiveMappingRow? SelectedMapping { get; set; }
    public void Flush() => _save.Flush();
    public void Attach(Step step)
    {
        Flush(); _generation++; _step = null; _setting = null; _lastAttachedStep = step;
        _setting = step.StepSetting.Setting is { Length: > 0 } bytes
            ? _serializer.Deserialize(bytes, step.StepSetting.SettingVersion) : _serializer.CreateDefault();
        _step = step;
        RefreshRows(); Notify(string.Empty);
    }
    public void Activate()
    {
        if (_step == null && _lastAttachedStep is { } step) Attach(step);
    }
    public void Deactivate()
    {
        Flush(); _generation++; _step = null; _setting = null;
        RefreshRows(); Notify(string.Empty);
    }
    private void RefreshRows()
    {
        Mappings.Clear(); SelectedMapping = null;
        if (_setting is CanReadBackgroundSetting setting)
            foreach (var item in setting.Mappings) AddRow(item);
    }
    private void AddRow(CanReceiveMapping item)
    {
        long generation = _generation; var setting = _setting;
        Mappings.Add(new(item, () => { if (generation == _generation && ReferenceEquals(setting, _setting)) Changed(nameof(Mappings)); }));
    }
    public void AddMapping()
    {
        if (_setting is not CanReadBackgroundSetting setting || setting.Mappings.Count >= 128) return;
        var item = new CanReceiveMapping(); setting.Mappings.Add(item); AddRow(item); Changed(nameof(Mappings));
    }
    public void RemoveMapping()
    {
        if (_setting is not CanReadBackgroundSetting setting || SelectedMapping is not { } selected || !setting.Mappings.Contains(selected.Item)) return;
        setting.Mappings.Remove(selected.Item); Mappings.Remove(selected); SelectedMapping = null; Changed(nameof(Mappings));
    }
    public string ConnectionName
    {
        get => _setting switch { CanReadBackgroundSetting s => s.ConnectionName, CanReadBackgroundStopSetting s => s.ConnectionName, CanTaskStatusSetting s => s.ConnectionName, _ => "" };
        set { if (ConnectionName == value) return; switch (_setting) { case CanReadBackgroundSetting s: s.ConnectionName = value; break; case CanReadBackgroundStopSetting s: s.ConnectionName = value; break; case CanTaskStatusSetting s: s.ConnectionName = value; break; default: return; } Changed(); }
    }
    public string TaskName
    {
        get => _setting switch { CanReadBackgroundSetting s => s.TaskName, CanReadBackgroundStopSetting s => s.TaskName, CanTaskStatusSetting s => s.TaskName, _ => "" };
        set { if (TaskName == value) return; switch (_setting) { case CanReadBackgroundSetting s: s.TaskName = value; break; case CanReadBackgroundStopSetting s: s.TaskName = value; break; case CanTaskStatusSetting s: s.TaskName = value; break; default: return; } Changed(); }
    }
    public int TimeoutMs
    {
        get => _setting switch { CanReadBackgroundSetting s => s.TimeoutMs, CanReadBackgroundStopSetting s => s.TimeoutMs, CanTaskStatusSetting s => s.TimeoutMs, _ => 5000 };
        set { if (TimeoutMs == value) return; switch (_setting) { case CanReadBackgroundSetting s: s.TimeoutMs = value; break; case CanReadBackgroundStopSetting s: s.TimeoutMs = value; break; case CanTaskStatusSetting s: s.TimeoutMs = value; break; default: return; } Changed(); }
    }
    public string ResultVariable
    {
        get => _setting switch { CanReadBackgroundSetting s => s.ResultVariable, CanTaskStatusSetting s => s.ResultVariable, _ => "" };
        set { if (ResultVariable == value) return; switch (_setting) { case CanReadBackgroundSetting s: s.ResultVariable = value; break; case CanTaskStatusSetting s: s.ResultVariable = value; break; default: return; } Changed(); }
    }
    public int PublishIntervalMs { get => (_setting as CanReadBackgroundSetting)?.PublishIntervalMs ?? 20; set { if (_setting is CanReadBackgroundSetting s && s.PublishIntervalMs != value) { s.PublishIntervalMs = value; Changed(); } } }
    public int MaxAgeMs { get => (_setting as CanReadBackgroundSetting)?.MaxAgeMs ?? 1000; set { if (_setting is CanReadBackgroundSetting s && s.MaxAgeMs != value) { s.MaxAgeMs = value; Changed(); } } }
    public bool IncludeInactive { get => (_setting as CanTaskStatusSetting)?.IncludeInactive ?? true; set { if (_setting is CanTaskStatusSetting s && s.IncludeInactive != value) { s.IncludeInactive = value; Changed(); } } }
    public bool IgnoreIfMissing { get => (_setting as CanReadBackgroundStopSetting)?.IgnoreIfMissing ?? false; set { if (_setting is CanReadBackgroundStopSetting s && s.IgnoreIfMissing != value) { s.IgnoreIfMissing = value; Changed(); } } }
    private void Changed([CallerMemberName] string? name = null)
    {
        Notify(name);
        if (_step != null && _setting != null) _save.Schedule(_step, _serializer, _setting);
    }
    private void Notify(string? name) => PropertyChanged?.Invoke(this, new(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}
