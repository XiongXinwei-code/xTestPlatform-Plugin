using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.ViewModels;

public class NiDaqAiStartViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private NiDaqAiStartSetting? _setting;

    public ObservableCollection<NiDaqAiChannel> Channels { get; } = new();

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (NiDaqAiStartSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (NiDaqAiStartSetting)_serializer.CreateDefault();
            Channels.Clear();
            foreach (var ch in _setting.Channels) Channels.Add(ch);
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _setting.Channels = Channels.ToList();
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); } catch (TaskCanceledException) { } });
    }

    public string TaskName { get => _setting?.TaskName ?? ""; set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); } }
    public double SampleRate { get => _setting?.SampleRate ?? 1000; set { if (_setting == null || _setting.SampleRate == value) return; _setting.SampleRate = value; OnPropertyChanged(); QueueSave(); } }
    public int MaxDurationMs { get => _setting?.MaxDurationMs ?? 0; set { if (_setting == null || _setting.MaxDurationMs == value) return; _setting.MaxDurationMs = value; OnPropertyChanged(); QueueSave(); } }
    public DaqExportFormat ExportFormat { get => _setting?.ExportFormat ?? DaqExportFormat.TdmsAndVariable; set { if (_setting == null || _setting.ExportFormat == value) return; _setting.ExportFormat = value; OnPropertyChanged(); QueueSave(); } }
    public string OutputDirectory { get => _setting?.OutputDirectory ?? ""; set { if (_setting == null || _setting.OutputDirectory == value) return; _setting.OutputDirectory = value; OnPropertyChanged(); QueueSave(); } }
    public string StatVariablePrefix { get => _setting?.StatVariablePrefix ?? ""; set { if (_setting == null || _setting.StatVariablePrefix == value) return; _setting.StatVariablePrefix = value; OnPropertyChanged(); QueueSave(); } }
    public int ReadBatchSize { get => _setting?.ReadBatchSize ?? 1000; set { if (_setting == null || _setting.ReadBatchSize == value) return; _setting.ReadBatchSize = value; OnPropertyChanged(); QueueSave(); } }

    public void AddChannel() { Channels.Add(new NiDaqAiChannel()); QueueSave(); }
    public void RemoveChannel(NiDaqAiChannel ch) { Channels.Remove(ch); QueueSave(); }
    public void NotifyChannelChanged() => QueueSave();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
