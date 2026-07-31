using UdpCommunication.StepPlugin.Executors;
using UdpCommunication.StepPlugin.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication.StepPlugin;

public sealed class UdpSendAndReceivePlugin : StepPluginBase<UdpSendAndReceiveSetting>
{
    public override string StepTypeId => "Network.UDP_SendAndReceive";
    public override string DisplayName => "UDP_SendAndReceive";
    public override string Category => "Network";
    public override string IconPath => string.Empty;
    public override string Description => "发送 UDP 报文并接收一帧回复；可按完全相等或包含字段校验回复。";
    public override IStepExecutor CreateExecutor() => new UdpSendAndReceiveExecutor(CreateSerializer());
    public override string GenerateDescription(byte[] setting) { var s = DeserializeSetting(setting); return $"UDP 收发 → {s.RemoteAddress}:{s.RemotePort}"; }
}
