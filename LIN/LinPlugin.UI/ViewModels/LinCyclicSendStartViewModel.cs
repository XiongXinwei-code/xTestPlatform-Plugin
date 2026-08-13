using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LIN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.ViewModels;

public class LinCyclicSendStartViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private LinCyclicSendStartSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (LinCyclicSendStartSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (LinCyclicSendStartSetting)_serializer.CreateDefault();
            foreach (var item in _setting.Frames)
                item.PropertyChanged += OnItemChanged;
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
    public string TaskName
    {
        get => _setting?.TaskName ?? string.Empty;
        set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); }
    }
    public bool EnableLog
    {
        get => _setting?.EnableLog ?? false;
        set { if (_setting == null || _setting.EnableLog == value) return; _setting.EnableLog = value; OnPropertyChanged(); QueueSave(); }
    }
    public ObservableCollection<LinCyclicFrameItem> Frames => _setting?.Frames ?? [];

    public void AddFrame()
    {
        if (_setting == null) return;
        var item = new LinCyclicFrameItem();
        item.PropertyChanged += OnItemChanged;
        _setting.Frames.Add(item);
        QueueSave();
    }

    public void RemoveFrame(LinCyclicFrameItem item)
    {
        if (_setting == null) return;
        item.PropertyChanged -= OnItemChanged;
        _setting.Frames.Remove(item);
        QueueSave();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => QueueSave();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
