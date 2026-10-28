using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Http.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI.ViewModels;

/// <summary>
/// XML 提取编辑器 ViewModel
/// </summary>
public class HttpXmlExtractViewModel : INotifyPropertyChanged
{
    private readonly PluginCommon.EditorSaveScheduler _saver = new();
    private readonly HashSet<HttpExtractItem> _observedItems = new();
    public void FlushPendingSave() => _saver.Flush();
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private HttpXmlExtractSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        _saver.Flush();
        if (_serializer == null || _step == null) return;
        if (_setting != null)
        {
            _setting.Items.CollectionChanged -= OnItemsChanged;
            foreach (var item in _observedItems) item.PropertyChanged -= OnItemChanged;
            _observedItems.Clear();
        }
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (HttpXmlExtractSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (HttpXmlExtractSetting)_serializer.CreateDefault();
            HookItems();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void HookItems()
    {
        if (_setting == null) return;
        _setting.Items.CollectionChanged -= OnItemsChanged;
        _setting.Items.CollectionChanged += OnItemsChanged;
        foreach (var item in _observedItems) item.PropertyChanged -= OnItemChanged;
        _observedItems.Clear();
        foreach (var item in _setting.Items)
        {
            if (_observedItems.Add(item)) item.PropertyChanged += OnItemChanged;
        }
    }

    private void OnItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        HookItems(); // Reset/Clear has no OldItems; reconcile actual current membership.
        QueueSave();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => QueueSave();

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _saver.Schedule(_step, _serializer, _setting);
    }

    public string SourceXml { get => _setting?.SourceXml ?? ""; set { if (_setting == null || _setting.SourceXml == value) return; _setting.SourceXml = value; OnPropertyChanged(); QueueSave(); } }
    public bool IgnoreNamespaces { get => _setting?.IgnoreNamespaces ?? true; set { if (_setting == null || _setting.IgnoreNamespaces == value) return; _setting.IgnoreNamespaces = value; OnPropertyChanged(); QueueSave(); } }
    public bool FailOnMissingPath { get => _setting?.FailOnMissingPath ?? true; set { if (_setting == null || _setting.FailOnMissingPath == value) return; _setting.FailOnMissingPath = value; OnPropertyChanged(); QueueSave(); } }
    public ObservableCollection<HttpExtractItem> Items => _setting?.Items ?? [];

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
