using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.ViewModels;

public class OpcUaDataAcqStopViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private OpcUaDataAcqStopSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (OpcUaDataAcqStopSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (OpcUaDataAcqStopSetting)_serializer.CreateDefault();
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

    public string TaskName { get => _setting?.TaskName ?? ""; set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); } }
    public int ExportFormat { get => (int)(_setting?.ExportFormat ?? DataAcqExportFormat.Csv); set { if (_setting == null) return; _setting.ExportFormat = (DataAcqExportFormat)value; OnPropertyChanged(); QueueSave(); } }
    public string CsvFilePath { get => _setting?.CsvFilePath ?? ""; set { if (_setting == null || _setting.CsvFilePath == value) return; _setting.CsvFilePath = value; OnPropertyChanged(); QueueSave(); } }
    public bool SaveStatistics { get => _setting?.SaveStatistics ?? true; set { if (_setting == null || _setting.SaveStatistics == value) return; _setting.SaveStatistics = value; OnPropertyChanged(); QueueSave(); } }
    public string StatVariablePrefix { get => _setting?.StatVariablePrefix ?? ""; set { if (_setting == null || _setting.StatVariablePrefix == value) return; _setting.StatVariablePrefix = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
