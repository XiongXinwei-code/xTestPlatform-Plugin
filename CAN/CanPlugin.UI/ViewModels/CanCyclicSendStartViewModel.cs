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
    private bool _active = true;
    private bool _rebindingChoices;
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public Action<string, Action>? ExecuteCommand { get => _save.ExecuteCommand; set => _save.ExecuteCommand = value; }
    public void Flush() => _save.Flush();
    private Step? _step;
    private Step? _loadedStep;
    private IStepSettingSerializer? _serializer;
    private CanCyclicSendStartSetting? _setting;
    private readonly HashSet<CyclicMessageItem> _observed = [];
    private ObservableCollection<CyclicMessageItem>? _observedCollection;
    private CyclicMessageItem? _selectedMessage;

    public CyclicMessageItem? SelectedMessage
    {
        get => _selectedMessage;
        set
        {
            if (!_active || _suppressSave || _rebindingChoices) return;
            if (value is not null && !Messages.Contains(value)) return;
            // An ItemsSource refresh may push null back before the replacement is selected.
            if (value is null && Messages.Count != 0) return;
            if (ReferenceEquals(_selectedMessage, value)) return;
            _selectedMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedProtectionTitle));
            OnPropertyChanged(nameof(SelectedProtectionChoice));
        }
    }

    public ObservableCollection<CyclicMessageItem> Messages => _setting?.Messages ?? [];
    public IReadOnlyList<CyclicProtectionChoice> ProtectionChoices { get; private set; } = [];
    public bool CanSelectProtection => _active && ProtectionChoices.Count != 0;
    public CyclicProtectionChoice? SelectedProtectionChoice
    {
        get => ProtectionChoices.FirstOrDefault(choice => ReferenceEquals(choice.Message, SelectedMessage));
        set
        {
            if (!_active || _suppressSave || _rebindingChoices || value is null || !ProtectionChoices.Contains(value)) return;
            SelectedMessage = value.Message;
        }
    }
    public string SelectedProtectionTitle => SelectedMessage == null ? "请先添加报文，再选择要配置 RC / CRC 的报文" : $"第 {Messages.IndexOf(SelectedMessage) + 1} 行 — CAN ID {SelectedMessage.CanId} 的 RC / CRC";

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
            _active = true;
            DetachRows();
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (CanCyclicSendStartSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (CanCyclicSendStartSetting)_serializer.CreateDefault();

            ObserveRows();
            RebuildProtectionChoices(selectedIndex >= 0 && selectedIndex < Messages.Count
                ? Messages[selectedIndex] : Messages.FirstOrDefault());
            _loadedStep = _step;

            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CyclicMessageItem.CanId) or nameof(CyclicMessageItem.FrameType) or nameof(CyclicMessageItem.UseFdFrame))
        {
            foreach (var choice in ProtectionChoices)
                if (ReferenceEquals(choice.Message, sender)) choice.RefreshDisplay();
            if (ReferenceEquals(sender, SelectedMessage)) OnPropertyChanged(nameof(SelectedProtectionTitle));
        }
        QueueSave();
    }

    private void QueueSave()
    {
        if (!_active || _suppressSave || _step == null || _setting == null || _serializer == null) return;
        _save.Schedule(_step, _serializer, _setting);
    }

    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public string TaskName { get => _setting?.TaskName ?? ""; set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); } }
    public int ControlRefreshMs { get => _setting?.ControlRefreshMs ?? 20; set { if (_setting == null || _setting.ControlRefreshMs == value) return; _setting.ControlRefreshMs = value; OnPropertyChanged(); QueueSave(); } }
    public int ExpressionTimeoutMs { get => _setting?.ExpressionTimeoutMs ?? 5000; set { if (_setting == null || _setting.ExpressionTimeoutMs == value) return; _setting.ExpressionTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }

    public void AddMessage()
    {
        if (_setting == null) return;
        var item = new CyclicMessageItem();
        _setting.Messages.Add(item);
        SelectedMessage = item;
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
        DetachRows(); ObserveRows();
        var selected = SelectedMessage is not null && Messages.Contains(SelectedMessage) ? SelectedMessage : Messages.FirstOrDefault();
        RebuildProtectionChoices(selected);
        QueueSave();
    }
    private void RebuildProtectionChoices(CyclicMessageItem? selected)
    {
        _rebindingChoices = true;
        try
        {
            ProtectionChoices = Messages.Select((message, index) => new CyclicProtectionChoice(message, index + 1)).ToArray();
            _selectedMessage = selected;
            OnPropertyChanged(nameof(ProtectionChoices));
            OnPropertyChanged(nameof(SelectedMessage));
            OnPropertyChanged(nameof(SelectedProtectionChoice));
            OnPropertyChanged(nameof(SelectedProtectionTitle));
            OnPropertyChanged(nameof(CanSelectProtection));
        }
        finally { _rebindingChoices = false; }
    }
    public void Deactivate()
    {
        Flush(); _active = false; DetachRows();
        OnPropertyChanged(nameof(CanSelectProtection));
    }
    public void Activate()
    {
        if (_active) return;
        Load();
        OnPropertyChanged(nameof(CanSelectProtection));
    }
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Read-only navigation label; editable settings still bind directly to the original message model.</summary>
public sealed class CyclicProtectionChoice(CyclicMessageItem message, int rowNumber) : INotifyPropertyChanged
{
    public CyclicMessageItem Message { get; } = message;
    public string Display => $"第 {rowNumber} 行 — ID {Message.CanId} · {Message.FrameType} · {(Message.UseFdFrame ? "CAN FD" : "Classic")}";
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void RefreshDisplay() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Display)));
}
