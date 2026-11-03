using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using CAN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

public class CanWriteViewModel : INotifyPropertyChanged
{
    private bool _suppressSave;
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 5000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public Action<string, Action>? ExecuteCommand { get => _save.ExecuteCommand; set => _save.ExecuteCommand = value; }
    public void Flush() => _save.Flush();
    private Step? _step;
    private Step? _loadedStep;
    private IStepSettingSerializer? _serializer;
    private CanWriteSetting? _setting;
    private CanPayloadProtectionSetting? _observedProtection;
    private ObservableCollection<CanWriteMessageItem>? _observedCollection;
    private readonly HashSet<CanWriteMessageItem> _observedRows = [];
    private CanWriteMessageItem? _selectedMessage;
    public CanPayloadProtectionSetting? Protection => _setting?.Protection;
    public ObservableCollection<CanWriteMessageItem> Messages => _setting?.Messages ?? [];
    public bool IsSingleMessage => !UseMessageList;
    public bool UseMessageList
    {
        get => _setting?.UseMessageList ?? false;
        set
        {
            if (_setting == null || _setting.UseMessageList == value) return;
            _setting.UseMessageList = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsSingleMessage)); QueueSave();
        }
    }
    public CanWriteMessageItem? SelectedMessage
    {
        get => _selectedMessage;
        set
        {
            if (_suppressSave || value is not null && !Messages.Contains(value)) return;
            if (value is null && Messages.Count != 0) return;
            if (ReferenceEquals(_selectedMessage, value)) return;
            _selectedMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedProtectionTitle));
        }
    }
    public string SelectedProtectionTitle => SelectedMessage == null ? "请先选择报文行" : $"第 {Messages.IndexOf(SelectedMessage) + 1} 行 — CAN ID {SelectedMessage.CanId} 的 RC / CRC";

    public void AttachSerializer(IStepSettingSerializer s) { Flush(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { Flush(); _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        var selectedIndex = ReferenceEquals(_loadedStep, _step) && _selectedMessage != null
            ? Messages.IndexOf(_selectedMessage) : -1;
        _suppressSave = true;
        try
        {
            if (_observedProtection != null) _observedProtection.PropertyChanged -= OnProtectionChanged;
            DetachRows();
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (CanWriteSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (CanWriteSetting)_serializer.CreateDefault();
            _setting.Protection ??= new();
            _setting.Messages ??= [];
            _observedProtection = _setting.Protection;
            _observedProtection.PropertyChanged += OnProtectionChanged;
            ObserveRows();
            _selectedMessage = selectedIndex >= 0 && selectedIndex < Messages.Count
                ? Messages[selectedIndex] : Messages.FirstOrDefault();
            _loadedStep = _step;
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void OnProtectionChanged(object? sender, PropertyChangedEventArgs e) => QueueSave();
    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, SelectedMessage) && e.PropertyName == nameof(CanWriteMessageItem.CanId))
            OnPropertyChanged(nameof(SelectedProtectionTitle));
        QueueSave();
    }
    private void DetachRows()
    {
        if (_observedCollection != null) _observedCollection.CollectionChanged -= RowsChanged;
        foreach (var row in _observedRows) row.PropertyChanged -= OnRowChanged;
        _observedRows.Clear(); _observedCollection = null;
    }
    private void ObserveRows()
    {
        if (_setting == null) return;
        _observedCollection = _setting.Messages; _observedCollection.CollectionChanged += RowsChanged;
        foreach (var row in _observedCollection) if (row != null && _observedRows.Add(row)) row.PropertyChanged += OnRowChanged;
    }
    private void RowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DetachRows(); ObserveRows();
        if (_setting != null && (SelectedMessage == null || !_setting.Messages.Contains(SelectedMessage)))
            SelectedMessage = _setting.Messages.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedProtectionTitle)); QueueSave();
    }
    public void AddMessage()
    {
        if (_setting == null || _setting.Messages.Count >= 128) return;
        var row = new CanWriteMessageItem(); _setting.Messages.Add(row); SelectedMessage = row;
    }
    public void RemoveMessage(CanWriteMessageItem row)
    { if (_setting != null) _setting.Messages.Remove(row); }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _save.Schedule(_step, _serializer, _setting);
    }

    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public string CanId { get => _setting?.CanId ?? ""; set { if (_setting == null || _setting.CanId == value) return; _setting.CanId = value; OnPropertyChanged(); QueueSave(); } }
    public int FrameType { get => (int)(_setting?.FrameType ?? CanFrameType.Standard); set { if (_setting == null) return; _setting.FrameType = (CanFrameType)value; OnPropertyChanged(); QueueSave(); } }
    public string Data { get => _setting?.Data ?? ""; set { if (_setting == null || _setting.Data == value) return; _setting.Data = value; OnPropertyChanged(); QueueSave(); } }
    public bool UseFdFrame { get => _setting?.UseFdFrame ?? false; set { if (_setting == null || _setting.UseFdFrame == value) return; _setting.UseFdFrame = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
