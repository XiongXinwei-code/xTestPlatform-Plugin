using System.ComponentModel;
using System.Runtime.CompilerServices;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.ViewModels;

/// <summary>Ethernet 编辑器 ViewModel 通用基类，提供 UI 线程即时保存与 Load 能力。</summary>
public abstract class EthernetViewModelBase<TSetting> : INotifyPropertyChanged where TSetting : class, new()
{
    private readonly PluginCommon.EditorSaveScheduler _saver = new();
    public Action<string, Action>? ExecuteCommand { get => _saver.ExecuteCommand; set => _saver.ExecuteCommand = value; }
    public void FlushPendingSave() => _saver.Flush();
    private bool _suppressSave;
    private bool _active = true;
    public void Deactivate() { FlushPendingSave(); _active = false; }
    public void Activate() { if (_active) return; _active = true; Load(); }
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    protected TSetting? Setting;

    public void AttachSerializer(IStepSettingSerializer s) { _saver.Flush(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _saver.Flush(); _step = step; _active = true; Load(); }

    private void Load()
    {
        _saver.Flush();
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
        if (!_active || _suppressSave || _step == null || Setting == null || _serializer == null) return;
        _saver.Schedule(_step, _serializer, Setting);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
