using System.ComponentModel;
using System.Runtime.CompilerServices;
using LIN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.ViewModels;

public class LinOpenViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private LinOpenSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

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
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); }
            catch (TaskCanceledException) { }
        });
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
    public string ConnectionName
    {
        get => _setting?.ConnectionName ?? string.Empty;
        set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
