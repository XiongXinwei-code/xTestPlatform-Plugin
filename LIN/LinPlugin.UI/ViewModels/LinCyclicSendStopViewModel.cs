using System.ComponentModel;
using System.Runtime.CompilerServices;
using LIN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.ViewModels;

public class LinCyclicSendStopViewModel : INotifyPropertyChanged
{
    private bool _suppressSave;
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 3000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public void Flush() => _save.Flush();
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private LinCyclicSendStopSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { Flush(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { Flush(); _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (LinCyclicSendStopSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (LinCyclicSendStopSetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _save.Schedule(_step, _serializer, _setting);
    }

    public string TaskName
    {
        get => _setting?.TaskName ?? string.Empty;
        set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
