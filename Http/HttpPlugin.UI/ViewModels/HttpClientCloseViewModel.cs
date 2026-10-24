using System.ComponentModel;
using System.Runtime.CompilerServices;
using Http.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI.ViewModels;

/// <summary>
/// HTTP 客户端关闭编辑器 ViewModel
/// </summary>
public class HttpClientCloseViewModel : INotifyPropertyChanged, IDataErrorInfo
{
    private readonly PluginCommon.EditorSaveScheduler _saver = new();
    public void FlushPendingSave() => _saver.Flush();
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private HttpClientCloseSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        _saver.Flush();
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (HttpClientCloseSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (HttpClientCloseSetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _saver.Schedule(_step, _serializer, _setting);
    }

    public string ClientName { get => _setting?.ClientName ?? ""; set { if (_setting == null || _setting.ClientName == value) return; _setting.ClientName = value; OnPropertyChanged(); QueueSave(); } }
    public bool IgnoreIfNotFound { get => _setting?.IgnoreIfNotFound ?? true; set { if (_setting == null || _setting.IgnoreIfNotFound == value) return; _setting.IgnoreIfNotFound = value; OnPropertyChanged(); QueueSave(); } }
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 5000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public string Error => this[nameof(TimeoutMs)];
    public string this[string name] => name == nameof(TimeoutMs) && TimeoutMs <= 0 ? "关闭排队总超时必须大于0 ms" : "";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
