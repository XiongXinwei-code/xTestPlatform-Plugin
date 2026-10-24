using System.ComponentModel;
using System.Runtime.CompilerServices;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.ViewModels;

public class NiDaqDoWriteViewModel : INotifyPropertyChanged
{
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private NiDaqDoWriteSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { FlushPendingSave(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { FlushPendingSave(); _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (NiDaqDoWriteSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (NiDaqDoWriteSetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private readonly System.Windows.Threading.Dispatcher _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
    private readonly PluginCommon.EditorSaveScheduler _saves = new();
    public void FlushPendingSave() => _saves.Flush();
    public Exception? LastSaveError => _saves.LastError;
    private void QueueSave()
    {
        if (!_dispatcher.CheckAccess()) { _dispatcher.Invoke(QueueSave); return; }
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        // 防抖保存留在 Dispatcher；刷新、卸载前 Flush 固定的原绑定。
        _saves.Schedule(_step, _serializer, _setting);
    }

    public string Channel { get => _setting?.Channel ?? ""; set { if (_setting == null || _setting.Channel == value) return; _setting.Channel = value; OnPropertyChanged(); QueueSave(); } }
    public string Value { get => _setting?.Value ?? ""; set { if (_setting == null || _setting.Value == value) return; _setting.Value = value; OnPropertyChanged(); QueueSave(); } }

    public int TimeoutMs { get => _setting?.TimeoutMs ?? 10000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
