using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

public class CanCyclicSendStartViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private CanCyclicSendStartSetting? _setting;

    public ObservableCollection<CyclicMessageItem> Messages => _setting?.Messages ?? [];

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (CanCyclicSendStartSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (CanCyclicSendStartSetting)_serializer.CreateDefault();

            // 订阅每个 item 的 PropertyChanged 以触发保存
            foreach (var item in _setting.Messages)
                item.PropertyChanged += OnItemChanged;

            _setting.Messages.CollectionChanged += (_, _) => QueueSave();

            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => QueueSave();

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); } catch (TaskCanceledException) { } });
    }

    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public string TaskName { get => _setting?.TaskName ?? ""; set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); } }
    public bool EnableLog { get => _setting?.EnableLog ?? false; set { if (_setting == null || _setting.EnableLog == value) return; _setting.EnableLog = value; OnPropertyChanged(); QueueSave(); } }

    public void AddMessage()
    {
        if (_setting == null) return;
        var item = new CyclicMessageItem();
        item.PropertyChanged += OnItemChanged;
        _setting.Messages.Add(item);
        OnPropertyChanged(nameof(Messages));
        QueueSave();
    }

    public void RemoveMessage(CyclicMessageItem item)
    {
        if (_setting == null) return;
        item.PropertyChanged -= OnItemChanged;
        _setting.Messages.Remove(item);
        OnPropertyChanged(nameof(Messages));
        QueueSave();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
