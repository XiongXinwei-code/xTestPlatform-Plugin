using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using CAN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

public class CanCyclicSendStartViewModel : INotifyPropertyChanged
{
    private bool _suppressSave;
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public void Flush() => _save.Flush();
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private CanCyclicSendStartSetting? _setting;
    private readonly HashSet<CyclicMessageItem> _observed = [];
    private ObservableCollection<CyclicMessageItem>? _observedCollection;

    public ObservableCollection<CyclicMessageItem> Messages => _setting?.Messages ?? [];

    public void AttachSerializer(IStepSettingSerializer s) { Flush(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { Flush(); _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            DetachRows();
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (CanCyclicSendStartSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (CanCyclicSendStartSetting)_serializer.CreateDefault();

            ObserveRows();

            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => QueueSave();

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _save.Schedule(_step, _serializer, _setting);
    }

    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public string TaskName { get => _setting?.TaskName ?? ""; set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); } }

    public void AddMessage()
    {
        if (_setting == null) return;
        var item = new CyclicMessageItem();
        _setting.Messages.Add(item);
        OnPropertyChanged(nameof(Messages));
        QueueSave();
    }

    public void RemoveMessage(CyclicMessageItem item)
    {
        if (_setting == null) return;
        _setting.Messages.Remove(item);
        OnPropertyChanged(nameof(Messages));
        QueueSave();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void DetachRows()
    {
        if (_observedCollection != null) _observedCollection.CollectionChanged -= RowsChanged;
        foreach (var row in _observed) row.PropertyChanged -= OnItemChanged;
        _observed.Clear(); _observedCollection = null;
    }
    private void ObserveRows()
    {
        if (_setting == null) return;
        _observedCollection = _setting.Messages;
        _observedCollection.CollectionChanged += RowsChanged;
        foreach (var row in _observedCollection) if (_observed.Add(row)) row.PropertyChanged += OnItemChanged;
    }
    private void RowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DetachRows(); ObserveRows(); QueueSave();
    }
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
