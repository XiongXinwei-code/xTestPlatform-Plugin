using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

public class CanWriteViewModel : INotifyPropertyChanged
{
    private bool _suppressSave;
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 5000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public void Flush() => _save.Flush();
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private CanWriteSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { Flush(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { Flush(); _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (CanWriteSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (CanWriteSetting)_serializer.CreateDefault();
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
    public string CanId { get => _setting?.CanId ?? ""; set { if (_setting == null || _setting.CanId == value) return; _setting.CanId = value; OnPropertyChanged(); QueueSave(); } }
    public int FrameType { get => (int)(_setting?.FrameType ?? CanFrameType.Standard); set { if (_setting == null) return; _setting.FrameType = (CanFrameType)value; OnPropertyChanged(); QueueSave(); } }
    public string Data { get => _setting?.Data ?? ""; set { if (_setting == null || _setting.Data == value) return; _setting.Data = value; OnPropertyChanged(); QueueSave(); } }
    public bool UseFdFrame { get => _setting?.UseFdFrame ?? false; set { if (_setting == null || _setting.UseFdFrame == value) return; _setting.UseFdFrame = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
