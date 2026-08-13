using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.ViewModels;

public class OpcUaBatchWriteViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private OpcUaBatchWriteSetting? _setting;

    public ObservableCollection<OpcUaBatchWriteItem> Items { get; private set; } = new();

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (OpcUaBatchWriteSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (OpcUaBatchWriteSetting)_serializer.CreateDefault();
            Items = _setting.Items;
            foreach (var item in Items)
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
        _ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); } catch (TaskCanceledException) { } });
    }

    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 5000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }

    public void AddItem() { var item = new OpcUaBatchWriteItem(); item.PropertyChanged += OnItemChanged; Items.Add(item); QueueSave(); }
    public void RemoveItem(OpcUaBatchWriteItem item) { item.PropertyChanged -= OnItemChanged; Items.Remove(item); QueueSave(); }
    public void NotifyItemChanged() => QueueSave();

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => QueueSave();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
