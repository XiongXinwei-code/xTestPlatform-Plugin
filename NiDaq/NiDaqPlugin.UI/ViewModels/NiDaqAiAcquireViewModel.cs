using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.ViewModels;

public class NiDaqAiAcquireViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private NiDaqAiAcquireSetting? _setting;

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
                ? (NiDaqAiAcquireSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (NiDaqAiAcquireSetting)_serializer.CreateDefault();
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

    public double SampleRate { get => _setting?.SampleRate ?? 1000; set { if (_setting == null || _setting.SampleRate == value) return; _setting.SampleRate = value; OnPropertyChanged(); QueueSave(); } }
    public int SamplesPerChannel { get => _setting?.SamplesPerChannel ?? 100; set { if (_setting == null || _setting.SamplesPerChannel == value) return; _setting.SamplesPerChannel = value; OnPropertyChanged(); QueueSave(); } }
    public string ResultVariablePrefix { get => _setting?.ResultVariablePrefix ?? ""; set { if (_setting == null || _setting.ResultVariablePrefix == value) return; _setting.ResultVariablePrefix = value; OnPropertyChanged(); QueueSave(); } }

    public void AddChannel() { Channels.Add(new NiDaqAiChannel()); QueueSave(); }
    public void RemoveChannel(NiDaqAiChannel ch) { Channels.Remove(ch); QueueSave(); }
    public void NotifyChannelChanged() => QueueSave();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
