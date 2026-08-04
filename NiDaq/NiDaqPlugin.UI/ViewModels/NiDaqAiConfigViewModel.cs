using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.ViewModels;

public class NiDaqAiConfigViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private NiDaqAiConfigSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (NiDaqAiConfigSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (NiDaqAiConfigSetting)_serializer.CreateDefault();
            Channels = _setting.Channels;
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

    public string TaskName { get => _setting?.TaskName ?? ""; set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); } }
    public double SampleRate { get => _setting?.SampleRate ?? 1000; set { if (_setting == null || _setting.SampleRate == value) return; _setting.SampleRate = value; OnPropertyChanged(); QueueSave(); } }
    public int SamplesPerChannel { get => _setting?.SamplesPerChannel ?? 100; set { if (_setting == null || _setting.SamplesPerChannel == value) return; _setting.SamplesPerChannel = value; OnPropertyChanged(); QueueSave(); } }
    public AiSampleMode SampleMode { get => _setting?.SampleMode ?? AiSampleMode.FiniteSamples; set { if (_setting == null || _setting.SampleMode == value) return; _setting.SampleMode = value; OnPropertyChanged(); QueueSave(); } }
    public string ClockSource { get => _setting?.ClockSource ?? ""; set { if (_setting == null || _setting.ClockSource == value) return; _setting.ClockSource = value; OnPropertyChanged(); QueueSave(); } }
    public bool UseTrigger { get => _setting?.UseTrigger ?? false; set { if (_setting == null || _setting.UseTrigger == value) return; _setting.UseTrigger = value; OnPropertyChanged(); QueueSave(); } }
    public string TriggerSource { get => _setting?.TriggerSource ?? ""; set { if (_setting == null || _setting.TriggerSource == value) return; _setting.TriggerSource = value; OnPropertyChanged(); QueueSave(); } }
    public TriggerEdge TriggerEdge { get => _setting?.TriggerEdge ?? TriggerEdge.Rising; set { if (_setting == null || _setting.TriggerEdge == value) return; _setting.TriggerEdge = value; OnPropertyChanged(); QueueSave(); } }

    public ObservableCollection<NiDaqAiChannel> Channels { get; private set; } = new();

    public void AddChannel() { Channels.Add(new NiDaqAiChannel()); QueueSave(); }
    public void RemoveChannel(NiDaqAiChannel ch) { Channels.Remove(ch); QueueSave(); }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
