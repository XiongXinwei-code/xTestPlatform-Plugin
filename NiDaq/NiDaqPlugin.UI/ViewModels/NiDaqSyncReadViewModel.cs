using System.ComponentModel;
using System.Runtime.CompilerServices;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.ViewModels;

public class NiDaqSyncReadViewModel : INotifyPropertyChanged
{
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private NiDaqSyncReadSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { FlushPendingSave(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { FlushPendingSave(); _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (NiDaqSyncReadSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (NiDaqSyncReadSetting)_serializer.CreateDefault();
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

    public string TaskName { get => _setting?.TaskName ?? ""; set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); } }
    public int SamplesToRead { get => _setting?.SamplesToRead ?? -1; set { if (_setting == null || _setting.SamplesToRead == value) return; _setting.SamplesToRead = value; OnPropertyChanged(); QueueSave(); } }
    public int ReadTimeoutMs { get => _setting?.ReadTimeoutMs ?? 10000; set { if (_setting == null || _setting.ReadTimeoutMs == value) return; _setting.ReadTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public int ExportTimeoutMs { get => _setting?.ExportTimeoutMs ?? 10000; set { if (_setting == null || _setting.ExportTimeoutMs == value) return; _setting.ExportTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public string ResultVariable { get => _setting?.ResultVariable ?? ""; set { if (_setting == null || _setting.ResultVariable == value) return; _setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
    public bool SaveToFile { get => _setting?.SaveToFile ?? false; set { if (_setting == null || _setting.SaveToFile == value) return; _setting.SaveToFile = value; OnPropertyChanged(); QueueSave(); } }
    public DaqExportFormat ExportFormat { get => _setting?.ExportFormat ?? DaqExportFormat.Csv; set { if (_setting == null || _setting.ExportFormat == value) return; _setting.ExportFormat = value; OnPropertyChanged(); QueueSave(); } }
    public string OutputDirectory { get => _setting?.OutputDirectory ?? ""; set { if (_setting == null || _setting.OutputDirectory == value) return; _setting.OutputDirectory = value; OnPropertyChanged(); QueueSave(); } }
    public int MaxFileSizeMB { get => _setting?.MaxFileSizeMB ?? 500; set { if (_setting == null || _setting.MaxFileSizeMB == value) return; _setting.MaxFileSizeMB = value; OnPropertyChanged(); QueueSave(); } }


    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
