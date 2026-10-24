using System.ComponentModel;
using System.Runtime.CompilerServices;
using LIN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.ViewModels;

public class LinOpenViewModel : INotifyPropertyChanged
{
    private bool _suppressSave;
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public void Flush() => _save.Flush();
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private LinOpenSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { Flush(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { Flush(); _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (LinOpenSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (LinOpenSetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _save.Schedule(_step, _serializer, _setting);
    }

    public int AdapterType
    {
        get => (int)(_setting?.AdapterType ?? LinAdapterType.NI);
        set { if (_setting == null) return; _setting.AdapterType = (LinAdapterType)value; OnPropertyChanged(); QueueSave(); }
    }
    public string Channel
    {
        get => _setting?.Channel ?? string.Empty;
        set { if (_setting == null || _setting.Channel == value) return; _setting.Channel = value; OnPropertyChanged(); QueueSave(); }
    }
    public int BaudRate
    {
        get => _setting?.BaudRate ?? 19200;
        set { if (_setting == null || _setting.BaudRate == value) return; _setting.BaudRate = value; OnPropertyChanged(); QueueSave(); }
    }
    public int LinVersion
    {
        get => (int)(_setting?.LinVersion ?? LinVersionType.LIN_2x);
        set { if (_setting == null) return; _setting.LinVersion = (LinVersionType)value; OnPropertyChanged(); QueueSave(); }
    }
    public bool IsMaster
    {
        get => _setting?.IsMaster ?? true;
        set { if (_setting == null || _setting.IsMaster == value) return; _setting.IsMaster = value; OnPropertyChanged(); QueueSave(); }
    }
    public int RxQueueSize
    {
        get => _setting?.RxQueueSize ?? 512;
        set { if (_setting == null || _setting.RxQueueSize == value) return; _setting.RxQueueSize = value; OnPropertyChanged(); QueueSave(); }
    }
    public string ConnectionName
    {
        get => _setting?.ConnectionName ?? string.Empty;
        set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
    }

    public int TimeoutMs
    {
        get => _setting?.TimeoutMs ?? 5000;
        set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
