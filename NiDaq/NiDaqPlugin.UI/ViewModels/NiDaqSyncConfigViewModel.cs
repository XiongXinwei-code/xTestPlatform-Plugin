using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.ViewModels;

public class NiDaqSyncConfigViewModel : INotifyPropertyChanged
{
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private NiDaqSyncConfigSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { FlushPendingSave(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { FlushPendingSave(); _step = step; Load(); }

    private PluginEditor.SettingsChangeTracker? _changes;
    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _changes?.Dispose(); _changes = new PluginEditor.SettingsChangeTracker(QueueSave);
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (NiDaqSyncConfigSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (NiDaqSyncConfigSetting)_serializer.CreateDefault();
            AiChannels = _setting.AiChannels; _changes.Track(AiChannels);
            EncoderChannels = _setting.EncoderChannels; _changes.Track(EncoderChannels);
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private readonly System.Windows.Threading.Dispatcher _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
    private readonly PluginCommon.EditorSaveScheduler _saves = new();
    public void FlushPendingSave() => _saves.Flush();
    public Exception? LastSaveError => _saves.LastError;
    private void QueueSave()
    {
        if (!_dispatcher.CheckAccess()) { _dispatcher.Invoke(QueueSave); return; }
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        // 防抖保存留在 Dispatcher；刷新、卸载前 Flush 固定的原绑定。
        _saves.Schedule(_step, _serializer, _setting);
    }

    public string TaskName { get => _setting?.TaskName ?? ""; set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); } }
    public double SampleRate { get => _setting?.SampleRate ?? 1000; set { if (_setting == null || _setting.SampleRate == value) return; _setting.SampleRate = value; OnPropertyChanged(); QueueSave(); } }
    public int SamplesPerChannel { get => _setting?.SamplesPerChannel ?? 100; set { if (_setting == null || _setting.SamplesPerChannel == value) return; _setting.SamplesPerChannel = value; OnPropertyChanged(); QueueSave(); } }
    public AiSampleMode SampleMode { get => _setting?.SampleMode ?? AiSampleMode.FiniteSamples; set { if (_setting == null || _setting.SampleMode == value) return; _setting.SampleMode = value; OnPropertyChanged(); QueueSave(); } }
    public string ClockSource { get => _setting?.ClockSource ?? ""; set { if (_setting == null || _setting.ClockSource == value) return; _setting.ClockSource = value; OnPropertyChanged(); QueueSave(); } }
    public bool UseTrigger { get => _setting?.UseTrigger ?? false; set { if (_setting == null || _setting.UseTrigger == value) return; _setting.UseTrigger = value; OnPropertyChanged(); QueueSave(); } }
    public string TriggerSource { get => _setting?.TriggerSource ?? ""; set { if (_setting == null || _setting.TriggerSource == value) return; _setting.TriggerSource = value; OnPropertyChanged(); QueueSave(); } }
    public TriggerEdge TriggerEdge { get => _setting?.TriggerEdge ?? TriggerEdge.Rising; set { if (_setting == null || _setting.TriggerEdge == value) return; _setting.TriggerEdge = value; OnPropertyChanged(); QueueSave(); } }

    public ObservableCollection<NiDaqAiChannel> AiChannels { get; private set; } = new();
    public ObservableCollection<NiDaqSyncEncoderChannel> EncoderChannels { get; private set; } = new();

    public void AddAiChannel() { var ch = new NiDaqAiChannel(); AiChannels.Add(ch); QueueSave(); }
    public void RemoveAiChannel(NiDaqAiChannel ch) { AiChannels.Remove(ch); QueueSave(); }
    public void AddEncoderChannel() { var ch = new NiDaqSyncEncoderChannel(); EncoderChannels.Add(ch); QueueSave(); }
    public void RemoveEncoderChannel(NiDaqSyncEncoderChannel ch) { EncoderChannels.Remove(ch); QueueSave(); }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => QueueSave();

    public int TimeoutMs { get => _setting?.TimeoutMs ?? 10000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
