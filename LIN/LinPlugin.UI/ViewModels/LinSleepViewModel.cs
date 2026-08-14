using System.ComponentModel;
using System.Runtime.CompilerServices;
using LIN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.ViewModels;

public class LinSleepViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private LinSleepSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (LinSleepSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (LinSleepSetting)_serializer.CreateDefault();
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

    public string ConnectionName
    {
        get => _setting?.ConnectionName ?? string.Empty;
        set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
    }

    public int SleepModeIndex
    {
        get => (int)(_setting?.SleepMode ?? LinSleepMode.Remote);
        set { if (_setting == null || (int)_setting.SleepMode == value || value < 0) return; _setting.SleepMode = (LinSleepMode)value; OnPropertyChanged(); QueueSave(); }
    }

    public int PostSleepDelayMs
    {
        get => _setting?.PostSleepDelayMs ?? 100;
        set { if (_setting == null || _setting.PostSleepDelayMs == value) return; _setting.PostSleepDelayMs = value; OnPropertyChanged(); QueueSave(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
