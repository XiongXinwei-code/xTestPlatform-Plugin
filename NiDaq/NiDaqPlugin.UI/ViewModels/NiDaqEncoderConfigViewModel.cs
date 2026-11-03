using System.ComponentModel;
using System.Runtime.CompilerServices;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.ViewModels;

public class NiDaqEncoderConfigViewModel : INotifyPropertyChanged
{
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private NiDaqEncoderConfigSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { FlushPendingSave(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { FlushPendingSave(); _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (NiDaqEncoderConfigSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (NiDaqEncoderConfigSetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private readonly System.Windows.Threading.Dispatcher _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
    private readonly PluginCommon.EditorSaveScheduler _saves = new();
    public Action<string, Action>? ExecuteCommand { get => _saves.ExecuteCommand; set => _saves.ExecuteCommand = value; }
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
    public string CounterChannel { get => _setting?.CounterChannel ?? ""; set { if (_setting == null || _setting.CounterChannel == value) return; _setting.CounterChannel = value; OnPropertyChanged(); QueueSave(); } }
    public EncoderDecodingType DecodingType { get => _setting?.DecodingType ?? EncoderDecodingType.X4; set { if (_setting == null || _setting.DecodingType == value) return; _setting.DecodingType = value; OnPropertyChanged(); QueueSave(); } }
    public int PulsesPerRevolution { get => _setting?.PulsesPerRevolution ?? 1024; set { if (_setting == null || _setting.PulsesPerRevolution == value) return; _setting.PulsesPerRevolution = value; OnPropertyChanged(); QueueSave(); } }
    public bool ZIndexEnable { get => _setting?.ZIndexEnable ?? false; set { if (_setting == null || _setting.ZIndexEnable == value) return; _setting.ZIndexEnable = value; OnPropertyChanged(); QueueSave(); } }
    public string AInputTerminal { get => _setting?.AInputTerminal ?? ""; set { if (_setting == null || _setting.AInputTerminal == value) return; _setting.AInputTerminal = value; OnPropertyChanged(); QueueSave(); } }
    public string BInputTerminal { get => _setting?.BInputTerminal ?? ""; set { if (_setting == null || _setting.BInputTerminal == value) return; _setting.BInputTerminal = value; OnPropertyChanged(); QueueSave(); } }
    public string ZInputTerminal { get => _setting?.ZInputTerminal ?? ""; set { if (_setting == null || _setting.ZInputTerminal == value) return; _setting.ZInputTerminal = value; OnPropertyChanged(); QueueSave(); } }
    public double InitialPositionTicks { get => _setting?.InitialPositionTicks ?? 0; set { if (_setting == null || _setting.InitialPositionTicks == value) return; _setting.InitialPositionTicks = value; OnPropertyChanged(); QueueSave(); } }
    public double ZIndexValueTicks { get => _setting?.ZIndexValueTicks ?? 0; set { if (_setting == null || _setting.ZIndexValueTicks == value) return; _setting.ZIndexValueTicks = value; OnPropertyChanged(); QueueSave(); } }
    public EncoderZPhase ZIndexPhase { get => _setting?.ZIndexPhase ?? EncoderZPhase.AHighBHigh; set { if (_setting == null || _setting.ZIndexPhase == value) return; _setting.ZIndexPhase = value; OnPropertyChanged(); QueueSave(); } }
    public Array ZIndexPhaseValues => Enum.GetValues<EncoderZPhase>();
    public double DistancePerPulse { get => _setting?.DistancePerPulse ?? 0.3515625; set { if (_setting == null || _setting.DistancePerPulse == value) return; _setting.DistancePerPulse = value; OnPropertyChanged(); QueueSave(); } }
    public EncoderUnit Unit { get => _setting?.Unit ?? EncoderUnit.Degrees; set { if (_setting == null || _setting.Unit == value) return; _setting.Unit = value; OnPropertyChanged(); QueueSave(); } }

    public int TimeoutMs { get => _setting?.TimeoutMs ?? 10000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
