using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

public class CanReadViewModel : INotifyPropertyChanged
{
    private bool _suppressSave;
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public Action<string, Action>? ExecuteCommand { get => _save.ExecuteCommand; set => _save.ExecuteCommand = value; }
    public void Flush() => _save.Flush();
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private CanReadSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { Flush(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { Flush(); _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (CanReadSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (CanReadSetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _save.Schedule(_step, _serializer, _setting);
    }

    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public string FilterId { get => _setting?.FilterId ?? ""; set { if (_setting == null || _setting.FilterId == value) return; _setting.FilterId = value; OnPropertyChanged(); QueueSave(); } }
    public int ReadMode { get => (int)(_setting?.ReadMode ?? CanReadMode.FirstMatching); set { if (_setting == null || (int)_setting.ReadMode == value) return; _setting.ReadMode = (CanReadMode)value; OnPropertyChanged(); OnPropertyChanged(nameof(ResultHelp)); QueueSave(); } }
    public string ResultHelp => ReadMode == (int)CanReadMode.EachId
        ? "每个 ID 各读一帧：结果写入带 ID 的 JSON；ID 变量为逗号分隔的实际接收顺序。一个总期限，缺失任一 ID 时不发布部分结果。"
        : "首个匹配帧：结果写入十六进制数据字符串；ID 变量保存实际接收的 ID。多个筛选 ID 中任意一个匹配即完成。";
    public int ReadTimeoutMs { get => _setting?.ReadTimeoutMs ?? 1000; set { if (_setting == null || _setting.ReadTimeoutMs == value) return; _setting.ReadTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public string ResultVariable { get => _setting?.ResultVariable ?? ""; set { if (_setting == null || _setting.ResultVariable == value) return; _setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string IdVariable { get => _setting?.IdVariable ?? ""; set { if (_setting == null || _setting.IdVariable == value) return; _setting.IdVariable = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
