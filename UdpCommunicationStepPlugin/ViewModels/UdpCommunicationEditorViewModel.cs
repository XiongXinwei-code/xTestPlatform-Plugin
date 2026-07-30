using System.ComponentModel;
using System.Runtime.CompilerServices;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Plugins.Contracts;
using UdpCommunicationStepPlugin.Setting;

namespace UdpCommunicationStepPlugin.ViewModels;

public sealed class UdpCommunicationEditorViewModel : INotifyPropertyChanged
{
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private UdpCommunicationSetting _setting = new();

    public UdpCommunicationSetting Setting
    {
        get => _setting;
        private set { _setting = value; OnPropertyChanged(); }
    }

    public void AttachSerializer(IStepSettingSerializer serializer) { _serializer = serializer; Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    public void Save()
    {
        if (_step is null || _serializer is null) return;
        _step.StepSetting.Setting = _serializer.Serialize(Setting);
        OnPropertyChanged(nameof(Setting));
    }

    private void Load()
    {
        if (_step is null || _serializer is null) return;
        Setting = _step.StepSetting.Setting is { Length: > 0 } bytes
            ? (UdpCommunicationSetting)_serializer.Deserialize(bytes, _step.StepSetting.SettingVersion)
            : (UdpCommunicationSetting)_serializer.CreateDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
