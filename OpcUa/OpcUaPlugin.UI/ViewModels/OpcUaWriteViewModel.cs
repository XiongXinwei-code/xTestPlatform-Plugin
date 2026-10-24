using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.ViewModels;

public class OpcUaWriteViewModel : INotifyPropertyChanged
{
    private readonly EditorSaveCoordinator _save = new();
    public Exception? LastSaveError => _save.LastError;
    public void FlushPendingSave() => _save.Flush();
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private OpcUaWriteSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { FlushPendingSave(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { FlushPendingSave(); _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (OpcUaWriteSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (OpcUaWriteSetting)_serializer.CreateDefault();
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

    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public string NodeId { get => _setting?.NodeId ?? ""; set { if (_setting == null || _setting.NodeId == value) return; _setting.NodeId = value; OnPropertyChanged(); QueueSave(); } }
    public string WriteValue { get => _setting?.WriteValue ?? ""; set { if (_setting == null || _setting.WriteValue == value) return; _setting.WriteValue = value; OnPropertyChanged(); QueueSave(); } }
    public int DataType { get => (int)(_setting?.DataType ?? OpcUaDataType.Auto); set { if (_setting == null) return; _setting.DataType = (OpcUaDataType)value; OnPropertyChanged(); QueueSave(); } }
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 3000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
