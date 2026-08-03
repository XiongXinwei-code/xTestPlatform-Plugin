using System.ComponentModel;
using System.Runtime.CompilerServices;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.ViewModels;

public class NiDaqDataAnalyzeViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private NiDaqDataAnalyzeSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (NiDaqDataAnalyzeSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (NiDaqDataAnalyzeSetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); } catch (TaskCanceledException) { } });
    }

    public string FilePath { get => _setting?.FilePath ?? ""; set { if (_setting == null || _setting.FilePath == value) return; _setting.FilePath = value; OnPropertyChanged(); QueueSave(); } }
    public string ChannelName { get => _setting?.ChannelName ?? ""; set { if (_setting == null || _setting.ChannelName == value) return; _setting.ChannelName = value; OnPropertyChanged(); QueueSave(); } }
    public AnalyzeMode Mode { get => _setting?.Mode ?? AnalyzeMode.Max; set { if (_setting == null || _setting.Mode == value) return; _setting.Mode = value; OnPropertyChanged(); QueueSave(); } }
    public string ReferenceChannel { get => _setting?.ReferenceChannel ?? ""; set { if (_setting == null || _setting.ReferenceChannel == value) return; _setting.ReferenceChannel = value; OnPropertyChanged(); QueueSave(); } }
    public double RangeStart { get => _setting?.RangeStart ?? 0; set { if (_setting == null || _setting.RangeStart == value) return; _setting.RangeStart = value; OnPropertyChanged(); QueueSave(); } }
    public double RangeEnd { get => _setting?.RangeEnd ?? 0; set { if (_setting == null || _setting.RangeEnd == value) return; _setting.RangeEnd = value; OnPropertyChanged(); QueueSave(); } }
    public string ResultVariable { get => _setting?.ResultVariable ?? ""; set { if (_setting == null || _setting.ResultVariable == value) return; _setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string RefAtPeakVariable { get => _setting?.RefAtPeakVariable ?? ""; set { if (_setting == null || _setting.RefAtPeakVariable == value) return; _setting.RefAtPeakVariable = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
