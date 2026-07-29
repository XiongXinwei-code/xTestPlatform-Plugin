using System.ComponentModel;
using System.Runtime.CompilerServices;
using SerialPortPlugin.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace SerialPortPlugin.UI.ViewModels;

public class SerialPortWriteViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private SerialPortWriteSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s)
    {
        _serializer = s;
        if (_step != null) Load();
    }

    public void AttachStep(Step step)
    {
        _step = step;
        Load();
    }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (SerialPortWriteSetting)_serializer.Deserialize(d, _serializer.SettingVersion)
                : (SerialPortWriteSetting)_serializer.CreateDefault();
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
            try
            {
                await Task.Delay(SaveDebounceMs, cts.Token);
                _step.StepSetting.Setting = _serializer.Serialize(_setting);
            }
            catch (TaskCanceledException) { }
        });
    }

    public string PortName
    {
        get => _setting?.PortName ?? "COM1";
        set { if (_setting != null && _setting.PortName != value) { _setting.PortName = value; OnPropertyChanged(); QueueSave(); } }
    }

    public string Data
    {
        get => _setting?.Data ?? string.Empty;
        set { if (_setting != null && _setting.Data != value) { _setting.Data = value; OnPropertyChanged(); QueueSave(); } }
    }

    public string Encoding
    {
        get => _setting?.Encoding ?? "ASCII";
        set { if (_setting != null && _setting.Encoding != value) { _setting.Encoding = value; OnPropertyChanged(); QueueSave(); } }
    }

    public bool AppendNewLine
    {
        get => _setting?.AppendNewLine ?? true;
        set { if (_setting != null && _setting.AppendNewLine != value) { _setting.AppendNewLine = value; OnPropertyChanged(); QueueSave(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
