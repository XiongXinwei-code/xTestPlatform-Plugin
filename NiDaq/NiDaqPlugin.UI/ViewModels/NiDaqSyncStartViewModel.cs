using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.ViewModels;

public class NiDaqSyncStartViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private NiDaqSyncStartSetting? _setting;

    public ObservableCollection<NiDaqAiChannel> AiChannels { get; } = new();
    public ObservableCollection<NiDaqSyncEncoderChannel> EncoderChannels { get; } = new();

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (NiDaqSyncStartSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (NiDaqSyncStartSetting)_serializer.CreateDefault();
            AiChannels.Clear();
            foreach (var ch in _setting.AiChannels) AiChannels.Add(ch);
            EncoderChannels.Clear();
            foreach (var ch in _setting.EncoderChannels) EncoderChannels.Add(ch);
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _setting.AiChannels = AiChannels.ToList();
        _setting.EncoderChannels = EncoderChannels.ToList();
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); } catch (TaskCanceledException) { } });
    }

    public string TaskName { get => _setting?.TaskName ?? ""; set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); } }
    public double SampleRate { get => _setting?.SampleRate ?? 1000; set { if (_setting == null || _setting.SampleRate == value) return; _setting.SampleRate = value; OnPropertyChanged(); QueueSave(); } }
    public int MaxDurationMs { get => _setting?.MaxDurationMs ?? 0; set { if (_setting == null || _setting.MaxDurationMs == value) return; _setting.MaxDurationMs = value; OnPropertyChanged(); QueueSave(); } }
    public bool UseTrigger { get => _setting?.UseTrigger ?? false; set { if (_setting == null || _setting.UseTrigger == value) return; _setting.UseTrigger = value; OnPropertyChanged(); QueueSave(); } }
    public string TriggerSource { get => _setting?.TriggerSource ?? ""; set { if (_setting == null || _setting.TriggerSource == value) return; _setting.TriggerSource = value; OnPropertyChanged(); QueueSave(); } }
    public TriggerEdge TriggerEdge { get => _setting?.TriggerEdge ?? Models.TriggerEdge.Rising; set { if (_setting == null || _setting.TriggerEdge == value) return; _setting.TriggerEdge = value; OnPropertyChanged(); QueueSave(); } }
    public DaqExportFormat ExportFormat { get => _setting?.ExportFormat ?? DaqExportFormat.TdmsAndVariable; set { if (_setting == null || _setting.ExportFormat == value) return; _setting.ExportFormat = value; OnPropertyChanged(); QueueSave(); } }
    public string OutputDirectory { get => _setting?.OutputDirectory ?? ""; set { if (_setting == null || _setting.OutputDirectory == value) return; _setting.OutputDirectory = value; OnPropertyChanged(); QueueSave(); } }
    public string StatVariablePrefix { get => _setting?.StatVariablePrefix ?? ""; set { if (_setting == null || _setting.StatVariablePrefix == value) return; _setting.StatVariablePrefix = value; OnPropertyChanged(); QueueSave(); } }
    public int ReadBatchSize { get => _setting?.ReadBatchSize ?? 1000; set { if (_setting == null || _setting.ReadBatchSize == value) return; _setting.ReadBatchSize = value; OnPropertyChanged(); QueueSave(); } }

    public void AddAiChannel() { AiChannels.Add(new NiDaqAiChannel()); QueueSave(); }
    public void RemoveAiChannel(NiDaqAiChannel ch) { AiChannels.Remove(ch); QueueSave(); }
    public void AddEncoderChannel() { EncoderChannels.Add(new NiDaqSyncEncoderChannel()); QueueSave(); }
    public void RemoveEncoderChannel(NiDaqSyncEncoderChannel ch) { EncoderChannels.Remove(ch); QueueSave(); }
    public void NotifyChannelChanged() => QueueSave();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
