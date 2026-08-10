using System.ComponentModel;
using System.Runtime.CompilerServices;
using LIN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.ViewModels;

public class LinReadViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private LinReadSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (LinReadSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (LinReadSetting)_serializer.CreateDefault();
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
    public string FilterFrameId
    {
        get => _setting?.FilterFrameId ?? string.Empty;
        set { if (_setting == null || _setting.FilterFrameId == value) return; _setting.FilterFrameId = value; OnPropertyChanged(); QueueSave(); }
    }
    public int ReadTimeoutMs
    {
        get => _setting?.ReadTimeoutMs ?? 1000;
        set { if (_setting == null || _setting.ReadTimeoutMs == value) return; _setting.ReadTimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }
    public string ResultVariable
    {
        get => _setting?.ResultVariable ?? string.Empty;
        set { if (_setting == null || _setting.ResultVariable == value) return; _setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); }
    }
    public string IdVariable
    {
        get => _setting?.IdVariable ?? string.Empty;
        set { if (_setting == null || _setting.IdVariable == value) return; _setting.IdVariable = value; OnPropertyChanged(); QueueSave(); }
    }
    public bool EnableLog
    {
        get => _setting?.EnableLog ?? true;
        set { if (_setting == null || _setting.EnableLog == value) return; _setting.EnableLog = value; OnPropertyChanged(); QueueSave(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
