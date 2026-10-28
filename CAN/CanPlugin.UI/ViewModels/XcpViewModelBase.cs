using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.XCP;
using CAN.XCP.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

/// <summary>XCP 编辑器 ViewModel 基类，包含公共 XCP 连接字段和防抖保存</summary>
public abstract class XcpViewModelBase<TSetting> : INotifyPropertyChanged where TSetting : XcpCommonSetting, new()
{
    private bool _suppressSave;
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public void Flush() => _save.Flush();
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    protected TSetting? Setting;

    public void AttachSerializer(IStepSettingSerializer s) { Flush(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { Flush(); _step = step; Load(); }

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
        _save.Schedule(_step, _serializer, Setting);
    }

    // ── 公共 XCP 字段 ────────────────────────────────────────────────
    public string ConnectionName
    {
        get => Setting?.ConnectionName ?? string.Empty;
        set { if (Setting == null || Setting.ConnectionName == value) return; Setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); }
    }
    public string TxId
    {
        get => Setting?.TxId ?? string.Empty;
        set { if (Setting == null || Setting.TxId == value) return; Setting.TxId = value; OnPropertyChanged(); QueueSave(); }
    }
    public string RxId
    {
        get => Setting?.RxId ?? string.Empty;
        set { if (Setting == null || Setting.RxId == value) return; Setting.RxId = value; OnPropertyChanged(); QueueSave(); }
    }
    public int TimeoutMs
    {
        get => Setting?.TimeoutMs ?? 1000;
        set { if (Setting == null || Setting.TimeoutMs == value) return; Setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
