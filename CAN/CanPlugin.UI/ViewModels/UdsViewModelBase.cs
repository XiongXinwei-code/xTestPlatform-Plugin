using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

/// <summary>UDS 编辑器 ViewModel 基类，包含公共 UDS 连接字段</summary>
public abstract class UdsViewModelBase<TSetting> : INotifyPropertyChanged where TSetting : UdsCommonSetting, new()
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    protected TSetting? Setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            Setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (TSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (TSetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    protected void QueueSave()
    {
        if (_suppressSave || _step == null || Setting == null || _serializer == null) return;
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(Setting); } catch (TaskCanceledException) { } });
    }

    // ── 公共 UDS 字段 ──────────────────────────────────────────
    public string ConnectionName { get => Setting?.ConnectionName ?? ""; set { if (Setting == null || Setting.ConnectionName == value) return; Setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public string TxId { get => Setting?.TxId ?? ""; set { if (Setting == null || Setting.TxId == value) return; Setting.TxId = value; OnPropertyChanged(); QueueSave(); } }
    public string RxId { get => Setting?.RxId ?? ""; set { if (Setting == null || Setting.RxId == value) return; Setting.RxId = value; OnPropertyChanged(); QueueSave(); } }
    public int FrameType { get => (int)(Setting?.FrameType ?? CAN.Models.CanFrameType.Standard); set { if (Setting == null) return; Setting.FrameType = (CAN.Models.CanFrameType)value; OnPropertyChanged(); QueueSave(); } }
    public bool UseFdFrame { get => Setting?.UseFdFrame ?? false; set { if (Setting == null || Setting.UseFdFrame == value) return; Setting.UseFdFrame = value; OnPropertyChanged(); QueueSave(); } }
    public int ResponseTimeoutMs { get => Setting?.ResponseTimeoutMs ?? 5000; set { if (Setting == null || Setting.ResponseTimeoutMs == value) return; Setting.ResponseTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public bool EnableLog { get => Setting?.EnableLog ?? true; set { if (Setting == null || Setting.EnableLog == value) return; Setting.EnableLog = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
