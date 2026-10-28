using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.ViewModels;

public class OpcUaDataAcqReadViewModel : INotifyPropertyChanged
{
    private readonly EditorSaveCoordinator _save = new();
    public Exception? LastSaveError => _save.LastError;
    public void FlushPendingSave() => _save.Flush();
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private OpcUaDataAcqReadSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { FlushPendingSave(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { FlushPendingSave(); _step = step; Load(); }

    private void Load()
    {
        _save.Detach();
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (OpcUaDataAcqReadSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (OpcUaDataAcqReadSetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        var step = _step; var setting = _setting; var serializer = _serializer;
        _save.Queue(() => step.StepSetting.Setting = serializer.Serialize(setting));
    }

    public string TaskName { get => _setting?.TaskName ?? ""; set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); } }
    public int SamplesToRead { get => _setting?.SamplesToRead ?? -1; set { if (_setting == null || _setting.SamplesToRead == value) return; _setting.SamplesToRead = value; OnPropertyChanged(); QueueSave(); } }
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 5000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public string ResultVariable { get => _setting?.ResultVariable ?? ""; set { if (_setting == null || _setting.ResultVariable == value) return; _setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string RecordsJsonVariable { get => _setting?.RecordsJsonVariable ?? ""; set { if (_setting == null || _setting.RecordsJsonVariable == value) return; _setting.RecordsJsonVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string HealthVariable { get => _setting?.HealthVariable ?? ""; set { if (_setting == null || _setting.HealthVariable == value) return; _setting.HealthVariable = value; OnPropertyChanged(); QueueSave(); } }
    public bool SaveToFile { get => _setting?.SaveToFile ?? false; set { if (_setting == null || _setting.SaveToFile == value) return; _setting.SaveToFile = value; OnPropertyChanged(); QueueSave(); } }
    public string CsvFilePath { get => _setting?.CsvFilePath ?? ""; set { if (_setting == null || _setting.CsvFilePath == value) return; _setting.CsvFilePath = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
