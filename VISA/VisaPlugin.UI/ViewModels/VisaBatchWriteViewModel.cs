using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VISA.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.ViewModels;

/// <summary>
/// VISA 批量写入编辑器 ViewModel
/// </summary>
public class VisaBatchWriteViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private VisaBatchWriteSetting? _setting;

    public ObservableCollection<VisaBatchWriteItemViewModel> Items { get; } = new();

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (VisaBatchWriteSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (VisaBatchWriteSetting)_serializer.CreateDefault();

            Items.Clear();
            foreach (var item in _setting.Items)
            {
                var vm = new VisaBatchWriteItemViewModel(item);
                vm.PropertyChanged += (_, _) => QueueSave();
                Items.Add(vm);
            }
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    public string ConnectionName
    {
        get => _setting?.ConnectionName ?? "";
        set
        {
            if (_setting == null || _setting.ConnectionName == value) return;
            _setting.ConnectionName = value;
            OnPropertyChanged();
            QueueSave();
        }
    }

    public void AddItem()
    {
        if (_setting == null) return;
        var item = new VisaBatchWriteItem();
        _setting.Items.Add(item);
        var vm = new VisaBatchWriteItemViewModel(item);
        vm.PropertyChanged += (_, _) => QueueSave();
        Items.Add(vm);
        QueueSave();
    }

    public void RemoveItem(VisaBatchWriteItemViewModel itemVm)
    {
        if (_setting == null) return;
        var idx = Items.IndexOf(itemVm);
        if (idx < 0) return;
        _setting.Items.RemoveAt(idx);
        Items.RemoveAt(idx);
        QueueSave();
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SaveDebounceMs, cts.Token);
                _step.StepSetting.Setting = _serializer.Serialize(_setting);
            }
            catch (TaskCanceledException) { }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>
/// 批量写入单条命令项的 ViewModel
/// </summary>
public class VisaBatchWriteItemViewModel : INotifyPropertyChanged
{
    private readonly VisaBatchWriteItem _item;

    public VisaBatchWriteItemViewModel(VisaBatchWriteItem item) => _item = item;

    public string Command
    {
        get => _item.Command;
        set { if (_item.Command == value) return; _item.Command = value; OnPropertyChanged(); }
    }

    public int DelayMs
    {
        get => _item.DelayMs;
        set { if (_item.DelayMs == value) return; _item.DelayMs = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
