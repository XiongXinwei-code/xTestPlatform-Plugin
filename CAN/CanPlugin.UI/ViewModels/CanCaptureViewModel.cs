using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

public sealed class CanCaptureViewModel : INotifyPropertyChanged
{
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public Action<string, Action>? ExecuteCommand { get => _save.ExecuteCommand; set => _save.ExecuteCommand = value; }
    private IStepSettingSerializer? _serializer;
    private Step? _step;
    private CanCaptureSetting? _setting;
    public void Flush() => _save.Flush();
    public void Attach(Step step)
    {
        Flush(); _step = step; _serializer = new CanCapturePlugin().CreateSerializer();
        _setting = step.StepSetting.Setting is { Length: > 0 } data
            ? (CanCaptureSetting)_serializer.Deserialize(data, step.StepSetting.SettingVersion) : (CanCaptureSetting)_serializer.CreateDefault();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
    private void Changed([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (_step != null && _setting != null && _serializer != null) _save.Schedule(_step, _serializer, _setting);
    }
    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting != null && _setting.ConnectionName != value) { _setting.ConnectionName = value; Changed(); } } }
    public string FilterIds { get => _setting?.FilterIds ?? ""; set { if (_setting != null && _setting.FilterIds != value) { _setting.FilterIds = value; Changed(); } } }
    public int DurationMs { get => _setting?.DurationMs ?? 1000; set { if (_setting != null && _setting.DurationMs != value) { _setting.DurationMs = value; Changed(); } } }
    public int SetupTimeoutMs { get => _setting?.SetupTimeoutMs ?? 5000; set { if (_setting != null && _setting.SetupTimeoutMs != value) { _setting.SetupTimeoutMs = value; Changed(); } } }
    public int MaxFrames { get => _setting?.MaxFrames ?? 10000; set { if (_setting != null && _setting.MaxFrames != value) { _setting.MaxFrames = value; Changed(); } } }
    public string ResultVariable { get => _setting?.ResultVariable ?? ""; set { if (_setting != null && _setting.ResultVariable != value) { _setting.ResultVariable = value; Changed(); } } }
    public bool ExportEnabled { get => _setting?.ExportEnabled ?? false; set { if (_setting != null && _setting.ExportEnabled != value) { _setting.ExportEnabled = value; Changed(); } } }
    public string ExportFilePath { get => _setting?.ExportFilePath ?? "\"\""; set { if (_setting != null && _setting.ExportFilePath != value) { _setting.ExportFilePath = value; Changed(); } } }
    public bool OverwriteExportFile { get => _setting?.OverwriteExportFile ?? false; set { if (_setting != null && _setting.OverwriteExportFile != value) { _setting.OverwriteExportFile = value; Changed(); } } }
    public int ExportTimeoutMs { get => _setting?.ExportTimeoutMs ?? 5000; set { if (_setting != null && _setting.ExportTimeoutMs != value) { _setting.ExportTimeoutMs = value; Changed(); } } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
