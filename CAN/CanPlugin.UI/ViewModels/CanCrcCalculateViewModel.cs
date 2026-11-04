using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

public sealed class CanCrcCalculateViewModel : INotifyPropertyChanged
{
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public Action<string, Action>? ExecuteCommand { get => _save.ExecuteCommand; set => _save.ExecuteCommand = value; }
    private IStepSettingSerializer? _serializer;
    private Step? _step;
    private CanCrcCalculateSetting? _setting;
    private bool _active;

    public void Flush() => _save.Flush();

    public void Attach(Step step)
    {
        Flush();
        DetachProtection();
        _step = step;
        _serializer = new CanCrcCalculatePlugin().CreateSerializer();
        _setting = step.StepSetting.Setting is { Length: > 0 } data
            ? (CanCrcCalculateSetting)_serializer.Deserialize(data, step.StepSetting.SettingVersion)
            : (CanCrcCalculateSetting)_serializer.CreateDefault();
        _setting.Protection ??= new CanCrcCalculateSetting().Protection;
        _active = true;
        _setting.Protection.PropertyChanged += ProtectionChanged;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public void Activate()
    {
        if (_active) return;
        _active = true;
        if (_setting != null) _setting.Protection.PropertyChanged += ProtectionChanged;
    }

    public void Deactivate()
    {
        Flush();
        DetachProtection();
        _active = false;
    }

    private void DetachProtection()
    {
        if (_setting != null) _setting.Protection.PropertyChanged -= ProtectionChanged;
    }

    private void ProtectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _setting?.Protection)) Changed(nameof(Protection));
    }

    private void Changed([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (_active && _step != null && _setting != null && _serializer != null)
            _save.Schedule(_step, _serializer, _setting);
    }

    public string Data { get => _setting?.Data ?? ""; set { if (_active && _setting != null && _setting.Data != value) { _setting.Data = value; Changed(); } } }
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 5000; set { if (_active && _setting != null && _setting.TimeoutMs != value) { _setting.TimeoutMs = value; Changed(); } } }
    public string ResultVariable { get => _setting?.ResultVariable ?? ""; set { if (_active && _setting != null && _setting.ResultVariable != value) { _setting.ResultVariable = value; Changed(); } } }
    public CanPayloadProtectionSetting? Protection => _setting?.Protection;
    public event PropertyChangedEventHandler? PropertyChanged;
}
