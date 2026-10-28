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
    private readonly EditorSaveCoordinator _save = new();
    public Exception? LastSaveError => _save.LastError;
    public void FlushPendingSave() => _save.Flush();
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private VisaBatchWriteSetting? _setting;

    public ObservableCollection<VisaBatchWriteItem> Items { get; private set; } = new();

    public void AttachSerializer(IStepSettingSerializer s) { FlushPendingSave(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { FlushPendingSave(); _step = step; Load(); }

    private void Load()
    {
        _save.Detach();
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (VisaBatchWriteSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (VisaBatchWriteSetting)_serializer.CreateDefault();

            Items = _setting.Items;
            _save.Observe(Items, QueueSave);
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
        Items.Add(item);
        QueueSave();
    }

    public void RemoveItem(VisaBatchWriteItem item)
    {
        if (_setting == null) return;
        Items.Remove(item);
        QueueSave();
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        var step = _step; var setting = _setting; var serializer = _serializer;
        _save.Queue(() => step.StepSetting.Setting = serializer.Serialize(setting));
    }

    public int TimeoutMs { get => _setting?.TimeoutMs ?? 0; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
