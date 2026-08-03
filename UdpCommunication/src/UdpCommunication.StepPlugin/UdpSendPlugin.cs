using UdpCommunication.StepPlugin.Executors;
using UdpCommunication.StepPlugin.Display;
using UdpCommunication.StepPlugin.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication.StepPlugin;

public sealed class UdpSendPlugin : StepPluginBase<UdpSendSetting>
{
    public override string StepTypeId => "Network.UDP_Send";
    public override string DisplayName => "UDP_Send";
    public override string Category => "Network";
    public override string IconPath => "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";
    public override string Description => "向目标 UDP 地址发送一帧报文。配置字段：RemoteAddress、RemotePort、LocalAddress、LocalPort、RequestFormat（Utf8Text 或 Hexadecimal）和 RequestData。";
    public override IStepExecutor CreateExecutor() => new UdpSendExecutor(CreateSerializer());
    public override string GenerateDescription(byte[] setting) { var s = DeserializeSetting(setting); return $"UDP 发送 → {s.RemoteAddress}:{s.RemotePort} [{s.RequestFormat}] {UdpDescriptionFormatter.Preview(s.RequestData)}"; }
}
