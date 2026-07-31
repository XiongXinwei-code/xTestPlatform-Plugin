using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.Protocol;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.StepPlugin.UI.ViewModels;

public sealed class UdpEditorViewModel : INotifyPropertyChanged
{
    private readonly IStepSettingSerializer _serializer; private readonly Func<byte[], string> _generateDescription; private readonly bool _receive; private Step? _step; private UdpSendSetting? _setting; private bool _loading;
    public UdpEditorViewModel(IStepSettingSerializer serializer, Func<byte[], string> generateDescription, bool receive) { _serializer = serializer; _generateDescription = generateDescription; _receive = receive; }
    public Array PacketFormats => Enum.GetValues<UdpPacketFormat>(); public Array ReplyMatchModes => Enum.GetValues<UdpReplyMatchMode>(); public Visibility ReceiveVisibility => _receive ? Visibility.Visible : Visibility.Collapsed;
    public void AttachStep(Step step) { _loading = true; try { _step = step; _setting = step.StepSetting.Setting is { Length: > 0 } data ? (UdpSendSetting)_serializer.Deserialize(data, step.StepSetting.SettingVersion) : (UdpSendSetting)_serializer.CreateDefault(); OnPropertyChanged(string.Empty); } finally { _loading = false; } }
    private UdpSendSetting Setting => _setting ?? throw new InvalidOperationException("编辑器未绑定步骤"); private UdpSendAndReceiveSetting Receive => (UdpSendAndReceiveSetting)Setting;
    public string RemoteAddress { get => Setting.RemoteAddress; set { Setting.RemoteAddress=value; Save(); } } public int RemotePort { get => Setting.RemotePort; set { Setting.RemotePort=value; Save(); } } public string LocalAddress { get => Setting.LocalAddress; set { Setting.LocalAddress=value; Save(); } } public int LocalPort { get => Setting.LocalPort; set { Setting.LocalPort=value; Save(); } } public string RequestData { get => Setting.RequestData; set { Setting.RequestData=value; Save(); } } public UdpPacketFormat RequestFormat { get => Setting.RequestFormat; set { Setting.RequestFormat=value; Save(); } }
    public int ReceiveTimeoutMs { get => Receive.ReceiveTimeoutMs; set { Receive.ReceiveTimeoutMs=value; Save(); } } public UdpPacketFormat ReplyFormat { get => Receive.ReplyFormat; set { Receive.ReplyFormat=value; Save(); } } public string ExpectedReply { get => Receive.ExpectedReply; set { Receive.ExpectedReply=value; Save(); } } public UdpReplyMatchMode MatchMode { get => Receive.MatchMode; set { Receive.MatchMode=value; Save(); } } public string ResponseVariable { get => Receive.ResponseVariable; set { Receive.ResponseVariable=value; Save(); } }
    private void Save() { if (!_loading && _step is not null) { var data = _serializer.Serialize(Setting); _step.StepSetting.Setting = data; _step.PropertiesSetting.General.StepDescription = _generateDescription(data); } } public event PropertyChangedEventHandler? PropertyChanged; private void OnPropertyChanged([CallerMemberName] string? n=null) => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(n));
}
