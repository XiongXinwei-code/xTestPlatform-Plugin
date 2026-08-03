using UdpCommunication.StepPlugin.Executors;
using UdpCommunication.StepPlugin.Display;
using UdpCommunication.StepPlugin.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication.StepPlugin;

public sealed class UdpSendAndReceivePlugin : StepPluginBase<UdpSendAndReceiveSetting>
{
    public override string StepTypeId => "Network.UDP_SendAndReceive";
    public override string DisplayName => "UDP_SendAndReceive";
    public override string Category => "Network";
    public override string IconPath => "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";
    public override string Description => "向目标 UDP 地址发送一帧报文并接收回复。配置字段包含发送/接收格式、超时、期望回复、完全相等或包含匹配模式以及可选回复变量；收到的回复必须满足已配置的期望才会通过。";
    public override IStepExecutor CreateExecutor() => new UdpSendAndReceiveExecutor(CreateSerializer());
    public override string GenerateDescription(byte[] setting) { var s = DeserializeSetting(setting); return $"UDP 收发 → {s.RemoteAddress}:{s.RemotePort} [{s.RequestFormat}] {UdpDescriptionFormatter.Preview(s.RequestData)}，期望 {s.MatchMode}: {UdpDescriptionFormatter.Preview(s.ExpectedReply)}"; }
}
