using System.ComponentModel;
using System.Runtime.CompilerServices;
using LIN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.ViewModels;

public class LinWriteReadViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private LinWriteReadSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (LinWriteReadSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (LinWriteReadSetting)_serializer.CreateDefault();
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
    public string FrameId
    {
        get => _setting?.FrameId ?? "0";
        set { if (_setting == null || _setting.FrameId == value) return; _setting.FrameId = value; OnPropertyChanged(); QueueSave(); }
    }
    public string Data
    {
        get => _setting?.Data ?? string.Empty;
        set { if (_setting == null || _setting.Data == value) return; _setting.Data = value; OnPropertyChanged(); QueueSave(); }
    }
    public int ChecksumType
    {
        get => (int)(_setting?.ChecksumType ?? LinChecksumType.Enhanced);
        set { if (_setting == null) return; _setting.ChecksumType = (LinChecksumType)value; OnPropertyChanged(); QueueSave(); }
    }
    public int ResponseTimeoutMs
    {
        get => _setting?.ResponseTimeoutMs ?? 500;
        set { if (_setting == null || _setting.ResponseTimeoutMs == value) return; _setting.ResponseTimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }
    public string ResultVariable
    {
        get => _setting?.ResultVariable ?? string.Empty;
        set { if (_setting == null || _setting.ResultVariable == value) return; _setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); }
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
