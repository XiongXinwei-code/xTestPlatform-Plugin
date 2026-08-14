using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

public class CanOpenViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private CanOpenSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (CanOpenSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (CanOpenSetting)_serializer.CreateDefault();
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

    public int AdapterType { get => (int)(_setting?.AdapterType ?? CanAdapterType.NI); set { if (_setting == null) return; _setting.AdapterType = (CanAdapterType)value; OnPropertyChanged(); QueueSave(); } }
    public string Channel { get => _setting?.Channel ?? ""; set { if (_setting == null || _setting.Channel == value) return; _setting.Channel = value; OnPropertyChanged(); QueueSave(); } }
    public int BaudRate { get => _setting?.BaudRate ?? 500000; set { if (_setting == null || _setting.BaudRate == value) return; _setting.BaudRate = value; OnPropertyChanged(); QueueSave(); } }
    public int Protocol { get => (int)(_setting?.Protocol ?? CanProtocolType.Classic); set { if (_setting == null) return; _setting.Protocol = (CanProtocolType)value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowDataBitRate)); QueueSave(); } }
    public int DataBitRate { get => _setting?.DataBitRate ?? 2000000; set { if (_setting == null || _setting.DataBitRate == value) return; _setting.DataBitRate = value; OnPropertyChanged(); QueueSave(); } }
    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public int RxQueueSize { get => _setting?.RxQueueSize ?? 512; set { if (_setting == null || _setting.RxQueueSize == value) return; _setting.RxQueueSize = value; OnPropertyChanged(); QueueSave(); } }

    public bool ShowDataBitRate => (_setting?.Protocol ?? CanProtocolType.Classic) != CanProtocolType.Classic;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
