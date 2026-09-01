using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.Helpers;
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

    public int AdapterType { get => (int)(_setting?.AdapterType ?? CanAdapterType.NI); set { if (_setting == null) return; _setting.AdapterType = (CanAdapterType)value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowNiOptions)); QueueSave(); } }
    public string Channel { get => _setting?.Channel ?? ""; set { if (_setting == null || _setting.Channel == value) return; _setting.Channel = value; OnPropertyChanged(); QueueSave(); } }
    public int BaudRate { get => _setting?.BaudRate ?? 500000; set { if (_setting == null || _setting.BaudRate == value) return; _setting.BaudRate = value; OnPropertyChanged(); NotifyTimingPreviewChanged(); QueueSave(); } }
    public int Protocol { get => (int)(_setting?.Protocol ?? CanProtocolType.Classic); set { if (_setting == null) return; _setting.Protocol = (CanProtocolType)value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowDataBitRate)); QueueSave(); } }
    public int DataBitRate { get => _setting?.DataBitRate ?? 2000000; set { if (_setting == null || _setting.DataBitRate == value) return; _setting.DataBitRate = value; OnPropertyChanged(); QueueSave(); } }
    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public int RxQueueSize { get => _setting?.RxQueueSize ?? 512; set { if (_setting == null || _setting.RxQueueSize == value) return; _setting.RxQueueSize = value; OnPropertyChanged(); QueueSave(); } }

    public bool EnableTermination { get => _setting?.EnableTermination ?? false; set { if (_setting == null || _setting.EnableTermination == value) return; _setting.EnableTermination = value; OnPropertyChanged(); QueueSave(); } }
    public int ArbitrationBitTimingMode { get => (int)(_setting?.ArbitrationBitTimingMode ?? CanBitTimingMode.Automatic); set { if (_setting == null) return; _setting.ArbitrationBitTimingMode = (CanBitTimingMode)value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowSamplePoint)); OnPropertyChanged(nameof(ShowTimingRegisters)); NotifyTimingPreviewChanged(); QueueSave(); } }
    public double ArbitrationSamplePoint { get => _setting?.ArbitrationSamplePoint ?? 80d; set { if (_setting == null || Math.Abs(_setting.ArbitrationSamplePoint - value) < 0.0001) return; _setting.ArbitrationSamplePoint = value; OnPropertyChanged(); NotifyTimingPreviewChanged(); QueueSave(); } }
    public int ArbitrationBrp { get => _setting?.ArbitrationBrp ?? 1; set { if (_setting == null || _setting.ArbitrationBrp == value) return; _setting.ArbitrationBrp = value; OnPropertyChanged(); NotifyTimingPreviewChanged(); QueueSave(); } }
    public int ArbitrationSjw { get => _setting?.ArbitrationSjw ?? 4; set { if (_setting == null || _setting.ArbitrationSjw == value) return; _setting.ArbitrationSjw = value; OnPropertyChanged(); NotifyTimingPreviewChanged(); QueueSave(); } }
    public int ArbitrationTseg1 { get => _setting?.ArbitrationTseg1 ?? 30; set { if (_setting == null || _setting.ArbitrationTseg1 == value) return; _setting.ArbitrationTseg1 = value; OnPropertyChanged(); NotifyTimingPreviewChanged(); QueueSave(); } }
    public int ArbitrationTseg2 { get => _setting?.ArbitrationTseg2 ?? 7; set { if (_setting == null || _setting.ArbitrationTseg2 == value) return; _setting.ArbitrationTseg2 = value; OnPropertyChanged(); NotifyTimingPreviewChanged(); QueueSave(); } }

    public bool ShowDataBitRate => (_setting?.Protocol ?? CanProtocolType.Classic) != CanProtocolType.Classic;
    public bool ShowNiOptions => (_setting?.AdapterType ?? CanAdapterType.NI) == CanAdapterType.NI;
    public bool ShowSamplePoint => (_setting?.ArbitrationBitTimingMode ?? CanBitTimingMode.Automatic) == CanBitTimingMode.SamplePoint;
    public bool ShowTimingRegisters => (_setting?.ArbitrationBitTimingMode ?? CanBitTimingMode.Automatic) == CanBitTimingMode.Registers;

    public string TimingPreview
    {
        get
        {
            if (_setting == null || _setting.ArbitrationBitTimingMode == CanBitTimingMode.Automatic)
                return "由 NI-XNET 根据波特率选择默认采样点。";

            try
            {
                var timing = CanBitTimingCalculator.Resolve(_setting);
                return timing == null ? "" : CanBitTimingCalculator.Describe(timing);
            }
            catch (Exception ex)
            {
                return $"配置无效：{ex.Message}";
            }
        }
    }

    private void NotifyTimingPreviewChanged() => OnPropertyChanged(nameof(TimingPreview));

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
